# Design: Issues #92-#95

## Settings persistence and notification

`HostSettingsStore.Save` resolves the final symbolic-link target as before.
For an existing target, it reads the Windows file-link count. A count greater
than one selects an in-place write so that all hard links continue to refer to
the same file record. A single-link target continues to use a temporary file
and replacement. `MainWindow` keeps the failed-load guard, but a user-triggered
save reports one short warning per window; close-time saves only log and return.

## Bridge input and state collection

`MainWindow` stores the invocation selected at `WM_RBUTTONDOWN`. If that
button-down was intercepted, the matching release is always handled. The
release attempts the Shell menu using the current safe state; if freshness or
target validation now rejects it, the normal Yazi path is still suppressed and
an audible/logged feedback path explains the unavailable menu.

The Lua plugin subscribes to Yazi DDS events that can change the exported
manager state. The callbacks only mark plugin state dirty. The asynchronous
bridge loop collects `cx` state on the initial snapshot or after a dirty event;
otherwise it emits the existing compact heartbeat frame using the last snapshot
revision. This retains compatibility with the current frame protocol and keeps
the expensive selected-path enumeration off unchanged heartbeat iterations.

## Last-instance user feedback

`App.OnStartup` keeps the existing result classification: only
`Rejected` proceeds to create a new window, `Accepted` exits after delivery,
and `Unknown` logs `last_instance_handoff_unknown`, shows a short message, and
exits without creating a duplicate window. The notification contains no path.

## Verification and live boundary

The executable tests cover deterministic protocol, persistence, reducer, and
classification behavior. Windows GUI, ConPTY environment inheritance,
foreground-lock behavior, Explorer/Yazi drag and Shell operations, symbolic
link privileges, and concurrent real processes remain live acceptance gates and
are recorded separately in the Issue #95 history record.
