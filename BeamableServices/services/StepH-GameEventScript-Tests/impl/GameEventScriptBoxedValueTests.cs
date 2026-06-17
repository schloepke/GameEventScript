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
}
