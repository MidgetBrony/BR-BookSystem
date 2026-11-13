# BR-BookSystem 2.1.3

- Adds per-book `ReadingDirection` metadata with `LeftToRight`, `RightToLeft`,
  and `TopToBottom` modes.
- Stacks consecutive pages vertically for wide art-book and presentation-style
  books instead of shrinking them into a side-by-side spread.
- Adds masked vertical page-pair transitions: Next scrolls upward and Previous
  reverses the animation.
- Keeps existing behavior unchanged when `ReadingDirection` is absent: Manga
  defaults to right-to-left and other book types default to left-to-right.
- Preserves custom reading direction when Calibre OPF metadata is enabled.
