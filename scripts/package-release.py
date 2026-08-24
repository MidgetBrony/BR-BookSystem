from __future__ import annotations

import hashlib
import zipfile
from pathlib import Path


root = Path(__file__).resolve().parent.parent
output = root / "bin/Release/netstandard2.1"
dist = root / "dist"
dist.mkdir(exist_ok=True)
archive_path = dist / "BR-BookSystem.zip"

files = {
    output / "BR_BookSystem.dll": "Mods/BR_BookSystem.dll",
    output / "SharpCompress.dll": "Mods/SharpCompress.dll",
    output / "System.Text.Encoding.CodePages.dll": "Mods/System.Text.Encoding.CodePages.dll",
    root / "UnityAssets/boxroomplus": "Mods/boxroomplus",
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
