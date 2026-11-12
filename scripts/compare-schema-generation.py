#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0
"""Require identical schema generation layouts and code from the C# and Swift CLIs.

Every distinct combination of sources, root and parameters in the schemaApi cases of the corpus runs `schema layout` in
both CLIs, once with its root and once with --all-roots, and `schema generate` for C# and for Swift with its root. Both
must print the same text, or both must fail with the same error code. The SHA-256 of every output is also ratcheted
against scripts/schema-generation-baseline.txt, so a change that alters existing generated code or layouts is noticed;
--update-baseline records a deliberate change. The role fixture is also generated into directories,
split by schema and by file, where both CLIs must write the same files with the same bytes. --self-test qualifies the
comparison with negative controls.
"""
import argparse
from concurrent.futures import ThreadPoolExecutor
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "conformance/runner"))
from core.corpus import read_corpus  # noqa: E402

SWIFT_CLI = ROOT / "artifacts/swift/tool/debug/ges"
CSHARP_CLI = ROOT / "artifacts/csharp/tool/bin/Release/net8.0/GameEventScript.Tool.dll"
TIMEOUT_SECONDS = 60
BASELINE = ROOT / "scripts/schema-generation-baseline.txt"
CODE = re.compile(r"error ([a-z]+\.[A-Za-z]+)")


def inputs(paths):
    """The distinct (sources, root, parameters) of the schemaApi cases with literal parameters, in corpus order."""
    seen, result = set(), []
    for case in read_corpus(paths):
        api = case.metadata.get("schemaApi") if case.kind == "schemaApi" else None
        if not isinstance(api, dict) or not api.get("sources") or not api.get("root"):
            continue
        # Typed API values have no literal that the CLI could pass on.
        if not all(isinstance(value, str) for value in (api.get("parameters") or {}).values()):
            continue
        parameters = tuple(sorted((api.get("parameters") or {}).items()))
        key = (tuple(api["sources"]), api["root"], parameters)
        if key not in seen:
            seen.add(key)
            result.append(key)
    return result


def outcome(result):
    """What a run is compared by: the output of a success, or the error code of a failure."""
    if result.returncode == 0:
        return ("ok", result.stdout.decode("utf-8"))
    match = CODE.search(result.stderr.decode("utf-8", "replace"))
    return ("error", result.returncode, match.group(1) if match else "")


def difference(csharp, swift):
    """None when both ports agree, otherwise a short description."""
    if csharp == swift:
        return None
    if csharp[0] == "ok" and swift[0] == "ok":
        left, right = csharp[1].splitlines(), swift[1].splitlines()
        for line, (a, b) in enumerate(zip(left, right), 1):
            if a != b:
                return f"line {line}: C# {a.strip()!r}, Swift {b.strip()!r}"
        return f"C# has {len(left)} lines, Swift {len(right)}"
    return f"C# {csharp[:3]!r}, Swift {swift[:3]!r}"


def compare(index, item, csharp, swift, work):
    sources, root, parameters = item
    directory = work / str(index)
    directory.mkdir()
    files = []
    for number, text in enumerate(sources):
        path = directory / f"s{number}.gess"
        path.write_text(text, encoding="utf-8")
        files.append(path.name)
    options = [argument for name, value in parameters for argument in ("--param", f"{name}={value}")]
    differences = []
    digests = []
    for selection in (["layout", "--root", root], ["layout", "--all-roots"], ["generate", "--root", root, "--language", "csharp", "--namespace", "Game.Wire", "--values"],
                      ["generate", "--root", root, "--language", "swift", "--namespace", "Wire", "--access", "internal", "--values"]):
        arguments = ["schema", selection[0], *files, *selection[1:], *options]
        runs = [subprocess.run([*command, *arguments], cwd=directory, capture_output=True, check=False, timeout=TIMEOUT_SECONDS) for command in (csharp, swift)]
        found = difference(outcome(runs[0]), outcome(runs[1]))
        if found:
            differences.append(f"{' '.join(selection)}: {found}")
        digests.append((key(item) + " " + "/".join(selection[:1] + [part for part in selection[1:] if part in ("csharp", "swift", "--all-roots")]), digest(outcome(runs[0]))))
    return differences, digests


def key(item):
    """A stable name of a schema input: the SHA-256 of its sources, root and parameters."""
    return hashlib.sha256(json.dumps(item, ensure_ascii=False).encode("utf-8")).hexdigest()[:16]


def digest(result):
    """The SHA-256 of an output, or of the error code of a failure."""
    return hashlib.sha256(json.dumps(result).encode("utf-8")).hexdigest()[:16]


def baseline_differences(digests, update, baseline=BASELINE):
    """The outputs whose digest differs from the recorded one, or every new and missing entry."""
    current = dict(digests)
    if update:
        baseline.write_text("".join(f"{name} {value}\n" for name, value in sorted(current.items())), encoding="utf-8")
        return []
    recorded = dict(line.rsplit(" ", 1) for line in baseline.read_text(encoding="utf-8").splitlines() if line)
    changed = [f"generation baseline: {name} changed" for name in sorted(recorded) if name in current and current[name] != recorded[name]]
    missing = [f"generation baseline: {name} is no longer generated" for name in sorted(recorded) if name not in current]
    new = [f"generation baseline: {name} is new" for name in sorted(current) if name not in recorded]
    return changed + missing + new


def directory_difference(csharp, swift):
    """None when two output directories hold the same files with the same bytes, otherwise a short description."""
    left = {path.name: path.read_bytes() for path in csharp.iterdir()} if csharp.is_dir() else {}
    right = {path.name: path.read_bytes() for path in swift.iterdir()} if swift.is_dir() else {}
    if sorted(left) != sorted(right):
        return f"C# writes {sorted(left)}, Swift {sorted(right)}"
    for name in sorted(left):
        if left[name] != right[name]:
            found = difference(("ok", left[name].decode("utf-8")), ("ok", right[name].decode("utf-8")))
            return f"{name}: {found}"
    return None


def self_test():
    ok = subprocess.CompletedProcess([], 0, b'{\n  "a": 1\n}\n', b"")
    changed = subprocess.CompletedProcess([], 0, b'{\n  "a": 2\n}\n', b"")
    failed = subprocess.CompletedProcess([], 1, b"", b"schema: error schema.unknownName: x\n")
    other = subprocess.CompletedProcess([], 1, b"", b"schema: error schema.syntax: x\n")
    assert difference(outcome(ok), outcome(ok)) is None
    assert difference(outcome(failed), outcome(failed)) is None
    assert "line 2" in difference(outcome(ok), outcome(changed))
    assert difference(outcome(ok), outcome(failed))
    assert difference(outcome(failed), outcome(other))
    longer = subprocess.CompletedProcess([], 0, b'{\n  "a": 1\n}\n\n', b"")
    assert difference(outcome(ok), outcome(longer))
    with tempfile.TemporaryDirectory() as temporary:
        work = Path(temporary)
        for side in ("a", "b"):
            (work / side).mkdir()
            (work / side / "A.swift").write_text("one\n", encoding="utf-8")
        assert directory_difference(work / "a", work / "b") is None
        (work / "b" / "A.swift").write_text("two\n", encoding="utf-8")
        assert "A.swift: line 1" in directory_difference(work / "a", work / "b")
        (work / "b" / "A.swift").write_text("one\n", encoding="utf-8")
        (work / "b" / ".ges-generated.json").write_text("{}\n", encoding="utf-8")
        assert "Swift" in directory_difference(work / "a", work / "b")
        recorded = work / "baseline.txt"
        assert baseline_differences({"x layout": "1", "y layout": "2"}, True, recorded) == []
        assert baseline_differences({"y layout": "2", "x layout": "1"}, False, recorded) == []
        found = baseline_differences({"x layout": "1", "y layout": "3", "z layout": "4"}, False, recorded)
        assert found == ["generation baseline: y layout changed", "generation baseline: z layout is new"], found
        assert baseline_differences({"x layout": "1"}, False, recorded) == ["generation baseline: y layout is no longer generated"]
    sample = inputs([ROOT / "conformance/suites/api/schemas.md"])
    assert sample and all(len(item) == 3 for item in sample)
    print("Schema generation comparison controls passed.")


# The checked-in generated code: (fixture directory, files, selection, language, namespace, file). Both CLIs must find it
# current with --check.
GOLDEN_FIXTURE = ROOT / "conformance/fixtures/SchemaGeneration/fleet.gess"
ROLES = ROOT / "conformance/fixtures/SchemaGeneration/roles"
TREE_FIXTURE = ROOT / "conformance/fixtures/SchemaGeneration/tree.gess"
COMPUTED_FIXTURE = ROOT / "conformance/fixtures/SchemaGeneration/computed.gess"
GEOMETRY_FIXTURE = ROOT / "conformance/fixtures/SchemaGeneration/geometry.gess"
ROLE_FILES = ["World.gess", "Panels.gess", "View.gess"]
CSHARP_GENERATED = ROOT / "implementation/csharp/GameEventScript.Generator/tests/Generated"
SWIFT_GENERATED = ROOT / "implementation/swift/GameEventScriptConformance/Tests/GameEventScriptConformanceTests/Generated"
GOLDENS = (
    (GOLDEN_FIXTURE.parent, [GOLDEN_FIXTURE.name], ["--root", "Fleet", "--param", "worldSize=2km"], "csharp", "GameEventScript.Tests.Generator.Generated", CSHARP_GENERATED / "Fleet.g.cs"),
    (GOLDEN_FIXTURE.parent, [GOLDEN_FIXTURE.name], ["--root", "Fleet", "--param", "worldSize=2km"], "swift", "Generated", SWIFT_GENERATED / "Fleet.swift"),
    (ROLES, ROLE_FILES, ["--wire", "world", "--param", "worldWidth=2000m", "--values"], "csharp", "GameEventScript.Tests.Generator.Roles.Wire", CSHARP_GENERATED / "RolesWire.g.cs"),
    (ROLES, ROLE_FILES, ["--model", "view", "--values"], "csharp", "GameEventScript.Tests.Generator.Roles.Model", CSHARP_GENERATED / "RolesModel.g.cs"),
    (ROLES, ROLE_FILES, ["--wire", "world", "--param", "worldWidth=2000m", "--values"], "swift", "RolesWire", SWIFT_GENERATED / "RolesWire.swift"),
    (ROLES, ROLE_FILES, ["--model", "view", "--values"], "swift", "RolesModel", SWIFT_GENERATED / "RolesModel.swift"),
    (ROLES, ROLE_FILES, ["--model", "view", "--bindings", "--values"], "csharp", "GameEventScript.Tests.Generator.Roles.Bound", CSHARP_GENERATED / "RolesBound.g.cs"),
    (ROLES, ROLE_FILES, ["--model", "view", "--bindings", "--values"], "swift", "RolesBound", SWIFT_GENERATED / "RolesBound.swift"),
    (TREE_FIXTURE.parent, [TREE_FIXTURE.name], ["--wire", "tree", "--values"], "csharp", "GameEventScript.Tests.Generator.Tree.Wire", CSHARP_GENERATED / "TreeWire.g.cs"),
    (TREE_FIXTURE.parent, [TREE_FIXTURE.name], ["--wire", "tree", "--values"], "swift", "TreeWire", SWIFT_GENERATED / "TreeWire.swift"),
    (TREE_FIXTURE.parent, [TREE_FIXTURE.name], ["--model", "tree", "--values"], "csharp", "GameEventScript.Tests.Generator.Tree.Model", CSHARP_GENERATED / "TreeModel.g.cs"),
    (TREE_FIXTURE.parent, [TREE_FIXTURE.name], ["--model", "tree", "--values"], "swift", "TreeModel", SWIFT_GENERATED / "TreeModel.swift"),
    (COMPUTED_FIXTURE.parent, [COMPUTED_FIXTURE.name], ["--wire", "computed", "--values"], "csharp", "GameEventScript.Tests.Generator.Computed.Wire", CSHARP_GENERATED / "ComputedWire.g.cs"),
    (COMPUTED_FIXTURE.parent, [COMPUTED_FIXTURE.name], ["--wire", "computed", "--values"], "swift", "ComputedWire", SWIFT_GENERATED / "ComputedWire.swift"),
    (COMPUTED_FIXTURE.parent, [COMPUTED_FIXTURE.name], ["--model", "computed", "--values"], "csharp", "GameEventScript.Tests.Generator.Computed.Model", CSHARP_GENERATED / "ComputedModel.g.cs"),
    (COMPUTED_FIXTURE.parent, [COMPUTED_FIXTURE.name], ["--model", "computed", "--values"], "swift", "ComputedModel", SWIFT_GENERATED / "ComputedModel.swift"),
    (GEOMETRY_FIXTURE.parent, [GEOMETRY_FIXTURE.name], ["--wire", "geometry", "--values"], "csharp", "GameEventScript.Tests.Generator.Geometry.Wire", CSHARP_GENERATED / "GeometryWire.g.cs"),
    (GEOMETRY_FIXTURE.parent, [GEOMETRY_FIXTURE.name], ["--wire", "geometry", "--values"], "swift", "GeometryWire", SWIFT_GENERATED / "GeometryWire.swift"),
    (GEOMETRY_FIXTURE.parent, [GEOMETRY_FIXTURE.name], ["--model", "geometry", "--bindings", "--documents"], "csharp", "GameEventScript.Tests.Generator.Geometry.Model", CSHARP_GENERATED / "GeometryModel.g.cs"),
    (GEOMETRY_FIXTURE.parent, [GEOMETRY_FIXTURE.name], ["--model", "geometry", "--bindings", "--documents"], "swift", "GeometryModel", SWIFT_GENERATED / "GeometryModel.swift"),
)
# The layouts of the role fixture, which both CLIs must print identically.
ROLE_LAYOUTS = (["--wire", "world", "--param", "worldWidth=2000m"], ["--wire", "panels"], ["--model", "view"])


def stale_goldens(csharp, swift):
    """The checked-in files that a CLI does not find current."""
    stale = []
    for directory, files, selection, language, space, path in GOLDENS:
        for name, command in (("C#", csharp), ("Swift", swift)):
            run = subprocess.run([*command, "schema", "generate", *files, *selection, "--language", language, "--namespace", space, "--access", "internal", "-o", str(path), "--check"],
                                 cwd=directory, capture_output=True, check=False, timeout=TIMEOUT_SECONDS)
            if run.returncode != 0:
                stale.append(f"{name} CLI: {path.relative_to(ROOT)} is not current: {run.stderr.decode('utf-8', 'replace').strip()}")
    return stale


# Split output of the role fixture: (selection, language, namespace), each split by schema and by file.
ROLE_SPLITS = ((["--wire", "world", "--param", "worldWidth=2000m", "--values"], "csharp", "Roles.Wire"),
               (["--wire", "world", "--param", "worldWidth=2000m", "--values"], "swift", "Roles.Wire"),
               (["--model", "view", "--bindings", "--values"], "csharp", "Roles.Model"),
               (["--model", "view", "--bindings", "--values"], "swift", "Roles.Model"))


def role_splits(csharp, swift, work):
    """The role fixture's split output, compared between the CLIs file by file."""
    differences = []
    for index, (selection, language, space) in enumerate(ROLE_SPLITS):
        for split in ("schema", "file"):
            directories = [work / f"split-{index}-{split}-{name}" for name in ("csharp", "swift")]
            for command, directory in zip((csharp, swift), directories):
                run = subprocess.run([*command, "schema", "generate", *ROLE_FILES, *selection, "--language", language, "--namespace", space, "--out-dir", str(directory), "--split", split],
                                     cwd=ROLES, capture_output=True, check=False, timeout=TIMEOUT_SECONDS)
                if run.returncode != 0:
                    differences.append(f"split {split} {language} {' '.join(selection)}: {run.stderr.decode('utf-8', 'replace').strip()}")
            found = directory_difference(*directories)
            if found:
                differences.append(f"split {split} {language} {' '.join(selection)}: {found}")
    return differences


def role_layouts(csharp, swift):
    """The role fixture's layouts by group, compared between the CLIs."""
    differences = []
    for selection in ROLE_LAYOUTS:
        runs = [subprocess.run([*command, "schema", "layout", *ROLE_FILES, *selection], cwd=ROLES, capture_output=True, check=False, timeout=TIMEOUT_SECONDS) for command in (csharp, swift)]
        found = difference(outcome(runs[0]), outcome(runs[1])) or (None if runs[0].returncode == 0 else f"C# failed: {runs[0].stderr.decode('utf-8', 'replace').strip()}")
        if found:
            differences.append(f"layout {' '.join(selection)}: {found}")
    return differences


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--csharp", type=Path, default=CSHARP_CLI, help="C# GameEventScript.Tool.dll")
    parser.add_argument("--swift", type=Path, default=SWIFT_CLI, help="Swift ges executable")
    parser.add_argument("--self-test", action="store_true", help="only check the comparison with negative controls")
    parser.add_argument("--update-baseline", action="store_true", help="record the digests of the current outputs")
    args = parser.parse_args()
    if args.self_test:
        self_test()
        return 0
    paths = sorted((ROOT / "conformance/suites").rglob("*.md"))
    items = inputs(paths)
    with tempfile.TemporaryDirectory() as temporary, ThreadPoolExecutor(max_workers=os.cpu_count() or 4) as pool:
        work = Path(temporary)
        results = list(pool.map(lambda pair: compare(pair[0], pair[1], ["dotnet", str(args.csharp)], [str(args.swift)], work), enumerate(items)))
        splits = role_splits(["dotnet", str(args.csharp)], [str(args.swift)], work)
    differences = [line for result, _ in results for line in result] + baseline_differences([entry for _, found in results for entry in found], args.update_baseline) + stale_goldens(["dotnet", str(args.csharp)], [str(args.swift)]) \
        + role_layouts(["dotnet", str(args.csharp)], [str(args.swift)]) + splits
    for line in differences:
        print(line)
    print(f"{len(items)} schema inputs, {2 * len(items)} layouts and {2 * len(items)} generated sources, {len(ROLE_LAYOUTS)} role layouts, {2 * len(ROLE_SPLITS)} split outputs, {len(GOLDENS)} checked-in files: {len(differences)} port differences")
    return 1 if differences else 0


if __name__ == "__main__":
    sys.exit(main())
