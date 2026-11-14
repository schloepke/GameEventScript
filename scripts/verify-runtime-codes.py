#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Fail when a `runtime.*` diagnostic code literal of either Runtime is missing from the other port or from the stable
catalog in specs/Diagnostics.md. Defensive paths that validated Programs never reach are invisible to the Conformance
corpus, so their codes are compared here. `--self-test` checks that the comparison detects drift."""
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
PORTS = {
    "C#": (ROOT / "implementation/csharp/GameEventScript.Runtime/src", "*.cs"),
    "Swift": (ROOT / "implementation/swift/GameEventScriptRuntime/Sources", "*.swift"),
}
LITERAL = re.compile(r'"(runtime\.[A-Za-z0-9]+)"')
CATALOG = re.compile(r"`(runtime\.[A-Za-z0-9]+)`")


def codes(texts):
    return {code for text in texts for code in LITERAL.findall(text)}


def differences(ports, catalog):
    """Ordered drift messages for {port: codes} against each other and the catalog."""
    result = []
    names = sorted(ports)
    for name in names:
        for other in names:
            if name != other and ports[name] - ports[other]:
                result.append(f"only in {name}: {sorted(ports[name] - ports[other])}")
        if ports[name] - catalog:
            result.append(f"{name} codes missing from specs/Diagnostics.md: {sorted(ports[name] - catalog)}")
    return result


def self_test():
    catalog = {"runtime.a", "runtime.b"}
    assert differences({"C#": {"runtime.a"}, "Swift": {"runtime.a"}}, catalog) == []
    assert len(differences({"C#": {"runtime.a"}, "Swift": {"runtime.a", "runtime.b"}}, catalog)) == 1
    assert len(differences({"C#": {"runtime.c"}, "Swift": {"runtime.c"}}, catalog)) == 2
    assert codes(['raise("runtime.x1")', "`runtime.y`"]) == {"runtime.x1"}
    print("Runtime code gate controls passed.")
    return 0


def main():
    if "--self-test" in sys.argv[1:]:
        return self_test()
    ports = {name: codes(path.read_text(encoding="utf-8") for path in root.rglob(pattern)) for name, (root, pattern) in PORTS.items()}
    catalog = set(CATALOG.findall((ROOT / "specs/Diagnostics.md").read_text(encoding="utf-8")))
    found = differences(ports, catalog)
    for line in found:
        print(line, file=sys.stderr)
    if found:
        return 1
    print(f"Verified {len(ports['C#'])} runtime diagnostic codes in both ports against the catalog.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
