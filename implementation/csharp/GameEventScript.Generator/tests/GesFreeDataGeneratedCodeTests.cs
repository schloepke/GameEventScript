// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Generator;
using GameEventScript.Runtime.Values;
using Model = GameEventScript.Tests.Generator.FreeData.Model;

namespace GameEventScript.Tests.Generator;

// The checked-in code of conformance/fixtures/SchemaGeneration/freedata.gess: models that hold Maps and Lists without an element
// type as GES values, with the empty defaults; wire code still rejects both.
[TestClass]
public sealed class GesFreeDataGeneratedCodeTests
{
    private static GesSchemaSet Set()
    {
        var fixture = Path.Combine(TestRepositoryPaths.ConformanceDirectory, "fixtures", "SchemaGeneration", "freedata.gess");
        return GesSchemaSet.Parse(new GesSchemaSource("freedata.gess", File.ReadAllText(fixture)));
    }

    private static GesSchemaGenerateOptions Options(GesSchemaLanguage language, string space) => new() { Language = language, Namespace = space, Access = GesSchemaAccess.Internal, Values = true, Bindings = true, Documents = true };

    private static GesDataMap Map(params (string, GesValue)[] entries) => GesDataMap.FromValue(GesValue.GesMap(entries))!.Value;

    private static GesValue Program(params (string, GesValue)[] fields)
    {
        var all = new List<(string, GesValue)> { ("script", GesValue.GesText("run")), ("layers", GesValue.GesList([])), ("pick", GesValue.GesText("first")) };
        foreach (var field in fields)
        {
            all.RemoveAll(entry => entry.Item1 == field.Item1);
            all.Add(field);
        }

        return GesValue.GesRecord("Program", [.. all]);
    }

    // Free data with a nested map, a list holding mixed items and a point.
    private static GesDataMap Data() => Map(
        ("hp", GesValue.GesInteger(3)),
        ("inner", GesValue.GesMap(("deep", GesValue.GesBoolean(true)))),
        ("items", GesValue.GesList([GesValue.GesInteger(1), GesValue.GesText("two")])),
        ("pos", GesValue.GesPoint(1, 2)));

    [TestMethod]
    public void CheckedInCodeIsCurrent()
    {
        var set = Set();
        var csharp = Path.Combine(TestRepositoryPaths.CSharpDirectory, "GameEventScript.Generator", "tests", "Generated");
        var swift = Path.Combine(TestRepositoryPaths.Root, "implementation", "swift", "GameEventScriptConformance", "Tests", "GameEventScriptConformanceTests", "Generated");
        var model = GesSchemaGenerator.ModelRoots(set, "freedata");
        Assert.AreEqual(File.ReadAllText(Path.Combine(csharp, "FreeDataModel.g.cs")), GesSchemaGenerator.GenerateModels(set, model, Options(GesSchemaLanguage.CSharp, "GameEventScript.Tests.Generator.FreeData.Model")));
        Assert.AreEqual(File.ReadAllText(Path.Combine(swift, "FreeDataModel.swift")), GesSchemaGenerator.GenerateModels(set, model, Options(GesSchemaLanguage.Swift, "FreeDataModel")));
    }

    [TestMethod]
    public void InitializerTakesTheEmptyDefaults()
    {
        var program = new Model.Program("run", null, [], new Model.Program.PickAlternative.Text("first"));
        Assert.AreEqual(0, program.Data.Length);
        Assert.IsEmpty(program.Args);
        Assert.IsEmpty(program.Steps);
        Assert.IsEmpty(new Model.Scene("stage", null).Programs);

        var parameters = typeof(Model.Program).GetConstructors().Single(constructor => constructor.GetParameters().Length > 0).GetParameters();
        CollectionAssert.AreEqual(new[] { false, false, false, false, true, true, true }, parameters.Select(parameter => parameter.HasDefaultValue).ToArray());
    }

    [TestMethod]
    public void MapsAndUntypedListsReadAndRoundTrip()
    {
        GesValue[] items = [GesValue.GesInteger(1), GesValue.GesText("two"), GesValue.GesPoint(0, 1), GesValue.GesRecord("Other", ("a", GesValue.GesInteger(2)))];
        var args = GesValue.GesList(items);
        var program = Model.Program.FromValue(Program(
            ("data", Data().ToValue()),
            ("args", args),
            ("extra", GesValue.GesMap(("note", GesValue.GesText("x")))),
            ("layers", GesValue.GesList([GesValue.GesMap(("z", GesValue.GesInteger(1))), GesValue.GesMap()])),
            ("pick", GesValue.GesMap(("p", GesValue.GesNothing())))));
        Assert.AreEqual(Data(), program.Data);
        Assert.AreEqual(GesValue.GesBoolean(true), program.Data.Get("inner")!.Value.AsMap()!.Get("deep"));
        Assert.AreEqual(GesValue.GesPoint(1, 2), program.Data.Get("pos"));
        CollectionAssert.AreEqual(items, program.Args.ToArray());
        Assert.AreEqual(args, program.Args.ToValue());
        Assert.AreEqual(GesValue.GesText("x"), program.Extra!.Value.Get("note"));
        Assert.HasCount(2, program.Layers);
        Assert.IsInstanceOfType<Model.Program.PickAlternative.Map>(program.Pick);

        var again = Model.Program.FromValue(program.ToValue());
        Assert.AreEqual(program, again);
        Assert.AreEqual(program.GetHashCode(), again.GetHashCode());
        again.Data = Map(("hp", GesValue.GesInteger(4)));
        Assert.AreNotEqual(program, again);
    }

    [TestMethod]
    public void LeftOutDataTakesTheEmptyDefault()
    {
        var program = Model.Program.FromValue(Program());
        Assert.AreEqual(0, program.Data.Length);
        Assert.IsEmpty(program.Args);
        Assert.IsEmpty(program.Steps);
        Assert.IsNull(program.Extra);
        var scene = Model.Scene.FromValue(GesValue.GesRecord("Scene", ("name", GesValue.GesText("stage"))));
        Assert.IsEmpty(scene.Programs);
        Assert.IsNull(scene.Notes);
    }

    [TestMethod]
    public void OtherValuesAreTypeMismatches()
    {
        void Mismatch(string path, params (string, GesValue)[] fields)
        {
            var error = Assert.ThrowsExactly<GesWireException>(() => Model.Program.FromValue(Program(fields)));
            Assert.AreEqual((GesWireException.TypeMismatch, path), (error.Code, error.Path));
        }

        Mismatch("data", ("data", GesValue.GesText("x")));
        Mismatch("data", ("data", GesValue.GesList([])));
        // A Record is no Map, though both hold fields.
        Mismatch("data", ("data", GesValue.GesRecord("Other", ("a", GesValue.GesInteger(1)))));
        Mismatch("args", ("args", GesValue.GesMap()));
        Mismatch("layers[]", ("layers", GesValue.GesList([GesValue.GesInteger(1)])));
        Mismatch("pick", ("pick", GesValue.GesInteger(1)));
    }

    [TestMethod]
    public void ExternalValuesAreNoFreeData()
    {
        var external = new Model.Scene("stage", null).ToGesValue();
        var error = Assert.ThrowsExactly<GesWireException>(() => Model.Program.FromValue(Program(("data", GesValue.GesMap(("held", GesValue.GesList([external])))))));
        Assert.AreEqual((GesWireException.TypeMismatch, "data"), (error.Code, error.Path));
        error = Assert.ThrowsExactly<GesWireException>(() => Model.Program.FromValue(Program(("args", GesValue.GesList([external])))));
        Assert.AreEqual((GesWireException.TypeMismatch, "args"), (error.Code, error.Path));
    }

    [TestMethod]
    public void DataTypesHoldNoExternalValues()
    {
        var external = new Model.Scene("stage", null).ToGesValue();
        var held = GesValue.GesMap(("held", GesValue.GesList([external])));
        Assert.ThrowsExactly<ArgumentException>(() => new GesDataMap(held.AsMap()!));
        Assert.ThrowsExactly<ArgumentException>(() => new GesDataList([GesValue.GesInteger(1), held]));
        Assert.IsNull(GesDataMap.FromValue(held));
        Assert.IsNull(GesDataList.FromValue(GesValue.GesList([external])));
        // A Record is no plain Map, though its fields may become one.
        var record = GesValue.GesRecord("Other", ("a", GesValue.GesInteger(1)));
        Assert.IsNull(GesDataMap.FromValue(record));
        Assert.AreEqual(Map(("a", GesValue.GesInteger(1))), new GesDataMap(record.AsMap()!));

        GesValue[] items = [GesValue.GesInteger(1)];
        var list = new GesDataList(items);
        items[0] = GesValue.GesInteger(2);
        Assert.AreEqual(GesValue.GesInteger(1), list[0]);
        Assert.AreEqual(GesDataList.Empty, new GesDataList([]));
        Assert.AreEqual(GesDataMap.Empty, Map());
        Assert.AreEqual(GesValue.GesMap(), GesDataMap.Empty.ToValue());
        Assert.AreEqual(GesValue.GesList([]), default(GesDataList).ToValue());
        Assert.AreEqual("[1]", list.ToString());
    }

    private const string ProgramDocument = """
        :Schema(:Program)
        script: "run"
        layers: [[z: 1]]
        pick: "first"
        data: [hp: 3, inner: [deep: true], items: [1, "two"], pos: :Point(1, 2)]
        args: [1, "two"]

        """;

    [TestMethod]
    public void ModelsReadTheirDocuments()
    {
        var program = Model.Program.FromDocument(ProgramDocument);
        Assert.AreEqual(Data(), program.Data);
        CollectionAssert.AreEqual(new[] { GesValue.GesInteger(1), GesValue.GesText("two") }, program.Args.ToArray());
        Assert.IsEmpty(program.Steps);
        var mismatch = Assert.ThrowsExactly<GesDocumentException>(() => Model.Program.FromDocument(ProgramDocument.Replace("data: [hp: 3, inner: [deep: true], items: [1, \"two\"], pos: :Point(1, 2)]", "data: [1, 2]")));
        Assert.AreEqual(("document.fieldType", 5, 7, "data"), (mismatch.Code, mismatch.Line, mismatch.Column, mismatch.Path));
    }

    [TestMethod]
    public void WireRootsStillRejectFreeData()
    {
        // Loading the set already rejects the field, before --models could name a model namespace.
        var source = new GesSchemaSource("wire.gess", "wire(w) schema :Program as {\n  script: :Text max 64\n  data: :Map default [:]\n}\n");
        var error = Assert.ThrowsExactly<GesSchemaException>(() => GesSchemaSet.Parse(source));
        Assert.AreEqual(("schema.notEncodable", 3, 9), (error.Code, error.Line, error.Column));
    }
}
