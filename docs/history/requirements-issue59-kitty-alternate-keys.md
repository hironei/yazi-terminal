# Requirements: Issue #59 unwanted en-US keyboard layout

## Scope

Prevent Windows from registering an unwanted `en-US / US` CTF/TSF input
profile (`HKCU\Software\Microsoft\CTF\SortOrder\AssemblyItem\0x00000409`)
while using Yazi Terminal, without deleting or rewriting any CTF/TSF or
keyboard-layout registry state and without changing the user's configured
input languages.

## Root cause

`Microsoft.Terminal.Control.dll` (native module behind the pinned
`CI.Microsoft.Terminal.Wpf` dependency) resolves Kitty keyboard protocol
alternate keys via:

```cpp
static const auto usLayout = LoadKeyboardLayoutW(L"00000409", 0);
```

This is a C++ function-local static, so it runs at most once per process,
the first time `TerminalInput::KeyboardHelper::getKittyUSBaseKey()` is
reached. That path is only reached when the terminal's Kitty flags have the
"report alternate keys" bit (value 4) set and a character key event is
encoded (`TerminalInput::_encodeKitty()`).

Yazi pushes Kitty flags 29 (`DISAMBIGUATE_ESCAPE_CODES` | `REPORT_ALTERNATE_KEYS`
| `REPORT_ALL_KEYS_AS_ESCAPE_CODES` | `REPORT_ASSOCIATED_TEXT` = 1+4+8+16) via
`CSI > 29 u` on startup. Once that is parsed by the terminal control's VT
state machine, the very next character key event encoded through the Kitty
path triggers the one-time `LoadKeyboardLayoutW("00000409", 0)` call, which
registers the CTF/TSF ghost profile as a side effect.

Confirmed by attaching WinDbg (`cdb.exe`) to a running `YaziTerminal.exe`,
breaking on `user32!LoadKeyboardLayoutW`, and reproducing with physical
keyboard input (`Yazi -> o -> nvim -> :qa! -> q`). The captured call stack:

```
USER32!LoadKeyboardLayoutW
Microsoft_Terminal_Control!...TerminalInput::KeyboardHelper::getKittyUSBaseKey+0x58
Microsoft_Terminal_Control!...TerminalInput::_encodeKitty+0x170
Microsoft_Terminal_Control!...TerminalInput::HandleKey+0x5b02a
Microsoft_Terminal_Control!...Terminal::SendCharEvent+0xb1
Microsoft_Terminal_Control!HwndTerminal::_SendCharEvent+0x11c
Microsoft_Terminal_Control!TerminalSendCharEvent+0x9
DirectWriteForwarder!ILStubClass.IL_STUB_PInvoke(...)
```

`Win32InputMode`, the `DECSET 9001` bridge, and YaziTerminal-specific
focus/HWND-subclass code are unrelated: the call happens purely from parsing
Yazi's own Kitty flags push plus a subsequent key event, independent of those
paths.

## Functional requirements

1. Yazi's `CSI > 29 u` Kitty flags push must reach the terminal control with
   the `REPORT_ALTERNATE_KEYS` bit (value 4) cleared, so
   `getKittyUSBaseKey()` / `LoadKeyboardLayoutW("00000409", ...)` is never
   reached.
2. Yazi's other three Kitty capabilities (`DISAMBIGUATE_ESCAPE_CODES`,
   `REPORT_ALL_KEYS_AS_ESCAPE_CODES`, `REPORT_ASSOCIATED_TEXT`) must remain
   enabled and unaffected.
3. The rewrite must apply regardless of how ConPTY output is chunked,
   including when the escape sequence is split across multiple output
   callbacks.
4. All other terminal output (color codes, cursor movement, plain text,
   other escape sequences) must pass through unmodified.
5. The fix must not delete, rewrite, or otherwise touch CTF/TSF or
   keyboard-layout registry state, and must not change the user's configured
   input languages.

## Non-functional requirements

- Keep the change local to the ConPTY-output-to-terminal-control boundary
  (`TermPTY.InterceptOutputToUITerminal`, a public hook already exposed by
  `EasyWindowsTerminalControl`); do not fork or patch the pinned
  `CI.Microsoft.Terminal.Wpf` native binary.
- Do not add a general-purpose VT parser; match only the specific Kitty
  flags push sequence.

## Acceptance criteria

- A regression test suite exercises: stripping the bit from `CSI > 29 u`,
  leaving a push without the bit untouched, leaving unrelated escape
  sequences and plain output untouched, and correct behavior when the push
  is split across chunks (including a split at every possible offset).
- Existing executable tests pass; the solution builds without new warnings
  or errors; `dotnet format --verify-no-changes` passes.
- Manual acceptance: attaching WinDbg to a debug build with the fix and
  repeating `Yazi -> o -> nvim -> :qa! -> q` with physical keyboard input
  records no `user32!LoadKeyboardLayoutW` hit (confirmed once; the
  underlying bug was itself observed intermittently before the fix, so this
  is not a guarantee against every possible code path, only the one
  confirmed by the call-stack evidence above).
- Regression check: Japanese IME input, Ctrl/Alt/Shift combinations, arrow
  keys, and function keys continue to work after the change (manual, live
  Windows session only).
