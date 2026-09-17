# Design: Issues #72-#83

## Settings

HostSettingsStore.LoadWithStatus distinguishes a missing file from a failed
read or parse. MainWindow preserves a failed existing file and skips its
close-time save until a later valid reload succeeds. Saves serialize to a
same-directory temporary file and then replace the target, with cleanup in a
finally block.

## Bridge and Shell safety

The plugin emits an initial snapshot and periodic state frames, including when
the visible state is unchanged. The reducer stamps every accepted
snapshot/state update with the host receive time. YaziShellTargetResolver
rejects available states older than one second.
A reducer rejection is surfaced to YaziBridgeSession, which disposes the
connection and allows the Lua retry loop to establish a fresh handshake. The
command catalog parser skips invalid entries while retaining the rest.

## Process and diagnostics

The last-instance server uses a five-second request deadline and the startup
client uses a six-second total deadline. Bridge environment variables are
restored when TermReady proves the Yazi child has been started. The WPF
dispatcher, AppDomain, task scheduler, and bridge receive loop have bounded
error paths. app.log keeps one approximately 1 MiB active file and one
rotated generation.

## Deferred compatibility items

No protocol change is introduced to correlate pointer coordinates with bridge
snapshots, distinguish terminal text selection from host drag initiation, or
guarantee foreground activation under every Windows foreground-lock state.
Those behaviors remain documented manual gates rather than speculative native
input changes.
