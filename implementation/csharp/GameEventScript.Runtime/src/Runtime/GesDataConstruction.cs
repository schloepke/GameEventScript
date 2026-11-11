// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using GameEventScript.Runtime.Vm;
using GameEventScript.Syntax;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime;

internal static class GesDataConstruction
{
    internal static bool IsType(string name) => GesLiteralForms.IsType(name);

    internal static int[]? Positions(string type, IReadOnlyList<string> labels) => GesLiteralForms.Positions(type, labels);

    internal static GesValue Create(string type, string[] labels, GesValue[] arguments, GesVirtualMachine? state, GesContext? context)
    {
        var positions = Positions(type, labels);
        return positions is null ? default : Create(type, positions, arguments, state, context);
    }

    // Positions come from Positions(type, labels), resolved once per constant type and label list.
    internal static GesValue Create(string type, int[] positions, GesValue[] arguments, GesVirtualMachine? state, GesContext? context)
    {
        if (arguments.Length != positions.Length)
            return default;
        var values = new GesValue[Math.Max(3, arguments.Length)];
        for (var i = 0; i < arguments.Length; i++)
            values[positions[i]] = arguments[i];
        var first = values[0];
        if (type == "nothing")
            return default;
        if (type == "number")
        {
            var number = GesNumber.Cast(first);
            if (arguments.Length == 1)
                return number;
            if (values[1].Kind != Text)
                return default;
            // The unit text accepts the same names as `:Quantity(...)`, plus the empty text for none.
            var unit = values[1].TextValue.Length == 0 ? GesQuantityUnit.UnitNone : GesQuantityUnits.ParseQuantityName(values[1].TextValue) ?? GesQuantityUnit.UnitInvalid;
            if (unit == GesQuantityUnit.UnitInvalid || number.Kind is not (Integer or Float))
                return default;
            if (number.Unit != GesQuantityUnit.UnitNone && number.Unit != unit)
                return default;
            return number.Kind == Integer ? GesValue.GesInteger(number.IntegerValue, unit) : GesValue.GesFloat(number.FloatValue, unit);
        }

        if (type == "record")
        {
            if (first.Kind != Text || !GesText.IsTypeName(first.TextValue) || values[1].Kind != Map || values[1].ObjectValue is not GesValueMap map)
                return default;
            var value = new GesValue();
            value.SetRecord(first.TextValue, map);
            return value;
        }

        if (type == "range" && arguments.Length > 1)
        {
            var last = values[1];
            var step = arguments.Length > 2 ? values[2] : GesValue.GesInteger(1);
            if (first.Kind is not (Integer or Float) || last.Kind is not (Integer or Float) || step.Kind is not (Integer or Float) || first.Unit != 0 || last.Unit != 0 || step.Unit != 0)
                return default;
            if (first.Kind == Integer && last.Kind == Integer && step.Kind == Integer)
                return GesValue.GesRange(first.IntegerValue, last.IntegerValue, step.IntegerValue);
            return GesValue.GesRange(first.AsNumeric, last.AsNumeric, step.AsNumeric);
        }

        if (type == "series" && first.Kind == Text)
        {
            if (arguments.Length > 1 && (values[1].Kind != Integer || values[1].Unit != 0 || values[1].IntegerValue < 0))
                return default;
            var series = first.TextValue switch
            {
                "fibonacci" => GesSeriesValue.Fibonacci(),
                "factorial" => GesSeriesValue.Factorial(),
                _ => null
            };
            return series is null ? default : GesValue.GesSeries(series.Drop(arguments.Length > 1 ? values[1].IntegerValue : 0));
        }

        if (type == "message" && arguments.Length == 2)
        {
            if (first.Message is not { } message)
                return default;
            var tags = new List<string>();
            AddTags(values[1], tags);
            try
            {
                return GesValue.GesMessage(message.WithTags(tags));
            }
            catch (ArgumentException)
            {
                return default;
            }
        }

        var kind = type switch
        {
            "percentage" => Percentage,
            "boolean" => GesBytecodeTypeKind.Boolean,
            "text" => Text,
            "tag" => Tag,
            "list" => List,
            "map" => Map,
            "dice" => Dice,
            "bytes" => Bytes,
            "range" => GesBytecodeTypeKind.Range,
            "series" => Series,
            "message" => Message,
            "handler" => Handler,
            _ => Nothing
        };
        return GesVirtualMachine.CastValue(first, kind, state, 0, context);
    }

    private static void AddTags(GesValue value, List<string> tags)
    {
        if (value.Kind == List && value.ObjectValue is GesValue[] list)
            foreach (var item in list)
                AddTags(item, tags);
        else
            // `_` is no tag name, so `nothing` makes the tag clause invalid.
            tags.Add(value.IsNothing ? "_" : value.AsText());
    }

    internal static GesValue Split(in GesValue input, in GesValue delimiter, bool whitespace)
    {
        if (input.Kind != Text || !whitespace && delimiter.Kind != Text)
            return default;
        var text = input.TextValue;
        var result = new List<GesValue>();
        if (whitespace)
        {
            var start = 0;
            for (var i = 0; i <= text.Length; i++)
            {
                if (i < text.Length && !IsWhitespace(text[i]))
                    continue;
                if (i > start)
                    result.Add(GesValue.GesText(text.Substring(start, i - start)));
                start = i + 1;
            }
        }
        else
        {
            var separator = delimiter.TextValue;
            if (separator.Length == 0)
                return default;
            var start = 0;
            while (true)
            {
                var end = text.IndexOf(separator, start, StringComparison.Ordinal);
                if (end < 0)
                    end = text.Length;
                result.Add(end == start ? default : GesValue.GesText(text.Substring(start, end - start)));
                if (end == text.Length)
                    break;
                start = end + separator.Length;
            }
        }

        var listValue = new GesValue();
        listValue.SetList(result.ToArray());
        return listValue;
    }

    // Fixed Unicode White_Space property, independent of the platform Unicode version.
    internal static bool IsWhitespace(char value) => value is >= '\u0009' and <= '\u000D' or '\u0020' or '\u0085' or '\u00A0' or '\u1680' or >= '\u2000' and <= '\u200A' or '\u2028' or '\u2029' or '\u202F' or '\u205F' or '\u3000';
}
