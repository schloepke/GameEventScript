using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime.Values;

namespace StepH_GameEventScript_Tests.impl;

[TestClass]
public sealed class GameEventScriptValueTests
{
    [TestMethod]
    public void PrimitiveValuesExposeFastReadersAndUnits()
    {
        var boolean = GameEventScriptValueFactory.GesBoolean(true);
        var integer = GameEventScriptValueFactory.GesInteger(42);
        var meter = GameEventScriptValueFactory.GesFloat(12.5d, GameEventScriptBytecodeInstructionUnit.UnitMeter);
        var percentage = GameEventScriptValueFactory.GesPercentage(0.25d);

        Assert.AreEqual(GameEventScriptBytecodeTypeKind.Boolean, boolean.Kind);
        Assert.AreEqual(GameEventScriptBytecodeTypeKind.Integer, integer.Kind);
        Assert.AreEqual(GameEventScriptBytecodeTypeKind.Float, meter.Kind);
        Assert.AreEqual(GameEventScriptBytecodeTypeKind.Percentage, percentage.Kind);
        Assert.IsTrue(boolean.Boolean);
        Assert.AreEqual(42, integer.Integer);
        Assert.AreEqual(12.5d, meter.Number);
        Assert.AreEqual(GameEventScriptBytecodeInstructionUnit.UnitMeter, meter.Unit);
        Assert.AreEqual(0.25d, percentage.Number);
        Assert.IsTrue(integer.IsIntegerNumber);
        Assert.IsFalse(meter.IsIntegerNumber);
    }

    [TestMethod]
    public void VectorsExposeComponentsAndUnits()
    {
        var vector = GameEventScriptValueFactory.GesVector(3d, 4d, 5d, GameEventScriptBytecodeInstructionUnit.UnitMeter);

        Assert.AreEqual(GameEventScriptBytecodeTypeKind.Vector, vector.Kind);
        Assert.AreEqual(3d, vector.X);
        Assert.AreEqual(4d, vector.Y);
        Assert.AreEqual(5d, vector.Z);
        Assert.AreEqual(GameEventScriptBytecodeInstructionUnit.UnitMeter, vector.Unit);
        Assert.IsTrue(vector.Boolean);
    }

    [TestMethod]
    public void PointsExposeComponentsAndUnits()
    {
        var point = GameEventScriptValueFactory.GesPoint(1d, 2d, 3d, GameEventScriptBytecodeInstructionUnit.UnitSecond);

        Assert.AreEqual(GameEventScriptBytecodeTypeKind.Point, point.Kind);
        Assert.AreEqual(1d, point.X);
        Assert.AreEqual(2d, point.Y);
        Assert.AreEqual(3d, point.Z);
        Assert.AreEqual(GameEventScriptBytecodeInstructionUnit.UnitSecond, point.Unit);
        Assert.IsTrue(point.Boolean);
    }

    [TestMethod]
    public void TextAndNothingExposeBoundarySemantics()
    {
        var text = GameEventScriptValueFactory.GesText("hello");
        var tag = GameEventScriptValueFactory.GesTag("pi");
        var nan = GameEventScriptValueFactory.GesFloat(double.NaN);

        Assert.AreEqual(GameEventScriptBytecodeTypeKind.Text, text.Kind);
        Assert.AreEqual(GameEventScriptBytecodeTypeKind.Tag, tag.Kind);
        Assert.AreEqual("hello", text.Text);
        Assert.AreEqual("pi", tag.Text);
        Assert.IsFalse(tag.IsNumeric);
        Assert.AreEqual(GameEventScriptBytecodeTypeKind.Nothing, nan.Kind);
        Assert.IsTrue(nan.IsNothing);
        Assert.IsFalse(nan.HasValue);
    }

    [TestMethod]
    public void ListAndDiceExposeDataCopies()
    {
        var list = GameEventScriptValueFactory.GesList([
            GameEventScriptValueFactory.GesInteger(1),
            GameEventScriptValueFactory.GesText("two")
        ]);
        var dice = GameEventScriptValueFactory.GesDice([3, 6, 1]);

        Assert.AreEqual(GameEventScriptBytecodeTypeKind.List, list.Kind);
        Assert.AreEqual(2, list.Length);
        Assert.AreEqual(1, list.AsList()[0].Integer);
        Assert.AreEqual("two", list.AsList()[1].Text);
        Assert.AreEqual(GameEventScriptBytecodeTypeKind.Dice, dice.Kind);
        CollectionAssert.AreEqual(new[] { 6, 3, 1 }, dice.AsDice().ToArray());
    }

    [TestMethod]
    public void MapAndRecordExposeVisibleSortedData()
    {
        var map = GameEventScriptValueFactory.GesMap([
            new KeyValuePair<string, GameEventScriptValue>("z", GameEventScriptValueFactory.GesInteger(3)),
            new KeyValuePair<string, GameEventScriptValue>("a", GameEventScriptValueFactory.GesInteger(1))
        ]);
        var record = GameEventScriptValueFactory.GesRecord("unit", [
            new KeyValuePair<string, GameEventScriptValue>("hp", GameEventScriptValueFactory.GesInteger(10)),
            new KeyValuePair<string, GameEventScriptValue>("_hidden", GameEventScriptValueFactory.GesText("secret"))
        ]);

        Assert.AreEqual(GameEventScriptBytecodeTypeKind.Map, map.Kind);
        Assert.AreEqual(2, map.Length);
        var a = map.GetMapValue("a");
        Assert.IsNotNull(a);
        Assert.AreEqual(1, a.Integer);
        CollectionAssert.AreEqual(new[] { "a", "z" }, map.AsMap().Keys.ToArray());
        Assert.AreEqual(GameEventScriptBytecodeTypeKind.Custom, record.Kind);
        Assert.AreEqual(1, record.Length);
        Assert.AreEqual("unit", record.CustomTypeName);
        var hp = record.GetMapValue("hp");
        Assert.IsNotNull(hp);
        Assert.AreEqual(10, hp.Integer);
        Assert.IsFalse(record.AsMap().ContainsKey("_hidden"));
    }

    [TestMethod]
    public void RangesMessagesAndHandlersExposeStoredObjects()
    {
        var intRange = GameEventScriptValueFactory.GesRange(1, 5, 2);
        var floatRange = GameEventScriptValueFactory.GesRange(1.5d, 2.5d, 0.5d);
        var message = GameEventScriptMessage.Create("Ping", ("amount", GesValue.GesInteger(7)));
        var messageValue = GameEventScriptValueFactory.GesMessage(message);
        var signature = GameEventScriptMessageSignature.Create("Ping", ["amount"]);
        var handler = GameEventScriptValueFactory.GesHandler(signature);

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
