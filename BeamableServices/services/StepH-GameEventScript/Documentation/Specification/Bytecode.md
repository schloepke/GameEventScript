<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Bytecode specification

This document defines the portable bytecode shape for the
`GameEventScriptProgram` container.
The goal is a compact, portable, high-level bytecode for the GameEventScript DSL
that is naturally executable by a linear program-counter VM.

The public bytecode model is **operand-stack-free**. Normal expression
evaluation reads from and writes to explicit local registers. A portable call stack
is still part of the VM target state for calls, return addresses, frame
metadata, scoped locals, and resumable execution. A native implementation call stack is not part
of script control flow.

The public artifact exposes one global linear instruction segment, static
per-handler/program resource requirements, entry addresses for handlers,
callables, and type-field helpers, plus normalized
tables for strings, ushort lists, external references, debug metadata, and
pipelines. High-level language constructs lower either to normal linear
instructions or to explicit opcodes that reference normalized tables. Function
and predicate calls use public callable entry addresses in VM-owned frames.
Helper expressions run through linear entry addresses and isolate their
temporary registers from handler locals with temporary frame extensions. Predicate
calls use `Call` or `CallExternal` with
`GameEventScriptInstructionFlag.NormalizeResultAsPredicate` set in
`UnitAndFlags`. Local calls use direct `Call` instructions with target entry
addresses and contiguous staged argument sequences. Extension calls use direct
`CallExternal` instructions with argument register lists.
Extrema reduce operators, type constructors, local builders, message literals,
handler binding, casts, type checks, member access, and seeded-random
expressions are layout-free direct instructions.

## Goals

- Represent executable script code as one linear instruction memory.
- Make every executable position addressable by a stable instruction address.
- Use labels only as debug/dump symbols that point at instruction addresses.
- Use register-style instructions: every value-producing instruction writes
  to a destination register and reads operands from source registers or pools.
- Compile control flow such as `if`, loops, guarded choices, predicates, and
  functions into jumps and calls.
- Keep domain-heavy collection operations high-level when that is faster or
  simpler than expanding them into many tiny instructions.
- Preserve iterating and short-circuit behavior where the language requires it.
- Keep the public artifact portable: no VM session state, executable delegates, no
  bound extension functions, no AST nodes, and no runtime value constants.

## Non-Goals

- No public exposure of bytecode runtime implementation types.
- No requirement that the debug dump format matches the binary `.gesb`
  encoding.
- No requirement to split every DSL operation into primitive opcodes.
- No operand stack for expression evaluation.
- No native implementation call-stack dependency for normal script control flow.

## Top-Level Artifact

The portable in-memory artifact is `GameEventScriptProgram`. The normative
`.gesb` V1 framing, segment registry, payload encodings, retention modes,
reader limits, canonical writer rules, and debug/source formats are specified
in [Binary format](BinaryFormat.md).

The five required runtime segments are ProgramMetadataSegment,
StringConstantSegment, UInt16IndexListSegment, BindingSegment, and CodeSegment.
DebugSymbolsSegment, SourceMapSegment, SourceArchiveSegment, and
BuildMetadataSegment are independently optional. `FileSize` is encoded by the
writer and is not kept as program state. Known sections are structured program
data, never retained raw bytes.

`OutboundMessage` bind entries list statically shaped `emit`/`publish` messages.
Their name and argument-name fields are the outbound message signature, allowing
loaders to construct outbound message-signature lookups without scanning the
instruction table.

Imports leave `EntryAddress` at `0xFFFF`. They are linked by table index from
instructions or side tables; extension and external-type implementation code is
not serialized into the script binary. Export bind kinds occupy `0x10` through
`0x1F`; import bind kinds occupy `0x20` through `0x2F`. Extension-call import
names are stored as `extension.function`; external-type import names
are stored as the type name.

Existing string and compact list pools remain important. Constants that fit the
linear instruction shape are encoded directly in typed load instructions:
booleans and `nothing` have dedicated opcodes; integer and float payloads use
raw 64-bit bits split over `A`/`B`; text, tags, and handler message names point
into `StringPool`.

After physical register allocation, every message-handler bind stores its static
resource requirements. `RequiredRegisterCount` is the maximum number of VM values
simultaneously retained while that handler runs. It includes the active frame,
staged values, and all live caller frames on the deepest synchronous call path.
`RequiredCallStackDepth` counts nested calls below the root handler, so a handler
that calls nothing has depth `0`. The program-level fields are the maxima across
all message handlers and allow a host to reject or pre-warm a program at load
time without executing it.

The synchronous call graph must be acyclic. Direct and indirect recursion are
compile errors, and a loader must validate the same invariant for deserialized
programs before accepting them. Loops and bounded collection operations are the
portable repetition mechanisms.

## Portable Constants

The linear bytecode has no object-shaped constant pool. Constants that fit the
instruction word are encoded by typed load opcodes:

- `LoadNothing`, `LoadTrue`, and `LoadFalse` have no payload.
- `LoadInteger` stores a signed 64-bit integer in the numeric `I64` payload view,
  plus optional numeric unit in `UnitAndFlags`.
- `LoadFloat` stores IEEE 754 double bits in the numeric `F64` payload view, plus
  optional numeric unit in `UnitAndFlags`. `NaN`, `Infinity`, and `-Infinity`
  are represented by their IEEE bit patterns. `NaN` may still exist internally
  as a failed mathematical result, but the DSL surface normalizes it to
  `nothing`: presence checks treat it as empty and type checks treat it as
  DSL `nothing`.
- `LoadPercentage` stores a percentage ratio in `F64` and must not carry unit
  flags. Percentage is a dedicated value kind, not a bytecode unit.
- `LoadText` and `LoadTag` store a `StringPool` index in the primary `X`
  operand.
- `LoadHandler` stores a `UShortListPool` message-shape index in `A`. The shape
  list is `[messageNameStringIndex, argumentNameStringIndex...]`.

Constants are concrete. For example `1`, `1.0`, `1m`, and `1s` remain distinct
loads. Runtime operations and explicit `:Number` casts may normalize finite
integral results to integer values. Larger constants such as future vector/point
literals should use a normalized data segment instead of reintroducing an object
constant pool.

String payloads are strict UTF-8 in `.gesb`. Runtime text length, indexing, and
iteration use Unicode scalar values, while SourceMap locations use UTF-8 byte
offsets. Names and tags referenced by bindings or typed operands must satisfy
the ASCII grammars in [Text semantics](Semantics/Text.md); the
shared program validator enforces this for compiler output and loaded binaries.

### Numeric Unit Encoding Target

`UnitAndFlags` is a byte. Its target portable layout is:

```text
bits 0..4  UnitId        0..31
bits 5..7  Reserved      must be zero in portable bytecode
```

Reserved bits are intended to be consumed from the most significant bit
downward. Bit 5 is kept as the last-resort reserved bit so the UnitId field can
grow to 6 bits later if those opcode flags are never needed.

The UnitId map keeps the defined ids stable and reserves
room for likely game-domain units:

| UnitId | Unit | Intended use |
| --- | --- | --- |
| `0` | none | Unitless scalar. |
| `1` | degree | Angles, headings, rotations. |
| `2` | meter | Positions, distances, ranges, radii. |
| `3` | second | Durations, cooldowns, tick time. |
| `4` | meter per second | Speed and velocity magnitude. |
| `5` | meter per second squared | Acceleration. |
| `6` | kilogram | Mass, load, inertia. |
| `7` | newton | Force, thrust, recoil. |
| `8` | joule | Energy, battery charge, heat energy. |
| `9` | watt | Power, generator output, energy consumption over time. |
| `10` | volt | Voltage for electrotechnical systems. |
| `11` | ampere | Current, charge flow, overload/thermal balancing. |
| `12` | hertz | Frequency, fire rate, polling, radio cadence. |
| `13` | bit | Information amount. |
| `14` | byte | Storage amount. |
| `15` | bit per second | Bandwidth and communication throughput. |
| `16` | kelvin | Temperature; Celsius/Fahrenheit syntax should normalize to Kelvin. |
| `17..31` | reserved | Future built-in or domain units. |

The compiler must only emit units supported by its matching runtime. The extended
map is a binary-format target so future unit additions do not need to reshape
the instruction word.

## Parameter Type Hints

Handlers, predicates, and functions may declare optional parameter type hints
using the same `as :type` language as value coercion:

```eventscript
on DamageTaken(unit as :Unit, amount as :Number) {
    publish DamageApplied(unit: unit, amount: amount)
}

predicate wounded(_ unit as :Unit) be unit.hp < unit.maxHp

function livingUnits(_ units as :List) be
    units[:filter unit where not (unit is wounded)]
```

Parameter type hints are not part of message or callable identity.

Predicates:

- `DamageTaken(unit,amount)` remains the signature id, regardless of parameter
  type hints.
- Callable signatures still use names, labels, and positions, not types.
- There is no overload resolution by type.
- Functions or predicates of one kind may overload a name only through distinct
  ordered external-label signatures. Function and predicate names may never
  overlap, even when their signatures differ.
- A parameter type hint means "coerce this bound value as the entry starts".
- Coercion is lenient and uses the same conversion predicates as
  `value as :type`.
- After entry coercion, the compiler and VM may treat the local register as
  normalized to that type for optimization.

The portable metadata carries the hint:

```text
HandlerParameterEntry
  ExternalLabel
  LocalName
  Register
  TypeName?
```

The executable code should contain explicit coercion instructions so the program
counter and dump show where normalization happens:

```text
@0000 RegisterLocals locals+=localCount
@0001 Cast dst=r0 src=r0 kind=Custom type=:Unit
@0002 Cast dst=r1 src=r1 kind=Integer
```

Handler arguments are preloaded into registers `0..n-1` before the entry starts.
Untyped handler parameters therefore emit no binding instruction. Callable
parameters use the same register/type metadata externally; the call ABI stages
arguments before `Call`, and the callee starts with those values already
assigned to registers `0..n-1`.

## Instruction Addresses

An instruction address is the zero-based index into `Code`.

```text
@0000 LoadInteger dst=r3 value=3
@0001 Add dst=r4 left=r1 right=r2
@0002 JumpIfNotTrue cond=r4 target=@0010
```

Predicates:

- Runtime instructions store numeric addresses, not label strings.
- Labels are optional debug symbols: `L_if_else = @0010`.
- All handler, callable, type-field, and helper entry points are addresses in
  the same global `Code` array.
- Address `-1` means "none" only in metadata fields where an optional address is
  explicitly allowed.
- A dump may group addresses by handler or callable, but address numbers are
  global and never restart inside a block.

## Instruction Shape

The portable instruction is compact and allocation-free to execute. Its encoded form is exactly 16 bytes: an opcode byte, a UnitAndFlags byte, three `u16` words, and one `u64` payload. In-memory representations need not mirror this byte layout.

```text
Instruction
  OpCode
  UnitAndFlags
  DestinationRegister
  XRegister, YRegister
  ConditionRegister, TargetAddress, EntryAddress
  StringIndex, ListIndex, SecondaryListIndex, BindId, TypeOperand
  Count
  ImmediateX, ImmediateY, Index
  AU, BU, CU, DU
  AS, BS, CS, DS
  I64, Payload, F64
```

Operands are interpreted by opcode:

- `DestinationRegister`: destination register for value-producing instructions.
- `XRegister`, `YRegister`: primary-word register operands.
- `ConditionRegister`, `TargetAddress`, `EntryAddress`: primary-word control-flow aliases.
- `StringIndex`, `ListIndex`, `SecondaryListIndex`, `BindId`, `TypeOperand`:
  primary-word pool/table or type operands.
- `ImmediateX`, `ImmediateY`: compact signed immediates in the primary word.
- `Index`: compact unsigned 1-based index in the primary word for `IndexAccess`.
- `Count`: signed local register delta alias over `ImmediateX`.
- `AU`, `BU`, `CU`, `DU`, plus signed `AS`, `BS`, `CS`, `DS` views:
  payload-word 16-bit views for wider opcodes.
- `I64`, `Payload`, `F64`: aligned payload-word literal views for signed
  integer payloads, raw unsigned payload bits, and double/float loads.
- Branch opcodes use the primary fields documented by their opcode shape.
- Iterator and pipeline terminal opcodes document their own register, immediate, and
  helper-entry fields explicitly. They do not use sentinel operands for absent
  parameters.
- Pool-backed opcodes use the documented `StringPool` or `UShortListPool`
  indices directly through aliases such as `StringIndex`, `ListIndex`,
  `SecondaryListIndex`, `AU`, or `BU`.
- `RegisterLocals Count` is the required prolog instruction for every executable
  entry address. Entry prologs use a non-negative signed count and add local
  registers beyond the arguments already present in the frame. Negative counts are
  only valid for normal scope exits. The bind/export tables do not carry this
  internal execution value.

Unused instruction fields are undefined and ignored. The instruction word does
not use sentinel operands for optional operands. Optional forms are represented
by distinct opcodes such as `ReturnVoid` and
`EmitMessageWithTags`, or by concrete empty pool entries. Side-table metadata may
still define its own optional `-1` fields where the table schema explicitly
allows them.

Opcode values are grouped in aligned operation-family blocks. Every family
starts at a `0x_0` boundary, and larger families may span multiple 16-value
pages. The VM still dispatches directly on the full opcode byte; the high nibble
is a portable layout convention and may be used by validators, dumpers, or
future decoders. Each group owns exactly one reserved tail range at the end of
the group; reserved opcode pages are not modeled as separate groups. The defined
groups are:

```text
0x00 Group 1: no-op, frame registers, jumps, calls, returns, emit operations
0x10 Group 1 continuation: emit/publish operations, casts, checks, move, access, handler binding
0x20 Group 1 type checks, loads, argument staging, value creation
0x30 Group 1 argument staging and value creation continuation
0x40 Group 1 record/external type construction, presence helpers, reserved tail 0x46..0x4F
0x50 Group 2: boolean algebra, comparison, math
0x60 Group 2 math/random/series continuation and numeric intrinsics
0x70 Group 2 numeric/degree intrinsics and trigonometry
0x80 Group 2 navigation/vector math intrinsics
0x90 Group 2 navigation/vector math continuation, reserved tail 0x98..0x9F
0xA0 Group 3: collection slicing, text/collection operators, map projections
0xB0 Group 3 membership, collection algebra, map projections, element terminals
0xC0 Group 3 iterators, aggregations, weighted terminals, collect terminals
0xD0 Group 3 generated collection/order/group/distinct builders, pattern operators, reserved tail 0xDC..0xFF
```

The exhaustive [canonical opcode field map](#canonical-opcode-field-map) in this document lists every defined opcode as its own row, and every group ends with one `reserved` row for its unused tail range.

Large structured metadata belongs in tables and pools, not nested instruction
objects. Examples: `UShortListPool` message shapes/register lists, `StringPool`
names, bind tables, and optional debug layouts.

An instruction that produces a `nothing` value writes it to `DestinationRegister`. Returning
without a value uses `ReturnVoid`; returning a register value uses
`ReturnValue XRegister`. There is no implicit push. There are no operand-stack `Pop` or
`Duplicate` instructions in the portable target model.

## Execution Model

A host-owned reusable VM state contains only the active resumable handler:

```text
pc
frames[]
callStack[]
scopeStack[]
randomStack[]
activeHighLevelOperationState
activeLinkedProgram
activeMessage
```

Each frame owns a local register array or a frame slice in a shared register memory:

```text
Frame
  EntryKind
  EntryIndex
  ReturnAddress
  ReturnDestinationRegister
  FrameBase
  RegisterCount
  ScopeMark
  RandomMark
```

The VM executor itself is stateless. One host has at most one VM state and cannot
run two handlers concurrently. The state is rebound to a linked program and
entry address for each handler, retained only while a frame is paused, and fully
reset after completion or error. Program string pools, bind tables, resolved
extensions, and resolved external types live in the immutable host-linked
program structure instead of this mutable state.

The native implementation stack is not part of script control flow. `Call` pushes a portable frame
record onto `callStack`; `ReturnValue` restores the next `pc` and writes the returned
value to the caller's destination register.

Manual stepping pauses after the instruction budget is consumed. The active
`pc` points the dump/debugger at the active instruction. If execution is inside
a high-level operation such as a pipeline, the VM may also expose
operation-local debug state such as selector index or item index.

## Entry Tables

Handlers and callables are metadata over the shared code segment. Their
`EntryAddress` points at a `RegisterLocals` prolog instruction. The instruction
immediately after the prolog is the first executable body instruction. Handler
and callable arguments are already present in registers `0..n-1` when their frame
starts.

```text
HandlerEntry
  MessageName
  SignatureId
  Parameters: ParameterEntry[]
  SignatureLabels
  RequiredTags[]
  ExcludedTags[]
  EntryAddress
  DeclarationOrder

CallableEntry
  Name
  Kind: Predicate | Function
  Parameters: ParameterEntry[]
  SignatureLabels
  SignatureId
  EntryAddress
  ReturnRegister
```

The local register count is intentionally not part of the bind/export metadata. It
is encoded as non-negative `RegisterLocals Count` at the entry address because it is
a VM execution detail needed equally by exported handlers/callables and private
helper entries.

`MessageHandler` entries subscribe by `SignatureId`. `MessageNameHandler`
entries subscribe by `MessageName` and tag filters only; the runtime invokes
them with a single direct `:Message` argument. Message-name handlers count as
normal delivery and therefore prevent `undeliverable` fallback when their tag
filters match. For `MessageNameHandler` entries, `SignatureId` still records the
synthetic message parameter signature such as `Damage(message)`, but it is
metadata and not the dispatch key.

System endpoint entries use reserved lowercase names outside normal message
casing. The defined endpoints are:

```text
initialization
undeliverable as message
```

`initialization` is encoded as a `MessageHandler` entry without
arguments. It is not normal external dispatch input; the host queues all loaded
initialization handlers directly when a `GameEventScriptContext` starts.

`undeliverable` is encoded as a `MessageNameHandler` entry with message name
`undeliverable`. It receives the original `:Message` when no `MessageHandler`
or `MessageNameHandler` could be queued for the original message after
signature/name and tag filters were applied. System endpoints still use normal
handler metadata, priority, and declaration order.
`undeliverable` also supports tag filters; `initialization` is parameterless and
does not use tag filters.

Type definitions may reference code addresses for computed fields and clamps:

```text
TypeDefinitionEntry
  TypeName
  Fields[]

TypeFieldEntry
  Name
  TypeName
  MinimumAddress?
  MaximumAddress?
  ComputedAddress?
  ReturnRegister
```

These addresses point at expression code that writes one value into the declared
return register. During record construction, computed-field and clamp helpers run in
helper frames whose visible registers contain the already materialized field values.
Temporary registers for these helpers must start after the source field registers so a
helper cannot overwrite its own inputs.

## Opcode Families

The opcode set should be high-level enough for GameEventScript, but flat enough
for `pc`-based execution.

### Registers and Coercion

- `LoadNothing dst`
- `LoadTrue dst`
- `LoadFalse dst`
- `LoadInteger dst i64 unitAndFlags`
- `LoadFloat dst f64 unitAndFlags`
- `LoadPercentage dst f64`
- `LoadText dst stringIndex`
- `LoadTag dst stringIndex`
- `LoadHandler dst messageNameIndex namedArgumentLayoutIndex`
- `Move dst src`
- `StageRegister src`
- `StageNothing`/`StageTrue`/`StageFalse`
- `StageInteger i64 unitAndFlags`
- `StageFloat f64 unitAndFlags`
- `StagePercentage f64`
- `StageText stringIndex`
- `StageTag stringIndex`
- `Cast dst src typeKind`
- `CastCustom dst src typeNameIndex`
- `CastUnit dst src unitAndFlags`
- `CastNumeric dst src`
- `CheckType dst src typeKind`
- `CheckCustomType dst src typeNameIndex`
- `CheckUnit dst src unitAndFlags`
- `CheckNumeric dst src`
- `CheckInteger dst src`
- `CheckFractional dst src`

`Cast` and `CheckType` use `TypeOperand` as `GameEventScriptBytecodeTypeKind`.
Built-in types are direct kind operands. Custom/external record types use
`CastCustom`/`CheckCustomType` with `TypeOperand` as the type-name `StringPool` index.
Units are not declared type kinds: unit casts and checks use
`CastUnit`/`CheckUnit` with the target unit in `UnitAndFlags`.
`:Number` lowers to `CastNumeric`; numeric casts keep integral values as
integers and use floats only when the value does not fit the integer
representation. `CastNumeric` is the only numeric path that parses text; invalid
text writes `nothing`. `CastNumeric` also accepts series by casting the first
term. `is numeric`, `is integer`, and `is fractional` are source-level check
constructs that lower to `CheckNumeric`, `CheckInteger`, and `CheckFractional`;
they do not parse text and do not treat series as numeric. `CheckNumeric` is
true exactly for values with a runtime numeric view: integer and float numbers,
percentages, booleans (`false` = `0`, `true` = `1`), and dice through the sum
of their rolls. Text and tags are not numeric here. `CheckInteger` is true when that numeric
view is finite and exactly integral; booleans and dice are therefore integer.
`CheckFractional` is true when the numeric view is finite and non-integral.
`Nothing`, text, list, map, range, vector, point, message, handler, series,
custom values, and non-numeric tags have no numeric view for these checks.
`CheckType :Number` still checks the actual runtime kind. `numeric`, `integer`,
and `fractional` are not declared type kinds or `:` tags. VM-level `AsNumeric`
is valid exactly for values where `CheckNumeric` would be true; `CastNumeric` is
broader only in that it may explicitly parse text or first-term-cast series
before producing a numeric value or `nothing`.

`Cast :Tag` is a validating cast. The target tag name must match source tag
syntax: first character lowercase letter, remaining characters ASCII letters or digits.
The empty string, numbers, quantities, percentages, formatted containers,
formatted vector/point values, whitespace, punctuation, brackets, colons inside
the value, and underscores are invalid and write `nothing`. Existing valid tags
remain unchanged. Boolean sources write `#true` or `#false`; text sources write a
tag only when the raw text is a valid tag name. Text values `true`, `True`,
`false`, and `False` are accepted and normalized to `#true` and `#false`.

`Cast :Text` is the formatting cast and does not validate the formatted text as
a tag. `Cast :Vector` and `Cast :Point` are structural conversions: vector to
point and point to vector copy the three components and optional unit directly.
These conversions are casts, not affine vector/point arithmetic.

`let` lowers to expression code that writes into a temporary or final register,
followed by an optional direct cast and `Move` into the declared local register.
Handler and callable parameter type hints lower to optional direct casts over
the preloaded argument registers.

### Scopes

- `RegisterLocals Count`

Scopes are explicit signed local register deltas. `RegisterLocals Count` extends the
same frame by `Count` registers when `Count > 0`, and clears/releases `-Count`
registers when `Count < 0`. Register addresses stay absolute in the current frame, so
reserving two locals from active registers `r0..r3` exposes `r0..r5`. Existing
parent registers remain visible and are not rolled back by scope exit. The compiler
must emit matching deltas for normal exits; `ReturnValue` and `ReturnVoid`
discard the whole active frame, so no negative `RegisterLocals` is needed
immediately before a return.

### Arithmetic and Logic

Binary operations read source registers and write `DestinationRegister`:

```text
Add dst=r3 left=r1 right=r2
Equal dst=r4 left=r3 right=r0
```

Required operations:

- `Add`, `Subtract`, `Multiply`, `Divide`
- `Power`, `IntegerDivide`, `Modulo`, `Remainder`
- `Equal`, `NotEqual`
- `Less`, `Greater`, `LessOrEqual`, `GreaterOrEqual`
- `And`, `Or`, `Xor`, `Implies`
- `Default`
- `Contains`, `ContainsValue`
- `StartsWith`, `EndsWith`
- `Union`, `Intersect`, `Zip`
- `Min`, `Max`
- direct unary opcodes such as `Negate`, `Not`, `Count`,
  `Abs`, `LogN`, `Exp`, `Sin`, `Cos`, `Tan`, `Asin`, `Acos`, and `Atan`
- `Clamp`

Text and tag values are text-compatible for `Count`, `Contains`, `StartsWith`,
and `EndsWith`: comparisons use exact scalar sequences over raw text without
the `#` tag prefix, and `Count` counts Unicode scalar values. Other operand
shapes follow their collection or invalid-operation semantics.

`Contains left, right` writes membership of `left` in `right`. For text/tag
right operands, `left` must also be text/tag and the operation performs an
ordinal raw-text substring check. For `List`, it checks item equality. For
`Dice`, `left` must be a unitless integer roll. For `Range`, `left` must be
numeric and equal to one range term. For `Map`/record/custom values, `left`
must be text/tag and is checked as a key. For `Vector` and `Point`,
`left` must be numeric and is compared with the three components. `right`
`Nothing` writes `Nothing`; unsupported shapes write boolean `false`.

`ContainsAny left, right` and `ContainsAll left, right` use the same container
membership rules as `Contains`, but interpret `left` as a list-like sequence of
needles. Lists, dice, text/tags, and ranges expand to their items; unsupported
needle shapes behave as an empty needle sequence. `ContainsAny` over an empty
needle sequence writes `false`; `ContainsAll` over an empty needle sequence
writes `true`. If `right` is an iterator, it is consumed until `ContainsAny` finds
a match, until `ContainsAll` has matched every needle, or until the iterator is
exhausted. `right` `Nothing` writes `Nothing`.

`ContainsValue left, right` writes value membership of map-like `right`.
It is defined for `Map`, record/custom/external map-like values, `Vector`, and
`Point`. Map-like values compare all stored values. Vectors and points compare
their `x`, `y`, and `z` components. `right` `Nothing` writes
`Nothing`; lists, dice, ranges, text, tags, and scalar values write boolean
`false`.

`HasAny source` and `HasAll source` evaluate boolean truthiness over sequence
items. They are defined for list, dice, range, map/custom values, text/tag
characters, vector/point components, and iterators. `HasAny` writes `true` on the
first truthy item and `false` for an empty sequence. `HasAll` writes `false` on
the first non-truthy item and `true` for an empty sequence. Source `Nothing`
writes `Nothing`; unsupported scalar sources write boolean `false`. Iterator
sources are consumed until the terminal result is known or until exhaustion.

`StartsWith left, right` and `EndsWith left, right` write boundary checks.
Text/tag operands compare raw text with ordinal rules. Sequence operands are
limited to `List`, `Dice`, and `Range`; both operands must be one of those
sequence shapes. The right sequence is matched as a prefix or suffix of the
left sequence. An empty right sequence matches, and a right sequence longer
than the left sequence writes boolean `false`. A `Nothing` left operand writes
`Nothing`; unsupported shapes write boolean `false`.

`Add` is primarily numeric, but has a collection fallback for single-value
insertion. `List + any` appends exactly one value and `any + List` prepends
exactly one value, so `List + List` nests the right list as one item. Dice values
preserve dice semantics only for unitless positive integers:
`Dice + Integer` and `Integer + Dice` insert one roll and write sorted dice.
Other dice/scalar additions write `nothing`. When either operand is text, `Add`
performs text concatenation before numeric or collection handling. The non-text
operand is formatted with the same representation used by `as :Text`.

`Subtract` is primarily numeric, but also removes from selected collection
shapes. `List - scalar` removes one matching item, and `List - List` removes
matching items with multiset semantics. `List - Dice` writes a list. `Dice -
Integer` removes one roll and writes dice, `Dice - Dice` performs multiset
subtraction and writes dice, and `Dice - List` writes a list. `scalar - List`
and `List - Map` write `nothing`. `Map - Map` removes entries whose keys are
present in the right map. `Map - Tag`, `Map - Text`, and `Map - List` of only
tags/text remove matching keys. A map key-removal list containing any
non-tag/non-text item writes `nothing`.

`Union` is the bytecode form of source `|`. `List | List` combines both lists.
`List | Dice` and `Dice | List` write a list. `Dice | Dice` writes sorted dice.
`Map | Map` merges keys and values with right-hand keys replacing left-hand
keys. `Map | List` treats the right operand as a key list and adds missing keys
as boolean `true` flags while preserving existing values. Key lists must contain
only tag/text values; invalid key lists and other operand shapes write
`nothing`.

`Intersect` is the bytecode form of source `&`. `List & List`, `List & Dice`,
and `Dice & List` use multiset semantics and write a list. `Dice & Dice` writes
sorted dice. `Map & Map` keeps keys present in both maps and preserves values
from the left map. `Map & List` keeps only listed keys. Key lists must contain
only tag/text values; invalid key lists write `nothing`. `Dice & Integer` and
`Integer & Dice` are not defined and write `nothing`.

`Zip` is defined only for list operands. It writes a list whose length is the
shorter operand length. Each item is a map with `left` and `right` entries
containing the paired values. Other operand combinations write `nothing`.

`Equal` and `NotEqual` preserve the same absent-value rule as
other Group 2 operations: if either direct operand is `Nothing`, or an internal
numeric `NaN` observed as `Nothing`, the result register receives `Nothing`.
For present operands, `Equal` first tries numeric comparison. Integers, floats,
percentages, booleans, and dice sums compare by numeric value when both
operands have a numeric view. Quantity units must both be absent
or exactly equal; integer fast paths must obey the same unit equality rule, so
`10 = 10m` is `false` and `10 <> 10m` is `true`. Numeric infinities compare
equal only when they have the same sign; numeric `NaN` values are never equal.
When either numeric side is represented as a double precision value, finite
values compare equal when their IEEE 754 values are within two ULPs. There is no
separate approximate-equality opcode or source operator.
`NotEqual` is the boolean negation of `Equal` after this `Nothing` propagation.

When numeric comparison does not apply, exact equality requires the same value
kind and uses kind-specific value equality:

| Kind | Exact equality rule |
| --- | --- |
| `Nothing` | Not reached by the opcode because `Nothing` propagates. |
| `Tag` | Exact Unicode-scalar tag text. Numeric tag constants use numeric comparison when the other operand is also numeric-capable. |
| `Text` | Exact Unicode-scalar text. Text is not implicitly numeric. |
| `Percentage` | Numeric ratio, including comparison with other numeric-capable operands. |
| `Number` | Numeric value and matching quantity unit, including integer/float cross-representation. |
| `Boolean` | Numeric value `0` or `1` in top-level equality. |
| `Dice` | Numeric sum in top-level equality. Dice roll sequence equality applies only inside structural value equality, such as dice values nested in lists or maps. |
| `Vector` | Same kind, same unit, and exact `x`, `y`, `z` components. |
| `Point` | Same kind, same unit, and exact `x`, `y`, `z` components. |
| `Range` | Exact `from`, `to`, and `step`. |
| `Series` | Same series signature id and offset. |
| `Message` | Same signature id, recursively equal arguments, and same tag sequence. |
| `Handler` | Same handler signature id. |
| `List` | Same length and ordered recursively equal items. |
| `Map`/record/custom map-like | Same runtime kind, same key set, and structurally equal values. Records additionally require the same declared type. |

Numeric operations must preserve the language distinction between absent input
and invalid mathematics. For arithmetic, numeric unary operations, `Clamp`, and
numeric random-range evaluation, any source operand that is `nothing` produces
`nothing`. If all source operands are present but the operation cannot be
computed as valid mathematics, the internal result may be numeric `NaN`; at the
DSL boundary it is observed as `nothing`.

Invalid numeric results include non-numeric operands in numeric operations,
incompatible quantity units, invalid vector/point arithmetic, and `mod`/`rem`
by zero. Scalar `Divide` follows IEEE floating-point behavior, so zero
denominators may produce `Infinity`, `-Infinity`, or `NaN` instead of
`nothing`.

Vector/point bytecode arithmetic follows the source affine model. Vectors may
add/subtract compatible vectors, multiply with a scalar on either side, and
divide by a scalar. Points may add/subtract compatible vectors, and
`point - point` yields a vector. Point scaling and scalar-over-point division
are invalid mathematics: `point * scalar`, `scalar * point`, `point / scalar`,
`scalar / point`, and any point operand in `IntegerDivide`, `Modulo`, or
`Remainder` write numeric `NaN` unless a direct source operand is `nothing`.
`Negate` negates vector components, but point negation is invalid
mathematics and writes numeric `NaN`.

`Power` with quantity units is valid only for a unitless numeric exponent of
`0` or `1`; boolean exponents use the standard numeric coercion (`false` = `0`,
`true` = `1`). Exponent `1` preserves the left quantity unit, exponent `0`
produces a unitless result, and all other quantity powers write numeric `NaN`
unless a direct source operand is `nothing`.

Numeric opcodes preserve value families where the source language does:
the portable overflow, conversion, binary64, rounding, division/modulo, and ULP
rules are normative in [Number semantics](Semantics/Numbers.md).
`Abs` keeps percentages as `Percentage`, keeps quantity units on numeric
quantities, and finite exactly integral numeric results are represented as
integer values when they fit signed 64-bit.
Percentage multiplication with a non-percentage scalar or quantity treats the
percentage as its stored ratio and writes a numeric result in the other
operand's value family, for example `10% * 10` and `10 * 10%` both write
numeric `1`, while `10% * 10m` and `10m * 10%` both write `1m`.

Math intrinsics are normal opcodes, not standard extension calls. Unary
intrinsics accept both source forms `op x` and `op(x)`. `ln` lowers to `LogN`,
`exp` lowers to `Exp`, and `abs` lowers to `Abs`. `sin`, `cos`, and `tan`
accept unitless radians or `degree` quantities and always write a unitless
numeric result. `asin` and `acos` accept only unitless numeric input in
`[-1, 1]`; outside that domain they write `nothing`. `atan` accepts unitless
numeric input. `atan2(y, x)` accepts two unitless or same-unit numeric operands,
writes unitless radians, and defines `atan2(0, 0)` as `0`.

Integer-style math intrinsics lower to direct opcodes. `floor`, `ceil`,
`truncate`, `round half even`, `round half up`, and `round half down` accept
unitless numeric input and write integer results. `rad(x)` converts degrees to
unitless radians, `deg(x)` converts unitless radians to a `degree` quantity, and
`wrap degree x` normalizes a degree or unitless numeric value into `[0, 360)`
degrees.

Navigation/vector intrinsics with scalar coordinate arguments require
parentheses and use arity to select 2D or 3D opcodes. Existing `Vector` and
`Point` values are always treated as 3D because their runtime representation
always carries `x`, `y`, and `z`. `hypot(x, y)` and `hypot(x, y, z)` accept
unitless or same-unit numeric operands and preserve matching units in the
result. `distance(a, b)` accepts scalar numeric values or compatible 3D
vector/point values; `distance(x1, y1, x2, y2)` and
`distance(x1, y1, z1, x2, y2, z2)` use 2D and 3D coordinate forms. Distance
results preserve matching units.

`distance squared(...)` and `length squared(...)` follow the same arity rules as
their non-squared counterparts but always write unitless numeric results because
the VM has no square-unit representation. `normalize(...)` writes a unitless
vector and writes `nothing` for zero-length inputs. `dot(...)` and `cross(...)`
accept compatible units and strip units from the result; object/3D `cross`
writes a unitless vector, while 2D `cross(x1, y1, x2, y2)` writes the scalar z
component. `angle between(...)` writes unitless radians and writes `nothing`
when either vector length is zero.

Logical operations use three-valued truth tables with `nothing` as unknown.
Runtime truth tests and false tests both fail for `nothing`.

Short-circuiting source constructs must compile to jumps when preserving lazy
behavior matters:

```text
; dst = a and b
@0100 JumpIfFalse cond=rA target=@0105
@0101 ... evaluate b into rB ...
@0102 And dst=rDst left=rA right=rB
@0103 Jump @0106
@0105 LoadFalse dst=rDst
@0106 ...
```

The exact lowering may differ, but the right-hand expression of `and`, `or`, and
`->` must be skipped when the language's short-circuit rule decides the result.
Implication is right-associative at source level and uses the truth table for
`not a or b`: false antecedent yields `true`; unknown participates as
`nothing` unless the consequent resolves the result to `true`.

The VM executes `and` and `or` laziness from the linear branch sequence; there
are no dedicated `and`/`or` short-circuit opcodes in portable bytecode.
`Implies` can be emitted as the binary implication combine inside that branch
sequence.

### Control Flow

- `Jump target`
- `JumpIfTrue cond target`
- `JumpIfFalse cond target`
- `JumpIfNotTrue cond target`
- `JumpIfNothing cond target`
- `CreateRangeIterator dst from to`
- `CreateRangeIteratorWithStep dst from to step`
- `CreateRangeIteratorShort dst fromI16 toI16 stepI16`
- `IteratorCreate dst collection`
- `IteratorCreateOrJump dst collection notIterableTarget`
- `IteratorNext dst iterator noMoreTarget`
- `IteratorClose iterator`
- `Call dst entryAddress`
- `ReturnValue src`
- `ReturnVoid`

`if` and guarded choices compile to condition code plus jumps. `else` runs when
the condition is not true, so normal `if` lowering should use `JumpIfNotTrue`,
not an implicit `as :Boolean` conversion.

Example:

```text
@0100 Move dst=r10 src=r0
@0101 LoadInteger dst=r11 value=0
@0102 Greater dst=r12 left=r10 right=r11
@0103 JumpIfNotTrue cond=r12 target=@0108
@0104 LoadInteger dst=r1 value=1
@0105 Jump @0110
@0108 LoadInteger dst=r1 value=2
@0110 ...
```

Guarded expressions lower to explicit condition jumps and value writes to a
shared destination register. Conditions are evaluated in order via `JumpIfNotTrue`;
only the first true branch value is evaluated, and otherwise code runs when no
condition is true. The public bytecode has no `GuardedChoice` layout.

`for` statements lower to normal linear iterator control flow. The source is
evaluated once, an iterator is stored in a temporary register, `IteratorNext` writes
each item into an item register and jumps to the close block when exhausted, and the
body runs inside an iteration scope. Literal I16 ranges should use
`CreateRangeIteratorShort`; dynamic ranges and collection sources use the register-based
iterator opcodes. Generated collections use the same iterator opcodes plus
VM-internal collection builder opcodes.

### Calls and Returns

Predicates and functions are normal callable entries. A call instruction
transfers control to the callable entry and returns to the next instruction.
Predicate calls use the same frame mechanism with
`NormalizeResultAsPredicate`, normalize the result to `boolean | nothing`, and
read their arguments from the stage sequence immediately before the call. The
call instruction does not store an argument count; the VM uses the contiguous
`Stage*` sequence immediately before the call and validates it against callable
metadata. A stage sequence may contain only `Stage*` instructions and must be
followed by a stage consumer (`Call`, `CreateVector`, `CreatePoint`,
`CreateList`, `CreateMap`, `CreateRecord`, or `CreateExternalType`). The
callee frame receives arguments in registers `0..n-1`.
The `x is predicate` syntax is unary sugar that lowers to one staged argument
plus `Call` with `NormalizeResultAsPredicate`.

```text
@0500 StageRegister src=r0
@0501 Call dst=r3 callable=wounded stagedArgs=1
@0502 JumpIfNotTrue cond=r3 target=@0510
@0520 StageRegister src=r2
@0521 Call dst=r4 predicate=@0900 flags=NormalizeResultAsPredicate stagedArgs=1
```

Type constructors, variadic operators, collection builders, maps,
message literals, handler binding, local calls, and predicate calls
reference bind ids, entry addresses, `StringPool`, or `UShortListPool` directly
from the instruction word. Record constructor code addresses are stored on
`Record` bind entries so host linking can resolve them centrally.

Call frame state:

```text
ReturnAddress
CallableIndex
CallerFrameBase
ReturnDestinationRegister
ScopeMark
RandomMark
```

Predicate bodies must compile as `:Boolean` or DSL `nothing`; an explicit
`as :Boolean` marks intentional boolean coercion. Predicate calls preserve
`nothing` so missing information remains "no statement" instead of becoming
`false`. Functions preserve the expression result.

### Loops

Loops should be compiled to explicit loop control instructions and jumps.

Range loop shape:

```text
@0200 RegisterLocals locals+=loopLocalCount
@0201 CreateRangeIteratorShort dst=rIterator from=1 to=20 step=1
@0202 IteratorNext dst=rItem iterator=rIterator noMore=@0210
@0203 RegisterLocals locals+=iterationLocalCount
@0204 Move dst=rIdentifier src=rItem
@0205 ...
@0208 RegisterLocals locals-=iterationLocalCount
@0209 Jump @0202
@0210 IteratorClose iterator=rIterator
@0211 RegisterLocals locals-=loopLocalCount
```

Collection loop shape:

```text
@0300 RegisterLocals locals+=loopLocalCount
@0301 IteratorCreate dst=rIterator source=rValues
@0302 IteratorNext dst=rItem iterator=rIterator noMore=@0310
@0303 RegisterLocals locals+=iterationLocalCount
@0304 ...
@0308 RegisterLocals locals-=iterationLocalCount
@0309 Jump @0302
@0310 IteratorClose iterator=rIterator
@0311 RegisterLocals locals-=loopLocalCount
```

Loop runtime state is stored in the VM-internal iterator value held by the
compiler-assigned iterator register.

### Values and Containers

- `LoadMessage dst messageShapeIndex argumentRegisterListIndex`
- `BindHandler dst handlerRegister argumentRegisterListIndex`
- `MemberAccess dst nameIndex objectRegister`
- `IndexAccess dst immediateIndex objectRegister`
- `PropertyAccess dst selectorRegister objectRegister`
- `CreateDice dst count sides`
- `CreateVector dst immediateX`
- `CreatePoint dst immediateX`
- `CreateList dst` consumes the contiguous staged value sequence immediately
  before the opcode as list items.
- `CreateMap dst keyNameListIndex` consumes the contiguous staged value sequence
  immediately before the opcode as map values; keys remain in `keyNameListIndex`
  in V1.
- `CreateRange dst fromRegister toRegister`

`IndexAccess` is reserved for non-negative literal selectors that fit the
unsigned 16-bit `Index` operand. Negative literal selectors and larger numeric
selectors lower through `PropertyAccess`, so they keep the normal runtime
selector semantics.
- `CreateRangeWithStep dst fromRegister toRegister stepRegister`
- `RandomTake dst fromRegister toRegister`
- `RandomTakeFloat dst fromRegister toRegister`
- `RandomPush seedRegister`
- `RandomPushConstant seedI64`
- `RandomPop`
- `CreateRecord dst recordBindId` consumes the contiguous staged value sequence
  immediately before the opcode as constructor values. The bind entry supplies
  the record type name, constructor parameter labels, and constructor entry
  address. Computed record fields are not constructor parameters; the
  constructor routine derives them. The constructor returns a custom record value
  directly.
- `CreateRecordValue dst mapRegister recordTypeNameIndex` creates the concrete
  custom record value from a constructor-produced field map. This opcode is
  intended for record-constructor routines; normal record construction uses
  `CreateRecord`.
- `CreateExternalType dst externalTypeConstructorReferenceIndex argumentNameListIndex`
  consumes the contiguous staged value sequence immediately before the opcode
  as constructor values.

Dice, vector, point, list, map, and range creation opcodes live in Group 1 with
other value-loading and construction instructions. `CreateRecord` and
`CreateExternalType` are kept at the end of the value-creation block because they construct
script records and host-bound external values. `CreateRecordValue` is the internal
record-constructor value materialization step.
These remain high-level because they map directly to public value semantics.
List indexes reference `UShortListPool`; name lists and message shapes contain
`StringPool` indexes, while register lists contain frame register indexes. Record
constructors reference `Record` bind ids; external type, list, map, vector, and
point constructors consume staged values instead of argument register lists.
Vector and point constructors are fixed built-ins. Their source arguments are
lowered into staged component values in canonical `x, y, z` order; `immediateX`
stores the first staged component index (`0`, `1`, or `2`) so leading missing
components do not need explicit zero stages.
Seeded random no longer has a side table or helper expression opcode. The
lowerer emits `RandomPush*`, the inline body instructions, and `RandomPop`.
Constant seeds are unitless signed `Int64`, so negative seeds such as `-145`
are valid source literals. A non-literal seed has already been established as
an inferred unitless integer or explicitly converted with `as :Number` by the
source program; the lowerer emits its value directly to `RandomPush`.

Both random push opcodes first save the complete active generator state.
`RandomPushConstant` then resets it to its immediate seed. `RandomPush` resets
it only when the register contains a unitless exact signed-64 integer; otherwise
it leaves the copied state active without raising a diagnostic. `RandomPop`
always restores the saved state. Thus even an invalid dynamic seed has a real,
balanced scope and cannot consume its parent stream.

Required portable value families:

- primitives: DSL `nothing`, `:Tag`, `:Text`, `:Boolean`
- numeric: `:Number`, `:Percentage`
- numeric quantities: `:Quantity(degree)`/`:Quantity(°)`, `:Quantity(m)`, `:Quantity(s)`
- vectors: `:Vector`
- points: `:Point`
- containers: `:Series`, `:Range`, `:List`, `:Map`, `:Dice`
- runtime values: `:Message`, `:Handler`
- custom record and external types

Series values are repeatable mathematical series. `[:term n]` reads the
zero-based term and is valid only for series; all non-series sources evaluate to
`nothing`. `[:take first n]` materializes the first terms as a list, and
`[:drop first n]` returns a shifted series. Because series are not finite,
`[:take last n]` and `[:drop last n]` evaluate to `nothing`.

Finite sequence values support direct slicing without pipeline materialization:
lists, dice, ranges, and iterators support `:take first`, `:drop first`,
`:take last`, `:drop last`, `:take highest`, `:take lowest`,
`:drop highest`, and `:drop lowest`. List slices return lists, dice slices
return dice, range slices return ranges, and iterator slices consume the iterator
and return materialized lists.

Message values are map-backed runtime values. The bytecode model exposes the
read-only members `name`, `signature`, `arguments`, and `tags` through normal
member/index access. The `signature` member contains the stable signature
string, for example `Damage(amount,kind)`.

### Emit, Publish, and Tags

The language distinguishes local/current-space emission from outward/shared-space
publishing:

```eventscript
emit LocalEvent(value) with #internal
publish BusEvent(value) with #radio, dynamicTags

on BusEvent(value) matching #radio without #blocked {
}
```

`Emit` targets the current event space. `Publish` targets the configured publish
hook; when no hook exists, it falls back to `Emit`. This keeps the language
host-independent while allowing the runtime to redirect published messages to a
bus, broadcaster, or parent dispatcher later.

`with` attaches delivery tags to the message. Tags are not part of
`SignatureId`, not part of handler parameter binding, and are normalized to a
unique ordered tag list while preserving first-seen order. A tag expression may
evaluate to a single tag or to a list of tags.

Handlers may declare static tag filters:

- `matching #a, #b`: all required tags must be present.
- `without #x, #y`: none of the excluded tags may be present.
- no filter: any tag list matches as long as the message signature matches, or
  the message name matches for `MessageNameHandler` entries.

Publishing and emitting are distinct opcodes. Static message literals are
registered as `OutboundMessage` bind entries. Direct
`EmitMessage*`/`PublishMessage*` opcodes store the outbound message bind id in
`MessageDestination`, the argument register-list in `ListIndex`, and tagged forms
store the tag register-list in `SecondaryListIndex`. Dynamic message values use the
message register in `XRegister`; tagged dynamic forms store their concrete tag register-list
index in `ListIndex`. Static outbound message signatures are not duplicated as
message-shape entries in `UShortListPool`; only the argument and tag register-lists
remain there. Zero-argument messages use a concrete empty argument-list entry in
`UShortListPool`.

Arguments and tags are evaluated by preceding code into registers:

```text
@0400 Move dst=r20 src=rDamage
@0401 Move dst=r21 src=rTarget
@0402 LoadTag dst=r22 tag=#radio
@0403 PublishMessageWithTags outbound=#0 args=#1 tags=#2
```

Publishing or emitting a first-class message value uses `PublishMessageValue`
or `EmitMessageValue`. Tagged dynamic values use
`PublishMessageValueWithTags messageRegister tagRegisterList` or
`EmitMessageValueWithTags messageRegister tagRegisterList`.

### Extensions, Intrinsics, Series, and External Types

- `CallExternal dst externalReferenceIndex argumentRegisterListIndex`
- `CreateSeries dst seriesKind`

Predicate extension calls use the same opcode with
`NormalizeResultAsPredicate` set in `UnitAndFlags`.

External references are collected at compile time and dynamically bound by the
host when loading the compiled artifact. Runtime extension binding is not
serialized into portable bytecode.

Core language intrinsics are non-overridable and do not appear in
`ExternalReferences`. Math, navigation, collection, and iterator terminals lower
to direct opcodes. Series creation is also a direct opcode. All extension call
arguments, including zero-argument calls, are represented by a concrete argument
register-list entry in `UShortListPool`.

External type constructors are emitted as `ExternalType` imports in the binding
segment and dynamically bound against the host's external type runtime registry.
The compiler validates source against a separate, declarative external type
catalog; neither that catalog nor executable bindings are stored in the program.
Record constructors and external constructors share the same source syntax but
remain distinguishable through their binding kind.

### Collection DSL

Collection operations should stay high-level enough to avoid exploding code size
and losing optimized paths, but pipeline selectors no longer live in public
metadata pools. Prefix selectors and selector expressions are lowered into
ordinary bytecode loops so every expression participates in normal slice
execution and opcode budgeting.

Core iterating shape:

```text
IteratorCreate source -> iterator
Count dst source
OneWeighted dst items weights
TakeWeighted dst items count weights
Distinct dst source
SortAscending dst source
SortDescending dst source
Reverse dst source
Shuffle dst source
OneRandom dst source
TakeRandom dst source count
First/Last/Single dst source
HasAny/HasAll dst source
ListBuilderCreate builder
ListBuilderAdd builder item
ListBuilderFinish dst builder
MapBuilderCreate builder
MapBuilderAdd builder key value
MapBuilderFinish dst builder
DistinctBuilderCreate builder
DistinctBuilderAdd builder key value
DistinctBuilderFinish dst builder
GroupBuilderCreate builder
GroupBuilderAdd builder key value
GroupBuilderFinish dst builder
OrderBuilderCreate builder
OrderBuilderAdd builder key value
OrderBuilderFinishAscending dst builder
OrderBuilderFinishDescending dst builder
```

`SortAscending` and `SortDescending` accept lists, dice, ranges, and iterators.
Lists and iterators materialize sorted lists. Dice materialize lists even when
descending order matches dice's natural order. Ranges stay ranges by preserving
or reversing their bounds and step. Direct maps, scalars, and `nothing` produce
`nothing`.

`order by` selectors accept iterable sources and lower to ordinary
`IteratorCreateOrJump` loops with `OrderBuilder*` opcodes. They materialize the
original items ordered by the projected key. Direct non-iterable sources
produce `nothing`.

`Reverse` accepts lists, dice, ranges, and iterators. Lists materialize reversed
lists, dice materialize lists so dice ordering is not normalized, ranges stay
ranges by swapping bounds and negating the step, and iterators materialize lists.
Maps, scalars, and `nothing` sources produce `nothing`.

`Shuffle` accepts lists, dice, ranges, and iterators. It always materializes a
list. Direct maps, scalars, and `nothing` sources produce `nothing`.

`Distinct` accepts lists, dice, and iterators. `distinct by` selectors lower to
ordinary `IteratorCreateOrJump` loops with `DistinctBuilder*` opcodes, so every
iterable source follows normal iterator semantics; non-iterable sources
produce `nothing`.

`group by` selectors lower to ordinary `IteratorCreateOrJump` loops with
`GroupBuilder*` opcodes. The result is a map from projected key text to lists
of matching source items. Every iterable source follows normal iterator
semantics; non-iterable sources produce `nothing`.

`KeysOfMap`, `ValuesOfMap`, and `EntriesOfMap` are strict map/custom-type
projection opcodes. They are not general enumerable materializers. Map-backed
custom type values follow the same rules as maps. Successful projections use
stable ordinal key order. If the operand is `nothing`, the result is
`nothing`; any other non-map operand also yields `nothing`.

`First`, `Last`, and `Single` are direct collection/iterator element terminals.
They accept lists, dice, ranges, maps, custom map-backed values, text, tags, and
VM iterators. Maps and custom values use stable ordinal value order. Empty,
invalid, or unsupported sources yield `nothing`; `Single` also yields `nothing`
when the source has more than one element. The DSL `:draw 1` selector lowers to
`First`; `:draw n` with `n > 1` lowers to `TakeFirst`. Deterministic
`:choose 1` also lowers to `First`, and deterministic `:choose n` with
`n > 1` lowers to `TakeFirst`. Random choice uses `OneRandom` for
`:choose 1 at random` and `TakeRandom` for `:choose n at random`.
`TakeRandom` chooses without replacement. Lists stay lists, dice stay dice,
and ranges or iterators materialize as lists. Predicated choices lower to explicit
filter loops before applying the deterministic or random terminal. Weighted
choice uses iterator loops so the predicate and weight expression execute as
normal bytecode for every candidate. The generated loop materializes two lists: candidate items and
their positive finite weights. `:choose 1 weighted by ...` then lowers to
`OneWeighted` and returns one item or `nothing`; `:choose n weighted by ...`
lowers to `TakeWeighted` and returns a list with up to `n` items. Only positive
finite weights participate.

`Count` is a fixed finite aggregation terminal over finite collection-like
sources or already-created iterators. It returns `0` for an empty finite source
and `nothing` for series sources because they would otherwise require unbounded
consumption. DSL `:sum` and `:average` selectors are lowered to explicit
bytecode loops so they can account for normal step budgets and avoid hidden
iterator consumption inside a single opcode.

DSL `:min` and `:max` selectors are lowered to explicit bytecode loops. The
generated loop keeps the first source item as the initial winner, evaluates the
projection inline for each item, compares projected values with normal
less/greater semantics, and returns the winning source item. Ties keep the
earlier source item. Empty finite iterators and series sources return `nothing`.

Fixed terminal opcodes cover materializers and operations that need full
collection semantics: map, distinct, group/order/sort/reverse, weighted
choose/shuffle, dice/card patterns, object matches, and series term/take/drop
operations. Pattern operations use `HasPattern` or `TakePattern` with `AU`
holding a `GameEventScriptBytecodePatternKind` value. `CountAny` and
`CountFace` use `ImmediateY` as the required count; `CountFace` additionally
uses `BU` as the already-evaluated face register. These opcodes reference only
source or iterator registers, immediate counts, pattern ids, face registers, and
binding registers; there are no pipeline selector, pattern, or object-pattern pools.

Iterator/materialization contract:

- `:Range` and `:Series` sources must not be blindly materialized
  before iterable terminal selectors.
- Iterable/short-circuit terminal selectors include `:any`, `:all`, and
  `:first`. `:any` and `:all` lower to the normal `HasAny` and `HasAll`
  opcodes over projected predicate values; when their source operand is a
  iterator, they consume the iterator only as far as needed. Direct `:contains x`,
  `:contains any xs`, and `:contains all xs` lower to the normal `Contains`,
  `ContainsAny`, and `ContainsAll` opcodes; when their container operand is a
  iterator, they consume the iterator only as far as needed.
- Prefix selectors are applied lazily on the iterating path.
- Selectors that require full collection semantics may materialize after runtime
  budgets such as `MaxRangeItems` are checked.
- Non-range list-like sources should keep an indexed hot path for selectors such
  as `:sum`, `:average`, `:count`, and edge selectors.

### Generated Collections

Generated collection expressions lower to normal linear iterator control flow:

```text
ListBuilderCreate builder
RegisterLocals locals+=collectionLocalCount
CreateRangeIterator* / IteratorCreate iterator
loop:
  IteratorNext item iterator noMore
  RegisterLocals locals+=iterationLocalCount
  Move identifier item
  optional predicate + JumpIfNotTrue skipProjection
  projection expression
  ListBuilderAdd builder projected
  RegisterLocals locals-=iterationLocalCount
  Jump loop
noMore:
IteratorClose iterator
RegisterLocals locals-=collectionLocalCount
ListBuilderFinish dst builder
```

Direct ranges after `in` remain invalid at source level. Range iteration should
use explicit range-source syntax.

At runtime, collection builders are VM-internal values and are not visible as DSL
values. `ListBuilderAdd` applies `MaxGeneratedCollectionItems` while
materializing the result. Map projections use VM-internal map builders in the
same linear loop shape; `MapBuilderAdd` skips empty or `nothing` keys and
overwrites duplicate keys with the later value.

## Format Invariants

- New bytecode format changes must increment `FormatVersion`.
- Bytecode dumps are debug tooling output and are not a stable wire format.
- Public bytecode must remain deterministic for the same script/options.
- Host dynamic linking remains separate from compilation.
- Runtime extension binding is not serialized into portable bytecode.
- Runtime execution must be resumable without relying on the native implementation call stack.
- The portable model must preserve source-level short-circuit and tri-state
  truth semantics.

## Canonical opcode field map

Opcode values are split into operation-family blocks. Larger families may span
multiple 16-value pages. The VM dispatches on the complete byte value; the high
nibble is a format convention, not a second runtime dispatch step.

### Group 1 - Control, Calls, Messages, Types, Values

| Hex | Opcode | UnitAndFlags | DestinationRegister | X | Y | Payload | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 0x00 | `Nop` | - | - | - | - | - | No operation. |
| 0x01 | `RegisterLocals` | - | - | `Count` | - | - | Adds `Count > 0` active local registers or releases `-Count` registers when `Count < 0`. Entry prologs reserve only locals beyond preloaded arguments. |
| 0x02 | `Jump` | - | - | - | `TargetAddress` | - | Unconditional branch. |
| 0x03 | `JumpIfTrue` | - | - | `ConditionRegister` | `TargetAddress` | - | Branches when `X.IsTrue()`. |
| 0x04 | `JumpIfFalse` | - | - | `ConditionRegister` | `TargetAddress` | - | Branches when `X.IsFalse()`. |
| 0x05 | `JumpIfNotTrue` | - | - | `ConditionRegister` | `TargetAddress` | - | Branches when `!X.IsTrue()`, including `nothing`. |
| 0x06 | `JumpIfNothing` | - | - | `ConditionRegister` | `TargetAddress` | - | Branches when `X.Kind` is `Nothing`. |
| 0x07 | `Call` | optional `NormalizeResultAsPredicate` | result register | - | `EntryAddress`=callable/predicate | - | Enters a VM-owned local call frame at a known code address. Arguments are the contiguous staged sequence immediately before the call. With `NormalizeResultAsPredicate`, the returned value is normalized to boolean or `nothing`. |
| 0x08 | `CreateSeries` | - | result register | - | `TypeOperand`=series kind | - | Creates a built-in mathematical series. Supported kinds are `Fibonacci` and `Factorial`. |
| 0x09 | `CallExternal` | optional `NormalizeResultAsPredicate` | result register | `BindId` | `ListIndex`=argument register-list index | - | Calls a dynamically bound host extension. With `NormalizeResultAsPredicate`, the result is normalized to boolean or `nothing`. |
| 0x0A | `ReturnVoid` | - | - | - | - | - | Returns no value from the current frame; normal calls map this to DSL `nothing`. |
| 0x0B | `ReturnValue` | - | - | `XRegister`=return | - | - | Returns the value in `X` from the current frame. |
| 0x0C | `EmitMessage` | - | `MessageDestination`=outbound message bind id | - | `ListIndex`=argument register-list index | - | Emits a statically shaped message without tags. |
| 0x0D | `EmitMessageWithTags` | - | `MessageDestination`=outbound message bind id | `SecondaryListIndex`=tag register-list index | `ListIndex`=argument register-list index | - | Emits a statically shaped message with tags. |
| 0x0E | `EmitMessageValue` | - | - | `XRegister`=message | - | - | Emits a dynamic message value without tags. |
| 0x0F | `EmitMessageValueWithTags` | - | - | `XRegister`=message | `ListIndex`=tag register-list index | - | Emits a dynamic message value with tags. |
| 0x10 | `PublishMessage` | - | `MessageDestination`=outbound message bind id | - | `ListIndex`=argument register-list index | - | Publishes a statically shaped message without tags. |
| 0x11 | `PublishMessageWithTags` | - | `MessageDestination`=outbound message bind id | `SecondaryListIndex`=tag register-list index | `ListIndex`=argument register-list index | - | Publishes a statically shaped message with tags. |
| 0x12 | `PublishMessageValue` | - | - | `XRegister`=message | - | - | Publishes a dynamic message value without tags. |
| 0x13 | `PublishMessageValueWithTags` | - | - | `XRegister`=message | `ListIndex`=tag register-list index | - | Publishes a dynamic message value with tags. |
| 0x14 | `Cast` | - | result register | `XRegister`=source | `TypeOperand`=type kind | - | Converts `X` to the declared built-in type. Custom/record types use `CastCustom`. `Cast :Tag` validates tag syntax; invalid tag text writes `nothing`. `Cast :Vector`/`:Point` structurally convert between vectors and points by copying components and unit. |
| 0x15 | `CastCustom` | - | result register | `XRegister`=source | `TypeOperand`=custom type string | - | Converts `X` to a custom/record type identified by `Y`. |
| 0x16 | `CastUnit` | target numeric unit | result register | `XRegister`=source | - | - | Converts `X` to the numeric unit carried in `UnitAndFlags`. Units are not represented as declared type kinds. |
| 0x17 | `CastNumeric` | - | result register | `XRegister`=source | - | - | Coerces `X` through the source-level `:Number` type. This is the only numeric path that parses text; invalid text writes `nothing`. Integral values stay integer; otherwise the result is float. |
| 0x18 | `CheckType` | - | result register | `XRegister`=source | `TypeOperand`=type kind | - | Writes whether `X` has the declared built-in type. Custom/record types use `CheckCustomType`. |
| 0x19 | `CheckCustomType` | - | result register | `XRegister`=source | `TypeOperand`=custom type string | - | Writes whether `X` has the custom/record type identified by `Y`. |
| 0x1A | `CheckUnit` | target numeric unit | result register | `XRegister`=source | - | - | Writes whether `X` has the numeric unit carried in `UnitAndFlags`. Units are not represented as declared type kinds. |
| 0x1B | `CheckNumeric` | - | result register | `XRegister`=source | - | - | Writes whether `X` has a runtime numeric view. Numbers, percentages, booleans (`false` = `0`, `true` = `1`), and dice sums are numeric. Text and tags are not parsed here. |
| 0x1C | `CheckInteger` | - | result register | `XRegister`=source | - | - | Writes whether `X` has a finite integral numeric view. Booleans and dice are integer. Text is not parsed here. |
| 0x1D | `CheckFractional` | - | result register | `XRegister`=source | - | - | Writes whether `X` has a finite non-integral numeric view. Text is not parsed here. |
| 0x1E | `Move` | - | result register | `XRegister`=source | - | - | Copies a register value/reference; the source register remains unchanged. |
| 0x1F | `MemberAccess` | - | result register | `StringIndex`=member name | `YRegister`=object | - | Reads a named member. |
| 0x20 | `IndexAccess` | - | result register | `Index`=1-based index | `YRegister`=object | - | Reads a statically known positional element. |
| 0x21 | `PropertyAccess` | - | result register | `XRegister`=property/index selector | `YRegister`=object | - | Reads a dynamic property: integer selectors use index semantics; text/tag selectors use member semantics. |
| 0x22 | `BindHandler` | - | result register | `XRegister`=handler/signature register | `ListIndex`=argument register-list index | - | Binds ordered argument values to a handler signature. Argument names come from the signature. |
| 0x23 | `LoadNothing` | - | result register | - | - | - | Loads `nothing`. |
| 0x24 | `LoadTrue` | - | result register | - | - | - | Loads boolean `true`. |
| 0x25 | `LoadFalse` | - | result register | - | - | - | Loads boolean `false`. |
| 0x26 | `LoadInteger` | numeric unit | result register | - | - | `I64`=signed integer | Loads an inline signed `Int64`. |
| 0x27 | `LoadFloat` | numeric unit | result register | - | - | `F64`=float | Loads an inline IEEE-754 `Float64`. |
| 0x28 | `LoadPercentage` | - | result register | - | - | `F64`=ratio | Loads an inline percentage ratio as the dedicated percentage value kind. |
| 0x29 | `LoadText` | - | result register | `StringIndex` | - | - | Loads a text literal. |
| 0x2A | `LoadTag` | - | result register | `StringIndex` | - | - | Loads a tag literal. |
| 0x2B | `LoadHandler` | - | result register | - | `ListIndex`=message shape | - | Loads a handler literal. The shape list is `[messageNameStringIndex, argumentNameStringIndex...]`. |
| 0x2C | `LoadMessage` | - | result register | `SecondaryListIndex`=message shape | `ListIndex`=argument register-list index | - | Loads a statically shaped message value. Shape is `[messageNameStringIndex, argumentNameStringIndex...]`. |
| 0x2D | `StageRegister` | - | - | `XRegister`=source | - | - | Stages a register value for the next stage-consuming instruction. |
| 0x2E | `StageNothing` | - | - | - | - | - | Stages DSL `nothing` for the next stage-consuming instruction. |
| 0x2F | `StageTrue` | - | - | - | - | - | Stages `true` for the next stage-consuming instruction. |
| 0x30 | `StageFalse` | - | - | - | - | - | Stages `false` for the next stage-consuming instruction. |
| 0x31 | `StageInteger` | numeric unit | - | - | - | `I64`=integer payload | Stages an inline integer value. |
| 0x32 | `StageFloat` | numeric unit | - | - | - | `F64`=float payload | Stages an inline float value. |
| 0x33 | `StageText` | - | - | `StringIndex` | - | - | Stages a text literal from `StringPool`. |
| 0x34 | `StageTag` | - | - | `StringIndex` | - | - | Stages a tag literal from `StringPool`. |
| 0x35 | `StagePercentage` | - | - | - | - | `F64`=ratio | Stages an inline percentage ratio value. |
| 0x36 | `CreateDice` | - | result register | `Count`=dice count | `ImmediateY`=side count | - | Creates a dice value; `X` and `Y` are not registers. |
| 0x37 | `CreateVector` | - | result register | `ImmediateX`=first staged component index | - | - | Creates a vector from the staged component sequence. `ImmediateX` is `0` for x, `1` for y, or `2` for z; missing components become `0`. |
| 0x38 | `CreatePoint` | - | result register | `ImmediateX`=first staged component index | - | - | Creates a point from the staged component sequence. `ImmediateX` is `0` for x, `1` for y, or `2` for z; missing components become `0`. |
| 0x39 | `CreateList` | - | result register | - | - | - | Creates a list from the contiguous staged value sequence immediately before the opcode. |
| 0x3A | `CreateMap` | - | result register | `SecondaryListIndex`=key names | - | - | Creates a map from key names and the contiguous staged value sequence immediately before the opcode. |
| 0x3B | `CreateRange` | - | result register | `XRegister`=from | `YRegister`=to | - | Creates a range value with implicit step `1`. |
| 0x3C | `CreateRangeWithStep` | - | result register | `XRegister`=from | `YRegister`=to | `AU`=step register | Creates a range value with explicit step. |
| 0x3D | `CreateRangeIterator` | - | iterator register | `XRegister`=from | `YRegister`=to | - | Creates a VM-internal range iterator with default step `+1`. |
| 0x3E | `CreateRangeIteratorWithStep` | - | iterator register | `XRegister`=from | `YRegister`=to | `AU`=step register | Creates a VM-internal range iterator with an explicit step. |
| 0x3F | `CreateRangeIteratorShort` | - | iterator register | `ImmediateX`=from | `ImmediateY`=to | `AS`=step | Creates a compact literal range iterator. |
| 0x40 | `CreateRecord` | - | result register | `BindId`=record bind id | - | - | Calls the record constructor bind with staged constructor-parameter values; computed fields are derived inside the constructor. |
| 0x41 | `CreateRecordValue` | - | result register | `XRegister`=map | `TypeOperand`=record type string | - | Creates the concrete custom record value from the constructor-produced field map. |
| 0x42 | `CreateExternalType` | - | result register | `BindId`=external type constructor reference | `ListIndex`=argument names | - | Constructs a host-bound external type value from named staged argument values. |
| 0x43 | `HasValue` | - | result register | `XRegister`=operand | - | - | Semantic value check; exact complement of `IsEmpty`. |
| 0x44 | `IsEmpty` | - | result register | `XRegister`=operand | - | - | Semantic emptiness check; true for `nothing`, `NaN`, and empty text/collections. |
| 0x45 | `Default` | - | result register | `XRegister`=left | `YRegister`=right | - | Presence/default operator. |
| 0x46..0x4F | reserved | - | - | - | - | - | Reserved tail of Group 1. |

### Group 2 - Boolean Algebra, Comparison, Math And Random

Group 2 numeric operations treat `nothing` as absent input: if any direct
numeric source operand is `nothing`, the result register receives `nothing`.
Otherwise, a present but non-computable numeric operation writes numeric `NaN`
to the result register. Scalar `Divide` keeps IEEE floating-point behavior for zero
denominators; `Modulo` and `Remainder` by zero write `NaN`.
Text is not implicitly numeric in Group 2. `Add` is the exception: when either
operand is text it concatenates text before numeric or collection handling.
Vector arithmetic supports compatible vector addition/subtraction, scalar
multiplication from either side, and vector-by-scalar division. Point arithmetic
supports only affine operations: point plus/minus vector and point minus point.
Point scaling, scalar divided by point, and point operands in integer division,
modulo, or remainder write numeric `NaN` unless a direct operand is `nothing`.
Unary negation of a vector negates its components; unary negation of a point
writes numeric `NaN`. `Power` preserves a left quantity unit only for a
unitless exponent of `1`, including `true` after numeric coercion; unit exponent
`0`, including `false`, produces a unitless result. Other quantity powers write
numeric `NaN`.
`Abs` preserves percentage values as percentages and preserves quantity
units on numeric quantities. Finite exactly integral numeric results are stored
as integer values when they fit signed 64-bit.
Percentage multiplication with a non-percentage scalar or quantity treats the
percentage as its stored ratio and writes a numeric result in the other
operand's value family, so `10% * 10` and `10 * 10%` both write numeric `1`.
`Equal` and `NotEqual` propagate `Nothing` when either operand
is absent. Exact equality first compares the numeric view for numeric-capable
operands (numbers, percentages, booleans, and dice sums), requiring identical
quantity units or no unit on both operands; this
also applies to integer fast paths. If numeric comparison does not apply, exact
equality requires the same value kind and kind-specific structural equality.
When either numeric side is represented as double precision, finite values
compare equal when their IEEE 754 values are within two ULPs. There is no
separate approximate-equality opcode.

| Hex | Opcode | UnitAndFlags | DestinationRegister | X | Y | Payload | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 0x50 | `Or` | - | result register | `XRegister`=left | `YRegister`=right | - | Tri-state boolean combine. |
| 0x51 | `And` | - | result register | `XRegister`=left | `YRegister`=right | - | Tri-state boolean combine. |
| 0x52 | `Xor` | - | result register | `XRegister`=left | `YRegister`=right | - | Tri-state boolean combine. |
| 0x53 | `Implies` | - | result register | `XRegister`=antecedent | `YRegister`=consequent | - | Binary implication combine. |
| 0x54 | `Not` | - | result register | `XRegister`=operand | - | - | Logical negation. |
| 0x55 | `Equal` | - | result register | `XRegister`=left | `YRegister`=right | - | Binary comparison. |
| 0x56 | `NotEqual` | - | result register | `XRegister`=left | `YRegister`=right | - | Binary comparison. |
| 0x57 | `Less` | - | result register | `XRegister`=left | `YRegister`=right | - | Binary comparison. |
| 0x58 | `Greater` | - | result register | `XRegister`=left | `YRegister`=right | - | Binary comparison. |
| 0x59 | `LessOrEqual` | - | result register | `XRegister`=left | `YRegister`=right | - | Binary comparison. |
| 0x5A | `GreaterOrEqual` | - | result register | `XRegister`=left | `YRegister`=right | - | Binary comparison. |
| 0x5B | `Add` | - | result register | `XRegister`=left | `YRegister`=right | - | Binary numeric operation. |
| 0x5C | `Subtract` | - | result register | `XRegister`=left | `YRegister`=right | - | Binary numeric operation. |
| 0x5D | `Multiply` | - | result register | `XRegister`=left | `YRegister`=right | - | Binary numeric operation. |
| 0x5E | `Divide` | - | result register | `XRegister`=left | `YRegister`=right | - | Binary numeric operation. |
| 0x5F | `Power` | - | result register | `XRegister`=left | `YRegister`=right | - | Binary numeric operation. |
| 0x60 | `IntegerDivide` | - | result register | `XRegister`=left | `YRegister`=right | - | Floor-like integer division operation. |
| 0x61 | `Modulo` | - | result register | `XRegister`=left | `YRegister`=right | - | Numeric modulo operation. |
| 0x62 | `Remainder` | - | result register | `XRegister`=left | `YRegister`=right | - | Numeric remainder operation. |
| 0x63 | `Min` | - | result register | `XRegister`=left | `YRegister`=right | - | Binary extrema reduce step. |
| 0x64 | `Max` | - | result register | `XRegister`=left | `YRegister`=right | - | Binary extrema reduce step. |
| 0x65 | `Negate` | - | result register | `XRegister`=operand | - | - | Numeric negation. |
| 0x66 | `Abs` | - | result register | `XRegister`=operand | - | - | Absolute value. |
| 0x67 | `LogN` | - | result register | `XRegister`=operand | - | - | Natural logarithm. |
| 0x68 | `Chance` | - | result register | `XRegister`=operand | - | - | Chance evaluation. |
| 0x69 | `Clamp` | - | result register | `XRegister`=value | `YRegister`=minimum | `AU`=maximum register | The only opcode with three direct source registers. |
| 0x6A | `RandomTake` | - | result register | `XRegister`=from | `YRegister`=to | - | Takes an integer random value from integer bounds using the current random scope. |
| 0x6B | `RandomTakeFloat` | - | result register | `XRegister`=from | `YRegister`=to | - | Takes a float random value from numeric bounds using the current random scope. |
| 0x6C | `RandomPush` | - | - | `XRegister`=seed | - | - | Saves the active random state and resets it from a valid dynamic unitless integer seed; an invalid seed retains the copied state. |
| 0x6D | `RandomPushConstant` | - | - | - | - | `I64`=signed seed | Saves the active random state and resets it from an inline signed `Int64`. |
| 0x6E | `RandomPop` | - | - | - | - | - | Restores the previous random scope. |
| 0x6F | `Term` | - | result register | `XRegister`=series | `YRegister`=index | - | Reads a zero-based mathematical series term; non-series sources yield `nothing`. |
| 0x70 | `Exp` | - | result register | `XRegister`=operand | - | - | Natural exponential. Unitless numeric input only; overflow to `+Infinity` is valid. |
| 0x71 | `Floor` | - | result register | `XRegister`=operand | - | - | Floors a unitless numeric value and writes an integer. |
| 0x72 | `Ceil` | - | result register | `XRegister`=operand | - | - | Ceils a unitless numeric value and writes an integer. |
| 0x73 | `Truncate` | - | result register | `XRegister`=operand | - | - | Truncates a unitless numeric value toward zero and writes an integer. |
| 0x74 | `RoundHalfEven` | - | result register | `XRegister`=operand | - | - | Rounds a unitless numeric value using midpoint-to-even and writes an integer. |
| 0x75 | `RoundHalfUp` | - | result register | `XRegister`=operand | - | - | Rounds midpoint values away from zero and writes an integer. |
| 0x76 | `RoundHalfDown` | - | result register | `XRegister`=operand | - | - | Rounds midpoint values toward zero and writes an integer. |
| 0x77 | `DegreeToRadians` | - | result register | `XRegister`=operand | - | - | Converts degrees to unitless radians. |
| 0x78 | `DegreeFromRadians` | - | result register | `XRegister`=operand | - | - | Converts unitless radians to a degree quantity. |
| 0x79 | `WrapDegree` | - | result register | `XRegister`=operand | - | - | Normalizes a degree or unitless numeric value into `[0, 360)` degrees. |
| 0x7A | `Sin` | - | result register | `XRegister`=operand | - | - | Sine. Unitless input is radians; degree input is converted to radians; result is unitless. |
| 0x7B | `Cos` | - | result register | `XRegister`=operand | - | - | Cosine. Unitless input is radians; degree input is converted to radians; result is unitless. |
| 0x7C | `Tan` | - | result register | `XRegister`=operand | - | - | Tangent. Unitless input is radians; degree input is converted to radians; result is unitless. |
| 0x7D | `Asin` | - | result register | `XRegister`=operand | - | - | Inverse sine for unitless numeric input in `[-1, 1]`; result is radians. |
| 0x7E | `Acos` | - | result register | `XRegister`=operand | - | - | Inverse cosine for unitless numeric input in `[-1, 1]`; result is radians. |
| 0x7F | `Atan` | - | result register | `XRegister`=operand | - | - | Inverse tangent for unitless numeric input; result is radians. |
| 0x80 | `Atan2` | - | result register | `XRegister`=y | `YRegister`=x | - | `atan2(y, x)` with matching units; result is radians. `(0, 0)` yields `0`. |
| 0x81 | `Hypot2D` | - | result register | `XRegister`=x | `YRegister`=y | - | 2D hypotenuse; matching units are retained in the result. |
| 0x82 | `Hypot3D` | - | result register | `XRegister`=x | `YRegister`=y | `AU`=z | 3D hypotenuse; matching units are retained in the result. |
| 0x83 | `Distance` | - | result register | `XRegister`=left | `YRegister`=right | - | Scalar distance or 3D vector/point distance; matching units are retained. |
| 0x84 | `Distance2D` | - | result register | `XRegister`=x1 | `YRegister`=y1 | `AU`=x2, `BU`=y2 | 2D coordinate distance; matching units are retained. |
| 0x85 | `Distance3D` | - | result register | `XRegister`=x1 | `YRegister`=y1 | `AU`=z1, `BU`=x2, `CU`=y2, `DU`=z2 | 3D coordinate distance; matching units are retained. |
| 0x86 | `DistanceSquared` | - | result register | `XRegister`=left | `YRegister`=right | - | Scalar or 3D vector/point squared distance; result is unitless. |
| 0x87 | `DistanceSquared2D` | - | result register | `XRegister`=x1 | `YRegister`=y1 | `AU`=x2, `BU`=y2 | 2D squared coordinate distance; result is unitless. |
| 0x88 | `DistanceSquared3D` | - | result register | `XRegister`=x1 | `YRegister`=y1 | `AU`=z1, `BU`=x2, `CU`=y2, `DU`=z2 | 3D squared coordinate distance; result is unitless. |
| 0x89 | `LengthSquared` | - | result register | `XRegister`=operand | - | - | Scalar square or vector/point squared length; result is unitless. |
| 0x8A | `LengthSquared2D` | - | result register | `XRegister`=x | `YRegister`=y | - | 2D squared length; result is unitless. |
| 0x8B | `LengthSquared3D` | - | result register | `XRegister`=x | `YRegister`=y | `AU`=z | 3D squared length; result is unitless. |
| 0x8C | `Normalize` | - | result register | `XRegister`=operand | - | - | Normalizes a 3D vector/point and returns a unitless vector; zero length yields `nothing`. |
| 0x8D | `Normalize2D` | - | result register | `XRegister`=x | `YRegister`=y | - | Normalizes 2D coordinates and returns a unitless vector with `z=0`; zero length yields `nothing`. |
| 0x8E | `Normalize3D` | - | result register | `XRegister`=x | `YRegister`=y | `AU`=z | Normalizes 3D coordinates and returns a unitless vector; zero length yields `nothing`. |
| 0x8F | `Dot` | - | result register | `XRegister`=left | `YRegister`=right | - | 3D vector/point dot product with matching units; result is unitless. |
| 0x90 | `Dot2D` | - | result register | `XRegister`=x1 | `YRegister`=y1 | `AU`=x2, `BU`=y2 | 2D coordinate dot product; result is unitless. |
| 0x91 | `Dot3D` | - | result register | `XRegister`=x1 | `YRegister`=y1 | `AU`=z1, `BU`=x2, `CU`=y2, `DU`=z2 | 3D coordinate dot product; result is unitless. |
| 0x92 | `Cross` | - | result register | `XRegister`=left | `YRegister`=right | - | 3D vector/point cross product with matching units; result is a unitless vector. |
| 0x93 | `Cross2D` | - | result register | `XRegister`=x1 | `YRegister`=y1 | `AU`=x2, `BU`=y2 | 2D coordinate cross product; result is the scalar z component, unitless. |
| 0x94 | `Cross3D` | - | result register | `XRegister`=x1 | `YRegister`=y1 | `AU`=z1, `BU`=x2, `CU`=y2, `DU`=z2 | 3D coordinate cross product; result is a unitless vector. |
| 0x95 | `AngleBetween` | - | result register | `XRegister`=left | `YRegister`=right | - | 3D vector/point angle in radians; zero length yields `nothing`. |
| 0x96 | `AngleBetween2D` | - | result register | `XRegister`=x1 | `YRegister`=y1 | `AU`=x2, `BU`=y2 | 2D coordinate angle in radians; zero length yields `nothing`. |
| 0x97 | `AngleBetween3D` | - | result register | `XRegister`=x1 | `YRegister`=y1 | `AU`=z1, `BU`=x2, `CU`=y2, `DU`=z2 | 3D coordinate angle in radians; zero length yields `nothing`. |
| 0x98..0x9F | reserved | - | - | - | - | - | Reserved tail of Group 2 after math/navigation expansion. |

### Group 3 - Text, Collections, Iterators

| Hex | Opcode | UnitAndFlags | DestinationRegister | X | Y | Payload | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 0xA0 | `TakeFirst` | - | result register | `XRegister`=source | `ImmediateY`=count | - | Takes the first `Y` values from a series, list, dice, range, or iterator source. |
| 0xA1 | `DropFirst` | - | result register | `XRegister`=source | `ImmediateY`=count | - | Drops the first `Y` values from a series, list, dice, range, or iterator source. |
| 0xA2 | `TakeLast` | - | result register | `XRegister`=source | `ImmediateY`=count | - | Takes the last `Y` values from a finite list, dice, range, or iterator source; series yields `nothing`. |
| 0xA3 | `DropLast` | - | result register | `XRegister`=source | `ImmediateY`=count | - | Drops the last `Y` values from a finite list, dice, range, or iterator source; series yields `nothing`. |
| 0xA4 | `TakeHighest` | - | result register | `XRegister`=source | `ImmediateY`=count | - | Takes the highest `Y` values from a finite list, dice, range, or iterator source; series yields `nothing`. |
| 0xA5 | `TakeLowest` | - | result register | `XRegister`=source | `ImmediateY`=count | - | Takes the lowest `Y` values from a finite list, dice, range, or iterator source; series yields `nothing`. |
| 0xA6 | `DropHighest` | - | result register | `XRegister`=source | `ImmediateY`=count | - | Drops the highest `Y` values from a finite list, dice, range, or iterator source; series yields `nothing`. |
| 0xA7 | `DropLowest` | - | result register | `XRegister`=source | `ImmediateY`=count | - | Drops the lowest `Y` values from a finite list, dice, range, or iterator source; series yields `nothing`. |
| 0xA8 | `OneRandom` | - | result register | `XRegister`=source | - | - | Chooses one random element from a finite list, dice, range, or iterator source; empty/invalid/series sources yield `nothing`. |
| 0xA9 | `TakeRandom` | - | result register | `XRegister`=source | `ImmediateY`=count | - | Chooses up to `Y` random elements without replacement. Lists stay lists, dice stay dice, ranges and iterators materialize as lists. |
| 0xAA | `OneWeighted` | - | result register | `XRegister`=items list | `YRegister`=weights list | - | Selects one item using positive finite weights. Empty/no-positive-weight lists -> `nothing`. |
| 0xAB | `TakeWeighted` | - | result register | `XRegister`=items list | `ImmediateY`=count | `AU`=weights list register | Selects up to `Y` items without replacement using positive finite weights. Result is a list. |
| 0xAC | `Count` | - | result register | `XRegister`=source | - | - | Counts collection/string/range/map items or consumes an iterator to count. |
| 0xAD | `StartsWith` | - | result register | `XRegister`=left | `YRegister`=right | - | Text/tag raw-text prefix check or list/dice/range sequence prefix check. |
| 0xAE | `EndsWith` | - | result register | `XRegister`=left | `YRegister`=right | - | Text/tag raw-text suffix check or list/dice/range sequence suffix check. |
| 0xAF | `Contains` | - | result register | `XRegister`=needle | `YRegister`=container | - | Text/tag substring, map key, list/dice/range membership, or vector/point component membership. |
| 0xB0 | `ContainsAny` | - | result register | `XRegister`=needles | `YRegister`=container | - | Tests whether the container contains any value from the needle sequence. Iterators are consumed until the first match or exhaustion. |
| 0xB1 | `ContainsAll` | - | result register | `XRegister`=needles | `YRegister`=container | - | Tests whether the container contains every value from the needle sequence. Iterators are consumed until all needles match or exhaustion. |
| 0xB2 | `HasAny` | - | result register | `XRegister`=source | - | - | Truthiness `any` over list/dice/range/map values/text/tag/vector/point or iterator sources. Empty -> `false`; `nothing` -> `nothing`. |
| 0xB3 | `HasAll` | - | result register | `XRegister`=source | - | - | Truthiness `all` over list/dice/range/map values/text/tag/vector/point or iterator sources. Empty -> `true`; `nothing` -> `nothing`. |
| 0xB4 | `ContainsValue` | - | result register | `XRegister`=needle | `YRegister`=container | - | Value membership for map-like containers, vectors, and points. |
| 0xB5 | `Union` | - | result register | `XRegister`=left | `YRegister`=right | - | Collection union/merge for list, dice, and map shapes; invalid shapes -> `nothing`. |
| 0xB6 | `Intersect` | - | result register | `XRegister`=left | `YRegister`=right | - | Map key intersection or list/dice multiset intersection; invalid shapes -> `nothing`. |
| 0xB7 | `Zip` | - | result register | `XRegister`=left | `YRegister`=right | - | List zip into `{ left, right }` maps up to the shorter length; invalid shapes -> `nothing`. |
| 0xB8 | `KeysOfMap` | - | result register | `XRegister`=operand | - | - | Map/custom-type keys projection; `nothing` and non-map operands produce `nothing`. |
| 0xB9 | `ValuesOfMap` | - | result register | `XRegister`=operand | - | - | Map/custom-type values projection; `nothing` and non-map operands produce `nothing`. |
| 0xBA | `EntriesOfMap` | - | result register | `XRegister`=operand | - | - | Map/custom-type entries projection; `nothing` and non-map operands produce `nothing`. |
| 0xBB | `First` | - | result register | `XRegister`=source | - | - | Returns the first element from list, dice, range, map/custom values, text/tag, or iterator; invalid/empty sources -> `nothing`. |
| 0xBC | `Last` | - | result register | `XRegister`=source | - | - | Returns the last element from list, dice, range, map/custom values, text/tag, or iterator; invalid/empty sources -> `nothing`. |
| 0xBD | `Single` | - | result register | `XRegister`=source | - | - | Returns the only element from list, dice, range, map/custom values, text/tag, or iterator; invalid/empty/multiple-element sources -> `nothing`. |
| 0xBE | `IteratorCreate` | - | iterator register | `XRegister`=collection | - | - | Creates a VM-internal iterator over a collection or range value. Non-iterable sources write `nothing`. |
| 0xBF | `IteratorCreateOrJump` | - | iterator register | `XRegister`=collection | `TargetAddress`=not-iterable | - | Creates a VM-internal iterator, or writes `nothing` and jumps to `Y` when no iterator can be created. |
| 0xC0 | `IteratorNext` | - | item register | `XRegister`=iterator | `TargetAddress`=no-more | - | Writes the next item and continues, or jumps to `Y` when exhausted. |
| 0xC1 | `IteratorClose` | - | - | `XRegister`=iterator | - | - | Disposes/closes a VM-internal iterator. |
| 0xC2 | `Distinct` | - | result register | `XRegister`=source | - | - | Materializes distinct source items in source order. Supports direct collection fast paths and iterators. |
| 0xC3 | `SortAscending` | - | result register | `XRegister`=source | - | - | Sorts source items ascending. Supports direct list, dice, range, and iterator sources. |
| 0xC4 | `SortDescending` | - | result register | `XRegister`=source | - | - | Sorts source items descending. Supports direct list, dice, range, and iterator sources. |
| 0xC5 | `Reverse` | - | result register | `XRegister`=source | - | - | Reverses list, dice, range, or iterator sources. Dice and iterators materialize lists; ranges stay ranges. |
| 0xC6 | `Shuffle` | - | result register | `XRegister`=source | - | - | Shuffles list, dice, range, or iterator sources. Result is a list. |
| 0xC7 | `ListBuilderCreate` | - | builder register | - | - | - | Creates a VM-internal list builder for generated collections. |
| 0xC8 | `ListBuilderAdd` | - | - | `XRegister`=builder | `YRegister`=item | - | Adds an item and checks `MaxGeneratedCollectionItems`. |
| 0xC9 | `ListBuilderFinish` | - | result register | `XRegister`=builder | - | - | Materializes the list builder as a list. |
| 0xCA | `MapBuilderCreate` | - | builder register | - | - | - | Creates a VM-internal map builder for generated map projections. |
| 0xCB | `MapBuilderAdd` | - | - | `XRegister`=builder | `YRegister`=key | `AU`=value register | Adds or overwrites a map entry; empty/nothing keys are skipped. |
| 0xCC | `MapBuilderFinish` | - | result register | `XRegister`=builder | - | - | Materializes the map builder as a map. |
| 0xCD | `DistinctBuilderCreate` | - | builder register | - | - | - | Creates a VM-internal builder for `distinct by` loop lowering. |
| 0xCE | `DistinctBuilderAdd` | - | - | `XRegister`=builder | `YRegister`=key | `AU`=value register | Adds the value only when the key was not seen before. |
| 0xCF | `DistinctBuilderFinish` | - | result register | `XRegister`=builder | - | - | Materializes the distinct-by builder as a list. |
| 0xD0 | `GroupBuilderCreate` | - | builder register | - | - | - | Creates a VM-internal builder for `group by` loop lowering. |
| 0xD1 | `GroupBuilderAdd` | - | - | `XRegister`=builder | `YRegister`=key | `AU`=value register | Adds the value to the group identified by the key text. |
| 0xD2 | `GroupBuilderFinish` | - | result register | `XRegister`=builder | - | - | Materializes the group builder as a map from keys to grouped lists. |
| 0xD3 | `OrderBuilderCreate` | - | builder register | - | - | - | Creates a VM-internal builder for `order by` loop lowering. |
| 0xD4 | `OrderBuilderAdd` | - | - | `XRegister`=builder | `YRegister`=key | `AU`=value register | Adds a key/value pair preserving source order for stable ordering. |
| 0xD5 | `OrderBuilderFinishAscending` | - | result register | `XRegister`=builder | - | - | Sorts by stored keys ascending and materializes the ordered values as a list. |
| 0xD6 | `OrderBuilderFinishDescending` | - | result register | `XRegister`=builder | - | - | Sorts by stored keys descending and materializes the ordered values as a list. |
| 0xD7 | `HasPattern` | - | result register | `XRegister`=source/iterator | `ImmediateY`=count for count patterns | `AU`=pattern kind, `BU`=face register for `CountFace` | Tests a dice/card pattern and returns boolean. |
| 0xD8 | `TakePattern` | - | result register | `XRegister`=source/iterator | `ImmediateY`=count for count patterns | `AU`=pattern kind, `BU`=face register for `CountFace` | Takes items matching a dice/card pattern. Dice sources produce dice; list sources produce lists. |
| 0xD9..0xFF | reserved | - | - | - | - | - | Reserved tail of Group 3 for future collection, iterator, pipeline, extension, or VM opcodes. |

## Side-Table Summary

| Pool / table | Used by |
| --- | --- |
| `StringPool` | `LoadText`, `LoadTag`, `MemberAccess`; indirectly through message/name lists in `UShortListPool` |
| `UShortListPool` | `LoadHandler`, `EmitMessage*`, `PublishMessage*`, `CreateExternalType`, `CreateMap`, `BindHandler`, `CallExternal*` |
| `OutboundMessageSignatures` / binary `OutboundMessage` binds | Statically shaped `emit`/`publish` message signatures, used by loaders without scanning code |

Local calls, predicate calls, construction, extension calls, and collection/message
builder opcodes reference bind ids, entry addresses, or `StringPool`/`UShortListPool`
directly from the instruction word. Record constructor code addresses live in
`Record` bind entries, not in `CreateRecord` instructions.
Pipeline selectors are lowered into linear helper entries and fixed iterator or
terminal opcodes; there are no pipeline selector or pattern pools.
