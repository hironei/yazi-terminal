# Issue #97 Acceptance Record

## Automated acceptance

- `dotnet restore YaziDesktopHost.slnx`: PASS with the pinned Scoop .NET 10
  SDK. The normal sandboxed attempt was blocked by NuGet SSL/ACL restrictions;
  the approved elevated retry succeeded.
- `dotnet build YaziDesktopHost.slnx --no-restore`: PASS, 0 warnings, 0 errors.
- `dotnet run --project tests/YaziDesktopHost.Tests/YaziDesktopHost.Tests.csproj
  --no-build --no-restore`: PASS, all executable tests passed, including the
  Issue #97 protocol, plugin-source, and final-path tests.
- `dotnet format YaziDesktopHost.slnx --verify-no-changes --no-restore`: PASS.
- `lua -e "assert(loadfile('yazi-desktop-host.yazi/main.lua'))"`: PASS.
- Existing `keymap-palette-actions.lua` fixture: PASS.
- `git diff --check`: PASS.

Automated checks cover protocol, target/final-path logic, and source/plugin
contracts only.

## Manual acceptance boundary

The following require a Windows desktop with the supported Yazi/`ya` fixture
and are not claimed by headless tests:

- `context-menu` opens the selected-or-hovered Shell menu from a user keymap.
- `context-menu-cwd` opens the current-directory Shell menu from a user keymap.
- Junction/SymbolicLink paths resolve to the real OneDrive filesystem path and
  expose the provider's registered menu items.
- Existing right-click, Shift+F10, and Ctrl+Shift+F10 behavior remains usable.

These manual checks were not run in this task because no live Yazi/OneDrive
desktop fixture was available.

## Follow-up on 2026-09-24

- In an isolated Yazi 26.9.1 session, the documented
  `plugin yazi-desktop-host --args=context-menu` binding reached the plugin
  with an empty `job.args` table. No command was sent.
- With `plugin yazi-desktop-host -- context-menu`, `job.args[1]` was
  `context-menu`, and the plugin sent a `kind = "command"` frame with that
  payload after the snapshot and heartbeats to a diagnostic named pipe.
- PowerShell `Copy-Item -LiteralPath $pluginSource -Destination
  $pluginDestination -Recurse -Force` nested the source folder when the
  destination directory already existed. The user manual now copies the
  source contents into the destination for both installation and updates.
- The user confirmed that two nested plugin directories existed on the other
  PC and that correcting the active plugin copy made the keymap action work.
  This is a user-reported GUI result; Junction/SymbolicLink and OneDrive menu
  behavior were not separately confirmed in this follow-up.
