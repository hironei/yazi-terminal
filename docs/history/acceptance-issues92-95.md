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

## Live acceptance run (2026-10-03)

Environment: Windows 11, Debug build of this branch launched from the build
output, the scoop Yazi install, the installed user's Yazi config and
`%LOCALAPPDATA%\YaziTerminal` data. The Computer Use tools were not exposed in
this session, so only checks drivable by scripts (process inspection, Win32
foreground state, UI Automation window enumeration, `SendKeys`) were run.

| Item | Result | Evidence |
| --- | --- | --- |
| #88 bridge environment reaches the Yazi child | PASS | Both Yazi processes under the host carried `YAZI_DESKTOP_HOST_PIPE`, `_INSTANCE_ID`, `_PROTOCOL`, `YAZI_CONFIG_HOME`, `COLORTERM`, `TERM`; the host process itself held none of the `YAZI_DESKTOP_HOST_*` values afterwards. |
| #88 hello/snapshot | PASS | After key input the host logged `bridge_Available`. |
| #88 reconnect; non-inheritance by an unrelated host-launched child | UNVERIFIED | Not exercised. |
| #87 `--last-instance` foreground | PASS (partial) | With the host minimized and another window in front, the second process exited 0 in ~0.6 s, no second host process, and the host became the foreground window. A forced foreground-lock state was not reproduced. |
| #87 no duplicate window for a hung instance | FAIL, then fixed | With the existing host suspended, the second process opened a full second window after the 6 s timeout. Cause: `connected` was set only after the request write completed, and the unbuffered pipe write to a hung server times out first, which classified the result as Rejected. Fixed by treating any accepted connection as owning the request. After the fix the second process logged `last_instance_handoff_unknown`, showed only the warning dialog, and opened no main window. |
| #87 new window when no instance is reachable | PASS | With a stale `last-instance.json` and no running host, `--last-instance` opened a normal window. |
| #87 explicit negative acknowledgement | UNVERIFIED | Covered by automated tests only. |
| #86 heartbeat (new and legacy plugin), stale right-click | UNVERIFIED | Needs Shell menu interaction. |
| #90 `open --hovered` | UNVERIFIED | Not exercised. |
| #85 symlink preserved on save | UNVERIFIED | The installed `settings.json` is a symlink and stayed intact and unmodified during the run; no save was triggered to avoid altering the user's settings. |
| #89 concurrent real-process log rotation | UNVERIFIED | Only the automated concurrent-append and rotation tests ran. |

New regression test: `last-instance client treats a stalled request write as
unknown`. Build: 0 warnings, 0 errors; test suite: all 123 tests pass.
