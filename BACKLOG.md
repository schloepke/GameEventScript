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
  Also evaluate importing `.gess` schemas as the source of record shapes, so
  the compiler checks field names, Tag sets and `:Flags` members (for example a
  misspelled `#enmy`) against them. Schemas cannot declare external functions
  and predicates; those stay in declaration modules.
- Offer a C# incremental source generator for `[GesExtension]` and `[GesType]` declarations, so that
  Unity IL2CPP builds need neither Reflection nor `link.xml` entries for annotated types, with the
  same rules as the Reflection registries and the Swift macros. Generated adapters would also lift the
  four-parameter limit of attributed functions and the need for value-type generic sharing at run time
  (`MakeGenericType`), which excludes NativeAOT and IL2CPP before Unity 2022 (specs/PublicApi.md).
- Bind selected schema parameters of generated code at run time: `ges schema generate
  --dynamic-param name` would turn only the constants that depend on the named parameters into
  fields of a generated binding value, created from `GesSchemaParameters` and checked against the
  layout; every other constant stays a literal. The `depends` lists of the generation layout
  (specs/Schemas.md#generation-layout) already name those constants. Measure the price against
  the interpreted writer and reader before adopting it; build it only on real demand.
- Provide a base for publish/subscribe transport of messages and data, outside Runtime, in a
  `GesNetwork` module or in `GesBridge`; decide which. It frames records, maps channels to message
  sets or schemas and carries the hash at connection setup (specs/Schemas.md#extension-points).
- Add schema elapsed-time references to the performance corpus: writer, reader and value path
  on Bot-sized records, with budgets per measured profile.
- Run the schema byte and vector suites, generated schema code (`ges schema generate --language csharp`)
  and an allocation measurement in a real Unity project: an IL2CPP player on arm64 and x64 and Mono
  in the editor, with the Generator DLL set in an `Editor` folder.
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
- A portable per-value size limit (Text scalars and List, Dice and Bytes items) checked by concatenation and
  conversion to Text, so that a host can run untrusted Programs without an outside memory bound. Runtime limits
  bound work and nesting today, not memory (specs/HostRuntime.md#memory).

## Language ports and distribution

- Implement the Kotlin port using the shared corpus and binary fixtures.
- Add further Swift performance profiles when supported CI hardware/toolchains
  are available. The initial Swift 6.4/macOS 27/Apple M3 Max profile is measured;
  keep timing regression runs separate from ordinary portable Conformance CI.
- Reduce Swift text-conversion temporary allocations when profiling justifies it;
  preserve the independently measured zero-allocation dispatch contract.
- Implement Go and Rust as additional planned language ports, preserving the
  same portable Core, `.gesb` format, and shared Markdown Conformance contracts.
- Add TypeScript/JavaScript as a target language of schema code generation
  (`ges schema generate --language typescript`): wire records and models with the
  same layout, byte-identical encoding and goldens as C# and Swift, checked by
  `compare-schema-generation.py` and compiled by `test-generated-schemas.py`.
  JavaScript numbers are binary64, so Int64 fields need `bigint` or a checked
  safe-integer range.
- Consider a GES-to-JavaScript transpiler that turns a Program into a
  JavaScript module with a small runtime for host, queue and values, verified
  against the shared Markdown Conformance; it must keep the VM's deterministic
  ordering, randomness and exact Int64 semantics.
- Add Godot support for schemas: a GDExtension that does wire, delta and
  document encoding natively (built on the Rust port, or on the C# port for
  Godot .NET projects, which can use the generated C# code directly), plus
  GDScript model generation (`ges schema generate --language gdscript --model`)
  with typed classes the extension fills. GDScript runs as interpreted bytecode,
  so a pure GDScript wire codec remains an optional later fallback for low
  rates; it would need byte-identical goldens like the other languages.
- Add an independent CI job with build, unit tests, strict shared Conformance,
  canonical `.gesb` fixtures and cross-language result comparison for every new
  implementation. A future C runtime additionally requires sanitizer jobs.
- Consider a `Sendable` Swift message snapshot type for forwarding between
  isolation domains without the JSON text round trip. `GesValue` and
  `GameEventScriptMessage` stay non-`Sendable` because External values are live
  host objects (specs/PublicApi.md#swift).
- Add Maven publication with the real Kotlin implementation; do not introduce
  empty package scaffolds.
- Consider a C runtime and optional thin C++ facade when gaming adoption or a
  later enterprise/embedded position justifies the native maintenance cost.
- A portable standard-extension library is not required for the 1.0 language
  baseline. Define one only after its contracts and shared Conformance cases
  exist; keep language-specific extensions with their implementations until then.
  The geometry library (`GesGeometryLibrary`, specs/PublicApi.md#geometry-library) is the first
  such library; follow its pattern of overlays in front of the host's own registries.

## Developer tooling

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
- Check whether IL2CPP contracts the C# Runtime's floating arithmetic, and keep products out of sums where it does. The VM
  computes `number + percentage` as `a + a * b`, distances as sums of squares and floating Range elements as `from + i * step`
  in single expressions, which a C++ compiler under `-ffp-contract=on` may fuse into one rounding, unlike .NET and Swift;
  specs/Semantics/Determinism.md requires separate operations. Generated computed-field code already avoids the shape
  (specs/Schemas.md#tools); measure on a device build before changing the hot path.
- After the Kotlin port is stable, build a dedicated IntelliJ plugin with native
  `.ges`/`.gesa` support beyond portable TextMate highlighting and `.region`
  folding. Keep the portable dump and TextMate bundles free of IntelliJ-specific
  markers in the meantime.

## Performance and optimizer follow-ups

- Scope-based register reuse for named locals: today every `let`, loop and `if let` binding pins its own register for the
  whole routine, so the frame grows with the number of bindings rather than with the values live at once (a generated
  handler with about 1,300 bindings exceeds the default 512 registers). Give named registers intervals from their first
  write to the end of their scope, extended across loop back edges, allocate them with the temporaries' linear scan in
  both compilers, keep debug symbols per range, and measure the performance corpus. Deferred because it changes every
  bytecode snapshot and should land with the instruction-format review before 1.0.

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
  Bridge where possible) build for WASI, with a CI gate that builds them.
  Syntax, Runtime and Compiler import no Foundation, which
  `scripts/verify-swift-portable-imports.py` enforces.
- Let the Compiler's program-wide validation rules read a source-neutral
  declaration set built from each source's syntax tree and from the external
  type catalog, instead of the semantic tree, so that external declarations and
  source declarations are checked by the same rules.
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
