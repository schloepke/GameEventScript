# Portable public API specification

> [!IMPORTANT]
> **Structural draft.** This document owns the normative language-neutral public API contract but is not yet complete.

## Scope

This specification defines the responsibilities, ownership, lifetime, nullability, mutability, synchronization, errors, and allocation requirements of the portable compiler, program, host, value, messaging, extension, external-type, binary, diagnostic, and conformance APIs.

Concrete C#, Swift, Kotlin, and C++ APIs may use idiomatic names and type shapes. Their observable semantics must remain equivalent. Host state transitions belong to [Host runtime](HostRuntime.md), transport-only program data to [Program model](ProgramModel.md), and `.gesb` encoding to [Binary format](BinaryFormat.md).

