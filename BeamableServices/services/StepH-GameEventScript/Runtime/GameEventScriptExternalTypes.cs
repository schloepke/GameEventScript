#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Linq;
using StepH.GameEventScript.Api;

namespace StepH.GameEventScript.Runtime;

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
        if (GameEventScriptBytecodeInstructionUnits.TryParseTypeName(typeName, out var unit))
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

        if (!IsLetterOnlyLowerStart(normalized))
        {
            throw new ArgumentException($"External GameEventScript type name ':{normalized}' must start with a lower-case letter and contain letters only.");
        }

        return normalized;
    }

    public static string NormalizeIdentifier(string? name, string parameterName)
    {
        var normalized = NormalizeName(name, parameterName);
        if (!IsIdentifier(normalized))
        {
            throw new ArgumentException($"External GameEventScript identifier '{normalized}' must start with a lower-case letter, contain letters only, and may end with _<index>.");
        }

        return normalized;
    }

    private static string NormalizeName(string? name, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name must not be null or whitespace.", parameterName);
        }

        return name.Trim();
    }

    private static bool IsLetterOnlyLowerStart(string value)
        => value.Length > 0 &&
           char.IsLower(value[0]) &&
           value.All(char.IsLetter);

    private static bool IsIdentifier(string value)
    {
        if (value.Length == 0 || !char.IsLower(value[0]))
        {
            return false;
        }

        var underscore = value.LastIndexOf('_');
        if (underscore < 0)
        {
            return value.All(char.IsLetter);
        }

        if (underscore == 0 || underscore == value.Length - 1)
        {
            return false;
        }

        var prefix = value[..underscore];
        var suffix = value[(underscore + 1)..];
        if (!prefix.All(char.IsLetter) || !suffix.All(char.IsDigit))
        {
            return false;
        }

        return suffix.Length == 1 || suffix[0] != '0';
    }
}

internal static class GameEventScriptExternalTypeValueConverter
{
    public static GameEventScriptValue CoerceToDeclaredType(GameEventScriptValue value, GameEventScriptExternalTypeFieldDefinition definition)
        => CoerceToDeclaredType(value, definition.Kind, definition.Unit, definition.TypeName);

    public static GameEventScriptValue CoerceToDeclaredType(GameEventScriptValue value, GameEventScriptExternalTypeParameterDefinition definition)
        => CoerceToDeclaredType(value, definition.Kind, definition.Unit, definition.TypeName);

    public static GameEventScriptValue CoerceToDeclaredType(GameEventScriptValue value, string typeName)
    {
        var (kind, unit) = GameEventScriptExternalTypeNames.GetKindAndUnit(typeName);
        return CoerceToDeclaredType(value, kind, unit, typeName);
    }

    private static GameEventScriptValue CoerceToDeclaredType(
        GameEventScriptValue value,
        GameEventScriptBytecodeTypeKind? kind,
        GameEventScriptBytecodeInstructionUnit? unit,
        string typeName)
    {
        if (kind == GameEventScriptBytecodeTypeKind.Vector &&
            unit is not null)
        {
            return GameEventScriptValueFactory.GesVector(value.X, value.Y, value.Z, unit.ToStoredUnit());
        }

        if (kind == GameEventScriptBytecodeTypeKind.Point &&
            unit is not null)
        {
            return GameEventScriptValueFactory.GesPoint(value.X, value.Y, value.Z, unit.ToStoredUnit());
        }

        if (kind == GameEventScriptBytecodeTypeKind.Float &&
            unit is not null)
        {
            return GameEventScriptValueFactory.GesFloat(value.AsNumber(), unit.ToStoredUnit());
        }

        return CoerceToDeclaredTypeCore(value, typeName);
    }

    private static GameEventScriptValue CoerceToDeclaredTypeCore(GameEventScriptValue value, string typeName)
    {
        return typeName switch
        {
            "nothing" => GameEventScriptValueFactory.GesNothing(),
            "tag" => CoerceToTag(value),
            "text" => GameEventScriptValueFactory.GesText(value.AsText()),
            "percentage" => GameEventScriptValueFactory.GesPercentage(value.AsNumber()),
            "degree" => GameEventScriptValueFactory.GesFloat(value.AsNumber(), GameEventScriptBytecodeInstructionUnit.UnitDegree),
            "meter" => GameEventScriptValueFactory.GesFloat(value.AsNumber(), GameEventScriptBytecodeInstructionUnit.UnitMeter),
            "second" => GameEventScriptValueFactory.GesFloat(value.AsNumber(), GameEventScriptBytecodeInstructionUnit.UnitSecond),
            "number" => GameEventScriptValueFactory.GesFloat(value.AsNumber()),
            "boolean" => GameEventScriptValueFactory.GesBoolean(value.AsBoolean()),
            "map" => GameEventScriptValueFactory.GesMap(value.AsMap()),
            "list" => GameEventScriptValueFactory.GesList(value.AsList()),
            _ => value
        };
    }

    private static GameEventScriptValue CoerceToTag(GameEventScriptValue value)
    {
        if (value.Kind == GameEventScriptBytecodeTypeKind.Boolean)
        {
            return GameEventScriptValueFactory.GesTag(value.AsBoolean() ? "true" : "false");
        }

        var text = value.AsText();
        if (value.Kind == GameEventScriptBytecodeTypeKind.Text)
        {
            return GameEventScriptTagRules.TryNormalizeTextCast(text, out var normalized)
                ? GameEventScriptValueFactory.GesTag(normalized)
                : GameEventScriptValueFactory.GesNothing();
        }

        return GameEventScriptTagRules.IsValidTagName(text)
            ? GameEventScriptValueFactory.GesTag(text)
            : GameEventScriptValueFactory.GesNothing();
    }

}
