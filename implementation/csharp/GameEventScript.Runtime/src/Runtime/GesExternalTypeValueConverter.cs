// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;

namespace GameEventScript.Runtime;

internal static class GesExternalTypeValueConverter
{
    internal static GesValue CoerceToDeclaredType(in GesValue value, GesExternalTypeParameterDefinition definition)
        => CoerceToDeclaredType(in value, definition.Kind, definition.Unit, definition.TypeName);

    internal static GesValue CoerceToDeclaredType(in GesValue value, GesExternalTypeFieldDefinition definition)
        => CoerceToDeclaredType(in value, definition.Kind, definition.Unit, definition.TypeName);

    private static GesValue CoerceToDeclaredType(in GesValue value, GesBytecodeTypeKind? kind, GesQuantityUnit? unit, string typeName)
    {
        if (kind == GesBytecodeTypeKind.Vector &&
            unit is not null && unit != GesQuantityUnit.UnitNone &&
            value.ObjectValue is GesSpatialValue vector)
        {
            var result = new GesValue();
            result.SetVector(vector.X, vector.Y, vector.Z, unit.ToStoredUnit());
            return result;
        }

        if (kind == GesBytecodeTypeKind.Point &&
            unit is not null && unit != GesQuantityUnit.UnitNone &&
            value.ObjectValue is GesSpatialValue point)
        {
            var result = new GesValue();
            result.SetPoint(point.X, point.Y, point.Z, unit.ToStoredUnit());
            return result;
        }

        if (kind == GesBytecodeTypeKind.Float &&
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
            case "Number":
                result.SetFloat(value.AsNumeric);
                return result;
            case "Boolean":
                result.SetBoolean(value.IsTrue);
                return result;
            // Unit names are checked only after the constant type names, which keeps those cases a direct dispatch.
            case var unitName when unitName != "\u00B0" && GesQuantityUnits.ParseTypeName(unitName) is { } unit:
                result.SetFloat(value.AsNumeric, unit);
                return result;
            default:
                return value;
        }
    }

    private static GesValue CoerceToTag(in GesValue value)
    {
        var result = new GesValue();
        if (value.Kind == GesBytecodeTypeKind.Boolean)
        {
            result.SetTag(value.IsTrue ? "true" : "false");
            return result;
        }

        var text = value.ToText;
        if (value.Kind == GesBytecodeTypeKind.Text)
        {
            if (GesTagRules.NormalizeTextCast(text) is { } normalized)
            {
                result.SetTag(normalized);
                return result;
            }

            result.SetNothing();
            return result;
        }

        if (GesTagRules.IsValidTagName(text))
        {
            result.SetTag(text);
            return result;
        }

        result.SetNothing();
        return result;
    }

}
