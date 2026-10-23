// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Globalization;
using GameEventScript.Api;

namespace GameEventScript.Tool;

/// <summary>Monotonic host clock of a CLI session that also shows host deadlines as local wall-clock times.</summary>
internal sealed class RunClock : IGesClock
{
    // Deadlines further away than about a century are shown as a remaining delay.
    private const long MaxShownMicroseconds = 3_155_760_000_000_000;
    private readonly long _origin = Stopwatch.GetTimestamp();
    // Wall-clock time of the monotonic origin; deadlines are shown relative to it.
    private readonly DateTime _wallOrigin = DateTime.UtcNow;

    /// <inheritdoc />
    public long ElapsedMicroseconds
    {
        get
        {
            var ticks = Stopwatch.GetTimestamp() - _origin;
            return ticks / Stopwatch.Frequency * 1_000_000 + ticks % Stopwatch.Frequency * 1_000_000 / Stopwatch.Frequency;
        }
    }

    // Adds the deadline to the session's start time and shows it in local time.
    internal string FormatDeadline(long deadline)
    {
        if (deadline > MaxShownMicroseconds) return $"in {deadline - ElapsedMicroseconds}us";
        var due = _wallOrigin.AddTicks(deadline * 10).ToLocalTime();
        return due.ToString(due.Date == DateTime.Today ? "HH:mm:ss.fff" : "yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
    }
}
