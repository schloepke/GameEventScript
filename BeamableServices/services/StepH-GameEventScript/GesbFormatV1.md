# `.gesb` portable binary format, version 1

This document is the normative container specification for `GameEventScriptProgram`.
Opcode semantics and operand shapes remain normative in `BytecodeSpec.md` and
`BytecodeOpcodeShape.md`. A program contains portable data only. Linking extensions,
external types, delegates, runtime caches, registries, and VM state is exclusively a
`GameEventScriptHost.Load` responsibility.

## Integer encoding and framing

All multi-byte integers use little endian. Sections are contiguous and have no
implicit padding or alignment.

The file starts with this fixed 16-byte header:

| Offset | Size | Field | V1 value |
|---:|---:|---|---|
| 0 | 4 | Magic | ASCII `GESB` |
| 4 | 2 | Version | `1` |
| 6 | 2 | Flags | `0`, reserved |
| 8 | 4 | HeaderSize | `16` |
| 12 | 4 | FileSize | exact total byte count |

Every following section starts with a 12-byte header:

| Offset | Size | Field |
|---:|---:|---|
| 0 | 2 | SectionType |
| 2 | 2 | SectionFlags |
| 4 | 2 | SectionVersion |
| 6 | 2 | Reserved, zero |
| 8 | 4 | PayloadLength |
| 12 | n | Payload |

`SectionFlags & 0x0001` is `Required`. `SectionFlags & 0x00F0` is the
compression-codec field. Codec zero means uncompressed. V1 writers emit only codec
zero for known sections. A loader rejects unsupported compression on a required
section. It may preserve or skip an optional unsupported section according to its
retention mode. All remaining flag bits are invalid in V1.

## Section registry and canonical order

| ID | Name | Cardinality | Required |
|---:|---|---:|---:|
| `0x0001` | ProgramMetadataSegment | exactly one | yes |
| `0x0002` | StringConstantSegment | exactly one | yes |
| `0x0003` | UInt16IndexListSegment | exactly one | yes |
| `0x0004` | BindingSegment | exactly one | yes |
| `0x0010` | CodeSegment | exactly one | yes |
| `0x0020` | DebugSymbolsSegment | zero or one | no |
| `0x0021` | SourceMapSegment | zero or one | no |
| `0x0022` | SourceArchiveSegment | zero or one | no |
| `0x0030` | BuildMetadataSegment | zero or one | no |
| `0x0040`–`0x004F` | future security/signature sections | reserved | undefined |
| `0x8000`–`0xFFFD` | private/experimental | any | no |
| `0xFFFE` | future named custom section | reserved | no |

Types `0x0000` and `0xFFFF` are invalid. V1 has one linear code address space and
therefore exactly one CodeSegment. Multiple programs are combined by separate
`Host.Load` calls, not by multiple code sections.

The canonical writer emits the known sections in the table order above, followed by
opaque optional sections in their original relative order. Known sections are always
encoded from the parsed program model; their original raw bytes are not retained.

## Required payloads

ProgramMetadataSegment version 1 has a fixed 16-byte payload:

```text
ModuleNameStringIndex    u16
RequiredRegisterCount   u16
RequiredCallStackDepth  u16
Reserved                u16 = 0
ProgramVersion          u64
```

`ProgramVersion` is always present. Zero means development or unspecified. The
module name is a stable program identity and must exist in StringConstantSegment.

StringConstantSegment:

```text
EntryCount u32
repeat EntryCount:
    ByteLength u32
    Utf8Bytes  byte[ByteLength]
```

UTF-8 is strict. Ordering and duplicates are semantic program data. At most 65,535
entries are allowed because bytecode indexes are `u16`.

UInt16IndexListSegment:

```text
ListCount u32
repeat ListCount:
    ElementCount u32
    Elements     u16[ElementCount]
```

At most 65,535 lists are allowed. Lists carry argument names, tags, register lists,
message shapes, and similar indexed operands; their element interpretation depends
on the referencing field or opcode.

BindingSegment:

```text
EntryCount u32
repeat EntryCount:
    Kind                    u8
    Flags                   u8 = 0
    Id                      u16
    NameStringIndex         u16
    ArgumentNamesListIndex  u16
    RequiredTagsListIndex   u16
    ExcludedTagsListIndex   u16
    EntryAddress            u16
    RequiredRegisterCount   u16
    RequiredCallStackDepth  u16
    Reserved                u16 = 0
```

`0xFFFF` is the missing ID/list/address sentinel. Executable binds require a valid
entry address; non-executable binds use the sentinel. The program-level resource
values equal the maxima of the handler metadata. IDs are globally unique per kind,
except handler IDs, which are overload ordinals scoped to their message name.

CodeSegment:

```text
InstructionCount u32
repeat InstructionCount:
    OpCode       u8
    UnitAndFlags u8
    Word0        u16
    Word1        u16
    Word2        u16
    Payload      u64
```

Each instruction is exactly 16 encoded bytes. These fields, not CLR layout, define
the file representation. Payload preserves integer bits, IEEE-754 binary64 bits, or
four overlaid `u16` words. V1 permits at most 65,535 instructions.

## Debug and source payloads

DebugSymbolsSegment has its own name pool, followed by fixed symbol records:

```text
NameCount u32
repeat NameCount: ByteLength u32, Utf8Name byte[ByteLength]
SymbolCount u32
repeat SymbolCount:
    SymbolKind u8          // 1 parameter, 2 local
    Reserved u8 = 0
    RegisterId u16
    NameIndex u32
    CodeStart u32
    CodeLength u32
```

Only explicit script parameters and locals are named. IDs are physical registers
after final allocation. Multiple non-overlapping ranges may later name the same
physical register; compiler temporaries remain unnamed.

SourceMapSegment uses stable source IDs assigned in `AddScript` order:

```text
SourceCount u32
repeat SourceCount:
    SourceNameLength u32, SourceName UTF-8
    SourceByteLength u32
    Sha256 byte[32]
    LineCount u32
    LineStartByteOffsets u32[LineCount]
MappingCount u32
repeat MappingCount:
    CodeStart u32
    CodeLength u32
    SourceId u32
    SourceStartByteOffset u32
    SourceByteLength u32
```

The source-table index is its stable `SourceId`; compiler inputs therefore receive
IDs in `AddScript` order. All source offsets address uncompressed UTF-8 bytes. The
first line offset is zero; subsequent offsets are strictly increasing UTF-8
boundaries and may equal the source length for a final empty line. SourceMap is
useful without an embedded source.

SourceArchiveSegment is independent:

```text
SourceCount u32
repeat SourceCount:
    SourceId u32
    SourceNameLength u32, SourceName UTF-8
    ContentByteLength u32
    Utf8Content byte[ContentByteLength]
```

Content is the exact logical compiler input encoded as UTF-8. Original BOM and file
encoding are not preserved. If map and archive are both present, IDs, names, byte
lengths, SHA-256 values, line boundaries, and UTF-8 validity must agree.

BuildMetadataSegment contains exactly two required length-prefixed UTF-8 strings:

```text
CompilerIdLength u32, CompilerId UTF-8
CompilerVersionLength u32, CompilerVersion UTF-8
```

The C# implementation emits compiler ID `steph.ges.compiler.csharp` and version
`0.1.0`. Other ports use stable implementation IDs. Runtime behavior never depends
on these values. Timestamps, host names, user names, absolute paths, and a separate
language field are forbidden so identical inputs remain reproducible.

## Reading, retention, validation, and writing

`GameEventScriptProgramReader` accepts bytes only. `PreserveAll` parses known
optional sections and retains unknown optional payloads as immutable opaque sections.
`PreserveKnown` drops unknown optional sections. `RuntimeOnly` materializes only the
five required runtime sections. Unknown required sections always fail. A known
optional section with unsupported version or compression is opaque only under
`PreserveAll`; otherwise it is skipped.

The default reader limits are 64 MiB per file, 256 sections, 32 MiB of source archive,
16 MiB of retained opaque payload, and 65,535 each for instructions, bindings,
strings, and index lists. Bounds and arithmetic are checked before allocation.

Reader, canonical writer, and `Host.Load` all invoke the shared program validator.
It validates framing, UTF-8, table references, bind and opcode forms, code targets,
the acyclic synchronous call graph, resource declarations, and debug/source ranges.
Failures use `GameEventScriptProgramFormatException` with a stable error code and,
where available, byte offset, section type, and entry index.

The portable numeric error codes are fixed as follows:

| Code | Name | Code | Name |
|---:|---|---:|---|
| 1 | InvalidMagic | 20 | InvalidUtf8 |
| 2 | UnsupportedFormatVersion | 21 | TooManyEntries |
| 3 | InvalidHeaderFlags | 22 | InvalidStringIndex |
| 4 | InvalidHeaderSize | 23 | InvalidListIndex |
| 5 | FileSizeMismatch | 24 | InvalidBindingKind |
| 6 | FileTooLarge | 25 | DuplicateBindingId |
| 7 | TooManySections | 26 | InvalidEntryAddress |
| 8 | TruncatedSectionHeader | 27 | InvalidOpcode |
| 9 | TruncatedSectionPayload | 28 | InvalidOperand |
| 10 | SectionTooLarge | 29 | InvalidJumpAddress |
| 11 | InvalidSectionType | 30 | InvalidCallAddress |
| 12 | InvalidSectionFlags | 31 | CyclicCallGraph |
| 13 | InvalidSectionReserved | 32 | InvalidResourceMetadata |
| 14 | UnsupportedSectionVersion | 33 | InvalidDebugSymbol |
| 15 | UnsupportedCompression | 34 | InvalidSourceMap |
| 16 | MissingRequiredSection | 35 | InvalidSourceArchive |
| 17 | DuplicateSection | 36 | SourceMetadataMismatch |
| 18 | UnknownRequiredSection | 37 | ReaderLimitExceeded |
| 19 | InvalidPayloadLength | 38 | InvalidProgram |

The writer exposes size calculation, writing into a caller-provided span, and array
creation. It emits file size from the encoded result; file size is not program state.
For canonical uncompressed data, write/read/rewrite is byte stable. Semantic equality,
not original byte identity, remains the product contract.

## Deferred V1 extensions

Compression codec bits and security section IDs are reserved only. V1 implements no
compression, signatures, certificates, key registry, trust policy, rollback policy,
or authenticity guarantee. Adding those features requires separate specifications.
