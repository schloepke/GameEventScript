<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Agent handover

## Workspace

- Repository root: the directory containing this file.
- Portable C# Core: `implementation/csharp/src/StepH.GameEventScript`.
- C# adapters: `implementation/csharp/src/StepH.GameEventScript.CSharpBridge`.
- Portable Conformance package:
  `implementation/csharp/src/StepH.GameEventScript.Conformance`.
- C# CLI tool: `implementation/csharp/tools/StepH.GameEventScript.Tool`.
- C# tests: `implementation/csharp/tests/StepH.GameEventScript.Tests`.
- Shared executable corpus and fixtures: `conformance`.
- Normative language-neutral specifications: `specs`.
- Documentation entry point: `docs/README.md`.
- Deferred work: `BACKLOG.md`.

Standard verification:

```bash
dotnet test implementation/csharp/tests/StepH.GameEventScript.Tests/StepH.GameEventScript.Tests.csproj --filter "TestCategory!=Performance"
```

## Working rules

- When the user explicitly asks for analysis only, do not modify files.
- Before the first public 1.0 release, implement the current contract directly;
  add compatibility APIs only when the user explicitly requires them.
- Prefer language-neutral contracts that map cleanly to Swift, Kotlin, Go, Rust,
  C/C++, C#, and Unity.
- Keep Reflection, Attributes, `System.Type`, delegate-based adapters,
  unordered public dictionary-input adapters, locks, threads, Tasks, and other
  CLR conveniences in `CSharpBridge`.
- Portable Core, API, Runtime, Compiler, and Conformance code must remain
  synchronous and free of filesystem, network, thread, and async dependencies.
- Standard-library `TryGetValue`, `TryAdd`, and `TryParse` calls are accepted in
  portable C# code. Do not introduce public GES-level `Try...out` contracts.
- Use `rg` for source and file searches.
- Preserve unrelated user changes and never revert them without an explicit
  request.

Handwritten C# follows `implementation/csharp/CodeStyle.md` and the root
`.editorconfig`.

Every public C# type and member requires valid XML documentation. Missing or
malformed XML documentation is a build error and must not be hidden with
`#pragma`.

Licensing follows `LICENSING.md`. Use exactly `Copyright 2026 Stephan Schlöpke`
and `SPDX-License-Identifier: Apache-2.0`, retain third-party notices, and honor
the documented generated, strict-format, and binary exclusions.

Generated DLLs, NuGet packages, symbols, reports, and release candidates belong
only below the ignored `artifacts` directory. The release dry run must never
publish.

## Change synchronization

- Observable behavior changes update the implementation, shared Markdown
  Conformance cases, and the owning normative specification together.
- Source-syntax changes additionally update both TextMate grammars under
  `tools/editors`.
- Public API changes additionally update XML documentation,
  `specs/PublicApi.md`, and the approved API snapshot.
- Bytecode or `.gesb` changes additionally update the owning specification,
  validators, canonical and invalid fixtures, Golden bytes, and affected GESA
  snapshots.
- New portable behavior belongs in Markdown Conformance. Native C# tests are
  reserved for implementation details, adapters, repository gates, bootstrap
  behavior, and performance/allocation properties that cannot use the portable
  corpus as their sole oracle.
- Conformance assertions use stable phase/code and structured context, never
  English diagnostic text or implementation-only AST/optimizer shapes.

## Program, bytecode, and binary

`GameEventScriptProgram` is the immutable reusable Compiler result and the
portable parsed representation of `.gesb` V1. Its complete object graph may
contain only data that round-trips losslessly and language-neutrally through
`.gesb`. It never contains host bindings, registries, delegates, Reflection
objects, runtime caches, AST nodes, VM state, queues, or random state.

Compiler, Reader, Writer, and `Host.Load` share the complete Program validator.
Known Program data is defensively copied and exposed through immutable views or
read-only spans. Host-specific indexes, resolved extensions/types, and other
execution caches belong to `GesLinkedProgram`.

Bytecode instructions use explicit numeric words and payload bits. CLR struct
layout never defines `.gesb` encoding. The binary format is canonical Little
Endian and known V1 sections are uncompressed. Compilation includes
DebugSymbols, SourceMap, and SourceArchive by default; size-sensitive builds
opt out with `GameEventScriptDebugInfoOptions.None`.

The normative owners are `specs/ProgramModel.md`, `specs/Bytecode.md`,
`specs/BinaryFormat.md`, and `specs/AssemblerFormat.md`.

## Language and compiler

- Source types use PascalCase, for example `:Number`, `:Quantity(m)`, and
  `:Unit`. Extension namespaces and functions remain lowercase, for example
  `:math.distance`.
- Module identifiers are dot-separated lowercase components with optional
  digits.
- `constant $name be LITERAL` declares a program-wide compile-time scalar.
  Constants are inlined and do not enter Program segments or runtime state.
- Local declarations use only `let name be expression`. Conversions belong to
  the expression, for example `let value be input as :Number`.
- Only variable bindings may use a canonical `_number` suffix. Other names may
  contain digits without underscores according to their owning grammar.
- Calls are acyclic. Callable overload identity is name plus ordered external
  argument labels, never declared types. Functions and predicates cannot share
  a base name.
- Lexical bindings cannot shadow visible ancestor bindings. Sibling scopes may
  independently reuse a name.
- Script state is immutable. Mutable host-bound Tables are not part of the
  current language.

The complete language contract is `specs/Language.md`; text and number details
are owned by `specs/Semantics/Text.md` and `specs/Semantics/Numbers.md`.

## Host and runtime

`GameEventScriptHost` is the autonomous serial execution unit. It may contain
only native handlers or any number of additively loaded Programs.

- `Load(program, priority)` links host-specific imports, registers handlers,
  queues one initialization event for the instance, and returns an idempotently
  detachable `GameEventScriptInstance`.
- Native `Subscribe` returns an idempotently detachable
  `GameEventScriptSubscription`.
- Instance and subscription handles use stable host-local registration IDs and
  store no detach/unsubscribe closures.
- Each host owns one `GameEventScriptContext`, one logical-message ring queue,
  one random generator, and at most one lazily created reusable `GesVmState`.
- `GameEventScriptVirtualMachine` is a stateless executor. Program-specific
  dynamic links live in `GesLinkedProgram`.
- One logical message and all captured handlers complete before the next
  message. Script handlers may pause across frames; native handlers are atomic.
- `Receive` and `Emit` enqueue locally. `Publish` enqueues locally and then calls
  one optional synchronous `IGameEventScriptPublishSink`.
- Subscription snapshots are captured when a message is enqueued. Lifecycle
  changes affect subsequently enqueued messages.
- Dispatch order is descending priority followed by registration order.
- Core is threadless and unsynchronized. A host is serial but not thread-affine:
  one caller at a time is required, while a paused handler may resume on another
  thread after an embedding-provided happens-before handoff.
- Optional C# synchronization and automatic pumping live in
  `CSharpBridge/GameEventScriptCSharpHostRunner.cs`.

Hot-path VM mutation goes through `GesVmState.Set...` methods. Read-only register
borrows are accepted. Never hold a mutable destination reference across an
operation that may grow or replace register storage.

The normative host state machine and limits are defined in
`specs/HostRuntime.md`; deterministic ordering and equality are defined in
`specs/Semantics/Determinism.md`.

## Randomness

Each host creates and exclusively owns its random generator from an optional
seed or copied start sequence. Mutable generators are never shared between
hosts. Sequence exhaustion continues with the host's private PRNG.

Random scopes and boundary markers belong to the host generator. Native,
extension, and script-handler boundaries restore leaked or faulted scopes.
`MaxRandomScopeDepth` allows exactly that many regular scopes plus one internal
overpush gate, without exception-based normal control flow.

A statically unknown `random with` seed requires an explicit `as :Number`
conversion. An invalid runtime seed executes against a copied state whose
consumption cannot advance the restored parent stream.

## Messages, extensions, and external types

Message arguments are ordered `GameEventScriptMessageArgument` values. Argument
order and labels define the signature. Core has no tuple or dictionary factory;
C# conveniences live in `CSharpBridge`, and dictionary binding requires a known
signature.

Portable native handlers implement `IGameEventScriptNativeMessageHandler`.
Extensions receive the host's `GameEventScriptContext`. External types use a
declarative compiler catalog, a separate host runtime registry, and portable
`IGameEventScriptExternalValue` instances. CLR objects and Reflection-backed
implementations remain in `CSharpBridge`.

## Conformance and verification

The authoritative executable corpus lives under `conformance`. Markdown is the
only authoring format. The portable parser and runner live in the separate
`StepH.GameEventScript.Conformance` package and remain fileless, networkless,
synchronous, and test-framework-independent.

Conformance distinguishes exact numeric storage with `:Number.int64` and
`:Number.binary64`, including corresponding Quantity and Range variants. These
are transport variants, not source type names.

The C# CI gate verifies formatting, non-performance tests, the independent
zero-allocation hot path, release artifact consumption, and byte-identical
package reproduction. Performance references are regression gates for the
current C# implementation, not cross-platform benchmark claims.

Current clean-checkout baseline:

```text
1704/1704 non-performance test executions passed
1/1 zero-allocation hot-path test passed
36/36 performance/allocation executions passed
```

## Documentation and backlog

`docs/README.md` is the canonical documentation index. Every document listed as
a specification there is normative. Normative documents describe only current
behavior and define each rule in exactly one owning document.

`BACKLOG.md` is the only list of deferred project work. Consult it when changing
adjacent architecture, implement an entry only when the user makes it part of
the current task, and remove the entry in the completing change.
