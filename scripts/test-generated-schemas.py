#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Check generated schema code against the shared corpus in C# and Swift.

The schemaApi cases with literal parameters name a root and give bytes to decode, bytes a value encodes to, a key, or a
delta to encode or decode. For every such schema the C# CLI generates C# and Swift, which compile into one C# program
(C# 9, warnings as errors) and one Swift executable (Swift 6 language mode, warnings as errors). Each program decodes the
given bytes with the generated type and writes the record again: an expected error must have the same code and path, a
valid record the same byte count and the same bytes, or the encoded bytes of the case where it also encodes a value. A delta is encoded from the decoded record and baseline, and decoded
against the baseline into the expected record. Operations on types without delta methods are counted as skipped.
The programs check shared baselines (GES_CHECK_SHARING); the unchecked form of release builds also compiles in both
languages, and a small C# program holds its remaining guarantee on the fleet fixture, qualified by a control without the
guard that must fail. --self-test qualifies the comparison.
"""
import argparse
from pathlib import Path
import re
import shutil
import struct
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "conformance/runner"))
from core.corpus import read_corpus  # noqa: E402

CSHARP_CLI = ROOT / "artifacts/csharp/tool/bin/Release/net8.0/GameEventScript.Tool.dll"
WORK = ROOT / "artifacts/generated-schemas"
CODE = re.compile(r"error ([a-z]+\.[A-Za-z]+)")
FLEET_FIXTURE = ROOT / "conformance/fixtures/SchemaGeneration/fleet.gess"
GENERATED_CSHARP = ROOT / "implementation/csharp/GameEventScript.Generator/tests/Generated"
ROLES = ROOT / "conformance/fixtures/SchemaGeneration/roles"
ROLE_FILES = ["World.gess", "Panels.gess", "View.gess"]
# Split output of the role fixture: each split once for wire code and once for models, so that the files of one generation
# reach each other's types and helpers and the shared file declares the namespace, nested for two of them.
SPLITS = (("schema", ["--wire", "world", "--param", "worldWidth=2000m", "--values"], "SchemaSplit.Wire"),
          ("file", ["--wire", "world", "--param", "worldWidth=2000m", "--values"], "FileSplitWire"),
          ("schema", ["--model", "view", "--bindings", "--values"], "SchemaSplitModel"),
          ("file", ["--model", "view", "--values"], "FileSplit.Model"))
GENERATED_SWIFT = ROOT / "implementation/swift/GameEventScriptConformance/Tests/GameEventScriptConformanceTests/Generated"
UNCHECKED_PROGRAM = """using System;
using Unchecked;

var source = new Fleet { Tick = 3 };
for (var index = 0; index < 3; index++) source.Ships.Add(new Ship { Id = index, Health = 10 });
var fleet = new Fleet();
fleet.Read(source.ToBytes());

// Without the check, a baseline that shares an unchanged list in the same place still keeps its values.
var shared = new Fleet { Tick = fleet.Tick, Ships = fleet.Ships };
var next = new Fleet();
next.CopyFrom(fleet);
next.Tick = 4;
fleet.ReadDelta(next.ToDeltaBytes(shared), shared);
if (fleet.Ships.Count != 3 || !fleet.WireEquals(next))
{
    Console.WriteLine("A shared unchanged list lost its values.");
    return 1;
}

return 0;
"""


def computed_names(sources, root):
    """The computed fields of the root schema, in declared order, read from its source."""
    for text in sources:
        match = re.search(r"schema :" + re.escape(root) + r" as \{(.*?)\n\}", text, re.S) or re.search(r"schema :" + re.escape(root) + r" as \{([^\n]*)\}", text)
        if match:
            return [name for name in re.findall(r"(?:^|[\n,{])\s*([a-z][A-Za-z0-9]*): [^\n,]*? computed by", match.group(1))]
    return []


def property_text(value):
    """A computed property as the harness prints it: the bits of a binary64, a Boolean, or nothing."""
    kind = value["type"]
    if kind == ":Nothing":
        return "nothing"
    if kind == ":Boolean":
        return "true" if value["value"] in (True, "true") else "false"
    return format(struct.unpack(">Q", struct.pack(">d", float(value["value"])))[0], "016x")


def failure(error):
    return ("error", error["code"], error.get("path"))


def cases(paths):
    """Schemas with their operations: (sources, root, parameters) -> list of (case id, operation, hex, second hex, limit, expected)."""
    result = {}
    for case in read_corpus(paths):
        api = case.metadata.get("schemaApi") if case.kind == "schemaApi" else None
        if not isinstance(api, dict) or not api.get("sources") or not api.get("root"):
            continue
        if not all(isinstance(value, str) for value in (api.get("parameters") or {}).values()):
            continue
        expected = (case.expected or {}).get("schema") or {}
        operations = []
        decoded = expected.get("decoded")
        if isinstance(api.get("decode"), str) and isinstance(decoded, dict) and "baseline" not in api:
            limit = api.get("decodeLimit")
            if "error" in decoded:
                operations.append(("decode", api["decode"], "", limit, failure(decoded["error"])))
            elif "value" in decoded:
                size = decoded.get("byteCount")
                # A case that also encodes a value gives the bytes the decoded record writes, which differ only where the value
                # orders its parts, as a Box its corners.
                written = (expected.get("encoded") or {}).get("bytes")
                outcome = ("ok", size if size is not None else len(api["decode"]) // 2) + ((written,) if isinstance(written, str) else ())
                operations.append(("decode", api["decode"], "", limit, outcome))
                names = computed_names(api["sources"], api["root"])
                if names:
                    entries = {entry["key"]: entry["value"] for entry in decoded["value"].get("entries", [])}
                    operations.append(("computed", api["decode"], "", limit, ("computed", " ".join(property_text(entries[name]) for name in names))))
        encoded = expected.get("encoded")
        if isinstance(encoded, dict) and isinstance(encoded.get("bytes"), str) and "baseline" not in api:
            operations.append(("decode", encoded["bytes"], "", None, ("ok", len(encoded["bytes"]) // 2)))
        if isinstance(api.get("key"), str) and isinstance(expected.get("key"), int):
            operations.append(("key", api["key"], "", None, ("key", expected["key"])))
        delta, outcome = api.get("deltaEncode"), expected.get("deltaEncoded")
        if isinstance(delta, dict) and isinstance(outcome, dict):
            operations.append(("deltaEncode", delta["record"], delta["baseline"], None, failure(outcome["error"]) if "error" in outcome else ("delta", outcome["bytes"])))
        delta, outcome = api.get("deltaDecode"), expected.get("deltaDecoded")
        if isinstance(delta, dict) and isinstance(outcome, dict):
            expectation = failure(outcome["error"]) if "error" in outcome else ("record", outcome["record"], outcome["deltaByteCount"])
            operations.append(("deltaDecode", delta["delta"], delta["baseline"], delta.get("byteLimit"), expectation))
        if operations:
            key = (tuple(api["sources"]), api["root"], tuple(sorted((api.get("parameters") or {}).items())))
            result.setdefault(key, []).extend((case.id, *operation) for operation in operations)
    return result


# Keyword names in both languages, a tag set beyond 256 tags and a type that two roots share.
GATE_SCHEMA = """schema :Shared as {
  class: :Boolean
  default: :Number integer between 0 and 3
}

schema :First as {
  struct: :Shared
  self: :Boolean
  init: :Text max 2
  operator: :Tag(""" + ", ".join(f"#t{index}" for index in range(300)) + """)
}

schema :Second as {
  in: :Shared
  internal: :Number optional
  var: :List(:Shared) max 2
}
"""

def generate(directory, sources, root, parameters, language, space, more=()):
    directory.mkdir(parents=True, exist_ok=True)
    files = []
    for number, text in enumerate(sources):
        path = directory / f"s{number}.gess"
        path.write_text(text, encoding="utf-8")
        files.append(path.name)
    options = [argument for name, value in parameters for argument in ("--param", f"{name}={value}")]
    run = subprocess.run(["dotnet", str(CSHARP_CLI), "schema", "generate", *files, "--root", root, *more, *options, "--language", language, "--namespace", space, "--values"],
                         cwd=directory, capture_output=True, check=False)
    if run.returncode != 0:
        match = CODE.search(run.stderr.decode("utf-8", "replace"))
        return None, match.group(1) if match else run.stderr.decode("utf-8", "replace").strip()
    return run.stdout.decode("utf-8"), None


def with_model_role(sources, root):
    """The sources with the root marked as a model root, or None when its declaration has roles or another form."""
    pattern = re.compile(rf"(?m)^schema :{re.escape(root)} as\b")
    if sum(len(pattern.findall(text)) for text in sources) != 1:
        return None
    return [pattern.sub(f"model schema :{root} as", text) for text in sources]


def generate_models(directory, sources, language, space):
    """Model code of the default group, or None when the schemas do not generate as models."""
    directory.mkdir(parents=True, exist_ok=True)
    files = []
    for number, text in enumerate(sources):
        path = directory / f"s{number}.gess"
        path.write_text(text, encoding="utf-8")
        files.append(path.name)
    run = subprocess.run(["dotnet", str(CSHARP_CLI), "schema", "generate", *files, "--model", "default", "--language", language, "--namespace", space],
                         cwd=directory, capture_output=True, check=False)
    return run.stdout.decode("utf-8") if run.returncode == 0 else None


def declares(code, root, member):
    """Whether the generated C# root type declares a member, within its own declaration."""
    found = re.search(rf" {re.escape(root)}( : [^\n]*)?\n    {{", code)
    if found is None:
        return False
    end = code.find("\n    }\n", found.start())
    return member in code[found.start():end]


def has_delta(code, root):
    return declares(code, root, "public int ReadDelta(")


def member(name, owner, swift=False):
    """The property of a computed field: its name in Pascal case, with Value when it equals its type, and camel case in Swift."""
    pascal = name[0].upper() + name[1:]
    if pascal == owner:
        pascal += "Value"
    return pascal[0].lower() + pascal[1:] if swift else pascal


def csharp_program(entries):
    lines = ["using System;", "", "internal static class Harness", "{",
             "    private static string Hex(ReadOnlySpan<byte> bytes) { var text = new System.Text.StringBuilder(); foreach (var b in bytes) text.Append(b.ToString(\"x2\")); return text.ToString(); }",
             "    private static string P(double value) => double.IsNaN(value) ? \"nothing\" : ((ulong)BitConverter.DoubleToInt64Bits(value)).ToString(\"x16\");",
             "    private static string P(double? value) => value is { } present ? P(present) : \"nothing\";",
             "    private static string P(bool value) => value ? \"true\" : \"false\";",
             "    private static string Run(int schema, string operation, byte[] data, byte[] second, long limit)", "    {", "        switch (schema)", "        {"]
    for index, root, key, _, delta, conversion, computed, model in entries:
        name = f"Gen.Case{index}.{root}"
        lines += [f"            case {index}:", "            {"]
        lines += [f"                if (operation == \"key\") return \"ok \" + ((long){name}.ReadKey(data)).ToString();" if key else "                if (operation == \"key\") return \"none\";"]
        lines += [f"                var record = new {name}();"]
        if computed:
            values = ", ".join(f"P(record.{member(field, root)})" for field in computed)
            lines += ["                if (operation == \"computed\")", "                {", "                    record.Read(data, limit);", f"                    return \"ok \" + string.Join(\" \", new[] {{ {values} }});", "                }"]
        if delta:
            lines += ["                if (operation != \"decode\")", "                {", f"                    var baseline = new {name}();", "                    baseline.Read(second);",
                      "                    if (operation == \"deltaDecode\")", "                    {", "                        var used = record.ReadDelta(data, baseline, limit);",
                      "                        return \"ok \" + used + \" \" + Hex(record.ToBytes());", "                    }", "                    record.Read(data);",
                      "                    return \"ok \" + Hex(record.ToDeltaBytes(baseline));", "                }"]
        else:
            lines += ["                if (operation != \"decode\") return \"none\";"]
        # A decoded record converts into a GES value, through the embedded schema sources, and that value back into an equal one;
        # equal, not the same bytes, since a Dice value orders its rolls. External values such as a Box compare by identity, so
        # two of them are alike when their JSON snapshots are.
        wrap = "" if conversion == "Value" else "GameEventScript.Runtime.Values.GesValue.GesMessage"
        lines += ["                var count = record.Read(data, limit);", f"                var copy = new {name}();", f"                var value = record.To{conversion}();",
                  f"                copy.Read{conversion}(value);", f"                if (!Same({wrap}(copy.To{conversion}()), {wrap}(value))) return \"value mismatch\";"]
        # A record with a model converts to it and back directly, to the same bytes.
        if model:
            lines += [f"                var round = new {name}();", "                round.CopyFrom(record.ToModel());",
                      "                if (Hex(round.ToBytes()) != Hex(record.ToBytes())) return \"model mismatch\";"]
        lines += [
                  "                return \"ok \" + count + \" \" + Hex(record.ToBytes());", "            }"]
    lines += ["            default: return \"none\";", "        }", "    }", "",
              "    private static byte[] Bytes(string text) => Convert.FromHexString(text == \".\" ? \"\" : text);", "",
              "    private static bool Same(GameEventScript.Runtime.Values.GesValue a, GameEventScript.Runtime.Values.GesValue b) => a.Equals(b) || a.ToJson() == b.ToJson();", "",
              "    private static void Main()", "    {", "        string? line;", "        while ((line = Console.ReadLine()) != null)", "        {",
              "            var parts = line.Split(' ');", "            var limit = parts[4] == \"-\" ? long.MaxValue : long.Parse(parts[4]);",
              "            try { Console.WriteLine(Run(int.Parse(parts[0]), parts[1], Bytes(parts[2]), Bytes(parts[3]), limit)); }",
              "            catch (GameEventScript.Api.GesWireException error) { Console.WriteLine(\"error \" + error.Code + \" \" + error.Path); }", "        }", "    }", "}"]
    return "\n".join(lines) + "\n"


def swift_program(entries):
    lines = ["import GameEventScriptRuntime", "",
             "func hex(_ bytes: [UInt8]) -> String { bytes.map { ($0 < 16 ? \"0\" : \"\") + String($0, radix: 16) }.joined() }", "",
             "func bytes(_ text: Substring) -> [UInt8] {", "    if text == \".\" { return [] }", "    var result: [UInt8] = []", "    var index = text.startIndex",
             "    while index < text.endIndex {", "        let next = text.index(index, offsetBy: 2)", "        result.append(UInt8(text[index..<next], radix: 16)!)",
             "        index = next", "    }", "    return result", "}", "",
             "func p(_ value: Double) -> String { value.isNaN ? \"nothing\" : String(value.bitPattern, radix: 16).leftPadded() }",
             "func p(_ value: Double?) -> String { value.map { p($0) } ?? \"nothing\" }",
             "func p(_ value: Bool) -> String { value ? \"true\" : \"false\" }",
             "extension String { func leftPadded() -> String { String(repeating: \"0\", count: Swift.max(0, 16 - count)) + self } }", "",
             "func run(_ schema: Int, _ operation: Substring, _ data: [UInt8], _ second: [UInt8], _ limit: Int) throws(GesWireError) -> String {", "    switch schema {"]
    for index, root, key, is_class, delta, conversion, computed, model in entries:
        binding = "let" if is_class else "var"
        lines += [f"    case {index}:"]
        if computed:
            values = ", ".join(f"p(record.{member(field, root, swift=True)})" for field in computed)
            lines += ["        if operation == \"computed\" {", f"            {binding} record = Case{index}.{root}()", "            _ = try record.read(from: data, byteLimit: limit)",
                      f"            return \"ok \" + [{values}].joined(separator: \" \")", "        }"]
        if key:
            lines += ["        if operation == \"key\" {", "            let buffer = UnsafeMutableRawBufferPointer.allocate(byteCount: max(data.count, 1), alignment: 8)",
                      "            defer { buffer.deallocate() }", "            buffer.copyBytes(from: data)",
                      f"            return \"ok \" + String(Int64(try Case{index}.{root}.readKey(from: UnsafeRawBufferPointer(rebasing: buffer[0..<data.count]))))", "        }"]
        else:
            lines += ["        if operation == \"key\" { return \"none\" }"]
        lines += [f"        {binding} record = Case{index}.{root}()"]
        if delta:
            lines += ["        if operation != \"decode\" {", f"            {binding} baseline = Case{index}.{root}()", "            try baseline.read(from: second)",
                      "            if operation == \"deltaDecode\" {", "                let used = try record.readDelta(from: data, against: baseline, byteLimit: limit)",
                      "                return \"ok \\(used) \" + hex(try record.encoded())", "            }", "            try record.read(from: data)",
                      "            return \"ok \" + hex(try record.deltaEncoded(against: baseline))", "        }"]
        else:
            lines += ["        if operation != \"decode\" { return \"none\" }"]
        label = conversion.lower()
        lines += ["        let count = try record.read(from: data, byteLimit: limit)", "        do {", f"            {binding} copy = Case{index}.{root}()",
                  f"            let value = try record.to{conversion}()", f"            try copy.read({label}: value)",
                  f"            if try copy.to{conversion}() != value, try copy.to{conversion}().json() != value.json() {{ return \"value mismatch\" }}",
                  "        } catch {", "            return \"value error \\(error)\"", "        }"]
        if model:
            lines += [f"        {binding} round = Case{index}.{root}()", "        round.read(model: record.toModel())",
                      "        if try round.encoded() != record.encoded() { return \"model mismatch\" }"]
        lines += ["        return \"ok \\(count) \" + hex(try record.encoded())"]
    lines += ["    default:", "        return \"none\"", "    }", "}", "",
              "while let line = readLine() {", "    let parts = line.split(separator: \" \", omittingEmptySubsequences: false)",
              "    let limit = parts[4] == \"-\" ? Int.max : Int(parts[4])!", "    do {",
              "        print(try run(Int(parts[0])!, parts[1], bytes(parts[2]), bytes(parts[3]), limit))", "    } catch {", "        print(\"error \\(error.code) \\(error.path)\")", "    }", "}"]
    return "\n".join(lines) + "\n"


def verdict(expected, observed, data):
    """None when an observation meets the corpus expectation, otherwise why not."""
    parts = observed.split(" ")
    if expected[0] == "error":
        # A case without a path leaves it open, as in the runner.
        actual = (parts[1], " ".join(parts[2:]) if expected[2] is not None else None) if parts[0] == "error" else None
        return None if actual == (expected[1], expected[2]) else f"expected {expected[1]} at '{expected[2]}', got {observed!r}"
    if expected[0] == "key":
        return None if parts[:2] == ["ok", str(expected[1])] else f"expected key {expected[1]}, got {observed!r}"
    if expected[0] == "delta":
        return None if parts == ["ok", expected[1]] else f"expected delta {expected[1]}, got {observed!r}"
    if expected[0] == "computed":
        return None if observed == "ok " + expected[1] else f"expected computed {expected[1]}, got {observed!r}"
    if expected[0] == "record":
        return None if parts == ["ok", str(expected[2]), expected[1]] else f"expected record {expected[1]} after {expected[2]} bytes, got {observed!r}"
    if parts[0] != "ok" or int(parts[1]) != expected[1]:
        return f"expected {expected[1]} bytes, got {observed!r}"
    written = expected[2] if len(expected) > 2 else data[:2 * expected[1]]
    return None if parts[2] == written else f"wrote {parts[2]} instead of {written}"


def self_test():
    assert verdict(("error", "wire.truncated", "x"), "error wire.truncated x", "") is None
    assert verdict(("error", "wire.truncated", "x"), "error wire.truncated y", "")
    assert verdict(("error", "wire.truncated", ""), "error wire.truncated ", "") is None
    assert verdict(("error", "wire.truncated", None), "error wire.truncated x", "") is None
    assert verdict(("error", "wire.truncated", None), "error wire.invalidIndex x", "")
    assert verdict(("ok", 2), "ok 2 0a0b", "0a0b0c") is None
    assert verdict(("ok", 2, "0b0a"), "ok 2 0b0a", "0a0b0c") is None
    assert verdict(("ok", 2, "0b0a"), "ok 2 0a0b", "0a0b0c")
    assert verdict(("ok", 2), "ok 2 0a0c", "0a0b0c")
    assert verdict(("ok", 2), "ok 3 0a0b0c", "0a0b0c")
    assert verdict(("ok", 2), "error wire.truncated x", "0a0b")
    assert verdict(("key", 7), "ok 7", "") is None
    assert verdict(("key", 7), "ok 8", "")
    assert verdict(("delta", "0741"), "ok 0741", "") is None
    assert verdict(("delta", "0741"), "ok 0742", "")
    assert verdict(("record", "0a0b", 3), "ok 3 0a0b", "") is None
    assert verdict(("record", "0a0b", 3), "ok 2 0a0b", "")
    assert verdict(("record", "0a0b", 3), "ok 3 0a0c", "")
    assert has_delta("    public partial struct S\n    {\n        public int ReadDelta(\n    }\n", "S")
    assert not has_delta("    public partial struct S\n    {\n    }\n    public partial struct T\n    {\n        public int ReadDelta(\n    }\n", "S")
    sample = cases([ROOT / "conformance/suites/api/schemas.md"])
    assert sample and all(operations for operations in sample.values())
    print("Generated schema controls passed.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--self-test", action="store_true", help="only check the comparison with negative controls")
    args = parser.parse_args()
    if args.self_test:
        self_test()
        return 0
    if WORK.exists():
        shutil.rmtree(WORK)
    csharp, swift = WORK / "csharp", WORK / "swift/Sources/Harness"
    csharp.mkdir(parents=True)
    swift.mkdir(parents=True)
    schemas = cases(sorted((ROOT / "conformance/suites").rglob("*.md")))
    entries, requests, skipped = [], [], {}
    for index, ((sources, root, parameters), operations) in enumerate(schemas.items()):
        directory = WORK / "schemas" / str(index)
        directory.mkdir(parents=True)
        code, error = generate(directory, sources, root, parameters, "csharp", f"Gen.Case{index}")
        if code is None:
            skipped[error] = skipped.get(error, 0) + len(operations)
            continue
        swift_code, _ = generate(directory, sources, root, parameters, "swift", f"Case{index}")
        conversion = "Value" if declares(code, root, " ToValue(") else "Message"
        # A record root that also generates as a model converts to it directly: the wire code shares the models' namespace.
        model = False
        marked = with_model_role(sources, root) if conversion == "Value" else None
        if marked is not None:
            models = {language: generate_models(directory / f"models-{language}", marked, language, space)
                      for language, space in (("csharp", f"Gen.Case{index}M"), ("swift", f"Case{index}M"))}
            if all(text is not None for text in models.values()):
                wired = {language: generate(directory / f"wired-{language}", marked, root, parameters, language, space, ["--models", models_space])[0]
                         for language, space, models_space in (("csharp", f"Gen.Case{index}", f"Gen.Case{index}M"), ("swift", f"Case{index}", f"Case{index}M"))}
                if all(text is not None for text in wired.values()):
                    code, swift_code, model = wired["csharp"], wired["swift"], True
                    (csharp / f"Case{index}M.g.cs").write_text(models["csharp"], encoding="utf-8")
                    (swift / f"Case{index}M.swift").write_text(models["swift"], encoding="utf-8")
        (csharp / f"Case{index}.g.cs").write_text(code, encoding="utf-8")
        (swift / f"Case{index}.swift").write_text(swift_code, encoding="utf-8")
        delta = has_delta(code, root)
        entries.append((index, root, declares(code, root, " ReadKey("), f"final class {root} {{" in swift_code, delta, conversion, computed_names(sources, root), model))
        for case_id, operation, data, second, limit, expected in operations:
            if operation.startswith("delta") and not delta:
                skipped["no delta methods"] = skipped.get("no delta methods", 0) + 1
                continue
            requests.append((case_id, index, operation, data.lower(), second.lower(), limit, expected))
    # The compilation gate: keyword names, a tag set beyond 256 tags and two roots sharing a type, in a namespace.
    gate = WORK / "schemas/gate"
    gate.mkdir(parents=True)
    for language, target, space in (("csharp", csharp / "Gate.g.cs", "Gen.Gate"), ("swift", swift / "Gate.swift", "Gate")):
        code, error = generate(gate, [GATE_SCHEMA], "First", [], language, space, ["--root", "Second"])
        if code is None:
            raise SystemExit(f"The gate schema does not generate: {error}")
        target.write_text(code, encoding="utf-8")
    # The checked-in wire and model code of the role fixture, which compare-schema-generation.py keeps current, compiles
    # under the same rules: models of every field type and spatial wire fields in 2D and 3D.
    for name in ("RolesWire", "RolesModel", "RolesBound"):
        shutil.copy(GENERATED_CSHARP / f"{name}.g.cs", csharp / f"{name}.g.cs")
        shutil.copy(GENERATED_SWIFT / f"{name}.swift", swift / f"{name}.swift")
    for split, selection, space in SPLITS:
        for language, target in (("csharp", csharp), ("swift", swift)):
            run = subprocess.run(["dotnet", str(CSHARP_CLI), "schema", "generate", *ROLE_FILES, *selection, "--language", language, "--namespace", space,
                                  "--access", "internal", "--out-dir", str(target / space), "--split", split], cwd=ROLES, capture_output=True, check=False)
            if run.returncode != 0:
                raise SystemExit(f"The role fixture does not generate split by {split}: {run.stderr.decode('utf-8', 'replace').strip()}")
    (csharp / "Harness.cs").write_text(csharp_program(entries), encoding="utf-8")
    (csharp / "Harness.csproj").write_text(f"""<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>9</LangVersion>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <!-- The harness checks shared baselines, so the conditional code of every schema compiles and runs; Unity below compiles without it. -->
    <DefineConstants>$(DefineConstants);GES_CHECK_SHARING</DefineConstants>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="{ROOT / 'implementation/csharp/GameEventScript.Runtime/src/GameEventScript.Runtime.csproj'}" />
  </ItemGroup>
</Project>
""", encoding="utf-8")
    (swift / "main.swift").write_text(swift_program(entries), encoding="utf-8")
    (WORK / "swift/Package.swift").write_text(f"""// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "Harness",
    platforms: [.macOS("10.15.4")],
    dependencies: [.package(path: "{ROOT / 'implementation/swift/GameEventScriptRuntime'}")],
    targets: [
        .executableTarget(
            name: "Harness",
            dependencies: [.product(name: "GameEventScriptRuntime", package: "GameEventScriptRuntime")],
            swiftSettings: [.unsafeFlags(["-warnings-as-errors"]), .define("GES_CHECK_SHARING")]
        ),
        // The same sources without the check, as release builds of users compile them; one build shares the Runtime.
        .executableTarget(
            name: "Unchecked",
            dependencies: [.product(name: "GameEventScriptRuntime", package: "GameEventScriptRuntime")],
            swiftSettings: [.unsafeFlags(["-warnings-as-errors"])]
        ),
    ]
)
""", encoding="utf-8")
    # Unity compiles C# 9 against .NET Standard 2.1, without unsafe code.
    unity = WORK / "csharp-unity"
    unity.mkdir()
    (unity / "Unity.csproj").write_text(f"""<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <LangVersion>9</LangVersion>
    <Nullable>enable</Nullable>
    <AllowUnsafeBlocks>false</AllowUnsafeBlocks>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="{csharp}/**/*.g.cs" />
    <ProjectReference Include="{ROOT / 'implementation/csharp/GameEventScript.Runtime/src/GameEventScript.Runtime.csproj'}" />
  </ItemGroup>
</Project>
""", encoding="utf-8")
    # The unchecked Swift target compiles copies of the harness sources.
    unchecked_swift = WORK / "swift/Sources/Unchecked"
    shutil.copytree(swift, unchecked_swift)
    # Release builds of users skip the check of shared baselines; this program holds the guarantee that remains there. Its
    # control removes the guard of the unchanged list from the same code and must fail, so the program cannot pass vacuously.
    fleet_directory = WORK / "schemas/fleet"
    fleet_directory.mkdir(parents=True)
    fleet, error = generate(fleet_directory, [FLEET_FIXTURE.read_text(encoding="utf-8")], "Fleet", [("worldSize", "2km")], "csharp", "Unchecked")
    if fleet is None:
        raise SystemExit(f"The fleet fixture does not generate: {error}")
    reading = fleet.index("internal bool __ReadDelta(Fleet baseline")
    guard = fleet.find("if (!ReferenceEquals(Ships, baseline.Ships))", reading)
    if guard < 0:
        raise SystemExit("The fleet code has no guard for a shared list in its delta reader.")
    unguarded = fleet[:guard] + "if (true)" + fleet[guard + len("if (!ReferenceEquals(Ships, baseline.Ships))"):]
    for name, code in (("csharp-unchecked", fleet), ("csharp-unchecked-control", unguarded)):
        project = WORK / name
        project.mkdir()
        (project / "Fleet.g.cs").write_text(code, encoding="utf-8")
        (project / "Program.cs").write_text(UNCHECKED_PROGRAM, encoding="utf-8")
        (project / "Unchecked.csproj").write_text(f"""<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>9</LangVersion>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="{ROOT / 'implementation/csharp/GameEventScript.Runtime/src/GameEventScript.Runtime.csproj'}" />
  </ItemGroup>
</Project>
""", encoding="utf-8")
    for command in (["dotnet", "build", str(unity), "-c", "Release", "-o", str(WORK / "csharp-unity-bin"), "-nologo"],
                    ["dotnet", "build", str(csharp), "-c", "Release", "-o", str(WORK / "csharp-bin"), "-nologo"],
                    ["dotnet", "build", str(WORK / "csharp-unchecked"), "-c", "Release", "-o", str(WORK / "csharp-unchecked-bin"), "-nologo"],
                    ["dotnet", "build", str(WORK / "csharp-unchecked-control"), "-c", "Release", "-o", str(WORK / "csharp-unchecked-control-bin"), "-nologo"],
                    ["swift", "build", "-c", "release", "--package-path", str(WORK / "swift"), "--disable-build-manifest-caching"]):
        build = subprocess.run(command, capture_output=True, check=False)
        if build.returncode != 0:
            errors = [line for line in build.stdout.decode("utf-8", "replace").splitlines() + build.stderr.decode("utf-8", "replace").splitlines() if "error" in line]
            raise SystemExit("\n".join(dict.fromkeys(errors[:40])) or f"{command[0]} build failed")
    lines = "".join(f"{index} {operation} {data or '.'} {second or '.'} {'-' if limit is None else limit}\n" for _, index, operation, data, second, limit, _ in requests)
    runs = {"C#": ["dotnet", str(WORK / "csharp-bin/Harness.dll")], "Swift": [str(WORK / "swift/.build/release/Harness")]}
    failures = 0
    guarantee = subprocess.run(["dotnet", str(WORK / "csharp-unchecked-bin/Unchecked.dll")], capture_output=True, check=False)
    if guarantee.returncode != 0:
        failures += 1
        print("C# unchecked:", (guarantee.stdout + guarantee.stderr).decode("utf-8", "replace").strip())
    control = subprocess.run(["dotnet", str(WORK / "csharp-unchecked-control-bin/Unchecked.dll")], capture_output=True, check=False)
    if control.returncode == 0:
        failures += 1
        print("C# unchecked control: the program passes without the guard of a shared list.")
    for port, command in runs.items():
        output = subprocess.run(command, input=lines.encode(), capture_output=True, check=True).stdout.decode().splitlines()
        if len(output) != len(requests):
            raise SystemExit(f"{port}: {len(output)} results for {len(requests)} requests")
        for (case_id, _, operation, data, _, _, expected), observed in zip(requests, output):
            problem = verdict(expected, observed, data)
            if problem:
                failures += 1
                print(f"{port} {case_id} {operation}: {problem}")
    skipped_text = ", ".join(f"{count} {code}" for code, count in sorted(skipped.items()))
    print(f"{len(entries)} generated schemas ({sum(1 for entry in entries if entry[-1])} with models), {len(requests)} operations in each port: {failures} failures; skipped {skipped_text or 'none'}")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
