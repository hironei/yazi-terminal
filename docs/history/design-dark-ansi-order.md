# Design: Dark ANSI Color Table Order

Issue: #24

## Palette correction

`ThemePalette.DarkColorTable` is the sole built-in source for the Dark
`TerminalColorTable`. Reorder its existing RGB values to ANSI index order:

| Index range | Order |
| --- | --- |
| 0-7 | black, red, green, yellow, blue, magenta, cyan, white |
| 8-15 | bright black, bright red, bright green, bright yellow, bright blue, bright magenta, bright cyan, bright white |

No public type or persisted representation changes. `ThemePalette.For` keeps
applying Yazi-derived named colors and settings overrides after it selects the
built-in table, so a valid user `TerminalColorTable` still replaces the full
corrected default table.

## Regression protection and validation

Extend the WPF-independent `ThemePalette` executable test to compare all 16
Dark entries with the expected RGB sequence. This guards mapping rather than
only table length or endpoint values. The manual's default Dark JSON example
uses the same sequence, while its existing ANSI index table describes the SGR
codes.

The WPF terminal renderer and a real Yazi session are outside the executable
test seam. Manual Windows validation should use indexed foreground and
background SGR sequences to confirm the backend receives the corrected table.
