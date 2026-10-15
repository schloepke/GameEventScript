// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0


namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteMapBuilderCreate(ushort destinationRegister)
    {
        CreateMapBuilder(destinationRegister);
    }
}
