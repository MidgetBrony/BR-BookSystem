# Book cache format

Choose the Books Library root in BOXROOM's Settings menu, then put one folder
per book beneath that selected folder.

Each book folder must contain `meta.json`, `cover.jpg`, and one supported book
file. CBZ/CBR pages are ordered naturally (`1.jpg`, `2.jpg`, `10.jpg`).
The physical book automatically follows the aspect ratio of `cover.jpg`, so
portrait, square, and wide landscape collections use matching models. The
PageFlip reader likewise fits its spread to the typical page shape in the
archive without stretching the artwork.

```json
{
  "Version": 1,
  "BookID": "freedom_planet_vol14",
  "Title": "Freedom Planet #14",
  "Series": "Freedom Planet",
  "Volume": 14,
  "Author": "Tom Fulp",
  "Publisher": "GalaxyTrail",
  "Language": "en",
  "Type": "Comic"
}
```

Set `"Type": "Manga"` to enable right-to-left page order and page-turn direction.

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
