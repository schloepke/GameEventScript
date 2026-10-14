// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.VM;

internal static class GesVmRegisterCallExternal
{
    internal static void GesVmCallExternal(this GesVirtualMachine state, ushort destinationRegister, ushort externalBindId, ushort argumentRegisterList, GesContext context, bool isPredicate)
    {
        if (externalBindId >= state.ExtensionCallBinds.Length || state.ExtensionCallBinds[externalBindId].Kind != GesBinaryBindKind.ExtensionCall)
        {
            state.SetNothing(destinationRegister);
            state.RaiseError(GesDiagnosticCodes.RuntimeInvalidExtensionBinding,
                $"External extension bind id '{externalBindId}' was not found.");
            return;
        }

        var bind = state.ExtensionCallBinds[externalBindId];
        var argumentRegisters = state.FetchUInt16SliceTableByPointer(argumentRegisterList);
        if (argumentRegisters.Length != bind.ArgumentNames.Count)
        {
            state.SetNothing(destinationRegister);
            state.RaiseError(GesDiagnosticCodes.RuntimeInvalidExtensionBinding,
                "External extension call argument count does not match the reference shape.");
            return;
        }

        if (externalBindId >= state.BoundExtensionCalls.Length || state.BoundExtensionCalls[externalBindId] is not { } function)
        {
            state.SetNothing(destinationRegister);
            state.RaiseError(GesDiagnosticCodes.RuntimeInvalidExtensionBinding,
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
            try
            {
                function.Invoke(call);
            }
            catch (GesFatalRuntimeException)
            {
                // A deliberate GesExtensionFaultException, or another
                // internal fatal signal, is reported as-is by the caller.
                throw;
            }
            catch (Exception exception)
            {
                var name = state.FetchStringByPointer(bind.Name);
                state.SetNothing(destinationRegister);
                state.RaiseError(GesDiagnosticCodes.RuntimeExtensionCallFailed,
                    $"Extension '{name}' failed unexpectedly.", GameEventScriptRuntimeExceptionText.Describe(exception), symbol: name);
                return;
            }

            if (!call.HasResult)
            {
                state.SetNothing(destinationRegister);
            }

            ref readonly var dst = ref state.Register(destinationRegister);
            if (isPredicate && dst.Kind is not GesBytecodeTypeKind.Boolean && dst.IsNotNothing)
            {
                state.SetNothing(destinationRegister);
            }
        }
        finally
        {
            var randomFault = context.EndRandomBoundary(randomBoundary);
            // Always restore the random scope, but retain a callback failure already reported above.
            if (state.State != GesVirtualMachine.StateValue.Error)
            {
                if (randomFault == GesRandomGenerator.ScopeBoundaryFault.BoundaryUnderflow)
                    state.RaiseError(GesDiagnosticCodes.RuntimeRandomStackUnderflow, "Random scope pop crossed the active extension boundary.");
                else if (randomFault == GesRandomGenerator.ScopeBoundaryFault.Unbalanced)
                    state.RaiseError(GesDiagnosticCodes.RuntimeRandomScopeImbalance, "Random scopes were not balanced when the extension returned.");
            }
            call.EndCall();
        }
    }
}
