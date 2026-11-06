// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace GameEventScript.Syntax;

// The explicit data forms such as `:Range(from: 1, to: 5)`: their lowercase type names and parameters.
internal static class GesLiteralForms
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
            _ => new[]
            {
                "value"
            }
        };
        if (labels.Count > parameters.Length || labels.Count == 0 && type != "nothing")
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
        if (type != "nothing" && !used[0])
            return null;
        return result;
    }

    // A built-in form name is the capitalized spelling of its type, such as `Range`.
    internal static bool IsBuiltinName(string name)
    {
        var builtin = name.ToLowerInvariant();
        return IsType(builtin) && name == char.ToUpperInvariant(builtin[0]) + builtin.Substring(1);
    }

    // A record type name of a document schema: a type name that names no built-in type.
    internal static bool IsRecordTypeName(string name) => GesText.IsTypeName(name) && !IsBuiltinName(name);
}
