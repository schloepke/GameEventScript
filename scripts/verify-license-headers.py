#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Require the LICENSING.md short header on the tracked file kinds that the C# license test does not cover.

The C# repository test checks C#, Markdown, project, workflow and editor files. This gate covers Swift (with SwiftPM
manifests), C and C headers, Python tooling, GES programs, documents and schemas, and authored web sources, so a file
of the Swift port or the tooling cannot ship without its header. The documented exclusions apply: checked-in generated
code under `Generated` directories and the Conformance inputs whose exact bytes are test data.
"""
import argparse
from pathlib import Path
import subprocess
import sys
import tempfile

ROOT = Path(__file__).resolve().parent.parent
COPYRIGHT = "Copyright 2026 Stephan Schlöpke"
SPDX = "SPDX-License-Identifier: Apache-2.0"
EXTENSIONS = {".swift", ".c", ".h", ".py", ".ges", ".gesd", ".gess", ".js", ".mjs", ".cjs", ".ts", ".css", ".astro", ".svg"}
# A directive, a shebang or an XML prolog may come first, so the header must start within these lines.
HEADER_LINES = 6


def excluded(path):
    parts = Path(path).parts
    return "Generated" in parts or parts[:1] == ("conformance",) or path.endswith(".g.cs")


def violations(root, paths):
    found = []
    for path in paths:
        if Path(path).suffix not in EXTENSIONS or excluded(path):
            continue
        try:
            head = (root / path).read_text(encoding="utf-8").splitlines()[:HEADER_LINES]
        except (OSError, UnicodeDecodeError):
            found.append(f"{path}: not readable as UTF-8 text")
            continue
        lines = [line for line in head if COPYRIGHT in line or SPDX in line]
        if len(lines) != 2 or COPYRIGHT not in lines[0] or SPDX not in lines[1]:
            found.append(f"{path}: missing the two-line license header near its start")
    return found


def tracked(root):
    output = subprocess.check_output(["git", "ls-files", "-z"], cwd=root)
    return [path.decode("utf-8", errors="surrogateescape") for path in output.split(b"\0") if path]


def self_test():
    with tempfile.TemporaryDirectory() as directory:
        root = Path(directory)
        files = {
            "good.swift": f"// {COPYRIGHT}\n// {SPDX}\n",
            "Package.swift": f"// swift-tools-version: 6.0\n// {COPYRIGHT}\n// {SPDX}\n",
            "tool.py": f"#!/usr/bin/env python3\n# {COPYRIGHT}\n# {SPDX}\n",
            "missing.swift": "import Foundation\n",
            "half.py": f"# {COPYRIGHT}\nprint(1)\n",
            "swapped.ges": f"// {SPDX}\n// {COPYRIGHT}\n",
            "Sources/Generated/Model.swift": "struct Model {}\n",
            "conformance/fixtures/case.gess": "schema :A as {}\n",
            "notes.txt": "no header needed\n",
        }
        for name, text in files.items():
            (root / name).parent.mkdir(parents=True, exist_ok=True)
            (root / name).write_text(text, encoding="utf-8")
        found = {line.split(":")[0] for line in violations(root, list(files))}
        expected = {"missing.swift", "half.py", "swapped.ges"}
        if found != expected:
            raise SystemExit(f"License header controls failed: found {sorted(found)}, expected {sorted(expected)}")
    print("License header controls passed.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--self-test", action="store_true", help="Qualify the check with negative controls")
    if parser.parse_args().self_test:
        return self_test()
    found = violations(ROOT, tracked(ROOT))
    if found:
        print("\n".join(found))
        raise SystemExit(f"{len(found)} files violate the license header policy in LICENSING.md.")
    print("License headers verified.")


if __name__ == "__main__":
    main()
