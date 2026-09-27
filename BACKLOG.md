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

- Reconstruction of executable host-bound values is deferred
  and is not required for 1.0. Revisit only for a demonstrated application need;
  portable literals and JSON already reconstruct data, and external snapshots
  become Records. See the [language scope review](docs/guide/distribution/Packages.md#language-scope-review-for-10).

## Language ports and distribution

- Implement the Kotlin port using the shared corpus and binary fixtures.
- Add further Swift performance profiles when supported CI hardware/toolchains
  are available. The initial Swift 6.4/macOS 26/Apple M3 Max profile is measured;
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

- Design a single implementation-independent Conformance orchestrator with thin
  language adapters, so corpus parsing, assertions and reporting do not need to
  be ported for each runtime. Keep native allocation and platform integration
  checks in their implementation-specific harnesses.

- Add Homebrew installation for the standalone CLI downloads and configure
  Developer ID signing/notarization and Windows Authenticode signing before
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

- In a more mature multi-runtime state, design a benchmark system with
  representative multi-program workloads, separated
  compile/load/message/VM measurements, native harnesses per language and a
  documented build-host/toolchain calibration index instead of comparing raw
  timings from unrelated machines. Existing C# and Swift profile measurements
  remain implementation regression gates, not cross-platform performance promises.
- Add bounded fuzzing for the `.gesb` reader and property-based Reader/Writer
  tests without weakening the existing canonical and malformed fixture corpus.
- Improve CFG/liveness-based register allocation and reuse of non-overlapping
  locals.
- Measure whether the bytecode optimizer passes produce meaningful changes with
  the compiler's direct register assignments.
- Revisit Message/Emit allocation only when measurements show a material hot-path
  benefit.
- Revisit a more VM-near extension-call model if boxing at the extension boundary
  becomes material again.
