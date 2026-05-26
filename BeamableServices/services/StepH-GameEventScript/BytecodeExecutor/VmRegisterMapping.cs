using System;
using System.Runtime.CompilerServices;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.BytecodeExecutor;

internal static class VmRegisterMapping
{

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmDefault(ref this VmValue dst, ref VmValue a, ref VmValue b) => dst = a.Kind == Nothing ? b : a;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void BindArguments(ref this VmValue destination, GameEventScriptValue argument, ref GameEventScriptTextTable textTable)
    {
        switch (argument.Kind)
        {
            case GameEventScriptValueKind.Nothing:
                destination.SetNothing();
                break;
            case GameEventScriptValueKind.Tag:
                destination.SetTag(argument.AsText());
                break;
            case GameEventScriptValueKind.Text:
                destination.SetText(argument.AsText());
                break;
            case GameEventScriptValueKind.Percentage:
                destination.SetPercentage(argument.AsNumber());
                break;
            case GameEventScriptValueKind.Vector:
            {
                var vector = (GameEventScriptVectorValue)argument;
                destination.SetVector(vector.X, vector.Y, vector.Z, vector.Unit);
                break;
            }
            case GameEventScriptValueKind.Point:
            {
                var point = (GameEventScriptPointValue)argument;
                destination.SetPoint(point.X, point.Y, point.Z, point.Unit);
                break;
            }
            case GameEventScriptValueKind.Number:
            {
                var unit = argument.Unit;
                if (argument.IsInteger()) destination.SetInteger(argument.AsInteger(), unit);
                else destination.SetFloat(argument.AsNumber(), unit);
                break;
            }
            case GameEventScriptValueKind.Boolean:
                destination.SetBoolean(argument.AsBoolean());
                break;
            case GameEventScriptValueKind.Series:
                throw new NotImplementedException();
                break;
            case GameEventScriptValueKind.Range:
                throw new NotImplementedException();
                break;
            case GameEventScriptValueKind.Message:
                throw new NotImplementedException();
                break;
            case GameEventScriptValueKind.Handler:
                throw new NotImplementedException();
                break;
            case GameEventScriptValueKind.List:
                throw new NotImplementedException();
                break;
            case GameEventScriptValueKind.Map:
                throw new NotImplementedException();
                break;
            case GameEventScriptValueKind.Dice:
                throw new NotImplementedException();
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static GameEventScriptValue ToGameEventScriptValue(this ref VmValue a, ref GameEventScriptTextTable textTable) => a.Kind switch
    {
        Integer => GameEventScriptValueFactory.GesInteger(a.IntegerValue, a.Unit),
        Float => GameEventScriptValueFactory.GesFloat(a.FloatValue, a.Unit),
        Percentage => GameEventScriptValueFactory.GesPercentage(a.FloatValue),
        Vector when a.ObjectValue is VmFloatTriplet vector => GameEventScriptValueFactory.GesVector(vector.X, vector.Y, vector.Z, a.Unit),
        Point when a.ObjectValue is VmFloatTriplet point => GameEventScriptValueFactory.GesPoint(point.X, point.Y, point.Z, a.Unit),
        GameEventScriptBytecodeTypeKind.Boolean => GameEventScriptValueFactory.GesBoolean(a.IsTrue),
        Text => GameEventScriptValueFactory.GesText(a.IsStorageObject ? a.ObjectValue! as string : textTable.Resolve((ushort)a.IntegerValue)),
        Tag => GameEventScriptValueFactory.GesTag(a.IsStorageObject ? a.ObjectValue! as string : textTable.Resolve((ushort)a.IntegerValue)),
        // FIXME this might not work here, since we need to binary to look up strings and tags
        _ => GameEventScriptValueFactory.GesNothing(),
    };
}
