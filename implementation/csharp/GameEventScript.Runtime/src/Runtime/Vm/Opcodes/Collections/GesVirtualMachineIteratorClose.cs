// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteIteratorClose(ushort iteratorRegister)
    {
        var iterator = Register(iteratorRegister);
        if (iterator is not { Kind: Iterator, ObjectValue: IDisposable it }) return;
        it.Dispose();
        SetNothing(iteratorRegister);
    }
}
