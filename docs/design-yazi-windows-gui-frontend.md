# Yazi Windows GUI Frontend Design

## Design status

This is the Phase 1 design for the initial empty repository. It intentionally
does not design the Shell integration or the Phase 2 bridge protocol in detail;
those depend on the unresolved requirements findings recorded in
`docs/requirements-yazi-windows-gui-frontend.md`.

## Composition root

```text
App
└── MainWindow
    ├── TerminalControl
    └── YaziSession
```

`MainWindow` owns the session lifetime. The session owns only the terminal
backend and Yazi child process. No file-manager state is duplicated in the
application.

## Terminal backend evaluation

ConPTY is the operating-system process boundary. The WPF renderer must parse
VT output and render an alternate screen, cursor, colors, Unicode, input, and
resize. A renderer is therefore a required dependency rather than an optional
implementation detail.

The current candidate for evaluation is `EasyWindowsTerminalControl` 1.0.38:

- It hosts the official Windows Terminal WPF control and uses the packaged
  ConPTY implementation through `TermPTY`.
- `EasyTerminalControl` owns the terminal surface, input, theme, font, and
  startup command configuration; `TermPTY` owns the child process and PTY
  stream.
- The package provides the native renderer/input path, including mouse and
  resize APIs, but its low-level/beta dependency surface and native HWND
  airspace behavior remain compatibility risks.

This is not an adoption decision. The Easy run passed Yazi CJK display,
keyboard, mouse, resize, IME candidate position, and normal full-screen
rendering. Deterministic 24-bit color and unexpected child-exit observation
remain open. If a required behavior is insufficient, compare a source-derived
Microsoft WPF Terminal Control before choosing the production backend. Until
that gate passes, the backend remains replaceable behind the session/control
boundary.

Adding the candidate package is a dependency change and requires explicit
approval under the repository instructions. If no candidate passes the gate,
implementation stops at the design boundary rather than substituting a small
WPF `TextBox` or parsing screen text.

## Yazi launch policy for Phase 1

1. If `YAZI_PATH` is non-empty, resolve that exact path and require it to be a
   file.
2. Otherwise resolve `yazi.exe` through the process PATH.
3. If resolution fails, show a bounded user-facing error and log only the
   failure category and executable name.
4. Start the session in the host process working directory for this slice.
5. Use Easy's `TermPTY` process/ConPTY creation and disposal API; do not add a
   second process wrapper unless the package cannot expose the required
   lifecycle hooks.

## Lifecycle and error boundaries

- Session startup happens after the WPF window is loaded so the control has a
  valid terminal geometry.
- Easy starts its child from the terminal control's loaded path. The bridge
  environment scope must therefore remain active until that asynchronous
  startup path has created the child; it is restored during window shutdown.
- The public `TermPTY` API does not expose a child-process exit event. Window
  close calls `DisconnectConPTYTerm`, closes PTY stdin, and stops the external
  terminal process; unexpected-exit observation remains an explicit gate.
- Window close is idempotent: stop accepting new input, close the bridge,
  disconnect the control, stop the PTY child, then allow WPF shutdown.
- Startup failures are classified as executable resolution, ConPTY/session
  creation, or child-start failure. The UI reports a short message; diagnostic
  logs do not include terminal contents.

## Future bridge boundary

The bridge is a separate service boundary from `TerminalControl`. The first
candidate is a Yazi DDS/local-event subscription plus a minimal bridge plugin
only where the public event stream cannot expose `hovered` and `selected`.
The protocol must include Yazi instance identity, message kind, JSON payload,
sequence/reconnect behavior, and explicit path encoding before implementation.
The host will never inspect the terminal screen buffer for these values.

## Testing seams

- Executable resolution is a pure service with injected environment/PATH
  lookup, so missing/invalid/valid paths are unit-testable.
- Session construction and ConPTY creation remain in the WPF composition root
  for this first slice; startup/session failures are therefore covered by the
  bounded UI error path and the manual host run rather than a headless session
  fixture.
- Manual acceptance owns the real Windows GUI checks: Yazi navigation,
  Japanese/Unicode, resize, IME candidate placement, focus, clipboard,
  mouse reporting, native HWND overlays, and child-process cleanup.

## Security and compatibility

- The host launches the user's configured Yazi and therefore inherits the
  user's Yazi configuration/plugin execution model; it does not sandbox Yazi.
- No user file contents are logged.
- Existing Yazi configuration and plugins are passed through unchanged in
  Phase 1. The host does not inject a GUI-only configuration file.
- Shell COM loading and drag/drop are outside this slice and must later be
  isolated behind explicit HRESULT/exception boundaries.
