# Requirements: Serialize Host Path Requests

Issue: #26

## Goal

Prevent host-originated Yazi path operations from interleaving. In particular,
a file transaction's `reveal A` followed by `open` must complete before a
different `cd` or file request starts.

## Functional requirements

1. One shared sequencer must own every host-generated `cd` and `reveal → open`
   transaction after Yazi is ready.
2. A startup file target and a `--last-instance` file target both use the same
   `OpenFile` transaction path as settings-file editing.
3. A `--last-instance` directory request uses that same sequencer for its
   `ChangeDirectory` transaction.
4. The last-instance control server acknowledges a request only after its
   queued transaction has completed successfully; cancellation or failure
   produces its existing negative acknowledgement.

## Acceptance criteria

- A delayed fake transaction controller proves that a startup-style
  `reveal A → open A` transaction completes before a queued last-instance
  `cd B` begins.
- The same test proves the last-instance client has no successful ACK while
  its transaction is queued or delayed, and succeeds only after completion.
- Existing argument-boundary, control-pipe, bridge, and host tests remain
  passing.

## Compatibility and non-goals

The external control protocol, command-line syntax, settings format, Yazi
bridge, and `ya emit-to` command arguments do not change. This does not infer
or serialize arbitrary user-defined Yazi command-palette actions; it covers
the host's explicit path transactions.
