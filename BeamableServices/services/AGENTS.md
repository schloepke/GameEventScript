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
- Compilation includes `DebugSymbols`, `SourceMap`, and `SourceArchive` by default; production or size-sensitive builds opt out explicitly with `GameEventScriptDebugInfoOptions.None`.
- `GameEventScriptHost` is the autonomous serial execution unit and can run with native handlers only.
- `Load(program, priority)` is additive and returns an idempotently detachable `GameEventScriptInstance`.
- Native `Subscribe` returns an idempotently detachable `GameEventScriptSubscription`.
- Each host creates one `GameEventScriptContext`, owns one logical-message ring queue, and lazily creates at most one reusable `GesVmState`.
- `GameEventScriptVirtualMachine` is a stateless executor; program-specific dynamic links live in `GesLinkedProgram`.
- `Receive` and `Emit` are local. `Publish` is local plus one optional synchronous `IGameEventScriptPublishSink`.
- Core is synchronous, threadless, and unsynchronized. Optional C# automatic execution lives in `CSharpBridge/GameEventScriptCSharpHostRunner.cs`.
- There is no Session, isolated Run, module interface, or module-owned VM compatibility API.
- `StepH-GameEventScript/HostArchitecture.md` is the normative portable responsibility/state-machine document.

## Recent Completed Work

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

- Prioritize the language-neutral contracts, metadata, and JSON conformance needed
  for the existing Swift/Kotlin/C++/C# monorepo before deeper optimizer work.

### Deferred language and state features

- Add a general immutable collection `fold`/`reduce` concept if concrete use cases
  exceed the existing specialized aggregations (`sum`, `average`, `min`, `max`,
  and `count`). Prefer a bounded collection operation over recursion or general
  local mutation.
- Design host-bound Tables as the future explicit mutation model. Mutations should
  enter a deterministic modification queue; snapshot visibility, read-your-writes,
  commit boundary, rollback, observation, persistence, and replication semantics
  remain to be specified.
- Finalize the JSON/wire message shape later; it is intentionally still open.

### Deferred binary-format extensions

- Specify and implement optional `.gesb` compression codecs separately; V1 only
  reserves the codec bits and emits known sections uncompressed.
- Specify signatures, certificates/keys, trust policy, and rollback behavior
  separately; V1 only reserves the security section range and provides no
  authenticity guarantee.

### Deferred performance work

- Improve CFG/liveness-based register allocation and reuse of non-overlapping
  locals after the monorepo-oriented contracts are stable.
- Check whether bytecode optimizer passes still produce meaningful diffs now that
  the compiler emits better registers directly.
- Revisit Message/Emit allocation only when performance data justifies it.
- Revisit a more VM-near extension call model if boxing at the extension boundary
  becomes expensive again.
