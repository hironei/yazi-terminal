# Issue #49 normal `q` exit requirements

## Scope

Issue #49 covers the false unexpected-exit dialog observed in Yazi Terminal
v1.0.1 when Yazi is closed with its normal `q` command. The supplied log shows
that process termination was detected, but reading the OS process exit code
raised `InvalidOperationException` and was classified as `Unknown`.

## Functional requirements

1. A normal Yazi `q` exit with status code `0` must not show the unexpected-exit
   dialog.
2. The host must wait for the OS process to reach an exited state before
   reading its exit code when the backend lifecycle signal arrives first.
3. A known non-zero exit code must continue to show the existing unexpected-exit
   dialog and perform existing cleanup.
4. If the backend process monitor has successfully completed but the exit code
   is unavailable, the host must treat that lifecycle completion as normal. A
   failed process wait or an unavailable terminal-marker exit code remains
   `Unknown` and retains the conservative unexpected-exit behavior.
5. Normal exit cleanup must close the host window and terminate the application
   without synchronously sending shutdown input to an already-exited process.

## Non-goals

- Do not change Yazi command-line arguments, bridge identifiers, or the
  `--last-instance` protocol.
- Do not change the wording of the unexpected-exit dialog.
- Do not change dependencies or release packaging.

## Acceptance criteria

- The exit-code reader waits for the OS process before reading `ExitCode`.
- The executable test suite covers the OS process exit-code read and preserves
  known normal, known abnormal, and unknown classifications.
- Solution build and the executable test suite pass.
- Manual Windows validation confirms that pressing `q` in real Yazi closes the
  host without the unexpected-exit dialog.
