# GameEventScript Bytecode Spec

This document defines the intended portable bytecode shape for the
`GameEventScriptProgram` container.
The goal is a compact, portable, high-level bytecode for the GameEventScript DSL
that is naturally executable by a linear program-counter VM.

The public bytecode model is **operand-stack-free**. Normal expression
evaluation reads from and writes to explicit local registers. A portable call stack
is still part of the VM target state for calls, return addresses, frame
metadata, scoped locals, and resumable execution. The C# call stack is not part
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
- Keep the public artifact portable: no VM session state, no C# delegates, no
  bound extension functions, no AST nodes, and no runtime value constants.

## Non-Goals

- No public exposure of bytecode runtime implementation types.
- No requirement that the debug dump format matches the binary `.gesb`
  encoding.
- No requirement to split every DSL operation into primitive opcodes.
- No operand stack for expression evaluation.
- No C# call stack dependency for normal script control flow.

## Top-Level Artifact

The target binary container is named `GameEventScriptProgram`. Its header is the
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
GameEventScriptProgram
  Header
  ModuleName
  RequiredRegisterCount   max simultaneous VM register values of any handler
  RequiredCallStackDepth  max nested calls below any root handler
  StringPool              zero-based UTF-8 strings in the file
  UInt16SliceTable        compact ushort lists used by code and metadata
  BindTable
    Kind                  0x10-0x1F export, 0x20-0x2F import
    MessageHandler | MessageNameHandler | Function | Predicate | ExtensionCall | OutboundMessage | ExternalType
    Id                    bind id within its kind-specific namespace
    Name                  string-pool index
    ArgumentNames         ordered string-pool indexes
    EntryAddress          global code address for exports, 0 for imports
    RequiredRegisterCount message-handler requirement, otherwise 0
    RequiredCallStackDepth message-handler requirement, otherwise 0
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
- `LoadInteger` stores a signed 64-bit integer in the overlapped `I64` payload,
  plus optional numeric unit in `UnitAndFlags`.
- `LoadFloat` stores IEEE 754 double bits in the overlapped `F64` payload, plus
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

predicate wounded(_ unit as :unit) be unit.hp < unit.maxHp

function livingUnits(_ units as :list) be
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
@0001 Cast dst=r0 src=r0 kind=Custom type=:unit
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

The portable instruction is compact and allocation-free to execute. The public
instruction word is currently a 16-byte explicit-layout value: an 8-byte primary
word plus an aligned 8-byte payload word.

```text
Instruction
  OpCode
  UnitAndFlags
  DestinationRegister
  XRegister, YRegister
  ConditionRegister, TargetAddress, EntryAddress
  StringIndex, ListIndex, SecondaryListIndex, ExternalReferenceIndex, TypeOperand
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
- `StringIndex`, `ListIndex`, `SecondaryListIndex`, `ExternalReferenceIndex`, `TypeOperand`:
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
the group; reserved opcode pages are not modeled as separate groups. The current
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

The exhaustive opcode field map lives in `BytecodeOpcodeShape.md`. That table
lists every currently defined opcode as its own row, and every group ends with
one `reserved` row for its currently unused tail range.

For JSON transport, instructions serialize as a normalized primary-word plus
payload object:

```json
{ "Opcode": "LoadInteger", "Flags": "0x00", "Dst": "0x0007", "X": "0x0000", "Y": "0x0000", "Parameter": "0x000000000000002A" }
```

`Flags` is the raw `UnitAndFlags` byte, `Dst` is the raw destination register,
`X` and `Y` are the raw primary operands, and `Parameter` is the raw unsigned
64-bit payload word covering bytes `8..15`. For literal instructions the
payload carries `I64`, raw `Payload`, or the IEEE-754 `F64` bit pattern.

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

The C# stack is not part of script control flow. `Call` pushes a portable frame
record onto `callStack`; `ReturnValue` restores the next `pc` and writes the returned
value to the caller's destination register.

Manual stepping pauses after the instruction budget is consumed. The current
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
them with a single direct `:message` argument. Message-name handlers count as
normal delivery and therefore prevent `undeliverable` fallback when their tag
filters match. For `MessageNameHandler` entries, `SignatureId` still records the
synthetic message parameter signature such as `Damage(message)`, but it is
metadata and not the dispatch key.

System endpoint entries use reserved lowercase names outside normal message
casing. The currently defined endpoints are:

```text
initialization
undeliverable as message
```

`initialization` is encoded as a `MessageHandler` entry without
arguments. It is not normal external dispatch input; the host queues all loaded
initialization handlers directly when a `GameEventScriptContext` starts.

`undeliverable` is encoded as a `MessageNameHandler` entry with message name
`undeliverable`. It receives the original `:message` when no `MessageHandler`
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
`:number` lowers to `CastNumeric`; numeric casts keep integral values as
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
`CheckType :number` still checks the actual runtime kind. `numeric`, `integer`,
and `fractional` are not declared type kinds or `:` tags. VM-level `AsNumeric`
is valid exactly for values where `CheckNumeric` would be true; `CastNumeric` is
broader only in that it may explicitly parse text or first-term-cast series
before producing a numeric value or `nothing`.

`Cast :tag` is a validating cast. The target tag name must match source tag
syntax: first character lowercase letter, remaining characters letters only.
The empty string, numbers, quantities, percentages, formatted containers,
formatted vector/point values, whitespace, punctuation, brackets, colons inside
the value, and underscores are invalid and write `nothing`. Existing valid tags
remain unchanged. Boolean sources write `#true` or `#false`; text sources write a
tag only when the raw text is a valid tag name. Text values `true`, `True`,
`false`, and `False` are accepted and normalized to `#true` and `#false`.

`Cast :text` is the formatting cast and does not validate the formatted text as
a tag. `Cast :vector` and `Cast :point` are structural conversions: vector to
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
and `EndsWith`: comparisons use raw text without the `:` tag prefix, using
ordinal comparison, and `Count` counts raw text characters. Other operand
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
operand is formatted with the same representation used by `as :text`.

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
| `Tag` | Ordinal raw tag text. Numeric tag constants use numeric comparison when the other operand is also numeric-capable. |
| `Text` | Ordinal text. Text is not implicitly numeric. |
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
| `Map`/record/custom map-like | Same key set and recursively equal values. |

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
not an implicit `as :boolean` conversion.

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
message literals, handler binding, local calls, and predicate calls now
reference bind ids, entry addresses, `StringPool`, or `UShortListPool` directly
from the instruction word. Record constructor code addresses are stored on
`Record` bind entries so later module linking can rebind them centrally.

Call frame state:

```text
ReturnAddress
CallableIndex
CallerFrameBase
ReturnDestinationRegister
ScopeMark
RandomMark
```

Predicate bodies must compile as `:boolean` or DSL `nothing`; an explicit
`as :boolean` marks intentional boolean coercion. Predicate calls preserve
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
  immediately before the opcode as map values; keys stay in `keyNameListIndex`
  for now.
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
are valid source literals. Dynamic seeds must be statically visible as a
unitless integer number, usually by declaring the value as `:number` or writing
an explicit `as :number` cast.

Required portable value families:

- primitives: DSL `nothing`, `:tag`, `:text`, `:boolean`
- numeric: `:number`, `:percentage`
- numeric quantities: `:quantity(degree)`/`:quantity(°)`, `:quantity(m)`, `:quantity(s)`
- vectors: `:vector`
- points: `:point`
- containers: `:series`, `:range`, `:list`, `:map`, `:dice`
- runtime values: `:message`, `:handler`
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

External type constructors are collected separately in
`ExternalTypeConstructorReferences` and dynamically bound against the host's
external type registry. Record constructors and external constructors share the
same source syntax but must remain distinguishable in portable metadata.

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

- `:range` and `:series` sources must not be blindly materialized
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

## Debug Segment

Debug metadata is optional and may be generated for deterministic dumps,
source/register lookup, or debugger UIs.

```text
DebugSegment
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
register operands:

```text
code[26]
@0000 L_handler_Start:
@0000 RegisterLocals locals+=5
@0001 RegisterLocals locals+=1
@0003 IteratorCreate dst=r2 source=r0
@0004 IteratorNext dst=r3 iterator=r2 noMore=@0010
@0005 ...
@0010 IteratorClose iterator=r2
@0011 RegisterLocals locals-=1
@0012 ReturnVoid
```

With typed parameters:

```text
handler DamageTaken(unit, amount)
  params:
    unit -> r0 as :unit
    amount -> r1 as :number

@0000 L_handler_DamageTaken:
@0000 RegisterLocals locals+=localCount
@0001 Cast dst=r0 src=r0 kind=Custom type=:unit
@0002 Cast dst=r1 src=r1 kind=Integer
@0003 ...
@0004 ...
```

A grouped view may still be offered, but it must keep global addresses visible.

## Format Invariants

- New bytecode format changes must increment `FormatVersion`.
- Bytecode dumps are debug tooling output and are not a stable wire format.
- Public bytecode must remain deterministic for the same script/options.
- Host dynamic linking remains separate from compilation.
- Runtime extension binding is not serialized into portable bytecode.
- Runtime execution must be resumable without relying on the C# call stack.
- The portable model must preserve source-level short-circuit and tri-state
  truth semantics.
