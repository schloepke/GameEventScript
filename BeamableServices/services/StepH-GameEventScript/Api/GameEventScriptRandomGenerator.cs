using System;
using StepH.GameEventScript.VirtualMachine;

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
    /// a default portable random generator for generating random values.
    /// </summary>
    /// <returns>
    /// A new <c>GameEventScriptRandomGenerator</c> initialized with a default generator seed.
    /// </returns>
    public static GameEventScriptRandomGenerator Create() => new(CreateDefaultSeed());

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
        if (_xoshiro != null) return _xoshiro.NextInclusiveInteger(minInclusive, maxInclusive);
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
        if (TryDequeueSequenceValue(out var queuedValue)) return Math.Min(Math.Max(queuedValue, minInclusive), maxInclusive);
        if (_xoshiro != null) return _xoshiro.NextInclusiveFloat(minInclusive, maxInclusive);
        if (minInclusive == maxInclusive) return minInclusive;
        var sample = NextUnitFloat();
        return minInclusive + (maxInclusive - minInclusive) * sample;
    }

    private GameEventScriptRandomGenerator(Random random)
    {
        _random = random;
    }

    private GameEventScriptRandomGenerator(long seed)
    {
        _xoshiro = new GesVmXoshiroRandom(seed);
    }

    private GameEventScriptRandomGenerator(double[] sequence)
    {
        _sequence = sequence;
        _xoshiro = new GesVmXoshiroRandom(CreateDefaultSeed());
    }

    private readonly GesVmXoshiroRandom? _xoshiro;
    private readonly Random? _random;
    private readonly double[]? _sequence;
    private readonly byte[] _uint64Buffer = new byte[8];
    private int _sequenceIndex;

    private bool TryDequeueSequenceValue(out double value)
    {
        if (_sequence == null || _sequenceIndex >= _sequence.Length)
        {
            value = 0;
            return false;
        }

        value = _sequence[_sequenceIndex++];
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
        if (_xoshiro != null) return _xoshiro.NextUInt64();
        if (_random == null) return 0;
        _random.NextBytes(_uint64Buffer);
        return BitConverter.ToUInt64(_uint64Buffer, 0);
    }

    private double NextUnitFloat()
    {
        if (_xoshiro != null) return _xoshiro.NextInclusiveFloat(0, 1);
        return _random?.NextDouble() ?? 0;
    }

    private static long ToLongSaturated(double value) => value switch
    {
        double.NaN => 0,
        <= long.MinValue => long.MinValue,
        >= long.MaxValue => long.MaxValue,
        _ => (long)Math.Truncate(value)
    };

    private static long CreateDefaultSeed()
    {
        var bytes = Guid.NewGuid().ToByteArray();
        return BitConverter.ToInt64(bytes, 0) ^ DateTime.UtcNow.Ticks;
    }
}
