// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Runtime;

namespace GameEventScript.Api;

internal static class GesExternalTypeNames
{
    public static string ToTypeName(GesBytecodeTypeKind kind, GesQuantityUnit? unit)
    {
        unit = unit is { } numericUnit && numericUnit.IsNumericUnit() ? numericUnit : null;

        if (unit is not null && kind is not (GesBytecodeTypeKind.Float or GesBytecodeTypeKind.Vector or GesBytecodeTypeKind.Point))
        {
            throw new ArgumentException($"External GameEventScript type '{kind}' cannot declare a numeric unit.", nameof(unit));
        }

        return kind switch
        {
            GesBytecodeTypeKind.Nothing => "Nothing",
            GesBytecodeTypeKind.Tag => "Tag",
            GesBytecodeTypeKind.Text => "Text",
            GesBytecodeTypeKind.Percentage => "Percentage",
            GesBytecodeTypeKind.Vector => "Vector",
            GesBytecodeTypeKind.Point => "Point",
            GesBytecodeTypeKind.Float => unit is null ? "Number" : $"Quantity({unit.Value.ToSuffix()})",
            GesBytecodeTypeKind.Boolean => "Boolean",
            GesBytecodeTypeKind.Series => "Series",
            GesBytecodeTypeKind.Range => "Range",
            GesBytecodeTypeKind.Handler => "Handler",
            GesBytecodeTypeKind.List => "List",
            GesBytecodeTypeKind.Map => "Map",
            GesBytecodeTypeKind.Dice => "Dice",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown GameEventScript value kind.")
        };
    }

    public static (GesBytecodeTypeKind? Kind, GesQuantityUnit? Unit) GetKindAndUnit(string typeName)
    {
        if (GesQuantityUnits.ParseSourceQuantityTypeName(typeName) is { } unit)
        {
            return (GesBytecodeTypeKind.Float, unit);
        }

        return typeName switch
        {
            "Nothing" => (GesBytecodeTypeKind.Nothing, null),
            "Tag" => (GesBytecodeTypeKind.Tag, null),
            "Text" => (GesBytecodeTypeKind.Text, null),
            "Percentage" => (GesBytecodeTypeKind.Percentage, null),
            "Vector" => (GesBytecodeTypeKind.Vector, null),
            "Point" => (GesBytecodeTypeKind.Point, null),
            "Number" => (GesBytecodeTypeKind.Float, null),
            "Boolean" => (GesBytecodeTypeKind.Boolean, null),
            "Series" => (GesBytecodeTypeKind.Series, null),
            "Range" => (GesBytecodeTypeKind.Range, null),
            "Handler" => (GesBytecodeTypeKind.Handler, null),
            "List" => (GesBytecodeTypeKind.List, null),
            "Map" => (GesBytecodeTypeKind.Map, null),
            "Dice" => (GesBytecodeTypeKind.Dice, null),
            _ => (null, null)
        };
    }

    public static string NormalizeTypeName(string? typeName)
    {
        var normalized = NormalizeName(typeName, "typeName");
        if (normalized.StartsWith(":", StringComparison.Ordinal))
        {
            normalized = normalized[1..];
        }

        if (!GesText.IsTypeName(normalized))
        {
            throw new ArgumentException($"External GameEventScript type name ':{normalized}' must start with an upper-case ASCII letter and contain ASCII letters or digits.");
        }

        return normalized;
    }

    public static string NormalizeIdentifier(string? name, string parameterName)
    {
        var normalized = NormalizeName(name, parameterName);
        if (!GesText.IsArgumentLabel(normalized))
        {
            throw new ArgumentException($"External GameEventScript identifier '{normalized}' must start with a lower-case ASCII letter and contain ASCII letters or digits.");
        }

        return normalized;
    }

    public static string NormalizeExtensionName(string? name)
    {
        var normalized = NormalizeName(name, "name");
        if (!GesText.IsTagName(normalized))
        {
            throw new ArgumentException($"GameEventScript extension name '{normalized}' must start with a lower-case ASCII letter and contain ASCII letters or digits.", nameof(name));
        }

        return normalized;
    }

    private static string NormalizeName(string? name, string parameterName)
    {
        if (name is null)
        {
            throw new ArgumentException("Name must not be null or whitespace.", parameterName);
        }

        GesText.RequireValidUnicode(name, parameterName);
        var normalized = GesText.TrimAsciiWhitespace(name);
        if (normalized.Length == 0) throw new ArgumentException("Name must not be null or whitespace.", parameterName);
        return normalized;
    }
}
