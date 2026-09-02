using System.Globalization;
using StepH.GameEventScript.Runtime;

namespace StepH_GameEventScript_Tests.Native.Core;

[TestClass]
public sealed class GameEventScriptNumberTests
{
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

}
