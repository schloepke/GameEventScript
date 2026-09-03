# `.gesb` binary format specification

> [!IMPORTANT]
> **Structural draft.** This document owns the normative portable binary container contract but is not yet complete.

## Scope

This specification defines file and section framing, Little Endian encoding, segment IDs and payloads, versions, limits, opaque retention, canonical writing, decoding, structural validation, and reserved extension ranges.

The in-memory immutable representation belongs to [Program model](ProgramModel.md). Opcode meaning belongs to [Bytecode](Bytecode.md). Debug rendering belongs to [Assembler format](AssemblerFormat.md).

