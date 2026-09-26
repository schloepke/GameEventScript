<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Changelog

User-visible changes and migration guidance are collected here before each
release. Entries under **Unreleased** are available on the development branch;
they are not yet included in a published package. Published release notes are
available on [GitHub Releases](https://github.com/schloepke/GameEventScript/releases).

## Unreleased

### Added

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

- SwiftPM distribution and SwiftBridge explicitly declare macOS 10.15, matching
  SwiftSyntax's minimum and fixing dependency planning with Swift 6.1.
- Swift highlighting preserves Unicode separators inside comments and GESA
  source-line strings without misclassifying subsequent assembler instructions.
- C# and Swift highlighting retain declaration keyword colors across line breaks
  and while declarations are incomplete.

- Swift CLI recognizes application-mode cursor keys used by Ghostty.
- C# CLI retains command history when switching between the ordinary prompt
  and delayed-message input handling.
