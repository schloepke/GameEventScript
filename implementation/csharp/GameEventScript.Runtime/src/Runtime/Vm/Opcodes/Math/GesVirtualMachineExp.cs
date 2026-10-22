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
    private void ExecuteExp(ushort destinationRegister, in GesValue a)
    {
        if (a.Kind is Nothing)
        {
            SetNothing(destinationRegister);
            return;
        }

        if (a.HasUnit || !a.IsNumeric)
        {
            SetFloat(destinationRegister, double.NaN);
            return;
        }

        SetFloat(destinationRegister, Math.Exp(a.AsNumeric));
    }
}
