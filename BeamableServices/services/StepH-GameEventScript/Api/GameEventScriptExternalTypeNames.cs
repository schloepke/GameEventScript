// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using StepH.GameEventScript.Runtime;

namespace StepH.GameEventScript.Api;

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
            GameEventScriptBytecodeTypeKind.Nothing => "nothing",
            GameEventScriptBytecodeTypeKind.Tag => "tag",
            GameEventScriptBytecodeTypeKind.Text => "text",
            GameEventScriptBytecodeTypeKind.Percentage => "percentage",
            GameEventScriptBytecodeTypeKind.Vector => "vector",
            GameEventScriptBytecodeTypeKind.Point => "point",
            GameEventScriptBytecodeTypeKind.Float => unit?.ToTypeName() ?? "number",
            GameEventScriptBytecodeTypeKind.Boolean => "boolean",
            GameEventScriptBytecodeTypeKind.Series => "series",
            GameEventScriptBytecodeTypeKind.Range => "range",
            GameEventScriptBytecodeTypeKind.Handler => "handler",
            GameEventScriptBytecodeTypeKind.List => "list",
            GameEventScriptBytecodeTypeKind.Map => "map",
            GameEventScriptBytecodeTypeKind.Dice => "dice",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown GameEventScript value kind.")
        };
    }

    public static (GameEventScriptBytecodeTypeKind? Kind, GameEventScriptBytecodeInstructionUnit? Unit) GetKindAndUnit(string typeName)
    {
        if (GameEventScriptBytecodeInstructionUnits.ParseTypeName(typeName) is { } unit)
        {
            return (GameEventScriptBytecodeTypeKind.Float, unit);
        }

        return typeName switch
        {
            "nothing" => (GameEventScriptBytecodeTypeKind.Nothing, null),
            "tag" => (GameEventScriptBytecodeTypeKind.Tag, null),
            "text" => (GameEventScriptBytecodeTypeKind.Text, null),
            "percentage" => (GameEventScriptBytecodeTypeKind.Percentage, null),
            "vector" => (GameEventScriptBytecodeTypeKind.Vector, null),
            "point" => (GameEventScriptBytecodeTypeKind.Point, null),
            "number" => (GameEventScriptBytecodeTypeKind.Float, null),
            "boolean" => (GameEventScriptBytecodeTypeKind.Boolean, null),
            "series" => (GameEventScriptBytecodeTypeKind.Series, null),
            "range" => (GameEventScriptBytecodeTypeKind.Range, null),
            "handler" => (GameEventScriptBytecodeTypeKind.Handler, null),
            "list" => (GameEventScriptBytecodeTypeKind.List, null),
            "map" => (GameEventScriptBytecodeTypeKind.Map, null),
            "dice" => (GameEventScriptBytecodeTypeKind.Dice, null),
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
            throw new ArgumentException($"External GameEventScript type name ':{normalized}' must start with a lower-case letter and contain letters only.");
        }

        return normalized;
    }

    public static string NormalizeIdentifier(string? name, string parameterName)
    {
        var normalized = NormalizeName(name, parameterName);
        if (!GameEventScriptText.IsIdentifier(normalized))
        {
            throw new ArgumentException($"External GameEventScript identifier '{normalized}' must start with a lower-case letter, contain letters only, and may end with _<index>.");
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
