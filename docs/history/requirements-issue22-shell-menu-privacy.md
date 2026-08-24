# Issue 22 Shell Context Menu Log Privacy Requirements

## Goal

Prevent Windows Shell context-menu display strings from being persisted to the
host's `%LOCALAPPDATA%\YaziTerminal\app.log` diagnostic file.

## Requirements

- The host must continue to create, display, and invoke the ordinary Windows
  Shell context menu for an actionable bridge target.
- The Shell-menu path must not enumerate menu item captions merely for
  diagnostics, before or after command selection.
- Category-level diagnostics such as interface kind, command identifier,
  operation stage, exception type, and HRESULT may remain available. Menu item
  captions and filesystem paths must not be written by this path.
- The executable test suite must guard against reintroducing the menu-text
  enumeration/logging implementation.

## Non-goals

- Changing Windows Shell command behavior, menu composition, or the general
  application-log location and retention policy.
- Retroactively modifying existing log files.

## Acceptance criteria

1. The service contains no `LogMenuItems` path or native menu-text retrieval
   used for logging.
2. The context-menu command invocation flow remains intact.
3. The prescribed build and executable test suite pass.
4. A manual Windows Shell menu invocation remains a release-validation gate.
