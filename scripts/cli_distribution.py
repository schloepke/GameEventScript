# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Shared identities and archive verification for standalone CLI downloads."""

import hashlib
import json
from pathlib import Path, PurePosixPath
import re
import shutil
import tarfile
import zipfile

ROOT = Path(__file__).resolve().parent.parent
TARGETS = {
    "csharp": ("win-x64", "win-arm64", "linux-x64", "linux-arm64", "osx-x64", "osx-arm64"),
    "swift": ("linux-x64", "linux-arm64", "osx-x64", "osx-arm64"),
}


def version(value):
    number = r"(?:0|[1-9][0-9]*)"
    if not re.fullmatch(rf"{number}\.{number}\.{number}(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?", value):
        raise ValueError("Use a release version such as 0.2.0 or 0.2.0-rc.1 (without a v prefix).")
    for part in value.partition("-")[2].split("."):
        if part.isdigit() and len(part) > 1 and part.startswith("0"):
            raise ValueError("Numeric prerelease identifiers must not have leading zeroes.")
    return value


def identity(implementation, release, target):
    version(release)
    if implementation not in TARGETS or target not in TARGETS[implementation]:
        raise ValueError(f"Unsupported CLI target: {implementation}/{target}")
    return f"ges-{implementation}-{release}-{target}"


def archive_name(implementation, release, target):
    return identity(implementation, release, target) + (".zip" if target.startswith("win-") else ".tar.gz")


def sha256(path):
    digest = hashlib.sha256()
    with Path(path).open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def extract(archive, destination, expected_root):
    """Extract only ordinary files/directories beneath the expected archive root."""
    destination = Path(destination)
    if destination.exists():
        raise ValueError("Archive verification requires a fresh destination.")
    destination.mkdir(parents=True)

    def checked(name):
        path = PurePosixPath(name)
        if "\\" in name or ":" in name or path.is_absolute() or ".." in path.parts or not path.parts or path.parts[0] != expected_root:
            raise ValueError(f"Unexpected archive member: {name}")
        return destination.joinpath(*path.parts)

    if str(archive).endswith(".zip"):
        with zipfile.ZipFile(archive) as package:
            for member in package.infolist():
                if (member.external_attr >> 16) & 0o170000 == 0o120000:
                    raise ValueError("Archive links are not allowed.")
                path = checked(member.filename)
                if member.is_dir():
                    path.mkdir(parents=True, exist_ok=True)
                else:
                    path.parent.mkdir(parents=True, exist_ok=True)
                    with package.open(member) as source, path.open("xb") as output:
                        shutil.copyfileobj(source, output)
    else:
        with tarfile.open(archive, "r:gz") as package:
            for member in package:
                path = checked(member.name)
                if member.isdir():
                    path.mkdir(parents=True, exist_ok=True)
                elif member.isfile():
                    path.parent.mkdir(parents=True, exist_ok=True)
                    with package.extractfile(member) as source, path.open("xb") as output:
                        shutil.copyfileobj(source, output)
                    path.chmod(member.mode & 0o777)
                else:
                    raise ValueError("Archive links and special files are not allowed.")
    return destination / expected_root


def verified_set(directory, release, revision):
    """Require one successful native verification receipt per published target."""
    version(release)
    directory = Path(directory)
    expected = {archive_name(language, release, target): (language, target)
                for language, targets in TARGETS.items() for target in targets}
    actual = {p.name for p in directory.iterdir() if p.name.endswith((".zip", ".tar.gz"))}
    if actual != set(expected):
        raise ValueError(f"Incomplete or unexpected CLI archive set: {actual ^ set(expected)}")
    result = []
    for name, (language, target) in sorted(expected.items()):
        archive = directory / name
        receipt = json.loads((directory / (name + ".json")).read_text())
        if receipt != {"archive": name, "version": release, "revision": revision, "implementation": language,
                       "target": target, "sha256": sha256(archive), "verified": True, "sourceClean": True}:
            raise ValueError(f"Invalid verification receipt: {name}")
        result.append(archive)
    return result
