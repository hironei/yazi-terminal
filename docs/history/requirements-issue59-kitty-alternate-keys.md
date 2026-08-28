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
reached. That path is only reached once the terminal's Kitty protocol state
has been activated by a client-issued flags push (`CSI > <flags> u`) and a
character key event is subsequently encoded
(`TerminalInput::_encodeKitty()`).

Yazi pushes Kitty flags 29 (`DISAMBIGUATE_ESCAPE_CODES` | `REPORT_ALTERNATE_KEYS`
| `REPORT_ALL_KEYS_AS_ESCAPE_CODES` | `REPORT_ASSOCIATED_TEXT` = 1+4+8+16) via
`CSI > 29 u` on startup. Confirmed by attaching WinDbg (`cdb.exe`) to a
running `YaziTerminal.exe`, breaking on `user32!LoadKeyboardLayoutW`, and
reproducing with physical keyboard input (`Yazi -> o -> nvim -> :qa! -> q`).
The captured call stack:

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

## Rejected approach: stripping only `REPORT_ALTERNATE_KEYS`

A first attempt (reverted; briefly shipped as v1.0.5, since pulled) rewrote
Yazi's `CSI > 29 u` push to `CSI > 25 u`, clearing only the
`REPORT_ALTERNATE_KEYS` bit (value 4) while leaving the other three Kitty
capabilities enabled. This broke Yazi's Shift+letter key handling (e.g.
Shift+Z, bound to the zoxide plugin, stopped launching zoxide) because Yazi
relies on the alternate-key (shifted key) field that this same bit gates.
Windows Terminal does not have this problem and was used as a working
reference.

Investigating why Windows Terminal does not reproduce the original bug:
`AllowKittyKeyboardMode` (upstream `microsoft/terminal`,
`src/cascadia/inc/ControlProperties.h`) defaults to `true` at the terminal
*core* level, but `Microsoft.Terminal.Wpf.TerminalControl` (the WPF control
this app embeds) does not expose any managed API to read or set it, or any
other `ICoreSettings`-level knob — its public surface is limited to
`Connection`, `Rows`/`Columns`, `AutoResize`, and `SetTheme`.
`EasyWindowsTerminalControl.EasyTerminalControl.Win32InputMode` is not a
settings-object property either; it works by sending a VT private-mode
sequence (`\x1b[?9001h`/`l`, DECSET/DECRST) to the terminal, which is the
only kind of runtime control this app has over the embedded control's
behavior. There is no equivalent VT sequence to toggle
`AllowKittyKeyboardMode` from the client side. Whatever Windows Terminal's
own profile-level effective default for Kitty support is, this app has no
way to replicate it through a settings API — only by controlling what
reaches the terminal's VT parser.

## Fix

Drop Yazi's entire Kitty flags push (`CSI > <flags> u`) before it reaches the
terminal control, via `TermPTY.InterceptOutputToUITerminal`. The terminal's
Kitty protocol state then stays at its inactive default, so
`TerminalInput::_encodeKitty()` is never reached and
`getKittyUSBaseKey()`/`LoadKeyboardLayoutW("00000409", ...)` is never called.
Kitty flags queries (`CSI ? u`) and pops (`CSI < u`) are left untouched, as
is all other terminal output. This matches Yazi's own protocol-negotiation
fallback that already works correctly against any terminal that does not
support the Kitty protocol at all, which includes ordinary usage in Windows
Terminal today.

## Functional requirements

1. Yazi's `CSI > <flags> u` Kitty flags push must never reach the terminal
   control, so the terminal's Kitty protocol state remains inactive and
   `getKittyUSBaseKey()` / `LoadKeyboardLayoutW("00000409", ...)` is never
   reached.
2. Kitty flags queries (`CSI ? u`) and pops (`CSI < u`) must pass through
   unmodified.
3. The suppression must apply regardless of how ConPTY output is chunked,
   including when the escape sequence is split across multiple output
   callbacks.
4. All other terminal output (color codes, cursor movement, plain text,
   other escape sequences) must pass through unmodified.
5. The fix must not delete, rewrite, or otherwise touch CTF/TSF or
   keyboard-layout registry state, and must not change the user's configured
   input languages.
6. Shift+letter key bindings that work correctly in Windows Terminal (e.g.
   Yazi's zoxide binding on Shift+Z) must continue to work identically in
   this app.

## Non-functional requirements

- Keep the change local to the ConPTY-output-to-terminal-control boundary
  (`TermPTY.InterceptOutputToUITerminal`, a public hook already exposed by
  `EasyWindowsTerminalControl`); do not fork or patch the pinned
  `CI.Microsoft.Terminal.Wpf` native binary.
- Do not add a general-purpose VT parser; match only the specific Kitty
  flags push sequence.

## Acceptance criteria

- A regression test suite exercises: dropping `CSI > 29 u` and a zero-flag
  `CSI > u` push, leaving queries/pops and unrelated escape sequences
  untouched, and correct behavior when a push is split across chunks
  (including a split at every possible offset).
- Existing executable tests pass; the solution builds without new warnings
  or errors; `dotnet format --verify-no-changes` passes.
- Manual acceptance: attaching WinDbg to a debug build with the fix and
  repeating `Yazi -> o -> nvim -> :qa! -> q` with physical keyboard input
  records no `user32!LoadKeyboardLayoutW` hit.
- Manual acceptance: Shift+Z correctly launches zoxide, matching Windows
  Terminal behavior (this is the regression the earlier, reverted approach
  introduced).
- Regression check: Japanese IME input, Ctrl/Alt/Shift combinations, arrow
  keys, and function keys continue to work after the change (manual, live
  Windows session only).
- As with the previous attempt, the original bug was itself observed
  intermittently before any fix, so a single successful manual verification
  is not an absolute guarantee against every possible code path.
