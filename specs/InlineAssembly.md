<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Inline assembly implementation contract

Status: C# and Swift implementations and initial measurements qualified. Each
implementation milestone must qualify the corresponding behavior in shared
Markdown Conformance before marking it implemented. Inline assembly is GES
source syntax, not a reader for the GESA dump format.

## Scope and syntax

The portable C# and Swift Compilers implement the same contract. No additional
module, opcode, binary section, or host-level ASM permission is required.

```ges
on Example(input) {
    asm {
        let x, y
        LoadInteger x, 10
        LoadInteger y, 20
    }
    let square be asm {
        Multiply square, x, x
    }
}

function square(x) be asm {
    Multiply square, x, x
}

predicate positive(x) be asm {
    .register zero
    LoadInteger zero, 0
    Greater positive, x, zero
}

record :Example as {
    x: :Number computed by asm {
        LoadInteger x, 10
    }
}
```

An independent ASM statement exports zero or more bindings declared by `let`.
Declarations have no initializer and can list multiple names. These bindings
become visible after the block in its containing lexical scope. A block in a
branch or loop never exports bindings beyond that containing scope.

An ASM initializer, callable body, or computed field has one implicit output
register named after its binding, callable, or field. These forms reject `let`
declarations. `.register name` declares block-local temporary storage in all
forms. Declarations precede executable instructions, apply once per block entry,
and emit no initialization instructions. Nested ASM blocks are not supported.

Parameters and all visible outer bindings are read-only. Existing names cannot
be shadowed. A callable-name/parameter collision is rejected only for an ASM
body. The implicit output is a fresh register, never an alias to an outer value.
Computed fields see only the inputs available to ordinary computed expressions.
Normal field casts, parameter casts, predicate result validation and predicate
normalization remain in force. ASM is not a general nested expression form.

Instructions use canonical opcode names. Register/label names follow ordinary
binding-name rules; labels occupy a separate block-local namespace. `name:`
marks an instruction boundary. Label references never cross block boundaries.
Unknown instructions and numeric opcode escapes are rejected.

## Operands and expansion

Register sources accept names or scalar literals. Literals are evaluated in
operand order and expand to explicit load instructions when a register is
required. Exact-encoding measurements should use explicit `Load…` instructions.
No arbitrary GES expressions occur in instruction operands. Units, types,
pattern kinds, modes, members, and counts are symbolic or literal operands,
never undocumented bit fields. Opcode-specific immediate ranges are checked.

String and list pool indices, binding IDs and code addresses are compiler-owned.
Text and tags use literals. Calls use the callable name and ordered argument
labels. Extensions use their normal namespace and ordered labels. Messages use
normal message signatures and tag syntax. Result-bearing/delayed sends name a
destination and preserve normal send behavior. Constructors use symbolic types
and argument labels. A syntax table for each symbolic expansion is required
before enabling that family; no fallback to raw pool indices is permitted.

Raw Stage instructions, RegisterLocals and VM Return instructions are forbidden.
Symbolic calls and staged constructors expand to one contiguous staging/consumer
sequence. No user label may enter that expansion. Calls participate in the normal
acyclic call graph and resource analysis. CreateRecordValue remains internal;
record construction must not bypass field/constructor semantics.

## Register and control-flow analysis

Every output and temporary starts abstractly uninitialized. No implicit
LoadNothing is emitted. Reads require definite initialization on all reachable
incoming paths. Every output must be initialized on every normal block exit.
Unreachable instructions still receive structural, operand and name checks.
Branch joins intersect definite-initialization sets; loops require a fixed point.
Source operands are checked before destination writes, including aliasing cases.
IteratorNext writes an item on success and Nothing on exhaustion; both edges
initialize its destination.

Physical register allocation must preserve values across back edges and branches.
Read-only inputs must stay live for all their reads; output/input aliasing must
not expose a write to a still-live outer binding. No automatic initializer may
be assumed from recycled physical registers. Labels have no runtime scope cost.

Iterators and builders are internal mutable capabilities, not GES output values.
They are permitted only in temporary registers, cannot be copied through Move,
returned/exported, stored in collections, or passed to messages/calls/extensions.
Each operation requires a compatible lifecycle state. Merges of incompatible
states, use after closing/finishing, and overwriting live resources are rejected.
Actual consumer/close semantics must be audited before enabling each family.
Random scopes must balance on all outgoing paths; a block cannot pop a caller's
scope. Runtime limits and boundary restoration remain active.

## Preservation and safety

Explicit instructions retain their order and are not folded, removed, combined
or hoisted. Surrounding optimization cannot delete a complete ASM block or move
effects across it. Pool interning, symbolic expansion, register assignment,
source maps and mandatory callable/field boundaries remain compiler work.
Effect analysis includes extensions, host member getters, random operations,
sends, constructors, custom casts and ParseLiteral's possible constructor calls.
Permitted effects follow the enclosing GES context; ASM adds no permission to
send messages from ordinary expression-only contexts.

A Program does not retain the AST or compiler analysis state. No ASM marker is a
security boundary. All untrusted Programs must be safe at Reader, Writer,
Compiler and Host.Load validation boundaries. The existing frame/staging/call
analysis is not by itself proof of internal-value safety. Adversarial binary
fixtures must qualify internal handles, resource use, aliases and escape paths.
Any required safety fix applies equally to non-ASM Programs in both runtimes.

## Delivery and verification

1. Contract and complete opcode inventory (this document).
2. Audit shared binary validation and qualify/harden internal-value safety.
3. C# parser, AST, symbol/flow analysis, preservation and lowering.
4. Swift implementation of the identical contract.
5. Normative language documentation, editor grammars, highlighters and examples.
6. Shared Conformance acceptance, binary interoperability and targeted native gates.
7. Collection-opcode comparison workloads and measured report.

Behavior, syntax, scoping, context restrictions, diagnostics and failures belong
primarily in shared Markdown Conformance. Assertions use stable phase/code and
structured context, never diagnostic prose. Prefer existing GESA snapshots and
binary fixture mechanisms to new native behavior tests. Native tests are limited
to implementation properties not expressible through the portable corpus,
including allocation, register allocation internals and measurement controls.

There is no Sum opcode today: sum already lowers to an iterator and arithmetic.
Candidate comparisons include Count, HasAny/HasAll, First/Last/Single,
Take/Drop, Distinct and ordering. Each pair must preserve type, Nothing/NaN,
unit, ordering and empty-input semantics. Timing excludes compilation, assembly
and input creation. Report allocations, code size, limits and pause granularity
as well as elapsed time; do not infer that all operations behave alike.

## Implementation progress

- Step 1: contract and inventory recorded.
- Step 2: internal-value escape validation implemented in both runtimes. The
  shared `program.internal-values` corpus has 19 binary fixtures covering all
  six internal families, copying, staging, message/return escape, self-insertion,
  branch/loop paths, wrong builder families, valid finishing, register release,
  overwrite, iterator closing and collection consumption. Validation uses two
  instruction-sized scratch arrays in C# and equivalent bounded worklists in
  Swift, rather than an instruction-by-register state matrix. It follows each
  reachable producer until overwrite/release; worst-case work is proportional
  to producer count times instruction count. VM dispatch is unchanged.
- Step 3: C# source parsing, explicit instructions, symbolic calls/sends and
  constructors, definite initialization, internal resource lifetimes and random
  scope balance are implemented. ASM locals use pinned physical storage because
  ordinary temporary intervals do not describe arbitrary backward branches.
  Optimization is preserved for other routines; a routine containing ASM is
  retained as lowered. Calls are conservatively effectful when such a routine
  exists. This is a compiler tradeoff, not an additional VM scope or frame.
- Step 4: Swift implements the same source syntax, operand forms and control-flow
  rules. All 39 assembly cases pass with both native compilers; the complete Swift
  corpus passes 1,722 cases with 31 optional performance cases skipped.
- Step 5: the guide and runnable example document the four binding contexts,
  symbolic forms and measurement tradeoffs. Both TextMate variants, Sublime and
  the embedded C#/Swift highlighters recognize ASM; shared highlighting cases
  qualify complete-document and incremental rendering.
- Step 6: 60 shared ASM cases cover valid execution, errors, instruction
  preservation, symbolic messages/calls, signed values and resource joins. The
  complete native Swift compiler corpus passes 1,774 cases; strict portable
  acceptance passes 1,743 and skips 31 optional performance cases. Swift Runtime
  passes 1,414 scenarios using C#-compiled inputs. C# Release/allocation, Swift
  API, native adapters, CLI, numeric text and product JSON roundtrips pass.
- Step 7: reproducible C# comparisons of Count, HasAny, HasAll and First against
  explicit iterator loops record elapsed time, allocation, code size, registers
  and pause granularity. The [measured report](../docs/guide/InlineAssemblyBenchmarks.md)
  supports retaining these opcodes; it does not generalize to unmeasured operations
  or runtimes. No opcode changes follow from this initial study.

## Complete opcode inventory

The table below is derived from the current Bytecode specification. Direct
means explicit operands, not unchecked bits. All categories still require
initialization, context, effect and resource checks.

| Opcode | Handling |
| --- | --- |
| `Nop` | Direct; typed operands |
| `RegisterLocals` | Forbidden; compiler-owned |
| `Jump` | Block-local label |
| `JumpIfTrue` | Block-local label |
| `JumpIfFalse` | Block-local label |
| `JumpIfNotTrue` | Block-local label |
| `JumpIfNothing` | Block-local label |
| `Call` | Symbolic expansion |
| `CreateSeries` | Direct; typed operands |
| `CallExternal` | Symbolic expansion |
| `ReturnVoid` | Forbidden; compiler-owned |
| `ReturnValue` | Forbidden; compiler-owned |
| `EmitMessage` | Symbolic expansion |
| `EmitMessageWithTags` | Symbolic expansion |
| `EmitMessageValue` | Symbolic expansion |
| `EmitMessageValueWithTags` | Symbolic expansion |
| `PublishMessage` | Symbolic expansion |
| `PublishMessageWithTags` | Symbolic expansion |
| `PublishMessageValue` | Symbolic expansion |
| `PublishMessageValueWithTags` | Symbolic expansion |
| `Cast` | Direct; typed operands |
| `CastCustom` | Symbolic text pool reference |
| `CastUnit` | Direct; typed operands |
| `CastNumeric` | Direct; typed operands |
| `CheckType` | Direct; typed operands |
| `CheckCustomType` | Symbolic text pool reference |
| `CheckUnit` | Direct; typed operands |
| `CheckNumeric` | Direct; typed operands |
| `CheckInteger` | Direct; typed operands |
| `CheckFractional` | Direct; typed operands |
| `Move` | Direct; typed operands |
| `MemberAccess` | Symbolic text pool reference |
| `IndexAccess` | Direct; typed operands |
| `PropertyAccess` | Direct; typed operands |
| `BindHandler` | Symbolic expansion |
| `LoadNothing` | Direct; typed operands |
| `LoadTrue` | Direct; typed operands |
| `LoadFalse` | Direct; typed operands |
| `LoadInteger` | Direct; typed operands |
| `LoadFloat` | Direct; typed operands |
| `LoadPercentage` | Direct; typed operands |
| `LoadText` | Symbolic text pool reference |
| `LoadTag` | Symbolic text pool reference |
| `LoadHandler` | Symbolic expansion |
| `LoadMessage` | Symbolic expansion |
| `StageRegister` | Forbidden; compiler-owned |
| `StageNothing` | Forbidden; compiler-owned |
| `StageTrue` | Forbidden; compiler-owned |
| `StageFalse` | Forbidden; compiler-owned |
| `StageInteger` | Forbidden; compiler-owned |
| `StageFloat` | Forbidden; compiler-owned |
| `StageText` | Forbidden; compiler-owned |
| `StageTag` | Forbidden; compiler-owned |
| `StagePercentage` | Forbidden; compiler-owned |
| `CreateDice` | Direct; typed operands |
| `CreateVector` | Symbolic expansion |
| `CreatePoint` | Symbolic expansion |
| `CreateList` | Symbolic expansion |
| `CreateMap` | Symbolic expansion |
| `CreateRange` | Direct; typed operands |
| `CreateRangeWithStep` | Direct; typed operands |
| `CreateRangeIterator` | Internal resource and edge-state analysis |
| `CreateRangeIteratorWithStep` | Internal resource and edge-state analysis |
| `CreateRangeIteratorShort` | Internal resource and edge-state analysis |
| `CreateRecord` | Symbolic expansion |
| `CreateRecordValue` | Forbidden; compiler-owned |
| `CreateExternalType` | Symbolic expansion |
| `HasValue` | Direct; typed operands |
| `IsEmpty` | Direct; typed operands |
| `Default` | Direct; typed operands |
| `Or` | Direct; typed operands |
| `And` | Direct; typed operands |
| `Xor` | Direct; typed operands |
| `Implies` | Direct; typed operands |
| `Not` | Direct; typed operands |
| `Equal` | Direct; typed operands |
| `NotEqual` | Direct; typed operands |
| `Less` | Direct; typed operands |
| `Greater` | Direct; typed operands |
| `LessOrEqual` | Direct; typed operands |
| `GreaterOrEqual` | Direct; typed operands |
| `Add` | Direct; typed operands |
| `Subtract` | Direct; typed operands |
| `Multiply` | Direct; typed operands |
| `Divide` | Direct; typed operands |
| `Power` | Direct; typed operands |
| `IntegerDivide` | Direct; typed operands |
| `Modulo` | Direct; typed operands |
| `Remainder` | Direct; typed operands |
| `Min` | Direct; typed operands |
| `Max` | Direct; typed operands |
| `Negate` | Direct; typed operands |
| `Abs` | Direct; typed operands |
| `LogN` | Direct; typed operands |
| `Chance` | Direct; typed operands |
| `Clamp` | Direct; typed operands |
| `RandomTake` | Direct; typed operands |
| `RandomTakeFloat` | Direct; typed operands |
| `RandomPush` | Balanced random scope |
| `RandomPushConstant` | Balanced random scope |
| `RandomPop` | Balanced random scope |
| `Term` | Direct; typed operands |
| `Exp` | Direct; typed operands |
| `Floor` | Direct; typed operands |
| `Ceil` | Direct; typed operands |
| `Truncate` | Direct; typed operands |
| `RoundHalfEven` | Direct; typed operands |
| `RoundHalfUp` | Direct; typed operands |
| `RoundHalfDown` | Direct; typed operands |
| `DegreeToRadians` | Direct; typed operands |
| `DegreeFromRadians` | Direct; typed operands |
| `WrapDegree` | Direct; typed operands |
| `Sin` | Direct; typed operands |
| `Cos` | Direct; typed operands |
| `Tan` | Direct; typed operands |
| `Asin` | Direct; typed operands |
| `Acos` | Direct; typed operands |
| `Atan` | Direct; typed operands |
| `Atan2` | Direct; typed operands |
| `Hypot2D` | Direct; typed operands |
| `Hypot3D` | Direct; typed operands |
| `Distance` | Direct; typed operands |
| `Distance2D` | Direct; typed operands |
| `Distance3D` | Direct; typed operands |
| `DistanceSquared` | Direct; typed operands |
| `DistanceSquared2D` | Direct; typed operands |
| `DistanceSquared3D` | Direct; typed operands |
| `LengthSquared` | Direct; typed operands |
| `LengthSquared2D` | Direct; typed operands |
| `LengthSquared3D` | Direct; typed operands |
| `Normalize` | Direct; typed operands |
| `Normalize2D` | Direct; typed operands |
| `Normalize3D` | Direct; typed operands |
| `Dot` | Direct; typed operands |
| `Dot2D` | Direct; typed operands |
| `Dot3D` | Direct; typed operands |
| `Cross` | Direct; typed operands |
| `Cross2D` | Direct; typed operands |
| `Cross3D` | Direct; typed operands |
| `AngleBetween` | Direct; typed operands |
| `AngleBetween2D` | Direct; typed operands |
| `AngleBetween3D` | Direct; typed operands |
| `TakeFirst` | Direct; typed operands |
| `DropFirst` | Direct; typed operands |
| `TakeLast` | Direct; typed operands |
| `DropLast` | Direct; typed operands |
| `TakeHighest` | Direct; typed operands |
| `TakeLowest` | Direct; typed operands |
| `DropHighest` | Direct; typed operands |
| `DropLowest` | Direct; typed operands |
| `OneRandom` | Direct; typed operands |
| `TakeRandom` | Direct; typed operands |
| `OneWeighted` | Direct; typed operands |
| `TakeWeighted` | Direct; typed operands |
| `Count` | Direct; typed operands |
| `StartsWith` | Direct; typed operands |
| `EndsWith` | Direct; typed operands |
| `Contains` | Direct; typed operands |
| `ContainsAny` | Direct; typed operands |
| `ContainsAll` | Direct; typed operands |
| `HasAny` | Direct; typed operands |
| `HasAll` | Direct; typed operands |
| `ContainsValue` | Direct; typed operands |
| `Union` | Direct; typed operands |
| `Intersect` | Direct; typed operands |
| `Zip` | Direct; typed operands |
| `KeysOfMap` | Direct; typed operands |
| `ValuesOfMap` | Direct; typed operands |
| `EntriesOfMap` | Direct; typed operands |
| `First` | Direct; typed operands |
| `Last` | Direct; typed operands |
| `Single` | Direct; typed operands |
| `IteratorCreate` | Internal resource and edge-state analysis |
| `IteratorCreateOrJump` | Internal resource and edge-state analysis |
| `IteratorNext` | Internal resource and edge-state analysis |
| `IteratorClose` | Internal resource and edge-state analysis |
| `Distinct` | Direct; typed operands |
| `SortAscending` | Direct; typed operands |
| `SortDescending` | Direct; typed operands |
| `Reverse` | Direct; typed operands |
| `Shuffle` | Direct; typed operands |
| `ListBuilderCreate` | Internal resource and edge-state analysis |
| `ListBuilderAdd` | Internal resource and edge-state analysis |
| `ListBuilderFinish` | Internal resource and edge-state analysis |
| `MapBuilderCreate` | Internal resource and edge-state analysis |
| `MapBuilderAdd` | Internal resource and edge-state analysis |
| `MapBuilderFinish` | Internal resource and edge-state analysis |
| `DistinctBuilderCreate` | Internal resource and edge-state analysis |
| `DistinctBuilderAdd` | Internal resource and edge-state analysis |
| `DistinctBuilderFinish` | Internal resource and edge-state analysis |
| `GroupBuilderCreate` | Internal resource and edge-state analysis |
| `GroupBuilderAdd` | Internal resource and edge-state analysis |
| `GroupBuilderFinish` | Internal resource and edge-state analysis |
| `OrderBuilderCreate` | Internal resource and edge-state analysis |
| `OrderBuilderAdd` | Internal resource and edge-state analysis |
| `OrderBuilderFinishAscending` | Internal resource and edge-state analysis |
| `OrderBuilderFinishDescending` | Internal resource and edge-state analysis |
| `HasPattern` | Direct; typed operands |
| `TakePattern` | Direct; typed operands |
| `ParseLiteral` | Direct; constructor effects and call graph |
| `EmitInstant` | Symbolic expansion |
| `EmitAfter` | Symbolic expansion |
| `PublishInstant` | Symbolic expansion |
| `PublishAfter` | Symbolic expansion |
| `ConstructData` | Symbolic expansion |
| `SplitText` | Direct; typed operands |

Inventory: 206 defined opcodes; reserved numeric values are forbidden.

## Symbolic source operands

Operands are comma-separated. Direct opcodes follow the operand order of the
compiler's typed builder (destination first when present). Source registers may
be replaced by scalar literals. Labels and destination registers require names.
Instruction names are case-sensitive. Immediate integers are range-checked.

| Source form | Meaning |
| --- | --- |
| `Call result, calculate(value: input)` | Resolve function/predicate and stage ordered arguments |
| `CallExternal result, :math.calculate(value: input)` | Resolve extension signature |
| `BindHandler result, handler(value: input)` | Bind ordered arguments to a handler register |
| `CreateList result, [first, second]` | Stage values and construct a list |
| `CreateMap result, [left: first, right: second]` | Intern keys and stage values |
| `CreateRecord result, :Sample(value: input)` | Invoke a script constructor |
| `CreateExternalType result, :Sample(value: input)` | Invoke a registered external constructor |
| `CreateVector result, :Vector(x: first, y: second)` | Construct a vector with ordered components |
| `CreatePoint result, :Point(x: first, y: second)` | Construct a point with ordered components |
| `ConstructData result, :Record('Sample', fields)` | Use an explicit data constructor |
| `LoadHandler result, Example(value)` | Intern a handler signature |
| `LoadMessage result, Example(value: input)` | Construct a message value |
| `emit Example(value: input) with #tag` | Ordinary statement send, including ordered tags |
| `publish Example(value: input)` | Ordinary publication |
| `EmitMessage Example(value: input)` | Canonical statement opcode spelling |
| `EmitMessageWithTags Example(value: input), [#tag]` | Canonical tagged statement spelling |
| `EmitMessageValue message` | Send a message register; tagged form takes a second list operand |
| `EmitInstant result, Example(value: input)` | Result-bearing immediate send |
| `EmitAfter result, 1s, Example(value: input), [#tag]` | Result-bearing delayed send, with optional tag list |

Each `Emit…` form also has a corresponding `Publish…` form. Expression blocks
in handlers may send messages; function, predicate and computed-field blocks may
not. The explicit `emit`/`publish` spellings retain their normal message syntax.
Arguments inside symbolic forms must be scalar literals or register names;
compute more involved values with preceding instructions or ordinary GES.

`Cast`/`CheckType` accept a built-in type such as `:Number`. These are the direct
VM operations; use `CastNumeric` for the VM's numeric conversion operation.
`CastCustom`/`CheckCustomType` take literal type names. `CastUnit`/`CheckUnit` and
optional third operands of `LoadInteger`/`LoadFloat` accept unit tags `#none`,
`#degree`, `#m`/`#meter`, `#s`/`#second`.
`CreateSeries` accepts `#fibonacci` or `#factorial`. `HasPattern`/`TakePattern`
accept a pattern tag (`#countAny`, `#countFace`, `#fullHouse`, `#straight`), an
Int16 count, and a face register only for `#countFace`. `SplitText` takes a
source and optionally a separator; omitting it selects whitespace splitting.

Invalid assembly is reported as `compile.invalidAssembly`; malformed source
continues to use `parse.syntax`. Ordinary symbol/signature diagnostics remain
applicable to symbolic operands.
