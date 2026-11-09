#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Require identical schema generation layouts and code from the C# and Swift CLIs.

Every distinct combination of sources, root and parameters in the schemaApi cases of the corpus runs `schema layout` in
both CLIs, once with its root and once with --all-roots, and `schema generate` for C# and for Swift with its root. Both
must print the same text, or both must fail with the same error code. --self-test qualifies the comparison with negative
controls.
"""
import argparse
from concurrent.futures import ThreadPoolExecutor
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "conformance/runner"))
from core.corpus import read_corpus  # noqa: E402

SWIFT_CLI = ROOT / "artifacts/swift/tool/release/ges"
CSHARP_CLI = ROOT / "artifacts/csharp/tool/bin/Release/net8.0/GameEventScript.Tool.dll"
TIMEOUT_SECONDS = 60
CODE = re.compile(r"error ([a-z]+\.[A-Za-z]+)")


def inputs(paths):
    """The distinct (sources, root, parameters) of the schemaApi cases with literal parameters, in corpus order."""
    seen, result = set(), []
    for case in read_corpus(paths):
        api = case.metadata.get("schemaApi") if case.kind == "schemaApi" else None
        if not isinstance(api, dict) or not api.get("sources") or not api.get("root"):
            continue
        # Typed API values have no literal that the CLI could pass on.
        if not all(isinstance(value, str) for value in (api.get("parameters") or {}).values()):
            continue
        parameters = tuple(sorted((api.get("parameters") or {}).items()))
        key = (tuple(api["sources"]), api["root"], parameters)
        if key not in seen:
            seen.add(key)
            result.append(key)
    return result


def outcome(result):
    """What a run is compared by: the output of a success, or the error code of a failure."""
    if result.returncode == 0:
        return ("ok", result.stdout.decode("utf-8"))
    match = CODE.search(result.stderr.decode("utf-8", "replace"))
    return ("error", result.returncode, match.group(1) if match else "")


def difference(csharp, swift):
    """None when both ports agree, otherwise a short description."""
    if csharp == swift:
        return None
    if csharp[0] == "ok" and swift[0] == "ok":
        left, right = csharp[1].splitlines(), swift[1].splitlines()
        for line, (a, b) in enumerate(zip(left, right), 1):
            if a != b:
                return f"line {line}: C# {a.strip()!r}, Swift {b.strip()!r}"
        return f"C# has {len(left)} lines, Swift {len(right)}"
    return f"C# {csharp[:3]!r}, Swift {swift[:3]!r}"


def compare(index, item, csharp, swift, work):
    sources, root, parameters = item
    directory = work / str(index)
    directory.mkdir()
    files = []
    for number, text in enumerate(sources):
        path = directory / f"s{number}.gess"
        path.write_text(text, encoding="utf-8")
        files.append(path.name)
    options = [argument for name, value in parameters for argument in ("--param", f"{name}={value}")]
    differences = []
    for selection in (["layout", "--root", root], ["layout", "--all-roots"], ["generate", "--root", root, "--language", "csharp", "--namespace", "Game.Wire"],
                      ["generate", "--root", root, "--language", "swift", "--namespace", "Wire", "--access", "internal"]):
        arguments = ["schema", selection[0], *files, *selection[1:], *options]
        runs = [subprocess.run([*command, *arguments], cwd=directory, capture_output=True, check=False, timeout=TIMEOUT_SECONDS) for command in (csharp, swift)]
        found = difference(outcome(runs[0]), outcome(runs[1]))
        if found:
            differences.append(f"{' '.join(selection)}: {found}")
    return differences


def self_test():
    ok = subprocess.CompletedProcess([], 0, b'{\n  "a": 1\n}\n', b"")
    changed = subprocess.CompletedProcess([], 0, b'{\n  "a": 2\n}\n', b"")
    failed = subprocess.CompletedProcess([], 1, b"", b"schema: error schema.unknownName: x\n")
    other = subprocess.CompletedProcess([], 1, b"", b"schema: error schema.syntax: x\n")
    assert difference(outcome(ok), outcome(ok)) is None
    assert difference(outcome(failed), outcome(failed)) is None
    assert "line 2" in difference(outcome(ok), outcome(changed))
    assert difference(outcome(ok), outcome(failed))
    assert difference(outcome(failed), outcome(other))
    longer = subprocess.CompletedProcess([], 0, b'{\n  "a": 1\n}\n\n', b"")
    assert difference(outcome(ok), outcome(longer))
    sample = inputs([ROOT / "conformance/suites/api/schemas.md"])
    assert sample and all(len(item) == 3 for item in sample)
    print("Schema generation comparison controls passed.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--csharp", type=Path, default=CSHARP_CLI, help="C# GameEventScript.Tool.dll")
    parser.add_argument("--swift", type=Path, default=SWIFT_CLI, help="Swift ges executable")
    parser.add_argument("--self-test", action="store_true", help="only check the comparison with negative controls")
    args = parser.parse_args()
    if args.self_test:
        self_test()
        return 0
    paths = sorted((ROOT / "conformance/suites").rglob("*.md"))
    items = inputs(paths)
    with tempfile.TemporaryDirectory() as temporary, ThreadPoolExecutor(max_workers=os.cpu_count() or 4) as pool:
        work = Path(temporary)
        results = list(pool.map(lambda pair: compare(pair[0], pair[1], ["dotnet", str(args.csharp)], [str(args.swift)], work), enumerate(items)))
    differences = [line for result in results for line in result]
    for line in differences:
        print(line)
    print(f"{len(items)} schema inputs, {2 * len(items)} layouts and {2 * len(items)} generated sources: {len(differences)} port differences")
    return 1 if differences else 0


if __name__ == "__main__":
    sys.exit(main())
