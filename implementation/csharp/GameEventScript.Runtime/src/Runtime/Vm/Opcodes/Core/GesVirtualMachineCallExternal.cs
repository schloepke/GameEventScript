// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteCallExternal(ushort destinationRegister, ushort externalBindId, ushort argumentRegisterList, GesContext context, bool isPredicate)
    {
        if (externalBindId >= ExtensionCallBinds.Length || ExtensionCallBinds[externalBindId].Kind != GesBinaryBindKind.ExtensionCall)
        {
            SetNothing(destinationRegister);
            RaiseError(GesDiagnosticCodes.RuntimeInvalidExtensionBinding,
                $"External extension bind id '{externalBindId}' was not found.");
            return;
        }

        var bind = ExtensionCallBinds[externalBindId];
        var argumentRegisters = FetchUInt16SliceTableByPointer(argumentRegisterList);
        if (argumentRegisters.Length != bind.ArgumentNames.Count)
        {
            SetNothing(destinationRegister);
            RaiseError(GesDiagnosticCodes.RuntimeInvalidExtensionBinding,
                "External extension call argument count does not match the reference shape.");
            return;
        }

        if (externalBindId >= BoundExtensionCalls.Length || BoundExtensionCalls[externalBindId] is not { } function)
        {
            SetNothing(destinationRegister);
            RaiseError(GesDiagnosticCodes.RuntimeInvalidExtensionBinding,
                $"External extension bind id '{externalBindId}' was not dynamically bound.");
            return;
        }

        var arguments = argumentRegisters.Length == 0
            ? GesValueArguments.Empty
            : new GesValueArguments(this, argumentRegisters);
        var call = ExtensionCall;
        call.BeginCall(this, destinationRegister, context, arguments);
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
                var name = FetchStringByPointer(bind.Name);
                SetNothing(destinationRegister);
                RaiseError(GesDiagnosticCodes.RuntimeExtensionCallFailed,
                    $"Extension '{name}' failed unexpectedly.", GesRuntimeExceptionText.Describe(exception), symbol: name);
                return;
            }

            if (!call.HasResult)
            {
                SetNothing(destinationRegister);
            }

            ref readonly var dst = ref Register(destinationRegister);
            if (isPredicate && dst.Kind is not GesBytecodeTypeKind.Boolean && dst.IsNotNothing)
            {
                SetNothing(destinationRegister);
            }
        }
        finally
        {
            var randomFault = context.EndRandomBoundary(randomBoundary);
            // Always restore the random scope, but retain a callback failure already reported above.
            if (State != GesVirtualMachine.StateValue.Error)
            {
                if (randomFault == GesRandomGenerator.ScopeBoundaryFault.BoundaryUnderflow)
                    RaiseError(GesDiagnosticCodes.RuntimeRandomStackUnderflow, "Random scope pop crossed the active extension boundary.");
                else if (randomFault == GesRandomGenerator.ScopeBoundaryFault.Unbalanced)
                    RaiseError(GesDiagnosticCodes.RuntimeRandomScopeImbalance, "Random scopes were not balanced when the extension returned.");
            }
            call.EndCall();
        }
    }
}
