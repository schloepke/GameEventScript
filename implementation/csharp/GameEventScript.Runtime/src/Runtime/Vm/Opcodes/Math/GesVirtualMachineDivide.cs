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
    private void ExecuteDivide(ushort destinationRegister, in GesValue a, in GesValue b)
    {

        var dst = DivideValues(in a, in b, this);
        SetValue(destinationRegister, in dst);
    }

    private void ExecuteDivideNumeric(ushort destinationRegister, ushort aRegister, ushort bRegister)
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
            ExecuteDivide(destinationRegister, in numeric0, in numeric1);
            return;
        }

        ExecuteDivide(destinationRegister, in a, in b);
    }

    private static GesValue DivideValues(in GesValue a, in GesValue b, GesVirtualMachine state)
    {
        var dst = new GesValue();
        switch (a.Kind)
        {
            case Integer when b.Kind is Integer:
                if (QuotientUnit(in a, in b) is { } unit) dst.SetFloat((double)a.IntegerValue / b.IntegerValue, unit);
                else dst.SetFloat(double.NaN);
                return dst;
            case Float when b.Kind is Float:
                if (QuotientUnit(in a, in b) is { } floatUnit) dst.SetFloat(a.FloatValue / b.FloatValue, floatUnit);
                else dst.SetFloat(double.NaN);
                return dst;
            case Float or Integer when b.Kind is Float or Integer:
                if (QuotientUnit(in a, in b) is { } mixedUnit) dst.SetFloat(a.AsNumeric / b.AsNumeric, mixedUnit);
                else dst.SetFloat(double.NaN);
                return dst;
            case Percentage when b.Kind is Percentage:
                dst.SetFloat(a.FloatValue / b.FloatValue);
                return dst;
            case Percentage when b.Kind is Integer or Float:
                if (b.HasUnit || QuotientUnit(in a, in b) is not { } percentageUnit)
                {
                    dst.SetFloat(double.NaN);
                    return dst;
                }

                dst.SetPercentage(a.FloatValue / b.AsNumeric);
                return dst;
            case Integer or Float when b.Kind is Percentage:
                if (QuotientUnit(in a, in b) is { } percentageRightUnit) dst.SetFloat(a.AsNumeric / b.FloatValue, percentageRightUnit);
                else dst.SetFloat(double.NaN);
                return dst;
            case Vector when b.Kind is Integer:
                if (a.ObjectValue is GesSpatialValue vectorInt && b.IntegerValue != 0 && QuotientUnit(in a, in b) is { } vectorIntegerUnit)
                    dst.SetVector(vectorInt.X / b.IntegerValue, vectorInt.Y / b.IntegerValue, vectorInt.Z / b.IntegerValue, vectorIntegerUnit);
                else dst.SetFloat(double.NaN);
                return dst;
            case Vector when b.Kind is Float:
                if (a.ObjectValue is GesSpatialValue vectorFloat && double.IsFinite(b.FloatValue) && b.FloatValue != 0d && QuotientUnit(in a, in b) is { } vectorFloatUnit)
                    dst.SetVector(vectorFloat.X / b.FloatValue, vectorFloat.Y / b.FloatValue, vectorFloat.Z / b.FloatValue, vectorFloatUnit);
                else dst.SetFloat(double.NaN);
                return dst;
            case Vector when b.Kind is Percentage:
                if (a.ObjectValue is GesSpatialValue vectorPercent && b.FloatValue != 0d) dst.SetVector(vectorPercent.X / b.FloatValue, vectorPercent.Y / b.FloatValue, vectorPercent.Z / b.FloatValue, a.Unit);
                else dst.SetFloat(double.NaN);
                return dst;
            case Vector when b.Kind is Nothing:
                dst.SetNothing();
                return dst;
            case Vector:
                var vectorDivisor = b.AsNumeric;
                if (a.ObjectValue is GesSpatialValue vector && !double.IsNaN(vectorDivisor) && double.IsFinite(vectorDivisor) && vectorDivisor != 0d && QuotientUnit(in a, in b) is { } vectorDivisorUnit)
                    dst.SetVector(vector.X / vectorDivisor, vector.Y / vectorDivisor, vector.Z / vectorDivisor, vectorDivisorUnit);
                else dst.SetFloat(double.NaN);
                return dst;
            case Point when b.Kind is Nothing:
                dst.SetNothing();
                return dst;
            case Point:
                dst.SetFloat(double.NaN);
                return dst;
            case Integer or Float or Percentage when b.Kind is Vector or Point:
                dst.SetFloat(double.NaN);
                return dst;
            case Percentage:
                if (b.Kind is Nothing)
                {
                    dst.SetNothing();
                    return dst;
                }

                var percentageDivisor = b.AsNumeric;
                if (double.IsNaN(percentageDivisor) || b.HasUnit || QuotientUnit(in a, in b) is not { } percentageFallbackUnit)
                {
                    dst.SetFloat(double.NaN);
                    return dst;
                }

                dst.SetPercentage(a.FloatValue / percentageDivisor);
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

        if (b.Kind is Vector or Point)
        {
            dst.SetFloat(double.NaN);
            return dst;
        }

        var aNum = a.AsNumeric;
        if (double.IsNaN(aNum))
        {
            dst.SetFloat(double.NaN);
            return dst;
        }

        var bNum = b.AsNumeric;
        if (double.IsNaN(bNum))
        {
            dst.SetFloat(double.NaN);
            return dst;
        }

        if (a.Kind is Percentage && b.HasUnit)
        {
            dst.SetFloat(double.NaN);
            return dst;
        }

        if (QuotientUnit(in a, in b) is not { } fallbackUnit)
        {
            dst.SetFloat(double.NaN);
            return dst;
        }

        var fallbackResult = aNum / bNum;
        if (a.Kind is Percentage) dst.SetPercentage(fallbackResult);
        else dst.SetFloat(fallbackResult, fallbackUnit);
        return dst;
    }
}
