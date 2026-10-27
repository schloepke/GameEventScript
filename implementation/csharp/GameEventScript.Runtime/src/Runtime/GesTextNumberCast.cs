// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Globalization;
using System.Text;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;

namespace GameEventScript.Runtime;

internal static class GesTextNumberCast
{
    // Grammar, exact Int64 decoding and decimal Percentage scaling are GES rules.
    // The runtime converter is used only for the final invariant decimal-to-binary64 rounding.
    private const NumberStyles DecimalStyles = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent;

    // Null distinguishes failed recognition from a successfully recognized NaN/nothing value.
    internal static GesValue? Read(string text, bool percentage = false, bool allowGrouping = true, bool percentageMagnitude = false)
        => Read(text, 0, text.Length, percentage, allowGrouping, percentageMagnitude);

    internal static GesValue? Read(string text, int start, int length, bool percentage = false, bool allowGrouping = true, bool percentageMagnitude = false)
    {
        var end = start + length;
        while (start < end && text[start] is ' ' or '\t') start++;
        while (end > start && text[end - 1] is ' ' or '\t') end--;
        if (start == end) return null;

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
        if (percentage && unit != GesQuantityUnit.UnitNone) return null;
        if (start == end) return null;
        if (text.AsSpan(start, end - start).SequenceEqual("NaN".AsSpan())) return GesValue.GesNothing();

        var position = start;
        var negative = text[position] == '-';
        if (text[position] is '+' or '-') position++;
        if (position == end) return null;
        if (text.AsSpan(position, end - position).SequenceEqual("Infinity".AsSpan()))
            return percentage ? GesValue.GesNothing() : GesValue.GesFloat(negative ? double.NegativeInfinity : double.PositiveInfinity, unit);

        var digitsStart = position;
        var underscores = false;
        var integerDigits = ReadDigits(text, ref position, end, ref underscores);
        if (integerDigits == 0) return null;
        var grouping = position < end && text[position] == ',';
        if (grouping)
        {
            if (!allowGrouping || integerDigits > 3) return null;
            while (position < end && text[position] == ',')
            {
                position++;
                if (ReadDigits(text, ref position, end, ref underscores) != 3) return null;
            }
        }
        var fractionalDigits = 0;
        if (position < end && text[position] == '.')
        {
            position++;
            fractionalDigits = ReadDigits(text, ref position, end, ref underscores);
            if (fractionalDigits == 0) return null;
        }
        var mantissaEnd = position;
        long exponent = 0;
        if (position < end && text[position] is 'e' or 'E')
        {
            position++;
            var exponentNegative = position < end && text[position] == '-';
            if (position < end && text[position] is '+' or '-') position++;
            var exponentStart = position;
            if (ReadDigits(text, ref position, end, ref underscores) == 0) return null;
            for (var i = exponentStart; i < position; i++)
                if (IsDigit(text[i])) exponent = Math.Min(1L << 50, exponent * 10 + text[i] - '0');
            if (exponentNegative) exponent = -exponent;
        }
        if (position != end || grouping && underscores) return null;

        if (factor != 1) return ReadFactored(text, digitsStart, mantissaEnd, negative, percentage, fractionalDigits, exponent + shift, factor, unit);

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
        if (first < 0) return percentage ? GesValue.GesPercentage(0) : GesValue.GesInteger(0, unit);

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
                return GesValue.GesInteger(value, unit);
            }
        }

        double number;
        if (!underscores && !grouping && shift == 0)
        {
            if (!double.TryParse(text.AsSpan(start, end - start), DecimalStyles, CultureInfo.InvariantCulture, out number)) return null;
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
            if (!double.TryParse(buffer.Slice(0, count), DecimalStyles, CultureInfo.InvariantCulture, out number)) return null;
        }
        return percentage
            ? double.IsFinite(number) ? GesValue.GesPercentage(number) : GesValue.GesNothing()
            : GesValue.GesFloat(number, unit);
    }

    // The energy spellings Wh and kWh multiply the mantissa digits exactly by a non-decimal factor before rounding.
    private static GesValue? ReadFactored(string text, int digitsStart, int mantissaEnd, bool negative, bool percentage, int fractionalDigits, long exponent, int factor, GesQuantityUnit unit)
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
        if (first < 0) return percentage ? GesValue.GesPercentage(0) : GesValue.GesInteger(0, unit);

        var power = exponent - fractionalDigits + digits - last - 1;
        if (!percentage && power >= 0 && last - first + 1 + power <= 19)
        {
            ulong magnitude = 0;
            for (var i = first; i <= last; i++) magnitude = magnitude * 10 + (uint)(digitBuffer[i] - '0');
            for (var i = 0L; i < power; i++) magnitude *= 10;
            var limit = negative ? 9223372036854775808UL : 9223372036854775807UL;
            if (magnitude <= limit)
            {
                var value = negative ? unchecked(-(long)magnitude) : (long)magnitude;
                return GesValue.GesInteger(value, unit);
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
        if (!double.TryParse(buffer.Slice(0, count), DecimalStyles, CultureInfo.InvariantCulture, out var number)) return null;
        return percentage
            ? double.IsFinite(number) ? GesValue.GesPercentage(number) : GesValue.GesNothing()
            : GesValue.GesFloat(number, unit);
    }

    // Only these characters end a unit spelling, so plain numbers and other text skip the suffix table.
    private static bool MayEndWithSuffix(string text, int start, int end)
    {
        var last = text[end - 1];
        return last is 's' or 'm' or 'h' or 'z' or 'g' or 'N' or 'J' or 'W' or 'V' or 'A' or 'K' or '\u00B0' or '\u00B2'
            || last == '2' && end - start > 2 && text[end - 2] == '^';
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

    internal static GesValue Percentage(in GesValue value)
    {
        if (value.Kind == GesBytecodeTypeKind.Text) return Read(value.TextValue, percentage: true) ?? GesValue.GesNothing();
        if (value.Kind == GesBytecodeTypeKind.Percentage) return value;
        var ratio = value.AsNumeric;
        return !value.HasUnit && double.IsFinite(ratio) ? GesValue.GesPercentage(ratio) : GesValue.GesNothing();
    }

    internal static string Format(long value, GesQuantityUnit unit)
    {
        var text = value.ToString(CultureInfo.InvariantCulture);
        return unit.IsNumericUnit() ? text + unit.ToSuffix() : text;
    }

    internal static string Format(double value, GesQuantityUnit unit)
    {
        var text = GesNumber.FormatCanonicalFloat(value);
        return unit.IsNumericUnit() ? text + unit.ToSuffix() : text;
    }

    internal static string FormatPercentage(double ratio)
    {
        if (ratio == 0) return "0%";
        // Format the ratio's exact Number view, then shift its decimal exponent without binary arithmetic.
        var text = GesNumber.CanRepresentAsInteger(ratio)
            ? ((long)ratio).ToString(CultureInfo.InvariantCulture)
            : GesNumber.FormatCanonicalFloat(ratio);
        var exponentIndex = text.IndexOf('e');
        if (exponentIndex >= 0)
        {
            var exponent = int.Parse(text.AsSpan(exponentIndex + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
            return text.Substring(0, exponentIndex) + "e" + (exponent + 2).ToString(CultureInfo.InvariantCulture) + "%";
        }

        var point = text.IndexOf('.');
        if (point < 0) return text + "00%";
        var builder = new StringBuilder(text.Length + 3);
        var sign = text[0] == '-' ? 1 : 0;
        var newPoint = point - sign + 2;
        var digitCount = text.Length - sign - 1;
        var firstNonzero = sign;
        while (firstNonzero < text.Length && text[firstNonzero] is '0' or '.') firstNonzero++;
        if (sign != 0) builder.Append('-');
        var outputDigits = 0;
        for (var i = sign; i < text.Length || outputDigits < newPoint; i++)
        {
            if (i < text.Length && text[i] == '.') continue;
            if (outputDigits == newPoint && outputDigits < digitCount) builder.Append('.');
            if (i >= firstNonzero || outputDigits >= newPoint - 1) builder.Append(i < text.Length ? text[i] : '0');
            outputDigits++;
        }
        while (builder.Length > 0 && builder[builder.Length - 1] == '0' && newPoint < digitCount) builder.Length--;
        if (builder.Length > 0 && builder[builder.Length - 1] == '.') builder.Length--;
        return builder.Append('%').ToString();
    }
}
