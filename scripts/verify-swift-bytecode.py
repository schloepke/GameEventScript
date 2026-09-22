#!/usr/bin/env python3
# Copyright 2026 Stephan Schlöpke
# SPDX-License-Identifier: Apache-2.0

"""Fail on drift between the explicit Swift and C# V1 enum/operand registries."""
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
CS = ROOT / "implementation/csharp/GameEventScript.Runtime/src/Api"
SWIFT = ROOT / "implementation/swift/GameEventScriptRuntime/Sources/GameEventScriptRuntime"


def lower(name):
    return name[0].lower() + name[1:]


def enum_values(text, name, swift=False):
    # These registries contain numeric enum declarations. Documentation can
    # contain braces or example assignments and must not delimit the enum body.
    text = re.sub(r"//[^\n]*", "", text)
    match = re.search(r"enum " + name + r"\b[^\{]*\{([^}]+)\}", text, re.S)
    if not match:
        raise RuntimeError(f"Missing enum {name}")
    pattern = r"case `?(\w+)`?\s*=\s*(0x[\da-fA-F]+|\d+)" if swift else r"^\s*(\w+)\s*=\s*(0x[\da-fA-F]+|\d+)"
    return {(key if swift else lower(key)): int(value, 0) for key, value in re.findall(pattern, match[1], re.M)}


def main():
    csharp = (CS / "GameEventScriptProgram.cs").read_text() + (CS / "GameEventScriptProgramFormat.cs").read_text()
    swift = (SWIFT / "Bytecode.swift").read_text() + (SWIFT / "ProgramFormat.swift").read_text()
    names = re.findall(r"public enum (\w+): (?:UInt\d+|Int),", swift)
    for name in names:
        expected, actual = enum_values(csharp, name), enum_values(swift, name, True)
        if not expected or expected != actual:
            raise RuntimeError(f"{name} identifier drift: C#={expected}; Swift={actual}")
    printer = (CS / "GameEventScriptOpcodePrinter.cs").read_text()
    expected = {
        lower(op): [lower(part.strip()) for part in parts.split(",") if part.strip()]
        for op, parts in re.findall(r"GameEventScriptBytecodeOpCode\.(\w+) => \[([^\]]*)\]", printer)
    }
    actual = {
        op: re.findall(r"\.(\w+)", parts)
        for op, parts in re.findall(r"case \.`?(\w+)`?:\s*\[([^\]]*)\]", (SWIFT / "BytecodeOperands.swift").read_text())
    }
    if expected != actual:
        changed = sorted(k for k in expected.keys() | actual.keys() if expected.get(k) != actual.get(k))
        raise RuntimeError(f"Opcode operand drift: {changed}")
    # Pattern operand arity depends on the instruction payload. Verify the three
    # C# forms and the explicit Swift base plus CountAny/CountFace additions.
    for opcode in ("HasPattern", "TakePattern"):
        block = printer.split("GameEventScriptBytecodeOpCode." + opcode + " =>", 1)[1].split(",\n\n", 1)[0]
        forms = re.findall(r"\[([^\]]*)\]", block)[:3]
        if forms != ["TargetRegister, IteratorRegister, PatternKind, CountImmediate, FaceRegister", "TargetRegister, IteratorRegister, PatternKind, CountImmediate", "TargetRegister, IteratorRegister, PatternKind"]:
            raise RuntimeError(f"Changed dynamic pattern operands: {opcode}")
    operands = (SWIFT / "BytecodeOperands.swift").read_text()
    validator = (SWIFT / "ProgramValidator.swift").read_text()
    for required in ("case .hasPattern, .takePattern: [.targetRegister, .iteratorRegister, .patternKind]",):
        if re.sub(r"\s+", "", required) not in re.sub(r"\s+", "", operands):
            raise RuntimeError("Swift pattern base operands changed")
    for required in ("if a == 1 { return opcode.operands + [.countImmediate, .faceRegister] }", "if a == 0 { return opcode.operands + [.countImmediate] }"):
        if re.sub(r"\s+", "", required) not in re.sub(r"\s+", "", validator):
            raise RuntimeError("Swift dynamic pattern operands changed")
    # Result-bearing sends use the same eight flag-dependent layouts in both ports.
    send_block = printer.split("return (indirect, tags, after) switch", 1)[1].split("};", 1)[0]
    forms = re.findall(r"\((true|false), (true|false), (true|false)\) => \[([^\]]*)\]", send_block)
    if len(forms) != 8:
        raise RuntimeError("Missing result-send operand forms")
    for indirect, tags, after, parts in forms:
        layout = ["TargetRegister"] + (["MessageRegister"] if indirect == "true" else ["OutboundMessage", "ArgumentRegisterList"])
        if tags == "true": layout.append("TagRegisterList")
        if after == "true": layout.append("AuxBRegister")
        if [p.strip() for p in parts.split(",")] != layout:
            raise RuntimeError("Changed result-send operands")
    for required in (
        "var result: [GesOperand] = [.targetRegister]",
        "result += unitAndFlags & 0x80 != 0 ? [.messageRegister] : [.outboundMessage, .argumentRegisterList]",
        "if unitAndFlags & 0x40 != 0 { result.append(.tagRegisterList) }",
        "if opcode == .emitAfter || opcode == .publishAfter { result.append(.auxBRegister) }",
    ):
        if re.sub(r"\s+", "", required) not in re.sub(r"\s+", "", validator):
            raise RuntimeError("Swift result-send dynamic operands changed")
    print(f"Verified {len(names)} V1 enums and {len(expected) + 6} opcode operand definitions against C#.")


if __name__ == "__main__":
    main()
