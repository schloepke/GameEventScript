// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using GameEventScript.Syntax;
using static GameEventScript.Api.GesBytecodeTypeKind;
using static GameEventScript.Runtime.Vm.GesVmUnitCalculation;

namespace GameEventScript.Runtime.Vm;

[SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator")]
internal partial class GesVirtualMachine
{
    private void ExecuteMax(ushort destinationRegister, in GesValue a, in GesValue b)
    {

        var dst = MaxValues(in a, in b, this);
        SetValue(destinationRegister, in dst);
    }

    private void ExecuteMaxNumeric(ushort destinationRegister, ushort aRegister, ushort bRegister)
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
            ExecuteMax(destinationRegister, in numeric0, in numeric1);
            return;
        }

        ExecuteMax(destinationRegister, in a, in b);
    }

    private static GesValue MaxValues(in GesValue a, in GesValue b, GesVirtualMachine state)
    {
        var dst = new GesValue();
        switch (a.Kind)
        {
            case Integer when b.Kind is Integer:
                if (SameUnit(in a, in b) is null) dst.SetFloat(double.NaN);
                else dst = b.IntegerValue > a.IntegerValue ? b : a;
                return dst;
            case Float when b.Kind is Float:
                if (SameUnit(in a, in b) is null || double.IsNaN(a.FloatValue) || double.IsNaN(b.FloatValue)) dst.SetFloat(double.NaN);
                else dst = b.FloatValue > a.FloatValue ? b : a;
                return dst;
            case Float or Integer when b.Kind is Float or Integer:
                if (SameUnit(in a, in b) is null)
                {
                    dst.SetFloat(double.NaN);
                    return dst;
                }

                var fastLeft = a.AsNumeric;
                var fastRight = b.AsNumeric;
                if (double.IsNaN(fastLeft) || double.IsNaN(fastRight)) dst.SetFloat(double.NaN);
                else dst = fastRight > fastLeft ? b : a;
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

        var leftNumber = double.NaN;
        var leftIsNumeric = false;
        var rightNumber = double.NaN;
        var rightIsNumeric = false;
        var leftRank = 8;
        var rightRank = 8;
        string? leftText = null;
        string? rightText = null;

        switch (a.Kind)
        {
            case Integer:
                leftNumber = a.IntegerValue;
                leftIsNumeric = true;
                leftRank = 1;
                break;
            case Float:
                leftNumber = a.FloatValue;
                leftIsNumeric = true;
                leftRank = 1;
                break;
            case Percentage:
                leftNumber = a.FloatValue;
                leftIsNumeric = true;
                leftRank = 1;
                break;
            case GesBytecodeTypeKind.Boolean:
                leftNumber = a.IsTrue ? 1d : 0d;
                leftIsNumeric = true;
                leftRank = 6;
                break;
            case Text:
                leftText = a.TextValue;
                leftRank = 2;
                break;
            case Tag:
                leftText = a.TextValue;
                leftRank = 3;
                break;
            case Vector:
                leftRank = 4;
                break;
            case Point:
                leftRank = 5;
                break;
            case GesBytecodeTypeKind.Range:
                leftRank = 8;
                break;
            case Message:
                leftRank = 9;
                break;
            case Handler:
                leftRank = 10;
                break;
            case List:
                leftRank = 11;
                break;
            case Map:
                leftRank = 12;
                break;
            case Dice:
                leftRank = 13;
                break;
        }

        switch (b.Kind)
        {
            case Integer:
                rightNumber = b.IntegerValue;
                rightIsNumeric = true;
                rightRank = 1;
                break;
            case Float:
                rightNumber = b.FloatValue;
                rightIsNumeric = true;
                rightRank = 1;
                break;
            case Percentage:
                rightNumber = b.FloatValue;
                rightIsNumeric = true;
                rightRank = 1;
                break;
            case GesBytecodeTypeKind.Boolean:
                rightNumber = b.IsTrue ? 1d : 0d;
                rightIsNumeric = true;
                rightRank = 6;
                break;
            case Text:
                rightText = b.TextValue;
                rightRank = 2;
                break;
            case Tag:
                rightText = b.TextValue;
                rightRank = 3;
                break;
            case Vector:
                rightRank = 4;
                break;
            case Point:
                rightRank = 5;
                break;
            case GesBytecodeTypeKind.Range:
                rightRank = 8;
                break;
            case Message:
                rightRank = 9;
                break;
            case Handler:
                rightRank = 10;
                break;
            case List:
                rightRank = 11;
                break;
            case Map:
                rightRank = 12;
                break;
            case Dice:
                rightRank = 13;
                break;
        }

        if (leftIsNumeric && rightIsNumeric)
        {
            if (SameUnit(in a, in b) is null || double.IsNaN(leftNumber) || double.IsNaN(rightNumber))
            {
                dst.SetFloat(double.NaN);
                return dst;
            }

            dst = rightNumber > leftNumber ? b : a;
            return dst;
        }

        if (leftRank != rightRank)
        {
            dst = rightRank > leftRank ? b : a;
            return dst;
        }

        var comparison = 0;
        switch (a.Kind)
        {
            case Text when b.Kind is Text:
                leftText ??= a.TextValue;
                rightText ??= b.TextValue;
                comparison = GesText.CompareScalarOrdinal(rightText, leftText);
                break;
            case Tag when b.Kind is Tag:
                leftText ??= a.TextValue;
                rightText ??= b.TextValue;
                comparison = GesText.CompareScalarOrdinal(rightText, leftText);
                break;
            case Vector when b.Kind is Vector:
            case Point when b.Kind is Point:
                if (a.Unit != b.Unit)
                {
                    comparison = b.Unit.CompareTo(a.Unit);
                    break;
                }

                if (a.ObjectValue is GesSpatialValue leftTriplet && b.ObjectValue is GesSpatialValue rightTriplet)
                {
                    comparison = rightTriplet.X.CompareTo(leftTriplet.X);
                    if (comparison == 0) comparison = rightTriplet.Y.CompareTo(leftTriplet.Y);
                    if (comparison == 0) comparison = rightTriplet.Z.CompareTo(leftTriplet.Z);
                }

                break;
            case GesBytecodeTypeKind.Boolean when b.Kind is GesBytecodeTypeKind.Boolean:
                comparison = b.IsTrue.CompareTo(a.IsTrue);
                break;
            default:
                comparison = b.Kind.CompareTo(a.Kind);
                break;
        }

        dst = comparison > 0 ? b : a;
        return dst;
    }
}
