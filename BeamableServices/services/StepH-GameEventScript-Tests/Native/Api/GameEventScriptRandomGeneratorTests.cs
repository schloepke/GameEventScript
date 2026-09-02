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

}
