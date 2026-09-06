// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using StepH.GameEventScript.Runtime.Values;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.Runtime.VM;

internal static class GesVmRegisterBooleanLogic
{
    internal static void GesVmOr(this GesVmState vmState, ushort dstRegister, in GesValue a, in GesValue b)
    {
        if (a.IsTruthDeterminate && b.IsTruthDeterminate) vmState.SetBoolean(dstRegister, a.IsTrue || b.IsTrue);
        else if (a.IsTruthIndeterminate && b.IsTrue || a.IsTrue && b.IsTruthIndeterminate) vmState.SetBoolean(dstRegister, true);
        else vmState.SetNothing(dstRegister);
    }

    internal static void GesVmAnd(this GesVmState vmState, ushort dstRegister, in GesValue a, in GesValue b)
    {
        if (a.IsTruthDeterminate && b.IsTruthDeterminate) vmState.SetBoolean(dstRegister, a.IsTrue && b.IsTrue);
        else if (a.IsTruthIndeterminate && b.IsFalse || a.IsFalse && b.IsTruthIndeterminate) vmState.SetBoolean(dstRegister, false);
        else vmState.SetNothing(dstRegister);
    }

    internal static void GesVmImplies(this GesVmState vmState, ushort dstRegister, in GesValue a, in GesValue b)
    {
        if (a.IsTruthDeterminate && b.IsTruthDeterminate) vmState.SetBoolean(dstRegister, !a.IsTrue || b.IsTrue);
        else if (a.IsFalse && b.IsTruthIndeterminate || a.IsTruthIndeterminate && b.IsTrue) vmState.SetBoolean(dstRegister, true);
        else vmState.SetNothing(dstRegister);
    }

    internal static void GesVmXor(this GesVmState vmState, ushort dstRegister, in GesValue a, in GesValue b)
    {
        if (a.IsTruthDeterminate && b.IsTruthDeterminate) vmState.SetBoolean(dstRegister, a.IsTrue ^ b.IsTrue);
        else vmState.SetNothing(dstRegister);
    }

    internal static void GesVmNot(this GesVmState vmState, ushort dstRegister, in GesValue a)
    {
        if (a.IsTruthDeterminate) vmState.SetBoolean(dstRegister, !a.IsTrue);
        else vmState.SetNothing(dstRegister);
    }

    internal static void GesVmChance(this GesVmState vmState, ushort dstRegister, in GesValue a)
    {
        if (a.IsNothing || a.HasUnit)
        {
            vmState.SetNothing(dstRegister);
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
                vmState.SetNothing(dstRegister);
                return;
        }

        if (double.IsNaN(ratio) || double.IsInfinity(ratio))
        {
            vmState.SetNothing(dstRegister);
        }
        else if (ratio <= 0d)
        {
            vmState.SetBoolean(dstRegister, false);
        }
        else if (ratio >= 1d)
        {
            vmState.SetBoolean(dstRegister, true);
        }
        else
        {
            vmState.SetBoolean(dstRegister, vmState.RandomGenerator.NextFloat(0, 1.0) < ratio);
        }
    }
}
