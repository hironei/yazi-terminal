# Phase 5 Explorer-to-Yazi Drop Requirements

## Scope and interpretation

The attached product requirement is a product specification, not an
instruction to copy every proposal verbatim. This phase covers the
Explorer/Desktop-to-Yazi direction from the attachment's Phase 5 priority.

The host accepts an OLE drop over the embedded terminal and delegates it to
the Windows Shell folder drop target for Yazi's current directory. Yazi and
the Shell remain authoritative for state and file-operation semantics.

## Functional requirements

- Register an OLE `IDropTarget` on the actual native terminal HWND while the
  host session is alive.
- On `DragEnter`, `DragOver`, and `Drop`, resolve the latest available
  filesystem `cwd` from the bridge.
- Delegate the original OLE data object to that directory's Shell
  `IDropTarget`; do not enumerate files or implement copy/move logic in the
  host.
- Preserve the Shell-provided effect negotiation, including normal Copy/Move,
  Ctrl/Shift modifiers, same- and cross-volume behavior, overwrite prompts,
  elevation, cancellation, links, and provider-specific virtual data.
- Reject unavailable bridge state, non-filesystem URLs, empty state, missing
  terminal HWND, or Shell target creation failures with `DROPEFFECT_NONE`.
- Revoke OLE registration before the terminal HWND is destroyed.
- Contain COM/OLE exceptions and log only categories, never dropped file
  contents or unnecessary full paths.

## Non-goals

- Dropping on a particular Yazi pane or hovered item; v1 targets `cwd`.
- Changing `cwd` with ordinary Yazi navigation keys while an Explorer drag is
  active; the OLE drag loop owns ordinary keyboard input during the drag.
- A custom file-operation engine, overwrite policy, progress UI, or refresh
  protocol owned by the host.
- Claiming support for third-party Shell extensions, cloud placeholders, or
  virtual file providers without manual validation.

## Acceptance criteria

- Pure tests cover `cwd` resolution and rejection of unavailable, URL, and
  empty bridge states.
- Build and existing tests pass without warnings or errors.
- Manual Windows validation confirms that Explorer can drop a file and a
  directory into Yazi's current directory, with the Shell choosing the
  negotiated operation and prompts.
- Manual validation covers Ctrl/Shift, same-/cross-volume behavior, cancel,
  and a destination that changes before the final drop.
- Closing the host revokes the drop target and leaves no owned Yazi process.
- A rejected or failed drop leaves the host and bridge usable.

## Manual gates and residual risk

- The native HWND and WPF airspace boundary require real Explorer interaction.
- Shell extension behavior, elevation prompts, virtual files, and cloud
  providers are compatibility observations, not headless guarantees.

## Manual validation result

The 2026-08-22 Windows run confirmed the required direction and modifier
behavior:

- Explorer file/folder → Yazi: PASS
- Ctrl/Shift Copy/Move negotiation: PASS
- Yazi → Explorer/Desktop: PASS
- Keyboard navigation that changes `cwd` during an active Explorer drag: not
  required; the drag loop does not deliver ordinary keys to Yazi.

The last item is accepted as a v1 non-goal. No system-wide keyboard hook or
other input interception is used to work around the OLE drag-loop boundary.
