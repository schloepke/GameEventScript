#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Validate the complete NuGet CLI set; publish only with explicit --publish."""
import argparse
import os
import subprocess
import sys

sys.dont_write_bytecode = True
from csharp_tool_packages import release_packages


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
            # No globs, no symbol auto-discovery, no skip-duplicate hiding mismatches.
            subprocess.run(["dotnet", "nuget", "push", str(path), "--no-symbols",
                            "--api-key", key, "--source", "https://api.nuget.org/v3/index.json"], check=True)
    print(f"{'Published' if args.publish else 'Validated'} {len(packages)} CLI packages.")


if __name__ == "__main__":
    main()
