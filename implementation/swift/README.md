<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Swift implementation

| SwiftPM package / import | Responsibility | Dependencies |
| --- | --- | --- |
| `GameEventScriptRuntime` | Immutable values and Programs, `.gesb` codecs/validation, GESA dumping, Host, VM, random streams, extensions and external types | Swift standard library and platform math library |
| `GameEventScriptConformance` | Shared Markdown parser, API and Runtime verification, result reports | Runtime |

Both library APIs are synchronous, fileless, and independent of a test framework.
There are no external package dependencies. The `ges-conformance` executable
owns file discovery, input loading, process exit status, and report writing.
The Swift Compiler will be a separate package depending on Runtime; an embedding
using Runtime does not acquire either a compiler or Conformance.

## Build and verify

Use Swift 6.0 or newer and .NET 10 for the reference compiler:

```bash
./scripts/test-swift.sh
python3 scripts/verify-swift-api.py
```

The script checks the explicit opcode/operand registry against C#, exports
binary inputs from the shared Markdown using the C# compiler, builds Swift in
Release, runs bootstrap checks, and produces reports below `artifacts/swift`.
Input manifests include both binary SHA-256 and complete Markdown-document
SHA-256. The original Markdown supplies every expected result; C# results are
not used as the Runtime oracle. Runtime itself has no .NET dependency.

The API gate compares declared exported symbols with `api/*.approved.txt`.
Update those snapshots explicitly after reviewing API changes:

```bash
python3 scripts/verify-swift-api.py --update
```

Verified on 2026-09-19 with Swift 6.4 on macOS arm64, Release:

- 89 documents / 1,460 case identities and requirements match C#.
- 69 message API, 15 value API, and one external-type API case pass.
- 1,137 script scenarios and four expected link failures pass on Swift.
- All 47 shared `.gesb` cases pass, including malformed files, canonical rewrites,
  optional-section retention, and execution of valid fixtures.
- All eight shared GESA snapshots pass for independently compiled Programs.
- Behavior assertions from 31 performance scenarios pass. These checks do not
  measure or claim a Swift allocation/timing profile.
- Eight native bootstrap tests pass, including the four Markdown bootstrap fixtures.

The Runtime-only total is **1,227/1,227**. The
[CapabilityMatrix](../../conformance/cross-language/CapabilityMatrix.md) records
this evidence separately from full-port acceptance. The Swift Compiler remains
unimplemented. Strict full-port execution still exits nonzero and records
missing Core capabilities; those cases are never reported as passing or skipped.

To regenerate both report sets explicitly after the export:

```bash
swift run --package-path implementation/swift/GameEventScriptConformance \
  --scratch-path artifacts/swift/conformance --build-system native -c release \
  ges-conformance --corpus conformance/suites \
  --fixtures conformance/fixtures/MarkdownV1 \
  --runtime-programs artifacts/swift/runtime-fixtures \
  --binary-fixtures conformance/fixtures \
  --output artifacts/swift/conformance-results --allow-incomplete
```

`--allow-incomplete` tolerates only missing Core support in the strict report.
Actual assertion, fixture-integrity, or Runtime verification failures still
fail the command. Omit this switch for strict full-port acceptance.
`RuntimeResults.json` and `.md` identify their C# compiler input and exclude
performance measurements. `ConformanceResults.json`, `.md`, and
`SwiftResults.json` retain the separate strict full-port result.

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
