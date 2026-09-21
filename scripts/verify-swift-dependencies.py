#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Require direct SwiftPM target dependencies for imports of repository modules.

Transitive availability can make a clean build pass without adding the imported
module to the native incremental build command's declared inputs. Read SwiftPM's
resolved target/source descriptions instead of parsing Package.swift ourselves.
"""

import json
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parent.parent
IMPORT = re.compile(
    r"^[ \t]*(?:@\w+(?:\([^\n)]*\))?[ \t]+)*"
    r"(?:(?:public|internal|package|private|fileprivate)[ \t]+)?import[ \t]+"
    r"(?:(?:typealias|struct|class|enum|protocol|let|var|func)[ \t]+)?([A-Za-z_]\w*)",
    re.MULTILINE,
)


def main():
    descriptions = []
    for manifest in [ROOT / "Package.swift", *sorted((ROOT / "implementation/swift").glob("*/Package.swift"))]:
        package = manifest.parent
        result = subprocess.run(
            ["swift", "package", "--package-path", str(package),
             "--scratch-path", str(ROOT / "artifacts/swift-dependencies" / package.name),
             "describe", "--type", "json"],
            capture_output=True, text=True,
        )
        if result.returncode:
            print(result.stderr, file=sys.stderr, end="")
            return result.returncode
        descriptions.append((package, json.loads(result.stdout)))
    if not descriptions:
        raise RuntimeError("No SwiftPM packages found.")
    modules = {
        target.get("c99name", target["name"])
        for _, description in descriptions for target in description["targets"]
    }
    errors = []
    checked = 0
    for package, description in descriptions:
        for target in description["targets"]:
            if target["module_type"] != "SwiftTarget":
                continue
            checked += 1
            dependencies = set(target.get("target_dependencies", [])) | set(target.get("product_dependencies", []))
            for name in target["sources"]:
                source = package / target["path"] / name
                text = source.read_text(encoding="utf-8")
                for match in IMPORT.finditer(text):
                    module = match.group(1)
                    if module in modules and module not in dependencies:
                        line = text[:match.start()].count("\n") + 1
                        errors.append(
                            f"{source.relative_to(ROOT)}:{line}: {target['name']} imports {module} "
                            "without a direct target/product dependency in Package.swift."
                        )
    if errors:
        print("\n".join(errors), file=sys.stderr)
        return 1
    print(f"Swift dependencies verified: {checked} Swift targets in {len(descriptions)} packages.")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (OSError, ValueError, KeyError, RuntimeError) as error:
        print(f"Swift dependency verification failed: {error}", file=sys.stderr)
        sys.exit(1)
