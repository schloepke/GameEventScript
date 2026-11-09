#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Keep the portable Swift libraries free of Foundation and other platform frameworks.

Syntax, Runtime and Compiler may import only each other and the C library of
their platform, so that WASM and embedded consumers do not ship Foundation.
Their local manifests may depend only on these local packages.
"""

import argparse
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parent.parent
PACKAGES = ("GameEventScriptSyntax", "GameEventScriptRuntime", "GameEventScriptCompiler", "GameEventScriptGenerator")
LIBC = {"Darwin", "Glibc", "Musl", "WASILibc", "ucrt", "Android", "Bionic"}
IMPORT = re.compile(
    r"^[ \t]*(?:@\w+(?:\([^\n)]*\))?[ \t]+)*"
    r"(?:(?:public|internal|package|private|fileprivate)[ \t]+)?import[ \t]+"
    r"(?:(?:typealias|struct|class|enum|protocol|let|var|func)[ \t]+)?([A-Za-z_]\w*)",
    re.MULTILINE,
)
PACKAGE_DEPENDENCY = re.compile(r"\.package\(\s*(\w+)\s*:\s*\"([^\"]*)\"")


def check(root):
    errors = []
    sources = 0
    allowed = set(PACKAGES) | LIBC
    for package in PACKAGES:
        directory = root / "implementation/swift" / package
        manifest = directory / "Package.swift"
        if not manifest.is_file():
            errors.append(f"{manifest.relative_to(root)}: manifest missing")
            continue
        for kind, location in PACKAGE_DEPENDENCY.findall(manifest.read_text(encoding="utf-8")):
            if kind != "path" or Path(location).name not in PACKAGES:
                errors.append(f"{manifest.relative_to(root)}: package dependency {location!r} is not a portable GES package")
        for source in sorted((directory / "Sources").rglob("*.swift")):
            sources += 1
            text = source.read_text(encoding="utf-8")
            for match in IMPORT.finditer(text):
                if match.group(1) not in allowed:
                    line = text.count("\n", 0, match.start()) + 1
                    errors.append(f"{source.relative_to(root)}:{line}: imports {match.group(1)}")
    if sources == 0:
        errors.append("no portable Swift sources found")
    return errors, sources


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--root", type=Path, default=ROOT, help="repository root to check")
    arguments = parser.parse_args()
    errors, sources = check(arguments.root.resolve())
    for error in errors:
        print(error, file=sys.stderr)
    if errors:
        return 1
    print(f"Portable Swift imports verified: {sources} sources in {', '.join(PACKAGES)}.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
