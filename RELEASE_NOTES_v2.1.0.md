# BR-BookSystem 2.1.0

Stable release of the expanded physical-book metadata and reader work tested in
the 2.1.0 beta.

## Added

- Optional direct Calibre `metadata.opf` reading, with `meta.json` BookID and
  Type preserved when both files exist.
- Back-cover summaries, author/publisher credit, and ISBN display.
- Calibre summary and ISBN conversion in `calibretometa.ps1`.
- String-based optional Volume metadata.
- Right-to-left Manga reader behavior matched by Manga-specific physical and
  shelf presentation.
- Lazy CBZ/CBR page extraction and texture decoding with a bounded page cache.

## Changed

- Removed the temporary legacy room migration that scanned and rewrote Book
  media references from type `2` to custom media type `1002`.
- BR-BookSystem continues to use custom media type `1002`. Existing libraries,
  metadata, BookIDs, reading progress, and rooms already saved with type `1002`
  remain compatible.
- Old type-`2` room references are left untouched and are no longer converted
  or required to be converted by BR-BookSystem.

## Compatibility

- Existing BookIDs, saved type-`1002` shelf slots, room placement, and reading
  progress are preserved.
- Books without OPF, Summary, ISBN, Series, or Volume continue to work.
- Remove or disable the legacy `Boxroom_Books.dll`; it is not compatible with
  BR-BookSystem.
