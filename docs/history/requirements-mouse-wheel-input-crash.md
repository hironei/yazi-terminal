# Requirements: File-view mouse-wheel crash

## Scope

Prevent Yazi Terminal from terminating when the user moves the mouse wheel
while Yazi is displaying file content, including the preview pane and a
configured editor such as nvim.

The fix covers the host's terminal input interception path. It does not
change Yazi's preview, opener, editor, or terminal mouse-reporting behavior.

## Functional requirements

1. `WM_MOUSEWHEEL` and other non-key terminal messages must not be converted as
   keyboard virtual-key messages by the clipboard-paste or command-palette
   shortcuts.
2. Negative wheel deltas and modifier bits must be accepted without an
   `OverflowException` or other managed exception escaping the terminal
   message hook.
3. Keyboard paste shortcuts (`Ctrl+Shift+V` and `Shift+Insert`) must retain
   their current behavior.
4. The native terminal window must continue receiving unhandled mouse input
   and the existing shell context-menu and drag/drop behavior must remain in
   scope.

## Non-functional requirements

- Preserve the existing .NET 10 / Windows x64 target and terminal backend.
- Keep the change local to input-message classification; do not upgrade
  dependencies or add a second terminal-input implementation.
- Do not log clipboard contents, file contents, or editor/preview data.

## Acceptance criteria

- A regression test exercises a mouse-wheel message with a negative wheel
  delta represented in the 32-bit `wParam` range and passes without throwing.
- Existing executable tests pass.
- The solution builds without new warnings or errors.
- Manual acceptance remains required for preview scrolling and nvim scrolling
  because the automated suite does not host a live WPF/native terminal HWND.
