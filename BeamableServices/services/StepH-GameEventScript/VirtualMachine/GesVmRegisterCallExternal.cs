using System;
using System.Buffers;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptBinaryBindTable;
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
        var arguments = argumentCount == 0 ? [] : ArrayPool<GameEventScriptBoxedValue>.Shared.Rent(argumentCount);

        try
        {
            for (var argumentIndex = 0; argumentIndex < argumentCount; argumentIndex++)
            {
                ref readonly var argument = ref state.Register(argumentSlots[argumentIndex]);
                arguments[argumentIndex] = GameEventScriptBoxedValue.FromVmValue(in argument);
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
                ArrayPool<GameEventScriptBoxedValue>.Shared.Return(arguments);
            }
        }
    }

    internal static void GesVmCallExternal(this GesVmState state, ushort destinationRegister, ushort externalBindId, ushort argumentSlotList, GameEventScriptSession session, bool isPredicate)
    {
        var found = false;
        GameEventScriptBinaryBindEntry bind = default;
        foreach (var entry in state.Binary.BindTable.Entries)
        {
            if (entry.Kind != GameEventScriptBinaryBindKind.ExtensionCall) continue;
            if (entry.Id == externalBindId)
            {
                bind = entry;
                found = true;
                break;
            }
        }

        if (!found)
        {
            state.SetNothing(destinationRegister);
            state.RaiseError($"External extension bind id '{externalBindId}' was not found.");
            return;
        }

        var argumentSlots = state.FetchUInt16SliceTableByPointer(argumentSlotList);
        if (argumentSlots.Length != bind.ArgumentNames.Count)
        {
            state.SetNothing(destinationRegister);
            state.RaiseError("External extension call argument count does not match the reference shape.");
            return;
        }

        var fullName = state.FetchStringByPointer(bind.Name);
        var separator = fullName.IndexOf('.');
        if (separator <= 0 || separator >= fullName.Length - 1)
        {
            state.SetNothing(destinationRegister);
            state.RaiseError($"External extension reference '{fullName}' has an invalid name.");
            return;
        }

        var argumentCount = argumentSlots.Length;
        var labels = argumentCount == 0 ? Array.Empty<string>() : new string[argumentCount];
        for (var labelIndex = 0; labelIndex < argumentCount; labelIndex++)
        {
            labels[labelIndex] = state.FetchStringByPointer(bind.ArgumentNames[labelIndex]);
        }

        var reference = new GameEventScriptExtensionReference(fullName[..separator], fullName[(separator + 1)..], labels);

        if (!session.ExtensionRegistry.TryResolve(reference, out var function))
        {
            state.SetNothing(destinationRegister);
            state.RaiseError($"GameEventScript extension '{reference.SignatureId}' was not dynamically bound to external bind id '{externalBindId}'.");
            return;
        }

        var arguments = argumentCount == 0 ? [] : ArrayPool<GameEventScriptBoxedValue>.Shared.Rent(argumentCount);

        try
        {
            for (var argumentIndex = 0; argumentIndex < argumentCount; argumentIndex++)
            {
                ref readonly var argument = ref state.Register(argumentSlots[argumentIndex]);
                arguments[argumentIndex] = GameEventScriptBoxedValue.FromVmValue(in argument);
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
                ArrayPool<GameEventScriptBoxedValue>.Shared.Return(arguments);
            }
        }
    }

    internal static void BindArguments(this GesVmState state, ushort destinationRegister, GameEventScriptValue argument)
    {
        ref var destination = ref state.Register(destinationRegister);
        destination.BindArguments(argument);
    }

    internal static void BindArguments(this GesVmState state, ushort destinationRegister, GameEventScriptBoxedValue argument)
        => state.SetValue(destinationRegister, in argument.GetVmValue());

    internal static void BindArguments(ref this GesVmValue destination, GameEventScriptValue argument)
    {
        switch (argument.Kind)
        {
            case GameEventScriptBytecodeTypeKind.Tag:
                destination.SetTag(argument.AsText());
                break;
            case GameEventScriptBytecodeTypeKind.Text:
                destination.SetText(argument.AsText());
                break;
            case GameEventScriptBytecodeTypeKind.Percentage:
                destination.SetPercentage(argument.AsNumber());
                break;
            case GameEventScriptBytecodeTypeKind.Vector when argument is GameEventScriptVectorValue vector:
                destination.SetVector(vector.X, vector.Y, vector.Z, vector.Unit);
                break;
            case GameEventScriptBytecodeTypeKind.Point when argument is GameEventScriptPointValue point:
                destination.SetPoint(point.X, point.Y, point.Z, point.Unit);
                break;
            case GameEventScriptBytecodeTypeKind.Integer:
            case GameEventScriptBytecodeTypeKind.Float:
                if (argument.IsInteger()) destination.SetInteger(argument.AsInteger(), argument.Unit);
                else if (argument.IsInfinity()) destination.SetFloat(argument.IsNegativeInfinity() ? double.NegativeInfinity : double.PositiveInfinity, argument.Unit);
                else destination.SetFloat(argument.AsNumber(), argument.Unit);
                break;
            case GameEventScriptBytecodeTypeKind.Boolean:
                destination.SetBoolean(argument.AsBoolean());
                break;
            case GameEventScriptBytecodeTypeKind.Range when argument is GameEventScriptRangeValue range:
                if (range.IsIntegerRange) destination.SetRange(range.From, range.To, range.Step);
                else destination.SetRange(range.FromNumber, range.ToNumber, range.StepNumber);
                break;
            case GameEventScriptBytecodeTypeKind.Handler when argument is GameEventScriptHandlerValue handler:
                destination.SetMessageHandler(handler.Signature);
                break;
            case GameEventScriptBytecodeTypeKind.List:
                var sourceItems = argument.AsList();
                var list = new GesVmValue[sourceItems.Count];
                for (var index = 0; index < sourceItems.Count; index++) list[index].BindArguments(sourceItems[index]);
                destination.SetList(list);
                break;
            case GameEventScriptBytecodeTypeKind.Map:
                var sourceEntries = argument.AsMap();
                var isCustomType = argument.TryGetCustomTypeName(out var customTypeName);
                var entries = new GesVmValueMapBuilder(sourceEntries.Count + (isCustomType ? 1 : 0));
                if (isCustomType)
                {
                    var typeName = new GesVmValue();
                    typeName.SetTag(customTypeName);
                    entries.Set(GesVmValueMap.HiddenRecordTypeField, typeName);
                }

                foreach (var (key, sourceValue) in sourceEntries)
                {
                    var value = new GesVmValue();
                    value.BindArguments(sourceValue);
                    entries.Set(key, value);
                }

                if (isCustomType) destination.SetRecord(entries.ToMap());
                else destination.SetMap(entries.ToMap());
                break;
            case GameEventScriptBytecodeTypeKind.Dice:
                var sourceDice = argument.AsDice().Rolls;
                var rolls = new int[sourceDice.Count];
                for (var index = 0; index < sourceDice.Count; index++)
                {
                    rolls[index] = sourceDice[index];
                }

                destination.SetDice(rolls);
                break;
            case GameEventScriptBytecodeTypeKind.Series when argument is GameEventScriptSeriesValue series:
                destination.SetSeries(GesVmSeries.FromExternal(series.Series).Drop(series.Offset));
                break;
            case GameEventScriptBytecodeTypeKind.Series:
            case GameEventScriptBytecodeTypeKind.Nothing:
            default:
                destination.SetNothing();
                break;
        }
    }

}
