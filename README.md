<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Game Event Script

[![GitHub release](https://img.shields.io/github/v/release/schloepke/GameEventScript)](https://github.com/schloepke/GameEventScript/releases/latest)
[![NuGet](https://img.shields.io/nuget/v/GameEventScript.Runtime)](https://www.nuget.org/packages/GameEventScript.Runtime)
[![Swift versions](https://img.shields.io/endpoint?url=https%3A%2F%2Fswiftpackageindex.com%2Fapi%2Fpackages%2Fschloepke%2FGameEventScript%2Fbadge%3Ftype%3Dswift-versions)](https://swiftpackageindex.com/schloepke/GameEventScript)
[![Swift platforms](https://img.shields.io/endpoint?url=https%3A%2F%2Fswiftpackageindex.com%2Fapi%2Fpackages%2Fschloepke%2FGameEventScript%2Fbadge%3Ftype%3Dplatforms)](https://swiftpackageindex.com/schloepke/GameEventScript)

**[gameeventscript.org](https://gameeventscript.org)** — guides, language reference, API documentation, and tools.

Game Event Script is a portable, deterministic scripting language and serial
message host for game logic. Write immutable, event-driven scripts and embed
them in C# or Swift applications. Both implementations follow shared
language-neutral specifications and executable Markdown Conformance tests.

```ges
module example

function double(_ value as :Number) be value + value

on Start() {
    emit Done(value: double(21))
}
```

The application sends `Start()`; the script emits `Done(value: 42)` for another
script or a native handler to receive.

## Get started

- [Learn the language](https://gameeventscript.org/docs/learn/language/)
- [Embed in C# and Unity](https://gameeventscript.org/docs/learn/csharp/)
- [Embed in Swift](https://gameeventscript.org/docs/learn/swift/)
- [Download the CLI](https://gameeventscript.org/docs/tools/downloads/) to compile, run, and explore scripts interactively.

### C# / NuGet

For compilation and native integration:

```bash
dotnet add package GameEventScript.Runtime
dotnet add package GameEventScript.Compiler
dotnet add package GameEventScript.CSharpBridge
```

The libraries target .NET Standard 2.1. Applications executing precompiled
`.gesb` programs can omit Compiler; the native Bridge is optional.

### Swift / SwiftPM

In Xcode, choose **File → Add Package Dependencies** and enter:

```text
https://github.com/schloepke/GameEventScript.git
```

Select a published version and add the products your target needs:
`GameEventScriptRuntime`, `GameEventScriptCompiler`, and optionally
`GameEventScriptSwiftBridge`. See the [Swift guide](https://gameeventscript.org/docs/learn/swift/)
for a complete example and SwiftPM manifest configuration.

Optional editor highlighting is available as `GameEventScript.SyntaxHighlighter`
on NuGet and `GameEventScriptSyntaxHighlighter` through SwiftPM. Neither depends
on the Runtime or Compiler.

All libraries share one release version. **Language and API compatibility may
change during 0.x development.** Keep your GES packages on the same version and
check the [release notes](https://github.com/schloepke/GameEventScript/releases)
for migration guidance. The [changelog](CHANGELOG.md) also tracks unreleased work;
the website identifies features that require a newer version.

## Working on GameEventScript

The documentation sources and specifications remain in this repository:

- [Documentation index](docs/README.md) and [language-neutral specifications](specs/Language.md)
- [C# build and development guide](implementation/csharp/README.md)
- [Swift build and development guide](implementation/swift/README.md)
- [Shared Conformance corpus](conformance)
- [Website development](website/README.md)
- [Backlog](BACKLOG.md)

For questions, ideas, and projects built with GES, visit
[GitHub Discussions](https://github.com/schloepke/GameEventScript/discussions).
Report bugs through [GitHub Issues](https://github.com/schloepke/GameEventScript/issues).

## License

Game Event Script is licensed under the [Apache License 2.0](LICENSE).
Copyright 2026 Stephan Schlöpke. See [Licensing](LICENSING.md) for attribution details.
