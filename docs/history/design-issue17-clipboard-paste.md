# Issue 17 Clipboard Paste Design

## Design status

This design adds a narrow host-side input boundary for Windows clipboard
paste. It does not modify Yazi or the terminal backend package.

## Flow

```text
WM_KEYDOWN / WPF MessageHook
        │
        ▼
Paste gesture matcher
        │ first keydown + required modifiers
        ▼
Clipboard.GetText(UnicodeText) on WPF STA
        │
        ▼
Bracketed paste framing
        │ ESC[200~ + text + ESC[201~
        ▼
TermPTY.WriteToTerm
        │
        ▼
ConPTY → Yazi parser → active input
```

## Integration boundary

`MainWindow` already owns both the `TermPTY` instance and the native terminal
message paths. A small internal pure helper owns the protocol framing and
gesture predicates so those rules can be tested without starting WPF.

The native `TerminalWindowSubclass` path is preferred because the embedded
terminal owns a child HWND. The existing `TerminalContainer.MessageHook`
path invokes the same handler when subclass attachment is unavailable. Both
paths call the same paste helper and return `true`/`handled = true` only for a
recognized paste gesture.

## Gesture and repeat rules

- `Ctrl+Shift+V` matches `WM_KEYDOWN`/`WM_SYSKEYDOWN` for `V` while Control
  and Shift are down.
- `Shift+Insert` matches the same messages for Insert while Shift is down.
- The previous-key-state bit in `lParam` suppresses auto-repeat.
- The matcher runs before command-palette and Shell context-menu handling, but
  only matches the two paste gestures, so those existing features remain
  independent.

## Clipboard and PTY behavior

`Clipboard.GetText(TextDataFormat.UnicodeText)` is called only after a
recognized gesture and only on the WPF UI thread. Text is not logged. An empty
string is treated as a successful no-op. `ExternalException` and
`InvalidOperationException` from clipboard access are caught and categorized.

The payload is framed with the same bracketed-paste markers enabled by Yazi's
terminal parser. The original text is preserved; no shell quoting or newline
rewriting is performed. `TermPTY.WriteToTerm` is the existing input API and
keeps the host independent of the backend's internal native control methods.

## Error handling and lifecycle

If `_term` is unavailable, the gesture is consumed and a category is logged;
the host continues running. If writing to the PTY fails, the exception is
contained and categorized. Clipboard contents never enter logs or persisted
settings. The helper is only reachable while the terminal is active and is
detached with the existing terminal message hooks during shutdown.

## Traceability

| Requirement | Design element | Verification |
| --- | --- | --- |
| 1-3 | Gesture matcher and `lParam` repeat bit | Pure tests |
| 4-5 | Clipboard read and bracketed framing | Pure framing tests + live Yazi input |
| 6 | Narrow clipboard/PTY exception boundary | Static review + executable tests |
| 7 | Shared handler in both message paths | Static review + live host |
| 8 | Exact modifier/key predicates | Negative pure tests |
