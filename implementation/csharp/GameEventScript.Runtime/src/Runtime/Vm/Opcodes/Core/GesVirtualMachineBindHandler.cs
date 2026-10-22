// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteBindHandler(ushort destinationRegister, in GesValue handler, GesUInt16IndexList argumentRegisters)
    {
        if (handler.Kind is not Handler || handler.ObjectValue is not GesMessageSignature signature)
        {
            SetNothing(destinationRegister);
            return;
        }

        if (argumentRegisters.Length == 0)
        {
            var emptyMessage = signature.CreateMessageFromOwnedValues(Array.Empty<GesValue>());
            if (emptyMessage is not null)
            {
                SetMessage(destinationRegister, emptyMessage);
            }
            else
            {
                SetNothing(destinationRegister);
            }

            return;
        }

        var arguments = new GesValue[argumentRegisters.Length];
        for (var index = 0; index < arguments.Length; index++)
        {
            arguments[index] = Register(argumentRegisters[index]);
        }
        var message = signature.CreateMessageFromOwnedValues(arguments);
        if (message is not null)
        {
            SetMessage(destinationRegister, message);
        }
        else
        {
            SetNothing(destinationRegister);
        }
    }
}
