# `.gesa` assembler format specification

> [!IMPORTANT]
> **Structural draft.** This document owns the normative human-readable Program dump format but is not yet complete.

## Scope

This specification defines `.gesa` directives, segments, regions, source blocks, source-line annotations, labels, bindings, instruction syntax, symbolic register names, comments, escaping, and canonical dump presentation.

`.gesa` is diagnostic and approval-oriented text, not the executable transport format. Executable encoding belongs to [Binary format](BinaryFormat.md), and opcode semantics belong to [Bytecode](Bytecode.md).

