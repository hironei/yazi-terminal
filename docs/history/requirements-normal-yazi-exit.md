# Normal Yazi exit requirements

## Scope

Issue #7 covers the host behavior when Yazi exits from its normal `q` command.
The attached screenshot is evidence of the reported symptom, not an additional
implementation instruction.

## Functional requirements

1. A Yazi process that exits with status code `0` is a normal exit.
2. A normal exit must not show the unexpected-exit error dialog.
3. The host must still perform its existing session cleanup and close the WPF
   window after a normal exit.
4. A known non-zero exit must retain the existing unexpected-exit dialog and
   cleanup.
5. Both exit-observation paths exposed by the terminal backend (the
   `Session Terminated` output marker and `WaitForExit`) must apply the same
   known/unknown classification and normal-exit decision.
6. An unavailable exit code is `Unknown`, not status `0`. The product must log
   that category and conservatively retain the unexpected-exit dialog and
   cleanup rather than treating the exit as normal.

## Non-goals

- Do not change Yazi command-line arguments, bridge identifiers, or the
  `--last-instance` protocol.
- Do not change the wording or behavior of the unexpected-exit dialog for
  abnormal exits.
- Do not add a dependency or alter release packaging.

## Acceptance criteria

- The executable test suite verifies that known status `0` is normal, every
  known non-zero status is abnormal, and `Unknown` stays distinct across both
  observation paths.
- The source routes both terminal termination signals through that policy.
- `dotnet restore`, solution build, and the executable test suite pass.
- Manual Windows validation remains the final check for pressing `q` in the
  real Yazi/ConPTY UI.
