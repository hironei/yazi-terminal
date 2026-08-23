# Design: Command Palette, Yazi Actions, and Light Theme Contrast

## Window boundary

The palette is a separate owned WPF `Window`, centered over the main window and
shown modally for one command selection. A normal WPF overlay inside
`MainWindow` would be hidden by the embedded terminal's native HWND airspace;
the separate window keeps the palette visible above that control and gives its
TextBox keyboard focus.

## Command model

`CommandPaletteCommand` contains a stable command identifier, display title,
description, and either a theme action or a `YaziBridgeCommand`. Theme commands
are always present. When the bridge session has a catalog, each reported Yazi
action is projected to a `Yazi: ...` palette command while retaining its
original key binding, description, and `run` string for execution.

The bridge plugin adds an optional `commands` array to the existing `hello`
payload. Each item contains `key`, `run`, and `description`. This keeps the
existing `yazi-desktop-host/1` protocol and remains compatible with older
hosts, which already ignore additional hello properties. The host validates
the optional catalog and drops it on disconnect.

The paired `ya.exe` is used for execution with:

```text
ya emit-to <client-id> <action> <args...>
```

The host tokenizes the keymap `run` string only for action/argument boundaries
(single and double quotes, with conservative backslash handling) and passes
the resulting tokens through `ProcessStartInfo.ArgumentList`. It never invokes
a host shell. Therefore Yazi remains responsible for interpreting actions
such as `shell` and `plugin`, matching the behavior of `ya.manager_emit` used
by the reference plugin.

## Input routing

- `MainWindow.PreviewKeyDown` handles `Ctrl+Shift+P` for ordinary WPF focus.
- The existing terminal message paths handle the same virtual-key combination
  for the native terminal HWND, schedule palette display through the WPF
  dispatcher, and mark the native key message handled so it is not sent to
  Yazi.
- The palette handles `Enter`, `Escape`, Up/Down, `j`/`k`, and text changes
  locally. `j`/`k` navigate only when the query is empty, preserving their use
  as filter characters once the user has started typing a query.
- After the palette closes, focus returns to the terminal when it exists.

## Theme and visual design

Remove the current top `Menu` from `MainWindow`. Add palette-only resource
brushes to `ThemeColors`: background, border, selected-row background, and
selected-row foreground. Keep Dark compatible with the current terminal palette.
For Light, use a Solarized Light-inspired palette: `#fdf6e3` for the terminal
surface, `#eee8d5` for host/input surfaces, and the dark Solarized base tone
`#073642` for default text. This avoids the glare of a white background while
keeping the palette readable. The built-in Solarized ANSI table remains the
fallback for the terminal.

When available, read the selected `light` or `dark` flavor from Yazi's
`theme.toml`, then read the corresponding `flavors/<name>.yazi/flavor.toml`.
Use its generic foreground, manager border, and active-tab colors for the host
and command palette. The host also records the generic file rule and folder
rule as diagnostics, but does not try to repaint individual terminal cells:
file-type, icon, mode, status, and other in-terminal styles belong to Yazi's
VT output. If `[app].overall.bg` is present, it is used as the embedded
terminal background. Yazi flavor files do not define a canonical terminal ANSI
table, so the built-in table remains the fallback. Missing files or unsupported
values fall back to the built-in palette.

Before launching Yazi, set `COLORTERM=truecolor` and
`TERM=xterm-256color`, and clear an inherited `NO_COLOR` value, in the child
environment. This preserves the color information emitted by Yazi's selected
flavor so the embedded terminal can match a normal true-color Yazi terminal.
The host's own visual settings are stored as JSON below
`%LOCALAPPDATA%\YaziTerminal\settings.json`; only the selected theme,
supported font family, and font size are persisted.

The palette applies the current theme colors directly to its own resources at
construction time; it does not share mutable resource dictionaries with the
owner window. Font family and font size are edited in the persisted
`%LOCALAPPDATA%\YaziTerminal\settings.json` through a palette command. The host
saves the current settings, sends `reveal <settings-path>` followed by `open`
to the active Yazi instance, and leaves the actual opener/editor selection to
Yazi's configuration. A `FileSystemWatcher` observes the settings directory;
changes are debounced on the WPF dispatcher, and valid JSON is applied through
the same theme/font terminal update path. Failed or partially-written JSON is
left untouched until a later successful save.

## Lifecycle and safety

`MainWindow` owns the palette invocation and selected command. A guard prevents
reentrant windows. Dialog cleanup is performed in `finally`, and closing the
main window cannot leave a palette operation holding the terminal focus. A
selected Yazi command is sent after the dialog closes using the existing
resolved `ya.exe` and client ID; failures are logged and do not affect theme
commands or bridge lifecycle.

## Test seams

Test command identifiers, filtering order, matching fields, empty results,
keyboard navigation policy, bridge command-catalog parsing, safe action token
construction, and the updated Light palette values in the existing executable
test suite. The solution build validates XAML event wiring and WPF compilation.
Actual keyboard capture through the native HWND, visual contrast, focus, DPI,
Yazi-version behavior, and execution against a live configured keymap remain
manual Windows acceptance checks.
