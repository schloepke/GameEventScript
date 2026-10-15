// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;
using static GameEventScript.Api.GesQuantityUnit;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteCreateVector(ushort destinationRegister, short start)
    {
        if (start == 0 && StageLength > 0)
        {
            ref readonly var first = ref RegisterStaged(0);
            switch (first.Kind)
            {
                case Vector:
                case Point:
                    if (StageLength > 2)
                    {
                        SetNothing(destinationRegister);
                        return;
                    }

                    if (first.ObjectValue is not GesSpatialValue triplet)
                    {
                        SetNothing(destinationRegister);
                        return;
                    }

                    var liftedZ = triplet.Z;
                    var liftedUnit = first.Unit;
                    if (StageLength > 1)
                    {
                        ref readonly var zValue = ref RegisterStaged(1);
                        var lifted = zValue.AsNumericWithUnit();
                        liftedZ = lifted.Value;
                        if (double.IsNaN(liftedZ) || liftedUnit != lifted.Unit)
                        {
                            SetNothing(destinationRegister);
                            return;
                        }
                    }

                    SetVector(destinationRegister, triplet.X, triplet.Y, liftedZ, liftedUnit);
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
        if (start == 0 && i < StageLength)
        {
            var read = RegisterStaged(i++).AsNumericWithUnit();
            x = read.Value;
            unitX = read.Unit;
        }
        if (start <= 1 && i < StageLength)
        {
            var read = RegisterStaged(i++).AsNumericWithUnit();
            y = read.Value;
            unitY = read.Unit;
        }
        if (start <= 2 && i < StageLength)
        {
            var read = RegisterStaged(i).AsNumericWithUnit();
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
            SetNothing(destinationRegister);
        }
        else
        {
            SetVector(destinationRegister, x, y, z, unit.Value);
        }
    }
}
