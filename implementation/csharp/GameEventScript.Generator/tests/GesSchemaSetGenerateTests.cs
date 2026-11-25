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

    [TestMethod]
    public void AMessageSuffixNamesOnlyMessageTypes()
    {
        const string messages = "wire schema :Shield as { strength: :Number integer between 0 and 100 }\nwire schema :ShieldMessage as { x: :Boolean }\n"
            + "wire messages Actions as (Shield, Move)\nwire message Shield(level: :Number integer between 0 and 10)\nwire message Move(x: :Number integer between 0 and 255)\n";
        var set = GesSchemaSet.Parse(messages);
        string Messages(string[] roots, string suffix, GesSchemaLanguage language = GesSchemaLanguage.CSharp)
            => GesSchemaGenerator.Generate(set, roots, new GesSchemaParameters(), new() { Language = language, MessageSuffix = suffix });
        foreach (var language in Languages)
        {
            // Without a suffix the message and the schema both claim Shield; with one, ShieldMessage meets the schema of that name.
            Assert.AreEqual("generate.nameCollision", Code(() => Messages([":Shield", "Shield()"], "", language)));
            Assert.AreEqual("generate.nameCollision", Code(() => Messages([":ShieldMessage", "Shield()"], "Message", language)));
            StringAssert.Contains(Messages([":Shield", "Shield()"], "Message", language), "ShieldMessage");
            foreach (var suffix in new[] { "a-b", "Ä", " " })
                Assert.AreEqual("generate.usage", Code(() => Messages(["Shield()"], suffix, language)));
            StringAssert.Contains(Messages(["Move"], "1x", language), "Move1x");
            Assert.AreEqual("generate.usage", Code(() => GesSchemaGenerator.GenerateModels(set, [":Shield"], new() { Language = language, MessageSuffix = "Message" })));
        }

        // Set members keep the declared names; only the message types they hold take the suffix.
        var actions = Messages(["Actions"], "Message");
        StringAssert.Contains(actions, "public MoveMessage Move");
        StringAssert.Contains(actions, "case Member.Move:");
        Assert.DoesNotContain("Member.MoveMessage", actions);

        // The suffix exists only in code: hashes and paths keep the declared name.
        string SchemaHash(string code) => Regex.Match(code, "SchemaHash = (0x[0-9a-f]{16})").Groups[1].Value;
        var plain = Messages(["Shield()"], "");
        var suffixed = Messages(["Shield()"], "Message");
        Assert.AreNotEqual("", SchemaHash(plain));
        Assert.AreEqual(SchemaHash(plain), SchemaHash(suffixed));
        Assert.AreEqual(plain, suffixed.Replace("ShieldMessage", "Shield"));
    }

    [TestMethod]
    public void ANetworkAndAMessageOfOneNameNeedAQualifiedRoot()
    {
        var set = GesSchemaSet.Parse("wire message Lobby(level: :Number integer between 0 and 10)\nnetwork :Lobby as {\n  client sends Lobby reliable\n}\n");
        foreach (var language in Languages)
        {
            string Lobby(string root) => GesSchemaGenerator.Generate(set, [root], new GesSchemaParameters(), new() { Language = language, MessageSuffix = "Message" });
            Assert.AreEqual("schema.ambiguousName", Assert.ThrowsExactly<GesSchemaException>(() => Lobby("Lobby")).Code);
            StringAssert.Contains(Lobby(":Lobby"), "LobbyMessage");
            Assert.AreNotEqual(Lobby(":Lobby"), Lobby("Lobby()"));
        }

        Assert.AreEqual("schema.ambiguousName", Assert.ThrowsExactly<GesSchemaException>(() => set.Schema("Lobby")).Code);
        Assert.AreEqual(GesSchemaKind.Message, set.Schema("Lobby()").Kind);
    }
}
