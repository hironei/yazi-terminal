# Phase 3 Shell Context Menu Requirements

## Scope and relationship to the attached requirement

The attached file is a product requirement, not an instruction to copy its
text into the repository. This document derives the first Phase 3 slice from
its context-menu requirements and consumes only the authoritative Phase 2
bridge state.

Phase 3 adds the classic Windows Shell context menu. It does not add a second
file-manager model and it does not infer paths from terminal output.

## Goals

- Resolve a Shell target from `YaziBridgeState` without reading the terminal
  screen.
- Apply the required precedence: selected items, then hovered item, then the
  current directory for an empty-area invocation.
- Reject unavailable bridge state, stale state, and non-filesystem Yazi URLs
  before calling a Windows Shell API.
- Obtain and invoke the ordinary classic `IContextMenu` for one or more
  filesystem paths on the WPF STA thread.
- Keep Shell COM/HRESULT failures at the feature boundary so they cannot crash
  the host or the bridge receive loop.

## Non-goals for this slice

- Windows 11 modern menu reconstruction or custom menu entries.
- Explorer/Desktop drag-and-drop; that remains Phase 4/5.
- Shell file-operation policy, copy/move logic, or a replacement for
  `IFileOperation`.
- Parsing VT output, using terminal coordinates to infer a path, or modifying
  Yazi selection behavior.
- Cross-parent multi-selection when the Shell provider cannot create one
  `IContextMenu` for all items. The adapter reports this as an unsupported
  target instead of silently reducing the selection.

## Target contract

The target resolver accepts an available bridge state and one invocation kind:

| Invocation | Target |
| --- | --- |
| Selected-or-hovered | all selected paths when selection is non-empty; otherwise hovered |
| Current-directory | `cwd` only |

Every path in the chosen target set must have `kind = filesystem`. A URL, an
unavailable state, an empty target, or an invalid path set is not actionable.
The resolver converts fully qualified Windows-compatible values, including
forward-slash drive paths and `file:` URIs, to Windows path syntax. Relative,
non-file URI, and invalid values are rejected; the resolver does not require
the path to exist.

## Shell API contract

The adapter uses the classic Shell sequence:

1. Parse each filesystem path into a PIDL.
2. Bind the first PIDL to its parent `IShellFolder` and retain each child PIDL.
3. Request `IContextMenu` with the selected child PIDLs.
4. Call `QueryContextMenu`, display it with `TrackPopupMenuEx`, and invoke the
   selected command with `InvokeCommand`.
5. Release the COM interfaces and PIDLs in a `finally` boundary.

The owner HWND is the WPF window. The call is synchronous on the UI/STA thread
because Shell extensions may display UI and require the owner message loop.
The adapter forwards the standard owner-draw and submenu messages to
`IContextMenu2`/`IContextMenu3` while the menu is open. When `IContextMenu3`
handles a message successfully, the host returns its `HandleMenuMsg2` LRESULT
to the WPF message hook; it must not replace that result with zero.

## Acceptance criteria

- Pure tests cover all target precedence and rejection cases, including CJK
  paths and URLs.
- Static/build verification succeeds without adding a COM NuGet dependency.
- On Windows, a manual run opens the classic Shell menu for a hovered file,
  a selected file, multiple selected files in one folder, and `cwd`.
- The selected item set is passed as a set; it is never reduced to the
  hovered item when selection exists.
- A Shell HRESULT/COM exception produces a bounded failure result and a
  category log, without terminating the host or bridge.
- A fake message-forwarding seam verifies that a successful
  `IContextMenu3.HandleMenuMsg2` call both marks the WPF message handled and
  propagates its LRESULT, while a failing HRESULT remains unhandled.
- Cross-parent multi-selection and third-party extension behavior are reported
  as manual compatibility results, not claimed from headless tests.

## Remaining manual gates

- Confirming that the `TerminalContainer` native `Handle` WndProc subclass
  receives `WM_RBUTTONDOWN`/`WM_RBUTTONUP` and `WM_CONTEXTMENU` from the
  EasyWindowsTerminalControl hosted HWND without breaking Yazi mouse
  reporting. The implementation suppresses the ordinary right-click action
  only while bridge state is available, then reads the latest bridge state
  before showing the Shell menu. The WPF `MessageHook` remains the fallback
  when native subclassing is unavailable.
- Confirming Shift+F10 and Ctrl+Shift+F10 behavior when the terminal owns
  keyboard focus. The former targets selected/hovered and the latter targets
  `cwd`.
- Third-party extensions such as TortoiseGit/TortoiseSVN, owner-draw menus,
  submenu message forwarding, elevation prompts, and Windows 11 presentation.
- Behavior when Yazi reports a path that disappears between bridge publication
  and Shell invocation.
