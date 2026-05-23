using System.Runtime.CompilerServices;
using StepH.GameEventScript.Api;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeInstructionUnit;

namespace StepH.GameEventScript.BytecodeExecutor;

internal static class VmRegisterUnitCalculation
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmCastUnit(ref this VmValue dst, ref VmValue xSlot, GameEventScriptBytecodeInstructionUnit unit)
    {
        switch (xSlot.Kind)
        {
            case Integer:
                if (unit is UnitNone || xSlot.Unit is UnitNone || xSlot.Unit == unit) dst.SetInteger(xSlot.IntegerValue, unit);
                else dst.SetFloat(double.NaN);
                return;
            case Float:
                if ((unit is UnitNone || xSlot.Unit is UnitNone || xSlot.Unit == unit) && double.IsFinite(xSlot.FloatValue)) dst.SetFloat(xSlot.FloatValue, unit);
                else dst.SetFloat(double.NaN);
                return;
            case Vector:
                if (xSlot.ObjectValue is VmFloatTriplet vector && (unit is UnitNone || xSlot.Unit is UnitNone || xSlot.Unit == unit)) dst.SetObject(Vector, vector, unit);
                else dst.SetFloat(double.NaN);
                return;
            case Point:
                if (xSlot.ObjectValue is VmFloatTriplet point && (unit is UnitNone || xSlot.Unit is UnitNone || xSlot.Unit == unit)) dst.SetObject(Point, point, unit);
                else dst.SetFloat(double.NaN);
                return;
            default:
                dst.SetFloat(double.NaN);
                return;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmCheckUnit(ref this VmValue dst, ref VmValue xSlot, GameEventScriptBytecodeInstructionUnit unit)
    {
        dst.SetBoolean(xSlot.Kind is Integer or Float or Vector or Point && xSlot.Unit == unit);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool HasSameUnit(ref VmValue a, ref VmValue b) => a.Unit == b.Unit;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static GameEventScriptBytecodeInstructionUnit DecodeNumericUnit(byte unitAndFlags) => (GameEventScriptBytecodeInstructionUnit)(unitAndFlags & 0x1F);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool TrySameUnit(ref VmValue a, ref VmValue b, out GameEventScriptBytecodeInstructionUnit unit)
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
    internal static bool TrySameUnit(ref VmValue a, ref VmValue b, ref VmValue c, out GameEventScriptBytecodeInstructionUnit unit)
    {
        if (a.Unit == b.Unit && b.Unit == c.Unit)
        {
            unit = a.Unit;
            return true;
        }

        unit = UnitNone;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool TryProductUnit(ref VmValue a, ref VmValue b, out GameEventScriptBytecodeInstructionUnit unit)
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
    internal static bool TryQuotientUnit(ref VmValue a, ref VmValue b, out GameEventScriptBytecodeInstructionUnit unit)
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
    internal static bool TryPowerUnit(ref VmValue value, double exponent, out GameEventScriptBytecodeInstructionUnit unit)
    {
        if (!value.HasUnit || exponent == 0.0)
        {
            unit = UnitNone;
            return true;
        }

        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (exponent == 1.0)
        {
            unit = value.Unit;
            return true;
        }

        unit = UnitNone;
        return false;
    }
    
}
