# Design: File-view mouse-wheel crash

## Cause

The WPF terminal message hook invokes the clipboard-paste classifier for every
terminal message. That classifier called `IntPtr.ToInt32()` before checking
whether the message was a keyboard message. A `WM_MOUSEWHEEL` `wParam` carries
the signed wheel delta in its high word; a negative delta can therefore be a
positive 32-bit value above `Int32.MaxValue`. On 64-bit .NET, `IntPtr.ToInt32()`
throws `OverflowException`, which escapes the WPF `MessageHook` path and can
terminate the host. The same helper is also called by the native window
subclass, so both input paths need the same safe classification.

## Change

Make the clipboard-paste helper reject non-key messages before reading any
keyboard fields. For key messages, read only the low 16 bits of `wParam`,
which is the Win32 virtual-key field, instead of converting the complete
message parameter to a signed 32-bit integer.

Keep the message-specific logic in the existing helper so the WPF message
hook and native window subclass share one classification rule. No mouse-wheel
message is marked handled by the host; it continues through the existing
terminal control path.

## Verification seam

Add a pure regression test at the clipboard-paste classifier boundary using a
negative wheel delta and modifier bits. Retain the existing keyboard shortcut
tests to prove the paste contract is unchanged. Live preview/editor scrolling
must be checked separately because the test executable does not create the
native terminal HWND.
