<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Agent handover

## Workspace

- Repository root: the directory containing this file.
- Portable C# Runtime: `implementation/csharp/GameEventScript.Runtime/src`.
- Portable C# Compiler: `implementation/csharp/GameEventScript.Compiler/src`.
- C# adapters: `implementation/csharp/GameEventScript.CSharpBridge/src`.
- Portable Conformance package:
  `implementation/csharp/GameEventScript.Conformance/src`.
- C# CLI tool: `implementation/csharp/GameEventScript.Tool/src`.
- C# native tests: each module’s `tests` directory; shared test support and
  repository/distribution gates: `implementation/csharp/verification`.
- Swift Runtime package: `implementation/swift/GameEventScriptRuntime`.
- Swift Compiler package: `implementation/swift/GameEventScriptCompiler`.
- Swift native adapters: `implementation/swift/GameEventScriptSwiftBridge`.
- Swift CLI package and `ges` executable: `implementation/swift/GameEventScriptTool`.
- Swift Conformance package and adapter: `implementation/swift/GameEventScriptConformance`.
- Shared executable corpus and fixtures: `conformance`.
- Normative language-neutral specifications: `specs`.
- Documentation entry point: `docs/README.md`.
- Deferred work: `BACKLOG.md`.

Standard verification:

```bash
dotnet test GameEventScript.sln --filter "TestCategory!=Performance"
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

Every authored public Swift declaration requires meaningful `///` documentation,
including enum cases, protocol requirements and bridge extensions. Document
lifetimes, failure behavior and conversion guarantees where relevant. The Swift
symbol-graph API gate rejects missing/empty comments and comments merely inherited
from another module; compiler-synthesized members are exempt.

Licensing follows `LICENSING.md`. Use exactly `Copyright 2026 Stephan Schlöpke`
and `SPDX-License-Identifier: Apache-2.0`, retain third-party notices, and honor
the documented generated, strict-format, and binary exclusions.

`scripts/clean.sh --dry-run` previews repository build-output cleanup;
`--artifacts-only` limits it to `artifacts`. It retains tracked files, skips
symlinked output directories and never traverses symlinks. Safety tests use
`python3 scripts/test-clean.py` with disposable workspaces.

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

The separate Compiler package depends only on Runtime. Runtime and CSharpBridge
must never depend on Compiler or Conformance. Shared value semantics, runtime
literal parsing, Program data and codecs remain in Runtime. Source-file I/O
belongs to the CLI or embedding; the portable compiler accepts source text.

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
- `:Dice[...]` is a deterministic literal of positive Int32 numeric rolls;
  `parse` recognizes the same data form and `as :Text` writes it in descending
  dice order. `:Dice(values)` remains a cast, and `roll dice NdM` draws random.
- Vector/Point text uses `:Vector(x: ..., y: ..., z: ...)` and `:Point(...)`.
  `parse` recognizes numeric positional or ordered labeled components with
  matching units and preserves kind, binary64 components, and unit on roundtrip.

The complete language contract is `specs/Language.md`; text and number details
are owned by `specs/Semantics/Text.md` and `specs/Semantics/Numbers.md`.

## Host and runtime

`GameEventScriptHost` is the autonomous serial execution unit. It may contain
only native handlers or any number of additively loaded Programs.

- Build creates a loading host. Register all initial Programs and native handlers,
  then call Start. Start runs initializations in Load order without ordinary
  message dispatch; the host becomes IsReady only if the complete group succeeds.
- Load on a ready host queues per-instance initialization in the normal FIFO.
  Instance.StartResult is absent while pending and retained after completion/failure.
  Failed init removes registrations, cancels captured deliveries to that instance,
  and discards its staged outputs. Other recipients and the ready host continue.
- Init emits retain enqueue order and wait for success; outbound publication waits for initial-group
  or later-instance success. PublishResult.OutboundDeferred reports staging.
  Ordinary Detach keeps its captured-snapshot semantics. See specs/HostRuntime.md.
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
`GameEventScript.Conformance` package and remain fileless, networkless,
synchronous, and test-framework-independent.

Conformance distinguishes exact numeric storage with `:Number.int64` and
`:Number.binary64`, including corresponding Quantity and Range variants. These
are transport variants, not source type names.

The C# CI gate verifies formatting, non-performance tests, the independent
zero-allocation hot path, release artifact consumption, and byte-identical
package reproduction. Performance references are regression gates for the
current C# implementation, not cross-platform benchmark claims.

Verified baseline (Release, 2026-09-21):

```text
2029/2029 non-performance test executions passed
32/32 allocation test executions passed, including the independent zero-allocation hot path
1517 shared Markdown Conformance cases in 94 documents
```

The combined verification command for the first two counts is:

```bash
dotnet test GameEventScript.sln --configuration Release --filter "TestCategory!=Performance|TestCategory=Allocation"
```

Elapsed-time benchmarks are a separate, profile-matched performance gate and
are not included in these counts.

## Swift port

The separate Swift Runtime package implements Program validation, `.gesb` V1
Reader/Writer, GESA dumping, serial host lifecycle, the register VM, private
random streams/scopes, extensions/external types, and runtime literal parsing.
Runtime remains independent of Conformance and Compiler. The separate
`implementation/swift/GameEventScriptCompiler` package depends only on Runtime
and implements source parsing, validation, lowering, optimization, register
allocation and Program generation. Conformance depends on both packages.
There is no extra Core package. File I/O belongs to the executable adapter/tests.

Every Swift target, including executable and test targets, declares a direct
target/product dependency for each repository module it imports. Transitive
availability alone is insufficient for reliable native incremental builds.
`python3 scripts/verify-swift-dependencies.py` checks imports against SwiftPM's
target/source descriptions; Swift CI runs this gate.

Swift Host and Compiler expose createBuilder()/create() factories matching the
C# builder workflow. Swift collections use immutable value semantics and copy-on-write storage.
Text and map-key equality/order use Unicode scalars, not Swift String's
canonical equivalence. Keep Int64 and Binary64 storage separate. Message and
Handler value storage is inline; VM entry borrows arguments by index rather
than allocating an argument array. Mutable callback arguments are borrowed only
for the synchronous invocation.

`./scripts/build-swift.sh` builds all SwiftPM packages independently in Release
without .NET; `--configuration debug` selects Debug. `./scripts/format-swift.sh`
checks the existing formatter configuration; `--fix` applies formatting.
Native SwiftPM build/test commands use `--disable-build-manifest-caching` to
rediscover added, renamed and removed sources in local dependency packages.
This regenerates build planning, not compiled objects. Keep the same option in
new native SwiftPM entry points. `python3 scripts/test-swift-incremental.py`
verifies source discovery and unchanged-build object reuse through the build script.
Run `./scripts/test-swift.sh` and `python3 scripts/verify-swift-api.py`.
The test script uses .NET 10 to export C#-compiled binary inputs below
`artifacts/swift/runtime-fixtures`, then verifies Swift in Release against the
original Markdown expectations. The manifest binds each input to its binary
and complete source-document SHA-256. No expected result is exported from C#.
`verify-swift-bytecode.py` checks the explicit enum and operand registry against
C#; ordinary package builds do not generate source.

Verified Swift coverage (Release, 2026-09-21): all 1,517 behavior checks from
94 shared Markdown documents pass with native Swift compilation. The strict
hardware-independent report passes 1,486 cases and skips 31 optional performance
measurements. Enabling the measured profile passes all 1,517 cases. Independent
Runtime verification passes 1,256 cases using C#-compiled Programs. Eighteen Conformance/adapter/bootstrap tests, seventeen SwiftBridge tests, and
twenty-eight CLI tests pass. `scripts/test-swift.sh` requires strict native acceptance and keeps
Runtime interoperability reports separate.

`scripts/test-swift-performance.sh` verifies the measured
`swift-6.4-release-macos26-arm64-m3max` profile. Five samples per workload use
median elapsed time and maximum cumulative requested libmalloc bytes on the
calling thread. Sixteen warmed dispatch cases measure zero bytes and allocation
counts. The native C instrumentation is an executable-only target; portable
libraries remain independent of measurement instrumentation. Controls qualify the counter before
measurement. Hardware/toolchain mismatches and failed controls are errors.
Baseline calibration and received Markdown preparation never overwrite corpus
references automatically; see `implementation/swift/Performance.md`.

`ConformanceEnvironment` advertises all Core capabilities and GESA snapshots.
Performance is opt-in and requires a profile plus measurement provider.
Its optional resource resolver accepts IDs and byte bounds; only the executable
adapter maps fixture paths to files. Native compilation itself needs no .NET.
Build products, symbol graphs, generated binary inputs and reports belong under
ignored `artifacts`. Public Swift API changes update `specs/PublicApi.md` and
`implementation/swift/api`. The separate `GameEventScriptTool` package implements
the native Swift `ges` CLI; it depends on Runtime and Compiler, never Conformance.
Foundation, filesystem/console I/O and POSIX terminal editing remain in that
executable package. `scripts/test-swift-tool.sh` verifies CLI adapters and real
process/PTY behavior without .NET; the full Swift test script includes it.
The GES/GESA grammars are embedded from the canonical TextMate files; refresh
them with `scripts/sync-swift-cli-grammars.py` and verify with `--check`.
`install-swift-tool.sh` builds and installs/updates `ges` in `$HOME/.local/bin`
or a selected `--tool-path`; the matching uninstall script removes only its
owned binary. The C# `dotnet ges` and Swift `ges` commands can coexist.

`GameEventScriptSwiftBridge` is an optional Runtime-only package for closure
handlers/publish sinks/extensions, strict native value conversions and explicit
KeyPath/getter/constructor bindings. Do not add a Compiler or Conformance
dependency to it. Compiler integration tests live in Conformance's native test
target; native Bridge tests live in the Bridge package. Run
`scripts/test-swift-bridge.sh` without .NET, or the full `scripts/test-swift.sh`.
The API gate and Xcode workspace include the Bridge.

Native conversion must reject numeric truncation, unit/kind loss and Swift
Dictionary collisions between scalar-distinct GES keys. Ordered message pairs
preserve signature labels; dictionary input requires an existing named signature.
External descriptor instances own executable bindings; Programs retain only
declarative data. The optional Swift Host runner takes exclusive ownership via
`sending` and uses a recursive lock. Both bridges wait for explicit Start on a
loading host, then schedule ordinary work on a shared background dispatcher.
Wrapping an already ready host schedules its pending work immediately. Runner lifecycle handles use its gate; raw Host
or callback state must not be accessed outside the transferred ownership domain.

## CLI

The C# `dotnet ges` and Swift `ges` tools provide `compile`, `check`, `run`, and
`dump`. The C# NuGet package remains `GameEventScript.Tool`; the installed command is
`dotnet-ges`, resolved by `dotnet ges` when the tool directory is on `PATH`.
`check` uses the complete compiler pipeline without writing a binary or executing
handlers.
`run` accepts jointly compiled sources or multiple `.gesb` files loaded in input
order. All Programs are loaded into one host before execution. By default it
drains initialization and then sends one `Main(args)` message, with a List of Text
values from `--arg`, `--args`, or the remainder after `--`. No implicit parsing
is applied. `--args` stops at the next option; `--` stops option parsing entirely.
`--scenario` replaces Main with a separately compiled GES scenario; `--interactive` replaces it with an explicit event console.
Those modes are mutually exclusive. Missing Main must not implicitly start a REPL.

The CLI registers native `ConsoleOut(...)`, `ConsoleErr(...)`, and `ErrorCode(code)`
handlers. ConsoleOut and ConsoleErr write ordered argument values to stdout and
stderr, respectively, concatenated without separators and followed by a newline.
ErrorCode sets a final script exit code from 0 to 255;
`nothing` resets it to 0. Last delivery wins; setting a code does not stop execution.
CLI/runtime errors and limits override the script code with failure code 1.
Diagnostics, status, and optional verbose event traces use stderr. No outbound
publish sink or custom extension/type registry is configured.

`--color` opts into ANSI output and interactive input highlighting; `NO_COLOR`
disables it. Interactive verbose event traces are yellow when color is enabled.
Live editing requires terminal input/output streams; redirected input
uses the plain line reader. Terminal editing and its dependencies belong only to
the CLI. Highlighting reuses the embedded GES and GESA TextMate grammars. The editor uses Ctrl+N
for a newline and Enter to submit; Shift/Alt+Enter depend on terminal modifier reporting.
The CLI decodes Ghostty's extended xterm and CSI-u Shift/Alt+Enter sequences before
the editor reads them; unrecognized or incomplete input remains unchanged at that boundary.
Interactive input uses the existing compiler with a temporary initialization handler; each input is pumped
and its Program detached. Local bindings do not persist between inputs.
`:help` documents console commands; `:help load` details additive loading. `:load <file>` adds one source or binary
Program and runs its initialization without calling Main. Failed read/compile/
decode/link operations preserve the existing session; runtime errors or limits
end it. An interactive session may start without initial program files.
`:list` lists persistent loaded Programs with stable CLI-local @IDs. `:handler`
lists registered script and native handlers, excluding completed initialization
and detached temporary inputs. `:dump <module|@ID>` uses the loaded Program's GESA
dump; `:source <module|@ID>` shows only embedded source documents, with a successful
notice if no source archive is present. Both support syntax colors via `--color`.
The editor and terminal source/dump displays use four-column tab stops. Tab
expansion is presentation-only; redirected output and saved GESA retain tabs.
Duplicate/anonymous modules can be selected by @ID. These inspection commands
write to stderr without executing handlers or re-reading files. Their inventory
belongs to the CLI and records only successful persistent loads/subscriptions.

`:unload <module|@ID>` detaches one Program; `:unloadAll` detaches all Programs.
Native console handlers, random state and script exit code remain. `:reload`
re-reads active Programs in their original source groups/load order on a fresh
host, preserving active IDs and never reusing detached IDs. It initializes the
complete group without calling Main, restarts a fixed seed and resets script exit
code. Preparation failures preserve the old session; runtime failures end it.
`:help reload` documents the lifecycle commands; `--quiet` hides their success reports.

Keep command parsing, file I/O, console observation, and process integration in
the tool. C# CLI adapter tests belong in `implementation/csharp/GameEventScript.Tool/tests`; Swift adapters are tested in
`GameEventScriptTool/Tests` and `scripts/test-swift-tool.py`. Portable language
and host semantics remain covered by shared Markdown. The command contract and examples
are documented in `implementation/csharp/GameEventScript.Tool/README.md`;
Swift installation and verification are documented in
`implementation/swift/GameEventScriptTool/README.md`.

## Documentation and backlog

`docs/README.md` is the canonical documentation index. Every document listed as
a specification there is normative. Normative documents describe only current
behavior and define each rule in exactly one owning document.

`BACKLOG.md` is the only list of deferred project work. Consult it when changing
adjacent architecture, implement an entry only when the user makes it part of
the current task, and remove the entry in the completing change.
