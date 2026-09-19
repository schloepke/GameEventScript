<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Swift implementation

| SwiftPM package / import | Responsibility | Dependencies |
| --- | --- | --- |
| `GameEventScriptRuntime` | Immutable values and Programs, `.gesb` codecs/validation, GESA dumping, Host, VM, random streams, extensions and external types | Swift standard library and platform math library |
| `GameEventScriptCompiler` | Source lexer/parser, validation, lowering, optimization, register allocation, immutable Programs and debug sections | Runtime |
| `GameEventScriptConformance` | Shared Markdown parser, native compilation/execution, bounded fixture verification, result reports | Runtime and Compiler |

All library APIs are synchronous, fileless, and independent of a test framework.
There are no external package dependencies. The `ges-conformance` executable
owns file discovery, input loading, process exit status, and report writing.
Compiler depends only on Runtime; an embedding using Runtime does not acquire
either Compiler or Conformance. The native Swift `ges` CLI is still deferred;
`ges-conformance` is the corpus verification tool.

## Build and verify

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

The API gate compares all three packages' declared exported symbols with
`api/*.approved.txt`. Update snapshots explicitly after reviewing API changes:

```bash
python3 scripts/verify-swift-api.py --update
```

Verified on 2026-09-19 with Swift 6.4 on macOS arm64, Release:

- **1,460/1,460 behavior checks** from the original 89 Markdown documents pass
  with native Swift compilation, including 131 expected compilation failures,
  15 bytecode constraints, two metadata cases, and all eight GESA snapshots.
- The strict report passes **1,429 cases**, with **31 optional performance cases
  skipped** because Swift does not yet provide a calibrated measurement profile.
  Their behavior is checked separately; no Swift allocation/timing claim is made.
- All 47 shared binary cases pass, including canonical runtime-segment comparisons
  against Swift compiler output, malformed inputs, rewrites and fixture execution.
- Independent Runtime verification passes **1,227/1,227** cases with C# inputs.
- Eleven native adapter/bootstrap tests pass, including the Markdown bootstrap
  fixtures, compiler ownership/options and resource-limit failure paths.

The [CapabilityMatrix](../../conformance/cross-language/CapabilityMatrix.md)
records the exact corpus identity and report locations.

Run the strict native suite without .NET:

```bash
swift run --package-path implementation/swift/GameEventScriptConformance \
  --scratch-path artifacts/swift/conformance --build-system native -c release \
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

let program = try GameEventScriptBuilder()
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

func execute(_ bytes: [UInt8]) throws -> GameEventScriptExecutionResult {
    let program = try GameEventScriptProgramReader.read(bytes)
    let host = try GameEventScriptHost(seed: 123)
    let output = try GameEventScriptMessageSignature(name: "Done", parameters: ["value"])
    _ = try host.subscribe(output, handler: Output())
    let instance = try host.load(program)
    let initialization = try host.runToCompletion()
    guard initialization.state == .completed else { return initialization }
    host.receive(try GameEventScriptMessage(name: "Start", arguments: [
        GameEventScriptMessageArgument(name: "value", value: .integer(42))
    ]))
    let result = try host.runToCompletion()
    instance.detach()
    return result
}
```

`executeFrame(opcodeBudget:)` allows bounded stepping and resumption. The host
retains subscriptions and loaded Programs until explicitly detached; discarding
a handle does not unsubscribe it. Enqueue-time snapshots and dispatch ordering
match the shared Host contract. Configuration is supplied to the Host initializer.

Programs and collections use immutable Swift value semantics and copy-on-write
storage. Unicode-scalar identity, exact Int64 ranges, Binary64 values, units, and
ordered argument labels follow the shared specifications. VM entry reads
arguments by index; frame/register storage and callback objects are reused.
Callbacks and argument borrows are synchronous and must not be retained.

Remote SwiftPM registry publication remains deferred. Packages can currently be
consumed independently by local path from the monorepo checkout.
