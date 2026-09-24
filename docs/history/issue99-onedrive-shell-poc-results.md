# Issue 99 OneDrive Shell PoC Results

## Automated checks

- The standalone `OneDriveShellPoc` builds as part of the solution.
- `--help` documents enumeration and explicit invocation modes.
- Invalid and ambiguous invocation targets are rejected before
  `InvokeCommand`.
- The solution build, executable tests, and format verification are recorded
  in the pull request validation summary.

## Identifier policy and conclusion

The PoC is a local discovery and verification tool, not a source of
repository-wide OneDrive identifiers. `QueryContextMenu` assigns offsets for
the current menu; those offsets are not persistent settings. Microsoft
documents canonical verbs as language-independent names supplied by a menu
handler and accepted by `InvokeCommand`, but does not promise that a
provider-specific value is stable across handler versions or environments.
The product therefore leaves these values unset by default and stores any
operator-discovered values only in that user's local settings file.

## Repeatable local procedure

1. Use the exact resolved filesystem target that the host will pass to Shell.
2. Run `dotnet run --project tools/OneDriveShellPoc -- "<target>" --json`.
3. Inspect locally and consider only entries with a successful canonical-verb
   status. Do not infer a verb from a localized menu label.
4. Test a candidate explicitly with `--invoke-verb <candidate>` and confirm
   the expected action in that local session.
5. Keep the JSON, target path, menu labels, and canonical-verb values local.
   Do not paste them into GitHub Issues, pull requests, logs, or public reports.

Repeat the procedure where behavior needs confirmation, such as for a file and
a folder. A result on one installation does not establish behavior on another.
Public repository history intentionally contains no environment-specific
menu output or discovered identifier values.

## Acceptance boundary

Automated checks validate parsing, enumeration, resource cleanup, and explicit
invocation guards. They do not establish live OneDrive registration, menu
contents, successful Share/Copy Link behavior, clipboard contents, or
cross-environment identifier stability. These remain operator-local checks.
