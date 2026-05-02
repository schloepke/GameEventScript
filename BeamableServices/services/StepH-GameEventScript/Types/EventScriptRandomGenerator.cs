#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;

namespace StepH.GameEventScript.Types;

public sealed class EventScriptRandomGenerator
{
    private readonly Random _random;
    private readonly Queue<decimal>? _sequence;

    private EventScriptRandomGenerator(Random random, IEnumerable<decimal>? sequence = null)
    {
        _random = random;
        _sequence = sequence == null ? null : new Queue<decimal>(sequence);
    }

    public static EventScriptRandomGenerator Create() => new(new Random());

    public static EventScriptRandomGenerator FromRandom(Random random) => new(random);

    public static EventScriptRandomGenerator FromSeed(int seed) => new(new Random(seed));

    public static EventScriptRandomGenerator FromSequence(params decimal[] values) => new(new Random(), values);

    public int NextInclusiveInt(int minInclusive, int maxInclusive)
    {
        if (minInclusive > maxInclusive) (minInclusive, maxInclusive) = (maxInclusive, minInclusive);
        if (TryDequeueSequenceValue(out var queuedValue))
            return Math.Min(Math.Max(queuedValue <= int.MinValue ? int.MinValue : queuedValue >= int.MaxValue ? int.MaxValue : (int)decimal.Truncate(queuedValue), minInclusive), maxInclusive);
        if (minInclusive == maxInclusive) return minInclusive;
        if (maxInclusive < int.MaxValue) return _random.Next(minInclusive, maxInclusive + 1);
        return minInclusive + (int)Math.Floor(_random.NextDouble() * ((long)maxInclusive - minInclusive + 1));
    }

    public decimal NextInclusiveDecimal(decimal minInclusive, decimal maxInclusive)
    {
        if (minInclusive > maxInclusive) (minInclusive, maxInclusive) = (maxInclusive, minInclusive);
        if (TryDequeueSequenceValue(out var queuedValue)) return Math.Min(Math.Max(queuedValue, minInclusive), maxInclusive);
        if (minInclusive == maxInclusive) return minInclusive;
        var sample = (decimal)_random.NextDouble();
        return minInclusive + ((maxInclusive - minInclusive) * sample);
    }

    private bool TryDequeueSequenceValue(out decimal value)
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