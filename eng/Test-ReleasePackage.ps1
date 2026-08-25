[CmdletBinding(DefaultParameterSetName = 'Directory')]
param(
    [Parameter(Mandatory, ParameterSetName = 'Directory')]
    [string] $PublishDirectory,

    [Parameter(Mandatory, ParameterSetName = 'Archive')]
    [string] $ArchivePath,

    [string] $ManifestPath = (Join-Path $PSScriptRoot 'third-party/redistribution-manifest.json'),
    [switch] $AllowBlocked
)

$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Test-ThirdPartyRedistribution.ps1') -ManifestPath $ManifestPath

$manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
$archiveEntries = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$noticeTextByPath = @{}
foreach ($notice in @($manifest.requiredNoticeFiles)) {
    $noticeTextByPath[[string] $notice.path] = @($notice.requiredText | ForEach-Object { [string] $_ })
}
$expectedHashByPath = @{}
foreach ($fileHash in @($manifest.requiredFileHashes)) {
    if (-not [string]::IsNullOrWhiteSpace([string] $fileHash.path)) {
        $expectedHashByPath[[string] $fileHash.path] = ([string] $fileHash.sha256).ToUpperInvariant()
    }
}
$actualHashByPath = @{}
$noticeContentByPath = @{}

function Get-StreamSha256 {
    param([System.IO.Stream] $Stream)

    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        return ([BitConverter]::ToString($sha256.ComputeHash($Stream)) -replace '-', '')
    }
    finally {
        $sha256.Dispose()
    }
}

if ($PSCmdlet.ParameterSetName -eq 'Directory') {
    if (-not (Test-Path -LiteralPath $PublishDirectory -PathType Container)) {
        throw "Publish directory was not found: $PublishDirectory"
    }

    $publishRoot = (Resolve-Path -LiteralPath $PublishDirectory).Path
    Get-ChildItem -LiteralPath $publishRoot -Recurse -File | ForEach-Object {
        $relative = $_.FullName.Substring($publishRoot.Length).TrimStart([char]'\', [char]'/').Replace('\', '/')
        [void] $archiveEntries.Add($relative)
        if ($noticeTextByPath.ContainsKey($relative)) {
            $noticeContentByPath[$relative] = Get-Content -LiteralPath $_.FullName -Raw
        }
        if ($expectedHashByPath.ContainsKey($relative)) {
            $actualHashByPath[$relative] = (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash.ToUpperInvariant()
        }
    }
}
else {
    if (-not (Test-Path -LiteralPath $ArchivePath -PathType Leaf)) {
        throw "Release archive was not found: $ArchivePath"
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $ArchivePath).Path)
    try {
        foreach ($entry in $archive.Entries) {
            if (-not [string]::IsNullOrWhiteSpace($entry.Name)) {
                $relative = $entry.FullName.Replace('\', '/')
                [void] $archiveEntries.Add($relative)
                if ($noticeTextByPath.ContainsKey($relative)) {
                    $reader = [System.IO.StreamReader]::new($entry.Open(), [System.Text.Encoding]::UTF8, $true)
                    try {
                        $noticeContentByPath[$relative] = $reader.ReadToEnd()
                    }
                    finally {
                        $reader.Dispose()
                    }
                }
                if ($expectedHashByPath.ContainsKey($relative)) {
                    $stream = $entry.Open()
                    try {
                        $actualHashByPath[$relative] = Get-StreamSha256 -Stream $stream
                    }
                    finally {
                        $stream.Dispose()
                    }
                }
            }
        }
    }
    finally {
        $archive.Dispose()
    }
}

$missing = @($manifest.requiredArchiveFiles | Where-Object { -not $archiveEntries.Contains($_) })
if ($missing.Count -gt 0) {
    throw "Release package is missing required files:`n - $($missing -join "`n - ")"
}

$invalidNotices = [System.Collections.Generic.List[string]]::new()
foreach ($noticePath in $noticeTextByPath.Keys) {
    $content = $noticeContentByPath[$noticePath]
    if ($null -eq $content) {
        $invalidNotices.Add("$noticePath could not be read from the release package.")
        continue
    }

    foreach ($requiredText in $noticeTextByPath[$noticePath]) {
        if (-not $content.Contains($requiredText)) {
            $invalidNotices.Add("$noticePath is missing required text.")
        }
    }
}

if ($invalidNotices.Count -gt 0) {
    throw "Release package notice content validation failed:`n - $($invalidNotices -join "`n - ")"
}

$invalidHashes = [System.Collections.Generic.List[string]]::new()
foreach ($hashPath in $expectedHashByPath.Keys) {
    if (-not $archiveEntries.Contains($hashPath)) {
        $invalidHashes.Add("$hashPath is missing from the release package.")
        continue
    }

    $actualHash = [string] $actualHashByPath[$hashPath]
    if ([string]::IsNullOrWhiteSpace($actualHash)) {
        $invalidHashes.Add("$hashPath could not be hashed.")
        continue
    }

    if ($actualHash -ne $expectedHashByPath[$hashPath]) {
        $invalidHashes.Add("$hashPath has SHA-256 $actualHash; expected $($expectedHashByPath[$hashPath]).")
    }
}

if ($invalidHashes.Count -gt 0) {
    throw "Release package file hash validation failed:`n - $($invalidHashes -join "`n - ")"
}

if (-not $AllowBlocked) {
    & (Join-Path $PSScriptRoot 'Test-ThirdPartyRedistribution.ps1') -ManifestPath $ManifestPath -RequireReleaseReady
}

$inputPath = if ($PSCmdlet.ParameterSetName -eq 'Directory') {
    (Resolve-Path -LiteralPath $PublishDirectory).Path
}
else {
    (Resolve-Path -LiteralPath $ArchivePath).Path
}

[pscustomobject]@{
    Input = $inputPath
    RequiredFiles = $manifest.requiredArchiveFiles.Count
    ReleaseState = $manifest.releaseState
    BlockedStateAllowed = [bool] $AllowBlocked
}
