# Last-instance command routing requirements

## Goal

Allow an explicit command-line invocation to reuse the most recently launched
Yazi Terminal window while preserving the existing new-window behavior.

## Scope

- An invocation without `--last-instance` creates a new WPF window.
- A positional directory is used as the initial directory of a new window.
- `--last-instance [directory]` targets the most recently launched instance.
- If the target is missing, stale, or unreachable, the invocation starts a new
  window with the requested directory instead of returning an error.
- A fallback-created window becomes the new last instance.
- The reused window activates and changes its active Yazi tab to the requested
  directory.

## Non-goals

- Reusing the current Git Bash or other terminal pane.
- Opening a new Yazi tab in the reused window.
- Changing the existing Yazi state bridge contract.
- Supporting remote or cross-user instance control.

## Functional requirements

1. The parser accepts a positional directory and the `--last-instance` option.
2. Normal startup remains a new window, including when no last instance exists.
3. Each host instance publishes a current-user-scoped control endpoint and
   records it as the last launched instance.
4. A last-instance request sends a bounded, framed, validated directory command
   to the recorded endpoint, with one deadline covering connection, request,
   and acknowledgement.
5. The host dispatches a request to the WPF thread, serializes directory
   actions, activates its window, and acknowledges success only after Yazi's
   documented `cd` action succeeds for the active tab.
6. Missing, malformed, stale, or unreachable endpoint metadata falls back to
   normal new-window startup.
7. Closing an instance must not clear a newer instance's endpoint metadata.
8. Directory values are treated as data and are never interpolated into a shell
   command line.

## Acceptance criteria

- Automated tests cover argument parsing, endpoint publication/removal,
  control-message framing/validation, bounded reads, negative acknowledgements,
  timeout/unreachable fallback decisions, and registry publication updates.
- Existing executable-resolution, bridge, shell, and theme tests remain passing.
- README and the user manual document `--last-instance` and its fallback.
- A Windows manual run verifies reuse, foreground activation, directory change,
  fallback startup, CJK/spaces in paths, and no second window on reuse.
- Automated results and live GUI/Yazi results are reported separately.
