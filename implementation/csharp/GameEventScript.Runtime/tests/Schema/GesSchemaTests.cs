// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Text;
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
    public void MessageSetMembersCarryTheSetHash()
    {
        var set = GesSchemaSet.Parse("message Hit(target: :Number integer between 0 and 15)\nmessage Died(target: :Number integer between 0 and 15)\nmessages :Combat as (Hit, Died)\n");
        var combat = set.Schema("Combat").Bind();
        var member = combat.Member("Hit");
        Assert.AreEqual(combat.Hash, member.Hash);
        Assert.AreEqual(combat.BindingHash, member.BindingHash);
        Assert.AreEqual(set.Schema("Hit").Hash, set.Schema("Hit").Bind().Hash);
        Assert.AreNotEqual(set.Schema("Hit").Hash, member.Hash);
        Assert.AreEqual(set.Schema("Hit").Hash, member.Schema.Hash);
    }

    [TestMethod]
    public void WriterAndReaderMatchTheWorkedExample()
    {
        var bot = Bot();
        Assert.AreEqual(14, bot.MaxByteCount);
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
    public void DeltasAllocateNothing()
    {
        var source = "schema :Part as {\n  v: :Number integer between 0 and 3\n  on: :Boolean\n}\n\nschema :S as {\n  id: :Number integer between 0 and 255 key\n"
            + "  x: :Quantity(m) between 0m and 100m step 1cm delta within 1m\n  name: :Text max 4 optional\n  items: :List(:Text max 2) max 2\n  part: :Part\n  pick: :Boolean | :Part\n}\n";
        var schema = GesSchemaSet.Parse([new GesSchemaSource("s.gess", source)]).Schema("S").Bind(new GesSchemaParameters());
        var first = new byte[GesSchemaValues.Encode(schema, Sample(1, "ab"), new byte[64])];
        GesSchemaValues.Encode(schema, Sample(1, "ab"), first);
        var second = new byte[GesSchemaValues.Encode(schema, Sample(2, "aä"), new byte[64])];
        GesSchemaValues.Encode(schema, Sample(2, "aä"), second);
        var delta = new byte[schema.MaxByteCount];
        var record = new byte[schema.MaxByteCount];

        void Round()
        {
            var length = GesSchemaDelta.Encode(schema, second, first, delta);
            var decoded = GesSchemaDelta.Decode(schema, delta.AsSpan(0, length), first, record);
            if (decoded.ByteCount != second.Length) throw new InvalidOperationException();
        }

        Round();
        CollectionAssert.AreEqual(second, record.AsSpan(0, second.Length).ToArray());
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var round = 0; round < 1000; round++) Round();
        Assert.AreEqual(before, GC.GetAllocatedBytesForCurrentThread());
    }

    [TestMethod]
    public void KeyedListDeltasAllocateNothing()
    {
        var source = "schema :Leaf as {\n  id: :Number integer key\n  v: :Number integer between 0 and 7\n}\n\nschema :Item as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 15\n  leaves: :List(:Leaf) max 2\n}\n\n"
            + "schema :S as {\n  items: :List(:Item) max 4\n}\n";
        var schema = GesSchemaSet.Parse([new GesSchemaSource("s.gess", source)]).Schema("S").Bind(new GesSchemaParameters());

        GesValue Leaf(long id, long v) => GesValue.GesRecord("Leaf", ("id", GesValue.GesInteger(id)), ("v", GesValue.GesInteger(v)));
        GesValue Item(long id, long hp, params GesValue[] leaves) => GesValue.GesRecord("Item", ("id", GesValue.GesInteger(id)), ("hp", GesValue.GesInteger(hp)), ("leaves", GesValue.GesList(leaves)));
        byte[] Encoded(params GesValue[] items)
        {
            var value = GesValue.GesRecord("S", ("items", GesValue.GesList(items)));
            var bytes = new byte[schema.MaxByteCount];
            return bytes.AsSpan(0, GesSchemaValues.Encode(schema, value, bytes)).ToArray();
        }

        var first = Encoded(Item(1, 2, Leaf(-1, 1), Leaf(4, 2)), Item(2, 3), Item(5, 4));
        var second = Encoded(Item(1, 2, Leaf(-1, 3), Leaf(4, 2)), Item(3, 7, Leaf(1, 1)), Item(5, 9));
        var delta = new byte[schema.MaxByteCount];
        var record = new byte[schema.MaxByteCount];

        void Round()
        {
            var length = GesSchemaDelta.Encode(schema, second, first, delta);
            var decoded = GesSchemaDelta.Decode(schema, delta.AsSpan(0, length), first, record);
            if (decoded.ByteCount != second.Length) throw new InvalidOperationException();
        }

        Round();
        CollectionAssert.AreEqual(second, record.AsSpan(0, second.Length).ToArray());
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var round = 0; round < 1000; round++) Round();
        Assert.AreEqual(before, GC.GetAllocatedBytesForCurrentThread());
    }

    [TestMethod]
    public void FieldsAreUsedInDeclarationOrder()
    {
        var schema = GesSchemaSet.Parse([new GesSchemaSource("s.gess", "schema :S as {\n  a: :Boolean\n  b: :Boolean optional\n}\n\nschema :T as {\n  c: :Boolean\n}\n")]);
        var first = schema.Schema("S").Bind(new GesSchemaParameters());
        var other = schema.Schema("T").Bind(new GesSchemaParameters());
        var (a, b, c) = (first.Field("a"), first.Field("b"), other.Field("c"));

        void Fails(string path, Action call)
        {
            var exception = Assert.ThrowsExactly<GesWireException>(call);
            Assert.AreEqual("wire.fieldOrder", exception.Code);
            Assert.AreEqual(path, exception.Path);
        }

        Fails("b", () => new GesSchemaWriter(first, new byte[8]).WriteBoolean(b, true));
        Fails("c", () => new GesSchemaWriter(first, new byte[8]).WriteBoolean(c, true));
        Fails("a", () => new GesSchemaReader(first, new byte[8]).Finish());
        Fails("b", () => new GesSchemaReader(first, new byte[8]).ReadBoolean(b));
        Fails("c", () => new GesSchemaReader(first, new byte[8]).ReadBoolean(c));
        Fails("", () => new GesSchemaReader(first, new byte[8]).ReadKey());
        Fails("b", () =>
        {
            var reader = new GesSchemaReader(first, new byte[8]);
            reader.ReadBoolean(a);
            reader.ReadBoolean(b);
        });
    }

    [TestMethod]
    public void DeltaTargetsMustNotOverlapTheirInputs()
    {
        var schema = GesSchemaSet.Parse([new GesSchemaSource("s.gess", "schema :S as {\n  a: :Boolean\n  b: :Boolean\n}\n")]).Schema("S").Bind(new GesSchemaParameters());
        var memory = new byte[8];
        byte[] other = [0];

        void Fails(Action call) => Assert.AreEqual("wire.overlappingBuffers", Assert.ThrowsExactly<GesWireException>(call).Code);

        Fails(() => GesSchemaDelta.Encode(schema, memory.AsSpan(0, 1), other, memory));
        Fails(() => GesSchemaDelta.Encode(schema, other, memory.AsSpan(7, 1), memory.AsSpan(4)));
        Fails(() => GesSchemaDelta.Decode(schema, memory.AsSpan(0, 1), other, memory.AsSpan(0, 4)));
        Fails(() => GesSchemaDelta.Decode(schema, other, memory, memory));
        Assert.AreEqual(1, GesSchemaDelta.Encode(schema, memory.AsSpan(0, 1), memory.AsSpan(0, 1), memory.AsSpan(1)));
        Assert.AreEqual(1, GesSchemaDelta.Decode(schema, memory.AsSpan(0, 1), memory.AsSpan(1, 1), memory.AsSpan(2)).ByteCount);
    }

    private static GesValue Sample(int x, string name)
    {
        var part = GesValue.GesRecord("Part", ["v", "on"], [GesValue.GesInteger(x), GesValue.GesBoolean(x == 1)]);
        return GesValue.GesRecord(
            "S",
            ["id", "x", "name", "items", "part", "pick"],
            [GesValue.GesInteger(7), GesValue.GesFloat(x * 40, GesQuantityUnit.UnitMeter), GesValue.GesText(name), GesValue.GesList([GesValue.GesText("a"), GesValue.GesText(name[..1])]), part, part]);
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
    public void ATagWithAMaximumWritesOnlyTagNames()
    {
        var bound = GesSchemaSet.Parse("schema :S as { t: :Tag max 3 }").Schema("S").Bind();
        var field = bound.Field("t");
        var buffer = new byte[8];
        var writer = new GesSchemaWriter(bound, buffer);
        foreach (var invalid in new[] { "", "Ab", "a_", "1a", "ä" })
        {
            var code = "";
            try
            {
                writer.WriteText(field, Encoding.UTF8.GetBytes(invalid));
            }
            catch (GesWireException exception)
            {
                code = exception.Code;
            }

            Assert.AreEqual(GesWireException.InvalidTag, code, invalid);
        }

        writer.WriteText(field, "ab"u8);
        Assert.AreEqual(3, writer.Finish());
        CollectionAssert.AreEqual(new byte[] { 0x86, 0x89, 0x01 }, buffer.AsSpan(0, 3).ToArray());
    }

    [TestMethod]
    [TestCategory("Allocation")]
    public void UnionsAndDiceAllocateNothing()
    {
        var bound = GesSchemaSet.Parse("schema :S as { v: :Quantity(°) wrapping between 0° and 360° step 1° | :Tag(#a, #b) | :Nothing, next: :Boolean, d: :Dice max 2 between 1 and 6, n: :Tag max 3 }").Schema("S").Bind();
        var (v, tag, next, d, roll) = (bound.Field("v"), bound.Field("v:Tag"), bound.Field("next"), bound.Field("d"), bound.Field("d[]"));
        var name = bound.Field("n");
        var tagName = new byte[] { 0x61, 0x62 };
        var received = new byte[3];
        Span<byte> buffer = stackalloc byte[(int)bound.MaxByteCount];

        void Round(Span<byte> memory)
        {
            var writer = new GesSchemaWriter(bound, memory);
            writer.SelectAlternative(v, 1);
            writer.WriteTag(tag, 1);
            writer.WriteBoolean(next, true);
            writer.BeginList(d, 1);
            writer.WriteInteger(roll, 4);
            writer.WriteText(name, tagName);
            var length = writer.Finish();
            var reader = new GesSchemaReader(bound, memory.Slice(0, length));
            Assert.AreEqual(1, reader.ReadAlternative(v));
            Assert.AreEqual(1, reader.ReadTag(tag));
            Assert.IsTrue(reader.ReadBoolean(next));
            Assert.AreEqual(1, reader.BeginList(d));
            Assert.AreEqual(4, reader.ReadInteger(roll));
            Assert.AreEqual(2, reader.ReadText(name, received));
            reader.Finish();

            writer = new GesSchemaWriter(bound, memory);
            writer.SelectAlternative(v, 2);
            writer.WriteBoolean(next, false);
            writer.BeginList(d, 0);
            writer.WriteText(name, tagName);
            length = writer.Finish();
            reader = new GesSchemaReader(bound, memory.Slice(0, length));
            Assert.AreEqual(2, reader.ReadAlternative(v));
            Assert.IsFalse(reader.ReadBoolean(next));
            Assert.AreEqual(0, reader.BeginList(d));
            Assert.AreEqual(2, reader.ReadText(name, received));
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

    [TestMethod]
    public void ARecordBelowTheDepthNamesTheDepth()
    {
        var set = GesSchemaSet.Parse("schema :Bind as {\n  name: :Text max 8\n  units: :List(:Bind) max 4 depth 2\n}\n");
        var schema = set.Schema("Bind").Bind();
        GesValue Bind(string name, params GesValue[] units) => GesValue.GesRecord("Bind", ("name", GesValue.GesText(name)), ("units", GesValue.GesList(units)));
        var buffer = new byte[schema.MaxByteCount];
        Assert.IsGreaterThan(0, GesSchemaValues.Encode(schema, Bind("a", Bind("b")), buffer));
        var error = Assert.ThrowsExactly<GesWireException>(() => GesSchemaValues.Encode(schema, Bind("a", Bind("b", Bind("c"))), buffer));
        Assert.AreEqual(GesWireException.TooDeep, error.Code);
        Assert.AreEqual("units[].units", error.Path);
        Assert.AreEqual("deeper than depth 2", error.Detail);
        Assert.AreEqual("wire.tooDeep at 'units[].units': deeper than depth 2", error.Message);
        Assert.AreEqual("", new GesWireException(GesWireException.TooLong, "units").Detail);
    }

    [TestMethod]
    public void ReservedNamesPointToBackticks()
    {
        var error = Assert.ThrowsExactly<GesSchemaException>(() => GesSchemaSet.Parse("schema :S as { length: :Number, b: :Number computed by length / 2 }"));
        Assert.AreEqual("schema.invalidExpression", error.Code);
        StringAssert.Contains(error.Message, "in backticks, as `length`");
        GesSchemaSet.Parse("schema :S as { length: :Number, b: :Number computed by `length` / 2 }").Schema("S").Bind();
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
