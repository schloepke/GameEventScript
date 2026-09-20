#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Verify declared Swift public API against explicitly approved symbol snapshots.

SwiftPM builds and symbol graphs stay below artifacts/swift-api. Snapshots retain
declarations, generic requirements, availability, and public type relationships;
source locations, documentation, compiler metadata, and synthesized members are
excluded. This script needs only Python's standard library and the Swift toolchain.
"""

import argparse
import difflib
import json
from pathlib import Path
import shutil
import subprocess
import sys


ROOT = Path(__file__).resolve().parent.parent
PACKAGES = ("GameEventScriptRuntime", "GameEventScriptCompiler", "GameEventScriptConformance", "GameEventScriptSwiftBridge")
SNAPSHOTS = ROOT / "implementation" / "swift" / "api"
ARTIFACTS = ROOT / "artifacts" / "swift-api"
RELATIONSHIPS = {
    "conformsTo", "inheritsFrom", "overrides", "requirementOf",
    "optionalRequirementOf", "defaultImplementationOf", "extensionTo",
}


def canonical(value):
    """A deterministic, reviewable single-line representation."""
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":"))


def ordered_metadata(value):
    """Metadata list order carries no public semantic meaning."""
    if isinstance(value, list):
        return sorted((ordered_metadata(item) for item in value), key=canonical)
    if isinstance(value, dict):
        return {key: ordered_metadata(item) for key, item in value.items()}
    return value


def dump_graphs(swift, package):
    scratch = ARTIFACTS / package
    scratch.mkdir(parents=True, exist_ok=True)
    # Clear this script's previous graph output so removed extension graphs cannot
    # survive a rebuild and accidentally conceal an API removal.
    for graph in scratch.rglob("*.symbols.json"):
        graph.unlink()
    options = [
        "--package-path", str(ROOT / "implementation" / "swift" / package),
        "--scratch-path", str(scratch), "--build-system", "native",
    ]
    # SwiftPM's graph dumper also visits synthesized test-runner modules. Build
    # them first so a fresh scratch directory works; none are included below.
    commands = [
        [swift, "build", *options, "--build-tests"],
        [swift, "package", *options, "dump-symbol-graph", "--skip-synthesized-members", "--minimum-access-level", "public"],
    ]
    log = ARTIFACTS / (package + ".log")
    print(f"Extracting {package} public symbols…", flush=True)
    with log.open("w", encoding="utf-8") as output:
        for command in commands:
            completed = subprocess.run(command, cwd=ROOT, stdout=output, stderr=subprocess.STDOUT, check=False)
            if completed.returncode:
                raise RuntimeError(f"Swift symbol extraction failed for {package}; see {log}")
    paths = sorted(
        path for path in scratch.rglob("*.symbols.json")
        if path.name == package + ".symbols.json" or path.name.startswith(package + "@")
    )
    if not paths:
        raise RuntimeError(f"Swift produced no symbol graph for {package}; see {log}")
    return [json.loads(path.read_text(encoding="utf-8")) for path in paths]


def snapshot(package, graphs):
    symbols = {}
    for graph in graphs:
        if graph.get("module", {}).get("name") != package:
            raise RuntimeError(f"Unexpected module identity in {package}'s graph")
        for symbol in graph.get("symbols", []):
            if symbol.get("accessLevel") not in ("public", "open"):
                continue
            precise = symbol["identifier"]["precise"]
            record = {
                "access": symbol["accessLevel"],
                "declaration": "".join(item["spelling"] for item in symbol.get("declarationFragments", [])),
                "kind": symbol["kind"]["identifier"],
                "path": package + "." + ".".join(symbol["pathComponents"]),
            }
            for field in ("swiftGenerics", "swiftExtension", "availability"):
                if symbol.get(field):
                    record[field] = ordered_metadata(symbol[field])
            # Precise identifiers are used only to resolve graph edges. Mangled
            # symbols are deliberately absent from the approval artifact.
            if precise in symbols and symbols[precise] != record:
                raise RuntimeError(f"Conflicting symbol graph entries for {record['path']}")
            symbols[precise] = record

    if not symbols:
        raise RuntimeError(f"Refusing to approve an empty public API for {package}")

    records = {"symbol " + canonical(record) for record in symbols.values()}
    sendable_sources = {
        relation["source"] for graph in graphs for relation in graph.get("relationships", [])
        if relation["kind"] == "conformsTo" and relation.get("targetFallback") == "Swift.Sendable"
    }
    for graph in graphs:
        for relation in graph.get("relationships", []):
            kind = relation["kind"]
            source = symbols.get(relation["source"])
            if kind not in RELATIONSHIPS or source is None:
                continue
            # Swift 6.2+ adds this transitive conformance to every Sendable type.
            # Sendable already captures the declared contract; the extra edge
            # would otherwise create toolchain-only drift against Swift 6.0.
            if (kind == "conformsTo" and relation.get("targetFallback") == "Swift.SendableMetatype"
                    and relation["source"] in sendable_sources):
                continue
            target_symbol = symbols.get(relation["target"])
            if target_symbol is not None:
                target = target_symbol["path"] + " :: " + target_symbol["declaration"]
            else:
                target = relation.get("targetFallback")
                if not target:
                    raise RuntimeError(f"Unresolved public {kind} target for {source['path']}")
            record = {
                "kind": kind,
                "source": source["path"] + " :: " + source["declaration"],
                "target": target,
            }
            if relation.get("swiftConstraints"):
                record["swiftConstraints"] = ordered_metadata(relation["swiftConstraints"])
            records.add("relationship " + canonical(record))

    header = [
        "# Copyright 2026 Stephan Schlöpke",
        "# SPDX-License-Identifier: Apache-2.0",
        "# Swift declared public API snapshot v1",
        f"# Module: {package}",
        "# Update explicitly with: python3 scripts/verify-swift-api.py --update",
        "",
    ]
    return "\n".join(header + sorted(records)) + "\n"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--update", action="store_true", help="approve the current declarations after review")
    arguments = parser.parse_args()
    swift = shutil.which("swift")
    if not swift:
        parser.error("swift must be available on PATH")

    generated = []
    try:
        for package in PACKAGES:
            generated.append((package, snapshot(package, dump_graphs(swift, package))))
    except (OSError, ValueError, KeyError, RuntimeError) as error:
        print(str(error), file=sys.stderr)
        return 2

    # Extract all packages successfully before changing any approved snapshot.
    if arguments.update:
        SNAPSHOTS.mkdir(parents=True, exist_ok=True)
        for package, content in generated:
            path = SNAPSHOTS / (package + ".approved.txt")
            with path.open("w", encoding="utf-8", newline="\n") as output:
                output.write(content)
            print(f"Approved {path.relative_to(ROOT)}")
        return 0

    changed = False
    for package, content in generated:
        path = SNAPSHOTS / (package + ".approved.txt")
        actual = ARTIFACTS / (package + ".received.txt")
        with actual.open("w", encoding="utf-8", newline="\n") as output:
            output.write(content)
        previous = path.read_text(encoding="utf-8") if path.exists() else ""
        if previous == content:
            print(f"Verified {package} public API")
            continue
        changed = True
        sys.stdout.writelines(difflib.unified_diff(
            previous.splitlines(keepends=True), content.splitlines(keepends=True),
            fromfile=str(path.relative_to(ROOT)), tofile=str(actual.relative_to(ROOT)),
        ))
    if changed:
        print("Swift public API changed. Review the diff, then explicitly approve with --update.", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
