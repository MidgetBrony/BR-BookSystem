# BR-BookSystem 1.6.2

## Reader formats

- Adds lazy, in-game PDF reading with annotations, filled forms, wide-page
  aspect preservation, and the bundled Windows PDFium runtime.
- Adds in-game EPUB reading in declared spine order with configurable Serif,
  Sans Serif, or Monospace fonts and a 30–72 px font-size range.
- Preserves EPUB raster illustrations as centered, aspect-fit pages for light
  novels. Complex CSS and SVG illustrations remain unsupported.
- Retains existing CBZ and CBR reading with natural filename ordering.

## Reading experience

- Saves the last spread independently for every stable `BookID` and restores
  it for CBZ, CBR, PDF, and EPUB books.
- Maps A/Left Arrow to the existing Previous action and D/Right Arrow to the
  existing Next action while no Unity or TextMeshPro input field has focus.
- Hides the inspection interface while PageFlip is open.
- Removes invisible EPUB control characters that could render as square glyphs.

## Manga and physical books

- Enables Manga right-to-left reading when metadata uses `"Type": "Manga"`.
- Opens Manga with the front cover on the left and the virtual blank side on the
  right; subsequent spreads place page 2 right and page 3 left.
- Fits physical books and PageFlip spreads to portrait, square, or wide cover
  artwork without stretching.
- Keeps spine geometry and automatically sized spine text aligned with the
  selected physical thickness.

## Packaging

- Requires BR-MediaAPI 1.0.1+ and ModsPanel 2.5.0+ through the BoxMate manifest.
- Includes PDFtoImage, PDFium, VersOne.Epub, SharpCompress, encoding support,
  ModsPanel, and the `boxroomplus` AssetBundle in the release archive.
- Places third-party notices under `Mods` so the complete archive remains within
  BoxMate's guarded installation roots.
- Uses the versioned `BR-BookSystem-1.6.2.zip` asset name so BoxMate cannot
  receive an older stable-name archive from an intermediary cache.
