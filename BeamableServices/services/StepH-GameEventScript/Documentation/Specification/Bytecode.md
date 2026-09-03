# Bytecode specification

> [!IMPORTANT]
> **Structural draft.** This document owns the normative executable bytecode contract but is not yet complete.

## Scope

This specification defines the logical instruction layout, opcode IDs, operand roles, flags, units, value kinds, control flow, calls, resource metadata, validation rules, and execution semantics.

The `.gesb` byte encoding belongs to [Binary format](BinaryFormat.md). Human-readable rendering belongs to [Assembler format](AssemblerFormat.md). Numeric behavior referenced by instructions belongs to [Number semantics](Semantics/Numbers.md).

