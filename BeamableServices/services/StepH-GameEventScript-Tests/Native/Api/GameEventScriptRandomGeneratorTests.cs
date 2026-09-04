using StepH.GameEventScript.Api;

namespace StepH_GameEventScript_Tests.Native.Api;

[TestClass]
public sealed class GameEventScriptRandomGeneratorTests
{
    [TestMethod]
    public void IntRangeLongSeedsKeepExistingIntSeedSequence()
    {
        var intSeed = GameEventScriptRandomGenerator.FromSeed(7);
        var longSeed = GameEventScriptRandomGenerator.FromSeed(7L);

        Assert.AreEqual(
            intSeed.NextInclusiveInteger(long.MinValue, long.MaxValue),
            longSeed.NextInclusiveInteger(long.MinValue, long.MaxValue));
        Assert.AreEqual(
            intSeed.NextFloat(-10, 10),
            longSeed.NextFloat(-10, 10));
    }

    [TestMethod]
    public void ScopedStreamRestoresItsParentState()
    {
        var scoped = GameEventScriptRandomGenerator.FromSeed(0L);
        var control = GameEventScriptRandomGenerator.FromSeed(0L);
        var expectedFirst = control.NextInclusiveInteger(long.MinValue, long.MaxValue);
        var expectedSecond = control.NextInclusiveInteger(long.MinValue, long.MaxValue);

        Assert.AreEqual(expectedFirst, scoped.NextInclusiveInteger(long.MinValue, long.MaxValue));
        Assert.IsTrue(scoped.Push(123L));
        _ = scoped.NextInclusiveInteger(long.MinValue, long.MaxValue);
        Assert.IsTrue(scoped.Pop());
        Assert.AreEqual(expectedSecond, scoped.NextInclusiveInteger(long.MinValue, long.MaxValue));
    }

    [TestMethod]
    public void SequenceInputIsCopiedAndFallsBackToThePortableGenerator()
    {
        var values = new[] { 0.25 };
        var random = GameEventScriptRandomGenerator.FromSequence(values);
        values[0] = 0.75;

        Assert.AreEqual(0.25, random.NextFloat(0, 1));
        var fallback = random.NextFloat(0, 1);
        Assert.IsTrue(fallback >= 0 && fallback <= 1);
    }

}
