#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Require that computed schema fields give the values of the GES virtual machine.

Every schemaApi case of the corpus that decodes a record with computed fields is turned into a GES program: each schema
becomes a script record with the same computed expressions, the record is constructed from the case's input, and the
program prints each computed field. Both CLIs run it on their virtual machine, and every printed value must have the bits of
the corpus expectation, which the value path of both runtimes and the generated code of both languages already meet.
--self-test qualifies the comparison with negative controls.
"""
import argparse
import json
from pathlib import Path
import re
import struct
import subprocess
import sys
import tempfile

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "conformance/runner"))
from core.corpus import read_corpus  # noqa: E402

SWIFT_CLI = ROOT / "artifacts/swift/tool/debug/ges"
CSHARP_CLI = ROOT / "artifacts/csharp/tool/bin/Release/net8.0/GameEventScript.Tool.dll"
TIMEOUT_SECONDS = 120

# The symbols of the portable unit names, as literals spell them.
SYMBOLS = {"degree": "°", "meter": "m", "second": "s", "meterPerSecond": "m/s", "meterPerSecondSquared": "m/s²", "kilogram": "kg",
           "newton": "N", "joule": "J", "watt": "W", "volt": "V", "ampere": "A", "hertz": "Hz", "squareMeter": "m²", "cubicMeter": "m³",
           "wattPerSquareMeter": "W/m²", "kelvin": "K", "degreePerSecond": "°/s", "decibel": "dB", "wattPerKelvin": "W/K",
           "joulePerKelvin": "J/K"}


def schemas(formatted):
    """The schemas of a formatted source: name -> list of (field, type text, computed expression or None)."""
    result = {}
    for match in re.finditer(r"(?ms)^schema :(\w+) as \{\n(.*?)^\}", formatted):
        fields = []
        for line in match.group(2).splitlines():
            entry = re.match(r"\s+(\w+): (.*)$", line)
            if not entry:
                return None
            name, rest = entry.groups()
            computed = None
            if " computed by " in rest:
                rest, computed = rest.split(" computed by ", 1)
            fields.append((name, rest, computed))
        result[match.group(1)] = fields
    return result


def record_type(text):
    """The script record field type of a schema type: its kind and unit, without constraints."""
    for pattern, kind in ((r":Quantity\(([^)]*)\)", None), (r":Number\b", ":Number"), (r":Percentage\b", ":Percentage"),
                          (r":Boolean\b", ":Boolean"), (r":List\b", ":List")):
        match = re.match(pattern, text)
        if match:
            return kind or f":Quantity({match.group(1)})"
    match = re.match(r":([A-Z]\w*)", text)
    return f":{match.group(1)}" if match else None


def literal(value):
    """A corpus value as GES source."""
    kind = value["type"]
    unit = SYMBOLS[value["unit"][1:]] if "unit" in value else ""
    if kind.endswith(".int64") or kind.endswith(".binary64"):
        return f"{value['value']}{unit}" if not value["value"].startswith("-") else f"({value['value']}{unit})"
    if kind == ":Percentage":
        return f"({value['value']} as :Percentage)"
    if kind == ":Boolean":
        return "true" if value["value"] in (True, "true") else "false"
    if kind == ":List":
        return "[" + ", ".join(literal(item) for item in value.get("items", [])) + "]"
    if kind == ":Nothing":
        return "nothing"
    entries = [f"{entry['key']}: {literal(entry['value'])}" for entry in value.get("entries", []) if entry["value"]["type"] != ":Nothing"]
    return f"{kind}({', '.join(entries)})"


def program(declared, root, value):
    """A program that constructs the record and prints its computed fields, each as its number, ratio or Boolean."""
    lines = []
    for name, fields in declared.items():
        parts = []
        for field, text, computed in fields:
            kind = record_type(text)
            if kind is None:
                return None, []
            parts.append(f"  {field}: {kind}" + (f" computed by {computed}" if computed else ""))
        lines += [f"record :{name} as {{", ",\n".join(parts), "}"]
    printed = []
    lines += ["on Main(args) {", f"  let value be {literal(value)}"]
    for field, text, computed in declared[root]:
        if computed is None:
            continue
        kind = record_type(text)
        shown = f"value.{field} as :Quantity(none)" if kind.startswith(":Quantity") else f"value.{field} as :Number" if kind == ":Percentage" else f"value.{field}"
        lines.append(f"  emit ConsoleOut({shown})")
        printed.append(field)
    lines.append("}")
    return "\n".join(lines) + "\n", printed


def bits(text):
    """A printed value: the bits of a number, a Boolean, or nothing."""
    if text in ("true", "false", "nothing"):
        return text
    number = {"Infinity": "inf", "-Infinity": "-inf", "∞": "inf", "-∞": "-inf"}.get(text, text)
    return format(struct.unpack(">Q", struct.pack(">d", float(number)))[0], "016x")


def expected(value):
    kind = value["type"]
    if kind == ":Nothing":
        return "nothing"
    if kind == ":Boolean":
        return "true" if value["value"] in (True, "true") else "false"
    return bits(value["value"])


def difference(wanted, seen):
    """None when the virtual machine printed the expected values, otherwise a short description."""
    if len(wanted) != len(seen):
        return f"expected {len(wanted)} values, got {seen}"
    for (name, want), got in zip(wanted, seen):
        if want != got:
            return f"{name}: expected {want}, got {got}"
    return None


def cases(paths):
    result = []
    for case in read_corpus(paths):
        api = case.metadata.get("schemaApi") if case.kind == "schemaApi" else None
        decoded = ((case.expected or {}).get("schema") or {}).get("decoded") or {}
        if not isinstance(api, dict) or "computed by" not in "".join(api.get("sources") or []) or "value" not in decoded or "encode" not in api:
            continue
        result.append((case.id, api["sources"], api["root"], api["encode"], decoded["value"]))
    return result


def check(csharp, swift):
    failures = []
    items = cases(sorted((ROOT / "conformance/suites").rglob("*.md")))
    for case_id, sources, root, value, decoded in items:
        with tempfile.TemporaryDirectory() as temporary:
            work = Path(temporary)
            for index, text in enumerate(sources):
                (work / f"s{index}.gess").write_text(text, encoding="utf-8")
            formatted = ""
            for index in range(len(sources)):
                run = subprocess.run([*csharp, "schema", "format", f"s{index}.gess"], cwd=work, capture_output=True, check=False, timeout=TIMEOUT_SECONDS)
                if run.returncode != 0:
                    failures.append(f"{case_id}: format failed: {run.stderr.decode('utf-8', 'replace').strip()}")
                formatted += run.stdout.decode("utf-8")
            declared = schemas(formatted)
            text, printed = program(declared, root, value) if declared and root in declared else (None, [])
            if text is None:
                failures.append(f"{case_id}: the schemas do not translate to script records")
                continue
            (work / "computed.ges").write_text(text, encoding="utf-8")
            entries = {entry["key"]: entry["value"] for entry in decoded["entries"]}
            wanted = [(name, expected(entries[name])) for name in printed]
            for port, command in (("C#", csharp), ("Swift", swift)):
                run = subprocess.run([*command, "run", "computed.ges"], cwd=work, capture_output=True, check=False, timeout=TIMEOUT_SECONDS)
                if run.returncode != 0:
                    failures.append(f"{port} {case_id}: {run.stderr.decode('utf-8', 'replace').strip()}")
                    continue
                found = difference(wanted, [bits(line) for line in run.stdout.decode("utf-8").splitlines()])
                if found:
                    failures.append(f"{port} {case_id}: {found}")
    return len(items), failures


def self_test():
    assert difference([("a", bits("1"))], [bits("1")]) is None
    assert difference([("a", bits("0.1"))], [bits("0.30000000000000004")])
    assert difference([("a", "nothing")], [bits("0")])
    assert difference([("a", "true")], ["false"])
    assert difference([("a", "true")], [])
    assert literal({"type": ":Quantity.binary64", "value": "0.25", "unit": ":second"}) == "0.25s"
    assert literal({"type": ":Percentage", "value": "0.25"}) == "(0.25 as :Percentage)"
    assert record_type(":Quantity(W) between 0W and 1kW step 1W") == ":Quantity(W)"
    declared = schemas("schema :S as {\n  a: :Number\n  b: :Number computed by a * 2\n}\n")
    assert declared == {"S": [("a", ":Number", None), ("b", ":Number", "a * 2")]}
    print("Computed field comparison controls passed.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--csharp", type=Path, default=CSHARP_CLI, help="C# GameEventScript.Tool.dll")
    parser.add_argument("--swift", type=Path, default=SWIFT_CLI, help="Swift ges executable")
    parser.add_argument("--self-test", action="store_true", help="only check the comparison with negative controls")
    args = parser.parse_args()
    if args.self_test:
        self_test()
        return 0
    count, failures = check(["dotnet", str(args.csharp)], [str(args.swift)])
    for line in failures:
        print(line)
    print(f"{count} records with computed fields on both virtual machines: {len(failures)} differences")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
