using System.Runtime.CompilerServices;
using StepH.GameEventScript.Api;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.BytecodeExecutor;

internal static class VmRegisterBooleanLogic
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmOr(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        if(a.Kind is Text or Tag) a.UpdatedTextTruthinessCache();
        if(b.Kind is Text or Tag) b.UpdatedTextTruthinessCache();
        if (a.IsTruthDeterminate && b.IsTruthDeterminate) dst.SetBoolean(a.IsTrue || b.IsTrue);
        else if (a.IsTruthIndeterminate && b.IsTrue || a.IsTrue && b.IsTruthIndeterminate) dst.SetBoolean(true);
        else dst.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmAnd(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        if(a.Kind is Text or Tag) a.UpdatedTextTruthinessCache();
        if(b.Kind is Text or Tag) b.UpdatedTextTruthinessCache();
        if (a.IsTruthDeterminate && b.IsTruthDeterminate) dst.SetBoolean(a.IsTrue && b.IsTrue);
        else if (a.IsTruthIndeterminate && b.IsFalse || a.IsFalse && b.IsTruthIndeterminate) dst.SetBoolean(false);
        else dst.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmImplies(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        if(a.Kind is Text or Tag) a.UpdatedTextTruthinessCache();
        if(b.Kind is Text or Tag) b.UpdatedTextTruthinessCache();
        if (a.IsTruthDeterminate && b.IsTruthDeterminate) dst.SetBoolean(!a.IsTrue || b.IsTrue);
        else if (a.IsFalse && b.IsTruthIndeterminate || a.IsTruthIndeterminate && b.IsTrue) dst.SetBoolean(true);
        else dst.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmXor(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        if(a.Kind is Text or Tag) a.UpdatedTextTruthinessCache();
        if(b.Kind is Text or Tag) b.UpdatedTextTruthinessCache();
        if (a.IsTruthDeterminate && b.IsTruthDeterminate) dst.SetBoolean(a.IsTrue ^ b.IsTrue);
        else dst.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmNot(ref this VmValue dst, ref VmValue a)
    {
        if(a.Kind is Text or Tag) a.UpdatedTextTruthinessCache();
        if (a.IsTruthDeterminate) dst.SetBoolean(!a.IsTrue);
        else dst.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmChance(ref this VmValue dst, ref VmValue a)
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
            dst.SetBoolean(dst.OwningState.RandomGenerator.NextInclusiveFloat(0, 1.0) < ratio);
        }
    }
}