#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Capture and compare the frozen performance reference of the port convergence.

implementation/PortMapping.md allows size-driven changes a cumulative slowdown of
at most 3% against a reference frozen at the start of the convergence, on the
same machine and toolchain. Single elapsed-time metrics vary by far more than 3%
between runs, so this tool repeats the complete performance corpus, keeps the
median of every metric, and judges elapsed time by the geometric mean of all
reference/candidate ratios. Allocation metrics are deterministic and must not
grow; a zero reference must stay zero.

This is a local measurement aid for the temporary convergence plan, not a CI
gate. Remove it together with implementation/PortMapping.md.

  capture --port csharp --runs 5 --output reference/csharp.json
  compare --port csharp --runs 5 --reference reference/csharp.json
"""

import argparse
import json
import math
import statistics
import subprocess
import sys
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
ELAPSED_BUDGET = 1.03

PORTS = {
    "csharp": {
        "build": ["dotnet", "build", "implementation/csharp/GameEventScript.Conformance/adapter/GameEventScript.ConformanceAdapter.csproj", "--configuration", "Release"],
        "profiles": ["csharp-dotnet-release-managed", "csharp-dotnet-release-macos-arm64"],
        "adapter": ["dotnet", "artifacts/csharp/conformance-adapter/bin/Release/net10.0/GameEventScript.ConformanceAdapter.dll"],
    },
    "swift": {
        "build": ["swift", "build", "--package-path", "implementation/swift/GameEventScriptConformance", "--scratch-path", "artifacts/swift/conformance",
                  "--disable-build-manifest-caching", "--configuration", "release"],
        "profiles": ["swift-6.4-release-macos26-arm64-m3max"],
        "adapter": ["artifacts/swift/conformance/release/ges-conformance-adapter"],
    },
}


def metrics(result):
    """Map (case, metric) to (measured value, unit) for one runner result document."""
    values = {}
    for case in result.get("cases", []):
        for name, metric in ((case.get("performance") or {}).get("metrics") or {}).items():
            values[f"{case['id']}|{name}"] = (float(metric["measured"]), metric["unit"])
    return values


def summarize(results):
    """Median every metric over repeated runs; record the run-to-run spread."""
    runs = [metrics(result) for result in results]
    keys = sorted(set().union(*runs)) if runs else []
    summary = {}
    for key in keys:
        samples = [run[key][0] for run in runs if key in run]
        unit = next(run[key][1] for run in runs if key in run)
        summary[key] = {"median": statistics.median(samples), "min": min(samples), "max": max(samples), "unit": unit, "runs": len(samples)}
    return summary


def is_elapsed(unit):
    return unit.startswith("ms")


def compare(reference, candidate):
    """Return (elapsed geometric-mean ratio, allocation regressions, missing metrics)."""
    ratios = []
    allocation_regressions = []
    missing = sorted(set(reference) - set(candidate))
    for key, expected in reference.items():
        if key not in candidate:
            continue
        actual = candidate[key]["median"]
        if is_elapsed(expected["unit"]):
            if expected["median"] > 0 and actual > 0:
                ratios.append(actual / expected["median"])
        elif actual > expected["median"]:
            allocation_regressions.append((key, expected["median"], actual, expected["unit"]))
    geometric_mean = math.exp(sum(math.log(r) for r in ratios) / len(ratios)) if ratios else 1.0
    return geometric_mean, allocation_regressions, missing


def run_corpus(port, runs):
    configuration = PORTS[port]
    subprocess.run(configuration["build"], cwd=ROOT, check=True, stdout=subprocess.DEVNULL)
    results = []
    with tempfile.TemporaryDirectory() as directory:
        for index in range(runs):
            output = Path(directory) / f"run-{index}.json"
            command = ["python3", "conformance/runner/main.py", "run", "--corpus", "conformance/suites/performance", "--kind", "performance"]
            for profile in configuration["profiles"]:
                command += ["--performance-profile", profile]
            command += ["--output", str(output), "--adapter", *configuration["adapter"]]
            # Budget failures against the corpus references do not invalidate a measurement.
            subprocess.run(command, cwd=ROOT, check=False, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            if not output.exists():
                raise RuntimeError(f"{port} run {index + 1} produced no result document")
            results.append(json.loads(output.read_text(encoding="utf-8")))
            print(f"{port}: run {index + 1}/{runs} complete", file=sys.stderr)
    return results


def environment():
    def command(*arguments):
        try:
            return subprocess.run(arguments, capture_output=True, text=True, check=False).stdout.strip().splitlines()[0]
        except (OSError, IndexError):
            return "unavailable"
    return {
        "commit": command("git", "-C", str(ROOT), "rev-parse", "HEAD"),
        "dotnet": command("dotnet", "--version"),
        "swift": command("swift", "--version"),
        "cpu": command("sysctl", "-n", "machdep.cpu.brand_string"),
        "os": command("sw_vers", "-productVersion"),
    }


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("action", choices=("capture", "compare"))
    parser.add_argument("--port", choices=sorted(PORTS), required=True)
    parser.add_argument("--runs", type=int, default=5)
    parser.add_argument("--output", type=Path, help="capture: reference file to write")
    parser.add_argument("--reference", type=Path, help="compare: reference file to read")
    args = parser.parse_args(argv)

    summary = summarize(run_corpus(args.port, args.runs))
    if args.action == "capture":
        if not args.output:
            parser.error("capture requires --output")
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps({"port": args.port, "environment": environment(), "metrics": summary}, indent=1, sort_keys=True) + "\n", encoding="utf-8")
        print(f"Captured {len(summary)} {args.port} metrics from {args.runs} runs in {args.output}")
        return 0

    if not args.reference:
        parser.error("compare requires --reference")
    reference = json.loads(args.reference.read_text(encoding="utf-8"))
    geometric_mean, regressions, missing = compare(reference["metrics"], summary)
    print(f"{args.port}: elapsed geometric mean {geometric_mean:.4f} (budget {ELAPSED_BUDGET:.2f})")
    for key, expected, actual, unit in regressions:
        print(f"  allocation increase {key}: {expected:g} -> {actual:g} {unit}")
    for key in missing:
        print(f"  missing metric {key}")
    return 0 if geometric_mean <= ELAPSED_BUDGET and not regressions and not missing else 1


if __name__ == "__main__":
    sys.exit(main())
