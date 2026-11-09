// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Generator;

namespace GameEventScript.Tests.Generator;

/// <summary>Checks the generation rules that the corpus harness cannot reach: names, namespaces and unsupported types.</summary>
[TestClass]
public sealed class GesSchemaGenerateTests
{
    private static string Generate(string source, string root, GesSchemaLanguage language, string space = "")
        => GesSchemaGenerator.Generate(GesSchemaSet.Parse(source), [root], new GesSchemaParameters(), new GesSchemaGenerateOptions { Language = language, Namespace = space });

    private static string Code(Action action) => Assert.ThrowsExactly<GesGeneratorException>(action).Code;

    [TestMethod]
    public void NamesFollowTheLanguage()
    {
        const string source = "schema :S as {\n  default: :Boolean\n  state: :Tag(#idle, #in)\n}\n";
        var csharp = Generate(source, "S", GesSchemaLanguage.CSharp, "Game.Net");
        StringAssert.Contains(csharp, "namespace Game.Net\n{");
        StringAssert.Contains(csharp, "public bool Default;");
        StringAssert.Contains(csharp, "public StateTag State;");
        StringAssert.Contains(csharp, "            In,\n");
        var swift = Generate(source, "S", GesSchemaLanguage.Swift, "Game.Net");
        StringAssert.Contains(swift, "public enum Game {\n    /// The namespace `Net` of generated schema types.\n    public enum Net {");
        StringAssert.Contains(swift, "public var `default`: Bool = false");
        StringAssert.Contains(swift, "case `in`");
        StringAssert.Contains(swift, "public struct S: Sendable {");
    }

    [TestMethod]
    public void CollidingNamesAreRejected()
    {
        Assert.AreEqual("generate.nameCollision", Code(() => Generate("schema :S as {\n  write: :Boolean\n}\n", "S", GesSchemaLanguage.CSharp)));
        Assert.AreEqual("generate.nameCollision", Code(() => Generate("schema :S as {\n  write: :Boolean\n}\n", "S", GesSchemaLanguage.Swift)));
        Assert.AreEqual("generate.nameCollision", Code(() => Generate("schema :S as {\n  s: :Boolean\n}\n", "S", GesSchemaLanguage.CSharp)));
        Assert.AreEqual("generate.nameCollision", Code(() => Generate("schema :S as {\n  state: :Tag(#a)\n  stateTag: :Boolean\n}\n", "S", GesSchemaLanguage.CSharp)));
        Assert.AreEqual("generate.nameCollision", Code(() => Generate("schema :S as {\n  heat: :Boolean optional\n  hasHeat: :Boolean\n}\n", "S", GesSchemaLanguage.Swift)));
        StringAssert.Contains(Generate("schema :S as {\n  s: :Boolean\n}\n", "S", GesSchemaLanguage.Swift), "public var s: Bool = false");
    }

    [TestMethod]
    public void InvalidNamespacesAndUnsupportedTypesAreRejected()
    {
        Assert.AreEqual("generate.invalidNamespace", Code(() => Generate("schema :S as {\n  a: :Boolean\n}\n", "S", GesSchemaLanguage.CSharp, "Game..Net")));
        Assert.AreEqual("generate.invalidNamespace", Code(() => Generate("schema :S as {\n  a: :Boolean\n}\n", "S", GesSchemaLanguage.CSharp, "class")));
        Assert.AreEqual("generate.invalidNamespace", Code(() => Generate("schema :S as {\n  a: :Boolean\n}\n", "S", GesSchemaLanguage.Swift, "1Wire")));
        Assert.AreEqual("generate.unsupported", Code(() => Generate("schema :S as {\n  a: :Text max 4\n}\n", "S", GesSchemaLanguage.CSharp)));
        Assert.AreEqual("generate.unsupported", Code(() => Generate("message M(a: :Boolean)\n", "M", GesSchemaLanguage.Swift)));
    }

    [TestMethod]
    public void SharedTypesAreGeneratedOnce()
    {
        const string source = "schema :Inner as {\n  a: :Boolean\n}\nschema :A as {\n  i: :Inner\n}\nschema :B as {\n  i: :Inner\n}\n";
        var text = GesSchemaGenerator.Generate(GesSchemaSet.Parse(source), ["A", "B"], new GesSchemaParameters(), new GesSchemaGenerateOptions());
        Assert.HasCount(2, text.Split("partial struct Inner"));
        StringAssert.Contains(text, "from the roots A, B.");
        StringAssert.Contains(text, "\"i.a\",");
    }
}
