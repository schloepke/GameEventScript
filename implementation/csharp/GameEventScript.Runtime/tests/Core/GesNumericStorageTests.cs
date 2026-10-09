// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Runtime.Values;

namespace GameEventScript.Tests.Native.Core;

/// <summary>Checks the internal separation between numeric projections and collection cardinality.</summary>
[TestClass]
public sealed class GesNumericStorageTests
{
    /// <summary>Dice caches an exact sum; overwriting that slot with nonnumeric data clears numeric storage.</summary>
    [TestMethod]
    public void NumericStorageDoesNotContainCollectionCounts()
    {
        var value = GesValue.GesDice([int.MaxValue, int.MaxValue, 1]);
        Assert.AreEqual(4294967295L, value.IntegerValue);
        Assert.AreEqual(3, value.Length);
        Assert.AreEqual(4294967295L, value.AsInteger());
        var retained = value;
        value.SetList([GesValue.GesInteger(7)]);
        Assert.AreEqual(0L, value.IntegerValue);
        Assert.AreEqual(1, value.Length);
        Assert.IsFalse(value.IsNumeric);
        Assert.AreEqual(4294967295L, retained.IntegerValue);
        value.SetText("a😀b");
        Assert.AreEqual(0L, value.IntegerValue);
        Assert.AreEqual(3, value.Length);
        value.SetRange(1L, long.MaxValue, 1L);
        Assert.AreEqual(0L, value.IntegerValue);
        Assert.AreEqual(long.MaxValue, value.CollectionCount);
        Assert.AreEqual(int.MaxValue, value.Length);
        value.SetDice([]);
        Assert.AreEqual(0L, value.IntegerValue);
        Assert.AreEqual(0, value.Length);
        Assert.IsTrue(value.IsNumeric);
        value.SetInteger(123);
        Assert.AreEqual(0, value.Length);
    }
}
