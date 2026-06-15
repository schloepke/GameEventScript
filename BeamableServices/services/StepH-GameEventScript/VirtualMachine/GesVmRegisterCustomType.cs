using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptBinaryBindTable;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeInstructionUnit;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.VirtualMachine;

internal static class GesVmRegisterCustomType
{
    internal static void GesVmCreateExternalType(ref this GesVmValue dst, ushort externalTypeConstructorBindId, ushort argumentNamesIndex, IGameEventScriptExternalTypeRegistry typeRegistry)
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
                    converted.GesVmCast(ref source, Boolean);
                    break;
                case "number":
                    converted.GesVmCast(ref source, Float);
                    break;
                case "numeric":
                    converted.GesVmCastNumeric(ref source);
                    break;
                case "percentage":
                    converted.GesVmCast(ref source, Percentage);
                    break;
                case "tag":
                    converted.GesVmCast(ref source, Tag);
                    break;
                case "text":
                    converted.GesVmCast(ref source, Text);
                    break;
                case "vector":
                    converted.GesVmCast(ref source, Vector);
                    break;
                case "point":
                    converted.GesVmCast(ref source, Point);
                    break;
                case "range":
                    converted.GesVmCast(ref source, Range);
                    break;
                case "message":
                    converted.GesVmCast(ref source, Message);
                    break;
                case "handler":
                    converted.GesVmCast(ref source, Handler);
                    break;
                case "list":
                    converted.GesVmCast(ref source, List);
                    break;
                case "map":
                    converted.GesVmCast(ref source, Map);
                    break;
                case "dice":
                    converted.GesVmCast(ref source, Dice);
                    break;
                case "degree":
                    converted.GesVmCastUnit(ref source, UnitDegree);
                    break;
                case "meter":
                    converted.GesVmCastUnit(ref source, UnitMeter);
                    break;
                case "second":
                    converted.GesVmCastUnit(ref source, UnitSecond);
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

        dst.BindArguments(constructor.Invoke(arguments));
    }
}
