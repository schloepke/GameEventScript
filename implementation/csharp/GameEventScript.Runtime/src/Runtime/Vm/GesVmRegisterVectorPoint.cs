// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;
using static GameEventScript.Api.GesQuantityUnit;

namespace GameEventScript.Runtime.Vm;

internal static class GesVmRegisterVectorPoint
{
    internal static void GesVmCreateVector(this GesVirtualMachine vm, ushort destinationRegister, short start)
    {
        if (start == 0 && vm.StageLength > 0)
        {
            ref readonly var first = ref vm.RegisterStaged(0);
            switch (first.Kind)
            {
                case Vector:
                case Point:
                    if (vm.StageLength > 2)
                    {
                        vm.SetNothing(destinationRegister);
                        return;
                    }

                    if (first.ObjectValue is not GesValueVectorPoint triplet)
                    {
                        vm.SetNothing(destinationRegister);
                        return;
                    }

                    var liftedZ = triplet.Z;
                    var liftedUnit = first.Unit;
                    if (vm.StageLength > 1)
                    {
                        ref readonly var zValue = ref vm.RegisterStaged(1);
                        var lifted = zValue.AsNumericWithUnit();
                        liftedZ = lifted.Value;
                        if (double.IsNaN(liftedZ) || liftedUnit != lifted.Unit)
                        {
                            vm.SetNothing(destinationRegister);
                            return;
                        }
                    }

                    vm.SetVector(destinationRegister, triplet.X, triplet.Y, liftedZ, liftedUnit);
                    return;
            }
        }

        ushort i = 0;
        GesQuantityUnit? unitX = null;
        GesQuantityUnit? unitY = null;
        GesQuantityUnit? unitZ = null;
        var x = 0.0d;
        var y = 0.0d;
        var z = 0.0d;
        if (start == 0 && i < vm.StageLength)
        {
            var read = vm.RegisterStaged(i++).AsNumericWithUnit();
            x = read.Value;
            unitX = read.Unit;
        }
        if (start <= 1 && i < vm.StageLength)
        {
            var read = vm.RegisterStaged(i++).AsNumericWithUnit();
            y = read.Value;
            unitY = read.Unit;
        }
        if (start <= 2 && i < vm.StageLength)
        {
            var read = vm.RegisterStaged(i).AsNumericWithUnit();
            z = read.Value;
            unitZ = read.Unit;
        }
        var unit = unitX;
        if (!unitX.HasValue)
        {
            if (!unitY.HasValue) unit = unitZ ?? UnitNone;
            else unit = !unitZ.HasValue || unitY == unitZ ? unitY : null;
        }
        else if (unitY.HasValue)
        {
            if (unitX != unitY || (unitZ.HasValue && unitX != unitZ)) unit = null;
        }
        else if (unitZ.HasValue && unitX != unitZ)
        {
            unit = null;
        }
        if (!unit.HasValue || double.IsNaN(x) || double.IsNaN(y) || double.IsNaN(z))
        {
            vm.SetNothing(destinationRegister);
        }
        else
        {
            vm.SetVector(destinationRegister, x, y, z, unit.Value);
        }
    }
    internal static void GesVmCreatePoint(this GesVirtualMachine vm, ushort destinationRegister, short start)
    {
        if (start == 0 && vm.StageLength > 0)
        {
            ref readonly var first = ref vm.RegisterStaged(0);
            switch (first.Kind)
            {
                case Vector:
                case Point:
                    if (vm.StageLength > 2)
                    {
                        vm.SetNothing(destinationRegister);
                        return;
                    }

                    if (first.ObjectValue is not GesValueVectorPoint triplet)
                    {
                        vm.SetNothing(destinationRegister);
                        return;
                    }

                    var liftedZ = triplet.Z;
                    var liftedUnit = first.Unit;
                    if (vm.StageLength > 1)
                    {
                        ref readonly var zValue = ref vm.RegisterStaged(1);
                        var lifted = zValue.AsNumericWithUnit();
                        liftedZ = lifted.Value;
                        if (double.IsNaN(liftedZ) || liftedUnit != lifted.Unit)
                        {
                            vm.SetNothing(destinationRegister);
                            return;
                        }
                    }

                    vm.SetPoint(destinationRegister, triplet.X, triplet.Y, liftedZ, liftedUnit);
                    return;
            }
        }

        ushort i = 0;
        GesQuantityUnit? unitX = null;
        GesQuantityUnit? unitY = null;
        GesQuantityUnit? unitZ = null;
        var x = 0.0d;
        var y = 0.0d;
        var z = 0.0d;
        if (start == 0 && i < vm.StageLength)
        {
            var read = vm.RegisterStaged(i++).AsNumericWithUnit();
            x = read.Value;
            unitX = read.Unit;
        }
        if (start <= 1 && i < vm.StageLength)
        {
            var read = vm.RegisterStaged(i++).AsNumericWithUnit();
            y = read.Value;
            unitY = read.Unit;
        }
        if (start <= 2 && i < vm.StageLength)
        {
            var read = vm.RegisterStaged(i).AsNumericWithUnit();
            z = read.Value;
            unitZ = read.Unit;
        }
        var unit = unitX;
        if (!unitX.HasValue)
        {
            if (!unitY.HasValue) unit = unitZ ?? UnitNone;
            else unit = !unitZ.HasValue || unitY == unitZ ? unitY : null;
        }
        else if (unitY.HasValue)
        {
            if (unitX != unitY || (unitZ.HasValue && unitX != unitZ)) unit = null;
        }
        else if (unitZ.HasValue && unitX != unitZ)
        {
            unit = null;
        }
        if (!unit.HasValue || double.IsNaN(x) || double.IsNaN(y) || double.IsNaN(z))
        {
            vm.SetNothing(destinationRegister);
        }
        else
        {
            vm.SetPoint(destinationRegister, x, y, z, unit.Value);
        }
    }

}
