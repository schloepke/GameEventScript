using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Types;

namespace StepH_GameEventScript_Tests.impl;

[TestClass]
public sealed class GameEventScriptFastValueTests
{
    [TestMethod]
    public void PrimitiveValuesAreStoredWithoutReferenceBacking()
    {
        var boolean = GseFastValue.FromBoolean(true);
        var integer = GseFastValue.FromInteger(42);
        var meter = GseFastValue.FromDecimal(12.5m, GseDecimalUnit.Meter);
        var percentage = GseFastValue.FromPercentage(0.25m);

        Assert.IsFalse(boolean.IsReferenceBacked);
        Assert.IsFalse(integer.IsReferenceBacked);
        Assert.IsFalse(meter.IsReferenceBacked);
        Assert.IsFalse(percentage.IsReferenceBacked);
        Assert.AreEqual(GseValueKind.Boolean, boolean.Kind);
        Assert.AreEqual(GseValueKind.Integer, integer.Kind);
        Assert.AreEqual(GseValueKind.Decimal, meter.Kind);
        Assert.AreEqual(GseValueKind.Percentage, percentage.Kind);
        Assert.IsTrue(boolean.Boolean);
        Assert.AreEqual(42, integer.Integer);
        Assert.AreEqual(12.5m, meter.Number);
        Assert.AreEqual(GseDecimalUnit.Meter, meter.Unit);
        Assert.AreEqual(0.25m, percentage.Number);
    }

    [TestMethod]
    public void VectorsAreStoredWithoutReferenceBackingAndRoundTripWithUnits()
    {
        var vector2 = GseFastValue.FromVector2(3m, 4m, GseDecimalUnit.Meter);
        var vector3 = GseFastValue.FromGseValue(GseValueFactory.Vector3(1m, 2m, 3m, GseDecimalUnit.Second));

        Assert.IsFalse(vector2.IsReferenceBacked);
        Assert.IsFalse(vector3.IsReferenceBacked);
        Assert.AreEqual(GseValueKind.Vector2, vector2.Kind);
        Assert.AreEqual(GseValueKind.Vector3, vector3.Kind);
        Assert.AreEqual(3m, vector2.X);
        Assert.AreEqual(4m, vector2.Y);
        Assert.AreEqual(GseDecimalUnit.Meter, vector2.Unit);
        Assert.AreEqual(1m, vector3.X);
        Assert.AreEqual(2m, vector3.Y);
        Assert.AreEqual(3m, vector3.Z);
        Assert.AreEqual(GseDecimalUnit.Second, vector3.Unit);
        Assert.AreEqual(GseValueFactory.Vector2(3m, 4m, GseDecimalUnit.Meter), vector2.ToGseValue());
        Assert.AreEqual(GseValueFactory.Vector3(1m, 2m, 3m, GseDecimalUnit.Second), vector3.ToGseValue());
    }

    [TestMethod]
    public void ReferenceValuesRemainReferenceBacked()
    {
        var text = GseFastValue.FromText("hello");
        var nan = GseFastValue.FromGseValue(GseValueFactory.DecimalNaN());
        var list = GseFastValue.FromGseValue(GseValueFactory.List([
            GseValueFactory.Integer(1),
            GseValueFactory.Integer(2)
        ]));

        Assert.IsTrue(text.IsReferenceBacked);
        Assert.IsTrue(nan.IsReferenceBacked);
        Assert.IsTrue(list.IsReferenceBacked);
        Assert.AreEqual(GseValueKind.Text, text.Kind);
        Assert.AreEqual(GseValueKind.Decimal, nan.Kind);
        Assert.AreEqual(GseValueKind.List, list.Kind);
        Assert.AreEqual("hello", text.Text);
        Assert.IsTrue(nan.ToGseValue().IsNaN());
        Assert.HasCount(2, list.ToGseValue().AsList());
    }
}
