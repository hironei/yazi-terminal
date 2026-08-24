# Design: Command Palette Navigation Test Seam

Issue: #31

`PaletteNavigation.TryGetMoveOffset` receives a small key enum, a
no-modifiers flag, and the current query. It returns `1`, `-1`, or `null`.
`null` is the explicit no-navigation outcome, so `CommandPaletteWindow` leaves
the WPF key event unhandled and the focused search box can receive normal text
input.

`PaletteNavigation.NextIndex` receives only item count, selected index, and
offset. It returns `-1` for an empty list, selects the first/last row when
nothing is selected, and uses modulo arithmetic for wrapping. The window alone
performs `SelectedIndex` and `ScrollIntoView` side effects after the pure helper
returns a valid index.

The executable tests exercise the helper directly; WPF focus routing,
`e.Handled`, text composition, and visual scrolling remain manual GUI
acceptance because they require a Windows dispatcher and control tree.
