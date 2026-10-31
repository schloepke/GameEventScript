// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Globalization;
using System.Text;

namespace GameEventScript.Syntax;

// Values of decimal number tokens, such as `1_000.5`, for the syntax rules that test a literal's value. Number tokens
// have no sign and no exponent; digit separators carry no value.
internal static class GesNumberText
{
    private const NumberStyles DecimalStyles = NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent;

    // The binary64 value, correctly rounded.
    internal static double Value(string text) => double.Parse(Digits(text), DecimalStyles, CultureInfo.InvariantCulture);

    // Whether a percentage token, such as `5` of `5%`, stays within the binary64 range as a ratio.
    internal static bool IsFinitePercentage(string text) => double.IsFinite(double.Parse(Digits(text) + "e-2", DecimalStyles, CultureInfo.InvariantCulture));

    // Whether the token is exactly an integer from 1 to Int32.MaxValue, such as `6` or `6.0`.
    internal static bool IsPositiveInt32(string text)
    {
        var digits = Digits(text);
        var point = digits.IndexOf('.');
        if (point >= 0)
        {
            for (var index = point + 1; index < digits.Length; index++)
                if (digits[index] != '0') return false;
            digits = digits.Substring(0, point);
        }

        digits = digits.TrimStart('0');
        return digits.Length is > 0 and < 10 || digits.Length == 10 && string.CompareOrdinal(digits, "2147483647") <= 0;
    }

    private static string Digits(string text)
    {
        if (text.IndexOf('_') < 0) return text;
        var builder = new StringBuilder(text.Length);
        foreach (var character in text)
            if (character != '_') builder.Append(character);
        return builder.ToString();
    }
}
