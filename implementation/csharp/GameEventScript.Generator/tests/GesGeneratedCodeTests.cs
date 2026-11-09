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

        // The read would overwrite the pooled ships of fleet while it reads them as the baseline.
        void Rejected(Fleet baseline, string path)
        {
            var error = Assert.ThrowsExactly<GesWireException>(() => fleet.ReadDelta(delta, baseline));
            Assert.AreEqual(GesWireException.OverlappingBuffers, error.Code);
            Assert.AreEqual(path, error.Path);
        }

        Rejected(fleet, "");
        Rejected(new Fleet { Ships = fleet.Ships }, "ships");
        Rejected(new Fleet { Ships = new List<Ship>(fleet.Ships) }, "ships");
        Rejected(new Fleet { Ships = { new Ship { Id = 7 }, fleet.Ships[2] } }, "ships");

        // Copying a record into itself, or from a record that holds the same list, keeps its values.
        fleet.CopyFrom(fleet);
        fleet.CopyFrom(new Fleet { Tick = 3, Ships = fleet.Ships });
        Assert.IsTrue(fleet.WireEquals(source));
        fleet.Ships[0].CopyFrom(fleet.Ships[0]);
        Assert.IsTrue(fleet.WireEquals(source));

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
