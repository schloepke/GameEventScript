// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace GameEventScript.Runtime.VM;

internal static class GesVmRegisterCallExternal
{
    internal static void GesVmCallExternal(this GesVmState state, ushort destinationRegister, ushort externalBindId, ushort argumentRegisterList, GameEventScriptContext context, bool isPredicate)
    {
        if (externalBindId >= state.ExtensionCallBinds.Length || state.ExtensionCallBinds[externalBindId].Kind != GameEventScriptBinaryBindKind.ExtensionCall)
        {
            state.SetNothing(destinationRegister);
            state.RaiseError(GameEventScriptDiagnosticCodes.RuntimeInvalidExtensionBinding,
                $"External extension bind id '{externalBindId}' was not found.");
            return;
        }

        var bind = state.ExtensionCallBinds[externalBindId];
        var argumentRegisters = state.FetchUInt16SliceTableByPointer(argumentRegisterList);
        if (argumentRegisters.Length != bind.ArgumentNames.Count)
        {
            state.SetNothing(destinationRegister);
            state.RaiseError(GameEventScriptDiagnosticCodes.RuntimeInvalidExtensionBinding,
                "External extension call argument count does not match the reference shape.");
            return;
        }

        if (externalBindId >= state.BoundExtensionCalls.Length || state.BoundExtensionCalls[externalBindId] is not { } function)
        {
            state.SetNothing(destinationRegister);
            state.RaiseError(GameEventScriptDiagnosticCodes.RuntimeInvalidExtensionBinding,
                $"External extension bind id '{externalBindId}' was not dynamically bound.");
            return;
        }

        var arguments = argumentRegisters.Length == 0
            ? GesValueArguments.Empty
            : new GesValueArguments(state, argumentRegisters);
        var call = state.ExtensionCall;
        call.BeginCall(state, destinationRegister, context, arguments);
        var randomBoundary = context.BeginRandomBoundary();
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
            var randomFault = context.EndRandomBoundary(randomBoundary);
            if (randomFault == GameEventScriptRandomGenerator.ScopeBoundaryFault.BoundaryUnderflow)
                state.RaiseError(GameEventScriptDiagnosticCodes.RuntimeRandomStackUnderflow, "Random scope pop crossed the active extension boundary.");
            else if (randomFault == GameEventScriptRandomGenerator.ScopeBoundaryFault.Unbalanced)
                state.RaiseError(GameEventScriptDiagnosticCodes.RuntimeRandomScopeImbalance, "Random scopes were not balanced when the extension returned.");
            call.EndCall();
        }
    }
}
