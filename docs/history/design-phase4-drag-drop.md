# Phase 4 Explorer/Yazi Drag-and-Drop Design

## Design status

This design resolves the Phase 4 contract before implementation. It keeps
state selection, terminal input interception, OLE registration, and Shell
operation delegation in separate boundaries.

## Boundaries

```text
YaziBridgeSession
        │ latest state
        ▼
YaziShellTargetResolver ── selected/hovered/cwd and filesystem validation
        │
        ├── YaziDragSource ── CF_HDROP ──► Explorer/Desktop
        │
        └── YaziOleDropTarget ◄── OLE ── Explorer/Desktop
                    │ latest cwd
                    ▼
             Shell folder IDropTarget
```

`MainWindow` owns the lifetime and attaches both directions only after the
terminal HWND exists. It detaches the message hook, revokes the OLE target,
and releases Shell COM objects before disposing ConPTY and bridge state.

## Yazi to Explorer source

`MainWindow` observes only the native terminal messages required to recognize a
left-button drag: button-down stores the origin, mouse-move checks the
platform drag threshold while the button is held, and capture/cancel/up clears
the pending source state. It does not handle ordinary messages before a drag
starts.

When the threshold is crossed, the host resolves the latest
`SelectedOrHovered` target. It creates a WPF `DataObject` with the complete
filesystem path set and calls the standard WPF OLE drag loop with Copy and
Move effects. The drag loop returns the negotiated effect; the host does not
perform the operation itself. A pending flag prevents re-entry while the OLE
loop is active.

## Explorer to Yazi target

The host registers a COM-visible `IDropTarget` on the actual native terminal
HWND. Because the package keeps the terminal HWND property internal, the
adapter isolates the package-specific handle lookup and reports an unavailable
feature if that lookup changes or returns zero.

For `DragEnter` and `DragOver`, the adapter resolves the latest `cwd` and
forwards the original OLE data object to the Shell folder's `IDropTarget`.
The Shell target computes the allowed effects. If the bridge is unavailable,
the destination is non-filesystem, or the Shell target cannot be created, the
adapter returns `DROPEFFECT_NONE`.

For `Drop`, the adapter forwards the final key state, point, and effect to the
same Shell target and then releases it. `DragLeave` releases the target
without touching files. The Shell folder therefore owns copy/move/link
semantics, prompts, cancellation, and provider-specific behavior.

## Error, lifetime, and logging rules

- `RegisterDragDrop`, `RevokeDragDrop`, COM activation, and `IDropTarget`
  failures are caught and logged as categories without paths.
- A failed source drag or target drop is bounded; it cannot escape the WPF
  message hook or bridge receive loop.
- OLE registration is revoked before the terminal HWND is destroyed.
- The service never caches an actionable bridge state across disconnect; every
  drag start and target callback resolves current state.

## Test seams

- Existing pure target-resolution tests cover source and cwd precedence.
- A small source-target helper is tested for selected/hovered and rejection
  behavior without starting WPF, OLE, Explorer, or COM Shell extensions.
- Build verification covers COM interop declarations and WPF integration.
- Real Explorer/Desktop interaction, Shell effects, and native HWND behavior
  remain manual acceptance gates.
