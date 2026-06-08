using StepH.GameEventScript.Api;

namespace StepH_GameEventScript_Tests.Api;

[TestClass]
public sealed class GameEventScriptRandomGeneratorTests
{
    [TestMethod]
    public void LongSeedsUseFullSeedWidth()
    {
        var lower32Seed = GameEventScriptRandomGenerator.FromSeed(1L);
        var upperBitSeed = GameEventScriptRandomGenerator.FromSeed(0x1_0000_0001L);

        var lower32Value = lower32Seed.NextInclusiveInteger(long.MinValue, long.MaxValue);
        var upperBitValue = upperBitSeed.NextInclusiveInteger(long.MinValue, long.MaxValue);

        Assert.AreNotEqual(lower32Value, upperBitValue);
    }

    [TestMethod]
    public void IntRangeLongSeedsKeepExistingIntSeedSequence()
    {
        var intSeed = GameEventScriptRandomGenerator.FromSeed(7);
        var longSeed = GameEventScriptRandomGenerator.FromSeed(7L);

        Assert.AreEqual(
            intSeed.NextInclusiveInteger(long.MinValue, long.MaxValue),
            longSeed.NextInclusiveInteger(long.MinValue, long.MaxValue));
        Assert.AreEqual(
            intSeed.NextInclusiveFloat(-10, 10),
            longSeed.NextInclusiveFloat(-10, 10));
    }
}
