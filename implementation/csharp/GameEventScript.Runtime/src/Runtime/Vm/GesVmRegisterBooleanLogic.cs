// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal static class GesVmRegisterBooleanLogic
{
    internal static void GesVmOr(this GesVirtualMachine vm, ushort dstRegister, in GesValue a, in GesValue b)
    {
        if (a.IsTruthDeterminate && b.IsTruthDeterminate) vm.SetBoolean(dstRegister, a.IsTrue || b.IsTrue);
        else if (a.IsTruthIndeterminate && b.IsTrue || a.IsTrue && b.IsTruthIndeterminate) vm.SetBoolean(dstRegister, true);
        else vm.SetNothing(dstRegister);
    }

    internal static void GesVmAnd(this GesVirtualMachine vm, ushort dstRegister, in GesValue a, in GesValue b)
    {
        if (a.IsTruthDeterminate && b.IsTruthDeterminate) vm.SetBoolean(dstRegister, a.IsTrue && b.IsTrue);
        else if (a.IsTruthIndeterminate && b.IsFalse || a.IsFalse && b.IsTruthIndeterminate) vm.SetBoolean(dstRegister, false);
        else vm.SetNothing(dstRegister);
    }

    internal static void GesVmImplies(this GesVirtualMachine vm, ushort dstRegister, in GesValue a, in GesValue b)
    {
        if (a.IsTruthDeterminate && b.IsTruthDeterminate) vm.SetBoolean(dstRegister, !a.IsTrue || b.IsTrue);
        else if (a.IsFalse && b.IsTruthIndeterminate || a.IsTruthIndeterminate && b.IsTrue) vm.SetBoolean(dstRegister, true);
        else vm.SetNothing(dstRegister);
    }

    internal static void GesVmXor(this GesVirtualMachine vm, ushort dstRegister, in GesValue a, in GesValue b)
    {
        if (a.IsTruthDeterminate && b.IsTruthDeterminate) vm.SetBoolean(dstRegister, a.IsTrue ^ b.IsTrue);
        else vm.SetNothing(dstRegister);
    }

    internal static void GesVmNot(this GesVirtualMachine vm, ushort dstRegister, in GesValue a)
    {
        if (a.IsTruthDeterminate) vm.SetBoolean(dstRegister, !a.IsTrue);
        else vm.SetNothing(dstRegister);
    }

    internal static void GesVmChance(this GesVirtualMachine vm, ushort dstRegister, in GesValue a)
    {
        if (a.IsNothing || a.HasUnit)
        {
            vm.SetNothing(dstRegister);
            return;
        }

        var ratio = 0d;
        switch (a.Kind)
        {
            case Integer:
                ratio = a.IntegerValue / 100d;
                break;
            case Float:
                ratio = a.FloatValue is > 1d or < -1d ? a.FloatValue / 100d : a.FloatValue;
                break;
            case Percentage:
                ratio = a.FloatValue;
                break;
            case Tag when a.IsNumeric:
                ratio = a.AsNumeric;
                ratio = ratio is > 1d or < -1d ? ratio / 100d : ratio;
                break;
            default:
                vm.SetNothing(dstRegister);
                return;
        }

        if (double.IsNaN(ratio) || double.IsInfinity(ratio))
        {
            vm.SetNothing(dstRegister);
        }
        else if (ratio <= 0d)
        {
            vm.SetBoolean(dstRegister, false);
        }
        else if (ratio >= 1d)
        {
            vm.SetBoolean(dstRegister, true);
        }
        else
        {
            vm.SetBoolean(dstRegister, vm.RandomGenerator.NextFloat(0, 1.0) < ratio);
        }
    }
}
