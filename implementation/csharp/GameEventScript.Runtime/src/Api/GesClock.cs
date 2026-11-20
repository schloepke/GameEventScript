// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics;

namespace GameEventScript.Api;

/// <summary>Supplies monotonic elapsed time to a serial host or a network end. Implementations must never move backwards.</summary>
public interface IGesClock
{
    /// <summary>Gets nonnegative elapsed whole microseconds from a stable, clock-local origin.</summary>
    long ElapsedMicroseconds { get; }
}

/// <summary>
/// The system monotonic clock, measured from the moment the instance was created. Reading allocates nothing and is
/// unaffected by changes of the wall-clock time. An instance may be shared between hosts and network ends that use one
/// timeline.
/// </summary>
public sealed class GesSystemClock : IGesClock
{
    private readonly long _origin = Stopwatch.GetTimestamp();

    /// <summary>Gets the whole microseconds elapsed since the instance was created.</summary>
    public long ElapsedMicroseconds
    {
        get
        {
            var ticks = Stopwatch.GetTimestamp() - _origin;
            return ticks / Stopwatch.Frequency * 1_000_000 + ticks % Stopwatch.Frequency * 1_000_000 / Stopwatch.Frequency;
        }
    }
}

/// <summary>
/// A virtual clock for deterministic simulation and tests: its time changes only through <see cref="Advance"/>. Not
/// thread-safe; one caller at a time, like the host or end that reads it.
/// </summary>
public sealed class GesVirtualClock : IGesClock
{
    /// <summary>Creates a virtual clock.</summary>
    /// <param name="elapsedMicroseconds">The starting time in whole microseconds.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="elapsedMicroseconds"/> is negative.</exception>
    public GesVirtualClock(long elapsedMicroseconds = 0)
    {
        if (elapsedMicroseconds < 0) throw new ArgumentOutOfRangeException(nameof(elapsedMicroseconds), "The time cannot be negative.");
        ElapsedMicroseconds = elapsedMicroseconds;
    }

    /// <summary>Gets the current virtual time in whole microseconds.</summary>
    public long ElapsedMicroseconds { get; private set; }

    /// <summary>Moves the virtual time forward. Advancing reads no clock and pumps nothing.</summary>
    /// <param name="microseconds">The whole microseconds to add.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="microseconds"/> is negative or the time would exceed <see cref="long.MaxValue"/>.
    /// </exception>
    public void Advance(long microseconds)
    {
        if (microseconds < 0) throw new ArgumentOutOfRangeException(nameof(microseconds), "Time does not go back.");
        if (microseconds > long.MaxValue - ElapsedMicroseconds) throw new ArgumentOutOfRangeException(nameof(microseconds), "The time exceeds the Int64 range.");
        ElapsedMicroseconds += microseconds;
    }
}
