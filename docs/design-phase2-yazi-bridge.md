# Phase 2 Yazi Bridge Design

## Design status

This is a design-level Phase 2 contract. The repository now contains the
protocol core, current-user named-pipe transport, bridge session, and an
opt-in Yazi plugin probe. The transport/session are not yet wired to Shell
features, and the pinned Yazi/ya 26.5.6 Windows fixture now passes the plugin
named-pipe snapshot/hover/selection gate. Broader version compatibility and
Shell integration remain out of scope.

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

1. `MainWindow` creates a random GUID instance identifier and starts the local
   bridge listener with current-user access control.
2. The host resolves the pinned Yazi executable, generates a positive numeric
   Yazi client ID, and starts it with that `--client-id` plus the bridge
   environment variables. The plugin remains
   opt-in through the user's Yazi `init.lua` configuration.
3. The bridge performs a hello handshake containing protocol version,
   instance identifier, and supported capabilities. The exact Yazi/ya version
   is recorded in the pinned compatibility fixture.
4. The host accepts state only after the handshake matches the pending launch.
5. The bridge sends one `snapshot`; only then does the host publish actionable
   `YaziState` to later features.
6. Incremental `state` messages update the reducer until goodbye, disconnect,
   or child shutdown.

The host must never infer successful bridge startup from a visible Yazi screen.

The current Easy implementation does not expose a custom child environment
block. `EasyTerminalControl` starts `TermPTY` from its loaded path and the
startup is asynchronous, so the host sets the three bridge variables before
adding the control and keeps that process-local environment scope until window
shutdown. The scope is restored on cleanup and is not a user configuration
change. This is a package-compatibility workaround; a future explicit
environment API should replace it because process environment variables are
shared by the host process while the scope is active.

## Transport framing and security

The initial transport is a local named pipe represented behind an
`IYaziBridgeTransport` interface. The current implementation provides
`YaziBridgePipeServer`, which uses `PipeOptions.CurrentUserOnly`, sequential
connections per generated instance, and a 64 KiB maximum frame. Each
connection carries newline-delimited UTF-8 JSON with a bounded maximum line
length. It validates `instanceId` at the protocol boundary; after a
disconnect, the session requires a new hello and snapshot before state becomes
actionable again. The actual Yazi plugin is not enabled by `MainWindow` yet.

The pipe carries semantic state only. It does not carry terminal escape
sequences, keystrokes, file contents, or Shell commands. The bridge process or
plugin is not trusted to select another host instance; the host owns the
pending instance binding and rejects mismatches. The host passes the pipe name,
instance ID, and protocol version in the child environment without modifying
the user's Yazi configuration.

`YaziBridgeSession` owns one active transport connection at a time, feeds
frames through the protocol parser and reducer, publishes available
snapshots/updates, and emits an unavailable notification on goodbye, protocol
failure, or disconnect before accepting a replacement connection. It is
currently started by `MainWindow`, but its state is not consumed by Shell
features until the Yazi plugin/configuration contract is approved.

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
