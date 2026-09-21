<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Game Event Script guides

This directory contains non-normative learning material: introductions, tutorials, recipes, examples, and integration guidance.

Guides optimize for understanding and may omit edge cases. Exact behavior is defined only by the documents linked from the [documentation index](../README.md). A guide must not introduce syntax, API behavior, or runtime guarantees that are absent from the owning specification.

## Distribution

- [Library packages and releases](distribution/Packages.md) describes public
  NuGet/SwiftPM products, Xcode consumption and the shared release procedure.
- [C# distribution](distribution/CSharp.md) describes reproducible NuGet,
  symbol, and DLL artifacts, the Unity DLL path, and the guarded release
  workflow.
- [CLI tool](../../implementation/csharp/GameEventScript.Tool/README.md)
  describes the current `dotnet ges` entry point and local .NET tool installation.

The [Swift CLI guide](../../implementation/swift/GameEventScriptTool/README.md)
describes the native `ges` command, installation and terminal console.
