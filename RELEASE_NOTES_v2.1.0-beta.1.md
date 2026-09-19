# BR-BookSystem 2.1.0-beta.1

Numbered BoxMate Beta prerelease for the expanded physical-book metadata and
reader work.

## Added

- Optional direct Calibre `metadata.opf` reading, with `meta.json` BookID and
  Type preserved when both files exist.
- Back-cover summaries, author/publisher credit, and ISBN display.
- Calibre summary and ISBN conversion in `calibretometa.ps1`.
- String-based optional Volume metadata.
- Right-to-left Manga reader behavior matched by Manga-specific physical and
  shelf presentation.
- Lazy CBZ/CBR page extraction and texture decoding with a bounded page cache.

## Compatibility

- Existing BookIDs, saved shelf slots, room placement, and reading progress are
  preserved.
- Books without OPF, Summary, ISBN, Series, or Volume continue to work.
- BoxMate Stable remains on 2.0.1; select the Beta channel for this prerelease.
