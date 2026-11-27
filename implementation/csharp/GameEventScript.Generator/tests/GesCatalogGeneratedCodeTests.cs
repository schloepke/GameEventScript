// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Generator;
using Model = GameEventScript.Tests.Generator.Catalog.Model;

namespace GameEventScript.Tests.Generator;

// The checked-in code of conformance/fixtures/SchemaGeneration/catalogs.gess: typed catalogs that read their entries into
// models, look them up by id and module, take the catalogs they depend on and resolve their handles like GesCatalog.
[TestClass]
public sealed class GesCatalogGeneratedCodeTests
{
    private static GesSchemaSet Set()
    {
        var fixture = Path.Combine(TestRepositoryPaths.ConformanceDirectory, "fixtures", "SchemaGeneration", "catalogs.gess");
        return GesSchemaSet.Parse(new GesSchemaSource("catalogs.gess", File.ReadAllText(fixture)));
    }

    private static GesSchemaGenerateOptions Options(GesSchemaLanguage language, string space) => new() { Language = language, Namespace = space, Access = GesSchemaAccess.Internal, Catalogs = true };

    [TestMethod]
    public void CheckedInCodeIsCurrent()
    {
        var set = Set();
        var csharp = Path.Combine(TestRepositoryPaths.CSharpDirectory, "GameEventScript.Generator", "tests", "Generated");
        var swift = Path.Combine(TestRepositoryPaths.Root, "implementation", "swift", "GameEventScriptConformance", "Tests", "GameEventScriptConformanceTests", "Generated");
        var model = GesSchemaGenerator.ModelRoots(set, "catalog");
        Assert.AreEqual(File.ReadAllText(Path.Combine(csharp, "CatalogModel.g.cs")), GesSchemaGenerator.GenerateModels(set, model, Options(GesSchemaLanguage.CSharp, "GameEventScript.Tests.Generator.Catalog.Model")));
        Assert.AreEqual(File.ReadAllText(Path.Combine(swift, "CatalogModel.swift")), GesSchemaGenerator.GenerateModels(set, model, Options(GesSchemaLanguage.Swift, "CatalogModel")));
    }

    private static Model.HullCatalog Hulls() => Model.HullCatalog.Read([new GesCatalogRoot("content", "hulls.gesd")],
        new Files(("content", "hulls.gesd", ":Catalog(@Hull)\nlight: [name: 'Light', armor: 20]\nheavy: [name: 'Heavy', armor: 80]\n")));

    private static readonly Files UnitFiles = new(
        ("content", "units.gesd", ":Catalog(@Unit)\nscout: [name: 'Scout', hull: @Hull:light~, escorts: [], speed: 120m/s]\nmodule drone {\n"
            + "  gnabblo: [name: 'Gnabblo', hull: @Hull:heavy~, escorts: [@scout], speed: 40m/s]\n"
            + "  brakk: [name: 'Brakk', hull: @Hull:light~, escorts: [@scout, @gnabblo], speed: 60m/s]\n}\n"));

    [TestMethod]
    public void ReadsEntriesIntoModelsAndLooksThemUp()
    {
        var hulls = Hulls();
        var units = Model.UnitCatalog.Read([new GesCatalogRoot("content", "units.gesd")], UnitFiles, hull: hulls);

        CollectionAssert.AreEqual(new[] { "drone.brakk", "drone.gnabblo", "scout" }, units.Entries.Select(entry => entry.Id).ToArray());
        CollectionAssert.AreEqual(units.Entries.Select(entry => entry.Handle).ToArray(), units.Index.ToArray());
        CollectionAssert.AreEqual(new[] { "drone.brakk", "drone.gnabblo" }, units.Module("drone").Select(entry => entry.Id).ToArray());
        Assert.IsEmpty(units.Module("dron"));
        Assert.IsNull(units["drone"]);
        Assert.IsEmpty(units.Sources);
        var brakk = units["drone.brakk"]!;
        Assert.AreEqual(new Model.UnitHandle("drone.brakk"), brakk.Handle);
        Assert.AreEqual("Brakk", brakk.Value.Name);
        Assert.AreEqual(60, brakk.Value.Speed);
        Assert.AreEqual(hulls["light"]!.Handle, brakk.Value.Hull.WithoutVersion());
        Assert.IsNotNull(brakk.Value.Hull.Version);
        CollectionAssert.AreEqual(new[] { new Model.UnitHandle("scout"), new Model.UnitHandle("drone.gnabblo") }, brakk.Value.Escorts);
        Assert.AreEqual(5, brakk.Origin.Line);
        Assert.AreEqual(80, hulls["heavy"]!.Value.Armor);
        Assert.AreSame(units.Catalog.Type, units.Catalog.Type);
        Assert.AreEqual("Unit", units.Catalog.Type.Name);
    }

    [TestMethod]
    public void NarrowsEntriesToProjectionsAndIndexesTheirVersions()
    {
        var files = new Files(("content", "units.gesd", ":Catalog(@Unit)\nscout: [name: 'Scout', hull: @Hull:light~, escorts: [], speed: 120m/s, look: [icon: 'eye']]\n"
            + "module drone {\n  brakk: [name: 'Brakk', hull: @Hull:light~, escorts: [@scout], speed: 60m/s]\n}\n"));
        var units = Model.UnitCatalog.Read([new GesCatalogRoot("content", "units.gesd")], files, hull: Hulls());

        var scout = units["scout"]!.Value;
        var display = Model.UnitDisplay.From(scout);
        Assert.AreEqual(new Model.UnitDisplay("Scout", scout.Hull, "eye"), display);
        scout.Look!.Icon = "ear";
        Assert.AreEqual("eye", display.Icon);
        // An absent optional Record on the path leaves the taken field absent.
        Assert.IsNull(Model.UnitDisplay.From(units["drone.brakk"]!.Value).Icon);
        Assert.AreEqual("Scout", Model.UnitLabel.From(display).Name);
        Assert.AreEqual(Model.UnitLabel.From(display), Model.UnitLabel.From(scout));
        Assert.ThrowsExactly<ArgumentNullException>(() => Model.UnitDisplay.From(null!));

        CollectionAssert.AreEqual(units.Index.ToArray(), units.UnitDisplayIndex.Select(handle => handle.WithoutVersion()).ToArray());
        Assert.IsTrue(units.UnitDisplayIndex.All(handle => handle.Version is not null));
        // Each projection has its own content hash.
        Assert.AreNotEqual(units.UnitDisplayIndex[1].Version, units.UnitLabelIndex[1].Version);
        CollectionAssert.AreEqual(new[] { units.UnitLabelIndex[0] }, units.UnitLabelModule("drone").ToArray());
        Assert.IsEmpty(units.UnitDisplayModule("dron"));
        Assert.ThrowsExactly<ArgumentNullException>(() => units.UnitDisplayModule(null!));
    }

    [TestMethod]
    public void RejectsPinsWithoutTheCatalogItDependsOn()
    {
        var exception = Assert.ThrowsExactly<GesCatalogException>(() => Model.UnitCatalog.Read([new GesCatalogRoot("content", "units.gesd")], UnitFiles));
        Assert.IsTrue(exception.Diagnostics.All(diagnostic => diagnostic.Code == GesCatalogException.InvalidPin));
    }

    private static string Code(string schemas, GesSchemaLanguage language)
    {
        var set = GesSchemaSet.Parse(new GesSchemaSource("names.gess", schemas));
        return Assert.ThrowsExactly<GesGeneratorException>(() => GesSchemaGenerator.GenerateModels(set, GesSchemaGenerator.ModelRoots(set, "catalog"), Options(language, "Names"))).Code;
    }

    [TestMethod]
    [DataRow("model(catalog) schema :Roots as { name: :Text max 8 }\nhandle @Roots of :Roots as { id: :Text max 8, version: :ContentHash }\n"
        + "model(catalog) schema :Unit as { roots: @Roots }\nhandle @Unit of :Unit as { id: :Text max 8, version: :ContentHash }\n")]
    [DataRow("model(catalog) schema :Unit as { name: :Text max 8 }\nhandle @Unit of :Unit as { id: :Text max 8, version: :ContentHash }\n"
        + "model(catalog) schema :UnitCatalog as { name: :Text max 8 }\n")]
    [DataRow("model(catalog) schema :Entry as { name: :Text max 8 }\nhandle @Entry of :Entry as { id: :Text max 8, version: :ContentHash }\n")]
    public void RejectsCatalogsWhoseNamesCollide(string schemas)
    {
        Assert.AreEqual("generate.nameCollision", Code(schemas, GesSchemaLanguage.CSharp));
        Assert.AreEqual("generate.nameCollision", Code(schemas, GesSchemaLanguage.Swift));
    }

    [TestMethod]
    public void LeavesHandleTypesWithoutAModelCatalogOut()
    {
        // A Number id, a composite and a state that is no model root generate no catalog.
        var set = GesSchemaSet.Parse(new GesSchemaSource("names.gess",
            "model(catalog) schema :Contact as { label: :Text max 8 }\nhandle @Target of :Contact as { id: :Number integer between 0 and 9, version: :Nothing }\n"
            + "handle @Slot of :Contact composite @Target as { part: :Text max 8 }\nschema :Other as { label: :Text max 8 }\n"
            + "handle @Other of :Other as { id: :Text max 8, version: :Nothing }\n"));
        var code = GesSchemaGenerator.GenerateModels(set, GesSchemaGenerator.ModelRoots(set, "catalog"), Options(GesSchemaLanguage.CSharp, "Names"));
        Assert.DoesNotContain("Catalog :", code);
    }

    [TestMethod]
    public void SplitOutputPlacesACatalogInTheFileOfItsHandleType()
    {
        // The catalog type has no declaration of its own; it goes with the handle type, as <Name>Handle does.
        var set = GesSchemaSet.Parse(
            new GesSchemaSource("states.gess", "model(data) schema :Unit as { name: :Text max 64 }\n"),
            new GesSchemaSource("handles.gess", "handle @Unit of :Unit as { id: :Text max 64, version: :ContentHash }\n"));
        var single = GesSchemaSet.Parse(new GesSchemaSource("units.gess",
            "handle @Unit of :Unit as { id: :Text max 64, version: :ContentHash }\nmodel(data) schema :Unit as { name: :Text max 64 }\n"));
        foreach (var language in new[] { GesSchemaLanguage.CSharp, GesSchemaLanguage.Swift })
        {
            var extension = language == GesSchemaLanguage.Swift ? ".swift" : ".g.cs";
            var options = new GesSchemaGenerateOptions { Language = language, Namespace = "Game.Data", Split = GesSchemaSplit.File, Catalogs = true };
            var files = GesSchemaGenerator.GenerateModelFiles(set, GesSchemaGenerator.ModelRoots(set, "data"), options);
            var handles = files.Single(file => file.Path == "Game.Data.handles" + extension);
            StringAssert.Contains(handles.Text, "UnitCatalog");
            StringAssert.Contains(handles.Text, " UnitHandle");
            Assert.IsFalse(files.Single(file => file.Path == "Game.Data.states" + extension).Text.Contains("UnitCatalog", StringComparison.Ordinal));
            var one = GesSchemaGenerator.GenerateModelFiles(single, GesSchemaGenerator.ModelRoots(single, "data"), options);
            StringAssert.Contains(one.Single(file => file.Path == "Game.Data.units" + extension).Text, "UnitCatalog");
            options.Split = GesSchemaSplit.Schema;
            Assert.IsTrue(GesSchemaGenerator.GenerateModelFiles(set, GesSchemaGenerator.ModelRoots(set, "data"), options).Any(file => file.Path == "Game.Data.UnitCatalog" + extension));
        }
    }

    [TestMethod]
    public void RejectsCatalogsInWireCode()
    {
        var set = Set();
        Assert.AreEqual("generate.usage", Assert.ThrowsExactly<GesGeneratorException>(
            () => GesSchemaGenerator.Generate(set, GesSchemaGenerator.ModelRoots(set, "catalog"), new GesSchemaParameters(), Options(GesSchemaLanguage.CSharp, "Names"))).Code);
    }

    private sealed class Files(params (string Root, string Path, string Text)[] files) : IGesCatalogFiles
    {
        public string? Read(string root, string path)
        {
            foreach (var file in files)
                if (file.Root == root && file.Path == path) return file.Text;
            return null;
        }
    }
}
