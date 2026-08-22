# Design: Light/Dark Theme Switching

Issue: #3

## Boundary

`MainWindow` owns the runtime theme state because it owns both the WPF host
surface and the `EasyTerminalControl` instance. A small immutable palette model
keeps the menu/resource updates and terminal theme creation driven by the same
mode. No setting store is introduced; the choice lasts for the current
application session only.

## UI and state

- Add a top-level WPF `Menu` with a `Theme` submenu containing checkable Light
  and Dark items.
- Keep the terminal in the remaining grid row.
- Store the active mode as a private `AppThemeMode` field initialized to `Dark`.
- `ApplyTheme` updates the window/application brushes and menu check state, then
  calls the embedded terminal's public `SetTheme` method when it is available.

## Palette

The host palette provides window/menu background, foreground, and border
brushes. The terminal palette supplies default background/foreground, selection
background, cursor style, and all sixteen ANSI colors. Dark uses the existing
black/white defaults; Light uses a near-white background, near-black text, a
blue selection color, and ANSI colors with adequate contrast on a light surface.

## Lifecycle and compatibility

The initial `ApplyTheme` call occurs after XAML initialization and before Yazi
startup. Terminal construction uses the active mode's terminal palette. Later
menu clicks reapply only the WPF resources and terminal theme; they do not
recreate `TermPTY`, the terminal control, or bridge services.

No bridge protocol, environment variable, shell integration, drag/drop, input
hook, package, or public compatibility identifier changes.

## Test seams

Extract the mode-to-palette mapping into an internal WPF-independent class or
methods that can be validated by the executable test suite. Test default mode,
distinct Light/Dark defaults, and complete ANSI palette creation. XAML/build
checks cover the menu wiring; live WPF visual contrast and changing the actual
embedded control remain manual acceptance items.
