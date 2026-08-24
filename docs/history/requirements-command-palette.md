# Requirements: Command Palette, Yazi Actions, and Light Theme Contrast

## Goal

Replace the permanent theme menu with a keyboard-first command palette,
improve Light theme readability, make the palette useful for Yazi actions
reported by the installed bridge plugin, and persist the host's visual
settings.

## Functional requirements

1. Pressing `Ctrl+Shift+P` while the host window or embedded terminal is
   focused opens a command palette owned by the current Yazi Terminal window.
2. The palette supports text filtering, keyboard selection, `Enter` to execute
   the selected command, and `Escape` to close without changing the theme.
   `Up`/`Down` and `k`/`j` move the selection with wrap-around. `j` and `k`
   are navigation keys while the query is empty; they remain typeable in a
   non-empty query so filtering is not made impossible for those characters.
3. The palette includes `Theme: Dark` and `Theme: Light` commands and applies
   the selected theme immediately without restarting Yazi, ConPTY, or bridge
   services.
4. The permanent top-level Theme menu is removed from the main window.
5. The palette remains readable in both modes, with an explicit selection style
   and sufficient contrast for its input, command list, and descriptions.
6. Light mode uses a less glaring terminal surface and higher-contrast default
   and ANSI colors than the current near-white palette.
7. When the bridge plugin reports Yazi keymap actions, the palette displays
   their descriptions and runs the selected action sequence through the paired
   `ya` executable and the current Yazi client identity. The supported source
   sections are `[[mgr.prepend_keymap]]` and `[[mgr.append_keymap]]`; bindings
   for other Yazi contexts are excluded because `ya emit-to` targets manager
   actions.
8. The host documents and logs which Yazi flavor values it consumes. Host
   mappings include the selected flavor, generic foreground, manager border,
   and active-tab colors. Yazi remains responsible for file-type, icon, mode,
   status, and other in-terminal styles through its own VT output.
9. The host sets a true-color terminal environment for the Yazi child so
   file/folder colors emitted by the selected Yazi flavor can be rendered by
   the terminal control.
10. The palette provides one settings-edit command instead of listing font
    families and sizes. It opens the persisted `settings.json` through Yazi's
    configured opener/editor. The selected theme, font family, and font size
    are restored on the next host launch. Valid changes saved while the host
    is running are applied immediately.

## Non-functional requirements

- Capture the shortcut even when the native terminal HWND owns keyboard focus,
  while preserving all other terminal input behavior.
- Keep the command list WPF-independent where practical so filtering and command
  identity can be tested without launching a WPF window.
- Do not add host-side shell execution, a new dependency, or a second bridge
  protocol. Yazi actions are sent to the already running Yazi
  instance with `ya emit-to`; `shell` actions remain Yazi-owned.
- Persist only the host visual settings under the existing per-user local
  application-data directory. A malformed or inaccessible settings file must
  fall back to defaults without preventing startup.
- Preserve the Yazi bridge identifiers, last-instance routing, shell integration,
  drag/drop, IME, and process lifecycle behavior.

## Edge cases and error handling

- Reopening the palette while it is already open is ignored.
- An empty or unmatched query displays an explicit empty state and cannot
  execute a missing command.
- `Escape` and closing the palette leave the active theme unchanged.
- The palette can open before Yazi has finished starting; applying a theme still
  updates host resources and the next terminal initialization uses that mode.
- A missing bridge, missing `ya.exe`, malformed command catalog, or malformed
  action does not crash the host; theme commands remain usable and the failure
  is logged.
- A scalar `run` produces one manager action. A `run` string array produces one
  `ya emit-to` invocation per string in declared order; an empty, malformed, or
  unsupported binding never executes a partial or re-ordered action sequence.
- A missing or unsupported Yazi flavor value does not crash the host. The
  documented built-in fallback remains active for values the host does not
  consume.
- The host passes action arguments with `ProcessStartInfo.ArgumentList`, so
  command text is not evaluated by a host shell. The user-selected Yazi action
  may still ask Yazi to run its own `shell` action.

## Acceptance criteria

- The main window has no permanent Theme menu.
- `Ctrl+Shift+P` opens the palette from both the WPF window and the embedded
   terminal focus path.
- With an empty query, `j`/`k` and `Down`/`Up` move the selected row in the same
  wrapping order; with a non-empty query, `j`/`k` are preserved as filter text.
- Typing `light` leaves `Theme: Light`, and `Enter` switches to the improved
  Light palette; `dark` does the same for Dark.
- `Escape` cancels without a theme change.
- A bridge hello payload containing command metadata adds searchable Yazi commands.
  Selecting a scalar run or a multi-run array sends the exact action/argument
  token sequence to the active Yazi client in the same order as the source
  binding.
- The user manual explicitly distinguishes host-consumed flavor fields from
  flavor fields rendered by Yazi itself, and explains that file/folder color
  matching depends on the Yazi child emitting VT color sequences.
- Selecting a theme persists the choice, and the settings-edit command opens
  `%LOCALAPPDATA%\YaziTerminal\settings.json` through Yazi. A new host process
  restores the theme, font family, and font size before creating the terminal.
  Saving valid JSON while the host is running updates the active terminal
  without restarting the host; malformed intermediate writes are ignored.
- Existing executable, bridge, last-instance, shell, drag/drop, and theme tests
  remain passing.
- The user manual documents the shortcut and command names.
