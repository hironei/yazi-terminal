# Yazi Desktop Host

Windows WPF host for the ordinary Yazi terminal file manager.
`EasyWindowsTerminalControl` 1.0.38 is the selected backend for the current
implementation. It hosts the Windows Terminal renderer and ConPTY through a
native terminal control; production packaging gates remain open.

The reviewed scope and known blockers are documented in:

- [Phase 1 requirements](docs/requirements-yazi-windows-gui-frontend.md)
- [Phase 1 design](docs/design-yazi-windows-gui-frontend.md)
- [Terminal host candidate evaluation](docs/terminal-host-evaluation.md)
- [Compatibility matrix and distribution gates](docs/compatibility-matrix.md)
- [Phase 2 bridge requirements](docs/requirements-phase2-yazi-bridge.md)
- [Phase 2 bridge design](docs/design-phase2-yazi-bridge.md)
- [Phase 2 Yazi event investigation](docs/phase2-yazi-event-investigation.md)
- [Phase 2 plugin probe](plugins/yazi-desktop-host.yazi/README.md)
- [Phase 3 Shell context-menu requirements](docs/requirements-phase3-shell-context-menu.md)
- [Phase 3 Shell context-menu design](docs/design-phase3-shell-context-menu.md)
- [Phase 4 drag-and-drop requirements](docs/requirements-phase4-drag-drop.md)
- [Phase 4 drag-and-drop design](docs/design-phase4-drag-drop.md)
- [Phase 5 Explorer-to-Yazi drop requirements](docs/requirements-phase5-explorer-to-yazi-drop.md)
- [Phase 5 Explorer-to-Yazi drop design](docs/design-phase5-explorer-to-yazi-drop.md)

Run the host with the .NET 10 SDK from the repository root:

```powershell
dotnet run --project src/YaziDesktopHost/YaziDesktopHost.csproj
```

Set `YAZI_PATH` when `yazi.exe` is not available through `PATH`. Later Shell
integration phases must not be implemented by parsing terminal screen text.

For an opt-in terminal color diagnostic, set `YAZI_DESKTOP_HOST_VT_FIXTURE=1`
before launching. The fixture is intended for manual validation only.
