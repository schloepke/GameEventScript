#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using StepH.GameEventScript.Api;

namespace StepH.GameEventScript.Runtime;

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
            return GameEventScriptTagRules.NormalizeTextCast(text) is { } normalized
                ? GameEventScriptValueFactory.GesTag(normalized)
                : GameEventScriptValueFactory.GesNothing();
        }

        return GameEventScriptTagRules.IsValidTagName(text)
            ? GameEventScriptValueFactory.GesTag(text)
            : GameEventScriptValueFactory.GesNothing();
    }

}
