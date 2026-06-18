using StepH.GameEventScript.Api;

namespace StepH_GameEventScript_Tests.impl;

[TestClass]
public sealed class GameEventScriptBoxedValueTests
{
    [TestMethod]
    public void PrimitiveValuesExposeFastReadersAndUnits()
    {
        var boolean = GameEventScriptBoxedValue.FromBoolean(true);
        var integer = GameEventScriptBoxedValue.FromInteger(42);
        var meter = GameEventScriptBoxedValue.FromFloat(12.5d, GameEventScriptBytecodeInstructionUnit.UnitMeter);
        var percentage = GameEventScriptBoxedValue.FromPercentage(0.25d);

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
        var vector = GameEventScriptBoxedValue.FromVector(3d, 4d, 5d, GameEventScriptBytecodeInstructionUnit.UnitMeter);

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
        var point = GameEventScriptBoxedValue.FromPoint(1d, 2d, 3d, GameEventScriptBytecodeInstructionUnit.UnitSecond);

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
        var text = GameEventScriptBoxedValue.FromText("hello");
        var tag = GameEventScriptBoxedValue.FromTag("pi");
        var nan = GameEventScriptBoxedValue.FromFloat(double.NaN);

        Assert.AreEqual(GameEventScriptBytecodeTypeKind.Text, text.Kind);
        Assert.AreEqual(GameEventScriptBytecodeTypeKind.Tag, tag.Kind);
        Assert.AreEqual("hello", text.Text);
        Assert.AreEqual("pi", tag.Text);
        Assert.IsTrue(tag.IsNumeric);
        Assert.AreEqual(GameEventScriptBytecodeTypeKind.Nothing, nan.Kind);
        Assert.IsTrue(nan.IsNothing);
        Assert.IsFalse(nan.HasValue);
    }

    [TestMethod]
    public void ListAndDiceExposeDataCopies()
    {
        var list = GameEventScriptBoxedValue.FromList([
            GameEventScriptBoxedValue.FromInteger(1),
            GameEventScriptBoxedValue.FromText("two")
        ]);
        var dice = GameEventScriptBoxedValue.FromDice([3, 6, 1]);

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
        var map = GameEventScriptBoxedValue.FromMap([
            new KeyValuePair<string, GameEventScriptBoxedValue>("z", GameEventScriptBoxedValue.FromInteger(3)),
            new KeyValuePair<string, GameEventScriptBoxedValue>("a", GameEventScriptBoxedValue.FromInteger(1))
        ]);
        var record = GameEventScriptBoxedValue.FromRecord("unit", [
            new KeyValuePair<string, GameEventScriptBoxedValue>("hp", GameEventScriptBoxedValue.FromInteger(10)),
            new KeyValuePair<string, GameEventScriptBoxedValue>("_hidden", GameEventScriptBoxedValue.FromText("secret"))
        ]);

        Assert.AreEqual(GameEventScriptBytecodeTypeKind.Map, map.Kind);
        Assert.AreEqual(2, map.Length);
        Assert.IsTrue(map.TryGetMapValue("a", out var a));
        Assert.AreEqual(1, a.Integer);
        CollectionAssert.AreEqual(new[] { "a", "z" }, map.AsMap().Keys.ToArray());
        Assert.AreEqual(GameEventScriptBytecodeTypeKind.Custom, record.Kind);
        Assert.AreEqual(1, record.Length);
        Assert.IsTrue(record.TryGetCustomTypeName(out var typeName));
        Assert.AreEqual("unit", typeName);
        Assert.IsTrue(record.TryGetMapValue("hp", out var hp));
        Assert.AreEqual(10, hp.Integer);
        Assert.IsFalse(record.AsMap().ContainsKey("_hidden"));
    }

    [TestMethod]
    public void RangesMessagesAndHandlersExposeStoredObjects()
    {
        var intRange = GameEventScriptBoxedValue.FromIntegerRange(1, 5, 2);
        var floatRange = GameEventScriptBoxedValue.FromFloatRange(1.5d, 2.5d, 0.5d);
        var message = GameEventScriptMessage.Create("Ping", ("amount", GameEventScriptBoxedValue.FromInteger(7)));
        var messageValue = GameEventScriptBoxedValue.FromMessage(message);
        var signature = GameEventScriptMessageSignature.Create("Ping", ["amount"]);
        var handler = GameEventScriptBoxedValue.FromHandler(signature);

        Assert.IsTrue(intRange.TryGetIntegerRange(out var from, out var to, out var step));
        Assert.AreEqual(1, from);
        Assert.AreEqual(5, to);
        Assert.AreEqual(2, step);
        Assert.IsTrue(floatRange.TryGetFloatRange(out var fromFloat, out var toFloat, out var stepFloat));
        Assert.AreEqual(1.5d, fromFloat);
        Assert.AreEqual(2.5d, toFloat);
        Assert.AreEqual(0.5d, stepFloat);
        Assert.AreSame(message, messageValue.Message);
        Assert.AreSame(signature, handler.Handler);
    }
}
