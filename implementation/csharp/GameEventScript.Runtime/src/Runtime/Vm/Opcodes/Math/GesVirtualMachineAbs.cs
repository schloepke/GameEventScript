// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics.CodeAnalysis;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

[SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator")]
internal partial class GesVirtualMachine
{
    private void ExecuteAbs(ushort destinationRegister, in GesValue a)
    {

        var dst = AbsValue(in a, this);
        SetValue(destinationRegister, in dst);
    }

    private void ExecuteAbsNumeric(ushort destinationRegister, ushort aRegister)
    {
        ref readonly var a = ref Register(aRegister);
        if (!a.IsNumeric)
        {
            SetNothing(destinationRegister);
            return;
        }
        if (a.Kind is GesBytecodeTypeKind.Boolean or Dice)
        {
            var numeric0 = NumericOperand(in a);
            ExecuteAbs(destinationRegister, in numeric0);
            return;
        }

        ExecuteAbs(destinationRegister, in a);
    }

    private static GesValue AbsValue(in GesValue a, GesVirtualMachine state)
    {
        var dst = new GesValue();
        switch (a.Kind)
        {
            case Integer:
                if (a.IntegerValue == long.MinValue) dst.SetFloat(-(double)a.IntegerValue, a.Unit);
                else dst.SetInteger(Math.Abs(a.IntegerValue), a.Unit);
                return dst;
            case Float:
                dst.SetFloat(Math.Abs(a.FloatValue), a.Unit);
                return dst;
            case Percentage:
                dst.SetPercentage(Math.Abs(a.FloatValue));
                return dst;
            case Vector:
                if (a.ObjectValue is not GesSpatialValue vector)
                {
                    dst.SetFloat(double.NaN);
                    return dst;
                }

                var length = Math.Sqrt(vector.X * vector.X + vector.Y * vector.Y + vector.Z * vector.Z);
                if (double.IsNaN(length)) dst.SetFloat(double.NaN);
                else dst.SetFloat(length, a.Unit);
                return dst;
            case Point:
                dst.SetFloat(double.NaN);
                return dst;
            case Nothing:
                dst.SetNothing();
                return dst;
            default:
                var number = a.AsNumeric;
                dst.SetFloat(double.IsNaN(number) ? double.NaN : Math.Abs(number));
                return dst;
        }
    }
}
