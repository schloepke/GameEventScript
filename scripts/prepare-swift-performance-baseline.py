#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Prepare reviewable Markdown baseline candidates from independent measured runs.

Never edits the corpus. All input runs must identify the exact current source
bytes, profile, machine and toolchain. Copy reviewed candidates into the corpus
explicitly; normal verification never updates references.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import re
import statistics

ROOT = Path(__file__).resolve().parent.parent


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("measurements", nargs="+", type=Path)
    parser.add_argument("--output", type=Path, default=ROOT / "artifacts/swift/performance-baseline")
    args = parser.parse_args()
    if len(args.measurements) < 3:
        parser.error("At least three independent process measurements are required")
    if not args.output.resolve().is_relative_to((ROOT / "artifacts").resolve()):
        parser.error("Generated candidates belong below artifacts")
    runs = [json.loads(path.read_text()) for path in args.measurements]
    provenance = runs[0]["provenance"]
    if len({run["provenance"]["measurementID"] for run in runs}) != len(runs) or len({run["provenance"]["processID"] for run in runs}) != len(runs):
        parser.error("Independent process measurements are required, not copies of the same run")
    for run in runs:
        for key in ["profile", "toolchain", "sdk", "cpu", "operatingSystem", "instrumentation", "sampleCount"]:
            if run["provenance"][key] != provenance[key]:
                parser.error("Measurement environments differ: " + key)
    by_run = [{case["id"]: case for case in run["cases"]} for run in runs]
    ids = set(by_run[0])
    if any(set(cases) != ids for cases in by_run):
        parser.error("Measurements cover different workloads")
    args.output.mkdir(parents=True, exist_ok=True)
    seen = set()
    profile = provenance["profile"]
    if not re.fullmatch(r"[a-z][a-z0-9.-]*", profile):
        parser.error("Invalid profile ID")
    for path in sorted((ROOT / "conformance/suites/performance").glob("*.md")):
        raw = path.read_bytes()
        text = raw.decode("utf-8")
        suite = re.search(r"(?m)^suiteId: (.+)$", text)[1].strip('"')
        for run in runs:
            expected = {item["suiteId"]: item["sha256"] for item in run["documents"]}
            if hashlib.sha256(raw).hexdigest().upper() != expected.get(suite):
                parser.error("Stale measurement: " + str(path))
        sections = re.split(r"(?m)(?=^## Test: )", text)
        for index, section in enumerate(sections):
            match = re.search(r"(?m)^id: (.+)$", section)
            if not match:
                continue
            identity = suite + "/" + match[1].strip('"')
            if identity not in ids:
                continue
            seen.add(identity)
            cases = [run[identity] for run in by_run]
            metric_ids = set(cases[0]["metrics"])
            if any(set(case["metrics"]) != metric_ids for case in cases):
                parser.error("Metric sets differ: " + identity)
            lines = [f"    {profile}:", "      metrics:"]
            for metric in sorted(metric_ids):
                values = [case["metrics"][metric]["measured"] for case in cases]
                unit = cases[0]["metrics"][metric]["unit"]
                if any(not math.isfinite(v) or v < 0 for v in values) or any(case["metrics"][metric]["unit"] != unit for case in cases):
                    parser.error("Invalid measurement: " + identity + "/" + metric)
                allocation = "allocated" in metric or metric.endswith(".allocations")
                reference = max(values) if allocation else statistics.median(values)
                lines += [f"        {metric}:", f"          reference: {reference:.12g}"]
                if allocation:
                    maximum = math.ceil(reference * 1.05) if reference else 0
                    if suite == "performance.low-allocation" and metric.startswith("run."):
                        if reference != 0:
                            parser.error("Cannot approve a nonzero dispatch allocation: " + identity)
                        maximum = 0
                    lines.append(f"          maximum: {maximum:.12g}")
                else:
                    floor = 0.05 / cases[0]["iterations"] if unit.endswith("/iteration") else 0.05
                    lines.append(f"          toleranceAbsolute: {max(reference * .25, floor):.12g}")
                lines.append(f"          unit: {unit}")
            block = "\n".join(lines) + "\n"
            # The final performance profile mapping is inside the expectation fence.
            start = section.index("performance:\n  profiles:")
            finish = section.index("```", start)
            existing = re.search(r"(?m)^    " + re.escape(profile) + r":\n.*?(?=^    [a-z]|\Z)", section[start:finish], re.S)
            if existing:
                lo, hi = start + existing.start(), start + existing.end()
                section = section[:lo] + block + section[hi:]
            else:
                section = section[:finish] + block + section[finish:]
            sections[index] = section
        candidate = args.output / (path.stem + ".received.md")
        candidate.write_text("".join(sections))
        print(candidate.relative_to(ROOT))
    if seen != ids:
        parser.error("Measurement cases do not match the corpus")
    print(f"Prepared {len(seen)} workload baselines for {profile}; source files were not modified.")


if __name__ == "__main__":
    main()
