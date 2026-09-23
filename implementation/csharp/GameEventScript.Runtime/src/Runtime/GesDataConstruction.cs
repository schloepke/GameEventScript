// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using GameEventScript.Runtime.VM;
using static GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace GameEventScript.Runtime;

internal static class GesDataConstruction
{
    internal static bool IsType(string name) => name is "nothing" or "number" or "percentage" or "boolean" or "text" or "tag" or "list" or "map" or "dice" or "vector" or "point" or "range" or "series" or "message" or "handler" or "record";
    // Returns parameter positions in evaluation order, without reordering evaluation of source arguments.
    internal static int[]? Positions(string type, IReadOnlyList<string> labels)
    {
        var parameters = type switch
        {
            "number" => new[]
            {
                "value",
                "unit"
            },
            "range" => new[]
            {
                "from",
                "to",
                "step"
            },
            "series" => new[]
            {
                "kind",
                "offset"
            },
            "record" => new[]
            {
                "type",
                "fields"
            },
            "message" => new[]
            {
                "value",
                "tags"
            },
            "vector" or "point" => new[]
            {
                "x",
                "y",
                "z"
            },
            _ => new[]
            {
                "value"
            }
        };
        if (labels.Count > parameters.Length || labels.Count == 0 && type is not ("nothing" or "vector" or "point"))
            return null;
        var result = new int[labels.Count];
        var used = new bool[parameters.Length];
        for (var i = 0; i < labels.Count; i++)
        {
            var position = labels[i] == "_" ? i : Array.IndexOf(parameters, labels[i]);
            if (position < 0 || used[position])
                return null;
            used[position] = true;
            result[i] = position;
        }

        if (type == "record" && (!used[0] || !used[1]) || type == "range" && labels.Count > 1 && (!used[0] || !used[1]))
            return null;
        if (type is not ("nothing" or "vector" or "point") && !used[0])
            return null;
        return result;
    }

    internal static GesValue Create(string type, string[] labels, GesValue[] arguments, GesVmState state, GameEventScriptContext context)
    {
        var positions = Positions(type, labels);
        if (positions is null || arguments.Length != labels.Length)
            return default;
        var values = new GesValue[Math.Max(3, arguments.Length)];
        for (var i = 0; i < arguments.Length; i++)
            values[positions[i]] = arguments[i];
        var first = values[0];
        if (type == "nothing")
            return default;
        if (type is "vector" or "point")
        {
            var unit = arguments.Length == 0 ? GameEventScriptBytecodeInstructionUnit.UnitNone : arguments[0].Unit;
            foreach (var argument in arguments)
                if (argument.Kind is not (Integer or Float) || argument.Unit != unit)
                    return default;
            return type == "vector"
                ? GesValue.GesVector(values[0].AsNumeric, values[1].IsNothing ? 0 : values[1].AsNumeric, values[2].IsNothing ? 0 : values[2].AsNumeric, unit)
                : GesValue.GesPoint(values[0].IsNothing ? 0 : values[0].AsNumeric, values[1].IsNothing ? 0 : values[1].AsNumeric, values[2].IsNothing ? 0 : values[2].AsNumeric, unit);
        }

        if (type == "number")
        {
            var number = GameEventScriptNumber.Cast(first);
            if (arguments.Length == 1)
                return number;
            if (values[1].Kind != Text)
                return default;
            var unit = values[1].TextValue switch
            {
                "" or "none" => GameEventScriptBytecodeInstructionUnit.UnitNone,
                "s" or "second" => GameEventScriptBytecodeInstructionUnit.UnitSecond,
                "m" or "meter" => GameEventScriptBytecodeInstructionUnit.UnitMeter,
                "°" or "degree" => GameEventScriptBytecodeInstructionUnit.UnitDegree,
                _ => GameEventScriptBytecodeInstructionUnit.UnitInvalid
            };
            if (unit == GameEventScriptBytecodeInstructionUnit.UnitInvalid || number.Kind is not (Integer or Float))
                return default;
            if (number.Unit != GameEventScriptBytecodeInstructionUnit.UnitNone && number.Unit != unit)
                return default;
            return number.Kind == Integer ? GesValue.GesInteger(number.IntegerValue, unit) : GesValue.GesFloat(number.FloatValue, unit);
        }

        if (type == "record")
        {
            if (first.Kind != Text || !GameEventScriptText.IsTypeName(first.TextValue) || values[1].Kind != Map || values[1].ObjectValue is not GesValueMap map)
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
                "fibonacci" => GesSeries.Fibonacci(),
                "factorial" => GesSeries.Factorial(),
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
            "boolean" => GameEventScriptBytecodeTypeKind.Boolean,
            "text" => Text,
            "tag" => Tag,
            "list" => List,
            "map" => Map,
            "dice" => Dice,
            "vector" => Vector,
            "point" => Point,
            "range" => GameEventScriptBytecodeTypeKind.Range,
            "series" => Series,
            "message" => Message,
            "handler" => Handler,
            _ => Nothing
        };
        return GesVmRegisterTypeCastCheck.GesVmCast(first, kind, state, 0, context);
    }

    private static void AddTags(GesValue value, List<string> tags)
    {
        if (value.Kind == List && value.ObjectValue is GesValue[] list)
            foreach (var item in list)
                AddTags(item, tags);
        else
            tags.Add(value.AsText());
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
