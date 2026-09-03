# Portable program model specification

> [!IMPORTANT]
> **Structural draft.** This document owns the normative immutable Program object graph but is not yet complete.

## Scope

This specification defines which language-neutral data a Program may contain, immutability and defensive-copy requirements, identity and version metadata, validation boundaries, reusable ownership, and the separation between serialized Program data and host-specific linking.

Binary representation belongs to [Binary format](BinaryFormat.md). Runtime linking and execution belong to [Host runtime](HostRuntime.md). Instruction semantics belong to [Bytecode](Bytecode.md).

