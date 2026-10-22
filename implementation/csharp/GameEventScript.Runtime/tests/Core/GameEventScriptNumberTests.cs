// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using GameEventScript.Runtime;

namespace GameEventScript.Tests.Native.Core;

[TestClass]
public sealed class GameEventScriptNumberTests
{
    [TestMethod]
    public void CanonicalFloatTextIsShortestRoundTripAndNormalizesSyntax()
    {
        var values = new[] { 0d, -0d, 0.1d, Math.PI, 1e20d, double.Epsilon, double.MaxValue };
        foreach (var value in values)
        {
            var text = GesNumber.FormatCanonicalFloat(value);
            Assert.AreEqual(
                BitConverter.DoubleToInt64Bits(value == 0d ? 0d : value),
                BitConverter.DoubleToInt64Bits(double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture)),
                text);
            Assert.DoesNotContain("E", text);
            Assert.DoesNotContain("e+", text);
        }

        Assert.AreEqual("0", GesNumber.FormatCanonicalFloat(-0d));
        Assert.AreEqual("1e20", GesNumber.FormatCanonicalFloat(1e20d));
        Assert.AreEqual("Infinity", GesNumber.FormatCanonicalFloat(double.PositiveInfinity));
        Assert.AreEqual("-Infinity", GesNumber.FormatCanonicalFloat(double.NegativeInfinity));
        Assert.AreEqual("NaN", GesNumber.FormatCanonicalFloat(double.NaN));
    }

    [TestMethod]
    public void UlpComparisonHandlesSignsZerosAndSpecialValues()
    {
        Assert.IsTrue(GesNumber.EqualsWithinUlps(1d, double.BitIncrement(1d), 1));
        Assert.IsFalse(GesNumber.EqualsWithinUlps(1d, double.BitIncrement(double.BitIncrement(1d)), 1));
        Assert.IsTrue(GesNumber.EqualsWithinUlps(0d, -0d, 0));
        Assert.IsTrue(GesNumber.EqualsWithinUlps(double.PositiveInfinity, double.PositiveInfinity, 0));
        Assert.IsFalse(GesNumber.EqualsWithinUlps(double.PositiveInfinity, double.NegativeInfinity, ulong.MaxValue));
        Assert.IsFalse(GesNumber.EqualsWithinUlps(double.NaN, double.NaN, ulong.MaxValue));
    }

}
