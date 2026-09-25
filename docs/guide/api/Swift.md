<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Swift API reference

These DocC references describe the **Unreleased development checkout**.
Each reference shows the source commit. It may include APIs not available in
the latest SwiftPM release; check the [changelog](../../../CHANGELOG.md) and
[build identity](https://gameeventscript.org/api/swift/build-info.json) when comparing packages.

| Module | Reference |
| --- | --- |
| Runtime | [GameEventScriptRuntime](https://gameeventscript.org/api/swift/gameeventscriptruntime/documentation/gameeventscriptruntime/) |
| Compiler | [GameEventScriptCompiler](https://gameeventscript.org/api/swift/gameeventscriptcompiler/documentation/gameeventscriptcompiler/) |
| Swift bridge | [GameEventScriptSwiftBridge](https://gameeventscript.org/api/swift/gameeventscriptswiftbridge/documentation/gameeventscriptswiftbridge/) |
| Syntax highlighter | [GameEventScriptSyntaxHighlighter](https://gameeventscript.org/api/swift/gameeventscriptsyntaxhighlighter/documentation/gameeventscriptsyntaxhighlighter/) |

DocC builds these pages from public Swift symbol graphs and their documentation
comments. The internal macro implementation, CLI and Conformance are excluded;
the bridge reference includes its public annotation API.

Start with the [Swift embedding guide](../Swift.md) or the
[Swift bridge guide](../../../implementation/swift/GameEventScriptSwiftBridge/README.md).
The [portable API specification](../../../specs/PublicApi.md) defines the
language-independent contract.
