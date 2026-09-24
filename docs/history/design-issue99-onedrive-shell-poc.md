# Issue 99 OneDrive Shell PoC Design

## Boundary

```text
tools/OneDriveShellPoc
        |
        +--> SHParseDisplayName / SHBindToParent
        +--> IShellFolder.GetUIObjectOf
        +--> IContextMenu.QueryContextMenu
        +--> Win32 menu enumeration
        +--> GetCommandString / InvokeCommand
```

The CLI owns a private copy of the minimal Shell interop needed for the
experiment. It does not reference `YaziDesktopHost`, `YaziBridgeState`, or
`WindowsShellContextMenuService`; the host and bridge remain unchanged.

## Command contract

```text
OneDriveShellPoc.exe <path> [--json]
OneDriveShellPoc.exe <path> [--invoke-id <command-id>]
OneDriveShellPoc.exe <path> [--invoke-verb <canonical-verb>]
```

Enumeration is the default. An ID must be a leaf command returned by
`GetMenuItemInfo`; a canonical verb must match exactly one non-empty successful
`GetCommandString` result. The command ID is converted to the Shell-relative
offset for the current `QueryContextMenu` invocation. Neither value is assumed
to be a persistent or cross-environment identifier.

## Resource ownership

The path PIDL is released with `CoTaskMemFree`. The parent `IShellFolder` and
context-menu COM objects are released in `finally` blocks. The temporary Win32
menu is destroyed after enumeration or invocation. Unmanaged buffers used by
`InvokeCommand` are released after the call.

## Enumeration and invocation

`QueryContextMenu` populates a temporary popup menu. The CLI walks menu
positions recursively, records whether an entry has a submenu, and obtains
leaf IDs from `MENUITEMINFO`. The optional canonical-verb lookup uses
`IContextMenu::GetCommandString`; its status is preserved so a failure or empty
value cannot be mistaken for a usable identifier. If a native handler fails,
the PoC's isolated probe process keeps that failure within the diagnostic
boundary.

The ID invocation passes a numeric offset through `CMINVOKECOMMANDINFO`. The
canonical-verb invocation uses `CMINVOKECOMMANDINFOEX` with
`CMIC_MASK_UNICODE`, providing both ANSI `lpVerb` and Unicode `lpVerbW`.
Invocation is always explicit and is never selected from localized display
text.

## Output and privacy

Plain text and `--json` are operator-selected console output. They may contain
target paths, localized menu text, and handler-provided identifiers. The tool
does not write them to the application log. Operators must keep the output
local and must not paste it into GitHub Issues, pull requests, or public logs.

## Error handling

Argument, Shell, HRESULT, and command-selection failures produce bounded
console errors and exit codes. Error text omits target paths and menu output.
The public result record documents only the repeatable investigation method
and the Windows API's identifier contract, not environment-specific results.
