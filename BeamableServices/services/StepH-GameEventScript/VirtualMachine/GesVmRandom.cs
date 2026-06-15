using System;
using StepH.GameEventScript.Api;

namespace StepH.GameEventScript.VirtualMachine;

internal struct GesVmSplitMix
{
    private ulong _state;

    internal GesVmSplitMix(long seed)
    {
        _state = unchecked((ulong)seed);
    }

    internal GesVmSplitMix(ulong seed)
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

internal sealed class GesVmXoshiroRandom
{
    private readonly GameEventScriptRandomGenerator? _external;
    private ulong _s0;
    private ulong _s1;
    private ulong _s2;
    private ulong _s3;

    internal GesVmXoshiroRandom(GameEventScriptRandomGenerator external)
    {
        _external = external;
    }

    internal GesVmXoshiroRandom(long seed)
    {
        Seed(seed);
    }

    internal GesVmXoshiroRandom(ulong seed)
    {
        Seed(seed);
    }

    internal void Seed(long seed) => Seed(unchecked((ulong)seed));

    internal void Seed(ulong seed)
    {
        var splitMix = new GesVmSplitMix(seed);
        _s0 = splitMix.NextUInt64();
        _s1 = splitMix.NextUInt64();
        _s2 = splitMix.NextUInt64();
        _s3 = splitMix.NextUInt64();
        if ((_s0 | _s1 | _s2 | _s3) == 0UL) _s0 = 0x9E3779B97F4A7C15UL;
    }

    internal ulong NextUInt64()
    {
        if (_external != null) return unchecked((ulong)_external.NextInclusiveInteger(long.MinValue, long.MaxValue));
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

    internal long NextLong(long minInclusive, long maxInclusive)
    {
        if (_external != null) return _external.NextInclusiveInteger(minInclusive, maxInclusive);
        if (minInclusive > maxInclusive) (minInclusive, maxInclusive) = (maxInclusive, minInclusive);
        if (minInclusive == maxInclusive) return minInclusive;
        var span = unchecked((ulong)(maxInclusive - minInclusive) + 1UL);
        var offset = NextUInt64Below(span);
        return unchecked(minInclusive + (long)offset);
    }

    internal int NextInclusiveInt(int minInclusive, int maxInclusive) => (int)NextLong(minInclusive, maxInclusive);

    internal long NextInclusiveInteger(long minInclusive, long maxInclusive) => NextLong(minInclusive, maxInclusive);

    internal ushort NextUShort(ushort minInclusive, ushort maxInclusive)
    {
        if (minInclusive > maxInclusive) (minInclusive, maxInclusive) = (maxInclusive, minInclusive);
        return (ushort)NextLong(minInclusive, maxInclusive);
    }

    internal double NextDouble(double minInclusive, double maxInclusive)
    {
        if (_external != null) return _external.NextInclusiveFloat(minInclusive, maxInclusive);
        if (double.IsNaN(minInclusive) || double.IsNaN(maxInclusive)) return double.NaN;
        if (minInclusive > maxInclusive) (minInclusive, maxInclusive) = (maxInclusive, minInclusive);
        if (minInclusive == maxInclusive) return minInclusive;
        return minInclusive + (maxInclusive - minInclusive) * NextUnitDouble();
    }

    internal double NextInclusiveFloat(double minInclusive, double maxInclusive) => NextDouble(minInclusive, maxInclusive);

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
}
