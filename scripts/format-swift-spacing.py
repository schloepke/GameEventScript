#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Require one blank line around Swift callable/type declarations, using SwiftSyntax."""

import argparse
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "scripts/SwiftDeclarationSpacing.swift"


def helper():
    info = subprocess.check_output(["swiftc", "-print-target-info"], text=True)
    host = Path(json.loads(info)["paths"]["runtimeResourcePath"]) / "host"
    identity = hashlib.sha256(SOURCE.read_bytes() + info.encode()).hexdigest()[:20]
    directory = ROOT / "artifacts/swift/formatting" / identity
    executable = directory / "declaration-spacing"
    if not executable.exists():
        directory.mkdir(parents=True, exist_ok=True)
        subprocess.run(["swiftc", "-I", str(host), "-L", str(host),
                        "-Xlinker", "-rpath", "-Xlinker", str(host),
                        str(SOURCE), "-o", str(executable)], check=True)
    return executable


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--fix", action="store_true")
    parser.add_argument("paths", nargs="+", type=Path)
    args = parser.parse_args()
    files = set()
    for path in args.paths:
        if path.is_dir():
            files.update(path.rglob("*.swift"))
        elif path.is_file():
            files.add(path)
        else:
            parser.error(f"Missing Swift source path: {path}")
    return subprocess.run([str(helper())] + (["--fix"] if args.fix else [])
                          + [str(path) for path in sorted(files)], check=False).returncode


if __name__ == "__main__":
    raise SystemExit(main())
