#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System.Runtime.CompilerServices;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeInstructionUnit;

namespace StepH.GameEventScript.BytecodeExecutor;

public static class VmRegisterUnitCalculation
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasSameUnit(ref VmValue a, ref VmValue b)
        => a.Unit == b.Unit;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameEventScriptBytecodeInstructionUnit DecodeNumericUnit(byte unitAndFlags)
        => (GameEventScriptBytecodeInstructionUnit)(unitAndFlags & 0x1F);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TrySameUnit(ref VmValue a, ref VmValue b, out GameEventScriptBytecodeInstructionUnit unit)
    {
        if (a.Unit == b.Unit)
        {
            unit = a.Unit;
            return true;
        }

        unit = UnitNone;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryProductUnit(ref VmValue a, ref VmValue b, out GameEventScriptBytecodeInstructionUnit unit)
    {
        if (a.HasUnit && b.HasUnit)
        {
            unit = UnitNone;
            return false;
        }

        unit = a.Unit is UnitNone ? b.Unit : a.Unit;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryQuotientUnit(ref VmValue a, ref VmValue b, out GameEventScriptBytecodeInstructionUnit unit)
    {
        switch (a.HasUnit)
        {
            case false when !b.HasUnit:
                unit = UnitNone;
                return true;
            case true when !b.HasUnit:
                unit = a.Unit;
                return true;
            default:
                unit = UnitNone;
                return a.Unit == b.Unit;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryPowerUnit(ref VmValue value, double exponent, out GameEventScriptBytecodeInstructionUnit unit)
    {
        if (!value.HasUnit || exponent == 0.0)
        {
            unit = UnitNone;
            return true;
        }

        if (exponent == 1.0)
        {
            unit = value.Unit;
            return true;
        }

        unit = UnitNone;
        return false;
    }
}