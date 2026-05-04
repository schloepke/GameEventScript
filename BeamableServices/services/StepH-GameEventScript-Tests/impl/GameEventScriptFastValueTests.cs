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
        var vector2 = GameEventScriptFastValue.FromVector2(3m, 4m, GameEventScriptDecimalUnit.Meter);
        var vector3 = GameEventScriptFastValue.FromGameEventScriptValue(GameEventScriptValueFactory.GesVector3(1m, 2m, 3m, GameEventScriptDecimalUnit.Second));

        Assert.IsFalse(vector2.IsReferenceBacked);
        Assert.IsFalse(vector3.IsReferenceBacked);
        Assert.AreEqual(GameEventScriptValueKind.Vector2, vector2.Kind);
        Assert.AreEqual(GameEventScriptValueKind.Vector3, vector3.Kind);
        Assert.AreEqual(3m, vector2.X);
        Assert.AreEqual(4m, vector2.Y);
        Assert.AreEqual(GameEventScriptDecimalUnit.Meter, vector2.Unit);
        Assert.AreEqual(1m, vector3.X);
        Assert.AreEqual(2m, vector3.Y);
        Assert.AreEqual(3m, vector3.Z);
        Assert.AreEqual(GameEventScriptDecimalUnit.Second, vector3.Unit);
        Assert.AreEqual(GameEventScriptValueFactory.GesVector2(3m, 4m, GameEventScriptDecimalUnit.Meter), vector2.ToGameEventScriptValue());
        Assert.AreEqual(GameEventScriptValueFactory.GesVector3(1m, 2m, 3m, GameEventScriptDecimalUnit.Second), vector3.ToGameEventScriptValue());
    }

    [TestMethod]
    public void PointsAreStoredWithoutReferenceBackingAndRoundTripWithUnits()
    {
        var point2 = GameEventScriptFastValue.FromPoint2(3m, 4m, GameEventScriptDecimalUnit.Meter);
        var point3 = GameEventScriptFastValue.FromGameEventScriptValue(GameEventScriptValueFactory.GesPoint3(1m, 2m, 3m, GameEventScriptDecimalUnit.Second));

        Assert.IsFalse(point2.IsReferenceBacked);
        Assert.IsFalse(point3.IsReferenceBacked);
        Assert.AreEqual(GameEventScriptValueKind.Point2, point2.Kind);
        Assert.AreEqual(GameEventScriptValueKind.Point3, point3.Kind);
        Assert.AreEqual(3m, point2.X);
        Assert.AreEqual(4m, point2.Y);
        Assert.AreEqual(GameEventScriptDecimalUnit.Meter, point2.Unit);
        Assert.AreEqual(1m, point3.X);
        Assert.AreEqual(2m, point3.Y);
        Assert.AreEqual(3m, point3.Z);
        Assert.AreEqual(GameEventScriptDecimalUnit.Second, point3.Unit);
        Assert.AreEqual(GameEventScriptValueFactory.GesPoint2(3m, 4m, GameEventScriptDecimalUnit.Meter), point2.ToGameEventScriptValue());
        Assert.AreEqual(GameEventScriptValueFactory.GesPoint3(1m, 2m, 3m, GameEventScriptDecimalUnit.Second), point3.ToGameEventScriptValue());
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
