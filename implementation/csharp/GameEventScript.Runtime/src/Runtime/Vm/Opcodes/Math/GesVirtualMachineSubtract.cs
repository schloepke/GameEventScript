// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics.CodeAnalysis;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using GameEventScript.Syntax;
using static GameEventScript.Api.GesBytecodeTypeKind;
using static GameEventScript.Api.GesQuantityUnit;
using static GameEventScript.Runtime.Vm.GesVmUnitCalculation;

namespace GameEventScript.Runtime.Vm;

[SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator")]
internal partial class GesVirtualMachine
{
    private void ExecuteSubtract(ushort dst, in GesValue a, in GesValue b)
    {

        switch (a.Kind)
        {
            case Integer when b.Kind is Integer:
                if (SameUnit(in a, in b) is { } unit)
                {
                    if (GesNumber.SubtractExact(a.IntegerValue, b.IntegerValue) is { } integerResult) SetInteger(dst, integerResult, unit);
                    else SetFloat(dst, (double)a.IntegerValue - b.IntegerValue, unit);
                }
                else SetFloat(dst, double.NaN);
                return;
            case Float when b.Kind is Float:
                if (SameUnit(in a, in b) is { } floatUnit) SetFloat(dst, a.FloatValue - b.FloatValue, floatUnit);
                else SetFloat(dst, double.NaN);
                return;
            case Float or Integer when b.Kind is Float or Integer:
                if (SameUnit(in a, in b) is { } mixedUnit) SetFloat(dst, a.AsNumeric - b.AsNumeric, mixedUnit);
                else SetFloat(dst, double.NaN);
                return;
            case Integer when b.Kind is Percentage:
                SetFloat(dst, a.IntegerValue - a.IntegerValue * b.FloatValue, a.Unit);
                return;
            case Float when b.Kind is Percentage:
                SetFloat(dst, a.FloatValue - a.FloatValue * b.FloatValue, a.Unit);
                return;
            case Percentage when b.Kind is Percentage:
                SetPercentage(dst, a.FloatValue - b.FloatValue);
                return;
            case Vector when b.Kind is Vector:
            {
                if (a.ObjectValue is GesSpatialValue av && b.ObjectValue is GesSpatialValue bv && SameUnit(in a, in b) is { } vectorUnit) SetVector(dst, av.X - bv.X, av.Y - bv.Y, av.Z - bv.Z, vectorUnit);
                else SetFloat(dst, double.NaN);
                return;
            }
            case Vector:
                if (b.Kind is not Nothing) SetFloat(dst, double.NaN);
                else SetNothing(dst);
                return;
            case Point when b.Kind is Vector:
            {
                if (a.ObjectValue is GesSpatialValue ap && b.ObjectValue is GesSpatialValue bv && SameUnit(in a, in b) is { } pointUnit) SetPoint(dst, ap.X - bv.X, ap.Y - bv.Y, ap.Z - bv.Z, pointUnit);
                else SetFloat(dst, double.NaN);
                return;
            }
            case Point when b.Kind is Point:
            {
                if (a.ObjectValue is GesSpatialValue ap && b.ObjectValue is GesSpatialValue bp && SameUnit(in a, in b) is { } pointUnit) SetVector(dst, ap.X - bp.X, ap.Y - bp.Y, ap.Z - bp.Z, pointUnit);
                else SetFloat(dst, double.NaN);
                return;
            }
            case Point:
                if (b.Kind is not Nothing) SetFloat(dst, double.NaN);
                else SetNothing(dst);
                return;
            case Integer or Float or Percentage when b.Kind is Vector or Point:
                SetFloat(dst, double.NaN);
                return;
            case List when b.Kind is List or Dice and not Nothing or not Map && a.ObjectValue is GesValue[] aList:
            {
                var removeList = b.Kind is List && b.ObjectValue is GesValue[] bList ? bList : null;
                var removeDice = b.Kind is Dice && b.ObjectValue is int[] bDice ? bDice : null;
                var removeCount = removeList?.Length ?? removeDice?.Length ?? 1;
                var removed = new bool[removeCount];
                var resultLength = 0;
                for (var i = 0; i < aList.Length; i++)
                {
                    var shouldRemove = false;
                    for (var j = 0; j < removeCount; j++)
                    {
                        if (removed[j]) continue;
                        var candidate = removeList is not null ? removeList[j] : b;
                        if (removeList is null && removeDice is not null)
                        {
                            candidate = new GesValue();
                            candidate.SetInteger(removeDice[j]);
                        }
                        if (!candidate.EqualsValue(in aList[i])) continue;
                        removed[j] = true;
                        shouldRemove = true;
                        break;
                    }

                    if (!shouldRemove) resultLength++;
                }

                var list = new GesValue[resultLength];
                var index = 0;
                Array.Clear(removed, 0, removed.Length);
                for (var i = 0; i < aList.Length; i++)
                {
                    var shouldRemove = false;
                    for (var j = 0; j < removeCount; j++)
                    {
                        if (removed[j]) continue;
                        var candidate = removeList is not null ? removeList[j] : b;
                        if (removeList is null && removeDice is not null)
                        {
                            candidate = new GesValue();
                            candidate.SetInteger(removeDice[j]);
                        }
                        if (!candidate.EqualsValue(in aList[i])) continue;
                        removed[j] = true;
                        shouldRemove = true;
                        break;
                    }

                    if (!shouldRemove) list[index++] = aList[i];
                }

                SetList(dst, list);
                return;
            }
            case Dice when b.Kind is Dice && a.ObjectValue is int[] aDice && b.ObjectValue is int[] bDice:
            {
                var removed = new bool[bDice.Length];
                var resultLength = 0;
                for (var i = 0; i < aDice.Length; i++)
                {
                    var shouldRemove = false;
                    for (var j = 0; j < bDice.Length; j++)
                    {
                        if (removed[j] || aDice[i] != bDice[j]) continue;
                        removed[j] = true;
                        shouldRemove = true;
                        break;
                    }

                    if (!shouldRemove) resultLength++;
                }

                var dice = new int[resultLength];
                var index = 0;
                Array.Clear(removed, 0, removed.Length);
                for (var i = 0; i < aDice.Length; i++)
                {
                    var shouldRemove = false;
                    for (var j = 0; j < bDice.Length; j++)
                    {
                        if (removed[j] || aDice[i] != bDice[j]) continue;
                        removed[j] = true;
                        shouldRemove = true;
                        break;
                    }

                    if (!shouldRemove) dice[index++] = aDice[i];
                }

                SetDice(dst, dice);
                return;
            }
            case Dice when b.Kind is List && a.ObjectValue is int[] aDice && b.ObjectValue is GesValue[] removeList:
            {
                var removed = new bool[removeList.Length];
                var resultLength = 0;
                for (var i = 0; i < aDice.Length; i++)
                {
                    var item = new GesValue();
                    item.SetInteger(aDice[i]);
                    var shouldRemove = false;
                    for (var j = 0; j < removeList.Length; j++)
                    {
                        if (removed[j] || !item.EqualsValue(in removeList[j])) continue;
                        removed[j] = true;
                        shouldRemove = true;
                        break;
                    }

                    if (!shouldRemove) resultLength++;
                }

                var list = new GesValue[resultLength];
                var index = 0;
                Array.Clear(removed, 0, removed.Length);
                for (var i = 0; i < aDice.Length; i++)
                {
                    var item = new GesValue();
                    item.SetInteger(aDice[i]);
                    var shouldRemove = false;
                    for (var j = 0; j < removeList.Length; j++)
                    {
                        if (removed[j] || !item.EqualsValue(in removeList[j])) continue;
                        removed[j] = true;
                        shouldRemove = true;
                        break;
                    }

                    if (!shouldRemove) list[index++].SetInteger(aDice[i]);
                }

                SetList(dst, list);
                return;
            }
            case Dice when b.Kind is Integer && b.Unit is UnitNone && a.ObjectValue is int[] aDice && b.IntegerValue is > 0 and <= int.MaxValue:
            {
                var roll = (int)b.IntegerValue;
                var removed = false;
                var resultLength = 0;
                for (var i = 0; i < aDice.Length; i++)
                {
                    if (!removed && aDice[i] == roll)
                    {
                        removed = true;
                        continue;
                    }

                    resultLength++;
                }

                var dice = new int[resultLength];
                var index = 0;
                removed = false;
                for (var i = 0; i < aDice.Length; i++)
                {
                    if (!removed && aDice[i] == roll)
                    {
                        removed = true;
                        continue;
                    }

                    dice[index++] = aDice[i];
                }

                SetDice(dst, dice);
                return;
            }
            case Map when b.Kind is not Nothing && a.ObjectValue is GesValueMap aMap:
            {
                string? singleKey = null;
                string[]? keys = null;
                GesValueMap? bMap = null;
                if (b.Kind is Map && b.ObjectValue is GesValueMap rightMap)
                {
                    bMap = rightMap;
                    // Keys are already sorted in GesValueMap.
                }
                else if (b.Kind is Text or Tag)
                {
                    singleKey = b.TextValue;
                }
                else if (b.Kind is List && b.ObjectValue is GesValue[] keyList)
                {
                    keys = new string[keyList.Length];
                    for (var i = 0; i < keyList.Length; i++)
                    {
                        var keyValue = keyList[i];
                        if (keyValue.Kind is not (Text or Tag))
                        {
                            SetNothing(dst);
                            return;
                        }

                        keys[i] = keyValue.TextValue;
                    }

                    Array.Sort(keys, GesText.ScalarComparer);
                }
                else
                {
                    SetNothing(dst);
                    return;
                }

                var map = new GesVmMapBuilder(aMap.Length);
                if (bMap is not null)
                {
                    var bi = 0;
                    for (var ai = 0; ai < aMap.Length; ai++)
                    {
                        var key = aMap.KeyAt(ai);
                        while (bi < bMap.Length && GesText.CompareScalarOrdinal(bMap.KeyAt(bi), key) < 0) bi++;
                        if (bi < bMap.Length && string.Equals(bMap.KeyAt(bi), key, StringComparison.Ordinal)) continue;
                        map.Set(key, aMap.ValueAt(ai));
                    }
                }
                else if (keys is not null)
                {
                    var ki = 0;
                    for (var ai = 0; ai < aMap.Length; ai++)
                    {
                        var key = aMap.KeyAt(ai);
                        while (ki < keys.Length && GesText.CompareScalarOrdinal(keys[ki], key) < 0) ki++;
                        if (ki < keys.Length && string.Equals(keys[ki], key, StringComparison.Ordinal)) continue;
                        map.Set(key, aMap.ValueAt(ai));
                    }
                }
                else
                {
                    for (var ai = 0; ai < aMap.Length; ai++)
                    {
                        var key = aMap.KeyAt(ai);
                        if (string.Equals(key, singleKey, StringComparison.Ordinal)) continue;
                        map.Set(key, aMap.ValueAt(ai));
                    }
                }

                SetMap(dst, map.ToMap());
                return;
            }
            case not List and not Map and not Nothing when b.Kind is List or Map:
                SetNothing(dst);
                return;
            case Percentage:
                if (b.Kind is not Nothing) SetFloat(dst, double.NaN);
                else SetNothing(dst);
                break;
            case Dice:
                SetNothing(dst);
                break;
            case Nothing:
                SetNothing(dst);
                return;
            default:
                if (b.Kind is Nothing)
                {
                    SetNothing(dst);
                }
                else
                {
                    var aNum = a.AsNumeric;
                    if (double.IsNaN(aNum))
                    {
                        SetFloat(dst, double.NaN);
                    }
                    else
                    {
                        var bNum = b.AsNumeric;
                        if (double.IsNaN(bNum)) SetFloat(dst, double.NaN);
                        else if (b.Kind is Percentage) SetFloat(dst, aNum - aNum * bNum, a.Unit);
                        else if (SameUnit(in a, in b) is { } fallbackUnit) SetFloat(dst, aNum - bNum, fallbackUnit);
                        else SetFloat(dst, double.NaN);
                    }
                }

                break;
        }
    }

    private void ExecuteSubtractNumeric(ushort dst, ushort aRegister, ushort bRegister)
    {
        ref readonly var a = ref Register(aRegister);
        ref readonly var b = ref Register(bRegister);
        if (!a.IsNumeric || !b.IsNumeric)
        {
            SetNothing(dst);
            return;
        }
        if (a.Kind is GesBytecodeTypeKind.Boolean or Dice || b.Kind is GesBytecodeTypeKind.Boolean or Dice)
        {
            var numeric0 = NumericOperand(in a);
            var numeric1 = NumericOperand(in b);
            ExecuteSubtract(dst, in numeric0, in numeric1);
            return;
        }

        ExecuteSubtract(dst, in a, in b);
    }
}
