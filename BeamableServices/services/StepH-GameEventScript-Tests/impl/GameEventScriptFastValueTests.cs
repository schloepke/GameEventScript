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
        var meter = GameEventScriptFastValue.FromFloat(12.5d, GameEventScriptNumericUnit.Meter);
        var percentage = GameEventScriptFastValue.FromPercentage(0.25d);

        Assert.IsFalse(boolean.IsReferenceBacked);
        Assert.IsFalse(integer.IsReferenceBacked);
        Assert.IsFalse(meter.IsReferenceBacked);
        Assert.IsFalse(percentage.IsReferenceBacked);
        Assert.AreEqual(GameEventScriptValueKind.Boolean, boolean.Kind);
        Assert.AreEqual(GameEventScriptValueKind.Integer, integer.Kind);
        Assert.AreEqual(GameEventScriptValueKind.Float, meter.Kind);
        Assert.AreEqual(GameEventScriptValueKind.Percentage, percentage.Kind);
        Assert.IsTrue(boolean.Boolean);
        Assert.AreEqual(42, integer.Integer);
        Assert.AreEqual(12.5d, meter.Number);
        Assert.AreEqual(GameEventScriptNumericUnit.Meter, meter.Unit);
        Assert.AreEqual(0.25d, percentage.Number);
    }

    [TestMethod]
    public void VectorsAreStoredWithoutReferenceBackingAndRoundTripWithUnits()
    {
        var vector = GameEventScriptFastValue.FromVector(3d, 4d, 0d, GameEventScriptNumericUnit.Meter);
        var vectorWithZ = GameEventScriptFastValue.FromGameEventScriptValue(GameEventScriptValueFactory.GesVector(1d, 2d, 3d, GameEventScriptNumericUnit.Second));

        Assert.IsFalse(vector.IsReferenceBacked);
        Assert.IsFalse(vectorWithZ.IsReferenceBacked);
        Assert.AreEqual(GameEventScriptValueKind.Vector, vector.Kind);
        Assert.AreEqual(GameEventScriptValueKind.Vector, vectorWithZ.Kind);
        Assert.AreEqual(3d, vector.X);
        Assert.AreEqual(4d, vector.Y);
        Assert.AreEqual(0d, vector.Z);
        Assert.AreEqual(GameEventScriptNumericUnit.Meter, vector.Unit);
        Assert.AreEqual(1d, vectorWithZ.X);
        Assert.AreEqual(2d, vectorWithZ.Y);
        Assert.AreEqual(3d, vectorWithZ.Z);
        Assert.AreEqual(GameEventScriptNumericUnit.Second, vectorWithZ.Unit);
        Assert.AreEqual(GameEventScriptValueFactory.GesVector(3d, 4d, 0d, GameEventScriptNumericUnit.Meter), vector.ToGameEventScriptValue());
        Assert.AreEqual(GameEventScriptValueFactory.GesVector(1d, 2d, 3d, GameEventScriptNumericUnit.Second), vectorWithZ.ToGameEventScriptValue());
    }

    [TestMethod]
    public void PointsAreStoredWithoutReferenceBackingAndRoundTripWithUnits()
    {
        var point = GameEventScriptFastValue.FromPoint(3d, 4d, 0d, GameEventScriptNumericUnit.Meter);
        var pointWithZ = GameEventScriptFastValue.FromGameEventScriptValue(GameEventScriptValueFactory.GesPoint(1d, 2d, 3d, GameEventScriptNumericUnit.Second));

        Assert.IsFalse(point.IsReferenceBacked);
        Assert.IsFalse(pointWithZ.IsReferenceBacked);
        Assert.AreEqual(GameEventScriptValueKind.Point, point.Kind);
        Assert.AreEqual(GameEventScriptValueKind.Point, pointWithZ.Kind);
        Assert.AreEqual(3d, point.X);
        Assert.AreEqual(4d, point.Y);
        Assert.AreEqual(0d, point.Z);
        Assert.AreEqual(GameEventScriptNumericUnit.Meter, point.Unit);
        Assert.AreEqual(1d, pointWithZ.X);
        Assert.AreEqual(2d, pointWithZ.Y);
        Assert.AreEqual(3d, pointWithZ.Z);
        Assert.AreEqual(GameEventScriptNumericUnit.Second, pointWithZ.Unit);
        Assert.AreEqual(GameEventScriptValueFactory.GesPoint(3d, 4d, 0d, GameEventScriptNumericUnit.Meter), point.ToGameEventScriptValue());
        Assert.AreEqual(GameEventScriptValueFactory.GesPoint(1d, 2d, 3d, GameEventScriptNumericUnit.Second), pointWithZ.ToGameEventScriptValue());
    }

    [TestMethod]
    public void ReferenceValuesRemainReferenceBacked()
    {
        var text = GameEventScriptFastValue.FromText("hello");
        var nan = GameEventScriptFastValue.FromGameEventScriptValue(GameEventScriptValueFactory.GesFloatNaN());
        var list = GameEventScriptFastValue.FromGameEventScriptValue(GameEventScriptValueFactory.GesList([
            GameEventScriptValueFactory.GesInteger(1),
            GameEventScriptValueFactory.GesInteger(2)
        ]));

        Assert.IsTrue(text.IsReferenceBacked);
        Assert.IsTrue(nan.IsReferenceBacked);
        Assert.IsTrue(list.IsReferenceBacked);
        Assert.AreEqual(GameEventScriptValueKind.Text, text.Kind);
        Assert.AreEqual(GameEventScriptValueKind.Float, nan.Kind);
        Assert.AreEqual(GameEventScriptValueKind.List, list.Kind);
        Assert.AreEqual("hello", text.Text);
        Assert.IsTrue(nan.ToGameEventScriptValue().IsNaN());
        Assert.HasCount(2, list.ToGameEventScriptValue().AsList());
    }
}
