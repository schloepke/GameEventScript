// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Runtime.Values;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeInstructionUnit;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.Runtime.VM;

internal static class GesVmRegisterCustomType
{
    internal static void GesVmCreateRecordValue(this GesVmState vmState, ushort destinationRegister, in GesValue mapValue, ushort typeTextPointer)
    {
        if (mapValue.Kind is Map && mapValue.ObjectValue is GesValueMap map)
        {
            vmState.SetRecord(destinationRegister, vmState.FetchStringByPointer(typeTextPointer), map);
            return;
        }

        vmState.SetNothing(destinationRegister);
    }

    internal static void GesVmCreateExternalType(this GesVmState vmState, ushort destinationRegister, ushort externalTypeConstructorBindId, ushort argumentNamesIndex)
    {
        if (externalTypeConstructorBindId >= vmState.ExternalTypeBinds.Length || vmState.ExternalTypeBinds[externalTypeConstructorBindId].Kind != GameEventScriptBinaryBindKind.ExternalType)
        {
            vmState.SetNothing(destinationRegister);
            vmState.RaiseError(GameEventScriptDiagnosticCodes.RuntimeInvalidExternalTypeBinding,
                $"External type constructor bind id '{externalTypeConstructorBindId}' was not found.");
            return;
        }

        var bind = vmState.ExternalTypeBinds[externalTypeConstructorBindId];
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

        if (externalTypeConstructorBindId >= vmState.BoundExternalTypeConstructors.Length || vmState.BoundExternalTypeConstructors[externalTypeConstructorBindId] is not { } constructor)
        {
            vmState.SetNothing(destinationRegister);
            vmState.RaiseError(GameEventScriptDiagnosticCodes.RuntimeInvalidExternalTypeBinding,
                $"External type constructor bind id '{externalTypeConstructorBindId}' was not dynamically bound.");
            return;
        }

        if (constructor.Definition.Parameters.Count != labels.Length)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        var arguments = labels.Length == 0 ? [] : new GesValue[labels.Length];
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

            var source = vmState.RegisterStaged((ushort)argumentIndex);
            GesValue converted;
            switch (parameter.TypeName)
            {
                case "nothing":
                    converted = new GesValue();
                    converted.SetNothing();
                    break;
                case "boolean":
                    converted = GesVmRegisterTypeCastCheck.GesVmCast(in source, Boolean, vmState, destinationRegister);
                    break;
                case "number":
                    converted = GesVmRegisterTypeCastCheck.GesVmCast(in source, Float, vmState, destinationRegister);
                    break;
                case "numeric":
                    converted = GesVmRegisterTypeCastCheck.GesVmCastNumeric(in source, vmState);
                    break;
                case "percentage":
                    converted = GesVmRegisterTypeCastCheck.GesVmCast(in source, Percentage, vmState, destinationRegister);
                    break;
                case "tag":
                    converted = GesVmRegisterTypeCastCheck.GesVmCast(in source, Tag, vmState, destinationRegister);
                    break;
                case "text":
                    converted = GesVmRegisterTypeCastCheck.GesVmCast(in source, Text, vmState, destinationRegister);
                    break;
                case "vector":
                    converted = GesVmRegisterTypeCastCheck.GesVmCast(in source, Vector, vmState, destinationRegister);
                    break;
                case "point":
                    converted = GesVmRegisterTypeCastCheck.GesVmCast(in source, Point, vmState, destinationRegister);
                    break;
                case "range":
                    converted = GesVmRegisterTypeCastCheck.GesVmCast(in source, Range, vmState, destinationRegister);
                    break;
                case "message":
                    converted = GesVmRegisterTypeCastCheck.GesVmCast(in source, Message, vmState, destinationRegister);
                    break;
                case "handler":
                    converted = GesVmRegisterTypeCastCheck.GesVmCast(in source, Handler, vmState, destinationRegister);
                    break;
                case "list":
                    converted = GesVmRegisterTypeCastCheck.GesVmCast(in source, List, vmState, destinationRegister);
                    break;
                case "map":
                    converted = GesVmRegisterTypeCastCheck.GesVmCast(in source, Map, vmState, destinationRegister);
                    break;
                case "dice":
                    converted = GesVmRegisterTypeCastCheck.GesVmCast(in source, Dice, vmState, destinationRegister);
                    break;
                case "degree":
                    converted = GesVmRegisterTypeCastCheck.GesVmCastUnit(in source, UnitDegree, vmState);
                    break;
                case "meter":
                    converted = GesVmRegisterTypeCastCheck.GesVmCastUnit(in source, UnitMeter, vmState);
                    break;
                case "second":
                    converted = GesVmRegisterTypeCastCheck.GesVmCastUnit(in source, UnitSecond, vmState);
                    break;
                default:
                    converted = new GesValue();
                    converted.SetNothing();
                    break;
            }

            if (converted.IsNothing)
            {
                vmState.SetNothing(destinationRegister);
                return;
            }

            arguments[parameterIndex] = GameEventScriptExternalTypeValueConverter.CoerceToDeclaredType(in converted, parameter);
        }

        var call = vmState.ExternalTypeConstructorCall;
        call.BeginCall(vmState, destinationRegister, new GesValueArguments(arguments), constructor.Definition.TypeName);
        try
        {
            constructor.Invoke(call);
            if (!call.HasResult)
            {
                vmState.SetNothing(destinationRegister);
            }
        }
        finally
        {
            call.EndCall();
        }
    }
}
