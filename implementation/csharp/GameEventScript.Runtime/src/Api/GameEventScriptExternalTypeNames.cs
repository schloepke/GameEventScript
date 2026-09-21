// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Runtime;

namespace GameEventScript.Api;

internal static class GameEventScriptExternalTypeNames
{
    public static string ToTypeName(GameEventScriptBytecodeTypeKind kind, GameEventScriptBytecodeInstructionUnit? unit)
    {
        unit = unit is { } numericUnit && numericUnit.IsNumericUnit() ? numericUnit : null;

        if (unit is not null && kind is not (GameEventScriptBytecodeTypeKind.Float or GameEventScriptBytecodeTypeKind.Vector or GameEventScriptBytecodeTypeKind.Point))
        {
            throw new ArgumentException($"External GameEventScript type '{kind}' cannot declare a numeric unit.", nameof(unit));
        }

        return kind switch
        {
            GameEventScriptBytecodeTypeKind.Nothing => "Nothing",
            GameEventScriptBytecodeTypeKind.Tag => "Tag",
            GameEventScriptBytecodeTypeKind.Text => "Text",
            GameEventScriptBytecodeTypeKind.Percentage => "Percentage",
            GameEventScriptBytecodeTypeKind.Vector => "Vector",
            GameEventScriptBytecodeTypeKind.Point => "Point",
            GameEventScriptBytecodeTypeKind.Float => unit is null ? "Number" : $"Quantity({unit.Value.ToSuffix()})",
            GameEventScriptBytecodeTypeKind.Boolean => "Boolean",
            GameEventScriptBytecodeTypeKind.Series => "Series",
            GameEventScriptBytecodeTypeKind.Range => "Range",
            GameEventScriptBytecodeTypeKind.Handler => "Handler",
            GameEventScriptBytecodeTypeKind.List => "List",
            GameEventScriptBytecodeTypeKind.Map => "Map",
            GameEventScriptBytecodeTypeKind.Dice => "Dice",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown GameEventScript value kind.")
        };
    }

    public static (GameEventScriptBytecodeTypeKind? Kind, GameEventScriptBytecodeInstructionUnit? Unit) GetKindAndUnit(string typeName)
    {
        if (GameEventScriptBytecodeInstructionUnits.ParseSourceQuantityTypeName(typeName) is { } unit)
        {
            return (GameEventScriptBytecodeTypeKind.Float, unit);
        }

        return typeName switch
        {
            "Nothing" => (GameEventScriptBytecodeTypeKind.Nothing, null),
            "Tag" => (GameEventScriptBytecodeTypeKind.Tag, null),
            "Text" => (GameEventScriptBytecodeTypeKind.Text, null),
            "Percentage" => (GameEventScriptBytecodeTypeKind.Percentage, null),
            "Vector" => (GameEventScriptBytecodeTypeKind.Vector, null),
            "Point" => (GameEventScriptBytecodeTypeKind.Point, null),
            "Number" => (GameEventScriptBytecodeTypeKind.Float, null),
            "Boolean" => (GameEventScriptBytecodeTypeKind.Boolean, null),
            "Series" => (GameEventScriptBytecodeTypeKind.Series, null),
            "Range" => (GameEventScriptBytecodeTypeKind.Range, null),
            "Handler" => (GameEventScriptBytecodeTypeKind.Handler, null),
            "List" => (GameEventScriptBytecodeTypeKind.List, null),
            "Map" => (GameEventScriptBytecodeTypeKind.Map, null),
            "Dice" => (GameEventScriptBytecodeTypeKind.Dice, null),
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

        if (!GameEventScriptText.IsTypeName(normalized))
        {
            throw new ArgumentException($"External GameEventScript type name ':{normalized}' must start with an upper-case ASCII letter and contain ASCII letters or digits.");
        }

        return normalized;
    }

    public static string NormalizeIdentifier(string? name, string parameterName)
    {
        var normalized = NormalizeName(name, parameterName);
        if (!GameEventScriptText.IsArgumentLabel(normalized))
        {
            throw new ArgumentException($"External GameEventScript identifier '{normalized}' must start with a lower-case ASCII letter and contain ASCII letters or digits.");
        }

        return normalized;
    }

    public static string NormalizeExtensionName(string? name)
    {
        var normalized = NormalizeName(name, "name");
        if (!GameEventScriptText.IsTagName(normalized))
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

        GameEventScriptText.RequireValidUnicode(name, parameterName);
        var normalized = GameEventScriptText.TrimAsciiWhitespace(name);
        if (normalized.Length == 0) throw new ArgumentException("Name must not be null or whitespace.", parameterName);
        return normalized;
    }
}
