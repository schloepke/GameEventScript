// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Globalization;
using System.Text;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime.Values;

namespace StepH.GameEventScript.Runtime;

internal static class TextNumberCast
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

        var unit = GameEventScriptBytecodeInstructionUnit.UnitNone;
        var scaled = percentageMagnitude || text[end - 1] == '%';
        switch (text[end - 1])
        {
            case '%': end--; break;
            case 'm': unit = GameEventScriptBytecodeInstructionUnit.UnitMeter; end--; break;
            case 's': unit = GameEventScriptBytecodeInstructionUnit.UnitSecond; end--; break;
            case '°': unit = GameEventScriptBytecodeInstructionUnit.UnitDegree; end--; break;
        }
        if (percentage && unit != GameEventScriptBytecodeInstructionUnit.UnitNone) return null;
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

        var power = exponent - fractionalDigits + digits - last - 1 - (scaled ? 2 : 0);
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
        if (!underscores && !grouping && !scaled)
        {
            if (!double.TryParse(text.AsSpan(start, end - start), DecimalStyles, CultureInfo.InvariantCulture, out number)) return null;
        }
        else
        {
            // Percentage scaling adjusts the decimal exponent before the single binary64 rounding.
            var capacity = mantissaEnd - start + 24;
            Span<char> buffer = capacity <= 256 ? stackalloc char[256] : new char[capacity];
            var count = 0;
            for (var i = start; i < mantissaEnd; i++)
                if (text[i] is not ('_' or ',')) buffer[count++] = text[i];
            buffer[count++] = 'e';
            (exponent - (scaled ? 2 : 0)).TryFormat(buffer.Slice(count), out var written, provider: CultureInfo.InvariantCulture);
            count += written;
            if (!double.TryParse(buffer.Slice(0, count), DecimalStyles, CultureInfo.InvariantCulture, out number)) return null;
        }
        return percentage
            ? double.IsFinite(number) ? GesValue.GesPercentage(number) : GesValue.GesNothing()
            : GesValue.GesFloat(number, unit);
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
        if (value.Kind == GameEventScriptBytecodeTypeKind.Text) return Read(value.TextValue, percentage: true) ?? GesValue.GesNothing();
        if (value.Kind == GameEventScriptBytecodeTypeKind.Percentage) return value;
        var ratio = value.AsNumeric;
        return !value.HasUnit && double.IsFinite(ratio) ? GesValue.GesPercentage(ratio) : GesValue.GesNothing();
    }

    internal static string Format(long value, GameEventScriptBytecodeInstructionUnit unit)
    {
        var text = value.ToString(CultureInfo.InvariantCulture);
        return unit.IsNumericUnit() ? text + unit.ToSuffix() : text;
    }

    internal static string Format(double value, GameEventScriptBytecodeInstructionUnit unit)
    {
        var text = GameEventScriptNumber.FormatCanonicalFloat(value);
        return unit.IsNumericUnit() ? text + unit.ToSuffix() : text;
    }

    internal static string FormatPercentage(double ratio)
    {
        if (ratio == 0) return "0%";
        // Format the ratio's exact Number view, then shift its decimal exponent without binary arithmetic.
        var text = GameEventScriptNumber.CanRepresentAsInteger(ratio)
            ? ((long)ratio).ToString(CultureInfo.InvariantCulture)
            : GameEventScriptNumber.FormatCanonicalFloat(ratio);
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
