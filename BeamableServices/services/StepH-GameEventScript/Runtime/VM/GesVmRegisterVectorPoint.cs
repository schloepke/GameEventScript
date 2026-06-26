using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeInstructionUnit;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime.Values;

namespace StepH.GameEventScript.Runtime.VM;

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

                    if (first.ObjectValue is not GesValueVectorPoint triplet)
                    {
                        vmState.SetNothing(destinationRegister);
                        return;
                    }

                    var liftedZ = triplet.Z;
                    var liftedUnit = first.Unit;
                    if (vmState.StageLength > 1)
                    {
                        ref readonly var zValue = ref vmState.RegisterStaged(1);
                        liftedZ = zValue.AsNumericWithUnit(out var liftedUnitZ);
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
        GameEventScriptBytecodeInstructionUnit? unitX = null;
        GameEventScriptBytecodeInstructionUnit? unitY = null;
        GameEventScriptBytecodeInstructionUnit? unitZ = null;
        var x = 0.0d;
        var y = 0.0d;
        var z = 0.0d;
        if (start == 0 && i < vmState.StageLength)
        {
            x = vmState.RegisterStaged(i++).AsNumericWithUnit(out var readUnitX);
            unitX = readUnitX;
        }
        if (start <= 1 && i < vmState.StageLength)
        {
            y = vmState.RegisterStaged(i++).AsNumericWithUnit(out var readUnitY);
            unitY = readUnitY;
        }
        if (start <= 2 && i < vmState.StageLength)
        {
            z = vmState.RegisterStaged(i).AsNumericWithUnit(out var readUnitZ);
            unitZ = readUnitZ;
        }
        var unit = unitX;
        if (!unitX.HasValue)
        {
            if (!unitY.HasValue) unit = unitZ ?? UnitNone;
            else unit = !unitZ.HasValue || unitY == unitZ ? unitY : null;
        }
        else if (unitY.HasValue)
        {
            if(unitX != unitY || (unitZ.HasValue && unitX != unitZ)) unit = null;
        } else if (unitZ.HasValue && unitX != unitZ)
        {
            unit = null;
        }
        if (!unit.HasValue || double.IsNaN(x) || double.IsNaN(y) || double.IsNaN(z))
        {
            vmState.SetNothing(destinationRegister);
        }
        else
        {
            vmState.SetVector(destinationRegister, x, y, z, unit.Value);
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

                    if (first.ObjectValue is not GesValueVectorPoint triplet)
                    {
                        vmState.SetNothing(destinationRegister);
                        return;
                    }

                    var liftedZ = triplet.Z;
                    var liftedUnit = first.Unit;
                    if (vmState.StageLength > 1)
                    {
                        ref readonly var zValue = ref vmState.RegisterStaged(1);
                        liftedZ = zValue.AsNumericWithUnit(out var liftedUnitZ);
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
        GameEventScriptBytecodeInstructionUnit? unitX = null;
        GameEventScriptBytecodeInstructionUnit? unitY = null;
        GameEventScriptBytecodeInstructionUnit? unitZ = null;
        var x = 0.0d;
        var y = 0.0d;
        var z = 0.0d;
        if (start == 0 && i < vmState.StageLength)
        {
            x = vmState.RegisterStaged(i++).AsNumericWithUnit(out var readUnitX);
            unitX = readUnitX;
        }
        if (start <= 1 && i < vmState.StageLength)
        {
            y = vmState.RegisterStaged(i++).AsNumericWithUnit(out var readUnitY);
            unitY = readUnitY;
        }
        if (start <= 2 && i < vmState.StageLength)
        {
            z = vmState.RegisterStaged(i).AsNumericWithUnit(out var readUnitZ);
            unitZ = readUnitZ;
        }
        var unit = unitX;
        if (!unitX.HasValue)
        {
            if (!unitY.HasValue) unit = unitZ ?? UnitNone;
            else unit = !unitZ.HasValue || unitY == unitZ ? unitY : null;
        }
        else if (unitY.HasValue)
        {
            if(unitX != unitY || (unitZ.HasValue && unitX != unitZ)) unit = null;
        } else if (unitZ.HasValue && unitX != unitZ)
        {
            unit = null;
        }
        if (!unit.HasValue || double.IsNaN(x) || double.IsNaN(y) || double.IsNaN(z))
        {
            vmState.SetNothing(destinationRegister);
        }
        else
        {
            vmState.SetPoint(destinationRegister, x, y, z, unit.Value);
        }
    }
    
}
