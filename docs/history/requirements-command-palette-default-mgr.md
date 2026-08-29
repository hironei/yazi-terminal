# Requirements: Version-Pinned Yazi Manager Commands in the Command Palette

Issue: #65

## Goal

Make the Command Palette useful without requiring the user to copy Yazi's
default manager bindings into their own `keymap.toml`. The palette should
offer the manager actions from the pinned Yazi compatibility baseline while
continuing to include user-defined manager bindings reported by the bridge.

## Functional requirements

1. The palette includes the standard `[mgr].keymap` records from the pinned
   Yazi/`ya` compatibility version 26.5.6.
2. Each bundled record preserves its standard key notation, action text,
   ordered multi-action sequence, and description.
3. Bundled commands remain available when the user's `keymap.toml` does not
   contain the corresponding binding.
4. Commands reported by the bridge from the user's manager keymap remain
   searchable and executable.
5. If a user command has the same complete action sequence as a bundled
   command, the palette does not show an unexpected duplicate entry.
6. The palette continues to execute manager actions through the existing
   `ya emit-to <client-id> ...` path.

## Non-functional requirements

- The pinned source version and source file location are explicit in the
  implementation and documentation.
- No new dependency, bridge protocol version, host shell invocation, or Yazi
  upstream modification is introduced.
- The bundled catalog is independent of bridge connectivity so standard
  manager commands remain visible when the bridge is unavailable.
- Existing custom command parsing, tokenization, and sequential multi-run
  execution behavior remain unchanged.

## Non-goals

- Enumerating Yazi's effective merged keymap at runtime.
- Including `[tasks]`, `[spot]`, `[pick]`, `[input]`, `[confirm]`, `[cmp]`, or
  `[help]` keymap contexts.
- Guaranteeing that `plugin`, `shell`, or state-dependent actions succeed when
  their Yazi-side prerequisites are unavailable.
- Automatically tracking a newer Yazi release without updating the pinned
  catalog.

## Edge cases and safety

- A bundled command may be valid but unavailable in a different Yazi version;
  the pinned version must be visible rather than implying universal support.
- Multi-action records must retain source order and must not be partially
  reordered or collapsed.
- Action text continues to be tokenized and passed with
  `ProcessStartInfo.ArgumentList`; it is never evaluated by a host shell.
- Missing `ya.exe`, bridge state, plugin dependencies, or an invalid action
  must not crash the host or remove the theme/settings commands.

## Acceptance criteria

- Standard manager actions such as `quit`, `open`, `paste`, `remove`, sorting,
  navigation, and tab operations appear without matching user keymap entries.
- Standard multi-action sort records preserve their action order.
- User-provided manager commands remain available without unexpected duplicate
  entries for an identical action sequence.
- Tests cover the pinned catalog, merge behavior, and an existing custom
  command path.
- The user manual states the pinned version, manager-only scope, and version
  compatibility limitation.
