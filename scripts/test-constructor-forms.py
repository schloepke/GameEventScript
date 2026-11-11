#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Run a matrix of type-constructor forms through both CLIs and require equal results.

Each form runs as source, `parse`, `parse strict` and a `.gesd` value, single-line and across lines. A port difference
in the value, the diagnostic codes or the first diagnostic position fails; a document's value is its formatted text,
whose layout both ports share for these one-line documents. The corpus owns the expected results; this
matrix finds forms the corpus does not reach. `--self-test` qualifies the comparison.
"""

import argparse
import concurrent.futures
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile

ROOT = Path(__file__).resolve().parents[1]
REPORT = ROOT / "artifacts/swift/constructor-forms/report.json"
SWIFT = [str(ROOT / "artifacts/swift/tool/debug/ges")]
CSHARP = ["dotnet", str(ROOT / "artifacts/csharp/tool/bin/Release/net8.0/GameEventScript.Tool.dll")]
HEAD = "record :Hit as {\n  a: :Number\n}\n\non Main(args) {\n"


VALUES = [
    "1", "1.5", "2m", "50%", '"a"', '"5"', "#a", "true", "nothing", "[1, 2]", "[a: 1]", ":Dice[6, 3]", ":Range(1, 3)",
    ":Series(fibonacci)", ":Handler(Foo(x))", ":Message(Foo())", ":Vector(1, 2)", ":Point(1m, 2m)", ':Record("Hit", [a: 1])',
]
TYPES = [
    "Nothing", "Boolean", "Number", "Percentage", "Text", "Tag", "List", "Map", "Dice", "Range", "Series", "Message",
    "Handler", "Record", "Quantity(m)", "Vector", "Point", "Unknown", "Hit",
]
MESSAGES = [
    "Foo", "Foo()", "Foo(1)", "Foo(_: 1)", "Foo(a: 1)", "Foo(a: 1, b: 2)", "Foo(1, 2)", "Foo with #a", "Foo() with #a",
    "Foo(a: 1) with #a, #b", "Foo(a: 1, a: 2)", "Foo_Bar()", "foo()", "initialization()", "undeliverable()",
    'Foo(a: 1) with "a"', "Foo() with nothing", "Foo() with []", "\u00c4b()",
]
HANDLERS = [
    "Foo", "Foo()", "Foo(x)", "Foo(x, y)", "Foo(_)", "Foo(_, _)", "Foo(x, _)", "Foo(x, x)", "Foo(1)", "Foo(x: 1)",
    "Foo_Bar(x)", "foo(x)", "initialization", "undeliverable", "initialization()", "undeliverable()", "initialization(x)",
    '"Foo"', "Foo with #a", "\u00c4b(x)",
]
BRACKETS = [
    ":Dice[]", ":Dice[6]", ":Dice[6, 3]", ":Dice[0]", ":Dice[-1]", ":Dice[1.5]", ':Dice["a"]', ":Dice[1m]", ":Dice [6]",
    ":List[1]", ":List[1, 2]", ":List[]", ":Map[a: 1]", ":Map[:]", ":Map[]",
]
CUSTOM = [":Hit(a: 1)", ":Hit(1)", ':Hit("x")', ':Hit(a: "x")', ":Hit(b: 1)", ":Hit(a: 1, a: 2)", ":Hit(a: 1, b: 2)"]


def type_forms():
    for name in TYPES:
        yield f":{name}"
        yield f":{name}()"
        yield f":{name} ()"
        for value in VALUES:
            yield f":{name}({value})"
            yield f":{name}(value: {value})"
        yield f":{name}(foo: 1)"
        yield f":{name}(1,)"
        yield f":{name}(_: 1)"
        yield f":{name}(1, 2)"
        yield f":{name}(1, 2, 3)"
        yield f":{name}(1, 2, 3, 4)"
        yield f":{name}(value: 1, value: 2)"


def number_forms():
    for value in ["1", "1.5", "2m", '"5"', "nothing", "true"]:
        for unit in ['"m"', '"none"', '""', '"second"', '"km"', '"xx"', "1", "nothing"]:
            yield f":Number({value}, {unit})"
            yield f":Number({value}, unit: {unit})"
            yield f":Number(value: {value}, unit: {unit})"
            yield f":Number(unit: {unit}, value: {value})"


def range_forms():
    cases = [
        ("1", "3", None), ("1", "3", "1"), ("1", "3", "2"), ("3", "1", "-1"), ("1.5", "3", None),
        ("1", "3", "0"), ("1m", "3m", None), ("1m", "3", None), ('"1"', "3", None), ("1", "nothing", None),
    ]
    for start, end, step in cases:
        labeled_step = f", step: {step}" if step else ""
        yield f":Range({start}, {end}{f', {step}' if step else ''})"
        yield f":Range(from: {start}, to: {end}{labeled_step})"
        yield f":Range(to: {end}, from: {start}{labeled_step})"
        if step:
            yield f":Range(step: {step}, from: {start}, to: {end})"
            yield f":Range({start}, {end}, step: {step})"
    yield from [":Range(from: 1)", ":Range(to: 3)", ":Range(step: 2)", ":Range(1, to: 3)", ":Range(from: 1, 3)"]


def series_forms():
    for kind in ["fibonacci", "factorial", '"fibonacci"', '"factorial"', '"other"', "1"]:
        yield f":Series({kind})"
        for offset in ["0", "2", "-1", "1.5", "2m", '"2"', "nothing"]:
            yield f":Series({kind}, {offset})"
            yield f":Series({kind}, offset: {offset})"
            yield f":Series(kind: {kind}, offset: {offset})"
            yield f":Series(offset: {offset}, kind: {kind})"
        yield f":Series(kind: {kind})"


def message_forms():
    for message in MESSAGES:
        yield f":Message({message})"
    for message, tags in [("Foo()", "#a"), ("Foo()", "[#a]"), ("Foo", "#a"), ("Foo(a: 1)", "[#a, #b]"), ("Foo()", '"a"'), ("Foo()", "1")]:
        yield f":Message({message}, {tags})"
        yield f":Message({message}, tags: {tags})"
        yield f":Message(value: {message}, tags: {tags})"
    yield from [":Message(value: Foo())", ':Message("Foo")', ":Message(1, 2)"]


def record_forms():
    for name in ['"Hit"', '"hit"', '"A_B"', '"A__B"', '""', "1", "nothing", "Hit"]:
        for fields in ["[a: 1]", "[:]", "1", "nothing", "[1]"]:
            yield f":Record({name}, {fields})"
            yield f":Record(type: {name}, fields: {fields})"
            yield f":Record(fields: {fields}, type: {name})"
    yield from [':Record("Hit")', ':Record(type: "Hit")', ":Record(fields: [a: 1])", ':Record("Hit", fields: [a: 1])', ':Record(type: "Hit", [a: 1])']


def quantity_forms():
    for unit in ["m", "s", "none", "second", "m/s", "km", "xx", '"m"', "1", "W/K", "deg/s", "meter"]:
        for value in ["1", "2m", '"3"', "true", "nothing"]:
            yield f":Quantity({unit})({value})"
        yield f":Quantity({unit})"


def matrix():
    forms = {}
    groups = {
        "type": type_forms(),
        "number": number_forms(),
        "range": range_forms(),
        "series": series_forms(),
        "message": message_forms(),
        "handler": (f":Handler({handler})" for handler in HANDLERS),
        "record": record_forms(),
        "custom": CUSTOM,
        "bracket": BRACKETS,
        "quantity": quantity_forms(),
    }
    for group, members in groups.items():
        for form in members:
            forms.setdefault(form, group)
    return forms


def wrapped(form):
    """The form with line breaks after its opening parenthesis, after its commas and before its closing one."""
    start = form.find("(")
    if start < 0 or not form.endswith(")"):
        return None
    depth = 0
    quote = None
    parts = [""]
    for c in form[start + 1:-1]:
        if quote:
            quote = None if c == quote else quote
        elif c == '"':
            quote = c
        elif c in "([":
            depth += 1
        elif c in ")]":
            depth -= 1
        if c == "," and depth == 0 and not quote:
            parts.append("")
        else:
            parts[-1] += c
    return form[: start + 1] + "\n" + ",\n".join(part.strip() for part in parts) + "\n)"


PARSE = {"parse": "parse", "strict": "parse strict"}
# `parse` reads its form at run time, so one program can hold many of them; a batch that does not end cleanly in both
# ports runs its forms one by one instead. A form that can fail to compile needs its own program. The CLI processes at
# most 64 messages per run, Main and one emit per form.
BATCH = 60


def single(form, mode):
    if mode in PARSE:
        return form, mode, HEAD + f"  emit ConsoleOut([{PARSE[mode]} '{form}'])\n}}\n", ".ges"
    if mode == "source":
        return form, mode, HEAD + f"  emit ConsoleOut([{form}])\n}}\n", ".ges"
    if mode == "lines":
        return form, mode, HEAD + f"  emit ConsoleOut([{wrapped(form)}])\n}}\n", ".ges"
    return form, mode, f"a: {form}\n", ".gesd"


def jobs(forms):
    forms = list(forms)
    # The longer batches go first, so that the single runs fill the workers behind them.
    for mode in PARSE:
        for start in range(0, len(forms), BATCH):
            yield "batch", (mode, forms[start:start + BATCH])
    for form in forms:
        yield "single", single(form, "source")
        yield "single", single(form, "document")
        if wrapped(form):
            yield "single", single(form, "lines")


def observe(command, path, suffix):
    arguments = [*command, "run", path] if suffix == ".ges" else [*command, "document", "format", path]
    process = subprocess.run(arguments, capture_output=True, text=True, timeout=120, cwd=os.path.dirname(path))
    errors = re.findall(r"\((\d+),(\d+)\): error ([\w.]+)", process.stderr)
    if errors:
        first = errors[0]
        return {"at": f"{first[0]},{first[1]}", "codes": [error[2] for error in errors]}
    if process.returncode:
        return {"exit": process.returncode, "error": process.stderr.strip().splitlines()[:1]}
    return {"value": process.stdout.rstrip("\n")}


def run_single(job):
    form, mode, text, suffix = job
    with tempfile.TemporaryDirectory() as directory:
        path = os.path.join(directory, "form" + suffix)
        Path(path).write_text(text, encoding="utf-8")
        return form, mode, observe(SWIFT, path, suffix), observe(CSHARP, path, suffix)


def batch_values(stdout, count):
    """The value of each form of a batch, or None unless every form printed exactly one line under its index."""
    lines = stdout.rstrip("\n").split("\n")
    if len(lines) != count:
        return None
    values = []
    for index, line in enumerate(lines):
        prefix = f"{index}:"
        if not line.startswith(prefix):
            return None
        values.append({"value": line[len(prefix):]})
    return values


def run_batch(mode, forms):
    text = HEAD + "".join(f"  emit ConsoleOut(\"{index}:\", [{PARSE[mode]} '{form}'])\n" for index, form in enumerate(forms)) + "}\n"
    with tempfile.TemporaryDirectory() as directory:
        path = os.path.join(directory, "forms.ges")
        Path(path).write_text(text, encoding="utf-8")
        observed = []
        for command in (SWIFT, CSHARP):
            process = subprocess.run([*command, "run", path], capture_output=True, text=True, timeout=120, cwd=directory)
            observed.append(batch_values(process.stdout, len(forms)) if process.returncode == 0 else None)
    if None in observed:
        return [run_single(single(form, mode)) for form in forms]
    return [(form, mode, swift, csharp) for form, swift, csharp in zip(forms, *observed)]


def run(job):
    kind, payload = job
    return [run_single(payload)] if kind == "single" else run_batch(*payload)


def differences(results):
    return [{"form": form, "mode": mode, "swift": swift, "csharp": csharp} for form, mode, swift, csharp in results if swift != csharp]


def self_test():
    same = {"value": "[1]"}
    assert not differences([("a", "source", same, dict(same))])
    assert differences([("a", "source", same, {"value": "[2]"})])
    assert differences([("a", "source", {"at": "1,1", "codes": ["x"]}, {"at": "1,2", "codes": ["x"]})])
    assert differences([("a", "source", {"at": "1,1", "codes": ["x"]}, {"at": "1,1", "codes": ["y"]})])
    assert differences([("a", "source", same, {"exit": 1, "error": []})])
    assert wrapped(':Range(1, [2, 3])') == ':Range(\n1,\n[2, 3]\n)'
    assert wrapped(':Dice[6]') is None
    assert batch_values("0:[1]\n1:[nothing]\n", 2) == [{"value": "[1]"}, {"value": "[nothing]"}]
    assert batch_values("0:[1]\n", 2) is None
    assert batch_values("0:[1]\n2:[2]\n", 2) is None
    assert batch_values("0:[1]\n1:[a\nb]\n", 2) is None


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--self-test", action="store_true")
    options = parser.parse_args()
    self_test()
    if options.self_test:
        print("Constructor-form comparison controls passed.")
        return 0
    forms = matrix()
    work = list(jobs(forms))
    with concurrent.futures.ThreadPoolExecutor(os.cpu_count() or 4) as pool:
        results = [result for results in pool.map(run, work) for result in results]
    found = differences(results)
    REPORT.parent.mkdir(parents=True, exist_ok=True)
    REPORT.write_text(json.dumps(found, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    for item in found[:20]:
        print(json.dumps(item, ensure_ascii=False))
    print(f"{len(forms)} forms in {len(results)} observations: {len(found)} port differences")
    return 1 if found else 0


if __name__ == "__main__":
    sys.exit(main())
