<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Game Event Script backlog

This is the canonical list of intentionally deferred or upcoming project work.
The backlog is informative: an entry is implemented only when the user makes it
part of the current task.

When an item is completed, remove it from this file in the same change and
record the resulting contract in the owning specification. Keep detailed design
decisions in specifications or dedicated plans and keep only a concise pointer
here.

## Bytecode and portable format

- Before 1.0, evaluate and select the instruction format using measured C# and
  Swift results: either retain fixed 16-byte instructions, optionally with
  constant pools, or use an 8-byte base word with zero to three additional
  8-byte payload words (8/16/24/32-byte instructions, with a two-bit payload
  count). Evaluate constant pools and directly addressed constants alongside
  both layouts. Compare execution time, runtime allocations, register pressure,
  resident Program memory and encoded code size on representative workloads;
  payload words are data, not separately budgeted opcodes. Implement and verify
  the selected changes before 1.0, or explicitly retain the current layout with
  the evidence and future binary compatibility implications recorded. Review
  addressing, validation, public Program/codec APIs and fixtures as part of any
  redesign. The format review is required for 1.0; a redesign is not predetermined.
  Tracked as a 1.0.0 prerequisite in [issue #32](https://github.com/schloepke/GameEventScript/issues/32).
- Stabilize the next bytecode and binary boundary with canonical fixtures before
  making it the input to additional runtimes.
- Evaluate a central tabular opcode/format definition using the existing C# and
  Swift descriptions and the mechanical `scripts/verify-swift-bytecode.py` gate.
  If generation is beneficial, generate checked-in language sources and
  documentation and verify their freshness in CI. Normal product and IDE builds
  must never require the generator.
- Specify and implement optional `.gesb` compression codecs separately; V1 only
  reserves the codec bits and emits known sections uncompressed.
- Specify signatures, certificates or keys, trust policy and rollback behavior
  separately; V1 only reserves the security section range and provides no
  authenticity guarantee.

## Language

- Declare external types and extensions in source and import them, so Programs
  compile without a host catalog and bind only at `Host.Load`. A
  `declaration module gamedata` contains only external declarations, for
  example `external record :Unit as { id: :Text, mount: :Tag }` with its
  constructor signatures and `external function :nav.shortestTurn(from:, to:)`;
  it produces no bytecode. Scripts make them visible with `import gamedata`;
  importing script modules with code is out of scope. Declarations become
  strict, but binding through the host stays available so game scripts can
  stay small; alternatively a game adds a standard import script. The Program
  stores the used declarations and `Host.Load` checks them against the
  registry and extensions. Decide whether `WithExternalTypeCatalog` remains as a
  convenience, and whether the Bridges generate declaration modules. Schedule
  after the Syntax module rework, on its source-neutral declaration set.
- Validate GES documents against schemas: `.gess` files hold record declarations
  and a later reading operation receives the available schemas (from a Program or
  loaded `.gess` files) to construct and check the root record, with document
  positions for missing, unknown or mistyped fields, no implicit unit casts,
  rejected computed fields and a validation option for `clamped` violations.
  Decide whether record fields need an explicit optional marker. The header
  format in specs/Documents.md already admits this; printing a schema prints
  record declarations.
- Explore computed handler bindings, for example `on Main(value computed by
  parseArguments(input: args), context) { ... }`. The message signature remains
  `Main(args, context)`, while only `value` and `context` are visible in the body.
  Permit only a direct function or Record constructor call with simple input
  parameters; operators, pipelines and nested calls belong inside that function
  or Record. Input names define the message arguments in order, independently
  of the called function's argument labels. Transformation is local to each
  handler and must not change the arguments delivered to other receivers.
  Finalize duplicate-name, positional-argument and result semantics before
  implementing compiler lowering and shared Conformance coverage.
- Add constructor-only Record inputs using `parameter` inside the existing
  field list, for example `record :Arguments as { parameter input: :List,
  name: :Text, parsed: :List computed by input[:select x => parse x when x is
  :Text otherwise x] }`. Parameters participate in constructor argument order,
  labels and type conversion, but are available only during construction and
  are not stored as Record fields. Ordinary fields remain stored constructor
  inputs; computed fields remain stored derived values. Preserve declaration
  order and the existing `_` convention for positional arguments. Avoid redundant
  moves or casts for direct field initialization; verify the generated bytecode.
  Finalize the contract and implement it in C# and Swift with shared Conformance
  coverage and editor grammar updates when this work is scheduled.
- Reconstruction of executable host-bound values is deferred
  and is not required for 1.0. Revisit only for a demonstrated application need;
  portable literals and JSON already reconstruct data, and external snapshots
  become Records. See the [language scope review](docs/guide/distribution/Packages.md#language-scope-review-for-10).

## Language ports and distribution

- Implement the Kotlin port using the shared corpus and binary fixtures.
- Add further Swift performance profiles when supported CI hardware/toolchains
  are available. The initial Swift 6.4/macOS 27/Apple M3 Max profile is measured;
  keep timing regression runs separate from ordinary portable Conformance CI.
- Reduce Swift text-conversion temporary allocations when profiling justifies it;
  preserve the independently measured zero-allocation dispatch contract.
- Implement Go and Rust as additional planned language ports, preserving the
  same portable Core, `.gesb` format, and shared Markdown Conformance contracts.
- Add an independent CI job with build, unit tests, strict shared Conformance,
  canonical `.gesb` fixtures and cross-language result comparison for every new
  implementation. A future C runtime additionally requires sanitizer jobs.
- Add Maven publication with the real Kotlin implementation; do not introduce
  empty package scaffolds.
- Consider a C runtime and optional thin C++ facade when gaming adoption or a
  later enterprise/embedded position justifies the native maintenance cost.
- A portable standard-extension library is not required for the 1.0 language
  baseline. Define one only after its contracts and shared Conformance cases
  exist; keep language-specific extensions with their implementations until then.

## Developer tooling

- Add `.gesd` document support to the editor packages (TextMate/Sublime grammars,
  highlighter cases) and CLI commands to check and format documents.
- Design a single implementation-independent Conformance orchestrator with thin
  language adapters, so corpus parsing, assertions and reporting do not need to
  be ported for each runtime. Keep native allocation and platform integration
  checks in their implementation-specific harnesses.

- Configure Developer ID signing/notarization and Windows Authenticode signing before
  promising platform-verified publisher identity.
- Consider optional NuGet author signing when an independent publisher signature
  is needed. Current publication uses Trusted Publishing and NuGet.org repository
  signing; author signing is not a prerequisite for releases. It requires a
  trusted code-signing certificate, secure signing infrastructure and verification
  after reproducible package preparation.

## Unity and editor integration

- Validate the separately staged C# DLL distribution in a real Unity project and
  add Compile- and PlayMode-smoke tests when extracting a Unity package. Public
  NuGet consumption has already been exercised in Battle Club on physical iOS
  (IL2CPP) and Android devices; that does not verify the staged DLL import path.
- Develop Unity editor integration, prepared MonoBehaviours and an installable
  Unity package in a real Unity project before extracting reusable integration
  sources into this monorepo.
- Consider reusable Unity editor hot-reload integration when extracting the
  editor package. Existing CLI `:reload` and the consuming Swift application's
  editor already reload programs through the existing host lifecycle. Keep file watching, threading and Unity dependencies in adapters.
- After the Kotlin port is stable, build a dedicated IntelliJ plugin with native
  `.ges`/`.gesa` support beyond portable TextMate highlighting and `.region`
  folding. Keep the portable dump and TextMate bundles free of IntelliJ-specific
  markers in the meantime.

## Performance and optimizer follow-ups

- Investigate the Swift/C# execution-time gap observed with release 0.3.0 on
  the same machine and the same precompiled `compute-benchmark.gesb`. User
  measurements with `hyperfine -w 10 -r 100` reported mean elapsed times of
  76.5 ms for locally built Swift, 86.8 ms for the Homebrew Swift binary, and
  33.9 ms for the directly invoked .NET AOT CLI (`dotnet-ges`). These are
  end-to-end observations, not isolated VM timings or general language claims.
  Separate startup, loading/validation and execution using native harnesses
  that load once and execute repeatedly; verify matching results and executed
  opcode counts. Profile Swift dispatch, register access, value copies, ARC and
  temporary allocations against C# before selecting optimizations. Record exact
  hardware, toolchains and build flags; investigate local/CI Swift build
  differences separately. Preserve language semantics and shared Conformance,
  and qualify improvements with the existing performance/allocation gates.
  Deferred analysis only; no optimization or toolchain migration is scheduled.
- In a more mature multi-runtime state, design a benchmark system with
  representative multi-program workloads, separated
  compile/load/message/VM measurements, native harnesses per language and a
  documented build-host/toolchain calibration index instead of comparing raw
  timings from unrelated machines. Existing C# and Swift profile measurements
  remain implementation regression gates, not cross-platform performance promises.
- Improve CFG/liveness-based register allocation and reuse of non-overlapping
  locals.
- Measure whether the bytecode optimizer passes produce meaningful changes with
  the compiler's direct register assignments.
- Revisit Message/Emit allocation only when measurements show a material hot-path
  benefit.
- Revisit a more VM-near extension-call model if boxing at the extension boundary
  becomes material again.
- Precompute constant approximations (`ln`, `exp`, non-integer powers) once at
  program load on the target platform, for example as values in
  `GesLinkedProgram`, instead of evaluating them on every execution. The
  compiler would mark such constant expressions instead of folding them; this
  touches bytecode and `.gesb`. Pursue only if run-time evaluation of constant
  approximations becomes measurable.
- Make all Swift library modules (Runtime, Compiler, Syntax, and the
  Bridge where possible) build for WASI, with a CI gate that builds them and
  rejects new Foundation imports in the portable modules. Runtime and Compiler
  no longer import Foundation.
- Base the compiler front end and highlighting on a lossless syntax tree in the
  Syntax module (in progress on `feature/syntax-modul`). This drops the Swift
  highlighter's Foundation dependency (`NSRegularExpression`, `NSString`,
  `JSONSerialization`), so WASM consumers such as the card game (about 67 MB,
  mostly unused Foundation) stop shipping it. Syntax, Runtime and Compiler end
  without Foundation or package dependencies; the Bridges and the CLI may keep
  theirs. Compile time may grow by at most 10 % against
  `scripts/benchmark-compile.py`, with unchanged VM time and allocations.
  - Done: the SyntaxHighlighter packages are renamed to Syntax; the compile
    benchmark records the baseline. Unit table, text and name rules and the
    lossless lexer live in Syntax, Runtime builds on it, and
    `scripts/test-syntax-roundtrip.py` checks both ports' token streams. The
    allocation-free runtime literal parser keeps its own implementation.
  - Done: both parsers also build the lossless syntax tree (Syntax module
    model and builder); `test-syntax-roundtrip.py --trees` checks token
    coverage, nesting and equal tree shapes for valid sources in both ports.
  - Done: the parser lives in Syntax and keeps of each expression only its
    nesting depth, range and literal shape; a Compiler binder in both ports
    builds the semantic tree from the syntax tree. Before the old parsers were
    removed, both pipelines produced identical semantic trees, diagnostics and
    syntax trees on the whole corpus and on generated nesting-limit sources.
    Syntax diagnostics live in Syntax and the Compiler converts them. Next:
    trees of sources with syntax errors become equal across ports with the
    shared recovery.
  - Parser: builds the immutable lossless tree with recovery, missing and
    skipped tokens and error nodes carrying one diagnostic each; a binder in
    the Compiler produces the existing semantic tree, so optimizer, lowering
    and Programs stay byte-identical. Keep the stack-safe recursion structure.
  - Validator: single-source checks (naming, duplicates, shadowing, constants,
    labels, nesting) move into Syntax; checks across sources and catalogs stay
    in the Compiler and read a source-neutral declaration set, so external
    declarations can later feed it. Both attach diagnostics to nodes.
  - Highlighter: categories and diagnostics per span, derived from the tree; no
    TextMate concepts, line states or scope stacks. GESA gets a small lossless
    lexer that highlights embedded source through the GES tree. The CLI moves
    onto the Syntax module. Public API: parse results, diagnostics and
    highlighting; node details via `InternalsVisibleTo`/`@_spi(Compiler)`.
  - Then rework `specs/SyntaxHighlighting.md` and `conformance/highlighting`,
    add `specs/Syntax.md` and a lossless roundtrip over the whole corpus, and
    compare highlighting performance and the card-game module size.
- Generate the TextMate editor bundles from the Syntax module's token and node
  classification instead of maintaining the grammars separately.
- Reduce library and binary size without noticeable performance loss now that
  the C#/Swift port convergence is complete. Its first phase grew the
  release Swift `ges` binary by about 175 KB (3.35 → 3.52 MB, `__TEXT` +16 KB)
  and its peak resident memory by about 110 KiB (+1.5%) on the compute
  benchmark, while instructions grew only 0.14%. Likely contributors are the
  new Program segment types with `RandomAccessCollection` conformances and the
  generated `GesDiagnosticCodes`. Measure per-module size contributions in both
  ports first; for context, the Lua runtime peaks at about 1.9 MiB on the same
  benchmark versus 7.8 MiB for Swift `ges`.
- After the compiler convergence, compact the C# compiler toward the Swift
  implementation at unchanged function and byte-identical output. Measured in
  characters without whitespace and comments (2026-10-16), C# is about 2.4x the
  size of Swift for rule-for-rule identical code (GesAstOptimizer) and 6–7x for
  the lexer, validation and API. Check first whether C# implements extra
  behavior there, for example collecting several validation diagnostics.
