# Requirements: Issues #85-#90

## Scope

Implement the accepted follow-up fixes from Issues #85-#90 without changing
the bridge identifiers or the pinned Yazi/terminal dependencies. The changes
must preserve existing settings, shell, last-instance, logging, environment,
and path-request behavior except where the issues explicitly require safer
failure handling.

## Functional requirements

- A failed read/parse of an existing settings file must not be cleared merely
  because a later close-time read succeeds. A failed state is cleared only by
  a valid reload that is applied to the in-memory settings. An initial missing
  file remains saveable.
- Settings replacement must not silently replace a file symlink with a regular
  file. Save through a resolved link target and reject unresolved or unsafe
  links.
- Bridge state freshness must use a monotonic clock. Shell context-menu
  interception must check the same freshness predicate at button-down and
  release; stale state must not consume the normal Yazi input path. The
  freshness policy must be negotiated through the hello `heartbeat`
  capability so older plugins remain usable without heartbeat traffic.
- Unchanged bridge state must use a compact heartbeat-marked existing `state`
  frame rather than rebuilding and sending the complete selection list. A
  heartbeat may refresh freshness only when it follows a successful state read
  and matches the host's current revision.
- Last-instance startup must not synchronously block the WPF startup thread.
  Server failures must be logged and clean up their endpoint. Only an explicit
  negative acknowledgement may trigger a new-window fallback; a null,
  malformed, timeout, or ACK loss after the request is fully written is an
  unknown handoff and must not create a second window. Failures before the
  request write completes remain rejected.
- File opening must explicitly open the hovered target after reveal.
- The fixed EasyWindowsTerminalControl 1.0.38 process-order evidence and the
  remaining live acceptance boundary must be recorded in history documentation.
- Logging rotation and append must be inter-process exclusive and recover from
  abandoned/timeout mutex ownership without recursively logging logger errors.

## Non-functional and security requirements

- Preserve the existing protocol names, current-user pipe ACL, and old client
  behavior where possible. Do not add broad permissions or dependency updates.
- Request, settings, and log paths must remain bounded and must not expose
  path contents in new diagnostics.
- Automated tests must cover normal, malformed, stale, timeout, capability
  negotiation, and recovery cases. GUI, foreground activation, symlink
  privilege, ConPTY inheritance,
  and Explorer/Yazi live behavior remain explicit manual gates.

## Acceptance criteria

The repository test executable passes; the solution builds; diff and formatter
checks pass; tests cover settings failed/reload/symlink policy, monotonic
freshness and stale mouse fallback, `open --hovered`, async last-instance
entry behavior, server loop recovery, and concurrent logger rotation. The
documentation states all unverified live gates.
