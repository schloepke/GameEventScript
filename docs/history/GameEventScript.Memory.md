<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# GameEventScript Memory

## Portable text and Unicode contract

Text/source portability is now normative in `PortableTextSemantics.md`. Source
files are strict UTF-8 with an optional stripped initial BOM. Names and tags use
explicit ASCII grammars; no platform Unicode classification defines the
language. Runtime text preserves exact, unnormalized Unicode scalar sequences,
and count/index/iteration/list conversion use Unicode Scalars with 1-based
indexing. Scalar-ordinal ordering replaces UTF-16 ordering. Compiler columns are
1-based scalar columns while `.gesb` SourceMaps remain UTF-8-byte-based. Linked
programs cache string scalar counts outside the serializable Program.

## Purpose

This document captures the current architectural state of GameEventScript in the
standalone `StepH.GameEventScript` module. Future changes should start from
this file and treat this module as the source of truth.

## Module Boundary

- Production project: `StepH-GameEventScript/StepH-GameEventScript.csproj`
- Test project: `StepH-GameEventScript-Tests/StepH-GameEventScript-Tests.csproj`
- Assembly name: `StepH.GameEventScript`
- Root namespace: `StepH.GameEventScript`
- Target framework: `netstandard2.1`
- Main public docs:
  - `StepH-GameEventScript/GameEventScript.md`
  - `StepH-GameEventScript/BytecodeSpec.md`
  - `StepH-GameEventScript/GameEventScript.bnf`

Legacy pre-rename documentation artifacts were removed. Do not recreate or
reference pre-rename documentation artifacts. The canonical names are
`GameEventScript.md`, `GameEventScript.Memory.md`, and `GameEventScript.bnf`.

## Namespace Predicate

All GameEventScript production namespaces and using directives should resolve inside
`StepH.GameEventScript`.

Expected namespace shape:

- `StepH.GameEventScript`
- `StepH.GameEventScript.Api`
- `StepH.GameEventScript.Compiler`
- `StepH.GameEventScript.Runtime`
- `StepH.GameEventScript.BytecodeExecutor`
- `StepH.GameEventScript.Types`

## Current Architecture

GameEventScript is now a standalone language/runtime module backed by the
register-oriented bytecode executor.

Pipeline:

```text
GameEventScriptBuilder parses sources -> validation/optimization -> GameEventScriptProgram -> Host.Load dynamic linking -> GameEventScriptInstance -> serial Host runtime
```

- `GameEventScriptProgram` is the immutable, portable compiler artifact. It has
  no VM, random, queue, delegates, or host binding and may be reused by many
  hosts.
- `GameEventScriptHost` is the autonomous serial execution unit. It may contain
  native handlers only or any number of additively loaded programs.
- `Load(program, priority)` dynamically links host-specific extensions/types,
  registers direct script handler entries, queues that instance's initialization
  once, and returns a detachable `GameEventScriptInstance`.
- The host creates one `GameEventScriptContext` and at most one `GesVmState`.
  The VM is a stateless executor; the state is rebound for each script handler
  and fully reset afterwards.
- `Receive` and `Emit` are local. `Publish` is local first and then calls one
  optional synchronous outbound sink.
- `ExecuteFrame` and `RunToCompletion` are synchronous caller-thread pumps.
  Core owns no lock, thread, task, dispatcher, or session.
- Optional C# synchronization/automatic pumping lives in `CSharpBridge`.

The old AST interpreter, experimental VM paths, `IGameEventScriptModule`,
module-owned VM, `GameEventScriptSession`, isolated run objects, and automatic
core dispatcher are removed without compatibility layers.

See `HostArchitecture.md` for the normative responsibility split and portable
host state machine.

Lexer, parser, tokens, and AST nodes are internal compiler implementation
details. AST JSON serialization is not production API. It is a test/debug helper
only and belongs in the test project when optimizer or parser dumps are needed.

## Public API Shape

Important public entry points:

- `GameEventScriptManager.CreateBuilder(...)`
- `GameEventScriptManager.Compile(...)`
- `GameEventScriptBuilder`
- `GameEventScriptProgram`
- `GameEventScriptHost.CreateBuilder()`
- `GameEventScriptHost.Load(...)`
- `GameEventScriptHost.Receive(...)`
- `GameEventScriptHost.ExecuteFrame(...)`
- `GameEventScriptHost.RunToCompletion()`
- `GameEventScriptInstance`
- `GameEventScriptSubscription`
- `GameEventScriptContext`
- `IGameEventScriptPublishSink`
- `GameEventScriptMessage.Create(...)`
- `GameEventScriptMessageSignature.Create(...)`
- `GesValue`

`GameEventScriptManager.Compile(...)` accepts
`GameEventScriptCompileOptions`.

There is no module or VM compile convenience API. `Compile(...)` always returns
the reusable program artifact; only a host links it for execution.

## Binary Bytecode Boundary

`GameEventScriptProgram` is the public in-memory bytecode artifact. It is
deliberately VM-free and portable:

- no runtime-specific VM implementation types
- no AST nodes
- no AST or mutable compiler state; optional sources are plain UTF-8 segment data
- no runtime handler/executable plans
- no runtime value constants
- no object-shaped constant pool

The artifact contains neutral, serializable segment data:

- ProgramMetadataSegment and StringConstantSegment
- compact UInt16IndexListSegment
- BindingSegment for handlers, records, callables, outbound messages, extension calls,
  and external types
- one linear CodeSegment
- optional DebugSymbolsSegment, SourceMapSegment, SourceArchiveSegment, and
  BuildMetadataSegment
- per-message-handler `RequiredRegisterCount` and `RequiredCallStackDepth`
- program maxima for both resource requirements

The public bytecode boundary is now the linear, register-shaped surface:
the instruction table is a global code segment, `GameEventScriptBytecodeInstruction` is
a 16-byte value type with typed access fields such as `DestinationRegister`,
`MessageDestination`, `XRegister`, `YRegister`, `ConditionRegister`, `TargetAddress`,
`EntryAddress`, `StringIndex`, `ListIndex`, `SecondaryListIndex`, `TypeOperand`,
`Count`, `AU`/`AS` through `DU`/`DS`, `Payload`, `I64`, and `F64`, and
handlers/callables/type fields expose entry addresses into that segment.
Structured metadata that would make instructions bulky lives in public side
tables addressed through opcode-specific use of the typed operand fields. Local
calls and predicate calls use direct callable entry addresses; extension calls,
collection/message builders, type constructors, maps, pipeline helpers, and
handler binding read their names, register lists, helper addresses, or immediate
counts directly through instruction operands and `StringPool`/`UShortListPool`.
The old nested expression,
statement, pipeline, generated-collection, guarded-choice, dice, and
object-match program graph has been removed from the compile/runtime path and
from the bytecode model.

`GameEventScriptProgram` carries `ModuleName` from the parsed source. For
multiple source modules in one builder, the binary name is the source-order join
of module names with `+`.

`OutboundMessageSignatures` carries statically shaped `emit`/`publish` message
signatures directly as message name plus ordered argument names. The compact
`.gesb` binary emits these as `OutboundMessage` bind entries, so loaders can
prebuild outbound message lookup tables and validate outbound contracts without
scanning `EmitMessage*` or `PublishMessage*` instructions. Static emit/publish
opcodes store the outbound message bind id in `MessageDestination`; only
argument-register and tag-register lists remain in `UShortListPool`.

`GameEventScriptProgram` is the parsed immutable representation of `.gesb` V1.
The file has a fixed 16-byte header and sequential 12-byte section headers, but
the header `FileSize` is writer output rather than program state. The normative
format, including required and optional segment payloads, is documented in
`GesbFormatV1.md`. Statically shaped emitted/published messages are represented
as `OutboundMessage` bind entries.

`GameEventScriptProgram` is the portable in-memory representation of that
future binary, not a host-linked runtime object. Every value it contains must
round-trip losslessly through the language-neutral `.gesb` format. Host
bindings, registries, delegates, reflection objects, derived runtime caches,
and VM state must remain outside the program; host-specific linking belongs in
`GesLinkedProgram` and happens during `GameEventScriptHost.Load(...)`.

The portable program also carries static VM resource metadata. Each message
handler bind stores `RequiredRegisterCount` and `RequiredCallStackDepth`; the
program stores the maximum of each field across its handlers. Register counts
include all simultaneously retained caller/callee frames and staged values.
Call depth counts nested calls below the root handler. Because recursive call
graphs are forbidden, both values are finite and can be calculated after final
physical register allocation. `Host.Load(...)` validates these values against
`MaxRegisterValues` and `MaxCallDepth` before registering the instance, then
uses the register maximum to pre-warm the reusable VM state.

The `.gesb` V1 reader and canonical writer are implemented. Changes to opcode
numbers or required segment payloads now require an explicit format-version
decision and matching cross-language conformance updates.

External types have separate compile-time and runtime boundaries. The compiler
uses `IGameEventScriptExternalTypeCatalog`, which contains declarative type data
only. `Host.Load(...)` uses `IGameEventScriptExternalTypeRegistry` only to link
constructor imports. Runtime instances implement
`IGameEventScriptExternalValue`; arbitrary CLR objects, reflection readers, and
conversion delegates remain in `CSharpBridge`. The portable conformance runner
uses a manual external type implementation rather than reflected C# classes.

Bind kinds in the `0x10` range export message handlers/functions/predicates;
bind kinds in the `0x20` range import extension calls and external types. Bind
entries carry string-pool indexes for name and argument names plus a global code
entry address for exports. Import binds leave the entry address at `0xFFFF` because
they are linked by table index to host implementations.

The bytecode executor loads `GameEventScriptProgram`, builds handler descriptors
from bind entries, and executes from global code entry addresses using register
frames plus a VM-owned call-frame stack rather than a script call path on the C#
stack.

Manual Fiber/stepping execution now starts from the same handler entry address
and uses the same linear instruction stepper as synchronous handler execution.
Its resumable state is explicit: program counter, end address, argument source,
VM call frames, frame registers, scope marks, VM-internal iterators, and random
scope state. There is no statement-program or
expression-program compatibility Fiber fallback.

Pipelines now lower mostly to explicit linear iterator/builder loops. `:sum`
and `:average` are emitted as normal bytecode loops so step budgets stay fair;
`Count` remains a direct terminal. Weighted choice is emitted as a normal
bytecode loop that materializes candidate and weight lists before `OneWeighted`
or `TakeWeighted`. Selector transforms no longer use lazy helper streams:
the former stream transform opcodes and TryEvaluate helpers
were removed in favor of explicit bytecode loops.
Generated collections and guarded choices use normal
linear iterator/builder and jump instructions.

Linear code generation for handlers, callables, type-field helpers, and
high-level helper entries emits directly from the source AST into `Code` plus
side tables. Runtime execution has no compatibility interpreter or high-level
`*Program` execution fallback: pipelines, dice/object matching, publish
operations, seeded-random scopes, guarded choices, and generated collections
execute as normal linear instructions or fixed VM-internal iterator terminals.
The compiled artifact no longer carries stack-depth metadata;
the projection fast path derives its small local register requirement from the
linear instruction sequence.

Literal constants are encoded by typed load opcodes, not by runtime value
instances. This is important because script value
equality is semantic and may treat different runtime kinds as equal, while
bytecode loads must preserve their concrete kind. Examples:

- `1`
- `1.0`
- `:percentage 30%`

The bytecode executor decodes those inline payloads when executing `LoadInteger`,
`LoadFloat`, `LoadPercentage`, `LoadText`, `LoadTag`, `LoadHandler`, `LoadTrue`,
`LoadFalse`, or `LoadNothing`. Percentages are a dedicated value kind loaded by
`LoadPercentage`; they are not encoded as numeric units.

`UnitAndFlags` is reserved as `bits 0..4 = UnitId` and `bits 5..7 = reserved
flags` in the target binary shape. Current runtime units are degree, meter, and
second. `Nothing` uses `UnitNone`; the invalid unit sentinel is `UnitInvalid`
for VM/register `nothing` semantics. Percentage is a separate kind. The bytecode target reserves room for likely game units such as
speed, acceleration, mass, force, energy, power, voltage, current, frequency,
bandwidth, and Kelvin temperature.

`GameEventScriptProgramDumper.Dump(...)` is the public debug disassembler for
the program model. It has no separate source-input API: it consumes embedded
SourceMap and SourceArchive data, annotates code with source lines, and marks
unmapped instructions as compiler-generated. Its deterministic assembler-like
`.gesa` text is not the binary or JSON wire format. Compilation generates all
three optional debug segments by default. Production or size-sensitive builds
must explicitly select `GameEventScriptDebugInfoOptions.None`; individual debug
segments can also be selected independently. When DebugSymbols are present,
instruction operands retain their physical register and append the active
source symbol, for example `r1(result)`. Compiler temporaries remain unnamed.
Embedded source archives are emitted as `.segment source "name.ges"` blocks
whose source content continues until the next `.segment` directive. Mapped
instruction lines use `.source-line "name.ges" 4 | source` and are preceded by
a blank line; assembler TextMate grammars inject the normal GameEventScript
source grammar into both forms. Free `.region "Name"` /
`.region-end "Name"` blocks wrap source, text, list, binding, and code segments
with visible comment separators and blank lines. The directives use a comment
highlighting scope and provide standard TextMate folding markers without
changing program or runtime semantics.

## Runtime Model

The runtime is message/context driven. Portable native handlers use:

```csharp
public interface IGameEventScriptNativeMessageHandler
{
    void Handle(GameEventScriptMessage message, GameEventScriptContext context);
}
```

`Action<GameEventScriptMessage, GameEventScriptContext>` overloads are C#
convenience adapters and live exclusively in `CSharpBridge`.

`GameEventScriptContext` is created once by its host and contains:

- `Random` (`GameEventScriptRandomGenerator`)
- `ExtensionRegistry`
- `RuntimeLimits`
- `Emit(...)` for local follow-up messages
- `Publish(...)` for local plus outbound messages
- read-only host queue/idle information

Script execution should be observed through emitted/published messages and the
host `IGameEventScriptRuntimeObserver`. Pump calls return allocation-free
`GameEventScriptExecutionResult` values with state and counters, not variable
snapshots.

## Host Model

`GameEventScriptHost` owns loaded handlers, subscriptions, external bindings,
dispatch indexes, FIFO queue, random stream, context, and reusable VM state.
There is no session layer.

Host behavior:

- `Receive(...)` captures subscriptions and queues external input locally.
- `ExecuteFrame(opcodeBudget)` pumps synchronously and may pause script bytecode.
- `RunToCompletion()` pumps synchronously until idle or a runtime limit.
- `Emit(...)` queues locally. `Publish(...)` queues locally first and then calls
  one configured `IGameEventScriptPublishSink`.
- `Load(...)` is additive and returns `GameEventScriptInstance`.
- `Subscribe(...)` returns `GameEventScriptSubscription`.
- Program instances and native subscriptions receive stable, non-reused
  host-local registration IDs. Their lifecycle handles call back into the host
  with that ID and store no detach/unsubscribe closures.
- `Detach()` and `Unsubscribe()` are idempotent. Already queued snapshots still
  run; changes affect messages enqueued later.
- Dispatch matches normal handlers by exact `SignatureId`.
- `on Message as message` registers a message-name handler that matches by
  message name and tag filters, then receives the original `:message` directly.
- The portable binary encodes normal handlers as `MessageHandler` bind entries
  and message-name handlers as `MessageNameHandler` bind entries. The synthetic
  `Message(message)` signature remains metadata for the handler frame, not the
  message-name dispatch key.
- `on initialization { ... }` is queued once per loaded instance at load time,
  after older queued messages and before later received messages.
- If no normal subscription can be queued after signature and tag filtering, the
  reserved lowercase system endpoint `on undeliverable as message` is attempted.
- Dispatch order is higher priority first, then registration order.
- There is no hard script-before-external ordering rule.
- External subscriber exceptions are swallowed to keep runtime dispatch lenient.

The host processes one logical message and all its matching handlers before the
next message. One running GES handler blocks every other handler; native handlers
are atomic. The scheduler frame budget is separate from safety limits, which
reset for every handler.

`WithRuntimeObserver(...)` is the unified host observer. It reports script/host
output via `MessageEmitted(...)` and `MessagePublished(...)`, and also reports
dispatch lifecycle and runtime-limit events. The external input message that
starts a host run is not an output message.

### Dispatch Snapshots and Synchronization

The portable host is deliberately threadless and has no lock. Its caller must
serialize access. The optional C# auto-runner supplies C# synchronization.

Registration model:

- `Load(...)` and `Subscribe(...)` validate inputs first.
- Each signature and each message-name dispatch name maps to an immutable
  subscription array snapshot.
- Adding a handler creates and publishes a new ordered array for that signature
  or message name.

Dispatch model:

- Enqueue captures the current exact/name arrays without building an invocation
  list.
- Exact-signature and message-name subscriptions are merged by priority and
  registration order before queueing.
- Undeliverable dispatch uses the original message tags for endpoint matching
  and does not recursively fallback for `undeliverable` itself.
- A handler may call `Subscribe(...)`; the new subscription applies to future
  dispatches only.
- A handler collection loaded during dispatch is visible as a whole to later
  dispatches, never half-registered.

### Automatic C# Runner

Automatic execution is not part of Core. `CSharpBridge` provides
`GameEventScriptCSharpHostRunner` and a shared internal dispatcher.

Important behavior:

- `host.RunAutomatically()` wraps an existing host.
- Concurrent calls through the runner are serialized per host.
- The shared worker means automatic hosts do not own a thread each.
- A host schedules at most one automatic dispatch job at a time.
- Core execution remains the same synchronous `RunToCompletion()` call.

Example:

```csharp
var host = GameEventScriptHost.CreateBuilder()
    .Build();
host.Load(program);
using var runner = host.RunAutomatically();
runner.Receive(message);
```

Swift, Kotlin, C++, and Unity choose their own actor, event-loop, scheduler, or
main-thread wrapper around Core.

### Current Production Readiness

The host model is suitable for controlled internal production candidate usage
when:

- scripts are compiled/loaded during setup
- synchronization is supplied at the integration boundary
- queue limits are configured for the game mode
- runtime observers/telemetry observe dropped messages and runtime limits

Remaining hardening includes broader allocation benchmarks and implementation of
equivalent wrappers in the other monorepo languages.

## Message and Argument Semantics

Message arguments are ordered positions with optional labels.

The portable API represents each position as an immutable
`GameEventScriptMessageArgument` containing `Name` plus `Value`.
`GameEventScriptMessageArguments.Create` accepts an ordered list of these pairs;
there is no Core dictionary or tuple factory. C# tuple helpers and dictionary
binding against an already known signature live in `CSharpBridge`.

Signature predicates:

- `SignatureId` is `MessageName(label1,label2,...)`.
- Unlabeled argument positions use `_`.
- Labels are positional.
- Labels are not sorted.
- Argument reordering changes the signature.
- Message name is part of the signature.
- Duplicate named labels are rejected after normalization. Multiple `_` labels
  are valid and remain positional.

Examples:

```text
Travel(from,to)
Travel(to,from)
Point(_,_)
```

These are distinct signatures. Missing, extra, reordered, or mismatched labels
produce no subscriber match rather than a runtime exception.

Portable JSON conformance encodes message arguments as an array of `{name,
value}` objects in signature order. This applies uniformly to input, expected
local/outbound output, nested `:message` values, and native conformance emits.

The same labeled-positional argument predicates are used by:

- messages
- handlers
- handler binding
- publish
- predicates
- functions
- extension calls

The only special call form is predicate sugar:

```eventscript
unit is wounded
heading is :nav.isNorth
```

It is allowed only for unary predicates/extensions and binds the tested value to the
first parameter.

## Language and Type Decisions

Stable language decisions:

- GameEventScript is immutable and lenient.
- Invalid runtime operations usually become `nothing`, no-op, or internal
  double `NaN`, depending on whether the result is absent or mathematically
  invalid.
- For mathematical operations, use the newer `NaN`/`Nothing` metaphor anchored
  in the new `BytecodeExecutor` VM: `Nothing` means absence/unknown and
  propagates as `Nothing`; invalid mathematical combinations may produce
  internal numeric `NaN`, but the DSL-visible boundary normalizes NaN to
  `nothing`. The spelling `:nan` is an ordinary tag with no numeric meaning;
  it must not be reserved or lowered specially.
- Examples of mathematical invalidity that should produce `NaN`: incompatible
  numeric units, non-numeric operands in numeric operations, invalid
  vector/point arithmetic, and modulo/remainder by zero.
- Scalar division follows floating-point numeric semantics: division by zero can
  produce `Infinity`, `-Infinity`, or `NaN` instead of `Nothing`.
- `nothing` is the only none value. `:optional` has been removed; public helper
  APIs may expose `GesMaybe(...)`, but bytecode/runtime values do not box
  optionals.
- `nothing` is DSL keyword syntax for the absence value and absence type. Do
  not spell the source type as `:nothing`; internal VM/bytecode/value codecs may
  still name the kind `Nothing` or serialize it as `:nothing`.
- `x has value` is the presence-check syntax. Do not use the old prefix form
  `has value x`. It is not a pure `is not nothing` check: it is false for
  `nothing`, empty structural values, and invalid numeric values
  (`NaN`/`Infinity`).
- `x in values of y` is the value-containment syntax. Do not use the old
  `x value in y` wording.
- `:message` and `:handler` are first-class value types.
- `:message` exposes `name`, `signature`, `arguments`, and `tags` through
  read-only members. `signature` is the DSL member for the stable signature
  string; do not expose `signatureid` or `signatureId`.
- Message/handler values expose read-only members but are not `:map`.
- `on Message as message` is the explicit name-based way to receive the original
  message for all signatures of `Message`.
- `undeliverable` is a reserved lowercase system endpoint, not a normal message
  name.
- Predicates must compile to `:boolean` or `nothing`; explicit `as :boolean` marks intentional coercion.
- Predicate calls preserve `nothing` for missing information instead of treating it as `false`.
- Runtime true checks and false checks both fail for `nothing`; `else` runs when the condition is not true.
- Functions preserve their expression result.
- `:series` is the repeatable, index-addressed value for potentially infinite mathematical terms.
- `:iterator` is removed. The bytecode/runtime may use internal one-time iterator
  values, but there is no public DSL `:iterator` type.
- `:set` has been removed. Use lists for ordered collections and key-only maps
  for tag/string membership.
- `%` is percentage literal syntax only.
- Modulo is `mod`.
- Truncating remainder is `rem`.
- Floor division is `div`.
- `:number` is the only source-level numeric type. Runtime values remain
  internally integer or float depending on whether a finite numeric result is
  exactly integral and fits signed 64-bit.
- `is numeric`, `is integer`, and `is fractional` are keyword-like source
  checks, not `:` type tags.

Built-in value families:

- primitives: `nothing`, `:tag`, `:text`, `:boolean`
- numeric: `:number`, `:percentage`
- numeric quantities: `:quantity(degree)`/`:quantity(°)`, `:quantity(m)`, `:quantity(s)`
- vectors: `:vector`
- points: `:point`
- containers: `:series`, `:range`, `:list`, `:map`, `:dice`
- runtime values: `:message`, `:handler`
- custom records: `record :customType as { ... }`

Numeric units are stored on integer and float scalar values. Source casts and
checks use `:quantity(...)`; `:degree`, `:meter`, `:second`, and `:seconds`
remain ordinary free tags.

Vectors may carry one shared numeric unit. Per-component mixed units are invalid
and evaluate to `NaN`.

Maps are string/tag-keyed. A key-only map entry such as `[enemy:, visible:]`
means `[enemy: true, visible: true]`; duplicate keys keep the normal
last-entry-wins map semantics.

### Collection Pipelines

Pipeline selectors must preserve iterating behavior where the language semantics
allow it.

Current execution shape:

- `:range` and `:series` sources are not blindly materialized
  before iterable terminal selectors.
- Iterable/short-circuit terminal selectors include `:any`, `:all`, `:first`,
  and direct `:contains` without prefix selectors.
- Prefix selectors are applied lazily on the iterating path.
- Terminal selectors that need the full collection may materialize after the
  runtime budget has checked `MaxRangeItems`.
- Non-range list-like sources should keep the indexed hot path for selectors such
  as `:sum`, `:average`, `:count`, and edge selectors. Avoid replacing this path
  with generic `IEnumerable`/closure-heavy enumeration unless performance and
  allocations are remeasured.

Current performance notes:

- The lazy range path should avoid full materialization before selectors that
  can short-circuit.
- The indexed list-like path should stay allocation-conscious for normal
  collections.
- `BytecodeExecutor` is the performance-oriented runtime path. Its design
  explores lower allocation execution with persistent register
  storage, staged argument frames, direct opcode helper dispatch, and compact
  VM-native value handling.
- Do not tune from a single hot-path benchmark alone. The next serious
  benchmark should exercise handlers, calls, predicates, short-circuiting,
  loops, pipelines, generated collections, guarded choices, seeded random,
  maps/lookups, messages, and observer events so optimization targets average
  script behavior instead of one construction.

## Extensions and Dynamic Binding

Host extensions use:

```eventscript
:extension.function
```

Supported call forms:

```eventscript
:combat.damage value
:combat.damage(value)
:combat.max of a and b and c
:nav.shortestTurn from: current to: target
:nav.shortestTurn(from: current, to: target)
heading is :nav.isNorth
```

External references are collected at compile time and dynamically bound once
when a compiled script is loaded into a `GameEventScriptHost`.

Binding model:

- `GameEventScriptProgram.Bindings` lists real host extension refs as
  `ExtensionCall` imports.
- Standard intrinsics do not appear in `ExternalReferences`.
- `GameEventScriptDynamicLinker` binds references against
  `IGameEventScriptExtensionRegistry`.
- Missing registry or missing function is a dynamic-link/load error.
- There is no per-call runtime map lookup for bound extension functions.

Extension runtime API:

- `IGameEventScriptExtensionRegistry`
- `IGameEventScriptExtensionFunction`
- `GameEventScriptExtensionReference`
- `GesExtensionCall`
- `GesValue`

`GesValue` is the public extension boundary value and the VM value storage. It
exposes direct readers for primitives, numeric units, percentages, vectors,
points, messages, handlers, collections, and external objects without an
additional boxed wrapper.

Required standard intrinsics:

- `floor`
- `ceil`
- `truncate`
- `round half even`
- `round half up`
- `round half down`
- `wrap degree`
- `rad`
- `deg`

They are parsed like extensions but implemented by the runtime and are not
host-overridable.

## Runtime Observation

Runtime observation is host-side and goes through
`IGameEventScriptRuntimeObserver`.

Current observer events are:

- `MessageEmitted`
- `MessagePublished`
- `DispatchStarted`
- `DispatchCompleted`
- `RuntimeLimitReached`
- `RuntimeError`

`RuntimeError` carries the language-neutral diagnostic defined by
`PortableDiagnostics.md`. A failing handler is aborted/reset while later handlers
in the captured dispatch snapshot continue. The pump result uses `RuntimeError`
and carries the first handler diagnostic observed in that pump call. Optional
debug metadata is for dumps/source lookup and is not required for diagnostics.

## Runtime Limits

Runtime budgets are part of `GameEventScriptRuntimeLimits`.

Current limits:

- `MaxProcessedEventsPerRun`
- `MaxQueuedMessagesPerRun`
- `MaxExecutionSteps`
- `MaxRegisterValues`
- `MaxLoopIterations`
- `MaxCallDepth`
- `MaxRangeItems`
- `MaxGeneratedCollectionItems`
- `MaxDiceCount`
- `MaxDiceSides`

`MaxProcessedEventsPerRun` bounds one synchronous pump call so cyclic message
graphs also terminate when they contain only atomic native handlers.

When an execution budget is reached, execution stops leniently and reports
`RuntimeLimitReached` through the runtime observer when configured. When
`MaxQueuedMessagesPerRun` is reached, the newest logical message is dropped.
`Emit` returns `false`; `Publish` reports local and outbound acceptance
separately.

## Tests and Conformance

Behavioral tests live in `StepH-GameEventScript-Tests`.

Important test areas:

- portable Markdown suites, fixtures and reports under
  `StepH-GameEventScript-Tests/Conformance`
- C#-specific API, compiler, runtime, bridge and Conformance-bootstrap tests
  under `StepH-GameEventScript-Tests/Native`

Conformance tests are the portable language/runtime contract. They should test
observable compile/runtime behavior:

- published messages
- runtime observer events
- parse/validate/compile/decode/link/runtime diagnostics
- message/signature API behavior

Conformance should not test parser AST shape, lexer internals, optimizer
internals, or implementation-only storage details. Error conformance matches
stable phase plus code and optional structured context; human-readable English
messages and technical details never determine success.

Primary verification command:

```bash
dotnet test StepH-GameEventScript-Tests/StepH-GameEventScript-Tests.csproj --no-restore
```

Current verification baseline:

- `dotnet test StepH-GameEventScript-Tests/StepH-GameEventScript-Tests.csproj --no-restore`
  is the primary validation command.
- Flow tests are intentionally out of scope for GameEventScript work unless the
  user explicitly asks for them.
- `BytecodeExecutorRuntimeCostCanBeReported` is a useful local signal, but it is not a
  complete performance benchmark for the final VM.
- JSON conformance is strict against the `BytecodeExecutor` runtime. Conformance
  mismatches must fail the test run, while the failure output should keep the
  focused value diffs plus VM state, bytecode, and script dumps for debugging.

Conformance includes a runtime collection test for iterable range selectors
short-circuiting before range materialization. Keep this coverage when changing
pipeline execution.

Future idea: JSON conformance specs may later move to Markdown-based specs.
The preferred shape is human-readable Markdown with machine-readable islands:
YAML frontmatter provides suite metadata, fenced `ges` blocks contain scripts,
Markdown tables define ordered steps, and ordinary fenced `yaml` blocks use
`gesBlock: case` or `gesBlock: expect` for typed metadata and expectations.
Typical Markdown extensions such as tables, admonitions and YAML frontmatter are
acceptable. A test section should be parseable by convention, for example
`## Test: name`, followed by one `ges` script block and one or more expectation
blocks. This keeps conformance close to documentation without forcing the
expected values back into full JSON blobs.

The public API snapshot lives at
`StepH-GameEventScript-Tests/Native/ApiSurface/PublicApiSurface.approved.txt`. Update it only
when intentionally changing exported API.

## Documentation

`GameEventScript.md` should remain the human-readable language guide.

`BytecodeSpec.md` should remain the public portable bytecode design reference.

`GameEventScript.bnf` should remain the compact grammar reference.
It documents skipped trivia as a lexical convention rather than as an ordinary
parser production. `:number` is the numeric `TypeTag`; `numeric`, `integer`,
and `fractional` are only valid as `is ...` check keywords.

When changing language syntax, update in this order:

1. parser/compiler/runtime implementation
2. JSON conformance specs
3. `GameEventScript.md`
4. `GameEventScript.bnf`
5. TextMate and TextMate Classic grammars under `StepH-GameEventScript/Editors`
   when syntax highlighting should reflect the new source syntax
6. `BytecodeSpec.md` when the portable bytecode shape changes
7. this memory file when architecture or major semantics change

## Maintenance Checks

Before major GameEventScript changes, verify:

- production namespaces and using directives resolve inside
  `StepH.GameEventScript`
- generated `bin`/`obj` files are ignored and not used as source truth
- docs and tests reference `StepH.GameEventScript`

## Intentional Non-Goals

- No long-term second production runtime engine. The old `BytecodeVM` reference
  runtime has been removed; `BytecodeExecutor` is the only production VM.
- No AST interpreter as reference behavior.
- No named-argument reordering.
- No exception-based normal script control flow.
- No mutable script variables or mutable collections.
- No host result object for script execution.

## Product Integration Notes

The intended integration model is one `GameEventScriptHost` per autonomous
serial actor, bot, match controller, or other execution unit. A host may load
multiple reusable programs and native handlers. The host owns deterministic
random state, its one context, runtime queue, and at most one VM state.

Future game integration should define:

- program loading during host setup or dynamic host operation
- deterministic seed handshake for multiplayer/replay
- inbound game messages such as `TurnStarted` or `ActionRequested`
- outbound script messages such as `ApplyDamage` or `SpawnUnit`
- domain object converters to and from `GesValue`
- runtime observer forwarding into game logs/telemetry
- optional development-only hot reload

Open integration questions:

- Does GameEventScript run server-authoritative only, or also client-side for
  prediction?
- Must dispatch run on the Unity main thread, or can it run on a worker with
  explicit main-thread marshalling?
- Should queued messages survive reconnect/restart, or stay in-memory only?
- For replay, do we persist seed plus inbound events, or the full published
  trace?
- Which outbound script messages are allowed to mutate game state?
- How are script versions pinned per match and migrated across live updates?

## Portable number contract

- `PortableNumberSemantics.md` is normative for signed-64 overflow, binary64
  special values, saturation, rounding, negative division/modulo, ULP equality,
  transcendental portability, and canonical conformance JSON.
- Compiler folding and VM execution share `GameEventScriptNumber`; do not
  reintroduce host-language checked/unchecked or formatting behavior.
- Conformance `maxFloatUlps` defaults to 4096 for legacy rounded fixtures and
  cross-libm tolerance; set it to `0` for exact finite binary64 cases. Runtime
  script equality remains a separate fixed two-ULP contract.
- Verification: 1041 non-performance tests, the zero-allocation hot-path test,
  and the JSON performance reference test pass.

## Portable determinism contract

- `PortableDeterminismSemantics.md` is normative for SplitMix64/xoshiro256**,
  bounded sampling, script versus structural equality, stable heterogeneous
  ordering, map/record order, iterator/range boundaries, and host dispatch order.
- Seeded random ports must match the checked-in raw UInt64, bounded integer, and
  Binary64-bit known-answer vectors. Merely matching two instances within one
  implementation is insufficient.
- The public binary64 method is `NextFloat(firstBound, secondBound)`, not
  `NextInclusiveFloat`: it scales a `[0,1)` unit sample, but final binary64
  rounding can still produce the upper bound. Equal or NaN bounds consume no
  seeded or `FromSequence` value.
- Nested `random with` scopes advance only their own seeded generator and must
  resume the exact parent stream after the inner scope ends.
- Range iterators emit their overflow-safe precomputed length. Do not terminate
  by incrementing beyond the final value; that wraps at Int64 boundaries and may
  make no progress for large Binary64 values.
- VM-created maps arrive with unique keys. The public array-based `GesMap`
  boundary normalizes duplicate keys with last-entry-wins so that the VM map hot
  path does not allocate extra deduplication buffers.
- Verification after completing the separate Random 2.4 checklist: 1057
  non-performance tests, the zero-allocation hot-path test, and the JSON
  performance reference test pass.
- External types now use separate declarative compile catalogs and host runtime
  registries. Portable runtime values implement `IGameEventScriptExternalValue`;
  CLR objects and reflection remain in `CSharpBridge`, while JSON conformance
  uses a manual implementation. Verification: 1059 non-performance tests, the
  zero-allocation hot-path test, and the JSON performance reference test pass.
