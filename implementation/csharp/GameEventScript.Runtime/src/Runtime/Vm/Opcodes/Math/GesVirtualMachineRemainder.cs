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
    private void ExecuteRemainder(ushort destinationRegister, in GesValue a, in GesValue b)
    {

        var dst = RemainderValues(in a, in b, this);
        SetValue(destinationRegister, in dst);
    }

    private void ExecuteRemainderNumeric(ushort destinationRegister, ushort aRegister, ushort bRegister)
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
            ExecuteRemainder(destinationRegister, in numeric0, in numeric1);
            return;
        }

        ExecuteRemainder(destinationRegister, in a, in b);
    }

    private static GesValue RemainderValues(in GesValue a, in GesValue b, GesVirtualMachine state)
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

                    dst.SetInteger(GesNumber.Remainder(a.IntegerValue, rightInteger), integerUnit);
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

                    dst.SetFloat(double.IsInfinity(rightFloat) ? leftFloat : leftFloat % rightFloat, floatUnit);
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

                    dst.SetFloat(double.IsInfinity(rightNumber) ? leftNumber : leftNumber % rightNumber, mixedUnit);
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

        dst.SetFloat(double.IsInfinity(right) ? left : left % right, unit);
        return dst;
    }
}
