#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Verify the naming rules shared by the C# and Swift library ports.

The rules are summarized in AGENTS.md and detailed in
implementation/PortMapping.md. This gate checks top-level library types, opcode
handler names and handler call sites:

- prefix: only the edge types use `GameEventScript`; every other type uses `Ges`.
- abbreviation: abbreviations are written as words (`Api`, `Vm`), also in
  namespaces and folders.
- only: after the idiomatic mappings, a type exists in both ports' module.
- handler: opcode handlers are named `Execute<OpCode>` (C# private members,
  Swift `execute<OpCode>`), optionally followed by a suffix.
- handler-call: Swift handlers are called only from dispatch and opcode files.

Known differences that later convergence phases remove are listed in
scripts/port-names-baseline.txt. The baseline is a ratchet: a new difference
fails, and so does a listed difference that no longer exists. Regenerate it
with --update-baseline after deliberately removing differences.
"""

import argparse
import re
import sys
from dataclasses import dataclass
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
BASELINE = ROOT / "scripts/port-names-baseline.txt"

EDGE_TYPES = {"GameEventScriptHost", "GameEventScriptProgram", "GameEventScriptMessage"}

# (module, C# source root, Swift source root), relative to the repository root.
MODULES = (
    ("Runtime", "implementation/csharp/GameEventScript.Runtime/src", "implementation/swift/GameEventScriptRuntime/Sources"),
    ("Compiler", "implementation/csharp/GameEventScript.Compiler/src", "implementation/swift/GameEventScriptCompiler/Sources"),
    ("Bridge", "implementation/csharp/GameEventScript.CSharpBridge/src", "implementation/swift/GameEventScriptSwiftBridge/Sources"),
    ("SyntaxHighlighter", "implementation/csharp/GameEventScript.SyntaxHighlighter/src", "implementation/swift/GameEventScriptSyntaxHighlighter/Sources"),
)

# Permanent, deliberate single-port types (normalized names). Temporary
# differences belong in the baseline instead.
PLATFORM_ONLY = {
    ("csharp", "Runtime", "IsExternalInit"): "C# compiler support type for init accessors on netstandard2.1",
    ("csharp", "Compiler", "IsExternalInit"): "C# compiler support type for init accessors on netstandard2.1",
    ("csharp", "Bridge", "IsExternalInit"): "C# compiler support type for init accessors on netstandard2.1",
    ("csharp", "Runtime", "GesReadOnlyArray"): "idiomatic mapping: Swift exposes [T]",
    ("csharp", "Compiler", "GesProgramBuilderEntry"): "C# 14 static extension container; Swift extends GameEventScriptProgram directly",
    ("swift", "Runtime", "GesApiError"): "idiomatic mapping: C# throws BCL ArgumentException/InvalidOperationException",
    ("swift", "Runtime", "GesMessageError"): "idiomatic mapping: C# throws BCL argument exceptions for invalid message input",
    ("swift", "SyntaxHighlighter", "GesHighlightingError"): "idiomatic mapping: C# throws BCL argument exceptions for invalid input",
    ("swift", "Runtime", "GesCompilerSupport"): "idiomatic mapping: @_spi(Compiler) surface; C# uses InternalsVisibleTo",
    ("swift", "Bridge", "GesTypeMacro"): "Swift macro implementation; C# uses Reflection on attributes",
    ("swift", "Bridge", "GesFieldMacro"): "Swift macro implementation; C# uses Reflection on attributes",
    ("swift", "Bridge", "GesConstructMacro"): "Swift macro implementation; C# uses Reflection on attributes",
}

# Technical types exempt from the prefix rule.
PREFIX_EXEMPT = {"IsExternalInit"}

# Capital runs that are accepted as words. `UInt16` and `CSharp` need no entry:
# the last capital of a run starts the next word, leaving a single letter.
ABBREVIATION_ALLOWED = set()

CSHARP_SKIP_DIRS = {"bin", "obj"}
SWIFT_SKIP_DIRS = {".build"}

CSHARP_TYPE = re.compile(
    r"^(?P<mods>(?:(?:public|internal|private|protected|file|static|sealed|abstract|readonly|partial|unsafe|ref|new)\s+)*)"
    r"(?:record\s+(?:struct|class)\s+|record\s+|class\s+|struct\s+|interface\s+|enum\s+|delegate\s+[\w<>\[\],?. ]+\s+)"
    r"(?P<name>[A-Za-z_]\w*)"
)
SWIFT_TYPE = re.compile(
    r"^(?P<mods>(?:@\w+(?:\([^)]*\))?\s+)*(?:(?:public|internal|package|fileprivate|private|open|final|indirect)\s+)*)"
    r"(?P<kind>class|struct|enum|protocol|actor|typealias|macro)\s+(?P<name>[A-Za-z_]\w*)"
)
CSHARP_NAMESPACE = re.compile(r"^\s*namespace\s+([\w.]+)", re.M)
CSHARP_EXTENSION_HANDLER = re.compile(r"static\s+void\s+(?P<name>\w+)\s*\(\s*this\s+GesVirtualMachine\b")
CSHARP_EXECUTE = re.compile(r"^\s*(?P<mods>(?:\w+\s+)*)void\s+(?P<name>Execute[A-Z]\w*)\s*\(", re.M)
SWIFT_EXECUTE_DECL = re.compile(r"\bfunc\s+(?P<name>execute[A-Z]\w*)\s*[(<]")
SWIFT_EXECUTE_CALL = re.compile(r"(?<!func )\b(?P<name>execute[A-Z]\w*)\s*\(")
OPCODE_ENUM = re.compile(r"enum\s+(?:GameEventScript|Ges)BytecodeOpCode\b[^{]*\{(?P<body>[^}]*)\}", re.S)


@dataclass(frozen=True)
class Declaration:
    port: str
    module: str
    name: str
    path: str


def source_files(root, suffix, skip_dirs):
    if not root.is_dir():
        return []
    return sorted(
        path for path in root.rglob("*" + suffix)
        if not any(part in skip_dirs for part in path.relative_to(root).parts)
    )


def strip_comments(text):
    text = re.sub(r"/\*.*?\*/", lambda match: "\n" * match.group(0).count("\n"), text, flags=re.S)
    return re.sub(r"//[^\n]*", "", text)


def csharp_types(text):
    """Top-level C# type names; file-scoped namespaces keep them at column 0."""
    names = []
    block_namespace = re.search(r"^namespace\s+[\w.]+\s*\{", text, re.M) is not None
    for line in strip_comments(text).splitlines():
        candidate = line[4:] if block_namespace and line.startswith("    ") and not line.startswith("     ") else line
        if candidate[:1].isspace():
            continue
        match = CSHARP_TYPE.match(candidate)
        if match and not re.search(r"\b(private|file)\b", match["mods"]):
            names.append(match["name"])
    return names


def swift_types(text):
    names = []
    for line in strip_comments(text).splitlines():
        if line[:1].isspace():
            continue
        match = SWIFT_TYPE.match(line)
        if match and not re.search(r"\b(private|fileprivate)\b", match["mods"]):
            names.append(match["name"])
    return names


def normalize(port, name):
    """Apply the idiomatic mappings so corresponding names compare equal."""
    if port == "csharp":
        if re.match(r"I(Ges|GameEventScript)[A-Z]", name):
            name = name[1:]
        if name.endswith("Attribute") and name != "Attribute":
            name = name[: -len("Attribute")]
        if name.endswith("Exception"):
            name = name[: -len("Exception")] + "Error"
    return name


def abbreviation(name):
    """Return the first capital-letter abbreviation (two or more letters) in a name."""
    for match in re.finditer(r"[A-Z]{2,}", name):
        run = match.group(0)
        end = match.end()
        word = run[:-1] if end < len(name) and name[end].islower() else run
        if len(word) >= 2 and word not in ABBREVIATION_ALLOWED:
            return word
    return None


def prefix_violation(name):
    if name in EDGE_TYPES or name in PREFIX_EXEMPT:
        return False
    base = name[1:] if re.match(r"I(Ges|GameEventScript)[A-Z]", name) else name
    return not (base.startswith("Ges") and (len(base) == 3 or base[3].isupper()))


def opcode_names(root):
    for path in source_files(root / "implementation/csharp/GameEventScript.Runtime/src", ".cs", CSHARP_SKIP_DIRS):
        match = OPCODE_ENUM.search(strip_comments(path.read_text(encoding="utf-8")))
        if match:
            return sorted(re.findall(r"^\s*(\w+)\s*=", match["body"], re.M), key=len, reverse=True)
    raise RuntimeError("Missing bytecode opcode enum in the C# Runtime")


def handler_matches(name, prefix, opcodes):
    if not name.startswith(prefix):
        return False
    rest = name[len(prefix):]
    return any(rest.startswith(opcode) and (len(rest) == len(opcode) or rest[len(opcode)].isupper() or rest[len(opcode)].isdigit()) for opcode in opcodes)


def collect(root):
    """Return the set of findings as (category, port, module, detail) tuples."""
    findings = set()
    declarations = []
    for module, csharp_root, swift_root in MODULES:
        for port, base, suffix, skip, extract in (
            ("csharp", root / csharp_root, ".cs", CSHARP_SKIP_DIRS, csharp_types),
            ("swift", root / swift_root, ".swift", SWIFT_SKIP_DIRS, swift_types),
        ):
            for path in source_files(base, suffix, skip):
                relative = path.relative_to(root).as_posix()
                text = path.read_text(encoding="utf-8")
                for name in extract(text):
                    declarations.append(Declaration(port, module, name, relative))
                for part in path.relative_to(base).parent.parts:
                    word = abbreviation(part)
                    if word:
                        findings.add(("abbreviation", port, module, "folder " + part))
                if port == "csharp":
                    for namespace in CSHARP_NAMESPACE.findall(strip_comments(text)):
                        for part in namespace.split("."):
                            if abbreviation(part):
                                findings.add(("abbreviation", port, module, "namespace " + namespace))

    for declaration in declarations:
        if declaration.name in PREFIX_EXEMPT:
            continue
        if prefix_violation(declaration.name):
            findings.add(("prefix", declaration.port, declaration.module, declaration.name))
        if abbreviation(declaration.name):
            findings.add(("abbreviation", declaration.port, declaration.module, declaration.name))

    for module, _, _ in MODULES:
        names = {
            port: {normalize(port, d.name) for d in declarations if d.port == port and d.module == module}
            for port in ("csharp", "swift")
        }
        for port, other in (("csharp", "swift"), ("swift", "csharp")):
            for name in names[port] - names[other]:
                if (port, module, name) not in PLATFORM_ONLY:
                    findings.add(("only", port, module, name))

    findings |= handler_findings(root)
    return findings


def handler_findings(root):
    findings = set()
    opcodes = opcode_names(root)
    csharp_root = root / "implementation/csharp/GameEventScript.Runtime/src"
    for path in source_files(csharp_root, ".cs", CSHARP_SKIP_DIRS):
        text = strip_comments(path.read_text(encoding="utf-8"))
        for match in CSHARP_EXTENSION_HANDLER.finditer(text):
            findings.add(("handler", "csharp", "Runtime", match["name"] + " (extension method)"))
        for match in CSHARP_EXECUTE.finditer(text):
            if not handler_matches(match["name"], "Execute", opcodes):
                findings.add(("handler", "csharp", "Runtime", match["name"]))
            if "private" not in match["mods"].split():
                findings.add(("handler", "csharp", "Runtime", match["name"] + " (not private)"))

    swift_root = root / "implementation/swift/GameEventScriptRuntime/Sources"
    swift_opcodes = [opcode[0].upper() + opcode[1:] for opcode in opcodes]
    for path in source_files(swift_root, ".swift", SWIFT_SKIP_DIRS):
        relative = path.relative_to(swift_root).as_posix()
        text = strip_comments(path.read_text(encoding="utf-8"))
        for match in SWIFT_EXECUTE_DECL.finditer(text):
            if not handler_matches(match["name"], "execute", swift_opcodes):
                findings.add(("handler", "swift", "Runtime", match["name"]))
        allowed_caller = relative.endswith("/VirtualMachineDispatch.swift") or "/Opcodes/" in "/" + relative
        if not allowed_caller:
            for match in SWIFT_EXECUTE_CALL.finditer(text):
                findings.add(("handler-call", "swift", "Runtime", f"{match['name']} in {path.name}"))
    return findings


def format_finding(finding):
    return "\t".join(finding)


def read_baseline(path):
    if not path.exists():
        return set()
    entries = set()
    for line in path.read_text(encoding="utf-8").splitlines():
        if line and not line.startswith("#"):
            parts = line.split("\t")
            if len(parts) != 4:
                raise RuntimeError(f"Malformed baseline line: {line!r}")
            entries.add(tuple(parts))
    return entries


def write_baseline(path, findings):
    header = [
        "# Copyright 2026 Stephan Schlöpke",
        "# SPDX-License-Identifier: Apache-2.0",
        "#",
        "# Known C#/Swift naming differences that implementation/PortMapping.md",
        "# phases remove. Generated by scripts/verify-port-names.py --update-baseline.",
        "# Format: category<TAB>port<TAB>module<TAB>detail",
    ]
    lines = sorted(format_finding(finding) for finding in findings)
    path.write_text("\n".join(header + lines) + "\n", encoding="utf-8")


def verify(root, baseline_path):
    """Return (new findings, stale baseline entries)."""
    findings = collect(root)
    baseline = read_baseline(baseline_path)
    return sorted(findings - baseline), sorted(baseline - findings)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--root", type=Path, default=ROOT)
    parser.add_argument("--baseline", type=Path, default=BASELINE)
    parser.add_argument("--update-baseline", action="store_true", help="Rewrite the baseline from the current sources")
    parser.add_argument("--list", action="store_true", help="Print all current findings, including baselined ones")
    args = parser.parse_args(argv)

    if args.update_baseline:
        findings = collect(args.root)
        write_baseline(args.baseline, findings)
        print(f"Wrote {len(findings)} baseline entries to {args.baseline}")
        return 0
    if args.list:
        for finding in sorted(collect(args.root)):
            print(format_finding(finding))
        return 0

    new, stale = verify(args.root, args.baseline)
    for finding in new:
        print("new difference: " + format_finding(finding), file=sys.stderr)
    for finding in stale:
        print("resolved difference still in baseline: " + format_finding(finding), file=sys.stderr)
    if new or stale:
        print("Fix the naming, or run scripts/verify-port-names.py --update-baseline after a deliberate change.", file=sys.stderr)
        return 1
    print(f"Port names verified ({len(read_baseline(args.baseline))} baselined differences remain).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
