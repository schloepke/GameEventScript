// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Runtime.Values;

namespace GameEventScript.Tests.Native.Api;

// Markdown Conformance covers catalog reading (api/catalogs.md); these tests keep the argument checks, the lookups of
// the result and the path arithmetic that the corpus reaches only through its file table.
[TestClass]
public sealed class GesCatalogTests
{
    private const string Schemas = "schema :Hull as { armor: :Number integer between 0 and 100 }\n"
        + "handle @Hull of :Hull as { id: :Text max 32, version: :ContentHash }\n"
        + "schema :Composition as { name: :Text max 64, hull: @Hull~, escort: @Unit optional }\n"
        + "handle @Unit of :Composition as { id: :Text max 16, version: :ContentHash }\n"
        + "schema :Contact as { label: :Text max 16 }\n"
        + "handle @Target of :Contact as { id: :Number integer between 0 and 65535, version: :Nothing }\n"
        + "handle @Slot of :Contact composite @Unit as { part: :Number integer between 0 and 15 }\n";

    private static GesHandleType Type(string name)
    {
        foreach (var type in GesSchemaSet.Parse([new GesSchemaSource("units.gess", Schemas)]).HandleTypes)
            if (type.Name == name) return type;
        throw new InvalidOperationException(name);
    }

    private static GesCatalog Hulls() => GesCatalog.Read(Type("Hull"), [new GesCatalogRoot("content", "hulls.gesd")],
        new Files(("content", "hulls.gesd", ":Catalog(@Hull)\nlight: [armor: 20]\nheavy: [armor: 80]\n")));

    [TestMethod]
    public void ReadsEntriesWithPinnedDependenciesAndOrdersThemById()
    {
        var hulls = Hulls();
        var files = new Files(
            ("content", "units.gesd", ":Catalog(@Unit)\nimport 'drones/all.gesd'\nscout: [name: 'Scout', hull: @Hull:light~]\n"),
            ("content", "drones/all.gesd", ":Catalog(@Unit)\nmodule drone {\n  gnabblo: [name: 'Gnabblo', hull: @Hull:heavy~, escort: @scout]\n  brakk: import 'brakk.gesd'\n}\n"),
            ("content", "drones/brakk.gesd", ":Schema(:Composition)\nname: 'Brakk', hull: @Hull:light~, escort: @gnabblo\n"));

        var catalog = GesCatalog.Read(Type("Unit"), [new GesCatalogRoot("content", "units.gesd")], files, [hulls]);

        CollectionAssert.AreEqual(new[] { "drone.brakk", "drone.gnabblo", "scout" }, catalog.Entries.Select(entry => entry.Id).ToArray());
        CollectionAssert.AreEqual(new[] { "drone.brakk", "drone.gnabblo" }, catalog.Module("drone").Select(entry => entry.Id).ToArray());
        Assert.IsEmpty(catalog.Module("dron"));
        Assert.IsNull(catalog["drone"]);
        var brakk = catalog["drone.brakk"]!;
        Assert.AreEqual("drones/all.gesd", brakk.Origin.SourceName);
        Assert.AreEqual("content", brakk.Origin.ModuleName);
        Assert.AreEqual(4, brakk.Origin.Line);
        Assert.AreEqual(3, brakk.Origin.Column);
        Assert.IsTrue(brakk.Handle.HasVersion);
        var fields = brakk.Value.AsMap()!;
        Assert.AreEqual(hulls["light"]!.Handle, fields.Get("hull")!.Value.Handle);
        Assert.AreEqual("@Unit:drone.gnabblo", fields.Get("escort")!.Value.Handle!.ToString());
        Assert.AreNotEqual(catalog["drone.gnabblo"]!.Handle, catalog["scout"]!.Handle);
    }

    [TestMethod]
    public void ReportsEveryErrorWithASuggestion()
    {
        var files = new Files(("content", "units.gesd",
            ":Catalog(@Unit)\nscout: [name: 'Scout', hull: @Hull:light~, escort: @scuot]\nmodule drone {\n  a: [name: 'A', hull: @Hull:hevy~]\n}\n"));

        var exception = Assert.ThrowsExactly<GesCatalogException>(() => GesCatalog.Read(Type("Unit"), [new GesCatalogRoot("content", "units.gesd")], files, [Hulls()]));

        CollectionAssert.AreEqual(new[] { GesCatalogException.UnknownReference, GesCatalogException.UnknownReference },
            exception.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray());
        Assert.AreEqual("scout", exception.Diagnostics[0].Suggestion);
        Assert.AreEqual(2, exception.Diagnostics[0].Location.Line);
        Assert.AreEqual("heavy", exception.Diagnostics[1].Suggestion);
    }

    [TestMethod]
    public void RejectsTypesRootsAndDependenciesItCannotRead()
    {
        var files = new Files();
        Assert.ThrowsExactly<ArgumentException>(() => GesCatalog.Read(Type("Slot"), [], files));
        Assert.ThrowsExactly<ArgumentException>(() => GesCatalog.Read(Type("Target"), [], files));
        Assert.ThrowsExactly<ArgumentException>(() => GesCatalog.Read(Type("Unit"), [new GesCatalogRoot("a", "x"), new GesCatalogRoot("a", "y")], files));
        Assert.ThrowsExactly<ArgumentException>(() => GesCatalog.Read(Type("Unit"), [], files, [Hulls(), Hulls()]));
        Assert.ThrowsExactly<ArgumentNullException>(() => GesCatalog.Read(Type("Unit"), [null!], files));
        Assert.IsEmpty(GesCatalog.Read(Type("Unit"), [], files).Entries);
    }

    [TestMethod]
    public void IndexesEntriesThroughProjectionsOfItsStateOnly()
    {
        var set = GesSchemaSet.Parse([new GesSchemaSource("units.gess", Schemas
            + "projection :Named of :Composition as { name }\nprojection :Short of :Named as { name }\nprojection :Label of :Contact as { label }\n")]);
        var unit = set.HandleTypes.First(type => type.Name == "Unit");
        var catalog = GesCatalog.Read(unit, [new GesCatalogRoot("content", "units.gesd")],
            new Files(("content", "units.gesd", ":Catalog(@Unit)\nscout: [name: 'Scout', hull: @Hull:light~]\n")), [Hulls()]);

        var named = catalog.Index(set.Schema("Named"));
        Assert.AreEqual(("@Unit:scout", "Named"), (named[0].WithoutVersion().ToString(), named[0].Projection));
        Assert.IsTrue(named[0].HasVersion);
        Assert.AreNotEqual(catalog["scout"]!.Handle.Version, named[0].Version);
        // A projection of a projection narrows the same state.
        Assert.HasCount(1, catalog.Index(set.Schema("Short")));
        Assert.ThrowsExactly<ArgumentNullException>(() => catalog.Index(null!));
        Assert.ThrowsExactly<ArgumentException>(() => catalog.Index(set.Schema("Composition")));
        Assert.ThrowsExactly<ArgumentException>(() => catalog.Index(set.Schema("Label")));
    }

    [TestMethod]
    public void CombinesImportPathsWithinTheRoot()
    {
        Assert.AreEqual("a/c.gesd", GesCatalog.Combine("a/b.gesd", "c.gesd"));
        Assert.AreEqual("c.gesd", GesCatalog.Combine("a/b.gesd", "/c.gesd"));
        Assert.AreEqual("c.gesd", GesCatalog.Combine("a/b.gesd", "../c.gesd"));
        Assert.AreEqual("a/d/c.gesd", GesCatalog.Combine("a/b.gesd", "./d//c.gesd"));
        Assert.IsNull(GesCatalog.Combine("a/b.gesd", "../../c.gesd"));
        Assert.IsNull(GesCatalog.Combine("", ".."));
    }

    private const string FileSchemas = "schema :Composition as { name: :Text max 16 }\n"
        + "handle @Unit of :Composition as { id: :Text max 16, version: :ContentHash }\n"
        + "schema :Source as { text: :Text }\n"
        + "handle @Repository of :Source as { id: :Text max 16, version: :Number integer between 0 and 1000 }\n"
        + "handle @UnitFiles of :Catalog(@Unit) composite @Repository as { part: :Text max 64 }\n"
        + "handle @Other of :Source composite @Repository as { part: :Text max 64 }\n";

    private static GesHandleType FileType(string name)
    {
        foreach (var type in GesSchemaSet.Parse([new GesSchemaSource("files.gess", FileSchemas)]).HandleTypes)
            if (type.Name == name) return type;
        throw new InvalidOperationException(name);
    }

    private static readonly GesHandle Content = GesHandle.Parse("@UnitFiles:content/units.gesd");

    private static GesCatalog ReadUnits(Action<GesHandleResolveCall> answer) => GesCatalog.Read(FileType("Unit"), [Content], new Resolver(answer));

    [TestMethod]
    public void ReadsThroughHandlesAndAnswersTheStatesOfItsEntries()
    {
        var requested = new List<string>();
        var catalog = ReadUnits(call =>
        {
            requested.Add(call.Handle.ToString());
            if (call.Handle.Part.AsText() == "units.gesd") call.SetDocument(":Catalog(@Unit)\nimport 'more.gesd'\nscout: [name: 'Scout']\n", GesValue.GesInteger(9));
            else call.SetDocument(":Catalog(@Unit)\none: [name: 'One']\n");
        });

        CollectionAssert.AreEqual(new[] { "@UnitFiles:content/units.gesd", "@UnitFiles:content~9/more.gesd" }, requested);
        CollectionAssert.AreEqual(new[] { "@UnitFiles:content~9/units.gesd" }, catalog.Sources.Select(source => source.ToString()).ToArray());
        Assert.AreEqual("@UnitFiles:content/units.gesd", catalog["one"]!.Origin.ModuleName);

        var call = new GesHandleResolveCall();
        var scout = catalog["scout"]!;
        call.Begin(GesHandle.Create("Unit", GesValue.GesText("scout")), FileType("Unit"), null, 0);
        catalog.Resolve(call);
        Assert.AreEqual(GesHandleResolveCall.AnswerKind.State, call.Answer);
        Assert.AreEqual(scout.Value, call.Record);
        Assert.IsTrue(GesValue.Equals(scout.Handle.Version, call.Version));
        call.Begin(GesHandle.Create("Unit", GesValue.GesText("ghost")), FileType("Unit"), null, 0);
        catalog.Resolve(call);
        Assert.AreEqual(GesHandleResolveCall.AnswerKind.Missing, call.Answer);
        call.End();
    }

    [TestMethod]
    public void ReportsAStateAnswerAsTheWrongKindOfFile()
    {
        var exception = Assert.ThrowsExactly<GesCatalogException>(() => ReadUnits(call => call.SetState(GesValue.GesNothing())));
        Assert.AreEqual(GesCatalogException.ImportKind, exception.Diagnostics[0].Code);
    }

    [TestMethod]
    public void FailsOnMissingSeveralOrInvalidAnswersAndPassesResolverErrorsOn()
    {
        Assert.AreEqual(GesDiagnosticCodes.RuntimeHandleResolveFailed, Assert.ThrowsExactly<GesHandleException>(() => ReadUnits(_ => { })).Code);
        Assert.AreEqual(GesDiagnosticCodes.RuntimeHandleResolveFailed, Assert.ThrowsExactly<GesHandleException>(() => ReadUnits(call =>
        {
            call.SetMissing();
            call.SetMissing();
        })).Code);
        Assert.AreEqual(GesDiagnosticCodes.RuntimeHandleResolveFailed, Assert.ThrowsExactly<GesHandleException>(
            () => ReadUnits(call => call.SetDocument(":Catalog(@Unit)\n", GesValue.GesBytes(new byte[] { 1 })))).Code);
        var thrown = new InvalidOperationException("offline");
        Assert.AreSame(thrown, Assert.ThrowsExactly<InvalidOperationException>(() => ReadUnits(_ => throw thrown)));
    }

    [TestMethod]
    public void RejectsRootsThatAreNoCatalogFiles()
    {
        var resolver = new Resolver(call => call.SetMissing());
        Assert.ThrowsExactly<ArgumentException>(() => GesCatalog.Read(FileType("Unit"), [GesHandle.Parse("@Repository:content")], resolver));
        Assert.ThrowsExactly<ArgumentException>(() => GesCatalog.Read(FileType("Unit"), [GesHandle.Parse("@Other:content/units.gesd")], resolver));
        Assert.ThrowsExactly<ArgumentException>(() => GesCatalog.Read(FileType("Unit"), [Content, GesHandle.Parse("@UnitFiles:content/units.gesd")], resolver));
        Assert.ThrowsExactly<ArgumentNullException>(() => GesCatalog.Read(FileType("Unit"), [Content], (IGesHandleResolver)null!));
        Assert.ThrowsExactly<ArgumentException>(() => GesCatalog.Read(FileType("Other"), [], resolver));
        Assert.IsEmpty(GesCatalog.Read(FileType("Unit"), [], resolver).Sources);
    }

    [TestMethod]
    public void LeavesCatalogFileTypesOutOfScripts()
    {
        var types = GesSchemaSet.Parse([new GesSchemaSource("files.gess", FileSchemas)]).HandleTypes;
        _ = GameEventScriptProgram.CreateBuilder().WithHandleTypes(types).AddScript("on Seen(unit as @Unit) { }").Compile();
        Assert.ThrowsExactly<GesCompileException>(() => GameEventScriptProgram.CreateBuilder().WithHandleTypes(types).AddScript("on Seen(file as @UnitFiles) { }").Compile());
    }

    private sealed class Resolver(Action<GesHandleResolveCall> answer) : IGesHandleResolver
    {
        public void Resolve(GesHandleResolveCall call) => answer(call);
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
