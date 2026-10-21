// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;
using static GameEventScript.Api.GesQuantityUnit;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private static GesValue CastUnitValue(in GesValue xValue, GesQuantityUnit unit, GesVirtualMachine state)
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
                if (xValue.ObjectValue is GesSpatialValue vector && (unit is UnitNone || xValue.Unit is UnitNone || xValue.Unit == unit)) dst.SetVector(vector, unit);
                else dst.SetFloat(double.NaN);
                return dst;
            case Point:
                if (xValue.ObjectValue is GesSpatialValue point && (unit is UnitNone || xValue.Unit is UnitNone || xValue.Unit == unit)) dst.SetPoint(point, unit);
                else dst.SetFloat(double.NaN);
                return dst;
            default:
                dst.SetFloat(double.NaN);
                return dst;
        }
    }

    internal static GesValue CastValue(in GesValue xValue, GesBytecodeTypeKind type, GesVirtualMachine state, ushort destinationRegister, GesContext? context = null)
    {
        var dst = new GesValue();
        switch (type)
        {
            case Nothing:
                dst.SetNothing();
                return dst;
            case GesBytecodeTypeKind.Boolean:
                dst.SetBoolean(xValue.IsTrue);
                return dst;
            case Integer:
                var integerNumber = xValue.AsNumeric;
                if (GesNumber.CanRepresentAsInteger(integerNumber)) dst.SetInteger((long)integerNumber, xValue.Kind is Integer or Float ? xValue.Unit : UnitNone);
                else dst.SetNothing();
                return dst;
            case Float:
                dst.SetFloat(xValue.AsNumeric, xValue.Kind is Integer or Float ? xValue.Unit : UnitNone);
                return dst;
            case Percentage:
                return GesTextNumberCast.Percentage(in xValue);
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

    private static GesValue CastNumericValue(in GesValue xValue, GesVirtualMachine state)
        => GesNumber.Cast(in xValue);

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

    private static GesValue CastList(GesVirtualMachine vm, in GesValue xValue, GesContext? context)
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
                    dst.SetList(vm.EmptyList);
                    return dst;
                }

                var list = new GesValue[xValue.Length];
                var utf16Offset = 0;
                for (var i = 0; i < list.Length; i++)
                {
                    var scalar = GesText.ScalarAtUtf16Offset(text, utf16Offset);
                    list[i].SetText(scalar, 1);
                    utf16Offset += scalar.Length;
                }
                dst.SetList(list);
                return dst;
            }
            case Vector or Point when xValue.ObjectValue is GesSpatialValue triplet:
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
            case GesBytecodeTypeKind.Range when xValue.ObjectValue is GesIntegerRange range:
            {
                if (xValue.CollectionCount > int.MaxValue)
                {
                    dst.SetNothing();
                    return dst;
                }

                if (context is not null && !context.RuntimeBudget.CheckRangeLengthWithinLimit(xValue.CollectionCount, "Range length exceeds the configured limit."))
                {
                    dst.SetList(vm.EmptyList);
                    return dst;
                }

                var list = new GesValue[(int)xValue.CollectionCount];
                var current = range.From;
                for (var i = 0; i < list.Length; i++)
                {
                    list[i].SetInteger(current);
                    current += range.Step;
                }

                dst.SetList(list);
                return dst;
            }
            case GesBytecodeTypeKind.Range when xValue.ObjectValue is GesFloatRange range:
            {
                if (xValue.CollectionCount > int.MaxValue)
                {
                    dst.SetNothing();
                    return dst;
                }

                if (context is not null && !context.RuntimeBudget.CheckRangeLengthWithinLimit(xValue.CollectionCount, "Range length exceeds the configured limit."))
                {
                    dst.SetList(vm.EmptyList);
                    return dst;
                }

                var list = new GesValue[(int)xValue.CollectionCount];
                for (var i = 0; i < list.Length; i++)
                {
                    list[i].SetFloat(GesRangeMath.GetFloatTerm(range.From, range.To, range.Step, i));
                }

                dst.SetList(list);
                return dst;
            }
            default:
                dst.SetList(vm.EmptyList);
                return dst;
        }
    }

    private static GesValue CastMap(GesVirtualMachine vm, in GesValue xValue)
    {
        var dst = new GesValue();
        switch (xValue.Kind)
        {
            case Map when xValue.ObjectValue is GesValueMap map:
                return xValue;
            case Custom when xValue.ObjectValue is GesCustomObject customObject:
                dst.SetMap(customObject.Map);
                return dst;
            case Custom when xValue.ObjectValue is GesExternalStorage externalValue:
            {
                var sourceEntries = externalValue.ToMap();
                var entries = new GesVmMapBuilder(sourceEntries.Length);
                for (var i = 0; i < sourceEntries.Length; i++)
                {
                    var key = sourceEntries.KeyAt(i);
                    entries.Set(key, sourceEntries.ValueAt(i));
                }

                dst.SetMap(entries.ToMap());
                return dst;
            }
            case Vector or Point when xValue.ObjectValue is GesSpatialValue triplet:
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
            case Vector when xValue.ObjectValue is GesSpatialValue vector:
                if (asPoint) dst.SetPoint(vector, xValue.Unit);
                else return xValue;
                return dst;
            case Point when xValue.ObjectValue is GesSpatialValue point:
                if (asPoint) return xValue;
                else dst.SetVector(point, xValue.Unit);
                return dst;
            case Integer or Float or Percentage or Tag or Dice or GesBytecodeTypeKind.Boolean:
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
                    if (!item.IsNumeric && item.Kind is not GesBytecodeTypeKind.Boolean)
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
                    if (!mapXValue.IsNumeric && mapXValue.Kind is not GesBytecodeTypeKind.Boolean)
                    {
                        dst.SetNothing();
                        return dst;
                    }

                    x = mapXValue.AsNumeric;
                }

                if (map.Get("y") is { } yValue)
                {
                    if (!yValue.IsNumeric && yValue.Kind is not GesBytecodeTypeKind.Boolean)
                    {
                        dst.SetNothing();
                        return dst;
                    }

                    y = yValue.AsNumeric;
                }

                if (map.Get("z") is { } zValue)
                {
                    if (!zValue.IsNumeric && zValue.Kind is not GesBytecodeTypeKind.Boolean)
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
                    if (!mapXValue.IsNumeric && mapXValue.Kind is not GesBytecodeTypeKind.Boolean)
                    {
                        dst.SetNothing();
                        return dst;
                    }

                    x = mapXValue.AsNumeric;
                }

                if (map.Get("y") is { } yValue)
                {
                    if (!yValue.IsNumeric && yValue.Kind is not GesBytecodeTypeKind.Boolean)
                    {
                        dst.SetNothing();
                        return dst;
                    }

                    y = yValue.AsNumeric;
                }

                if (map.Get("z") is { } zValue)
                {
                    if (!zValue.IsNumeric && zValue.Kind is not GesBytecodeTypeKind.Boolean)
                    {
                        dst.SetNothing();
                        return dst;
                    }

                    z = zValue.AsNumeric;
                }

                break;
            }
            case GesBytecodeTypeKind.Range when xValue.ObjectValue is GesIntegerRange range:
            {
                var current = range.From;
                for (var i = 0; i < xValue.CollectionCount && i < 3; i++)
                {
                    if (i == 0) x = current;
                    else if (i == 1) y = current;
                    else z = current;
                    current += range.Step;
                }

                break;
            }
            case GesBytecodeTypeKind.Range when xValue.ObjectValue is GesFloatRange range:
            {
                for (var i = 0; i < xValue.CollectionCount && i < 3; i++)
                {
                    var current = GesRangeMath.GetFloatTerm(range.From, range.To, range.Step, i);
                    if (i == 0) x = current;
                    else if (i == 1) y = current;
                    else z = current;
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
                if (GesTagRules.IsValidTagName(xValue.TextValue)) return xValue;
                else dst.SetNothing();
                return dst;
            case Text:
                if (GesTagRules.NormalizeTextCast(xValue.TextValue) is { } tag) dst.SetTag(tag);
                else dst.SetNothing();
                return dst;
            case GesBytecodeTypeKind.Boolean:
                dst.SetTag(xValue.IsTrue ? "true" : "false");
                return dst;
            default:
                dst.SetNothing();
                return dst;
        }
    }

    private static bool IsCustomType(in GesValue value, string typeName)
    {
        if (value.ObjectValue is GesExternalStorage externalValue) return string.Equals(externalValue.CustomTypeName, typeName, StringComparison.Ordinal);
        return value.Kind switch
        {
            Custom when value.ObjectValue is GesCustomObject customObject => string.Equals(customObject.TypeName, typeName, StringComparison.Ordinal),
            _ => false
        };
    }
}
