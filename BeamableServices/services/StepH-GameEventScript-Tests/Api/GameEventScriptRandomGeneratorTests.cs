using StepH.GameEventScript.Api;

namespace StepH_GameEventScript_Tests.Api;

[TestClass]
public sealed class GameEventScriptRandomGeneratorTests
{
    [TestMethod]
    public void SeededGeneratorMatchesPortableRawKnownAnswerVectors()
    {
        AssertRawSequence(0L,
        [
            0x99EC5F36CB75F2B4UL, 0xBF6E1F784956452AUL,
            0x1A5F849D4933E6E0UL, 0x6AA594F1262D2D2CUL,
            0xBBA5AD4A1F842E59UL, 0xFFEF8375D9EBCACAUL,
            0x6C160DEED2F54C98UL, 0x8920AD648FC30A3FUL
        ]);
        AssertRawSequence(1L,
        [
            0xB3F2AF6D0FC710C5UL, 0x853B559647364CEAUL,
            0x92F89756082A4514UL, 0x642E1C7BC266A3A7UL,
            0xB27A48E29A233673UL, 0x24C123126FFDA722UL,
            0x123004EF8DF510E6UL, 0x61954DCC47B1E89DUL
        ]);
        AssertRawSequence(-1L,
        [
            0x8F5520D52A7EAD08UL, 0xC476A018CAA1802DUL,
            0x81DE31C0D260469EUL, 0xBF658D7E065F3C2FUL,
            0x913593FDA1BCA32AUL, 0xBB535E93941BA525UL,
            0x5ECDA415C3C6DFDEUL, 0xC487398FC9DE9AE2UL
        ]);
    }

    [TestMethod]
    public void SeededGeneratorMatchesPortableBoundedIntegerVector()
    {
        var random = GameEventScriptRandomGenerator.FromSeed(0L);
        long[] expected = [16, -95, -9, -45, -76, 43, -62, -21];

        foreach (var value in expected)
        {
            Assert.AreEqual(value, random.NextInclusiveInteger(-100, 100));
        }
    }

    [TestMethod]
    public void SeededGeneratorMatchesPortableBinary64Vector()
    {
        var random = GameEventScriptRandomGenerator.FromSeed(0L);
        ulong[] expectedBits =
        [
            0x3FE33D8BE6D96EBEUL,
            0x3FE7EDC3EF092AC8UL,
            0x3FBA5F849D4933E0UL,
            0x3FDAA9653C498B4AUL,
            0x3FE774B5A943F085UL
        ];

        foreach (var expected in expectedBits)
        {
            var actual = random.NextInclusiveFloat(0d, 1d);
            Assert.AreEqual(expected, unchecked((ulong)BitConverter.DoubleToInt64Bits(actual)));
        }
    }

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

    private static void AssertRawSequence(long seed, IReadOnlyList<ulong> expected)
    {
        var random = GameEventScriptRandomGenerator.FromSeed(seed);
        foreach (var expectedRaw in expected)
        {
            var signed = random.NextInclusiveInteger(long.MinValue, long.MaxValue);
            var actualRaw = unchecked((ulong)(signed - long.MinValue));
            Assert.AreEqual(expectedRaw, actualRaw);
        }
    }
}
