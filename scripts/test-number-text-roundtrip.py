#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Verify actual C# ↔ Swift numeric text exchange, independently of formatter spelling."""

import argparse
import json
import math
from pathlib import Path
import struct
import subprocess

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "artifacts/swift/number-text-roundtrip"
EXPORT = ROOT / "artifacts/swift/csharp-exporter"
PACKAGE = ROOT / "implementation/swift/GameEventScriptConformance"
SCRATCH = ROOT / "artifacts/swift/conformance"
PROJECT = ROOT / "implementation/csharp/GameEventScript.Conformance/fixture-exporter"
MASK = (1 << 64) - 1
SEED = 0x4745534E554D4245
RANDOM_COUNT = 4096
UNITS = ("none", "m", "s", "degree")


def bits(value):
    return struct.unpack(">Q", struct.pack(">d", value))[0]


def double(payload):
    return struct.unpack(">d", struct.pack(">Q", int(payload, 16)))[0]


def samples():
    # Fixed SplitMix64 stream, independent of the product RNG and Python's random module.
    state = SEED
    for _ in range(RANDOM_COUNT):
        state = (state + 0x9E3779B97F4A7C15) & MASK
        value = ((state ^ (state >> 30)) * 0xBF58476D1CE4E5B9) & MASK
        value = ((value ^ (value >> 27)) * 0x94D049BB133111EB) & MASK
        yield value ^ (value >> 31)


def cases():
    raw = {0, 1, 2, 0x000FFFFFFFFFFFFF, 0x0010000000000000,
           0x7FEFFFFFFFFFFFFF, 0x7FF0000000000000, 0x7FF8000000000000}
    # Decimal notation transitions, percentage scaling, exact-integer boundaries,
    # and both immediate binary64 neighbours of representative decimal fractions.
    for value in (0.01, 0.1, 0.125, 1.0, 1.02, 100.2, 1000.01,
                  1e-5, 1e-4, 1e15, 1e16, 1e17, 1e20, 2.0**53, 2.0**63):
        raw.update(bits(x) for x in (math.nextafter(value, -math.inf), value,
                                    math.nextafter(value, math.inf)))
    raw.update(value | (1 << 63) for value in tuple(raw))
    raw.update(samples())
    rows = []
    for value in sorted(raw):
        for unit in UNITS:
            rows.append({"kind": "float", "payload": f"{value:016x}", "unit": unit})
        rows.append({"kind": "percentage", "payload": f"{value:016x}", "unit": "none"})
    integers = {0, 1, -1, 2**53 - 1, 2**53, 2**53 + 1, -(2**53 + 1),
                -(2**63), -(2**63) + 1, 2**63 - 2, 2**63 - 1}
    integers.update(value if value < 2**63 else value - 2**64 for value in samples())
    for value in sorted(integers):
        for unit in UNITS:
            rows.append({"kind": "integer", "payload": str(value), "unit": unit})
    return rows


def expected(row, *, number=False):
    """Independent canonical identity oracle: exact Int64 or exact binary64 bits + unit."""
    unit = row["unit"]
    if row["kind"] == "integer":
        return f'integer:{row["payload"]}:{unit}'
    value = double(row["payload"])
    if math.isnan(value):
        return "nothing"
    if row["kind"] == "percentage":
        unit = "none"
        if math.isfinite(value) and not number:
            return f'percentage:{bits(0.0 if value == 0 else value):016x}:none'
    if math.isfinite(value) and -(2**63) <= value < 2**63 and value.is_integer():
        return f"integer:{int(value)}:{unit}"
    return f"float:{bits(value):016x}:{unit}"


def mismatches(row, response, *, reading):
    errors = []
    for key, wanted in [("source", expected(row))] + ([("number", expected(row, number=True))] if reading else []):
        if response.get(key) != wanted:
            errors.append(f"{key}: expected {wanted}, got {response.get(key)!r}")
    if reading and expected(row).startswith("percentage:") and response.get("percentage") != expected(row):
        errors.append(f"percentage: expected {expected(row)}, got {response.get('percentage')!r}")
    if not isinstance(response.get("text"), str) or not response["text"]:
        errors.append("missing formatted text")
    return errors


def run_adapter(command, name, rows):
    source = OUTPUT / f"{name}.input.json"
    target = OUTPUT / f"{name}.output.json"
    source.write_text(json.dumps(rows), encoding="utf-8")
    # A failed/stale invocation must never reuse output from an earlier run.
    target.unlink(missing_ok=True)
    subprocess.run(command + ["--number-text", str(source), str(target)], cwd=ROOT, check=True, timeout=120)
    responses = json.loads(target.read_text(encoding="utf-8"))
    if not isinstance(responses, list) or len(responses) != len(rows):
        raise AssertionError(f"{name}: response count differs from {len(rows)} requests")
    return responses


def validate(name, rows, responses, *, reading):
    failure_path = OUTPUT / f"{name}.failures.json"
    failure_path.unlink(missing_ok=True)
    failures = []
    for index, (row, response) in enumerate(zip(rows, responses, strict=True)):
        errors = mismatches(row, response, reading=reading)
        if errors:
            failures.append({"index": index, "input": row, "errors": errors})
    if failures:
        failure_path.write_text(json.dumps(failures, indent=2), encoding="utf-8")
        raise AssertionError(f"{name}: {len(failures)} mismatches; first: {failures[0]}")


def controls(command, name):
    # Wrong value, one-ULP drift, lost/wrong unit and wrong percentage scaling.
    # Every control must be rejected by the same comparator as the real exchange.
    rows = [
        {"kind": "integer", "payload": "1", "unit": "none", "text": "2"},
        {"kind": "float", "payload": f"{bits(0.1):016x}", "unit": "none", "text": "0.10000000000000002"},
        {"kind": "integer", "payload": "1", "unit": "m", "text": "1s"},
        {"kind": "integer", "payload": "1", "unit": "m", "text": "1"},
        {"kind": "percentage", "payload": f"{bits(0.1):016x}", "unit": "none", "text": "100%"},
    ]
    responses = run_adapter(command, name + "-controls", rows)
    for row, response in zip(rows, responses, strict=True):
        if not mismatches(row, response, reading=True):
            raise AssertionError(f"{name}: negative control was accepted: {row}")
    # Percentage and Number may have identical bits, but must remain distinct kinds.
    row = rows[-1]
    identity = expected(row)
    response = {"source": identity, "text": "10%", "number": expected(row, number=True),
                "percentage": identity.replace("percentage:", "float:")}
    if not mismatches(row, response, reading=True):
        raise AssertionError(f"{name}: kind-loss control was accepted")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--skip-build", action="store_true", help="Use adapters already built by test-swift.sh")
    args = parser.parse_args()
    OUTPUT.mkdir(parents=True, exist_ok=True)
    summary = OUTPUT / "summary.json"
    summary.unlink(missing_ok=True)
    if not args.skip_build:
        subprocess.run(["dotnet", "build", str(PROJECT), "--configuration", "Release", "--artifacts-path", str(EXPORT)], cwd=ROOT, check=True)
        subprocess.run(["swift", "build", "--package-path", str(PACKAGE), "--scratch-path", str(SCRATCH),
                        "--build-system", "native", "--disable-build-manifest-caching", "--configuration", "release"], cwd=ROOT, check=True)
    adapters = {
        "csharp": ["dotnet", str(ROOT / "artifacts/csharp/runtime-fixture-exporter/bin/release/GameEventScript.RuntimeFixtureExporter.dll")],
        "swift": [str(SCRATCH / "release/ges-conformance")],
    }
    rows = cases()
    written = {}
    for name, command in adapters.items():
        controls(command, name)
        written[name] = run_adapter(command, name + "-write", rows)
        validate(name + "-write", rows, written[name], reading=False)
    for writer, reader in [("csharp", "swift"), ("swift", "csharp")]:
        exchanged = [dict(row, text=result["text"]) for row, result in zip(rows, written[writer], strict=True)]
        name = writer + "-to-" + reader
        actual = run_adapter(adapters[reader], name, exchanged)
        validate(name, exchanged, actual, reading=True)
        print(f"{name}: {len(rows)}/{len(rows)} exact numeric text roundtrips passed", flush=True)
    summary.write_text(json.dumps({"status": "passed", "seed": f"{SEED:016x}", "randomBitPatterns": RANDOM_COUNT,
                                   "casesPerDirection": len(rows), "directions": 2, "negativeControlsPerAdapter": 6}, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
