# Requirements: Preserve Space-Containing Editor Paths

Issue: #63

## Goal

Opening a file whose full Windows path contains spaces must reach Yazi's
configured opener/editor as the same single file path.

## Scope

- Host-originated file opens use the existing `reveal` followed by `open`
  transaction.
- The file path is passed as a typed process argument and is not embedded in a
  shell-like command string.
- Directory routing and user-defined Yazi opener configuration remain
  unchanged.

## Non-goals

- Selecting or replacing the user's editor or opener.
- Changing the Yazi bridge, last-instance protocol, or command-line syntax.
- Supporting multiple file paths in one invocation.

## Acceptance criteria

- A path such as `C:\Program Files\Yazi Terminal\notes.txt` is one item in the
  `ya emit-to` argument list.
- Existing command-palette action tokenization remains unchanged.
- Existing directory, file-routing, last-instance, and path-serialization
  tests remain passing.
- Automated verification and live Yazi/editor behavior are reported
  separately.
