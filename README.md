# BR-BookSystem

BR-BookSystem adds physical books and comics to **BOXROOM**. Books behave like
the game's existing GameBoxes and Music Albums: you can take one from the Book
Box, carry it, place it around the room, organize it on supported shelves,
inspect it, and read it.

The mod supports in-game CBZ, CBR, PDF, and EPUB reading.

## What it adds

- A **Book Box** as the first item in the mod catalogue.
- Books that can be held, freely placed, picked up, deleted, and saved with the
  room.
- Proper placement on BOXROOM shelves and compatible media containers.
- Cover artwork on the front, cover-shaped portrait or wide models, and
  automatically sized title text on the spine.
- Demand-loaded, resolution-limited cover textures designed for very large
  libraries without retaining every cover in memory.
- Different physical thicknesses based on the book's `Type` metadata.
- A BOXROOM-style inspection screen showing the book's metadata.
- A **Read** action in place of the normal GameBox/Album action.
- An in-game PageFlip reader for CBZ, CBR, PDF, and EPUB books.
- Per-book reading progress that restores the last open spread for every format.
- Manga right-to-left reading when metadata uses `"Type": "Manga"`.
- A configurable **Book Folder Location**, EPUB font size, and EPUB font type in
  the shared ModsPanel screen.

## Requirements

- BOXROOM with MelonLoader installed.
- BR-MediaAPI 1.0.1 or newer.
- ModsPanel 2.5.0 or newer.
- The format-reader runtime files included in the release ZIP.

BoxMate installs and updates BR-MediaAPI and ModsPanel automatically from the
dependencies declared in `manifest.json`. Manual installations must install
those two required mods separately.

## Installation

1. Close BOXROOM.
2. Download `BR-BookSystem-2.0.1.zip` from the latest GitHub release.
3. Extract the ZIP into the BOXROOM game directory.
4. Confirm that the included files landed in `BOXROOM/Mods`.
5. Remove or disable the older `Boxroom_Books.dll` if it is installed.
6. Start BOXROOM.

The BR-BookSystem release includes:

```text
BOXROOM/
├── Mods/
│   ├── BR_BookSystem.dll
│   ├── PDFtoImage.dll
│   ├── VersOne.Epub.dll
│   ├── ModsPanel.dll
│   ├── boxroomplus
│   ├── SharpCompress.dll
│   ├── System.Text.Encoding.CodePages.dll
│   └── BR-BookSystem-THIRD-PARTY-NOTICES.md
└── BOXROOM_Data/
    └── Plugins/
        └── x86_64/
            └── pdfium.dll
```

`Boxroom_Books.dll` and `BR_BookSystem.dll` must not be enabled together. The
legacy mod still uses the former Books media type and conflicts with this system.

## Setting up your library

1. Create a folder anywhere on your computer for your book library.
2. Put each book in its own subfolder.
3. In BOXROOM, open **Mods**, then select **Mod Settings**.
4. Find the **BR-BookSystem** section.
5. Use **Browse** beside **Book Folder Location** and select the library root.

Example library:

```text
My Books/
├── Freedom Planet 14/
│   ├── meta.json
│   ├── cover.jpg
│   └── Freedom Planet 14.cbz
├── Batman 101/
│   ├── meta.json
│   ├── cover.jpg
│   └── Batman 101.cbr
└── Example Novel/
    ├── meta.json
    ├── cover.jpg
    └── Example Novel.epub
```

The folder selected in Mod Settings is the library root. Do not select an
individual book folder.

Press **Refresh** after adding, removing, or changing books while BOXROOM is
running. The selected library path is stored in BOXROOM's global Settings save;
the mod contains no hardcoded library location.

The same BR-BookSystem settings page has an **EPUB Reader** group. Choose a
**Font Size** from 30–72 px and **Font Type** (Serif, Sans Serif, or Monospace).
The saved choice takes effect the next time an EPUB is opened.

## Book folder format

Every book folder requires:

- `meta.json`
- `cover.jpg`
- One `.cbz`, `.cbr`, `.pdf`, or `.epub` file

Example `meta.json`:

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

`BookID` must be unique and should remain unchanged after the book has been
placed in a saved room. The other fields are displayed during inspection and
`Type` is also used to select the physical thickness.

See [BOOK_FORMAT.md](BOOK_FORMAT.md) for the compact format reference.

## Supported formats

### CBZ and CBR

CBZ and CBR books open inside BOXROOM using the PageFlip reader. Images are
ordered naturally, so `2.jpg` appears before `10.jpg`.

CBR support uses SharpCompress and requires the included
`System.Text.Encoding.CodePages.dll` dependency.

### PDF and EPUB

PDF books open inside the same PageFlip reader as comics. Pages are rendered on
demand around the current spread, so large PDFs do not have to be converted or
retained in memory all at once. Annotations and filled forms are included in the
rendered page image.

EPUB chapter text is read in the book's declared spine order, reflowed using the
selected font settings, and rendered on demand in PageFlip. Embedded JPEG, PNG,
WebP, and other Skia-readable raster illustrations are preserved in reading
order and shown as centered, aspect-fit pages—useful for light novels. Complex
CSS layouts and SVG illustrations are not currently reproduced. If an EPUB
cannot be parsed, the mod falls back to the operating system's associated reader.

## Using books in BOXROOM

1. Place a **Book Box** from the mod catalogue.
2. Select a book from the box to hold it.
3. Place it freely or move it into an available shelf/media-container slot.
4. Pick it up and use BOXROOM's normal inspect input.
5. Select **Read** from the inspection interface.

For CBZ/CBR/PDF/EPUB PageFlip books, use the page controls to move backward and forward.
With no text field focused, `A` or Left Arrow invokes Previous and `D` or Right
Arrow invokes Next. These shortcuts use the existing button actions.
Press `Esc`, `B`, or the controller back action to close the reader.
The current spread is saved by `BookID` after each page turn and when the reader
closes, then restored the next time that book is opened.

Books whose metadata has `"Type": "Manga"` open at the right-hand beginning
with the cover alone on the left and a blank side on the right, then advance
through their pages right-to-left.
Their Previous/Next buttons and
keyboard shortcuts retain their semantic meaning while PageFlip uses the
opposite physical turn direction.

## Saving and restoring

Shelf books and freely placed books are stored in BOXROOM's normal room save.
The save records the unique `BookID`, position, and rotation; metadata and cover
art are loaded again from the configured library.

Keep a book's folder and `BookID` available after placing it. If the library is
missing or a `BookID` changes, the saved object cannot restore its book data.

### One-time media type migration

This pre-Movies release contains a temporary migration for rooms saved when
Books used media type `2`. Load each existing room and save it once. Recognized
loose and shelved Books retain their `BookID`, position, rotation, scale, shelf
slot, and other saved state while their media type is changed to `1002`.

The MelonLoader log reports the number of loose and shelf references migrated,
plus any unrecognized type-`2` shelf references left unchanged. Keep the
configured Book library available during migration so shelf `BookID` values can
be verified. This code does not register type `2` as Books and is intended to be
removed before BOXROOM assigns type `2` to native Movies.

## Troubleshooting

### No books appear

- Confirm **Book Folder Location** points to the library root.
- Open **Mods > Mod Settings** and press **Refresh** in the BR-BookSystem section.
- Check that every book has `meta.json`, `cover.jpg`, and a supported book file.
- Confirm every `BookID` is populated and unique.

### CBR books do not open

- Confirm `SharpCompress.dll` and `System.Text.Encoding.CodePages.dll` are both
  present in `BOXROOM/Mods`.
- Restart BOXROOM after replacing dependencies.

### PDF does not open in PageFlip

- Confirm `PDFtoImage.dll` is present in `BOXROOM/Mods`.
- Confirm `pdfium.dll` is present in `BOXROOM_Data/Plugins/x86_64`.
- Restart BOXROOM after replacing either dependency.

### EPUB does not open in PageFlip

- Confirm `VersOne.Epub.dll` is present in `BOXROOM/Mods`.
- Check the MelonLoader log for malformed EPUB spine or chapter errors.
- If in-game parsing fails, choose an associated desktop EPUB reader when the
  operating system fallback opens.

### Books conflict, disappear, or behave like the old mod

Remove `Boxroom_Books.dll`. Only `BR_BookSystem.dll` should provide Books media
type `1002`.

### Finding useful errors

Routine book loading and interaction are intentionally quiet. The mod logs only
actionable warnings and exception details for malformed library entries,
missing assets/dependencies, failed readers, and failed BOXROOM integration.

## Building from source

The PageFlip sources used by the reader are included under `PageFlip/`; the
separate historical `AssetLoader` project is not required. The source build
references sibling `BR-MediaAPI` and `ModsPanel` repositories. Building the
solution builds ModsPanel automatically; BR-MediaAPI must already have a
Release build available at the documented sibling path.

### Configure the BOXROOM path

Copy `Directory.Build.user.props.example` to `Directory.Build.user.props` and
set `GamePath` to your BOXROOM installation:

```xml
<Project>
  <PropertyGroup>
    <GamePath>C:\Program Files (x86)\Steam\steamapps\common\My Game Room</GamePath>
  </PropertyGroup>
</Project>
```

`Directory.Build.user.props` is Git-ignored so local Steam paths are not
committed. You can instead provide `BOXROOM_GAME_PATH` as an environment
variable or pass `-p:GamePath=...` to MSBuild.

### Build and deploy

```powershell
dotnet build -c Release
```

A normal Release build copies the mod and archive dependencies into
`BOXROOM/Mods`.

To compile without deploying—particularly while BOXROOM is running—use:

```powershell
dotnet build -c Release -p:DeployToGame=false
```

### AssetBundle

The `boxroomplus` AssetBundle contains the Book Box and the loose, shelf,
inspection, and reader prefabs. It is authored separately in PrefabFactory and
must be copied into `BOXROOM/Mods` after prefab changes.

The editable Unity handoff and the matching compiled Windows bundle are checked
in under `UnityAssets/`. Import `UnityAssets/BR-BookSystem-UnityAssets.unitypackage`
when changing the prefabs or integrating the feature into BOXROOM source; see
`UnityAssets/README.md` for the exact Unity version and rebuild procedure.

The C# project can compile without rebuilding the bundle, but changes to prefab
hierarchies, models, anchors, materials, or the PageFlip UI require an updated
`boxroomplus` file.

## Source guide

- `Core.cs` — MelonLoader startup, media registration, and saved-placeable
  routing.
- `BookLibrarySettings.cs` — ModsPanel registration and persisted library path.
- `BookCatalogueBox.cs` — catalogue registration and the source Book Box.
- `PhysicalBooks/Media/` — metadata model, library scanning, and BOXROOM media
  routing.
- `PhysicalBooks/` — loose props, shelf items, pickup behavior, prefab loading,
  placement, and save-state integration.
- `PhysicalBooks/BookCoverTextureCache.cs` — dimension-only cover discovery,
  exposed-shelf demand loading, shared 512/1024px textures, and eviction.
- `BookEnhancements.cs` — held and inspection visuals, spine text, thickness,
  Read prompts, external-document launching, and fallback reading.
- `PageFlipReaderController.cs` — common CBZ/CBR/PDF/EPUB reader lifecycle,
  lazy page rendering, keyboard actions, Manga RTL layout, and progress hooks.
- `EpubPageRenderer.cs` — EPUB spine parsing, text pagination, font rendering,
  and embedded raster illustrations.
- `EpubReaderSettings.cs` — persisted EPUB font controls in ModsPanel.
- `ReadingProgress.cs` — per-`BookID` last-spread persistence for every format.
- `PageFlip/` — vendored page-curl implementation; original attribution is
  retained in `PageFlip/Book.cs`.
- `ComicArchive.cs` — CBZ/CBR extraction and natural page ordering.

The comments in each class explain why its BOXROOM patches and separate prefab
paths are required.

## Third-party components

See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for SharpCompress,
System.Text.Encoding.CodePages, PDFtoImage/PDFium, VersOne.Epub, ModsPanel, and
PageFlip attribution.
