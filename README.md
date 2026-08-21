# Yazi Desktop Host

Initial Phase 1 implementation of a Windows WPF host for the ordinary Yazi
terminal file manager.

The reviewed scope and known blockers are documented in:

- [Phase 1 requirements](docs/requirements-yazi-windows-gui-frontend.md)
- [Phase 1 design](docs/design-yazi-windows-gui-frontend.md)

The repository is intentionally empty of runtime code until the terminal
renderer dependency is approved and restored. Later Shell integration phases
must not be implemented by parsing terminal screen text.
