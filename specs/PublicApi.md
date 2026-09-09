<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Portable public API specification

This document defines the normative language-neutral public API shared by the
Swift, Kotlin, C++, C#, and Unity implementations. It specifies concepts and
observable behavior, not the spelling imposed by one language.

The C# implementation is the current reference implementation. A port may use
idiomatic constructors, optionals, result types, collection views, naming, and
callback forms, but it must preserve the responsibilities, state transitions,
ordering, errors, and data described here. The public C# API snapshot is a
regression tool for the C# binding and is not the portable API definition.

## Scope and dependency direction

The portable surface consists of two conceptual modules:

```text
GameEventScript Core
  values, messages, compiler, program, binary codec, host, runtime boundaries

GameEventScript Conformance
  Markdown parser, normalized test model, runner, results, reports, received output
  -> depends on GameEventScript Core
```

Core, compiler, host, VM, and program code must not depend on Conformance.
Conformance is delivered as the separate optional
`StepH.GameEventScript.Conformance` package. Its public contract is independent
of the reference test framework and filesystem adapters.

The following documents own detailed behavior and are incorporated by reference:

- [Language](Language.md) owns source-language syntax and evaluation.
- [Host runtime](HostRuntime.md) owns dispatch, random boundaries, execution,
  and the host state machine.
- [Program model](ProgramModel.md) owns immutable program data and permitted
  representation.
- [Bytecode](Bytecode.md) and [Binary format](BinaryFormat.md) own numeric IDs,
  instructions, segments, validation, and `.gesb` encoding.
- [Diagnostics](Diagnostics.md) owns stable diagnostic phases and codes.
- [Text](Semantics/Text.md), [numbers](Semantics/Numbers.md), and
  [determinism](Semantics/Determinism.md) own value-level portable semantics.
- [Conformance Markdown](Conformance/MarkdownFormat.md) and the
  [Conformance runner](Conformance/Runner.md) own normalized test fields and
  execution details.

C# reflection attributes, delegates, locks, threads, tasks, `System.Type`, and
dictionary conveniences belong to `CSharpBridge`. They are public C# adapters,
not portable Core concepts. Other ports may provide equivalent conveniences,
but conformance code and portable application code must not require them.

## Normative conventions

### Names used by this specification

Type and operation names below are conceptual PascalCase names. They correspond
closely to the C# reference API only to make cross-checking practical. Swift,
Kotlin, and C++ may use their normal casing and overload conventions. Removing a
concept, changing its ownership, or changing its result is not an idiomatic
mapping and is therefore not permitted.

`List<T>` means an ordered immutable view unless the operation is explicitly a
builder input. `Bytes` means an ordered immutable byte sequence. `Optional<T>`
means explicit absence, not a sentinel native object. Fixed-width integer names
refer to their exact bit width.

### Nullability and invalid arguments

- Required object, string, callback, and sequence inputs must be present.
- Optional inputs are identified explicitly. An absent optional sequence has the
  same meaning as an empty sequence only where this document says so.
- Public output collections are never null. They are empty when no items exist.
- Collection elements that are not explicitly optional must not be null.
- Invalid caller arguments fail synchronously before observable state changes.
  Each language uses its idiomatic argument/precondition error.
- Script, decode, link, and runtime failures use structured GES diagnostics or
  format errors. Platform exception text and exception class names are never a
  portable comparison value.
- Portable identifiers use the grammar and normalization rules in
  [Text](Semantics/Text.md). Comparisons are ordinal and culture independent.

### Copy and reference semantics

- Immutable data objects copy mutable input sequences before returning from
  construction. Their public sequences are read-only views.
- Value records may be native value types or immutable reference objects. Their
  equality and hash behavior must follow portable content, never object identity.
- `Program`, messages, signatures, definitions, diagnostics, and normalized
  conformance models may be shared for concurrent read access.
- `Builder`, `Host`, `Context`, lifecycle handles, random generator, extension
  calls, and runner callbacks are identity-bearing reference objects.
- Returning a read-only view must not expose storage that a caller can mutate.

### Synchronization and reentrancy

- Core operations are synchronous, threadless, and internally unsynchronized.
- Immutable values and programs permit concurrent reads. A builder, host,
  context, random generator, or active call object does not permit concurrent use.
- A host is serial but not thread-affine. A later call may run on another thread
  after an embedding-provided happens-before handoff.
- Host pumping is not reentrant: a callback must not call ExecuteFrame or
  RunToCompletion on the active Host. Synchronous Receive, Load, Subscribe,
  Detach, and Unsubscribe calls from a native callback are permitted and obey
  the enqueue-time snapshot rules; they must return before the callback returns.
- Observer and publish-sink callbacks run synchronously on the pumping caller.
- Conformance parser, runner, writers, resolver, provider, and sink are also
  synchronous. They start no tasks or threads.

### Allocation contract

The allocation contract applies to the warmed runtime hot path, not compilation,
Markdown/YAML parsing, `.gesb` decoding, message construction, debug dumping, or
report creation.

After host warmup, queue dispatch, handler selection, execution-result creation,
publish-result creation, VM stepping/resume, and random scope operations must not
allocate heap objects by themselves. Value-style execution and publish results
must therefore remain representable without heap allocation. User callbacks,
extension implementations, emitted messages, and values may allocate.

The dispatch guarantee applies equally to exact-signature and message-name
handlers, with no observer or with observer callbacks that allocate nothing.
Obtaining the dispatch signature for VM entry or observer callbacks is part of
the runtime's own dispatch work and must not allocate after warmup.

## Compiler API

### CompilerBuilder

`CompilerBuilder` is a mutable, single-caller configuration and source accumulator.
It owns snapshots of all inputs needed after an operation returns.

| Operation | Contract |
| --- | --- |
| `Create()` | Returns an empty builder with debug information set to `All`, program version `0`, and no external-type catalog. |
| `AddScript(text, sourceName?)` | Adds one logical source in call order. Text is required and must be valid Unicode. The optional source name is diagnostic/debug identity, not a path to open; absence uses the implementation's stable unknown-source name. The builder retains its own source snapshot. |
| `WithDebugInfo(options)` | Replaces the debug emission mask. `None`, `DebugSymbols`, `SourceMap`, and `SourceArchive` are independent flags; `All` is their union. |
| `WithProgramVersion(version)` | Sets the exact unsigned 64-bit application program version; zero means development/unspecified. |
| `WithExternalTypeCatalog(catalog)` | Sets the immutable declarative catalog used for compile-time resolution. The catalog is required when configured and contains no runtime callbacks. |
| `Compile(options)` | Parses all added sources as one compilation, validates, optimizes, materializes an immutable Program, and validates that Program before returning it. Sources retain their AddScript order and source IDs. |

`CompileOptions` is an immutable or snapshot-at-call value containing
`DebugInfo` and `ProgramVersion`. Operation-local options override matching
builder defaults. Ports may choose either builder setters or one options object,
provided there is one unambiguous resolved configuration. Compilation never
modifies an earlier Program.

An empty builder produces the valid empty Program named `EmptyModule`.
Malformed source fails with one or more structured diagnostics.
The fixed source nesting limits in [Language](Language.md#source-nesting-limits)
apply to every compilation. Exceeding them produces the Parse diagnostic
`parse.sourceNestingExceeded` (`DiagnosticCodes.ParseSourceNestingExceeded` in
C#), including source identity and location. CompileOptions cannot disable
these limits.
`CompileException` is the C# transport carrying an ordered,
non-empty diagnostic list. A port may return a result union instead. Diagnostics
remain the portable failure value.

`GameEventScriptManager.CreateScriptBuilder`, `CreateHostBuilder`, and
`Compile(text, options)` are C# facade conveniences over the operations in this
document. They introduce no additional state or semantics.

## Program and bytecode data API

### Program

`Program` is the immutable, reusable, language-neutral parsed representation of
one `.gesb` artifact. Only Compiler and ProgramReader create Programs publicly.
There is no free-form public Program constructor.

The Program exposes:

| Property | Contract |
| --- | --- |
| `FormatVersion` | `.gesb` format version represented by the model. |
| `ModuleName`, `ProgramVersion` | Stable program identity and unsigned application version from ProgramMetadata. |
| `RequiredRegisterCount`, `RequiredCallStackDepth` | Maxima across executable handlers, verified by the validator. |
| `Metadata`, `StringConstants`, `UInt16IndexLists`, `Bindings`, `Code` | Exactly one required immutable runtime segment of each kind. |
| `DebugSymbols`, `SourceMap`, `SourceArchive`, `BuildMetadata` | Independent optional known segments. Absence is represented explicitly. |
| `OpaqueSections` | Immutable retained unknown optional sections in original relative order. |

All segment entry order, duplicate string constants, instruction bits, source
bytes, and opaque payload bytes are meaningful immutable data. Resolving a
string or index list returns a decoded value/view without mutating Program.
Out-of-range access fails as an argument/index error for direct low-level API
use; Reader, Writer, Validator, and Host must report structured format/link
failures before such access can occur for untrusted input.

### Required segment views

The portable segment model contains these data records and operations:

- `ProgramMetadataSegment`: module name, required register count, required call
  depth, and program version.
- `StringConstantSegment`: immutable UTF-8 storage and slice records;
  `Resolve(index)` strictly decodes one string and `ResolveSize(index)` returns
  its byte length.
- `UInt16IndexListSegment`: immutable `u16` storage and slice records;
  `Resolve(index)` returns one immutable indexed list view.
- `BindingSegment`: ordered bindings containing kind, ID, name index, ordered
  argument-name indices, required/excluded tag indices, optional entry address,
  and per-handler resource requirements.
- `CodeSegment`: ordered 16-byte logical instructions; `Count`/`Length` and
  indexed access do not expose mutable program storage.

`ReadOnlyArray<T>`, string slices, and `UInt16IndexList` are representation
helpers for immutable ordered access. A port may use its native immutable list
or slice type instead. `Count` and `Length` are aliases where both exist in C#;
they must report the same value.

An index into a resolved `UInt16IndexList` is zero-based relative to that list.
Only `0 <= index < Length` is valid, irrespective of storage before or after the
slice. An empty list has no valid index. Access outside these relative bounds
fails as an argument/index error before applying the storage offset.

### Instruction and numeric identifier types

`BytecodeInstruction` exposes opcode, unit/flag byte, three `u16` words, and a
`u64` payload with the semantic aliases specified by [Bytecode](Bytecode.md).
The aliases read and write the same logical words/bits. C# struct layout and
native endianness do not define the binary format.

The following enum-like types expose the fixed numeric IDs from the bytecode and
binary specifications and must reject undefined values at validation boundaries:

- `BytecodeOpCode`, `BytecodeTypeKind`, `BytecodeInstructionUnit`,
  `InstructionFlag`, `BytecodePatternKind`, and `BytecodeSeriesKind`;
- `BinaryBindKind`, `SectionType`, `SectionFlags`, and
  `DebugSymbolKind`.

`BytecodeInstructionUnits` supplies pure conversions between type names, units,
and suffixes. Parse operations return absence for an unknown valid string;
predicate operations never use locale; formatting returns canonical GES text.
`BinaryFormat` exposes the fixed V1 magic bytes, format version, file-header
size, and section-header size as named constants; their numeric values are owned
by BinaryFormat.md.

### Optional program data

- `DebugSymbolsSegment` exposes ordered `DebugSymbol` records. Each record has
  kind, physical register ID, name, and half-open code range.
- `SourceMapSegment` exposes ordered `SourceMapSource` and `SourceMapEntry`
  records. Hashes and line offsets are immutable bytes/integers; source and code
  ranges are half-open and use the units defined by the binary specification.
- `SourceArchiveSegment` exposes ordered `SourceArchiveEntry` records.
  `ResolveText()` strictly decodes retained UTF-8 content.
- `BuildMetadataSegment` exposes required non-empty compiler ID and compiler
  version when the optional segment exists. Runtime decisions must ignore them.
- `OpaqueSection` exposes type, flags, version, original ordinal, and immutable
  raw payload. Only unknown optional sections may be represented this way.

The public constructors of individual segment records are data helpers. They
defensively copy their inputs but cannot assemble a Program and do not bypass
Program validation.

## Program codec, validation, and dump API

### ProgramReader

`ProgramReader.Read(bytes, options) -> Program` consumes caller-provided bytes
only. It performs overflow-safe bounded structural decoding followed by complete
semantic validation. It retains no caller buffer. Success returns a fully valid
immutable Program; failure returns/throws `ProgramFormatError` with a stable
`ProgramFormatErrorCode` and optional byte offset, section type, and entry index.

`ProgramReadOptions` contains a required `ProgramReadLimits` snapshot and one
retention mode:

- `PreserveAll`: parse all supported known sections and retain unknown optional
  sections as opaque data;
- `PreserveKnown`: parse supported known sections and discard unknown optional
  sections;
- `RuntimeOnly`: materialize required runtime sections only.

`ProgramReadLimits` exposes maxima for file bytes, section count, source archive
bytes, retained opaque bytes, instructions, bindings, strings, and index lists.
All values are validated before allocation. The shared Default object is
read-only by convention; a mutable language mapping must snapshot settings at
the start of Read.

### ProgramWriter

| Operation | Contract |
| --- | --- |
| `GetEncodedSize(program)` | Fully validates Program and returns the exact canonical byte count with checked arithmetic. |
| `Write(program, destination)` | Fully validates before modifying destination, requires sufficient caller storage, writes canonical Little Endian `.gesb`, and returns bytes written. |
| `ToArray(program)` | Allocates exactly the required output byte array and delegates to canonical Write semantics. |

The writer reconstructs every known section from structured data. Only opaque
payloads are copied raw. It performs no file or stream I/O.

### ProgramValidator and ProgramDumper

`ProgramValidator.Validate(program)` is deterministic, side-effect free, and
performs the same complete semantic checks used by Reader, Writer, and Host.Load.
It does not link executable callbacks. Failure uses ProgramFormatError.

`ProgramDumper.Dump(program, includeInstructionAddresses = false)` returns
canonical `.gesa` text for a valid Program. It uses embedded debug/source
segments when available, performs no file I/O, and never changes Program. The
dumper is not a trust boundary and does not replace explicit Program validation.
Dump allocation is outside the runtime hot path.

## Message and value API

### MessageSignature

`MessageSignature` is immutable content identified by normalized message name
plus ordered parameter labels. Tags and values are not part of a signature.

| Operation | Contract |
| --- | --- |
| `Create(name, parameters)` | Normalizes and copies the name and ordered labels. An absent parameter sequence means empty. Repeated labels remain part of the signature; constructing actual MessageArguments later rejects duplicate named labels while repeated `_` labels remain permitted. |
| `NormalizeMessageName(name)` | Trims portable ASCII edge whitespace and validates the message-name grammar; absent input yields empty only for this normalization helper. |
| `NormalizeParameterName(name)` | Trims portable ASCII edge whitespace; absent/empty becomes `_`; otherwise validates a portable identifier. |
| `CreateSignatureId(name, parameters)` | Returns canonical `Name(label,...)` from normalized content. |
| `Matches(message)` | True exactly when normalized name and complete signature ID are equal ordinally. Message tags and values are ignored. |
| `CreateMessage(values)` | Binds values in parameter order. Returns absence when arity does not match or the signature is Empty. |
| `WithArguments(values...)` | Same binding, but invalid arity is a synchronous argument error. |

Signature equality and hashing use canonical signature content. `Empty` is a
read-only sentinel and cannot create a message.
Public parameter views cannot change the signature or any message created from
it. Message factories snapshot caller-owned value collections; later changes to
those collections cannot change an existing message.

### Message and arguments

`MessageArgument` is an immutable name/value pair. `MessageArguments` is an
immutable ordered collection exposing Count, indexed argument access, indexed
name/value access, and lookup by normalized named label. Lookup returns absence
when not found. It never converts the collection to unordered semantics.

`Message.Create(name, arguments?, tags?)` validates and copies the logical
inputs. Absent arguments/tags mean empty. `WithTags(tags)` returns a new Message
whose normalized tag set is the stable merge of existing and supplied tags; it does not
mutate the original. `HasTag(tag)` uses normalized ordinal matching.
Public tag and argument-label views cannot modify a message, its signature, or
any other message sharing its immutable data. This also applies after a message
has been enqueued: its delivery tags and argument labels remain unchanged.

Message equality compares signature, argument values in order, and normalized
tags in order. Hashing must preserve the equal-values-have-equal-hash invariant.
Human formatting is diagnostic only and must not be parsed as transport.

### Value

`Value` is the compact tagged GES runtime value. A language may map it to a
struct or another compact representation. Public factory operations create:

- Nothing and Boolean;
- signed-64 Integer, Binary64 Float, Percentage, and numeric values with a unit;
- Text and Tag;
- Vector and Point;
- IntegerRange and FloatRange;
- Dice from ordered roll values;
- immutable List and Map;
- Handler signature and Message;
- immutable Record by type name and ordered fields;
- External values through the external-type boundary.

Factory operations snapshot mutable arrays/maps before returning. The portable
ordered map/record model must not derive order from platform dictionary
iteration. C# dictionary factories belong to CSharpBridge and delegate to the
portable ordered factories; their canonical ordering is governed by
[Determinism](Semantics/Determinism.md).

`ValueKind`, `ValueUnit`, `HasValue`, `IsNothing`, `IsNumeric`, `HasUnit`,
`Length`, `CustomTypeName`, coordinate/range/message/handler views, and typed
read operations expose the active value. A typed `As...` operation on an
incompatible kind follows the public value conversion contract and must never
expose uninitialized native storage. `AsList` and `AsMap` return immutable
views; `AsDice` returns an independent ordered copy in the C# mapping.

Value equality is the strict structural equality in [Determinism](Semantics/Determinism.md),
including numeric rules in [Numbers](Semantics/Numbers.md). Hashing must be
consistent with this equality. `ToString` is nonnormative diagnostic formatting.

`ValueSlice`, `ValueArguments`, and `ValueMap` are read-only runtime views.
They expose Length, indexed values, kind/unit checks, typed reads, and map key
lookup. Indexes are zero-based at the API boundary. Invalid indexes fail as
argument/index errors; VM opcodes must instead evaluate according to Language
semantics. A borrowed read-only value reference must not outlive the callback or
operation that supplied its backing storage.

`IntegerRange` and `FloatRange` are immutable triples `(from, to, step)`; they do
not enumerate by themselves. Range termination and overflow behavior belong to
the deterministic language semantics.

## Host construction and lifecycle API

### HostBuilder

`HostBuilder` is mutable and single-caller. Every `Build()` creates an independent
Host and private random generator. Reusing a builder must not cause hosts to
share queues, random state, runtime budgets, dispatch state, or VM state.
Immutable configuration and configured registry, sink, or observer callback
identities may be shared; the embedding then owns any synchronization and
reentrancy requirements of those callbacks.

| Operation | Contract |
| --- | --- |
| `WithRuntimeLimits(limits)` | Replaces the required immutable limits configuration. A language with mutable option objects must snapshot all scalar values at Build. |
| `WithRandomSeed(seed)` | Selects the complete signed-64 deterministic seed and clears a prior sequence configuration. |
| `WithRandomSequence(values, fallbackSeed?)` | Copies a finite Binary64 start sequence, optionally selects a deterministic fallback seed, and clears a prior direct seed. Exhaustion continues with the private fallback generator. |
| `WithRegistry(registry)` | Selects the extension registry. Absence is represented by an empty registry, not null. |
| `WithExternalTypeRegistry(registry)` | Selects the runtime external-constructor registry. |
| `WithPublishSink(sink)` | Selects the single outbound sink. Not calling it means no sink. |
| `WithRuntimeObserver(observer)` | Selects the single observer. Not calling it means no observer. |
| `Build()` | Validates configuration and returns a new idle, native-capable Host with no loaded Program or subscription. |

### RuntimeLimits

`RuntimeLimits` is a configuration snapshot with the following signed integer
settings: processed events per RunToCompletion call, queued logical
messages, execution steps per handler, register values, loop iterations, call
depth, random scope depth, range items, generated collection items, dice count,
and dice sides. The exact defaults are part of the public versioned API.

| Setting | Default |
| --- | ---: |
| `MaxProcessedEventsPerRun` | 64 |
| `MaxQueuedMessagesPerRun` | 0 (unlimited) |
| `MaxExecutionSteps` | 100,000 |
| `MaxRegisterValues` | 512 |
| `MaxLoopIterations` | 100,000 |
| `MaxCallDepth` | 64 |
| `MaxRandomScopeDepth` | 16 |
| `MaxRangeItems` | 10,000 |
| `MaxGeneratedCollectionItems` | 10,000 |
| `MaxDiceCount` | 1,000 |
| `MaxDiceSides` | 1,000,000 |

A nonpositive value disables the processed-event, queue, execution-step, loop,
range, generated-item, dice-count, or dice-side limit. A nonpositive register
limit selects the portable default capacity of 512. Call depth is clamped to the
encoded `u16` range for linking; zero permits only root handlers. Random scope
depth alone must be in `0...65535` and is validated at Build. Host.Load checks
static register/call requirements before registration. Dynamic limit exhaustion
is a structured runtime-limit result/observation, not a thrown script exception.
The counting rules and boundary behavior for loops and incrementally generated
collections are defined in [Host runtime](HostRuntime.md#iterator-and-generated-collection-limits).

### Host

Host owns one serial execution domain. The complete state machine and snapshot
rules are in [Host runtime](HostRuntime.md).

| Operation | Contract |
| --- | --- |
| `CreateBuilder()` | Returns a new HostBuilder. |
| `Load(program, priority = 0)` | Validates, checks resource limits, links imports, registers all script handlers additively, enqueues one initialization snapshot, and returns an Instance. It is all-or-nothing. |
| `Subscribe(signature, handler, requiredTags?, excludedTags?, priority = 0)` | Adds one exact-signature native subscription and returns a Subscription. Inputs are copied/retained as immutable registration data. |
| `SubscribeMessageName(name, handler, requiredTags?, excludedTags?, priority = 0)` | Adds a native subscription matching every signature of that normalized message name. |
| `Receive(message)` | Captures current matching subscriptions and attempts to enqueue the logical message locally. Returns whether accepted. It never pumps and never publishes outbound. |
| `ExecuteFrame(opcodeBudget)` | Synchronously pumps on the caller until the scheduler budget pauses script execution, the Host becomes idle, or a runtime limit/error ends the call. Budget must be positive. |
| `RunToCompletion()` | Synchronously pumps until idle or a runtime limit/error terminates this pump call. It creates no worker thread. |
| `IsIdle` | True only when no active message/handler and no queued logical message exists. |
| `PendingMessageCount` | Number of queued logical messages according to HostRuntime; it never counts handler invocations. |

Equal priority uses stable registration order. A logical message completes all
captured handlers before the next logical message. Load/Subscribe/Detach/
Unsubscribe changes affect snapshots captured afterward only.
`Load` rejects insufficient initialization-queue capacity with
`link.initializationQueueFull`. The full atomicity and retry contract is defined
in [Host runtime](HostRuntime.md#program-load-atomicity).

`Instance` is a Host-owned lifecycle handle for one linked Program. `Program`
is the original immutable object. `Detach()` returns true exactly once when it
transitions attached to detached; later calls return false. `IsAttached` reports
that transition. Detach never cancels handlers already captured by an enqueued
message and never attaches to another Host.

`Subscription` is the equivalent native-handler lifecycle handle.
`Unsubscribe()` returns true exactly once; later calls return false.
`IsSubscribed` reports that transition. Both handle types retain host identity
and a stable non-reused host-local registration ID, not a closure.

## Context, messaging, and execution results

### Context

Exactly one Context exists per Host. The Host passes the same identity to native
handlers and extensions. Context exposes `Random`, `RuntimeLimits`, the extension
registry, `IsIdle`, and `PendingMessageCount` as borrowed Host views.

`Emit(message)` attempts local enqueue and returns acceptance. Convenience
overloads construct a message from name and ordered arguments. `Publish(message)`
first attempts the identical local enqueue, then invokes the configured outbound
sink once unless a random fault gate forbids delivery. It returns PublishResult.

Context has no independent queue, VM, or scheduler. It must not be used
concurrently or retained for asynchronous calls after its callback. Emit and
Publish do not recursively dispatch the new message.

### PublishSink and PublishResult

`PublishSink.Publish(message) -> bool` is a synchronous, preferably nonblocking
handoff. `true` means accepted by the next layer, not remotely delivered.

`PublishResult` is a value with:

- `LocalAccepted`: local queue accepted the message;
- `OutboundAttempted`: a configured sink was invoked;
- `OutboundAccepted`: that invocation returned true;
- `AnyAccepted`: derived `LocalAccepted || OutboundAccepted`.

No sink produces attempted=false/accepted=false. Sink rejection is not an error.
A sink exception becomes a runtime diagnostic and outbound rejection; it never
rolls back successful local enqueue and does not escape the portable Host API.

### NativeMessageHandler and RuntimeObserver

`NativeMessageHandler.Handle(message, context)` is one atomic trusted callback.
It may Emit, Publish, and use Context.Random synchronously. An implementation
failure becomes `runtime.nativeHandlerFailure`; it aborts that handler, restores
runtime boundaries, and does not corrupt Host state.

RuntimeObserver receives synchronous callbacks in exact dispatch order:

- `MessageEmitted(message, accepted)` after each Emit attempt;
- `MessagePublished(message, result)` after local/outbound Publish processing;
- `DispatchStarted(message, dispatchSignatureId)` immediately before a handler;
- `DispatchCompleted(message, dispatchSignatureId)` when the handler boundary closes, including cleanup after a handler failure or limit;
- `RuntimeLimitReached(name, detail, limit)` for a limit stop;
- `RuntimeError(diagnostic)` for a runtime diagnostic.

An observer is observational only and has no return value. Observer callbacks
must not throw. Throwing is an embedding-contract violation, is not converted to
a GES diagnostic, and lies outside Host state/recovery guarantees. This differs
deliberately from publish-sink exceptions, which the Host contains as specified
above.

### ExecutionResult

`ExecutionResult` is an allocation-free value containing state, opcodes executed,
logical messages processed, messages emitted, messages published, and an optional
first diagnostic. Counters cover only the current pump call.

The states are:

- `Paused`: a script handler remains resumable because ExecuteFrame exhausted
  its scheduler opcode budget;
- `Completed`: the pump reached idle without a stopping limit/error;
- `RuntimeLimitReached`: a configured safety limit stopped the pump;
- `RuntimeError`: at least one runtime diagnostic occurred during the pump; the
  Host still completes the remaining captured dispatch work unless another
  stopping limit is reached.

The Host remains reusable after a limit or runtime error as specified by
HostRuntime. Native callbacks are atomic and do not consume scheduler opcodes.

## Random generator API

`RandomGenerator` is mutable identity-owned state. `Create()` chooses a
nondeterministic seed; `FromSeed(Int64)` uses the exact portable seed;
`FromSequence(values)` copies a finite-length Binary64 sequence and continues
with a private generator after exhaustion. The 32-bit seed overload is an exact widening
convenience.

`NextInclusiveInteger(first, second)` samples uniformly over the inclusive
signed-64 range after ordering the bounds. `NextFloat(first, second)` uses a
`[0,1)` source and Binary64 scaling; final rounding may produce the upper bound.
Equal and NaN bounds consume no stream value. Exact transitions and known-answer
vectors are in [Determinism](Semantics/Determinism.md).

`Push(seed)` starts a child scope from that seed. `Push()` clones the current
stream into a child scope. `Pop()` restores the parent stream. The Boolean result
reports whether the requested transition was accepted. Host-owned generators
add private runtime boundary and fault-gate behavior from HostRuntime. Runtime
markers are deliberately not public API; only Host can fence callbacks.

A RandomGenerator is not thread-safe. A Host never accepts a live generator from
outside, preventing cross-Host random-state interference.

## Extension API

### ExtensionRegistry and references

`ExtensionReference` is immutable normalized `(extensionName, functionName,
orderedArgumentLabels)` data with a canonical SignatureId. It copies labels.

`ExtensionRegistry.Resolve(reference) -> Optional<ExtensionFunction>` is called
during Host.Load, not for each invocation. Absence causes a stable link failure.
The registry must resolve equal references consistently during one Load and must
not depend on culture or reflection in portable Core.

`ExtensionFunction.Invoke(call)` is a synchronous trusted callback. Host/VM
establishes a random boundary before invoking it and restores that boundary
after return, including imbalance and limit handling.

### ExtensionCall

ExtensionCall is a reusable call-scoped mutable adapter. During Invoke it exposes
borrowed ordered `Arguments`, Host `Context`, `Random`, and `RuntimeLimits`.
Exactly one result may be selected through `SetNothing`, `SetValue`,
`SetBoolean`, `SetInteger`, `SetFloat`, `SetPercentage`, `SetText`, or `SetTag`;
the last setter call is the effective result. Omitting a result produces Nothing.

The callback must not retain the call, arguments, Context, or borrowed values.
Setters are invalid outside an active invocation. Implementations should reuse
the adapter and must not allocate it per hot-path invocation after warmup.

## External-type API

### Declarative catalog

`ExternalTypeDefinition` is immutable normalized data containing a type name,
ordered unique fields, and ordered unique constructor signatures.
`ExternalTypeFieldDefinition` and `ExternalTypeParameterDefinition` each contain
a normalized name and exactly one portable type description: builtin kind plus
unit, or custom type name. `ExternalTypeConstructorDefinition` contains type name,
ordered unique parameters, and derived SignatureId.

Construction copies sequences and rejects null entries, duplicate fields,
duplicate constructor signatures, duplicate parameter labels, constructors for
another type, and constructor parameters that are not declared fields.
The resulting field, constructor, and parameter views must not expose mutable
backing storage through collection adapters or auxiliary collection APIs.

`ExternalTypeCatalog.Types` is immutable declaration order.
`Resolve(typeName)` normalizes and returns a definition or absence. Catalog
construction rejects duplicate type names. The catalog contains no constructor,
field accessor, delegate, reflection object, or host instance.
Its public enumeration and name lookup must continue to describe the same
definitions after construction; accessing a collection view cannot alter either.

### Runtime bindings

`ExternalTypeConstructorReference` is immutable normalized type name plus ordered
argument labels and canonical SignatureId. `CreateSignatureId` is a pure canonical
formatter.

`ExternalTypeRegistry.Resolve(reference) -> Optional<ExternalTypeConstructor>`
runs during Host.Load. Absence or a returned constructor whose `Definition`
does not exactly match the import causes a stable link failure.

`ExternalTypeConstructor.Invoke(call)` is synchronous. The call exposes borrowed
ordered Arguments. `SetExternalValue(value)` accepts only the expected declared
type; mismatch becomes a runtime diagnostic and Nothing. `SetNothing()` rejects
construction without throwing a script exception. Omitting a result also yields
Nothing. The call object and arguments must not be retained.

`ExternalValue.Definition` identifies the declarative type. `GetField(name)`
returns a Value or absence. It must be synchronous and deterministic for an
immutable logical external value during one handler. Platform object identity,
reflection, and lifetime management are adapter responsibilities.

## Diagnostics and failure transport

`Diagnostic` is immutable structured data containing phase, stable ASCII code,
human message, optional symbol and symbol kind, optional SourceLocation,
optional program/handler identity, and optional technical details.
`SourceLocation` contains optional source ID, source/module names, and one-based
Unicode-scalar line/column bounds. Missing context is explicit absence.

Diagnostic phases are Parse, Validate, Compile, Decode, Link, and Runtime.
`SymbolKind` classifies unknown, type, predicate, function, handler, message,
variable, and global definition. `DiagnosticCodes` exposes stable named constants;
`Decode(formatErrorCode)` maps a `.gesb` format failure to its stable decode code.

`CompileException`, `DynamicLinkException`, `ProgramFormatException`, and the
abstract fatal-runtime base are C# failure transports carrying diagnostics. A
language may use thrown errors or result unions. Callers must be able to inspect
the same structured portable fields without parsing text. Runtime diagnostics
normally flow through Observer and ExecutionResult rather than escaping callbacks;
the fatal-runtime base is for trusted native integration and does not define a
script-visible exception mechanism.

## Conformance parser API

### Parser operation

`ConformanceMarkdownParser.Parse(utf8BytesOrText, limits?) -> ConformanceDocument`
is synchronous and fileless. Bytes are strict UTF-8. Text is treated as the same
Unicode content and converted to the canonical parser model. The parser applies
the complete Markdown structure, limited YAML, schema, cross-reference, and
limit validation before returning.

Success returns one fully validated immutable Document. Failure carries an
ordered non-empty list of ConformanceDiagnostic values. `ConformanceParseException`
is the C# transport only. Markdown tokens, YAML nodes, and parse trees are private
implementation details and never appear in the public result.

`ConformanceParserLimits` contains maxima for document bytes, tests, YAML depth,
YAML nodes, scalar bytes, sources/test, source bytes/test, steps/test, and hosts/test.
Limits are checked before proportional allocation. The Default configuration is
read-only by convention and is snapshotted at parse start.

### Normalized document model

`ConformanceDocument` owns immutable source identity, format version, suite ID,
title, frontmatter range, and ordered Cases. `ConformanceSourceDocument` retains
immutable original UTF-8 bytes, BOM presence, and detected line-ending style so
a ReceivedWriter can preserve authoring bytes. `ConformanceSourceRange` is a
value containing UTF-8 byte offset/length and one-based line/column bounds.
Source bytes, nested sequences, and lookup maps expose no writable backing
storage through their public collection views, including map key/value views.

`ConformanceCase` owns normalized identity (`Id`, `SuiteId`, `FullId`, title),
kind, level, categories, tags, capability requirements, sources, steps, compile
options, runtime/random/sink/external-registry settings, native handlers,
deferred programs, host count, optional kind-specific input, expectations,
assembler text, and exact source ranges needed for received output.

All nested case model types are immutable data projections of the fields defined
by [Conformance Markdown](Conformance/MarkdownFormat.md):

- source/message/value data: `SourceInput`, `Message`, `Argument`, `Value`,
  `ValueEntry`, and `MessageSignatureDefinition`;
- execution data: `Step`, `PumpMode`, `NativeHandler`, `NativeEmit`,
  `NativeAction`, `NativeActionKind`, `RandomConfiguration`, `RuntimeLimits`,
  `PublishSinkMode`, and `ExternalTypeRegistryMode`;
- configuration: `CapabilityRequirements`, `CompileOptions`, and
  `ComparisonOptions` with exact/Ulp Binary64 comparison;
- kind-specific inputs: `BinaryFixture`, `MessageApiCase`, `ValueApiCase`,
  `ExternalTypeApiCase`, and `PerformanceWorkload`;
- expectations: `Expectation`, `StepExpectation`, `ChannelExpectation`,
  `ObservationExpectation`, `ObserverEventExpectation`, `ExpectedDiagnostic`,
  `RuntimeLimitExpectation`, `PublishResultExpectation`, `BinaryExpectation`,
  `OpcodeExpectation`, `CompileMetadataExpectation`, `ProgramResourceExpectation`,
  `HandlerResourceExpectation`, `MessageDefinitionExpectation`,
  `MessageApiExpectation`, `ValueApiExpectation`, `ExternalTypeApiExpectation`,
  `PerformanceExpectation`, `PerformanceProfile`, and `PerformanceMetric`.

`ConformanceMessageApiCase.CompareConformanceMessage` and
`ConformanceMessageApiExpectation.ConformanceEquals` expose the optional paired
transport comparison input and result defined in the Markdown format. They
exercise the same conformance comparison as runtime message expectations.

`ConformanceNativeAction.ExpectedError` exposes the optional structured link
diagnostic expected from `loadProgram`. The Markdown format defines its
exclusivity with `ExpectedResult` and the runner's continuation behavior.

`ConformancePerformanceWorkload.ObserveRuntime` exposes the Boolean measurement
observer selection, defaulting to true, defined by the Markdown format. It does
not disable observations in the separate correctness execution. Platform
allocation counters remain behind `PerformanceProvider`, outside the portable
model and runner.
The same collection immutability requirement covers parser diagnostics,
resource bytes, environment capabilities, measurements, and runner reports.

Enum-like model values have the closed sets specified by MarkdownFormat:
`TestKind`, `TestLevel`, `PumpMode`, `PublishSinkMode`,
`ExternalTypeRegistryMode`, `ObserverEventKind`, `NativeActionKind`,
`BinaryOutcome`, and `Binary64ComparisonMode`. A parser rejects unknown V1
values rather than materializing an unknown enum ordinal.

Every model collection preserves author order unless its schema explicitly
requires canonical sorting. Optional submodels are absent, not empty fabricated
objects. Callers may retain and concurrently read a parsed Document.

## Conformance runner API

### Environment and callbacks

`ConformanceRunnerEnvironment` is an immutable snapshot containing runner and
implementation IDs/versions, sorted capability IDs, portable extension catalog/
registry inputs, optional resource resolver, optional performance provider, and
optional performance profile ID. Environment validation precedes case execution.

`ConformanceRunnerOptions` contains RunnerLimits plus flags controlling inclusion
of successful assembler output and technical details. `RunnerLimits` bounds case
count, frames per step, and resource bytes. Defaults are immutable/snapshotted.

`ResourceResolver.Resolve(resourceId, maximumByteCount) -> ResourceResult` is the
only external fixture byte boundary. ResourceResult is immutable status
(`Found`, `NotFound`, `LimitExceeded`, or `Error`), bytes, and optional stable
error code. Found bytes are copied/treated immutable and rechecked against the
limit and SHA-256. The runner never interprets a path or URL.

`PerformanceProvider.Measure(case, profileId) -> PerformanceMeasurement`
returns ordered `MeasuredMetric(id, unit, canonicalValue)` values after ordinary
correctness execution. It owns platform timing/allocation mechanics and must not
mutate the case. `ResultSink.CaseCompleted(result)` is called once per completed
case in execution order; its result is observational and does not replace the
runner-owned immutable result.

Resolver and performance-provider failures are caught at the case boundary and
produce an Error result with a stable runner code. ResultSink is an outer
reporting callback and must not throw; a sink exception is an embedding-contract
violation and is not converted into another case result.

### Runner operations

| Operation | Contract |
| --- | --- |
| `RunCase(document, caseId, environment, options?, sink?)` | Resolves exactly one full or document-local case ID and executes no other case. |
| `RunDocument(document, environment, options?, sink?)` | Runs cases once in document order and returns one report. |
| `RunCorpus(documents, environment, options?, sink?)` | Preflights the complete caller-ordered corpus, rejects duplicate identities before execution, then runs each case once in document/case order. |

All operations are synchronous, threadless, fileless, and test-framework-free.
Options and sink are optional; absence selects defaults/no notifications. Each
case receives isolated mutable compiler/Host/random/observer state. The detailed
execution algorithm and capability behavior are defined by
[Conformance runner](Conformance/Runner.md).

### Results

`ConformanceCaseResult` is immutable and contains case/suite identity, title,
kind, level, categories, tags, status, stable code, missing capabilities,
mismatches, diagnostics, runtime-limit results, optional actual assembler,
optional performance result, and optional technical details.

Status is exactly `Passed`, `Failed`, `Skipped`, or `Error`:

- Failed means the implementation ran but did not meet an expectation.
- Error means invalid environment/model/resource or an unhandled implementation
  failure prevented a valid comparison.
- Skipped is permitted only for an absent declared optional capability.

`Mismatch` stores stable code/path, expected/actual canonical text, and optional
structured result diagnostic. `ResultDiagnostic` is the report-safe projection
of a Core diagnostic. `RuntimeLimitResult` stores limit name, detail, and value.
`PerformanceResult` contains selected profile ID and ordered
`PerformanceMetricResult` values `(id, unit, reference, measured, allowed,
passed)`. Canonical numeric strings avoid locale and Binary64 parser differences.

`ConformanceRunReport` contains immutable runner/implementation identity,
capabilities, optional performance profile, ordered case results, aggregate
status, and RunSummary. Summary counts total/passed/failed/skipped/error exactly
once and must equal the Cases collection. A framework adapter aggregates already
returned case results; it must not rerun the corpus merely to write a report.

## Conformance writer API

All writers are pure with respect to their input models, perform no file I/O,
use deterministic ordering, and either return Text/Bytes or write into
caller-provided storage. `ToArray` output is UTF-8 with LF and no BOM unless the
specific received-preservation rule says otherwise.

| Writer | Operations and contract |
| --- | --- |
| `ResultJsonWriter` | `ToText(report)` / `ToArray(report)` emit the complete canonical machine-readable result with fixed property order. |
| `MarkdownReportWriter` | `ToText(report)` emits the human-readable aggregate report and performance tables. It is informative, not runner input. |
| `ReceivedMarkdownWriter` | `ToText(document, report)` / `ToArray(...)` return an approval candidate that replaces only eligible performance references and assembler payload ranges. It never writes or overwrites the source document. |
| `CrossLanguageResultJsonWriter` | `Identify(documents)` returns CorpusIdentity; `ToText/ToArray(documents, report)` emit the compact canonical cross-language comparison artifact. |

Received output first verifies document/report identity, case presence, source
ranges, non-overlap, and stale authored content. Failure returns a stable
`ReceivedWriterCode` (`InvalidReport`, `MissingCase`, `MissingRange`,
`OverlappingRange`, or `StaleRange`); the C# transport is
`ConformanceReceivedWriteException`. A failed operation returns no partial
candidate and never mutates document bytes.

`CorpusIdentity` contains Markdown format version, document count, case count,
and SHA-256 of canonical corpus identity. `Identify` is deterministic for the
caller-supplied ordered documents and never reads discovery paths.

## Language-binding requirements

### C# and Unity

The portable API is exposed by the `StepH.GameEventScript` Core package and the
optional `StepH.GameEventScript.Conformance` package. The separate
`StepH.GameEventScript.CSharpBridge` package supplies:

- `GameEventScriptCSharpBuilderExtensions.AddFile(builder, path)`, also callable
  as `builder.AddFile(path)`, synchronously reads strict UTF-8 and delegates to
  `AddScript(text, path)`. The supplied path becomes the source identity; a
  leading BOM follows the portable source rules. Reading and decoding finish
  before the builder changes, and no file handle is retained;
- `GameEventScriptCSharpValue.GesMap(entries)` and
  `GameEventScriptCSharpValue.GesRecord(typeName, fields)` snapshot dictionary
  entries and delegate to the portable ordered value factories. Null or empty
  dictionaries mean no entries; dictionary enumeration order does not determine
  the resulting canonical key order;
- tuple/dictionary message and Context conveniences;
- delegate native-handler adapters;
- reflection/attribute extension and external-type registries;
- an optional lock-based automatic Host runner with the ownership-transfer and
  scheduling rules in [HostRuntime](HostRuntime.md#c-automatic-runner).

These adapters must delegate to the portable semantics. Unity consumes the C#
DLL and may choose main-thread/manual pumping instead of the automatic runner.

### Swift, Kotlin, and C++

Ports should prefer native immutable collection views, nullable/optional result
types, and their standard error transport. They must retain:

- ordered message arguments and stable signature IDs;
- exact fixed-width integer/Binary64 values and `.gesb` IDs;
- one serial, non-thread-affine Host execution domain;
- synchronous callbacks and explicit outer synchronization;
- idempotent lifecycle handles and enqueue-time snapshots;
- separate declarative external catalogs and executable runtime registries;
- fileless byte/text codec and Conformance operations;
- identical diagnostics, result states, ordering, and canonical writer output.

An actor, coroutine, executor, or mutex wrapper is an embedding adapter. It must
not become hidden scheduling inside portable Host operations.

## Completeness map

The following map assigns every exported C# reference type to exactly one
conceptual public family and owning section. Every public constructor, method,
property, field, and event inherits the assignment of its declaring type. The
approved C# API snapshot fixes the concrete reference-binding spellings and
members; reviewing a snapshot change therefore also requires reviewing the
corresponding row below. Numeric enum members and individual normalized
Conformance fields are owned by their linked format specifications rather than
duplicated as a second source of truth here.

| Public family | Owning section |
| --- | --- |
| Builder, CompileOptions, CompileException, manager facade | Compiler API |
| Program, all segment/entry/view types, instruction and ID types | Program and bytecode data API |
| BinaryFormat constants, Reader, read options/limits/retention, Writer, Validator, format error, Dumper | Program codec, validation, and dump API |
| Message, MessageArgument(s), MessageSignature | Message and value API |
| Value, ValueSlice, ValueArguments, ValueMap, integer/float range | Message and value API |
| HostBuilder, RuntimeLimits, Host, Instance, Subscription | Host construction and lifecycle API |
| Context, NativeMessageHandler, PublishSink/Result, Observer, ExecutionResult/State | Context, messaging, and execution results |
| RandomGenerator | Random generator API |
| ExtensionReference, ExtensionRegistry/Function, ExtensionCall | Extension API |
| External definitions/catalog, constructor reference/registry/call, ExternalValue | External-type API |
| Diagnostic, codes/phase/symbol/location and failure transports | Diagnostics and failure transport |
| Conformance parser, limits/diagnostics, Document/Case and normalized nested models | Conformance parser API |
| Environment/options/limits, resolver/provider/sink, Runner, results/report/summary | Conformance runner API |
| Result, Markdown, Received, and CrossLanguage writers plus CorpusIdentity | Conformance writer API |
| CSharpBridge filesystem, reflection, delegate, dictionary, and automatic-runner adapters | Language-binding requirements; non-portable |

The portable public API is limited to the families above. VM execution state,
compiler trees, filesystem services, network clients, task schedulers, and
reflection objects remain private or embedding-specific.
