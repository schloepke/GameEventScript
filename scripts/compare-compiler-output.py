#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Compile every source program of the shared corpus with the C# and Swift CLIs and compare the .gesb bytes.

Both compilers must produce byte-identical Programs for the same sources and debug options. Only the
BuildMetadata section differs by design, because it names the producing implementation; every other section
is compared byte for byte. Cases that both compilers reject are counted separately; a program that only one
compiler accepts, or that compiles to different bytes, is a difference. Reports and workspaces stay below
artifacts/compiler-comparison.
"""

import argparse
from concurrent.futures import ThreadPoolExecutor
import hashlib
import json
import os
from pathlib import Path
import shutil
import struct
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
WORK = ROOT / "artifacts" / "compiler-comparison"
SWIFT_CLI = ROOT / "artifacts/swift/tool/out/Products/Release/ges"
CSHARP_CLI = ROOT / "artifacts/csharp/tool/bin/Release/net8.0/GameEventScript.Tool.dll"


BUILD_METADATA = 0x0030


def sections(data):
    """Section type and raw bytes in file order; the header's FileSize is excluded, as it covers BuildMetadata."""
    if data[:4] != b"GESB" or len(data) < 16:
        raise ValueError("not a .gesb file")
    result = [(0, data[:12])]
    offset = struct.unpack_from("<I", data, 8)[0]
    while offset < len(data):
        kind, _, _, _, length = struct.unpack_from("<HHHHI", data, offset)
        result.append((kind, data[offset:offset + 12 + length]))
        offset += 12 + length
    return result


def comparable(data):
    """Every section except the implementation-specific BuildMetadata."""
    return [section for section in sections(data) if section[0] != BUILD_METADATA]


def programs(requests):
    """Unique (sources, debug) programs of the corpus, each with the first case ID that uses it."""
    unique = {}
    for case in requests["cases"]:
        groups = {}
        for source in case.get("sources", []):
            groups.setdefault(source.get("program", "main"), []).append((source["name"], source["text"]))
        debug = bool((case.get("compile") or {}).get("debugInfo", ["debugSymbols"]))
        for name, sources in groups.items():
            key = hashlib.sha256(json.dumps([sources, debug]).encode("utf-8")).hexdigest()
            unique.setdefault(key, {"case": case["id"], "program": name, "sources": sources, "debug": debug})
    return unique


def compile_with(command, directory, program, label):
    """Compile one program; returns the .gesb bytes or None when the compiler rejects it."""
    output = directory / f"{label}.gesb"
    arguments = [*command, "compile", *[source for source, _ in program["sources"]], "-o", str(output), "-q"]
    if not program["debug"]:
        arguments.append("--no-debug")
    result = subprocess.run(arguments, cwd=directory, capture_output=True, check=False)
    return output.read_bytes() if result.returncode == 0 and output.exists() else None


def compare(key, program, csharp, swift):
    directory = WORK / "programs" / key[:16]
    if directory.exists():
        shutil.rmtree(directory)
    directory.mkdir(parents=True)
    for name, text in program["sources"]:
        (directory / name).write_text(text, encoding="utf-8")
    left = compile_with(csharp, directory, program, "csharp")
    right = compile_with(swift, directory, program, "swift")
    if left is None and right is None:
        verdict = "both-rejected"
    elif left is None or right is None:
        verdict = "csharp-only" if right is None else "swift-only"
    else:
        verdict = "identical" if comparable(left) == comparable(right) else "different"
    if verdict in ("identical", "both-rejected"):
        shutil.rmtree(directory)
    return {"case": program["case"], "program": program["program"], "debug": program["debug"], "verdict": verdict, "workspace": str(directory)}


def summarize(results):
    counts = {}
    for result in results:
        counts[result["verdict"]] = counts.get(result["verdict"], 0) + 1
    return counts


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--swift", type=Path, default=SWIFT_CLI, help="Swift ges executable")
    parser.add_argument("--csharp", type=Path, default=CSHARP_CLI, help="C# GameEventScript.Tool.dll")
    parser.add_argument("--kind", default="supported", help="Corpus kind filter passed to the runner")
    args = parser.parse_args(argv)
    for path in (args.swift, args.csharp):
        if not path.exists():
            parser.error(f"missing compiler CLI {path}; build both tools first")
    WORK.mkdir(parents=True, exist_ok=True)
    requests_path = WORK / "requests.json"
    subprocess.run([sys.executable, "conformance/runner/main.py", "requests", "--kind", args.kind, "--output", str(requests_path)],
                   cwd=ROOT, check=True, stdout=subprocess.DEVNULL)
    unique = programs(json.loads(requests_path.read_text(encoding="utf-8")))
    with ThreadPoolExecutor(max_workers=os.cpu_count() or 4) as pool:
        results = list(pool.map(lambda item: compare(item[0], item[1], ["dotnet", str(args.csharp)], [str(args.swift)]), unique.items()))
    counts = summarize(results)
    (WORK / "report.json").write_text(json.dumps({"counts": counts, "results": results}, indent=1) + "\n", encoding="utf-8")
    print(f"{len(results)} programs: " + ", ".join(f"{verdict} {count}" for verdict, count in sorted(counts.items())))
    differences = [result for result in results if result["verdict"] not in ("identical", "both-rejected")]
    for result in differences:
        print(f"  {result['verdict']}: {result['case']} [{result['program']}] -> {result['workspace']}")
    return 1 if differences else 0


if __name__ == "__main__":
    sys.exit(main())
