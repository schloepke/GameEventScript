using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptBinaryBindTable;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeInstructionUnit;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.VirtualMachine;

internal static class GesVmRegisterCustomType
{
    internal static void GesVmCreateExternalType(this GesVmState vmState, ushort destinationRegister, ushort externalTypeConstructorBindId, ushort argumentNamesIndex, IGameEventScriptExternalTypeRegistry typeRegistry)
    {
        var found = false;
        GameEventScriptBinaryBindEntry bind = default;
        foreach (var entry in vmState.Binary.BindTable.Entries)
        {
            if (entry.Kind != GameEventScriptBinaryBindKind.ExternalType) continue;
            if (entry.Id != externalTypeConstructorBindId) continue;
            bind = entry;
            found = true;
            break;
        }

        if (!found)
        {
            vmState.SetNothing(destinationRegister);
            vmState.RaiseError($"External type constructor bind id '{externalTypeConstructorBindId}' was not found.");
            return;
        }

        var argumentNames = vmState.FetchUInt16SliceTableByPointer(argumentNamesIndex);
        if (argumentNames.Length != vmState.StageLength || argumentNames.Length != bind.ArgumentNames.Count)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        var labels = argumentNames.Length == 0 ? [] : new string[argumentNames.Length];
        for (var labelIndex = 0; labelIndex < labels.Length; labelIndex++)
        {
            labels[labelIndex] = vmState.FetchStringByPointer(argumentNames[labelIndex]);
            if (!string.Equals(labels[labelIndex], GameEventScriptMessageSignature.UnlabeledParameterName, System.StringComparison.Ordinal)) continue;
            vmState.SetNothing(destinationRegister);
            return;
        }

        var typeName = vmState.FetchStringByPointer(bind.Name);
        var reference = new GameEventScriptExternalTypeConstructorReference(typeName, labels);
        if (!typeRegistry.TryResolve(reference, out var constructor))
        {
            vmState.SetNothing(destinationRegister);
            vmState.RaiseError($"GameEventScript external type constructor ':{reference.SignatureId}' was not dynamically bound to external bind id '{externalTypeConstructorBindId}'.");
            return;
        }

        if (constructor.Definition.Parameters.Count != labels.Length)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        var arguments = labels.Length == 0 ? [] : new GameEventScriptBoxedValue[labels.Length];
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
                vmState.SetNothing(destinationRegister);
                return;
            }

            var converted = new GesVmValue();
            var source = vmState.RegisterStaged((ushort)argumentIndex);
            switch (parameter.TypeName)
            {
                case "nothing":
                    converted.SetNothing();
                    break;
                case "boolean":
                    GesVmRegisterTypeCastCheck.GesVmCast(ref converted, in source, Boolean, vmState, destinationRegister);
                    break;
                case "number":
                    GesVmRegisterTypeCastCheck.GesVmCast(ref converted, in source, Float, vmState, destinationRegister);
                    break;
                case "numeric":
                    GesVmRegisterTypeCastCheck.GesVmCastNumeric(ref converted, in source, vmState);
                    break;
                case "percentage":
                    GesVmRegisterTypeCastCheck.GesVmCast(ref converted, in source, Percentage, vmState, destinationRegister);
                    break;
                case "tag":
                    GesVmRegisterTypeCastCheck.GesVmCast(ref converted, in source, Tag, vmState, destinationRegister);
                    break;
                case "text":
                    GesVmRegisterTypeCastCheck.GesVmCast(ref converted, in source, Text, vmState, destinationRegister);
                    break;
                case "vector":
                    GesVmRegisterTypeCastCheck.GesVmCast(ref converted, in source, Vector, vmState, destinationRegister);
                    break;
                case "point":
                    GesVmRegisterTypeCastCheck.GesVmCast(ref converted, in source, Point, vmState, destinationRegister);
                    break;
                case "range":
                    GesVmRegisterTypeCastCheck.GesVmCast(ref converted, in source, Range, vmState, destinationRegister);
                    break;
                case "message":
                    GesVmRegisterTypeCastCheck.GesVmCast(ref converted, in source, Message, vmState, destinationRegister);
                    break;
                case "handler":
                    GesVmRegisterTypeCastCheck.GesVmCast(ref converted, in source, Handler, vmState, destinationRegister);
                    break;
                case "list":
                    GesVmRegisterTypeCastCheck.GesVmCast(ref converted, in source, List, vmState, destinationRegister);
                    break;
                case "map":
                    GesVmRegisterTypeCastCheck.GesVmCast(ref converted, in source, Map, vmState, destinationRegister);
                    break;
                case "dice":
                    GesVmRegisterTypeCastCheck.GesVmCast(ref converted, in source, Dice, vmState, destinationRegister);
                    break;
                case "degree":
                    GesVmRegisterTypeCastCheck.GesVmCastUnit(ref converted, in source, UnitDegree, vmState);
                    break;
                case "meter":
                    GesVmRegisterTypeCastCheck.GesVmCastUnit(ref converted, in source, UnitMeter, vmState);
                    break;
                case "second":
                    GesVmRegisterTypeCastCheck.GesVmCastUnit(ref converted, in source, UnitSecond, vmState);
                    break;
                default:
                    converted.SetNothing();
                    break;
            }

            if (converted.IsNothing)
            {
                vmState.SetNothing(destinationRegister);
                return;
            }

            arguments[parameterIndex] = GameEventScriptExternalTypeValueConverter.CoerceToDeclaredType(
                GameEventScriptBoxedValue.FromVmValue(in converted),
                parameter);
        }

        vmState.BindArguments(destinationRegister, constructor.Invoke(arguments));
    }
}
