// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Runtime;

namespace GameEventScript.Api;

/// <summary>
/// An immutable signed-64 range descriptor. Terms are computed lazily without binary64 conversion.
/// </summary>
public sealed class GesIntegerRange(long from, long to, long step)
{
    /// <summary>
    /// Gets the from.
    /// </summary>
    public long From { get; } = from;

    /// <summary>
    /// Gets the to.
    /// </summary>
    public long To { get; } = to;

    /// <summary>
    /// Gets the step.
    /// </summary>
    public long Step { get; } = step;

    internal long Count { get; } = GameEventScriptRangeMath.GetLength(from, to, step);
}

/// <summary>
/// An immutable binary64 range descriptor; terms are computed lazily.
/// </summary>
public sealed class GesFloatRange(double from, double to, double step)
{
    /// <summary>
    /// Gets the from.
    /// </summary>
    public double From { get; } = from;

    /// <summary>
    /// Gets the to.
    /// </summary>
    public double To { get; } = to;

    /// <summary>
    /// Gets the step.
    /// </summary>
    public double Step { get; } = step;

    internal long Count { get; } = GameEventScriptRangeMath.GetLength(from, to, step);
}
