// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;

namespace GameEventScript.Api;

/// <summary>Literal recognition outside a Host exceeded one of its fixed protection limits.</summary>
public sealed class GesLiteralLimitException : Exception
{
    /// <summary>Creates a limit failure for the named limit.</summary>
    /// <param name="limitName">The stable limit name, for example <c>MaxLiteralDepth</c>.</param>
    /// <param name="limit">The exceeded limit value.</param>
    public GesLiteralLimitException(string limitName, int limit) : base($"Literal parsing exceeds {limitName} ({limit}).")
    {
        LimitName = limitName ?? throw new ArgumentNullException(nameof(limitName));
        Limit = limit;
    }

    /// <summary>The stable name of the exceeded limit: <c>MaxLiteralInputScalars</c>, <c>MaxLiteralDepth</c>,
    /// <c>MaxLiteralItems</c> or <c>MaxRangeItems</c>.</summary>
    public string LimitName { get; }

    /// <summary>The exceeded limit value.</summary>
    public int Limit { get; }

    // Source position of the form whose evaluation exceeded the limit, for document errors.
    internal int Offset { get; init; }
}
