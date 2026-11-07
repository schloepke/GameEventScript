// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections;
using System.Collections.Generic;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using GameEventScript.Syntax;

namespace GameEventScript.Tests.Native.Values;

/// <summary>Checks the C# spellings of entries, member reads and traversal that the value-based corpus cannot reach.</summary>
[TestClass]
public sealed class GesValueAccessTests
{
    [TestMethod]
    public void EntriesAreWrittenAsTupleArguments()
    {
        Assert.AreEqual(0, GesValue.GesMap().Length);
        Assert.AreEqual(0, GesValue.GesRecord("Hit").Length);
        Assert.AreEqual(GesValue.GesMap(["a", "b"], [GesValue.GesInteger(1), GesValue.GesInteger(2)]), GesValue.GesMap(("b", GesValue.GesInteger(2)), ("a", GesValue.GesInteger(1))));
        Assert.AreEqual(GesValue.GesRecord("Hit", ["a"], [GesValue.GesInteger(2)]), GesValue.GesRecord("Hit", ("a", GesValue.GesInteger(1)), ("a", GesValue.GesInteger(2))));
    }

    [TestMethod]
    public void EntriesRejectInvalidKeysLikeParallelArrays()
    {
        Assert.ThrowsExactly<ArgumentException>(() => GesValue.GesMap(("\uD800", GesValue.GesNothing())));
        Assert.ThrowsExactly<ArgumentException>(() => GesValue.GesMap(["\uD800"], [GesValue.GesNothing()]));
    }

    [TestMethod]
    public void MissingNamesDoNotFailButAnAbsentNameDoes()
    {
        var map = GesValue.GesMap(("a", GesValue.GesInteger(1)), ("\u2478", GesValue.GesInteger(2)));
        Assert.IsTrue(map["\uD800"].IsNothing);
        Assert.IsTrue(map["\uD800x"].IsNothing);
        Assert.ThrowsExactly<ArgumentNullException>(() => GesValue.GesNothing()[null!]);
        Assert.ThrowsExactly<ArgumentNullException>(() => GesValue.GesNothing().ReadField(null!));
    }

    [TestMethod]
    public void FactoriesNameTheArgumentOfTheirForm()
    {
        var nothing = GesValue.GesNothing();
        Assert.AreEqual("entries", Assert.ThrowsExactly<ArgumentNullException>(() => GesValue.GesMap(((string, GesValue)[])null!)).ParamName);
        Assert.AreEqual("entries", Assert.ThrowsExactly<ArgumentNullException>(() => GesValue.GesMap((null!, nothing))).ParamName);
        Assert.AreEqual("entries", Assert.ThrowsExactly<ArgumentException>(() => GesValue.GesMap(("\uD800", nothing))).ParamName);
        Assert.AreEqual("fields", Assert.ThrowsExactly<ArgumentNullException>(() => GesValue.GesRecord("Hit", ((string, GesValue)[])null!)).ParamName);
        Assert.AreEqual("fields", Assert.ThrowsExactly<ArgumentNullException>(() => GesValue.GesRecord("Hit", (null!, nothing))).ParamName);
        Assert.AreEqual("fields", Assert.ThrowsExactly<ArgumentException>(() => GesValue.GesRecord("Hit", ("\uD800", nothing))).ParamName);
        Assert.AreEqual("keys", Assert.ThrowsExactly<ArgumentNullException>(() => GesValue.GesMap(null!, [nothing])).ParamName);
        Assert.AreEqual("values", Assert.ThrowsExactly<ArgumentNullException>(() => GesValue.GesRecord("Hit", ["a"], null!)).ParamName);
        Assert.AreEqual("keys", Assert.ThrowsExactly<ArgumentException>(() => GesValue.GesRecord("Hit", ["\uD800"], [nothing])).ParamName);
    }

    [TestMethod]
    public void TheLastDuplicateWinsInLongInputs()
    {
        // Beyond the insertion-sort range of Array.Sort, which is not stable.
        var keys = new string[3000];
        var values = new GesValue[3000];
        for (var index = 0; index < keys.Length; index++)
        {
            keys[index] = "key" + (index % 3);
            values[index] = GesValue.GesInteger(index);
        }

        foreach (var value in new[] { GesValue.GesMap(keys, values), GesValue.GesRecord("Hit", keys, values) })
        {
            Assert.AreEqual(3, value.Length);
            Assert.AreEqual(2997L, value["key0"].AsInteger());
            Assert.AreEqual(2998L, value["key1"].AsInteger());
            Assert.AreEqual(2999L, value["key2"].AsInteger());
        }
    }

    [TestMethod]
    public void LoneSurrogatesCompareAsOneUnit()
    {
        Assert.IsLessThan(0, GesText.CompareScalarOrdinal("\uD800a", "\uD800b"));
        Assert.AreEqual(0, GesText.CompareScalarOrdinal(new string(['\uD800', 'a']), "\uD800a"));
        Assert.AreEqual(0, GesText.CompareScalarOrdinal(new string(['\uD800']), "\uD800"));
        Assert.IsGreaterThan(0, GesText.CompareScalarOrdinal("\uD83D\uDE00", "\uFFFF"));
    }

    [TestMethod]
    public void TraversalWorksThroughEveryEnumerationForm()
    {
        var map = GesValue.GesMap(("b", GesValue.GesInteger(2)), ("a", GesValue.GesInteger(1))).AsMap()!;
        var keys = new List<string>();
        foreach (var (key, value) in map) keys.Add(key + value.AsInteger());
        CollectionAssert.AreEqual(new[] { "a1", "b2" }, keys);

        using IEnumerator<KeyValuePair<string, GesValue>> typed = ((IEnumerable<KeyValuePair<string, GesValue>>)map).GetEnumerator();
        Assert.IsTrue(typed.MoveNext());
        Assert.AreEqual("a", typed.Current.Key);
        typed.Reset();
        Assert.IsTrue(typed.MoveNext());
        Assert.AreEqual("a", typed.Current.Key);

        var count = 0;
        foreach (var entry in (IEnumerable)map)
        {
            Assert.IsInstanceOfType<KeyValuePair<string, GesValue>>(entry);
            count++;
        }
        Assert.AreEqual(2, count);
    }

    [TestMethod]
    [TestCategory("Allocation")]
    public void TraversalAndMemberReadsAllocateNothing()
    {
        var value = GesValue.GesRecord("Unit", ("hp", GesValue.GesInteger(5)), ("position", GesValue.GesPoint(1, 2, 3)));
        var map = value.AsMap()!;

        long Round()
        {
            long total = 0;
            foreach (var entry in map) total += entry.Key.Length;
            total += value["hp"].AsInteger() + (long)value["position"]["y"].AsNumber();
            return value["missing"]["x"].IsNothing ? total : 0;
        }

        Assert.AreEqual(17L, Round());
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var round = 0; round < 1000; round++) Round();
        Assert.AreEqual(before, GC.GetAllocatedBytesForCurrentThread());
    }

    [TestMethod]
    [TestCategory("Allocation")]
    public void ExternalFieldReadsAllocateNothing()
    {
        var host = new Fixed();
        var value = GesValue.GesExternal(host);
        Assert.AreSame(host, value.ExternalValue);

        double Round() => value.ReadField("y")!.Value.AsNumber() + (((Fixed)value.ExternalValue!).Y);

        Assert.AreEqual(4d, Round());
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var round = 0; round < 1000; round++) Round();
        Assert.AreEqual(before, GC.GetAllocatedBytesForCurrentThread());
    }

    private sealed class Fixed : IGesExternalValue
    {
        public double Y => 2;

        public GesExternalTypeDefinition Definition { get; } = new("Fixed", [new GesExternalTypeFieldDefinition("x", GesBytecodeTypeKind.Float), new GesExternalTypeFieldDefinition("y", GesBytecodeTypeKind.Float)], []);

        public GesValue? GetField(string fieldName) => GesValue.GesFloat(2);
    }
}
