// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Globalization;
using System.Text;

namespace GameEventScript.Syntax;

// Values of number tokens, such as `1_000.5` or `0xff`, for the syntax rules that test a literal's value. Number tokens
// have no sign and no exponent; digit separators carry no value.
internal static class GesNumberText
{
    private const NumberStyles DecimalStyles = NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent;

    // The binary64 value, correctly rounded.
    internal static double Value(string text)
        => IsHexadecimal(text) ? GesNumberLiteral.Read(text).IntegerValue : double.Parse(Digits(text), DecimalStyles, CultureInfo.InvariantCulture);

    // Whether a percentage token, such as `5` of `5%`, stays within the binary64 range as a ratio.
    internal static bool IsFinitePercentage(string text) => double.IsFinite(double.Parse(Digits(text) + "e-2", DecimalStyles, CultureInfo.InvariantCulture));

    // Whether the token is exactly an integer from 1 to Int32.MaxValue, such as `6` or `6.0`.
    internal static bool IsPositiveInt32(string text)
    {
        if (IsHexadecimal(text)) return GesNumberLiteral.Read(text).IntegerValue is > 0 and <= int.MaxValue;
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

    // Whether the token is exactly an integer from 0 to 255, such as `0`, `255`, `0xff` or `7.0`.
    internal static bool IsByte(string text)
    {
        if (IsHexadecimal(text)) return GesNumberLiteral.Read(text).IntegerValue is >= 0 and <= 255;
        var digits = Digits(text);
        var point = digits.IndexOf('.');
        if (point >= 0)
        {
            for (var index = point + 1; index < digits.Length; index++)
                if (digits[index] != '0') return false;
            digits = digits.Substring(0, point);
        }

        digits = digits.TrimStart('0');
        return digits.Length < 3 || digits.Length == 3 && string.CompareOrdinal(digits, "255") <= 0;
    }

    // The lexer only forms valid hexadecimal tokens, which start with `0x`.
    private static bool IsHexadecimal(string text) => text.Length > 1 && text[1] == 'x';

    private static string Digits(string text)
    {
        if (text.IndexOf('_') < 0) return text;
        var builder = new StringBuilder(text.Length);
        foreach (var character in text)
            if (character != '_') builder.Append(character);
        return builder.ToString();
    }
}
