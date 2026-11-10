// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Generator;
using GameEventScript.Tests.Generator.Generated;

namespace GameEventScript.Tests.Generator;

// The checked-in code generated from conformance/fixtures/SchemaGeneration/fleet.gess, which makes changes of the generated
// text visible in review and serves the allocation gate of generated records.
[TestClass]
public sealed class GesGeneratedCodeTests
{
#if DEBUG
    private static readonly bool DebugBuild = true;
#else
    private static readonly bool DebugBuild = false;
#endif

    private static string Generated(GesSchemaLanguage language, string space)
    {
        var fixture = Path.Combine(TestRepositoryPaths.ConformanceDirectory, "fixtures", "SchemaGeneration", "fleet.gess");
        var set = GesSchemaSet.Parse(new GesSchemaSource("fleet.gess", File.ReadAllText(fixture)));
        var options = new GesSchemaGenerateOptions { Language = language, Namespace = space, Access = GesSchemaAccess.Internal };
        return GesSchemaGenerator.Generate(set, ["Fleet"], new GesSchemaParameters().With("worldSize", "2km"), options);
    }

    [TestMethod]
    public void CheckedInCodeIsCurrent()
    {
        var csharp = Path.Combine(TestRepositoryPaths.CSharpDirectory, "GameEventScript.Generator", "tests", "Generated", "Fleet.g.cs");
        var swift = Path.Combine(TestRepositoryPaths.Root, "implementation", "swift", "GameEventScriptConformance", "Tests", "GameEventScriptConformanceTests", "Generated", "Fleet.swift");
        Assert.AreEqual(File.ReadAllText(csharp), Generated(GesSchemaLanguage.CSharp, "GameEventScript.Tests.Generator.Generated"), "Regenerate Fleet.g.cs with ges schema generate.");
        Assert.AreEqual(File.ReadAllText(swift), Generated(GesSchemaLanguage.Swift, "Generated"), "Regenerate Fleet.swift with ges schema generate.");
    }

    [TestMethod]
    public void BaselinesSharingStorageAreRejected()
    {
        var source = new Fleet { Tick = 3 };
        for (var index = 0; index < 3; index++) source.Ships.Add(new Ship { Id = index, Health = 10 });
        var fleet = new Fleet();
        fleet.Read(source.ToBytes());
        var delta = new byte[Fleet.MaxByteCount];

        // The read would overwrite what the baseline holds: the record itself, its pooled ships or its list.
        void Rejected<T>(Func<T> read)
        {
            var error = Assert.ThrowsExactly<GesWireException>(() => read());
            Assert.AreEqual(GesWireException.OverlappingBuffers, error.Code);
            Assert.AreEqual("", error.Path);
        }

        Rejected(() => fleet.ReadDelta(delta, fleet));
        // A keyed record is rejected before its key is read.
        Rejected(() => fleet.Ships[0].ReadDelta(ReadOnlySpan<byte>.Empty, fleet.Ships[0]));
#if DEBUG
        // Only debug builds walk the storage for shared records and lists.
        Rejected(() => fleet.ReadDelta(delta, new Fleet { Ships = fleet.Ships }));
        Rejected(() => fleet.ReadDelta(delta, new Fleet { Ships = new List<Ship>(fleet.Ships) }));
        Rejected(() => fleet.ReadDelta(delta, new Fleet { Ships = { new Ship { Id = 7 }, fleet.Ships[2] } }));
        var own = new Fleet { Ships = { new Ship { Id = 7 } } };
        Rejected(() => own.ReadDelta(delta, new Fleet { Ships = own.Ships }));
        // A list inside a pooled ship and a nested class record are storage of the target too.
        var cargo = new Fleet();
        cargo.CopyFrom(fleet);
        cargo.Ships[1].Cargo = fleet.Ships[1].Cargo;
        Rejected(() => fleet.ReadDelta(delta, cargo));
        var flag = new Fleet();
        flag.CopyFrom(fleet);
        flag.Flag = fleet.Flag;
        Rejected(() => fleet.ReadDelta(delta, flag));
#else
        // Release builds skip the walk, but an unchanged list that the baseline shares keeps its values.
        var shared = new Fleet { Tick = fleet.Tick, Ships = fleet.Ships };
        var next = new Fleet();
        next.CopyFrom(fleet);
        next.Tick = 4;
        fleet.ReadDelta(next.ToDeltaBytes(shared), shared);
        Assert.IsTrue(fleet.WireEquals(next));
#endif

        // Copying a record into itself, or from a record that holds the same list, keeps its values.
        fleet.CopyFrom(fleet);
        fleet.CopyFrom(new Fleet { Tick = 3, Ships = fleet.Ships });
        Assert.IsTrue(fleet.WireEquals(source));
        fleet.Ships[0].CopyFrom(fleet.Ships[0]);
        Assert.IsTrue(fleet.WireEquals(source));

        // Another pooled ship of the same fleet is a valid baseline, since the read writes only into the target ship.
        var changed = new Ship { Id = 1, Health = 11 };
        var shipDelta = changed.ToDeltaBytes(fleet.Ships[1]);
        var spare = new Fleet();
        spare.CopyFrom(fleet);
        spare.Ships[0].ReadDelta(shipDelta, spare.Ships[1]);
        Assert.IsTrue(spare.Ships[0].WireEquals(changed));

        var baseline = new Fleet();
        baseline.CopyFrom(fleet);
        source.Ships[1].Health = 11;
        var length = source.WriteDelta(baseline, delta);
        fleet.ReadDelta(delta.AsSpan(0, length), baseline);
        Assert.IsTrue(fleet.WireEquals(source));
    }

    [TestMethod]
    [TestCategory("Allocation")]
    public void GeneratedRecordsAllocateNothing()
    {
        if (DebugBuild) Assert.Inconclusive("Debug builds check shared baselines, which allocates; the gate holds for release builds.");
        var name = "frigate"u8.ToArray();
        var fleet = new Fleet { Tick = 1 };
        for (var index = 0; index < 48; index++)
        {
            var ship = new Ship { Id = index * 2, Health = 50 };
            ship.Pose.X = index * 10.5;
            ship.Pose.Y = 100;
            ship.SetNameUtf8(name);
            fleet.Ships.Add(ship);
        }

        var spare = fleet.Ships[47];
        var baseline = new Fleet();
        baseline.CopyFrom(fleet);
        var received = new Fleet();
        var applied = new Fleet();
        var record = new byte[Fleet.MaxByteCount];
        var delta = new byte[Fleet.MaxByteCount];
        long sink = 0;

        // Each round changes a ship, removes the last one or adds it again, writes and reads the full record and a delta.
        void Round(int round)
        {
            fleet.Tick = (fleet.Tick + 1) % 65536;
            var ship = fleet.Ships[round % 40];
            ship.Health = (ship.Health + 1) % 101;
            ship.Pose.Heading = round % 720 * 0.5;
            ship.HasTarget = round % 2 == 0;
            ship.Target = round % 65536;
            if (round % 2 == 0) fleet.Ships.RemoveAt(fleet.Ships.Count - 1);
            else fleet.Ships.Add(spare);
            sink += received.Read(record.AsSpan(0, fleet.Write(record)));
            sink += applied.ReadDelta(delta.AsSpan(0, fleet.WriteDelta(baseline, delta)), baseline);
            baseline.CopyFrom(applied);
        }

        for (var round = 0; round < 100; round++) Round(round);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var round = 100; round < 1100; round++) Round(round);
        Assert.AreEqual(before, GC.GetAllocatedBytesForCurrentThread());
        Assert.IsTrue(received.WireEquals(fleet));
        Assert.AreNotEqual(0, sink);
    }
}
