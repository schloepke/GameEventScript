// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Runtime.Values;

namespace GameEventScript.Tests.Native.Api;

// Markdown Conformance covers store reading (api/stores.md); these tests keep the argument checks, the lookups of
// the result and the path arithmetic that the corpus reaches only through its file table.
[TestClass]
public sealed class GesStoreTests
{
    private const string Schemas = "schema :Hull as { armor: :Number integer between 0 and 100 }\n"
        + "handle @Hull of :Hull with id :Text max 32 versioned by hash(#ges)\n"
        + "schema :Composition as { name: :Text max 64, hull: @Hull~, escort: @Unit optional }\n"
        + "handle @Unit of :Composition with id :Text max 16 versioned by hash(#ges)\n"
        + "schema :Contact as { label: :Text max 16 }\n"
        + "handle @Target of :Contact with id :Number integer between 0 and 65535\n"
        + "handle @Slot of :Contact attached to @Unit with part :Number integer between 0 and 15\n";

    private static GesHandleType Type(string name)
    {
        foreach (var type in GesSchemaSet.Parse([new GesSchemaSource("units.gess", Schemas)]).HandleTypes)
            if (type.Name == name) return type;
        throw new InvalidOperationException(name);
    }

    private static GesStore Hulls() => GesStore.Read(Type("Hull"), [new GesStoreRoot("content", "hulls.gesd")],
        new Files(("content", "hulls.gesd", ":Store(@Hull)\nlight: [armor: 20]\nheavy: [armor: 80]\n")));

    [TestMethod]
    public void ReadsEntriesWithPinnedDependenciesAndOrdersThemById()
    {
        var hulls = Hulls();
        var files = new Files(
            ("content", "units.gesd", ":Store(@Unit)\nimport 'drones/all.gesd'\nscout: [name: 'Scout', hull: @Hull:light~]\n"),
            ("content", "drones/all.gesd", ":Store(@Unit)\nmodule drone {\n  gnabblo: [name: 'Gnabblo', hull: @Hull:heavy~, escort: @scout]\n  brakk: import 'brakk.gesd'\n}\n"),
            ("content", "drones/brakk.gesd", ":Schema(:Composition)\nname: 'Brakk', hull: @Hull:light~, escort: @gnabblo\n"));

        var store = GesStore.Read(Type("Unit"), [new GesStoreRoot("content", "units.gesd")], files, [hulls]);

        CollectionAssert.AreEqual(new[] { "drone.brakk", "drone.gnabblo", "scout" }, store.Entries.Select(entry => entry.Id).ToArray());
        CollectionAssert.AreEqual(new[] { "drone.brakk", "drone.gnabblo" }, store.Module("drone").Select(entry => entry.Id).ToArray());
        Assert.IsEmpty(store.Module("dron"));
        Assert.IsNull(store["drone"]);
        var brakk = store["drone.brakk"]!;
        Assert.AreEqual("drones/all.gesd", brakk.Origin.SourceName);
        Assert.AreEqual("content", brakk.Origin.ModuleName);
        Assert.AreEqual(4, brakk.Origin.Line);
        Assert.AreEqual(3, brakk.Origin.Column);
        Assert.IsTrue(brakk.Handle.HasVersion);
        var fields = brakk.Value.AsMap()!;
        Assert.AreEqual(hulls["light"]!.Handle, fields.Get("hull")!.Value.Handle);
        Assert.AreEqual("@Unit:drone.gnabblo", fields.Get("escort")!.Value.Handle!.ToString());
        Assert.AreNotEqual(store["drone.gnabblo"]!.Handle, store["scout"]!.Handle);
    }

    [TestMethod]
    public void ReportsEveryErrorWithASuggestion()
    {
        var files = new Files(("content", "units.gesd",
            ":Store(@Unit)\nscout: [name: 'Scout', hull: @Hull:light~, escort: @scuot]\nmodule drone {\n  a: [name: 'A', hull: @Hull:hevy~]\n}\n"));

        var exception = Assert.ThrowsExactly<GesStoreException>(() => GesStore.Read(Type("Unit"), [new GesStoreRoot("content", "units.gesd")], files, [Hulls()]));

        CollectionAssert.AreEqual(new[] { GesStoreException.UnknownReference, GesStoreException.UnknownReference },
            exception.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray());
        Assert.AreEqual("scout", exception.Diagnostics[0].Suggestion);
        Assert.AreEqual(2, exception.Diagnostics[0].Location.Line);
        Assert.AreEqual("heavy", exception.Diagnostics[1].Suggestion);
    }

    [TestMethod]
    public void RejectsTypesRootsAndDependenciesItCannotRead()
    {
        var files = new Files();
        Assert.ThrowsExactly<ArgumentException>(() => GesStore.Read(Type("Slot"), [], files));
        Assert.ThrowsExactly<ArgumentException>(() => GesStore.Read(Type("Target"), [], files));
        Assert.ThrowsExactly<ArgumentException>(() => GesStore.Read(Type("Unit"), [new GesStoreRoot("a", "x"), new GesStoreRoot("a", "y")], files));
        Assert.ThrowsExactly<ArgumentException>(() => GesStore.Read(Type("Unit"), [], files, [Hulls(), Hulls()]));
        Assert.ThrowsExactly<ArgumentNullException>(() => GesStore.Read(Type("Unit"), [null!], files));
        Assert.IsEmpty(GesStore.Read(Type("Unit"), [], files).Entries);
    }

    [TestMethod]
    public void IndexesEntriesThroughProjectionsOfItsStateOnly()
    {
        var set = GesSchemaSet.Parse([new GesSchemaSource("units.gess", Schemas
            + "projection :Named of :Composition as { name }\nprojection :Short of :Named as { name }\nprojection :Label of :Contact as { label }\n")]);
        var unit = set.HandleTypes.First(type => type.Name == "Unit");
        var store = GesStore.Read(unit, [new GesStoreRoot("content", "units.gesd")],
            new Files(("content", "units.gesd", ":Store(@Unit)\nscout: [name: 'Scout', hull: @Hull:light~]\n")), [Hulls()]);

        var named = store.Index(set.Schema("Named"));
        Assert.AreEqual(("@Unit:scout", "Named"), (named[0].WithoutVersion().ToString(), named[0].Projection));
        Assert.IsTrue(named[0].HasVersion);
        Assert.AreNotEqual(store["scout"]!.Handle.Version, named[0].Version);
        // A projection of a projection narrows the same state.
        Assert.HasCount(1, store.Index(set.Schema("Short")));
        Assert.ThrowsExactly<ArgumentNullException>(() => store.Index(null!));
        Assert.ThrowsExactly<ArgumentException>(() => store.Index(set.Schema("Composition")));
        Assert.ThrowsExactly<ArgumentException>(() => store.Index(set.Schema("Label")));
    }

    [TestMethod]
    public void CombinesImportPathsWithinTheRoot()
    {
        Assert.AreEqual("a/c.gesd", GesStore.Combine("a/b.gesd", "c.gesd"));
        Assert.AreEqual("c.gesd", GesStore.Combine("a/b.gesd", "/c.gesd"));
        Assert.AreEqual("c.gesd", GesStore.Combine("a/b.gesd", "../c.gesd"));
        Assert.AreEqual("a/d/c.gesd", GesStore.Combine("a/b.gesd", "./d//c.gesd"));
        Assert.IsNull(GesStore.Combine("a/b.gesd", "../../c.gesd"));
        Assert.IsNull(GesStore.Combine("", ".."));
    }

    private const string SourceSchemas = "schema :Composition as { name: :Text max 16 }\n"
        + "handle @Unit of :Composition with id :Text max 16 versioned by hash(#ges)\n"
        + "handle @Repository with id :Text max 16 versioned by revision max 1000\n"
        + "handle @File attached to @Repository with part :Text max 64\n"
        + "handle @Stateless with id :Text max 16\n";

    private static GesHandleType SourceType(string name)
    {
        foreach (var type in GesSchemaSet.Parse([new GesSchemaSource("sources.gess", SourceSchemas)]).HandleTypes)
            if (type.Name == name) return type;
        throw new InvalidOperationException(name);
    }

    private static readonly GesHandle Content = GesHandle.Parse("@Repository:content");

    private static GesStore ReadUnits(Func<GesHandle, string, GesStoreFile> answer)
        => GesStore.Read(SourceType("Unit"), [new GesStoreSourceRoot(Content, "units.gesd")], new Source(answer));

    [TestMethod]
    public void ReadsFromRootHandlesAndPinsTheVersionOfTheFirstAnswer()
    {
        var requested = new List<string>();
        var store = ReadUnits((root, path) =>
        {
            requested.Add(root + " " + path);
            return path == "units.gesd"
                ? new GesStoreFile(":Store(@Unit)\nimport 'more.gesd'\nscout: [name: 'Scout']\n", GesValue.GesInteger(9))
                : new GesStoreFile(":Store(@Unit)\none: [name: 'One']\n");
        });

        CollectionAssert.AreEqual(new[] { "@Repository:content units.gesd", "@Repository:content~9 more.gesd" }, requested);
        CollectionAssert.AreEqual(new[] { "@Repository:content~9" }, store.Sources.Select(source => source.ToString()).ToArray());
        Assert.AreEqual("@Repository:content", store["one"]!.Origin.ModuleName);

        var call = new GesHandleResolveCall();
        var scout = store["scout"]!;
        call.Begin(GesHandle.Create("Unit", GesValue.GesText("scout")), SourceType("Unit"), null, 0);
        store.Resolve(call);
        Assert.AreEqual(GesHandleResolveCall.AnswerKind.State, call.Answer);
        Assert.AreEqual(scout.Value, call.Record);
        Assert.IsTrue(GesValue.Equals(scout.Handle.Version, call.Version));
        call.Begin(GesHandle.Create("Unit", GesValue.GesText("ghost")), SourceType("Unit"), null, 0);
        store.Resolve(call);
        Assert.AreEqual(GesHandleResolveCall.AnswerKind.Missing, call.Answer);
        call.End();
    }

    [TestMethod]
    public void FailsOnInvalidOrChangingVersionsAndPassesSourceErrorsOn()
    {
        Assert.AreEqual(GesStoreException.ImportMissing, Assert.ThrowsExactly<GesStoreException>(() => ReadUnits((_, _) => GesStoreFile.Missing)).Diagnostics[0].Code);
        Assert.AreEqual(GesDiagnosticCodes.RuntimeHandleResolveFailed, Assert.ThrowsExactly<GesHandleException>(
            () => ReadUnits((_, _) => new GesStoreFile(":Store(@Unit)\n", GesValue.GesBytes(new byte[] { 1 })))).Code);
        var version = 1L;
        Assert.AreEqual(GesDiagnosticCodes.RuntimeHandleVersionMismatch, Assert.ThrowsExactly<GesHandleException>(
            () => ReadUnits((_, path) => new GesStoreFile(path == "units.gesd" ? ":Store(@Unit)\nimport 'more.gesd'\n" : ":Store(@Unit)\n", GesValue.GesInteger(version++)))).Code);
        var thrown = new InvalidOperationException("offline");
        Assert.AreSame(thrown, Assert.ThrowsExactly<InvalidOperationException>(() => ReadUnits((_, _) => throw thrown)));
    }

    [TestMethod]
    public void RejectsRootHandlesWithAPartTwiceGivenRootsAndTypesWithoutAStore()
    {
        var source = new Source((_, _) => GesStoreFile.Missing);
        var unit = SourceType("Unit");
        Assert.ThrowsExactly<ArgumentException>(() => GesStore.Read(unit, [new GesStoreSourceRoot(GesHandle.Parse("@File:content/units.gesd"), "units.gesd")], source));
        Assert.ThrowsExactly<ArgumentException>(() => GesStore.Read(unit, [new GesStoreSourceRoot(Content, "a.gesd"), new GesStoreSourceRoot(Content, "b.gesd")], source));
        Assert.ThrowsExactly<ArgumentNullException>(() => GesStore.Read(unit, [new GesStoreSourceRoot(Content, "units.gesd")], (IGesStoreSource)null!));
        Assert.ThrowsExactly<ArgumentException>(() => GesStore.Read(SourceType("Stateless"), [], source));
        Assert.ThrowsExactly<ArgumentException>(() => GesStore.Read(SourceType("File"), [], source));
        Assert.IsEmpty(GesStore.Read(unit, Array.Empty<GesStoreSourceRoot>(), source).Sources);
    }

    private sealed class Source(Func<GesHandle, string, GesStoreFile> answer) : IGesStoreSource
    {
        public GesStoreFile Read(GesHandle root, string path) => answer(root, path);
    }

    private sealed class Files(params (string Root, string Path, string Text)[] files) : IGesStoreFiles
    {
        public string? Read(string root, string path)
        {
            foreach (var file in files)
                if (file.Root == root && file.Path == path) return file.Text;
            return null;
        }
    }
}
