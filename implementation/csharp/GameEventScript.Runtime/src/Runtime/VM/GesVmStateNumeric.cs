// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GameEventScriptBytecodeTypeKind;
using static GameEventScript.Runtime.VM.GesVmUnitCalculation;

namespace GameEventScript.Runtime.VM;

internal partial class GesVmState
{
    internal void MoveNumeric(ushort destination, ushort source)
    {
        ref readonly var value = ref Register(source);
        if (!value.IsNumeric) SetNothing(destination);
        else if (value.Kind is GameEventScriptBytecodeTypeKind.Boolean or Dice) SetInteger(destination, value.IntegerValue);
        else SetValue(destination, in value);
    }

    internal void AddNumeric(ushort destinationRegister, ushort left, ushort right)
    {
        ref readonly var a = ref Register(left);
        ref readonly var b = ref Register(right);
        // Numeric scalar cases write directly; no generic value result or collection dispatch.
        switch (a.NumericKind)
        {
            case Integer when b.NumericKind is Integer:
                if (SameUnit(in a, in b) is { } unit)
                {
                    if (GameEventScriptNumber.AddExact(a.IntegerValue, b.IntegerValue) is { } integerResult) SetInteger(destinationRegister, integerResult, unit);
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

    internal void SubtractNumeric(ushort dst, ushort aRegister, ushort bRegister)
    {
        ref readonly var a = ref Register(aRegister);
        ref readonly var b = ref Register(bRegister);
        if (!a.IsNumeric || !b.IsNumeric)
        {
            SetNothing(dst);
            return;
        }
        if (a.Kind is GameEventScriptBytecodeTypeKind.Boolean or Dice || b.Kind is GameEventScriptBytecodeTypeKind.Boolean or Dice)
        {
            var numeric0 = NumericOperand(in a);
            var numeric1 = NumericOperand(in b);
            this.GesVmSubtract(dst, in numeric0, in numeric1);
            return;
        }

        this.GesVmSubtract(dst, in a, in b);
    }

    internal void MultiplyNumeric(ushort dst, ushort aRegister, ushort bRegister)
    {
        ref readonly var a = ref Register(aRegister);
        ref readonly var b = ref Register(bRegister);
        if (!a.IsNumeric || !b.IsNumeric)
        {
            SetNothing(dst);
            return;
        }
        if (a.Kind is GameEventScriptBytecodeTypeKind.Boolean or Dice || b.Kind is GameEventScriptBytecodeTypeKind.Boolean or Dice)
        {
            var numeric0 = NumericOperand(in a);
            var numeric1 = NumericOperand(in b);
            this.GesVmMultiply(dst, in numeric0, in numeric1);
            return;
        }

        this.GesVmMultiply(dst, in a, in b);
    }

    internal void DivideNumeric(ushort destinationRegister, ushort aRegister, ushort bRegister)
    {
        ref readonly var a = ref Register(aRegister);
        ref readonly var b = ref Register(bRegister);
        if (!a.IsNumeric || !b.IsNumeric)
        {
            SetNothing(destinationRegister);
            return;
        }
        if (a.Kind is GameEventScriptBytecodeTypeKind.Boolean or Dice || b.Kind is GameEventScriptBytecodeTypeKind.Boolean or Dice)
        {
            var numeric0 = NumericOperand(in a);
            var numeric1 = NumericOperand(in b);
            this.GesVmDivide(destinationRegister, in numeric0, in numeric1);
            return;
        }

        this.GesVmDivide(destinationRegister, in a, in b);
    }

    internal void PowerNumeric(ushort destinationRegister, ushort aRegister, ushort bRegister)
    {
        ref readonly var a = ref Register(aRegister);
        ref readonly var b = ref Register(bRegister);
        if (!a.IsNumeric || !b.IsNumeric)
        {
            SetNothing(destinationRegister);
            return;
        }
        if (a.Kind is GameEventScriptBytecodeTypeKind.Boolean or Dice || b.Kind is GameEventScriptBytecodeTypeKind.Boolean or Dice)
        {
            var numeric0 = NumericOperand(in a);
            var numeric1 = NumericOperand(in b);
            this.GesVmPower(destinationRegister, in numeric0, in numeric1);
            return;
        }

        this.GesVmPower(destinationRegister, in a, in b);
    }

    internal void FloorDivideNumeric(ushort destinationRegister, ushort aRegister, ushort bRegister)
    {
        ref readonly var a = ref Register(aRegister);
        ref readonly var b = ref Register(bRegister);
        if (!a.IsNumeric || !b.IsNumeric)
        {
            SetNothing(destinationRegister);
            return;
        }
        if (a.Kind is GameEventScriptBytecodeTypeKind.Boolean or Dice || b.Kind is GameEventScriptBytecodeTypeKind.Boolean or Dice)
        {
            var numeric0 = NumericOperand(in a);
            var numeric1 = NumericOperand(in b);
            this.GesVmFloorDivide(destinationRegister, in numeric0, in numeric1);
            return;
        }

        this.GesVmFloorDivide(destinationRegister, in a, in b);
    }

    internal void ModuloNumeric(ushort destinationRegister, ushort aRegister, ushort bRegister)
    {
        ref readonly var a = ref Register(aRegister);
        ref readonly var b = ref Register(bRegister);
        if (!a.IsNumeric || !b.IsNumeric)
        {
            SetNothing(destinationRegister);
            return;
        }
        if (a.Kind is GameEventScriptBytecodeTypeKind.Boolean or Dice || b.Kind is GameEventScriptBytecodeTypeKind.Boolean or Dice)
        {
            var numeric0 = NumericOperand(in a);
            var numeric1 = NumericOperand(in b);
            this.GesVmModulo(destinationRegister, in numeric0, in numeric1);
            return;
        }

        this.GesVmModulo(destinationRegister, in a, in b);
    }

    internal void RemainderNumeric(ushort destinationRegister, ushort aRegister, ushort bRegister)
    {
        ref readonly var a = ref Register(aRegister);
        ref readonly var b = ref Register(bRegister);
        if (!a.IsNumeric || !b.IsNumeric)
        {
            SetNothing(destinationRegister);
            return;
        }
        if (a.Kind is GameEventScriptBytecodeTypeKind.Boolean or Dice || b.Kind is GameEventScriptBytecodeTypeKind.Boolean or Dice)
        {
            var numeric0 = NumericOperand(in a);
            var numeric1 = NumericOperand(in b);
            this.GesVmRemainder(destinationRegister, in numeric0, in numeric1);
            return;
        }

        this.GesVmRemainder(destinationRegister, in a, in b);
    }

    internal void MinNumeric(ushort destinationRegister, ushort aRegister, ushort bRegister)
    {
        ref readonly var a = ref Register(aRegister);
        ref readonly var b = ref Register(bRegister);
        if (!a.IsNumeric || !b.IsNumeric)
        {
            SetNothing(destinationRegister);
            return;
        }
        if (a.Kind is GameEventScriptBytecodeTypeKind.Boolean or Dice || b.Kind is GameEventScriptBytecodeTypeKind.Boolean or Dice)
        {
            var numeric0 = NumericOperand(in a);
            var numeric1 = NumericOperand(in b);
            this.GesVmMin(destinationRegister, in numeric0, in numeric1);
            return;
        }

        this.GesVmMin(destinationRegister, in a, in b);
    }

    internal void MaxNumeric(ushort destinationRegister, ushort aRegister, ushort bRegister)
    {
        ref readonly var a = ref Register(aRegister);
        ref readonly var b = ref Register(bRegister);
        if (!a.IsNumeric || !b.IsNumeric)
        {
            SetNothing(destinationRegister);
            return;
        }
        if (a.Kind is GameEventScriptBytecodeTypeKind.Boolean or Dice || b.Kind is GameEventScriptBytecodeTypeKind.Boolean or Dice)
        {
            var numeric0 = NumericOperand(in a);
            var numeric1 = NumericOperand(in b);
            this.GesVmMax(destinationRegister, in numeric0, in numeric1);
            return;
        }

        this.GesVmMax(destinationRegister, in a, in b);
    }

    internal void NegateNumeric(ushort destinationRegister, ushort aRegister)
    {
        ref readonly var a = ref Register(aRegister);
        if (!a.IsNumeric)
        {
            SetNothing(destinationRegister);
            return;
        }
        if (a.Kind is GameEventScriptBytecodeTypeKind.Boolean or Dice)
        {
            var numeric0 = NumericOperand(in a);
            this.GesVmNegate(destinationRegister, in numeric0);
            return;
        }

        this.GesVmNegate(destinationRegister, in a);
    }

    internal void AbsNumeric(ushort destinationRegister, ushort aRegister)
    {
        ref readonly var a = ref Register(aRegister);
        if (!a.IsNumeric)
        {
            SetNothing(destinationRegister);
            return;
        }
        if (a.Kind is GameEventScriptBytecodeTypeKind.Boolean or Dice)
        {
            var numeric0 = NumericOperand(in a);
            this.GesVmAbs(destinationRegister, in numeric0);
            return;
        }

        this.GesVmAbs(destinationRegister, in a);
    }

    internal void ClampNumeric(ushort destinationRegister, ushort valueRegister, ushort minRegister, ushort maxRegister)
    {
        ref readonly var value = ref Register(valueRegister);
        ref readonly var min = ref Register(minRegister);
        ref readonly var max = ref Register(maxRegister);
        if (!value.IsNumeric || !min.IsNumeric || !max.IsNumeric)
        {
            SetNothing(destinationRegister);
            return;
        }
        if (value.Kind is GameEventScriptBytecodeTypeKind.Boolean or Dice || min.Kind is GameEventScriptBytecodeTypeKind.Boolean or Dice || max.Kind is GameEventScriptBytecodeTypeKind.Boolean or Dice)
        {
            var numeric0 = NumericOperand(in value);
            var numeric1 = NumericOperand(in min);
            var numeric2 = NumericOperand(in max);
            this.GesVmClamp(destinationRegister, in numeric0, in numeric1, in numeric2);
            return;
        }

        this.GesVmClamp(destinationRegister, in value, in min, in max);
    }

    private static GesValue NumericOperand(in GesValue value)
        => value.Kind is GameEventScriptBytecodeTypeKind.Boolean or Dice ? GesValue.GesInteger(value.IntegerValue) : value;
}
