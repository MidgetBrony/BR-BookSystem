# Book cache format

Choose the Books Library root in BOXROOM's Settings menu, then put one folder
per book beneath that selected folder.

Each book folder must contain `meta.json`, `cover.jpg`, and one supported book
file. Alternatively, enable **Use Calibre metadata.opf when available** and a
normal Calibre book folder can use `metadata.opf` instead of `meta.json`.
CBZ/CBR pages are ordered naturally (`1.jpg`, `2.jpg`, `10.jpg`).
The physical book automatically follows the aspect ratio of `cover.jpg`, so
portrait, square, and wide landscape collections use matching models. The
PageFlip reader likewise fits its spread to the typical page shape in the
archive without stretching the artwork.

```json
{
  "Version": 2,
  "BookID": "freedom_planet_vol14",
  "Title": "Freedom Planet #14",
  "Series": "Freedom Planet",
  "Volume": "14",
  "Author": "Tom Fulp",
  "Publisher": "GalaxyTrail",
  "ISBN": "9781234567897",
  "Language": "en",
  "Type": "Comic",
  "Summary": "A concise description printed on the physical back cover."
}
```

`Summary` is optional. When present, BR-BookSystem typesets it on the physical
back cover using BOXROOM's own font. Use plain text; long summaries are fitted
and ellipsized to the available cover area.

`Volume` is also optional and is only display metadata for books in a series.
It is a string so fractional and named entries such as `"1.5"`, `"Special"`,
or `"Part II"` are preserved. BR-BookSystem does not use it for identity,
sorting, saving, reading progress, or the physical book model. Existing numeric
`Volume` values from Version 1 metadata remain compatible.

`ISBN` is optional. When supplied, it is printed in the lower-right back-cover
footer. Calibre OPF reading and the converter recognize ISBN identifiers; books
without one simply omit the footer entry.

The included `calibretometa.ps1` converter reads Calibre's description into
`Summary`, removes HTML formatting, and only emits `Volume` when Calibre has a
series index. Use `-Force` to regenerate an existing `meta.json`. Conversion is
optional when direct OPF reading is enabled. OPF metadata takes priority when
present; books without it continue using `meta.json`. Calibre subjects/tags such
as `Manga`, `Comic`, `Graphic Novel`, and `Magazine` determine the physical/read
type, with CBZ/CBR defaulting to `Comic` and other formats to `Book`.

When both metadata files exist, BR-BookSystem deliberately preserves `BookID`
and `Type` from `meta.json`. `BookID` is the stable identity stored by shelf and
room saves, while `Type` controls physical thickness and Manga reading direction.
All descriptive fields still come from Calibre. A folder containing only OPF
uses Calibre's UUID as its stable ID.

Set `"Type": "Manga"` to make the physical book right-bound and enable
right-to-left page order and page-turn direction. Other book types remain
left-bound.

The book file can use any filename ending in `.cbz`, `.cbr`, `.pdf`, or
`.epub`. CBZ and CBR are read through SharpCompress and PageFlip. CBR
additionally requires `System.Text.Encoding.CodePages.dll` beside the mod DLL.
PDF pages render on demand inside PageFlip through the included PDFium runtime.
EPUB chapter text is reflowed and paginated inside PageFlip in spine order.
Font size and font type are configurable in the BR-BookSystem Mod Settings.
Embedded raster illustrations are preserved in reading order as aspect-fit pages.

Books appear in the Book Box and can be placed on supported shelves. Pick up a
book and use BOXROOM's normal inspect action; the inspect prompt becomes
**Read** and opens PageFlip for CBZ/CBR/PDF/EPUB.
Press `Esc`, `B`, or the controller back button to close PageFlip.
Reading progress is saved separately for each stable `BookID` across all four
supported formats.
When no text field has focus, `A`/Left Arrow invokes Previous and `D`/Right
Arrow invokes Next without replacing the existing reader buttons.
