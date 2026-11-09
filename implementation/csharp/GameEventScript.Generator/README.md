<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# C# Generator module

> **Since: Unreleased**

`GameEventScript.Generator` is the .NET Standard 2.1 library behind `ges schema layout`
and `ges schema generate`. It turns `.gess` schemas into typed record code, in C# or
Swift, that writes, reads and compares records and their deltas with the bytes of the
Runtime's schema writer and reader. Build integrations, such as a Unity editor menu or
an MSBuild target, use it to generate without the CLI.

It depends on Runtime and Syntax only, and neither of them depends on it: the Generator
belongs to build tools, a game never ships it, and the code it generates needs Runtime only.

```csharp
using GameEventScript.Api;
using GameEventScript.Generator;

var schemas = GesSchemaSet.Parse(new GesSchemaSource("world.gess", File.ReadAllText("world.gess")));
var parameters = new GesSchemaParameters().With("worldWidth", "2000m");
var code = GesSchemaGenerator.Generate(schemas, ["Bot"], parameters, new GesSchemaGenerateOptions
{
    Language = GesSchemaLanguage.CSharp,
    Namespace = "Game.Net",
});
File.WriteAllText("Bot.g.cs", code);
```

`GesSchemaGenerator.Layout` returns the language-neutral generation layout as JSON
instead. The options select the language, a namespace, `Internal` access, value
conversion (`Values`) and, for Swift classes, `CheckedExclusivity`. The Generator reads
no files: the caller passes the source text and writes the result. Equal inputs give
identical text in the C# and Swift ports.

Errors of the sources, of binding and of parameters are `GesSchemaException`s with a
`schema.*` code; a request that cannot be generated is a `GesGeneratorException` with a
`generate.*` code.

In Unity, import the `generator` DLL set (Syntax, Runtime and Generator) into an
`Editor` folder, so that player builds never include it. The generated C# is C# 9 and
runs under Mono and IL2CPP without `unsafe`, Reflection or allocations on its hot path.

Run `dotnet test implementation/csharp/GameEventScript.Generator/tests`. The
[schema specification](../../../specs/Schemas.md#tools) defines the generated code and
the [generation layout](../../../specs/Schemas.md#generation-layout);
[PublicApi](../../../specs/PublicApi.md#schema-code-generation) defines this API. The NuGet
package is available from the next release; a source checkout can reference the project now.
