# Book cache format

Choose the Books Library root in BOXROOM's Settings menu, then put one folder
per book beneath that selected folder.

Each book folder must contain `meta.json`, `cover.jpg`, and one supported book
file. CBZ/CBR pages are ordered naturally (`1.jpg`, `2.jpg`, `10.jpg`).

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

The book file can use any filename ending in `.cbz`, `.cbr`, `.pdf`, or
`.epub`. CBZ and CBR are read through SharpCompress and PageFlip. CBR
additionally requires `System.Text.Encoding.CodePages.dll` beside the mod DLL.
PDF and EPUB files open through the operating system's associated reader.

Books appear in the Book Box and can be placed on supported shelves. Pick up a
book and use BOXROOM's normal inspect action; the inspect prompt becomes
**Read** and opens PageFlip for CBZ/CBR or the installed OS reader for PDF/EPUB.
Press `Esc`, `B`, or the controller back button to close PageFlip.
