<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Changelog

User-visible changes and migration guidance are collected here before each
release. Entries under **Unreleased** are available on the development branch;
they are not yet included in a published package. Published release notes are
available on [GitHub Releases](https://github.com/schloepke/GameEventScript/releases).

## Unreleased

### Breaking

- A number directly followed by a unit spelling is now one quantity literal, so
  `10m/s`, `6m/s²` or `120°/s` are a speed, an acceleration and an angular speed
  rather than divisions by a binding `s`. Write `10m / s` with whitespace to
  divide. Text such as `'250ms'` now parses as `0.25s` instead of failing.
- `floor`, `ceil`, `truncate` and the three `round` forms keep the unit of a
  quantity, at runtime and in constant folding: `floor 2.5m` is now `2m` instead
  of the unitless `2`, and `round half even 2.5m/s` is `2m/s`. Percentages still
  round to unitless numbers. A rounded time such as `floor 1.5s` is therefore a
  valid send delay. Remove the unit explicitly with `as :Quantity(none)` where a
  plain number is needed.
- Swift `GesQuantityUnit` has new cases for the added physical units, so an
  exhaustive `switch` over it must handle `kilogram`, `newton`, `joule`, `watt`,
  `volt`, `ampere`, `hertz`, `kelvin` and `degreePerSecond` (or add a `default`).
  C# `GesQuantityUnit` gains the same members; existing values keep their numbers.
- Runtime observer callbacks report the host-local send ID of the message they
  describe: C# `MessageEmitted(message, id, accepted)`,
  `MessagePublished(message, id, result)`, `DispatchStarted(message, id,
  dispatchSignatureId)` and `DispatchCompleted(message, id, dispatchSignatureId)`;
  Swift `messageEmitted(_:id:accepted:)`, `messagePublished(_:id:result:)`,
  `dispatchStarted(_:id:signatureID:)` and `dispatchCompleted(_:id:signatureID:)`.
  Every queued emit or received message, processed publish, delayed send and
  initialization gets the next ID of its host, so a trace can match each dispatch
  to its emit, publish or delayed send; 0 marks an emit that was not queued or a
  publish the host did not accept. Messages themselves carry no ID.
  Migrate by adding the `id` parameter: every callback now has an empty default
  implementation in both ports, so an observer that keeps an old signature still
  compiles but no longer receives that callback.
- Public API types are renamed so that only the three edge types keep the
  `GameEventScript` prefix: `GameEventScriptHost`, `GameEventScriptProgram` and
  `GameEventScriptMessage`. All other public types use `Ges`, identically in C#
  and Swift, for example `GesContext`, `GesHostBuilder`, `GesMessageArgument`,
  `GesRuntimeLimits`, `IGesPublishSink` (Swift: `GesPublishSink`) and
  `GesSyntaxHighlighter`. Deviating names: `GameEventScriptBuilder` becomes
  `GesProgramBuilder`, `GameEventScriptInstance` becomes `GesProgramInstance`,
  the C# `GameEventScriptBytecodeInstructionUnit` and Swift `GesUnit` become
  `GesQuantityUnit`, the concrete `GameEventScriptExternalTypeCatalog` becomes
  `GesDeclaredExternalTypeCatalog` (the C# interface is `IGesExternalTypeCatalog`,
  the Swift protocol `GesExternalTypeCatalog`), Swift `GameEventScriptExtensionFault`
  becomes `GesExtensionFaultError` and Swift `GameEventScriptAPIError` becomes
  `GesApiError`. Behavior is unchanged; migrate by renaming references.
- Builders are created directly or through their target type, following each
  language's idiom. C#: `new GesProgramBuilder()` (the static `Create()` is
  removed) or, with C# 14, `GameEventScriptProgram.CreateBuilder()`; hosts via
  `new GesHostBuilder()` or `GameEventScriptHost.CreateBuilder()`. Swift:
  `GesProgramBuilder()` or `GameEventScriptProgram.makeBuilder()` (`create()` is
  removed); hosts via `GesHostBuilder()` or `GameEventScriptHost.makeBuilder()`
  (formerly `createBuilder()`). The Swift `GameEventScriptHost` initializer is no
  longer public; configure hosts through the builder. Swift factory methods use
  `make`: `GesMessageSignature.makeMessage(_:)`, `makeSignatureId(name:parameterNames:)`
  and `GesSyntaxHighlighter.makeState()`.
- Reading, encoding, validating and dumping Programs are members of
  `GameEventScriptProgram`; the separate reader, writer, validator and dumper
  types are no longer public. C#: `GameEventScriptProgram.Read(bytes, options)`,
  `program.Encode()` (formerly `ToArray`), `program.GetEncodedSize()`,
  `program.Write(destination)`, `program.Validate()` and `program.Dump(...)`.
  Swift: `GameEventScriptProgram.read(_:options:)`, `program.encode()` (formerly
  `bytes`), `program.encodedSize()`, `program.write(into:)`, `program.validate()`
  and `program.dump(includeInstructionAddresses:)`.
- The Swift Program exposes the same segments as C#: `metadata`,
  `stringConstants`, `uint16IndexLists`, `bindings`, `code`, `debugSymbols`,
  `sourceMap`, `sourceArchive` and `buildMetadata` are segment types
  (`GesStringConstantSegment`, `GesBindingSegment`, `GesCodeSegment`, …) that are
  also random-access collections over their entries. The binding entry type is
  `GesBindingSegment.Entry` in both languages (formerly C#
  `GameEventScriptBinaryBindEntry`, Swift `GameEventScriptBinding`); Swift
  `GameEventScriptSourceMap` and `GameEventScriptBuildMetadata` become
  `GesSourceMapSegment` and `GesBuildMetadataSegment`. Swift
  `GesDebugInfoOptions` moves from the Compiler to the Runtime module, as in C#.
- Message and value JSON are members of the encoded types; `GameEventScriptMessageJson`
  is removed. C#: `message.ToJson()`, `GameEventScriptMessage.FromJson(json)`,
  `value.ToJson()` and `GesValue.FromJson(json)`. Swift: `message.json()`,
  `GameEventScriptMessage(json:)`, `value.json()` and `GesValue(json:)`.
- `GesSymbolKind` is renamed to `GesDiagnosticSymbolKind` in both languages to
  separate it from `GesDebugSymbolKind`.
- Swift gains `GesDiagnosticCodes` with the same stable codes as C#, and
  `GesOpaqueSection.flags` is a `GesSectionFlags` option set instead of a raw
  `UInt16`, as in C#. The C# `GesInstructionFlag` enum is no longer public; both
  languages expose the raw `UnitAndFlags`/`unitAndFlags` byte.
- Bridge types use the `GesBridge` prefix and identical names for shared roles:
  `GesBridgeHostRunner`, `GesBridgeExtensionRegistry`,
  `GesBridgeExternalTypeRegistry`, `GesBridgeMessageHandler`,
  `GesBridgePublishSink` and `GesBridgeValueConversion` (formerly
  `GameEventScriptCSharp…`/`GameEventScriptSwift…`). C# registries are created with
  `GesBridgeExtensionRegistry.Create(types)`/`CreateBuilder()` and
  `GesBridgeExternalTypeRegistry.Create(types)`; the static `GameEventScriptCSharpExtensions`
  and `GameEventScriptCSharpExternalTypes` entry classes are removed. C# gains the
  public `GesBridgeMessageHandler` and `GesBridgePublishSink` delegate adapters.
  Swift descriptors become `GesBridgeFieldBinding<Root>`,
  `GesBridgeConstructorBinding<Root>`, `GesBridgeTypeBinding<Root>` and
  `GesBridgeAnyTypeBinding`; `GesBridgeValueConvertible`,
  `GesBridgeConversionError` and `GesBridgeHostRunnerRegistration` replace their
  `GameEventScriptSwift…` predecessors. Attributes and macros keep their names.
- Swift bridge members no longer name the language: `setSwiftValue(_:)`,
  `swiftValue(at:as:)`, `withSwiftArguments(_:)` and the `swiftArguments:` labels of
  message, emit and publish conveniences become `setNativeValue(_:)`,
  `nativeValue(at:as:)`, `withNativeArguments(_:)` and `nativeArguments:`.
- Swift no longer exposes runtime helper types that C# keeps internal:
  `GesNumber`, `GesText` and `GesSpatialValue` (use `GesValue.x`/`y`/`z`) are
  internal, and `GesSeriesValue` with `GesValue.series(_:)`/`seriesValue` is
  available only to the Compiler.
- Swift reports the public value kind as `GesValue.valueKind: GesBytecodeTypeKind`,
  the same single type C# uses for `ValueKind`; `GesValueKind` and `kind` are no
  longer public. Integer and binary64 ranges are both `.range` (use
  `integerRangeValue`/`floatRangeValue`), and records and external values are
  `.custom`. `GesMessageArguments.kindAt(_:)` returns `GesBytecodeTypeKind`.
- Swift maps follow the C# model of sorted keys with parallel values:
  `GesValue.map(keys:values:)` and `GesValue.record(typeName:keys:values:)` create
  them, and `GesValueMap.key(at:)`/`value(at:)` read them next to `length`,
  `get(_:)` and `containsKey(_:)`. `GesMapEntry`, `GesValueMap.entries` and
  `GesValue.mapEntries` are no longer public.
- C# `GesValueMap.StorageLength` is removed; it always equaled `Length`. Use
  `Length` instead.

### Added

- Physical units in C# and Swift: meter per second (`m/s`), meter per second
  squared (`m/s²`, ASCII `m/s^2`), kilogram (`kg`), newton (`N`), joule (`J`),
  watt (`W`), volt (`V`), ampere (`A`), hertz (`Hz`), kelvin (`K`) and degree
  per second (`°/s`, ASCII `deg/s`), next to meters, seconds and degrees (ASCII
  `deg`). Literals accept the prefixed spellings `km`, `mm`, `ms`, `kN`, `kJ`, `MJ`,
  `kW`, `MW`, `kV`, `kHz`, `Wh` and `kWh`, which normalize exactly to the base
  unit. Products and quotients follow a closed derivation table, for example
  `10m / 2s` is `5m/s`, `2kg * 3m/s²` is `6N` and `1 / 2s` is `0.5Hz` (formerly
  `nothing`); other combinations of two units remain `nothing`. Every unit works
  in `:Quantity(...)`, text formatting and parsing, message JSON, vectors and
  points, inline assembly (`#meterPerSecond`) and host APIs, including Swift
  `@GesField(unit: .meterPerSecond)`. The new UnitIds are 4 to 12, 16 and 17;
  Swift `GesQuantityUnit` is now `Sendable`.
- Runtime observers can follow delayed messages. C# `MessageScheduled(message, id,
  deadline, publish)` / Swift `messageScheduled(_:id:deadline:publish:)` reports
  each accepted `emit … after` or `publish … after` once, with its send ID and its
  absolute deadline in microseconds on the host clock.
  `MessageDiscarded(id)` / `messageDiscarded(id:)` reports every queued,
  pending-initialization or delayed message removed before it was dispatched,
  such as after a failed initialization, so each send ID ends in a dispatch or a
  discard. Hosts without an observer behave as before. `ges run --verbose`
  traces them as `schedule #<id> … at <local time>` and `discard #<id>` and shows
  the send ID on emit, publish and dispatch lines.
- C# `IGesRuntimeObserver` gives every callback an empty default implementation,
  like Swift, so an observer implements only the callbacks it needs.

### Changed

- A delayed `emit after …` without recipients now behaves like the same emit
  without `after`: it is not scheduled and is observed as not queued, so it no
  longer keeps the host waiting, counts towards the queue limit or appears in
  `NextMessageDelay`. Its script result stays true. Delayed publishes are still
  scheduled for the outbound sink.
- The root SwiftPM package now exposes only the four libraries. Build `ges` from
  `implementation/swift/GameEventScriptTool`, or install it via Homebrew, the
  install script or standalone downloads. The root macOS minimum is now 10.15;
  the separate CLI retains its 10.15.4 minimum.
- Swift `GameEventScriptProgram` is now an immutable `final class`, so hosts and
  callers share a single Program instance. Program contents, binary encoding and
  copy-on-write isolation of returned arrays are unchanged.
- Script handlers execute faster in both runtimes. Integer arithmetic and
  integer range loops write results in place, constant map literals and data
  constructions no longer redo per-execution work, and sorting numeric keys is
  no longer quadratic. The Swift VM additionally mutates registers in place
  without per-access exclusivity checks or reference counting on integer paths.
  Behavior and results are unchanged.
- Both compilers move loop-invariant constant loads and arithmetic in front of
  their loop, so loops execute fewer instructions. Random draws, messages and
  host calls stay in place and results are unchanged; executed opcode counts
  drop and register requirements can grow slightly.
- The C# and Swift compilers now produce byte-identical Programs for the same
  sources and compilation options, including debug symbols, source maps and
  source archives. Only BuildMetadata differs to identify the producing compiler.
- The Swift compiler now writes the BuildMetadata section with compiler ID
  `steph.ges.compiler.swift` and version `0.1.0`, as the binary format requires
  of every port. Runtime behavior does not depend on it.

### Fixed

- The Swift compiler reports every diagnostic of a failing phase, as C# does,
  instead of only the first: the parser resumes after a syntax error at the next
  declaration or statement, and validation reports all semantic errors in the
  order that `specs/Diagnostics.md` defines. Call, arity and `is`-predicate
  checks now run during validation, so they appear in that list.
- The C# compiler rejects records with a repeated field name or an unknown field
  type, as Swift does. A field type may be a built-in type, a record of the
  program or a configured external type.
- Both compilers reject `:Integer` and `:Float` wherever a type is named; use
  `as :Number` to convert and `is integer` or `is fractional` to check. Casts and
  checks to other custom types bound only at run time remain valid.
- `ln`, `exp`, roots and powers without an exactly representable integer result
  are no longer folded at compile time; they run on the target platform's math
  library, so a constant such as `ln 2` and `ln x` for `x = 2` always agree
  within one execution. Their exact inner terms are still folded.
- Constant folding of integer products with a unit is exact like VM execution:
  `3000000000m * 3000000001` stays `9000000003000000000m` instead of being
  rounded through binary64; only an Int64 overflow falls back to binary64.
- When a source exceeds the nesting limit, both compilers still report the
  syntax errors found before it in that source.
- Both compilers no longer hang on a `module` declaration after another
  declaration or on a repeated one; it is now a `parse.syntax` error at the
  `module` keyword.
- Compiler diagnostics in C# and Swift start at the same, most specific source
  position: the offending token, the repeated or misnamed parameter, argument,
  condition or binding, the failing call or constructor, or the first construct
  that exceeds the nesting limit.

## [0.4.0] — 2026-10-11

### Added

- Inline `asm` in C# and Swift: statement blocks, binding initializers, function
  and predicate bodies, and computed Record fields. Symbolic operands preserve
  compiler-owned signatures and addresses; definite-initialization and scope
  checks keep outer bindings immutable. GESA dumps are not assembler input.
- Compact nested loops, such as `for suit in suits and rank in ranks { ... }`,
  with the same ordering and scope as ordinary nested loops.
- Condition-first guarded choices, `when condition then value otherwise fallback`,
  alongside the existing value-first spelling. Parameterless predicates support
  `is name` and `is not name`.
- Inclusive `value is between lower and upper` and `is not between` comparisons,
  evaluating the value once and preserving short-circuit semantics.
- Parenthesized extension calls support direct member access and collection
  selectors, such as `:board.top(zone: #discard).id`.
- Combined collection pipelines: `cartesian`, `lockstep`/`zip`, `union`,
  `intersect` and `difference`, with source-less or leading-source forms.
  Multiple bindings work in loops and selectors; compact
  `[:entries :filter ... :select ...]` chains avoid repeated brackets.
  List `A * B` materializes a binary Cartesian product. `:foreach` evaluates an
  expression for each item and returns `nothing`.
- Compound iterator modes, component register outputs and the Cartesian opcode
  (`0xE0`) in both runtimes, validators, GESA and inline ASM. Pipeline component
  iteration avoids an intermediate pair allocation per row.
- Strict Numeric mode for arithmetic, Min/Max, Negate/Abs, Clamp and Move.
  Inline ASM uses a final `#numeric` operand; dumps display `mode=Numeric`.
  Accepted values match `is numeric`, including Boolean as 0/1 and Dice sums.
- Optional Host opcode instrumentation and `run --profile report.md` in both
  CLIs. Reports include opcode, source-line and instruction measurements, six
  VM loop phases, terminal loop checks and partial results on runtime failures.
- Browser Card Lab with editable Highest Card, Mau Mau, Skat and Blackjack rules.
  One shared Wasm instance serves the game and fullscreen editor. The editor
  loads examples, Draft and Saved sources, downloads source, checks diagnostics
  and restarts the game on close. Player-count changes also restart immediately.
- Card Lab source folding and a read-only highlighted GESA dump, with line numbers,
  region folding and inline source annotations. Full-document search includes
  folded content; Ctrl/Cmd+F adopts selected text and supports match navigation.
- Configurable Card Lab player layout, colored player/zone/table badges, titled
  notices and action buttons below cards or zones. Blackjack uses bottom seating
  and reports outcomes through badges and table text without a result popup.
- Bounded C#/Swift reader mutation fuzzing, independently generated canonical
  roundtrip properties, bidirectional binary exchange and pinned 0.3.0 consumer
  upgrade checks. Release documentation defines platform evidence and separate,
  revision-bound performance qualification without extra native Swift PR builds.

### Changed

- `sum` and `average` are strictly numeric, including singleton collections.
  Nonnumeric values make the result `nothing`; later projections still execute.
  Numeric strings are not converted implicitly. Use explicit conversions for
  numeric text and `fold` for concatenation.
- Combined collection sources evaluate every source expression once from left
  to right before validation, including invalid combinations. Construction no
  longer creates preliminary validation iterators.
- Qualified extension names must be contiguous (`:namespace.name`).
- Opcode values `0xF8..0xFF` are reserved for local experiments and rejected by
  standard compilers, validators and runtimes; they are not a plugin ABI.

### Fixed

- C# distribution smoke tests use fresh build and restore directories, preventing
  stale reproducible DLLs from a prior version contaminating package validation.
- Program validation rejects internal VM values escaping into script-visible
  data, including through inline assembly control flow.
- Swift compiler lowering now matches C# for constant Text-key access, collection
  guard branches and short-circuit expressions. Adjacent source mappings are
  compacted; inlined constants are attributed to their use sites.
- Combined pipelines preserve ordinary selector semantics: implicit numeric
  aggregates consume complete elements, Dice results retain their kind, Map
  restrictions remain enforced, and Dice difference rejects invalid face values.
- Card Lab browser assets and workers share a build version to prevent mixed
  cached revisions, including stale dump requests. Dump source names follow the
  loaded game instead of always using `rules.ges`.
- Removed Card Lab folding entries accidentally inserted into the historical
  0.2.0 and 0.3.0 notes; that feature belongs to this development release.

### Performance

- Swift uses compact value storage, inline dispatch and owner-local typed register
  writes, reducing register access and value-copy overhead while preserving exact
  Int64 arithmetic, units and runtime access checks. Dice sums use cached numeric
  storage; collection counts stay in payloads. Numeric operations use register
  fast paths. Profiler timestamps are captured at callback boundaries.
- C# single binders no longer allocate binding lists, and binding metadata is
  stored only on relevant AST nodes. Swift normal iterators reuse fixed operand
  descriptions during validation.
- Reviewed profile-specific reference updates distinguish deliberate storage and
  safety-validation costs from avoidable allocations. All zero-dispatch allocation
  budgets remain zero. See the [performance review](docs/development/Release040Performance.md).

### Migrating from 0.3.0

- Upgrade the GES libraries together and recompile scripts with the matching
  Compiler and Runtime. New Numeric modes, compound iterators and Cartesian
  instructions require the updated Runtime. GESB remains V1; this does not imply
  compatibility with every pre-1.0 binary. The tested 0.3.0 upgrade covers selected
  consumers and frozen fixtures, not arbitrary old Programs.
- Perform a clean rebuild of C# consumers after updating packages. Incremental
  CopyLocal can retain an old reproducible DLL with matching timestamp and size;
  the upgrade check uses `dotnet build -t:Rebuild`.
- Replace text/list accumulation through `[:sum]` with an explicit `fold`.
  Convert text deliberately, for example `values[:sum value => value as :Number]`.
  Boolean and Dice remain numeric; incompatible units still produce `nothing`.
  Recompile existing binaries to adopt the corrected aggregate contract.
- Remove spaces or comments inside qualified extension names; whitespace before
  arguments remains valid. The existing value-first `when` syntax, predicate
  calls and nested loops remain supported. Do not mix the two choice spellings
  within one chain.
- Do not depend on short-circuiting between combined source expressions: even
  when an earlier source is invalid, later source expressions execute. Native
  extension side effects are not rolled back.

### Known limitations

- Pre-1.0 compatibility remains subject to the
  [version policy](docs/guide/distribution/Packages.md#version-policy).
  [Platform coverage](docs/guide/distribution/Platforms.md) distinguishes full
  Conformance from package and CLI smoke checks; portable libraries do not imply
  full qualification on every OS, browser or embedding.
- Native effects remain application-owned and nontransactional. Profiling adds
  measurement overhead; profile numbers are not cross-machine speed promises.
  Card Lab is an example embedding, not a multiplayer service or a general WASM
  distribution of all SwiftPM products.
- Final candidate package, symbol, reproducibility, installation and platform
  evidence is required before publishing. See the
  [0.4.0 closing review](docs/development/Release040Review.md) for the distinction
  between completed development work and remaining publication gates.

## [0.3.0] — 2026-09-29

This release adds CLI editing, portable and native AOT NuGet tools, and Host
idle-duration observations in C# and Swift.

### Added

- Exposed the `ges` executable in the root SwiftPM package alongside the libraries.
  Homebrew and standalone downloads remain available. The root package now requires
  macOS 10.15.4 to cover CLI Foundation I/O.

- Host idle-duration observations in C# and Swift, including synchronized Bridge
  runner access, for application-defined inactivity recovery without host timers.

- Added Homebrew installation through `schloepke/gameeventscript/ges`. New standalone
  CLI releases provide C# for Windows and Swift for macOS/Linux; NuGet tools remain
  available across platforms. Previously published downloads remain available.

- Added external-editor REPL drafts with `:edit`, scratch ID `0`, explicit `:save`,
  conflict checks, in-memory reloads and unsaved-change protection on `:quit`.
  Clearing scratch removes its previous handlers. Explicit `:reload` refreshes
  clean file-backed drafts from disk while preserving unsaved edits.
  Editor selection supports GES_EDITOR before VISUAL/EDITOR, with nano (macOS/Linux)
  or notepad.exe (Windows) as the default.
  Terminal editors inherit the foreground terminal in Swift as well as .NET;
  `:help` lists editing, saving and forced exit directly.

- Added NuGet release preparation for the portable `GameEventScript.Tool` and
  native `GameEventScript.Tool.Aot` CLI, with verified Windows/macOS/Linux
  x64 and ARM64 package jobs and explicit manual publication.

- Added `--aot` to the local C# CLI installer for native macOS/Linux builds,
  including updates, switching installation modes and native uninstallation.

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

- Both CLI multiline prompts use plain continuation indentation. Run diagnostics
  appear in red with `--color`, respecting `NO_COLOR`.

- CLI program selectors now use plain numeric IDs (`:source 1`, `:dump 1`,
  `:unload 1`). The previous `@1` spelling remains a compatibility alias.

- Clarified application ownership of state mutation and persistence, including
  native handler and extension effects, external-value stability and failure
  recovery. Existing runtime behavior is unchanged.

### Fixed

- Swift compiler diagnostics now explain validation and compilation failures
  instead of repeating their diagnostic codes, including unknown REPL types.

- Fixed native AOT C# CLI crashes when listing programs, showing source archives
  or tracing text arguments, using generated JSON serialization metadata.

- Fixed C# CLI interactive history and cursor editing without `--color`; color
  selection now affects highlighting only.

### Migrating from 0.2.0

- SwiftPM consumers on macOS must declare macOS 10.15.4 or newer when using the
  root package, which now also exposes the optional `ges` executable.

- Upgrade the GES library packages together to 0.3.0; SwiftPM consumers use the
  `0.3.0` tag. Idle-duration observations require this version or newer.
- NuGet CLI installations can use either `GameEventScript.Tool` or
  `GameEventScript.Tool.Aot`. Uninstall one before switching to the other because
  both provide `dotnet-ges`. Native tool installation requires .NET SDK 10+.
- New standalone C# archives target Windows. On macOS/Linux, use the Swift CLI
  through Homebrew or release downloads, or install a C# NuGet tool.
- REPL edits stay in memory until `:save`. Explicit `:reload` refreshes clean
  sources from disk and preserves unsaved drafts; rebuilding the session reruns
  initialization and clears queued work. Numeric selectors replace `@ID` in
  examples, while the old spelling remains accepted.

## [0.2.0] — 2026-09-26

This development release expands the language, native integration and tooling.
C# and Swift share the same release version. Compatibility may change during 0.x;
see the migration notes below before upgrading from 0.1.0.

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

[0.4.0]: https://github.com/schloepke/GameEventScript/compare/0.3.0...0.4.0
[0.3.0]: https://github.com/schloepke/GameEventScript/compare/0.2.0...0.3.0
[0.2.0]: https://github.com/schloepke/GameEventScript/compare/0.1.0...0.2.0
[0.1.0]: https://github.com/schloepke/GameEventScript/releases/tag/0.1.0
