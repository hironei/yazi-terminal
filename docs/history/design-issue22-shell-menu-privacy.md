# Issue 22 Shell Context Menu Log Privacy Design

## Decision

`WindowsShellContextMenuService` does not inspect native `MENUITEMINFO` values
after `QueryContextMenu`. It retains the existing `IContextMenu` query, popup
tracking, and command-offset invocation flow, while writing only category-level
events through `AppLogger`.

## Rationale

Windows Shell extensions supply menu captions, which can contain file names or
other user data. Capturing captions creates an unnecessary persistent-data
path. Removing the diagnostic enumeration closes that path without changing
the Shell command passed to `InvokeCommand`.

## Error handling and verification

The existing boundary still records the operation stage, exception type, and
HRESULT for failures. A reflection-based executable test asserts that the
service has no private menu-text enumeration method, native menu-text import,
or `MIIM_STRING` flag. Real Shell extension behavior and menu display require
manual Windows validation.

## Traceability

| Requirement | Design element |
| --- | --- |
| Do not persist menu captions | Remove native menu-text enumeration and logging. |
| Preserve Shell operation | Retain query, popup tracking, and command invocation. |
| Regression protection | Structural executable test. |
