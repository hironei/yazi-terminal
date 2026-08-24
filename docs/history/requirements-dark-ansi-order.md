# Requirements: Dark ANSI Color Table Order

Issue: #24

## Goal

Make the built-in Dark terminal color table use the documented standard ANSI
16-color index order, so Yazi's indexed SGR colors map to their expected
colors.

## Functional requirements

1. Dark `TerminalColorTable` indexes 0-7 must be black, red, green, yellow,
   blue, magenta, cyan, and white.
2. Indexes 8-15 must be the corresponding bright colors in the same order.
3. The Light table, settings-file schema, color override precedence, and Yazi
   flavor handling remain unchanged.
4. The user manual's built-in Dark settings example must match the runtime
   table and its ANSI index reference.

## Acceptance criteria

- A palette-model regression test asserts every built-in Dark table entry at
  indexes 0 through 15.
- The executable test suite, build, and formatting verification pass.
- Manual Windows/Yazi validation records whether `30`-`37`, `40`-`47`,
  `90`-`97`, and `100`-`107` render in the documented order; it is not
  automated by this change.

## Compatibility and non-goals

The change only corrects the default Dark indexed palette. Explicit
`TerminalColorTable` values in a user's settings file continue to override all
16 entries and are not migrated or rewritten. No protocol, persistence shape,
terminal backend, Yazi process, or bridge behavior changes.
