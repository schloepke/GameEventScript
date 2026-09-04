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
    private const int DefaultMaxScopeDepth = 16;

    internal readonly struct State
    {
        internal State(double[]? sequence, int sequenceIndex, ulong s0, ulong s1, ulong s2, ulong s3)
        {
            Sequence = sequence;
            SequenceIndex = sequenceIndex;
            S0 = s0;
            S1 = s1;
            S2 = s2;
            S3 = s3;
        }

        internal double[]? Sequence { get; }
        internal int SequenceIndex { get; }
        internal ulong S0 { get; }
        internal ulong S1 { get; }
        internal ulong S2 { get; }
        internal ulong S3 { get; }
    }

    internal readonly struct ScopeBoundary
    {
        internal ScopeBoundary(int token)
        {
            Token = token;
        }

        internal int Token { get; }
    }

    internal enum ScopeBoundaryFault
    {
        None,
        LimitExceeded,
        BoundaryUnderflow,
        Unbalanced
    }

    private struct ScopeBoundaryState
    {
        internal int Token;
        internal int ScopeDepth;
    }

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
    public static GameEventScriptRandomGenerator Create() => new(CreateDefaultSeed(), null, DefaultMaxScopeDepth);

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
        return new GameEventScriptRandomGenerator(seed, null, DefaultMaxScopeDepth);
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
    public static GameEventScriptRandomGenerator FromSequence(params double[] values)
        => new(CreateDefaultSeed(), CopySequence(values), DefaultMaxScopeDepth);

    /// <summary>
    /// Saves the active random stream and starts a nested scope from an identical
    /// copy. Leaving the scope restores the saved parent stream.
    /// </summary>
    /// <returns><see langword="true"/> when the scope was entered; otherwise <see langword="false"/> when the configured scope limit was reached.</returns>
    public bool Push() => PushScope(null);

    /// <summary>
    /// Saves the active random stream and starts a nested deterministic stream
    /// initialized from <paramref name="seed"/>. Leaving the scope restores the
    /// saved parent stream.
    /// </summary>
    /// <param name="seed">The signed 64-bit seed for the nested stream.</param>
    /// <returns><see langword="true"/> when the scope was entered; otherwise <see langword="false"/> when the configured scope limit was reached.</returns>
    public bool Push(long seed) => PushScope(seed);

    /// <summary>
    /// Leaves the most recently entered random scope and restores its parent
    /// stream.
    /// </summary>
    /// <returns><see langword="true"/> when a scope was left; otherwise <see langword="false"/> when no scope can be left at the current runtime boundary.</returns>
    public bool Pop()
    {
        if (_faultGateActive)
        {
            if (_overpushDepth > 0) _overpushDepth--;
            if (_faultOwnerToken == 0 && _overpushDepth == 0) ClearFaultGate();
            return false;
        }

        var boundaryDepth = _boundaryDepth == 0 ? 0 : _boundaries![_boundaryDepth - 1].ScopeDepth;
        if (_scopeDepth <= boundaryDepth)
        {
            if (_boundaryDepth > 0) EnterFaultGate(ScopeBoundaryFault.BoundaryUnderflow, 0);
            return false;
        }

        RestoreState(in _scopeStates![--_scopeDepth]);
        _scopeStates[_scopeDepth] = default;
        return true;
    }

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
        if (minInclusive == maxInclusive) return minInclusive;
        if (DequeueSequenceValue() is { } queuedValue)
        {
            return Math.Min(Math.Max(ToLongSaturated(queuedValue), minInclusive), maxInclusive);
        }

        var span = unchecked((ulong)(maxInclusive - minInclusive) + 1UL);
        var offset = NextUInt64Below(span);
        return unchecked(minInclusive + (long)offset);
    }

    /// <summary>
    /// Generates a binary64 value between two bounds.
    /// The generator draws a unit value from the half-open interval [0, 1) and
    /// scales it to the ordered bounds. Binary64 rounding may nevertheless
    /// produce the upper bound.
    /// </summary>
    /// <param name="firstBound">
    /// The first accepted bound. Bounds are swapped when this value is greater
    /// than <paramref name="secondBound"/>.
    /// </param>
    /// <param name="secondBound">
    /// The second accepted bound.
    /// </param>
    /// <returns>
    /// A binary64 value between the ordered bounds. Equal bounds are returned
    /// and NaN bounds produce NaN; neither case consumes the random stream.
    /// </returns>
    public double NextFloat(double firstBound, double secondBound)
    {
        if (double.IsNaN(firstBound) || double.IsNaN(secondBound)) return double.NaN;
        if (firstBound > secondBound) (firstBound, secondBound) = (secondBound, firstBound);
        if (firstBound == secondBound) return firstBound;
        if (DequeueSequenceValue() is { } queuedValue)
        {
            return Math.Min(Math.Max(queuedValue, firstBound), secondBound);
        }

        return firstBound + (secondBound - firstBound) * NextUnitDouble();
    }

    internal static GameEventScriptRandomGenerator CreateForHost(int maxScopeDepth)
        => new(CreateDefaultSeed(), null, ValidateMaxScopeDepth(maxScopeDepth));

    internal static GameEventScriptRandomGenerator FromSeedForHost(long seed, int maxScopeDepth)
        => new(seed, null, ValidateMaxScopeDepth(maxScopeDepth));

    internal static GameEventScriptRandomGenerator FromSequenceForHost(double[] values, long? fallbackSeed, int maxScopeDepth)
        => new(fallbackSeed ?? CreateDefaultSeed(), CopySequence(values), ValidateMaxScopeDepth(maxScopeDepth));

    private GameEventScriptRandomGenerator(long seed, double[]? sequence, int maxScopeDepth)
    {
        _maxScopeDepth = maxScopeDepth;
        Initialize(seed);
        _sequence = sequence;
    }

    private readonly int _maxScopeDepth;
    private State[]? _scopeStates;
    private ScopeBoundaryState[]? _boundaries;
    private int _scopeDepth;
    private int _boundaryDepth;
    private int _nextBoundaryToken;
    private bool _faultGateActive;
    private int _faultOwnerToken;
    private int _overpushDepth;
    private ScopeBoundaryFault _fault;
    private double[]? _sequence;
    private int _sequenceIndex;
    private ulong _s0;
    private ulong _s1;
    private ulong _s2;
    private ulong _s3;

    internal int ScopeDepth => _scopeDepth;
    internal bool HasActiveScopeFault => _faultGateActive;

    internal ScopeBoundary MarkScopeBoundary()
    {
        var boundaries = _boundaries ??= new ScopeBoundaryState[4];
        if (_boundaryDepth == boundaries.Length)
        {
            Array.Resize(ref boundaries, checked(boundaries.Length * 2));
            _boundaries = boundaries;
        }
        var token = unchecked(++_nextBoundaryToken);
        if (token == 0) token = unchecked(++_nextBoundaryToken);
        boundaries[_boundaryDepth++] = new ScopeBoundaryState { Token = token, ScopeDepth = _scopeDepth };
        return new ScopeBoundary(token);
    }

    internal ScopeBoundaryFault ReleaseScopeBoundary(ScopeBoundary boundary)
    {
        var boundaries = _boundaries;
        if (_boundaryDepth == 0 || boundaries is null || boundaries[_boundaryDepth - 1].Token != boundary.Token)
            throw new InvalidOperationException("Random scope boundaries must be released in reverse order.");

        var state = boundaries[--_boundaryDepth];
        boundaries[_boundaryDepth] = default;
        var fault = ScopeBoundaryFault.None;
        if (_faultGateActive && _faultOwnerToken == boundary.Token)
        {
            fault = _fault;
            ClearFaultGate();
        }

        if (_scopeDepth != state.ScopeDepth && fault == ScopeBoundaryFault.None) fault = ScopeBoundaryFault.Unbalanced;
        while (_scopeDepth > state.ScopeDepth)
        {
            RestoreState(in _scopeStates![--_scopeDepth]);
            _scopeStates[_scopeDepth] = default;
        }

        return fault;
    }

    private bool PushScope(long? seed)
    {
        if (_faultGateActive)
        {
            _overpushDepth++;
            return false;
        }

        if (_scopeDepth >= _maxScopeDepth)
        {
            EnterFaultGate(ScopeBoundaryFault.LimitExceeded, 1);
            return false;
        }

        var scopeStates = EnsureScopeStates();
        scopeStates[_scopeDepth++] = CaptureState();
        if (seed is { } validSeed) ResetToSeed(validSeed);
        return true;
    }

    private void EnterFaultGate(ScopeBoundaryFault fault, int overpushDepth)
    {
        EnsureScopeStates()[_maxScopeDepth] = CaptureState();
        _faultGateActive = true;
        _faultOwnerToken = _boundaryDepth == 0 ? 0 : _boundaries![_boundaryDepth - 1].Token;
        _overpushDepth = overpushDepth;
        _fault = fault;
    }

    private void ClearFaultGate()
    {
        RestoreState(in _scopeStates![_maxScopeDepth]);
        _scopeStates[_maxScopeDepth] = default;
        _faultGateActive = false;
        _faultOwnerToken = 0;
        _overpushDepth = 0;
        _fault = ScopeBoundaryFault.None;
    }

    private State[] EnsureScopeStates()
        => _scopeStates ??= new State[_maxScopeDepth + 1];

    private double? DequeueSequenceValue()
    {
        if (_sequence == null || _sequenceIndex >= _sequence.Length)
        {
            return null;
        }

        return _sequence[_sequenceIndex++];
    }

    private static long ToLongSaturated(double value) => GameEventScriptNumber.ToIntegerSaturated(value);

    private static double[] CopySequence(double[] values)
    {
        _ = values ?? throw new ArgumentNullException(nameof(values));
        if (values.Length == 0) return [];
        var result = new double[values.Length];
        Array.Copy(values, result, values.Length);
        return result;
    }

    private static int ValidateMaxScopeDepth(int value)
    {
        if (value < 0 || value > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(value), "Random scope depth must be between 0 and 65,535.");
        return value;
    }

    internal State CaptureState() => new(_sequence, _sequenceIndex, _s0, _s1, _s2, _s3);

    internal void RestoreState(in State state)
    {
        _sequence = state.Sequence;
        _sequenceIndex = state.SequenceIndex;
        _s0 = state.S0;
        _s1 = state.S1;
        _s2 = state.S2;
        _s3 = state.S3;
    }

    internal void ResetToSeed(long seed)
    {
        _sequence = null;
        _sequenceIndex = 0;
        Initialize(seed);
    }

    private void Initialize(long seed)
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
