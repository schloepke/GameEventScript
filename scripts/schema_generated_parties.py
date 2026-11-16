# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Generated schema code as the third and fourth party of the schema exchange.

Both CLIs generate C# and Swift code for the exchange's schemas. A C# and a Swift executable compile that code, and each
request runs through the generated type and through the interpreted path of its own port (`GesSchemaValues` and
`GesSchemaDelta`): an executable prints the generated result, or `mismatch` when the two paths differ in bytes, byte
counts, error codes, paths or, through `--values`, decoded values. The caller compares these lines with its oracle and the
two ports with each other. The Swift classes keep their exclusivity checks, so an overlapping access in generated code traps.
"""
from pathlib import Path
import shutil
import subprocess

ROOT = Path(__file__).resolve().parents[1]
CSHARP_CLI = ROOT / 'artifacts/csharp/tool/bin/Release/net8.0/GameEventScript.Tool.dll'
SWIFT_CLI = ROOT / 'artifacts/swift/tool/debug/ges'


def _generate(command, source, root, language, space):
    run = subprocess.run([*command, 'schema', 'generate', source.name, '--root', root, '--language', language, '--namespace', space, '--values', '--checked-exclusivity'],
                         cwd=source.parent, capture_output=True, check=False)
    if run.returncode != 0:
        raise SystemExit(f'generate {root} {language}: {run.stderr.decode("utf-8", "replace")}')
    return run.stdout.decode('utf-8')


def _csharp_program(schemas):
    cases = []
    for index, (root, _) in enumerate(schemas):
        name = f'Gen.S{index}.{root}'
        cases.append(f'''            case "{root}":
            {{
                var bound = Bound[{index}];
                if (operation == "decode")
                {{
                    var record = new {name}();
                    string generated;
                    var bytes = "";
                    try
                    {{
                        generated = "ok " + record.Read(first, limit);
                        bytes = " " + Hex(record.ToBytes());
                    }}
                    catch (GesWireException error) {{ generated = Error(error); }}
                    string interpreted;
                    try
                    {{
                        var decoded = GesSchemaValues.Decode(bound, first, limit);
                        interpreted = "ok " + decoded.ByteCount;
                        if (generated == interpreted && !record.ToValue().Equals(decoded.Value)) interpreted = "another value";
                    }}
                    catch (GesWireException error) {{ interpreted = Error(error); }}
                    return generated == interpreted ? generated + bytes : "mismatch " + generated + " | " + interpreted;
                }}

                var baseline = new {name}();
                baseline.Read(second);
                var buffer = new byte[bound.MaxByteCount];
                if (operation == "delta")
                {{
                    var record = new {name}();
                    string generated;
                    try {{ generated = "ok " + record.ReadDelta(first, baseline, limit) + " " + Hex(record.ToBytes()); }} catch (GesWireException error) {{ generated = Error(error); }}
                    string interpreted;
                    try
                    {{
                        var decoded = GesSchemaDelta.Decode(bound, first, second, buffer, limit);
                        interpreted = "ok " + decoded.DeltaByteCount + " " + Hex(buffer.AsSpan(0, decoded.ByteCount));
                    }}
                    catch (GesWireException error) {{ interpreted = Error(error); }}
                    return generated == interpreted ? generated : "mismatch " + generated + " | " + interpreted;
                }}

                var source = new {name}();
                source.Read(first);
                var delta = Hex(source.ToDeltaBytes(baseline));
                var reference = Hex(buffer.AsSpan(0, GesSchemaDelta.Encode(bound, first, second, buffer)));
                return delta == reference ? "ok " + delta : "mismatch " + delta + " | " + reference;
            }}''')
    sources = ', '.join(f'"{path}"' for _, path in schemas)
    roots = ', '.join(f'"{root}"' for root, _ in schemas)
    return f'''using System;
using System.IO;
using GameEventScript.Api;

internal static class Harness
{{
    private static readonly string[] Sources = {{ {sources} }};
    private static readonly string[] Roots = {{ {roots} }};
    private static readonly GesBoundSchema[] Bound = Array.ConvertAll(Roots, root => GesSchemaSet.Parse(File.ReadAllText(Sources[Array.IndexOf(Roots, root)])).Schema(root).Bind(new GesSchemaParameters()));

    private static string Hex(ReadOnlySpan<byte> bytes) => Convert.ToHexString(bytes).ToLowerInvariant();

    private static string Error(GesWireException error) => "error " + error.Code + " " + error.Path;

    private static string Run(string root, string operation, byte[] first, byte[] second, long limit)
    {{
        switch (root)
        {{
{chr(10).join(cases)}
            default: throw new ArgumentException(root);
        }}
    }}

    private static byte[] Bytes(string text) => Convert.FromHexString(text == "." ? "" : text);

    private static void Main()
    {{
        string? line;
        while ((line = Console.ReadLine()) != null)
        {{
            var parts = line.Split(' ');
            Console.WriteLine(Run(parts[0], parts[1], Bytes(parts[2]), Bytes(parts[3]), parts[4] == "-" ? long.MaxValue : long.Parse(parts[4])));
        }}
    }}
}}
'''


def _swift_program(schemas, classes):
    cases = []
    for index, (root, _) in enumerate(schemas):
        name = f'S{index}.{root}'
        binding = 'let' if classes[root] else 'var'
        cases.append(f'''    case "{root}":
        let bound = boundSchemas[{index}]
        if operation == "decode" {{
            {binding} record = {name}()
            var generated: String
            var bytes = ""
            do {{
                generated = "ok \\(try record.read(from: first, byteLimit: limit))"
                bytes = " " + hex(try record.encoded())
            }} catch {{ generated = failure(error) }}
            var interpreted: String
            do {{
                let decoded = try GesSchemaValues.decode(bound, first, byteLimit: limit)
                interpreted = "ok \\(decoded.byteCount)"
                if generated == interpreted, try record.toValue() != decoded.value {{ interpreted = "another value" }}
            }} catch {{ interpreted = failure(error) }}
            return generated == interpreted ? generated + bytes : "mismatch " + generated + " | " + interpreted
        }}
        {binding} baseline = {name}()
        try baseline.read(from: second)
        if operation == "delta" {{
            {binding} record = {name}()
            var generated: String
            do {{ generated = "ok \\(try record.readDelta(from: first, against: baseline, byteLimit: limit)) " + hex(try record.encoded()) }} catch {{ generated = failure(error) }}
            var interpreted: String
            do {{
                let target = UnsafeMutableRawBufferPointer.allocate(byteCount: max(bound.maxByteCount, 1), alignment: 8)
                defer {{ target.deallocate() }}
                let decoded = try first.withUnsafeBytes {{ delta in try second.withUnsafeBytes {{ held in try GesSchemaDelta.decode(bound, delta: delta, baseline: held, into: target, byteLimit: limit) }} }}
                interpreted = "ok \\(decoded.deltaByteCount) " + hex(Array(target[0..<decoded.byteCount]))
            }} catch {{ interpreted = failure(error) }}
            return generated == interpreted ? generated : "mismatch " + generated + " | " + interpreted
        }}
        {binding} source = {name}()
        try source.read(from: first)
        let bytes = hex(try source.deltaEncoded(against: baseline))
        let target = UnsafeMutableRawBufferPointer.allocate(byteCount: max(bound.maxByteCount, 1), alignment: 8)
        defer {{ target.deallocate() }}
        let count = try first.withUnsafeBytes {{ record in try second.withUnsafeBytes {{ held in try GesSchemaDelta.encode(bound, record: record, baseline: held, into: target) }} }}
        let reference = hex(Array(target[0..<count]))
        return bytes == reference ? "ok " + bytes : "mismatch " + bytes + " | " + reference''')
    sources = ', '.join(f'"{path}"' for _, path in schemas)
    roots = ', '.join(f'"{root}"' for root, _ in schemas)
    return f'''import Foundation
import GameEventScriptRuntime

let sourcePaths = [{sources}]
let boundSchemas = try zip(sourcePaths, [{roots}]).map {{ path, root in
    try GesSchemaSet(parsing: String(contentsOfFile: path, encoding: .utf8)).schema(root).bind()
}}

func hex(_ bytes: [UInt8]) -> String {{ bytes.map {{ ($0 < 16 ? "0" : "") + String($0, radix: 16) }}.joined() }}

func failure(_ error: any Error) -> String {{
    guard let error = error as? GesWireError else {{ return "unexpected \\(error)" }}
    return "error \\(error.code) \\(error.path)"
}}

func bytes(_ text: Substring) -> [UInt8] {{
    if text == "." {{ return [] }}
    var result: [UInt8] = []
    var index = text.startIndex
    while index < text.endIndex {{
        let next = text.index(index, offsetBy: 2)
        result.append(UInt8(text[index..<next], radix: 16)!)
        index = next
    }}
    return result
}}

func run(_ root: Substring, _ operation: Substring, _ first: [UInt8], _ second: [UInt8], _ limit: Int) throws -> String {{
    switch root {{
{chr(10).join(cases)}
    default:
        fatalError(String(root))
    }}
}}

while let line = readLine() {{
    let parts = line.split(separator: " ", omittingEmptySubsequences: false)
    print(try run(parts[0], parts[1], bytes(parts[2]), bytes(parts[3]), parts[4] == "-" ? Int.max : Int(parts[4])!))
}}
'''


def build(work, schemas):
    """Generates the schemas with both CLIs, checks that they write identical text and builds both executables. `schemas`
    lists (root, source text); returns the commands of the C# and the Swift executable."""
    if work.exists():
        shutil.rmtree(work)
    csharp, swift = work / 'csharp', work / 'swift/Sources/Generated'
    csharp.mkdir(parents=True)
    swift.mkdir(parents=True)
    listed = []
    classes = {}
    for index, (root, text) in enumerate(schemas):
        source = work / f'S{index}.gess'
        source.write_text(text, encoding='utf-8')
        listed.append((root, str(source)))
        for language, target, space in (('csharp', csharp / f'S{index}.g.cs', f'Gen.S{index}'), ('swift', swift / f'S{index}.swift', f'S{index}')):
            code = _generate(['dotnet', str(CSHARP_CLI)], source, root, language, space)
            if code != _generate([str(SWIFT_CLI)], source, root, language, space):
                raise SystemExit(f'The CLIs generate different {language} code for {root}.')
            target.write_text(code, encoding='utf-8')
            if language == 'swift':
                classes[root] = f'final class {root} {{' in code
    (csharp / 'Harness.cs').write_text(_csharp_program(listed), encoding='utf-8')
    (csharp / 'Harness.csproj').write_text(f'''<Project Sdk="Microsoft.NET.Sdk">
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
''', encoding='utf-8')
    (swift / 'main.swift').write_text(_swift_program(listed, classes), encoding='utf-8')
    (work / 'swift/Package.swift').write_text(f'''// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "Generated",
    platforms: [.macOS("10.15.4")],
    dependencies: [.package(path: "{ROOT / 'implementation/swift/GameEventScriptRuntime'}")],
    targets: [
        .executableTarget(
            name: "Generated",
            dependencies: [.product(name: "GameEventScriptRuntime", package: "GameEventScriptRuntime")],
            swiftSettings: [.unsafeFlags(["-warnings-as-errors"])]
        )
    ]
)
''', encoding='utf-8')
    for command in (['dotnet', 'build', str(csharp), '-c', 'Release', '-o', str(work / 'csharp-bin'), '-nologo'],
                    ['swift', 'build', '-c', 'release', '--package-path', str(work / 'swift'), '--disable-build-manifest-caching']):
        result = subprocess.run(command, capture_output=True, check=False)
        if result.returncode != 0:
            lines = result.stdout.decode('utf-8', 'replace').splitlines() + result.stderr.decode('utf-8', 'replace').splitlines()
            raise SystemExit('\n'.join(dict.fromkeys(line for line in lines if 'error' in line)) or f'{command[0]} build failed')
    return {'C#': ['dotnet', str(work / 'csharp-bin/Harness.dll')], 'Swift': [str(work / 'swift/.build/release/Generated')]}


def run(commands, requests):
    """Runs every request, a tuple (root, operation, first, second, limit), in both executables; returns the lines per port."""
    text = ''.join(f'{root} {operation} {first or "."} {second or "."} {"-" if limit is None else limit}\n' for root, operation, first, second, limit in requests)
    results = {}
    for port, command in commands.items():
        output = subprocess.run(command, input=text.encode(), capture_output=True, check=True, timeout=600).stdout.decode().splitlines()
        if len(output) != len(requests):
            raise SystemExit(f'{port}: {len(output)} results for {len(requests)} requests')
        results[port] = output
    return results
