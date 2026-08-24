# Design: Configurable Light/Dark Theme Colors

Issue: pending GitHub authentication recovery

## Configuration boundary

`HostSettingsStore` remains the sole JSON persistence boundary. The persisted
shape adds an optional `ThemeColors` object with `Dark` and `Light` children;
existing top-level settings are unchanged:

```json
{
  "Theme": "Dark",
  "FontFamily": "MS Gothic",
  "FontSize": 14,
  "ThemeColors": {
    "Dark": {
      "HostBackground": "#000000",
      "TerminalColorTable": ["#000000", "#000080", "... 14 more ..."]
    },
    "Light": {}
  }
}
```

JSON color strings are parsed into `RgbColor` values at the persistence
boundary. `ThemeColorOverrides` holds nullable values so omitted fields remain
distinguishable from explicit values and can fall back independently.

## Palette resolution

`ThemePalette.For(mode, yaziTheme, overrides)` resolves colors in this order:

1. Built-in Light/Dark defaults, including the existing 16-entry ANSI table.
2. Supported colors read from the selected Yazi flavor.
3. Valid settings overrides for the selected mode.

The final `ThemeColors` object remains the single source for WPF brushes,
command-palette colors, and `TerminalTheme`. No WPF or terminal API receives
raw JSON values.

## Validation and recovery

Named colors accept only six hexadecimal digits preceded by `#`, with optional
case-insensitivity. Invalid named values are ignored individually. The ANSI
table is accepted only when it has exactly 16 valid entries; otherwise the
entire effective table is retained. JSON, I/O, and permission failures use the
existing settings fallback and logging path.

## Runtime lifecycle

`MainWindow` stores the parsed Light/Dark overrides alongside the current mode
and font settings. Startup, command-palette theme selection, and debounced
settings reload all pass the selected mode's overrides into `ThemePalette`.
The existing terminal instance is updated through `SetTheme`; Yazi, ConPTY,
bridge services, and input hooks are not recreated.

When `SaveSettings` writes the file, it includes the current parsed overrides
so switching the active mode or opening the editor cannot erase custom colors.

## Test seams and traceability

- `HostSettingsStore` tests cover round-trip serialization, case-insensitive
  hex parsing, missing values, invalid values, and ANSI table validation.
- `ThemePalette` tests cover settings-over-Yazi precedence and all fields/table
  entries reaching the final palette.
- Existing startup, bridge, command, and Yazi flavor tests remain unchanged
  except where they construct the expanded settings model.

This design satisfies requirements 1-7, all error cases, compatibility, and
the acceptance criteria without changing package dependencies or protocol
identifiers.
