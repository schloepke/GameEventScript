#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Verify actual C# ↔ Swift GES document exchange: text printed by one port reads back bit-identically in the other.

Printed layout and numeric spelling may differ between ports; only the read documents are compared, against an
independent Python oracle of exact Int64 values, Binary64 bits, units, kinds and structure.
"""

import argparse
from decimal import Decimal
import hashlib
import json
import math
from pathlib import Path
import struct
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "conformance/runner"))
from core.transport import execute  # noqa: E402
from core.runner import encode  # noqa: E402
from checks.runtime_checks import value as normalize  # noqa: E402
from checks.value_api_checks import input_value  # noqa: E402
from checks import numeric_checks  # noqa: E402

OUTPUT = ROOT / "artifacts/swift/document-roundtrip"
CSHARP_PROJECT = ROOT / "implementation/csharp/GameEventScript.Conformance/adapter/GameEventScript.ConformanceAdapter.csproj"
SWIFT_PACKAGE = ROOT / "implementation/swift/GameEventScriptConformance"
SWIFT_SCRATCH = ROOT / "artifacts/swift/conformance"
ADAPTERS = {
    "csharp": ["dotnet", str(ROOT / "artifacts/csharp/conformance-adapter/bin/Release/net10.0/GameEventScript.ConformanceAdapter.dll")],
    "swift": [str(SWIFT_SCRATCH / "release/ges-conformance-adapter")],
}
MASK = (1 << 64) - 1
SEED = 0x474553444F43554D
DOCUMENTS = 8
ENTRIES = 384
EXACT = {"mode": "exact"}
# Base symbol, conformance unit name and whether the unit has prefixed spellings.
UNITS = [("", None), ("m", "meter"), ("s", "second"), ("N", "newton"), ("J", "joule"), ("W", "watt"), ("V", "volt"), ("Hz", "hertz"),
         ("kg", "kilogram"), ("A", "ampere"), ("K", "kelvin"), ("°", "degree"), ("m/s", "meterPerSecond"), ("m/s²", "meterPerSecondSquared"),
         ("°/s", "degreePerSecond")]
TEXTS = ["", "plain", "say \"hi\"", "it's", "line\nbreak", "carriage\rreturn", "crlf\r\nend", "tab\there", "// not a comment",
         "[1, 2", "#tag", "true", "123", "😀 non-BMP", "Grüße", "trailing ", " leading", "a: b, c"]
KEYS = ["plain", "two words", "a\"b", "it's", "", "😀", "x:y", "Upper", "with\nbreak", "// key"]


def stream(seed):
    # Fixed SplitMix64 stream, independent of the product RNG and Python's random module.
    state = seed
    while True:
        state = (state + 0x9E3779B97F4A7C15) & MASK
        value = ((state ^ (state >> 30)) * 0xBF58476D1CE4E5B9) & MASK
        value = ((value ^ (value >> 27)) * 0x94D049BB133111EB) & MASK
        yield value ^ (value >> 31)


def double(bits):
    return struct.unpack(">d", struct.pack(">Q", bits))[0]


def spelling(value):
    """Exact decimal source spelling of a finite double: Python's shortest round-trip repr."""
    if math.isinf(value):
        return "Infinity" if value > 0 else "-Infinity"
    return repr(value)


def number(value, symbol, unit):
    """Document literal and oracle for one Binary64 or Int64 quantity, after integral-storage canonicalization."""
    if isinstance(value, int):
        text, storage, oracle = str(value), "int64", str(value)
    elif math.isfinite(value) and (exact := Decimal(repr(value))) == exact.to_integral_value() and -(2**63) <= exact < 2**63:
        # An integral decimal spelling within Int64 reads exactly as Int64, even when it differs from the double.
        text, storage, oracle = spelling(value), "int64", str(int(exact))
    else:
        text, storage, oracle = spelling(value), "binary64", spelling(value)
    kind = ":Quantity." if unit else ":Number."
    expected = {"type": kind + storage, "value": oracle}
    if unit:
        expected["unit"] = ":" + unit
    return text + symbol, expected


def percentage(ratio):
    magnitude = format(Decimal(repr(ratio)) * 100, "f" if abs(ratio) < 1e15 and (ratio == 0 or abs(ratio) > 1e-15) else "")
    return magnitude + "%", {"type": ":Percentage", "value": repr(0.0 if ratio == 0 else ratio)}


def quoted(text):
    return "'" + text.replace("'", "''") + "'"


def scalar(random):
    choice = next(random) % 10
    if choice < 6:
        symbol, unit = UNITS[next(random) % len(UNITS)]
        if next(random) % 3 == 0:
            raw = next(random)
            value = raw - (1 << 64) if raw >= 1 << 63 else raw
            if next(random) % 2:
                value >>= next(random) % 60
        else:
            value = double(next(random))
            while math.isnan(value):
                value = double(next(random))
        return number(value, symbol, unit)
    if choice == 6:
        ratio = double(next(random))
        while not math.isfinite(ratio):
            ratio = double(next(random))
        return percentage(ratio)
    if choice == 7:
        text = TEXTS[next(random) % len(TEXTS)]
        return quoted(text), {"type": ":Text", "value": text}
    if choice == 8:
        tag = ["ready", "drive", "a1", "isReady"][next(random) % 4]
        return "#" + tag, {"type": ":Tag", "value": tag}
    flag = next(random) % 3
    return (["true", "false", "nothing"][flag],
            [{"type": ":Boolean", "value": "true"}, {"type": ":Boolean", "value": "false"}, {"type": ":Nothing"}][flag])


def key_text(key):
    plain = key[:1].isascii() and key[:1].islower() and key.isascii() and key.isalnum()
    return key if plain else quoted(key)


def scalar_order(key):
    return [ord(c) for c in key]


def container(random, depth):
    if depth > 2 or next(random) % 3:
        return scalar(random)
    count = next(random) % 4
    if next(random) % 2:
        items = [container(random, depth + 1) for _ in range(count)]
        breaks = next(random) % 2
        separator = ",\n" if breaks else ", "
        text = "[" + ("\n" if breaks else "") + separator.join(t for t, _ in items) + ("\n]" if breaks else "]")
        return text, {"type": ":List", "items": [e for _, e in items]}
    entries = {}
    for index in range(count):
        key = KEYS[next(random) % len(KEYS)] + str(index)
        entries[key] = container(random, depth + 1)
    if not entries:
        return "[:]", {"type": ":Map", "entries": []}
    text = "[" + ", ".join(key_text(k) + ": " + t for k, (t, _) in entries.items()) + "]"
    ordered = sorted(entries.items(), key=lambda item: scalar_order(item[0]))
    return text, {"type": ":Map", "entries": [{"key": k, "value": e} for k, (_, e) in ordered]}


def documents():
    random = stream(SEED)
    result = []
    for index in range(DOCUMENTS):
        lines, entries = [], {}
        record = index % 2 == 1
        if record:
            lines.append(":Schema(:Sample) // record root")
        for entry in range(ENTRIES):
            key = f"v{entry}"
            text, expected = container(random, 0)
            lines.append(f"{key}: {text}")
            entries[key] = expected
        ordered = sorted(entries.items(), key=lambda item: scalar_order(item[0]))
        value = {"type": ":Sample" if record else ":Map", "entries": [{"key": k, "value": e} for k, e in ordered]}
        result.append({"text": "\n".join(lines) + "\n", "value": value, "schema": {"kind": "type", "name": "Sample"} if record else None})
    for name, text, value, schema in edge_documents():
        result.append({"name": name, "text": text, "value": value, "schema": schema})
    return result


def edge_documents():
    edges = [1.0, 0.25, 1e-3, 999.9999, 1000.0, 999999.5, 1e21, 1e22, 1e-7, 5e-324, 2.2250738585072014e-308,
             1.7976931348623157e308, 9007199254740993.0, 0.1, 21.4, 1.5e6, -0.8, math.inf, -math.inf]
    integers = [9007199254740993, -(2**63), 2**63 - 1, 1000, 21400, 1500000, -999]
    lines, entries = ["// boundary values for prefix selection", ":Schema('schemas/edges.gess')"], {}
    for symbol, unit in UNITS:
        for index, value in enumerate(edges + integers):
            text, expected = number(value, symbol, unit)
            key = f"u{UNITS.index((symbol, unit))}x{index}"
            lines.append(f"{key}: {text}")
            entries[key] = expected
    ordered = sorted(entries.items(), key=lambda item: scalar_order(item[0]))
    yield ("quantities", "\n".join(lines) + "\n", {"type": ":Map", "entries": [{"key": k, "value": e} for k, e in ordered]},
           {"kind": "file", "name": "schemas/edges.gess"})


def request(texts, printing):
    cases = [{"id": f"exchange/{i}", "kind": "documentApi", "sources": [], "compile": {"debugInfo": [], "binaryRoundTrip": False},
              "requires": {"core": ["document-api"], "optional": []}, "api": {"text": text, "print": {} if printing else None}}
             for i, text in enumerate(texts)]
    return {"formatVersion": 1, "requestDigest": hashlib.sha256(encode(cases)).hexdigest(), "cases": cases}


def observe(name, texts, printing):
    payload = request(texts, printing)
    observed = execute(ADAPTERS[name], payload, timeout=300)
    if observed.get("requestDigest") != payload["requestDigest"]:
        raise AssertionError(f"{name}: request digest mismatch")
    results = []
    for case in payload["cases"]:
        observation = observed["observations"].get(case["id"])
        if not isinstance(observation, dict) or observation.get("status") != "ok":
            raise AssertionError(f"{name}: {case['id']} failed: {observation}")
        results.append(observation["document"])
    return results


def mismatch(expected, read):
    """Independent comparator: exact kinds, storage, bits, units, structure and schema."""
    if not isinstance(read, dict) or "error" in read:
        return f"not readable: {read}"
    if read.get("schema") != expected["schema"]:
        return f"schema {read.get('schema')} != {expected['schema']}"
    actual = input_value(read["value"])
    if not numeric_checks.equal(normalize(expected["value"]), actual, EXACT):
        return "value differs"
    return None


def validate(label, expected, observed):
    failures = []
    for index, (document, read) in enumerate(zip(expected, observed, strict=True)):
        problem = mismatch(document, read)
        if problem:
            failures.append({"document": index, "problem": problem})
    if failures:
        path = OUTPUT / f"{label}.failures.json"
        path.write_text(json.dumps({"failures": failures, "observed": observed}, indent=2, ensure_ascii=False), encoding="utf-8")
        raise AssertionError(f"{label}: {len(failures)} mismatching documents; first: {failures[0]}")


def controls(expected, observed):
    """The comparator must reject value drift, unit loss, storage change, kind change and schema change."""
    document = expected[0]
    read = observed[0]
    entry = next(e for e in read["value"]["entries"] if e["value"]["type"] == ":Quantity.binary64" and e["value"]["value"] not in ("Infinity", "-Infinity"))
    mutations = []

    def mutated(change):
        copy = json.loads(json.dumps(read))
        target = next(e for e in copy["value"]["entries"] if e["key"] == entry["key"])
        change(copy, target["value"])
        mutations.append(copy)

    mutated(lambda _, v: v.update(value=repr(math.nextafter(float(v["value"]), math.inf))))
    mutated(lambda _, v: v.pop("unit") and v.update(type=":Number.binary64"))
    mutated(lambda _, v: v.update(unit=":second" if v["unit"] != ":second" else ":meter"))
    mutated(lambda _, v: v.update(type=":Percentage") or v.pop("unit"))
    mutated(lambda c, _: c.update(schema={"kind": "file", "name": "other.gess"}))
    integer = next(e for e in read["value"]["entries"] if e["value"]["type"].endswith(".int64"))
    copy = json.loads(json.dumps(read))
    target = next(e for e in copy["value"]["entries"] if e["key"] == integer["key"])
    target["value"]["type"] = target["value"]["type"].replace(".int64", ".binary64")
    mutations.append(copy)
    for mutation in mutations:
        if mismatch(document, mutation) is None:
            raise AssertionError(f"negative control accepted: {json.dumps(mutation)[:300]}")
    return len(mutations)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--skip-build", action="store_true", help="Use adapters already built by test-swift.sh")
    args = parser.parse_args()
    OUTPUT.mkdir(parents=True, exist_ok=True)
    summary = OUTPUT / "summary.json"
    summary.unlink(missing_ok=True)
    for stale in OUTPUT.glob("*.failures.json"):
        stale.unlink()
    if not args.skip_build:
        subprocess.run(["dotnet", "build", str(CSHARP_PROJECT), "--configuration", "Release"], cwd=ROOT, check=True)
        subprocess.run(["swift", "build", "--package-path", str(SWIFT_PACKAGE), "--scratch-path", str(SWIFT_SCRATCH),
                        "--disable-build-manifest-caching", "--configuration", "release"], cwd=ROOT, check=True)
    expected = documents()
    texts = [document["text"] for document in expected]
    printed = {}
    control_count = 0
    for name in ADAPTERS:
        observed = observe(name, texts, printing=True)
        validate(name + "-read", expected, [o["read"] for o in observed])
        validate(name + "-reread", expected, [o["reread"] for o in observed])
        control_count = controls(expected, [o["read"] for o in observed])
        printed[name] = [o["printed"] for o in observed]
        (OUTPUT / f"{name}.printed.json").write_text(json.dumps(printed[name], ensure_ascii=False, indent=2), encoding="utf-8")
    for writer, reader in [("csharp", "swift"), ("swift", "csharp")]:
        observed = observe(reader, printed[writer], printing=False)
        validate(f"{writer}-to-{reader}", expected, [o["read"] for o in observed])
        print(f"{writer}-to-{reader}: {len(expected)}/{len(expected)} documents read back bit-identically", flush=True)
    summary.write_text(json.dumps({"status": "passed", "seed": f"{SEED:016x}", "documents": len(expected),
                                   "directions": 2, "negativeControls": control_count}, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
