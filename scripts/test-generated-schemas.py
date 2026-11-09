#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Check generated schema code against the shared corpus in C# and Swift.

The schemaApi cases with literal parameters name a root and give bytes to decode, bytes a value encodes to, a key, or a
delta to encode or decode. For every such schema the C# CLI generates C# and Swift, which compile into one C# program
(C# 9, warnings as errors) and one Swift executable (Swift 6 language mode, warnings as errors). Each program decodes the
given bytes with the generated type and writes the record again: an expected error must have the same code and path, a
valid record the same byte count and the same bytes. A delta is encoded from the decoded record and baseline, and decoded
against the baseline into the expected record. Operations on types without delta methods are counted as skipped.
--self-test qualifies the comparison.
"""
import argparse
from pathlib import Path
import re
import shutil
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "conformance/runner"))
from core.corpus import read_corpus  # noqa: E402

CSHARP_CLI = ROOT / "artifacts/csharp/tool/bin/Release/net8.0/GameEventScript.Tool.dll"
WORK = ROOT / "artifacts/generated-schemas"
CODE = re.compile(r"error ([a-z]+\.[A-Za-z]+)")


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
                operations.append(("decode", api["decode"], "", limit, ("ok", size if size is not None else len(api["decode"]) // 2)))
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


def generate(directory, sources, root, parameters, language, space):
    files = []
    for number, text in enumerate(sources):
        path = directory / f"s{number}.gess"
        path.write_text(text, encoding="utf-8")
        files.append(path.name)
    options = [argument for name, value in parameters for argument in ("--param", f"{name}={value}")]
    run = subprocess.run(["dotnet", str(CSHARP_CLI), "schema", "generate", *files, "--root", root, *options, "--language", language, "--namespace", space],
                         cwd=directory, capture_output=True, check=False)
    if run.returncode != 0:
        match = CODE.search(run.stderr.decode("utf-8", "replace"))
        return None, match.group(1) if match else run.stderr.decode("utf-8", "replace").strip()
    return run.stdout.decode("utf-8"), None


def declares(code, root, member):
    """Whether the generated C# root type declares a member, within its own declaration."""
    start = code.find(f" {root}\n    {{")
    end = code.find("\n    }\n", start)
    return start >= 0 and member in code[start:end]


def has_delta(code, root):
    return declares(code, root, "public int ReadDelta(")


def csharp_program(entries):
    lines = ["using System;", "", "internal static class Harness", "{",
             "    private static string Hex(ReadOnlySpan<byte> bytes) { var text = new System.Text.StringBuilder(); foreach (var b in bytes) text.Append(b.ToString(\"x2\")); return text.ToString(); }",
             "    private static string Run(int schema, string operation, byte[] data, byte[] second, long limit)", "    {", "        switch (schema)", "        {"]
    for index, root, key, _, delta in entries:
        name = f"Gen.Case{index}.{root}"
        lines += [f"            case {index}:", "            {"]
        lines += [f"                if (operation == \"key\") return \"ok \" + ((long){name}.ReadKey(data)).ToString();" if key else "                if (operation == \"key\") return \"none\";"]
        lines += [f"                var record = new {name}();"]
        if delta:
            lines += ["                if (operation != \"decode\")", "                {", f"                    var baseline = new {name}();", "                    baseline.Read(second);",
                      "                    if (operation == \"deltaDecode\")", "                    {", "                        var used = record.ReadDelta(data, baseline, limit);",
                      "                        return \"ok \" + used + \" \" + Hex(record.ToBytes());", "                    }", "                    record.Read(data);",
                      "                    return \"ok \" + Hex(record.ToDeltaBytes(baseline));", "                }"]
        else:
            lines += ["                if (operation != \"decode\") return \"none\";"]
        lines += ["                var count = record.Read(data, limit);", "                return \"ok \" + count + \" \" + Hex(record.ToBytes());", "            }"]
    lines += ["            default: return \"none\";", "        }", "    }", "",
              "    private static byte[] Bytes(string text) => Convert.FromHexString(text == \".\" ? \"\" : text);", "",
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
             "func run(_ schema: Int, _ operation: Substring, _ data: [UInt8], _ second: [UInt8], _ limit: Int) throws(GesWireError) -> String {", "    switch schema {"]
    for index, root, key, is_class, delta in entries:
        binding = "let" if is_class else "var"
        lines += [f"    case {index}:"]
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
        lines += ["        let count = try record.read(from: data, byteLimit: limit)", "        return \"ok \\(count) \" + hex(try record.encoded())"]
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
    if expected[0] == "record":
        return None if parts == ["ok", str(expected[2]), expected[1]] else f"expected record {expected[1]} after {expected[2]} bytes, got {observed!r}"
    if parts[0] != "ok" or int(parts[1]) != expected[1]:
        return f"expected {expected[1]} bytes, got {observed!r}"
    return None if parts[2] == data[:2 * expected[1]] else f"wrote {parts[2]} instead of {data[:2 * expected[1]]}"


def self_test():
    assert verdict(("error", "wire.truncated", "x"), "error wire.truncated x", "") is None
    assert verdict(("error", "wire.truncated", "x"), "error wire.truncated y", "")
    assert verdict(("error", "wire.truncated", ""), "error wire.truncated ", "") is None
    assert verdict(("error", "wire.truncated", None), "error wire.truncated x", "") is None
    assert verdict(("error", "wire.truncated", None), "error wire.invalidIndex x", "")
    assert verdict(("ok", 2), "ok 2 0a0b", "0a0b0c") is None
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
        (csharp / f"Case{index}.g.cs").write_text(code, encoding="utf-8")
        (swift / f"Case{index}.swift").write_text(swift_code, encoding="utf-8")
        delta = has_delta(code, root)
        entries.append((index, root, declares(code, root, " ReadKey("), f"final class {root} {{" in swift_code, delta))
        for case_id, operation, data, second, limit, expected in operations:
            if operation.startswith("delta") and not delta:
                skipped["no delta methods"] = skipped.get("no delta methods", 0) + 1
                continue
            requests.append((case_id, index, operation, data.lower(), second.lower(), limit, expected))
    (csharp / "Harness.cs").write_text(csharp_program(entries), encoding="utf-8")
    (csharp / "Harness.csproj").write_text(f"""<Project Sdk="Microsoft.NET.Sdk">
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
            swiftSettings: [.unsafeFlags(["-warnings-as-errors"])]
        )
    ]
)
""", encoding="utf-8")
    for command in (["dotnet", "build", str(csharp), "-c", "Release", "-o", str(WORK / "csharp-bin"), "-nologo"],
                    ["swift", "build", "-c", "release", "--package-path", str(WORK / "swift"), "--disable-build-manifest-caching"]):
        build = subprocess.run(command, capture_output=True, check=False)
        if build.returncode != 0:
            errors = [line for line in build.stdout.decode("utf-8", "replace").splitlines() + build.stderr.decode("utf-8", "replace").splitlines() if "error" in line]
            raise SystemExit("\n".join(dict.fromkeys(errors[:40])) or f"{command[0]} build failed")
    lines = "".join(f"{index} {operation} {data or '.'} {second or '.'} {'-' if limit is None else limit}\n" for _, index, operation, data, second, limit, _ in requests)
    runs = {"C#": ["dotnet", str(WORK / "csharp-bin/Harness.dll")], "Swift": [str(WORK / "swift/.build/release/Harness")]}
    failures = 0
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
    print(f"{len(entries)} generated schemas, {len(requests)} operations in each port: {failures} failures; skipped {skipped_text or 'none'}")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
