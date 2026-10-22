// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;
using static GameEventScript.Runtime.Vm.GesVmUnitCalculation;

namespace GameEventScript.Runtime.Vm;

[SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator")]
internal partial class GesVirtualMachine
{
    private void ExecuteClamp(ushort destinationRegister, in GesValue value, in GesValue min, in GesValue max)
    {

        var dst = ClampValues(in value, in min, in max, this);
        SetValue(destinationRegister, in dst);
    }

    private void ExecuteClampNumeric(ushort destinationRegister, ushort valueRegister, ushort minRegister, ushort maxRegister)
    {
        ref readonly var value = ref Register(valueRegister);
        ref readonly var min = ref Register(minRegister);
        ref readonly var max = ref Register(maxRegister);
        if (!value.IsNumeric || !min.IsNumeric || !max.IsNumeric)
        {
            SetNothing(destinationRegister);
            return;
        }
        if (value.Kind is GesBytecodeTypeKind.Boolean or Dice || min.Kind is GesBytecodeTypeKind.Boolean or Dice || max.Kind is GesBytecodeTypeKind.Boolean or Dice)
        {
            var numeric0 = NumericOperand(in value);
            var numeric1 = NumericOperand(in min);
            var numeric2 = NumericOperand(in max);
            ExecuteClamp(destinationRegister, in numeric0, in numeric1, in numeric2);
            return;
        }

        ExecuteClamp(destinationRegister, in value, in min, in max);
    }

    private static GesValue ClampValues(in GesValue value, in GesValue min, in GesValue max, GesVirtualMachine state)
    {
        var dst = new GesValue();
        switch (value.Kind)
        {
            case Integer when min.Kind is Integer && max.Kind is Integer:
            {
                if (SameUnit(in value, in min, in max) is not { } integerUnit)
                {
                    dst.SetFloat(double.NaN);
                    return dst;
                }

                var lower = min.IntegerValue <= max.IntegerValue ? min.IntegerValue : max.IntegerValue;
                var upper = min.IntegerValue <= max.IntegerValue ? max.IntegerValue : min.IntegerValue;
                var result = value.IntegerValue < lower ? lower : value.IntegerValue > upper ? upper : value.IntegerValue;
                dst.SetInteger(result, integerUnit);
                return dst;
            }
            case Float when min.Kind is Float && max.Kind is Float:
            {
                if (SameUnit(in value, in min, in max) is not { } floatUnit)
                {
                    dst.SetFloat(double.NaN);
                    return dst;
                }

                var lower = min.FloatValue <= max.FloatValue ? min.FloatValue : max.FloatValue;
                var upper = min.FloatValue <= max.FloatValue ? max.FloatValue : min.FloatValue;
                if (double.IsNaN(value.FloatValue) || double.IsNaN(lower) || double.IsNaN(upper))
                {
                    dst.SetFloat(double.NaN, floatUnit);
                    return dst;
                }

                var result = value.FloatValue < lower ? lower : value.FloatValue > upper ? upper : value.FloatValue;
                dst.SetFloat(result, floatUnit);
                return dst;
            }
            case Float or Integer when min.Kind is Float or Integer && max.Kind is Float or Integer:
            {
                if (SameUnit(in value, in min, in max) is not { } mixedUnit)
                {
                    dst.SetFloat(double.NaN);
                    return dst;
                }

                var x = value.AsNumeric;
                var a = min.AsNumeric;
                var b = max.AsNumeric;
                var lower = a <= b ? a : b;
                var upper = a <= b ? b : a;
                if (double.IsNaN(x) || double.IsNaN(lower) || double.IsNaN(upper))
                {
                    dst.SetFloat(double.NaN, mixedUnit);
                    return dst;
                }

                var result = x < lower ? lower : x > upper ? upper : x;
                dst.SetFloat(result, mixedUnit);
                return dst;
            }
            case Nothing:
                dst.SetNothing();
                return dst;
            case Vector or Point:
                if (min.Kind is Nothing || max.Kind is Nothing) dst.SetNothing();
                else dst.SetFloat(double.NaN);
                return dst;
        }

        if (min.Kind is Nothing || max.Kind is Nothing)
        {
            dst.SetNothing();
            return dst;
        }

        if (min.Kind is Vector or Point || max.Kind is Vector or Point || SameUnit(in value, in min, in max) is not { } unit)
        {
            dst.SetFloat(double.NaN);
            return dst;
        }

        var raw = value.AsNumeric;
        var minimum = min.AsNumeric;
        var maximum = max.AsNumeric;
        var lowerFallback = minimum <= maximum ? minimum : maximum;
        var upperFallback = minimum <= maximum ? maximum : minimum;
        if (double.IsNaN(raw) || double.IsNaN(lowerFallback) || double.IsNaN(upperFallback))
        {
            dst.SetFloat(double.NaN, unit);
            return dst;
        }

        var fallbackResult = raw < lowerFallback ? lowerFallback : raw > upperFallback ? upperFallback : raw;
        dst.SetFloat(fallbackResult, unit);
        return dst;
    }
}
