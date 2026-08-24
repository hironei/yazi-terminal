# Requirements: Command Palette Navigation Test Seam

Issue: #31

## Goal

Make Command Palette selection behavior independently testable while preserving
the existing keyboard interaction.

## Functional requirements

1. A WPF-independent navigation policy decides whether a key requests list
   movement and returns the movement offset when it does.
2. A WPF-independent index helper handles empty lists, no current selection,
   and wrap-around at both list boundaries.
3. `Up`/`Down` keep their existing navigation behavior. `j`/`k` navigate only
   with no modifiers and an empty query; a non-empty query leaves those keys
   available to filtering input.
4. Keys that are not palette navigation must not be marked handled by the
   palette navigation path.

## Acceptance criteria

- Executable tests cover an empty list, unselected list, first/last wrapping,
  empty/non-empty query behavior for `j`/`k`, and non-navigation filter input.
- The WPF window delegates its key decision and next-index calculation to the
  pure helpers.
- Existing command filtering and command identity tests remain passing.

## Non-goals

This change does not alter command filtering, command execution, text entry,
terminal key capture, or the visual WPF list behavior beyond preserving the
existing navigation policy.
