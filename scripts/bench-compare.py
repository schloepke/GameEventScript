#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Compare the steady state of one compiled program across the Swift CLI and four .NET configurations.

Builds the Swift CLI in Release, the .NET CLI in Release (run under the JIT with default tiering and with
DOTNET_TieredCompilation=0), a ReadyToRun publish and a NativeAOT publish for this machine, compiles the script once,
runs `bench --json` with every configuration on the same binary, and prints one table. Every configuration must report
the same checksum. Results and builds stay below artifacts/bench.

Usage: bench-compare.py [script.ges|program.gesb] [--warmup N] [--iterations N] [--min-time MS] [--skip-build]
"""
import argparse
import json
import os
from pathlib import Path
import platform
import subprocess
import sys

ROOT = Path(__file__).resolve().parent.parent
WORK = ROOT / "artifacts/bench"
TOOL = ROOT / "implementation/csharp/GameEventScript.Tool/src/GameEventScript.Tool.csproj"
SWIFT = WORK / "swift/release/ges"
JIT = ROOT / "artifacts/csharp/tool/bin/Release/net8.0/GameEventScript.Tool.dll"
R2R = WORK / "dotnet-r2r/GameEventScript.Tool.dll"
AOT = WORK / "dotnet-aot/GameEventScript.Tool"


def rid():
    system = {"Darwin": "osx", "Linux": "linux", "Windows": "win"}[platform.system()]
    machine = "arm64" if platform.machine().lower() in ("arm64", "aarch64") else "x64"
    return f"{system}-{machine}"


def run(arguments, quiet=False):
    result = subprocess.run([str(argument) for argument in arguments], cwd=ROOT, capture_output=quiet, text=True)
    if result.returncode != 0:
        raise SystemExit(f"Command failed: {' '.join(map(str, arguments))}\n{result.stdout or ''}{result.stderr or ''}")


def build():
    quiet = {"quiet": True}
    run(["swift", "build", "--package-path", "implementation/swift/GameEventScriptTool", "--scratch-path", WORK / "swift",
         "--disable-build-manifest-caching", "--configuration", "release"], **quiet)
    run(["dotnet", "build", TOOL, "--configuration", "Release"], **quiet)
    run(["dotnet", "publish", TOOL, "--configuration", "Release", "--runtime", rid(), "--self-contained", "false",
         "-p:PublishReadyToRun=true", "-p:PackAsTool=false", "--artifacts-path", WORK / "dotnet-r2r-build", "--output", R2R.parent], **quiet)
    run(["dotnet", "publish", TOOL, "--configuration", "Release", "--runtime", rid(), "-p:GesPublishAot=true", "-p:PackAsTool=false",
         "--artifacts-path", WORK / "dotnet-aot-build", "--output", AOT.parent], **quiet)


CONFIGURATIONS = (
    ("Swift (Release)", "swift", lambda: [str(SWIFT)], {}),
    (".NET JIT (default tiering)", "dotnet-jit", lambda: ["dotnet", str(JIT)], {}),
    (".NET JIT (TieredCompilation=0)", "dotnet-jit-tc0", lambda: ["dotnet", str(JIT)], {"DOTNET_TieredCompilation": "0"}),
    (".NET ReadyToRun", "dotnet-r2r", lambda: ["dotnet", str(R2R)], {}),
    (".NET NativeAOT", "dotnet-aot", lambda: [str(AOT)], {}),
)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("script", nargs="?", type=Path, default=ROOT / "examples/compute-benchmark/compute-benchmark.ges")
    parser.add_argument("--warmup", default="20")
    parser.add_argument("--iterations", default="50")
    parser.add_argument("--min-time", default="0")
    parser.add_argument("--skip-build", action="store_true", help="Reuse the builds of an earlier run")
    args = parser.parse_args()
    WORK.mkdir(parents=True, exist_ok=True)
    if not args.skip_build:
        build()
    program = args.script.resolve()
    if program.suffix != ".gesb":
        # One binary for every configuration, so all of them execute the same instructions.
        compiled = WORK / (program.stem + ".gesb")
        run(["dotnet", JIT, "compile", program, "-o", compiled, "-q"])
        program = compiled

    rows = []
    for name, slug, command, variables in CONFIGURATIONS:
        environment = {key: value for key, value in os.environ.items() if not key.startswith(("DOTNET_Tiered", "DOTNET_ReadyToRun"))}
        environment.update(variables)
        result = subprocess.run([*command(), "bench", str(program), "--warmup", args.warmup, "--iterations", args.iterations,
                                 "--min-time", args.min_time, "--json"], cwd=ROOT, env=environment, capture_output=True, text=True)
        if result.returncode != 0:
            raise SystemExit(f"{name} failed:\n{result.stderr}")
        report = json.loads(result.stdout)
        (WORK / f"{slug}.json").write_text(result.stdout)
        rows.append((name, report))

    checksums = {report["checksum"] for _, report in rows}
    if len(checksums) != 1:
        raise SystemExit("The configurations compute different checksums: " + ", ".join(f"{name} {report['checksum']}" for name, report in rows))
    first = rows[0][1]
    print(f"Program: {program.relative_to(ROOT) if program.is_relative_to(ROOT) else program}, entry {first['entry']}, "
          f"{first['warmup']} warm-up and {first['iterations']} timed runs, {first['os']} {first['arch']}, checksum {first['checksum']}")
    print()
    header = ("Configuration", "Runtime", "Load ms", "Median ms", "p95 ms", "Alloc/iter")
    table = [header] + [(name, f"{report['runtime']} {report['runtimeVersion']}", f"{report['loadMs']:.3f}", f"{report['medianMs']:.3f}",
                         f"{report['p95Ms']:.3f}",
                         "n/a" if report["allocatedBytesPerIteration"] is None else f"{report['allocatedBytesPerIteration']:,} B")
                        for name, report in rows]
    widths = [max(len(row[column]) for row in table) for column in range(len(header))]
    for index, row in enumerate(table):
        print("| " + " | ".join(cell.ljust(widths[column]) if column < 2 else cell.rjust(widths[column]) for column, cell in enumerate(row)) + " |")
        if index == 0:
            print("|" + "|".join("-" * (width + 2) for width in widths) + "|")


if __name__ == "__main__":
    sys.exit(main())
