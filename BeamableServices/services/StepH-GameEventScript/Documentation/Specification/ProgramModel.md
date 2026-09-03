# Portable program model specification

This document defines the language-neutral ownership and data contract of
`GameEventScriptProgram`. [Binary format](BinaryFormat.md) defines the normative byte encoding;
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
  [Binary format](BinaryFormat.md);
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

## Construction and validation boundaries

The portable public API has exactly two program-producing operations:

- the compiler materializes a program from validated source and runs the shared
  complete program validator before returning it;
- the reader performs bounded structural decoding and runs the same complete
  program validator before returning the decoded program.

There is no public free-form `GameEventScriptProgram` constructor or public
factory that combines arbitrary segments. Public segment constructors are data
helpers only and cannot create a program. Internal compiler builders and
test-only friend access are implementation details and do not extend the
portable construction contract.

Validation is deliberately repeated at output and execution trust boundaries:

- the writer validates before calculating or writing the canonical encoding;
- `Host.Load` validates before checking host resource limits, resolving any
  executable binding, allocating VM capacity, registering handlers, or queuing
  initialization.

This repetition is intentional. A writer and a host must not rely on the claimed
origin of a program, on a prior validation performed by another component, or on
language-specific visibility rules. Validation failure leaves the destination
buffer and host state unchanged.

## Representation independence

The `.gesb` payload fields define the portable meaning. C# struct layout, CPU
endianness, padding, object layout, and overlapping field aliases are
implementation details only. Other language ports need not reproduce them in
memory.

In particular, the C# instruction stores three numeric `u16` words and one
numeric `u64` payload. Semantic aliases use casts, shifts, masks, and exact
Binary64 bit conversion rather than overlapping CLR fields. Its sequential
16-byte size is a compact runtime optimization only. Serialization and
deserialization read and write the individual V1 fields in canonical
little-endian order. Raw CLR struct bytes are never a valid shortcut for `.gesb`
encoding.

All identifiers carried by program bytes have fixed numeric values: section
types and flags, binding kinds, debug symbol kinds, opcodes, instruction units
and flags, bytecode type kinds, pattern kinds, and series kinds. A source-language
enum declaration is only a convenient implementation view of those IDs; ordinal
position or declaration order is never an encoding rule.

## Preservation and roundtrip meaning

All known retained segments are parsed into the immutable model and are encoded
again from that model. Unknown optional sections retained by `PreserveAll` keep
their payload bytes and section metadata. `PreserveKnown` and `RuntimeOnly`
intentionally discard sections according to their documented retention policy;
discarded data is not part of the resulting program.

For every resulting program, the writer must be able to encode all retained
program data. Reading those canonical bytes must reconstruct the same portable
meaning. Byte identity is required only where [Binary format](BinaryFormat.md) or a canonical
fixture explicitly requires it.

## Regression gates

Every implementation must cover the semantic equivalents of these gates:

- mutating any input sequence after segment or program construction cannot alter
  retained program data;
- the public API cannot freely construct an arbitrary program graph;
- `compile -> write -> read -> canonical rewrite -> load -> execute` preserves
  both encoded meaning and runtime behavior;
- primary word aliases, signed views, four payload words, signed `i64`, and exact
  Binary64 bits have the numeric relationships specified by `.gesb` regardless
  of native memory byte order;
- canonical fixtures protect encoded bytes and the shared numeric definition
  protects every serialized enum-like ID.

The C# port additionally snapshots its public API and verifies its compact
instruction size. Those two checks are implementation-specific and are not
requirements for the in-memory representation used by another language.
