#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Measure generated code for a List of packed records in C# and Swift.

The schema is a map track of 1, 4 and 16 million packed points. For each count both ports generate models and wire code
with `--models`, then fill a model, copy it into the wire record, encode, decode and convert back, and report the median
of several runs per step. The decoded model must equal the original, so the benchmark checks itself. A point takes 26
bits, so 16 million fit the 64 MiB largest encoding (specs/Schemas.md#largest-encoding).

Usage: bench-packed-schemas.py [--counts 1000000,4000000,16000000] [--runs 5] [--json]
"""
import argparse
import json
from pathlib import Path
import shutil
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
CSHARP_CLI = ROOT / "artifacts/csharp/tool/bin/Release/net8.0/GameEventScript.Tool.dll"
WORK = ROOT / "artifacts/bench-packed-schemas"
SCHEMA = """wire(map) model(map) packed schema :MapPoint as {
  height: :Number integer between 0 and 4095
  surface: :Number integer between 0 and 9
  kind: :Tag(#rock, #sand, #water)
  wear: :Number integer between 0 and 15
  flags: :Flags(#impassable, #blocksLineOfSight, #indestructible, #reserved)
}

wire(map) model(map) schema :MapTrack as { points: :List(:MapPoint) max 16000000 }
"""
STEPS = ["copyFromModel", "encode", "decode", "toModel"]

CSHARP_PROGRAM = """using System;
using System.Collections.Generic;
using System.Diagnostics;
using Bench.Model;

var count = int.Parse(args[0]);
var runs = int.Parse(args[1]);
var points = new List<MapPoint>(count);
for (var index = 0; index < count; index++) points.Add(new MapPoint(index % 4096, index % 10, (MapPoint.KindTag)(index % 3), index % 16, (MapPoint.FlagsFlags)(index % 16)));
var model = new MapTrack { Points = points };
var times = new List<double>[] { new(), new(), new(), new() };
var bytes = 0;
var equal = true;
for (var run = 0; run < runs + 1; run++)
{
    var clock = Stopwatch.StartNew();
    var wire = new Bench.Wire.MapTrack();
    wire.CopyFrom(model);
    var copy = clock.Elapsed.TotalMilliseconds;
    clock.Restart();
    var data = wire.ToBytes();
    var encode = clock.Elapsed.TotalMilliseconds;
    clock.Restart();
    var back = new Bench.Wire.MapTrack();
    back.Read(data);
    var decode = clock.Elapsed.TotalMilliseconds;
    clock.Restart();
    var result = back.ToModel();
    var convert = clock.Elapsed.TotalMilliseconds;
    bytes = data.Length;
    equal &= result.Equals(model);
    if (run == 0) continue; // warm-up
    times[0].Add(copy);
    times[1].Add(encode);
    times[2].Add(decode);
    times[3].Add(convert);
}
static string Median(List<double> values) { values.Sort(); return values[values.Count / 2].ToString("F3", System.Globalization.CultureInfo.InvariantCulture); }
Console.WriteLine($"{{\\"bytes\\":{bytes},\\"equal\\":{(equal ? "true" : "false")},\\"copyFromModel\\":{Median(times[0])},\\"encode\\":{Median(times[1])},\\"decode\\":{Median(times[2])},\\"toModel\\":{Median(times[3])}}}");
"""

SWIFT_PROGRAM = """import GameEventScriptRuntime

let count = Int(CommandLine.arguments[1])!
let runs = Int(CommandLine.arguments[2])!
var points: [BenchModel.MapPoint] = []
points.reserveCapacity(count)
for index in 0..<count { points.append(BenchModel.MapPoint(height: index % 4096, surface: index % 10, kind: BenchModel.MapPoint.KindTag.allCases[index % 3], wear: index % 16, flags: BenchModel.MapPoint.FlagsFlags(rawValue: UInt64(index % 16)))) }
let model = BenchModel.MapTrack(points: points)
var times: [[Double]] = [[], [], [], []]
var bytes = 0
var equal = true

func milliseconds(_ start: UInt64) -> Double { Double(DispatchTimeLike.now() - start) / 1_000_000 }

enum DispatchTimeLike {
    static func now() -> UInt64 {
        var time = timespec()
        clock_gettime(CLOCK_MONOTONIC_RAW, &time)
        return UInt64(time.tv_sec) * 1_000_000_000 + UInt64(time.tv_nsec)
    }
}

for run in 0..<(runs + 1) {
    var start = DispatchTimeLike.now()
    let wire = BenchWire.MapTrack()
    wire.read(model: model)
    let copy = milliseconds(start)
    start = DispatchTimeLike.now()
    let data = try wire.encoded()
    let encode = milliseconds(start)
    start = DispatchTimeLike.now()
    let back = BenchWire.MapTrack()
    try back.read(from: data)
    let decode = milliseconds(start)
    start = DispatchTimeLike.now()
    let result = BenchModel.MapTrack(wire: back)
    let convert = milliseconds(start)
    bytes = data.count
    equal = equal && result == model
    if run == 0 { continue }
    times[0].append(copy)
    times[1].append(encode)
    times[2].append(decode)
    times[3].append(convert)
}

func median(_ values: [Double]) -> Double { values.sorted()[values.count / 2] }
func text(_ value: Double) -> String { String(Double(Int(value * 1000)) / 1000) }
print("{\\"bytes\\":\\(bytes),\\"equal\\":\\(equal),\\"copyFromModel\\":\\(text(median(times[0]))),\\"encode\\":\\(text(median(times[1]))),\\"decode\\":\\(text(median(times[2]))),\\"toModel\\":\\(text(median(times[3])))}")
"""


def run(command, cwd=None):
    result = subprocess.run(command, cwd=cwd, capture_output=True, text=True, check=False)
    if result.returncode != 0:
        lines = [line for line in (result.stdout + result.stderr).splitlines() if "error" in line.lower()]
        raise SystemExit("\n".join(lines[:20]) or f"{command[0]} failed")
    return result.stdout


def generate(language, arguments, target):
    run(["dotnet", str(CSHARP_CLI), "schema", "generate", "map.gess", "--language", language, *arguments, "-o", str(target)], cwd=WORK)


def prepare():
    if not CSHARP_CLI.exists():
        run(["dotnet", "build", str(ROOT / "implementation/csharp/GameEventScript.Tool/src"), "-c", "Release", "-nologo"])
    if WORK.exists():
        shutil.rmtree(WORK)
    csharp, swift = WORK / "csharp", WORK / "swift/Sources/Bench"
    csharp.mkdir(parents=True)
    swift.mkdir(parents=True)
    (WORK / "map.gess").write_text(SCHEMA, encoding="utf-8")
    generate("csharp", ["--model", "map", "--namespace", "Bench.Model"], csharp / "Model.g.cs")
    generate("csharp", ["--wire", "map", "--namespace", "Bench.Wire", "--models", "Bench.Model"], csharp / "Wire.g.cs")
    generate("swift", ["--model", "map", "--namespace", "BenchModel"], swift / "Model.swift")
    generate("swift", ["--wire", "map", "--namespace", "BenchWire", "--models", "BenchModel"], swift / "Wire.swift")
    (csharp / "Program.cs").write_text(CSHARP_PROGRAM, encoding="utf-8")
    (csharp / "Bench.csproj").write_text(f"""<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ServerGarbageCollection>false</ServerGarbageCollection>
    <TieredPGO>true</TieredPGO>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="{ROOT / 'implementation/csharp/GameEventScript.Runtime/src/GameEventScript.Runtime.csproj'}" />
  </ItemGroup>
</Project>
""", encoding="utf-8")
    (swift / "main.swift").write_text(SWIFT_PROGRAM.replace("import GameEventScriptRuntime", "#if canImport(Glibc)\nimport Glibc\n#else\nimport Darwin\n#endif\nimport GameEventScriptRuntime"), encoding="utf-8")
    (WORK / "swift/Package.swift").write_text(f"""// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "Bench",
    platforms: [.macOS("10.15.4")],
    dependencies: [.package(path: "{ROOT / 'implementation/swift/GameEventScriptRuntime'}")],
    targets: [.executableTarget(name: "Bench", dependencies: [.product(name: "GameEventScriptRuntime", package: "GameEventScriptRuntime")])]
)
""", encoding="utf-8")
    run(["dotnet", "build", str(csharp), "-c", "Release", "-o", str(WORK / "csharp-bin"), "-nologo"])
    run(["swift", "build", "-c", "release", "--package-path", str(WORK / "swift"), "--disable-build-manifest-caching"])


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--counts", default="1000000,4000000,16000000", help="comma-separated point counts, at most 16000000")
    parser.add_argument("--runs", type=int, default=5, help="measured runs per count after one warm-up")
    parser.add_argument("--json", action="store_true", help="print one JSON object instead of a table")
    args = parser.parse_args()
    counts = [int(count) for count in args.counts.split(",")]
    if any(count < 1 or count > 16000000 for count in counts) or args.runs < 1:
        raise SystemExit("Counts lie between 1 and 16000000, and runs are positive.")
    prepare()
    ports = {"C#": ["dotnet", str(WORK / "csharp-bin/Bench.dll")], "Swift": [str(WORK / "swift/.build/release/Bench")]}
    results = []
    for count in counts:
        for port, command in ports.items():
            observed = json.loads(run([*command, str(count), str(args.runs)]).strip().splitlines()[-1])
            if not observed["equal"]:
                raise SystemExit(f"{port}: the decoded model of {count} points differs from the original.")
            results.append({"port": port, "count": count, **observed})
    if args.json:
        print(json.dumps({"runs": args.runs, "results": results}, indent=2))
        return 0
    print(f"Median milliseconds of {args.runs} runs; a point takes 26 bits on the wire and one 32-bit word in memory.")
    print(f"{'port':<6} {'points':>9} {'bytes':>10} " + " ".join(f"{step:>14}" for step in STEPS))
    for result in results:
        print(f"{result['port']:<6} {result['count']:>9} {result['bytes']:>10} " + " ".join(f"{result[step]:>14.3f}" for step in STEPS))
    return 0


if __name__ == "__main__":
    sys.exit(main())
