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
temporary slots from handler locals with temporary frame extensions. Predicate calls
use direct `CallPredicate` instructions with target entry addresses and
contiguous staged argument sequences. Local calls use direct `Call`
instructions with target entry addresses and contiguous staged argument
sequences. Extension calls use direct
`CallStandard*` or `CallExternal*` instructions with argument slot lists.
Extrema reduce operators, type constructors, local builders, message literals,
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
  UInt16SliceTable        compact ushort lists used by code and metadata
  BindTable
    Kind                  0x10-0x1F export, 0x20-0x2F import
    MessageHandler | Function | Predicate | ExtensionCall | OutboundMessage | ExternalType
    Name                  string-pool index
    ArgumentNames         ordered string-pool indexes
    EntryAddress          global code address for exports, 0 for imports
```

`OutboundMessage` bind entries list statically shaped `emit`/`publish` messages.
Their name and argument-name fields are the outbound message signature, allowing
loaders to construct outbound message-signature lookups without scanning the
instruction table.

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
  OutboundMessageSignatures
  ExternalReferences
  ExternalTypeConstructorReferences
  Code: Instruction[]
  Handlers: HandlerEntry[]
  Callables: CallableEntry[]
  TypeDefinitions: TypeDefinitionEntry[]
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
  OutboundMessageSignatures
  ExternalReferences
  ExternalTypeConstructorReferences
  Code: IReadOnlyList<GameEventScriptBytecodeInstruction>
  Handlers
  Callables
  TypeDefinitions
  MaxFrameSlots
  DebugSegment

GameEventScriptBytecodeInstruction
  OpCode
  UnitAndFlags
  DestinationSlot
  XSlot/YSlot
  ConditionSlot/TargetAddress/EntryAddress
  StringIndex/ListIndex/SecondaryListIndex/ExternalReferenceIndex/TypeOperand
  ImmediateX/ImmediateY/Count
  AU, BU, CU, DU
  AS, BS, CS, DS
  I64/Payload/F64
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
  are represented by their IEEE bit patterns.
- `LoadPercentage` stores a percentage ratio in `F64` and must not carry unit
  flags. Percentage is a dedicated value kind, not a bytecode unit.
- `LoadText` and `LoadTag` store a `StringPool` index in the primary `X`
  operand.
- `LoadHandler` stores a `UShortListPool` message-shape index in `A`. The shape
  list is `[messageNameStringIndex, argumentNameStringIndex...]`.

Constants are concrete. For example `1`, `1.0`, `1m`, and `1s` remain distinct
loads. Runtime operations and explicit `:number` casts may normalize finite
integral results to integer values. Larger constants such as future vector/point
literals should use a normalized data segment instead of reintroducing an object
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

The compiler must only emit units supported by the current runtime. The extended
map is a binary-format target so future unit additions do not need to reshape
the instruction word.

## Parameter Type Hints

Handlers, predicates, and functions may declare optional parameter type hints
using the same `as :type` language as value coercion:

```eventscript
on DamageTaken(unit as :unit, amount as :number) {
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
HandlerParameterEntry
  ExternalLabel
  LocalName
  Slot
  TypeName?
```

The executable code should contain explicit coercion instructions so the program
counter and dump show where normalization happens:

```text
@0000 SlotLocals locals+=localCount
@0001 Cast dst=s0 src=s0 kind=Custom type=:unit
@0002 Cast dst=s1 src=s1 kind=Integer
```

Handler arguments are preloaded into slots `0..n-1` before the entry starts.
Untyped handler parameters therefore emit no binding instruction. Callable
parameters use the same slot/type metadata externally; the call ABI stages
arguments before `Call`, and the callee starts with those values already
assigned to slots `0..n-1`.

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
instruction word is currently a 16-byte explicit-layout value: an 8-byte primary
word plus an aligned 8-byte payload word.

```text
Instruction
  OpCode
  UnitAndFlags
  DestinationSlot
  XSlot, YSlot
  ConditionSlot, TargetAddress, EntryAddress
  StringIndex, ListIndex, SecondaryListIndex, ExternalReferenceIndex, TypeOperand
  Count
  ImmediateX, ImmediateY
  AU, BU, CU, DU
  AS, BS, CS, DS
  I64, Payload, F64
```

Operands are interpreted by opcode:

- `DestinationSlot`: destination slot for value-producing instructions.
- `XSlot`, `YSlot`: primary-word slot operands.
- `ConditionSlot`, `TargetAddress`, `EntryAddress`: primary-word control-flow aliases.
- `StringIndex`, `ListIndex`, `SecondaryListIndex`, `ExternalReferenceIndex`, `TypeOperand`:
  primary-word pool/table or type operands.
- `ImmediateX`, `ImmediateY`: compact signed immediates in the primary word.
- `Count`: signed local slot delta alias over `ImmediateX`.
- `AU`, `BU`, `CU`, `DU`, plus signed `AS`, `BS`, `CS`, `DS` views:
  payload-word 16-bit views for wider opcodes.
- `I64`, `Payload`, `F64`: aligned payload-word literal views for signed
  integer payloads, raw unsigned payload bits, and double/float loads.
- Branch opcodes use the primary fields documented by their opcode shape.
- Iterator and pipeline terminal opcodes document their own slot, immediate, and
  helper-entry fields explicitly. They do not use sentinel operands for absent
  parameters.
- Pool-backed opcodes use the documented `StringPool` or `UShortListPool`
  indices directly through aliases such as `StringIndex`, `ListIndex`,
  `SecondaryListIndex`, `AU`, or `BU`.
- `SlotLocals Count` is the required prolog instruction for every executable
  entry address. Entry prologs use a non-negative signed count and add local
  slots beyond the arguments already present in the frame. Negative counts are
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
future decoders. The current groups are:

```text
0x00 Group 1: no-op, frame slots, jumps, calls, returns, emit operations
0x10 Group 1 continuation: emit/publish operations, casts, checks, move, access, handler binding
0x20 Group 1 numeric helper casts/checks and reserved tail
0x30 Group 2: loads, argument staging, type construction
0x40 Group 2 continuation and reserved tail
0x50 Group 3: boolean, comparison, implication, presence checks
0x60 Group 3 comparison reserved space
0x70 Group 4: math and random
0x80 Group 4 continuation and reserved tail
0x90 Group 5: text/collection operators, range/iterator setup
0xA0 Group 5 stream next/close/reduce/fold, series, PipelineIterator
0xB0 Group 6: collection and value building
0xC0 Group 7: pipeline materializers, transforms, membership terminals
0xD0 Group 7 pipeline ordering, slicing, random terminals
0xE0 Group 7 pipeline dice/pattern terminals and reserved tail
0xF0 reserved for future pipeline, extension, or VM opcodes
```

The exhaustive opcode field map lives in `BytecodeOpcodeShape.md`. That table
lists every currently defined opcode as its own row. Unused address ranges are
marked as `reserved` ranges.

For JSON transport, instructions serialize as a normalized primary-word plus
payload object:

```json
{ "Opcode": "LoadInteger", "Flags": "0x00", "Dst": "0x0007", "X": "0x0000", "Y": "0x0000", "Parameter": "0x000000000000002A" }
```

`Flags` is the raw `UnitAndFlags` byte, `Dst` is the raw destination slot,
`X` and `Y` are the raw primary operands, and `Parameter` is the raw unsigned
64-bit payload word covering bytes `8..15`. For literal instructions the
payload carries `I64`, raw `Payload`, or the IEEE-754 `F64` bit pattern.

Large structured metadata belongs in tables and pools, not nested instruction
objects. Examples: `UShortListPool` message shapes/slot lists, `StringPool`
names, bind tables, and optional debug/diagnostic layouts.

An instruction that produces a `nothing` value writes it to `DestinationSlot`. Returning
without a value uses `ReturnVoid`; returning a slot value uses
`ReturnValue XSlot`. There is no implicit push. There are no operand-stack `Pop` or
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
record onto `callStack`; `ReturnValue` restores the next `pc` and writes the returned
value to the caller's destination slot.

Manual stepping pauses after the instruction budget is consumed. The current
`pc` points the dump/debugger at the active instruction. If execution is inside
a high-level operation such as a pipeline, the VM may also expose
operation-local debug state such as selector index or item index.

## Entry Tables

Handlers and callables are metadata over the shared code segment. Their
`EntryAddress` points at a `SlotLocals` prolog instruction. The instruction
immediately after the prolog is the first executable body instruction. Handler
and callable arguments are already present in slots `0..n-1` when their frame
starts.

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
  DeclarationOrder

CallableEntry
  Name
  Kind: Predicate | Function
  Parameters: ParameterEntry[]
  SignatureLabels
  SignatureId
  EntryAddress
  ReturnSlot
```

The local slot count is intentionally not part of the bind/export metadata. It
is encoded as non-negative `SlotLocals Count` at the entry address because it is
a VM execution detail needed equally by exported handlers/callables and private
helper entries.

`ExactSignature` handlers subscribe by `SignatureId`. `MessageEnvelope` handlers
subscribe by `MessageName` and tag filters only; the runtime invokes them with a
single `:envelope` argument whose `message` field is the original message.
Message-envelope handlers count as normal delivery and therefore prevent
`undeliverable` fallback when their tag filters match. For `MessageEnvelope`
handlers, `SignatureId` still records the synthetic envelope parameter signature
such as `Damage(envelope)`, but it is metadata and not the dispatch key.

System endpoint entries use reserved lowercase names outside normal message
casing. The currently defined endpoints are:

```text
initialization
undeliverable as envelope
```

`initialization` is encoded as an `ExactSignature` handler entry without
arguments. It is not normal external dispatch input; the host queues all loaded
initialization handlers directly when a `GameEventScriptSession` starts.

`undeliverable` is encoded as a `MessageEnvelope` handler entry with message name
`undeliverable`. It receives a map-backed `:envelope` value when no
`ExactSignature` or `MessageEnvelope` handler could be queued for the original
message after signature/name and tag filters were applied. System endpoints
still use normal handler metadata, priority, and declaration order.
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
`:number` lowers to `CastNumeric`; numeric casts keep integral values as
integers and use floats only when the value does not fit the integer
representation. `is numeric`, `is integer`, and `is fractional` are source-level
check constructs that lower to `CheckNumeric`, `CheckInteger`, and
`CheckFractional`. `numeric`, `integer`, and `fractional` are not declared type
kinds or `:` tags.

`let` lowers to expression code that writes into a temporary or final slot,
followed by an optional direct cast and `Move` into the declared local slot.
Handler and callable parameter type hints lower to optional direct casts over
the preloaded argument slots.

### Scopes

- `SlotLocals Count`

Scopes are explicit signed local slot deltas. `SlotLocals Count` extends the
same frame by `Count` slots when `Count > 0`, and clears/releases `-Count`
slots when `Count < 0`. Slot addresses stay absolute in the current frame, so
reserving two locals from active slots `s0..s3` exposes `s0..s5`. Existing
parent slots remain visible and are not rolled back by scope exit. The compiler
must emit matching deltas for normal exits; `ReturnValue` and `ReturnVoid`
discard the whole active frame, so no negative `SlotLocals` is needed
immediately before a return.

### Arithmetic and Logic

Binary operations read source slots and write `DestinationSlot`:

```text
Add dst=s3 left=s1 right=s2
Equal dst=s4 left=s3 right=s0
```

Required operations:

- `Add`, `Subtract`, `Multiply`, `Divide`
- `Power`, `IntegerDivide`, `Modulo`, `Remainder`
- `Equal`, `NotEqual`, `ApproxEqual`
- `Less`, `Greater`, `LessOrEqual`, `GreaterOrEqual`
- `And`, `Or`, `Xor`, `Implies`
- `Default`
- `Contains`, `ContainsValue`
- `StartsWith`, `EndsWith`
- `Intersect`, `Combine`, `Except`, `Zip`
- `Min`, `Max`
- direct unary opcodes such as `UnaryNegate`, `UnaryNot`, `UnaryLength`,
  `UnaryAbs`, and `UnaryNaturalLog`
- `Clamp`

Numeric operations must preserve the language distinction between absent input
and invalid mathematics. For arithmetic, numeric unary operations, `Clamp`, and
numeric random-range evaluation, any source operand that is `nothing` produces
`nothing`. If all source operands are present but the operation cannot be
computed as valid mathematics, the result is a numeric `NaN`.

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

Numeric opcodes preserve value families where the source language does:
`UnaryAbs` keeps percentages as `Percentage`, keeps quantity units on numeric
quantities, and finite exactly integral numeric results are represented as
integer values when they fit signed 64-bit.

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

The VM executes `and` and `or` laziness from the linear branch sequence; there
are no dedicated `and`/`or` short-circuit opcodes in portable bytecode.
`Implies` can be emitted as the binary implication combine inside that branch
sequence.

### Control Flow

- `Jump target`
- `JumpIfTrue cond target`
- `JumpIfFalse cond target`
- `JumpIfNotTrue cond target`
- `RangeIterator dst from to`
- `RangeIteratorWithStep dst from to step`
- `RangeIteratorShort dst fromI16 toI16 stepI16`
- `CollectionIterator dst collection`
- `StreamNext dst iterator noMoreTarget`
- `StreamClose iterator`
- `Call dst entryAddress`
- `CallPredicate dst entryAddress`
- `ReturnValue src`
- `ReturnVoid`

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
evaluated once, an iterator is stored in a temporary slot, `StreamNext` writes
each item into an item slot and jumps to the close block when exhausted, and the
body runs inside an iteration scope. Literal I16 ranges should use
`RangeIteratorShort`; dynamic ranges and collection sources use the slot-based
iterator opcodes. Generated collections use the same iterator opcodes plus
VM-internal collection builder opcodes.

### Calls and Returns

Predicates and functions are normal callable entries. A call instruction
transfers control to the callable entry and returns to the next instruction.
Predicate calls use the same frame mechanism, normalize the result to
`boolean | nothing`, and read their arguments from the stage sequence immediately
before the call. The call instruction does not store an argument count; the VM
uses the contiguous `Stage*` sequence immediately before the call and validates
it against callable metadata. A stage sequence may contain only `Stage*`
instructions and must be followed by `Call` or `CallPredicate`. The callee
frame receives arguments in slots `0..n-1`.
The `x is predicate` syntax is unary sugar that lowers to one staged argument
plus `CallPredicate`.

```text
@0500 StageRegister src=s0
@0501 Call dst=s3 callable=wounded stagedArgs=1
@0502 JumpIfNotTrue cond=s3 target=@0510
@0520 StageRegister src=s2
@0521 CallPredicate dst=s4 predicate=@0900 stagedArgs=1
```

Type constructors, variadic operators, collection builders, maps,
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
@0200 SlotLocals locals+=loopLocalCount
@0201 RangeIteratorShort dst=sIterator from=1 to=20 step=1
@0202 StreamNext dst=sItem iterator=sIterator noMore=@0210
@0203 SlotLocals locals+=iterationLocalCount
@0204 MoveSlot dst=sIdentifier src=sItem
@0205 ...
@0208 SlotLocals locals-=iterationLocalCount
@0209 Jump @0202
@0210 StreamClose iterator=sIterator
@0211 SlotLocals locals-=loopLocalCount
```

Collection loop shape:

```text
@0300 SlotLocals locals+=loopLocalCount
@0301 CollectionIterator dst=sIterator source=sValues
@0302 StreamNext dst=sItem iterator=sIterator noMore=@0310
@0303 SlotLocals locals+=iterationLocalCount
@0304 ...
@0308 SlotLocals locals-=iterationLocalCount
@0309 Jump @0302
@0310 StreamClose iterator=sIterator
@0311 SlotLocals locals-=loopLocalCount
```

Loop runtime state is stored in the VM-internal iterator value held by the
compiler-assigned iterator slot.

### Values and Containers

- `BuildList dst itemSlotListIndex`
- `BuildMap dst keyNameListIndex valueSlotListIndex`
- `LoadMessage dst messageShapeIndex argumentSlotListIndex`
- `BindHandler dst handlerSlot argumentSlotListIndex`
- `MemberAccess dst nameIndex objectSlot`
- `IndexedAccess dst selectorSlot objectSlot`
- `Range dst fromSlot toSlot`
- `RangeWithStep dst fromSlot toSlot stepSlot`
- `Dice dst count sides`
- `Random dst fromSlot toSlot`
- `RandomPush seedSlot`
- `RandomPushConstant seedI64`
- `RandomPop`
- `CreateVector dst immediateX`
- `CreatePoint dst immediateX`
- `TypeConstructor dst typeNameIndex argumentNameListIndex argumentSlotListIndex`

These remain high-level because they map directly to public value semantics.
List indexes reference `UShortListPool`; name lists and message shapes contain
`StringPool` indexes, while slot lists contain frame slot indexes.
Vector and point constructors are fixed built-ins. Their source arguments are
lowered into staged component values in canonical `x, y, z` order; `immediateX`
stores the first staged component index (`0`, `1`, or `2`) so leading missing
components do not need explicit zero stages.
Seeded random no longer has a side table or helper expression opcode. The
lowerer emits `RandomPush*`, the inline body instructions, and `RandomPop`.
Constant seeds are unitless signed `Int64`, so negative seeds such as `-145`
are valid source literals. Dynamic seeds must be statically visible as a
unitless integer number, usually by declaring the value as `:number` or writing
an explicit `as :number` cast.

Required portable value families:

- primitives: `:nothing`, `:tag`, `:text`, `:boolean`
- numeric: `:number`, `:percentage`
- numeric quantities: `:quantity(degree)`/`:quantity(°)`, `:quantity(m)`, `:quantity(s)`
- vectors: `:vector`
- points: `:point`
- containers: `:series`, `:range`, `:list`, `:map`, `:dice`
- runtime values: `:message`, `:handler`, `:envelope`
- custom record and external types

Series values are index-addressed, repeatable mathematical series. Supported
operations are `:term`, `:take`, and `:drop`; unsupported lookup/selector
operations evaluate to `nothing`.

Envelope values are map-backed system values. The bytecode model should
treat `:envelope` as an open typed map so fields can be added later
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
unique ordered tag list while preserving first-seen order. A tag expression may
evaluate to a single tag or to a list of tags.

Handlers may declare static tag filters:

- `matching :a, :b`: all required tags must be present.
- `without :x, :y`: none of the excluded tags may be present.
- no filter: any tag list matches as long as the message signature matches, or
  the message name matches for `MessageEnvelope` handlers.

Publishing and emitting are distinct opcodes. Static message literals use a
message shape and argument slot lists from `UShortListPool`; direct
`EmitMessage*`/`PublishMessage*` opcodes store the shape in `MessageDestination`,
the argument list in `ListIndex`, and tagged forms store the tag slot-list in
`SecondaryListIndex`. Dynamic message values use the message slot in `XSlot`;
tagged dynamic forms store their concrete tag slot-list index in `ListIndex`.
The message shape is encoded as
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
and losing optimized paths, but pipeline selectors no longer live in public
metadata pools. Prefix selectors and selector helper expressions are lowered to
normal entry addresses in the global code segment.

Core streaming shape:

```text
CollectionIterator source -> iterator
PipelineIterator transformedIterator sourceIterator nextEntry itemBindingSlot captureSlotList
PipelineCollectList dst iterator
PipelineFirst/Last/Single dst iterator
PipelineHasAny/HasAll dst iterator
StreamReduce dst iterator itemBindingSlot reducerEntry
StreamReduceOrDefault dst iterator defaultSlot itemBindingSlot reducerEntry
StreamFold dst iterator seedSlot itemBindingSlot reducerEntry
CollectionBuilderList builder
CollectionBuilderAdd builder item
CollectionBuilderFinish dst builder
```

`PipelineIterator` is a lazy one-time adapter over another VM iterator. Its
helper entry runs as an isolated helper frame: the current source item is bound
to helper-local slot `AU` (normally slot `0`), and `BU` references a
`UShortListPool` entry containing caller-frame capture slots copied when the
iterator is created. Captures are exposed to the helper in order starting at
slot `1`. `ReturnValue` yields the returned value. `ReturnVoid` means "no
yielded item"; the adapter continues with the next source item until the source
iterator itself is exhausted. For normal call frames, `ReturnVoid` is mapped to
DSL `nothing`; for pipeline iterators it is control flow.

Reducer entries use `DestinationSlot` as the current accumulator slot and the
instruction item binding slot as the current element. A reducer `ReturnValue`
replaces the accumulator. A reducer `ReturnVoid` replaces the accumulator with
DSL `nothing`.

Generic reduction contracts:

- `StreamReduce`: empty iterator -> `nothing`; one item -> that item; two or
  more items -> first item is accumulator, reducer starts with the second item.
- `StreamReduceOrDefault`: empty iterator -> default slot; one item -> that
  item; two or more items -> first item is accumulator, reducer starts with the
  second item. `:sum` uses this form so empty sum returns `0`, while non-empty
  sums preserve the first projected value's type/unit.
- `StreamFold`: empty iterator -> seed slot; otherwise seed is accumulator
  and the reducer runs for every item. `:count` and `:average` use this form.

Fixed terminal opcodes cover materializers and operations that need full
collection semantics: map, distinct, group/order/sort/reverse, random
choose/draw/shuffle, dice patterns, object matches, and series term/take/drop
operations. These opcodes reference only iterator slots, helper
entry addresses, immediate counts, and binding slots; there are no pipeline
selector, pattern, or object-pattern pools.

Streaming/materialization contract:

- `:range` and `:series` sources must not be blindly materialized
  before streamable terminal selectors.
- Streamable/short-circuit terminal selectors include `:any`, `:all`, `:first`,
  and direct `:contains` without prefix selectors.
- Prefix selectors are applied lazily on the streaming path.
- Selectors that require full collection semantics may materialize after runtime
  budgets such as `MaxRangeItems` are checked.
- Non-range list-like sources should keep an indexed hot path for selectors such
  as `:sum`, `:average`, `:count`, and edge selectors.

### Generated Collections

Generated collection expressions lower to normal linear iterator control flow:

```text
CollectionBuilderList/Set builder
SlotLocals locals+=collectionLocalCount
RangeIterator* / CollectionIterator iterator
loop:
  StreamNext item iterator noMore
  SlotLocals locals+=iterationLocalCount
  MoveSlot identifier item
  optional predicate + JumpIfNotTrue skipProjection
  projection expression
  CollectionBuilderAdd builder projected
  SlotLocals locals-=iterationLocalCount
  Jump loop
noMore:
StreamClose iterator
SlotLocals locals-=collectionLocalCount
CollectionBuilderFinish dst builder
```

Direct ranges after `in` remain invalid at source level. Range iteration should
use explicit range-source syntax.

At runtime, collection builders are VM-internal values and are not visible as DSL
values. `CollectionBuilderAdd` applies `MaxGeneratedCollectionItems` while
materializing the result.

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
  preloaded argument slots plus optional parameter-cast execution.
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
@0000 SlotLocals locals+=5
@0001 SlotLocals locals+=1
@0003 CollectionIterator dst=s2 source=s0
@0004 StreamNext dst=s3 iterator=s2 noMore=@0010
@0005 ...
@0010 StreamClose iterator=s2
@0011 SlotLocals locals-=1
@0012 ReturnVoid
```

With typed parameters:

```text
handler DamageTaken(unit, amount)
  params:
    unit -> s0 as :unit
    amount -> s1 as :number

@0000 L_handler_DamageTaken:
@0000 SlotLocals locals+=localCount
@0001 Cast dst=s0 src=s0 kind=Custom type=:unit
@0002 Cast dst=s1 src=s1 kind=Integer
@0003 ...
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
