# Normal Yazi exit design

## Classification

`YaziProcessExitPolicy.IsNormalExit` is the single pure policy used by the WPF
host. Exit code `0` is normal; every other exit code is abnormal.

## Lifecycle integration

`MainWindow` handles both the terminal's `Session Terminated` marker and the
background `Process.WaitForExit` observer through one `HandleProcessExit`
method. The marker is a second lifecycle signal emitted after the backend has
waited for the child; the handler still checks `HasExited` before reading the
exit code. The wait observer supplies the same code when it wins the race with
the marker.
Easy's public `IProcess` abstraction does not expose the exit code, while the
pinned package's wrapped process exposes a `Process` property and, depending on
the backend build, a `Pid` property. The host reads either reflectively and
treats a `WaitForExit()` completion as normal when the backend has already
disposed the process object before its exit code can be read. A failure while
waiting for the process still remains an abnormal exit.

For a normal exit, the host logs the normal lifecycle event, runs the existing
`DisposeSession` cleanup, and closes the window. For an abnormal exit, the
existing `HandleUnexpectedExit` dialog and cleanup remain unchanged. If the
exit code cannot be read, the host preserves the conservative abnormal-exit
behavior.

## Test seam and compatibility

The policy is independently testable without constructing a WPF window or
starting ConPTY. No bridge, command-line, persistence, package, or public
protocol behavior changes.
