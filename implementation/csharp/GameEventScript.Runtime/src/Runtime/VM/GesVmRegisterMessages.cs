// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.VM;

internal static class GesVmRegisterMessages
{
    internal static void CreateMessageSignature(this GesVirtualMachine vm, ushort destinationRegister, GesUInt16IndexList shape)
    {
        if (shape.Length == 0)
        {
            vm.RaiseError(GesDiagnosticCodes.RuntimeInvalidMessageShape,
                "Cannot create message signature from empty shape.");
            return;
        }
        var messageName = vm.FetchStringByPointer(shape[0]);
        var argumentNames = new string[shape.Length - 1];
        for (var index = 1; index < shape.Length; index++)
        {
            argumentNames[index - 1] = vm.FetchStringByPointer(shape[index]);
        }
        vm.SetMessageHandler(destinationRegister, GesMessageSignature.Create(messageName, argumentNames));
    }
    internal static void CreateMessage(this GesVirtualMachine vm, ushort destinationRegister, GesUInt16IndexList shape, GesUInt16IndexList argumentRegisters)
    {
        if (shape.Length == 0 || argumentRegisters.Length != shape.Length - 1)
        {
            vm.RaiseError(GesDiagnosticCodes.RuntimeInvalidMessageShape,
                "Message signature shape and argument registers mismatch.");
            return;
        }
        var messageName = vm.FetchStringByPointer(shape[0]);
        if (argumentRegisters.Length == 0)
        {
            try
            {
                var signatureId = GesMessageSignature.CreateSignatureId(messageName, GesMessageArguments.Empty.SignatureLabels);
                vm.SetMessage(destinationRegister, GameEventScriptMessage.CreatePrecomputed(messageName, GesMessageArguments.Empty, signatureId));
            }
            catch (ArgumentException)
            {
                vm.SetNothing(destinationRegister);
            }

            return;
        }

        var argumentNames = new string[argumentRegisters.Length];
        var values = new GesValue[argumentRegisters.Length];
        for (var index = 0; index < argumentRegisters.Length; index++)
        {
            argumentNames[index] = vm.FetchStringByPointer(shape[index + 1]);
            values[index] = vm.Register(argumentRegisters[index]);
        }
        try
        {
            var arguments = GesMessageArguments.CreatePrecomputed(GesReadOnlyArray<string>.FromOwnedArray(argumentNames), values);
            var signatureId = GesMessageSignature.CreateSignatureId(messageName, argumentNames);
            vm.SetMessage(destinationRegister, GameEventScriptMessage.CreatePrecomputed(messageName, arguments, signatureId));
        }
        catch (ArgumentException)
        {
            vm.SetNothing(destinationRegister);
        }
    }
    internal static void BindHandler(this GesVirtualMachine vm, ushort destinationRegister, in GesValue handler, GesUInt16IndexList argumentRegisters)
    {
        if (handler.Kind is not Handler || handler.ObjectValue is not GesMessageSignature signature)
        {
            vm.SetNothing(destinationRegister);
            return;
        }

        if (argumentRegisters.Length == 0)
        {
            var emptyMessage = signature.CreateMessageFromOwnedValues(Array.Empty<GesValue>());
            if (emptyMessage is not null)
            {
                vm.SetMessage(destinationRegister, emptyMessage);
            }
            else
            {
                vm.SetNothing(destinationRegister);
            }

            return;
        }

        var arguments = new GesValue[argumentRegisters.Length];
        for (var index = 0; index < arguments.Length; index++)
        {
            arguments[index] = vm.Register(argumentRegisters[index]);
        }
        var message = signature.CreateMessageFromOwnedValues(arguments);
        if (message is not null)
        {
            vm.SetMessage(destinationRegister, message);
        }
        else
        {
            vm.SetNothing(destinationRegister);
        }
    }

}
