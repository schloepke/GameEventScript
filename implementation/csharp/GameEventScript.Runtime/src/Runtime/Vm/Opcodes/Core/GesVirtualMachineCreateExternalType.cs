// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;
using static GameEventScript.Api.GesQuantityUnit;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteCreateExternalType(ushort destinationRegister, ushort externalTypeConstructorBindId, ushort argumentNamesIndex)
    {
        if (externalTypeConstructorBindId >= ExternalTypeBinds.Length || ExternalTypeBinds[externalTypeConstructorBindId].Kind != GesBinaryBindKind.ExternalType)
        {
            SetNothing(destinationRegister);
            RaiseError(GesDiagnosticCodes.RuntimeInvalidExternalTypeBinding,
                $"External type constructor bind id '{externalTypeConstructorBindId}' was not found.");
            return;
        }

        var bind = ExternalTypeBinds[externalTypeConstructorBindId];
        var argumentNames = FetchUInt16SliceTableByPointer(argumentNamesIndex);
        if (argumentNames.Length != StageLength || argumentNames.Length != bind.ArgumentNames.Count)
        {
            SetNothing(destinationRegister);
            return;
        }

        var labels = argumentNames.Length == 0 ? [] : new string[argumentNames.Length];
        for (var labelIndex = 0; labelIndex < labels.Length; labelIndex++)
        {
            labels[labelIndex] = FetchStringByPointer(argumentNames[labelIndex]);
            if (!string.Equals(labels[labelIndex], GesMessageSignature.UnlabeledParameterName, System.StringComparison.Ordinal)) continue;
            SetNothing(destinationRegister);
            return;
        }

        if (externalTypeConstructorBindId >= BoundExternalTypeConstructors.Length || BoundExternalTypeConstructors[externalTypeConstructorBindId] is not { } constructor)
        {
            SetNothing(destinationRegister);
            RaiseError(GesDiagnosticCodes.RuntimeInvalidExternalTypeBinding,
                $"External type constructor bind id '{externalTypeConstructorBindId}' was not dynamically bound.");
            return;
        }

        if (constructor.Definition.Parameters.Count != labels.Length)
        {
            SetNothing(destinationRegister);
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
                SetNothing(destinationRegister);
                return;
            }

            var source = RegisterStaged((ushort)argumentIndex);
            GesValue converted;
            if (GesQuantityUnits.ParseSourceQuantityTypeName(parameter.TypeName) is { } quantityUnit)
            {
                converted = CastUnitValue(in source, quantityUnit, this);
                if (converted.IsNothing)
                {
                    SetNothing(destinationRegister);
                    return;
                }

                arguments[parameterIndex] = GesExternalTypeValueConverter.CoerceToDeclaredType(in converted, parameter);
                continue;
            }

            switch (parameter.TypeName)
            {
                case "Nothing":
                    converted = new GesValue();
                    converted.SetNothing();
                    break;
                case "Boolean":
                    converted = CastValue(in source, Boolean, this, destinationRegister);
                    break;
                case "Number":
                    converted = CastValue(in source, Float, this, destinationRegister);
                    break;
                case "numeric":
                    converted = CastNumericValue(in source, this);
                    break;
                case "Percentage":
                    converted = CastValue(in source, Percentage, this, destinationRegister);
                    break;
                case "Tag":
                    converted = CastValue(in source, Tag, this, destinationRegister);
                    break;
                case "Text":
                    converted = CastValue(in source, Text, this, destinationRegister);
                    break;
                case "Vector":
                    converted = CastValue(in source, Vector, this, destinationRegister);
                    break;
                case "Point":
                    converted = CastValue(in source, Point, this, destinationRegister);
                    break;
                case "Range":
                    converted = CastValue(in source, Range, this, destinationRegister);
                    break;
                case "Message":
                    converted = CastValue(in source, Message, this, destinationRegister);
                    break;
                case "Handler":
                    converted = CastValue(in source, Handler, this, destinationRegister);
                    break;
                case "List":
                    converted = CastValue(in source, List, this, destinationRegister);
                    break;
                case "Map":
                    converted = CastValue(in source, Map, this, destinationRegister);
                    break;
                case "Dice":
                    converted = CastValue(in source, Dice, this, destinationRegister);
                    break;
                case "degree":
                    converted = CastUnitValue(in source, UnitDegree, this);
                    break;
                case "meter":
                    converted = CastUnitValue(in source, UnitMeter, this);
                    break;
                case "second":
                    converted = CastUnitValue(in source, UnitSecond, this);
                    break;
                default:
                    converted = new GesValue();
                    converted.SetNothing();
                    break;
            }

            if (converted.IsNothing)
            {
                SetNothing(destinationRegister);
                return;
            }

            arguments[parameterIndex] = GesExternalTypeValueConverter.CoerceToDeclaredType(in converted, parameter);
        }

        var call = ExternalTypeConstructorCall;
        call.BeginCall(this, destinationRegister, new GesValueArguments(arguments), constructor.Definition.TypeName);
        try
        {
            try
            {
                constructor.Invoke(call);
            }
            catch (GesFatalRuntimeException)
            {
                // A deliberate GesExtensionFaultException, or another
                // internal fatal signal, is reported as-is by the caller.
                throw;
            }
            catch (System.Exception exception)
            {
                SetNothing(destinationRegister);
                RaiseError(GesDiagnosticCodes.RuntimeExternalConstructorFailed,
                    $"External type '{constructor.Definition.TypeName}' constructor failed unexpectedly.",
                    GesRuntimeExceptionText.Describe(exception), symbol: constructor.Definition.TypeName);
                return;
            }

            if (!call.HasResult)
            {
                SetNothing(destinationRegister);
            }
        }
        finally
        {
            call.EndCall();
        }
    }
}
