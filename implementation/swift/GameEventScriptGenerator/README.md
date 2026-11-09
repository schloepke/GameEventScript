<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Swift Generator module

> **Since: Unreleased**

`GameEventScriptGenerator` is the SwiftPM module behind `ges schema layout` and
`ges schema generate`. It turns `.gess` schemas into typed record code, in Swift or
C#, that writes, reads and compares records and their deltas with the bytes of the
Runtime's schema writer and reader. Build integrations, such as a SwiftPM plugin, use
it to generate without the CLI.

It depends on Runtime and Syntax only and imports no Foundation. Neither Runtime nor
Compiler depends on it: the Generator belongs to build tools, a game never ships it,
and the code it generates needs Runtime only. The public repository-root package exports
this product; this directory is its standalone development package.

```swift
import GameEventScriptGenerator
import GameEventScriptRuntime

let schemas = try GesSchemaSet(sources: [GesSchemaSource(name: "world.gess", text: worldSource)])
let parameters = try GesSchemaParameters().with("worldWidth", literal: "2000m")
let code = try GesSchemaGenerator.generate(
    schemas,
    roots: ["Bot"],
    parameters: parameters,
    options: GesSchemaGenerateOptions(language: .swift, namespace: "Wire")
)
```

`GesSchemaGenerator.layout(_:roots:parameters:)` returns the language-neutral
generation layout as JSON instead. The options select the language, a namespace,
`.internal` access, value conversion (`values`) and, for Swift classes,
`checkedExclusivity`. The Generator reads no files: the caller passes the source text
and writes the result. Equal inputs give identical text in the C# and Swift ports.

Errors of the sources, of binding and of parameters are `GesSchemaError`s with a
`schema.*` code; a request that cannot be generated is a `GesGeneratorError` with a
`generate.*` code.

Generated Swift classes skip the dynamic exclusivity checks unless `checkedExclusivity`
is set, so a caller must not access one record while another access to it is under way.
See the [schema specification](../../../specs/Schemas.md#tools) for this contract, the
generated code and the [generation layout](../../../specs/Schemas.md#generation-layout);
[PublicApi](../../../specs/PublicApi.md#schema-code-generation) defines this API.

Run `swift test --package-path implementation/swift/GameEventScriptGenerator`, or the
full `scripts/test-swift.sh`.
