using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptBinaryBindTable;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeInstructionUnit;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.VirtualMachine;

internal static class GesVmRegisterCustomType
{
    internal static void GesVmCreateExternalType(this GesVmState state, ushort destinationRegister, ushort externalTypeConstructorBindId, ushort argumentNamesIndex, IGameEventScriptExternalTypeRegistry typeRegistry)
    {
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
            state.SetNothing(destinationRegister);
            state.RaiseError($"External type constructor bind id '{externalTypeConstructorBindId}' was not found.");
            return;
        }

        var argumentNames = state.FetchUInt16SliceTableByPointer(argumentNamesIndex);
        if (argumentNames.Length != state.StageLength || argumentNames.Length != bind.ArgumentNames.Count)
        {
            state.SetNothing(destinationRegister);
            return;
        }

        var labels = argumentNames.Length == 0 ? [] : new string[argumentNames.Length];
        for (var labelIndex = 0; labelIndex < labels.Length; labelIndex++)
        {
            labels[labelIndex] = state.FetchStringByPointer(argumentNames[labelIndex]);
            if (!string.Equals(labels[labelIndex], GameEventScriptMessageSignature.UnlabeledParameterName, System.StringComparison.Ordinal)) continue;
            state.SetNothing(destinationRegister);
            return;
        }

        var typeName = state.FetchStringByPointer(bind.Name);
        var reference = new GameEventScriptExternalTypeConstructorReference(typeName, labels);
        if (!typeRegistry.TryResolve(reference, out var constructor))
        {
            state.SetNothing(destinationRegister);
            state.RaiseError($"GameEventScript external type constructor ':{reference.SignatureId}' was not dynamically bound to external bind id '{externalTypeConstructorBindId}'.");
            return;
        }

        if (constructor.Definition.Parameters.Count != labels.Length)
        {
            state.SetNothing(destinationRegister);
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
                state.SetNothing(destinationRegister);
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
                    GesVmRegisterTypeCastCheck.GesVmCast(ref converted, in source, Boolean, state, destinationRegister);
                    break;
                case "number":
                    GesVmRegisterTypeCastCheck.GesVmCast(ref converted, in source, Float, state, destinationRegister);
                    break;
                case "numeric":
                    GesVmRegisterTypeCastCheck.GesVmCastNumeric(ref converted, in source, state);
                    break;
                case "percentage":
                    GesVmRegisterTypeCastCheck.GesVmCast(ref converted, in source, Percentage, state, destinationRegister);
                    break;
                case "tag":
                    GesVmRegisterTypeCastCheck.GesVmCast(ref converted, in source, Tag, state, destinationRegister);
                    break;
                case "text":
                    GesVmRegisterTypeCastCheck.GesVmCast(ref converted, in source, Text, state, destinationRegister);
                    break;
                case "vector":
                    GesVmRegisterTypeCastCheck.GesVmCast(ref converted, in source, Vector, state, destinationRegister);
                    break;
                case "point":
                    GesVmRegisterTypeCastCheck.GesVmCast(ref converted, in source, Point, state, destinationRegister);
                    break;
                case "range":
                    GesVmRegisterTypeCastCheck.GesVmCast(ref converted, in source, Range, state, destinationRegister);
                    break;
                case "message":
                    GesVmRegisterTypeCastCheck.GesVmCast(ref converted, in source, Message, state, destinationRegister);
                    break;
                case "handler":
                    GesVmRegisterTypeCastCheck.GesVmCast(ref converted, in source, Handler, state, destinationRegister);
                    break;
                case "list":
                    GesVmRegisterTypeCastCheck.GesVmCast(ref converted, in source, List, state, destinationRegister);
                    break;
                case "map":
                    GesVmRegisterTypeCastCheck.GesVmCast(ref converted, in source, Map, state, destinationRegister);
                    break;
                case "dice":
                    GesVmRegisterTypeCastCheck.GesVmCast(ref converted, in source, Dice, state, destinationRegister);
                    break;
                case "degree":
                    GesVmRegisterTypeCastCheck.GesVmCastUnit(ref converted, in source, UnitDegree, state);
                    break;
                case "meter":
                    GesVmRegisterTypeCastCheck.GesVmCastUnit(ref converted, in source, UnitMeter, state);
                    break;
                case "second":
                    GesVmRegisterTypeCastCheck.GesVmCastUnit(ref converted, in source, UnitSecond, state);
                    break;
                default:
                    converted.SetNothing();
                    break;
            }

            if (converted.IsNothing)
            {
                state.SetNothing(destinationRegister);
                return;
            }

            arguments[parameterIndex] = GameEventScriptExternalTypeValueConverter.CoerceToDeclaredType(
                converted.ToGameEventScriptValue(),
                parameter);
        }

        state.BindArguments(destinationRegister, constructor.Invoke(arguments));
    }
}
