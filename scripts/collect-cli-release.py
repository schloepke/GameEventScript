#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Validate a complete CLI release and optionally attach it to an existing release."""

import argparse
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile

sys.dont_write_bytecode = True
from cli_distribution import sha256, verified_set, version


def download_asset(tag, repository, name, directory):
    """Downloads one published release asset, to compare it with the verified local file."""
    subprocess.run(["gh", "release", "download", tag, "--repo", repository, "--pattern", name, "--dir", str(directory)], check=True)
    return Path(directory) / name


def missing_assets(tag, repository, files, published):
    """The files still to upload. An asset that a failed earlier upload already attached is kept when its bytes equal the
    verified file, so the upload can be resumed; a differing asset is never overwritten."""
    missing = []
    with tempfile.TemporaryDirectory() as directory:
        for path in files:
            if path.name not in published:
                missing.append(path)
            elif sha256(download_asset(tag, repository, path.name, directory)) != sha256(path):
                raise ValueError(f"The published asset {path.name} differs from the verified file; published downloads are never overwritten.")
    return missing


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("version", type=version)
    parser.add_argument("--revision", required=True)
    parser.add_argument("--directory", type=Path, required=True)
    parser.add_argument("--publish", action="store_true")
    args = parser.parse_args()
    archives = verified_set(args.directory, args.version, args.revision)
    checksums = args.directory / f"ges-cli-{args.version}-SHA256SUMS.txt"
    checksums.write_text("".join(f"{sha256(path)}  {path.name}\n" for path in archives))
    if not args.publish:
        print(f"Verified all {len(archives)} CLI downloads. No publication performed. Checksums: {checksums}")
        return
    tag = os.environ.get("GITHUB_REF", "").removeprefix("refs/tags/")
    if os.environ.get("GITHUB_REF") not in (f"refs/tags/{args.version}", f"refs/tags/v{args.version}"):
        raise ValueError("Publication requires the matching version tag, not a branch.")
    revision = subprocess.check_output(["git", "rev-parse", "HEAD"], text=True).strip()
    tagged = subprocess.check_output(["git", "rev-parse", f"refs/tags/{tag}^{{commit}}"], text=True).strip()
    if revision != args.revision or tagged != revision:
        raise ValueError("Archive verification must belong to the checked-out release tag.")
    repository = os.environ["GITHUB_REPOSITORY"]
    release = json.loads(subprocess.check_output(["gh", "release", "view", tag, "--repo", repository,
                                                  "--json", "tagName,assets"], text=True))
    if release["tagName"] != tag:
        raise ValueError("The release belongs to another tag.")
    missing = missing_assets(tag, repository, [*archives, checksums], {asset["name"] for asset in release["assets"]})
    if not missing:
        print("Every CLI download is already attached with identical bytes; nothing to upload.")
        return
    subprocess.run(["gh", "release", "upload", tag, "--repo", repository, *map(str, missing)], check=True)


if __name__ == "__main__":
    main()
