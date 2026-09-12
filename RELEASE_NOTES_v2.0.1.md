# BR-BookSystem 2.0.1

## Required pre-Movies migration

- Moves Books from media type `2` to custom-media ID `1002`, leaving type `2`
  available for BOXROOM's upcoming native Movies implementation.
- Temporarily migrates recognized legacy Book references in both loose
  placements and shelf slots when a room is loaded.
- Preserves each BookID, shelf slot, transform, scale, paint, and other room
  state while changing only the persisted media type.
- Reports migrated loose, shelf, total, unrecognized, and invalid-record counts
  in the MelonLoader log.
- Does not register or route type `2` as Books. The migration code is intended
  to be removed in a later release before native Movies ships.

## What users must do

Install 2.0.1 before the BOXROOM Movies update. Load every existing room that
contains Books and save it once. The saved Book references will then use type
`1002`.

Shelf references are verified against the configured Book library, so keep the
library available while migrating. Unrecognized type-`2` shelf references are
left unchanged and reported in the log.

## Validation

- A beta tester confirmed the migration successfully converted an existing
  BR-BookSystem room and the migrated Books continued to work.
- Release build and mixed-save migration tests pass, including state
  preservation, unknown-reference isolation, and repeat-run idempotency.
