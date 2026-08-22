# Phase 4 Explorer/Yazi Drag-and-Drop Requirements

## Scope and interpretation

The attached product requirement is a product specification, not an
instruction to copy or implement every proposal verbatim. This phase resolves
its drag-and-drop requirements into a bounded Windows OLE/Shell slice.

Phase 4 covers the Yazi-to-Explorer direction around the embedded terminal.
The shared OLE registration boundary is implemented alongside this slice so
Phase 5 can add Explorer-to-Yazi without a second input interception path;
Phase 5 has its own acceptance document. Yazi remains authoritative for
selection and current directory state. The host must not parse terminal output
or implement its own file-operation policy.

## Functional requirements

- Yazi to Explorer/Desktop:
  - Use the complete `selected` set when it is non-empty.
  - Otherwise use the filesystem `hovered` item.
  - Expose the paths as a standard Shell file-drop data object.
  - Start the drag only after the normal left-button movement threshold is
    crossed; ordinary clicks and Yazi mouse input remain untouched.
- A missing, unavailable, stale, or non-filesystem bridge state rejects the
  operation without invoking a Shell API.
- A Shell/OLE/HRESULT failure is contained at the feature boundary and is
  recorded by category only; file contents and unnecessary full paths are not
  logged.

## Non-goals

- A custom drag image, custom copy/move rules, or a replacement for Explorer.
- Parsing `CF_HDROP` paths to implement a second file-operation pipeline.
- Explorer-to-Yazi drop behavior; that is Phase 5.
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
  - ordinary Yazi click, hover, keyboard, mouse reporting, IME, and resize
    behavior remain intact;
  - closing the host revokes OLE registration and leaves no owned process.
- A failed Shell/OLE operation does not terminate the host or bridge session.

## Manual gates and residual risk

- The EasyWindowsTerminalControl hosted HWND may impose airspace and native
  drag-loop constraints; actual Explorer interaction is a manual gate.
- Third-party Shell extensions and destination-side behavior are outside this
  source-direction gate and are covered by Phase 5 where applicable.
