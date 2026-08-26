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
  resize, alternate-screen rendering, and 24-bit color passed a historical
  pinned Windows/Yazi fixture; they are not a current-plugin live validation.
- Explorer/Desktop drag-and-drop is supported in the validated directions;
  Yazi and Windows Shell remain responsible for file-operation semantics.
- The product target is Windows x64. The historical validated Yazi/`ya` fixture
  is 26.5.6; see the compatibility evidence for current artifact/source status.

## User installation

### Requirements

- Windows x64
- .NET 10 Windows Desktop runtime for the framework-dependent release archive
- `yazi.exe` and its paired `ya.exe`

The compatibility boundary is the historical exact Windows, Yazi, and `ya`
fixture recorded in the archived compatibility evidence. Current release
artifact inspection and source/plugin identity are recorded separately; x86 and
ARM64 are not product targets.

Download the latest Windows x64 archive from the
[GitHub Releases](https://github.com/hironei/yazi-terminal/releases), extract
it, and start `YaziTerminal.exe`.

The archive contains the executable, its native terminal dependencies, this
manual, the MIT `LICENSE`, and the optional bridge plugin.

## Plugin installation

The optional `yazi-desktop-host.yazi` bridge plugin publishes Yazi's current
directory, hovered item, and selection to Yazi Terminal. It is required for the
host's bridge-backed Shell targeting and Explorer drag-and-drop behavior.
The historical validated fixture is Yazi/`ya` 26.5.6. Current plugin/live-Yazi
validation status is recorded separately in the compatibility evidence.

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
and `FontSize`. `FontFamily` accepts any installed font family name, such as
`HackGen Console`. The font settings are:

| Setting | Allowed values | Default |
| --- | --- | --- |
| `FontFamily` | Any installed font family name | `MS Gothic` |
| `FontSize` | Any positive integer from `1` to `32767` | `14` |

Missing, blank, or invalid values fall back independently to those defaults.
To confirm a fallback after saving or restarting, check
`%LOCALAPPDATA%\YaziTerminal\app.log` for
`settings_font_family_fallback` and/or `settings_font_size_fallback`, then
replace it with an installed font family name or a font size in the documented range. Add
a `ThemeColors` object to customize Light and Dark independently. Color values
must be six-digit RGB strings in `#RRGGBB` format. The named fields are
`HostBackground`, `HostForeground`, `PaletteBackground`, `PaletteForeground`,
`PaletteBorder`, `PaletteInputBackground`, `PaletteSelectionBackground`,
`PaletteSelectionForeground`, `TerminalBackground`, `TerminalForeground`,
and `TerminalSelectionBackground`. `TerminalColorTable` accepts exactly 16
colors in ANSI order (black, red, green, yellow, blue, magenta, cyan, white,
then their bright variants).

The named color settings affect these parts of the host:

| Setting | Affected area |
| --- | --- |
| `HostBackground` | Host window surface outside the embedded terminal |
| `HostForeground` | Host-side foreground color, such as labels and menu text |
| `PaletteBackground` | Command Palette background |
| `PaletteForeground` | Command Palette title, search text, and command text |
| `PaletteBorder` | Command Palette border and search-box border |
| `PaletteInputBackground` | Command Palette search-box background |
| `PaletteSelectionBackground` | Selected Command Palette row background |
| `PaletteSelectionForeground` | Text in the selected Command Palette row |
| `TerminalBackground` | Embedded terminal default background |
| `TerminalForeground` | Embedded terminal default text color |
| `TerminalSelectionBackground` | Embedded terminal selection background |
| `TerminalColorTable` | ANSI/VT indexed colors emitted by Yazi in the embedded terminal |

`TerminalColorTable` uses the standard 16-entry ANSI order. Indexes 0-7 are
used by normal foreground/background colors (`30`-`37` / `40`-`47`), and
indexes 8-15 are used by bright foreground/background colors (`90`-`97` /
`100`-`107`):

| Index | Color | Typical ANSI foreground/background codes |
| ---: | --- | --- |
| 0 | Black | `30` / `40` |
| 1 | Red | `31` / `41` |
| 2 | Green | `32` / `42` |
| 3 | Yellow | `33` / `43` |
| 4 | Blue | `34` / `44` |
| 5 | Magenta | `35` / `45` |
| 6 | Cyan | `36` / `46` |
| 7 | White | `37` / `47` |
| 8 | Bright black (gray) | `90` / `100` |
| 9 | Bright red | `91` / `101` |
| 10 | Bright green | `92` / `102` |
| 11 | Bright yellow | `93` / `103` |
| 12 | Bright blue | `94` / `104` |
| 13 | Bright magenta | `95` / `105` |
| 14 | Bright cyan | `96` / `106` |
| 15 | Bright white | `97` / `107` |

To paste Windows clipboard text into a Yazi input field, press `Ctrl+Shift+V`
or `Shift+Insert`. Yazi Terminal sends the text using bracketed paste so
multiline and non-ASCII text remain intact. `Ctrl+V` remains an ordinary
terminal key and is not treated as a paste shortcut.

For example:

```json
{
  "Theme": "Dark",
  "FontFamily": "MS Gothic",
  "FontSize": 14,
  "ThemeColors": {
    "Dark": {
      "HostBackground": "#000000",
      "HostForeground": "#FFFFFF",
      "PaletteBackground": "#252526",
      "PaletteForeground": "#F1F1F1",
      "PaletteBorder": "#525252",
      "PaletteInputBackground": "#2D2D30",
      "PaletteSelectionBackground": "#264F78",
      "PaletteSelectionForeground": "#FFFFFF",
      "TerminalBackground": "#000000",
      "TerminalForeground": "#FFFFFF",
      "TerminalSelectionBackground": "#1E5AA0",
      "TerminalColorTable": [
        "#000000", "#800000", "#008000", "#808000",
        "#000080", "#800080", "#008080", "#C0C0C0",
        "#808080", "#FF0000", "#00FF00", "#FFFF00",
        "#0000FF", "#FF00FF", "#00FFFF", "#FFFFFF"
      ]
    },
    "Light": {
      "HostBackground": "#EEE8D5",
      "HostForeground": "#073642",
      "PaletteBackground": "#FDF6E3",
      "PaletteForeground": "#073642",
      "PaletteBorder": "#93A1A1",
      "PaletteInputBackground": "#EEE8D5",
      "PaletteSelectionBackground": "#268BD2",
      "PaletteSelectionForeground": "#FDF6E3",
      "TerminalBackground": "#FDF6E3",
      "TerminalForeground": "#073642",
      "TerminalSelectionBackground": "#EEE8D5",
      "TerminalColorTable": [
        "#073642", "#DC322F", "#859900", "#B58900",
        "#268BD2", "#D33682", "#2AA198", "#EEE8D5",
        "#002B36", "#CB4B16", "#586E75", "#657B83",
        "#839496", "#6C71C4", "#93A1A1", "#FDF6E3"
      ]
    }
  }
}
```

## Popular color presets

The following presets are based on popular names listed in the official
[Yazi flavors catalog](https://github.com/yazi-rs/flavors). They configure the
host window, Command Palette, embedded terminal, and ANSI table together.
They are host color presets, not replacements for the complete Yazi flavor;
Yazi's own file-type and widget colors remain controlled by Yazi.

Copy one object into `ThemeColors.Dark` or `ThemeColors.Light` in
`settings.json`.

### Dark: Catppuccin Mocha

```json
{
  "HostBackground": "#1E1E2E", "HostForeground": "#CDD6F4",
  "PaletteBackground": "#313244", "PaletteForeground": "#CDD6F4",
  "PaletteBorder": "#585B70", "PaletteInputBackground": "#313244",
  "PaletteSelectionBackground": "#89B4FA", "PaletteSelectionForeground": "#1E1E2E",
  "TerminalBackground": "#1E1E2E", "TerminalForeground": "#CDD6F4",
  "TerminalSelectionBackground": "#45475A",
  "TerminalColorTable": [
    "#45475A", "#F38BA8", "#A6E3A1", "#F9E2AF",
    "#89B4FA", "#F5C2E7", "#94E2D5", "#BAC2DE",
    "#585B70", "#F38BA8", "#A6E3A1", "#F9E2AF",
    "#89B4FA", "#F5C2E7", "#94E2D5", "#CDD6F4"
  ]
}
```

### Dark: Tokyo Night

```json
{
  "HostBackground": "#1A1B26", "HostForeground": "#A9B1D6",
  "PaletteBackground": "#24283B", "PaletteForeground": "#A9B1D6",
  "PaletteBorder": "#414868", "PaletteInputBackground": "#24283B",
  "PaletteSelectionBackground": "#7AA2F7", "PaletteSelectionForeground": "#1A1B26",
  "TerminalBackground": "#1A1B26", "TerminalForeground": "#A9B1D6",
  "TerminalSelectionBackground": "#33467C",
  "TerminalColorTable": [
    "#15161E", "#F7768E", "#73DACA", "#E0AF68",
    "#7AA2F7", "#BB9AF7", "#7DCFFF", "#A9B1D6",
    "#414868", "#F7768E", "#73DACA", "#E0AF68",
    "#7AA2F7", "#BB9AF7", "#7DCFFF", "#C0CAF5"
  ]
}
```

### Dark: Gruvbox Dark

```json
{
  "HostBackground": "#282828", "HostForeground": "#EBDBB2",
  "PaletteBackground": "#3C3836", "PaletteForeground": "#EBDBB2",
  "PaletteBorder": "#665C54", "PaletteInputBackground": "#3C3836",
  "PaletteSelectionBackground": "#458588", "PaletteSelectionForeground": "#FBF1C7",
  "TerminalBackground": "#282828", "TerminalForeground": "#EBDBB2",
  "TerminalSelectionBackground": "#504945",
  "TerminalColorTable": [
    "#282828", "#CC241D", "#98971A", "#D79921",
    "#458588", "#B16286", "#689D6A", "#A89984",
    "#928374", "#FB4934", "#B8BB26", "#FABD2F",
    "#83A598", "#D3869B", "#8EC07C", "#EBDBB2"
  ]
}
```

### Dark: Nord

```json
{
  "HostBackground": "#2E3440", "HostForeground": "#D8DEE9",
  "PaletteBackground": "#3B4252", "PaletteForeground": "#D8DEE9",
  "PaletteBorder": "#4C566A", "PaletteInputBackground": "#3B4252",
  "PaletteSelectionBackground": "#88C0D0", "PaletteSelectionForeground": "#2E3440",
  "TerminalBackground": "#2E3440", "TerminalForeground": "#D8DEE9",
  "TerminalSelectionBackground": "#434C5E",
  "TerminalColorTable": [
    "#3B4252", "#BF616A", "#A3BE8C", "#EBCB8B",
    "#81A1C1", "#B48EAD", "#88C0D0", "#E5E9F0",
    "#4C566A", "#BF616A", "#A3BE8C", "#EBCB8B",
    "#81A1C1", "#B48EAD", "#8FBCBB", "#ECEFF4"
  ]
}
```

### Dark: Dracula

```json
{
  "HostBackground": "#282A36", "HostForeground": "#F8F8F2",
  "PaletteBackground": "#44475A", "PaletteForeground": "#F8F8F2",
  "PaletteBorder": "#6272A4", "PaletteInputBackground": "#44475A",
  "PaletteSelectionBackground": "#BD93F9", "PaletteSelectionForeground": "#282A36",
  "TerminalBackground": "#282A36", "TerminalForeground": "#F8F8F2",
  "TerminalSelectionBackground": "#44475A",
  "TerminalColorTable": [
    "#21222C", "#FF5555", "#50FA7B", "#F1FA8C",
    "#BD93F9", "#FF79C6", "#8BE9FD", "#F8F8F2",
    "#6272A4", "#FF6E6E", "#69FF94", "#FFFFA5",
    "#D6ACFF", "#FF92DF", "#A4FFFF", "#FFFFFF"
  ]
}
```

### Light: Catppuccin Latte

```json
{
  "HostBackground": "#EFF1F5", "HostForeground": "#4C4F69",
  "PaletteBackground": "#E6E9EF", "PaletteForeground": "#4C4F69",
  "PaletteBorder": "#9CA0B0", "PaletteInputBackground": "#E6E9EF",
  "PaletteSelectionBackground": "#1E66F5", "PaletteSelectionForeground": "#EFF1F5",
  "TerminalBackground": "#EFF1F5", "TerminalForeground": "#4C4F69",
  "TerminalSelectionBackground": "#CCD0DA",
  "TerminalColorTable": [
    "#5C5F77", "#D20F39", "#40A02B", "#DF8E1D",
    "#1E66F5", "#EA76CB", "#179299", "#ACB0BE",
    "#6C6F85", "#D20F39", "#40A02B", "#DF8E1D",
    "#1E66F5", "#EA76CB", "#179299", "#BCC0CC"
  ]
}
```

### Light: Gruvbox Light

```json
{
  "HostBackground": "#FBF1C7", "HostForeground": "#3C3836",
  "PaletteBackground": "#F2E5BC", "PaletteForeground": "#3C3836",
  "PaletteBorder": "#A89984", "PaletteInputBackground": "#F2E5BC",
  "PaletteSelectionBackground": "#458588", "PaletteSelectionForeground": "#FBF1C7",
  "TerminalBackground": "#FBF1C7", "TerminalForeground": "#3C3836",
  "TerminalSelectionBackground": "#D5C4A1",
  "TerminalColorTable": [
    "#FBF1C7", "#CC241D", "#98971A", "#D79921",
    "#458588", "#B16286", "#689D6A", "#7C6F64",
    "#928374", "#9D0006", "#79740E", "#B57614",
    "#076678", "#8F3F71", "#427B58", "#3C3836"
  ]
}
```

### Light: Flexoki Light

```json
{
  "HostBackground": "#FFFCF0", "HostForeground": "#100F0F",
  "PaletteBackground": "#F2F0E5", "PaletteForeground": "#100F0F",
  "PaletteBorder": "#B7B5AC", "PaletteInputBackground": "#F2F0E5",
  "PaletteSelectionBackground": "#205EA6", "PaletteSelectionForeground": "#FFFCF0",
  "TerminalBackground": "#FFFCF0", "TerminalForeground": "#100F0F",
  "TerminalSelectionBackground": "#E6E4D9",
  "TerminalColorTable": [
    "#100F0F", "#AF3029", "#66800B", "#AD8301",
    "#205EA6", "#A02F6F", "#24837B", "#E6E4D9",
    "#575653", "#D14D41", "#879A39", "#D0A215",
    "#4385BE", "#CE5D97", "#3AA99F", "#FFFCF0"
  ]
}
```

### Light: Kanagawa Lotus

```json
{
  "HostBackground": "#F2ECBC", "HostForeground": "#545464",
  "PaletteBackground": "#E7DDB6", "PaletteForeground": "#545464",
  "PaletteBorder": "#A09CAC", "PaletteInputBackground": "#E7DDB6",
  "PaletteSelectionBackground": "#6693BF", "PaletteSelectionForeground": "#F2ECBC",
  "TerminalBackground": "#F2ECBC", "TerminalForeground": "#545464",
  "TerminalSelectionBackground": "#DCD7BA",
  "TerminalColorTable": [
    "#545464", "#C84053", "#6F894E", "#77713F",
    "#6693BF", "#B35B79", "#4E8CA2", "#DCD7BA",
    "#8A8980", "#D7474B", "#6E8F4A", "#836F3F",
    "#7E9CD8", "#A45A8D", "#5E9AA9", "#F2ECBC"
  ]
}
```

### Light: Rosé Pine Dawn

```json
{
  "HostBackground": "#FAF4ED", "HostForeground": "#575279",
  "PaletteBackground": "#F2E9DE", "PaletteForeground": "#575279",
  "PaletteBorder": "#9893A5", "PaletteInputBackground": "#F2E9DE",
  "PaletteSelectionBackground": "#286983", "PaletteSelectionForeground": "#FAF4ED",
  "TerminalBackground": "#FAF4ED", "TerminalForeground": "#575279",
  "TerminalSelectionBackground": "#F2E9DE",
  "TerminalColorTable": [
    "#575279", "#B4637A", "#6E6A86", "#EA9D34",
    "#286983", "#907AA9", "#56949F", "#F2E9DE",
    "#9893A5", "#B4637A", "#6E6A86", "#EA9D34",
    "#286983", "#907AA9", "#56949F", "#FAF4ED"
  ]
}
```

Omitted or invalid named colors fall back independently. An invalid
`TerminalColorTable` falls back as a whole. Settings colors take precedence
over colors read from the selected Yazi flavor; omitted settings still use
the flavor integration and built-in defaults. When the file is saved, the
running host watches it and applies valid changes immediately.

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
palette also lists manager actions from `[[mgr.prepend_keymap]]` and
`[[mgr.append_keymap]]` in the configured Yazi `keymap.toml`, including their
descriptions and key bindings. A scalar `run` sends one action; a string-array
`run` sends each action in its declared order through `ya emit-to`. Other
keymap contexts are not listed because the palette targets the current Yazi
manager. Plugin and `shell` actions remain owned and interpreted by Yazi. The
catalog is read when the bridge connects, so restart Yazi Terminal after
changing the keymap.

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

The host may write category-level failure diagnostics to
`%LOCALAPPDATA%\YaziTerminal\app.log`. Shell context-menu item names are not
read or written to that log.

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
`CI.Microsoft.Terminal.Wpf`. Its version-specific provenance is tied to
Microsoft Terminal commit `9ae724aa...`; the full upstream Microsoft Terminal
`LICENSE` and `NOTICE.md` are shipped and their exact SHA-256 values are
validated in every release ZIP. This is an engineering provenance record, not a
legal conclusion. See the repository's
[third-party notices](../THIRD-PARTY-NOTICES.md) for the package evidence. The
v0.1.8 Release is retained, while its old ZIP is replaced with a
notice-complete repackaging; executable code is unchanged.
