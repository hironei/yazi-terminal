# Phase 2 Yazi Event Investigation

## Result

The locally installed Yazi/`ya` pair is `26.5.6` (`aa52643`, 2026-05-05).
The following commands were run from the Windows host:

```text
yazi.exe --help
ya.exe pub-to --help
yazi.exe --version
```

The CLI exposes:

- `--client-id <CLIENT_ID>`
- `--local-events <LOCAL_EVENTS>`
- `--remote-events <REMOTE_EVENTS>`
- `--cwd-file <CWD_FILE>`
- `--chooser-file <CHOOSER_FILE>`
- `ya pub-to <RECEIVER> <KIND>` with string, JSON, and list bodies

This confirms the instance-targeting and DDS reporting options needed by the
Phase 2 design. It does not by itself prove that the options can provide a
safe host sideband when Yazi is running inside the current ConPTY session.

## Public event coverage

Yazi's DDS documentation describes real-time output records with `kind`,
receiver, sender, and a JSON body. The documented built-in state-relevant
events include:

- `cd`, which carries the tab and URL after a directory change;
- `hover`, which carries the tab and hovered URL;
- operation events such as rename, move, trash, and delete;
- handshake and shutdown events such as `hi`, `hey`, and `bye`.

The documented built-in event list does not include a selection-change event.
The plugin context does expose `cx.active.current.selected` and
`cx.active.current.hovered`, so a minimal Lua plugin can observe the required
state. The plugin overview also documents the Windows plugin location under
`%AppData%\\yazi\\config\\plugins\\`.

`--chooser-file` is an open/chooser result, and `--cwd-file` is an exit-time
directory result. Neither is a live snapshot of hover and selection and they
cannot replace the Phase 2 bridge.

## Transport conclusion

Yazi's documented event reporting writes one record per line to `stdout`. In
the current host, Yazi's `stdout` is the same ConPTY stream that carries VT
output. Therefore treating those records as the host's primary protocol would
mix semantic records with terminal bytes and require stream inspection. This
is an architectural inference from the current session boundary, not a claim
that DDS is unsuitable for other external integrations.

The implementation decision is:

1. Use DDS `cd`/`hover` as the source semantics where available.
2. Use a minimal, version-pinned plugin to construct complete snapshots and
   selection updates from Lua context.
3. Emit the host envelope over the independent Phase 2 transport, never over
   the ConPTY output stream.
4. Treat plugin installation/configuration as an explicit compatibility and
   user-consent decision; do not silently overwrite the user's Yazi config.

## Pinned Windows bridge probe

The pinned Windows fixture was run with yazi.exe and ya.exe 26.5.6
(aa52643, 2026-05-05), the current host build, and a temporary
YAZI_CONFIG_HOME. The plugin was enabled only in that temporary init.lua.

- Passing the GUID bridge identifier to --client-id was rejected by Yazi;
  a positive cryptographic numeric client ID is now generated separately.
- The Lua plugin emitted hello sequence 0 and snapshot sequence 1.
- The host accepted live hover and selection updates after sending j and
  Space through CommandLineSession.
- A CJK and space-containing working directory was preserved in the live
  snapshot.
- fs.access opened the current-user named pipe when the plugin received the
  full Windows path \\.\pipe\<name>; the short pipe name did not provide this
  compatibility.
- The bridge session reached unavailable on transport disconnect.

This proves the pinned plugin/pipe path for the snapshot, hover, selection,
UTF-8, and disconnect gates. The host session and probe now implement
reconnection with a fresh hello/snapshot, and the host-side reconnect test
passes. A full real-Yazi reconnect run remains open before production Shell
integration; the selected terminal backend's child-exit notification and
process cleanup were verified separately.

## References

- [Yazi CLI](https://yazi-rs.github.io/docs/cli/)
- [Yazi DDS](https://yazi-rs.github.io/docs/dds/)
- [Yazi plugin context](https://yazi-rs.github.io/docs/plugins/context/)
- [Yazi plugin overview](https://yazi-rs.github.io/docs/plugins/overview/)
