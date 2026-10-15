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
    private void ExecuteModulo(ushort destinationRegister, ushort x, ushort y)
    {
        ref readonly var a = ref Register(x);
        ref readonly var b = ref Register(y);
        if (a.Kind is Integer && b.Kind is Integer && SameUnit(in a, in b) is { } unit && b.IntegerValue != 0)
            SetInteger(destinationRegister, GesNumber.Modulo(a.IntegerValue, b.IntegerValue), unit);
        else
            ExecuteModuloGeneral(destinationRegister, in a, in b);
    }

    private void ExecuteModuloGeneral(ushort destinationRegister, in GesValue a, in GesValue b)
    {

        var dst = ModuloValues(in a, in b, this);
        SetValue(destinationRegister, in dst);
    }

    private void ExecuteModuloNumeric(ushort destinationRegister, ushort aRegister, ushort bRegister)
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
            ExecuteModuloGeneral(destinationRegister, in numeric0, in numeric1);
            return;
        }

        ExecuteModuloGeneral(destinationRegister, in a, in b);
    }

    private static GesValue ModuloValues(in GesValue a, in GesValue b, GesVirtualMachine state)
    {
        var dst = new GesValue();
        switch (a.Kind)
        {
            case Integer when b.Kind is Integer:
                if (SameUnit(in a, in b) is { } integerUnit)
                {
                    var rightInteger = b.IntegerValue;
                    if (rightInteger == 0)
                    {
                        dst.SetFloat(double.NaN, integerUnit);
                        return dst;
                    }

                    dst.SetInteger(GesNumber.Modulo(a.IntegerValue, rightInteger), integerUnit);
                }
                else dst.SetFloat(double.NaN);

                return dst;
            case Float when b.Kind is Float:
                if (SameUnit(in a, in b) is { } floatUnit)
                {
                    var leftFloat = a.FloatValue;
                    var rightFloat = b.FloatValue;
                    if (double.IsNaN(leftFloat) || double.IsNaN(rightFloat) || double.IsInfinity(leftFloat) || rightFloat == 0d)
                    {
                        dst.SetFloat(double.NaN, floatUnit);
                        return dst;
                    }

                    if (double.IsInfinity(rightFloat))
                    {
                        dst.SetFloat(leftFloat, floatUnit);
                        return dst;
                    }

                    var floatModuloResult = leftFloat % rightFloat;
                    if (floatModuloResult != 0d && (floatModuloResult < 0d && rightFloat > 0d || floatModuloResult > 0d && rightFloat < 0d)) floatModuloResult += rightFloat;
                    dst.SetFloat(floatModuloResult, floatUnit);
                }
                else dst.SetFloat(double.NaN);

                return dst;
            case Float or Integer when b.Kind is Float or Integer:
                if (SameUnit(in a, in b) is { } mixedUnit)
                {
                    var leftNumber = a.AsNumeric;
                    var rightNumber = b.AsNumeric;
                    if (double.IsNaN(leftNumber) || double.IsNaN(rightNumber) || double.IsInfinity(leftNumber) || rightNumber == 0d)
                    {
                        dst.SetFloat(double.NaN, mixedUnit);
                        return dst;
                    }

                    if (double.IsInfinity(rightNumber))
                    {
                        dst.SetFloat(leftNumber, mixedUnit);
                        return dst;
                    }

                    var mixedModuloResult = leftNumber % rightNumber;
                    if (mixedModuloResult != 0d && (mixedModuloResult < 0d && rightNumber > 0d || mixedModuloResult > 0d && rightNumber < 0d)) mixedModuloResult += rightNumber;
                    dst.SetFloat(mixedModuloResult, mixedUnit);
                }
                else dst.SetFloat(double.NaN);

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

        if (a.Kind is Vector or Point || b.Kind is Vector or Point || SameUnit(in a, in b) is not { } unit)
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
        if (double.IsNaN(right) || double.IsInfinity(left) || right == 0d)
        {
            dst.SetFloat(double.NaN, unit);
            return dst;
        }

        if (double.IsInfinity(right))
        {
            dst.SetFloat(left, unit);
            return dst;
        }

        var fallbackModuloResult = left % right;
        if (fallbackModuloResult != 0d && (fallbackModuloResult < 0d && right > 0d || fallbackModuloResult > 0d && right < 0d)) fallbackModuloResult += right;
        dst.SetFloat(fallbackModuloResult, unit);
        return dst;
    }
}
