using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Types;

namespace StepH_GameEventScript_Tests.impl;

[TestClass]
public sealed class GameEventScriptFastValueTests
{
    [TestMethod]
    public void PrimitiveValuesAreStoredWithoutReferenceBacking()
    {
        var boolean = GameEventScriptFastValue.FromBoolean(true);
        var integer = GameEventScriptFastValue.FromInteger(42);
        var meter = GameEventScriptFastValue.FromDecimal(12.5m, GameEventScriptDecimalUnit.Meter);
        var percentage = GameEventScriptFastValue.FromPercentage(0.25m);

        Assert.IsFalse(boolean.IsReferenceBacked);
        Assert.IsFalse(integer.IsReferenceBacked);
        Assert.IsFalse(meter.IsReferenceBacked);
        Assert.IsFalse(percentage.IsReferenceBacked);
        Assert.AreEqual(GameEventScriptValueKind.Boolean, boolean.Kind);
        Assert.AreEqual(GameEventScriptValueKind.Integer, integer.Kind);
        Assert.AreEqual(GameEventScriptValueKind.Decimal, meter.Kind);
        Assert.AreEqual(GameEventScriptValueKind.Percentage, percentage.Kind);
        Assert.IsTrue(boolean.Boolean);
        Assert.AreEqual(42, integer.Integer);
        Assert.AreEqual(12.5m, meter.Number);
        Assert.AreEqual(GameEventScriptDecimalUnit.Meter, meter.Unit);
        Assert.AreEqual(0.25m, percentage.Number);
    }

    [TestMethod]
    public void VectorsAreStoredWithoutReferenceBackingAndRoundTripWithUnits()
    {
        var vector = GameEventScriptFastValue.FromVector(3m, 4m, 0m, GameEventScriptDecimalUnit.Meter);
        var vectorWithZ = GameEventScriptFastValue.FromGameEventScriptValue(GameEventScriptValueFactory.GesVector(1m, 2m, 3m, GameEventScriptDecimalUnit.Second));

        Assert.IsFalse(vector.IsReferenceBacked);
        Assert.IsFalse(vectorWithZ.IsReferenceBacked);
        Assert.AreEqual(GameEventScriptValueKind.Vector, vector.Kind);
        Assert.AreEqual(GameEventScriptValueKind.Vector, vectorWithZ.Kind);
        Assert.AreEqual(3m, vector.X);
        Assert.AreEqual(4m, vector.Y);
        Assert.AreEqual(0m, vector.Z);
        Assert.AreEqual(GameEventScriptDecimalUnit.Meter, vector.Unit);
        Assert.AreEqual(1m, vectorWithZ.X);
        Assert.AreEqual(2m, vectorWithZ.Y);
        Assert.AreEqual(3m, vectorWithZ.Z);
        Assert.AreEqual(GameEventScriptDecimalUnit.Second, vectorWithZ.Unit);
        Assert.AreEqual(GameEventScriptValueFactory.GesVector(3m, 4m, 0m, GameEventScriptDecimalUnit.Meter), vector.ToGameEventScriptValue());
        Assert.AreEqual(GameEventScriptValueFactory.GesVector(1m, 2m, 3m, GameEventScriptDecimalUnit.Second), vectorWithZ.ToGameEventScriptValue());
    }

    [TestMethod]
    public void PointsAreStoredWithoutReferenceBackingAndRoundTripWithUnits()
    {
        var point = GameEventScriptFastValue.FromPoint(3m, 4m, 0m, GameEventScriptDecimalUnit.Meter);
        var pointWithZ = GameEventScriptFastValue.FromGameEventScriptValue(GameEventScriptValueFactory.GesPoint(1m, 2m, 3m, GameEventScriptDecimalUnit.Second));

        Assert.IsFalse(point.IsReferenceBacked);
        Assert.IsFalse(pointWithZ.IsReferenceBacked);
        Assert.AreEqual(GameEventScriptValueKind.Point, point.Kind);
        Assert.AreEqual(GameEventScriptValueKind.Point, pointWithZ.Kind);
        Assert.AreEqual(3m, point.X);
        Assert.AreEqual(4m, point.Y);
        Assert.AreEqual(0m, point.Z);
        Assert.AreEqual(GameEventScriptDecimalUnit.Meter, point.Unit);
        Assert.AreEqual(1m, pointWithZ.X);
        Assert.AreEqual(2m, pointWithZ.Y);
        Assert.AreEqual(3m, pointWithZ.Z);
        Assert.AreEqual(GameEventScriptDecimalUnit.Second, pointWithZ.Unit);
        Assert.AreEqual(GameEventScriptValueFactory.GesPoint(3m, 4m, 0m, GameEventScriptDecimalUnit.Meter), point.ToGameEventScriptValue());
        Assert.AreEqual(GameEventScriptValueFactory.GesPoint(1m, 2m, 3m, GameEventScriptDecimalUnit.Second), pointWithZ.ToGameEventScriptValue());
    }

    [TestMethod]
    public void ReferenceValuesRemainReferenceBacked()
    {
        var text = GameEventScriptFastValue.FromText("hello");
        var nan = GameEventScriptFastValue.FromGameEventScriptValue(GameEventScriptValueFactory.GesDecimalNaN());
        var list = GameEventScriptFastValue.FromGameEventScriptValue(GameEventScriptValueFactory.GesList([
            GameEventScriptValueFactory.GesInteger(1),
            GameEventScriptValueFactory.GesInteger(2)
        ]));

        Assert.IsTrue(text.IsReferenceBacked);
        Assert.IsTrue(nan.IsReferenceBacked);
        Assert.IsTrue(list.IsReferenceBacked);
        Assert.AreEqual(GameEventScriptValueKind.Text, text.Kind);
        Assert.AreEqual(GameEventScriptValueKind.Decimal, nan.Kind);
        Assert.AreEqual(GameEventScriptValueKind.List, list.Kind);
        Assert.AreEqual("hello", text.Text);
        Assert.IsTrue(nan.ToGameEventScriptValue().IsNaN());
        Assert.HasCount(2, list.ToGameEventScriptValue().AsList());
    }
}
