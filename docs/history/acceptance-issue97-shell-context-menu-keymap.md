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
