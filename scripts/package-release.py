from __future__ import annotations

import hashlib
import zipfile
from pathlib import Path


root = Path(__file__).resolve().parent.parent
output = root / "bin/Release/netstandard2.1"
pdfium = (
    Path.home()
    / ".nuget/packages/bblanchon.pdfium.win32/137.0.7149"
    / "runtimes/win-x64/native/pdfium.dll"
)
dist = root / "dist"
dist.mkdir(exist_ok=True)
archive_path = dist / "BR-BookSystem.zip"

files = {
    output / "BR_BookSystem.dll": "Mods/BR_BookSystem.dll",
    output / "PDFtoImage.dll": "Mods/PDFtoImage.dll",
    output / "VersOne.Epub.dll": "Mods/VersOne.Epub.dll",
    output / "SharpCompress.dll": "Mods/SharpCompress.dll",
    output / "System.Text.Encoding.CodePages.dll": "Mods/System.Text.Encoding.CodePages.dll",
    root.parent / "ModsPanel/bin/Release/netstandard2.1/ModsPanel.dll": "Mods/ModsPanel.dll",
    root / "UnityAssets/boxroomplus": "Mods/boxroomplus",
    pdfium: "BOXROOM_Data/Plugins/x86_64/pdfium.dll",
    root / "THIRD_PARTY_NOTICES.md": "Mods/BR-BookSystem-THIRD-PARTY-NOTICES.md",
}

missing = [str(path) for path in files if not path.is_file()]
if missing:
    raise FileNotFoundError("Missing release files: " + ", ".join(missing))

with zipfile.ZipFile(archive_path, "w", zipfile.ZIP_DEFLATED) as archive:
    for source, destination in files.items():
        archive.write(source, destination)

digest = hashlib.sha256(archive_path.read_bytes()).hexdigest()
(dist / "SHA256SUMS.txt").write_text(
    f"{digest}  {archive_path.name}\n", encoding="utf-8"
)
print(f"{digest}  {archive_path.name}")
