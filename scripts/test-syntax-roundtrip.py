#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Verify the lossless Syntax lexers of C# and Swift on every corpus source and on synthetic edge cases.

Each port's adapter lexes every source with `--syntax-tokens` and reports kind, offsets and normalized text per token
in Unicode scalars. The tokens of a source must cover it without gaps, leading trivia may only hold inline whitespace,
`//` comments and a leading BOM, and both ports must report identical token streams. Negative controls qualify the
checks before the real observations are judged.
"""

import argparse
import copy
import json
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "conformance/runner"))
from core.corpus import read_corpus  # noqa: E402
from core.runner import requests, supported  # noqa: E402

SWIFT_SCRATCH = ROOT / "artifacts/swift/conformance"
ADAPTERS = {
    "csharp": ["dotnet", str(ROOT / "artifacts/csharp/conformance-adapter/bin/Release/net10.0/GameEventScript.ConformanceAdapter.dll")],
    "swift": [str(SWIFT_SCRATCH / "release/ges-conformance-adapter")],
}
BUILDS = {
    "csharp": ["dotnet", "build", str(ROOT / "implementation/csharp/GameEventScript.Conformance/adapter/GameEventScript.ConformanceAdapter.csproj"),
               "--configuration", "Release", "--nologo", "--verbosity", "quiet"],
    "swift": ["swift", "build", "--package-path", str(ROOT / "implementation/swift/GameEventScriptConformance"), "--scratch-path", str(SWIFT_SCRATCH),
              "--disable-build-manifest-caching", "--configuration", "release", "--product", "ges-conformance-adapter"],
}
SYNTHETIC = [
    "",
    "\ufeff",
    "\ufeffmodule bom\non Start { emit Done }\n",
    "on Start {\r\n  emit Done(value: 1) // trailing comment\r\n}\r\n",
    "  \t// only a comment",
    "on Start { emit Done(value: 'unterminated) }",
    "on Start { emit Done(single: 'C:\\', double: \"path\\\", doubled: 'a\\''b') }",
    "on Start { emit Done(emoji: '😀', math: 2 × 3 ÷ 4 − 1, angle: 90°, root: √4) }",
    "on Start { let x_1 be 10m/s; let bad_01 be 1 }",
    "on Start { emit Done(value: 1 && 2, other: 3 ||| 4, sign: § ) }",
    "on Start {\remit Done\r}",
    "on Start { emit Done(value: 1) }\u000b\u000c",
]


# Names and decoded Text carry their value in the token text; other kinds keep a per-port spelling, such as `\\n` or a
# number without its `%`, and are compared by kind and offsets.
VALUE_KINDS = {"Identifier", "Message", "TypeName", "ConstantReference", "Tag", "Selector", "Text"}


def comparable(stream):
    return [item if item[0] in VALUE_KINDS else item[:4] for item in stream]


def tokens(port, request):
    completed = subprocess.run([*ADAPTERS[port], "--syntax-tokens"], input=json.dumps(request), capture_output=True, text=True, check=False)
    if completed.returncode != 0:
        raise SystemExit(f"{port} adapter failed: {completed.stderr.strip()}")
    return json.loads(completed.stdout)["sources"]


def trivia_problem(text, start):
    position = 0
    while position < len(text):
        if text[position] in " \t":
            position += 1
        elif text.startswith("//", position):
            while position < len(text) and text[position] not in "\r\n":
                position += 1
        elif text[position] == "\ufeff" and start + position == 0:
            position += 1
        else:
            return f"unexpected trivia {text[position]!r}"
    return None


def coverage_problems(source, stream):
    problems = []
    position = 0
    for index, (kind, full_start, start, end, _) in enumerate(stream):
        if full_start != position or not full_start <= start <= end:
            problems.append(f"token {index} {kind} has offsets {full_start}, {start}, {end} after {position}")
            return problems
        problem = trivia_problem(source[full_start:start], full_start)
        if problem:
            problems.append(f"token {index} {kind}: {problem}")
        position = end
    if not stream or stream[-1][0] != "EndOfFile" or position != len(source):
        problems.append(f"tokens end at {position} of {len(source)} without a final EndOfFile")
    return problems


def compare(sources, observed):
    problems = []
    for key, source in sources.items():
        streams = {port: data.get(key) for port, data in observed.items()}
        for port, stream in streams.items():
            if stream is None:
                problems.append(f"{key}: {port} reported no tokens")
                continue
            problems += [f"{key} ({port}): {problem}" for problem in coverage_problems(source, stream)]
        present = [comparable(stream) for stream in streams.values() if stream is not None]
        if len(present) > 1 and any(stream != present[0] for stream in present[1:]):
            first = next(index for index, pair in enumerate(zip(*present)) if len({json.dumps(item) for item in pair}) > 1) if all(present) else 0
            detail = ", ".join(f"{port}: {stream[first] if first < len(stream) else None}" for port, stream in zip(streams, present))
            problems.append(f"{key}: ports differ at token {first} ({detail})")
    return problems


def controls(sources, observed):
    # Each mutation must be detected, so a passing comparison is meaningful.
    key = next(key for key, source in sources.items() if len(observed[next(iter(observed))].get(key, [])) > 2)
    port = next(iter(observed))
    mutations = [
        lambda stream: stream.pop(1),
        lambda stream: stream.__setitem__(0, [stream[0][0], 0, stream[0][2] + 1, stream[0][3] + 1, stream[0][4]]),
        lambda stream: stream.__setitem__(-1, ["Illegal", *stream[-1][1:]]),
    ]
    if len(observed) > 1:
        value = next(index for index, item in enumerate(observed[port][key]) if item[0] in VALUE_KINDS)
        mutations.append(lambda stream: stream.__setitem__(value, [*stream[value][:4], stream[value][4] + "x"]))
    for mutate in mutations:
        changed = copy.deepcopy(observed)
        mutate(changed[port][key])
        if not compare({key: sources[key]}, changed):
            raise SystemExit("A negative control was not detected by the token checks.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--port", choices=sorted(ADAPTERS), action="append", help="Port to check; repeat for several (default: both)")
    parser.add_argument("--skip-build", action="store_true", help="Use the existing Release adapters")
    args = parser.parse_args()
    ports = args.port or sorted(ADAPTERS)

    cases = [case for case in read_corpus(sorted((ROOT / "conformance/suites").rglob("*.md"))) if supported(case) and case.sources]
    request = requests(cases)
    template = next(case for case in request["cases"] if case["kind"] == "compileError")
    for index, text in enumerate(SYNTHETIC):
        case = copy.deepcopy(template)
        case["id"] = f"syntax.synthetic/case-{index:02d}"
        case["sources"] = [{"name": "synthetic.ges", "program": "main", "text": text}]
        request["cases"].append(case)
    sources = {case["id"] + "#" + str(index): source["text"] for case in request["cases"] for index, source in enumerate(case["sources"])}

    observed = {}
    for port in ports:
        if not args.skip_build:
            subprocess.run(BUILDS[port], check=True, stdout=subprocess.DEVNULL)
        observed[port] = tokens(port, request)
    controls(sources, observed)
    problems = compare(sources, observed)
    for problem in problems[:40]:
        print(problem)
    if problems:
        raise SystemExit(f"{len(problems)} lossless lexing problems in {len(sources)} sources.")
    print(f"{len(sources)} sources lexed losslessly" + (" and identically by " + " and ".join(ports) if len(ports) > 1 else f" by {ports[0]}") + ".")


if __name__ == "__main__":
    main()
