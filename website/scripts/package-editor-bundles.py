# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Package canonical editor bundles as reproducible website downloads."""

from pathlib import Path
from zipfile import ZIP_DEFLATED, ZipFile, ZipInfo
import sys


ROOT = Path(__file__).resolve().parents[2]
sys.dont_write_bytecode = True
sys.path.insert(0, str(ROOT / 'scripts'))
from website_assets import is_metadata

OUTPUT = ROOT / "artifacts/website/public/downloads"
VARIANTS = (
    ("TextMate", "GameEventScript-TextMate.zip"),
    ("TextMate Classic", "GameEventScript-TextMate-Classic.zip"),
)


def package_bundles():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    for variant, filename in VARIANTS:
        source = ROOT / "tools/editors" / variant
        files = [("LICENSE", ROOT / "LICENSE")]
        for bundle in ("GameEventScript.tmbundle", "GameEventScriptAssembler.tmbundle"):
            for file in sorted((source / bundle).rglob("*")):
                if is_metadata(file.relative_to(source)):
                    continue
                if file.is_symlink():
                    raise ValueError(f"Bundle must not contain symlinks: {file}")
                if file.is_file():
                    files.append((file.relative_to(source).as_posix(), file))
        with ZipFile(OUTPUT / filename, "w") as archive:
            for name, file in files:
                entry = ZipInfo(name, date_time=(2026, 1, 1, 0, 0, 0))
                entry.create_system = 3
                entry.external_attr = 0o100644 << 16
                entry.compress_type = ZIP_DEFLATED
                archive.writestr(entry, file.read_bytes())
        print(f"Packaged {filename} ({len(files)} files).")


if __name__ == "__main__":
    package_bundles()
