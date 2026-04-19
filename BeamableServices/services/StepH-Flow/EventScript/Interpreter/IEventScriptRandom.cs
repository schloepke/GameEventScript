#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;

namespace StepH.Flow.EventScript.Interpreter;

public interface IEventScriptRandom
{
    int NextInclusive(int minInclusive, int maxInclusive);
}

public sealed class DefaultEventScriptRandom(Random? random = null) : IEventScriptRandom
{
    private readonly Random _random = random ?? new Random();

    public int NextInclusive(int minInclusive, int maxInclusive)
    {
        if (minInclusive > maxInclusive)
        {
            (minInclusive, maxInclusive) = (maxInclusive, minInclusive);
        }

        if (maxInclusive != int.MaxValue) return _random.Next(minInclusive, maxInclusive + 1);
        var sample = _random.NextDouble();
        return minInclusive + (int)Math.Floor(sample * ((long)maxInclusive - minInclusive + 1));
    }
}