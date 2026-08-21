# Yazi Windows GUI Frontend Requirements

## Scope and interpretation

The source document is an attached product requirement, not an instruction to
copy its text verbatim into the repository. The user request is to review the
requirement and execute the repository-aware development workflow. This file
records the reviewed, testable scope for the first implementation slice.

The repository is an empty initial repository. The first slice is therefore
limited to Phase 1 (embedded WPF terminal host and Yazi process lifecycle).
Phases 2-5 remain planned work and are not claimed as implemented here.

## Product goal

Run the ordinary `yazi.exe` process inside one Windows WPF window through
ConPTY. Yazi remains authoritative for file-manager state, rendering, key
bindings, plugins, and file operations. The GUI must not parse terminal text or
reimplement a file-manager model.

## Phase 1 functional requirements

- The application targets Windows and .NET 10 WPF.
- The application starts `yazi.exe` as a ConPTY child process, without opening
  a separate terminal window.
- The terminal control renders VT output and forwards keyboard input to Yazi.
- The terminal control supports the normal Yazi full-screen terminal surface,
  including alternate screen and resize propagation. Japanese and Unicode
  rendering are acceptance-tested on Windows.
- Closing the WPF window disposes the terminal session and terminates or
  observes the Yazi child according to the selected terminal-session API.
- If Yazi cannot be resolved, ConPTY cannot be created, or the child exits
  unexpectedly, the application reports a user-readable error and does not
  fail with an unhandled process-level exception.
- No screen-buffer text is parsed to infer a path, selection, or current
  directory in Phase 1.

## Phase 1 non-goals

- DDS/Yazi bridge state synchronization.
- Shell context menus.
- Explorer/Desktop drag-and-drop.
- Yazi plugin changes, including `yazi_vcs`, pane-link, and pane-diff.
- A second file-manager model, independent preview, VCS implementation, or
  Vim/Yazi keymap implementation.
- Quantitative startup-performance targets.

## Cross-phase requirements retained for later slices

- Phase 2 obtains `cwd`, hovered path, and selected paths through a Yazi
  public interface or a minimal documented bridge plugin, never by screen
  parsing.
- Phase 3 uses Windows Shell context-menu APIs for selected/hovered paths and
  for the current directory. Selection precedence is selected items first,
  hovered item second; an empty-area invocation targets `cwd`.
- Phases 4-5 use OLE Shell data transfer and preserve Windows copy/move
  semantics. The GUI does not invent a separate copy/move policy.
- COM work is STA-compatible, HRESULT failures are contained at the feature
  boundary, and Shell extension failures cannot crash the application.
- Logs cover lifecycle and integration failures without persisting file
  contents or unnecessary full paths.

## Reviewed findings

### MAJOR-1: terminal renderer/dependency is not selected

ConPTY supplies a pseudoconsole stream; it does not render a WPF terminal.
The requirement names ConPTY but leaves the VT renderer and its dependency,
native assets, license, input/IME behavior, and maintenance policy undefined.
Phase 1 cannot be accepted until one renderer is selected and validated on the
target framework. The initial review evaluated the `VirtualTerminal` packages;
the current implementation instead evaluates `EasyWindowsTerminalControl`,
which combines a Windows Terminal WPF control with ConPTY. This dependency
change was explicitly requested for the current pipeline run.

`EasyWindowsTerminalControl` is an evaluation target, not an adoption decision.
The real Yazi run passed CJK, IME, keyboard input, mouse reporting, resize, and
normal full-screen rendering. Deterministic 24-bit color and unexpected child
exit observation remain open gates. If any required behavior is insufficient,
compare a WPF Terminal Control derived from the Microsoft Windows Terminal
implementation before selecting the production backend.

### MAJOR-2: bridge protocol and Yazi compatibility range are undefined

The requirement correctly forbids screen parsing, but it does not define the
supported Yazi version range, event subscription command line, message schema,
instance identity, reconnect behavior, or how `hovered` and `selected` are
published to an external host. DDS documents `cd`/`hover` reporting and Lua
context access, but a host protocol for all required state still needs a
versioned design and fixture tests before Phase 2.

### MAJOR-3: Explorer -> Yazi drop semantics are underspecified

An `IDropTarget` receives a Shell data object, but the requirement does not say
whether the target delegates to `IFileOperation`, a Shell folder drop target,
or another Shell API, nor how overwrite, cancellation, elevation, links, and
cross-volume moves are handled. “Windows standard file operation” is not a
testable implementation contract until that choice and the error/cancel
behavior are specified.

### MAJOR-4: platform and runtime compatibility matrix is missing

The attached document names Windows and .NET 10 but does not fix the minimum
Windows version, CPU architectures, Yazi/`ya` version pair, or a reproducible
manual-test environment. ConPTY support begins with Windows 10 version 1809,
but the application still needs an explicit support matrix and a matching
Yazi/`ya` fixture before “normal Yazi” is testable.

### MAJOR-5: terminal input ownership is not compatible with later Shell hooks

The selected terminal renderer may be WPF-native or an `HwndHost` around a
Win32 terminal. In the latter case keyboard and mouse input is delivered to
the hosted HWND rather than ordinary WPF events. The requirement does not say
which component owns focus, IME, right-click interception, or drag initiation.
This must be decided before Phase 3-5 so GUI integration does not silently
break Yazi mouse reporting.

### MAJOR-6: ConPTY lifecycle and back-pressure are not acceptance-testable

The requirement does not define concurrent output draining, input writes,
resize ordering, child exit observation, final-output draining, or the order of
child/session/pseudoconsole disposal. These are required to prevent deadlock
and orphaned `yazi.exe` processes and must be included in both the session
contract and the error-path tests.

### MAJOR-7: Shell context-menu scope is not defined

“Explorer-equivalent” can mean the classic `IContextMenu` menu or the newer
Windows 11 Shell experience. Multiple selection, parent HWND, menu-coordinate
translation, `IContextMenu2/3` message forwarding, bitness, and the boundary
for third-party Shell Extension hangs/crashes are not specified. The later
phase needs a bounded classic-menu contract or a deliberate newer Shell API
choice, plus an explicit guarantee boundary.

### MINOR-1: Yazi executable resolution and initial directory are unspecified

The implementation needs a deterministic precedence for an explicit setting,
PATH lookup, and a missing executable, plus a defined initial working
directory. Phase 1 uses an explicit `YAZI_PATH` override when present, then
PATH lookup, and starts in the host process working directory unless a future
setting changes it.

The later implementation must also define startup arguments, environment
inheritance, configuration-directory behavior, and whether non-filesystem Yazi
URLs are excluded from Shell integration.

### MINOR-2: manual acceptance boundaries are not stated

IME, DPI, focus, Shell extensions, Explorer/Desktop drag-and-drop, and actual
third-party Yazi plugins cannot be proven by a headless build. They need a
Windows manual acceptance matrix and must be reported separately from static
tests.

### NIT-1: “thin” and “avoid heavy initialization” need an observable boundary

The requirement is useful guidance but not a measurable v1 performance target.
For this first slice it is recorded as an architecture constraint: no generic
host, DI framework, update check, or eager Shell integration.

## Acceptance criteria for this slice

- `dotnet build` succeeds for the Windows WPF project on a Windows .NET 10 SDK.
- Static tests cover executable resolution and failure classification without
  starting a GUI.
- A Windows manual run starts Yazi in the embedded terminal, accepts normal
  navigation keys, displays Japanese/Unicode fixtures, and responds to resize.
- Closing the window leaves no owned Yazi child process.
- The documented Phase 2-5 blockers above remain explicit; no later-phase
  feature is represented as complete.

## Terminal host evaluation gate

The current candidate must pass a real Windows/Yazi compatibility check for all
of the following before adoption:

- VT output and 24-bit color
- alternate-screen full-screen rendering
- Unicode, CJK, wide-character, and Japanese text rendering
- keyboard input and focus behavior
- IME composition and committed text
- xterm mouse reporting and ordinary Yazi mouse actions
- terminal resize propagation and reflow

The evaluation records pass/fail evidence per item. A failure does not relax
the Phase 1 acceptance criteria; it triggers comparison with a WPF Terminal
Control derived from Microsoft Windows Terminal instead.

## Traceability to the attached document

- Attached sections 2, 3, 4, 6, 7, 18, 19, 20, 21, and 22 map to Phase 1 and
  the cross-cutting constraints above.
- Attached sections 8, 9-17, and 23-24 are retained as later phases and are
  deliberately not implemented by the first slice.
- Attached sections 25-29 are investigation guidance. They are not acceptance
  criteria and do not authorize copying code or adding dependencies without a
  compatibility and license review.

## Requirements review status

Review round 1 found no `BLOCKER`, but `MAJOR-1` through `MAJOR-7` remain.
The requirements phase is therefore not passed. The practical next decision
is to approve the Phase 1 terminal dependency and runtime matrix, then resolve
the bridge and Shell/D&D contracts before their respective phases begin.
