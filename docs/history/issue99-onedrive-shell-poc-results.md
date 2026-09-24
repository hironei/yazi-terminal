# Issue 99 OneDrive Shell PoC Results

## Automated checks

- The standalone `OneDriveShellPoc` builds as part of the solution and
  `--help` starts without requiring a OneDrive installation.
- `--invoke-id` skips canonical-verb probes; enumeration and verb invocation
  use one worker per leaf command, with a configurable
  `--verb-probe-timeout-ms` (100–60000 ms, default 3000 ms).
- The repository's existing executable test suite does not reference this
  standalone tool. There are no automated unit tests for its argument parser,
  menu enumeration, resource cleanup, or invocation guards; those behaviors
  must not be described as automatically verified.
- Build, existing executable test suite, format verification, and the `--help`
  smoke test are recorded separately in the pull request validation summary.

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

The operator confirmed locally that the normal OneDrive folder menu could be
opened and that its Share action displayed the copied-link confirmation. No
menu JSON, path, or discovered identifier was retained here. This manual check
does not establish behavior on another installation.

Automated checks currently cover compilation, formatting, the existing host
test suite, and CLI help only; they do not unit-test this tool's parser,
enumeration, resource cleanup, or invocation guards. Live checks do not verify
clipboard contents or cross-environment identifier stability.

Invocation uses a null owner HWND and does not run a Windows message loop or
forward `IContextMenu2/3` messages. Handlers that need an owner window, posted
message processing, or a live context-menu lifetime can fail or appear to do
nothing. The temporary menu is destroyed as the command exits. Enumeration
also does not send `WM_INITMENUPOPUP`, so handlers that populate submenus only
when opened may not expose their nested commands in the listing. These are
limitations of this diagnostic PoC, not evidence that the host integration
will behave the same way.
