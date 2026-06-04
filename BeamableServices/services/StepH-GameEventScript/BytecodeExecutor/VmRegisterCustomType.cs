using System.Collections.Generic;
using System.Runtime.CompilerServices;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptBinaryBindTable;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeInstructionUnit;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.BytecodeExecutor;

internal static class VmRegisterCustomType
{

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmCreateExternalType(ref this VmValue dst, ushort externalTypeConstructorBindId, ushort argumentNamesIndex, IGameEventScriptExternalTypeRegistry typeRegistry)
    {
        var state = dst.OwningState;
        var found = false;
        GameEventScriptBinaryBindEntry bind = default;
        foreach (var entry in state.Binary.BindTable.Entries)
        {
            if (entry.Kind != GameEventScriptBinaryBindKind.ExternalType) continue;
            if (entry.Id != externalTypeConstructorBindId) continue;
            bind = entry;
            found = true;
            break;
        }

        if (!found)
        {
            dst.SetNothing();
            state.RaiseError($"External type constructor bind id '{externalTypeConstructorBindId}' was not found.");
            return;
        }

        var argumentNames = state.FetchUInt16SliceTableByPointer(argumentNamesIndex);
        if (argumentNames.Length != state.StageLength || argumentNames.Length != bind.ArgumentNames.Count)
        {
            dst.SetNothing();
            return;
        }

        var labels = argumentNames.Length == 0 ? [] : new string[argumentNames.Length];
        for (var labelIndex = 0; labelIndex < labels.Length; labelIndex++)
        {
            labels[labelIndex] = state.FetchStringByPointer(argumentNames[labelIndex]);
            if (!string.Equals(labels[labelIndex], GameEventScriptMessageSignature.UnlabeledParameterName, System.StringComparison.Ordinal)) continue;
            dst.SetNothing();
            return;
        }

        var typeName = state.FetchStringByPointer(bind.Name);
        var reference = new GameEventScriptExternalTypeConstructorReference(typeName, labels);
        if (!typeRegistry.TryResolve(reference, out var constructor))
        {
            dst.SetNothing();
            state.RaiseError($"GameEventScript external type constructor ':{reference.SignatureId}' was not dynamically bound to external bind id '{externalTypeConstructorBindId}'.");
            return;
        }

        if (constructor.Definition.Parameters.Count != labels.Length)
        {
            dst.SetNothing();
            return;
        }

        var arguments = labels.Length == 0 ? [] : new GameEventScriptValue[labels.Length];
        for (var parameterIndex = 0; parameterIndex < constructor.Definition.Parameters.Count; parameterIndex++)
        {
            var parameter = constructor.Definition.Parameters[parameterIndex];
            var argumentIndex = -1;
            for (var labelIndex = 0; labelIndex < labels.Length; labelIndex++)
            {
                if (!string.Equals(labels[labelIndex], parameter.Name, System.StringComparison.Ordinal)) continue;
                argumentIndex = labelIndex;
                break;
            }

            if (argumentIndex < 0)
            {
                dst.SetNothing();
                return;
            }

            var converted = state.CreateNothing();
            var source = state.RegisterStaged((ushort)argumentIndex);
            switch (parameter.TypeName)
            {
                case "nothing":
                    converted.SetNothing();
                    break;
                case "boolean":
                    converted.VmCast(ref source, Boolean);
                    break;
                case "number":
                    converted.VmCast(ref source, Float);
                    break;
                case "numeric":
                    converted.VmCastNumeric(ref source);
                    break;
                case "percentage":
                    converted.VmCast(ref source, Percentage);
                    break;
                case "tag":
                    converted.VmCast(ref source, Tag);
                    break;
                case "text":
                    converted.VmCast(ref source, Text);
                    break;
                case "vector":
                    converted.VmCast(ref source, Vector);
                    break;
                case "point":
                    converted.VmCast(ref source, Point);
                    break;
                case "range":
                    converted.VmCast(ref source, Range);
                    break;
                case "message":
                    converted.VmCast(ref source, Message);
                    break;
                case "handler":
                    converted.VmCast(ref source, Handler);
                    break;
                case "list":
                    converted.VmCast(ref source, List);
                    break;
                case "map":
                    converted.VmCast(ref source, Map);
                    break;
                case "dice":
                    converted.VmCast(ref source, Dice);
                    break;
                case "degree":
                    converted.VmCastUnit(ref source, UnitDegree);
                    break;
                case "meter":
                    converted.VmCastUnit(ref source, UnitMeter);
                    break;
                case "second":
                    converted.VmCastUnit(ref source, UnitSecond);
                    break;
                default:
                    converted.SetNothing();
                    break;
            }

            if (converted.IsNothing)
            {
                dst.SetNothing();
                return;
            }

            arguments[parameterIndex] = GameEventScriptExternalTypeValueConverter.CoerceToDeclaredType(
                converted.ToGameEventScriptValue(),
                parameter);
        }

        var result = constructor.Invoke(arguments);
        if (result.TryGetCustomTypeName(out var customTypeName))
        {
            var sourceEntries = result.AsMap();
            var entries = new Dictionary<string, VmValue>(sourceEntries.Count + 1, System.StringComparer.Ordinal)
            {
                [GameEventScriptValue.HiddenTypeKey] = state.CreateTag(customTypeName)
            };

            foreach (var (key, sourceValue) in sourceEntries)
            {
                var value = state.CreateNothing();
                value.BindArguments(sourceValue);
                entries[key] = value;
            }

            dst.SetRecord(new VmMapObject(state, entries));
            return;
        }

        dst.BindArguments(result);
    }
}
