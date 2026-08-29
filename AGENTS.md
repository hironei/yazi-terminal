# Yazi Terminal repository instructions

## Project scope

- This repository contains the Windows WPF host for Yazi, publicly named
  **Yazi Terminal**.
- The supported product target is Windows x64 on .NET 10.
- `EasyWindowsTerminalControl` is the selected terminal backend.
- Preserve the legacy `YaziDesktopHost` source namespace and
  `yazi-desktop-host` bridge environment/protocol identifiers unless a change
  explicitly includes a compatibility migration.
- Detailed requirements, designs, and investigation notes are historical
  records under `docs/history/`; keep user-facing guidance in `README.md` and
  `docs/user-manual.md`.

## Build and test

Run these commands from the repository root after dependency changes or code
changes:

```powershell
dotnet restore YaziDesktopHost.slnx
dotnet build YaziDesktopHost.slnx --no-restore
dotnet run --project tests/YaziDesktopHost.Tests/YaziDesktopHost.Tests.csproj --no-build --no-restore
```

The test project is an executable test suite; report each failed test rather
than treating a nonzero exit as a successful run.

For a release candidate, publish only `win-x64` and include the MIT `LICENSE`,
README, user manual, and the `yazi-desktop-host.yazi` directory.

## Documentation and release

- Keep README and the user manual concise and user-facing.
- Keep release artifacts, `README.md`, and `docs/user-manual.md` in English.
  Japanese is allowed in development records and Issue discussions.
- Record compatibility evidence and implementation history under
  `docs/history/`.
- Before a release, run a Release `win-x64` publish, verify the output and
  native assets, and document any unverified GUI, IME, Shell, or packaging
  behavior.
- For `CI.Microsoft.Terminal.Wpf`, keep the provenance record version-pinned:
  retain the upstream Terminal commit, official WPF pack-pipeline reference,
  assembly metadata/rebuild findings, native asset hash comparison, and the
  full upstream `LICENSE` and `NOTICE.md` snapshots. The managed assembly has
  no package PDB/SourceLink; an independently rebuilt managed DLL need not be
  byte-identical, so do not claim byte identity from matching metadata alone.
- A release ZIP must be validated by `eng/Test-ReleasePackage.ps1`, including
  the manifest's required notice-file SHA-256 values. If an existing release
  asset is remediated, first preserve the old asset's executable/native entry
  hashes, then replace the same-named asset and verify the public download URL;
  update the release evidence JSON and notes to state whether code bytes were
  preserved. Do not leave both the old and remediated ZIP under different names.
- Keep the redistribution manifest and `ThirdPartyRedistribution.props` in
  sync. A `verified` release requires every resolved package to be `verified`,
  complete notice files to be shipped, and the release ZIP hash check to pass.
- Whenever the pinned Yazi version changes, and before every release, refresh
  the Command Palette's bundled manager catalog from the matching official
  Yazi manual and `yazi-config/preset/keymap-default.toml`. Update
  `YaziDefaultManagerCommands.cs`, its pinned version/source metadata, the
  related tests, and user-facing documentation together. Verify the complete
  `[mgr].keymap` action set, including descriptions and multi-action order;
  do not treat the catalog as current until the release-target Yazi version
  has been checked.
- Do not commit or push unless the user explicitly requests it.
- Do not discard unrelated user changes, including the local `.codex/`
  directory.
