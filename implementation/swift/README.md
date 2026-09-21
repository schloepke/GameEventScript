<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Swift implementation

| SwiftPM package / import | Responsibility | Dependencies |
| --- | --- | --- |
| `GameEventScriptRuntime` | Immutable values and Programs, `.gesb` codecs/validation, GESA dumping, Host, VM, random streams, extensions and external types | Swift standard library and platform math library |
| `GameEventScriptCompiler` | Source lexer/parser, validation, lowering, optimization, register allocation, immutable Programs and debug sections | Runtime |
| [`GameEventScriptSwiftBridge`](GameEventScriptSwiftBridge/README.md) | Native closure adapters, typed value conversion, KeyPath/external-type bindings and an optional synchronized Host runner | Runtime; Foundation for the runner |
| `GameEventScriptConformance` | Shared Markdown parser, native compilation/execution, bounded fixture verification, result reports | Runtime and Compiler |
| `GameEventScriptTool` / `ges` executable | Compile/check/run/dump, interactive console, filesystem and terminal adapters | Runtime, Compiler, Foundation and POSIX |

Runtime, Compiler and Conformance APIs are synchronous, fileless, and independent of a test framework.
There are no external package dependencies. The `ges-conformance` executable
owns file discovery, input loading, process exit status, and report writing.
Compiler depends only on Runtime; an embedding using Runtime does not acquire
either Compiler or Conformance. SwiftBridge is an optional Runtime-only adapter
dependency for native integration. The [native Swift `ges` CLI](GameEventScriptTool/README.md)
is the user-facing tool; `ges-conformance` is the corpus verification tool.

## Build and verify

### Xcode workspace

[`GameEventScript.xcworkspace`](GameEventScript.xcworkspace) opens Runtime,
Compiler, SwiftBridge, Conformance and the CLI together. The workspace references the local
SwiftPM packages directly; their `Package.swift` manifests remain the build configuration.
There are no duplicate `.xcodeproj` targets or source lists to maintain.

Open it from the repository root:

```bash
./scripts/open-swift-xcode.sh
```

The launcher configures this workspace's local Xcode preferences so Derived Data
and build products stay below `artifacts/swift/xcode`, then opens Xcode. It preserves
other preferences and does not change global Xcode settings. These user settings
are ignored by Git; the workspace and shared test scheme are tracked. After the
first setup, the workspace can also be opened directly in Finder.

Select **My Mac** and one of the `GameEventScriptRuntime`,
`GameEventScriptCompiler`, `GameEventScriptSwiftBridge`, `GameEventScriptConformance` or `GameEventScriptTool`
schemes to build with **Cmd+B**. `GameEventScriptTool` runs the native CLI tests with **Cmd+U**;
The `ges` executable scheme runs the CLI. The shared `GameEventScriptConformance`
scheme runs the native Bridge and corpus tests with **Cmd+U** in Debug. The
`GameEventScriptSwiftBridge` scheme runs its native tests independently.
Before the first full test run, execute
`./scripts/test-swift.sh` once to prepare the C#-compiled interoperability fixtures
in `artifacts/swift/runtime-fixtures`; refresh them after corpus changes.
The `ges-conformance` executable is also available as a scheme; its command-line
arguments are documented below. Performance measurements continue to use the
separate Release script and calibrated profile.

For a headless Xcode test run:

```bash
./scripts/open-swift-xcode.sh --configure-only
xcodebuild -workspace implementation/swift/GameEventScript.xcworkspace \
  -scheme GameEventScriptConformance -destination 'platform=macOS' \
  -derivedDataPath artifacts/swift/xcode -parallel-testing-enabled NO test
```

### SwiftPM verification

The CLI and Conformance packages require macOS 10.15.4 or newer for the
executable adapters' throwing Foundation file-handle I/O. Runtime, Compiler and
SwiftBridge do not inherit this package requirement.

All entry points are in the repository's `scripts` directory and can also be
invoked by absolute path from another working directory:

| Command | Purpose |
| --- | --- |
| `./scripts/clean.sh [--dry-run] [--artifacts-only]` | Remove repository build outputs; preview with `--dry-run` ([scope](../../README.md#clean-build-outputs)) |
| `./scripts/build-swift.sh` | Build every SwiftPM package independently in Release; no .NET dependency |
| `./scripts/build-swift.sh --configuration debug` | Build the same packages in Debug |
| `./scripts/format-swift.sh` | Check 250-column formatting and one blank line around callable/type declarations |
| `./scripts/format-swift.sh --fix` | Apply formatting to manifests, Sources, Tests and the spacing helper |
| `python3 scripts/test-swift-spacing.py` | Verify syntax-aware declaration spacing and preservation of comments/strings |
| `./scripts/test-swift-tool.sh` | Verify CLI adapters, real process I/O and terminal editing without .NET |
| `./scripts/test-swift-bridge.sh` | Verify native Swift adapters and Compiler/Runtime binding integration without .NET |
| `./scripts/install-swift-tool.sh` | Build all packages and install/update the native `ges` CLI |
| `./scripts/uninstall-swift-tool.sh` | Remove the owned Swift CLI installation |
| `./scripts/test-swift.sh` | Export C# interoperability fixtures, run native tests, strict shared Conformance and bidirectional numeric text roundtrips |
| `python3 scripts/test-number-text-roundtrip.py` | Verify exact C# ↔ Swift Number/Quantity/Percentage text exchange with deterministic inputs |
| `./scripts/test-swift-performance.sh` | Verify the calibrated Release performance profile |
| `python3 scripts/verify-swift-api.py` | Verify approved public API snapshots and public Swift documentation |
| `python3 scripts/test-swift-api.py` | Verify the documentation gate's positive and negative controls |
| `python3 scripts/test-swift-incremental.py` | Verify added/renamed/removed dependency sources and unchanged-build object reuse |
| `python3 scripts/verify-swift-dependencies.py` | Verify direct target dependencies for imports, including native tests |
| `python3 scripts/verify-swift-bytecode.py` | Verify the shared opcode/operand registry |
| `./scripts/open-swift-xcode.sh` | Configure local build paths and open the Xcode workspace |

Build outputs use `artifacts/swift/runtime`, `compiler`, `swiftbridge`, `conformance` and `tool`;
the Conformance directory is shared with the existing test scripts. Build and
format scripts discover local packages from their manifests, so added packages
participate without duplicating source lists. Formatting excludes generated
artifacts and Xcode's user state. SwiftPM publication remains deferred. The CLI
installer defaults to `$HOME/.local/bin` and supports `--tool-path DIRECTORY`;
see the [CLI guide](GameEventScriptTool/README.md) for installation and updates.

Build/test scripts regenerate the native SwiftPM build plan with
`--disable-build-manifest-caching`. This ensures new, renamed or removed files in
local dependency packages are discovered, including a new Runtime file while
building Compiler. Compiled objects and module caches remain incremental; no
Clean is performed during installation. Use the same option for manual native
SwiftPM builds after source-structure changes. The incremental CI test exercises
these changes in disposable packages and verifies object reuse on an unchanged build.

Use Swift 6.0 or newer. The complete verification script additionally uses .NET 10
for independent cross-language binary inputs:

```bash
./scripts/test-swift.sh
python3 scripts/verify-swift-api.py
```

The script checks the explicit opcode/operand registry against C#, builds Swift
in Release, compiles the original Markdown sources natively, and runs the strict
Conformance report. Separately it exports C#-compiled binary inputs and verifies
them against the same Markdown expectations in the Swift Runtime. Those manifests
include both binary SHA-256 and complete Markdown-document SHA-256; no expected
results are exported from C#. Neither Compiler nor Runtime depends on .NET.

Every authored public declaration has a `///` documentation comment, including
properties, initializers, enum cases, protocol requirements and bridge extensions.
These comments are available in Xcode Quick Help and Swift symbol graphs. The API
gate rejects missing or empty comments and comments merely inherited from another
module for handwritten implementations. Compiler-synthesized members are excluded.

The API gate compares all four library packages' declared exported symbols with
`api/*.approved.txt`. Update snapshots explicitly after reviewing API changes:

```bash
python3 scripts/verify-swift-api.py --update
```

Verified on 2026-09-21 with Swift 6.4 on macOS arm64, Release:

- **1,519/1,519 behavior checks** from the original 94 Markdown documents pass
  with native Swift compilation, including 158 expected compilation failures,
  15 bytecode constraints, three metadata cases, and all nine source-based GESA snapshots.
- The ordinary hardware-independent report passes **1,488 cases** and skips
  **31 optional performance measurements**. The last calibrated performance run
  passed all 31 measured workloads.
- The Swift 6.4/macOS 26/Apple M3 Max profile measures cumulative allocations and
  elapsed time. All sixteen warmed dispatch variants have **zero allocations**.
  See [Performance.md](Performance.md) for scope, references and reproduction.
- All 52 shared binary cases pass, including canonical runtime-segment comparisons
  against Swift compiler output, malformed inputs, rewrites, fixture execution and four GESA snapshots of identical binary inputs.
- Independent Runtime verification passes **1,258/1,258** cases with C# inputs.
- Eighteen Conformance/adapter/bootstrap tests pass, including the Markdown bootstrap
  fixtures, compiler ownership/options and resource-limit failure paths.

The [CapabilityMatrix](../../conformance/cross-language/CapabilityMatrix.md)
records the exact corpus identity and report locations.

Run the strict native suite without .NET:

```bash
swift run --package-path implementation/swift/GameEventScriptConformance \
  --scratch-path artifacts/swift/conformance --build-system native --disable-build-manifest-caching -c release \
  ges-conformance --corpus conformance/suites \
  --fixtures conformance/fixtures/MarkdownV1 \
  --binary-fixtures conformance/fixtures \
  --output artifacts/swift/conformance-results
```

`ConformanceResults.json`, `.md`, and `SwiftResults.json` contain native Swift
results. Adding `--runtime-programs artifacts/swift/runtime-fixtures` after the
C# export also produces `RuntimeResults.json` and `.md` with explicitly identified
C# compiler inputs. Generated inputs, reports and build products stay under
ignored `artifacts`.

## Compile source text

Add `GameEventScriptCompiler` as a local SwiftPM dependency/product in the same
way as Runtime below. The builder accepts texts and optional diagnostic source
names; it performs no file I/O:

```swift
import GameEventScriptCompiler
import GameEventScriptRuntime

let program = try GameEventScriptBuilder.create()
    .addScript("""
        module example
        function double(_ value as :Number) be value + value
        on Main(args) { emit ConsoleOut(double(21)) }
        """, sourceName: "example.ges")
    .compile()
let bytes = try GameEventScriptProgramWriter.bytes(program)
// The embedding can save bytes, or load program directly into a Host.
```

Repeated `addScript` calls compile sources jointly. `compile()` can be repeated;
previously returned Programs remain immutable. Debug symbols, source maps and
source archives are included by default. Use `.withDebugInfo(.none)` for compact
output, `.withProgramVersion(...)` to set the portable version, and
`.withExternalTypeCatalog(...)` for declarative external types. Runtime bindings
remain separate. `GameEventScriptCompileError.diagnostics` exposes stable phase,
code, symbol and source location. Invalid public options throw API errors.

## Consume the Runtime

Add the package to a local SwiftPM consumer:

```swift
.package(path: "/path/to/GameEventScript/implementation/swift/GameEventScriptRuntime")
```

Add its product to the consuming target:

```swift
.product(name: "GameEventScriptRuntime", package: "GameEventScriptRuntime")
```

Load bytes supplied by the embedding and execute messages:

```swift
import GameEventScriptRuntime

final class Output: GameEventScriptNativeMessageHandler {
    func handle(_ message: GameEventScriptMessage, context: GameEventScriptContext) throws {
        // Deliver message.arguments to your product; Runtime performs no I/O.
    }
}

func execute(_ bytes: [UInt8]) throws -> (start: GameEventScriptStartResult, execution: GameEventScriptExecutionResult?) {
    let program = try GameEventScriptProgramReader.read(bytes)
    let host = try GameEventScriptHost.createBuilder().withRandomSeed(123).build()
    let output = try GameEventScriptMessageSignature(name: "Done", parameters: ["value"])
    _ = try host.subscribe(output, handler: Output())
    let instance = try host.load(program)
    defer { instance.detach() }
    let startup = try host.start()
    guard host.isReady else { return (startup, nil) }
    host.receive(try GameEventScriptMessage(name: "Start", arguments: [
        GameEventScriptMessageArgument(name: "value", value: .integer(42))
    ]))
    let result = try host.runToCompletion()
    return (startup, result)
}
```

`executeFrame(opcodeBudget:)` allows bounded stepping and resumption. The host
retains subscriptions and loaded Programs until explicitly detached; discarding
a handle does not unsubscribe it. Enqueue-time snapshots and dispatch ordering
match the shared Host contract. Configure through `createBuilder()`, then build, load all Programs and call
`start()`. Start runs only the initial group; its emitted messages wait for normal
pumping. A later Load with initialization has a pending instance `startResult` until its queued init
finishes. A failed late init removes that instance, its outputs and captured
deliveries without resetting host readiness. See [Host runtime](../../specs/HostRuntime.md) for the full contract.

Programs and collections use immutable Swift value semantics and copy-on-write
storage. Unicode-scalar identity, exact Int64 ranges, Binary64 values, units, and
ordered argument labels follow the shared specifications. VM entry reads
arguments by index; frame/register storage and callback objects are reused.
Callbacks and argument borrows are synchronous and must not be retained.

Remote SwiftPM registry publication remains deferred. Packages can currently be
consumed independently by local path from the monorepo checkout.

## Native Swift integration

The optional [SwiftBridge guide](GameEventScriptSwiftBridge/README.md) provides
examples for closure subscriptions, ordered message arguments, strict native
value conversion, extension registries, KeyPath-based external types and the
synchronized Host runner. Its package depends only on Runtime; compiler catalogs
use Runtime's declarative interfaces. Existing portable protocols remain usable
without any Bridge dependency.

### Declaration spacing

Swift formatting uses a 250-character target width and respects existing line
breaks. Deliberately multiline function bodies remain multiline; compact bodies
may stay on one line. This also preserves existing breaks elsewhere.
`lineBreakBeforeEachArgument` places each parameter/argument on its own line
when a list wraps; fitting lists may remain on one line. Functions, methods, initializers, deinitializers,
subscripts and type declarations (including extensions) are separated from
neighbouring items by exactly one blank line. Documentation comments and
attributes stay with their declaration; no blank line is inserted immediately
after an opening brace. Consecutive stored properties remain grouped.

`swift-format` controls wrapping and maximum blank lines. The formatting script
also uses the active toolchain’s SwiftParser/SwiftSyntax modules to enforce
declaration spacing without rewriting string contents. Its compiled helper is
cached under `artifacts/swift/formatting`; it adds no package dependency. Both
formatting and the helper’s regression controls run in Swift CI.
