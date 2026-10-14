// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Runtime;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GameEventScriptBytecodeInstructionUnit;
using static GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace GameEventScript.Runtime.VM;

internal static class GesVmRegisterCustomType
{
    internal static void GesVmCreateRecordValue(this GesVirtualMachine vm, ushort destinationRegister, in GesValue mapValue, ushort typeTextPointer)
    {
        if (mapValue.Kind is Map && mapValue.ObjectValue is GesValueMap map)
        {
            vm.SetRecord(destinationRegister, vm.FetchStringByPointer(typeTextPointer), map);
            return;
        }

        vm.SetNothing(destinationRegister);
    }

    internal static void GesVmCreateExternalType(this GesVirtualMachine vm, ushort destinationRegister, ushort externalTypeConstructorBindId, ushort argumentNamesIndex)
    {
        if (externalTypeConstructorBindId >= vm.ExternalTypeBinds.Length || vm.ExternalTypeBinds[externalTypeConstructorBindId].Kind != GameEventScriptBinaryBindKind.ExternalType)
        {
            vm.SetNothing(destinationRegister);
            vm.RaiseError(GameEventScriptDiagnosticCodes.RuntimeInvalidExternalTypeBinding,
                $"External type constructor bind id '{externalTypeConstructorBindId}' was not found.");
            return;
        }

        var bind = vm.ExternalTypeBinds[externalTypeConstructorBindId];
        var argumentNames = vm.FetchUInt16SliceTableByPointer(argumentNamesIndex);
        if (argumentNames.Length != vm.StageLength || argumentNames.Length != bind.ArgumentNames.Count)
        {
            vm.SetNothing(destinationRegister);
            return;
        }

        var labels = argumentNames.Length == 0 ? [] : new string[argumentNames.Length];
        for (var labelIndex = 0; labelIndex < labels.Length; labelIndex++)
        {
            labels[labelIndex] = vm.FetchStringByPointer(argumentNames[labelIndex]);
            if (!string.Equals(labels[labelIndex], GameEventScriptMessageSignature.UnlabeledParameterName, System.StringComparison.Ordinal)) continue;
            vm.SetNothing(destinationRegister);
            return;
        }

        if (externalTypeConstructorBindId >= vm.BoundExternalTypeConstructors.Length || vm.BoundExternalTypeConstructors[externalTypeConstructorBindId] is not { } constructor)
        {
            vm.SetNothing(destinationRegister);
            vm.RaiseError(GameEventScriptDiagnosticCodes.RuntimeInvalidExternalTypeBinding,
                $"External type constructor bind id '{externalTypeConstructorBindId}' was not dynamically bound.");
            return;
        }

        if (constructor.Definition.Parameters.Count != labels.Length)
        {
            vm.SetNothing(destinationRegister);
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
                vm.SetNothing(destinationRegister);
                return;
            }

            var source = vm.RegisterStaged((ushort)argumentIndex);
            GesValue converted;
            if (GameEventScriptBytecodeInstructionUnits.ParseSourceQuantityTypeName(parameter.TypeName) is { } quantityUnit)
            {
                converted = GesVmRegisterTypeCastCheck.GesVmCastUnit(in source, quantityUnit, vm);
                if (converted.IsNothing)
                {
                    vm.SetNothing(destinationRegister);
                    return;
                }

                arguments[parameterIndex] = GameEventScriptExternalTypeValueConverter.CoerceToDeclaredType(in converted, parameter);
                continue;
            }

            switch (parameter.TypeName)
            {
                case "Nothing":
                    converted = new GesValue();
                    converted.SetNothing();
                    break;
                case "Boolean":
                    converted = GesVmRegisterTypeCastCheck.GesVmCast(in source, Boolean, vm, destinationRegister);
                    break;
                case "Number":
                    converted = GesVmRegisterTypeCastCheck.GesVmCast(in source, Float, vm, destinationRegister);
                    break;
                case "numeric":
                    converted = GesVmRegisterTypeCastCheck.GesVmCastNumeric(in source, vm);
                    break;
                case "Percentage":
                    converted = GesVmRegisterTypeCastCheck.GesVmCast(in source, Percentage, vm, destinationRegister);
                    break;
                case "Tag":
                    converted = GesVmRegisterTypeCastCheck.GesVmCast(in source, Tag, vm, destinationRegister);
                    break;
                case "Text":
                    converted = GesVmRegisterTypeCastCheck.GesVmCast(in source, Text, vm, destinationRegister);
                    break;
                case "Vector":
                    converted = GesVmRegisterTypeCastCheck.GesVmCast(in source, Vector, vm, destinationRegister);
                    break;
                case "Point":
                    converted = GesVmRegisterTypeCastCheck.GesVmCast(in source, Point, vm, destinationRegister);
                    break;
                case "Range":
                    converted = GesVmRegisterTypeCastCheck.GesVmCast(in source, Range, vm, destinationRegister);
                    break;
                case "Message":
                    converted = GesVmRegisterTypeCastCheck.GesVmCast(in source, Message, vm, destinationRegister);
                    break;
                case "Handler":
                    converted = GesVmRegisterTypeCastCheck.GesVmCast(in source, Handler, vm, destinationRegister);
                    break;
                case "List":
                    converted = GesVmRegisterTypeCastCheck.GesVmCast(in source, List, vm, destinationRegister);
                    break;
                case "Map":
                    converted = GesVmRegisterTypeCastCheck.GesVmCast(in source, Map, vm, destinationRegister);
                    break;
                case "Dice":
                    converted = GesVmRegisterTypeCastCheck.GesVmCast(in source, Dice, vm, destinationRegister);
                    break;
                case "degree":
                    converted = GesVmRegisterTypeCastCheck.GesVmCastUnit(in source, UnitDegree, vm);
                    break;
                case "meter":
                    converted = GesVmRegisterTypeCastCheck.GesVmCastUnit(in source, UnitMeter, vm);
                    break;
                case "second":
                    converted = GesVmRegisterTypeCastCheck.GesVmCastUnit(in source, UnitSecond, vm);
                    break;
                default:
                    converted = new GesValue();
                    converted.SetNothing();
                    break;
            }

            if (converted.IsNothing)
            {
                vm.SetNothing(destinationRegister);
                return;
            }

            arguments[parameterIndex] = GameEventScriptExternalTypeValueConverter.CoerceToDeclaredType(in converted, parameter);
        }

        var call = vm.ExternalTypeConstructorCall;
        call.BeginCall(vm, destinationRegister, new GesValueArguments(arguments), constructor.Definition.TypeName);
        try
        {
            try
            {
                constructor.Invoke(call);
            }
            catch (GameEventScriptFatalRuntimeException)
            {
                // A deliberate GameEventScriptExtensionFaultException, or another
                // internal fatal signal, is reported as-is by the caller.
                throw;
            }
            catch (System.Exception exception)
            {
                vm.SetNothing(destinationRegister);
                vm.RaiseError(GameEventScriptDiagnosticCodes.RuntimeExternalConstructorFailed,
                    $"External type '{constructor.Definition.TypeName}' constructor failed unexpectedly.",
                    GameEventScriptRuntimeExceptionText.Describe(exception), symbol: constructor.Definition.TypeName);
                return;
            }

            if (!call.HasResult)
            {
                vm.SetNothing(destinationRegister);
            }
        }
        finally
        {
            call.EndCall();
        }
    }
}
