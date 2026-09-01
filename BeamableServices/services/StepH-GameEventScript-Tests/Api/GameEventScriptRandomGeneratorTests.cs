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
        AssertRawSequence(long.MinValue,
        [
            0xD01BFA9B44A998C3UL, 0x797C6B72FF690D62UL,
            0x4576AF98398380B1UL, 0xE5CE401830AAA16CUL,
            0xA5EECCC1D5D5EE1AUL, 0xCD43606B62171D67UL,
            0x4205C135C5E32535UL, 0x6D11E27BBF34F857UL
        ]);
        AssertRawSequence(long.MaxValue,
        [
            0x0E1C2B4B82E8C0C5UL, 0x19167A27A6E0D81BUL,
            0x7B5F1A55D35896BDUL, 0x0D19F02BF9005C90UL,
            0x0EEE111B5F85ACA0UL, 0xBB969C534267CF4FUL,
            0xBAEC81932902A56EUL, 0x134E81D9C55B497CUL
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
            var actual = random.NextFloat(0d, 1d);
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
            intSeed.NextFloat(-10, 10),
            longSeed.NextFloat(-10, 10));
    }

    [TestMethod]
    public void ReversedBoundsProduceTheOrderedBoundSequence()
    {
        var orderedInteger = GameEventScriptRandomGenerator.FromSeed(0L);
        var reversedInteger = GameEventScriptRandomGenerator.FromSeed(0L);
        Assert.AreEqual(
            orderedInteger.NextInclusiveInteger(-100, 100),
            reversedInteger.NextInclusiveInteger(100, -100));

        var orderedFloat = GameEventScriptRandomGenerator.FromSeed(0L);
        var reversedFloat = GameEventScriptRandomGenerator.FromSeed(0L);
        Assert.AreEqual(
            orderedFloat.NextFloat(-10d, 20d),
            reversedFloat.NextFloat(20d, -10d));
    }

    [TestMethod]
    public void EqualAndNaNBoundsDoNotConsumeTheSeededStream()
    {
        var expected = GameEventScriptRandomGenerator.FromSeed(0L)
            .NextInclusiveInteger(long.MinValue, long.MaxValue);
        var actual = GameEventScriptRandomGenerator.FromSeed(0L);

        Assert.AreEqual(7L, actual.NextInclusiveInteger(7L, 7L));
        Assert.AreEqual(3.5d, actual.NextFloat(3.5d, 3.5d));
        Assert.IsTrue(double.IsNaN(actual.NextFloat(double.NaN, 1d)));
        Assert.AreEqual(expected, actual.NextInclusiveInteger(long.MinValue, long.MaxValue));
    }

    [TestMethod]
    public void EqualAndNaNBoundsDoNotConsumeTheTestSequence()
    {
        var random = GameEventScriptRandomGenerator.FromSequence(17d, 0.75d);

        Assert.AreEqual(7L, random.NextInclusiveInteger(7L, 7L));
        Assert.AreEqual(3.5d, random.NextFloat(3.5d, 3.5d));
        Assert.IsTrue(double.IsNaN(random.NextFloat(double.NaN, 1d)));
        Assert.AreEqual(17L, random.NextInclusiveInteger(0L, 100L));
        Assert.AreEqual(0.75d, random.NextFloat(0d, 1d));
    }

    [TestMethod]
    public void Binary64ScalingMayRoundToTheUpperBound()
    {
        var random = GameEventScriptRandomGenerator.FromSeed(0L);
        var upperBound = Math.BitIncrement(1d);

        Assert.AreEqual(upperBound, random.NextFloat(1d, upperBound));
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
