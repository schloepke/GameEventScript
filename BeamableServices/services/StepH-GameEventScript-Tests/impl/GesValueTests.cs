using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime.Values;

namespace StepH_GameEventScript_Tests.impl;

[TestClass]
public sealed class GesValueTests
{
    [TestMethod]
    public void PrimitiveValuesExposeReadersAndUnits()
    {
        var boolean = GesValue.GesBoolean(true);
        var integer = GesValue.GesInteger(42);
        var meter = GesValue.GesFloat(12.5d, GameEventScriptBytecodeInstructionUnit.UnitMeter);
        var percentage = GesValue.GesPercentage(0.25d);

        Assert.AreEqual(GameEventScriptBytecodeTypeKind.Boolean, boolean.ValueKind);
        Assert.AreEqual(GameEventScriptBytecodeTypeKind.Integer, integer.ValueKind);
        Assert.AreEqual(GameEventScriptBytecodeTypeKind.Float, meter.ValueKind);
        Assert.AreEqual(GameEventScriptBytecodeTypeKind.Percentage, percentage.ValueKind);
        Assert.IsTrue(boolean.AsBoolean());
        Assert.AreEqual(42, integer.AsInteger());
        Assert.AreEqual(12.5d, meter.AsNumber());
        Assert.AreEqual(GameEventScriptBytecodeInstructionUnit.UnitMeter, meter.ValueUnit);
        Assert.AreEqual(0.25d, percentage.AsNumber());
        Assert.IsTrue(integer.ValueKind is GameEventScriptBytecodeTypeKind.Integer);
        Assert.IsFalse(meter.ValueKind is GameEventScriptBytecodeTypeKind.Integer);
    }

    [TestMethod]
    public void VectorsExposeComponentsAndUnits()
    {
        var vector = GesValue.GesVector(3d, 4d, 5d, GameEventScriptBytecodeInstructionUnit.UnitMeter);

        Assert.AreEqual(GameEventScriptBytecodeTypeKind.Vector, vector.ValueKind);
        Assert.AreEqual(3d, vector.X);
        Assert.AreEqual(4d, vector.Y);
        Assert.AreEqual(5d, vector.Z);
        Assert.AreEqual(GameEventScriptBytecodeInstructionUnit.UnitMeter, vector.ValueUnit);
        Assert.IsTrue(vector.AsBoolean());
    }

    [TestMethod]
    public void PointsExposeComponentsAndUnits()
    {
        var point = GesValue.GesPoint(1d, 2d, 3d, GameEventScriptBytecodeInstructionUnit.UnitSecond);

        Assert.AreEqual(GameEventScriptBytecodeTypeKind.Point, point.ValueKind);
        Assert.AreEqual(1d, point.X);
        Assert.AreEqual(2d, point.Y);
        Assert.AreEqual(3d, point.Z);
        Assert.AreEqual(GameEventScriptBytecodeInstructionUnit.UnitSecond, point.ValueUnit);
        Assert.IsTrue(point.AsBoolean());
    }

    [TestMethod]
    public void TextAndNothingExposeBoundarySemantics()
    {
        var text = GesValue.GesText("hello");
        var tag = GesValue.GesTag("pi");
        var nan = GesValue.GesFloat(double.NaN);

        Assert.AreEqual(GameEventScriptBytecodeTypeKind.Text, text.ValueKind);
        Assert.AreEqual(GameEventScriptBytecodeTypeKind.Tag, tag.ValueKind);
        Assert.AreEqual("hello", text.AsText());
        Assert.AreEqual("pi", tag.AsText());
        Assert.IsFalse(tag.IsNumeric);
        Assert.AreEqual(GameEventScriptBytecodeTypeKind.Nothing, nan.ValueKind);
        Assert.IsTrue(nan.IsNothing);
        Assert.IsFalse(nan.HasValue);
    }

    [TestMethod]
    public void ListAndDiceExposeDataCopies()
    {
        var list = GesValue.GesList([
            GesValue.GesInteger(1),
            GesValue.GesText("two")
        ]);
        var dice = GesValue.GesDice([3, 6, 1]);

        Assert.AreEqual(GameEventScriptBytecodeTypeKind.List, list.ValueKind);
        Assert.AreEqual(2, list.Length);
        Assert.AreEqual(1, list.AsList().GetAsInteger(0));
        Assert.AreEqual("two", list.AsList().GetAsText(1));
        Assert.AreEqual(GameEventScriptBytecodeTypeKind.Dice, dice.ValueKind);
        CollectionAssert.AreEqual(new[] { 6, 3, 1 }, dice.AsDice());
    }

    [TestMethod]
    public void MapAndRecordExposeVisibleSortedData()
    {
        var map = GesValue.GesMap(
            ["z", "a"],
            [GesValue.GesInteger(3), GesValue.GesInteger(1)]);
        var record = GesValue.GesRecord(
            "unit",
            ["hp", "_hidden"],
            [GesValue.GesInteger(10), GesValue.GesText("secret")]);

        Assert.AreEqual(GameEventScriptBytecodeTypeKind.Map, map.ValueKind);
        Assert.AreEqual(2, map.Length);
        var mapView = map.AsMap();
        Assert.IsNotNull(mapView);
        var a = mapView.Get("a");
        Assert.IsNotNull(a);
        Assert.AreEqual(1, a.Value.AsInteger());
        Assert.AreEqual("a", mapView.KeyAt(0));
        Assert.AreEqual("z", mapView.KeyAt(1));
        Assert.AreEqual(GameEventScriptBytecodeTypeKind.Custom, record.ValueKind);
        Assert.AreEqual(1, record.Length);
        Assert.AreEqual("unit", record.CustomTypeName);
        var recordView = record.AsMap();
        Assert.IsNotNull(recordView);
        var hp = recordView.Get("hp");
        Assert.IsNotNull(hp);
        Assert.AreEqual(10, hp.Value.AsInteger());
        Assert.IsFalse(recordView.ContainsKey("_hidden"));
    }

    [TestMethod]
    public void RangesMessagesAndHandlersExposeStoredObjects()
    {
        var intRange = GesValue.GesRange(1, 5, 2);
        var floatRange = GesValue.GesRange(1.5d, 2.5d, 0.5d);
        var message = GameEventScriptMessage.Create("Ping", ("amount", GesValue.GesInteger(7)));
        var messageValue = GesValue.GesMessage(message);
        var signature = GameEventScriptMessageSignature.Create("Ping", ["amount"]);
        var handler = GesValue.GesHandler(signature);

        Assert.IsNotNull(intRange.IntegerRange);
        Assert.AreEqual(1, intRange.IntegerRange.From);
        Assert.AreEqual(5, intRange.IntegerRange.To);
        Assert.AreEqual(2, intRange.IntegerRange.Step);
        Assert.IsNotNull(floatRange.FloatRange);
        Assert.AreEqual(1.5d, floatRange.FloatRange.From);
        Assert.AreEqual(2.5d, floatRange.FloatRange.To);
        Assert.AreEqual(0.5d, floatRange.FloatRange.Step);
        Assert.AreSame(message, messageValue.Message);
        Assert.AreSame(signature, handler.Handler);
    }
}
