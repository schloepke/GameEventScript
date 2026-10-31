#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Measure how long the C# and Swift compilers need for every source group of the shared corpus.

Each adapter prepares its inputs once and then compiles all groups per round, including groups that are expected to
fail. The report gives the median round time per port and, with --baseline, the change against an earlier report.
This is a development measurement for compiler changes, not a calibrated performance gate.
"""

import argparse
import json
from pathlib import Path
import statistics
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "conformance/runner"))
from core.corpus import read_corpus  # noqa: E402
from core.runner import requests, supported  # noqa: E402

SWIFT_SCRATCH = ROOT / "artifacts/swift/conformance"
ADAPTERS = {
    "csharp": ["dotnet", str(ROOT / "artifacts/csharp/conformance-adapter/bin/Release/net10.0/GameEventScript.ConformanceAdapter.dll")],
    "swift": [str(SWIFT_SCRATCH / "release/ges-conformance-adapter")],
}
BUILDS = {
    "csharp": ["dotnet", "build", str(ROOT / "implementation/csharp/GameEventScript.Conformance/adapter/GameEventScript.ConformanceAdapter.csproj"),
               "--configuration", "Release", "--nologo", "--verbosity", "quiet"],
    "swift": ["swift", "build", "--package-path", str(ROOT / "implementation/swift/GameEventScriptConformance"), "--scratch-path", str(SWIFT_SCRATCH),
              "--disable-build-manifest-caching", "--configuration", "release", "--product", "ges-conformance-adapter"],
}
COMPILED_KINDS = {"bytecodeSnapshot", "compileError", "bytecode", "compileMetadata", "programBinary", "scriptApi", "loadError"}


def measure(port, request, rounds):
    completed = subprocess.run([*ADAPTERS[port], "--benchmark-compile", str(rounds)], input=json.dumps(request), capture_output=True, text=True, check=False)
    if completed.returncode != 0:
        raise SystemExit(f"{port} adapter failed: {completed.stderr.strip()}")
    benchmark = json.loads(completed.stdout)["benchmark"]
    allocated = benchmark["allocatedBytes"]
    return {
        "units": benchmark["units"],
        "failures": benchmark["failures"],
        "medianMilliseconds": statistics.median(benchmark["nanoseconds"]) / 1e6,
        "medianAllocatedBytes": statistics.median(allocated) if allocated else None,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--port", choices=sorted(ADAPTERS), action="append", help="Port to measure; repeat for several (default: both)")
    parser.add_argument("--rounds", type=int, default=15, help="Timed rounds after one warm-up round (default: 15)")
    parser.add_argument("--skip-build", action="store_true", help="Use the existing Release adapters")
    parser.add_argument("--output", type=Path, help="Write the report as JSON")
    parser.add_argument("--baseline", type=Path, help="Earlier report to compare against")
    args = parser.parse_args()
    ports = args.port or sorted(ADAPTERS)

    cases = [case for case in read_corpus(sorted((ROOT / "conformance/suites").rglob("*.md"))) if supported(case) and case.kind in COMPILED_KINDS]
    request = requests(cases)
    baseline = json.loads(args.baseline.read_text(encoding="utf-8")) if args.baseline else {}
    report = {}
    for port in ports:
        if not args.skip_build:
            subprocess.run(BUILDS[port], check=True, stdout=subprocess.DEVNULL)
        report[port] = measure(port, request, args.rounds)
        result = report[port]
        line = f"{port}: {result['units']} groups ({result['failures']} rejected), median {result['medianMilliseconds']:.1f} ms"
        if result["medianAllocatedBytes"] is not None:
            line += f", {result['medianAllocatedBytes'] / 1e6:.1f} MB allocated"
        if port in baseline:
            change = result["medianMilliseconds"] / baseline[port]["medianMilliseconds"] - 1
            line += f" ({change:+.1%} against baseline)"
        print(line)
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
