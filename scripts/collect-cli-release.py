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

sys.dont_write_bytecode = True
from cli_distribution import sha256, verified_set, version


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
    names = {p.name for p in [*archives, checksums]}
    if release["tagName"] != tag or names & {asset["name"] for asset in release["assets"]}:
        raise ValueError("Release tag differs or CLI assets already exist; published downloads are never overwritten.")
    subprocess.run(["gh", "release", "upload", tag, "--repo", repository, *map(str, archives), str(checksums)], check=True)


if __name__ == "__main__":
    main()
