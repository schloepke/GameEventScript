#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Validate the complete NuGet CLI set; publish only with explicit --publish."""
import argparse
import os
import sys

sys.dont_write_bytecode = True
from csharp_tool_packages import release_packages
import nuget_publication


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory")
    parser.add_argument("version")
    parser.add_argument("--publish", action="store_true")
    args = parser.parse_args()
    packages = release_packages(args.directory, args.version)
    if args.publish:
        key = os.environ.get("NUGET_API_KEY")
        if not key:
            raise ValueError("A short-lived NUGET_API_KEY is required")
        for path in packages:
            # No globs and no symbol auto-discovery; a version already published with identical contents is skipped and
            # one with other contents fails, so a rerun resumes without hiding a mismatch.
            package_id, version = path.name.removesuffix(".nupkg").removesuffix("." + args.version), args.version
            nuget_publication.push(path, package_id, version, symbols=False)
    print(f"{'Published' if args.publish else 'Validated'} {len(packages)} CLI packages.")


if __name__ == "__main__":
    main()
