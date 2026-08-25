# CI.Microsoft.Terminal.Wpf 1.25.260303002 provenance evidence

Captured 2026-08-25 from the local NuGet cache and public Microsoft/GitHub
endpoints. This record is about binary provenance; it does not make a legal
determination about redistribution of the CI repackage.

## Local package

- Cache path: `%USERPROFILE%\.nuget\packages\ci.microsoft.terminal.wpf\1.25.260303002\ci.microsoft.terminal.wpf.1.25.260303002.nupkg`.
- Package SHA-256: `3E639A019607432552F8BB3385B51D065FF8CB19608D4C7E17528C9481A10545`.
- The expanded nuspec content and complete extracted-file SHA-256 inventory
  are kept next to this file. The nuspec file is a source-controlled text copy
  (the original bytes, including line endings, are covered by the recorded
  package-entry hash). It identifies `CI2Nuget Repackage Team` as the
  author and has only a Git repository commit field (no repository URL,
  license expression, license file, or notice entry).
- `dotnet nuget verify` reports a NuGet.org repository signature with subject
  `CN=NuGet.org Repository by Microsoft, O=NuGet.org Repository by Microsoft,
  L=Redmond, S=Washington, C=US`; this is package repository signing and is
  not a Microsoft Terminal WPF build signature.

## Version and source correspondence

- The nuspec commit `9ae724aa5b080aafbeea2bbf88db630b182cc802` is the commit
  tagged by Microsoft Terminal release `v1.25.622.0`.
- The Microsoft release notes identify that release's binary build as
  `1.25.260303002-preview`, matching the package version's build stem.
- The official source pipeline
  `build/pipelines/templates-v2/job-build-package-wpf.yml` defines the WPF
  pack target (`Terminal\\wpf\\WpfTerminalControl:Pack`) and publishes a
  `wpf-nupkg-$(BuildConfiguration)` artifact. The source project builds
  `Microsoft.Terminal.Wpf` for `net472;net8.0-windows`.
- The package's x64 native
  `Microsoft.Terminal.Control.dll` SHA-256 is exactly equal to the same file
  in the Microsoft `Microsoft.WindowsTerminalPreview_1.25.622.0_x64.zip`:
  `37C26108DAD75C1F73784184CB34E8B14DE8AFF8F1053319E40AA47453476E5F`.
  This confirms component/version correspondence, not the provenance of the
  managed WPF DLL or the package container.

## Official artifact retrieval

- The public Microsoft GitHub release assets for `v1.25.622.0` contain the
  Windows Terminal archives and ConPTY preview package, but no
  `Microsoft.Terminal.Wpf` nupkg. The GitHub release workflow run exposes no
  downloadable WPF artifact.
- NuGet flat-container lookup returns HTTP 404 for
  `microsoft.terminal.wpf/1.25.260303002` and HTTP 200 only for the
  `ci.microsoft.terminal.wpf` package. The Azure DevOps API endpoint for the
  Microsoft `OS` project redirects anonymous callers to sign-in, so the
  pipeline artifact could not be retrieved or hash-compared from this
  environment.

## Microsoft source notices

At the same commit, the official source contains `LICENSE` (MIT) and
`NOTICE.md`. Their captured SHA-256 values are recorded in the TSV inventory:

- `LICENSE`: `5D177F23ECFEB0EA8E050B6A5A16355E1AE9A0B286436CA8F83ED08B3795BE6B`
- `NOTICE.md`: `E7FBAADEE6AB20C28B87730A510EE5F5815D8FB4BD88D1D54D282DC2A74C072`

Sources:

- https://github.com/microsoft/terminal/releases/tag/v1.25.622.0
- https://github.com/microsoft/terminal/tree/9ae724aa5b080aafbeea2bbf88db630b182cc802
- https://github.com/microsoft/terminal/blob/9ae724aa5b080aafbeea2bbf88db630b182cc802/build/pipelines/templates-v2/job-build-package-wpf.yml
- https://raw.githubusercontent.com/microsoft/terminal/9ae724aa5b080aafbeea2bbf88db630b182cc802/LICENSE
- https://raw.githubusercontent.com/microsoft/terminal/9ae724aa5b080aafbeea2bbf88db630b182cc802/NOTICE.md

## Determination

The commit and official WPF pipeline provide a **provenance lead / verified
candidate** for the source and version. The exact Microsoft-generated WPF
nupkg (or an independently reproducible managed-DLL build) was not obtainable,
and only the native x64 component hash was matched. Therefore the release
gate remains **blocked**; this evidence must not be used to mark the CI
repackage `verified` without an authorized maintainer's artifact-level review.
