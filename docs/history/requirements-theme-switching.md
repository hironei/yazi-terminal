# Requirements: Light/Dark Theme Switching

Issue: #3

## Goal

Allow a user to switch the running Yazi Terminal host between Light and Dark
appearance modes without restarting the host or Yazi.

## Functional requirements

1. The host exposes a visible Theme menu with Light and Dark choices.
2. Dark is the default mode, preserving the current black terminal appearance.
3. Selecting a mode updates the WPF host chrome and the embedded terminal
   background, foreground, selection color, and ANSI palette as one operation.
4. The selected mode can be changed while Yazi is running; the terminal process
   and bridge session remain unchanged.
5. Light mode uses sufficient contrast for host labels/menu items and ordinary
   terminal text; Dark mode retains the current high-contrast behavior.

## Non-functional requirements

- Preserve the existing Yazi bridge environment/protocol identifiers and shell,
  drag/drop, input, and startup behavior.
- Do not add a dependency or require a settings migration for this scoped
  runtime switch.
- Keep theme selection on the WPF UI thread and avoid changing terminal message
  hook ownership.

## Edge cases and error handling

- Choosing the already-active mode is idempotent.
- Theme application must be safe before the terminal has finished starting;
  host resources still update even if terminal creation later fails.
- No external file or user data is written by theme switching.

## Acceptance criteria

- A fresh launch starts in Dark mode.
- The Theme menu exposes Light and Dark and marks the active choice.
- Each choice changes both host chrome and the embedded terminal palette without
  restarting Yazi.
- Existing executable, bridge, shell integration, drag/drop, and terminal tests
  remain passing.
- The user manual describes the menu and Dark default.
