// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.CSharpBridge;
using GameEventScript.Runtime.Values;

namespace GameEventScript.Tests.Native.CSharpBridge;

[TestClass]
public sealed class GameEventScriptCSharpValueAdapterTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void DictionaryFactoriesPreserveScalarKeyOrderAndSnapshotEntries(bool record)
    {
        var entries = new Dictionary<string, GesValue>
        {
            ["\U00010000"] = GesValue.GesInteger(3),
            ["\uE000"] = GesValue.GesInteger(2),
            ["a"] = GesValue.GesInteger(1)
        };
        var value = record ? GameEventScriptCSharpValue.GesRecord("Sample", entries) : GameEventScriptCSharpValue.GesMap(entries);
        var reordered = new Dictionary<string, GesValue>
        {
            ["a"] = GesValue.GesInteger(1),
            ["\uE000"] = GesValue.GesInteger(2),
            ["\U00010000"] = GesValue.GesInteger(3)
        };
        Assert.AreEqual(value, record ? GameEventScriptCSharpValue.GesRecord("Sample", reordered) : GameEventScriptCSharpValue.GesMap(reordered));

        entries["a"] = GesValue.GesInteger(99);
        entries.Clear();
        var map = value.AsMap()!;
        Assert.AreEqual(3, map.Length);
        Assert.AreEqual("a", map.KeyAt(0));
        Assert.AreEqual("\uE000", map.KeyAt(1));
        Assert.AreEqual("\U00010000", map.KeyAt(2));
        for (var index = 0; index < map.Length; index++) Assert.AreEqual(GesValue.GesInteger(index + 1), map.ValueAt(index));
        if (record) Assert.AreEqual("Sample", value.CustomTypeName);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void DictionaryFactoriesAcceptNullAndEmptyInputs(bool record, bool nullInput)
    {
        Dictionary<string, GesValue>? entries = nullInput ? null : new Dictionary<string, GesValue>();
        var value = record ? GameEventScriptCSharpValue.GesRecord("Sample", entries) : GameEventScriptCSharpValue.GesMap(entries);
        var expected = record ? GesValue.GesRecord("Sample", [], []) : GesValue.GesMap([], []);

        Assert.AreEqual(expected, value);
        Assert.AreEqual(0, value.AsMap()!.Length);
    }
}
