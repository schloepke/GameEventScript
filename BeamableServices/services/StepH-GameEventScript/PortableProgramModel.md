# Portable Program Model

This document defines the language-neutral ownership and data contract of
`GameEventScriptProgram`. `GesbFormatV1.md` remains the normative byte encoding;
this document defines which information may exist in the parsed in-memory model.

## Role

A program is an immutable parsed representation of one `.gesb` file. It contains
transportable compiler output, not an executable host binding. The same program
may be retained, read concurrently, and loaded into multiple independent hosts.

Every program value must have a lossless language-neutral representation in
`.gesb`. A port may choose different classes, structs, arrays, slices, or other
storage strategies, but these implementation choices must not add observable
program state.

## Permitted data

The complete program graph consists only of:

- the `.gesb` format version and program metadata;
- fixed-width integer fields and explicitly numbered enum-like values;
- UTF-8 string-pool bytes and immutable slice metadata;
- immutable `u16` index lists;
- immutable binding records;
- immutable instruction words with the logical fields defined by
  `GesbFormatV1.md`;
- optional immutable debug symbols, source-map records, source archive bytes,
  and build metadata;
- optional opaque-section metadata plus its retained raw bytes.

Strings in the C# model are immutable decoded Unicode values. Source archive and
opaque payloads remain immutable bytes. Source-map hashes are the specified
SHA-256 bytes of logical UTF-8 source content; runtime or platform hash codes are
never program data.

## Forbidden data

A program must never contain or retain:

- host, context, instance, subscription, queue, observer, or publish-sink state;
- VM state, active execution frames, registers, random state, budgets, or runtime
  caches;
- extension registries, external-type runtime registries, executable callbacks,
  delegates, function pointers, reflection objects, or arbitrary platform
  objects;
- AST nodes, parser tokens, compiler passes, mutable builders, or other compiler
  working state;
- platform-dependent object identities, addresses, native handles, default hash
  codes, timestamps, hostnames, usernames, or absolute build paths.

Declarative extension and external-type references are allowed only through the
portable names, signatures, IDs, and table entries encoded in `.gesb`. Their
executable implementations belong to the host-specific linked program created by
`Host.Load`.

## Ownership and immutability

Program construction takes a snapshot of all supplied sequences. The program and
every nested segment or entry own immutable copies of their table and payload
data. No mutable array or collection owned by a program may be exposed to a
caller or another Core component.

The C# implementation enforces this through `GameEventScriptReadOnlyArray<T>`:

- construction copies the supplied sequence;
- public access is read-only and indexed;
- internal bulk reads use `ReadOnlySpan<T>` and never expose the backing array;
- nested reference entries are themselves immutable and defensively copy their
  byte and index payloads;
- `GameEventScriptBytecodeInstruction` is returned by value, so its mutable C#
  struct representation cannot mutate the stored instruction table.

After a program has been returned by the compiler or reader, no operation may
change its observable data. Decoded strings, resolved imports, ID-indexed tables,
scalar-count tables, and other accelerators are linked-program data and must not
be cached back into the program.

Concurrent read access to a program is permitted. This does not make a host or a
VM thread-safe; their synchronization contracts are separate.

## Representation independence

The `.gesb` payload fields define the portable meaning. C# explicit struct
layout, CPU endianness, padding, object layout, and overlapping field aliases are
implementation details only. Other language ports need not reproduce them in
memory.

In particular, the C# 16-byte explicit-layout instruction is a compact runtime
view. Serialization and deserialization read and write the individual V1 fields
in canonical little-endian order. Raw CLR struct bytes are never a valid shortcut
for `.gesb` encoding.

## Preservation and roundtrip meaning

All known retained segments are parsed into the immutable model and are encoded
again from that model. Unknown optional sections retained by `PreserveAll` keep
their payload bytes and section metadata. `PreserveKnown` and `RuntimeOnly`
intentionally discard sections according to their documented retention policy;
discarded data is not part of the resulting program.

For every resulting program, the writer must be able to encode all retained
program data. Reading those canonical bytes must reconstruct the same portable
meaning. Byte identity is required only where `GesbFormatV1.md` or a canonical
fixture explicitly requires it.
