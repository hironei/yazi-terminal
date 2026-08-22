# Phase 5 Explorer-to-Yazi Drop Design

## Boundary

```text
Explorer / Desktop OLE source
            │ IDataObject + effects
            ▼
WindowsShellDragDropService
            │ latest bridge cwd
            ▼
IShellFolder.GetUIObjectOf(IID_IDropTarget)
            │
            ▼
Windows Shell folder drop target
```

The Phase 4 service owns the native HWND registration and COM callback
lifetime. Phase 5 owns the destination-side contract and does not introduce a
second WPF drop-event path, which would be unreliable across the terminal's
HwndHost airspace boundary.

## Callback behavior

- `DragEnter` releases any prior target, resolves `cwd`, obtains the Shell
  folder's `IDropTarget`, and forwards the original data object and key state.
- `DragOver` forwards the current point, key state, and effect to the active
  Shell target. If the target is unavailable, it returns `DROPEFFECT_NONE`.
- `Drop` resolves `cwd` again immediately before the operation. If the
  directory changed during the drag, the old target is released and a fresh
  Shell target receives `DragEnter` followed by `Drop`.
- `DragLeave` releases the target without touching files.

`SHParseDisplayName` and `SHBindToParent` obtain the directory PIDL and parent
folder. `IShellFolder.GetUIObjectOf` is requested with `IID_IDropTarget`; all
PIDLs and COM interfaces are released in bounded cleanup paths.

## Error and security boundary

The host never reads the incoming file list, writes files, or persists paths.
It passes only the original OLE data object to the Shell provider selected for
the validated current directory. Any COM/OLE exception becomes
`DROPEFFECT_NONE` and a category log. OLE registration is revoked before
ConPTY and the terminal HWND are disposed.

## Verification seams

- The existing `YaziShellTargetResolver` tests cover current-directory
  filesystem, URL, unavailable, and empty cases.
- The WPF build verifies the COM declarations and integration wiring.
- A real Explorer/Desktop drag is required to verify effect negotiation,
  prompts, cancellation, and provider behavior; it cannot be proven by the
  headless test executable.
