# Host window exit design

`HandleProcessExit` already knows when the Yazi process has exited and routes
normal status through `DisposeSession`. That path now passes
`processAlreadyExited: true`. `DisposeSession` still disconnects the terminal,
clears the host visual tree, disposes the bridge environment, and removes the
last-instance server, but skips `CloseStdinToApp` and `StopExternalTermOnly`
when the child has already terminated.

The skipped calls are retained for the regular `Window.Closing` path, where
Yazi may still be running and the host must request terminal shutdown. This
keeps the change limited to the normal `q` exit path: the process monitor can
return to the dispatcher, the WPF window can close, and application shutdown
can complete.

No bridge, command-line, persistence, package, or public protocol behavior
changes. The final acceptance check is a real Windows run because the blocking
behavior is in the ConPTY terminal backend and cannot be proven by the
executable unit-test suite alone.
