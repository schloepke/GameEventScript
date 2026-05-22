using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.BytecodeExecutor;

internal static class VmRegisterMessages
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool CreateMessageSignature(ref this VmValue dest, ReadOnlySpan<ushort> shape, ref VmState vmState,  GameEventScriptSession session)
    {
        if (shape.Length == 0) return vmState.RaiseError("Cannot create message signature from empty shape");
        var messageName = vmState.Binary.TextConstantTable.Resolve(shape[0]);
        var argumentNames = new List<string>(shape.Length - 1);
        for (var index = 1; index < shape.Length; index++)
        {
            argumentNames.Add(vmState.Binary.TextConstantTable.Resolve(shape[index]));
        }
        dest.SetObject(Handler, GameEventScriptMessageSignature.Create(messageName, argumentNames));
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool CreateMessage(ref this VmValue dest, ReadOnlySpan<ushort> shape, ReadOnlySpan<ushort> argumentSlots, ref VmState vmState,  GameEventScriptSession session)
    {
        if (shape.Length == 0 || argumentSlots.Length != shape.Length - 1) return vmState.RaiseError("Message signature shape and argument slots mismatch");
        var messageName = vmState.Binary.TextConstantTable.Resolve(shape[0]);
        var pairs = new KeyValuePair<string, GameEventScriptValue>[argumentSlots.Length];
        for (var index = 0; index < argumentSlots.Length; index++)
        {
            pairs[index] = new KeyValuePair<string, GameEventScriptValue>(
                vmState.Binary.TextConstantTable.Resolve(shape[index + 1]),
                vmState.Register(argumentSlots[index]).ToGameEventScriptValue());
        }
        dest.SetObject(Message, GameEventScriptMessage.Create(messageName, GameEventScriptNamedArguments.CreateOrdered(pairs)));
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool BindHandler(ref this VmValue dest, ref VmValue handler, ReadOnlySpan<ushort> argumentSlots, ref VmState vmState,  GameEventScriptSession session)
    {
        if (handler.Kind is not Handler || handler.ObjectValue is not GameEventScriptMessageSignature signature)
        {
            dest.SetNothing();
            return true;
        }

        var arguments = new GameEventScriptValue[argumentSlots.Length];
        for (var index = 0; index < arguments.Length; index++)
        {
            arguments[index] = vmState.Register(argumentSlots[index]).ToGameEventScriptValue();
        }
        if (signature.TryCreateMessage(arguments, out var message))
        {
            dest.SetObject(Message, message);
        }
        else
        {
            dest.SetNothing();
        }

        return true;
    }

}
