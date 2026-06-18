using System;
using System.Collections.Generic;
using StepH.GameEventScript.Api;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.VirtualMachine;

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
    internal static void CreateMessage(this GesVmState vmState, ushort destinationRegister, ReadOnlySpan<ushort> shape, ReadOnlySpan<ushort> argumentSlots)
    {
        if (shape.Length == 0 || argumentSlots.Length != shape.Length - 1)
        {
            vmState.RaiseError("Message signature shape and argument slots mismatch");
            return;
        }
        var messageName = vmState.FetchStringByPointer(shape[0]);
        var pairs = new KeyValuePair<string, GameEventScriptBoxedValue>[argumentSlots.Length];
        for (var index = 0; index < argumentSlots.Length; index++)
        {
            pairs[index] = new KeyValuePair<string, GameEventScriptBoxedValue>(vmState.FetchStringByPointer(shape[index + 1]), GameEventScriptBoxedValue.FromVmValue(in vmState.Register(argumentSlots[index])));
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
    internal static void BindHandler(this GesVmState vmState, ushort destinationRegister, in GesVmValue handler, ReadOnlySpan<ushort> argumentSlots)
    {
        if (handler.Kind is not Handler || handler.ObjectValue is not GameEventScriptMessageSignature signature)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        var arguments = new GameEventScriptBoxedValue[argumentSlots.Length];
        for (var index = 0; index < arguments.Length; index++)
        {
            arguments[index] = GameEventScriptBoxedValue.FromVmValue(in vmState.Register(argumentSlots[index]));
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
