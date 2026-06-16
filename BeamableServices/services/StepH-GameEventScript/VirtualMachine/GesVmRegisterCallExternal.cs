using System;
using System.Buffers;
using System.Collections.Generic;
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
        var arguments = argumentCount == 0 ? [] : ArrayPool<GameEventScriptFastValue>.Shared.Rent(argumentCount);

        try
        {
            for (var argumentIndex = 0; argumentIndex < argumentCount; argumentIndex++)
            {
                arguments[argumentIndex] = state.Register(argumentSlots[argumentIndex]).ToGameEventScriptFastValue();
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

            state.BindArguments(destinationRegister, result);
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
                ArrayPool<GameEventScriptFastValue>.Shared.Return(arguments);
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

        var arguments = argumentCount == 0 ? [] : ArrayPool<GameEventScriptFastValue>.Shared.Rent(argumentCount);

        try
        {
            for (var argumentIndex = 0; argumentIndex < argumentCount; argumentIndex++)
            {
                arguments[argumentIndex] = state.Register(argumentSlots[argumentIndex]).ToGameEventScriptFastValue();
            }

            var result = function.Invoke(new GameEventScriptExtensionContext(session), arguments.AsSpan(0, argumentCount));
            state.BindArguments(destinationRegister, result);
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
                ArrayPool<GameEventScriptFastValue>.Shared.Return(arguments);
            }
        }
    }

    private static void BindArguments(this GesVmState state, ushort destinationRegister, GameEventScriptFastValue argument)
    {
        ref var destination = ref state.Register(destinationRegister);
        destination.BindArguments(argument);
    }

    internal static void BindArguments(this GesVmState state, ushort destinationRegister, GameEventScriptValue argument)
    {
        ref var destination = ref state.Register(destinationRegister);
        destination.BindArguments(argument);
    }

    private static void BindArguments(ref this GesVmValue destination, GameEventScriptFastValue argument)
    {
        switch (argument.Kind)
        {
            case GameEventScriptValueKind.Nothing:
                destination.SetNothing();
                break;
            case GameEventScriptValueKind.Boolean:
                destination.SetBoolean(argument.Boolean);
                break;
            case GameEventScriptValueKind.Number:
                if (argument.IsIntegerNumber) destination.SetInteger(argument.Integer, argument.Unit);
                else destination.SetFloat(argument.Number, argument.Unit);
                break;
            case GameEventScriptValueKind.Percentage:
                destination.SetPercentage(argument.Number);
                break;
            case GameEventScriptValueKind.Vector:
                destination.SetVector(argument.X, argument.Y, argument.Z, argument.Unit);
                break;
            case GameEventScriptValueKind.Point:
                destination.SetPoint(argument.X, argument.Y, argument.Z, argument.Unit);
                break;
            default:
                destination.BindArguments(argument.ToGameEventScriptValue());
                break;
        }
    }

    internal static void BindArguments(ref this GesVmValue destination, GameEventScriptValue argument)
    {
        switch (argument.Kind)
        {
            case GameEventScriptValueKind.Tag:
                destination.SetTag(argument.AsText());
                break;
            case GameEventScriptValueKind.Text:
                destination.SetText(argument.AsText());
                break;
            case GameEventScriptValueKind.Percentage:
                destination.SetPercentage(argument.AsNumber());
                break;
            case GameEventScriptValueKind.Vector when argument is GameEventScriptVectorValue vector:
                destination.SetVector(vector.X, vector.Y, vector.Z, vector.Unit);
                break;
            case GameEventScriptValueKind.Point when argument is GameEventScriptPointValue point:
                destination.SetPoint(point.X, point.Y, point.Z, point.Unit);
                break;
            case GameEventScriptValueKind.Number:
                if (argument.IsInteger()) destination.SetInteger(argument.AsInteger(), argument.Unit);
                else if (argument.IsInfinity()) destination.SetFloat(argument.IsNegativeInfinity() ? double.NegativeInfinity : double.PositiveInfinity, argument.Unit);
                else destination.SetFloat(argument.AsNumber(), argument.Unit);
                break;
            case GameEventScriptValueKind.Boolean:
                destination.SetBoolean(argument.AsBoolean());
                break;
            case GameEventScriptValueKind.Range when argument is GameEventScriptRangeValue range:
                if (range.IsIntegerRange) destination.SetRange(range.From, range.To, range.Step);
                else destination.SetRange(range.FromNumber, range.ToNumber, range.StepNumber);
                break;
            case GameEventScriptValueKind.Message when argument is GameEventScriptMessageValue message:
                destination.SetMessage(message.Value);
                break;
            case GameEventScriptValueKind.Handler when argument is GameEventScriptHandlerValue handler:
                destination.SetMessageHandler(handler.Signature);
                break;
            case GameEventScriptValueKind.List:
                var sourceItems = argument.AsList();
                var list = destination.OwningState.CreateList(sourceItems.Count);
                for (var index = 0; index < sourceItems.Count; index++) list[index].BindArguments(sourceItems[index]);
                destination.SetList(list);
                break;
            case GameEventScriptValueKind.Map:
                if (argument is IGameEventScriptExternalObjectValue)
                {
                    destination.SetExternalCustomType(argument);
                    break;
                }

                var sourceEntries = argument.AsMap();
                var isCustomType = argument.TryGetCustomTypeName(out var customTypeName);
                var entries = new GesVmValueMapBuilder(destination.OwningState, sourceEntries.Count + (isCustomType ? 1 : 0));
                if (isCustomType)
                {
                    entries.Set(GesVmValueMap.HiddenRecordTypeField, destination.OwningState.CreateTag(customTypeName));
                }

                foreach (var (key, sourceValue) in sourceEntries)
                {
                    var value = destination.OwningState.CreateNothing();
                    value.BindArguments(sourceValue);
                    entries.Set(key, value);
                }

                if (isCustomType) destination.SetRecord(entries.ToMap());
                else destination.SetMap(entries.ToMap());
                break;
            case GameEventScriptValueKind.Dice:
                var sourceDice = argument.AsDice().Rolls;
                var rolls = new int[sourceDice.Count];
                for (var index = 0; index < sourceDice.Count; index++)
                {
                    rolls[index] = sourceDice[index];
                }

                destination.SetDice(rolls);
                break;
            case GameEventScriptValueKind.Series when argument is GameEventScriptSeriesValue series:
                destination.SetSeries(series);
                break;
            case GameEventScriptValueKind.Series:
            case GameEventScriptValueKind.Nothing:
            default:
                destination.SetNothing();
                break;
        }
    }

    private static GameEventScriptFastValue ToGameEventScriptFastValue(this in GesVmValue value)
    {
        switch (value.Kind)
        {
            case Nothing:
                return GameEventScriptFastValue.Nothing;
            case GameEventScriptBytecodeTypeKind.Boolean:
                return GameEventScriptFastValue.FromBoolean(value.IsTrue);
            case Integer:
                return GameEventScriptFastValue.FromInteger(value.IntegerValue, value.Unit);
            case Float:
                return double.IsNaN(value.FloatValue) ? GameEventScriptFastValue.Nothing : GameEventScriptFastValue.FromFloat(value.FloatValue, value.Unit);
            case Percentage:
                return GameEventScriptFastValue.FromPercentage(value.FloatValue);
            case Vector when value.ObjectValue is GesVmValueVectorPoint vector:
                return GameEventScriptFastValue.FromVector(vector.X, vector.Y, vector.Z, value.Unit);
            case Point when value.ObjectValue is GesVmValueVectorPoint point:
                return GameEventScriptFastValue.FromPoint(point.X, point.Y, point.Z, value.Unit);
            case Text:
                return GameEventScriptFastValue.FromText(value.TextValue);
            default:
                return GameEventScriptFastValue.FromGameEventScriptValue(value.ToGameEventScriptValue());
        }
    }

    internal static GameEventScriptValue ToGameEventScriptValue(this in GesVmValue a) => a.Kind switch
    {
        Integer => GameEventScriptValueFactory.GesInteger(a.IntegerValue, a.Unit),
        Float => double.IsNaN(a.FloatValue) ? GameEventScriptValueFactory.GesNothing() : GameEventScriptValueFactory.GesFloat(a.FloatValue, a.Unit),
        Percentage => GameEventScriptValueFactory.GesPercentage(a.FloatValue),
        Vector when a.ObjectValue is GesVmValueVectorPoint vector => GameEventScriptValueFactory.GesVector(vector.X, vector.Y, vector.Z, a.Unit),
        Point when a.ObjectValue is GesVmValueVectorPoint point => GameEventScriptValueFactory.GesPoint(point.X, point.Y, point.Z, a.Unit),
        GameEventScriptBytecodeTypeKind.Boolean => GameEventScriptValueFactory.GesBoolean(a.IsTrue),
        Text => GameEventScriptValueFactory.GesText(a.TextValue),
        Tag => GameEventScriptValueFactory.GesTag(a.TextValue),
        List when a.ObjectValue is GesVmValue[] list => GameEventScriptValueFactory.GesList(list.ToGameEventScriptValues()),
        Map when a.ObjectValue is GesVmValueMap map => GameEventScriptValueFactory.GesMap(map.ToGameEventScriptValues()),
        Custom when a.ObjectValue is GameEventScriptValue custom => custom,
        Custom when a.ObjectValue is GesVmValueMap map => map.ToGameEventScriptCustomTypeValue(a.OwningState),
        Dice when a.ObjectValue is int[] dice => GameEventScriptValueFactory.GesDice(dice),
        GameEventScriptBytecodeTypeKind.Range when a.ObjectValue is GesVmValueRangeInteger r => GameEventScriptValueFactory.GesRange(r.From, r.To, r.Step),
        GameEventScriptBytecodeTypeKind.Range when a.ObjectValue is GesVmValueRangeFloat r => GameEventScriptValueFactory.GesRange(r.From, r.To, r.Step),
        Series when a.ObjectValue is GameEventScriptSeriesValue series => series,
        Handler when a.ObjectValue is GameEventScriptMessageSignature signature => GameEventScriptValueFactory.GesHandler(signature),
        Message when a.ObjectValue is GameEventScriptMessage message => GameEventScriptValueFactory.GesMessage(message),
        _ => GameEventScriptValueFactory.GesNothing(),
    };

    private static List<GameEventScriptValue> ToGameEventScriptValues(this GesVmValue[] list)
    {
        var result = new List<GameEventScriptValue>(list.Length);
        for (var i = 0; i < list.Length; i++)
        {
            ref readonly var value = ref list[i];
            result.Add(value.ToGameEventScriptValue());
        }

        return result;
    }

    private static Dictionary<string, GameEventScriptValue> ToGameEventScriptValues(this GesVmValueMap valueMap)
    {
        var result = new Dictionary<string, GameEventScriptValue>(valueMap.Length, StringComparer.Ordinal);
        for (var i = 0; i < valueMap.StorageLength; i++)
        {
            var key = valueMap.KeyAt(i);
            if (!valueMap.IsVisibleAt(i)) continue;
            var value = valueMap.ValueAt(i);
            result.Add(key, value.ToGameEventScriptValue());
        }

        return result;
    }

    private static GameEventScriptValue ToGameEventScriptCustomTypeValue(this GesVmValueMap valueMap, GesVmState state)
    {
        var typeName = string.Empty;
        if (valueMap.TryGet(GesVmValueMap.HiddenRecordTypeField, out var marker) && marker.Kind is Tag)
        {
            typeName = marker.TextValue;
        }

        return string.IsNullOrEmpty(typeName)
            ? GameEventScriptValueFactory.GesNothing()
            : GameEventScriptValueFactory.GesCustomType(typeName, valueMap.ToGameEventScriptValues());
    }
    
}
