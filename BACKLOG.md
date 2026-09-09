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

## Before the first public package release

- Decide whether public product, type, namespace, assembly, documentation and
  package names should use the neutral `Ges`/`GES` form so that the acronym can
  represent Game Event Script or a later product position such as Guided Event
  Script.
- Register or select a GES-specific domain and transferable publisher namespace;
  align NuGet, Maven, SwiftPM, GitHub and future organization ownership.
- Finalize package IDs, publisher identity, repository visibility and release
  signing before enabling `NUGET_PUBLISH_ENABLED` or any other public registry
  publication.
- Enable branch protection and require `C# CI / verify` when the repository is
  public or the GitHub account supports protection for private repositories.

## Bytecode and portable format

- Redesign numeric and other constant pools together with immediate operands and
  measure compact instruction layouts, including forms such as arithmetic with a
  directly addressed constant. Start this work with the first Swift port rather
  than optimizing the current C#-only representation for hypothetical consumers.
- Stabilize the next bytecode and binary boundary with canonical fixtures before
  making it the input to additional runtimes.
- During the first Swift port, allow an explicit, mechanically checked duplicate
  opcode and format description long enough to expose the real commonalities and
  language-specific differences. Based on that evidence, decide whether a
  maintainable central tabular definition is beneficial; only then generate
  checked-in language sources and documentation and add a CI drift gate. Normal
  product and IDE builds must never require the generator.
- Specify and implement optional `.gesb` compression codecs separately; V1 only
  reserves the codec bits and emits known sections uncompressed.
- Specify signatures, certificates or keys, trust policy and rollback behavior
  separately; V1 only reserves the security section range and provides no
  authenticity guarantee.

## Language and state

- Implement the agreed [Text and number conversion contract](specs/Semantics/Numbers.md#text-and-number-conversion)
  in the compiler/runtime and shared Markdown corpus. Cover decimal/exponent
  input, underscore separators, strict text-only comma grouping, exact Int64
  parsing, Binary64 rounding boundaries, Quantity suffixes, and Percentage
  scaling/roundtrip. Replace Percentage cast magnitude/storage heuristics with
  the uniform ratio rule, add direct Text-to-Percentage conversion, and verify
  exact Number/Text roundtrips at zero, negative values, `100%`, and above.
  Centralize C# numeric text reading and writing in an internal static
  `TextNumberCast` class, shared by compiler literal decoding, constant folding,
  and runtime casts. Number and Percentage decoding must use the same numeric
  conversion rules while preserving source token boundaries, target value kinds,
  and the distinction between compiler diagnostics and invalid runtime casts.
  Adapt existing numeric-text assertions to
  permit the specified output variants while preserving exact ordinary Text
  comparison. The contract was specified before implementation; the current C#
  parser and corpus do not yet establish conformance to it. Select platform
  conversion routines or a dedicated implementation based on correctness and
  measured allocation/performance results.
- Implement the agreed [parse expression](specs/Language.md#parse-expression),
  [literal recognition](specs/Semantics/Text.md#literal-recognition-from-text),
  and [data-value text output](specs/Semantics/Text.md#text-output-of-data-values)
  in the compiler/runtime and shared Markdown corpus. These contracts were
  specified before implementation; the current parser and corpus do not yet
  establish conformance to them. Finalize the accepted data-only literal subset,
  non-Text-input behavior, and portable input/nesting/item limits before coding.
  Start with independent scalar values and recursively nested Lists/Maps;
  handle Handler, Series, record reconstruction, and external-value contracts
  separately. Handler signatures and built-in Series kind/offset are data and
  can receive literal representations without capturing executable host state.
  Reuse numeric conversion and source literal rules without evaluating script
  expressions or constructing a runtime Compiler/Program. Compare a direct
  value reader sharing scanner/decoder routines with a literal-only entry point
  in the existing parser; the existing parser builds AST/source metadata that
  runtime parsing does not need. Align exponent/special-value recognition with
  the numeric writer while keeping commas structural in literal containers.
  Enter parsing through a dedicated bytecode operation, with no parser setup,
  persistent parser state, or additional parse-mode checks on ordinary VM
  instructions. Verify existing scripts without `parse` against unchanged
  performance/allocation gates, and measure parsing/formatting separately.
  Add Markdown coverage for plain Text, explicitly quoted numeric/Boolean-looking
  Text, doubled quotes, malformed quotes/containers, complete input consumption,
  exact original-Text fallback, and successful `nothing` recognition. Verify
  root Text identity and nested Text quoting independently, including mixed
  lists such as `[1, "1"]`. Preserve the exact numeric roundtrip assertions;
  ordinary script `=` is not a sufficient oracle for exact kind, unit, and
  nested structure because it has numeric coercion/tolerance and propagates
  `nothing`. Preserve the Boolean truth-view and Text-to-List scalar-expansion
  casts. Synchronize bytecode/binary validators and fixtures, both editor
  grammars, and the shared Markdown corpus when implementing the contract.
- Add a general immutable collection `fold`/`reduce` operation if concrete use
  cases exceed the existing specialized aggregations (`sum`, `average`, `min`,
  `max`, and `count`). Prefer a bounded collection operation over recursion or
  general local mutation.
- Design host-bound Tables as the explicit mutation model. Mutations should
  enter a deterministic modification queue; snapshot visibility,
  read-your-writes, commit boundaries, rollback, observation, persistence and
  replication remain to be specified.
- Finalize the product wire envelope independently of the already stable
  Conformance transport, retaining ordered message argument arrays.

## Language ports and distribution

- Implement Swift as the first additional language port and Kotlin as the next
  port, using `.gesb` fixtures and the shared Markdown Conformance corpus for
  differential acceptance against C#.
- Add an independent CI job with build, unit tests, strict shared Conformance,
  canonical `.gesb` fixtures and cross-language result comparison for every new
  implementation. A future C runtime additionally requires sanitizer jobs.
- Add SwiftPM and Maven publication only with their real implementations; do not
  introduce empty package scaffolds.
- Consider a C runtime and optional thin C++ facade when gaming adoption or a
  later enterprise/embedded position justifies the native maintenance cost.
- Define a portable standard-extension library only after its contracts and
  shared Conformance cases exist; keep language-specific extensions with their
  implementations until then.

## Unity and editor integration

- Validate the staged C# DLL set in a real Unity project with the selected
  scripting backend and API compatibility level, then add Compile- and
  PlayMode-smoke tests to CI for the extracted Unity package.
- Develop Unity editor integration, prepared MonoBehaviours and an installable
  Unity package in a real Unity project before extracting reusable integration
  sources into this monorepo.
- Consider development-only hot reload as an embedding feature without moving
  threading, filesystem or Unity dependencies into portable Core.
- After the Kotlin port is stable, build a dedicated IntelliJ plugin with native
  `.ges`/`.gesa` support beyond portable TextMate highlighting and `.region`
  folding. Keep the portable dump and TextMate bundles free of IntelliJ-specific
  markers in the meantime.

## Performance and optimizer follow-ups

- Treat the current performance and allocation tests as regression gates against
  the established C# baseline. In a more mature multi-runtime state, design a
  real benchmark system with representative multi-program workloads, separated
  compile/load/message/VM measurements, native harnesses per language and a
  documented build-host/toolchain calibration index instead of comparing raw
  timings from unrelated machines.
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
