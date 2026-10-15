// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;
using static GameEventScript.Runtime.Vm.GesVmUnitCalculation;

namespace GameEventScript.Runtime.Vm;

[SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator")]
internal partial class GesVirtualMachine
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ExecuteMultiply(ushort destinationRegister, ushort x, ushort y)
    {
        ref readonly var a = ref Register(x);
        ref readonly var b = ref Register(y);
        if (a.Kind is Integer && b.Kind is Integer && ProductUnit(in a, in b) is { } unit && GesNumber.MultiplyExact(a.IntegerValue, b.IntegerValue) is { } result)
            SetInteger(destinationRegister, result, unit);
        else
            ExecuteMultiplyGeneral(destinationRegister, in a, in b);
    }

    private void ExecuteMultiplyGeneral(ushort dst, in GesValue a, in GesValue b)
    {

        switch (a.Kind)
        {
            case Integer when b.Kind is Integer:
                if (ProductUnit(in a, in b) is { } unit)
                {
                    if (GesNumber.MultiplyExact(a.IntegerValue, b.IntegerValue) is { } integerResult) SetInteger(dst, integerResult, unit);
                    else SetFloat(dst, (double)a.IntegerValue * b.IntegerValue, unit);
                }
                else SetFloat(dst, double.NaN);
                return;
            case Float when b.Kind is Float:
                if (ProductUnit(in a, in b) is { } floatUnit) SetFloat(dst, a.FloatValue * b.FloatValue, floatUnit);
                else SetFloat(dst, double.NaN);
                return;
            case Float or Integer when b.Kind is Float or Integer:
                if (ProductUnit(in a, in b) is { } mixedUnit) SetFloat(dst, a.AsNumeric * b.AsNumeric, mixedUnit);
                else SetFloat(dst, double.NaN);
                return;
            case Integer when b.Kind is Percentage:
                SetFloat(dst, a.IntegerValue * b.FloatValue, a.Unit);
                return;
            case Float when b.Kind is Percentage:
                SetFloat(dst, a.FloatValue * b.FloatValue, a.Unit);
                return;
            case Percentage when b.Kind is Percentage:
                SetPercentage(dst, a.FloatValue * b.FloatValue);
                return;
            case Percentage when b.Kind is Integer:
                SetFloat(dst, a.FloatValue * b.IntegerValue, b.Unit);
                return;
            case Percentage when b.Kind is Float:
                SetFloat(dst, a.FloatValue * b.FloatValue, b.Unit);
                return;
            case Percentage when b.Kind is Vector:
                if (b.ObjectValue is GesSpatialValue percentageVector) SetVector(dst, a.FloatValue * percentageVector.X, a.FloatValue * percentageVector.Y, a.FloatValue * percentageVector.Z, b.Unit);
                else SetFloat(dst, double.NaN);
                return;
            case Percentage when b.Kind is Point:
                SetFloat(dst, double.NaN);
                return;
            case Vector when b.Kind is Integer:
                if (a.ObjectValue is GesSpatialValue vectorInt && ProductUnit(in a, in b) is { } vectorIntegerUnit)
                    SetVector(dst, vectorInt.X * b.IntegerValue, vectorInt.Y * b.IntegerValue, vectorInt.Z * b.IntegerValue, vectorIntegerUnit);
                else SetFloat(dst, double.NaN);
                return;
            case Vector when b.Kind is Float:
                if (a.ObjectValue is GesSpatialValue vectorFloat && double.IsFinite(b.FloatValue) && ProductUnit(in a, in b) is { } vectorFloatUnit)
                    SetVector(dst, vectorFloat.X * b.FloatValue, vectorFloat.Y * b.FloatValue, vectorFloat.Z * b.FloatValue, vectorFloatUnit);
                else SetFloat(dst, double.NaN);
                return;
            case Vector when b.Kind is Percentage:
                if (a.ObjectValue is GesSpatialValue vectorPercent) SetVector(dst, vectorPercent.X * b.FloatValue, vectorPercent.Y * b.FloatValue, vectorPercent.Z * b.FloatValue, a.Unit);
                else SetFloat(dst, double.NaN);
                return;
            case Vector when b.Kind is Nothing:
                SetNothing(dst);
                return;
            case Vector:
                var vectorScalar = b.AsNumeric;
                if (a.ObjectValue is GesSpatialValue vector && !double.IsNaN(vectorScalar) && double.IsFinite(vectorScalar) && ProductUnit(in a, in b) is { } vectorScalarUnit)
                    SetVector(dst, vector.X * vectorScalar, vector.Y * vectorScalar, vector.Z * vectorScalar, vectorScalarUnit);
                else SetFloat(dst, double.NaN);
                return;
            case Point when b.Kind is Nothing:
                SetNothing(dst);
                return;
            case Point:
                SetFloat(dst, double.NaN);
                return;
            case Integer or Float when b.Kind is Vector:
                var scalar = a.AsNumeric;
                if (b.ObjectValue is GesSpatialValue rightVector && double.IsFinite(scalar) && ProductUnit(in a, in b) is { } rightVectorUnit)
                    SetVector(dst, scalar * rightVector.X, scalar * rightVector.Y, scalar * rightVector.Z, rightVectorUnit);
                else SetFloat(dst, double.NaN);
                return;
            case Integer or Float when b.Kind is Point:
                SetFloat(dst, double.NaN);
                return;
            case Percentage:
                if (b.Kind is Nothing)
                {
                    SetNothing(dst);
                    return;
                }

                var percentageScalar = b.AsNumeric;
                if (double.IsNaN(percentageScalar))
                {
                    SetFloat(dst, double.NaN);
                    return;
                }

                var percentageResult = a.FloatValue * percentageScalar;
                if (b.HasUnit) SetFloat(dst, percentageResult, b.Unit);
                else SetFloat(dst, percentageResult);
                return;
            case Nothing:
                SetNothing(dst);
                return;
        }

        if (b.Kind is Nothing)
        {
            SetNothing(dst);
            return;
        }

        var fallbackLeft = a.AsNumeric;
        if (double.IsNaN(fallbackLeft))
        {
            SetFloat(dst, double.NaN);
            return;
        }

        switch (b.Kind)
        {
            case Vector:
            {
                if (b.ObjectValue is GesSpatialValue vector && double.IsFinite(fallbackLeft) && ProductUnit(in a, in b) is { } vectorUnit)
                    SetVector(dst, fallbackLeft * vector.X, fallbackLeft * vector.Y, fallbackLeft * vector.Z, vectorUnit);
                else SetFloat(dst, double.NaN);
                return;
            }
            case Point:
            {
                SetFloat(dst, double.NaN);
                return;
            }
        }

        var bNum = b.AsNumeric;
        if (double.IsNaN(bNum) || ProductUnit(in a, in b) is not { } fallbackUnit)
        {
            SetFloat(dst, double.NaN);
            return;
        }

        SetFloat(dst, fallbackLeft * bNum, b.Kind is Percentage ? a.Unit : fallbackUnit);
    }

    private void ExecuteMultiplyNumeric(ushort dst, ushort aRegister, ushort bRegister)
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
            ExecuteMultiplyGeneral(dst, in numeric0, in numeric1);
            return;
        }

        ExecuteMultiplyGeneral(dst, in a, in b);
    }
}
