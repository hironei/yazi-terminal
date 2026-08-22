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

The selected backend for the current implementation is
`EasyWindowsTerminalControl` 1.0.38:

- It hosts the official Windows Terminal WPF control and uses the packaged
  ConPTY implementation through `TermPTY`.
- `EasyTerminalControl` owns the terminal surface, input, theme, font, and
  startup command configuration; `TermPTY` owns the child process and PTY
  stream.
- The package provides the native renderer/input path, including mouse and
  resize APIs, but its low-level/beta dependency surface and native HWND
  airspace behavior remain compatibility risks.

The Easy backend is selected for the current host implementation. The run
passed Yazi CJK display, keyboard, mouse, resize, IME candidate position,
normal full-screen rendering, deterministic 24-bit color, and unexpected
child-exit handling. Packaging and native HWND overlay behavior remain open.
If a later gate fails, compare a source-derived Microsoft WPF Terminal Control;
the backend remains replaceable behind the session/control boundary.

## Dependency licensing and supply-chain decision

> `EasyWindowsTerminalControl` はMIT Licenseで公開されており、内部で第三者NuGet `CI.Microsoft.Terminal.Wpf` を利用している。`CI.Microsoft.Terminal.Wpf` のNuGet metadataにはライセンスが明示されていないが、その上流であるMicrosoft Windows TerminalはMIT Licenseであり、WPF ControlのソースもMicrosoft公式リポジトリに存在する。
>
> `EasyWindowsTerminalControl`および`WPF-UI.Terminal`という公開MITプロジェクトで同パッケージの利用実績がある。
>
> v1ではNuGet依存として利用を許容する。ただし`CI.Microsoft.Terminal.Wpf`はMicrosoft公式NuGetではないため、ライセンス問題ではなく**サプライチェーン・保守性上のリスク**として記録する。
>
> バイナリ配布時にはMicrosoft TerminalのMIT LICENSEおよびNOTICEの扱いを改めて確認する。

This is a v1 use decision, not a legal conclusion about the repackage. The
dependency remains replaceable if its provenance, maintenance, or distribution
terms become unacceptable.

The selected package is already part of the current implementation. A future
backend or dependency change still requires an explicit compatibility and
license review; a small WPF `TextBox` or screen parsing remains out of scope.

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
- The public `TermPTY` API does not expose a child-process exit event. The host
  waits on its public process handle and consumes the package's
  `Session Terminated` terminal-output lifecycle marker as a second signal.
  Window close calls `DisconnectConPTYTerm`, closes PTY stdin, and stops the
  external terminal process.
- Window close is idempotent: stop accepting new input, close the bridge,
  disconnect the control, stop the PTY child, and finish bridge disposal
  asynchronously so WPF shutdown is not blocked by the receive loop.
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
  mouse reporting, native HWND overlays, child-process cleanup, and the
  unexpected-exit notification.

## Security and compatibility

- The host launches the user's configured Yazi and therefore inherits the
  user's Yazi configuration/plugin execution model; it does not sandbox Yazi.
- No user file contents are logged.
- Existing Yazi configuration and plugins are passed through unchanged in
  Phase 1. The host does not inject a GUI-only configuration file.
- Shell COM loading and drag/drop are outside this slice and must later be
  isolated behind explicit HRESULT/exception boundaries.
