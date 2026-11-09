// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Generator;

namespace GameEventScript.Tests.Generator;

/// <summary>Checks the generation rules that the corpus harness cannot reach: names, namespaces and the shape of types.</summary>
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
        StringAssert.Contains(Generate("schema :S as {\n  s: :Boolean\n}\n", "S", GesSchemaLanguage.CSharp), "public bool SValue;");
        Assert.AreEqual("generate.nameCollision", Code(() => Generate("schema :S as {\n  s: :Boolean\n  sValue: :Boolean\n}\n", "S", GesSchemaLanguage.CSharp)));
        Assert.AreEqual("generate.nameCollision", Code(() => Generate("schema :S as {\n  quantize: :Boolean\n}\n", "S", GesSchemaLanguage.Swift)));
        Assert.AreEqual("generate.nameCollision", Code(() => Generate("schema :S as {\n  state: :Tag(#a)\n  stateTag: :Boolean\n}\n", "S", GesSchemaLanguage.CSharp)));
        Assert.AreEqual("generate.nameCollision", Code(() => Generate("schema :S as {\n  heat: :Boolean optional\n  hasHeat: :Boolean\n}\n", "S", GesSchemaLanguage.Swift)));
        StringAssert.Contains(Generate("schema :S as {\n  s: :Boolean\n}\n", "S", GesSchemaLanguage.Swift), "public var sValue: Bool = false");
    }

    [TestMethod]
    public void InvalidNamespacesAreRejected()
    {
        Assert.AreEqual("generate.invalidNamespace", Code(() => Generate("schema :S as {\n  a: :Boolean\n}\n", "S", GesSchemaLanguage.CSharp, "Game..Net")));
        Assert.AreEqual("generate.invalidNamespace", Code(() => Generate("schema :S as {\n  a: :Boolean\n}\n", "S", GesSchemaLanguage.CSharp, "class")));
        Assert.AreEqual("generate.invalidNamespace", Code(() => Generate("schema :S as {\n  a: :Boolean\n}\n", "S", GesSchemaLanguage.Swift, "1Wire")));
    }

    [TestMethod]
    public void StorageMakesClassesAndMessagesGetTagsAndSets()
    {
        const string source = "schema :Cell as {\n  x: :Boolean\n}\nschema :S as {\n  name: :Text max 4\n  cells: :List(:Cell) max 2\n  words: :List(:Text max 2) max 2\n  heat: :Number optional\n}\n"
            + "message Hit(target: :Number integer between 0 and 9) tags (#critical)\nmessage Miss()\nmessages :Events as (Hit, Miss)\n";
        var set = GesSchemaSet.Parse(source);
        var csharp = GesSchemaGenerator.Generate(set, ["S", "Events"], new GesSchemaParameters(), new GesSchemaGenerateOptions());
        StringAssert.Contains(csharp, "public sealed partial class S");
        StringAssert.Contains(csharp, "public partial struct Cell");
        StringAssert.Contains(csharp, "public List<Cell> Cells = new List<Cell>();");
        StringAssert.Contains(csharp, "public sealed partial class WordsItem");
        StringAssert.Contains(csharp, "public ReadOnlySpan<byte> NameUtf8");
        StringAssert.Contains(csharp, "public double? HeatOptional");
        StringAssert.Contains(csharp, "public bool Critical;");
        StringAssert.Contains(csharp, "public partial struct Events");
        StringAssert.Contains(csharp, "public Member Selected;");
        var swift = GesSchemaGenerator.Generate(set, ["S", "Events"], new GesSchemaParameters(), new GesSchemaGenerateOptions { Language = GesSchemaLanguage.Swift });
        StringAssert.Contains(swift, "public final class S {");
        StringAssert.Contains(swift, "public var cells: [Cell] = []");
        StringAssert.Contains(swift, "public var nameUTF8: [UInt8] = []");
        StringAssert.Contains(swift, "public var heatOptional: Double? {");
        StringAssert.Contains(swift, "public var critical = false");
        StringAssert.Contains(swift, "public var selected: Member = .hit");
        Assert.AreEqual("generate.nameCollision", Code(() => Generate("schema :S as {\n  name: :Text max 4\n  nameUtf8: :Boolean\n}\n", "S", GesSchemaLanguage.CSharp)));
        Assert.AreEqual("generate.nameCollision", Code(() => Generate("message M(critical: :Boolean) tags (#critical)\n", "M", GesSchemaLanguage.Swift)));
    }

    [TestMethod]
    public void SchemasGetDeltasWithKeyedLists()
    {
        const string source = "schema :Item as {\n  id: :Number integer between 0 and 9 key\n}\nschema :S as {\n  x: :Number integer between 0 and 9 delta within 2\n  name: :Text max 4\n}\n"
            + "schema :K as {\n  items: :List(:Item) max 2\n}\nmessage M(a: :Boolean)\n";
        var set = GesSchemaSet.Parse(source);
        var csharp = GesSchemaGenerator.Generate(set, ["S", "K", "M"], new GesSchemaParameters(), new GesSchemaGenerateOptions());
        StringAssert.Contains(csharp, "public int WriteDelta(S baseline, Span<byte> buffer)");
        StringAssert.Contains(csharp, "public void CopyFrom(S other)");
        StringAssert.Contains(csharp, "public bool WireEquals(S other)");
        StringAssert.Contains(csharp, "GesWireRules.WriteDeltaRange(ref writer");
        StringAssert.Contains(csharp, "public int WriteDelta(K baseline, Span<byte> buffer)");
        StringAssert.Contains(csharp, "private bool __WriteKeyed0(K baseline, ref GesBitWriter writer, string[] paths, int at)");
        StringAssert.Contains(csharp, "internal readonly ulong __KeyOrder(string[] paths, int at) => __Wire0(Id, paths, at);");
        StringAssert.Contains(csharp, "var order = global::Item.__ReadKeyOrder(ref reader, paths, at + 2);");
        Assert.IsFalse(csharp.Contains("WriteDelta(in M baseline", StringComparison.Ordinal));
        var spaced = GesSchemaGenerator.Generate(set, ["K"], new GesSchemaParameters(), new GesSchemaGenerateOptions { Namespace = "Game.Net" });
        StringAssert.Contains(spaced, "global::Game.Net.Item.__ReadKeyOrder(");
        var swift = GesSchemaGenerator.Generate(set, ["S", "K", "M"], new GesSchemaParameters(), new GesSchemaGenerateOptions { Language = GesSchemaLanguage.Swift });
        StringAssert.Contains(swift, "public func writeDelta(against baseline: S, into buffer: UnsafeMutableRawBufferPointer) throws(GesWireError) -> Int {");
        StringAssert.Contains(swift, "public func copy(from other: S) {");
        StringAssert.Contains(swift, "public func quantize() throws(GesWireError) {");
        StringAssert.Contains(swift, "public func writeDelta(against baseline: K, into buffer: UnsafeMutableRawBufferPointer) throws(GesWireError) -> Int {");
        StringAssert.Contains(swift, "let order = try Item.__readKeyOrder(&reader, paths, at + 2)");
    }

    [TestMethod]
    public void ValuesEmbedTheSourcesAndParameters()
    {
        const string source = "parameter p: :Percentage\nparameter w: :Quantity(°)\nschema :S as {\n  a: :Percentage between 0% and p step 1%\n  b: :Quantity(°) between 0° and w\n}\nmessage M(a: :Boolean)\n";
        var set = GesSchemaSet.Parse(new GesSchemaSource("dir/s.gess", source));
        var parameters = new GesSchemaParameters().With("p", "50%").With("w", "90°");
        var csharp = GesSchemaGenerator.Generate(set, ["S", "M"], parameters, new GesSchemaGenerateOptions { Values = true });
        StringAssert.Contains(csharp, "public readonly global::GameEventScript.Runtime.Values.GesValue ToValue()");
        StringAssert.Contains(csharp, "public void ReadMessage(GameEventScriptMessage message)");
        StringAssert.Contains(csharp, "new GesSchemaSource(\"s.gess\", string.Join(\"\\n\", new[]");
        StringAssert.Contains(csharp, "\"parameter w: :Quantity(\\u00B0)\",");
        StringAssert.Contains(csharp, ".With(\"p\", \"5e1%\").With(\"w\", \"9e1\\u00B0\")");
        Assert.IsFalse(GesSchemaGenerator.Generate(set, ["S"], parameters, new GesSchemaGenerateOptions()).Contains("ToValue", StringComparison.Ordinal));
        var swift = GesSchemaGenerator.Generate(set, ["S", "M"], parameters, new GesSchemaGenerateOptions { Language = GesSchemaLanguage.Swift, Values = true });
        StringAssert.Contains(swift, "public func toValue() throws -> GesValue {");
        StringAssert.Contains(swift, "public mutating func read(message: GameEventScriptMessage) throws {");
        StringAssert.Contains(swift, "try parameters.with(\"w\", literal: \"9e1\\u{B0}\")");
        const string storage = "schema :T as {\n  name: :Text max 4\n  flag: :Boolean\n}\n";
        var uncheckedCode = GesSchemaGenerator.Generate(GesSchemaSet.Parse(storage), ["T"], new GesSchemaParameters(), new GesSchemaGenerateOptions { Language = GesSchemaLanguage.Swift });
        StringAssert.Contains(uncheckedCode, "@exclusivity(unchecked) public var flag: Bool = false");
        StringAssert.Contains(uncheckedCode, "@exclusivity(unchecked) public var nameUTF8: [UInt8] = []");
        var checkedCode = GesSchemaGenerator.Generate(GesSchemaSet.Parse(storage), ["T"], new GesSchemaParameters(), new GesSchemaGenerateOptions { Language = GesSchemaLanguage.Swift, CheckedExclusivity = true });
        Assert.IsFalse(checkedCode.Contains("@exclusivity", StringComparison.Ordinal));
        Assert.IsFalse(GesSchemaGenerator.Generate(set, ["S"], parameters, new GesSchemaGenerateOptions { Language = GesSchemaLanguage.Swift }).Contains("@exclusivity", StringComparison.Ordinal));
        const string collision = "schema :S as {\n  toValue: :Boolean\n}\n";
        Assert.AreEqual("generate.nameCollision", Code(() => GesSchemaGenerator.Generate(GesSchemaSet.Parse(collision), ["S"], new GesSchemaParameters(), new GesSchemaGenerateOptions { Values = true })));
        Assert.AreEqual("generate.nameCollision", Code(() => GesSchemaGenerator.Generate(GesSchemaSet.Parse(collision), ["S"], new GesSchemaParameters(), new GesSchemaGenerateOptions { Language = GesSchemaLanguage.Swift, Values = true })));
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
