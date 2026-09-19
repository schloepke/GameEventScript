<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Swift implementation

The Swift port starts with two independent SwiftPM packages:

| Package / import | Responsibility | Dependencies |
| --- | --- | --- |
| `GameEventScriptRuntime` | Immutable values, units, collections, range descriptors, messages, and signatures | Swift standard library |
| `GameEventScriptConformance` | Markdown parsing, API-case execution, corpus identity, and result reports | Runtime |

The Conformance package also supplies the `ges-conformance` executable. File
discovery, fixture loading, process exit codes, and report writing live in that
adapter. The library APIs are synchronous and accept in-memory data. There are
no external package dependencies.

This is the first Runtime foundation, not a complete script runtime. It executes
the shared `valueApi` and `messageApi` cases. Host dispatch, random execution,
`.gesb` codecs, VM instructions, extensions, runtime literal parsing, and the
Compiler are not yet implemented. Series currently contains metadata only.
The remaining work is tracked in the root [backlog](../../BACKLOG.md).

The Compiler will be a separate SwiftPM package depending on Runtime, following
the same distribution boundary as C#. There is no empty Compiler package and
no extra Core package. Products depending on Runtime do not acquire Conformance
or a compiler.

## Build and verify

Use Swift 6.0 or newer. From the repository root:

```bash
./scripts/test-swift.sh
python3 scripts/verify-swift-api.py
```

The script builds the packages in Release, checks the existing Markdown parser
bootstrap fixtures, parses the exact shared corpus, verifies its fingerprint
and case requirements against C#, and executes all supported API cases.
The API check compares declared exported symbols with the approved Swift
snapshots under `api`. Build products and generated reports stay below
`artifacts/swift`.

All corpus cases receive an explicit result. Missing Core capabilities produce
`conformance.runner.missingCoreCapability` errors, never passing results or
skips. The development script permits only these expected incompleteness
errors; real assertion failures and other runner errors still fail the build.
Remove `--allow-incomplete` for strict full-port acceptance:

```bash
swift run --package-path implementation/swift/GameEventScriptConformance \
  --scratch-path artifacts/swift/conformance --build-system native -c release \
  ges-conformance --corpus conformance/suites \
  --fixtures conformance/fixtures/MarkdownV1 \
  --output artifacts/swift/conformance-results
```

The strict command currently exits nonzero because the Runtime port is
incomplete. It still writes `ConformanceResults.json`, `ConformanceResults.md`,
and `SwiftResults.json`. The compact report uses the same corpus hash, case IDs,
kinds, levels, and capability requirements as the C# reference.

Verified on 2026-09-19 with Swift 6.4 on macOS arm64: all 89 documents and
1,460 case identities match C#; all 69 message API and 15 value API cases pass,
along with the four shared Markdown bootstrap fixtures. The other 1,376 cases
explicitly report missing Core support. The
[CapabilityMatrix](../../conformance/cross-language/CapabilityMatrix.md) links
the generated evidence.

## Consume the Runtime

For local SwiftPM development, add the repository package by path:

```swift
.package(path: "/path/to/GameEventScript/implementation/swift/GameEventScriptRuntime")
```

Then add the product to the consuming target:

```swift
.product(name: "GameEventScriptRuntime", package: "GameEventScriptRuntime")
```

Example:

```swift
import GameEventScriptRuntime

let argument = try GameEventScriptMessageArgument(name: "value", value: .integer(42))
let message = try GameEventScriptMessage(name: "Done", arguments: [argument])
let signature = try GameEventScriptMessageSignature(name: "Done", parameters: ["value"])
assert(signature.matches(message))
```

Collections use Swift value semantics and copy-on-write storage. String and map
key identity follows GES Unicode-scalar rules rather than Swift's canonical
Unicode equivalence. Fixed-width integers, Binary64, units, ordered arguments,
and normalized numeric values follow the shared specifications.

Packages are currently consumed together from the monorepo checkout. Remote
package distribution and registry publication remain deferred until the
implementation and release process are ready.
