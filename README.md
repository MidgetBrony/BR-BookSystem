# BR-BookSystem

A MelonLoader mod that adds Books as BOXROOM media type `2`.

## Features

- Uses a configurable Books Library folder in BOXROOM's Settings menu.
- Supports CBZ and CBR archives in PageFlip.
- Opens PDF and EPUB books in the operating system's default reader.
- Adds a Book Box as the first mod-catalogue item.
- Supports shelf placement, free placement, pickup, inspection, reading, and
  room-save restoration.
- Uses book `Type` to select physical thickness while keeping placed books
  anchored to their supporting surface.

See [BOOK_FORMAT.md](BOOK_FORMAT.md) for the cache schema.

## Source layout

- `Core.cs` — mod bootstrap, media registration, and saved-placeable routing.
- `PhysicalBooks/` — media data, cache loading, physical props, shelf items,
  pickup, placement, and save-state integration.
- `BookEnhancements.cs` — held/inspect visuals, spine text, thickness, prompts,
  and the fallback reader.
- `PageFlipReaderController.cs` — PageFlip reader integration.
- `ComicArchive.cs` — CBZ/CBR page extraction.
- `BookCatalogueBox.cs` — Book Box catalogue item and unplaced-book filling.
- `BookLibrarySettings.cs` — native Settings panel and persisted library path.

The PageFlip `Book.cs` and `AutoFlip.cs` sources are linked from the sibling
`AssetLoader` repository. The `boxroomplus` AssetBundle is authored and built in
`C:\Users\Rusty\PrefabFactory`.

## Build

`Directory.Build.props` defines the local BOXROOM installation through
`GamePath`.

```powershell
dotnet build -c Release
```

Normal builds deploy the mod DLL and archive dependencies into BOXROOM's
`Mods` directory. To compile without deploying—especially while BOXROOM is
running—use:

```powershell
dotnet build -c Release -p:DeployToGame=false
```

The AssetBundle is separate and must be copied from
`C:\Users\Rusty\PrefabFactory\AssetBundles\boxroomplus` to BOXROOM's `Mods`
directory after prefab changes.

## Runtime files

The following files belong directly in BOXROOM's `Mods` directory:

- `BR_BookSystem.dll`
- `boxroomplus`
- `SharpCompress.dll`
- `System.Text.Encoding.CodePages.dll`

Do not leave the older `Boxroom_Books.dll` enabled; it also claims media type
`2` and conflicts with this mod.

## Logging policy

Routine book loading and interaction are intentionally quiet. The mod logs
only actionable warnings and errors, including malformed cache entries,
missing assets/dependencies, and failed reader or Harmony integration paths.
