#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Push one verified package to nuget.org so that a failed release can be run again.

A package version that nuget.org already holds is skipped only when its contents equal the verified local package,
apart from the repository signature that nuget.org adds; a differing package fails, so a duplicate never hides a
mismatch. GES_NUGET_FLAT_CONTAINER points the lookup at another feed for tests.
"""
import argparse
import hashlib
import io
import os
from pathlib import Path
import subprocess
import sys
import urllib.error
import urllib.request
import zipfile

FLAT_CONTAINER = os.environ.get("GES_NUGET_FLAT_CONTAINER", "https://api.nuget.org/v3-flatcontainer")
SOURCE = "https://api.nuget.org/v3/index.json"
# nuget.org signs every accepted package, which adds this entry to the archive.
REPOSITORY_SIGNATURE = ".signature.p7s"


def published(package_id, version):
    """The bytes of the package version on the feed, or None when the feed does not hold it."""
    name, number = package_id.lower(), version.lower()
    url = f"{FLAT_CONTAINER}/{name}/{number}/{name}.{number}.nupkg"
    try:
        with urllib.request.urlopen(url, timeout=60) as response:
            return response.read()
    except urllib.error.HTTPError as error:
        if error.code == 404:
            return None
        raise
    except (urllib.error.URLError, FileNotFoundError) as error:
        if isinstance(getattr(error, "reason", None), FileNotFoundError) or isinstance(error, FileNotFoundError):
            return None
        raise


def contents(data):
    """A digest per archive entry without the repository signature, or of the raw bytes for a non-archive."""
    try:
        with zipfile.ZipFile(io.BytesIO(data)) as archive:
            return {entry.filename: hashlib.sha256(archive.read(entry)).hexdigest()
                    for entry in archive.infolist() if entry.filename != REPOSITORY_SIGNATURE}
    except zipfile.BadZipFile:
        return hashlib.sha256(data).hexdigest()


def push(path, package_id, version, symbols):
    remote = published(package_id, version)
    if remote is not None:
        if contents(remote) != contents(Path(path).read_bytes()):
            raise SystemExit(f"nuget.org already holds {package_id} {version} with other contents; a version is never replaced.")
        print(f"Skipped {package_id} {version}: nuget.org already holds this verified package.")
        return
    key = os.environ.get("NUGET_API_KEY")
    if not key:
        raise SystemExit("A short-lived NUGET_API_KEY is required")
    command = ["dotnet", "nuget", "push", str(path)] + ([] if symbols else ["--no-symbols"]) + ["--api-key", key, "--source", SOURCE]
    subprocess.run(command, check=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("path")
    parser.add_argument("package_id")
    parser.add_argument("version")
    parser.add_argument("--no-symbols", action="store_true", help="Push the package without its symbol package")
    args = parser.parse_args()
    push(args.path, args.package_id, args.version, not args.no_symbols)


if __name__ == "__main__":
    main()
