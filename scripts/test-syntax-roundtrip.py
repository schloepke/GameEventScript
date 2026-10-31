#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Verify the lossless Syntax lexers of C# and Swift on every corpus source and on synthetic edge cases.

Each port's adapter lexes every source with `--syntax-tokens` and reports kind, offsets and normalized text per token
in Unicode scalars. The tokens of a source must cover it without gaps, leading trivia may only hold inline whitespace,
`//` comments and a leading BOM, and both ports must report identical token streams. Negative controls qualify the
checks before the real observations are judged.

With `--trees`, a port's syntax trees must hold every token exactly once and nest correctly; valid sources must have no
error nodes, and every source must have the same tree in both ports, error nodes included.
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


def trees(port, request):
    completed = subprocess.run([*ADAPTERS[port], "--syntax-trees"], input=json.dumps(request), capture_output=True, text=True, check=False)
    if completed.returncode != 0:
        raise SystemExit(f"{port} adapter failed: {completed.stderr.strip()}")
    return json.loads(completed.stdout)["sources"]


def nested(nodes):
    # Rebuilds [kind, first, end, children] from the flat preorder list without recursion.
    # A node also carries the diagnostic range the parser recorded, or none.
    built = [[kind, first, end, [], tuple(rest[1:3])] for kind, first, end, *rest in nodes]
    for index, (_, _, _, parent, *_) in enumerate(nodes):
        if parent >= 0:
            built[parent][3].append(built[index])
    return built[0]


def node_problems(node, first, end, path):
    kind, start, stop, children, _ = node
    problems = []
    if not first <= start <= stop <= end:
        return [f"{path}/{kind} spans tokens {start}..{stop} outside {first}..{end}"]
    position = start
    for index, child in enumerate(children):
        if child[1] < position:
            problems.append(f"{path}/{kind} child {index} {child[0]} starts at token {child[1]} before {position}")
        problems += node_problems(child, start, stop, f"{path}/{kind}")
        position = max(position, child[2])
    return problems


def error_nodes(node):
    return (node[0] == "Error") + sum(error_nodes(child) for child in node[3])


def shape(node):
    kind, start, stop, children, diagnostic_range = node
    label = kind if diagnostic_range in ((), (-1, -1)) else f"{kind}{list(diagnostic_range)}"
    return [label, start, stop, [shape(child) for child in children]]


def first_difference(left, right, left_port, right_port, path="CompilationUnit"):
    if left[:3] != right[:3]:
        return f"at {path}: {left_port} {left[:3]}, {right_port} {right[:3]}"
    for index, (a, b) in enumerate(zip(left[3], right[3])):
        if a != b:
            return first_difference(a, b, left_port, right_port, f"{path}/{index}:{a[0]}")
    return f"at {path}: {left_port} has {len(left[3])} children, {right_port} {len(right[3])}"


def tree_problems(sources, lexed, parsed, valid):
    problems = []
    for key, source in sources.items():
        shapes = {}
        for port, data in parsed.items():
            entry = data.get(key)
            if entry is None:
                problems.append(f"{key}: {port} reported no tree")
                continue
            tokens, root = entry["tokens"], nested(entry["tree"])
            if [token[:4] for token in tokens] != [token[:4] for token in lexed[port][key]]:
                problems.append(f"{key} ({port}): the tree does not hold every lexed token exactly once and in order")
            if root[0] != "CompilationUnit" or root[1] != 0 or root[2] != len(tokens):
                problems.append(f"{key} ({port}): the root {root[0]} spans {root[1]}..{root[2]} of {len(tokens)} tokens")
            problems += [f"{key} ({port}): {problem}" for problem in node_problems(root, 0, len(tokens), "")]
            if key in valid and error_nodes(root):
                problems.append(f"{key} ({port}): a valid source has error nodes")
            shapes[port] = [shape(root), entry.get("roles", [])]
        # Both parsers share their recovery, so also sources with syntax errors have equal trees.
        if len(shapes) > 1 and len({json.dumps(tree) for tree in shapes.values()}) > 1:
            (port, (tree, roles)), (other, (other_tree, other_roles)) = list(shapes.items())[:2]
            if roles != other_roles:
                index = next(i for i, (a, b) in enumerate(zip(roles + [None], other_roles + [None])) if a != b)
                problems.append(f"{key}: the ports give token {index} different roles: {port} {roles[index:index + 1]}, {other} {other_roles[index:index + 1]}")
            else:
                problems.append(f"{key}: the ports build different trees; {first_difference(tree, other_tree, port, other)}")
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
    # Trees at the portable nesting limits are deeper than Python's default recursion limit.
    sys.setrecursionlimit(20000)
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--port", choices=sorted(ADAPTERS), action="append", help="Port to check; repeat for several (default: both)")
    parser.add_argument("--skip-build", action="store_true", help="Use the existing Release adapters")
    parser.add_argument("--trees", choices=sorted(ADAPTERS), action="append", default=[], help="Also check this port's syntax trees; repeat for several")
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
    # Sources of cases that compile successfully must parse without error nodes.
    valid = {case["id"] + "#" + str(index) for case in request["cases"] if case["kind"] in ("bytecode", "bytecodeSnapshot", "compileMetadata", "scriptApi")
             for index, _ in enumerate(case["sources"])}
    parsed = {port: trees(port, request) for port in args.trees}
    if len(parsed) > 1:
        # A node that one port closes a token later must be detected, so equal trees are meaningful.
        port = next(iter(parsed))
        key = next(key for key, entry in parsed[port].items() if len(entry["tree"]) > 2)
        changed = copy.deepcopy(parsed)
        node = changed[port][key]["tree"][-1]
        node[1] = node[1] - 1 if node[1] > 0 else node[1] + 1
        if not tree_problems({key: sources[key]}, observed, {name: {key: data[key]} for name, data in changed.items()}, valid):
            raise SystemExit("A negative control was not detected by the tree checks.")
    problems += tree_problems(sources, observed, parsed, valid)
    for problem in problems[:40]:
        print(problem)
    if problems:
        raise SystemExit(f"{len(problems)} lossless lexing problems in {len(sources)} sources.")
    print(f"{len(sources)} sources lexed losslessly" + (" and identically by " + " and ".join(ports) if len(ports) > 1 else f" by {ports[0]}") + "."
          + (f" Syntax trees of {' and '.join(args.trees)} hold every token." if args.trees else ""))


if __name__ == "__main__":
    main()
