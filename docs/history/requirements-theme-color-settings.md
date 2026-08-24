# Requirements: Configurable Light/Dark Theme Colors

Issue: #18

## Goal

Allow users to configure every color used by the host's Light and Dark themes
through the existing `%LOCALAPPDATA%\YaziTerminal\settings.json` file.

## Functional requirements

1. The existing `Theme`, `FontFamily`, and `FontSize` settings remain
   compatible with settings files that do not contain color configuration.
   Supported families are `MS Gothic`, `Consolas`, `Cascadia Mono`, and
   `Cascadia Code`; supported sizes are `12`, `14`, `16`, `18`, and `20`.
   The defaults are `MS Gothic` and `14`.
2. Settings may contain independent `Dark` and `Light` color sections under
   `ThemeColors`.
3. Each section may configure these `#RRGGBB` fields:
   `HostBackground`, `HostForeground`, `PaletteBackground`,
   `PaletteForeground`, `PaletteBorder`, `PaletteInputBackground`,
   `PaletteSelectionBackground`, `PaletteSelectionForeground`,
   `TerminalBackground`, `TerminalForeground`, and
   `TerminalSelectionBackground`.
4. Each section may configure `TerminalColorTable` with exactly 16
   `#RRGGBB` entries in terminal color-table order.
5. A valid setting overrides the built-in palette and the corresponding color
   read from a Yazi flavor file. Missing settings retain the existing
   built-in/Yazi-derived fallback.
6. Settings apply at startup, when the command palette opens, and after the
   watched settings file is reloaded while the host is running.
7. Editing or switching themes through the existing settings command preserves
   the configured color sections.

## Non-functional requirements

- The color format is opaque RGB only; alpha and other color syntaxes are not
  introduced.
- The existing WPF, terminal, Yazi process, bridge, shell, drag/drop, input,
  and startup boundaries remain unchanged.
- The configuration is local to the current user and is not sent to Yazi or
  any external service.

## Error handling and edge cases

- Missing or invalid color fields fall back independently to the effective
  built-in/Yazi color.
- A missing, invalid, or non-16-entry `TerminalColorTable` falls back to the
  effective 16-entry table as a whole.
- Malformed JSON and inaccessible files retain the current all-settings
  fallback behavior and do not prevent host startup. Unsupported persisted font
  settings fall back independently to their documented defaults and emit a
  font-specific fallback log event.
- Theme settings are not applied partially while a settings file is being
  written; the existing reload debounce remains the consistency boundary.

## Acceptance criteria

- A user can configure all 11 named `ThemeColors` fields and all 16 ANSI
  entries independently for Light and Dark in `settings.json`.
- A settings file without `ThemeColors` produces the same palette as before.
- A valid custom color is visible through the palette model used by both the
  WPF resources and the embedded terminal theme.
- Yazi flavor colors remain supported when no corresponding settings override
  exists, and settings overrides take precedence when both are present.
- Tests cover persistence, valid/invalid parsing, table validation,
  precedence, and fallback.
- The user manual documents the schema, format, precedence, and an example.
