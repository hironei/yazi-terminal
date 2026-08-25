[CmdletBinding()]
param(
    [string] $ManifestPath = (Join-Path $PSScriptRoot 'third-party/redistribution-manifest.json'),
    [switch] $RequireReleaseReady
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $ManifestPath -PathType Leaf)) {
    throw "Redistribution manifest was not found: $ManifestPath"
}

try {
    $manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
}
catch {
    throw "Redistribution manifest is not valid JSON: $ManifestPath. $($_.Exception.Message)"
}

$problems = [System.Collections.Generic.List[string]]::new()
if ($manifest.schemaVersion -ne 1) {
    $problems.Add("Unsupported redistribution manifest schema version '$($manifest.schemaVersion)'.")
}

if ([string]::IsNullOrWhiteSpace($manifest.releaseState) -or
    $manifest.releaseState -notin @('blocked', 'verified')) {
    $problems.Add("releaseState must be 'blocked' or 'verified'.")
}

if ($null -eq $manifest.packages -or $manifest.packages.Count -eq 0) {
    $problems.Add('At least one resolved third-party package is required.')
}

$seenPackages = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$requiredNoticeFiles = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($package in @($manifest.packages)) {
    if ([string]::IsNullOrWhiteSpace($package.id) -or [string]::IsNullOrWhiteSpace($package.version)) {
        $problems.Add('Each package requires a non-empty id and version.')
        continue
    }

    $identity = "$($package.id)/$($package.version)"
    if (-not $seenPackages.Add($identity)) {
        $problems.Add("Package '$identity' occurs more than once.")
    }

    if ($package.status -notin @('declared', 'blocked', 'verified')) {
        $problems.Add("Package '$identity' has unsupported status '$($package.status)'.")
    }

    if ([string]::IsNullOrWhiteSpace($package.evidence)) {
        $problems.Add("Package '$identity' has no evidence description.")
    }

    if ($null -eq $package.assets -or $package.assets.Count -eq 0) {
        $problems.Add("Package '$identity' has no published asset mapping.")
    }

    foreach ($noticeFile in @($package.noticeFiles)) {
        if ([string]::IsNullOrWhiteSpace($noticeFile)) {
            $problems.Add("Package '$identity' contains an empty notice path.")
            continue
        }

        [void] $requiredNoticeFiles.Add([string] $noticeFile)
    }

    if ($package.status -in @('declared', 'verified') -and $package.noticeFiles.Count -eq 0) {
        $problems.Add("Package '$identity' is $($package.status) but has no notice file.")
    }
}

foreach ($noticeFile in $requiredNoticeFiles) {
    $repositoryRoot = Split-Path -Parent $PSScriptRoot
    $noticePath = Join-Path $repositoryRoot $noticeFile
    if (-not (Test-Path -LiteralPath $noticePath -PathType Leaf)) {
        $problems.Add("Declared notice file is missing: $noticeFile")
    }
}

$nonVerifiedPackages = @($manifest.packages | Where-Object status -ne 'verified')
if ($manifest.releaseState -eq 'verified' -and $nonVerifiedPackages.Count -gt 0) {
    $problems.Add("releaseState is verified while these packages are not verified: $($nonVerifiedPackages.id -join ', ').")
}

if ($null -eq $manifest.requiredArchiveFiles -or $manifest.requiredArchiveFiles.Count -eq 0) {
    $problems.Add('requiredArchiveFiles must identify the release package contents.')
}

foreach ($requiredFile in @($manifest.requiredArchiveFiles)) {
    if ([string]::IsNullOrWhiteSpace($requiredFile)) {
        $problems.Add('requiredArchiveFiles contains an empty path.')
    }
}

$requiredArchiveFileSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($requiredFile in @($manifest.requiredArchiveFiles)) {
    if (-not [string]::IsNullOrWhiteSpace($requiredFile)) {
        [void] $requiredArchiveFileSet.Add([string] $requiredFile)
    }
}

foreach ($fileHash in @($manifest.requiredFileHashes)) {
    $hashPath = [string] $fileHash.path
    $sha256 = [string] $fileHash.sha256
    if ([string]::IsNullOrWhiteSpace($hashPath) -or
        -not $requiredArchiveFileSet.Contains($hashPath)) {
        $problems.Add("Required file hash path '$hashPath' is not listed in requiredArchiveFiles.")
    }

    if ($sha256 -notmatch '^[0-9A-Fa-f]{64}$') {
        $problems.Add("Required file hash for '$hashPath' is not a SHA-256 value.")
    }
}

if ($null -eq $manifest.requiredNoticeFiles -or $manifest.requiredNoticeFiles.Count -eq 0) {
    $problems.Add('requiredNoticeFiles must identify the notice content to validate.')
}

foreach ($notice in @($manifest.requiredNoticeFiles)) {
    $noticeFile = [string] $notice.path
    if ([string]::IsNullOrWhiteSpace($noticeFile)) {
        $problems.Add('requiredNoticeFiles contains an empty path.')
        continue
    }

    if (-not $requiredArchiveFileSet.Contains($noticeFile)) {
        $problems.Add("Required notice '$noticeFile' is not listed in requiredArchiveFiles.")
    }

    $noticePath = Join-Path (Split-Path -Parent $PSScriptRoot) $noticeFile
    if (-not (Test-Path -LiteralPath $noticePath -PathType Leaf)) {
        $problems.Add("Required notice file is missing: $noticeFile")
        continue
    }

    $content = Get-Content -LiteralPath $noticePath -Raw
    if ($null -eq $notice.requiredText -or $notice.requiredText.Count -eq 0) {
        $problems.Add("Required notice '$noticeFile' has no required text.")
        continue
    }

    foreach ($requiredText in @($notice.requiredText)) {
        if ([string]::IsNullOrWhiteSpace($requiredText) -or -not $content.Contains([string] $requiredText)) {
            $problems.Add("Required notice '$noticeFile' does not contain its declared text.")
        }
    }
}

if ($problems.Count -gt 0) {
    throw "Third-party redistribution manifest validation failed:`n - $($problems -join "`n - ")"
}

if ($RequireReleaseReady -and $manifest.releaseState -ne 'verified') {
    throw "Third-party redistribution release gate is $($manifest.releaseState): $($manifest.releaseBlocker)"
}

[pscustomobject]@{
    ReleaseState = $manifest.releaseState
    PackageCount = $manifest.packages.Count
    BlockedPackages = @($manifest.packages | Where-Object status -eq 'blocked' | ForEach-Object id)
    ManifestPath = (Resolve-Path -LiteralPath $ManifestPath).Path
}
