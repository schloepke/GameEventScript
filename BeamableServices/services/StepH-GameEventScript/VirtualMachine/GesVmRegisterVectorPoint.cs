using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeInstructionUnit;

namespace StepH.GameEventScript.VirtualMachine;

internal static class GesVmRegisterVectorPoint
{
    internal static void GesVmCreateVector(this GesVmState vmState, ushort destinationRegister, short start)
    {
        if (start == 0 && vmState.StageLength > 0)
        {
            ref readonly var first = ref vmState.RegisterStaged(0);
            switch (first.Kind)
            {
                case Vector:
                case Point:
                    if (vmState.StageLength > 2)
                    {
                        vmState.SetNothing(destinationRegister);
                        return;
                    }

                    if (first.ObjectValue is not GesVmValueVectorPoint triplet)
                    {
                        vmState.SetNothing(destinationRegister);
                        return;
                    }

                    var liftedZ = triplet.Z;
                    var liftedUnit = first.Unit;
                    if (vmState.StageLength > 1)
                    {
                        ref readonly var zSlot = ref vmState.RegisterStaged(1);
                        liftedZ = zSlot.AsNumericWithUnit(out var liftedUnitZ);
                        if (double.IsNaN(liftedZ) || liftedUnit != liftedUnitZ)
                        {
                            vmState.SetNothing(destinationRegister);
                            return;
                        }
                    }

                    vmState.SetVector(destinationRegister, triplet.X, triplet.Y, liftedZ, liftedUnit);
                    return;
            }
        }

        ushort i = 0;
        var unitX = UnitNothing;
        var unitY = UnitNothing;
        var unitZ = UnitNothing;
        var x = start == 0 && i < vmState.StageLength ? vmState.RegisterStaged(i++).AsNumericWithUnit(out unitX) : 0.0d;
        var y = start <= 1 && i < vmState.StageLength ? vmState.RegisterStaged(i++).AsNumericWithUnit(out unitY) : 0.0d;
        var z = start <= 2 && i < vmState.StageLength ? vmState.RegisterStaged(i).AsNumericWithUnit(out unitZ) : 0.0d;
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
            vmState.SetNothing(destinationRegister);
        }
        else
        {
            vmState.SetVector(destinationRegister, x, y, z, unit);
        }
    }
    internal static void GesVmCreatePoint(this GesVmState vmState, ushort destinationRegister, short start)
    {
        if (start == 0 && vmState.StageLength > 0)
        {
            ref readonly var first = ref vmState.RegisterStaged(0);
            switch (first.Kind)
            {
                case Vector:
                case Point:
                    if (vmState.StageLength > 2)
                    {
                        vmState.SetNothing(destinationRegister);
                        return;
                    }

                    if (first.ObjectValue is not GesVmValueVectorPoint triplet)
                    {
                        vmState.SetNothing(destinationRegister);
                        return;
                    }

                    var liftedZ = triplet.Z;
                    var liftedUnit = first.Unit;
                    if (vmState.StageLength > 1)
                    {
                        ref readonly var zSlot = ref vmState.RegisterStaged(1);
                        liftedZ = zSlot.AsNumericWithUnit(out var liftedUnitZ);
                        if (double.IsNaN(liftedZ) || liftedUnit != liftedUnitZ)
                        {
                            vmState.SetNothing(destinationRegister);
                            return;
                        }
                    }

                    vmState.SetPoint(destinationRegister, triplet.X, triplet.Y, liftedZ, liftedUnit);
                    return;
            }
        }

        ushort i = 0;
        var unitX = UnitNothing;
        var unitY = UnitNothing;
        var unitZ = UnitNothing;
        var x = start == 0 && i < vmState.StageLength ? vmState.RegisterStaged(i++).AsNumericWithUnit(out unitX) : 0.0d;
        var y = start <= 1 && i < vmState.StageLength ? vmState.RegisterStaged(i++).AsNumericWithUnit(out unitY) : 0.0d;
        var z = start <= 2 && i < vmState.StageLength ? vmState.RegisterStaged(i).AsNumericWithUnit(out unitZ) : 0.0d;
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
            vmState.SetNothing(destinationRegister);
        }
        else
        {
            vmState.SetPoint(destinationRegister, x, y, z, unit);
        }
    }
    
}
