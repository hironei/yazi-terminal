# Host window exit requirements

## Scope

Issue #11 covers the follow-up to normal Yazi exit: pressing `q` must close
the Yazi Terminal window and terminate the host application, not leave an
empty terminal window behind.

## Functional requirements

1. A normal Yazi exit must complete host session cleanup and close the WPF
   window.
2. Cleanup for a process that has already exited must not synchronously repeat
   terminal input/process shutdown that can block the UI thread.
3. Closing the host while Yazi is still running retains the existing terminal
   shutdown path.
4. Abnormal exits retain the existing error dialog and cleanup behavior.

## Non-goals

- Do not change the Yazi command line, bridge identifiers, or last-instance
  protocol.
- Do not change abnormal-exit messaging.
- Do not change terminal backend dependencies or packaging.

## Acceptance criteria

- The executable test suite, solution build, and `git diff --check` pass.
- On Windows, pressing `q` in the real Yazi/ConPTY UI removes the Yazi Terminal
  window and the corresponding host process.
