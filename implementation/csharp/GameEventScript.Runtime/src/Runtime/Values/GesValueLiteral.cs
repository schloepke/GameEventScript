// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Api;

namespace GameEventScript.Runtime.Values;

/// <remarks>Data literal recognition with the rules of the GES <c>parse</c> and <c>parse strict</c> expressions.
/// Without a running Program, Program Record constructors are unknown types; <c>:Record("Type", [...])</c> reconstructs
/// Records directly. Recognition never executes constructors or host callbacks.</remarks>
public partial struct GesValue
{
    /// <summary>Recognizes one complete data literal, like the GES <c>parse</c> expression.</summary>
    /// <param name="input">The literal text, for example <c>150kW</c>, <c>#standard</c> or <c>[energy: 150kW]</c>.</param>
    /// <returns>The recognized value, or <paramref name="input"/> as unchanged Text when it is not a complete data
    /// literal.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="input"/> contains an unpaired surrogate.</exception>
    /// <exception cref="GesLiteralLimitException">The input exceeds a fixed literal parsing limit.</exception>
    public static GesValue ParseLiteral(string input)
    {
        var text = GesText(input ?? throw new ArgumentNullException(nameof(input)));
        return GesLiteralParser.ParseData(in text) ?? text;
    }

    /// <summary>Recognizes one complete data literal, like the GES <c>parse strict</c> expression.</summary>
    /// <param name="input">The literal text. Text values must be quoted, for example <c>'standard'</c>.</param>
    /// <returns>The recognized value, which may be Nothing for the literal <c>nothing</c>, or <see langword="null"/>
    /// when <paramref name="input"/> is not a complete data literal.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="input"/> contains an unpaired surrogate.</exception>
    /// <exception cref="GesLiteralLimitException">The input exceeds a fixed literal parsing limit.</exception>
    public static GesValue? ParseLiteralStrict(string input)
    {
        var text = GesText(input ?? throw new ArgumentNullException(nameof(input)));
        return GesLiteralParser.ParseData(in text);
    }

    /// <summary>
    /// Formats this value with the GES document printer: literal text that <see cref="ParseLiteral"/> reads back to an
    /// equal value within the literal parsing limits, with Text quoted and large containers in indented block form.
    /// </summary>
    /// <param name="format">Layout options, or <see langword="null"/> for the defaults.</param>
    /// <returns>The formatted literal text without a trailing line break.</returns>
    /// <exception cref="ArgumentException">The value is or contains an External value, a Dice roll below 1 or a Record
    /// whose type name is not a type name.</exception>
    public readonly string Format(GesDocumentFormat? format = null) => GesDocumentFormatter.Format(in this, format ?? new GesDocumentFormat());
}
