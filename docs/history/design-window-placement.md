# Window Placement Persistence Design

## Data model

Extend the existing `HostSettings` model with optional `WindowPlacement` data:

- `LastMonitorId`: the Windows display device name selected for the most recent
  placement, for example `\\\\.\\DISPLAY1`.
- `Monitors`: a bounded list of per-display records.
- Each record contains the monitor id, the native normal-position rectangle in
  screen pixels, and a visible/maximized state.

The placement records are optional and independently parsed. Invalid records
are discarded; if no valid record remains, placement is treated as absent.
Older settings files therefore remain valid, and invalid placement does not
discard unrelated appearance settings.

## Runtime flow

1. `MainWindow` loads `HostSettings` before `InitializeComponent` and retains
   the optional placement model.
2. After the first render, `WindowPlacementNative` enumerates connected
   displays, selects the last-used connected display (or a deterministic
   fallback), clamps the saved normal rectangle to that display's work area,
   and applies it with native `SetWindowPos`/`ShowWindow` calls. This keeps
   saved physical-pixel geometry correct on per-monitor DPI configurations.
3. `LocationChanged`, `SizeChanged`, and `StateChanged` capture the current
   native placement into the in-memory per-display list. This records a display
   when the window is moved between displays without writing the settings file
   for every pixel of a drag.
4. During `Closing`, the current native placement is captured and the complete
   appearance plus placement settings are saved once.

## Native boundary

`WindowPlacementNative` owns the Win32 interop for `GetWindowPlacement`,
`EnumDisplayMonitors`, `GetMonitorInfo`, `MonitorFromWindow`, `SetWindowPos`,
and `ShowWindow`. It converts native structures to immutable domain values.
The restore is deferred until after WPF's initial layout because WPF can
overwrite native placement while the window is being initialized.
The domain catalog owns selection, upsert, validation, and work-area clamping,
which keeps tests independent from a live multi-monitor desktop.

The monitor identifier is the Windows display device name. The saved work-area
rectangle is not used as identity; it is only used at restore time to validate
and clamp placement. If a monitor is missing, the selected fallback is used and
the persisted monitor records are retained for a later reconnection.

## Error handling and compatibility

Interop failures are non-fatal: the default WPF placement remains in effect and
the settings file is still usable for appearance settings. Settings write
failures follow the existing `HostSettingsStore` logging behavior. No new user
data beyond window geometry and display device names is logged.

## Traceability

| Requirement | Design coverage |
| --- | --- |
| Persist display, bounds, and state | Optional `HostSettings.WindowPlacement`; close capture |
| Per-display history | In-memory capture events plus catalog upsert |
| Restore and fallback | Connected monitor enumeration and selection |
| Visibility after topology changes | Work-area clamp before native apply |
| Minimized handling | Capture maps minimized to visible normal state |
| Compatibility and malformed data | Optional independent parser and validation |
| No dependency / testability | Existing serializer and isolated native boundary |
