// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics.CodeAnalysis;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;
using static GameEventScript.Runtime.Vm.GesVmUnitCalculation;

namespace GameEventScript.Runtime.Vm;

[SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator")]
internal partial class GesVirtualMachine
{
    private void ExecuteIntegerDivide(ushort destinationRegister, in GesValue a, in GesValue b)
    {

        var dst = IntegerDivideValues(in a, in b, this);
        SetValue(destinationRegister, in dst);
    }

    private void ExecuteIntegerDivideNumeric(ushort destinationRegister, ushort aRegister, ushort bRegister)
    {
        ref readonly var a = ref Register(aRegister);
        ref readonly var b = ref Register(bRegister);
        if (!a.IsNumeric || !b.IsNumeric)
        {
            SetNothing(destinationRegister);
            return;
        }
        if (a.Kind is GesBytecodeTypeKind.Boolean or Dice || b.Kind is GesBytecodeTypeKind.Boolean or Dice)
        {
            var numeric0 = NumericOperand(in a);
            var numeric1 = NumericOperand(in b);
            ExecuteIntegerDivide(destinationRegister, in numeric0, in numeric1);
            return;
        }

        ExecuteIntegerDivide(destinationRegister, in a, in b);
    }

    private static GesValue IntegerDivideValues(in GesValue a, in GesValue b, GesVirtualMachine state)
    {
        var dst = new GesValue();
        switch (a.Kind)
        {
            case Integer when b.Kind is Integer:
                if (IntegerQuotientUnit(in a, in b) is { } integerUnit)
                {
                    if (GesNumber.FloorDivideExact(a.IntegerValue, b.IntegerValue) is { } integerResult) dst.SetInteger(integerResult, integerUnit);
                    else dst.SetFloat(Math.Floor((double)a.IntegerValue / b.IntegerValue), integerUnit);
                }
                else dst.SetFloat(double.NaN);

                return dst;
            case Float when b.Kind is Float:
                if (IntegerQuotientUnit(in a, in b) is { } floatUnit)
                {
                    var result = Math.Floor(a.FloatValue / b.FloatValue);
                    dst.SetFloat(result, floatUnit);
                }
                else dst.SetFloat(double.NaN);

                return dst;
            case Float or Integer when b.Kind is Float or Integer:
                if (IntegerQuotientUnit(in a, in b) is { } mixedUnit)
                {
                    var result = Math.Floor(a.AsNumeric / b.AsNumeric);
                    dst.SetFloat(result, mixedUnit);
                }
                else dst.SetFloat(double.NaN);

                return dst;
            case Integer or Float when b.Kind is Percentage:
                if (IntegerQuotientUnit(in a, in b) is { } percentageRightUnit)
                {
                    var result = Math.Floor(a.AsNumeric / b.FloatValue);
                    dst.SetFloat(result, percentageRightUnit);
                }
                else dst.SetFloat(double.NaN);

                return dst;
            case Percentage when b.Kind is Integer or Float or Percentage:
                if (IntegerQuotientUnit(in a, in b) is { } percentageLeftUnit)
                {
                    var result = Math.Floor(a.FloatValue / b.AsNumeric);
                    dst.SetFloat(result, percentageLeftUnit);
                }
                else dst.SetFloat(double.NaN);

                return dst;
            case Vector or Point:
                if (b.Kind is Nothing) dst.SetNothing();
                else dst.SetFloat(double.NaN);
                return dst;
            case Integer or Float or Percentage when b.Kind is Vector or Point:
                dst.SetFloat(double.NaN);
                return dst;
            case Nothing:
                dst.SetNothing();
                return dst;
        }

        if (b.Kind is Nothing)
        {
            dst.SetNothing();
            return dst;
        }

        if (b.Kind is Vector or Point || IntegerQuotientUnit(in a, in b) is not { } unit)
        {
            dst.SetFloat(double.NaN);
            return dst;
        }

        var left = a.AsNumeric;
        if (double.IsNaN(left))
        {
            dst.SetFloat(double.NaN);
            return dst;
        }

        var right = b.AsNumeric;
        if (double.IsNaN(right))
        {
            dst.SetFloat(double.NaN);
            return dst;
        }

        var floorResult = Math.Floor(left / right);
        dst.SetFloat(floorResult, unit);
        return dst;
    }
}
