# Phase 4 Explorer/Yazi Drag-and-Drop Requirements

## Scope and interpretation

The attached product requirement is a product specification, not an
instruction to copy or implement every proposal verbatim. This phase resolves
its drag-and-drop requirements into a bounded Windows OLE/Shell slice.

Phase 4 adds bidirectional drag-and-drop around the embedded Yazi terminal.
Yazi remains authoritative for selection and current directory state. The host
must not parse terminal output or implement its own file-operation policy.

## Functional requirements

- Yazi to Explorer/Desktop:
  - Use the complete `selected` set when it is non-empty.
  - Otherwise use the filesystem `hovered` item.
  - Expose the paths as a standard Shell file-drop data object.
  - Start the drag only after the normal left-button movement threshold is
    crossed; ordinary clicks and Yazi mouse input remain untouched.
- Explorer/Desktop to Yazi:
  - Accept an OLE drop over the embedded terminal HWND.
  - Resolve the destination from the latest available filesystem `cwd`.
  - Delegate the incoming data object to the Shell folder `IDropTarget` for
    that directory, preserving Shell copy/move/link negotiation, prompts,
    overwrite behavior, elevation, cancellation, and cross-volume handling.
  - Do not copy, move, overwrite, or delete files in host code.
- A missing, unavailable, stale, or non-filesystem bridge state rejects the
  operation without invoking a Shell API.
- A Shell/OLE/HRESULT failure is contained at the feature boundary and is
  recorded by category only; file contents and unnecessary full paths are not
  logged.

## Non-goals

- A custom drag image, custom copy/move rules, or a replacement for Explorer.
- Dropping onto a particular Yazi pane or hovered item; v1 targets `cwd`.
- Parsing `CF_HDROP` paths to implement a second file-operation pipeline.
- Dragging arbitrary terminal text, URLs, or non-filesystem Yazi resources.
- Claiming GUI acceptance from unit tests or a headless build.

## Compatibility and safety

- The implementation uses the existing `TerminalContainer.MessageHook` and
  hosted HWND boundary because the terminal is an HwndHost surface.
- The drop target is registered only while the host window owns the terminal
  session and is revoked before terminal disposal.
- The incoming data object is passed only to the Shell drop target selected for
  the current directory. The host does not persist or inspect file contents.
- Shell extensions and elevation UI execute under the normal Windows user
  context and remain outside the host's reliability guarantee.

## Acceptance criteria

- Pure tests cover selected-over-hovered source precedence and rejection of
  unavailable, URL, and empty bridge states.
- The WPF solution builds with no warnings or errors and existing tests pass.
- Manual Windows validation confirms:
  - one selected file drags to Explorer/Desktop;
  - multiple selected files drag as one set;
  - no selection falls back to hovered file;
  - Explorer/Desktop can drop files and directories into `cwd`;
  - Ctrl/Shift and same-/cross-volume behavior follows Shell negotiation;
  - ordinary Yazi click, hover, keyboard, mouse reporting, IME, and resize
    behavior remain intact;
  - closing the host revokes OLE registration and leaves no owned process.
- A failed Shell/OLE operation does not terminate the host or bridge session.

## Manual gates and residual risk

- The EasyWindowsTerminalControl hosted HWND may impose airspace and native
  drag-loop constraints; actual Explorer interaction is a manual gate.
- Third-party Shell extensions, cloud placeholders, virtual files, elevation
  prompts, and cancellation are compatibility observations rather than
  headless guarantees.
