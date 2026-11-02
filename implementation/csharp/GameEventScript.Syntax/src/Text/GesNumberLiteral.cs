// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Globalization;
using GameEventScript.Api;

namespace GameEventScript.Syntax;

internal enum GesNumberLiteralKind : byte
{
    Invalid,
    // NaN, or a percentage outside the binary64 range: recognized, but the value nothing.
    Nothing,
    Integer,
    Float,
    Percentage
}

/// <summary>
/// A recognized number text such as <c>1_000</c>, <c>2.5km</c> or <c>90%</c>: exact Int64 values, binary64 values
/// rounded once from their decimal digits, and percentage ratios. Runtime turns it into a value; documents only test it.
/// </summary>
internal readonly struct GesNumberLiteral
{
    // Grammar, exact Int64 decoding and decimal Percentage scaling are GES rules.
    // The runtime converter is used only for the final invariant decimal-to-binary64 rounding.
    private const NumberStyles DecimalStyles = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent;
    private static readonly GesNumberLiteral Nothing = new(GesNumberLiteralKind.Nothing, 0, 0, GesQuantityUnit.UnitNone);

    private GesNumberLiteral(GesNumberLiteralKind kind, long integer, double number, GesQuantityUnit unit)
    {
        Kind = kind;
        IntegerValue = integer;
        FloatValue = number;
        Unit = unit;
    }

    internal GesNumberLiteralKind Kind { get; }

    internal long IntegerValue { get; }

    // The binary64 value, or the ratio of a percentage.
    internal double FloatValue { get; }

    internal GesQuantityUnit Unit { get; }

    internal bool IsValid => Kind != GesNumberLiteralKind.Invalid;

    private static GesNumberLiteral Integer(long value, GesQuantityUnit unit) => new(GesNumberLiteralKind.Integer, value, 0, unit);

    private static GesNumberLiteral Float(double value, GesQuantityUnit unit) => new(GesNumberLiteralKind.Float, 0, value, unit);

    private static GesNumberLiteral Percentage(double ratio) => new(GesNumberLiteralKind.Percentage, 0, ratio, GesQuantityUnit.UnitNone);

    // Invalid distinguishes failed recognition from a successfully recognized NaN/nothing value.
    internal static GesNumberLiteral Read(string text, bool percentage = false, bool allowGrouping = true, bool percentageMagnitude = false)
        => Read(text, 0, text.Length, percentage, allowGrouping, percentageMagnitude);

    internal static GesNumberLiteral Read(string text, int start, int length, bool percentage = false, bool allowGrouping = true, bool percentageMagnitude = false)
    {
        var end = start + length;
        while (start < end && text[start] is ' ' or '\t') start++;
        while (end > start && text[end - 1] is ' ' or '\t') end--;
        if (start == end) return default;

        var unit = GesQuantityUnit.UnitNone;
        var percent = percentageMagnitude || text[end - 1] == '%';
        // A unit suffix scales the decimal value by Factor × 10^Exponent; a percentage by 10^-2.
        var shift = percent ? -2 : 0;
        var factor = 1;
        if (text[end - 1] == '%') end--;
        else if (MayEndWithSuffix(text, start, end) &&
                 GesQuantityUnits.TryMatchTrailingSuffix(text, start, end, out var suffix) && IsSuffixAttached(text, start, end - suffix.Text.Length))
        {
            unit = suffix.Unit;
            shift = suffix.Exponent;
            factor = suffix.Factor;
            end -= suffix.Text.Length;
        }
        if (percentage && unit != GesQuantityUnit.UnitNone) return default;
        if (start == end) return default;
        if (text.AsSpan(start, end - start).SequenceEqual("NaN".AsSpan())) return Nothing;

        var position = start;
        var negative = text[position] == '-';
        if (text[position] is '+' or '-') position++;
        if (position == end) return default;
        if (text.AsSpan(position, end - position).SequenceEqual("Infinity".AsSpan()))
            return percentage ? Nothing : Float(negative ? double.NegativeInfinity : double.PositiveInfinity, unit);

        var digitsStart = position;
        var underscores = false;
        var integerDigits = ReadDigits(text, ref position, end, ref underscores);
        if (integerDigits == 0) return default;
        var grouping = position < end && text[position] == ',';
        if (grouping)
        {
            if (!allowGrouping || integerDigits > 3) return default;
            while (position < end && text[position] == ',')
            {
                position++;
                if (ReadDigits(text, ref position, end, ref underscores) != 3) return default;
            }
        }
        var fractionalDigits = 0;
        if (position < end && text[position] == '.')
        {
            position++;
            fractionalDigits = ReadDigits(text, ref position, end, ref underscores);
            if (fractionalDigits == 0) return default;
        }
        var mantissaEnd = position;
        long exponent = 0;
        if (position < end && text[position] is 'e' or 'E')
        {
            position++;
            var exponentNegative = position < end && text[position] == '-';
            if (position < end && text[position] is '+' or '-') position++;
            var exponentStart = position;
            if (ReadDigits(text, ref position, end, ref underscores) == 0) return default;
            for (var i = exponentStart; i < position; i++)
                if (IsDigit(text[i])) exponent = Math.Min(1L << 50, exponent * 10 + text[i] - '0');
            if (exponentNegative) exponent = -exponent;
        }
        if (position != end || grouping && underscores) return default;

        if (factor != 1) return ReadFactored(text, digitsStart, mantissaEnd, negative, fractionalDigits, exponent + shift, factor, unit);

        var digits = 0;
        var first = -1;
        var last = -1;
        for (var i = digitsStart; i < mantissaEnd; i++)
        {
            if (!IsDigit(text[i])) continue;
            if (text[i] != '0')
            {
                if (first < 0) first = digits;
                last = digits;
            }
            digits++;
        }
        if (first < 0) return percentage ? Percentage(0) : Integer(0, unit);

        var power = exponent - fractionalDigits + digits - last - 1 + shift;
        if (!percentage && power >= 0 && last - first + 1 + power <= 19)
        {
            ulong magnitude = 0;
            var digitIndex = 0;
            for (var i = digitsStart; i < mantissaEnd; i++)
            {
                if (!IsDigit(text[i])) continue;
                if (digitIndex >= first && digitIndex <= last) magnitude = magnitude * 10 + (uint)(text[i] - '0');
                digitIndex++;
            }
            for (var i = 0L; i < power; i++) magnitude *= 10;
            var limit = negative ? 9223372036854775808UL : 9223372036854775807UL;
            if (magnitude <= limit)
            {
                var value = negative ? unchecked(-(long)magnitude) : (long)magnitude;
                return Integer(value, unit);
            }
        }

        double number;
        if (!underscores && !grouping && shift == 0)
        {
            if (!double.TryParse(text.AsSpan(start, end - start), DecimalStyles, CultureInfo.InvariantCulture, out number)) return default;
        }
        else
        {
            // Percentage and prefix scaling adjust the decimal exponent before the single binary64 rounding.
            var capacity = mantissaEnd - start + 24;
            Span<char> buffer = capacity <= 256 ? stackalloc char[256] : new char[capacity];
            var count = 0;
            for (var i = start; i < mantissaEnd; i++)
                if (text[i] is not ('_' or ',')) buffer[count++] = text[i];
            buffer[count++] = 'e';
            (exponent + shift).TryFormat(buffer.Slice(count), out var written, provider: CultureInfo.InvariantCulture);
            count += written;
            if (!double.TryParse(buffer.Slice(0, count), DecimalStyles, CultureInfo.InvariantCulture, out number)) return default;
        }
        return percentage
            ? double.IsFinite(number) ? Percentage(number) : Nothing
            : Float(number, unit);
    }

    // The energy spellings Wh and kWh multiply the mantissa digits exactly by a non-decimal factor before rounding.
    // They carry a unit, so the caller has already rejected percentage decoding.
    private static GesNumberLiteral ReadFactored(string text, int digitsStart, int mantissaEnd, bool negative, int fractionalDigits, long exponent, int factor, GesQuantityUnit unit)
    {
        var mantissaDigits = 0;
        for (var i = digitsStart; i < mantissaEnd; i++)
            if (IsDigit(text[i])) mantissaDigits++;
        Span<char> digitBuffer = mantissaDigits + 2 <= 128 ? stackalloc char[128] : new char[mantissaDigits + 2];
        var digits = 0;
        for (var i = digitsStart; i < mantissaEnd; i++)
            if (IsDigit(text[i])) digitBuffer[digits++] = text[i];
        digits = MultiplyDecimal(digitBuffer, digits, factor);

        var first = -1;
        var last = -1;
        for (var i = 0; i < digits; i++)
        {
            if (digitBuffer[i] == '0') continue;
            if (first < 0) first = i;
            last = i;
        }
        if (first < 0) return Integer(0, unit);

        var power = exponent - fractionalDigits + digits - last - 1;
        if (power >= 0 && last - first + 1 + power <= 19)
        {
            ulong magnitude = 0;
            for (var i = first; i <= last; i++) magnitude = magnitude * 10 + (uint)(digitBuffer[i] - '0');
            for (var i = 0L; i < power; i++) magnitude *= 10;
            var limit = negative ? 9223372036854775808UL : 9223372036854775807UL;
            if (magnitude <= limit)
            {
                var value = negative ? unchecked(-(long)magnitude) : (long)magnitude;
                return Integer(value, unit);
            }
        }

        Span<char> buffer = digits + 32 <= 256 ? stackalloc char[256] : new char[digits + 32];
        var count = 0;
        if (negative) buffer[count++] = '-';
        digitBuffer.Slice(0, digits).CopyTo(buffer.Slice(count));
        count += digits;
        buffer[count++] = 'e';
        (exponent - fractionalDigits).TryFormat(buffer.Slice(count), out var written, provider: CultureInfo.InvariantCulture);
        count += written;
        return double.TryParse(buffer.Slice(0, count), DecimalStyles, CultureInfo.InvariantCulture, out var number) ? Float(number, unit) : default;
    }

    // Only these characters end a unit spelling, so plain numbers and other text skip the suffix table. A native test
    // keeps the set in step with GesQuantityUnits.Suffixes.
    internal static bool MayEndWithSuffix(string text, int start, int end)
    {
        var last = text[end - 1];
        return last is 's' or 'm' or 'h' or 'z' or 'g' or 't' or 'B' or 'N' or 'J' or 'W' or 'V' or 'A' or 'K' or '\u00B0' or '\u00B2' or '\u00B3'
            || last is '2' or '3' && end - start > 2 && text[end - 2] == '^';
    }

    // A unit suffix belongs to the number only when it directly follows a digit or the special values.
    private static bool IsSuffixAttached(string text, int start, int suffixStart)
    {
        if (IsDigit(text[suffixStart - 1])) return true;
        var position = start;
        if (text[position] is '+' or '-') position++;
        var length = suffixStart - position;
        return length == 8 && string.CompareOrdinal(text, position, "Infinity", 0, 8) == 0
            || suffixStart - start == 3 && string.CompareOrdinal(text, start, "NaN", 0, 3) == 0;
    }

    // Multiplies the decimal digits in place by a small positive factor and returns the new digit count.
    private static int MultiplyDecimal(Span<char> digits, int count, int factor)
    {
        var carry = 0;
        for (var i = count - 1; i >= 0; i--)
        {
            var product = (digits[i] - '0') * factor + carry;
            digits[i] = (char)('0' + product % 10);
            carry = product / 10;
        }
        if (carry == 0) return count;
        var extra = carry >= 10 ? 2 : 1;
        for (var i = count - 1; i >= 0; i--) digits[i + extra] = digits[i];
        for (var i = extra - 1; i >= 0; i--)
        {
            digits[i] = (char)('0' + carry % 10);
            carry /= 10;
        }
        return count + extra;
    }

    private static int ReadDigits(string text, ref int position, int end, ref bool underscores)
    {
        var count = 0;
        while (position < end && IsDigit(text[position]))
        {
            count++;
            position++;
            if (position < end && text[position] == '_' && position + 1 < end && IsDigit(text[position + 1]))
            {
                underscores = true;
                position++;
            }
        }
        return count;
    }

    private static bool IsDigit(char value) => value is >= '0' and <= '9';
}
