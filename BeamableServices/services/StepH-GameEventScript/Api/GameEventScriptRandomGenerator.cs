using System;
using System.Collections.Generic;

namespace StepH.GameEventScript.Api;

/// <summary>
/// A sealed class that provides mechanisms for generating random numbers or values,
/// either through a pseudo-random number generator or a predefined sequence of values.
/// This class is useful for scenarios where deterministic or non-deterministic random
/// value generation is required, especially in game event scripts.
/// </summary>
public sealed class GameEventScriptRandomGenerator
{
    /// <summary>
    /// Creates and returns a new instance of <c>GameEventScriptRandomGenerator</c> using
    /// a default <c>System.Random</c> instance for generating random values.
    /// </summary>
    /// <returns>
    /// A new <c>GameEventScriptRandomGenerator</c> initialized with a default
    /// <c>System.Random</c> instance.
    /// </returns>
    public static GameEventScriptRandomGenerator Create() => new(new Random());

    /// <summary>
    /// Creates and returns a new instance of <c>GameEventScriptRandomGenerator</c> using
    /// the specified <c>System.Random</c> instance for generating random values.
    /// </summary>
    /// <param name="random">
    /// An instance of <c>System.Random</c> used for generating random values.
    /// </param>
    /// <returns>
    /// A new <c>GameEventScriptRandomGenerator</c> initialized with the specified
    /// <c>System.Random</c> instance.
    /// </returns>
    public static GameEventScriptRandomGenerator FromRandom(Random random) => new(random);

    /// <summary>
    /// Creates and returns a new instance of <c>GameEventScriptRandomGenerator</c>
    /// initialized with a <c>System.Random</c> instance seeded with the specified value.
    /// </summary>
    /// <param name="seed">
    /// The seed value used to initialize the <c>System.Random</c> instance for deterministic
    /// random value generation.
    /// </param>
    /// <returns>
    /// A new <c>GameEventScriptRandomGenerator</c> initialized with a <c>System.Random</c> instance
    /// seeded with the specified value.
    /// </returns>
    public static GameEventScriptRandomGenerator FromSeed(int seed) => new(new Random(seed));

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
        if (seed is >= int.MinValue and <= int.MaxValue) return FromSeed((int)seed);
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
    public static GameEventScriptRandomGenerator FromSequence(params double[] values) => new(new Random(), values);

    /// <summary>
    /// Generates a random integer between the specified minimum and maximum values, inclusive.
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
    public int NextInclusiveInt(int minInclusive, int maxInclusive) => (int)NextInclusiveInteger(minInclusive, maxInclusive);

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
        if (TryDequeueSequenceValue(out var queuedValue)) return Math.Min(Math.Max(ToLongSaturated(queuedValue), minInclusive), maxInclusive);
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
        if (minInclusive > maxInclusive) (minInclusive, maxInclusive) = (maxInclusive, minInclusive);
        if (TryDequeueSequenceValue(out var queuedValue)) return Math.Min(Math.Max(queuedValue, minInclusive), maxInclusive);
        if (minInclusive == maxInclusive) return minInclusive;
        var sample = NextUnitFloat();
        return minInclusive + (maxInclusive - minInclusive) * sample;
    }

    private GameEventScriptRandomGenerator(Random random, IEnumerable<double>? sequence = null)
    {
        _random = random;
        _sequence = sequence == null ? null : new Queue<double>(sequence);
    }

    private GameEventScriptRandomGenerator(long seed)
    {
        _random = null;
        _seed64State = (ulong)seed;
    }

    private readonly Random? _random;
    private readonly Queue<double>? _sequence;
    private readonly byte[] _uint64Buffer = new byte[8];
    private ulong _seed64State;

    private bool TryDequeueSequenceValue(out double value)
    {
        if (_sequence == null || _sequence.Count == 0)
        {
            value = 0;
            return false;
        }

        value = _sequence.Dequeue();
        return true;
    }

    private ulong NextUInt64Below(ulong exclusiveUpperBound)
    {
        if (exclusiveUpperBound == 0UL)
        {
            return NextUInt64();
        }

        var threshold = unchecked(0UL - exclusiveUpperBound) % exclusiveUpperBound;
        while (true)
        {
            var value = NextUInt64();
            if (value >= threshold)
            {
                return value % exclusiveUpperBound;
            }
        }
    }

    private ulong NextUInt64()
    {
        if (_random == null) return NextSeededUInt64();
        _random.NextBytes(_uint64Buffer);
        return BitConverter.ToUInt64(_uint64Buffer, 0);
    }

    private double NextUnitFloat()
    {
        if (_random != null) return _random.NextDouble();
        return (NextSeededUInt64() >> 11) * (1.0 / (1UL << 53));
    }

    private ulong NextSeededUInt64()
    {
        unchecked
        {
            var value = _seed64State += 0x9E3779B97F4A7C15UL;
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
            return value ^ (value >> 31);
        }
    }

    private static long ToLongSaturated(double value) => value switch
    {
        double.NaN => 0,
        <= long.MinValue => long.MinValue,
        >= long.MaxValue => long.MaxValue,
        _ => (long)Math.Truncate(value)
    };
}
