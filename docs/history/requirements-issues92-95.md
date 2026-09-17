# Requirements: Issues #92-#95

## Scope

Implement the accepted follow-up behavior for Issues #92-#94 and record the
Windows acceptance boundary tracked by Issue #95. Preserve the existing bridge
protocol identifiers, current-user pipe boundary, pinned Yazi/terminal
dependencies, and the no-duplicate-window policy for uncertain last-instance
handoffs.

## Functional requirements

- When settings persistence is skipped because the settings file failed to
  load, an interactive settings change must give the user a one-time visible
  notification. Close-time persistence must remain non-interactive.
- Saving through a symbolic link must preserve the link. Saving an existing
  hard-linked settings file must preserve the shared file identity; ordinary
  files retain the existing temporary-file replacement behavior.
- Settings-save tests must exercise the load-status decision and verify the
  hard-link target and link count after saving.
- A right-click intercepted at button-down must suppress the matching button-up
  even if bridge state becomes stale between the two messages. The Shell menu
  may be skipped when the state is no longer safe, but the normal Yazi input
  path must not receive an unmatched release.
- The bridge plugin must read the complete current Yazi state before every
  state or heartbeat frame so selection-only changes are observed even when no
  selection event is available. A heartbeat may be sent only after that read
  succeeds; a failed read must not refresh the freshness of an older state.
- An unknown `--last-instance` handoff result must remain a no-duplicate-window
  outcome and must visibly tell the user that delivery could not be confirmed.
- Issue #95 acceptance evidence must distinguish automated coverage from live
  ConPTY, foreground, Explorer/Yazi, symlink, and multi-process behavior.

## Non-functional and compatibility requirements

- Do not add dependencies or change the bridge frame schema beyond the
  existing heartbeat capability and marker.
- Keep diagnostics path-free and bounded. Notifications must not expose target
  paths or settings contents.
- Preserve legacy plugins without the heartbeat capability and preserve the
  existing explicit-negative-acknowledgement fallback.

## Acceptance criteria

- The solution builds and the executable test suite passes.
- Tests cover settings status decisions, normal-file and hard-link counts,
  right-click interception state retention, pre-heartbeat state reads, and Unknown
  handoff notification/fallback classification.
- `git diff --check` passes and no unrelated user files are changed.
- History documentation records every live check that was not executable in the
  current environment.
