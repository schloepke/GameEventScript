<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Changelog

User-visible changes and migration guidance are collected here before each
release. Entries under **Unreleased** are available on the development branch;
they are not yet included in a published package. Published release notes are
available on [GitHub Releases](https://github.com/schloepke/GameEventScript/releases).

## Unreleased

### Added

- Added generated GES/GESA Sublime Text and bat grammars, TextMate/Sublime CLI
  actions, and shared syntax-only completions and snippets with website downloads.

- Published the release version policy: compatible fixes in 0.x patches, features
  and contract changes in 0.x minor releases with migration notes, and Semantic
  Versioning for the declared compatibility surface from 1.0 onward.

- Defined source and binary compatibility within major release lines from 1.0,
  including message/conversion guarantees, recompilation exceptions for random
  and opcode-budget consumption, and no guaranteed support for pre-1.0 binaries.

- Recorded the current event-driven language as the 1.0 baseline after reviewing
  remaining language work; host-bound reconstruction and a portable
  standard-extension library are not required for that baseline.
- Recorded the C# and Swift embedding API baseline and source-API compatibility
  rules, including intentional native-language differences and API snapshot gates.

### Changed

- Clarified application ownership of state mutation and persistence, including
  native handler and extension effects, external-value stability and failure
  recovery. Existing runtime behavior is unchanged.

## [0.2.0] — 2026-09-26

This development release expands the language, native integration and tooling.
C# and Swift share the same release version. Compatibility may change during 0.x;
see the migration notes below before upgrading from 0.1.0.

### Added

- Host idle-duration observations in C# and Swift, including synchronized Bridge
  runner access, for application-defined inactivity recovery without host timers.

- Website publication triggers the hosting pull webhook after changed output is
  successfully pushed to `site`, with bounded retries and a secret-backed URL.

- Standalone CLI release archives: C# for Windows, Linux and macOS; Swift for
  Linux and macOS; x64 and ARM64 for each. Downloads include runtime dependencies,
  license notices, release identity and checksums, with native relocation tests
  and an explicitly triggered GitHub Release upload.

- Pixel-Duo icons embedded in the NuGet library packages and local .NET CLI package.

- Root SwiftPM test targets reuse native SwiftBridge, macro and highlighter tests,
  making them discoverable by Swift Package Index and runnable with `swift test`.

- Generated C# DocFX and Swift DocC API references in the documentation website,
  with source identity checks, Swift Package Index metadata and compatibility badges.

- Informative feature-version notes in specifications and guides, distinguishing
  the published baseline from unreleased behavior.

- Pixel-Duo website identity with a shared blue/teal palette, repository-owned
  brand sources, and Mermaid diagrams for host startup and dispatch.

- Downloadable TextMate JSON and classic XML editor bundles for GES/GESA in the
  website's Tools section, generated from the canonical repository grammars.

- English language, C# and Swift introductory guides, plus a static documentation
  website with a landing page, local search and the existing normative references.
- Website CI verifies pull requests and publishes successful main builds to a
  separate `site` branch for pull-based deployment by existing webhosting.

- Standalone C# and Swift SyntaxHighlighter libraries, shared with both CLIs,
  using canonical TextMate rules. They expose UTF-16 ranges, semantic categories,
  scope stacks, incremental line states and an optional ANSI renderer.

- SwiftBridge annotations `@GesType`, `@GesField` and `@GesConstruct` generate
  native bindings, with inferred field types and explicit numeric units.

- Explicit portable type data forms, including Range, built-in Series, Handler,
  Message and immutable Record snapshots; `parse` reconstructs them and can run
  known script Record constructors after complete input recognition.
- Text splitting with `[:split on separator]` and `[:split on whitespace]`.
- Explicit Runtime-only V1 product JSON codecs in C# and Swift, preserving
  message argument order and exporting external values as Record snapshots.
- C# and Swift support conditional bindings with `if let`, including multiple
  bindings and conditions separated by semicolons. Checks short-circuit from
  left to right; successful bindings are available to subsequent checks and
  the then body.
- Collection selectors `[:fold acc be seed, value => expression]` and
  `[:reduce acc, value => expression]` accumulate values in iteration order.
  An empty fold returns its seed; an empty reduce returns `nothing`.
- `emit` and `publish` can return a Boolean indicating whether the host accepted
  the send. This does not guarantee delivery or outbound sink acceptance.
- `emit after duration Message(...)` and `publish after duration Message(...)`
  schedule messages using time quantities, including fractional seconds.
  Delayed publication invokes the outbound sink only when due.
- Hosts accept an injectable monotonic clock, expose `NextMessageDelay` /
  `nextMessageDelay`, and return `Waiting` / `.waiting` when only future work
  remains. Native bridge runners schedule wakeups automatically.
- CLI batch runs wait for delayed messages. Interactive sessions process due
  messages while preserving unfinished input and command history.
- Shared Markdown Conformance supports virtual-clock advancement and waiting
  assertions, with coverage for the new language and scheduling behavior.

### Changed

- Website navigation stays visible on desktop landing pages; mobile landing and
  documentation headers scroll with the page to leave more room for content.

- Record, Range, Series, Handler and Message text now uses reconstructible data
  forms. Integral Binary64 range inputs normalize to exact integer ranges.
- GESB V1 adds ConstructData and SplitText; older readers reject these opcodes.
- **Source compatibility:** every `if` header and then body share one local
  scope; else has a separate sibling scope. Bindings from either branch never
  escape the `if`, including unbraced bodies. For a binding needed afterward,
  use `let value be expression when condition otherwise nothing`.
- **Embedding:** `RunToCompletion()` / `runToCompletion()` never sleeps. A
  custom event loop must handle `Waiting` / `.waiting` and pump again when the
  next message is due. Delayed messages count toward the queue limit and keep
  `IsIdle` / `isIdle` false.
- **Bytecode compatibility:** four result-bearing send opcodes extend GESB V1.
  The original statement-send encodings remain unchanged. Programs using the
  new opcodes require an updated Runtime; older validators reject them.

### Fixed

- Website deployment uses the POST method required by Plesk webhooks and reports
  HTTP status or connection failure categories without exposing the webhook URL.

- SwiftPM distribution and SwiftBridge explicitly declare macOS 10.15, matching
  SwiftSyntax's minimum and fixing dependency planning with Swift 6.1.
- Swift highlighting preserves Unicode separators inside comments and GESA
  source-line strings without misclassifying subsequent assembler instructions.
- C# and Swift highlighting retain declaration keyword colors across line breaks
  and while declarations are incomplete.

- Swift CLI recognizes application-mode cursor keys used by Ghostty.
- C# CLI retains command history when switching between the ordinary prompt
  and delayed-message input handling.

### Migrating from 0.1.0

- Upgrade all GES library packages used by an application together to 0.2.0.
  SwiftPM consumers use the `0.2.0` tag. Syntax highlighting is an optional new
  product; add it only to targets that use it.
- Move bindings needed after an `if` outside its branches. Neither braced nor
  unbraced branch bindings escape; conditional expressions with
  `when ... otherwise ...` can initialize an outer binding.
- Custom host pumps must handle `Waiting` / `.waiting` and resume when
  `NextMessageDelay` / `nextMessageDelay` is due. `RunToCompletion()` /
  `runToCompletion()` does not wait for future messages; bridge runners and CLI
  batch execution handle this waiting for their callers.
- Deploy the updated Runtime before using binaries containing the new data,
  splitting or result-bearing send opcodes. The GESB format remains V1, but
  0.1.0 validators reject instructions they do not support.
- Update consumers that depend on the previous Text spelling of Records, Ranges,
  Series, Handlers or Messages. These values now use explicit reconstructible
  data forms; ordinary top-level Text remains unchanged. Integral Range inputs
  normalize to exact integer arithmetic even when supplied as Binary64.
- Product JSON is an explicit transport API. External values arrive as immutable
  Record snapshots, not native objects; decoding does not invoke constructors or
  restore host bindings. Local message delivery continues to use values directly.

## [0.1.0] — 2026-09-21

First public development release of the immutable, event-driven core.

- Published Runtime, Compiler and native Bridge libraries for C# on NuGet and
  Swift through one versioned SwiftPM package.
- Verified C# and Swift against the shared Markdown Conformance corpus, including
  portable GESB V1 program exchange and deterministic execution.
- Included serial Hosts, explicit startup, multiple loaded programs, native
  handlers, extensions and external types, with execution limits.
- Provided source-installed development CLIs for compilation, validation,
  execution, binary inspection and interactive use.
- Documented installation and the pre-1.0 compatibility policy.

[0.2.0]: https://github.com/schloepke/GameEventScript/compare/0.1.0...0.2.0
[0.1.0]: https://github.com/schloepke/GameEventScript/releases/tag/0.1.0
