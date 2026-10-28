// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;

namespace GameEventScript.Api;

/// <summary>
/// Layout options for printing GES documents and values. Layout is a recommendation: every printed document reads
/// back to an equal document, while indentation, line breaks and numeric spelling may differ between implementations.
/// </summary>
public sealed record GesDocumentFormat
{
    private readonly int _indentation = 2;
    private readonly int _lineWidth = 100;

    /// <summary>The largest supported <see cref="Indentation"/>.</summary>
    public const int MaximumIndentation = 100;

    /// <summary>Spaces per nesting level of a block container, from 0 to <see cref="MaximumIndentation"/>; 2 by default.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative or above <see cref="MaximumIndentation"/>.</exception>
    public int Indentation
    {
        get => _indentation;
        init => _indentation = value is >= 0 and <= MaximumIndentation ? value : throw new ArgumentOutOfRangeException(nameof(Indentation));
    }

    /// <summary>Maximum line width in Unicode scalars up to which a container is printed inline; 100 by default.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than 1.</exception>
    public int LineWidth
    {
        get => _lineWidth;
        init => _lineWidth = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(LineWidth));
    }

    /// <summary>
    /// Whether scalar quantities use exact prefixed unit spellings such as <c>21.4kN</c> or <c>250ms</c>; on by default.
    /// When off, every quantity uses its base unit symbol.
    /// </summary>
    public bool PrefixedUnits { get; init; } = true;
}
