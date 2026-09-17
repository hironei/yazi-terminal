# Issue #95 Acceptance Record

Date: 2026-09-17

## Automated evidence

- `dotnet restore YaziDesktopHost.slnx`: PASS after allowing the configured
  NuGet source; the restricted first attempt reported NU1301 network/SSL
  access failure.
- `dotnet build YaziDesktopHost.slnx --no-restore`: PASS, zero warnings and
  zero errors.
- `dotnet run --project tests/YaziDesktopHost.Tests/YaziDesktopHost.Tests.csproj --no-build --no-restore`: PASS; all executable tests passed,
  including settings normal-file and hard-link counts, save-status notification
  policy, pre-heartbeat state collection, right-click release suppression, and
  Unknown last-instance classification.
- `dotnet format YaziDesktopHost.slnx --no-restore --verbosity minimal`: PASS.
- `git diff --check`: PASS.

## Live acceptance boundary

The following remain UNVERIFIED in this run:

- ConPTY/Yazi child inheritance of the bridge environment, hello/snapshot,
  reconnect, and non-inheritance by unrelated child processes.
- `--last-instance` foreground activation, including Windows foreground-lock
  conditions.
- No duplicate window on a slow or non-responsive existing instance, and the
  explicit-negative-acknowledgement new-window fallback.
- New/legacy heartbeat behavior, stale right-click fallback, and Explorer/Yazi
  Shell context-menu and drag-and-drop behavior.
- `open --hovered` against a live Yazi instance.
- Symbolic-link preservation using the installed user's settings path and
  concurrent real-process log rotation.

The Computer Use inventory exposed no targetable Windows applications in this
session, so these live checks could not be performed. The automated suite does
not substitute for them.

## Post-review corrections

The initial implementation incorrectly depended on a non-existent `select`
event and could refresh stale selection with a heartbeat. The corrected plugin
reads manager state before every emitted frame and emits no heartbeat when that
read fails. The initial Win32 file-information declaration also used 8-byte
`long` time fields, which shifted `NumberOfLinks`; it now uses the native
4-byte field layout, and the tests require ordinary files to report exactly one
link and fail if the hard-link fixture cannot be created.
