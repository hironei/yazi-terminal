# Design: Version-Pinned Yazi Manager Commands in the Command Palette

Issue: #65

## Catalog source and ownership

The host owns a read-only catalog copied from Yazi's
`yazi-config/preset/keymap-default.toml` `[mgr].keymap` section at version
26.5.6. The catalog is represented as `YaziBridgeCommand` records so the
existing palette projection and execution path can be reused. Its source
version and upstream path are kept beside the catalog definition.

The catalog is intentionally a snapshot, not an action registry. It provides
the standard key notation, action sequence, and description that make a
command discoverable. Yazi remains the authority that accepts or rejects the
action at runtime.

## Merge behavior

`CommandPaletteCommands.WithYaziCommands` starts with the pinned manager
catalog, then merges bridge-reported user manager commands. The complete
ordered action sequence is the identity used to avoid duplicate entries. A
user-reported record replaces the bundled record for the same sequence so its
custom key and description remain visible. A different sequence remains a
separate command, even when its first action is the same.

The standard catalog is always supplied by the host, so theme commands and
standard manager commands do not depend on the bridge connection. User
commands are still cleared on bridge disconnect by the existing session
lifecycle.

## Scope and execution

Only the `[mgr]` section is copied. Manager actions can be sent to the active
client using the existing `ya emit-to <client-id> <action> <args...>` process
controller. Other Yazi UI contexts have different active receivers and are
not included. `shell` and `plugin` records may be present because they are
part of the pinned manager keymap; their interpretation and prerequisites
remain Yazi-owned.

The host does not read a Yazi installation's private preset at runtime. This
avoids depending on an installation layout and makes the release behavior
deterministic. Updating the pinned Yazi baseline is an explicit catalog and
documentation change.

## Test seams and traceability

- Test the pinned version and representative standard records, including a
  multi-action sort entry.
- Test that standard commands exist with no bridge commands.
- Test replacement of a bundled record by an identical user action sequence
  and retention of distinct custom sequences.
- Keep existing command filtering, bridge parsing, action tokenization, and
  sequential execution tests passing.
- Record the upstream source URL and version in the catalog implementation and
  user manual.
