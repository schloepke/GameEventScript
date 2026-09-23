// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace GameEventScript.Api;

/// <summary>Supplies monotonic elapsed time to a serial host. Implementations must never move backwards.</summary>
public interface IGameEventScriptClock
{
    /// <summary>Gets nonnegative elapsed whole microseconds from a stable, clock-local origin.</summary>
    long ElapsedMicroseconds { get; }
}

internal sealed class GameEventScriptSystemClock : IGameEventScriptClock
{
    private readonly long _origin = Stopwatch.GetTimestamp();
    /// <inheritdoc />
    public long ElapsedMicroseconds
    {
        get
        {
            var ticks = Stopwatch.GetTimestamp() - _origin;
            return ticks / Stopwatch.Frequency * 1_000_000 + ticks % Stopwatch.Frequency * 1_000_000 / Stopwatch.Frequency;
        }
    }
}
