// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Runtime.Values;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteCast(ushort destinationRegister, in GesValue xValue, GesBytecodeTypeKind type, GesContext? context = null)
    {
        var dst = CastValue(in xValue, type, this, destinationRegister, context);
        SetValue(destinationRegister, in dst);
    }
}
