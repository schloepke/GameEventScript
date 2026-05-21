using System.Runtime.CompilerServices;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.BytecodeExecutor;

internal static class VmRegisterBooleanLogic
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmOr(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        if (a.IsNotNothing && b.IsNotNothing) dst.SetBoolean(a.IsTrue || b.IsTrue);
        else if (a.IsNothing && b.IsTrue || a.IsTrue && b.IsNothing) dst.SetBoolean(true);
        else dst.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmAnd(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        if (a.IsNotNothing && b.IsNotNothing) dst.SetBoolean(a.IsTrue && b.IsTrue);
        else if (a.IsNothing && b.IsFalse || a.IsFalse && b.IsNothing) dst.SetBoolean(false);
        else dst.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmImplies(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        if (a.IsNotNothing && b.IsNotNothing) dst.SetBoolean(!a.IsTrue || b.IsTrue);
        else if (a.IsFalse && b.IsNothing || a.IsNothing && b.IsTrue) dst.SetBoolean(true);
        else dst.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmXor(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        if (a.IsNotNothing && b.IsNotNothing) dst.SetBoolean(a.IsTrue ^ b.IsTrue);
        else dst.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmNot(ref this VmValue dst, ref VmValue a)
    {
        if (a.IsNotNothing) dst.SetBoolean(!a.IsTrue);
        else dst.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmChance(ref this VmValue dst, ref VmValue a, ref VmState state)
    {
        if (a.IsNothing || a.HasUnit)
        {
            dst.SetNothing();
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
            default:
                dst.SetNothing();
                return;
        }

        if (double.IsNaN(ratio) || double.IsInfinity(ratio))
        {
            dst.SetNothing();
        }
        else
        {
            dst.SetBoolean(state.RandomGenerator.NextInclusiveFloat(0, 1.0) < ratio);
        }
    }
}