# Design: Serialize Host Path Requests

Issue: #26

## Shared transaction boundary

`YaziPathRequestSequencer` owns one asynchronous `SemaphoreSlim` and accepts
only two typed requests: `ChangeDirectory` and `OpenFile`. It delegates the
whole typed request while holding the gate. Its production controller invokes
the existing `YaziDirectoryController` for `cd` and `YaziFileController` for
the complete `reveal → open` sequence, so the gate spans both `ya` commands.

`MainWindow` creates the sequencer after resolving the paired `ya.exe` and
client ID. It uses the same instance for startup initial-file opening,
last-instance directory/file requests, and settings-file editing. It retains
the existing readiness, shutdown, error logging, and foreground-activation
checks outside the controller boundary.

## ACK and ordering lifecycle

```text
startup OpenFile(A) ─┐
last-instance cd(B) ─┼─> YaziPathRequestSequencer ─> Yazi process controller
settings OpenFile(S) ─┘
```

The last-instance server already awaits its `RequestReceived` handler before
writing an ACK. The handler now awaits the shared sequencer; therefore an ACK
cannot precede either waiting for an earlier request or completion of its own
`cd`/`reveal → open` work. A canceled wait or failed controller operation
returns `false`, preserving the negative-ACK and new-window fallback behavior.

## Test seam

The sequencer depends on `IYaziPathTransactionController`. An executable test
uses a delayed fake controller with the real control server/client: it starts a
startup-style file request, queues a last-instance `cd`, observes no interleaved
operation and no ACK, then releases each transaction and verifies order and
the final ACK. Real WPF dispatch, foreground activation, `ya.exe`, and Yazi
state changes remain manual Windows acceptance checks.
