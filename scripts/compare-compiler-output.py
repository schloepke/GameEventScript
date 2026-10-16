#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Compile every source program of the shared corpus with the C# and Swift CLIs and compare the .gesb bytes.

Both compilers must produce byte-identical Programs for the same sources and debug options. Only the
BuildMetadata section differs by design, because it names the producing implementation; every other section
is compared byte for byte. For programs that both compilers reject, the ordered diagnostic lists must match in
source, start line and column, and code (specs/Diagnostics.md); both compilers report every diagnostic of a phase,
so the lists cover recovery and collection as well. A rejection counts only when the CLI exits with 1 and reports
compiler diagnostics; a crash, timeout, CLI error or silent failure is a process failure and always a difference.
A program that only one compiler accepts, that compiles to different bytes or sections in a different order, or
whose rejection reports different codes or positions, is a difference. Differences are ratcheted against
scripts/compiler-output-baseline.txt: a new or resolved difference fails until the baseline is regenerated
with --update-baseline. Reports and workspaces stay below artifacts/compiler-comparison.
"""

import argparse
from concurrent.futures import ThreadPoolExecutor
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import struct
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
WORK = ROOT / "artifacts" / "compiler-comparison"
SWIFT_CLI = ROOT / "artifacts/swift/tool/release/ges"
CSHARP_CLI = ROOT / "artifacts/csharp/tool/bin/Release/net8.0/GameEventScript.Tool.dll"
BASELINE = ROOT / "scripts" / "compiler-output-baseline.txt"
SECTION_NAMES = {0: "header", 0x01: "metadata", 0x02: "strings", 0x03: "indexLists", 0x04: "bindings", 0x10: "code",
                 0x20: "debugSymbols", 0x21: "sourceMap", 0x22: "sourceArchive"}


BUILD_METADATA = 0x0030


def sections(data):
    """Section type and raw bytes in file order; the header's FileSize is excluded, as it covers BuildMetadata."""
    if data[:4] != b"GESB" or len(data) < 16:
        raise ValueError("not a .gesb file")
    result = [(0, data[:12])]
    offset = struct.unpack_from("<I", data, 8)[0]
    while offset < len(data):
        kind, _, _, _, length = struct.unpack_from("<HHHHI", data, offset)
        result.append((kind, data[offset:offset + 12 + length]))
        offset += 12 + length
    return result


def comparable(data):
    """Every section except the implementation-specific BuildMetadata."""
    return [section for section in sections(data) if section[0] != BUILD_METADATA]


def differing_sections(left, right):
    """Names of the comparable sections whose bytes differ or that only one Program contains, plus sectionOrder
    when the sections both contain appear in a different order or a different number of times."""
    first, second = comparable(left), comparable(right)
    a, b = dict(first), dict(second)
    names = {SECTION_NAMES.get(kind, f"0x{kind:04x}") for kind in set(a) | set(b) if a.get(kind) != b.get(kind)}
    shared = set(a) & set(b)
    if [kind for kind, _ in first if kind in shared] != [kind for kind, _ in second if kind in shared]:
        names.add("sectionOrder")
    return sorted(names)


def programs(requests):
    """Unique (sources, debug) programs of the corpus, each with the first case ID that uses it."""
    unique = {}
    for case in requests["cases"]:
        groups = {}
        for source in case.get("sources", []):
            groups.setdefault(source.get("program", "main"), []).append((source["name"], source["text"]))
        debug = bool((case.get("compile") or {}).get("debugInfo", ["debugSymbols"]))
        for name, sources in groups.items():
            key = hashlib.sha256(json.dumps([sources, debug]).encode("utf-8")).hexdigest()
            unique.setdefault(key, {"case": case["id"], "program": name, "sources": sources, "debug": debug})
    return unique


# One CLI diagnostic line: source, optional (line,column), then "error <code>:"; messages are implementation text.
DIAGNOSTIC = re.compile(r"^(?P<source>.*?)(?:\((?P<line>\d+),(?P<column>\d+)\))?: error (?P<code>[a-z]+\.[A-Za-z0-9.]+):", re.M)


def diagnostics(stderr):
    """Ordered (source, line, column, code) of every reported diagnostic; message text is not compared."""
    return [(Path(m["source"]).name, m["line"], m["column"], m["code"]) for m in DIAGNOSTIC.finditer(stderr)]


COMPILER_PHASES = ("parse.", "validate.", "compile.")
TIMEOUT_SECONDS = 60


def outcome(returncode, output, found):
    """Classifies one compiler run: ("compiled", bytes), ("rejected", diagnostics) for a regular compiler rejection,
    or ("failed", reason) for anything else, such as a crash, a CLI error or a rejection without compiler
    diagnostics, so that two failing processes never count as agreeing rejections."""
    if returncode == 0:
        return ("compiled", output) if output is not None else ("failed", "exit 0 without output")
    if returncode == 1 and found and all(item[3].startswith(COMPILER_PHASES) for item in found):
        return "rejected", found
    codes = ",".join(item[3] for item in found) or "no compiler diagnostics"
    return "failed", f"exit {returncode}: {codes}"


def compile_with(command, directory, program, label):
    """Compile one program and classify the run (see outcome)."""
    output = directory / f"{label}.gesb"
    arguments = [*command, "compile", *[source for source, _ in program["sources"]], "-o", str(output), "-q"]
    if not program["debug"]:
        arguments.append("--no-debug")
    try:
        result = subprocess.run(arguments, cwd=directory, capture_output=True, check=False, timeout=TIMEOUT_SECONDS)
    except subprocess.TimeoutExpired:
        return "failed", f"timeout after {TIMEOUT_SECONDS}s"
    found = diagnostics(result.stderr.decode("utf-8", errors="replace"))
    return outcome(result.returncode, output.read_bytes() if output.exists() else None, found)


def compare(key, program, csharp, swift):
    directory = WORK / "programs" / key[:16]
    if directory.exists():
        shutil.rmtree(directory)
    directory.mkdir(parents=True)
    for name, text in program["sources"]:
        (directory / name).write_text(text, encoding="utf-8")
    (left_kind, left), (right_kind, right) = compile_with(csharp, directory, program, "csharp"), compile_with(swift, directory, program, "swift")
    verdict = run_verdict(left_kind, left, right_kind, right)
    if verdict in ("identical", "both-rejected"):
        shutil.rmtree(directory)
    shown = {"csharp": None if left_kind == "compiled" else left, "swift": None if right_kind == "compiled" else right}
    details = shown if verdict in ("diagnostics", "positions") or verdict.endswith("failed") else None
    return {"case": program["case"], "program": program["program"], "debug": program["debug"], "verdict": verdict, "workspace": str(directory),
            "diagnostics": details}


def run_verdict(left_kind, left, right_kind, right):
    """Verdict for the C# (left) and Swift (right) outcomes of one program."""
    if "failed" in (left_kind, right_kind):
        return "both-failed" if left_kind == right_kind else ("csharp-failed" if left_kind == "failed" else "swift-failed")
    if left_kind == right_kind == "rejected":
        return rejection_verdict(left, right)
    if left_kind != right_kind:
        return "csharp-only" if right_kind == "rejected" else "swift-only"
    differing = differing_sections(left, right)
    return "different:" + ",".join(differing) if differing else "identical"


def rejection_verdict(left, right):
    """Both compilers rejected the program: equal ordered diagnostics agree; otherwise codes or only positions differ."""
    if left == right:
        return "both-rejected"
    return "diagnostics" if [item[3] for item in left] != [item[3] for item in right] else "positions"


def baseline_line(result):
    return "\t".join([result["case"], result["program"], "debug" if result["debug"] else "nodebug", result["verdict"]])


def ratchet(results, baseline):
    """(new differences, resolved baseline entries) of this run."""
    current = {baseline_line(result) for result in results if result["verdict"] not in ("identical", "both-rejected")}
    return sorted(current - baseline), sorted(baseline - current)


def differing(line):
    """Program key and differing section names of a baseline line."""
    case, program, debug, verdict = line.split("\t")
    names = set(verdict.split(":", 1)[1].split(",")) if verdict.startswith("different:") else {verdict}
    return (case, program, debug), names


def regressions(new, resolved):
    """New lines that are not a strict improvement of the resolved line for the same program."""
    previous = dict(differing(line) for line in resolved)
    return [line for line in new if not (lambda key, names: key in previous and names < previous[key])(*differing(line))]


def summarize(results):
    counts = {}
    for result in results:
        counts[result["verdict"]] = counts.get(result["verdict"], 0) + 1
    return counts


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--swift", type=Path, default=SWIFT_CLI, help="Swift ges executable")
    parser.add_argument("--csharp", type=Path, default=CSHARP_CLI, help="C# GameEventScript.Tool.dll")
    parser.add_argument("--kind", default="supported", help="Corpus kind filter passed to the runner")
    parser.add_argument("--update-baseline", action="store_true", help="Record the current differences as the baseline")
    args = parser.parse_args(argv)
    for path in (args.swift, args.csharp):
        if not path.exists():
            parser.error(f"missing compiler CLI {path}; build both tools first")
    WORK.mkdir(parents=True, exist_ok=True)
    requests_path = WORK / "requests.json"
    subprocess.run([sys.executable, "conformance/runner/main.py", "requests", "--kind", args.kind, "--output", str(requests_path)],
                   cwd=ROOT, check=True, stdout=subprocess.DEVNULL)
    unique = programs(json.loads(requests_path.read_text(encoding="utf-8")))
    with ThreadPoolExecutor(max_workers=os.cpu_count() or 4) as pool:
        results = list(pool.map(lambda item: compare(item[0], item[1], ["dotnet", str(args.csharp)], [str(args.swift)]), unique.items()))
    counts = summarize(results)
    (WORK / "report.json").write_text(json.dumps({"counts": counts, "results": results}, indent=1) + "\n", encoding="utf-8")
    identical = counts.get("identical", 0)
    rejected = counts.get("both-rejected", 0)
    print(f"{len(results)} programs: identical {identical}, both rejected {rejected}, different {len(results) - identical - rejected}")
    lines = BASELINE.read_text(encoding="utf-8").splitlines() if BASELINE.exists() else []
    baseline = {line for line in lines if line and not line.startswith("#")}
    if args.update_baseline:
        current = sorted({baseline_line(result) for result in results if result["verdict"] not in ("identical", "both-rejected")})
        BASELINE.write_text("# case\tprogram\tdebug\tverdict; regenerate with scripts/compare-compiler-output.py --update-baseline\n"
                            + "".join(line + "\n" for line in current), encoding="utf-8")
        print(f"Wrote {len(current)} baseline entries to {BASELINE}")
        return 0
    new, resolved = ratchet(results, baseline)
    worse = set(regressions(new, resolved))
    for line in new:
        print(f"{'new difference' if line in worse else 'improved difference'}: {line}")
    for line in resolved:
        print(f"resolved difference still in baseline: {line}")
    if new or resolved:
        print(f"{len(worse)} new or worse, {len(new) - len(worse)} improved, {len(resolved)} baseline entries changed.")
    if new or resolved:
        print("Fix the compiler, or run scripts/compare-compiler-output.py --update-baseline after a deliberate change.")
        return 1
    print(f"Compiler output verified ({len(baseline)} baselined differences remain).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
