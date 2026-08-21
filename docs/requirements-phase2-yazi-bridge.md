# Phase 2 Yazi Bridge Requirements

## Scope and relationship to the attached requirement

The attached product requirement describes the desired Shell and drag-and-drop
experience. This document is the repository's derived Phase 2 contract; it is
not a verbatim copy of the attachment and it does not make the later Shell
features complete.

Phase 2 supplies authoritative Yazi state to the host so that later phases can
invoke Windows Shell behavior without parsing the terminal screen. The required
state is the active working directory, the hovered item, and the selected
items. Yazi remains authoritative for navigation, selection, and file-manager
operations.

## Goals

- Identify one Yazi instance unambiguously from host launch through bridge
  shutdown.
- Transport semantic state over a sideband channel separate from the ConPTY
  byte stream.
- Support a versioned, UTF-8 JSON contract with snapshots, ordered updates,
  reconnect detection, and explicit unavailable state.
- Preserve filesystem paths and non-filesystem Yazi URLs as different values;
  later Shell phases may act only on supported filesystem paths.
- Make the protocol parser and state reducer testable without starting WPF or
  Yazi.

## Non-goals

- Parsing VT output, screen-buffer text, cursor position, or rendered labels.
- Reimplementing Yazi's file-manager model, selection rules, or file
  operations.
- Implementing Shell context menus, Explorer/Desktop drop targets, or OLE data
  transfer. Those remain later phases.
- Treating a Yazi DDS event written into the terminal output as a reliable
  host protocol without an independently framed sideband transport.

## Compatibility and launch contract

The supported matrix must record the exact paired `yazi` and `ya` versions,
Windows version, architecture, and bridge plugin revision. The first fixture
uses the locally verified Yazi `26.5.6`; a newer Yazi documentation version
must not silently become the supported runtime.

The host generates a cryptographically random GUID instance identifier for
every launch. Yazi 26.5.6 requires `--client-id` to be a globally unique
number, so the host also generates a positive cryptographic numeric client ID
for that launch argument. The GUID is included in every bridge message and the
bridge must reject messages for a different instance. The launch arguments,
environment changes, numeric client ID, and plugin revision are recorded in the
compatibility fixture.

Yazi DDS is the semantic source to evaluate. Its public documentation exposes
client targeting and events such as `cd` and `hover`, while Lua plugin context
exposes selected files. The Phase 2 implementation may use a minimal
documented plugin for the state that the public event stream does not provide,
but the host-facing contract remains independent of Yazi's internal Lua data
structures.

## Sideband transport requirements

1. ConPTY carries terminal input and VT output only.
2. Semantic bridge messages use a separately framed local transport. The
   preferred design is a current-user-scoped named pipe with one connection per
   Yazi instance; an equivalent local transport is acceptable only if it has
   the same instance binding, framing, and access control.
3. A pipe name or random token alone is not a security boundary. The server
   must restrict connections to the launching Windows user and validate the
   instance identifier before accepting state.
4. Messages are UTF-8, newline-delimited JSON objects. A message must be
   rejected if it is oversized, malformed, missing required fields, or contains
   a different protocol version.
5. The bridge must send a hello/capabilities message, one complete snapshot,
   incremental state updates, and a goodbye or error message when it can do so.
6. If the connection drops, the host marks state unavailable and must not use
   the last known paths for a later Shell action until a fresh snapshot is
   accepted.

## Message contract

Every message has this envelope:

```json
{
  "protocol": "yazi-desktop-host/1",
  "instanceId": "guid",
  "sequence": 42,
  "kind": "snapshot",
  "payload": {}
}
```

`sequence` is a non-negative, strictly increasing number within an instance.
The first accepted state is a `snapshot`. A sequence gap, duplicate, or
out-of-order update makes the reducer request or await a new snapshot rather
than guessing the missing state.

The state payload uses explicit path values:

```json
{
  "tab": 0,
  "cwd": { "kind": "filesystem", "value": "C:\\work" },
  "hovered": { "kind": "filesystem", "value": "C:\\work\\a.txt" },
  "selected": [
    { "kind": "filesystem", "value": "C:\\work\\a.txt" }
  ]
}
```

`cwd` is required in a snapshot. `hovered` is nullable, and `selected` is an
array that may be empty. A path has `kind` `filesystem` or `url`; an unknown
kind is invalid. Values are UTF-8 strings and are not shell commands. The
bridge must not canonicalize, expand, or execute them while parsing.

Required message kinds are `hello`, `snapshot`, `state`, `goodbye`, and
`error`. A `state` message may update any combination of `cwd`, `hovered`, and
`selected`, but its payload must state which fields are present so that an
empty selection or cleared hover is distinguishable from an omitted field.

## State and lifecycle behavior

- Before the first valid snapshot, the host exposes no actionable bridge
  state.
- A valid snapshot replaces all prior state for the matching instance.
- An accepted incremental update advances the state only when its sequence is
  exactly the next expected value.
- A `goodbye`, parse error, authorization error, sequence gap, or transport
  disconnect marks the state unavailable and records a reason category.
- Host shutdown closes the bridge before disposing the terminal session. Late
  messages are ignored after shutdown begins.
- Logs record lifecycle categories, protocol version, instance correlation,
  and sequence diagnostics, but not file contents or full paths.

## Acceptance criteria

- A pure parser test accepts valid UTF-8 snapshots and rejects malformed JSON,
  oversized messages, unknown protocol versions, wrong instance IDs, invalid
  path kinds, and missing required fields.
- Reducer tests cover first snapshot, ordered updates, empty selection,
  cleared hover, duplicate/out-of-order messages, sequence gaps, goodbye,
  disconnect, and reconnect requiring a fresh snapshot.
- Fixtures cover Japanese/CJK names, spaces, surrogate pairs, long paths,
  filesystem roots, and non-filesystem URLs without logging their contents.
- A Windows manual run proves that one host instance receives state only from
  its own Yazi instance while the ConPTY display remains ordinary Yazi output.
- The manual run records the exact Yazi/`ya` versions and whether `cwd`,
  hovered, selected, reconnect, and child-exit behavior passed.
- No acceptance test depends on screen text, rendered coordinates, or a
  terminal-control `OutputText` property.

## Open decisions before production integration

- Confirm the minimal plugin/event combination that publishes selected items
  and hover changes for the supported Yazi version range. The pinned 26.5.6
  fixture now passes both changes through the opt-in plugin; a broader version
  range remains unvalidated.
- Confirm the named-pipe implementation and current-user-only behavior on the
  minimum Windows version. The pinned Windows fixture now proves that
  `PipeOptions.CurrentUserOnly` plus the full Windows pipe path can be opened
  by `fs.access`; other Yazi/Windows versions remain unvalidated.
- Confirm whether the host can start the bridge without changing the user's
  existing Yazi configuration; the temporary fixture proves this for an
  opt-in `init.lua` setup and does not overwrite user configuration.
- Confirm reconnect behavior with a real Yazi instance after the pipe is
  interrupted. The host session and plugin probe now retry with hello sequence
  0 and require a fresh snapshot, but the full real-Yazi reconnect gate remains
  unvalidated.
- Confirm the real child-exit signal and shutdown ordering of the selected
  terminal backend before claiming the lifecycle gate complete.
