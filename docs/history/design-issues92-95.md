# Design: Issues #92-#95

## Settings persistence and notification

`HostSettingsStore.Save` resolves the final symbolic-link target as before.
For an existing target, it reads the Windows file-link count using the exact
4-byte-aligned `BY_HANDLE_FILE_INFORMATION` layout. A count greater than one
selects an in-place write so that all hard links continue to refer to the same
file record. A single-link target continues to use a temporary file and
replacement. The in-place hard-link path is intentionally not transactional:
another reader can observe a partially written file. `MainWindow` keeps the
failed-load guard, but a user-triggered save reports one short warning per
window; close-time saves only log and return. The settings-edit command does
not report a warning for its preparatory save.

## Bridge input and state collection

`MainWindow` stores the invocation selected at `WM_RBUTTONDOWN`. If that
button-down was intercepted, the matching release is always handled. The
release attempts the Shell menu using the current safe state; if freshness or
target validation now rejects it, the normal Yazi path is still suppressed and
an audible/logged feedback path explains the unavailable menu.

The Lua plugin reads `cx` state before every state or heartbeat frame. The
comparison uses `states_equal`, so unchanged reads still emit the existing
compact heartbeat frame using the last snapshot revision. If the read fails,
the loop emits no heartbeat and allows the host's freshness cutoff to expire
the last known state. This avoids depending on a selection event that is not
available in the pinned Yazi runtime while retaining the existing frame
protocol.

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
