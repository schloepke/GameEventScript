// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteNormalize(ushort destinationRegister, in GesValue value)
    {
        if (value.Kind is Nothing)
        {
            SetNothing(destinationRegister);
            return;
        }

        if (value.Kind is not (Vector or Point) || value.ObjectValue is not GesSpatialValue triplet)
        {
            SetNothing(destinationRegister);
            return;
        }

        var length = Math.Sqrt(triplet.X * triplet.X + triplet.Y * triplet.Y + triplet.Z * triplet.Z);
        if (length == 0d || double.IsNaN(length))
        {
            SetNothing(destinationRegister);
            return;
        }

        SetVector(destinationRegister, triplet.X / length, triplet.Y / length, triplet.Z / length);
    }
}
