// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

[SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator")]
internal partial class GesVirtualMachine
{
    private void ExecuteNegate(ushort destinationRegister, in GesValue a)
    {

        var dst = NegateValue(in a, this);
        SetValue(destinationRegister, in dst);
    }

    private void ExecuteNegateNumeric(ushort destinationRegister, ushort aRegister)
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
            ExecuteNegate(destinationRegister, in numeric0);
            return;
        }

        ExecuteNegate(destinationRegister, in a);
    }

    private static GesValue NegateValue(in GesValue a, GesVirtualMachine state)
    {
        var dst = new GesValue();
        switch (a.Kind)
        {
            case Integer:
                if (a.IntegerValue == long.MinValue) dst.SetFloat(-(double)a.IntegerValue, a.Unit);
                else dst.SetInteger(-a.IntegerValue, a.Unit);
                return dst;
            case Float:
                dst.SetFloat(-a.FloatValue, a.Unit);
                return dst;
            case Percentage:
                dst.SetPercentage(-a.FloatValue);
                return dst;
            case Vector:
                if (a.ObjectValue is GesSpatialValue vector)
                {
                    dst.SetVector(-vector.X, -vector.Y, -vector.Z, a.Unit);
                }
                else dst.SetFloat(double.NaN);

                return dst;
            case Point:
                dst.SetFloat(double.NaN);
                return dst;
            case Nothing:
                dst.SetNothing();
                return dst;
            default:
                var number = a.AsNumeric;
                if (double.IsNaN(number)) dst.SetFloat(double.NaN);
                else dst.SetFloat(-number);
                return dst;
        }
    }
}
