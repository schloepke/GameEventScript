// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteLoadMessage(ushort destinationRegister, GesUInt16IndexList shape, GesUInt16IndexList argumentRegisters)
    {
        if (shape.Length == 0 || argumentRegisters.Length != shape.Length - 1)
        {
            RaiseError(GesDiagnosticCodes.RuntimeInvalidMessageShape,
                "Message signature shape and argument registers mismatch.");
            return;
        }
        var messageName = FetchStringByPointer(shape[0]);
        if (argumentRegisters.Length == 0)
        {
            try
            {
                var signatureId = GesMessageSignature.CreateSignatureId(messageName, GesMessageArguments.Empty.SignatureLabels);
                SetMessage(destinationRegister, GameEventScriptMessage.CreatePrecomputed(messageName, GesMessageArguments.Empty, signatureId));
            }
            catch (ArgumentException)
            {
                SetNothing(destinationRegister);
            }

            return;
        }

        var argumentNames = new string[argumentRegisters.Length];
        var values = new GesValue[argumentRegisters.Length];
        for (var index = 0; index < argumentRegisters.Length; index++)
        {
            argumentNames[index] = FetchStringByPointer(shape[index + 1]);
            values[index] = Register(argumentRegisters[index]);
        }
        try
        {
            var arguments = GesMessageArguments.CreatePrecomputed(GesReadOnlyArray<string>.FromOwnedArray(argumentNames), values);
            var signatureId = GesMessageSignature.CreateSignatureId(messageName, argumentNames);
            SetMessage(destinationRegister, GameEventScriptMessage.CreatePrecomputed(messageName, arguments, signatureId));
        }
        catch (ArgumentException)
        {
            SetNothing(destinationRegister);
        }
    }
}
