using System;
using System.Buffers;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.VirtualMachine;

internal static class GesVmRegisterCallExternal
{
    internal static void GesVmCallStandard(this GesVmState state, ushort destinationRegister, ushort extensionShapeIndex, ushort argumentSlotList, bool isPredicate)
    {
        var shape = state.FetchUInt16SliceTableByPointer(extensionShapeIndex);
        var argumentSlots = state.FetchUInt16SliceTableByPointer(argumentSlotList);
        if (shape.Length < 2 || argumentSlots.Length != shape.Length - 2)
        {
            state.SetNothing(destinationRegister);
            state.RaiseError("Invalid standard extension call shape.");
            return;
        }

        var argumentCount = argumentSlots.Length;
        var arguments = argumentCount == 0 ? [] : ArrayPool<GameEventScriptValue>.Shared.Rent(argumentCount);

        try
        {
            for (var argumentIndex = 0; argumentIndex < argumentCount; argumentIndex++)
            {
                ref readonly var argument = ref state.Register(argumentSlots[argumentIndex]);
                arguments[argumentIndex] = GameEventScriptValueFactory.FromVmValue(in argument);
            }

            var labels = argumentCount == 0 ? Array.Empty<string>() : new string[argumentCount];
            for (var labelIndex = 0; labelIndex < argumentCount; labelIndex++)
            {
                labels[labelIndex] = state.FetchStringByPointer(shape[labelIndex + 2]);
            }

            var reference = new GameEventScriptExtensionReference(state.FetchStringByPointer(shape[0]), state.FetchStringByPointer(shape[1]), labels);
            if (!GesStandardExtensions.TryInvoke(reference, arguments.AsSpan(0, argumentCount), out var result))
            {
                state.SetNothing(destinationRegister);
                state.RaiseError($"Unknown standard extension '{reference.SignatureId}'.");
                return;
            }

            state.SetValue(destinationRegister, in result.GetVmValue());
            ref readonly var dst = ref state.Register(destinationRegister);
            if (isPredicate && dst.Kind is not GameEventScriptBytecodeTypeKind.Boolean && dst.IsNotNothing)
            {
                state.SetNothing(destinationRegister);
            }
        }
        finally
        {
            if (argumentCount > 0)
            {
                Array.Clear(arguments, 0, argumentCount);
                ArrayPool<GameEventScriptValue>.Shared.Return(arguments);
            }
        }
    }

    internal static void GesVmCallExternal(this GesVmState state, ushort destinationRegister, ushort externalBindId, ushort argumentSlotList, GameEventScriptSession session, bool isPredicate)
    {
        if (externalBindId >= state.ExtensionCallBinds.Length || state.ExtensionCallBinds[externalBindId].Kind != GameEventScriptBinaryBindKind.ExtensionCall)
        {
            state.SetNothing(destinationRegister);
            state.RaiseError($"External extension bind id '{externalBindId}' was not found.");
            return;
        }

        var bind = state.ExtensionCallBinds[externalBindId];
        var argumentSlots = state.FetchUInt16SliceTableByPointer(argumentSlotList);
        if (argumentSlots.Length != bind.ArgumentNames.Count)
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

        var argumentCount = argumentSlots.Length;
        var arguments = argumentCount == 0 ? [] : ArrayPool<GameEventScriptValue>.Shared.Rent(argumentCount);

        try
        {
            for (var argumentIndex = 0; argumentIndex < argumentCount; argumentIndex++)
            {
                ref readonly var argument = ref state.Register(argumentSlots[argumentIndex]);
                arguments[argumentIndex] = GameEventScriptValueFactory.FromVmValue(in argument);
            }

            var result = function.Invoke(new GameEventScriptExtensionContext(session), arguments.AsSpan(0, argumentCount));
            state.SetValue(destinationRegister, in result.GetVmValue());
            ref readonly var dst = ref state.Register(destinationRegister);
            if (isPredicate && dst.Kind is not GameEventScriptBytecodeTypeKind.Boolean && dst.IsNotNothing)
            {
                state.SetNothing(destinationRegister);
            }
        }
        finally
        {
            if (argumentCount > 0)
            {
                Array.Clear(arguments, 0, argumentCount);
                ArrayPool<GameEventScriptValue>.Shared.Return(arguments);
            }
        }
    }

    internal static void BindArguments(this GesVmState state, ushort destinationRegister, GameEventScriptValue argument)
        => state.SetValue(destinationRegister, in argument.GetVmValue());
}
