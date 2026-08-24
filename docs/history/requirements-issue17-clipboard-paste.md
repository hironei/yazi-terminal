# Issue 17 Clipboard Paste Requirements

## Scope

When Yazi Terminal has focus in a Yazi input field, users must be able to
paste Windows Unicode clipboard text with the standard Windows Terminal
shortcuts that were tested for this issue: `Ctrl+Shift+V` and `Shift+Insert`.
The implementation applies to the host's embedded WPF terminal and must not
depend on the optional Yazi desktop bridge plugin.

## Goals

- Intercept the two paste gestures before they are forwarded as ordinary
  modified key events to Yazi.
- Read Unicode text from the Windows clipboard on the WPF UI/STA thread.
- Send the text through the existing `TermPTY` input boundary using Yazi's
  bracketed-paste protocol (`ESC[200~` and `ESC[201~`).
- Preserve clipboard text, including line breaks and non-ASCII characters,
  without interpreting it as a Shell command in the host.
- Keep the existing Shell context-menu behavior and normal keyboard input
  unchanged.

## Non-goals

- Changing Yazi's keymap or installing a Yazi plugin for paste.
- Adding a dependency or replacing `EasyWindowsTerminalControl`.
- Changing the optional bridge protocol or its environment variables.
- Making right-click a paste gesture. When the bridge is available, right-click
  remains reserved for the host's Shell context menu.
- Implementing a custom clipboard manager or clipboard history feature.

## Functional requirements

1. A first `WM_KEYDOWN` for `V` with both Control and Shift held is recognized
   as paste.
2. A first `WM_KEYDOWN` for Insert with Shift held is recognized as paste.
3. Repeated keydown messages for a held gesture do not paste repeatedly.
4. A non-empty Unicode clipboard string is wrapped in bracketed-paste markers
   and written once to the existing terminal PTY.
5. Empty clipboard text produces no PTY input.
6. Clipboard access failures are contained, categorized in the application log,
   and do not terminate the host.
7. Paste handling is available through both the hosted HWND subclass and the
   existing WPF `MessageHook` fallback.
8. Other keys, including plain `V`, `Ctrl+V`, and unmodified Insert, retain
   their existing behavior.

## Security and compatibility

- Clipboard contents are user-selected input and are not logged or persisted.
- The host does not parse, expand, or execute clipboard text.
- The existing terminal backend, ConPTY, bridge identifiers, and Shell routing
  remain unchanged.
- The feature is Windows-only, matching the WPF host target.

## Acceptance criteria

- Pure tests cover gesture recognition, repeated-key rejection, empty text, and
  exact bracketed-paste framing for ASCII, CJK, and multiline text.
- The prescribed restore, build, and executable test suite pass, or failures
  are classified as environment failures.
- On a live Windows host, `Ctrl+Shift+V` and `Shift+Insert` insert a known
  clipboard string into Yazi's `s` input field.
- On a live host with and without the bridge, paste behavior is identical;
  bridge-backed right-click Shell behavior remains separate.
