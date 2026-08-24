# Issue #21 third-party redistribution design

## Decision

Use a checked-in, version-pinned redistribution manifest as the single
engineering source for release status and archive notice paths. The project
imports a `blocked` property from the same release-gate directory, so an
ordinary `dotnet publish` fails before producing a distributable output.

## Components

```text
ThirdPartyRedistribution.props ──> YaziDesktopHost.csproj PrepareForPublish gate
redistribution-manifest.json ──> Test-ThirdPartyRedistribution.ps1
                                       └──> Publish-Release.ps1
                                              └──> Test-ReleasePackage.ps1
```

`Test-ThirdPartyRedistribution.ps1` validates the manifest shape and, when
`-RequireReleaseReady` is selected, fails unless its state is `verified`.
`Publish-Release.ps1` requires that state before restore/publish/archive work.
`Test-ReleasePackage.ps1` can inspect a directory or ZIP and confirms that the
notice files declared in the manifest were included; it also applies the same
release-ready check unless explicitly used for retrospective blocked-state
inspection.

## Evidence and state model

`declared` means a package's own nuspec declares an SPDX MIT expression and
the project has copied the corresponding copyright/license notice. It is not a
legal audit beyond that package metadata. `blocked` means the package lacks a
license expression, license URL/file, repository URL, and bundled notice, or
otherwise lacks a confirmed redistribution route. `verified` may be set only
after an authorized maintainer records the evidence, notices, and version
specific asset mapping in a reviewed change.

The 1.25.260303002 CI repackage is `blocked`. Microsoft Terminal's upstream
MIT LICENSE and NOTICE are useful provenance leads, but do not establish the
repackage's license chain. No notice is invented for that package.

## Packaging

The project copies `THIRD-PARTY-NOTICES.md` and the two evidenced MIT notices
to publish output. A future `verified` manifest revision must add the exact
CI-repackage LICENSE/NOTICE files to `noticeFiles`; the archive validator will
then require them automatically. The script generates the archive only after
the status check and copies the project LICENSE, README, user manual, and
bridge plugin before validating the final ZIP.

## Error handling and security

Scripts accept literal paths, reject ambiguous directory/archive input, and
fail closed on malformed manifests, duplicate package identifiers, absent
required paths, or a non-verified release state. They do not download,
execute, or modify third-party packages. A command-line property can always be
used to bypass an MSBuild policy, so release procedure and archive validation
remain mandatory controls; bypassing them is not an authorized release path.

## Design review (round 1)

No BLOCKER or MAJOR finding. The fail-closed default meets the safety boundary
without changing the terminal backend. Residual MINOR: the publish target is a
policy guard, not a technical defense against a deliberately supplied global
MSBuild property; final release review must run the scripts on the artifact.
