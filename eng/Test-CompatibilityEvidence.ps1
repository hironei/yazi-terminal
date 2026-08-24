[CmdletBinding()]
param(
    [string] $EvidencePath = (Join-Path $PSScriptRoot '../docs/history/compatibility-evidence/v0.1.8.json'),
    [string] $ArchivePath,
    [switch] $RequireCurrentSource,
    [switch] $RequireArchive
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot

if (-not (Test-Path -LiteralPath $EvidencePath -PathType Leaf)) {
    throw "Compatibility evidence was not found: $EvidencePath"
}

try {
    $evidence = Get-Content -LiteralPath $EvidencePath -Raw | ConvertFrom-Json
}
catch {
    throw "Compatibility evidence is not valid JSON: $EvidencePath. $($_.Exception.Message)"
}

$problems = [System.Collections.Generic.List[string]]::new()
if ($evidence.SchemaVersion -ne 1) {
    $problems.Add("Unsupported evidence schema '$($evidence.SchemaVersion)'.")
}

foreach ($property in @('Tag', 'ArchiveFileName', 'ArchiveSha256', 'EntryCount', 'Entries', 'Executable')) {
    if ($null -eq $evidence.Release.$property -or [string]::IsNullOrWhiteSpace([string] $evidence.Release.$property)) {
        $problems.Add("Release.$property is required.")
    }
}

foreach ($property in @('Commit', 'PluginPath', 'PluginGitBlob', 'PluginSha256')) {
    if ([string]::IsNullOrWhiteSpace([string] $evidence.Source.$property)) {
        $problems.Add("Source.$property is required.")
    }
}

if ($evidence.Release.Entries.Count -ne $evidence.Release.EntryCount) {
    $problems.Add('Release EntryCount does not match Entries.')
}

if ($problems.Count -gt 0) {
    throw "Compatibility evidence structure validation failed:`n - $($problems -join "`n - ")"
}

if ($RequireCurrentSource) {
    $currentCommit = (& git -C $repositoryRoot rev-parse HEAD).Trim()
    if ($currentCommit -ne $evidence.Source.Commit) {
        throw "Evidence source commit differs from HEAD: evidence=$($evidence.Source.Commit) current=$currentCommit"
    }

    $pluginPath = Join-Path $repositoryRoot $evidence.Source.PluginPath
    if (-not (Test-Path -LiteralPath $pluginPath -PathType Leaf)) {
        throw "Evidence plugin is missing from source: $($evidence.Source.PluginPath)"
    }

    $currentBlob = (& git -C $repositoryRoot rev-parse "HEAD:$($evidence.Source.PluginPath)").Trim()
    $currentHash = (Get-FileHash -LiteralPath $pluginPath -Algorithm SHA256).Hash
    if ($currentBlob -ne $evidence.Source.PluginGitBlob -or $currentHash -ne $evidence.Source.PluginSha256) {
        throw 'Evidence plugin revision/blob/hash differs from current source.'
    }

    $pluginContent = Get-Content -LiteralPath $pluginPath -Raw
    foreach ($requiredFragment in @('json_commands(get_all_commands())', 'while true do', 'sequence = 0', 'ya.sleep(retry_interval)')) {
        if (-not $pluginContent.Contains($requiredFragment)) {
            throw "Current plugin lacks required bridge/catalog/reconnect fragment: $requiredFragment"
        }
    }
}

if ($RequireArchive -and [string]::IsNullOrWhiteSpace($ArchivePath)) {
    throw 'RequireArchive needs ArchivePath.'
}

if (-not [string]::IsNullOrWhiteSpace($ArchivePath)) {
    if (-not (Test-Path -LiteralPath $ArchivePath -PathType Leaf)) {
        throw "Release archive was not found: $ArchivePath"
    }

    $actualHash = (Get-FileHash -LiteralPath $ArchivePath -Algorithm SHA256).Hash
    if ($actualHash -ne $evidence.Release.ArchiveSha256) {
        throw "Release archive hash differs: evidence=$($evidence.Release.ArchiveSha256) actual=$actualHash"
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $ArchivePath).Path)
    try {
        $actualEntries = @($archive.Entries |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_.Name) } |
            ForEach-Object { $_.FullName.Replace('\', '/') } |
            Sort-Object)
    }
    finally {
        $archive.Dispose()
    }

    $difference = Compare-Object -ReferenceObject @($evidence.Release.Entries) -DifferenceObject $actualEntries
    if ($null -ne $difference) {
        throw "Release archive entries differ from recorded evidence: $($difference | Out-String)"
    }
}

[pscustomobject]@{
    EvidencePath = (Resolve-Path -LiteralPath $EvidencePath).Path
    ReleaseTag = $evidence.Release.Tag
    CurrentSourceChecked = [bool] $RequireCurrentSource
    ArchiveChecked = -not [string]::IsNullOrWhiteSpace($ArchivePath)
    LiveYaziPluginFixture = $evidence.Verification.LiveYaziPluginFixture
}
