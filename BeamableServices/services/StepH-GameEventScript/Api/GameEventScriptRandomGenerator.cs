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
    public int NextInclusiveInt(int minInclusive, int maxInclusive)
    {
        if (minInclusive > maxInclusive) (minInclusive, maxInclusive) = (maxInclusive, minInclusive);
        if (TryDequeueSequenceValue(out var queuedValue))
            return Math.Min(Math.Max(queuedValue <= int.MinValue ? int.MinValue : queuedValue >= int.MaxValue ? int.MaxValue : (int)Math.Truncate(queuedValue), minInclusive), maxInclusive);
        if (minInclusive == maxInclusive) return minInclusive;
        if (maxInclusive < int.MaxValue) return _random.Next(minInclusive, maxInclusive + 1);
        return minInclusive + (int)Math.Floor(_random.NextDouble() * ((long)maxInclusive - minInclusive + 1));
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
        var sample = (double)_random.NextDouble();
        return minInclusive + ((maxInclusive - minInclusive) * sample);
    }

    private GameEventScriptRandomGenerator(Random random, IEnumerable<double>? sequence = null)
    {
        _random = random;
        _sequence = sequence == null ? null : new Queue<double>(sequence);
    }

    private readonly Random _random;
    private readonly Queue<double>? _sequence;

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
}