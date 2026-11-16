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
    /// Gets the first term.
    /// </summary>
    public long From { get; } = from;

    /// <summary>
    /// Gets the inclusive bound; the last term is the last one reached from <see cref="From"/> by whole steps that
    /// does not pass it.
    /// </summary>
    public long To { get; } = to;

    /// <summary>
    /// Gets the signed distance between consecutive terms. A step of 0, or a step that points away from
    /// <see cref="To"/>, describes an empty range.
    /// </summary>
    public long Step { get; } = step;

    internal long Count { get; } = GesRangeMath.GetLength(from, to, step);
}

/// <summary>
/// An immutable binary64 range descriptor; terms are computed lazily.
/// </summary>
public sealed class GesFloatRange(double from, double to, double step)
{
    /// <summary>
    /// Gets the first term.
    /// </summary>
    public double From { get; } = from;

    /// <summary>
    /// Gets the inclusive bound; the last term is the last one reached from <see cref="From"/> by whole steps that
    /// does not pass it.
    /// </summary>
    public double To { get; } = to;

    /// <summary>
    /// Gets the signed distance between consecutive terms. A step of 0, a step that points away from
    /// <see cref="To"/>, or a non-finite component describes an empty range.
    /// </summary>
    public double Step { get; } = step;

    internal long Count { get; } = GesRangeMath.GetLength(from, to, step);
}
