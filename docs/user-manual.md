# Yazi Terminal User Manual

Yazi Terminal is a Windows WPF host for the ordinary Yazi terminal file
manager. It embeds a Windows Terminal-based control, so Yazi runs in the host
window instead of opening a separate terminal window.

The public repository name is `yazi-terminal`. Internal source paths,
namespaces, and bridge identifiers still use the legacy `YaziDesktopHost` /
`yazi-desktop-host` names where changing them would break existing build or
Yazi plugin compatibility.

## At a glance

- The current terminal backend is `EasyWindowsTerminalControl` 1.0.38.
- CJK display, keyboard input, Japanese IME conversion, mouse reporting,
  resize, alternate-screen rendering, and 24-bit color passed the current
  Windows/Yazi validation.
- Explorer/Desktop drag-and-drop is supported in the validated directions;
  Yazi and Windows Shell remain responsible for file-operation semantics.
- The product target is Windows x64. The validated Yazi/`ya` fixture is 26.5.6.

## User installation

### Requirements

- Windows x64
- .NET 10 Windows Desktop runtime for the framework-dependent release archive
- `yazi.exe` and its paired `ya.exe`

The current compatibility boundary is the exact Windows, Yazi, and `ya` fixture
recorded in the archived compatibility evidence. x86 and ARM64 are not product
targets.

Download the latest Windows x64 archive from the
[GitHub Releases](https://github.com/hironei/yazi-terminal/releases), extract
it, and start `YaziTerminal.exe`.

The archive contains the executable, its native terminal dependencies, this
manual, the MIT `LICENSE`, and the optional bridge plugin.

## Plugin installation

The optional `yazi-desktop-host.yazi` bridge plugin publishes Yazi's current
directory, hovered item, and selection to Yazi Terminal. It is required for the
host's bridge-backed Shell targeting and Explorer drag-and-drop behavior.
The current validated fixture is Yazi/`ya` 26.5.6.

Use one of the following installation methods.

### Install with Yazi's package manager

Yazi's package manager command is `ya pkg add`. From PowerShell or another
shell, run:

```powershell
ya pkg add hironei/yazi-terminal:yazi-desktop-host
```

This downloads the `yazi-desktop-host.yazi` package from the repository and
records it in Yazi's `package.toml`. On another machine, install the locked
dependencies with `ya pkg install`; to update the package, use `ya pkg upgrade`.
To remove it later, run:

```powershell
ya pkg delete hironei/yazi-terminal:yazi-desktop-host
```

### Manual installation from the repository

The plugin is also included in the release archive and repository under
`plugins/yazi-desktop-host.yazi`. From the repository root, copy it into the
current user's Yazi plugin directory:

```powershell
$pluginSource = (Resolve-Path 'plugins\yazi-desktop-host.yazi').Path
$pluginDestination = Join-Path $env:APPDATA 'yazi\config\plugins\yazi-desktop-host.yazi'
New-Item -ItemType Directory -Force -Path (Split-Path $pluginDestination) | Out-Null
Copy-Item -LiteralPath $pluginSource -Destination $pluginDestination -Recurse -Force
```

Add the following line to `%APPDATA%\yazi\config\init.lua`. Merge it with an
existing file; do not replace the user's other configuration:

```lua
require("yazi-desktop-host"):setup {}
```

Restart Yazi Terminal after changing the plugin configuration. The host supplies
the pipe and instance identifiers to the Yazi child automatically.

For a manual installation, update the plugin by copying the repository
directory again. To uninstall it, remove the
`require("yazi-desktop-host"):setup {}` line from `init.lua` and delete
`%APPDATA%\yazi\config\plugins\yazi-desktop-host.yazi`.

The plugin currently uses the legacy `yazi-desktop-host` name and bridge
protocol intentionally; this preserves compatibility with the host and existing
Yazi configuration while the public product name is Yazi Terminal.

## Developer setup

The following section is for contributors building Yazi Terminal from source.
Users installing a release archive can skip it.

Install the .NET 10 SDK, then run these commands from the repository root:

```powershell
dotnet restore YaziDesktopHost.slnx
dotnet build YaziDesktopHost.slnx --no-restore
dotnet run --project tests/YaziDesktopHost.Tests/YaziDesktopHost.Tests.csproj --no-build --no-restore
```

To run the host from source:

```powershell
dotnet run --project src/YaziDesktopHost/YaziDesktopHost.csproj
```

The host resolves `yazi.exe` from `PATH`. To use a specific executable, set
`YAZI_PATH` before starting it:

```powershell
$env:YAZI_PATH = 'C:\Tools\yazi\yazi.exe'
dotnet run --project src/YaziDesktopHost/YaziDesktopHost.csproj
```

The host starts Yazi in the host process working directory for the current
implementation. Yazi remains responsible for file-manager state and file
operation semantics.

## Terminal operation

The embedded terminal supports the normal Yazi interaction path, including:

- keyboard navigation and commands
- CJK and Unicode display
- Japanese IME composition, conversion, and commit
- xterm mouse reporting
- terminal resize and reflow
- alternate-screen rendering and 24-bit color

The host also supports Explorer/Desktop drag-and-drop in the validated
directions. Ctrl/Shift Copy/Move behavior follows the Windows Shell effect
negotiation.

## Diagnostics

For the opt-in terminal color fixture, set the following environment variable
before starting the host:

```powershell
$env:YAZI_DESKTOP_HOST_VT_FIXTURE = '1'
dotnet run --project src/YaziDesktopHost/YaziDesktopHost.csproj
```

The fixture is for manual validation and is not part of normal Yazi display.

## Known limitations

- The product target is Windows x64 only.
- Changing Yazi's current directory with ordinary keyboard navigation while an
  Explorer drag is already in progress is not supported in v1; the OLE drag
  loop does not deliver those keys to Yazi.
- Packaged native HWND/WPF overlay behavior remains a separate manual gate.
- The exact supported Yazi/`ya` version policy is not yet a compatibility range;
  use the pinned fixture until that policy is defined.
- The bridge plugin is an opt-in compatibility probe and is not a general
  Yazi-version compatibility guarantee.

## License

Yazi Terminal is released under the [MIT License](../LICENSE).

Third-party dependencies are distributed under their own terms. In particular,
`EasyWindowsTerminalControl` uses the third-party NuGet package
`CI.Microsoft.Terminal.Wpf`. The v1 design permits that dependency with a
recorded supply-chain and maintenance risk; binary distributions must include
the applicable Microsoft Terminal `LICENSE` and `NOTICE` handling after it has
been confirmed. See the [dependency decision in the design document](history/design-yazi-windows-gui-frontend.md#dependency-licensing-and-supply-chain-decision).
