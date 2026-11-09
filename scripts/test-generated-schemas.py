#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Check generated schema code against the shared corpus in C# and Swift.

The schemaApi cases with literal parameters name a root and give bytes to decode, bytes a value encodes to, or a key. For
every such schema the C# CLI generates C# and Swift, which compile into one C# program (C# 9, warnings as errors) and one
Swift executable (Swift 6 language mode, warnings as errors). Each program decodes the given bytes with the generated type
and writes the record again: an expected error must have the same code and path, a valid record the same byte count and
the same bytes. Types that cannot be generated yet are counted as skipped. --self-test qualifies the comparison.
"""
import argparse
import os
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


def cases(paths):
    """Schemas with their operations: (sources, root, parameters) -> list of (case id, operation, hex, limit, expected)."""
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
                operations.append(("decode", api["decode"], limit, ("error", decoded["error"]["code"], decoded["error"].get("path"))))
            elif "value" in decoded:
                size = decoded.get("byteCount")
                operations.append(("decode", api["decode"], limit, ("ok", size if size is not None else len(api["decode"]) // 2)))
        encoded = expected.get("encoded")
        if isinstance(encoded, dict) and isinstance(encoded.get("bytes"), str) and "baseline" not in api:
            operations.append(("decode", encoded["bytes"], None, ("ok", len(encoded["bytes"]) // 2)))
        if isinstance(api.get("key"), str) and isinstance(expected.get("key"), int):
            operations.append(("key", api["key"], None, ("key", expected["key"])))
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


def csharp_program(entries):
    lines = ["using System;", "using System.Collections.Generic;", "", "internal static class Harness", "{",
             "    private static string Hex(ReadOnlySpan<byte> bytes) { var text = new System.Text.StringBuilder(); foreach (var b in bytes) text.Append(b.ToString(\"x2\")); return text.ToString(); }",
             "    private static string Run(int schema, string operation, byte[] data, long limit)", "    {", "        switch (schema)", "        {"]
    for index, root, has_key in entries:
        lines += [f"            case {index}:", "            {", f"                if (operation == \"key\") return {('\"ok \" + ((long)Gen.Case' + str(index) + '.' + root + '.ReadKey(data)).ToString()') if has_key else '\"none\"'};",
                  f"                var record = new Gen.Case{index}.{root}();", "                var count = record.Read(data, limit);",
                  "                return \"ok \" + count + \" \" + Hex(record.ToBytes());", "            }"]
    lines += ["            default: return \"none\";", "        }", "    }", "",
              "    private static void Main()", "    {", "        string? line;", "        while ((line = Console.ReadLine()) != null)", "        {",
              "            var parts = line.Split(' ');", "            var data = Convert.FromHexString(parts[2]);",
              "            var limit = parts[3] == \"-\" ? long.MaxValue : long.Parse(parts[3]);",
              "            try { Console.WriteLine(Run(int.Parse(parts[0]), parts[1], data, limit)); }",
              "            catch (GameEventScript.Api.GesWireException error) { Console.WriteLine(\"error \" + error.Code + \" \" + error.Path); }", "        }", "    }", "}"]
    return "\n".join(lines) + "\n"


def swift_program(entries):
    lines = ["import GameEventScriptRuntime", "",
             "func hex(_ bytes: [UInt8]) -> String { bytes.map { ($0 < 16 ? \"0\" : \"\") + String($0, radix: 16) }.joined() }", "",
             "func bytes(_ text: Substring) -> [UInt8] {", "    var result: [UInt8] = []", "    var index = text.startIndex",
             "    while index < text.endIndex {", "        let next = text.index(index, offsetBy: 2)", "        result.append(UInt8(text[index..<next], radix: 16)!)",
             "        index = next", "    }", "    return result", "}", "",
             "func run(_ schema: Int, _ operation: Substring, _ data: [UInt8], _ limit: Int) throws(GesWireError) -> String {", "    switch schema {"]
    for index, root, has_key in entries:
        lines += [f"    case {index}:"]
        if has_key:
            lines += ["        if operation == \"key\" {", "            let buffer = UnsafeMutableRawBufferPointer.allocate(byteCount: max(data.count, 1), alignment: 8)",
                      "            defer { buffer.deallocate() }", "            buffer.copyBytes(from: data)",
                      f"            return \"ok \" + String(Int64(try Case{index}.{root}.readKey(from: UnsafeRawBufferPointer(rebasing: buffer[0..<data.count]))))", "        }"]
        else:
            lines += ["        if operation == \"key\" { return \"none\" }"]
        lines += [f"        var record = Case{index}.{root}()", "        let count = try record.read(from: data, byteLimit: limit)",
                  "        return \"ok \\(count) \" + hex(try record.encoded())"]
    lines += ["    default:", "        return \"none\"", "    }", "}", "",
              "while let line = readLine() {", "    let parts = line.split(separator: \" \", omittingEmptySubsequences: false)",
              "    let limit = parts[3] == \"-\" ? Int.max : Int(parts[3])!", "    do {",
              "        print(try run(Int(parts[0])!, parts[1], bytes(parts[2]), limit))", "    } catch {", "        print(\"error \\(error.code) \\(error.path)\")", "    }", "}"]
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
        entries.append((index, root, f"ReadKey(" in code))
        for case_id, operation, data, limit, expected in operations:
            requests.append((case_id, index, operation, data.lower(), limit, expected))
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
    subprocess.run(["dotnet", "build", str(csharp), "-c", "Release", "-o", str(WORK / "csharp-bin"), "-v", "quiet", "-nologo"], check=True)
    subprocess.run(["swift", "build", "-c", "release", "--package-path", str(WORK / "swift"), "--disable-build-manifest-caching"], check=True)
    lines = "".join(f"{index} {operation} {data} {'-' if limit is None else limit}\n" for _, index, operation, data, limit, _ in requests)
    runs = {"C#": ["dotnet", str(WORK / "csharp-bin/Harness.dll")], "Swift": [str(WORK / "swift/.build/release/Harness")]}
    failures = 0
    for port, command in runs.items():
        output = subprocess.run(command, input=lines.encode(), capture_output=True, check=True).stdout.decode().splitlines()
        if len(output) != len(requests):
            raise SystemExit(f"{port}: {len(output)} results for {len(requests)} requests")
        for (case_id, _, operation, data, _, expected), observed in zip(requests, output):
            problem = verdict(expected, observed, data)
            if problem:
                failures += 1
                print(f"{port} {case_id} {operation}: {problem}")
    skipped_text = ", ".join(f"{count} {code}" for code, count in sorted(skipped.items()))
    print(f"{len(entries)} generated schemas, {len(requests)} operations in each port: {failures} failures; skipped {skipped_text or 'none'}")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
