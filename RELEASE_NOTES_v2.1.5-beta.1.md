# BR-BookSystem 2.1.5 Beta 1

- Adds optional per-book `ReadingDirection` metadata with `LeftToRight`,
  `RightToLeft`, and `TopToBottom` values.
- Adds a top-to-bottom layout for wide art books and presentation-style pages,
  showing the previous page above the current page around a horizontal spine.
- Reuses the original PageFlip curl, clipping, easing, and moving shadows in the
  vertical layout instead of substituting a separate scrolling animation.
- Counter-rotates CBZ, CBR, PDF, and EPUB page artwork during lazy rendering so
  pages remain upright while the original reader turns vertically.
- Preserves `ReadingDirection` from `meta.json` when Calibre OPF metadata is
  enabled.
- Keeps existing libraries unchanged when the field is absent: Manga defaults
  to right-to-left and all other book types default to left-to-right.

Validation: Release build completed without warnings. Top-to-bottom layout,
spacing, artwork orientation, navigation, and the rotated original page curl
passed live in-game visual acceptance with *Calvin and Hobbes*.
