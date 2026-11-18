# BR-BookSystem 2.1.6

## Fixed

- Removes the bundled `ModsPanel.dll` from the release archive and local deployment target.
- Leaves ModsPanel installation and updates exclusively to the dependency declared in `manifest.json`.
- Prevents BR-BookSystem installation or repair from overwriting a newer shared ModsPanel release.

The book, comic, PDF, EPUB, asset-bundle, and native PDFium runtime contents are unchanged from 2.1.5.
