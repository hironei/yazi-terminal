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
pinned package's wrapped process exposes a public `Process` property. The host
reads that property reflectively and treats an unavailable property as an
observation failure, which keeps the conservative abnormal-exit behavior.

For a normal exit, the host logs the normal lifecycle event, runs the existing
`DisposeSession` cleanup, and closes the window. For an abnormal exit, the
existing `HandleUnexpectedExit` dialog and cleanup remain unchanged. If the
exit code cannot be read, the host preserves the conservative abnormal-exit
behavior.

## Test seam and compatibility

The policy is independently testable without constructing a WPF window or
starting ConPTY. No bridge, command-line, persistence, package, or public
protocol behavior changes.
