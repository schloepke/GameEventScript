// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Globalization;
using System.Text;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using GameEventScript.Syntax;

namespace GameEventScript.Runtime;

internal static class GesTextNumberCast
{
    // Null distinguishes failed recognition from a successfully recognized NaN/nothing value.
    internal static GesValue? Read(string text, bool percentage = false, bool allowGrouping = true, bool percentageMagnitude = false)
        => Value(GesNumberLiteral.Read(text, 0, text.Length, percentage, allowGrouping, percentageMagnitude));

    internal static GesValue? Read(string text, int start, int length, bool percentage = false, bool allowGrouping = true, bool percentageMagnitude = false)
        => Value(GesNumberLiteral.Read(text, start, length, percentage, allowGrouping, percentageMagnitude));

    internal static GesValue? Value(in GesNumberLiteral number) => number.Kind switch
    {
        GesNumberLiteralKind.Integer => GesValue.GesInteger(number.IntegerValue, number.Unit),
        GesNumberLiteralKind.Float => GesValue.GesFloat(number.FloatValue, number.Unit),
        GesNumberLiteralKind.Percentage => GesValue.GesPercentage(number.FloatValue),
        GesNumberLiteralKind.Nothing => GesValue.GesNothing(),
        _ => null
    };

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
