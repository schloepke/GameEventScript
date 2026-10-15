// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics.CodeAnalysis;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

[SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator")]
internal partial class GesVirtualMachine
{
    private void ExecuteCeil(ushort destinationRegister, in GesValue a)
    {
        if (a.Kind is Nothing)
        {
            SetNothing(destinationRegister);
            return;
        }

        if (!a.IsNumeric)
        {
            SetFloat(destinationRegister, double.NaN);
            return;
        }

        SetInteger(destinationRegister, ToIntegerSaturated(Math.Ceiling(a.AsNumeric)));
    }
}
