<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# C# and Swift port convergence plan

This is a temporary working plan. It records the naming and structure decisions
for converging the C# and Swift ports and the phases that implement them. Work
through the phases in order; remove this document in the change that completes
the last phase. The permanent rules are summarized in `AGENTS.md`.

## Rules

### Principle

Ports provide the same functionality, concepts and structure, so a developer who
knows one port finds their way in another. Each port nevertheless follows its
language's API design guidelines and should feel as native as possible, because
the usual case is one developer using one port in their own language. Idioms may
change spelling and mechanics (factories, errors, builders, literals), never the
primary concepts: every port works with the same edge types and roles.

### Prefixes

- `GameEventScript` is reserved for the three API edge types, the core nouns of
  the model (a Program runs in a Host and receives Messages):
  `GameEventScriptHost`, `GameEventScriptProgram` and `GameEventScriptMessage`.
- Edge types bundle their own input/output: Program reading/writing and Message
  JSON encoding/decoding are members of the edge type, not separate types.
- Every other library type, public or internal, uses `Ges`. Bridge types use
  `GesBridge`.
- Exempt: nested types, `private`/`fileprivate` types, executables (CLI,
  Conformance adapters) and tests, and technically required types such as
  C# `IsExternalInit`.
- Bridge attributes and macros keep their short names: `[GesType]`/`@GesType`,
  `[GesField]`/`@GesField`, `[GesConstruct]`/`@GesConstruct`, and so on. They
  mark user types and are not Bridge objects.
- Abbreviations are written as words: `Ges`, `Vm`, `Api`; never `GES`, `VM`, `API`.
- No type may share its name with a Swift module (for example
  `GameEventScriptCompiler`, `GameEventScriptSyntaxHighlighter`); such a type
  breaks module qualification.

### Identical names, idiomatic spelling

Corresponding types and members use identical names in both ports. C# files are
named after their main type or with a name that summarizes their content; partial
class files append an aspect (`GesBinaryBuilderOpcodes.cs`). Swift file names are free. Only these language-idiomatic mappings may differ:

| Concept | C# | Swift | Later Java/Kotlin |
|---|---|---|---|
| Error type | `GesXException` | `GesXError` | `GesXException` |
| Platform argument/state errors | BCL `ArgumentException`, `InvalidOperationException` | `GesApiError` | platform exceptions |
| Interface | `IGesX` | `GesX` (protocol) | `GesX` |
| Direct creation of data types | constructor or `Create(...)` | throwing or plain `init` | constructor |
| Value creation | `GesValue.GesInteger(5)`; with `using static`, `GesInteger(5)` | `GesValue.integer(5)`; in context, `.integer(5)` | `GesValue.integer(5)`; with static import, `integer(5)` |
| Builder creation | `new GesXBuilder()` | `GesXBuilder()` | constructor |
| Builder convenience on target type | `GameEventScriptHost.CreateBuilder()` | `GameEventScriptHost.makeBuilder()` | `newBuilder()` |
| Builder completion | `Build()` | `build()` | `build()` |
| Other factory methods | `CreateX(...)` | `makeX(...)` | `newX(...)` / `createX(...)` |
| Immutable list exposure | `GesReadOnlyArray<T>` / read-only spans | `[T]` | read-only list |
| Cross-package internals | `internal` + `InternalsVisibleTo` | `@_spi(Compiler)` / `@_spi(Bridge)` public | module-internal |
| Type-erased wrapper | object or interface | `GesAnyX` | interface |

An error type may be absent from a port that cannot produce that error.

C# value factories keep the `Ges` member prefix because C# methods and types share
PascalCase: after `using static`, `Integer(5)`, `List(...)` or `Range(...)` would
read like type names and collide with `List<T>` and `System.Range`. Swift and
Java/Kotlin factories are lower-case and resolve through the expected type or
the static import, so they omit the redundant prefix.

### Creation

- Root objects (`GameEventScriptHost`, `GameEventScriptProgram`) are created
  through builders. Their own constructors/initializers are internal.
- `GameEventScriptMessage` uses the idiomatic constructor/initializer and JSON
  members (C# `GameEventScriptMessage.FromJson(...)`/`message.ToJson()`, Swift
  `GameEventScriptMessage(json:)`/`message.json()`).
- Builders are directly creatable. A builder accessor on the target type is only
  a convenience.
- `GameEventScriptProgram.CreateBuilder()` is a C# 14 static extension member in
  the Compiler package. The Compiler project may use C# 14 while still targeting
  netstandard2.1; the compiler embeds the required marker attribute. C# 9
  consumers such as Unity cannot use the extension syntax and use
  `new GesProgramBuilder()`. Swift provides
  `GameEventScriptProgram.makeBuilder()` as a Compiler-module extension.
- Values are created through static factories on `GesValue`; its constructors are
  internal where the language allows it (the C# struct default value remains
  Nothing).
- Message arguments, signatures, options and limits use the idiomatic
  constructor/initializer.
- No central value factory and no `Host.createMessage`: factories stay on their
  type, and platform conveniences (C# tuples, Swift literals) belong in the
  Bridge or in idiomatic Swift literal conformances.

### Layers

- Core (Runtime, Compiler) is normative, portable and structurally identical in
  both ports: same types, same layers (`Api`, `Program`, `Runtime/Vm`,
  `Runtime/Values`, compiler phases).
- Bridge adapts the Core to the platform (threads, locks, dispatchers,
  Reflection versus KeyPath/macros, delegates versus closures). The same role
  has the same name; platform-only types are allowed.
- SyntaxHighlighter is a separate module with the entry type
  `GesSyntaxHighlighter`.

### Performance and size

- Optimize for performance first, then for library/binary size. Hot-path
  duplication is acceptable when it is measurably faster.
- Size reductions are welcome at any time if they do not noticeably reduce
  performance: at most about 3% slower on the affected workloads, zero-allocation
  paths stay at zero.
- The 3% is a cumulative budget, not a per-change allowance. Compare against the
  frozen reference measurement taken at the start of phase 0 on the same machine
  and toolchain, not against the previous change. Single elapsed metrics vary by
  up to about ±20% (C#) and ±10% (Swift) between runs, so the budget applies to
  the geometric mean of all elapsed ratios over repeated runs; allocation metrics
  must not grow at all.
- Inlining attributes (`AggressiveInlining`, `NoInlining`, `@inline(__always)`,
  `@inline(never)`) are added only with a measured benefit and a short rationale
  comment.
- Opcode handlers are `Execute<OpCode>` members of `GesVirtualMachine`, named after
  the `GesBytecodeOpCode` case and callable only from dispatch: C# `private`
  members of the partial class, Swift `internal` members whose calls the port gate
  restricts to the dispatch file and `Opcodes`. Trivial opcodes may stay in
  dispatch; the set need not be identical across ports.

## Decided mappings

General rule: `GameEventScriptX` becomes `GesX` and `IGameEventScriptX` becomes
`IGesX`. The following entries deviate from that rule or merge diverging names.

### Public API

| C# today | Swift today | Target |
|---|---|---|
| `GameEventScriptBuilder` | `GameEventScriptBuilder` | `GesProgramBuilder` |
| `GameEventScriptHostBuilder` | `GameEventScriptHostBuilder` | `GesHostBuilder` |
| `GameEventScriptInstance` | `GameEventScriptInstance` | `GesProgramInstance` |
| `GameEventScriptRandomGenerator` | `GameEventScriptRandomGenerator` | `GesRandomGenerator` |
| `GameEventScriptMessage` | `GameEventScriptMessage` | unchanged (edge type) |
| `GameEventScriptMessageArgument(s)`, `…MessageSignature` | same | `GesMessageArgument(s)`, `GesMessageSignature` |
| `GameEventScriptMessageJson` | `GameEventScriptMessageJson` | JSON members: C# `ToJson()`/`FromJson(json)`, Swift `json()`/`init(json:)` on `GameEventScriptMessage` and `GesValue` |
| `GameEventScriptProgramReader/Writer/Validator/Dumper` | same | members of `GameEventScriptProgram`: `Read`/`read`, `Encode`/`encode`, `GetEncodedSize`/`encodedSize`, `Write(Span)`/`write(into:)`, `Validate`/`validate`, `Dump`/`dump` |
| `GameEventScriptIntegerRange`/`FloatRange` | `GesIntegerRange`/`GesFloatRange` | `GesIntegerRange`/`GesFloatRange` |
| `GameEventScriptBytecodeInstructionUnit` | `GesUnit` | `GesQuantityUnit` (the unit of a source `:Quantity`) |
| `GameEventScriptDebugInfoOptions` (Runtime) | `GameEventScriptDebugInfoOptions` (Compiler) | `GesDebugInfoOptions` in Runtime |
| Program segments (`…CodeSegment`, `…BindingSegment`, …) | flat Program properties | segment model in both ports: `GesCodeSegment`, `GesStringConstantSegment`, `GesBindingSegment`, `GesDebugSymbolsSegment`, `GesSourceMapSegment`, `GesSourceArchiveSegment`, `GesBuildMetadataSegment`, … |
| `GameEventScriptBindingSegment.GameEventScriptBinaryBindEntry` | `GameEventScriptBinding` | `GesBindingSegment.Entry` (nested) |
| `IGameEventScriptExternalTypeCatalog` + class `GameEventScriptExternalTypeCatalog` | `…CatalogProtocol` + struct | `IGesExternalTypeCatalog`/`GesExternalTypeCatalog` + `GesDeclaredExternalTypeCatalog` |
| `GameEventScriptExtensionFaultException` | `GameEventScriptExtensionFault` | `GesExtensionFaultException` / `GesExtensionFaultError` |
| BCL exceptions | `GameEventScriptAPIError` | `GesApiError` (Swift only) |
| `GameEventScriptSyntaxHighlighter`, `…AnsiRenderer`, `…Highlight*`, `…Syntax*` | same | `GesSyntaxHighlighter`, `GesAnsiRenderer`, `GesHighlight*`, `GesSyntax*` |
| – | `GameEventScriptCompilerSupport` (`@_spi`) | `GesCompilerSupport` |
| `signature.CreateMessage(...)` | `signature.createMessage(...)` | `CreateMessage` / `makeMessage` |
| public `GameEventScriptHost` constructor path | public `GameEventScriptHost.init(...)` | internal; builders only |

### Bridge

| C# today | Swift today | Target |
|---|---|---|
| `GameEventScriptCSharpHostRunner` | `GameEventScriptSwiftHostRunner` | `GesBridgeHostRunner` |
| – | `GameEventScriptSwiftRegistration` | `GesBridgeHostRunnerRegistration` |
| `…CSharpExtensionRegistryBuilder` + `…CSharpExtensions` | `…SwiftExtensionRegistry` | `GesBridgeExtensionRegistry` (+ `GesBridgeExtensionRegistryBuilder` in C#) |
| internal Reflection extension functions | `…SwiftExtension` | `GesBridgeExtension` |
| `…CSharpExternalTypeRegistry` | `…SwiftExternalTypeRegistry` | `GesBridgeExternalTypeRegistry` |
| `…CSharpNativeMessageHandler` (internal) | `…SwiftMessageHandler` | `GesBridgeMessageHandler` |
| host-extension publish callbacks | `…SwiftPublishSink` | `GesBridgePublishSink` |
| `…CSharpValue` | `…SwiftValue` | `GesBridgeValueConversion` |
| – | `…SwiftValueConvertible` | `GesBridgeValueConvertible` |
| ? | `…SwiftConversionError` | `GesBridgeConversionException` / `GesBridgeConversionError` |
| – | `…SwiftField<Root>`, `…SwiftConstructor<Root>`, `…SwiftType<Root>`, `…SwiftTypeBinding` | `GesBridgeFieldBinding<Root>`, `GesBridgeConstructorBinding<Root>`, `GesBridgeTypeBinding<Root>`, `GesBridgeAnyTypeBinding` |
| `…CSharpHostExtensions`, `…CSharpContextExtensions`, `…CSharpMessage`, `…CSharpExternalTypes` | – | `GesBridgeHostExtensions`, `GesBridgeContextExtensions`, …; fold script-extension registration into `GesBridgeExtensionRegistry` |

### Internal

| C# today | Swift today | Target |
|---|---|---|
| namespace/folder `Runtime.VM` | folder `Runtime/VM` | `Vm` |
| `GesValueRangeInteger`/`Float` | (public ranges) | merge into or rename next to `GesIntegerRange`/`GesFloatRange` |
| `GameEventScriptNumber`/`Text` | public `GesNumber`/`GesText` | internal `GesNumber`/`GesText` |
| `GameEventScriptSystemClock` | `GesSystemClock` | `GesSystemClock` |
| `GesExternalValue` (class) | – | rename; conflicts with the Swift protocol name |
| `GesValueVectorPoint` | `GesSpatialValue` | one name |
| `GesSeries` | `GesSeriesValue` + empty `enum GesSeries` | one name |
| `IGesIterator` + concrete iterators | `class GesIterator` | one model and name |
| `GesVmCompoundIterators` | `GesCompoundSources` | one name |
| `MessageRingQueue`, `PendingMessage`, `SubscriptionEntry` (nested) | `GesMessageQueue`, `GesPendingMessage`, `GesSubscriptionEntry` | Swift names, top-level internal in both |
| `GesProgramCallGraphValidator` (in `VM`), debug checks in `…ProgramValidator` | call graph in resource validator, `ProgramDebugValidator` | `GesProgramResourceValidator`, `GesProgramInternalValueValidator`, `GesProgramCallGraphValidator`, `GesProgramDebugValidator` in the Program layer |
| `TextNumberCast`, `TextLiteralReader` | `TextNumberCast`, `MessageNames` | `Ges` prefix |
| ~100 AST records (`BinaryExpressionNode`, …) | `GesExpression.Kind` cases | `Ges` prefix in C#; typed cases in Swift |
| `GesVmRegister*` static classes, `GesVm<Op>` / `<Op>Registers` methods | `execute<Op>` extensions | `Execute<OpCode>` members |
| `SetMessageHandler`, `SetRange`, `SetExternalType`, `SetTextPointer`, `StageTagConstant`, `RaiseError`, `ModifyLocalRegisters`, `EnsureRegisterCapacity` | `setHandler`, `setIntegerRange`/`setFloatRange`, `setExternal`, `setTextConstant`, `stageTag`, `fail`, `modifyLocals`, `grow` | `SetHandler`, `SetIntegerRange`/`SetFloatRange`, `SetExternal`, `SetTextConstant`/`SetTagConstant`, `StageTextConstant`/`StageTagConstant`, `RaiseError`, `ModifyLocals`, `EnsureRegisterCapacity` |

## Phases

Each phase is one or more commits on the current task branch. Every phase ends
with:

```bash
dotnet test GameEventScript.sln --configuration Release --filter "TestCategory!=Performance|TestCategory=Allocation"
sh scripts/test-csharp-conformance.sh
./scripts/test-swift.sh
python3 scripts/verify-swift-api.py
python3 scripts/verify-port-names.py
```

Phases touching the VM or value hot paths additionally run the C# performance
gate and `scripts/test-swift-performance.sh` and compare against the frozen
reference measurement:

```bash
python3 scripts/port-performance-reference.py compare --port csharp --runs 5 --reference implementation/port-performance-reference/csharp.json --baseline-commit 30811af3
python3 scripts/port-performance-reference.py compare --port swift --runs 5 --reference implementation/port-performance-reference/swift.json --baseline-commit 30811af3
```

`--baseline-commit` builds the reference revision in a temporary worktree and
measures it interleaved with the candidate, so machine drift affects both sides
equally; allocations are always compared with the frozen files. `30811af3`
contains exactly the measured reference sources plus the recalibrated corpus.

### Frozen reference

- Captured on the source state of `6f47325d` (unchanged code since `03d82419`)
  with five complete performance-corpus runs per port; see the `environment`
  block in `implementation/port-performance-reference/*.json` for machine and
  toolchains. Only that machine/toolchain combination may be compared.
- Qualification: comparing the unchanged sources against the reference yields an
  elapsed geometric mean of 1.0176 for C# and 1.0053 for Swift (five runs each).
  C# noise alone therefore uses about 1.8 of the 3 percentage points; repeat a
  C# comparison or raise `--runs` before attributing a result between 1.02 and
  1.03 to a change.
- Drift: at the end of phase 1 the unchanged reference sources measured 1.046
  (C#) against the frozen file, while the phase 1 result measured interleaved
  against them was 1.0004 (C#) and 0.9955 (Swift). Elapsed-time budgets
  therefore use the interleaved baseline, not the frozen file.
- Phase 1 also removed a 24-byte Swift compile allocation introduced by an
  eagerly computed index-list offset table; offsets are now derived on demand.
- Pre-existing C# gate failure at capture time, not caused by the convergence:
  `performance.core/case-0004` measures `program-load.allocated` 18.125 KiB
  against the stored corpus reference 16.10 KiB (budget 16.91 KiB),
  deterministically. This corpus budget was subsequently recalibrated to
  18.125 KiB with a 19.03125 KiB maximum after three matching measurements;
  the frozen convergence reference is unchanged. The Swift performance corpus
  passed completely.
- `scripts/port-performance-reference.py`, its controls
  `scripts/test-port-performance-reference.py` and the reference files are
  removed together with this document.

### Phase 0 – Rules and gate

- [x] Record decisions in this document and summarize the rules in `AGENTS.md`.
- [x] Take and store the frozen C# and Swift reference measurements.
- [x] Add `scripts/verify-port-names.py`: extract library types and opcode
      handler names from both ports, apply the idiomatic mappings, fail on
      unmapped differences, restrict Swift `execute…` calls to dispatch and
      `Opcodes`. The ratchet baseline `scripts/port-names-baseline.txt` starts
      with 855 known differences and shrinks per phase.
- [x] Add its regression controls and wire it into C# CI (runs on every push
      and pull request, covering both ports).
- [x] Resolve the pre-existing C# `performance.core/case-0004` allocation
      failure: recalibrate only the corpus program-load allocation reference
      from three matching measurements, retaining the 5% allowance.

### Phase 1 – Public API

- [x] Rename public types per the prefix rule and the public mapping table in
      both ports, snapshots, specs, guides and CHANGELOG. Deferred to the
      structural steps below: Program Reader/Writer/Validator/Dumper, Message
      JSON, and the Swift-only `GameEventScriptBinding`, `…BuildMetadata` and
      `…SourceMap` (segment model). Bridge types follow in phase 2.
- [x] Builders: direct creation, `CreateBuilder()`/`makeBuilder()` conveniences,
      C# 14 for the Compiler project only, internal Host initializer in Swift,
      Swift `make…` factory methods. The C# convenience lives in
      `GesProgramBuilderEntry`; API gates ignore its compiler-generated marker types.
- [x] Segment Program model in Swift; Reader/Writer/Validator/Dumper as Program
      members in both ports (C# `Encode()` replaces `ToArray`, Swift `encode()`
      replaces `bytes`; Swift segments also conform to `RandomAccessCollection`).
- [x] Move `GesDebugInfoOptions` to the Swift Runtime.
- [x] Message JSON as `GameEventScriptMessage` members in both ports; value JSON
      likewise as `GesValue` members.
- [x] Show the C# `using static` value-factory style in the guides.
- [x] Errors: apply the error mappings; Swift gains `GesDiagnosticCodes`. Permanent
      port-specific types are listed with reasons in `PLATFORM_ONLY` of
      `scripts/verify-port-names.py` (BCL-mapped Swift errors, the C# exception
      base class, the C# enum extension container `GesQuantityUnits`). Swift
      gains `GesSectionFlags`; the C# `GesInstructionFlag` becomes internal.
- [x] Disambiguate `GesSymbolKind` versus `GesDebugSymbolKind`: renamed to
      `GesDiagnosticSymbolKind`.
- [x] Update XML/`///` documentation, `specs/PublicApi.md`, both API snapshots,
      guides, website examples, `scripts/test-guide-examples.py` inputs, and
      `CHANGELOG.md` (breaking change).

### Phase 2 – Bridge

- [x] Apply the Bridge mapping table in both ports.
- [x] Consolidate value conversion and extension registration entry points
      (C# `GesBridgeExtensionRegistry.Create/CreateBuilder`,
      `GesBridgeExternalTypeRegistry.Create`; C# gains `GesBridgePublishSink` and a
      public `GesBridgeMessageHandler`).
- [x] Update Bridge tests, macros and documentation. Single-bridge types are
      listed with reasons in the port gate.

### Phase 3 – Internal names and layers

- [x] `VM` → `Vm` in namespaces and folders.
- [x] Validators in the Program layer with identical split (C# `Api/Validation`, Swift
      `Api/Program/Validation`; CallGraph runs Resource in both, Debug is separate).
- [x] Host internals, value types and iterators per the internal mapping table:
      C# host internals become GesMessageQueue, GesPendingMessage,
      GesSubscriptionEntry, GesDelayedMessage and GesDeferredPublication; the C#
      range payload merges into GesIntegerRange/GesFloatRange; GesSpatialValue,
      GesSeriesValue, GesExternalStorage, GesCompoundSources, GesSystemClock,
      GesNumber and GesText use one name in both ports.
- [x] Align public value types: Swift `GesSpatialValue`, `GesText`, `GesNumber`
      become internal and `GesSeriesValue` `@_spi(Compiler)`, as in C#; C#
      `GesValueSlice` maps idiomatically to Swift `[GesValue]`.
- [x] `Ges` prefix for C# AST records and remaining unprefixed internal types
      (no prefix or abbreviation findings remain in the port gate).
- [x] Rename C# files whose type is renamed in this phase and split
      `GameEventScriptProgram.cs` like the Swift files: `GameEventScriptProgram.cs`,
      `GesProgramSegments.cs`, `GesBytecode.cs`, `GesBinaryFormat.cs`,
      `GesReadOnlyArray.cs`.
- [x] Decided follow-ups: Swift adopts the C# iterator model with one dedicated
      iterator per source kind and a non-generic direct register write; the
      public value kind is `GesBytecodeTypeKind` in both ports; Swift maps expose
      sorted keys with parallel values like C# (internal storage stays a
      contiguous sorted entry array).

### Phase 4 – VM handlers

- [x] C#: convert `GesVmRegister*` classes into `private Execute<OpCode>` members
      of `partial class GesVirtualMachine`, one file per opcode under
      `Runtime/Vm/Opcodes/{Core,Math,Collections}`; helpers shared by several
      handlers under `Support`. Done as one mechanical commit (A/B 0.9966).
- [x] Align handler names with opcode cases (`IntegerDivide`, `LogN`,
      `KeysOfMap`, separate `HasAny`/`HasAll`, `ContainsAny`/`ContainsAll`, …);
      fast and general variants use the Swift suffixes `Numeric`/`General`.
- [x] Align VM setter and staging names per the internal mapping table:
      `SetHandler`, `SetIntegerRange`/`SetFloatRange`, `SetExternal`,
      `SetTextConstant`/`SetTagConstant`, `StageTextConstant`/`StageTagConstant`,
      `RaiseError`, `ModifyLocals`, `EnsureRegisterCapacity` (Swift lower camel case).
- [x] Swift: fix handler names that do not match their opcode
      (`compoundIterator` in `IteratorCreate`), remove empty namespace files,
      move helper extensions off the VM-opcode files where they do not belong.
      Multi-opcode handlers keep a descriptive name in both ports (`Send`,
      `EmitOrPublishMessage…`). A/B against 30811af3: 0.9827.
- [x] Re-measure every existing inlining attribute per group in both ports
      (C# 14 `AggressiveInlining`, Swift 38 `@inline(__always)` and 120
      `@inline(never)`); keep only measured benefits with a rationale comment.
      C# keeps `ExecuteAdd` (3.4x on core/case-0005). Swift drops all
      `@inline(never)` (1.0025, +16 KB adapter) and the four neutral
      `@inline(__always)`; the VM core, `GesValue` and Add/Modulo/Multiply fast
      paths stay (2–13% on core and low-allocation runs).

- [x] Map storage layout, decided by measurement in both ports with the new
      `performance.maps` workloads. Parallel key and value arrays win: Swift
      entries versus parallel arrays run equally fast (±1.5%) but parallel arrays
      allocate 16–30% fewer bytes at equal allocation counts, because maps built
      from one constant key layout share its key array; C# entries run 3–10%
      slower with 4–8% more bytes. Both ports now use parallel arrays. The entry
      layout would not have let C# drop its caches; the parallel layout drops the
      `_valueList` copy instead (map-values 10% faster, 43% fewer bytes). Swift's
      map value iterator iterates the shared value array.

### Phase 5 – Compiler

- [ ] Swift: typed `GesTokenKind`, `GesBinaryOperator`, `GesUnaryOperator`
      instead of strings.
- [ ] Identical phase types in both ports: `GesLexer`, `GesParser`,
      `GesAstValidator`, `GesShadowingValidator`, `GesAstOptimizer`,
      `GesBinaryBuilder`. Analyze first how Swift distributes AST optimization.
- [ ] Named optimization passes behind `IGesBinaryOptimizationPass` /
      `GesBinaryOptimizationPass` with the C# pass names.
- [ ] Verify byte-identical `.gesb` output for the corpus and the
      `compile.loop-invariants` snapshot.

### Phase 6 – Tools and adapters (low priority)

- [ ] Align shared CLI concepts (`RunSession`, `RunWorkspace`, `RunConsole`,
      `RunInventory`, `RunProfiler`) and Conformance adapter observation names
      where both ports have the concept.
- [ ] Remove this document and its reference in `AGENTS.md`.
