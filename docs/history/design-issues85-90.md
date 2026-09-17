# Design: Issues #85-#90

## Settings and logging

`HostSettingsStore` resolves only the final settings-file link before writing,
keeps temporary files beside the resolved target, and replaces the target
without unlinking the user-facing symlink. `MainWindow` tracks whether the
current settings came from a valid disk load; close-time probing cannot clear a
prior failed-load guard. `AppLogger.AppendLine` takes a per-log named mutex
around size check, rotation, and append.

## Bridge freshness

`YaziBridgeState` stores the receive timestamp as a monotonic timestamp from a
shared `TimeProvider`. The resolver compares elapsed time with
`TimeProvider.GetElapsedTime`. The Lua bridge emits a full snapshot initially,
full state only after a changed structured state, and a compact heartbeat-marked
`state` frame carrying the last state sequence otherwise. Reusing the existing
`state` kind keeps older hosts from disconnecting on an unknown message kind.
The reducer accepts the marker only after a snapshot and updates freshness
without changing paths; a mismatched revision is a connection error. The
native right-click down path uses resolver status, and a release that has
become stale is forwarded to normal input.

## Last instance and file opening

The WPF app starts the last-instance send asynchronously before creating the
main window. The client distinguishes accepted, explicitly rejected, and
unknown-after-write results; only explicit rejection creates a new window, so
an ACK loss cannot duplicate a request in a second window. The control server
has an outer fault boundary, logs failures, and removes its registry entry in
cleanup. Registry metadata carries the target PID and the sender requests
foreground permission with `AllowSetForegroundWindow`; Windows foreground-lock
failures remain a manual gate. `YaziFileController` sends `open --hovered`
after a successful reveal. A completion result is logged server-side but is not
sent as a second wire frame for compatibility with the existing protocol.

## Environment evidence

The fixed NuGet 1.0.38 repository commit is documented as creating the child
process before firing `TermReady` in the normal path. A live bridge-inheritance
trial remains required before claiming complete acceptance; no dependency or
package API change is made here.
