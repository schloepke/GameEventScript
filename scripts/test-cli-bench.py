#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Check the in-process benchmark mode of the CLIs against its shared contract.

Each given CLI measures a tiny compiled script with `bench --iterations 3 --warmup 1 --json` and must print one JSON
object with exactly the contract's members in order, the text form must carry the same values, and a failing run, a
missing entry and a source file must fail as the contract says. With both CLIs, their checksums must be equal.

Usage: test-cli-bench.py [--swift <ges>] [--csharp <dotnet-ges.dll>]
"""
import argparse
import json
from pathlib import Path
import subprocess
import tempfile

MEMBERS = ["runtime", "runtimeVersion", "os", "arch", "script", "entry", "freshInstance", "loadMs", "instanceMedianMs",
           "warmup", "iterations", "minMs", "medianMs", "meanMs", "p95Ms", "maxMs", "stddevMs", "allocatedBytesPerIteration",
           "gen0Collections", "gen1Collections", "gen2Collections", "tieredCompilation", "tieredPgo", "readyToRun", "checksum"]
SCRIPT = """module bench.probe
function square(_ x) be x * x
on Main(args) {
    let total be :Range(1, 200)[:fold acc be 0, item => acc + square(item)]
    emit ConsoleOut("total=", total, " text=", "Grüße")
}
on Fail() {
    emit ErrorCode(code: 3)
}
"""


def bench(command, work, *arguments, expected=0):
    result = subprocess.run([*command, "bench", *arguments], cwd=work, capture_output=True, text=True, encoding="utf-8", timeout=120)
    assert result.returncode == expected, (command, arguments, result.returncode, result.stdout, result.stderr)
    return result


def check(name, command, work):
    report = json.loads(bench(command, work, "probe.gesb", "--iterations", "3", "--warmup", "1", "--json").stdout)
    assert list(report) == MEMBERS, (name, list(report))
    assert report["script"] == "probe.gesb" and report["entry"] == "Main" and report["iterations"] == 3 and report["warmup"] == 1
    assert report["minMs"] <= report["medianMs"] <= report["p95Ms"] <= report["maxMs"], report
    assert report["instanceMedianMs"] is None and report["freshInstance"] is False
    assert len(report["checksum"]) == 16 and int(report["checksum"], 16) >= 0
    if report["runtime"] == "swift":
        assert all(report[member] is None for member in MEMBERS[17:24]), report
    else:
        assert report["runtime"] in ("dotnet-jit", "dotnet-aot") and isinstance(report["allocatedBytesPerIteration"], int)
        assert report["tieredCompilation"] is None if report["runtime"] == "dotnet-aot" else isinstance(report["tieredCompilation"], bool)

    detailed = json.loads(bench(command, work, "probe.gesb", "--iterations", "3", "--warmup", "0", "--json", "--per-iteration",
                                "--fresh-instance", "--gc-between").stdout)
    assert list(detailed) == MEMBERS + ["samplesMs"] and len(detailed["samplesMs"]) == 3, detailed
    assert detailed["freshInstance"] is True and detailed["instanceMedianMs"] is not None
    assert detailed["checksum"] == report["checksum"], "The checksum depends only on the timed runs' output."

    text = bench(command, work, "probe.gesb", "--iterations", "2", "--warmup", "1", "--per-iteration").stdout.splitlines()
    assert text[0] == "Benchmark: probe.gesb, entry Main" and text[1].startswith("Runtime: " + report["runtime"] + " "), text
    labels = [line.split(":")[0] for line in text]
    assert labels == ["Benchmark", "Runtime", "Tiering", "Load", "Warm-up", "Iterations", "Run 1", "Run 2", "Time per run (ms)",
                      "Allocated per run", "Checksum"], labels

    failure = bench(command, work, "probe.gesb", "--entry", "Fail", "--warmup", "0", "--iterations", "2", expected=1).stderr
    assert "error cli.benchFailed: iteration 1: the script set ErrorCode(3)" in failure, failure
    assert "error cli.missingEntry" in bench(command, work, "probe.gesb", "--entry", "Nowhere", expected=1).stderr
    # The host's system endpoints name no message a benchmark could send.
    for endpoint in ("initialization", "undeliverable"):
        assert "error cli.missingEntry" in bench(command, work, "probe.gesb", "--entry", endpoint, expected=1).stderr
    assert "error cli.usage" in bench(command, work, "probe.ges", expected=2).stderr
    return report["checksum"]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--swift", type=Path)
    parser.add_argument("--csharp", type=Path)
    args = parser.parse_args()
    commands = {}
    if args.swift:
        commands["swift"] = [str(args.swift.resolve())]
    if args.csharp:
        commands["csharp"] = ["dotnet", str(args.csharp.resolve())]
    if not commands:
        raise SystemExit("Give --swift, --csharp or both.")
    with tempfile.TemporaryDirectory() as directory:
        work = Path(directory)
        (work / "probe.ges").write_text(SCRIPT, encoding="utf-8")
        first = next(iter(commands.values()))
        subprocess.run([*first, "compile", "probe.ges", "-o", "probe.gesb", "-q"], cwd=work, check=True)
        checksums = {name: check(name, command, work) for name, command in commands.items()}
    if len(set(checksums.values())) != 1:
        raise SystemExit(f"The CLIs compute different checksums: {checksums}")
    print(f"bench follows its contract in {', '.join(checksums)}; checksum {next(iter(checksums.values()))}.")


if __name__ == "__main__":
    main()
