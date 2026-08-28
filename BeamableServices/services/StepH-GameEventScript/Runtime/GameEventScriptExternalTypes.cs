#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime.Values;

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

    internal static GesValue CoerceToDeclaredType(in GesValue value, GameEventScriptExternalTypeParameterDefinition definition)
        => CoerceToDeclaredType(in value, definition.Kind, definition.Unit, definition.TypeName);

    internal static GesValue CoerceToDeclaredType(in GesValue value, GameEventScriptExternalTypeFieldDefinition definition)
        => CoerceToDeclaredType(in value, definition.Kind, definition.Unit, definition.TypeName);

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

    private static GesValue CoerceToDeclaredType(
        in GesValue value,
        GameEventScriptBytecodeTypeKind? kind,
        GameEventScriptBytecodeInstructionUnit? unit,
        string typeName)
    {
        if (kind == GameEventScriptBytecodeTypeKind.Vector &&
            unit is not null &&
            value.ObjectValue is GesValueVectorPoint vector)
        {
            var result = new GesValue();
            result.SetVector(vector.X, vector.Y, vector.Z, unit.ToStoredUnit());
            return result;
        }

        if (kind == GameEventScriptBytecodeTypeKind.Point &&
            unit is not null &&
            value.ObjectValue is GesValueVectorPoint point)
        {
            var result = new GesValue();
            result.SetPoint(point.X, point.Y, point.Z, unit.ToStoredUnit());
            return result;
        }

        if (kind == GameEventScriptBytecodeTypeKind.Float &&
            unit is not null)
        {
            var result = new GesValue();
            result.SetFloat(value.AsNumeric, unit.ToStoredUnit());
            return result;
        }

        return CoerceToDeclaredTypeCore(in value, typeName);
    }

    private static GesValue CoerceToDeclaredTypeCore(in GesValue value, string typeName)
    {
        var result = new GesValue();
        switch (typeName)
        {
            case "nothing":
                result.SetNothing();
                return result;
            case "tag":
                return CoerceToTag(in value);
            case "text":
                result.SetText(value.ToText);
                return result;
            case "percentage":
                result.SetPercentage(value.AsNumeric);
                return result;
            case "degree":
                result.SetFloat(value.AsNumeric, GameEventScriptBytecodeInstructionUnit.UnitDegree);
                return result;
            case "meter":
                result.SetFloat(value.AsNumeric, GameEventScriptBytecodeInstructionUnit.UnitMeter);
                return result;
            case "second":
                result.SetFloat(value.AsNumeric, GameEventScriptBytecodeInstructionUnit.UnitSecond);
                return result;
            case "number":
                result.SetFloat(value.AsNumeric);
                return result;
            case "boolean":
                result.SetBoolean(value.IsTrue);
                return result;
            default:
                return value;
        }
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

    private static GesValue CoerceToTag(in GesValue value)
    {
        var result = new GesValue();
        if (value.Kind == GameEventScriptBytecodeTypeKind.Boolean)
        {
            result.SetTag(value.IsTrue ? "true" : "false");
            return result;
        }

        var text = value.ToText;
        if (value.Kind == GameEventScriptBytecodeTypeKind.Text)
        {
            if (GameEventScriptTagRules.NormalizeTextCast(text) is { } normalized)
            {
                result.SetTag(normalized);
                return result;
            }

            result.SetNothing();
            return result;
        }

        if (GameEventScriptTagRules.IsValidTagName(text))
        {
            result.SetTag(text);
            return result;
        }

        result.SetNothing();
        return result;
    }

}
