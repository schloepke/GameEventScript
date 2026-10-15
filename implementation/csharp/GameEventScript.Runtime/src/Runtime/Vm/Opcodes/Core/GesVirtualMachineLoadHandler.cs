// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteLoadHandler(ushort destinationRegister, GesUInt16IndexList shape)
    {
        if (shape.Length == 0)
        {
            RaiseError(GesDiagnosticCodes.RuntimeInvalidMessageShape,
                "Cannot create message signature from empty shape.");
            return;
        }
        var messageName = FetchStringByPointer(shape[0]);
        var argumentNames = new string[shape.Length - 1];
        for (var index = 1; index < shape.Length; index++)
        {
            argumentNames[index - 1] = FetchStringByPointer(shape[index]);
        }
        SetMessageHandler(destinationRegister, GesMessageSignature.Create(messageName, argumentNames));
    }
}
