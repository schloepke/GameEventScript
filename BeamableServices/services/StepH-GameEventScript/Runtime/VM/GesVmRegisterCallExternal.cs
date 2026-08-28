using System;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime.Values;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.Runtime.VM;

internal static class GesVmRegisterCallExternal
{
    internal static void GesVmCallExternal(this GesVmState state, ushort destinationRegister, ushort externalBindId, ushort argumentRegisterList, GameEventScriptSession session, bool isPredicate)
    {
        if (externalBindId >= state.ExtensionCallBinds.Length || state.ExtensionCallBinds[externalBindId].Kind != GameEventScriptBinaryBindKind.ExtensionCall)
        {
            state.SetNothing(destinationRegister);
            state.RaiseError($"External extension bind id '{externalBindId}' was not found.");
            return;
        }

        var bind = state.ExtensionCallBinds[externalBindId];
        var argumentRegisters = state.FetchUInt16SliceTableByPointer(argumentRegisterList);
        if (argumentRegisters.Length != bind.ArgumentNames.Count)
        {
            state.SetNothing(destinationRegister);
            state.RaiseError("External extension call argument count does not match the reference shape.");
            return;
        }

        if (externalBindId >= state.BoundExtensionCalls.Length || state.BoundExtensionCalls[externalBindId] is not { } function)
        {
            state.SetNothing(destinationRegister);
            state.RaiseError($"External extension bind id '{externalBindId}' was not dynamically bound.");
            return;
        }

        var arguments = argumentRegisters.Length == 0
            ? GesValueArguments.Empty
            : new GesValueArguments(state, argumentRegisters);
        var call = state.ExtensionCall;
        call.BeginCall(state, destinationRegister, session, arguments);
        try
        {
            function.Invoke(call);
            if (!call.HasResult)
            {
                state.SetNothing(destinationRegister);
            }

            ref readonly var dst = ref state.Register(destinationRegister);
            if (isPredicate && dst.Kind is not GameEventScriptBytecodeTypeKind.Boolean && dst.IsNotNothing)
            {
                state.SetNothing(destinationRegister);
            }
        }
        finally
        {
            call.EndCall();
        }
    }
}
