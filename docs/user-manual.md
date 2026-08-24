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
`yazi-desktop-host.yazi`. From the repository root, copy it into the
current user's Yazi plugin directory:

```powershell
$pluginSource = (Resolve-Path 'yazi-desktop-host.yazi').Path
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

### Command-line path routing

Starting `YaziTerminal.exe` normally creates a new window. A directory can be
passed as the first positional argument and becomes Yazi's initial directory:

```powershell
YaziTerminal.exe C:\work\project
```

An existing file can also be passed. Yazi starts in the file's parent
directory, reveals the file, and runs its `open` action. Yazi's `[open]` and
`[opener]` configuration decides whether that action uses the configured
editor or another opener:

```powershell
YaziTerminal.exe C:\work\project\README.md
```

To request the most recently launched Yazi Terminal window instead, use
`--last-instance`:

```powershell
YaziTerminal.exe --last-instance C:\work\project
YaziTerminal.exe --last-instance C:\work\project\README.md
```

The existing window is brought to the foreground and its active Yazi tab is
asked to change to the directory. For a file target, the existing window
reveals and opens the file using Yazi's configured opener/editor. If no usable
last instance exists, the command starts a new window instead. The option is
current-user-only and does not reuse a Git Bash or other terminal pane.

From Git Bash, quote the executable and directory when either path contains
spaces:

```bash
"/c/Tools/Yazi Terminal/YaziTerminal.exe" --last-instance "C:/work/project"
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

Press `Ctrl+Shift+P` to open the Command Palette. Type `light` or `dark`,
select `Theme: Light` or `Theme: Dark`, and press `Enter`. Use `Up`/`Down` or
`k`/`j` to move the selection; `j` and `k` navigate when the query is empty
and remain available as filter text after typing. Press `Escape` to close the
palette without applying a command. Dark is the default. Theme changes apply
immediately to the host and embedded terminal for the current session and are
persisted for the next launch.

To edit the terminal font family or font size, select
`Settings: Edit terminal appearance`. The host saves the current values to
`%LOCALAPPDATA%\YaziTerminal\settings.json`, then asks Yazi to reveal and open
that file using Yazi's configured opener/editor. Edit the JSON values and
save the file. The file contains `Theme`, `FontFamily`,
and `FontSize`; unsupported values fall back to the defaults. When the file is
saved, the running host watches it and applies valid changes immediately.

When Yazi's `%APPDATA%\yazi\config\theme.toml` selects a flavor, the host also
reads the matching `flavors\<name>.yazi\flavor.toml` on startup and when the
palette is opened. The integration boundary is:

| Flavor value | Host behavior |
| --- | --- |
| `[flavor]` `light`/`dark` | Read to select the matching flavor file. |
| `[app]` `overall.bg` | Used as the embedded terminal background when present. |
| `[mgr]` `cwd` and `[filetype]` fallback `url = "*"` | Used as the host/default terminal foreground when present. |
| `[mgr]` `border_style` | Used for the command palette border. |
| `[tabs]` `active` | Used for the command palette selected-row colors. |
| `[filetype]` MIME/URL-specific rules, including folder `url = "*/"` | Not repainted by the host; Yazi itself must emit these colors through VT. |
| `[icon]`, `[mode]`, `[status]`, `[pick]`, `[input]`, `[cmp]`, `[tasks]`, `[which]`, `[help]`, `[spot]`, `[notify]` | Not parsed by the host; these remain Yazi-owned styles. |
| ANSI color table | Not defined by `flavor.toml`; the built-in Solarized/Dark fallback remains in use. |

The host launches Yazi with `COLORTERM=truecolor` and `TERM=xterm-256color`,
and clears an inherited `NO_COLOR` value so file/folder colors emitted by Yazi
can be rendered by the embedded terminal.
The host does not synthesize file colors from the flavor file: if the terminal
version and host version differ, check Yazi's selected flavor and VT color
capability first. Invalid or missing theme files do not prevent startup;
unsupported host mappings use built-in fallbacks.

When the optional `yazi-desktop-host.yazi` bridge plugin is installed, the
palette also lists actions from the configured Yazi `keymap.toml`, including
their descriptions and key bindings. Selecting one sends the action to the
current Yazi instance through `ya emit-to`; plugin and `shell` actions remain
owned and interpreted by Yazi. The catalog is read when the bridge connects,
so restart Yazi Terminal after changing the keymap.

The embedded terminal supports the normal Yazi interaction path, including:

- keyboard navigation and commands
- CJK and Unicode display
- Japanese IME composition, conversion, and commit
- xterm mouse reporting
- terminal resize and reflow
- alternate-screen rendering and 24-bit color

With the bridge plugin installed and connected, right-clicking opens the
Windows Shell context menu for the selected or hovered item. Holding `Shift`
while right-clicking opens the menu for Yazi's current directory (the parent
folder of the displayed item). The bridge state must be available for either
operation.

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
