[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $ReleaseTag,

    [Parameter(Mandatory)]
    [string] $ArchivePath,

    [string] $OutputPath
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$pluginRelativePath = 'yazi-desktop-host.yazi/main.lua'
$pluginPath = Join-Path $repositoryRoot $pluginRelativePath

if (-not (Test-Path -LiteralPath $ArchivePath -PathType Leaf)) {
    throw "Release archive was not found: $ArchivePath"
}

if (-not (Test-Path -LiteralPath $pluginPath -PathType Leaf)) {
    throw "Bridge plugin was not found: $pluginPath"
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $repositoryRoot "docs/history/compatibility-evidence/$ReleaseTag.json"
}

function Get-ZipEntryHash {
    param([System.IO.Compression.ZipArchiveEntry] $Entry)

    $algorithm = [System.Security.Cryptography.SHA256]::Create()
    try {
        $stream = $Entry.Open()
        try {
            $hash = $algorithm.ComputeHash($stream)
        }
        finally {
            $stream.Dispose()
        }
    }
    finally {
        $algorithm.Dispose()
    }

    return [Convert]::ToHexString($hash)
}

function Get-ZipEntryFileVersion {
    param([System.IO.Compression.ZipArchiveEntry] $Entry)

    $temporaryFile = Join-Path ([System.IO.Path]::GetTempPath()) ("yazi-compat-" + [guid]::NewGuid().ToString('N') + '.exe')
    try {
        $source = $Entry.Open()
        try {
            $destination = [System.IO.File]::Create($temporaryFile)
            try {
                $source.CopyTo($destination)
            }
            finally {
                $destination.Dispose()
            }
        }
        finally {
            $source.Dispose()
        }

        $version = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($temporaryFile)
        return [ordered]@{
            FileVersion = $version.FileVersion
            ProductVersion = $version.ProductVersion
        }
    }
    finally {
        if (Test-Path -LiteralPath $temporaryFile) {
            Remove-Item -LiteralPath $temporaryFile -Force
        }
    }
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archiveFullPath = (Resolve-Path -LiteralPath $ArchivePath).Path
$archiveHash = (Get-FileHash -LiteralPath $archiveFullPath -Algorithm SHA256).Hash
$archive = [System.IO.Compression.ZipFile]::OpenRead($archiveFullPath)
try {
    $entries = @($archive.Entries |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_.Name) } |
        ForEach-Object { $_.FullName.Replace('\', '/') } |
        Sort-Object)
    $pluginEntry = $archive.GetEntry($pluginRelativePath)
    $executableEntry = $archive.GetEntry('YaziTerminal.exe')
    if ($null -eq $pluginEntry -or $null -eq $executableEntry) {
        throw 'The archive must contain yazi-desktop-host.yazi/main.lua and YaziTerminal.exe.'
    }

    $archivePluginHash = Get-ZipEntryHash $pluginEntry
    $fileVersion = Get-ZipEntryFileVersion $executableEntry
}
finally {
    $archive.Dispose()
}

$sourcePluginHash = (Get-FileHash -LiteralPath $pluginPath -Algorithm SHA256).Hash
if ($sourcePluginHash -ne $archivePluginHash) {
    throw "The release archive plugin does not match the current source plugin: archive=$archivePluginHash source=$sourcePluginHash"
}

$sourceCommit = (& git -C $repositoryRoot rev-parse HEAD).Trim()
$pluginBlob = (& git -C $repositoryRoot rev-parse "HEAD:$pluginRelativePath").Trim()
if ($LASTEXITCODE -ne 0) {
    throw 'Unable to resolve the current plugin Git blob.'
}

$evidence = [ordered]@{
    SchemaVersion = 1
    Release = [ordered]@{
        Tag = $ReleaseTag
        ArchiveFileName = [System.IO.Path]::GetFileName($archiveFullPath)
        ArchiveSha256 = $archiveHash
        EntryCount = $entries.Count
        Entries = $entries
        Executable = [ordered]@{
            Path = 'YaziTerminal.exe'
            FileVersion = $fileVersion.FileVersion
            ProductVersion = $fileVersion.ProductVersion
        }
    }
    Source = [ordered]@{
        Commit = $sourceCommit
        PluginPath = $pluginRelativePath
        PluginGitBlob = $pluginBlob
        PluginSha256 = $sourcePluginHash
    }
    Verification = [ordered]@{
        ArtifactInspection = 'observed'
        PluginSourceInspection = 'observed'
        HostProtocolTests = 'record separately from this manifest'
        LiveYaziPluginFixture = 'not-run'
    }
}

$outputDirectory = Split-Path -Parent $OutputPath
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$json = ($evidence | ConvertTo-Json -Depth 8).Replace("`r`n", "`n")
[System.IO.File]::WriteAllText($OutputPath, $json + "`n", [System.Text.UTF8Encoding]::new($false))

[pscustomobject]@{
    EvidencePath = (Resolve-Path -LiteralPath $OutputPath).Path
    ReleaseTag = $ReleaseTag
    SourceCommit = $sourceCommit
    PluginGitBlob = $pluginBlob
    ArchiveSha256 = $archiveHash
    EntryCount = $entries.Count
}
