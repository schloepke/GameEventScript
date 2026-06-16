using System;
using System.Collections.Generic;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.VirtualMachine;

internal static class GesVmRegisterMessages
{
    internal static void CreateMessageSignature(this GesVmState state, ushort destinationRegister, ReadOnlySpan<ushort> shape)
    {
        if (shape.Length == 0)
        {
            state.RaiseError("Cannot create message signature from empty shape");
            return;
        }
        var messageName = state.FetchStringByPointer(shape[0]);
        var argumentNames = new List<string>(shape.Length - 1);
        for (var index = 1; index < shape.Length; index++)
        {
            argumentNames.Add(state.FetchStringByPointer(shape[index]));
        }
        state.SetMessageHandler(destinationRegister, GameEventScriptMessageSignature.Create(messageName, argumentNames));
    }
    internal static void CreateMessage(this GesVmState state, ushort destinationRegister, ReadOnlySpan<ushort> shape, ReadOnlySpan<ushort> argumentSlots)
    {
        if (shape.Length == 0 || argumentSlots.Length != shape.Length - 1)
        {
            state.RaiseError("Message signature shape and argument slots mismatch");
            return;
        }
        var messageName = state.FetchStringByPointer(shape[0]);
        var pairs = new KeyValuePair<string, GameEventScriptValue>[argumentSlots.Length];
        for (var index = 0; index < argumentSlots.Length; index++)
        {
            pairs[index] = new KeyValuePair<string, GameEventScriptValue>(state.FetchStringByPointer(shape[index + 1]), state.Register(argumentSlots[index]).ToGameEventScriptValue());
        }
        try
        {
            state.SetMessage(destinationRegister, GameEventScriptMessage.Create(messageName, GameEventScriptNamedArguments.CreateOrdered(pairs)));
        }
        catch (ArgumentException)
        {
            state.SetNothing(destinationRegister);
        }
    }
    internal static void BindHandler(this GesVmState state, ushort destinationRegister, in GesVmValue handler, ReadOnlySpan<ushort> argumentSlots)
    {
        if (handler.Kind is not Handler || handler.ObjectValue is not GameEventScriptMessageSignature signature)
        {
            state.SetNothing(destinationRegister);
            return;
        }

        var arguments = new GameEventScriptValue[argumentSlots.Length];
        for (var index = 0; index < arguments.Length; index++)
        {
            arguments[index] = state.Register(argumentSlots[index]).ToGameEventScriptValue();
        }
        if (signature.TryCreateMessage(arguments, out var message))
        {
            state.SetMessage(destinationRegister, message);
        }
        else
        {
            state.SetNothing(destinationRegister);
        }
    }

}
