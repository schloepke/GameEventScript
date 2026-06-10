using StepH.GameEventScript.Api;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeInstructionUnit;

namespace StepH.GameEventScript.VirtualMachine;

internal static class GesVmRegisterVectorPoint
{
    internal static void GesVmCreateVector(ref this GesVmValue dst, short start, GesVmState state)
    {
        if (start == 0 && state.StageLength > 0)
        {
            ref var first = ref state.RegisterStaged(0);
            switch (first.Kind)
            {
                case Vector:
                case Point:
                    if (state.StageLength > 2)
                    {
                        dst.SetNothing();
                        return;
                    }

                    if (first.ObjectValue is not GesVmFloatTriplet triplet)
                    {
                        dst.SetNothing();
                        return;
                    }

                    var liftedZ = triplet.Z;
                    var liftedUnit = first.Unit;
                    if (state.StageLength > 1)
                    {
                        ref var zSlot = ref state.RegisterStaged(1);
                        liftedZ = zSlot.AsNumericWithUnit(out var liftedUnitZ);
                        if (double.IsNaN(liftedZ) || liftedUnit != liftedUnitZ)
                        {
                            dst.SetNothing();
                            return;
                        }
                    }

                    dst.SetVector(triplet.X, triplet.Y, liftedZ, liftedUnit);
                    return;
            }
        }

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
    internal static void GesVmCreatePoint(ref this GesVmValue dst, short start, GesVmState state)
    {
        if (start == 0 && state.StageLength > 0)
        {
            ref var first = ref state.RegisterStaged(0);
            switch (first.Kind)
            {
                case Vector:
                case Point:
                    if (state.StageLength > 2)
                    {
                        dst.SetNothing();
                        return;
                    }

                    if (first.ObjectValue is not GesVmFloatTriplet triplet)
                    {
                        dst.SetNothing();
                        return;
                    }

                    var liftedZ = triplet.Z;
                    var liftedUnit = first.Unit;
                    if (state.StageLength > 1)
                    {
                        ref var zSlot = ref state.RegisterStaged(1);
                        liftedZ = zSlot.AsNumericWithUnit(out var liftedUnitZ);
                        if (double.IsNaN(liftedZ) || liftedUnit != liftedUnitZ)
                        {
                            dst.SetNothing();
                            return;
                        }
                    }

                    dst.SetPoint(triplet.X, triplet.Y, liftedZ, liftedUnit);
                    return;
            }
        }

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
