[CmdletBinding()]
param(
    [string] $OutputDirectory = (Join-Path $PSScriptRoot '../artifacts/release/win-x64'),
    [string] $Dotnet = 'dotnet'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repositoryRoot 'src/YaziDesktopHost/YaziDesktopHost.csproj'
$manifestPath = Join-Path $PSScriptRoot 'third-party/redistribution-manifest.json'

& (Join-Path $PSScriptRoot 'Test-ThirdPartyRedistribution.ps1') -ManifestPath $manifestPath -RequireReleaseReady

$outputPath = [System.IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $outputPath) {
    throw "Release output directory already exists: $outputPath"
}

& $Dotnet restore (Join-Path $repositoryRoot 'YaziDesktopHost.slnx')
if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }

& $Dotnet publish $projectPath --no-restore -c Release -r win-x64 --self-contained false -p:PublishReadyToRun=false -o $outputPath
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

Copy-Item -LiteralPath (Join-Path $repositoryRoot 'LICENSE') -Destination $outputPath
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'README.md') -Destination $outputPath
New-Item -ItemType Directory -Path (Join-Path $outputPath 'docs') | Out-Null
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'docs/user-manual.md') -Destination (Join-Path $outputPath 'docs/user-manual.md')
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'yazi-desktop-host.yazi') -Destination (Join-Path $outputPath 'yazi-desktop-host.yazi') -Recurse

$archivePath = "$outputPath.zip"
$archiveInputs = @(Get-ChildItem -LiteralPath $outputPath -Force | ForEach-Object FullName)
if ($archiveInputs.Count -eq 0) {
    throw "Release output directory is empty: $outputPath"
}

Compress-Archive -LiteralPath $archiveInputs -DestinationPath $archivePath -CompressionLevel Optimal
& (Join-Path $PSScriptRoot 'Test-ReleasePackage.ps1') -ArchivePath $archivePath -ManifestPath $manifestPath

Write-Output $archivePath
