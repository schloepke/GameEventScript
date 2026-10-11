// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.RegularExpressions;
using GameEventScript.Api;

internal sealed class VirtualClock : IGameEventScriptClock
{
    /// <summary>Returns deterministic elapsed microseconds advanced only by fixture actions.</summary>
    public long ElapsedMicroseconds { get; private set; }

    internal static bool ValidDuration(string? value) => value is not null
        && Regex.IsMatch(value, @"\A(?:0|[1-9][0-9]*)\z") && long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out _);

    internal bool Advance(string value)
    {
        var delta = long.Parse(value, NumberStyles.None, CultureInfo.InvariantCulture);
        if (delta > long.MaxValue - ElapsedMicroseconds) return false;
        ElapsedMicroseconds += delta;
        return true;
    }
}
