# GameEventScript Bytecode Spec

This document defines the intended portable bytecode shape for
`GameEventScriptCompiled` and the `GameEventScriptBinary` container.
The goal is a compact, portable, high-level bytecode for the GameEventScript DSL
that is naturally executable by a linear program-counter VM.

The public bytecode model is **operand-stack-free**. Normal expression
evaluation reads from and writes to explicit local slots. A portable call stack
is still part of the VM target state for calls, return addresses, frame
metadata, scoped locals, and resumable execution. The C# call stack is not part
of script control flow.

The public artifact exposes one global linear `Code` segment, `MaxFrameSlots`,
entry addresses for handlers, callables, and type-field helpers, plus normalized
tables for strings, ushort lists, external references, debug metadata, and
pipelines. High-level language constructs lower either to normal linear
instructions or to explicit opcodes that reference normalized tables. Function
and predicate calls use public callable entry addresses in VM-owned frames.
Helper expressions run through linear entry addresses and isolate their
temporary slots from handler locals and scope-change tracking. Predicate calls
use direct `CallPredicate` instructions with target entry addresses and argument
slot-list indexes. Local calls use direct `Call` instructions with target entry
addresses and argument slot-list indexes. Extension calls use direct
`CallStandard*` or `CallExternal*` instructions with argument slot lists.
Variadic operators, type constructors, local builders, message literals,
handler binding, casts, type checks, member access, and seeded-random
expressions are layout-free direct instructions.

## Goals

- Represent executable script code as one linear instruction memory.
- Make every executable position addressable by a stable instruction address.
- Use labels only as debug/dump symbols that point at instruction addresses.
- Use slot/register-style instructions: every value-producing instruction writes
  to a destination slot and reads operands from source slots or pools.
- Compile control flow such as `if`, loops, guarded choices, predicates, and
  functions into jumps and calls.
- Keep domain-heavy collection operations high-level when that is faster or
  simpler than expanding them into many tiny instructions.
- Preserve streaming and short-circuit behavior where the language requires it.
- Keep the public artifact portable: no VM session state, no C# delegates, no
  bound extension functions, no AST nodes, and no runtime `GameEventScriptValue`
  constants.

## Non-Goals

- No public exposure of `StepH.GameEventScript.BytecodeVM` implementation types.
- No requirement that the diagnostic dump format matches the binary `.gesb`
  encoding.
- No requirement to split every DSL operation into primitive opcodes.
- No operand stack for expression evaluation.
- No C# call stack dependency for normal script control flow.

## Top-Level Artifact

The target binary container is named `GameEventScriptBinary`. Its header is the
fixed 16-byte `GameEventScriptBinaryHeader` shape:

```text
GameEventScriptBinaryHeader
  Magic:      4 bytes  "GESB"
  Version:    u16      current 1
  Flags:      u16      reserved
  HeaderSize: u32      current 16
  FileSize:   u32      0 while represented only in memory
```

The first normalized binary tables are intentionally compact:

```text
GameEventScriptBinary
  Header
  ModuleName
  StringPool              zero-based UTF-8 strings in the file
  BindTable
    Kind                  0x10-0x1F export, 0x20-0x2F import
    MessageHandler | Function | Predicate | ExtensionCall | ExternalType
    Name                  string-pool index
    ArgumentNames         ordered string-pool indexes
    EntryAddress          global code address for exports, 0 for imports
```

Imports leave `EntryAddress` at `0`. They are linked by table index from
instructions or side tables; extension and external-type implementation code is
not serialized into the script binary. Export bind kinds occupy `0x10` through
`0x1F`; import bind kinds occupy `0x20` through `0x2F`. Extension-call import
names are currently stored as `extension.function`; external-type import names
are stored as the type name.

The public `GameEventScriptCompiled` model is shaped around a single code
segment and side tables:

```text
GameEventScriptCompiled
  ModuleName
  FormatVersion
  StringPool
  UShortListPool
  ExternalReferences
  ExternalTypeConstructorReferences
  Code: Instruction[]
  Handlers: HandlerEntry[]
  Callables: CallableEntry[]
  TypeDefinitions: TypeDefinitionEntry[]
  PipelinePatternPool
  PipelineObjectPatternPool
  PipelineSelectorPool
  PipelinePool
  DebugSegment
  MaxFrameSlots
  MaxCallStackDepth
```

Existing string and compact list pools remain important. Constants that fit the
linear instruction shape are encoded directly in typed load instructions:
booleans and `nothing` have dedicated opcodes; integer and float payloads use
raw 64-bit bits split over `A`/`B`; text, tags, and handler message names point
into `StringPool`.

Current C# public surface:

```text
GameEventScriptCompiled
  StringPool
  UShortListPool
  ExternalReferences
  ExternalTypeConstructorReferences
  Code: IReadOnlyList<GameEventScriptBytecodeInstruction>
  Handlers
  Callables
  TypeDefinitions
  MaxFrameSlots
  DebugSegment
  PipelinePatternPool
  PipelineObjectPatternPool
  PipelineSelectorPool
  PipelinePool

GameEventScriptBytecodeInstruction
  OpCode
  Dest
  A
  B
  C
```

`MaxFrameSlots` is the maximum local slot count needed by any handler or
callable frame, including parameters, user `let` bindings, compiler temporaries,
loop temporaries, and high-level operation temporaries.

## Portable Constants

The linear bytecode has no object-shaped constant pool. Constants that fit the
instruction word are encoded by typed load opcodes:

- `LoadNothing`, `LoadTrue`, and `LoadFalse` have no payload.
- `LoadInteger` stores a signed 64-bit integer in the overlapped `I64` payload,
  plus optional numeric unit in `UnitAndFlags`.
- `LoadFloat` stores IEEE 754 double bits in the overlapped `F64` payload, plus
  optional numeric unit in `UnitAndFlags`. `NaN`, `Infinity`, and `-Infinity`
  are represented by their IEEE bit patterns. `UnitAndFlags=Percentage` makes
  the same opcode load a percentage ratio.
- `LoadText` and `LoadTag` store a `StringPool` index in `C`.
- `LoadHandler` stores a `UShortListPool` message-shape index in `A`. The shape
  list is `[messageNameStringIndex, argumentNameStringIndex...]`.

Constants are concrete. For example `:integer 1`, `:float 1`, `1m`, and `1s`
must remain distinct loads even if runtime operations can compare or coerce
some of them. Larger constants such as `Uuid` or future vector/point literals
should use a normalized data segment instead of reintroducing an object
constant pool.

### Numeric Unit Encoding Target

`UnitAndFlags` is a byte. Its target portable layout is:

```text
bits 0..4  UnitId        0..31
bits 5..7  Reserved      must be zero in portable bytecode
```

Reserved bits are intended to be consumed from the most significant bit
downward. Bit 5 is kept as the last-resort reserved bit so the UnitId field can
grow to 6 bits later if those opcode flags are never needed.

The target UnitId map keeps the currently implemented ids stable and reserves
room for likely game-domain units:

| UnitId | Unit | Intended use |
| --- | --- | --- |
| `0` | none | Unitless scalar. |
| `1` | degree | Angles, headings, rotations. |
| `2` | meter | Positions, distances, ranges, radii. |
| `3` | second | Durations, cooldowns, tick time. |
| `4` | ratio / percent | Chances, multipliers, resistance; `%` literals store ratios. |
| `5` | meter per second | Speed and velocity magnitude. |
| `6` | meter per second squared | Acceleration. |
| `7` | kilogram | Mass, load, inertia. |
| `8` | newton | Force, thrust, recoil. |
| `9` | joule | Energy, battery charge, heat energy. |
| `10` | watt | Power, generator output, energy consumption over time. |
| `11` | volt | Voltage for electrotechnical systems. |
| `12` | ampere | Current, charge flow, overload/thermal balancing. |
| `13` | hertz | Frequency, fire rate, polling, radio cadence. |
| `14` | bit | Information amount. |
| `15` | byte | Storage amount. |
| `16` | bit per second | Bandwidth and communication throughput. |
| `17` | kelvin | Temperature; Celsius/Fahrenheit syntax should normalize to Kelvin. |
| `18..31` | reserved | Future built-in or domain units. |

The compiler must only emit units supported by the current runtime. The extended
map is a binary-format target so future unit additions do not need to reshape
the instruction word.

## Parameter Type Hints

Handlers, predicates, and functions may declare optional parameter type hints
using the same `as :type` language as value coercion:

```eventscript
on DamageTaken(unit as :unit, amount as :integer) {
    publish DamageApplied(unit: unit, amount: amount)
}

predicate wounded(_ unit as :unit) means unit.hp < unit.maxHp

function livingUnits(_ units as :list) means
    units[:filter unit where not (unit is wounded)]
```

Parameter type hints are not part of message or callable identity.

Predicates:

- `DamageTaken(unit,amount)` remains the signature id, regardless of parameter
  type hints.
- Callable signatures still use names, labels, and positions, not types.
- There is no overload resolution by type.
- Different semantic meanings should use different message/predicate/function
  names instead of type overloads.
- A parameter type hint means "coerce this bound value as the entry starts".
- Coercion is lenient and uses the same conversion predicates as
  `value as :type`.
- After entry coercion, the compiler and VM may treat the local slot as
  normalized to that type for optimization.

The portable metadata carries the hint:

```text
ParameterEntry
  ExternalLabel
  LocalName
  Slot
  TypeName?
```

The executable code should contain explicit coercion instructions so the program
counter and dump show where normalization happens:

```text
@0000 BindParameter dst=s0 arg=unit
@0001 CastCustom dst=s0 src=s0 type=:unit
@0002 BindParameter dst=s1 arg=amount
@0003 CastInteger dst=s1 src=s1
```

For untyped parameters the compiler emits only `BindParameter`.

## Instruction Addresses

An instruction address is the zero-based index into `Code`.

```text
@0000 LoadInteger dst=s3 value=3
@0001 Add dst=s4 left=s1 right=s2
@0002 JumpIfNotTrue cond=s4 target=@0010
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

The portable instruction is compact and allocation-free to execute. The public
instruction word is a 12-byte explicit-layout value with typed overlapping
views:

```text
Instruction
  OpCode
  UnitAndFlags
  Dest_U16
  A_U16, B_U16, C_U16, D_U16
  A_I16, B_I16, C_I16, D_I16
  A_I32, B_I32
  A_U32, B_U32
  I64, U64, F64
```

Operands are interpreted by opcode:

- `Dest_U16`: destination slot for value-producing instructions.
- `A_U16`, `B_U16`, `C_U16`, `D_U16`: slots, branch targets, pool indices, or
  small unsigned immediates.
- `A_I16`, `B_I16`, `C_I16`, `D_I16`: compact signed immediates for opcodes
  that explicitly document signed 16-bit operands.
- `A_I32`/`B_I32` and `A_U32`/`B_U32`: reserved compact 32-bit immediate views.
- `I64`, `U64`, `F64`: inline literal payloads for integer, unsigned seed, and
  double/float loads.
- Branch opcodes use `A_U16` as the target address and conditional jumps use
  `C_U16` as the condition slot.
- Loop/block opcodes use `A_U16` as the body target, `B_U16` as the end target,
  and `C_U16` as the side-table layout index.
- Side-table-backed value opcodes use `C_U16` as the layout/data index. Their
  full operand lists live in side tables; `A_U16` and `B_U16` may mirror the
  first two slots for fast/direct access.

Unused instruction fields are undefined and ignored. The instruction word does
not use sentinel operands for optional operands. Optional forms are represented
by distinct opcodes such as `ReturnNothing` and
`EmitMessageWithTags`, or by concrete empty pool entries. Side-table metadata may
still define its own optional `-1` fields where the table schema explicitly
allows them.

Opcode values are grouped in 16-value blocks by operation family. The VM still
dispatches directly on the full opcode byte; the group nibble is a portable
layout convention and may be used by validators, dumpers, or future decoders.
The current groups are:

```text
0x00 core loads, slot movement, branches, returns
0x10 generic boolean, comparison, arithmetic, and default operations
0x20 primitive integer fast-path operations
0x30 unary, random, dice, and range value operations
0x40 collection/text operations, key/value projections, short-circuit markers
0x50 primitive/domain casts
0x60 collection/message/reference casts
0x70 primitive/domain type checks
0x80 collection/message/reference type checks
0x90 construction, access, handlers, predicates, calls
0xA0 scopes and message emit/publish operations
0xB0 iterators and collection builders
0xC0 reserved
0xD0 pipeline operations; 0xD1..0xDF reserved for pipeline expansion
0xE0 reserved
0xF0 reserved
```

For JSON transport, instructions serialize as a normalized four-field object:

```json
{ "Opcode": "LoadInteger", "Flags": "0x04", "Dst": "0x0007", "Parameter": "0x000000000000002A" }
```

`Flags` is the raw `UnitAndFlags` byte, `Dst` is the raw destination slot, and
`Parameter` is the raw unsigned 64-bit payload covering bytes `4..11`. For
slot/index/target instructions the payload packs `A`, `B`, `C`, and `D` into
successive 16-bit lanes. For literal instructions the same payload carries
`I64`, `U64`, or the IEEE-754 `F64` bit pattern.

Large structured metadata belongs in side tables and pools, not nested
instruction objects. Examples: `UShortListPool` message shapes/slot lists,
pipeline pools, pipeline selector/pattern pools, and diagnostic layouts.

An instruction that produces a `nothing` value writes it to `Dest_U16`. Returning
without a value uses `ReturnNothing`; returning a slot value uses `Return
A_U16`. There is no implicit push. There are no operand-stack `Pop` or
`Duplicate` instructions in the portable target model.

## Execution Model

A script VM session owns mutable execution state:

```text
pc
frames[]
callStack[]
scopeStack[]
randomStack[]
activeHighLevelOperationState
```

Each frame owns a local slot array or a frame slice in a shared slot memory:

```text
Frame
  EntryKind
  EntryIndex
  ReturnAddress
  ReturnDestinationSlot
  FrameBase
  SlotCount
  ScopeMark
  RandomMark
```

The C# stack is not part of script control flow. `Call` pushes a portable frame
record onto `callStack`; `Return` restores the next `pc` and writes the returned
value to the caller's destination slot.

Manual stepping pauses after the instruction budget is consumed. The current
`pc` points the dump/debugger at the active instruction. If execution is inside
a high-level operation such as a pipeline, the VM may also expose
operation-local debug state such as selector index or item index.

## Entry Tables

Handlers and callables are metadata over the shared code segment.

```text
HandlerEntry
  MessageName
  DispatchKind: ExactSignature | MessageEnvelope
  SignatureId
  Parameters: ParameterEntry[]
  SignatureLabels
  RequiredTags[]
  ExcludedTags[]
  EntryAddress
  LocalSlotCount
  DeclarationOrder

CallableEntry
  Name
  Kind: Predicate | Function
  Parameters: ParameterEntry[]
  SignatureLabels
  SignatureId
  EntryAddress
  LocalSlotCount
  ReturnSlot
```

`LocalSlotCount` includes parameters, declared locals, and compiler temporaries.
Temporary slots are implementation details but are part of the portable frame
layout for the bytecode version.

`ExactSignature` handlers subscribe by `SignatureId`. `MessageEnvelope` handlers
subscribe by `MessageName` and tag filters only; the runtime invokes them with a
single `:envelope` argument whose `message` field is the original message.
Message-envelope handlers count as normal delivery and therefore prevent
`undeliverable` fallback when their tag filters match. For `MessageEnvelope`
handlers, `SignatureId` still records the synthetic envelope parameter signature
such as `Damage(envelope)`, but it is metadata and not the dispatch key.

System endpoint entries use reserved lowercase names outside normal message
casing and bind their envelope with explicit `as` syntax. The currently defined
endpoint is:

```text
undeliverable as envelope
```

It is encoded as a `MessageEnvelope` handler entry with message name
`undeliverable`. It receives a dictionary-backed `:envelope` value when no
`ExactSignature` or `MessageEnvelope` handler could be queued for the original
message after signature/name and tag filters were applied. System endpoints
still use normal handler metadata, priority, declaration order, and tag filters.

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
  ReturnSlot
```

These addresses point at expression code that writes one value into the declared
return slot. During record construction, computed-field and clamp helpers run in
helper frames whose visible slots contain the already materialized field values.
Temporary slots for these helpers must start after the source field slots so a
helper cannot overwrite its own inputs.

## Opcode Families

The opcode set should be high-level enough for GameEventScript, but flat enough
for `pc`-based execution.

### Slots and Coercion

- `LoadNothing dst`
- `LoadTrue dst`
- `LoadFalse dst`
- `LoadInteger dst i64 unitAndFlags`
- `LoadFloat dst f64 unitAndFlags`
- `LoadText dst stringIndex`
- `LoadTag dst stringIndex`
- `LoadHandler dst messageNameIndex namedArgumentLayoutIndex`
- `Move dst src`
- `BindParameter dst parameterIndex`
- `CastBoolean`/`CastInteger`/`CastFloat`/etc. `dst src`
- `CastCustom dst src nameIndex`
- `TypeCheckBoolean`/`TypeCheckInteger`/etc. `dst src`
- `TypeCheckCustom dst src nameIndex`

`let` lowers to expression code that writes into a temporary or final slot,
followed by an optional direct cast and `Move` into the declared local slot.
Parameter type hints lower to `BindParameter` plus an optional direct cast in
the entry prologue.

### Scopes

- `EnterScope data=scopePlan`
- `ExitScope data=scopePlan`

Scopes are explicit instructions. They restore locals through the VM scope stack
instead of relying on C# `try/finally`.

### Arithmetic and Logic

Binary operations read source slots and write `Dest_U16`:

```text
Add dst=s3 left=s1 right=s2
Equal dst=s4 left=s3 right=s0
```

Required operations:

- `Add`, `Subtract`, `Multiply`, `Divide`
- `Power`, `IntegerDivide`, `Modulo`, `Remainder`
- primitive integer variants for comparison and arithmetic
- `Equal`, `NotEqual`, `ApproxEqual`
- `Less`, `Greater`, `LessOrEqual`, `GreaterOrEqual`
- `And`, `Or`, `Xor`, `Implies`
- `Default`
- `Contains`, `ContainsValue`
- `StartsWith`, `EndsWith`
- `Intersect`, `Combine`, `Except`, `Zip`
- direct unary opcodes such as `UnaryNegate`, `UnaryNot`, `UnaryLength`,
  `UnaryAbs`, and `UnaryNaturalLog`
- `Variadic`
- `Clamp`

Logical operations use three-valued truth tables with `nothing` as unknown.
Runtime truth tests and false tests both fail for `nothing`.

Short-circuiting source constructs must compile to jumps when preserving lazy
behavior matters:

```text
; dst = a and b
@0100 JumpIfFalse cond=sA target=@0105
@0101 ... evaluate b into sB ...
@0102 And dst=sDst left=sA right=sB
@0103 Jump @0106
@0105 LoadFalse dst=sDst
@0106 ...
```

The exact lowering may differ, but the right-hand expression of `and`, `or`, and
`->` must be skipped when the language's short-circuit rule decides the result.
Implication is right-associative at source level and uses the truth table for
`not a or b`: false antecedent yields `true`; unknown participates as
`nothing` unless the consequent resolves the result to `true`.

The VM executes `and` and `or` laziness from the linear branch sequence.
`ShortCircuitAnd` and `ShortCircuitOr` are lowering metadata and are not needed
for runtime execution. `ShortCircuitImplies` can be emitted as the binary
implication combine inside that branch sequence.

### Control Flow

- `Jump target`
- `JumpIfTrue cond target`
- `JumpIfFalse cond target`
- `JumpIfNotTrue cond target`
- `RangeIterator dst from to`
- `RangeIteratorWithStep dst from to step`
- `RangeIteratorShort dst fromI16 toI16 stepI16`
- `CollectionIterator dst collection`
- `IteratorNext dst iterator noMoreTarget`
- `IteratorClose iterator`
- `Call dst entryAddress argumentSlotList`
- `CallPredicate dst entryAddress argumentSlotList`
- `Return src`
- `ReturnNothing`

`if` and guarded choices compile to condition code plus jumps. `else` runs when
the condition is not true, so normal `if` lowering should use `JumpIfNotTrue`,
not an implicit `as :boolean` conversion.

Example:

```text
@0100 Move dst=s10 src=s0
@0101 LoadInteger dst=s11 value=0
@0102 Greater dst=s12 left=s10 right=s11
@0103 JumpIfNotTrue cond=s12 target=@0108
@0104 LoadInteger dst=s1 value=1
@0105 Jump @0110
@0108 LoadInteger dst=s1 value=2
@0110 ...
```

Guarded expressions lower to explicit condition jumps and value writes to a
shared destination slot. Conditions are evaluated in order via `JumpIfNotTrue`;
only the first true branch value is evaluated, and otherwise code runs when no
condition is true. The public bytecode has no `GuardedChoice` layout.

`for` statements lower to normal linear iterator control flow. The source is
evaluated once, an iterator is stored in a temporary slot, `IteratorNext` writes
each item into an item slot and jumps to the close block when exhausted, and the
body runs inside an iteration scope. Literal I16 ranges should use
`RangeIteratorShort`; dynamic ranges and collection sources use the slot-based
iterator opcodes. Generated collections use the same iterator opcodes plus
VM-internal collection builder opcodes.

### Calls and Returns

Predicates and functions are normal callable entries. A call instruction
transfers control to the callable entry and returns to the next instruction.
Predicate calls use the same frame mechanism, normalize the result to
`boolean | nothing`, and read their arguments from the `B_U16` slot-list. The
`x is predicate` syntax is unary sugar that lowers to `CallPredicate` with a
single-entry argument slot-list.

```text
@0500 Call dst=s3 callable=wounded args=#0
@0501 JumpIfNotTrue cond=s3 target=@0510
@0520 CallPredicate dst=s4 predicate=@0900 args=#1
```

Type constructors, variadic operators, collection builders, dictionaries,
message literals, handler binding, local calls, and predicate calls now
reference entry addresses, `StringPool`, or `UShortListPool` directly from the
instruction word.

Call frame state:

```text
ReturnAddress
CallableIndex
CallerFrameBase
ReturnDestinationSlot
ScopeMark
RandomMark
```

Predicate bodies must compile as `:boolean` or `:nothing`; an explicit
`as :boolean` marks intentional boolean coercion. Predicate calls preserve
`nothing` so missing information remains "no statement" instead of becoming
`false`. Functions preserve the expression result.

### Loops

Loops should be compiled to explicit loop control instructions and jumps.

Range loop shape:

```text
@0200 RangeLoopInit data=rangeLoopPlan
@0201 RangeLoopMoveNextOrJump dst=sItem data=rangeLoopPlan target=@0210
@0202 EnterScope data=loopScope
@0203 ...
@0208 ExitScope data=loopScope
@0209 Jump @0201
@0210 RangeLoopEnd data=rangeLoopPlan
```

Collection loop shape:

```text
@0300 CollectionLoopInit source=sValues data=collectionLoopPlan
@0301 CollectionLoopMoveNextOrJump dst=sItem data=collectionLoopPlan target=@0310
@0302 ...
@0309 Jump @0301
@0310 CollectionLoopEnd data=collectionLoopPlan
```

Loop runtime state belongs to the VM session, indexed by the loop plan or by a
compiler-assigned loop temporary slot.

### Values and Containers

- `BuildList dst itemSlotListIndex`
- `BuildSequence dst itemSlotListIndex`
- `BuildSet dst itemSlotListIndex`
- `BuildDictionary dst keyNameListIndex valueSlotListIndex`
- `BuildMessage dst messageShapeIndex argumentSlotListIndex`
- `BindHandler dst operandSlotListIndex argumentNameListIndex`
- `MemberAccess dst targetSlot nameIndex`
- `IndexedAccess dst targetSlot selectorSlot`
- `Range dst fromSlot toSlot`
- `RangeWithStep dst fromSlot toSlot stepSlot`
- `Dice dst count sides`
- `Random dst fromSlot toSlot`
- `RandomPush seedSlot`
- `RandomPushConstant seedU64`
- `RandomPop`
- `TypeConstructor dst typeNameIndex argumentNameListIndex argumentSlotListIndex`

These remain high-level because they map directly to public value semantics.
List indexes reference `UShortListPool`; name lists and message shapes contain
`StringPool` indexes, while slot lists contain frame slot indexes.
Seeded random no longer has a side table or helper expression opcode. The
lowerer emits `RandomPush*`, the inline body instructions, and `RandomPop`.
Constant seeds are unitless `U64`; signed integer literals are mapped by their
two's-complement bit pattern. Dynamic seeds must be statically visible as
unitless `:integer`, usually by declaring the value as `:integer` or writing an
explicit `as :integer` cast.

Required portable value families:

- primitives: `:nothing`, `:tag`, `:text`, `:boolean`, `:uuid`
- numeric: `:integer`, `:float`, `:percentage`
- numeric units: `:degree`, `:meter`, `:second`
- vectors: `:vector`
- points: `:point`
- containers: `:optional`, `:sequence`, `:series`, `:range`, `:list`,
  `:dictionary`, `:set`, `:dice`
- runtime values: `:message`, `:handler`, `:envelope`, `:ref`
- custom record and external types

UUIDs are RFC-compatible 128-bit binary values. They should compare by high/low
bits and should be valid dictionary/ref ids. Invalid non-comparison operations
evaluate to `nothing`.

Refs are immutable handles made from a target type name and id value. The target
type must be a record type or an external type. The id should be a portable
scalar, usually `:text` or `:uuid`.

Series values are index-addressed, repeatable mathematical series. Supported
operations are `:term`, `:take`, and `:drop`; unsupported lookup/selector
operations evaluate to `nothing`.

Envelope values are dictionary-backed system values. The bytecode model should
treat `:envelope` as an open typed dictionary so fields can be added later
without changing instruction shape. The currently guaranteed fields are
`message: :message` and `tags: :list` of tag values.

### Emit, Publish, and Tags

The language distinguishes local/current-space emission from outward/shared-space
publishing:

```eventscript
emit LocalEvent(value) with :internal
publish BusEvent(value) with :radio, dynamicTags

on BusEvent(value) matching :radio without :blocked {
}
```

`Emit` targets the current event space. `Publish` targets the configured publish
hook; when no hook exists, it falls back to `Emit`. This keeps the language
host-independent while allowing the runtime to redirect published messages to a
bus, broadcaster, or parent dispatcher later.

`with` attaches envelope tags to the message. Tags are not part of
`SignatureId`, not part of handler parameter binding, and are normalized to a
set while preserving first-seen order. A tag expression may evaluate to a single
tag or to a list/set/sequence of tags.

Handlers may declare static tag filters:

- `matching :a, :b`: all required tags must be present.
- `without :x, :y`: none of the excluded tags may be present.
- no filter: any tag set matches as long as the message signature matches, or
  the message name matches for `MessageEnvelope` handlers.

Publishing and emitting are distinct opcodes. Static message literals use a
message shape and argument slot lists from `UShortListPool`; dynamic message
values use the message slot. Tagged forms use distinct `*WithTags` opcodes with
a concrete tag slot-list index. A message shape is encoded as
`[messageNameStringIndex, argumentNameStringIndex...]`. Zero-argument messages
use a concrete empty argument-list entry in `UShortListPool`.

Arguments and tags are evaluated by preceding code into slots:

```text
@0400 Move dst=s20 src=sDamage
@0401 Move dst=s21 src=sTarget
@0402 LoadTag dst=s22 tag=:radio
@0403 PublishMessageWithTags shape=#0 args=#1 tags=#2
```

Publishing or emitting a first-class message value uses `PublishMessageValue`
or `EmitMessageValue`. Tagged dynamic values use
`PublishMessageValueWithTags messageSlot tagSlotList` or
`EmitMessageValueWithTags messageSlot tagSlotList`.

### Extensions, Intrinsics, and External Types

- `CallStandard dst extensionShapeIndex argumentSlotListIndex`
- `CallStandardPredicate dst extensionShapeIndex argumentSlotListIndex`
- `CallExternal dst externalReferenceIndex argumentSlotListIndex`
- `CallExternalPredicate dst externalReferenceIndex argumentSlotListIndex`

External references are collected at compile time and dynamically bound by the
host when loading the compiled artifact. Runtime extension binding is not
serialized into portable bytecode.

Standard intrinsics are non-overridable and do not appear in
`ExternalReferences`. They are represented by `CallStandard*` with an extension
shape stored in `UShortListPool` as
`[extensionNameStringIndex, functionNameStringIndex, argumentNameStringIndex...]`.
All extension call arguments, including zero-argument calls, are represented by
a concrete argument slot-list entry in `UShortListPool`.

External type constructors are collected separately in
`ExternalTypeConstructorReferences` and dynamically bound against the host's
external type registry. Record constructors and external constructors share the
same source syntax but must remain distinguishable in portable metadata.

### Collection DSL

Collection operations should stay high-level enough to avoid exploding code size
and losing optimized paths.

Recommended shape:

```text
Pipeline dst pipelineIndex
CollectionBuilderList builder
CollectionBuilderSet builder
CollectionBuilderAdd builder item
CollectionBuilderFinish dst builder
```

`PipelinePool` is portable metadata, not nested code. It references pipeline
selector entries and expression entry addresses through `PipelineSelectorPool`.
Pipeline-owned pattern pools are named together so they are easy to identify in
the binary surface.

```text
Pipeline
  SourceSlot
  PrefixSelectorIndexes[]
  TerminalSelectorIndex

PipelineSelector
  Kind
  IdentifierSlot
  PredicateAddress?
  PredicateResultSlot?
  ProjectionAddress?
  ProjectionResultSlot?
  SecondaryIdentifierSlot?
  SecondaryProjectionAddress?
  SecondaryProjectionResultSlot?
  Count
  Mode
  SecondaryMode
  Flag
  PipelinePatternIndex?
  ObjectPatternIndex?

PipelinePattern
  Kind: FullHouse | Straight | Count
  Count?
  FaceAddress?

PipelineObjectPattern
  Entries[]

PipelineObjectPatternEntry
  Key
  ValueKind: Expression | Nested
  ExpressionAddress?
  NestedPatternIndex?
```

Required selector kinds:

- prefix selectors: `Filter`, `Select`
- predicate terminals: `Predicate` for `:any`, `:all`, and related modes
- numeric terminals: `Sum`, `Average`, `Count`, `Min`, `Max`
- edge terminals: `Edge` for `first`, `last`, `single`, `highest`, `lowest`
- dictionary terminal: `Dictionary`
- membership terminal: `Contains`
- ordering/grouping terminals: `Sort`, `Distinct`, `GroupBy`, `OrderBy`,
  `Reverse`
- slicing terminals: `SequenceSlice`
- series terminal: `SeriesTerm`
- dice/object pattern terminals: `Pattern`, `ObjectMatch`, `TakePattern`
- random terminals: `Choose`, `Draw`, `Shuffle`

Streaming/materialization contract:

- `:range`, `:sequence`, and `:series` sources must not be blindly materialized
  before streamable terminal selectors.
- Streamable/short-circuit terminal selectors include `:any`, `:all`, `:first`,
  and direct `:contains` without prefix selectors.
- Prefix selectors are applied lazily on the streaming path.
- Selectors that require full collection semantics may materialize after runtime
  budgets such as `MaxRangeItems` are checked.
- Non-range list-like sources should keep an indexed hot path for selectors such
  as `:sum`, `:average`, `:count`, and edge selectors.

The VM may execute a pipeline as one high-level operation while retaining
operation-local state for stepping. The active `pc` remains at the `Pipeline`
instruction; debug state can identify selector/item progress when needed.

### Generated Collections

Generated collection expressions lower to normal linear iterator control flow:

```text
CollectionBuilderList/Set builder
EnterScope
RangeIterator* / CollectionIterator iterator
loop:
  IteratorNext item iterator noMore
  EnterScope
  MoveSlot identifier item
  optional predicate + JumpIfNotTrue skipProjection
  projection expression
  CollectionBuilderAdd builder projected
  ExitScope
  Jump loop
noMore:
IteratorClose iterator
ExitScope
CollectionBuilderFinish dst builder
```

Direct ranges after `in` remain invalid at source level. Range iteration should
use explicit range-source syntax.

At runtime, collection builders are VM-internal values and are not visible as DSL
values. `CollectionBuilderAdd` applies `MaxGeneratedCollectionItems` while
materializing the result.

### Patterns

Pipeline pattern metadata lives in `PipelinePatternPool` and
`PipelineObjectPatternPool`. Pattern helper expressions are entry addresses in
the global code segment and write their result into normal frame slots.

### Diagnostics

Diagnostics are execution instrumentation, not bytecode. The bytecode stream
does not contain diagnostic-only instructions. Diagnostic-only metadata is
carried by the optional `DebugSegment`, linked to instruction addresses and
slots. Runtime collectors read the executable diagnostic sites derived from that
segment; production bytecode side tables must not carry diagnostic-only fields.
If debug info is disabled, the debug segment may be empty. Compile diagnostics
request debug info so trace collectors can resolve instruction addresses and
slots.

```text
DebugSegment
  DiagnosticSites[]

DebugDiagnosticSite
  Kind: LetEvaluated | ExpressionEvaluatedToNothing
  Timing: BeforeInstruction | AfterInstruction
  Address
  Slot
  Name
```

The VM records diagnostic events while executing normal instructions:

- Handler and parameter events are derived from handler metadata and
  `BindParameter`/cast execution.
- Function and predicate call events are derived from callable metadata and
  direct call entry addresses.
- Let and expression-to-nothing events are derived from debug diagnostic sites.
- Publish argument events are derived from message shape and slot-list metadata.

## Debug Segment

Debug metadata is optional and should be generated when compile diagnostics are
enabled, and may also be generated for deterministic dumps or debugger UIs.

```text
DebugSegment
  DiagnosticSites[]
  Labels[]
  AddressToSource[]
  AddressToLogicalNode[]
```

Label examples:

```text
L_handler_Start = @0000
L_if_7_else = @0108
L_if_7_end = @0110
L_for_8_head = @0201
L_for_8_end = @0210
L_predicate_wounded = @0500
```

The VM never needs labels to execute. They exist for dumps, debugger UI, traces,
and human-readable bytecode reviews.

## Dump Format

The primary bytecode dump should show a single global address space and explicit
slot operands:

```text
code[26]
@0000 L_handler_Start:
@0000 EnterScope data=#0
@0001 BindParameter dst=s0 arg=values
@0002 Pipeline dst=s2 source=s0 data=#0
@0003 LoadInteger dst=s3 value=0
@0004 Greater dst=s4 left=s2 right=s3
@0005 JumpIfNotTrue cond=s4 target=@0010 ; L_if_0_else
@0006 ...
@0010 L_if_0_else:
@0010 ...
@0025 Return src=sReturn
```

With typed parameters:

```text
handler DamageTaken(unit, amount)
  params:
    unit -> s0 as :unit
    amount -> s1 as :integer

@0000 L_handler_DamageTaken:
@0000 BindParameter dst=s0 arg=unit
@0001 CastCustom dst=s0 src=s0 type=:unit
@0002 BindParameter dst=s1 arg=amount
@0003 CastInteger dst=s1 src=s1
@0004 ...
```

A grouped view may still be offered, but it must keep global addresses visible.

## Format Invariants

- New bytecode format changes must increment `FormatVersion`.
- Bytecode dumps are diagnostic only and are not a stable wire format.
- Public bytecode must remain deterministic for the same script/options.
- Host dynamic linking remains separate from compilation.
- Runtime extension binding is not serialized into portable bytecode.
- Runtime execution must be resumable without relying on the C# call stack.
- The portable model must preserve source-level short-circuit and tri-state
  truth semantics.
