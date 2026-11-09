<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Plan: generated schema code (`ges schema generate`)

Status: accepted; phases 1 to 3 are implemented, phase 4 is next. This plan is removed when the feature lands; the normative
rules then live in `specs/Schemas.md`.

Variant B of the schema API: typed code per schema with inlined constants, next to the
interpreted writer and reader (variant A), which stay unchanged in behavior.

## Decisions

| Topic | Decision |
| --- | --- |
| Parameters | Bound at generation (`--param`). Widths, ranges and quantization are constants; the binding hash is a constant of the generated code. A map-dependent size is generated with its largest value; deltas use the delta range and do not depend on it. Binding parameters at run time is [deferred](#later). |
| Wire values | Equality and `WriteDelta` compare wire values (indices and stored bits), never raw doubles. `Quantize()` rounds an instance to its wire values. |
| Failed reads | After an error the target instance is unspecified and must not serve as a baseline; the baseline is never changed. |
| Variable data | Storage owned by the generated instance that grows on demand up to `max` and is then reused. Nothing is allocated in the constructor; once an instance has held its largest data, writing and reading allocate nothing. |
| Scope | Full records and deltas, including keyed lists, in the same feature. |
| Languages | C# 9 (Unity) and Swift first; Kotlin, Rust and Go later through the same layout. |
| Generator | A separate Generator package in both ports, never a dependency of Runtime or Compiler, so it is never shipped with a game. Both CLIs generate every target language, with identical layout and identical text. |
| Further CLIs | None planned. Ports for Kotlin, Rust or Go start with their runtime libraries; their code comes from the Generator. Whether a port gets a CLI is decided when it is created. |
| Shape | `struct` for schemas with only fixed-width fields, `sealed class`/`final class` otherwise. |
| Unions | One member per alternative. Alternatives with storage stay small until used, because storage grows on demand. |
| Bit primitives | Variant A's writer and reader move onto the new public primitives, provided measurement shows no loss. |
| Burst | Not planned; the C# output stays managed. |

## Command

```bash
ges schema generate world.gess panel.gess --root World_Bot --root GameAnswers \
    --param worldWidth=2000m --param worldHeight=2000m \
    --language csharp --namespace Game.Net -o Wire.g.cs
ges schema generate world.gess panel.gess --all-roots --param ... --language swift --namespace Wire -o Wire.swift
ges schema layout world.gess panel.gess --all-roots --param ... -o wire.layout.json
```

Both CLIs accept every target language; `dotnet ges` and `ges` produce identical files.

- `--root` may be repeated; `--all-roots` selects every schema and message set declared in the
  given files. Every type reached from any root is generated exactly once, so a shared type such
  as `:Panel` or `:World_Radar` appears once in the output. This is sound because parameters
  belong to files, not to references: a schema is bound identically wherever it is reached.
- A message set generates its members and one entry point that reads the member index.
- `--namespace` applies to both languages: a C# `namespace`, and in Swift an enclosing
  `public enum` (one nested enum per dotted component), so that generated names such as
  `World` or `Panel` cannot collide with types of the app.
- `--access public|internal` sets the visibility of the generated types (default `public`).
- `--values` additionally generates the conversion to and from GES values (see below).
- `--check` generates in memory and compares with the existing output file instead of writing
  it: equal exits with 0, a difference or a missing file fails with a message naming the file,
  so that tests and CI can verify checked-in output.
- Output starts with a header naming the tool, the roots, the parameters, and per root the
  schema hash and the binding hash. It contains no date or version, so a regeneration is
  reproducible.
- The same rules as `hash` apply: a `--param` that no root reaches is `schema.unknownName`.

## Layout and emitters

Generation is split into two steps, so that the target languages share everything except
the final text:

1. **Layout.** Both CLIs compute a language-neutral generation layout from the bound schemas:
   the deduplicated types in a stable order, their declared names (each emitter converts them
   to its language), every field with its
   encoding, width, bounds, quantization constants (exact integers and binary64 bit patterns),
   key, optional and change-bit structure, delta ranges and keyed-list sections, plus the
   hashes and `MaxByteCount` per type. `ges schema layout` writes it as canonical JSON. Its
   format is normative (a new section of `specs/Schemas.md`) and versioned by a `layout`
   number; both CLIs must produce byte-identical JSON for every corpus schema.
   Every derived constant (width, bounds, quantization, `MaxByteCount`, binding hash) records
   the parameters it depends on, as a possibly empty sorted list of names. Generation ignores
   these lists today; they let a later run-time binding of parameters extend the layout without
   changing its format.
2. **Emitters.** An emitter is a pure function from the layout to source text. It does not see
   the schema source, the binder or any port internals, so its behavior is fully determined by
   the parity-checked layout.

Both ports implement the layout and every emitter, like the two compilers: the layout and the
text of every emitter must be identical between them. A further target language adds one
emitter to each Generator package. The layout JSON also lets build integrations and later
tools consume a generation without a Generator of their own.

## Modules

- **Runtime** gains the public primitives the generated code calls, in both ports:
  - `GesBitWriter` and `GesBitReader`: bit-exact writing and reading over a caller's buffer, with
    the padding of `Finish`, a byte limit, and the positions a delta encoder needs (mark,
    rewind, set a bit). C# `ref struct`s over spans; Swift structs over raw buffer pointers.
  - Public construction of `GesWireException` / `GesWireError` with code and path, so generated
    code reports the same codes and paths as variant A.
  - UTF-8 and tag-name validation over bytes, and the quantization helpers of
    `GesSchemaNumbers` that generated code calls with constant operands.
  These primitives contain no schema knowledge.
- **Generator** is a new package in both ports (C# `GameEventScript.Generator`, Swift
  `GameEventScriptGenerator`) that depends on Syntax and Runtime only. It computes the layout
  (`GesSchemaLayout`) from the bound schema through internal access (C# `InternalsVisibleTo`,
  Swift `@_spi(Generator)`) and holds the emitters (`GesSchemaEmitter` per language). Runtime,
  Compiler and the Bridges never depend on it, so a game that ships the Compiler for scripts does
  not ship the Generator. No public layout API is added to Runtime. It becomes a public package
  (NuGet package and SwiftPM product) so that build integrations, such as a Unity editor menu,
  an MSBuild target or a SwiftPM plugin, can generate without the CLI.
- **Tool** adds the `layout` and `generate` subcommands in both CLIs on top of the Generator.

## Generated shape

Names follow the language: C# PascalCase members, Swift lowerCamelCase.

| Schema part | C# 9 | Swift |
| --- | --- | --- |
| Schema with only fixed-width fields | `struct` | `struct` |
| Schema holding Text, List or Dice, directly or nested | `sealed class` | `final class` |
| Boolean | `bool` | `Bool` |
| Integer field | `long` | `Int64` |
| Quantized or non-quantized Number, Quantity, Percentage | `double` | `Double` |
| Closed Tag set | generated enum on the smallest unsigned type holding the index: `byte`, `ushort`, `uint` | `UInt8`, `UInt16`, `UInt32` |
| `:Tag max n` | UTF-8 bytes up to `n` and a length | same |
| Text `max n` | UTF-8 bytes up to `n` and a length | same |
| List / Dice `max n` | elements up to `n` and a count | same |
| `optional` | `bool HasName` next to the value | `hasName` |
| Union | the alternative index as a generated enum and one member per alternative | same |
| Point, Vector, nested schema | nested struct or class | same |

A class rather than a struct when storage is involved: copying a C# struct would share the
arrays, and mutating a copied Swift array would allocate through copy-on-write.

Generated names come from the layout. The Generator converts schema, field, tag and member
names to the target spelling, escapes keywords (C# `@`, Swift backticks) and rejects two names
that become equal in one scope with a generation error naming both.

In Swift, generated structs and enums conform to `Sendable` when all their members are
`Sendable`, which holds for every fixed-width schema; classes are not `Sendable`. The output
compiles in the Swift 6 language mode with complete concurrency checking.

Storage grows on demand: a Text or List starts empty and grows (doubling, capped at `max`) when
reading or setting needs more room; element instances of a list of classes are created when
first needed and kept. Nothing shrinks. `Reserve(...)` lets a caller pay the growth up front,
for example when loading a map. A full `:Panel` therefore costs memory only for the cells and
text it has actually held.

Every generated type has:

- `SchemaHash`, `BindingHash` and `MaxByteCount` as constants;
- `Write(buffer) -> bytes` and `Read(data, byteLimit) -> bytes consumed` into the existing instance,
  over C# spans and Swift raw buffer pointers;
- `WriteDelta(baseline, buffer) -> bytes` and `ReadDelta(data, baseline, byteLimit) -> bytes`
  into the existing instance, which must be another instance than the baseline;
- for a schema with a key, `static ReadKey(data)`;
- `CopyFrom(other)` for classes, and value equality of the wire values (`Equals` in C#, `==` in Swift);
- `Quantize()`, which replaces every quantized value by the value its index decodes to, so that
  a sender holds exactly what a receiver reads.

Raw members serve the hot path. Next to them, convenience members may allocate and are meant
for app code outside it:

- Text and `:Tag max n` as `string` / `String` (get and set; setting validates like writing);
- List and Dice as a read-only view: C# `IReadOnlyList<T>`, Swift `RandomAccessCollection`;
- an `optional` field as `T?` in C# and an Optional in Swift (get and set; `null`/`nil` is absent);
- writing to and reading from byte arrays: C# `byte[] ToBytes()` (reading takes a `byte[]`
  through the span overload), Swift `encoded() -> [UInt8]` and reading from `[UInt8]`. Swift
  also gains `Span`/`OutputSpan` overloads if the deployment targets of the root package allow
  them; phase 1 decides this.

With `--values`, every generated record type also converts to and from a GES Record value, and
every message type and message set to and from `GameEventScriptMessage`, with the mapping of
the [value path](../../specs/Schemas.md#value-path). The conversion allocates; it is for messages
that are created in scripts, not for the hot path.

## Semantics

Generated code is another implementation of the same contract, not a new one:

- **Wire values.** Equality and `WriteDelta` compare what is transmitted: the index of a
  quantized or wrapping value, an integer, the stored bits of a non-quantized number, the
  bytes of a Text. A sender that keeps a written instance as its baseline (`CopyFrom`) therefore
  computes the same deltas as against the receiver's decoded copy, and rounding errors cannot
  accumulate. `GesSchemaDelta` already compares wire values; generated code matches it.
- **Failed reads.** When `Read` or `ReadDelta` reports a `wire.*` error, the target instance is
  unspecified: it stays memory-safe (counts within capacity, valid union index), but its values
  are a mix of old and new and it must not serve as a baseline. The caller requests a full
  record. `ReadDelta` never modifies the baseline, which must be another instance.

- the same bytes as variant A for every valid record, and as `GesSchemaDelta` for every delta,
  including the canonical delta form and the three sections of keyed lists;
- the same validation of untrusted data, with the same `wire.*` code and field path at the same
  point; a delta is checked against the baseline in wire order;
- writing checks values like variant A (ranges, Text and tag validity, counts), with the same
  codes;
- after warm-up, success allocates nothing and boxes nothing, in C# under Mono and IL2CPP and in
  Swift;
- `--values` conversions give the same values and messages as `GesSchemaValues` decoding the
  same bytes.

Constants are emitted as exact literals: Int64 bounds and widths as integers, binary64 values
as bit patterns, so that every target language computes the same quantization as the
interpreted path.

## Verification

- **Layout parity.** A script computes the layout of every corpus schema with both CLIs and
  requires identical JSON, and generates every language with both CLIs and requires identical
  text, like `compare-compiler-output.py`. Checked-in golden files of selected schemas make
  changes of the generated text visible in review.
- **Corpus as oracle.** A harness per port collects the `schemaApi` cases that encode, decode or
  transcode records, generates their schemas into one C# assembly and one Swift executable,
  and checks the corpus expectations: bytes, consumed lengths and error codes with paths. Cases
  whose call sequence only variant A can express (`wire.fieldOrder`) are excluded by kind.
- **Roundtrip and fuzzing.** `test-schema-roundtrip.py` gains generated code as a third and fourth
  party: random records and deltas, and fuzzed bytes, must give identical results to variant A in
  both ports. `--values` conversions are compared with `GesSchemaValues` on the same records.
- **Compilation gates.** Generated C# compiles against the Unity 6000.3 reference assembly with
  C# 9 and without `unsafe`; generated Swift compiles with the repository's Swift toolchain
  without Foundation, in the Swift 6 language mode with complete concurrency checking, and a
  test requires `Sendable` exactly for the fixed-width types. Two roots sharing a type, a Swift
  namespace, keyword names and a tag set beyond 256 tags are part of the gate.
- **Check mode.** CLI tests cover `generate --check` for an equal, a different and a missing file.
- **Allocation.** The allocation gates of both ports cover generated write, read, delta write and
  delta read after warm-up.
- **Performance.** A measurement against variant A on the Bot and a keyed fleet decides whether
  the gain justifies the feature as specified; it is reported, not yet a budget. The same
  measurement decides whether variant A moves onto the primitives.

## Phases

Each phase is one reviewable change and keeps all gates green.

1. Runtime primitives in both ports, public API snapshots, native tests; measurement of variant
   A on the primitives and, without loss, the move.
   Done: `GesBitWriter`, `GesBitReader`, `GesWireRules` and public wire error codes. Variant A
   moved onto them; a Bot write and read and a delta of 48 keyed Bots measured 5 % faster in C#
   and 2 to 4 % faster in Swift than before.
2. Generator package in both ports, layout format and `ges schema layout` with the parity
   script (done: `GesSchemaGenerator.Layout`, `ges schema layout`, `compare-schema-generation.py`,
   format in `specs/Schemas.md#generation-layout`), then the C# and Swift emitters in both ports
   for full records of fixed-width
   schemas (Boolean, numbers, tags, key, nesting, Point, Vector, union, optional), multiple
   roots and namespaces, name conversion, `Sendable`, byte-array conveniences,
   `generate --check`, and the corpus harness (done: `GesSchemaGenerator.Generate`,
   `ges schema generate`, `test-generated-schemas.py`).
3. Text, `:Tag max n`, List and Dice with on-demand storage and convenience members; message sets.
   Done: lists are C# `List<T>` and Swift arrays that keep their capacity, list elements of
   classes come from a pool, union, Text and List elements get item types, messages get a Boolean
   per tag, and message sets hold `Selected` and one record per message. `Reserve(...)` is not
   needed: a caller fills a record once with its largest data. `CopyFrom` moves to phase 4.
4. Deltas without keyed lists: change bits, delta ranges, wrapping, unions, optional fields;
   wire-value equality and `Quantize()`, which deltas compare by.
5. Keyed lists in deltas.
6. `--values`, roundtrip and fuzzing parties, allocation gates, measurement, `specs/Schemas.md`,
   guide, CHANGELOG, PublicApi, Generator package distribution (NuGet, SwiftPM product,
   `docs/guide/distribution/Packages.md`, AGENTS.md), removal of this plan and of the backlog entry.

## Later

Recorded here so that the layout format and the generated shape leave room for it; built only
on real demand.

- **Run-time parameters.** `--dynamic-param name` binds only the named parameters at run time
  through a generated binding value (created from `GesSchemaParameters` and checked against the
  layout); every other derived constant stays a literal. The dependency lists of the layout name
  exactly the constants that become fields of that binding value. The measurement against
  variant A then shows its price; the gain of generated code does not come from inlined
  constants alone, but also from the absence of allocations, name lookups and `GesValue` trees.
