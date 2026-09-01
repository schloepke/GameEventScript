using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Runtime.Values;

namespace StepH_GameEventScript_Tests.Runtime;

[TestClass]
public sealed class GameEventScriptRangeMathTests
{
    [TestMethod]
    public void IntegerRangesHandleInt64BoundariesWithoutWrapping()
    {
        Assert.AreEqual(1L, GameEventScriptRangeMath.GetLength(long.MaxValue, long.MaxValue, 1));
        Assert.AreEqual(long.MaxValue, GameEventScriptRangeMath.GetTerm(long.MaxValue, long.MaxValue, 1, 1));
        Assert.IsNull(GameEventScriptRangeMath.GetTerm(long.MaxValue, long.MaxValue, 1, 2));

        Assert.AreEqual(1L, GameEventScriptRangeMath.GetLength(long.MinValue, long.MinValue, -1));
        Assert.AreEqual(long.MinValue, GameEventScriptRangeMath.GetTerm(long.MinValue, long.MinValue, -1, 1));
        Assert.IsNull(GameEventScriptRangeMath.GetTerm(long.MinValue, long.MinValue, -1, 2));

        Assert.AreEqual(2L, GameEventScriptRangeMath.GetLength(long.MaxValue - 1, long.MaxValue, 1));
        Assert.AreEqual(long.MaxValue, GameEventScriptRangeMath.GetTerm(long.MaxValue - 1, long.MaxValue, 1, 2));
        Assert.AreEqual(2L, GameEventScriptRangeMath.GetLength(long.MinValue + 1, long.MinValue, -1));
        Assert.AreEqual(long.MinValue, GameEventScriptRangeMath.GetTerm(long.MinValue + 1, long.MinValue, -1, 2));
    }

    [TestMethod]
    public void RangeDirectionAndZeroStepProduceEmptyRanges()
    {
        Assert.AreEqual(0L, GameEventScriptRangeMath.GetLength(3, 1, 1));
        Assert.AreEqual(0L, GameEventScriptRangeMath.GetLength(1, 3, -1));
        Assert.AreEqual(0L, GameEventScriptRangeMath.GetLength(1, 1, 0));
        Assert.AreEqual(0L, GameEventScriptRangeMath.GetLength(3d, 1d, 1d));
        Assert.AreEqual(0L, GameEventScriptRangeMath.GetLength(1d, 3d, -1d));
        Assert.AreEqual(0L, GameEventScriptRangeMath.GetLength(1d, 1d, 0d));
    }

    [TestMethod]
    public void RangeIteratorsStopAtPrecomputedLengthAtNumericBoundaries()
    {
        var maxIterator = new GesIntegerRangeIterator(long.MaxValue, long.MaxValue, 1);
        Assert.AreEqual(long.MaxValue, maxIterator.Next().Value.IntegerValue);
        Assert.IsFalse(maxIterator.Next().HasValue);

        var minIterator = new GesIntegerRangeIterator(long.MinValue, long.MinValue, -1);
        Assert.AreEqual(long.MinValue, minIterator.Next().Value.IntegerValue);
        Assert.IsFalse(minIterator.Next().HasValue);

        var nonProgressingFloat = new GesFloatRangeIterator(1e308, 1e308, 1d);
        Assert.AreEqual(1e308, nonProgressingFloat.Next().Value.FloatValue);
        Assert.IsFalse(nonProgressingFloat.Next().HasValue);
    }
}
