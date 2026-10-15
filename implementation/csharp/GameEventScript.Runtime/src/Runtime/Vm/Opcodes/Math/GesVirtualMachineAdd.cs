// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;
using static GameEventScript.Api.GesQuantityUnit;
using static GameEventScript.Runtime.Vm.GesVmUnitCalculation;

namespace GameEventScript.Runtime.Vm;

[SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator")]
internal partial class GesVirtualMachine
{
    // Integer operands are the hot case: compute with the general path's helpers and write the
    // result in place, without an intermediate GesValue. Every other outcome, including mismatched
    // units, overflow or a zero divisor, takes the complete general path, as in the Swift port.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ExecuteAdd(ushort destinationRegister, ushort x, ushort y)
    {
        ref readonly var a = ref Register(x);
        ref readonly var b = ref Register(y);
        if (a.Kind is Integer && b.Kind is Integer && SameUnit(in a, in b) is { } unit && GesNumber.AddExact(a.IntegerValue, b.IntegerValue) is { } result)
            SetInteger(destinationRegister, result, unit);
        else
            ExecuteAddGeneral(destinationRegister, in a, in b);
    }

    private void ExecuteAddGeneral(ushort destinationRegister, in GesValue a, in GesValue b)
    {
        var dst = AddValues(in a, in b, this);
        SetValue(destinationRegister, in dst);
    }

    private void ExecuteAddNumeric(ushort destinationRegister, ushort left, ushort right)
    {
        ref readonly var a = ref Register(left);
        ref readonly var b = ref Register(right);
        // Numeric scalar cases write directly; no generic value result or collection dispatch.
        switch (a.NumericKind)
        {
            case Integer when b.NumericKind is Integer:
                if (SameUnit(in a, in b) is { } unit)
                {
                    if (GesNumber.AddExact(a.IntegerValue, b.IntegerValue) is { } integerResult) SetInteger(destinationRegister, integerResult, unit);
                    else SetFloat(destinationRegister, (double)a.IntegerValue + b.IntegerValue, unit);
                }
                else SetFloat(destinationRegister, double.NaN);
                return;
            case Float when b.NumericKind is Float:
                if (SameUnit(in a, in b) is { } floatUnit) SetFloat(destinationRegister, a.FloatValue + b.FloatValue, floatUnit);
                else SetFloat(destinationRegister, double.NaN);
                return;
            case Float or Integer when b.NumericKind is Float or Integer:
                if (SameUnit(in a, in b) is { } mixedUnit) SetFloat(destinationRegister, a.AsNumeric + b.AsNumeric, mixedUnit);
                else SetFloat(destinationRegister, double.NaN);
                return;
            case Integer when b.NumericKind is Percentage:
                SetFloat(destinationRegister, a.IntegerValue + a.IntegerValue * b.FloatValue, a.Unit);
                return;
            case Float when b.NumericKind is Percentage:
                SetFloat(destinationRegister, a.FloatValue + a.FloatValue * b.FloatValue, a.Unit);
                return;
            case Percentage when b.NumericKind is Percentage:
                SetPercentage(destinationRegister, a.FloatValue + b.FloatValue);
                return;
            default:
                SetNothing(destinationRegister);
                return;
        }
    }

    private static GesValue AddValues(in GesValue a, in GesValue b, GesVirtualMachine state)
    {
        var dst = new GesValue();
        switch (a.Kind)
        {
            case Integer when b.Kind is Integer:
                if (SameUnit(in a, in b) is { } unit)
                {
                    if (GesNumber.AddExact(a.IntegerValue, b.IntegerValue) is { } integerResult) dst.SetInteger(integerResult, unit);
                    else dst.SetFloat((double)a.IntegerValue + b.IntegerValue, unit);
                }
                else dst.SetFloat(double.NaN);
                break;
            case Float when b.Kind is Float:
                if (SameUnit(in a, in b) is { } floatUnit) dst.SetFloat(a.FloatValue + b.FloatValue, floatUnit);
                else dst.SetFloat(double.NaN);
                break;
            case Float or Integer when b.Kind is Float or Integer:
                if (SameUnit(in a, in b) is { } mixedUnit) dst.SetFloat(a.AsNumeric + b.AsNumeric, mixedUnit);
                else dst.SetFloat(double.NaN);
                break;
            case Integer when b.Kind is Percentage:
                dst.SetFloat(a.IntegerValue + a.IntegerValue * b.FloatValue, a.Unit);
                break;
            case Float when b.Kind is Percentage:
                dst.SetFloat(a.FloatValue + a.FloatValue * b.FloatValue, a.Unit);
                break;
            case Percentage when b.Kind is Percentage:
                dst.SetPercentage(a.FloatValue + b.FloatValue);
                break;
            case Vector when b.Kind is Vector:
                if (a.ObjectValue is GesSpatialValue av && b.ObjectValue is GesSpatialValue bv && SameUnit(in a, in b) is { } vectorUnit) dst.SetVector(av.X + bv.X, av.Y + bv.Y, av.Z + bv.Z, vectorUnit);
                else dst.SetFloat(double.NaN);
                break;
            case Point when b.Kind is Vector:
                if (a.ObjectValue is GesSpatialValue ap && b.ObjectValue is GesSpatialValue bvv && SameUnit(in a, in b) is { } pointUnit) dst.SetPoint(ap.X + bvv.X, ap.Y + bvv.Y, ap.Z + bvv.Z, pointUnit);
                else dst.SetFloat(double.NaN);
                break;
            case Integer or Float or Percentage when b.Kind is Vector or Point:
                dst.SetFloat(double.NaN);
                break;
            case Text when b.Kind is not Nothing:
                dst.SetText(a.TextValue + b.ToText);
                break;
            case not Nothing when b.Kind is Text:
                dst.SetText(a.ToText + b.TextValue);
                break;
            case List when b.Kind is not Nothing && a.ObjectValue is GesValue[] aList:
            {
                var list = new GesValue[aList.Length + 1];
                for (var i = 0; i < aList.Length; i++) list[i] = aList[i];
                list[aList.Length] = b;
                dst.SetList(list);
                break;
            }
            case not List and not Nothing when b.Kind is List && b.ObjectValue is GesValue[] rightList:
            {
                var list = new GesValue[rightList.Length + 1];
                list[0] = a;
                for (var i = 0; i < rightList.Length; i++) list[i + 1] = rightList[i];
                dst.SetList(list);
                break;
            }
            case Dice when b.Kind is Integer && b.Unit is UnitNone && b.IntegerValue is > 0 and <= int.MaxValue && a.ObjectValue is int[] aDice:
            {
                var dice = new int[aDice.Length + 1];
                Array.Copy(aDice, dice, aDice.Length);
                dice[aDice.Length] = (int)b.IntegerValue;
                dst.SetDice(dice);
                break;
            }
            case Integer when a.Unit is UnitNone && a.IntegerValue is > 0 and <= int.MaxValue && b.Kind is Dice && b.ObjectValue is int[] bDice:
            {
                var dice = new int[bDice.Length + 1];
                dice[0] = (int)a.IntegerValue;
                Array.Copy(bDice, 0, dice, 1, bDice.Length);
                dst.SetDice(dice);
                break;
            }
            case Percentage:
            case Vector:
            case Point:
                if (b.Kind is not Nothing) dst.SetFloat(double.NaN);
                else dst.SetNothing();
                break;
            case Dice when b.Kind is not List:
            case not List and not Map and not Nothing when b.Kind is Map or Dice:
            case Nothing:
                dst.SetNothing();
                break;
            default:
                if (b.Kind is Nothing)
                {
                    dst.SetNothing();
                    break;
                }

                var aNum = a.AsNumeric;
                if (double.IsNaN(aNum))
                {
                    dst.SetFloat(double.NaN);
                }
                else
                {
                    var bNum = b.AsNumeric;
                    if (double.IsNaN(bNum)) dst.SetFloat(double.NaN);
                    else if (b.Kind is Percentage) dst.SetFloat(aNum + aNum * bNum, a.Unit);
                    else if (SameUnit(in a, in b) is { } fallbackUnit) dst.SetFloat(aNum + bNum, fallbackUnit);
                    else dst.SetFloat(double.NaN);
                }

                break;
        }
        return dst;
    }
}
