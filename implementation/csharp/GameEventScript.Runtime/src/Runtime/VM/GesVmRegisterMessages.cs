// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace GameEventScript.Runtime.VM;

internal static class GesVmRegisterMessages
{
    internal static void CreateMessageSignature(this GesVmState vmState, ushort destinationRegister, GameEventScriptUInt16IndexList shape)
    {
        if (shape.Length == 0)
        {
            vmState.RaiseError(GameEventScriptDiagnosticCodes.RuntimeInvalidMessageShape,
                "Cannot create message signature from empty shape.");
            return;
        }
        var messageName = vmState.FetchStringByPointer(shape[0]);
        var argumentNames = new string[shape.Length - 1];
        for (var index = 1; index < shape.Length; index++)
        {
            argumentNames[index - 1] = vmState.FetchStringByPointer(shape[index]);
        }
        vmState.SetMessageHandler(destinationRegister, GameEventScriptMessageSignature.Create(messageName, argumentNames));
    }
    internal static void CreateMessage(this GesVmState vmState, ushort destinationRegister, GameEventScriptUInt16IndexList shape, GameEventScriptUInt16IndexList argumentRegisters)
    {
        if (shape.Length == 0 || argumentRegisters.Length != shape.Length - 1)
        {
            vmState.RaiseError(GameEventScriptDiagnosticCodes.RuntimeInvalidMessageShape,
                "Message signature shape and argument registers mismatch.");
            return;
        }
        var messageName = vmState.FetchStringByPointer(shape[0]);
        if (argumentRegisters.Length == 0)
        {
            try
            {
                var signatureId = GameEventScriptMessageSignature.CreateSignatureId(messageName, GameEventScriptMessageArguments.Empty.SignatureLabels);
                vmState.SetMessage(destinationRegister, GameEventScriptMessage.CreatePrecomputed(messageName, GameEventScriptMessageArguments.Empty, signatureId));
            }
            catch (ArgumentException)
            {
                vmState.SetNothing(destinationRegister);
            }

            return;
        }

        var argumentNames = new string[argumentRegisters.Length];
        var values = new GesValue[argumentRegisters.Length];
        for (var index = 0; index < argumentRegisters.Length; index++)
        {
            argumentNames[index] = vmState.FetchStringByPointer(shape[index + 1]);
            values[index] = vmState.Register(argumentRegisters[index]);
        }
        try
        {
            var arguments = GameEventScriptMessageArguments.CreatePrecomputed(GameEventScriptReadOnlyArray<string>.FromOwnedArray(argumentNames), values);
            var signatureId = GameEventScriptMessageSignature.CreateSignatureId(messageName, argumentNames);
            vmState.SetMessage(destinationRegister, GameEventScriptMessage.CreatePrecomputed(messageName, arguments, signatureId));
        }
        catch (ArgumentException)
        {
            vmState.SetNothing(destinationRegister);
        }
    }
    internal static void BindHandler(this GesVmState vmState, ushort destinationRegister, in GesValue handler, GameEventScriptUInt16IndexList argumentRegisters)
    {
        if (handler.Kind is not Handler || handler.ObjectValue is not GameEventScriptMessageSignature signature)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (argumentRegisters.Length == 0)
        {
            var emptyMessage = signature.CreateMessageFromOwnedValues(Array.Empty<GesValue>());
            if (emptyMessage is not null)
            {
                vmState.SetMessage(destinationRegister, emptyMessage);
            }
            else
            {
                vmState.SetNothing(destinationRegister);
            }

            return;
        }

        var arguments = new GesValue[argumentRegisters.Length];
        for (var index = 0; index < arguments.Length; index++)
        {
            arguments[index] = vmState.Register(argumentRegisters[index]);
        }
        var message = signature.CreateMessageFromOwnedValues(arguments);
        if (message is not null)
        {
            vmState.SetMessage(destinationRegister, message);
        }
        else
        {
            vmState.SetNothing(destinationRegister);
        }
    }

}
