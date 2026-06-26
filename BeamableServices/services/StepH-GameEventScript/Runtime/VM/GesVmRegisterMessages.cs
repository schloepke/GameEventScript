using System;
using System.Collections.Generic;
using StepH.GameEventScript.Api;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;
using StepH.GameEventScript.Runtime.Values;

namespace StepH.GameEventScript.Runtime.VM;

internal static class GesVmRegisterMessages
{
    internal static void CreateMessageSignature(this GesVmState vmState, ushort destinationRegister, ReadOnlySpan<ushort> shape)
    {
        if (shape.Length == 0)
        {
            vmState.RaiseError("Cannot create message signature from empty shape");
            return;
        }
        var messageName = vmState.FetchStringByPointer(shape[0]);
        var argumentNames = new List<string>(shape.Length - 1);
        for (var index = 1; index < shape.Length; index++)
        {
            argumentNames.Add(vmState.FetchStringByPointer(shape[index]));
        }
        vmState.SetMessageHandler(destinationRegister, GameEventScriptMessageSignature.Create(messageName, argumentNames));
    }
    internal static void CreateMessage(this GesVmState vmState, ushort destinationRegister, ReadOnlySpan<ushort> shape, ReadOnlySpan<ushort> argumentRegisters)
    {
        if (shape.Length == 0 || argumentRegisters.Length != shape.Length - 1)
        {
            vmState.RaiseError("Message signature shape and argument registers mismatch");
            return;
        }
        var messageName = vmState.FetchStringByPointer(shape[0]);
        var pairs = new KeyValuePair<string, GameEventScriptValue>[argumentRegisters.Length];
        for (var index = 0; index < argumentRegisters.Length; index++)
        {
            pairs[index] = new KeyValuePair<string, GameEventScriptValue>(vmState.FetchStringByPointer(shape[index + 1]), GameEventScriptValueFactory.FromVmValue(in vmState.Register(argumentRegisters[index])));
        }
        try
        {
            vmState.SetMessage(destinationRegister, GameEventScriptMessage.Create(messageName, GameEventScriptNamedArguments.CreateOrdered(pairs)));
        }
        catch (ArgumentException)
        {
            vmState.SetNothing(destinationRegister);
        }
    }
    internal static void BindHandler(this GesVmState vmState, ushort destinationRegister, in GesValue handler, ReadOnlySpan<ushort> argumentRegisters)
    {
        if (handler.Kind is not Handler || handler.ObjectValue is not GameEventScriptMessageSignature signature)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        var arguments = new GameEventScriptValue[argumentRegisters.Length];
        for (var index = 0; index < arguments.Length; index++)
        {
            arguments[index] = GameEventScriptValueFactory.FromVmValue(in vmState.Register(argumentRegisters[index]));
        }
        if (signature.TryCreateMessage(arguments, out var message))
        {
            vmState.SetMessage(destinationRegister, message);
        }
        else
        {
            vmState.SetNothing(destinationRegister);
        }
    }

}
