# Yazi Desktop Host

Initial Phase 1 implementation of a Windows WPF host for the ordinary Yazi
terminal file manager. `EasyWindowsTerminalControl` 1.0.38 is currently the
leading terminal candidate for evaluation, not yet the selected production
backend. It hosts the Windows Terminal renderer and ConPTY through a native
terminal control.

The reviewed scope and known blockers are documented in:

- [Phase 1 requirements](docs/requirements-yazi-windows-gui-frontend.md)
- [Phase 1 design](docs/design-yazi-windows-gui-frontend.md)
- [Terminal host candidate evaluation](docs/terminal-host-evaluation.md)
- [Phase 2 bridge requirements](docs/requirements-phase2-yazi-bridge.md)
- [Phase 2 bridge design](docs/design-phase2-yazi-bridge.md)
- [Phase 2 Yazi event investigation](docs/phase2-yazi-event-investigation.md)
- [Phase 2 plugin probe](plugins/yazi-desktop-host.yazi/README.md)

Run the host with the .NET 10 SDK from the repository root:

```powershell
dotnet run --project src/YaziDesktopHost/YaziDesktopHost.csproj
```

Set `YAZI_PATH` when `yazi.exe` is not available through `PATH`. Later Shell
integration phases must not be implemented by parsing terminal screen text.

For an opt-in terminal color diagnostic, set `YAZI_DESKTOP_HOST_VT_FIXTURE=1`
before launching. The fixture is intended for manual validation only.
