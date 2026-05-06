# GameEventScript Bytecode Spec

Status: draft for the next public bytecode model.

This document defines the intended portable bytecode shape for
`GameEventScriptCompiled`. The goal is not to produce a tiny CPU-like instruction
set. The goal is a compact, portable, high-level bytecode that is efficient for
the GameEventScript DSL, easy to dump, and naturally executable by a stackless
VM with a program counter.

## Goals

- Represent executable script code as one linear instruction memory.
- Make every executable position addressable by a stable instruction address.
- Use labels only as debug/dump symbols that point at instruction addresses.
- Compile control flow such as `if`, loops, guarded choices, predicates, and functions
  into jumps and calls rather than nested program objects.
- Keep domain-heavy collection operations high-level when that is faster or
  simpler than expanding them into many tiny instructions.
- Keep the public artifact portable: no VM session state, no C# delegates, no
  bound extension functions, no AST nodes, and no runtime `GameEventScriptValue`
  constants.

## Non-Goals

- No public exposure of `StepH.GameEventScript.BytecodeVM` implementation types.
- No promise that this draft is the final binary `.gesb` encoding.
- No requirement to split every DSL operation into primitive opcodes.
- No C# call stack dependency for normal script control flow.

## Top-Level Artifact

The next public `GameEventScriptCompiled` model should be shaped around a single
code segment:

```text
GameEventScriptCompiled
  FormatVersion
  StringPool
  ConstantPool
  SignaturePool
  NamedArgumentLayouts
  ExternalReferences
  TypeMetadata
  Code: Instruction[]
  Handlers: HandlerEntry[]
  Callables: CallableEntry[]
  TypeDefinitions: TypeDefinitionEntry[]
  DebugSymbols
  MaxOperandStackDepth
  MaxCallStackDepth
  MaxLocalSlots
```

Existing pools remain important. They keep instructions small and preserve
portable data identity. Runtime values are decoded from bytecode constants only
when executing.

## Parameter Type Hints

Handlers, predicates, and functions may declare optional parameter type hints using the
same `as :type` language as value coercion:

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
- Different semantic meanings should use different message/predicate/function names
  instead of type overloads.
- A parameter type hint means "coerce this bound value as the entry starts".
- Coercion is lenient and uses the same conversion predicates as `value as :type`.
- After the entry coercion, the compiler and VM may treat the local slot as
  normalized to that type for optimization.

The portable metadata should carry the hint:

```text
ParameterEntry
  ExternalLabel
  LocalName
  Slot
  TypeName?
```

Compatibility note for the current nested bytecode model: until the linear
`Instruction[]` representation lands, handlers and callables expose this as a
parallel `ParameterTypes` list, and callable/predicate instructions carry
`DeclaredTypes` for their bound argument slots. The VM performs the same entry
coercion before executing the handler, predicate, or function body.

The executable code should still contain explicit coercion instructions so the
program counter and dump show where normalization happens:

```text
@0000 BindParameter s0 arg=unit
@0001 CoerceSlot s0 type=:unit
@0002 BindParameter s1 arg=amount
@0003 CoerceSlot s1 type=:integer
```

For untyped parameters the compiler emits only `BindParameter`.

## Instruction Addresses

An instruction address is the zero-based index into `Code`.

```text
@0000 LoadConstant #3
@0001 StoreSlot s1
@0002 JumpIfFalse @0010
```

Predicates:

- Runtime instructions store numeric addresses, not label strings.
- Labels are optional debug symbols: `L_if_else = @0010`.
- All handler, callable, type-field, and helper entry points are addresses in
  the same global `Code` array.
- Address `-1` means "none" only in metadata fields where an optional address is
  explicitly allowed.
- A dump may group addresses by handler or callable, but the address numbers are
  global and never restart at zero inside a block.

## Instruction Shape

The portable instruction should be compact and allocation-free to execute. A
logical instruction has:

```text
Instruction
  OpCode
  A
  B
  C
  Target
  Target2
  Data
```

The exact public C# type can use clearer property names, but the model should
avoid per-instruction object graphs. Operands are interpreted by opcode:

- `A`, `B`, `C`: slot indices, counts, pool indices, or small enum values.
- `Target`, `Target2`: instruction addresses.
- `Data`: index into a side table for larger DSL-specific metadata.

Large structured metadata belongs in side tables, not nested instruction
objects. Examples: publish layouts, pipeline plans, record type layouts, object
match patterns, and selector plans.

## Execution Model

A script VM session owns mutable execution state:

```text
pc
operandStack[]
locals[]
callStack[]
scopeStack[]
randomStack[]
activeHighLevelOperationState
```

The C# stack is not part of script control flow. `Call` pushes a return address
and frame metadata onto `callStack`; `Return` restores the next `pc`.

Manual stepping pauses after instruction budget is consumed. The current `pc`
is enough to point the dump/debugger at the active instruction. If execution is
inside a high-level operation such as a pipeline, the VM may also expose
operation-local debug state such as selector index or item index.

## Entry Tables

Handlers and callables are metadata over the shared code segment.

```text
HandlerEntry
  MessageName
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
```

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
```

These addresses point at expression code that returns one value on the operand
stack.

## Opcode Families

The opcode set should be high-level enough for GameEventScript, but flat enough
for `pc`-based execution.

### Stack and Slots

- `LoadConstant constantIndex`
- `LoadSlot slot`
- `StoreSlot slot`
- `BindParameter slot parameterIndex`
- `CoerceSlot slot typeIndex`
- `CoerceTop typeIndex`
- `Pop`
- `Duplicate`
- `LoadNothing`

`let` lowers to expression code followed by optional cast/coercion and
`StoreSlot`. Parameter type hints lower to `BindParameter` plus optional
`CoerceSlot` in the entry prologue.

### Scopes

- `EnterScope`
- `ExitScope`

Scopes are explicit instructions. They restore locals through the VM scope stack
instead of relying on C# `try/finally`.

### Arithmetic and Logic

Keep the existing optimized numeric and boolean operations where useful:

- `Add`, `Subtract`, `Multiply`, `Divide`
- `Power`, `IntegerDivide`, `Modulo`, `Remainder`
- primitive integer variants
- `Equal`, `NotEqual`, `ApproxEqual`, comparisons
- `And`, `Or`, `Xor`, `Unary`, `Variadic`
- `Cast`
- `Default`

Short-circuiting source constructs should compile to jumps when preserving lazy
behavior matters.

### Control Flow

- `Jump target`
- `JumpIfTrue target`
- `JumpIfFalse target`
- `Call callableIndex`
- `Return`
- `ReturnValue`
- `Halt`

`if` and guarded choices compile to condition code plus jumps. There should be
no nested `ThenProgram`, `ElseProgram`, or `GuardedChoiceProgram` in the public
bytecode.

Example:

```text
@0100 LoadSlot s0
@0101 LoadConstant #0
@0102 Greater
@0103 JumpIfFalse @0108
@0104 LoadConstant #1
@0105 StoreSlot s1
@0106 Jump @0110
@0108 LoadConstant #2
@0109 StoreSlot s1
@0110 ...
```

### Loops

Loops should be compiled to explicit loop control instructions and jumps.

Range loop shape:

```text
@0200 RangeLoopInit data=rangeLoopPlan
@0201 RangeLoopMoveNextOrJump data=rangeLoopPlan target=@0210
@0202 StoreSlot sItem
@0203 EnterScope
@0204 ...
@0208 ExitScope
@0209 Jump @0201
@0210 RangeLoopEnd data=rangeLoopPlan
```

Collection loop shape:

```text
@0300 CollectionLoopInit data=collectionLoopPlan
@0301 CollectionLoopMoveNextOrJump data=collectionLoopPlan target=@0310
@0302 StoreSlot sItem
@0303 ...
@0309 Jump @0301
@0310 CollectionLoopEnd data=collectionLoopPlan
```

Loop runtime state belongs to the VM session, indexed by the loop plan or by a
compiler-assigned loop temporary slot.

### Values and Containers

- `BuildList count`
- `BuildSequence count`
- `BuildSet count`
- `BuildDictionary layoutIndex`
- `BuildMessage publishOrMessageLayoutIndex`
- `BindHandler layoutIndex`
- `MemberAccess nameIndex`
- `IndexedAccess`
- `Range`
- `Dice`
- `Random`
- `TypeCheck typeIndex`
- `TypeConstructor typeIndex layoutIndex`

These remain high-level because they map directly to public value semantics.

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
- no filter: any tag set matches as long as the message signature matches.

The instruction should use a side-table layout:

```text
PublishLayout
  MessageName
  SignatureId
  ArgumentLabelsLayoutIndex
  ArgumentCount
  TagExpressionCount
```

Arguments are evaluated by preceding code and consumed from the operand stack in
layout order; tag expressions are evaluated after message arguments and merged
into the message envelope:

```text
@0400 LoadSlot sDamage
@0401 LoadSlot sTarget
@0402 LoadConstant :radio
@0403 Publish kind=Publish layout=ApplyDamage(damage,target) tags=1
```

Publishing or emitting a first-class message value can use a separate
`PublishMessageValue` instruction with the same `PublishKind` and tag payload.

### Extensions and Intrinsics

- `CallExtension externalReferenceIndex argumentCount`
- `CallIntrinsic intrinsicId argumentCount`

External references are still collected at compile time and dynamically bound by
the host when loading the compiled artifact. Standard intrinsics remain
non-overridable and do not appear in `ExternalReferences`.

### Collection DSL

Collection operations should stay high-level enough to avoid exploding code size
and losing optimized paths.

Recommended shape:

```text
Pipeline planIndex
GenerateCollection planIndex
MaterializeIterationSource planIndex
```

`PipelinePlan` is portable metadata, not nested code. It references selector
plans and expression entry addresses:

```text
PipelinePlan
  SourceAddress
  PrefixSelectors[]
  TerminalSelector

SelectorPlan
  Kind
  IdentifierSlot
  PredicateAddress?
  ProjectionAddress?
  SecondaryIdentifierSlot?
  SecondaryProjectionAddress?
  Count
  Mode
  PatternIndex?
```

The VM may execute a pipeline as one high-level operation while retaining
operation-local state for stepping. The active `pc` remains at the `Pipeline`
instruction; debug state can identify selector/item progress when needed.

## Calls and Returns

Predicates and functions are normal callable entries. A call instruction transfers
control to the callable entry and returns to the next instruction.

```text
@0500 LoadSlot sUnit
@0501 Call callable=wounded argc=1
@0502 JumpIfFalse @0510
```

Call frame state:

```text
ReturnAddress
CallableIndex
LocalBase or LocalSnapshotMark
OperandStackBase
ScopeMark
```

Predicates coerce their returned value to boolean at the callable boundary. Functions
preserve the expression result.

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
L_rule_wounded = @0500
```

The VM never needs labels to execute. They exist for dumps, debugger UI, traces,
and human-readable bytecode reviews.

## Dump Format

The primary bytecode dump should show a single global address space:

```text
code[26]
@0000 L_handler_Start:
@0000 EnterScope
@0001 BindParameter s0, arg=values
@0002 LoadSlot s0
@0003 Pipeline data=#0
@0004 StoreSlot s2
@0005 LoadSlot s2
@0006 LoadConstant #0
@0007 Greater
@0008 JumpIfFalse @0013 ; L_if_0_else
@0009 ...
@0013 L_if_0_else:
@0013 ...
@0025 Return
```

With typed parameters:

```text
handler DamageTaken(unit, amount)
  params:
    unit -> s0 as :unit
    amount -> s1 as :integer

@0000 L_handler_DamageTaken:
@0000 BindParameter s0, arg=unit
@0001 CoerceSlot s0, type=:unit
@0002 BindParameter s1, arg=amount
@0003 CoerceSlot s1, type=:integer
@0004 ...
```

A grouped view may still be offered, but it must keep global addresses visible.

## Migration Plan

1. Introduce the new public bytecode model as the next format version.
2. Update the compiler lowerer to emit linear `Code` plus metadata side tables.
3. Update the dumper to show global addresses and labels.
4. Update the BytecodeVM executable builder to consume linear public bytecode.
5. Remove nested `StatementProgram`, nested `ExpressionProgram`, and nested
   branch/body program references from the public artifact.
6. Keep conformance behavior unchanged; only bytecode shape and VM internals
   should move.

During migration, an adapter may convert the current nested model to the new
linear model internally, but the final public shape should be linear.

## Compatibility Predicates

- New bytecode format changes must increment `FormatVersion`.
- Old dumps are diagnostic only and are not a stable wire format.
- Public bytecode must remain deterministic for the same script/options.
- Host dynamic linking remains separate from compilation.
- Runtime extension binding is not serialized into portable bytecode.

## Open Design Questions

- Should all code live in one global `Instruction[]`, or should future binary
  encoding allow multiple segments while presenting one logical address space?
- Should high-level pipeline execution expose selector/item debug state through
  diagnostics, a debugger API, or only the bytecode dump?
- Should `Publish` consume already-evaluated stack values, or should the publish
  layout reference argument expression entry addresses? The stack-consuming form
  is simpler for a PC VM.
- Should call frames snapshot all locals, or use local-base frames plus explicit
  scope restore logs? The latter should allocate less but is more complex.
