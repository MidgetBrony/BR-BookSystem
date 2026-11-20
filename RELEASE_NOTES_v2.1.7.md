# BR-BookSystem 2.1.7

## Fixed

- Preserves a loose book's stable media identity when BOXROOM links the newly
  created room save state after first placement.
- Prevents freely placed books from returning as empty physical shells after a
  room save and reload.

Already-saved loose books whose room data contains empty `CustomData` cannot be
identified automatically and must be replaced once. Books stored on shelves are
unaffected because their media identities are persisted by the shelf.
