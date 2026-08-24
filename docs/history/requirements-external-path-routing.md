# External path routing requirements

## Goal

Route a path passed to Yazi Terminal according to its filesystem type: start
Yazi in a directory when the path is a folder, and open an existing file with
Yazi's configured opener/editor when the path is a file.

## Scope

- A normal invocation with an existing directory preserves the current startup
  behavior.
- A normal invocation with an existing file starts Yazi in the file's parent
  directory, reveals the file, and runs Yazi's `open` action.
- `--last-instance` sends either a directory-change request or a file-open
  request to the most recently launched instance.
- A missing path remains a directory argument for compatibility with the
  existing initial-directory behavior.
- File and directory paths remain data passed through `ArgumentList` or the
  validated last-instance protocol; they are not interpolated into a shell.

## Non-goals

- Replacing or overriding the user's Yazi `[open]`/`[opener]` configuration.
- Guessing an editor for file types that Yazi does not configure as editable.
- Changing the Yazi bridge environment or public protocol identifiers.
- Supporting multiple positional paths.

## Acceptance criteria

- Existing directory and no-argument startup tests remain passing.
- An existing file is represented by its full path plus its parent directory.
- Normal startup sends `reveal <file>` followed by `open` after Yazi is ready.
- Last-instance control frames distinguish `cd` from `open` and route both
  successfully through the control server.
- Paths containing spaces, CJK characters, and Windows separators retain their
  argument boundaries.
- README and the user manual describe folder and file invocation behavior.
- Automated tests and live Yazi/editor behavior are reported separately.
