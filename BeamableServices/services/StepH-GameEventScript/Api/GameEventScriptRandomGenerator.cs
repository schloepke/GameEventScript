using System;
using StepH.GameEventScript.Runtime;

namespace StepH.GameEventScript.Api;

/// <summary>
/// A sealed class that provides mechanisms for generating random numbers or values,
/// either through a pseudo-random number generator or a predefined sequence of values.
/// This class is useful for scenarios where deterministic or non-deterministic random
/// value generation is required, especially in game event scripts.
/// </summary>
public sealed class GameEventScriptRandomGenerator
{
    private struct SplitMix
    {
        private ulong _state;

        internal SplitMix(ulong seed)
        {
            _state = seed;
        }

        internal ulong NextUInt64()
        {
            unchecked
            {
                var value = _state += 0x9E3779B97F4A7C15UL;
                value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
                value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
                return value ^ (value >> 31);
            }
        }
    }

    /// <summary>
    /// Creates and returns a new instance of <c>GameEventScriptRandomGenerator</c> using
    /// a default portable random generator for generating random values.
    /// </summary>
    /// <returns>
    /// A new <c>GameEventScriptRandomGenerator</c> initialized with a default generator seed.
    /// </returns>
    public static GameEventScriptRandomGenerator Create() => new(CreateDefaultSeed());

    /// <summary>
    /// Creates and returns a new instance of <c>GameEventScriptRandomGenerator</c>
    /// initialized with the specified deterministic seed.
    /// </summary>
    /// <param name="seed">
    /// The seed value used for deterministic random value generation.
    /// </param>
    /// <returns>
    /// A new <c>GameEventScriptRandomGenerator</c> initialized with the specified seed.
    /// </returns>
    public static GameEventScriptRandomGenerator FromSeed(int seed) => FromSeed((long)seed);

    /// <summary>
    /// Creates and returns a new instance of <c>GameEventScriptRandomGenerator</c>
    /// initialized with the specified deterministic 64-bit seed.
    /// </summary>
    /// <param name="seed">
    /// The seed value used for deterministic random value generation.
    /// </param>
    /// <returns>
    /// A new <c>GameEventScriptRandomGenerator</c> initialized with the specified seed.
    /// </returns>
    public static GameEventScriptRandomGenerator FromSeed(long seed)
    {
        return new GameEventScriptRandomGenerator(seed);
    }

    /// <summary>
    /// Creates and returns a new instance of <c>GameEventScriptRandomGenerator</c>
    /// initialized to generate random values from a predefined sequence.
    /// </summary>
    /// <param name="values">
    /// An array of <c>double</c> values representing the predefined sequence
    /// of random numbers to be used by the generator.
    /// </param>
    /// <returns>
    /// A new <c>GameEventScriptRandomGenerator</c> initialized to use the specified
    /// sequence of random values.
    /// </returns>
    public static GameEventScriptRandomGenerator FromSequence(params double[] values) => new(values);

    /// <summary>
    /// Generates a random 64-bit signed integer between the specified minimum and maximum values, inclusive.
    /// This method ensures that the generated value falls within the provided range,
    /// swapping the bounds if the minimum value is greater than the maximum.
    /// </summary>
    /// <param name="minInclusive">
    /// The inclusive lower bound of the random number to generate.
    /// If greater than <paramref name="maxInclusive"/>, the values are swapped.
    /// </param>
    /// <param name="maxInclusive">
    /// The inclusive upper bound of the random number to generate.
    /// If less than <paramref name="minInclusive"/>, the values are swapped.
    /// </param>
    /// <returns>
    /// A random integer within the range defined by <paramref name="minInclusive"/> and <paramref name="maxInclusive"/>.
    /// </returns>
    public long NextInclusiveInteger(long minInclusive, long maxInclusive)
    {
        if (minInclusive > maxInclusive) (minInclusive, maxInclusive) = (maxInclusive, minInclusive);
        if (DequeueSequenceValue() is { } queuedValue)
        {
            return Math.Min(Math.Max(ToLongSaturated(queuedValue), minInclusive), maxInclusive);
        }

        if (minInclusive == maxInclusive) return minInclusive;
        var span = unchecked((ulong)(maxInclusive - minInclusive) + 1UL);
        var offset = NextUInt64Below(span);
        return unchecked(minInclusive + (long)offset);
    }

    /// <summary>
    /// Generates a random double value within the specified inclusive range.
    /// </summary>
    /// <param name="minInclusive">
    /// The minimum value of the range, inclusive.
    /// </param>
    /// <param name="maxInclusive">
    /// The maximum value of the range, inclusive.
    /// </param>
    /// <returns>
    /// A random double value that is greater than or equal to <paramref name="minInclusive"/>
    /// and less than or equal to <paramref name="maxInclusive"/>.
    /// </returns>
    public double NextInclusiveFloat(double minInclusive, double maxInclusive)
    {
        if (double.IsNaN(minInclusive) || double.IsNaN(maxInclusive)) return double.NaN;
        if (minInclusive > maxInclusive) (minInclusive, maxInclusive) = (maxInclusive, minInclusive);
        if (DequeueSequenceValue() is { } queuedValue)
        {
            return Math.Min(Math.Max(queuedValue, minInclusive), maxInclusive);
        }

        if (minInclusive == maxInclusive) return minInclusive;
        return minInclusive + (maxInclusive - minInclusive) * NextUnitDouble();
    }

    private GameEventScriptRandomGenerator(long seed)
    {
        Seed(seed);
    }

    private GameEventScriptRandomGenerator(double[] sequence)
    {
        _sequence = sequence;
        Seed(CreateDefaultSeed());
    }

    private readonly double[]? _sequence;
    private int _sequenceIndex;
    private ulong _s0;
    private ulong _s1;
    private ulong _s2;
    private ulong _s3;

    private double? DequeueSequenceValue()
    {
        if (_sequence == null || _sequenceIndex >= _sequence.Length)
        {
            return null;
        }

        return _sequence[_sequenceIndex++];
    }

    private static long ToLongSaturated(double value) => GameEventScriptNumber.ToIntegerSaturated(value);

    private void Seed(long seed)
    {
        var splitMix = new SplitMix(unchecked((ulong)seed));
        _s0 = splitMix.NextUInt64();
        _s1 = splitMix.NextUInt64();
        _s2 = splitMix.NextUInt64();
        _s3 = splitMix.NextUInt64();
        if ((_s0 | _s1 | _s2 | _s3) == 0UL) _s0 = 0x9E3779B97F4A7C15UL;
    }

    private ulong NextUInt64()
    {
        unchecked
        {
            var result = RotateLeft(_s1 * 5UL, 7) * 9UL;
            var t = _s1 << 17;
            _s2 ^= _s0;
            _s3 ^= _s1;
            _s1 ^= _s2;
            _s0 ^= _s3;
            _s2 ^= t;
            _s3 = RotateLeft(_s3, 45);
            return result;
        }
    }

    private ulong NextUInt64Below(ulong exclusiveUpperBound)
    {
        if (exclusiveUpperBound == 0UL) return NextUInt64();
        var threshold = unchecked(0UL - exclusiveUpperBound) % exclusiveUpperBound;
        while (true)
        {
            var value = NextUInt64();
            if (value >= threshold) return value % exclusiveUpperBound;
        }
    }

    private double NextUnitDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));

    private static ulong RotateLeft(ulong value, int offset) => (value << offset) | (value >> (64 - offset));

    private static long CreateDefaultSeed()
    {
        var bytes = Guid.NewGuid().ToByteArray();
        return BitConverter.ToInt64(bytes, 0) ^ DateTime.UtcNow.Ticks;
    }
}
