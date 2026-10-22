// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Runtime.Values;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void CreateIterator(ushort destinationRegister, in GesValue x)
    {
        if (x.CreateIterator() is { } iterator) SetIterator(destinationRegister, iterator);
        else SetNothing(destinationRegister);
    }
}
