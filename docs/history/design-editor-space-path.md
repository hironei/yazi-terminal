# Design: Preserve Space-Containing Editor Paths

## Typed Yazi action arguments

`YaziCommandController` retains its string-based action API for command-palette
commands, which need shell-like tokenization for user-authored action strings.
It also exposes a typed command-plus-arguments path for host-generated actions.
The typed path appends each value directly to `ProcessStartInfo.ArgumentList`.

`YaziFileController` uses the typed path for `reveal`, passing the full file
path as one argument, and continues to send `open` as a separate action. The
existing path request sequencer therefore preserves the same transaction and
ACK behavior while removing the lossy string round-trip from file opening.

## Compatibility and verification

The resulting `ya emit-to <client> reveal <path>` invocation is unchanged at
the process boundary for paths without spaces. Paths containing spaces and
Unicode characters retain their argument boundaries. No path contents are
logged, persisted, or sent through a shell.
