using System.Globalization;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Runtime.Values;

namespace StepH_GameEventScript_Tests.impl;

[TestClass]
public sealed class GameEventScriptNumberTests
{
    [TestMethod]
    public void SignedIntegerOperationsDetectOverflowWithoutClrCheckedContext()
    {
        Assert.AreEqual(long.MaxValue, GameEventScriptNumber.AddExact(long.MaxValue - 1, 1));
        Assert.IsNull(GameEventScriptNumber.AddExact(long.MaxValue, 1));
        Assert.AreEqual(long.MinValue, GameEventScriptNumber.SubtractExact(long.MinValue + 1, 1));
        Assert.IsNull(GameEventScriptNumber.SubtractExact(long.MinValue, 1));
        Assert.AreEqual(9_007_199_254_740_993L, GameEventScriptNumber.MultiplyExact(9_007_199_254_740_993L, 1));
        Assert.IsNull(GameEventScriptNumber.MultiplyExact(long.MaxValue, 2));
    }

    [TestMethod]
    public void IntegerConversionUsesExplicitExclusiveUpperBoundaryAndSaturation()
    {
        Assert.IsFalse(GameEventScriptNumber.CanRepresentAsInteger(GameEventScriptNumber.Int64UpperExclusive));
        Assert.IsTrue(GameEventScriptNumber.CanRepresentAsInteger(GameEventScriptNumber.Int64LowerInclusive));
        Assert.AreEqual(long.MaxValue, GameEventScriptNumber.ToIntegerSaturated(GameEventScriptNumber.Int64UpperExclusive));
        Assert.AreEqual(long.MinValue, GameEventScriptNumber.ToIntegerSaturated(double.NegativeInfinity));
        Assert.AreEqual(long.MaxValue, GameEventScriptNumber.ToIntegerSaturated(double.PositiveInfinity));
        Assert.AreEqual(0L, GameEventScriptNumber.ToIntegerSaturated(double.NaN));
        Assert.AreEqual(-12L, GameEventScriptNumber.ToIntegerSaturated(-12.9d));
    }

    [TestMethod]
    public void DivisionModuloAndRemainderHaveDefinedNegativeSemantics()
    {
        Assert.AreEqual(-3L, GameEventScriptNumber.FloorDivideExact(-7, 3));
        Assert.AreEqual(-3L, GameEventScriptNumber.FloorDivideExact(7, -3));
        Assert.AreEqual(2L, GameEventScriptNumber.FloorDivideExact(-7, -3));
        Assert.AreEqual(2L, GameEventScriptNumber.Modulo(-7, 3));
        Assert.AreEqual(-2L, GameEventScriptNumber.Modulo(7, -3));
        Assert.AreEqual(-1L, GameEventScriptNumber.Remainder(-7, 3));
        Assert.AreEqual(1L, GameEventScriptNumber.Remainder(7, -3));
    }

    [TestMethod]
    public void CanonicalFloatTextIsShortestRoundTripAndNormalizesSyntax()
    {
        var values = new[] { 0d, -0d, 0.1d, Math.PI, 1e20d, double.Epsilon, double.MaxValue };
        foreach (var value in values)
        {
            var text = GameEventScriptNumber.FormatCanonicalFloat(value);
            Assert.AreEqual(
                BitConverter.DoubleToInt64Bits(value == 0d ? 0d : value),
                BitConverter.DoubleToInt64Bits(double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture)),
                text);
            Assert.DoesNotContain("E", text);
            Assert.DoesNotContain("e+", text);
        }

        Assert.AreEqual("0", GameEventScriptNumber.FormatCanonicalFloat(-0d));
        Assert.AreEqual("1e20", GameEventScriptNumber.FormatCanonicalFloat(1e20d));
        Assert.AreEqual("Infinity", GameEventScriptNumber.FormatCanonicalFloat(double.PositiveInfinity));
        Assert.AreEqual("-Infinity", GameEventScriptNumber.FormatCanonicalFloat(double.NegativeInfinity));
        Assert.AreEqual("NaN", GameEventScriptNumber.FormatCanonicalFloat(double.NaN));
    }

    [TestMethod]
    public void UlpComparisonHandlesSignsZerosAndSpecialValues()
    {
        Assert.IsTrue(GameEventScriptNumber.EqualsWithinUlps(1d, double.BitIncrement(1d), 1));
        Assert.IsFalse(GameEventScriptNumber.EqualsWithinUlps(1d, double.BitIncrement(double.BitIncrement(1d)), 1));
        Assert.IsTrue(GameEventScriptNumber.EqualsWithinUlps(0d, -0d, 0));
        Assert.IsTrue(GameEventScriptNumber.EqualsWithinUlps(double.PositiveInfinity, double.PositiveInfinity, 0));
        Assert.IsFalse(GameEventScriptNumber.EqualsWithinUlps(double.PositiveInfinity, double.NegativeInfinity, ulong.MaxValue));
        Assert.IsFalse(GameEventScriptNumber.EqualsWithinUlps(double.NaN, double.NaN, ulong.MaxValue));
    }

    [TestMethod]
    public void ValueBoundariesCanonicalizeNegativeZeroAndNanComponents()
    {
        var negativeZero = GesValue.GesFloat(-0d);
        Assert.AreEqual(0L, negativeZero.AsInteger());
        Assert.AreEqual("0", negativeZero.AsText());
        Assert.IsTrue(GesValue.GesVector(double.NaN, 0d, 0d).IsNothing);
        Assert.IsTrue(GesValue.GesPoint(0d, double.NaN, 0d).IsNothing);
    }
}
