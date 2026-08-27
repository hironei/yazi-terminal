# Issue #49 normal `q` exit design

## Exit-code acquisition

`MainWindow` already captures an OS `System.Diagnostics.Process` from the
backend process identifier before waiting for termination. Every exit-code
read path now calls `WaitForExit()` on that OS process immediately before
reading `ExitCode`. This closes the race in which the backend reports its
termination or disposes its wrapper before the OS process object has refreshed
its exit state.

The existing reflective fallbacks remain in place for backend versions that
expose only `Process` or `Pid`. If all reads still fail, the caller creates an
`Unknown` exit and retains the existing conservative error handling.

## Lifecycle and cleanup

The process-monitor path distinguishes a completed wait from a failed wait.
Known status `0` and a completed process monitor are normal, known non-zero
statuses are abnormal, and `Unknown` remains distinct. If exit-code reading
fails after `WaitForExit()` has completed, the host uses the completed-monitor
state so a normal `q` shutdown does not become a false dialog. A failed wait or
an unavailable terminal-marker exit remains `Unknown` and retains the existing
dialog and cleanup path.

## Test seam

The executable test suite starts a short-lived Windows process and invokes the
internal exit-code reader with that process object. This verifies that the
reader waits for process termination before accessing `ExitCode` without
constructing the WPF window or ConPTY session.
