# Phase 3 Shell Context Menu Design

## Design status

This is the first implementation design for Phase 3. It intentionally keeps
the target resolver independent from WPF and isolates all Win32/COM work in a
Shell adapter. The actual terminal right-click hook remains a manual gate
because EasyWindowsTerminalControl owns a hosted Win32 input surface.

## Boundary

```text
YaziBridgeSession
        │ StateChanged(YaziBridgeState?)
        ▼
YaziShellTargetResolver  ── pure precedence/path-kind rules
        ▼
WindowsShellContextMenu  ── PIDL, IShellFolder, IContextMenu, Win32 menu
        ▼
WPF owner HWND / Windows Shell
```

The adapter receives a state snapshot at invocation time. It must not cache a
previous actionable state after the bridge reports unavailable or disconnect.
The bridge state sequence is retained in the state object for diagnostics, but
the resolver never attempts to repair a sequence gap.

Filesystem values published by the Yazi bridge are normalized at the Shell
target boundary. Windows drive paths using `/` separators and `file:` URIs are
converted to fully qualified Windows paths before any Shell API call; relative,
non-file URI, and otherwise invalid values are rejected as unsupported. The
original bridge state remains unchanged.

## Target resolution

`YaziShellTargetResolver` returns a typed result rather than throwing for
normal unavailable/unsupported cases. It uses these rules:

1. `CurrentDirectory` selects exactly `cwd`.
2. `SelectedOrHovered` selects the complete `selected` list when it is not
   empty.
3. If there is no selection, a filesystem `hovered` item is selected.
4. A non-filesystem path in the selected set or chosen fallback is rejected.
5. `null` or unavailable state is rejected.

This makes it impossible for the Shell layer to accidentally fall back from a
mixed/unsupported explicit selection to a different hovered item.

## Shell adapter

`WindowsShellContextMenuService` is a synchronous STA-bound adapter. It owns
no Yazi state and exposes a result category (`Invoked`, `Canceled`,
`Unavailable`, `Unsupported`, `Failed`). It logs only the category and HRESULT
through `AppLogger`; it does not persist paths or menu text.

PIDLs returned by `SHParseDisplayName` are freed with the Shell allocator.
The child PIDL pointers remain valid only while their full PIDL allocations are
held. Multi-selection is passed to `IShellFolder.GetUIObjectOf` as one array.
The adapter requires all selected paths to have the same parent directory,
which matches the Shell folder contract; otherwise it returns `Unsupported`.

The menu uses an ID range reserved by the host. The selected command is
converted to an offset before `IContextMenu.InvokeCommand` is called. An
`HwndSource` hook forwards `WM_INITMENUPOPUP`, owner-draw, and menu-character
messages to `IContextMenu3` or `IContextMenu2` so registered Shell extensions
can update their submenus.

## Invocation integration

`MainWindow` locates the package's hosted `TerminalContainer` and uses its
public native `Handle` for a WndProc subclass. On `WM_RBUTTONDOWN` and
`WM_RBUTTONUP`, the host suppresses the terminal's ordinary right-click action
when the bridge is available, then queues menu creation after the release. This
is required because the WPF `MessageHook` does not guarantee interception of
messages delivered to the hosted terminal child window. A normal right-click
invokes the selected/hovered target; Shift+right-click explicitly invokes
`cwd` (the current/parent folder). Shift+F10 invokes the selected/hovered
target, while Ctrl+Shift+F10 explicitly invokes `cwd`.

The WndProc subclass handles only the right-button messages, `WM_CONTEXTMENU`,
and Shift+F10. The existing `TerminalContainer.MessageHook` remains attached
for drag-and-drop and is retained as a fallback when native subclassing is not
available. Shell API failures are logged with the operation stage and HRESULT;
paths and menu text are not persisted. The hosted HWND discovery and menu
presentation remain manual compatibility gates.

## Testing seams

- Target resolution is covered by pure tests with filesystem, URL, CJK, empty,
  mixed, unavailable, and precedence fixtures.
- Shell COM calls are not faked as a headless success. Manual Windows tests
  cover real menu creation and invocation; HRESULT failures are covered by
  the adapter's result boundary and static build checks.
