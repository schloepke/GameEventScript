using System.Runtime.CompilerServices;
using StepH.GameEventScript.Api;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeInstructionUnit;

namespace StepH.GameEventScript.BytecodeExecutor;

internal static class VmRegisterVectorPoint
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmCreateVector(ref this VmValue dst, short start, VmState state)
    {
        ushort i = 0;
        var unitX = UnitNothing;
        var unitY = UnitNothing;
        var unitZ = UnitNothing;
        var x = start == 0 && i < state.StageLength ? state.RegisterStaged(i++).AsNumericWithUnit(out unitX) : 0.0d;
        var y = start <= 1 && i < state.StageLength ? state.RegisterStaged(i++).AsNumericWithUnit(out unitY) : 0.0d;
        var z = start <= 2 && i < state.StageLength ? state.RegisterStaged(i).AsNumericWithUnit(out unitZ) : 0.0d;
        var unit = unitX;
        if (unitX is UnitNothing)
        {
            if (unitY is UnitNothing) unit = unitZ is UnitNothing ? UnitNone : unitZ;
            else unit = unitZ is UnitNothing || unitY == unitZ ? unitY : UnitNothing;
        }
        else if (unitY is not UnitNothing)
        {
            if(unitX != unitY || (unitZ is not UnitNothing && unitX != unitZ)) unit = UnitNothing;
        } else if (unitZ is not UnitNothing && unitX != unitZ)
        {
            unit = UnitNothing;
        }
        if (unit is UnitNothing || double.IsNaN(x) || double.IsNaN(y) || double.IsNaN(z))
        {
            dst.SetNothing();
        }
        else
        {
            dst.SetVector(x, y, z, unit);
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmCreatePoint(ref this VmValue dst, short start, VmState state)
    {
        ushort i = 0;
        var unitX = UnitNothing;
        var unitY = UnitNothing;
        var unitZ = UnitNothing;
        var x = start == 0 && i < state.StageLength ? state.RegisterStaged(i++).AsNumericWithUnit(out unitX) : 0.0d;
        var y = start <= 1 && i < state.StageLength ? state.RegisterStaged(i++).AsNumericWithUnit(out unitY) : 0.0d;
        var z = start <= 2 && i < state.StageLength ? state.RegisterStaged(i).AsNumericWithUnit(out unitZ) : 0.0d;
        var unit = unitX;
        if (unitX is UnitNothing)
        {
            if (unitY is UnitNothing) unit = unitZ is UnitNothing ? UnitNone : unitZ;
            else unit = unitZ is UnitNothing || unitY == unitZ ? unitY : UnitNothing;
        }
        else if (unitY is not UnitNothing)
        {
            if(unitX != unitY || (unitZ is not UnitNothing && unitX != unitZ)) unit = UnitNothing;
        } else if (unitZ is not UnitNothing && unitX != unitZ)
        {
            unit = UnitNothing;
        }
        if (unit is UnitNothing || double.IsNaN(x) || double.IsNaN(y) || double.IsNaN(z))
        {
            dst.SetNothing();
        }
        else
        {
            dst.SetPoint(x, y, z, unit);
        }
    }
    
}