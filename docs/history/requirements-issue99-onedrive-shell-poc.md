# Issue 99 OneDrive Shell PoC Requirements

## Scope

Provide a standalone Windows .NET CLI for measuring the classic Windows Shell
context menu exposed for one filesystem path. The CLI is a diagnostic tool for
Issue #99 and is not integrated into Yazi, the bridge protocol, or the product
runtime.

## Functional requirements

- Accept one filesystem path and create its classic `IContextMenu` through
  `SHParseDisplayName`, `SHBindToParent`, and `IShellFolder.GetUIObjectOf`.
- Enumerate nested menu entries and report command ID, command offset, menu
  text, submenu status, and canonical verb lookup status/value.
- Invoke one selected leaf command by command ID or by an unambiguous canonical
  verb only when the corresponding option is explicitly supplied.
- Keep enumeration as the default; never infer invocation from menu text.
- Support human-readable text and structured JSON output for local inspection.
- Return nonzero exit codes for invalid arguments, Shell/COM failures, and
  unknown or ambiguous invocation targets.

## Non-functional requirements and safety

- Target Windows x64/.NET 10 without adding a COM or NuGet dependency.
- Keep the tool isolated under `tools/`; leave product runtime and bridge
  protocol unchanged.
- Do not write paths, shared URLs, clipboard contents, or menu output to a
  persistent application log. Console output is selected by the operator and
  must be treated as local diagnostic data.
- Release COM interfaces, PIDLs, unmanaged menu handles, and command buffers
  on every success and failure path.
- Use an STA entry point for Shell extensions that require COM STA behavior.

## Acceptance criteria

- The CLI builds as part of the solution and `--help` is available without a
  OneDrive installation.
- A real filesystem path can be enumerated without modifying the host or
  bridge.
- Every leaf command records lookup success, empty result, or lookup failure
  explicitly; only one successful unambiguous verb may be invoked.
- The local procedure supports file/folder and Shell action checks without
  requiring environment-specific values to be committed.
- The public record contains no raw JSON output, target path, localized menu
  dump, or discovered environment-specific identifier.
- Build, executable tests, formatting, and `git diff --check` pass. Live Windows
  Shell/OneDrive checks are reported separately from automated validation.
