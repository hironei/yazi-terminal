# Phase 2 Yazi Bridge Design

## Design status

This is a design-level Phase 2 contract. The repository may implement and test
the pure message parser and state reducer described below, but it does not yet
add the bridge transport, a Yazi plugin, a named-pipe implementation, or Shell
integration. Those runtime pieces start only after the open decisions in the
Phase 2 requirements are resolved against a pinned Yazi/`ya` fixture.

## Boundary

```text
MainWindow
├── TerminalControl  ── ConPTY: VT bytes and keyboard/mouse input
├── YaziSession      ── process and terminal lifecycle
└── YaziBridge       ── sideband JSON: semantic Yazi state
```

`TerminalControl` and `YaziBridge` share the generated instance identifier but
must not share a parser. The terminal path is opaque to the bridge. This keeps
the prohibition on screen parsing enforceable and allows the terminal backend
to be replaced independently.

## Startup sequence

1. `MainWindow` creates a random instance identifier and starts the local
   bridge listener with current-user access control.
2. The host resolves the pinned Yazi executable and starts it with the
   instance identifier and the approved bridge/plugin configuration.
3. The bridge performs a hello handshake containing protocol version,
   instance identifier, Yazi version, and supported capabilities.
4. The host accepts state only after the handshake matches the pending launch.
5. The bridge sends one `snapshot`; only then does the host publish actionable
   `YaziState` to later features.
6. Incremental `state` messages update the reducer until goodbye, disconnect,
   or child shutdown.

The host must never infer successful bridge startup from a visible Yazi screen.

## Transport framing and security

The initial transport is a local named pipe represented behind an
`IYaziBridgeTransport` interface. The current implementation provides
`YaziBridgePipeServer`, which uses `PipeOptions.CurrentUserOnly`, one accepted
connection per generated instance, and a 64 KiB maximum frame. Each connection
carries newline-delimited UTF-8 JSON with a bounded maximum line length. It
validates `instanceId` at the protocol boundary; the actual Yazi bridge/plugin
is not wired into `MainWindow` yet.

The pipe carries semantic state only. It does not carry terminal escape
sequences, keystrokes, file contents, or Shell commands. The bridge process or
plugin is not trusted to select another host instance; the host owns the
pending instance binding and rejects mismatches.

## DTOs and reducer

The implementation should keep transport DTOs separate from feature-facing
state:

```text
BridgeEnvelope
  protocol: string
  instanceId: Guid
  sequence: ulong
  kind: Hello | Snapshot | State | Goodbye | Error
  payload: JsonElement

YaziPath
  kind: Filesystem | Url
  value: string

YaziState
  instanceId: Guid
  sequence: ulong
  tab: int
  cwd: YaziPath
  hovered: YaziPath?
  selected: IReadOnlyList<YaziPath>
  availability: Available | Unavailable
```

The parser validates syntax and field shape; the reducer validates instance,
sequence, message ordering, and snapshot requirements. Neither layer resolves
or executes paths. A later Shell adapter may reject `Url` values and perform
its own path validation immediately before invoking a Shell API.

The reducer is monotonic: it accepts a snapshot for the pending instance,
then exactly-next sequence updates. Any gap transitions to `Unavailable` and
discards the actionable state. Reconnection starts at the handshake and must
deliver a new snapshot before returning to `Available`.

## Yazi adapter

The adapter should first use documented Yazi DDS capabilities for instance
targeting and directory/hover notifications. A minimal plugin is allowed for
selected-item state or event details not exposed by that public stream. The
adapter translates Yazi-specific event bodies into the stable host envelope;
Yazi Lua context objects must not leak into host DTOs.

An event stream written to the same ConPTY output is not a sideband protocol:
it would be interleaved with VT output and would force the host to inspect the
terminal stream. This is an architectural inference from the current session
boundary, so it is explicitly rejected as the primary transport even though
Yazi documents event reporting to output. The adapter must instead write the
framed envelope to the independent bridge transport.

## Lifecycle and failure handling

- Bridge listener startup failure is a bounded Phase 2 startup error; the
  terminal may still be usable only if the product explicitly permits a
  no-integration mode.
- Child/session shutdown first prevents new state publication, then closes the
  bridge, then disposes the terminal session. All operations are idempotent.
- A child-exit signal, bridge goodbye, or pipe disconnect marks state
  unavailable. The host must not invoke a later Shell action with stale state.
- Malformed input, an oversized frame, an authorization mismatch, and a
  protocol error close the bridge connection and log only a reason category.
- Cancellation and WPF dispatcher shutdown are treated as normal lifecycle
  outcomes, not unexpected bridge faults.

## Testing seams

- `BridgeMessageParser`: pure UTF-8/JSON and size-limit tests.
- `YaziStateReducer`: pure ordering, snapshot, disconnect, and reconnect tests.
- `IYaziBridgeTransport`: local integration tests for framing, cancellation,
  current-user access, and connection replacement.
- `YaziDdsAdapter`: fixture tests translating pinned DDS/plugin messages into
  the host envelope; no terminal output fixture is permitted.
- WPF composition tests: bridge startup and shutdown are bounded and do not
  block the UI thread.
- Manual Windows acceptance: real Yazi launch, CJK paths, selection/hover
  changes, reconnect, child exit, and absence of orphan processes.

## Later-phase handoff

Phase 3 consumes only `YaziState` and applies the selection precedence
`selected` first, `hovered` second, and `cwd` for an empty-area invocation.
Phase 3 must still reject unavailable state, non-filesystem URLs, and stale
sequence data. Phases 4-5 must not reach into the bridge or terminal control;
they consume the same feature-facing state and Shell/OLE boundaries.
