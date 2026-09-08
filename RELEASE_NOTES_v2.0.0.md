# BR-BookSystem 2.0.0

## Large-library cover system

- Replaces eager loading of every encoded cover with lightweight cover paths and
  header-only image-dimension reads during library scanning.
- Loads shelf artwork only when BOXROOM marks a book cover as exposed.
- Shares reference-counted cover textures instead of creating duplicate decoded
  textures for shelf, placed, held, and inspected representations.
- Limits shelf covers to 512 px and held, placed, and inspected covers to 1024 px,
  then compresses them and releases the readable CPU copy.
- Evicts artwork when no active visual needs it, preventing memory from growing
  indefinitely while moving between shelves.

## Variable-size books

- Preserves cover-aspect-driven book width without decoding the full image during
  room loading.
- Keeps artwork loaded when a differently sized book leaves part of the cover
  visible; only fully covered shelf art is hidden and released.
- Corrects resized shelf-cover orientation while retaining the existing held and
  inspection orientation behavior.

## Validation

- A user library containing roughly 1,000–2,000 placed books reduced its observed
  room-load peak from approximately 27 GB to approximately 8 GB with the initial
  shared-cache build. Version 2.0.0 adds true exposed-only loading and resolution
  caps on top of that result.
- Small-library in-game testing confirmed shelf, held, placed, and inspection
  behavior. Minor cover-orientation edge cases may remain for unusual source art.
