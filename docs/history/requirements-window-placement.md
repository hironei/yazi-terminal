# Window Placement Persistence Requirements

## Goal

Yazi Terminal must remember the display and window placement used by the host
and restore it after a restart. The behavior must remain compatible with
existing per-user settings files.

## Functional requirements

1. Persist the last display identity, normal window bounds, and normal/maximized
   state in `%LOCALAPPDATA%\\YaziTerminal\\settings.json`.
2. Keep one placement record per display observed during the current and prior
   sessions, so moving the window between displays retains each display's last
   position and size.
3. At startup, restore the record for the last-used display when it is
   connected. If it is disconnected, select a connected display with a saved
   record, then fall back to the Windows primary display/current default.
4. Restore exact saved bounds when they remain valid. Clamp stale bounds and
   sizes to the selected display's work area so the window remains visible after
   resolution, taskbar, or monitor-topology changes.
5. Do not restore a minimized state as minimized; restore its normal bounds and
   use a visible normal or maximized state.
6. Missing or malformed placement data must be ignored without invalidating
   theme, font, or color settings.
7. Existing settings files without placement data must continue to load with
   the current fixed-size startup behavior.

## Non-functional requirements

- Use the existing settings file and serializer; do not add a dependency.
- Preserve the legacy `YaziDesktopHost` namespace and bridge identifiers.
- Avoid logging full paths or other sensitive user data for placement handling.
- Capture placement without blocking Yazi startup or terminal input.
- Keep native display/window interop isolated behind a testable boundary.

## Acceptance criteria

- Unit-level tests cover round-trip serialization, per-display upsert and
  selection, malformed data, disconnected-display fallback, and bounds
  clamping.
- A Windows single-display restart check restores the prior position and size.
- A Windows two-display check restores the last-used display and preserves the
  independently observed placement when the window is moved between displays.
- `dotnet restore`, `dotnet build --no-restore`, the executable test suite, and
  `git diff --check` pass.

## Current implementation evidence

Before this change, `MainWindow.xaml` specified only fixed `Width`/`Height`
values, and no source path captured a monitor identifier or native window
placement. `HostSettings` persisted theme, font, and color values only. Thus
the requested behavior was not implemented.
