# Normal Yazi exit design

## Classification

`YaziProcessExitPolicy` is the single pure policy used by the WPF host. It
models a `Known(code)` exit separately from `Unknown` rather than substituting
an unavailable code with `0`. A known `0` is normal, every known non-zero code
is abnormal, and `Unknown` has its own classification. The product policy maps
both abnormal and unknown classifications to the existing unexpected-exit
dialog and cleanup; the host additionally logs the unknown category.

## Lifecycle integration

`MainWindow` handles both the terminal's `Session Terminated` marker and the
background `Process.WaitForExit` observer through one `HandleProcessExit`
method. Each path constructs the same `YaziProcessExit` model before the common
policy decides the outcome. The marker is a second lifecycle signal emitted
after the backend has waited for the child; the handler still checks
`HasExited` before reading the exit code. The wait observer supplies the same
model when it wins the race with the marker.
Easy's public `IProcess` abstraction does not expose the exit code, while the
pinned package's wrapped process exposes a `Process` property and, depending on
the backend build, a `Pid` property. The host reads either reflectively. If the
code cannot be read after either lifecycle signal, that path supplies
`Unknown`; it never substitutes normal status `0`. A failure while waiting for
the process is also represented as `Unknown` and follows the conservative
unexpected-exit action.

For a normal exit, the host logs the normal lifecycle event, runs the existing
`DisposeSession` cleanup, and closes the window. For an abnormal or unknown
exit, the existing `HandleUnexpectedExit` dialog and cleanup remain unchanged;
unknown also logs its distinct category.

## Test seam and compatibility

The policy is independently testable without constructing a WPF window or
starting ConPTY. No bridge, command-line, persistence, package, or public
protocol behavior changes.
