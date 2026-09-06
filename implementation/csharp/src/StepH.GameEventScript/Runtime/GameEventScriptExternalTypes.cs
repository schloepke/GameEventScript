// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime.Values;

namespace StepH.GameEventScript.Runtime;

internal static class GameEventScriptExternalTypeValueConverter
{
    internal static GesValue CoerceToDeclaredType(in GesValue value, GameEventScriptExternalTypeParameterDefinition definition)
        => CoerceToDeclaredType(in value, definition.Kind, definition.Unit, definition.TypeName);

    internal static GesValue CoerceToDeclaredType(in GesValue value, GameEventScriptExternalTypeFieldDefinition definition)
        => CoerceToDeclaredType(in value, definition.Kind, definition.Unit, definition.TypeName);

    private static GesValue CoerceToDeclaredType(in GesValue value, GameEventScriptBytecodeTypeKind? kind, GameEventScriptBytecodeInstructionUnit? unit, string typeName)
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
            case "Nothing":
                result.SetNothing();
                return result;
            case "Tag":
                return CoerceToTag(in value);
            case "Text":
                result.SetText(value.ToText);
                return result;
            case "Percentage":
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
            case "Number":
                result.SetFloat(value.AsNumeric);
                return result;
            case "Boolean":
                result.SetBoolean(value.IsTrue);
                return result;
            default:
                return value;
        }
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
