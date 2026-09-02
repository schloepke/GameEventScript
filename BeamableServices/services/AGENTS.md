# Agent Handoff

## Workspace

Root: `/Users/stephan/Projects/BattleClub/BeamableServices/services`

Main project: `StepH-GameEventScript`

Tests: `StepH-GameEventScript-Tests`

Standard verification:

```bash
dotnet test StepH-GameEventScript-Tests/StepH-GameEventScript-Tests.csproj --filter "TestCategory!=Performance"
```

## Collaboration Rules

- The user often wants analysis first when explicitly saying "nur analysieren", "nichts ändern", or similar. Otherwise implementation is usually expected.
- Do not preserve legacy compatibility unless the user explicitly asks for it. The API and DSL are still in development.
- Prefer portability toward Swift, Kotlin, C++, and similar targets.
- Keep C#-specific code in `StepH-GameEventScript/CSharpBridge`.
- The portable Core/API/Runtime/Compiler should avoid C#-specific patterns where practical.
- `CSharpBridge` may use C# idioms such as Reflection, Attributes, `System.Type`, `out`, locks, threads, and `Try...out`.
- In portable core code, avoid own GES-level `Try...out` concepts. Standard library calls such as `Dictionary.TryGetValue`, `TryAdd`, and `TryParse` are currently accepted.
- HotPath VM mutation should go through `GesVmState.Set...` methods. Read-only register borrows are currently accepted for performance.
- Be careful with direct register refs: never hold a mutable destination ref across operations that may grow or replace register storage.
- Use `rg` for code search.
- Do not revert user changes unless explicitly requested.

## Current Architecture Direction

The project has a portable Game Event Script host/VM architecture with a compact value model:

- The old polymorphic value graph has been removed or largely replaced.
- `GesValue` / `GameEventScriptValue` are the current compact value concepts.
- Runtime VM code lives under `StepH-GameEventScript/Runtime/VM`.
- C# Reflection and annotation support lives under `StepH-GameEventScript/CSharpBridge`.
- The old VM/compiler path has been removed or superseded by the new binary compiler and VM.
- Standard extensions were migrated into opcodes where possible.
- Series now use direct VM concepts and `CreateSeries`.
- `GameEventScriptProgram` is the immutable reusable compiler result.
- `GameEventScriptProgram` is the portable parsed representation of the `.gesb` V1 binary. It may contain only data that can be serialized to `.gesb` and deserialized again losslessly and language-neutrally. Host bindings, registries, delegates, reflection objects, runtime caches, and VM state belong outside the program.
- `StepH-GameEventScript/PortableProgramModel.md` is the normative ownership and
  permitted-data contract for the immutable program object graph.
- Bytecode instructions store numeric words and payload bits; semantic aliases
  use casts, shifts, masks, and Binary64 bit conversion rather than overlapping
  CLR fields. The compact sequential C# struct never defines `.gesb` encoding.
- Compilation includes `DebugSymbols`, `SourceMap`, and `SourceArchive` by default; production or size-sensitive builds opt out explicitly with `GameEventScriptDebugInfoOptions.None`.
- `GameEventScriptHost` is the autonomous serial execution unit and can run with native handlers only.
- `Load(program, priority)` is additive and returns an idempotently detachable `GameEventScriptInstance`.
- Portable native handlers implement `IGameEventScriptNativeMessageHandler`.
  C# `Action` adapters live exclusively in `CSharpBridge`.
- Native `Subscribe` returns an idempotently detachable `GameEventScriptSubscription`.
- Program instances and native subscriptions use stable host-local registration
  IDs for lifecycle operations; their handles store no detach/unsubscribe closures.
- Each host creates one `GameEventScriptContext`, owns one logical-message ring queue, and lazily creates at most one reusable `GesVmState`.
- `GameEventScriptVirtualMachine` is a stateless executor; program-specific dynamic links live in `GesLinkedProgram`.
- External types use a declarative compiler catalog, a separate host runtime
  registry, and portable `IGameEventScriptExternalValue` instances. CLR-backed
  implementations remain in `CSharpBridge`.
- `Receive` and `Emit` are local. `Publish` is local plus one optional synchronous `IGameEventScriptPublishSink`.
- Message arguments use ordered portable `GameEventScriptMessageArgument`
  pairs. Core has no tuple/dictionary factory; C# conveniences live in
  `CSharpBridge`, and dictionary binding requires a known signature.
- Core is synchronous, threadless, and unsynchronized. Optional C# automatic execution lives in `CSharpBridge/GameEventScriptCSharpHostRunner.cs`.
- There is no Session, isolated Run, module interface, or module-owned VM compatibility API.
- `StepH-GameEventScript/HostArchitecture.md` is the normative portable responsibility/state-machine document.
- `StepH-GameEventScript/PortableDeterminismSemantics.md` is normative for the
  seeded PRNG, script/structural equality, stable ordering, iteration/ranges,
  and equal-priority host dispatch.

## Recent Completed Work

### Portable Conformance V1 Contracts

- `ConformanceMarkdownV1.md` normatively defines the strict UTF-8 Markdown
  authoring structure, limited YAML subset, stable suite/case IDs, inheritance,
  source/program grouping, ordered step tables, expectations, test kinds,
  Binary64 comparison, performance profiles, and bytecode snapshots.
- `ConformanceRunnerV1.md` normatively separates parsing from synchronous,
  threadless execution and defines capabilities, skip/error rules, case and
  corpus execution, canonical result JSON, aggregate Markdown reports, and
  source-range-based received updates.
- `## Fixtures` is documentation-only in V1. The received writer may propose
  performance-reference and `gesa` updates but never overwrites authored input.
- Implementation of the package/parser/validator is the next conformance step;
  the existing JSON corpus remains active until deterministic migration proves
  parity.

### Portable Program Model Hardening

- Audited the complete `GameEventScriptProgram` graph and documented its
  permitted transport-only data, ownership, construction, validation, and
  representation rules in `PortableProgramModel.md`.
- Removed internal mutable backing-array exposure. All nested program sequences
  are defensively copied and Core bulk reads receive only read-only spans or
  immutable slices.
- Compiler, reader, writer, and `Host.Load` are explicit shared-validator trust
  boundaries.
- Bytecode instructions no longer use overlapping CLR fields. Numeric word and
  payload properties are endian-independent while the C# value type remains 16
  bytes; `.gesb` bytes continue to be field-wise canonical Little Endian.
- Regression tests cover deep defensive copies, public construction, complete
  compile/write/read/rewrite/load/execute behavior, payload bits, golden bytes,
  and numeric API IDs.

Verification after this change:

```text
1073/1073 non-performance tests passed
1/1 zero-allocation hot-path test passed
1/1 JSON performance reference test passed
```

### Portable Diagnostic Contract

- Added language-neutral `parse`, `validate`, `compile`, `decode`, `link`, and
  `runtime` diagnostics with stable ASCII codes and optional symbol/source/
  program/handler context.
- Parser, validator, compiler, `.gesb` decoding, dynamic linking, native handlers,
  publish sinks, and VM failures now expose structured data; C# exceptions are
  transport only.
- Runtime handler diagnostics flow through the observer and execution result
  without adding successful hot-path allocations. JSON conformance no longer
  matches English error text.
- `StepH-GameEventScript/PortableDiagnostics.md` is normative.

Verification after this change:

```text
1069/1069 non-performance tests passed
1/1 zero-allocation hot-path test passed
1/1 JSON performance reference test passed
```

### Ordered Portable Message Arguments

- Core message construction now consumes ordered
  `GameEventScriptMessageArgument` pairs. Argument order remains part of the
  signature and is never derived from dictionary/property iteration.
- Duplicate named labels are rejected after normalization; repeated `_` labels
  remain valid positional arguments.
- Tuple helpers and signature-directed dictionary binding live only in
  `CSharpBridge`.
- All conformance input/output, nested message values, and native emits use the
  ordered JSON `args` array. Equality is position-sensitive.

Verification after this change:

```text
1067/1067 non-performance tests passed
1/1 zero-allocation hot-path test passed
JSON performance allocations and binary dump match the reference; the final
timing run reported only the long mixed case at 818.3905 ms versus an allowed
817.833885 ms after an earlier passing run. No allocation regression remains.
```

### Portable Native Handlers and ID-Based Lifecycle

- Core native subscriptions use `IGameEventScriptNativeMessageHandler`; all
  `Action<GameEventScriptMessage, GameEventScriptContext>` convenience overloads
  and adapters live in `CSharpBridge`.
- Program instances and native subscriptions receive stable, non-reused,
  host-local registration IDs. `Detach()` and `Unsubscribe()` call the host with
  that ID and retain no `Func<bool>` closures.
- Host lifecycle lookup uses intrusive registration links on already allocated
  instance/subscription objects. This avoids eager registry dictionaries and the
  load-allocation regression they would introduce.
- Existing enqueue-time subscription snapshots remain immutable. Detaching or
  unsubscribing removes only future dispatch visibility.
- JSON conformance uses a manual portable native handler, while C# delegate
  convenience and automatic serialized pumping remain covered by bridge tests.

Verification after this change:

```text
1061/1061 non-performance tests passed
1/1 zero-allocation hot-path test passed
1/1 JSON performance reference test passed
```

### Portable External-Type Boundary

- External type declarations are separated from runtime bindings.
  `GameEventScriptBuilder.WithExternalTypeCatalog(...)` consumes only portable
  declarative definitions, while
  `GameEventScriptHostBuilder.WithExternalTypeRegistry(...)` configures the
  constructor bindings resolved by `Host.Load`.
- `GameEventScriptExternalTypeDefinition` no longer contains CLR field readers,
  delegates, or constructor bindings. Runtime instances cross the Core boundary
  through `IGameEventScriptExternalValue`.
- CLR objects, Reflection, Attributes, field readers, constructor invocation,
  and conversion stay in `CSharpBridge`. Its registry implements both portable
  inputs only as a C# convenience adapter.
- JSON conformance uses a manual external-type catalog, runtime registry, and
  value implementation rather than a reflected C# fixture. Reflection behavior
  remains covered by separate bridge tests.

Verification after this change:

```text
1059/1059 non-performance tests passed
1/1 zero-allocation hot-path test passed
1/1 JSON performance reference test passed
```

### Portable Determinism Semantics

- SplitMix64 plus xoshiro256** now have language-neutral raw, bounded-integer,
  and Binary64 known-answer vectors.
- The binary64 API is `NextFloat(firstBound, secondBound)`: it scales a
  `[0,1)` source, while final binary64 rounding may still produce the upper
  bound. Equal and NaN bounds consume no seeded or `FromSequence` value.
- Reversed/equal bounds, full Int64 generation, signed/full-width seeds,
  upper-bound rounding, and nested `random with` parent-stream restoration now
  have direct or JSON conformance coverage.
- Stable sorting, Unicode-scalar map/record order, last-entry-wins duplicate map
  keys, cross-kind equality, strict nested structural equality, iterator order,
  and equal-priority dispatch are one normative contract.
- Range iterators now stop by their overflow-safe precomputed length. They cannot
  wrap past `Int64` boundaries or repeat forever when a Binary64 step no longer
  changes a large current value.
- JSON conformance covers equality and range boundaries; existing JSON cases
  cover stable direct/iterator ordering, map/record order, and script/native plus
  multi-program dispatch order.

Verification after this change:

```text
1057/1057 non-performance tests passed
1/1 zero-allocation hot-path test passed
1/1 JSON performance reference test passed
```

### Portable Number Semantics

- Signed-64 arithmetic, overflow fallback, binary64-to-integer saturation,
  midpoint rounding, negative `div`/`mod`/`rem`, NaN/Infinity/zero handling, and
  two-ULP runtime equality now use one explicit language-neutral number core.
- Compiler constant folding and VM execution share the same rules, including
  exact integer operations above `2^53` and the exclusive binary64 `2^63`
  boundary.
- Conformance JSON now writes shortest roundtrip binary64 decimals with canonical
  exponents and compares finite floats with configurable `maxFloatUlps` (default
  4096; zero is exact).
- The normative contract is `StepH-GameEventScript/PortableNumberSemantics.md`.

Verification after this change:

```text
1041/1041 non-performance tests passed
1/1 zero-allocation hot-path test passed
1/1 JSON performance reference test passed
```

### Portable Text and Unicode Semantics

- Source input is a valid Unicode-scalar sequence; file input is strict UTF-8,
  an optional initial BOM is removed, and `LF`, `CRLF`, and `CR` are the only
  logical newlines. Portable horizontal whitespace is ASCII space/tab.
- Language names and tags use explicit ASCII grammars. Runtime text is not
  normalized; length, 1-based indexing, iteration, and text-to-list conversion
  operate on Unicode scalar values. Ordering is scalar ordinal.
- Compiler columns are 1-based Unicode-scalar columns while `.gesb` SourceMap
  ranges remain UTF-8 byte offsets. The normative contract is
  `StepH-GameEventScript/PortableTextSemantics.md`.
- Linked programs precompute scalar counts for string constants so the portable
  contract does not add per-load or per-count hot-path work.

Verification after this change:

```text
1034/1034 non-performance tests passed
1/1 zero-allocation hot-path test passed
1/1 JSON performance reference test passed
```

### Portable `.gesb` V1

- Added the canonical little-endian sectioned `.gesb` V1 reader and writer,
  bounded retention modes, opaque optional-section preservation, stable format
  errors, and shared validation in reader, writer, and `Host.Load`.
- `GameEventScriptProgram` now exposes immutable runtime, debug/source, build
  metadata, and opaque segments. The compiler always optimizes and emits stable
  C# compiler metadata plus optional DebugSymbols, SourceMap, and SourceArchive.
- Source IDs follow `AddScript` order and source mappings use UTF-8 byte offsets.
  `GameEventScriptProgramDumper` consumes embedded source data and interleaves
  source-line comments. It has no legacy API for separately supplied source text.
- Golden, invalid, retention, Unicode, runtime roundtrip, and JSON binary-roundtrip
  tests cover the portable boundary. `StepH-GameEventScript/GesbFormatV1.md` is
  the normative container specification.

Verification after this change:

```text
1024/1024 non-performance tests passed
1/1 zero-allocation hot-path test passed
1/1 JSON performance reference test passed
```

### Static VM Resource Metadata and Acyclic Calls

- The compiler rejects direct and indirect cycles in the synchronous script call
  graph. Program loading validates the invariant again for future untrusted
  `.gesb` input.
- After physical register allocation, each message-handler bind stores
  `RequiredRegisterCount` and `RequiredCallStackDepth`; the program stores the
  maximum of both values across all message handlers.
- Register requirements include simultaneous caller/callee frames and staged
  values. The root handler has call-stack depth zero.
- `Host.Load(...)` rejects programs whose declared requirements exceed
  `MaxRegisterValues` or `MaxCallDepth` and uses the register requirement to
  pre-warm the host-owned VM state.
- JSON conformance covers metadata and load-limit rejection; low-level compiler
  tests cover direct and indirect cycles.

Verification after this change:

```text
1002/1002 non-performance tests passed
1/1 zero-allocation hot-path test passed
```

### Host / Program / VM Split

- Removed `IGameEventScriptModule`, `GameEventScriptSession`, isolated runs, the module-owned VM, and automatic Core dispatch.
- Added immutable program table views and host-specific linked-program data.
- Added direct resumable script dispatch, immutable subscription snapshots, and a growing logical-message ring queue.
- Added value-type execution/publish results, local-plus-outbound Publish semantics, initialization-per-instance, Detach, and Unsubscribe.
- Migrated JSON conformance to `Program -> Host.Load -> Receive -> ExecuteFrame/RunToCompletion`, including multi-program and frame-resume cases.
- Added allocation validation showing zero queue/selection/frame/resume heap allocation after warmup when message creation and output payload creation are excluded.

Verification after this architecture change:

```text
993/993 non-performance tests passed
1/1 zero-allocation hot-path test passed
1/1 JSON performance reference test passed
```

### Custom `Try...` Cleanup

The user asked why `bool TryXXXX` concepts still existed outside `CSharpBridge`.

Completed cleanup:

- Compiler/Rewriter custom `Try...` methods were removed or renamed:
  - `TryEmitExpressionToRegister` -> `EmitExpressionToRegister`
  - `TryEmitCollectionPipelineInto` -> `EmitCollectionPipelineInto`
  - `TryEmitSpatialConstructor` -> `EmitSpatialConstructor`
  - Rewriter helpers like `TryGetJumpTarget`, `TryInvertBranch`, `TryFoldUnary`, `TryFoldBinary`, and `TryGetLocalConstant` were converted to nullable/default-return styles.
- Runtime/Budget/Host custom `Try...` methods were renamed:
  - execution-step accounting now uses `ReserveExecutionSlice` / `CompleteExecutionSlice`
  - `TryConsumeLoopIteration` -> `ConsumeLoopIterationIfAvailable`
  - `TryEnterCall` -> `EnterCallIfAvailable`
  - `TryCheckRangeLength` -> `CheckRangeLengthWithinLimit`
  - `TryCheckGeneratedCollectionItemCount` -> `CheckGeneratedCollectionItemCountWithinLimit`
  - `TryCheckDice` -> `CheckDiceWithinLimit`
  - `TryEnqueue...` -> `Enqueue...`

Verification after this cleanup:

```text
990/990 non-performance tests passed
```

Remaining `Try...` outside `CSharpBridge` should only be standard-library style uses such as:

- `TryGetValue`
- `TryAdd`
- `TryParse`

## Important Files

- `StepH-GameEventScript/Compiler/GesCompiler.cs`
- `StepH-GameEventScript/Compiler/GesBinaryBuilder.cs`
- `StepH-GameEventScript/Compiler/GesBinaryBuilderRewriter.cs`
- `StepH-GameEventScript/Runtime/GesRuntimeBudget.cs`
- `StepH-GameEventScript/Api/GameEventScriptProgram.cs`
- `StepH-GameEventScript/Api/GameEventScriptProgramReader.cs`
- `StepH-GameEventScript/Api/GameEventScriptProgramWriter.cs`
- `StepH-GameEventScript/Api/GameEventScriptProgramValidator.cs`
- `StepH-GameEventScript/Api/GameEventScriptProgramDumper.cs`
- `StepH-GameEventScript/Api/GameEventScriptHost.cs`
- `StepH-GameEventScript/Api/GameEventScriptContext.cs`
- `StepH-GameEventScript/Runtime/VM/GesLinkedProgram.cs`
- `StepH-GameEventScript/Runtime/VM/GameEventScriptVirtualMachine.cs`
- `StepH-GameEventScript/Runtime/VM/GesVmState.cs`
- `StepH-GameEventScript/CSharpBridge/GameEventScriptCSharpHostRunner.cs`
- `StepH-GameEventScript/HostArchitecture.md`
- `StepH-GameEventScript/GesbFormatV1.md`

## Known Warnings

The normal test build currently emits XML documentation warnings for `GesValueMap`. These warnings were not part of the last cleanup task.

## Architecture Backlog

This is the persistent list of intentionally deferred or upcoming architecture
work. Keep these topics in mind when changing adjacent code, but do not implement
an item merely because it is listed here. Work on it when the user makes it part
of the current task. Whenever a backlog item is completed, remove it from this
section as part of the same change; record the outcome in the relevant normative
documentation or, when useful for handoff, under `Recent Completed Work`. Do not
leave completed or checked-off items in the backlog. Keep entries short; if this
section grows substantially, move the details to a dedicated backlog document
and retain a required pointer here.

### Next portable architecture steps

- Prioritize the language-neutral contracts, metadata, and portable Markdown
  conformance needed for the existing Swift/Kotlin/C++/C# monorepo before deeper
  optimizer work. Keep the existing JSON corpus only until deterministic
  Markdown migration proves parity.

### Deferred language and state features

- Add a general immutable collection `fold`/`reduce` concept if concrete use cases
  exceed the existing specialized aggregations (`sum`, `average`, `min`, `max`,
  and `count`). Prefer a bounded collection operation over recursion or general
  local mutation.
- Design host-bound Tables as the future explicit mutation model. Mutations should
  enter a deterministic modification queue; snapshot visibility, read-your-writes,
  commit boundary, rollback, observation, persistence, and replication semantics
  remain to be specified.
- Finalize the product wire envelope later. The language-port conformance shape
  is already fixed, including ordered message `args` arrays.

### Deferred binary-format extensions

- Specify and implement optional `.gesb` compression codecs separately; V1 only
  reserves the codec bits and emits known sections uncompressed.
- Specify signatures, certificates/keys, trust policy, and rollback behavior
  separately; V1 only reserves the security section range and provides no
  authenticity guarantee.

### Deferred editor tooling

- After the Kotlin port is stable, build a dedicated IntelliJ plugin with
  native `.ges`/`.gesa` support beyond the portable TextMate highlighting and
  `.region` folding. Keep the portable dump and TextMate bundles free of
  IntelliJ-specific markers in the meantime.

### Deferred performance work

- Improve CFG/liveness-based register allocation and reuse of non-overlapping
  locals after the monorepo-oriented contracts are stable.
- Check whether bytecode optimizer passes still produce meaningful diffs now that
  the compiler emits better registers directly.
- Revisit Message/Emit allocation only when performance data justifies it.
- Revisit a more VM-near extension call model if boxing at the extension boundary
  becomes expensive again.
