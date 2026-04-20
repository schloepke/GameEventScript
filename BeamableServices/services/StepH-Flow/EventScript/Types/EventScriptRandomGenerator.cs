#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;

namespace StepH.Flow.EventScript.Types;

public sealed class EventScriptRandomGenerator
{
    private readonly Random? _random;
    private readonly Queue<object>? _sequence;

    public EventScriptRandomGenerator() : this(random: new Random())
    {
    }

    private EventScriptRandomGenerator(int seed) : this(random: new Random(seed))
    {
    }

    private EventScriptRandomGenerator(Random random)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
    }

    private EventScriptRandomGenerator(IEnumerable<object> sequence)
    {
        _sequence = new Queue<object>(sequence ?? throw new ArgumentNullException(nameof(sequence)));
    }

    public static EventScriptRandomGenerator FromSeed(int seed) => new(seed);

    public static EventScriptRandomGenerator FromSequence(params object[] values) => new(values);

    public int NextInclusiveInt(int minInclusive, int maxInclusive)
    {
        if (TryDequeueSequenceValue(out var queuedValue))
        {
            var intValue = queuedValue switch
            {
                int value => value,
                long value when value >= int.MinValue && value <= int.MaxValue => (int)value,
                decimal value when decimal.Truncate(value) == value &&
                                   value >= int.MinValue &&
                                   value <= int.MaxValue => (int)value,
                _ => throw new InvalidOperationException($"Queued random value '{queuedValue}' is not a valid integer random value.")
            };

            ValidateRange(intValue, minInclusive, maxInclusive);
            return intValue;
        }

        var random = _random ?? throw new InvalidOperationException("No random source configured.");
        NormalizeRange(ref minInclusive, ref maxInclusive);
        if (maxInclusive != int.MaxValue) return random.Next(minInclusive, maxInclusive + 1);
        var sample = random.NextDouble();
        random.Next();
        return minInclusive + (int)Math.Floor(sample * ((long)maxInclusive - minInclusive + 1));
    }

    public decimal NextInclusiveDecimal(decimal minInclusive, decimal maxInclusive)
    {
        if (TryDequeueSequenceValue(out var queuedValue))
        {
            var decimalValue = queuedValue switch
            {
                decimal value => value,
                int value => value,
                long value => value,
                _ => throw new InvalidOperationException($"Queued random value '{queuedValue}' is not a valid decimal random value.")
            };

            ValidateRange(decimalValue, minInclusive, maxInclusive);
            return decimalValue;
        }

        var random = _random ?? throw new InvalidOperationException("No random source configured.");
        NormalizeRange(ref minInclusive, ref maxInclusive);
        if (minInclusive == maxInclusive)
        {
            return minInclusive;
        }

        var sample = (decimal)random.NextDouble();
        return minInclusive + ((maxInclusive - minInclusive) * sample);
    }

    private bool TryDequeueSequenceValue(out object value)
    {
        if (_sequence is { Count: > 0 })
        {
            value = _sequence.Dequeue();
            return true;
        }

        value = default!;
        return false;
    }

    private static void NormalizeRange(ref int minInclusive, ref int maxInclusive)
    {
        if (minInclusive > maxInclusive)
        {
            (minInclusive, maxInclusive) = (maxInclusive, minInclusive);
        }
    }

    private static void NormalizeRange(ref decimal minInclusive, ref decimal maxInclusive)
    {
        if (minInclusive > maxInclusive)
        {
            (minInclusive, maxInclusive) = (maxInclusive, minInclusive);
        }
    }

    private static void ValidateRange(int value, int minInclusive, int maxInclusive)
    {
        NormalizeRange(ref minInclusive, ref maxInclusive);
        if (value < minInclusive || value > maxInclusive)
        {
            throw new InvalidOperationException($"Queued random value {value} out of expected range [{minInclusive}, {maxInclusive}]");
        }
    }

    private static void ValidateRange(decimal value, decimal minInclusive, decimal maxInclusive)
    {
        NormalizeRange(ref minInclusive, ref maxInclusive);
        if (value < minInclusive || value > maxInclusive)
        {
            throw new InvalidOperationException($"Queued random value {value} out of expected range [{minInclusive}, {maxInclusive}]");
        }
    }
}
