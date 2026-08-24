# BR-BookSystem Unity assets

This folder contains both sides of the book-prefab handoff.

- `BR-BookSystem-UnityAssets.unitypackage` is the editable Unity source package.
  It contains `BookBox`, `MediaBox`, `MediaBoxShelf`, `BookReader`, their
  materials and shader dependencies, the PageFlip sources/sprites, and the
  AssetBundle build/export editor scripts.
- `boxroomplus` is the Windows runtime AssetBundle currently consumed by
  `BookAssetBundle.cs`. Install it beside `BR_BookSystem.dll` in BOXROOM's
  `Mods` folder.

## Editing and rebuilding

1. Use Unity `6000.4.11f1`, matching BOXROOM and the authoring project.
2. Import `BR-BookSystem-UnityAssets.unitypackage` into the BOXROOM Unity
   project or a compatible prefab-authoring project.
3. Keep the four prefab asset-bundle name set to `boxroomplus`.
4. Run **Boxroom+ > Build AssetBundles**.
5. Take the Windows `boxroomplus` output and place it beside the mod DLL.

The export script uses `ExportPackageOptions.IncludeDependencies`; exporting
the prefab files alone omits required materials, sprites, shaders, and metadata.
