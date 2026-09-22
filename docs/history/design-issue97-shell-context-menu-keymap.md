# Issue #97 Design: Yazi Keymap Shell Context Menu

## Boundaries

```text
Yazi keymap
    ↓ plugin entry / existing bridge pipe
YaziBridgeSession command request
    ↓ latest bridge state
YaziShellTargetResolver
    ↓ normalized filesystem paths
WindowsShellFinalPathResolver
    ↓ real filesystem paths
WindowsShellContextMenuService
```

The plugin never sends a target path. It sends only one of two fixed command
names over the already negotiated named pipe. This keeps path authority in the
host and preserves the existing bridge protocol identifier.

## Bridge command message

The plugin sends the existing envelope shape with `kind = "command"` and a
payload such as `{"command":"context-menu"}`. The message participates in the
existing contiguous sequence. It advances protocol sequencing but does not
replace the last state snapshot or its heartbeat revision.

`YaziBridgeStateReducer` accepts command messages after the handshake. The
session validates the payload against the fixed allowlist and raises a command
event only for valid requests. Invalid requests are logged and ignored so a
bad user binding cannot disconnect an otherwise valid state stream.

The plugin's async bridge loop sends pending requests only after the initial
snapshot has been sent. Pending requests are bounded and are dropped when a
pipe connection is unavailable or closes; a request from an old Yazi session
must not be replayed against a later state session.

## Host invocation

`MainWindow` subscribes to the bridge command event and dispatches it to the
WPF UI thread. It maps the two allowlisted names to the existing
`YaziShellInvocation` values and uses the current cursor position for the
popup location. The existing queue and stale-state checks remain in place.

The final menu path is resolved exactly at the menu-display boundary from the
latest state. Existing mouse and F10 paths therefore share the same final-path
resolver; no separate keymap-specific Shell invocation is added.

## Final-path resolution

`WindowsShellFinalPathResolver` is independent of WPF and COM. For each path
it starts at the target and walks toward the root to find the nearest
Junction/SymbolicLink ancestor. It replaces that component with the one-level
link target and repeats. Relative link targets are resolved relative to the
link's parent. The loop is bounded; a broken link, link-loop, or limit
exhaustion rejects the complete target set. If no relevant link exists, the
normalized path is preserved so the existing Shell API reports ordinary
missing-path behavior.

The resolver returns a new `YaziShellTarget` with the same invocation and
bridge sequence. It does not change the bridge snapshot. Multiple paths are
resolved independently before the existing Shell adapter checks that their
resolved parent directories match.

## Plugin API

The module keeps its existing `setup` export and adds the Yazi plugin `entry`
export. The entry accepts only the two documented `job.args` values and queues
the request for the setup-owned bridge loop. The plugin continues to publish
snapshots, state updates, command catalog metadata, and heartbeats unchanged.

## Error and compatibility behavior

- Missing setup/bridge connection: the plugin logs the request failure and
  leaves normal Yazi operation intact.
- Unknown bridge command: host logs and ignores it.
- Unavailable or stale state: host logs the existing unavailable category and
  does not open a menu.
- Link resolution failure: host logs a final-path category and does not fall
  back to the apparent path.
- Existing plugins that never send `command` messages remain compatible.
- GUI focus, native HWND message routing, third-party Shell extensions,
  OneDrive registration, and modern-menu presentation remain manual gates.

## Test seams

- Protocol tests exercise command kind parsing, reducer sequencing, and
  session events.
- The final-path algorithm has an internal link-probe seam so tests can model
  item links, parent links, chains, loops, and broken targets without requiring
  Developer Mode or a particular Shell provider.
- The Lua source/fixture checks cover plugin entry, command-envelope markers,
  and the existing keymap catalog parser. Live queue behavior remains a
  desktop/Yazi acceptance check.
- The executable test suite and solution build cover integration and compile
  the WPF/Win32 boundaries; live Yazi/Shell/OneDrive behavior remains manual.
