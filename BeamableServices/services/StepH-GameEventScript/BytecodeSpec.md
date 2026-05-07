# GameEventScript Bytecode Spec

Status: draft for the next public bytecode model.

This document defines the intended portable bytecode shape for
`GameEventScriptCompiled`. The goal is a compact, portable, high-level bytecode
for the GameEventScript DSL that is naturally executable by a linear
program-counter VM.

The next VM model is **operand-stack-free**. Normal expression evaluation reads
from and writes to explicit local slots. A portable call stack is still part of
the VM state for calls, return addresses, frame metadata, scoped locals, and
resumable execution. The C# call stack is not part of script control flow.

## Goals

- Represent executable script code as one linear instruction memory.
- Make every executable position addressable by a stable instruction address.
- Use labels only as debug/dump symbols that point at instruction addresses.
- Use slot/register-style instructions: every value-producing instruction writes
  to a destination slot and reads operands from source slots or pools.
- Compile control flow such as `if`, loops, guarded choices, predicates, and
  functions into jumps and calls rather than nested program objects.
- Keep domain-heavy collection operations high-level when that is faster or
  simpler than expanding them into many tiny instructions.
- Preserve streaming and short-circuit behavior where the language requires it.
- Keep the public artifact portable: no VM session state, no C# delegates, no
  bound extension functions, no AST nodes, and no runtime `GameEventScriptValue`
  constants.

## Non-Goals

- No public exposure of `StepH.GameEventScript.BytecodeVM` implementation types.
- No promise that this draft is the final binary `.gesb` encoding.
- No requirement to split every DSL operation into primitive opcodes.
- No operand stack for expression evaluation.
- No C# call stack dependency for normal script control flow.

## Top-Level Artifact

The next public `GameEventScriptCompiled` model should be shaped around a single
code segment and side tables:

```text
GameEventScriptCompiled
  FormatVersion
  StringPool
  ConstantPool
  SignaturePool
  NamedArgumentLayouts
  ExternalReferences
  ExternalTypeConstructorReferences
  TypeMetadata
  Code: Instruction[]
  Handlers: HandlerEntry[]
  Callables: CallableEntry[]
  TypeDefinitions: TypeDefinitionEntry[]
  PublishLayouts
  CallLayouts
  PipelinePlans
  SelectorPlans
  IterationSourcePlans
  GeneratedCollectionPlans
  DicePatterns
  ObjectMatchPatterns
  DebugSymbols
  MaxFrameSlots
  MaxCallStackDepth
```

Existing pools remain important. They keep instructions small and preserve
portable data identity. Runtime values are decoded from bytecode constants only
when executing.

`MaxFrameSlots` is the maximum local slot count needed by any handler or
callable frame, including parameters, user `let` bindings, compiler temporaries,
loop temporaries, and high-level operation temporaries. It replaces the old
operand-stack-depth concept.

## Portable Constants

The constant pool stores portable bytecode constants, not runtime
`GameEventScriptValue` instances.

Required constant kinds:

- `Nothing`
- `Boolean`
- `Integer`: signed 64-bit integer plus optional numeric unit
- `Float`: IEEE 754 double plus optional numeric unit, preserving `NaN`,
  `Infinity`, and `-Infinity`
- `Percentage`: double ratio
- `Text`
- `Tag`
- `Uuid`: two signed 64-bit words representing the canonical RFC 128-bit value
- `Handler`: message name plus signature labels

`Ref` values may be represented either as a typed constructor plan or as a
portable constant containing target type plus id value. If encoded as constants,
the id must be a portable scalar value such as `Text` or `Uuid`.

Constants are concrete. For example `:integer 1`, `:float 1`, `1m`, and `1s`
must remain distinct constants even if runtime operations can compare or coerce
some of them.

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
@0001 CoerceSlot dst=s0 src=s0 type=:unit
@0002 BindParameter dst=s1 arg=amount
@0003 CoerceSlot dst=s1 src=s1 type=:integer
```

For untyped parameters the compiler emits only `BindParameter`.

## Instruction Addresses

An instruction address is the zero-based index into `Code`.

```text
@0000 LoadConstant dst=s3 constant=#3
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

The portable instruction should be compact and allocation-free to execute. A
logical instruction has:

```text
Instruction
  OpCode
  Dest
  A
  B
  C
  Target
  Target2
  Data
```

The exact public C# type can use clearer property names, but the model should
avoid per-instruction object graphs. Operands are interpreted by opcode:

- `Dest`: destination slot, or `-1` for instructions that do not produce a value.
- `A`, `B`, `C`: source slots, counts, pool indices, or small enum values.
- `Target`, `Target2`: instruction addresses.
- `Data`: index into a side table for larger DSL-specific metadata.

Large structured metadata belongs in side tables, not nested instruction
objects. Examples: publish layouts, call layouts, pipeline plans, selector
plans, record type layouts, object match patterns, dice patterns, and loop
plans.

An instruction that produces `nothing` writes it to `Dest`. There is no implicit
push. There are no `Pop` or `Duplicate` instructions in the portable target
model.

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
`undeliverable` fallback when their tag filters match.

System endpoint entries use reserved lowercase names outside normal message
casing and bind their envelope with explicit `as` syntax. The currently defined
endpoint is:

```text
undeliverable as envelope
```

It receives a dictionary-backed `:envelope` value when no normal handler could
be queued for a message after signature and tag filters were applied. System
endpoints still use normal handler metadata, priority, declaration order, and
tag filters.

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
return slot.

## Opcode Families

The opcode set should be high-level enough for GameEventScript, but flat enough
for `pc`-based execution.

### Slots and Coercion

- `LoadConstant dst constantIndex`
- `LoadNothing dst`
- `Move dst src`
- `BindParameter dst parameterIndex`
- `CoerceSlot dst src typeIndex`
- `Cast dst src castKind`
- `TypeCheck dst src typeIndex`

`let` lowers to expression code that writes into a temporary or final slot,
followed by optional `Cast`/`CoerceSlot` and `Move` into the declared local slot.
Parameter type hints lower to `BindParameter` plus optional `CoerceSlot` in the
entry prologue.

### Scopes

- `EnterScope data=scopePlan`
- `ExitScope data=scopePlan`

Scopes are explicit instructions. They restore locals through the VM scope stack
instead of relying on C# `try/finally`.

### Arithmetic and Logic

Binary operations read source slots and write `Dest`:

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
- `Unary`
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
@0105 LoadConstant dst=sDst constant=false
@0106 ...
```

The exact lowering may differ, but the right-hand expression of `and`, `or`, and
`->` must be skipped when the language's short-circuit rule decides the result.
Implication is right-associative at source level and uses the truth table for
`not a or b`: false antecedent yields `true`; unknown participates as
`nothing` unless the consequent resolves the result to `true`.

Compatibility note for the current nested bytecode model: until the linear
format lands, `and`, `or`, and implication may still appear as
`ShortCircuitAnd`, `ShortCircuitOr`, and `ShortCircuitImplies` instructions with
a nested right-hand expression program.

### Control Flow

- `Jump target`
- `JumpIfTrue cond target`
- `JumpIfFalse cond target`
- `JumpIfNothing cond target`
- `JumpIfNotTrue cond target`
- `JumpIfNotFalse cond target`
- `Call dst callableIndex callLayoutIndex`
- `Return src`
- `Halt`

`if` and guarded choices compile to condition code plus jumps. `else` runs when
the condition is not true, so normal `if` lowering should use `JumpIfNotTrue`,
not an implicit `as :boolean` conversion.

Example:

```text
@0100 Move dst=s10 src=s0
@0101 LoadConstant dst=s11 constant=#0
@0102 Greater dst=s12 left=s10 right=s11
@0103 JumpIfNotTrue cond=s12 target=@0108
@0104 LoadConstant dst=s1 constant=#1
@0105 Jump @0110
@0108 LoadConstant dst=s1 constant=#2
@0110 ...
```

Guarded expressions lower to a sequence of condition jumps and value writes to a
shared destination slot.

### Calls and Returns

Predicates and functions are normal callable entries. A call instruction
transfers control to the callable entry and returns to the next instruction.

```text
@0500 Call dst=s3 callable=wounded args=#0
@0501 JumpIfNotTrue cond=s3 target=@0510
```

`CallLayout` describes the source argument slots and their labels:

```text
CallLayout
  ArgumentCount
  ArgumentSlots[]
  ArgumentLabelsLayoutIndex
```

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

- `BuildList dst itemStart count`
- `BuildSequence dst itemStart count`
- `BuildSet dst itemStart count`
- `BuildDictionary dst layoutIndex`
- `BuildMessage dst layoutIndex`
- `BindHandler dst calleeSlot callLayoutIndex`
- `MemberAccess dst targetSlot nameIndex`
- `IndexedAccess dst targetSlot selectorSlot`
- `Range dst fromSlot toSlot stepSlot`
- `Dice dst count sides`
- `Random dst fromSlot toSlot`
- `SeededRandom dst seedSlot bodyAddress`
- `TypeConstructor dst typeIndex callLayoutIndex`

These remain high-level because they map directly to public value semantics.

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

`emit` and `publish` should use the same high-level instruction family. The
instruction records intent with a small kind field:

```text
PublishKind
  Emit
  Publish
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

The instruction uses a side-table layout:

```text
PublishLayout
  MessageName
  SignatureId
  ArgumentLabelsLayoutIndex
  ArgumentSlots[]
  TagSlots[]
```

Arguments and tags are evaluated by preceding code into slots:

```text
@0400 Move dst=s20 src=sDamage
@0401 Move dst=s21 src=sTarget
@0402 LoadConstant dst=s22 constant=:radio
@0403 Publish kind=Publish layout=#0
```

Publishing or emitting a first-class message value can use a separate
`PublishMessageValue kind messageSlot tagSlotsLayout` instruction.

### Extensions, Intrinsics, and External Types

- `CallExtension dst externalReferenceIndex callLayoutIndex`
- `CallStandard dst standardIntrinsicId callLayoutIndex`
- `TypeConstructor dst typeIndex callLayoutIndex`

External references are collected at compile time and dynamically bound by the
host when loading the compiled artifact. Runtime extension binding is not
serialized into portable bytecode.

Standard intrinsics are non-overridable and do not appear in
`ExternalReferences`. The target linear model should represent them as
`CallStandard` or an equivalent direct intrinsic id, not as a host extension
lookup.

Compatibility note for the current nested bytecode model: standard intrinsics
are currently parsed like extensions and may execute through `CallExtension`
with an external reference slot of `-1`.

External type constructors are collected separately in
`ExternalTypeConstructorReferences` and dynamically bound against the host's
external type registry. Record constructors and external constructors share the
same source syntax but must remain distinguishable in portable metadata.

### Collection DSL

Collection operations should stay high-level enough to avoid exploding code size
and losing optimized paths.

Recommended shape:

```text
Pipeline dst sourceSlot planIndex
GenerateCollection dst planIndex
MaterializeIterationSource dst planIndex
```

`PipelinePlan` is portable metadata, not nested code. It references selector
plans and expression entry addresses:

```text
PipelinePlan
  PrefixSelectorPlanIndexes[]
  TerminalSelectorPlanIndex

SelectorPlan
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
  PatternIndex?
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

Generated collection expressions lower to a high-level plan:

```text
GeneratedCollectionPlan
  CollectionType: list | set
  IdentifierSlot
  IterationSourcePlanIndex
  PredicateAddress?
  PredicateResultSlot?
  ProjectionAddress
  ProjectionResultSlot
```

Iteration sources:

```text
IterationSourcePlan
  Kind: Collection | Range
  CollectionSlot?
  RangeFromSlot?
  RangeToSlot?
  RangeStepSlot?
```

Direct ranges after `in` remain invalid at source level. Range iteration should
use the explicit range source fields.

### Patterns

Dice and object-match patterns are side-table data.

```text
DicePattern
  Kind: FullHouse | Straight | Count
  Count?
  FaceAddress?
  FaceResultSlot?

ObjectMatchPattern
  Entries[]

ObjectMatchEntry
  Key
  ValueKind: Expression | NestedPattern
  ExpressionAddress?
  ExpressionResultSlot?
  NestedPatternIndex?
```

## Debug Symbols

Debug symbols are optional but should be generated when compile diagnostics are
enabled, and may also be generated for deterministic dumps.

```text
DebugSymbols
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
@0003 LoadConstant dst=s3 constant=#0
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
@0001 CoerceSlot dst=s0 src=s0 type=:unit
@0002 BindParameter dst=s1 arg=amount
@0003 CoerceSlot dst=s1 src=s1 type=:integer
@0004 ...
```

A grouped view may still be offered, but it must keep global addresses visible.

## Migration Plan

1. Finalize this operand-stack-free public bytecode model and increment
   `FormatVersion`.
2. Add side-table types for call layouts, publish layouts, pipeline plans,
   selector plans, iteration sources, generated collections, patterns, scopes,
   and loop plans.
3. Update the compiler lowerer to allocate parameter, local, and temporary slots
   and emit linear `Code`.
4. Update the dumper to show global addresses, labels, and explicit slot
   operands.
5. Update the BytecodeVM executable builder to consume linear public bytecode.
6. Remove nested `StatementProgram`, nested `ExpressionProgram`, and nested
   branch/body program references from the public artifact.
7. Keep conformance behavior unchanged; only bytecode shape and VM internals
   should move.

During migration, an adapter may convert the current nested model to the new
linear model internally, but the final public shape should be linear and
operand-stack-free.

## Compatibility Predicates

- New bytecode format changes must increment `FormatVersion`.
- Old dumps are diagnostic only and are not a stable wire format.
- Public bytecode must remain deterministic for the same script/options.
- Host dynamic linking remains separate from compilation.
- Runtime extension binding is not serialized into portable bytecode.
- Runtime execution must be resumable without relying on the C# call stack.
- The portable model must preserve source-level short-circuit and tri-state
  truth semantics.

## Open Design Questions

- Should the physical binary encoding use one code segment, or allow multiple
  segments while presenting one logical global address space?
- Should high-level pipeline execution expose selector/item debug state through
  diagnostics, a debugger API, or only the bytecode dump?
- Should call frames use local-base frame slices or per-frame local arrays? Frame
  slices should allocate less but are more complex.
- Should `Ref` constants be first-class constants, or should refs always be
  encoded as constructor operations?
- Which standard intrinsics deserve dedicated opcodes instead of
  `CallStandard` ids?
