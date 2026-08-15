# Installing BR-BookSystem

1. Install MelonLoader for BOXROOM.
2. Close BOXROOM.
3. Open the downloaded ZIP and copy its `Mods` folder into the BOXROOM game
   directory. Allow Windows to merge the folder.
4. Remove or disable the old `Boxroom_Books.dll` if it is present. Both mods use
   media type `2` and cannot be enabled together.
5. Start BOXROOM and open **Settings**.
6. Under Gameplay, choose **Book Folder Location**, select the folder containing
   your individual book folders, and press **Apply**.

The release installs these files into `BOXROOM/Mods`:

- `BR_BookSystem.dll`
- `boxroomplus`
- `SharpCompress.dll`
- `System.Text.Encoding.CodePages.dll`

Each book folder needs `meta.json`, `cover.jpg`, and one `.cbz`, `.cbr`, `.pdf`,
or `.epub` file. See `BOOK_FORMAT.md` for the complete metadata example.

CBZ and CBR books open in the in-game PageFlip reader. PDF and EPUB books open
with the default reader installed on the operating system.
