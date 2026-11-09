// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Api;

namespace GameEventScript.Tests.Native.Schema;

/// <summary>Checks the public bit primitives and wire rules that generated schema code builds on.</summary>
[TestClass]
public sealed class GesBitTests
{
    [TestMethod]
    public void EveryWidthRoundTripsAcrossWordBoundaries()
    {
        var random = new Random(7);
        var widths = new int[400];
        var values = new ulong[widths.Length];
        for (var index = 0; index < widths.Length; index++)
        {
            widths[index] = index % 65;
            var bits = (ulong)random.NextInt64() ^ ((ulong)random.NextInt64() << 1);
            values[index] = widths[index] == 64 ? bits : bits & ((1UL << widths[index]) - 1);
        }

        var buffer = new byte[2048];
        var writer = new GesBitWriter(buffer);
        long total = 0;
        for (var index = 0; index < widths.Length; index++)
        {
            writer.Write(values[index], widths[index]);
            total += widths[index];
            Assert.AreEqual(total, writer.BitPosition);
        }

        var length = writer.Finish();
        Assert.AreEqual((int)((total + 7) / 8), length);

        var reader = new GesBitReader(buffer.AsSpan(0, length));
        for (var index = 0; index < widths.Length; index++) Assert.AreEqual(values[index], reader.Read(widths[index], "f"));
        Assert.AreEqual(length, reader.Finish());
    }

    [TestMethod]
    public void BitsAreLeastSignificantFirstInLittleEndianBytes()
    {
        var buffer = new byte[3];
        var writer = new GesBitWriter(buffer);
        writer.Write(0b101, 3);
        writer.Write(0x1FF, 9);
        writer.WriteBytes([0xAB]);
        Assert.AreEqual(3, writer.Finish());
        CollectionAssert.AreEqual(new byte[] { 0b1111_1101, 0b1011_1111, 0b0000_1010 }, buffer);
    }

    [TestMethod]
    public void SetBitAndRewindReachFlushedWords()
    {
        var buffer = new byte[16];
        var writer = new GesBitWriter(buffer);
        writer.Write(0, 3);
        writer.Write(ulong.MaxValue, 64);
        writer.Write(0, 5);
        writer.SetBit(1);
        writer.SetBit(70);
        Assert.AreEqual(72, writer.BitPosition);
        writer.Rewind(66);
        writer.Write(0b10, 2);
        Assert.AreEqual(68, writer.BitPosition);
        Assert.AreEqual(9, writer.Finish());

        var reader = new GesBitReader(buffer.AsSpan(0, 9));
        Assert.AreEqual(0b010UL, reader.Read(3, ""));
        Assert.AreEqual(ulong.MaxValue >> 1, reader.Read(63, ""));
        Assert.AreEqual(0b10UL, reader.Read(2, ""));
        reader.Seek(3);
        Assert.AreEqual(1UL, reader.Read(1, ""));

        var again = new GesBitWriter(buffer);
        again.Write(ulong.MaxValue, 64);
        again.Write(1, 6);
        again.Rewind(60);
        again.Write(0, 4);
        Assert.AreEqual(64, again.BitPosition);
        Assert.AreEqual(8, again.Finish());
        Assert.AreEqual(ulong.MaxValue >> 4, new GesBitReader(buffer.AsSpan(0, 8)).Read(64, ""));
    }

    [TestMethod]
    public void AFullBufferFailsWithoutWritingPastIt()
    {
        var buffer = new byte[1];
        AssertWire(GesWireException.BufferTooSmall, "", () =>
        {
            var writer = new GesBitWriter(buffer);
            writer.Write(0x3FF, 10);
            writer.Finish();
        });
        AssertWire(GesWireException.BufferTooSmall, "", () => new GesBitWriter(new byte[7]).Write(ulong.MaxValue, 64));
    }

    [TestMethod]
    public void TheEndOfDataNamesTheFieldAndTheLimit()
    {
        var data = new byte[] { 0xFF, 0x00, 0x01 };
        AssertWire(GesWireException.Truncated, "a.b", () => new GesBitReader(data).Read(25, "a.b"));
        AssertWire(GesWireException.LimitExceeded, "c", () => new GesBitReader(data, 2).Read(17, "c"));
        AssertWire(GesWireException.Truncated, "d", () => new GesBitReader(data).Require(25, "d"));
        AssertWire(GesWireException.InvalidPadding, "", () =>
        {
            var reader = new GesBitReader(data);
            reader.Read(1, "");
            reader.Finish();
        });

        var destination = new byte[4];
        AssertWire(GesWireException.Truncated, "e", () => new GesBitReader(data).ReadBytes(destination, "e"));
        CollectionAssert.AreEqual(new byte[4], destination);
        var whole = new GesBitReader(data, 3);
        Assert.AreEqual(0xFFUL, whole.Read(8, ""));
        whole.Require(16, "");
        Assert.AreEqual(8, whole.BitPosition);
    }

    [TestMethod]
    public void QuantizationFollowsTheSpecificationVectors()
    {
        // Step 1cm (p = 1, k = 2) in [0m, 5000m].
        ulong Index(double value, double numerator = 1, long lower = 0, long upper = 500_000) =>
            GesWireRules.QuantizedIndex(value, lower * numerator / 100, upper * numerator / 100, 100, numerator, lower, "x");
        Assert.AreEqual(123456UL, Index(1234.565));
        Assert.AreEqual(14UL, Index(0.135));
        Assert.AreEqual(100UL, Index(1.005));
        Assert.AreEqual(30UL, Index(0.30000000000000004));
        Assert.AreEqual(500000UL, Index(4999.996));
        Assert.AreEqual(247UL, Index(12.34, 5, 0, 100_000));
        Assert.AreEqual(1234.56, GesWireRules.QuantizedValue(123456, 0, 1, 100));
        Assert.AreEqual(12.35, GesWireRules.QuantizedValue(247, 0, 5, 100));
        AssertWire(GesWireException.OutOfRange, "x", () => Index(5000.004));
        AssertWire(GesWireException.OutOfRange, "x", () => Index(-0.004));
        AssertWire(GesWireException.OutOfRange, "x", () => Index(double.NaN));

        // 0.1° in a period of 360°, with a lower bound of 0.
        Assert.AreEqual(2700UL, GesWireRules.WrappedQuantizedIndex(-90, 10, 1, 0, 3600, "h"));
        Assert.AreEqual(0UL, GesWireRules.WrappedQuantizedIndex(359.95, 10, 1, 0, 3600, "h"));
        AssertWire(GesWireException.OutOfRange, "h", () => GesWireRules.WrappedQuantizedIndex(double.PositiveInfinity, 10, 1, 0, 3600, "h"));
        AssertWire(GesWireException.OutOfRange, "h", () => GesWireRules.WrappedQuantizedIndex(1e15, 10, 1, 0, 3600, "h"));
        Assert.AreEqual(359UL, GesWireRules.WrappedIndex(-1, 0, 360));
        Assert.AreEqual(0UL, GesWireRules.WrappedIndex(-360, 0, 360));
        Assert.AreEqual(5UL, GesWireRules.WrappedIndex(long.MaxValue, long.MaxValue - 5, 10));
    }

    [TestMethod]
    public void TextAndTagRulesWorkOnBytes()
    {
        Assert.IsTrue(GesWireRules.IsValidUtf8("aä€😀"u8));
        Assert.IsTrue(GesWireRules.IsValidUtf8([]));
        Assert.IsFalse(GesWireRules.IsValidUtf8([0xC0, 0x80]));
        Assert.IsFalse(GesWireRules.IsValidUtf8([0xED, 0xA0, 0x80]));
        Assert.IsFalse(GesWireRules.IsValidUtf8([0xE2, 0x82]));
        Assert.IsFalse(GesWireRules.IsValidUtf8([0xF4, 0x90, 0x80, 0x80]));
        Assert.IsTrue(GesWireRules.IsTagName("a1B"u8));
        Assert.IsFalse(GesWireRules.IsTagName("A1"u8));
        Assert.IsFalse(GesWireRules.IsTagName("a_b"u8));
        Assert.IsFalse(GesWireRules.IsTagName([]));
    }

    [TestMethod]
    public void WireErrorsCanBeCreatedWithTheirCodes()
    {
        var error = new GesWireException(GesWireException.InvalidDelta, "bots.x");
        Assert.AreEqual("wire.invalidDelta", error.Code);
        Assert.AreEqual("bots.x", error.Path);
    }

    [TestMethod]
    [TestCategory("Allocation")]
    public void BitPrimitivesAllocateNothing()
    {
        var buffer = new byte[64];
        var text = new byte[] { 0x61, 0x62 };
        var destination = new byte[2];
        long sink = 0;
        void Round()
        {
            var writer = new GesBitWriter(buffer);
            writer.Write(5, 3);
            writer.Write(ulong.MaxValue, 64);
            writer.WriteBytes(text);
            writer.SetBit(1);
            var length = writer.Finish();
            var reader = new GesBitReader(buffer.AsSpan(0, length));
            sink += (long)reader.Read(3, "a");
            sink += (long)reader.Read(64, "b");
            reader.ReadBytes(destination, "c");
            sink += reader.Finish();
            sink += (long)GesWireRules.QuantizedIndex(12.34, 0, 50, 100, 1, 0, "x") + (GesWireRules.IsValidUtf8(text) ? 1 : 0);
        }

        Round();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var round = 0; round < 1000; round++) Round();
        Assert.AreEqual(before, GC.GetAllocatedBytesForCurrentThread());
        Assert.AreNotEqual(0, sink);
    }

    private static void AssertWire(string code, string path, Action action)
    {
        try
        {
            action();
        }
        catch (GesWireException exception)
        {
            Assert.AreEqual(code, exception.Code);
            Assert.AreEqual(path, exception.Path);
            return;
        }

        Assert.Fail($"Expected {code}.");
    }
}
