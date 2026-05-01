using StepH.Flow.EventScript.Runtime;
using StepH.Flow.EventScript.Types;

namespace StepH_Flow_Tests.EventScript.impl;

[TestClass]
public sealed class EventScriptFastValueTests
{
    [TestMethod]
    public void PrimitiveValuesAreStoredWithoutReferenceBacking()
    {
        var boolean = EventScriptFastValue.FromBoolean(true);
        var integer = EventScriptFastValue.FromInteger(42);
        var meter = EventScriptFastValue.FromDecimal(12.5m, EventScriptDecimalUnit.Meter);
        var percentage = EventScriptFastValue.FromPercentage(0.25m);

        Assert.IsFalse(boolean.IsReferenceBacked);
        Assert.IsFalse(integer.IsReferenceBacked);
        Assert.IsFalse(meter.IsReferenceBacked);
        Assert.IsFalse(percentage.IsReferenceBacked);
        Assert.AreEqual(EventScriptValueKind.Boolean, boolean.Kind);
        Assert.AreEqual(EventScriptValueKind.Integer, integer.Kind);
        Assert.AreEqual(EventScriptValueKind.Decimal, meter.Kind);
        Assert.AreEqual(EventScriptValueKind.Percentage, percentage.Kind);
        Assert.IsTrue(boolean.Boolean);
        Assert.AreEqual(42, integer.Integer);
        Assert.AreEqual(12.5m, meter.Number);
        Assert.AreEqual(EventScriptDecimalUnit.Meter, meter.Unit);
        Assert.AreEqual(0.25m, percentage.Number);
    }

    [TestMethod]
    public void VectorsAreStoredWithoutReferenceBackingAndRoundTripWithUnits()
    {
        var vector2 = EventScriptFastValue.FromVector2(3m, 4m, EventScriptDecimalUnit.Meter);
        var vector3 = EventScriptFastValue.FromEventScriptValue(EventScriptValueFactory.Vector3(1m, 2m, 3m, EventScriptDecimalUnit.Second));

        Assert.IsFalse(vector2.IsReferenceBacked);
        Assert.IsFalse(vector3.IsReferenceBacked);
        Assert.AreEqual(EventScriptValueKind.Vector2, vector2.Kind);
        Assert.AreEqual(EventScriptValueKind.Vector3, vector3.Kind);
        Assert.AreEqual(3m, vector2.X);
        Assert.AreEqual(4m, vector2.Y);
        Assert.AreEqual(EventScriptDecimalUnit.Meter, vector2.Unit);
        Assert.AreEqual(1m, vector3.X);
        Assert.AreEqual(2m, vector3.Y);
        Assert.AreEqual(3m, vector3.Z);
        Assert.AreEqual(EventScriptDecimalUnit.Second, vector3.Unit);
        Assert.AreEqual(EventScriptValueFactory.Vector2(3m, 4m, EventScriptDecimalUnit.Meter), vector2.ToEventScriptValue());
        Assert.AreEqual(EventScriptValueFactory.Vector3(1m, 2m, 3m, EventScriptDecimalUnit.Second), vector3.ToEventScriptValue());
    }

    [TestMethod]
    public void ReferenceValuesRemainReferenceBacked()
    {
        var text = EventScriptFastValue.FromText("hello");
        var nan = EventScriptFastValue.FromEventScriptValue(EventScriptValueFactory.DecimalNaN());
        var list = EventScriptFastValue.FromEventScriptValue(EventScriptValueFactory.List([
            EventScriptValueFactory.Integer(1),
            EventScriptValueFactory.Integer(2)
        ]));

        Assert.IsTrue(text.IsReferenceBacked);
        Assert.IsTrue(nan.IsReferenceBacked);
        Assert.IsTrue(list.IsReferenceBacked);
        Assert.AreEqual(EventScriptValueKind.Text, text.Kind);
        Assert.AreEqual(EventScriptValueKind.Decimal, nan.Kind);
        Assert.AreEqual(EventScriptValueKind.List, list.Kind);
        Assert.AreEqual("hello", text.Text);
        Assert.IsTrue(nan.ToEventScriptValue().IsNaN());
        Assert.HasCount(2, list.ToEventScriptValue().AsList());
    }
}
