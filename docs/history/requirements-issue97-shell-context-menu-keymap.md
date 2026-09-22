# Issue #97 Requirements: Yazi Keymap Shell Context Menu

## Goal

Allow a user-defined Yazi manager keymap to request the existing Windows Shell
context menu for the current Yazi target, using the target's resolved
filesystem path when Junctions or SymbolicLinks are present.

## Scope

- The bundled `yazi-desktop-host.yazi` plugin exposes `context-menu` and
  `context-menu-cwd` plugin actions.
- `context-menu` uses the existing selected-then-hovered precedence.
- `context-menu-cwd` uses the current directory only.
- The bridge carries a bounded command request without carrying a filesystem
  path from the plugin to the host.
- The host resolves every selected target path through Junction/SymbolicLink
  ancestors before invoking the existing classic Windows Shell context-menu
  service.
- Existing right-click, Shift+F10, and Ctrl+Shift+F10 behavior remains
  available and uses the same final-path resolution boundary.

## Non-goals

- Direct OneDrive link-copy/share operations; those remain Issue #98.
- OneDrive or SharePoint URL generation, Graph API calls, or provider-specific
  path detection.
- Rebuilding the Windows 11 modern context menu.
- Adding host-side key bindings or changing Yazi's keymap precedence.
- Replacing the existing Shell COM adapter or its same-parent multi-selection
  contract.

## Functional requirements

1. A Yazi keymap may bind `plugin yazi-desktop-host --args=context-menu` and
   open the selected-or-hovered Shell menu.
2. A Yazi keymap may bind `plugin yazi-desktop-host --args=context-menu-cwd`
   and open the current-directory Shell menu.
3. The host resolves all selected paths before evaluating the existing
   same-parent Shell requirement; it never silently falls back to the original
   path or to hovered state after a resolution failure.
4. The final-path resolver handles a link on the item itself, a link in a
   parent directory, and multiple link layers with a finite resolution limit.
5. A malformed, unknown, unavailable, stale, or unresolvable command request
   does not crash the host or terminate a usable bridge session.
6. Existing mouse and keyboard Shell context-menu paths remain wired to the
   existing target resolver and context-menu service.

## Non-functional and security requirements

- Preserve the `yazi-desktop-host/1` bridge identity and existing snapshot,
  state, heartbeat, and reconnect behavior.
- Accept command requests only after the bridge handshake and validate the
  command against a fixed allowlist.
- Do not accept a path, executable, Shell verb, or menu text from the plugin
  request. The host owns target selection and Shell invocation.
- Keep COM and filesystem failures at the Shell feature boundary with category
  logging; do not persist paths or menu labels.
- Do not add a dependency or launch PowerShell for link resolution.

## Acceptance criteria

- Static/plugin tests show both requested keymap command forms are documented
  and the plugin entry sends the expected bridge command.
- Protocol/session tests cover command parsing, handshake/sequence handling,
  and invalid command isolation.
- Resolver tests cover selected/hovered/cwd precedence, item links, parent
  links, multiple links, resolution-limit failure, and no silent fallback.
- Build and executable tests pass using the repository commands.
- A manual Windows/Yazi acceptance run confirms both keymap commands open the
  menu, a Junction/SymbolicLink into a OneDrive directory produces the menu
  from the real path, and existing right-click/Shift+F10/Ctrl+Shift+F10 still
  work. Automated tests do not claim this GUI/Shell/OneDrive result.
