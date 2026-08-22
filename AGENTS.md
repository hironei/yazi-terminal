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
- Record compatibility evidence and implementation history under
  `docs/history/`.
- Before a release, run a Release `win-x64` publish, verify the output and
  native assets, and document any unverified GUI, IME, Shell, or packaging
  behavior.
- Do not commit or push unless the user explicitly requests it.
- Do not discard unrelated user changes, including the local `.codex/`
  directory.
