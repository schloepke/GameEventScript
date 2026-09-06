// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Globalization;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Runtime.Values;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeInstructionUnit;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.Runtime.VM;

internal static class GesVmRegisterTypeCastCheck
{
    internal static void GesVmCastUnit(this GesVmState vmState, ushort destinationRegister, in GesValue xValue, GameEventScriptBytecodeInstructionUnit unit)
    {
        var dst = GesVmCastUnit(in xValue, unit, vmState);
        vmState.SetValue(destinationRegister, in dst);
    }
    internal static GesValue GesVmCastUnit(in GesValue xValue, GameEventScriptBytecodeInstructionUnit unit, GesVmState state)
    {
        var dst = new GesValue();
        switch (xValue.Kind)
        {
            case Integer:
                if (unit is UnitNone || xValue.Unit is UnitNone || xValue.Unit == unit) dst.SetInteger(xValue.IntegerValue, unit);
                else dst.SetFloat(double.NaN);
                return dst;
            case Float:
                if ((unit is UnitNone || xValue.Unit is UnitNone || xValue.Unit == unit) && double.IsFinite(xValue.FloatValue)) dst.SetFloat(xValue.FloatValue, unit);
                else dst.SetFloat(double.NaN);
                return dst;
            case Vector:
                if (xValue.ObjectValue is GesValueVectorPoint vector && (unit is UnitNone || xValue.Unit is UnitNone || xValue.Unit == unit)) dst.SetVector(vector, unit);
                else dst.SetFloat(double.NaN);
                return dst;
            case Point:
                if (xValue.ObjectValue is GesValueVectorPoint point && (unit is UnitNone || xValue.Unit is UnitNone || xValue.Unit == unit)) dst.SetPoint(point, unit);
                else dst.SetFloat(double.NaN);
                return dst;
            default:
                dst.SetFloat(double.NaN);
                return dst;
        }
    }
    internal static void GesVmCheckUnit(this GesVmState vmState, ushort destinationRegister, in GesValue xValue, GameEventScriptBytecodeInstructionUnit unit)
    {
        vmState.SetBoolean(destinationRegister, xValue.Kind is Integer or Float or Vector or Point && xValue.Unit == unit);
    }
    internal static void GesVmCast(this GesVmState vmState, ushort destinationRegister, in GesValue xValue, GameEventScriptBytecodeTypeKind type, GameEventScriptContext? context = null)
    {
        var dst = GesVmCast(in xValue, type, vmState, destinationRegister, context);
        vmState.SetValue(destinationRegister, in dst);
    }
    internal static GesValue GesVmCast(in GesValue xValue, GameEventScriptBytecodeTypeKind type, GesVmState state, ushort destinationRegister, GameEventScriptContext? context = null)
    {
        var dst = new GesValue();
        switch (type)
        {
            case Nothing:
                dst.SetNothing();
                return dst;
            case GameEventScriptBytecodeTypeKind.Boolean:
                dst.SetBoolean(xValue.IsTrue);
                return dst;
            case Integer:
                var integerNumber = xValue.AsNumeric;
                if (GameEventScriptNumber.CanRepresentAsInteger(integerNumber)) dst.SetInteger((long)integerNumber, xValue.Kind is Integer or Float ? xValue.Unit : UnitNone);
                else dst.SetNothing();
                return dst;
            case Float:
                dst.SetFloat(xValue.AsNumeric, xValue.Kind is Integer or Float ? xValue.Unit : UnitNone);
                return dst;
            case Percentage:
                if (xValue.HasUnit)
                {
                    dst.SetNothing();
                    return dst;
                }

                var percentageNumber = xValue.AsNumeric;
                if (double.IsFinite(percentageNumber)) dst.SetPercentage(xValue.Kind is Integer || percentageNumber is > 1d or < -1d ? percentageNumber / 100d : percentageNumber);
                else dst.SetNothing();
                return dst;
            case Text:
                return CastText(in xValue);
            case Tag:
                return CastTag(in xValue);
            case Vector:
                return CastVectorOrPoint(in xValue, asPoint: false);
            case Point:
                return CastVectorOrPoint(in xValue, asPoint: true);
            case Dice:
                switch (xValue.Kind)
                {
                    case Dice:
                        return xValue;
                    case List when xValue.ObjectValue is GesValue[] list:
                    {
                        var dice = new int[list.Length];
                        for (var i = 0; i < list.Length; i++)
                        {
                            var item = list[i];
                            if (item.Kind is not Integer || item.IntegerValue <= 0 || item.IntegerValue > int.MaxValue)
                            {
                                dst.SetNothing();
                                return dst;
                            }

                            dice[i] = (int)item.IntegerValue;
                        }

                        dst.SetDice(dice);
                        return dst;
                    }
                    case not List:
                        dst.SetDice([]);
                        return dst;
                    default:
                        dst.SetNothing();
                        return dst;
                }
            case List:
                return CastList(state, in xValue, context);
            case Map:
                return CastMap(state, in xValue);

            case Custom:
                dst.SetNothing();
                return dst;
            default:
                if (xValue.Kind == type) return xValue;
                else dst.SetNothing();
                return dst;
        }
    }
    internal static void GesVmCastNumeric(this GesVmState vmState, ushort destinationRegister, in GesValue xValue)
    {
        var dst = GesVmCastNumeric(in xValue, vmState);
        vmState.SetValue(destinationRegister, in dst);
    }
    internal static GesValue GesVmCastNumeric(in GesValue xValue, GesVmState state)
    {
        var dst = new GesValue();
        if (xValue.Kind is Text)
        {
            if (double.TryParse(xValue.TextValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
            {
                dst.SetFloat(parsed);
            }
            else
            {
                dst.SetNothing();
            }

            return dst;
        }

        if (xValue.Kind is Series && xValue.ObjectValue is GesSeries series)
        {
            dst = series.GetTerm(0);
            return GesVmCastNumeric(in dst, state);
        }

        dst.SetFloat(xValue.AsNumeric, xValue.Kind is Integer or Float ? xValue.Unit : UnitNone);
        return dst;
    }
    internal static void GesVmCastCustom(this GesVmState vmState, ushort destinationRegister, in GesValue xValue, ushort typeTextPointer)
    {
        var dst = new GesValue();
        var typeName = vmState.FetchStringByPointer(typeTextPointer);
        if (IsCustomType(in xValue, typeName))
        {
            if (xValue.Kind is Custom)
            {
                dst = xValue;
                vmState.SetValue(destinationRegister, in dst);
                return;
            }

            dst.SetNothing();
            vmState.SetValue(destinationRegister, in dst);
            return;
        }

        if (xValue.Kind is Map && xValue.ObjectValue is GesValueMap map)
        {
            for (ushort recordId = 0; recordId < vmState.RecordConstructors.Length; recordId++)
            {
                var bind = vmState.RecordConstructors[recordId];
                if (bind.Kind is not GameEventScriptBinaryBindKind.Record ||
                    !string.Equals(vmState.FetchStringByPointer(bind.Name), typeName, StringComparison.Ordinal))
                {
                    continue;
                }

                vmState.ClearStage();
                for (var argumentIndex = 0; argumentIndex < bind.ArgumentNames.Count; argumentIndex++)
                {
                    var argumentName = vmState.FetchStringByPointer(bind.ArgumentNames[argumentIndex]);
                    if (map.Get(argumentName) is { } argument)
                    {
                        var stagedArgument = argument;
                        vmState.StageValue(in stagedArgument);
                    }
                    else vmState.StageNothing();
                }

                vmState.CallRecordConstructor(recordId, destinationRegister);
                return;
            }

            dst.SetNothing();
            vmState.SetValue(destinationRegister, in dst);
            return;
        }

        dst.SetNothing();
        vmState.SetValue(destinationRegister, in dst);
    }
    internal static void GesVmCheckType(this GesVmState vmState, ushort destinationRegister, in GesValue xValue, GameEventScriptBytecodeTypeKind type)
    {
        vmState.SetBoolean(destinationRegister, type switch
        {
            Nothing => xValue.IsNothing,
            Map => xValue.Kind is Map or Custom,
            Custom => false,
            _ => xValue.IsNotNothing && xValue.Kind == type
        });
    }
    internal static void GesVmCheckNumeric(this GesVmState vmState, ushort destinationRegister, in GesValue xValue) => vmState.SetBoolean(destinationRegister, xValue.IsNumeric);
    internal static void GesVmCheckInteger(this GesVmState vmState, ushort destinationRegister, in GesValue xValue)
    {
        if (!xValue.IsNumeric)
        {
            vmState.SetBoolean(destinationRegister, false);
            return;
        }

        var number = xValue.AsNumeric;
        vmState.SetBoolean(destinationRegister, GameEventScriptNumber.CanRepresentAsInteger(number));
    }
    internal static void GesVmCheckFractional(this GesVmState vmState, ushort destinationRegister, in GesValue xValue)
    {
        if (!xValue.IsNumeric)
        {
            vmState.SetBoolean(destinationRegister, false);
            return;
        }

        var number = xValue.AsNumeric;
        vmState.SetBoolean(destinationRegister, double.IsFinite(number) && number != Math.Truncate(number));
    }
    internal static void GesVmCheckCustomType(this GesVmState vmState, ushort destinationRegister, in GesValue xValue, ushort typeTextPointer)
    {
        vmState.SetBoolean(destinationRegister, IsCustomType(in xValue, vmState.FetchStringByPointer(typeTextPointer)));
    }
    private static GesValue CastText(in GesValue xValue)
    {
        var dst = new GesValue();
        if (xValue.Kind is Text)
        {
            return xValue;
        }

        dst.SetText(xValue.ToText);
        return dst;
    }
    private static GesValue CastList(GesVmState vmState, in GesValue xValue, GameEventScriptContext? context)
    {
        var dst = new GesValue();
        switch (xValue.Kind)
        {
            case List:
                return xValue;
            case Text or Tag:
            {
                var text = xValue.TextValue;
                if (text.Length == 0)
                {
                    dst.SetList(vmState.EmptyList);
                    return dst;
                }

                var list = new GesValue[xValue.Length];
                var utf16Offset = 0;
                for (var i = 0; i < list.Length; i++)
                {
                    var scalar = GameEventScriptText.ScalarAtUtf16Offset(text, utf16Offset);
                    list[i].SetText(scalar, 1);
                    utf16Offset += scalar.Length;
                }
                dst.SetList(list);
                return dst;
            }
            case Vector or Point when xValue.ObjectValue is GesValueVectorPoint triplet:
            {
                var list = new GesValue[3];
                list[0].SetFloat(triplet.X, xValue.Unit);
                list[1].SetFloat(triplet.Y, xValue.Unit);
                list[2].SetFloat(triplet.Z, xValue.Unit);
                dst.SetList(list);
                return dst;
            }
            case Dice when xValue.ObjectValue is int[] dice:
            {
                var list = new GesValue[dice.Length];
                for (var i = 0; i < dice.Length; i++) list[i].SetInteger(dice[i]);
                dst.SetList(list);
                return dst;
            }
            case GameEventScriptBytecodeTypeKind.Range when xValue.ObjectValue is GesValueRangeInteger range:
            {
                if (xValue.IntegerValue > int.MaxValue)
                {
                    dst.SetNothing();
                    return dst;
                }

                if (context is not null && !context.RuntimeBudget.CheckRangeLengthWithinLimit(xValue.IntegerValue, "Range length exceeds the configured limit."))
                {
                    dst.SetList(vmState.EmptyList);
                    return dst;
                }

                var list = new GesValue[(int)xValue.IntegerValue];
                var current = range.From;
                for (var i = 0; i < list.Length; i++)
                {
                    list[i].SetInteger(current);
                    current += range.Step;
                }

                dst.SetList(list);
                return dst;
            }
            case GameEventScriptBytecodeTypeKind.Range when xValue.ObjectValue is GesValueRangeFloat range:
            {
                if (xValue.IntegerValue > int.MaxValue)
                {
                    dst.SetNothing();
                    return dst;
                }

                if (context is not null && !context.RuntimeBudget.CheckRangeLengthWithinLimit(xValue.IntegerValue, "Range length exceeds the configured limit."))
                {
                    dst.SetList(vmState.EmptyList);
                    return dst;
                }

                var list = new GesValue[(int)xValue.IntegerValue];
                var current = range.From;
                for (var i = 0; i < list.Length; i++)
                {
                    list[i].SetFloat(current);
                    current += range.Step;
                }

                dst.SetList(list);
                return dst;
            }
            default:
                dst.SetList(vmState.EmptyList);
                return dst;
        }
    }
    private static GesValue CastMap(GesVmState vmState, in GesValue xValue)
    {
        var dst = new GesValue();
        switch (xValue.Kind)
        {
            case Map when xValue.ObjectValue is GesValueMap map:
                return xValue;
            case Custom when xValue.ObjectValue is GesCustomObject customObject:
                dst.SetMap(customObject.Map);
                return dst;
            case Custom when xValue.ObjectValue is GesExternalValue externalValue:
            {
                var sourceEntries = externalValue.ToMap();
                var entries = new GesVmMapBuilder(sourceEntries.Length);
                for (var i = 0; i < sourceEntries.StorageLength; i++)
                {
                    var key = sourceEntries.KeyAt(i);
                    entries.Set(key, sourceEntries.ValueAt(i));
                }

                dst.SetMap(entries.ToMap());
                return dst;
            }
            case Vector or Point when xValue.ObjectValue is GesValueVectorPoint triplet:
            {
                var x = new GesValue();
                x.SetFloat(triplet.X, xValue.Unit);
                var y = new GesValue();
                y.SetFloat(triplet.Y, xValue.Unit);
                var z = new GesValue();
                z.SetFloat(triplet.Z, xValue.Unit);
                var entries = new GesVmMapBuilder(3);
                entries.Set("x", x);
                entries.Set("y", y);
                entries.Set("z", z);
                dst.SetMap(entries.ToMap());
                return dst;
            }
            default:
                dst.SetMap(new GesVmMapBuilder(0).ToMap());
                return dst;
        }
    }
    private static GesValue CastVectorOrPoint(in GesValue xValue, bool asPoint)
    {
        var dst = new GesValue();
        var unit = UnitNone;
        double x = 0;
        double y = 0;
        double z = 0;

        switch (xValue.Kind)
        {
            case Vector when xValue.ObjectValue is GesValueVectorPoint vector:
                if (asPoint) dst.SetPoint(vector, xValue.Unit);
                else return xValue;
                return dst;
            case Point when xValue.ObjectValue is GesValueVectorPoint point:
                if (asPoint) return xValue;
                else dst.SetVector(point, xValue.Unit);
                return dst;
            case Integer or Float or Percentage or Tag or Dice or GameEventScriptBytecodeTypeKind.Boolean:
                if (xValue.Kind is Dice)
                {
                    if (xValue.ObjectValue is not int[] dice)
                    {
                        dst.SetNothing();
                        return dst;
                    }

                    if (dice.Length > 0) x = dice[0];
                    if (dice.Length > 1) y = dice[1];
                    if (dice.Length > 2) z = dice[2];
                    break;
                }

                var number = xValue.AsNumeric;
                if (!double.IsFinite(number))
                {
                    dst.SetNothing();
                    return dst;
                }

                x = number;
                unit = xValue.Kind is Integer or Float ? xValue.Unit : UnitNone;
                break;
            case List when xValue.ObjectValue is GesValue[] list:
                for (var i = 0; i < list.Length && i < 3; i++)
                {
                    var item = list[i];
                    if (!item.IsNumeric && item.Kind is not GameEventScriptBytecodeTypeKind.Boolean)
                    {
                        dst.SetNothing();
                        return dst;
                    }

                    var value = item.AsNumeric;
                    if (!double.IsFinite(value))
                    {
                        dst.SetNothing();
                        return dst;
                    }

                    if (i == 0) x = value;
                    else if (i == 1) y = value;
                    else z = value;
                }

                break;
            case Map when xValue.ObjectValue is GesValueMap map:
            {
                if (map.Get("x") is { } mapXValue)
                {
                    if (!mapXValue.IsNumeric && mapXValue.Kind is not GameEventScriptBytecodeTypeKind.Boolean)
                    {
                        dst.SetNothing();
                        return dst;
                    }

                    x = mapXValue.AsNumeric;
                }

                if (map.Get("y") is { } yValue)
                {
                    if (!yValue.IsNumeric && yValue.Kind is not GameEventScriptBytecodeTypeKind.Boolean)
                    {
                        dst.SetNothing();
                        return dst;
                    }

                    y = yValue.AsNumeric;
                }

                if (map.Get("z") is { } zValue)
                {
                    if (!zValue.IsNumeric && zValue.Kind is not GameEventScriptBytecodeTypeKind.Boolean)
                    {
                        dst.SetNothing();
                        return dst;
                    }

                    z = zValue.AsNumeric;
                }

                if (!(double.IsFinite(x) && double.IsFinite(y) && double.IsFinite(z)))
                {
                    dst.SetNothing();
                    return dst;
                }

                break;
            }
            case Custom when xValue.ObjectValue is GesCustomObject customObject:
            {
                var map = customObject.Map;
                if (map.Get("x") is { } mapXValue)
                {
                    if (!mapXValue.IsNumeric && mapXValue.Kind is not GameEventScriptBytecodeTypeKind.Boolean)
                    {
                        dst.SetNothing();
                        return dst;
                    }

                    x = mapXValue.AsNumeric;
                }

                if (map.Get("y") is { } yValue)
                {
                    if (!yValue.IsNumeric && yValue.Kind is not GameEventScriptBytecodeTypeKind.Boolean)
                    {
                        dst.SetNothing();
                        return dst;
                    }

                    y = yValue.AsNumeric;
                }

                if (map.Get("z") is { } zValue)
                {
                    if (!zValue.IsNumeric && zValue.Kind is not GameEventScriptBytecodeTypeKind.Boolean)
                    {
                        dst.SetNothing();
                        return dst;
                    }

                    z = zValue.AsNumeric;
                }

                break;
            }
            case GameEventScriptBytecodeTypeKind.Range when xValue.ObjectValue is GesValueRangeInteger range:
            {
                var current = range.From;
                for (var i = 0; i < xValue.IntegerValue && i < 3; i++)
                {
                    if (i == 0) x = current;
                    else if (i == 1) y = current;
                    else z = current;
                    current += range.Step;
                }

                break;
            }
            case GameEventScriptBytecodeTypeKind.Range when xValue.ObjectValue is GesValueRangeFloat range:
            {
                var current = range.From;
                for (var i = 0; i < xValue.IntegerValue && i < 3; i++)
                {
                    if (i == 0) x = current;
                    else if (i == 1) y = current;
                    else z = current;
                    current += range.Step;
                }

                break;
            }
            default:
                dst.SetNothing();
                return dst;
        }

        if (asPoint) dst.SetPoint(x, y, z, unit);
        else dst.SetVector(x, y, z, unit);
        return dst;
    }
    private static GesValue CastTag(in GesValue xValue)
    {
        var dst = new GesValue();
        switch (xValue.Kind)
        {
            case Tag:
                if (GameEventScriptTagRules.IsValidTagName(xValue.TextValue)) return xValue;
                else dst.SetNothing();
                return dst;
            case Text when xValue.ObjectValue is string text:
                if (GameEventScriptTagRules.NormalizeTextCast(text) is { } tag) dst.SetTag(tag);
                else dst.SetNothing();
                return dst;
            case GameEventScriptBytecodeTypeKind.Boolean:
                dst.SetTag(xValue.IsTrue ? "true" : "false");
                return dst;
            default:
                dst.SetNothing();
                return dst;
        }
    }
    private static bool IsCustomType(in GesValue value, string typeName)
    {
        if (value.ObjectValue is GesExternalValue externalValue) return string.Equals(externalValue.CustomTypeName, typeName, StringComparison.Ordinal);
        return value.Kind switch
        {
            Custom when value.ObjectValue is GesCustomObject customObject => string.Equals(customObject.TypeName, typeName, StringComparison.Ordinal),
            _ => false
        };
    }
}
