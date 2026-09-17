# Issue #95 Acceptance Record

Date: 2026-09-17

## Automated evidence

- `dotnet restore YaziDesktopHost.slnx`: PASS after allowing the configured
  NuGet source; the restricted first attempt reported NU1301 network/SSL
  access failure.
- `dotnet build YaziDesktopHost.slnx --no-restore`: PASS, zero warnings and
  zero errors.
- `dotnet run --project tests/YaziDesktopHost.Tests/YaziDesktopHost.Tests.csproj --no-build --no-restore`: PASS; all executable tests passed,
  including settings hard-link preservation, save-status notification policy,
  heartbeat/event-driven state collection, right-click release suppression, and
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
