// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteLengthSquared(ushort destinationRegister, in GesValue value)
    {
        if (value.Kind is Nothing)
        {
            SetNothing(destinationRegister);
            return;
        }

        switch (value.Kind)
        {
            case Integer or Float or Percentage or GesBytecodeTypeKind.Boolean or Dice:
                if (!value.IsNumeric)
                {
                    SetNothing(destinationRegister);
                    return;
                }

                var n = value.AsNumeric;
                SetFloat(destinationRegister, n * n);
                return;
            case Vector or Point when value.ObjectValue is GesSpatialValue triplet:
                SetFloat(destinationRegister, triplet.X * triplet.X + triplet.Y * triplet.Y + triplet.Z * triplet.Z);
                return;
            default:
                SetNothing(destinationRegister);
                return;
        }
    }
}
