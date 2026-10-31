#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Fail on drift between the keyword-like name token sets of the C# and Swift source parsers.

C# `IsArgumentLabelKind` and `IsExtensionFunctionNameKind` own which keyword tokens may spell an
argument label or an extension function name. Swift copies them into `labelKeywords` and
`intrinsicNames`; both sets must name the same token kinds, apart from the identifier kind that
each parser checks separately.
"""
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
CSHARP = ROOT / "implementation/csharp/GameEventScript.Syntax/src/Parsing/GesSyntaxParser.cs"
SWIFT = ROOT / "implementation/swift/GameEventScriptSyntax/Sources/GameEventScriptSyntax/Parsing/SyntaxParserExpressions.swift"


def lower(name):
    return name[0].lower() + name[1:]


def csharp_kinds(text, method):
    match = re.search(r"(?:private|internal) static bool " + method + r"\(GesTokenKind kind\)\s*=>\s*kind is ([^;]+);", text, re.S)
    if not match:
        raise RuntimeError(f"Missing C# {method}")
    return {lower(name) for name in re.split(r"\s+or\s+", match[1].strip())} - {"identifier"}


def swift_sets(text):
    intrinsic = re.search(r"static let intrinsicNames: Set<GesTokenKind> = \[([^\]]*)\]", text, re.S)
    labels = re.search(r"static let labelKeywords: Set<GesTokenKind> = intrinsicNames\.union\(\[([^\]]*)\]\)", text, re.S)
    if not intrinsic or not labels:
        raise RuntimeError("Missing Swift intrinsicNames or labelKeywords")
    names = set(re.findall(r"\.(\w+)", intrinsic[1]))
    return names, names | set(re.findall(r"\.(\w+)", labels[1]))


def differences(csharp, swift):
    """Ordered drift messages; empty when both parsers accept the same keyword tokens."""
    intrinsic, labels = swift_sets(swift)
    result = []
    for name, expected, actual in (("extension function names", csharp_kinds(csharp, "IsExtensionFunctionNameKind"), intrinsic),
                                   ("argument labels", csharp_kinds(csharp, "IsArgumentLabelKind"), labels)):
        if expected != actual:
            result.append(f"{name}: C# only {sorted(expected - actual)}, Swift only {sorted(actual - expected)}")
    return result


def main():
    found = differences(CSHARP.read_text(encoding="utf-8"), SWIFT.read_text(encoding="utf-8"))
    if found:
        print("Parser token drift:\n" + "\n".join(found), file=sys.stderr)
        return 1
    print("Verified Swift argument-label and extension-name token sets against C#.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
