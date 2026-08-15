# BR-BookSystem

BR-BookSystem adds physical books and comics to **BOXROOM**. Books behave like
the game's existing GameBoxes and Music Albums: you can take one from the Book
Box, carry it, place it around the room, organize it on supported shelves,
inspect it, and read it.

The mod supports in-game CBZ/CBR reading as well as PDF/EPUB books through your
normal desktop reader.

## What it adds

- A **Book Box** as the first item in the mod catalogue.
- Books that can be held, freely placed, picked up, deleted, and saved with the
  room.
- Proper placement on BOXROOM shelves and compatible media containers.
- Cover artwork on the front and automatically sized title text on the spine.
- Different physical thicknesses based on the book's `Type` metadata.
- A BOXROOM-style inspection screen showing the book's metadata.
- A **Read** action in place of the normal GameBox/Album action.
- An in-game PageFlip reader for CBZ and CBR comics.
- PDF and EPUB launching through the operating system's default application.
- A configurable **Book Folder Location** in BOXROOM's Settings menu.

## Requirements

- BOXROOM with MelonLoader installed.
- The four runtime files included in the release ZIP.
- An application associated with `.pdf` or `.epub` files if you want to use
  those formats.

## Installation

1. Close BOXROOM.
2. Download `BR-BookSystem-v1.0.11.zip` from the GitHub release.
3. Extract the ZIP into the BOXROOM game directory.
4. Confirm that the included files landed in `BOXROOM/Mods`.
5. Remove or disable the older `Boxroom_Books.dll` if it is installed.
6. Start BOXROOM.

The release includes:

```text
BOXROOM/
└── Mods/
    ├── BR_BookSystem.dll
    ├── boxroomplus
    ├── SharpCompress.dll
    └── System.Text.Encoding.CodePages.dll
```

`Boxroom_Books.dll` and `BR_BookSystem.dll` must not be enabled together. Both
claim BOXROOM media type `2`, which causes conflicting book systems.

## Setting up your library

1. Create a folder anywhere on your computer for your book library.
2. Put each book in its own subfolder.
3. In BOXROOM, open **Settings**.
4. Find **Book Folder Location** under Gameplay.
5. Select the library folder and press **Apply**.

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

The folder selected in Settings is the library root. Do not select an
individual book folder.

Press **Refresh** after adding, removing, or changing books while BOXROOM is
running. The selected library path is stored in BOXROOM's global Settings save;
the mod contains no hardcoded library location.

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

PDF and EPUB books open outside BOXROOM using the default application registered
with the operating system. Install and associate a compatible reader before
using **Read**. BR-BookSystem does not contain its own PDF or EPUB renderer.

## Using books in BOXROOM

1. Place a **Book Box** from the mod catalogue.
2. Select a book from the box to hold it.
3. Place it freely or move it into an available shelf/media-container slot.
4. Pick it up and use BOXROOM's normal inspect input.
5. Select **Read** from the inspection interface.

For CBZ/CBR PageFlip books, use the page controls to move backward and forward.
Press `Esc`, `B`, or the controller back action to close the reader.

## Saving and restoring

Shelf books and freely placed books are stored in BOXROOM's normal room save.
The save records the unique `BookID`, position, and rotation; metadata and cover
art are loaded again from the configured library.

Keep a book's folder and `BookID` available after placing it. If the library is
missing or a `BookID` changes, the saved object cannot restore its book data.

## Troubleshooting

### No books appear

- Confirm **Book Folder Location** points to the library root.
- Press **Apply** or **Refresh** in Settings.
- Check that every book has `meta.json`, `cover.jpg`, and a supported book file.
- Confirm every `BookID` is populated and unique.

### CBR books do not open

- Confirm `SharpCompress.dll` and `System.Text.Encoding.CodePages.dll` are both
  present in `BOXROOM/Mods`.
- Restart BOXROOM after replacing dependencies.

### PDF or EPUB does not open

- Open the file from Windows first.
- Choose or install a default PDF/EPUB reader when prompted.
- Return to BOXROOM and select **Read** again.

### Books conflict, disappear, or behave like the old mod

Remove `Boxroom_Books.dll`. Only `BR_BookSystem.dll` should provide Books media
type `2`.

### Finding useful errors

Routine book loading and interaction are intentionally quiet. The mod logs only
actionable warnings and exception details for malformed library entries,
missing assets/dependencies, failed readers, and failed BOXROOM integration.

## Building from source

The repository is self-contained for code builds. The PageFlip sources used by
the reader are included under `PageFlip/`; the separate historical
`AssetLoader` project is not required.

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

The C# project can compile without rebuilding the bundle, but changes to prefab
hierarchies, models, anchors, materials, or the PageFlip UI require an updated
`boxroomplus` file.

## Source guide

- `Core.cs` — MelonLoader startup, media registration, and saved-placeable
  routing.
- `BookLibrarySettings.cs` — native Settings panel and persisted library path.
- `BookCatalogueBox.cs` — catalogue registration and the source Book Box.
- `PhysicalBooks/Media/` — metadata model, library scanning, and BOXROOM media
  routing.
- `PhysicalBooks/` — loose props, shelf items, pickup behavior, prefab loading,
  placement, and save-state integration.
- `BookEnhancements.cs` — held and inspection visuals, spine text, thickness,
  Read prompts, external-document launching, and fallback reading.
- `PageFlipReaderController.cs` — connects BookData and comic pages to PageFlip.
- `PageFlip/` — vendored page-curl implementation; original attribution is
  retained in `PageFlip/Book.cs`.
- `ComicArchive.cs` — CBZ/CBR extraction and natural page ordering.

The comments in each class explain why its BOXROOM patches and separate prefab
paths are required.

## Third-party components

See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for SharpCompress,
System.Text.Encoding.CodePages, and PageFlip attribution.
