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

    private static string Models(string source, string space) => GesSchemaGenerator.GenerateModels(GesSchemaSet.Parse(source), ["Document"], new GesSchemaGenerateOptions { Namespace = space, Documents = true });

    [TestMethod]
    public void TheDocumentTypeTakesNoGeneratedName()
    {
        // In a namespace the reading type is Document, which a model root of that name would also be.
        Assert.AreEqual("generate.nameCollision", Code(() => Models("schema :Document as { a: :Boolean }\n", "Game.Units")));
        StringAssert.Contains(Models("schema :Document as { a: :Boolean }\n", ""), "abstract class ModelDocument");
    }

    [TestMethod]
    public void TheValuesOfRecordDefaultsTakeNoTypeName()
    {
        // The values of the record defaults of :S are held by SDefaults, which the schema of that name would also be.
        var set = GesSchemaSet.Parse("schema :C as { a: :Number default 1 }\nschema :SDefaults as { b: :Boolean }\nschema :S as {\n  c: :C default [:]\n  d: :SDefaults\n}\n");
        foreach (var language in new[] { GesSchemaLanguage.CSharp, GesSchemaLanguage.Swift })
        {
            Assert.AreEqual("generate.nameCollision", Code(() => GesSchemaGenerator.GenerateModels(set, ["S"], new GesSchemaGenerateOptions { Language = language, Values = true })));
            Assert.AreEqual("generate.nameCollision", Code(() => GesSchemaGenerator.GenerateModels(set, ["S"], new GesSchemaGenerateOptions { Language = language, Bindings = true })));
            StringAssert.Contains(GesSchemaGenerator.GenerateModels(set, ["S"], new GesSchemaGenerateOptions { Language = language }), "SDefaults");
        }
    }

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
        Assert.AreEqual("generate.nameCollision", Code(() => Generate("schema :S as {\n  mode: :Flags(#rawValue, #other)\n}\n", "S", GesSchemaLanguage.Swift)));
        StringAssert.Contains(Generate("schema :S as {\n  mode: :Flags(#rawValue, #other)\n}\n", "S", GesSchemaLanguage.CSharp), "RawValue = 1UL << 0,");
        StringAssert.Contains(Generate("schema :S as {\n  s: :Boolean\n}\n", "S", GesSchemaLanguage.Swift), "public var sValue: Bool = false");
    }

    [TestMethod]
    public void SwiftModelsEscapeKeywordsOnlyWhereSwiftNeedsIt()
    {
        var set = GesSchemaSet.Parse("model schema :S as {\n  default: :Text\n  in: :Boolean\n  var: :Boolean\n}\n");
        var options = new GesSchemaGenerateOptions { Language = GesSchemaLanguage.Swift, Values = true, Bindings = true };
        var swift = GesSchemaGenerator.GenerateModels(set, GesSchemaGenerator.ModelRoots(set, "default"), options);
        StringAssert.Contains(swift, "var `default`: String");
        StringAssert.Contains(swift, "init(default: String, in: Bool, `var`: Bool)");
        StringAssert.Contains(swift, "self.`default` = `default`");
        StringAssert.Contains(swift, "S(default: field0, in: field1, var: field2)");
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
    [TestMethod]
    public void SplitOutputPlacesEachDeclarationInTheFileOfItsDeclaration()
    {
        var set = GesSchemaSet.Parse(
            new GesSchemaSource("maps/world.gess", "tags :Side as (#left, #right)\nschema :World as {\n  bot: :Bot\n  side: :Side\n}\n"),
            new GesSchemaSource("bots.gess", "schema :Bot as {\n  names: :List(:Text max 4) max 2\n}\n"));
        foreach (var language in new[] { GesSchemaLanguage.CSharp, GesSchemaLanguage.Swift })
        {
            var extension = language == GesSchemaLanguage.Swift ? ".swift" : ".g.cs";
            var options = new GesSchemaGenerateOptions { Language = language, Namespace = "Game.Wire", Values = true, Split = GesSchemaSplit.Schema };
            var files = GesSchemaGenerator.GenerateFiles(set, ["World"], new GesSchemaParameters(), options);
            CollectionAssert.AreEqual(new[] { "Game.Wire.Bot", "Game.Wire.Side", "Game.Wire.World", "_Game.Wire", ".ges-generated" },
                files.Select(file => file.Path.Replace(extension, "", StringComparison.Ordinal).Replace(".json", "", StringComparison.Ordinal)).ToArray());
            StringAssert.Contains(files[0].Text, "NamesItem");
            Assert.IsFalse(files[2].Text.Contains("NamesItem", StringComparison.Ordinal));
            StringAssert.Contains(files[3].Text, "__GesSchemas");
            StringAssert.Contains(files[4].Text, "\"split\": \"schema\"");
            StringAssert.Contains(files[4].Text, "\"files\": [\n    \"Game.Wire.Bot" + extension);
            options.Split = GesSchemaSplit.File;
            files = GesSchemaGenerator.GenerateFiles(set, ["World"], new GesSchemaParameters(), options);
            CollectionAssert.AreEqual(new[] { "Game.Wire.bots" + extension, "Game.Wire.world" + extension, "_Game.Wire" + extension, ".ges-generated.json" }, files.Select(file => file.Path).ToArray());
            StringAssert.Contains(files[1].Text, "Side");
        }

        var swift = GesSchemaGenerator.GenerateFiles(set, ["World"], new GesSchemaParameters(), new GesSchemaGenerateOptions { Language = GesSchemaLanguage.Swift, Namespace = "Game.Wire", Split = GesSchemaSplit.Schema });
        StringAssert.Contains(swift[0].Text, "extension Game.Wire {\n");
        StringAssert.Contains(swift[3].Text, "public enum Game {\n    /// The namespace `Wire` of generated schema types.\n    public enum Wire {}\n}\n");
        Assert.IsFalse(swift[0].Text.Contains("from the root", StringComparison.Ordinal));
    }

    [TestMethod]
    public void SplitOutputRejectsMismatchedRequestsAndCollidingFileNames()
    {
        var set = GesSchemaSet.Parse("schema :S as {\n  on: :Boolean\n}\n");
        var split = new GesSchemaGenerateOptions { Split = GesSchemaSplit.Schema };
        Assert.AreEqual("generate.usage", Code(() => GesSchemaGenerator.Generate(set, ["S"], new GesSchemaParameters(), split)));
        Assert.AreEqual("generate.usage", Code(() => GesSchemaGenerator.GenerateModels(set, ["S"], split)));
        Assert.AreEqual("generate.usage", Code(() => GesSchemaGenerator.GenerateFiles(set, ["S"], new GesSchemaParameters(), new GesSchemaGenerateOptions())));
        Assert.AreEqual("generate.usage", Code(() => GesSchemaGenerator.GenerateModelFiles(set, ["S"], new GesSchemaGenerateOptions())));
        var cases = GesSchemaSet.Parse("schema :Ab as {\n  b: :AB\n}\nschema :AB as {\n  on: :Boolean\n}\n");
        Assert.AreEqual("generate.nameCollision", Code(() => GesSchemaGenerator.GenerateFiles(cases, ["Ab"], new GesSchemaParameters(), split)));
        var sources = GesSchemaSet.Parse(new GesSchemaSource("a/s.gess", "schema :A as {\n  b: :B\n}\n"), new GesSchemaSource("b/s.gess", "schema :B as {\n  on: :Boolean\n}\n"));
        Assert.AreEqual("generate.nameCollision", Code(() => GesSchemaGenerator.GenerateModelFiles(sources, ["A"], new GesSchemaGenerateOptions { Split = GesSchemaSplit.File })));
        Assert.HasCount(4, GesSchemaGenerator.GenerateModelFiles(sources, ["A"], new GesSchemaGenerateOptions { Split = GesSchemaSplit.Schema }));
    }

    [TestMethod]
    public void NestedTypesMustNotHideTopLevelTypes()
    {
        var set = GesSchemaSet.Parse("tags :ModeTag as (#p, #q, #r)\nschema :SlotsItem as {\n  a: :Boolean\n}\nschema :Squad as {\n  mode: :Tag(#a, #b)\n  other: :ModeTag\n}\nschema :Crew as {\n  slots: :List(:Text max 4 | :Number integer between 0 and 3) max 3\n  spare: :SlotsItem\n}\n");
        foreach (var language in new[] { GesSchemaLanguage.CSharp, GesSchemaLanguage.Swift })
        {
            var options = new GesSchemaGenerateOptions { Language = language };
            Assert.AreEqual("generate.nameCollision", Code(() => GesSchemaGenerator.Generate(set, ["Squad"], new GesSchemaParameters(), options)));
            Assert.AreEqual("generate.nameCollision", Code(() => GesSchemaGenerator.Generate(set, ["Crew"], new GesSchemaParameters(), options)));
            Assert.AreEqual("generate.nameCollision", Code(() => GesSchemaGenerator.GenerateModels(set, ["Squad"], options)));
        }
    }

    [TestMethod]
    public void TypesMustNotTakeNamesThatGeneratedCodeUses()
    {
        var swift = new GesSchemaGenerateOptions { Language = GesSchemaLanguage.Swift };
        var csharp = new GesSchemaGenerateOptions();
        foreach (var name in new[] { "String", "Int", "Type", "Swift" })
        {
            var set = GesSchemaSet.Parse($"schema :{name} as {{\n  on: :Boolean\n}}\n");
            Assert.AreEqual("generate.nameCollision", Code(() => GesSchemaGenerator.Generate(set, [name], new GesSchemaParameters(), swift)));
            Assert.AreEqual("generate.nameCollision", Code(() => GesSchemaGenerator.GenerateModels(set, [name], swift)));
            GesSchemaGenerator.Generate(set, [name], new GesSchemaParameters(), csharp);
        }

        var runtime = GesSchemaSet.Parse("schema :GesValue as {\n  on: :Boolean\n}\n");
        foreach (var options in new[] { csharp, swift })
        {
            Assert.AreEqual("generate.nameCollision", Code(() => GesSchemaGenerator.Generate(runtime, ["GesValue"], new GesSchemaParameters(), options)));
            Assert.AreEqual("generate.nameCollision", Code(() => GesSchemaGenerator.GenerateModels(runtime, ["GesValue"], options)));
        }

        var member = GesSchemaSet.Parse("schema :S as {\n  gesValue: :Boolean\n  gesture: :Boolean\n}\n");
        Assert.AreEqual("generate.nameCollision", Code(() => GesSchemaGenerator.Generate(member, ["S"], new GesSchemaParameters(), csharp)));
        GesSchemaGenerator.Generate(GesSchemaSet.Parse("schema :Gesture as {\n  gesture: :Boolean\n}\n"), ["Gesture"], new GesSchemaParameters(), csharp);
    }

    [TestMethod]
    public void SwiftModelsReserveTheHashableMembers()
    {
        foreach (var field in new[] { "hashValue", "hash" })
        {
            var set = GesSchemaSet.Parse($"schema :Unit as {{\n  {field}: :Number integer between 0 and 9\n}}\n");
            Assert.AreEqual("generate.nameCollision", Code(() => GesSchemaGenerator.GenerateModels(set, ["Unit"], new GesSchemaGenerateOptions { Language = GesSchemaLanguage.Swift })));
            GesSchemaGenerator.GenerateModels(set, ["Unit"], new GesSchemaGenerateOptions());
        }
    }

    [TestMethod]
    public void ManifestNamesTheValueConversionsThatDocumentsInclude()
    {
        var set = GesSchemaSet.Parse("schema :S as {\n  on: :Boolean\n}\n");
        var documents = GesSchemaGenerator.GenerateModelFiles(set, ["S"], new GesSchemaGenerateOptions { Split = GesSchemaSplit.Schema, Documents = true });
        var both = GesSchemaGenerator.GenerateModelFiles(set, ["S"], new GesSchemaGenerateOptions { Split = GesSchemaSplit.Schema, Documents = true, Values = true });
        Assert.HasCount(both.Count, documents);
        for (var index = 0; index < both.Count; index++) Assert.AreEqual(both[index].Text, documents[index].Text, both[index].Path);
        StringAssert.Contains(documents[^1].Text, "\"values\": true");
    }
}
