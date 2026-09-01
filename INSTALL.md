# Installing BR-BookSystem

1. Install MelonLoader for BOXROOM.
2. Install BR-MediaAPI 1.0.1+ and ModsPanel 2.5.0+, or let BoxMate install the
   declared dependencies automatically.
3. Close BOXROOM.
4. Open the downloaded ZIP and copy its `Mods` and `BOXROOM_Data` folders into
   the BOXROOM game directory. Allow Windows to merge both folders.
5. Remove or disable the old `Boxroom_Books.dll` if it is present. Both mods use
   media type `2` and cannot be enabled together.
6. Start BOXROOM and open **Mods**, then **Mod Settings**.
7. In the **BR-BookSystem** section, use **Browse** beside **Book Folder
   Location** and select the folder containing your individual book folders.

The release installs these files into `BOXROOM/Mods`:

- `BR_BookSystem.dll`
- `PDFtoImage.dll`
- `VersOne.Epub.dll`
- `ModsPanel.dll`
- `boxroomplus`
- `SharpCompress.dll`
- `System.Text.Encoding.CodePages.dll`

The release also installs `pdfium.dll` into
`BOXROOM/BOXROOM_Data/Plugins/x86_64` for in-game PDF rendering.

Each book folder needs `meta.json`, `cover.jpg`, and one `.cbz`, `.cbr`, `.pdf`,
or `.epub` file. See `BOOK_FORMAT.md` for the complete metadata example.

CBZ, CBR, PDF, and EPUB books open in the in-game PageFlip reader. The reader
saves progress per `BookID`. Use `"Type": "Manga"` for right-to-left reading.
The BR-BookSystem Mod Settings section also provides EPUB font size and font
type controls.
