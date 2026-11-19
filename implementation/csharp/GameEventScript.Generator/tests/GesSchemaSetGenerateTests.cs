// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using GameEventScript.Api;
using GameEventScript.Generator;

namespace GameEventScript.Tests.Generator;

/// <summary>Checks the options and the shape of generated schema sets.</summary>
[TestClass]
public sealed class GesSchemaSetGenerateTests
{
    private const string Source = "parameter size: :Quantity(m)\nwire schema :S as { x: :Quantity(m) between 0m and size step 1m }\n";

    private static readonly GesSchemaLanguage[] Languages = [GesSchemaLanguage.CSharp, GesSchemaLanguage.Swift];

    private static string Generate(GesSchemaGenerateOptions options, string size = "64m", string source = Source)
        => GesSchemaGenerator.Generate(GesSchemaSet.Parse(source), ["S"], new GesSchemaParameters().With("size", size), options);

    private static string Code(Action action) => Assert.ThrowsExactly<GesGeneratorException>(action).Code;

    private static string Hash(string code) => Regex.Match(code, "(?:Hash => |hash: UInt64 = )(0x[0-9A-Fa-f]{16})").Groups[1].Value;

    [TestMethod]
    public void OptionsMustNameTypesAndModules()
    {
        foreach (var language in Languages)
        {
            Assert.AreEqual("generate.usage", Code(() => Generate(new() { Language = language, SchemaSet = "Set", UseSchemaSet = "Game.Set" })));
            Assert.AreEqual("generate.usage", Code(() => Generate(new() { Language = language, SchemaSet = "1Set" })));
            Assert.AreEqual("generate.usage", Code(() => Generate(new() { Language = language, SchemaSet = "Game.Set" })));
            Assert.AreEqual("generate.usage", Code(() => Generate(new() { Language = language, SchemaSet = "class" })));
            Assert.AreEqual("generate.usage", Code(() => Generate(new() { Language = language, UseSchemaSet = "Game..Set" })));
            Assert.AreEqual("generate.nameCollision", Code(() => Generate(new() { Language = language, SchemaSet = "S" })));
        }

        // A C# member cannot share its class's name; a Swift member can.
        foreach (var name in new[] { "Set", "Hash", "Parameters", "Bind", "BindNetwork", "ReadDocument" })
            Assert.AreEqual("generate.nameCollision", Code(() => Generate(new() { Language = GesSchemaLanguage.CSharp, SchemaSet = name })));
        StringAssert.Contains(Generate(new() { Language = GesSchemaLanguage.Swift, SchemaSet = "Set" }), "enum Set");

        // Only Swift files import modules.
        Assert.AreEqual("generate.usage", Code(() => Generate(new() { Language = GesSchemaLanguage.CSharp, Imports = ["Shared"] })));
        Assert.AreEqual("generate.usage", Code(() => Generate(new() { Language = GesSchemaLanguage.Swift, Imports = ["Shared.Net"] })));
        StringAssert.Contains(Generate(new() { Language = GesSchemaLanguage.Swift, Imports = ["Shared"] }), "\nimport Shared\n");
    }

    [TestMethod]
    public void TheSetHashCoversSourcesAndParameters()
    {
        var csharp = Generate(new() { SchemaSet = "GameSchemas" });
        var swift = Generate(new() { Language = GesSchemaLanguage.Swift, SchemaSet = "GameSchemas" });
        StringAssert.Contains(csharp, "public static class GameSchemas");
        StringAssert.Contains(csharp, "public static GesBoundSchema Bind(string root, ulong bindingHash)");
        StringAssert.Contains(swift, "public enum GameSchemas");
        Assert.AreNotEqual("", Hash(csharp));
        Assert.AreEqual(Hash(csharp), Hash(swift));
        Assert.AreNotEqual(Hash(csharp), Hash(Generate(new() { SchemaSet = "GameSchemas" }, "65m")));
        Assert.AreNotEqual(Hash(csharp), Hash(Generate(new() { SchemaSet = "GameSchemas" }, source: Source + "\n")));
        Assert.AreEqual(Hash(csharp), Hash(Generate(new() { SchemaSet = "GameSchemas" })));
    }

    [TestMethod]
    public void UsingASetDeclaresNone()
    {
        foreach (var language in Languages)
        {
            var code = Generate(new() { Language = language, UseSchemaSet = "Shared.GameSchemas" });
            Assert.DoesNotContain("GameSchemas\n", code);
        }
    }
}
