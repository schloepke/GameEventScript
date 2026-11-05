// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;

namespace GameEventScript.Tests.Native.Schema;

/// <summary>Checks the implementation details of the writer and reader that the value-based corpus cannot reach.</summary>
[TestClass]
public sealed class GesSchemaTests
{
    private const string BotSource = """
        constant $maxHealth be 135
        parameter worldWidth: :Quantity(m)
        parameter worldHeight: :Quantity(m)

        schema :Bot as {
          id: :Number integer between 0 and 65535 key
          x: :Quantity(m) between 0m and worldWidth step 1cm delta within 1.27m
          y: :Quantity(m) between 0m and worldHeight step 1cm delta within 1.27m
          heading: :Quantity(°) wrapping between 0° and 360° step 0.1° delta within 8°
          turretBearing: :Quantity(°) wrapping between 0° and 360° step 0.1° delta within 8°
          radarBearing: :Quantity(°) wrapping between 0° and 360° step 0.1° delta within 8°
          health: :Number integer between 0 and $maxHealth
          firing: :Boolean
          shielded: :Boolean
          overheated: :Boolean
        }
        """;

    private static GesBoundSchema Bot() => GesSchemaSet.Parse(BotSource).Schema("Bot").Bind(new GesSchemaParameters().With("worldWidth", "5000m").With("worldHeight", "5km"));

    [TestMethod]
    public void HashVectorsAndSpellingIndependence()
    {
        var bot = Bot();
        Assert.AreEqual("baecf12abb34e7c1", bot.Hash.ToString("x16"));
        Assert.AreEqual("551dd726cf2392d4", bot.BindingHash.ToString("x16"));
        var plain = GesSchemaSet.Parse("schema :Force as { force: :Quantity(N) between 0kN and 50kN step 0.01kN }");
        var spelled = GesSchemaSet.Parse("// other\n\nschema :Force as {\n force: :Quantity(N) between 0N and 50000N step 10N\n}\n");
        Assert.AreEqual(plain.Schema("Force").CanonicalForm, spelled.Schema("Force").CanonicalForm);
        Assert.AreEqual("gess1\nschema Force\nfield force Quantity(newton) between 0e0 5e4 step 1e1\n", plain.Schema("Force").CanonicalForm);
    }

    [TestMethod]
    public void WriterAndReaderMatchTheWorkedExample()
    {
        var bot = Bot();
        Assert.AreEqual(13, bot.MaxByteCount);
        Span<byte> buffer = stackalloc byte[32];
        var writer = new GesSchemaWriter(bot, buffer);
        writer.WriteInteger(bot.Field("id"), 42);
        writer.WriteNumber(bot.Field("x"), 1234.565);
        writer.WriteNumber(bot.Field("y"), 4999.996);
        writer.WriteNumber(bot.Field("heading"), -90);
        writer.WriteNumber(bot.Field("turretBearing"), 359.95);
        writer.WriteNumber(bot.Field("radarBearing"), 10);
        writer.WriteInteger(bot.Field("health"), 135);
        writer.WriteBoolean(bot.Field("firing"), true);
        writer.WriteBoolean(bot.Field("shielded"), false);
        writer.WriteBoolean(bot.Field("overheated"), true);
        var length = writer.Finish();
        Assert.AreEqual(13, length);

        var reader = new GesSchemaReader(bot, buffer.Slice(0, length));
        Assert.AreEqual(42, reader.ReadKey());
        Assert.AreEqual(1234.56, reader.ReadNumber(bot.Field("x")));
        Assert.AreEqual(5000d, reader.ReadNumber(bot.Field("y")));
        Assert.AreEqual(270d, reader.ReadNumber(bot.Field("heading")));
        Assert.AreEqual(0d, reader.ReadNumber(bot.Field("turretBearing")));
        Assert.AreEqual(10d, reader.ReadNumber(bot.Field("radarBearing")));
        Assert.AreEqual(135, reader.ReadInteger(bot.Field("health")));
        Assert.IsTrue(reader.ReadBoolean(bot.Field("firing")));
        Assert.IsFalse(reader.ReadBoolean(bot.Field("shielded")));
        Assert.IsTrue(reader.ReadBoolean(bot.Field("overheated")));
        Assert.AreEqual(13, reader.Finish());
    }

    [TestMethod]
    public void DescriptionIsSharedAndConvertsBounds()
    {
        var set = GesSchemaSet.Parse("parameter w: :Quantity(m)\nschema :U as {\n  x: :Quantity(m) between 0.5m and w\n}\nschema :A as {\n  u: :U\n}\n");
        var unit = set.Schema("U");
        Assert.AreSame(unit, set.Schema("A").Fields[0].Alternatives[0].Schema);
        Assert.AreSame(unit, set.Schemas[0]);
        var x = unit.Fields[0].Alternatives[0];
        Assert.AreEqual(0.5, x.Lower!.Value.ToDouble());
        Assert.IsNull(x.Upper!.Value.ToDouble());
        Assert.AreEqual("w", x.Upper!.Value.Parameter);
        Assert.AreSame(unit, unit.Bind(new GesSchemaParameters().With("w", "5m")).Schema);
    }

    [TestMethod]
    public void RepeatedPresenceReadsOneBit()
    {
        var bound = GesSchemaSet.Parse("schema :S as {\n  a: :Boolean optional\n  b: :Boolean\n}\n").Schema("S").Bind();
        var (a, b) = (bound.Field("a"), bound.Field("b"));
        byte[] data = [0b011];
        var reader = new GesSchemaReader(bound, data);
        Assert.IsTrue(reader.ReadPresence(a));
        Assert.IsTrue(reader.ReadPresence(a));
        Assert.IsTrue(reader.ReadBoolean(a));
        Assert.IsFalse(reader.ReadBoolean(b));
        Assert.AreEqual(1, reader.Finish());
    }

    [TestMethod]
    public void RejectedTagLeavesTheWriterUnchanged()
    {
        var state = GesSchemaSet.Parse("schema :S as {\n  state: :Tag(#a, #b) optional\n}\n").Schema("S").Bind();
        var field = state.Field("state");
        var buffer = new byte[2];
        var writer = new GesSchemaWriter(state, buffer);
        try
        {
            writer.WriteTag(field, 9);
            Assert.Fail("Expected wire.unknownTag.");
        }
        catch (GesWireException exception)
        {
            Assert.AreEqual(GesWireException.UnknownTag, exception.Code);
        }

        writer.WriteTag(field, 1);
        Assert.AreEqual(1, writer.Finish());
        Assert.AreEqual(0b11, buffer[0]);
    }

    [TestMethod]
    public void WriterAndReaderAllocateNothing()
    {
        var bot = Bot();
        var (id, x, y, heading, turret, radar, health, firing, shielded, overheated) = (bot.Field("id"), bot.Field("x"), bot.Field("y"), bot.Field("heading"), bot.Field("turretBearing"), bot.Field("radarBearing"), bot.Field("health"), bot.Field("firing"), bot.Field("shielded"), bot.Field("overheated"));
        var buffer = new byte[32];
        void Round()
        {
            var writer = new GesSchemaWriter(bot, buffer);
            writer.WriteInteger(id, 42);
            writer.WriteNumber(x, 1234.565);
            writer.WriteNumber(y, 7);
            writer.WriteNumber(heading, 90);
            writer.WriteNumber(turret, 45);
            writer.WriteNumber(radar, 10);
            writer.WriteInteger(health, 135);
            writer.WriteBoolean(firing, true);
            writer.WriteBoolean(shielded, false);
            writer.WriteBoolean(overheated, true);
            var length = writer.Finish();
            var reader = new GesSchemaReader(bot, buffer.AsSpan(0, length));
            reader.ReadKey();
            reader.ReadNumber(x);
            reader.ReadNumber(y);
            reader.ReadNumber(heading);
            reader.ReadNumber(turret);
            reader.ReadNumber(radar);
            reader.ReadInteger(health);
            reader.ReadBoolean(firing);
            reader.ReadBoolean(shielded);
            reader.ReadBoolean(overheated);
            reader.Finish();
        }

        Round();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var round = 0; round < 1000; round++) Round();
        Assert.AreEqual(before, GC.GetAllocatedBytesForCurrentThread());
    }

    [TestMethod]
    public void AnInvalidAlternativeWritesNothing()
    {
        var bound = GesSchemaSet.Parse("schema :S as { a: :Boolean | :Text max 1 optional }").Schema("S").Bind();
        var (a, flag) = (bound.Field("a"), bound.Field("a:Boolean"));
        var buffer = new byte[8];
        var writer = new GesSchemaWriter(bound, buffer);
        var code = "";
        try
        {
            writer.SelectAlternative(a, 2);
        }
        catch (GesWireException exception)
        {
            code = exception.Code;
        }

        Assert.AreEqual(GesWireException.OutOfRange, code);
        writer.SelectAlternative(a, 0);
        writer.WriteBoolean(flag, true);
        Assert.AreEqual(1, writer.Finish());
        Assert.AreEqual(0b101, buffer[0]);
    }

    [TestMethod]
    [TestCategory("Allocation")]
    public void UnionsAndDiceAllocateNothing()
    {
        var bound = GesSchemaSet.Parse("schema :S as { v: :Quantity(°) wrapping between 0° and 360° step 1° | :Tag(#a, #b) | :Nothing, next: :Boolean, d: :Dice max 2 between 1 and 6 }").Schema("S").Bind();
        var (v, tag, next, d, roll) = (bound.Field("v"), bound.Field("v:Tag"), bound.Field("next"), bound.Field("d"), bound.Field("d[]"));
        Span<byte> buffer = stackalloc byte[(int)bound.MaxByteCount];

        void Round(Span<byte> memory)
        {
            var writer = new GesSchemaWriter(bound, memory);
            writer.SelectAlternative(v, 1);
            writer.WriteTag(tag, 1);
            writer.WriteBoolean(next, true);
            writer.BeginList(d, 1);
            writer.WriteInteger(roll, 4);
            var length = writer.Finish();
            var reader = new GesSchemaReader(bound, memory.Slice(0, length));
            Assert.AreEqual(1, reader.ReadAlternative(v));
            Assert.AreEqual(1, reader.ReadTag(tag));
            Assert.IsTrue(reader.ReadBoolean(next));
            Assert.AreEqual(1, reader.BeginList(d));
            Assert.AreEqual(4, reader.ReadInteger(roll));
            reader.Finish();

            writer = new GesSchemaWriter(bound, memory);
            writer.SelectAlternative(v, 2);
            writer.WriteBoolean(next, false);
            writer.BeginList(d, 0);
            length = writer.Finish();
            reader = new GesSchemaReader(bound, memory.Slice(0, length));
            Assert.AreEqual(2, reader.ReadAlternative(v));
            Assert.IsFalse(reader.ReadBoolean(next));
            Assert.AreEqual(0, reader.BeginList(d));
            reader.Finish();
        }

        Round(buffer);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var round = 0; round < 1000; round++) Round(buffer);
        Assert.AreEqual(before, GC.GetAllocatedBytesForCurrentThread());
    }

    [TestMethod]
    [TestCategory("Allocation")]
    public void DecodingAllocatesOnlyWhatTextsHold()
    {
        var schema = GesSchemaSet.Parse("schema :S as { items: :List(:Text max 1000) max 1000 }").Schema("S").Bind();
        var items = new GesValue[1000];
        for (var item = 0; item < items.Length; item++) items[item] = GesValue.GesText("");
        var data = new byte[2048];
        var length = GesSchemaValues.Encode(schema, GesValue.GesRecord("S", ["items"], [GesValue.GesList(items)]), data);
        var before = GC.GetAllocatedBytesForCurrentThread();
        GesSchemaValues.Decode(schema, data.AsSpan(0, length));
        Assert.IsLessThan(200_000L, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [TestMethod]
    public void ListsNestAndOptionalsMarkAbsence()
    {
        var set = GesSchemaSet.Parse("schema :Mount as { size: :Number integer between 0 and 3\n label: :Text max 4 optional }\nschema :Unit as { mounts: :List(:Mount) max 2\n next: :Boolean }");
        var unit = set.Schema("Unit").Bind();
        var (mounts, size, label, next) = (unit.Field("mounts"), unit.Field("mounts[].size"), unit.Field("mounts[].label"), unit.Field("next"));
        Span<byte> buffer = stackalloc byte[32];
        var element = unit.Field("mounts[]");
        var writer = new GesSchemaWriter(unit, buffer);
        writer.BeginList(mounts, 2);
        writer.BeginNested(element);
        writer.WriteInteger(size, 1);
        writer.WriteText(label, "ab"u8);
        writer.BeginNested(element);
        writer.WriteInteger(size, 3);
        writer.WriteAbsent(label);
        writer.WriteBoolean(next, true);
        var length = writer.Finish();

        var reader = new GesSchemaReader(unit, buffer.Slice(0, length));
        Assert.AreEqual(2, reader.BeginList(mounts));
        reader.BeginNested(element);
        Assert.AreEqual(1, reader.ReadInteger(size));
        Assert.IsTrue(reader.ReadPresence(label));
        Span<byte> text = stackalloc byte[4];
        Assert.AreEqual(2, reader.ReadText(label, text));
        reader.BeginNested(element);
        Assert.AreEqual(3, reader.ReadInteger(size));
        Assert.IsFalse(reader.ReadPresence(label));
        Assert.IsTrue(reader.ReadBoolean(next));
        Assert.AreEqual(length, reader.Finish());
    }

    [TestMethod]
    public void ReaderRejectsInvalidData()
    {
        var set = GesSchemaSet.Parse("schema :Small as { value: :Number integer between 0 and 4 }");
        var small = set.Schema("Small").Bind();
        var field = small.Field("value");
        Assert.AreEqual("wire.invalidIndex", Read(small, field, [7]));
        Assert.AreEqual("wire.invalidPadding", Read(small, field, [0x09]));
        Assert.AreEqual("wire.truncated", Read(small, field, []));
    }

    private static string Read(GesBoundSchema schema, GesSchemaField field, byte[] data)
    {
        try
        {
            var reader = new GesSchemaReader(schema, data);
            reader.ReadInteger(field);
            reader.Finish();
            return "";
        }
        catch (GesWireException exception)
        {
            return exception.Code;
        }
    }
}
