# BR-BookSystem 2.1.5

- Adds optional per-book `ReadingDirection` metadata with `LeftToRight`,
  `RightToLeft`, and `TopToBottom` values.
- Adds a top-to-bottom layout for wide art books and presentation-style pages,
  showing pages around a horizontal centre spine without an added gutter.
- Reuses the original PageFlip curl, clipping, easing, and moving shadows in the
  vertical layout instead of substituting a separate scrolling animation.
- Counter-rotates CBZ, CBR, PDF, and EPUB page artwork during lazy rendering so
  pages remain upright while the original reader turns vertically.
- Preserves `ReadingDirection` from `meta.json` when Calibre OPF metadata is
  enabled.
- Keeps existing libraries unchanged when the field is absent: Manga defaults
  to right-to-left and all other book types default to left-to-right.
- Restores PageFlip behavior that testers identified as regressed in 2.1.2.

Validation: Release build completed without warnings. The top-to-bottom layout,
spacing, artwork orientation, navigation, original page curl, and the recovered
2.1.2 behavior passed live in-game acceptance across multiple testers.
