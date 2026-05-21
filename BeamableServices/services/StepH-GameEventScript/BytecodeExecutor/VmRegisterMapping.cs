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
    internal static void BindArguments(ref this VmValue destination, GameEventScriptValue argument)
    {
        switch (argument.Kind)
        {
            case GameEventScriptValueKind.Nothing:
                destination.SetNothing();
                break;
            case GameEventScriptValueKind.Tag:
                throw new NotImplementedException();
                break;
            case GameEventScriptValueKind.Text:
                throw new NotImplementedException();
                break;
            case GameEventScriptValueKind.Percentage:
                destination.SetPercentage(argument.AsNumber());
                break;
            case GameEventScriptValueKind.Vector:
                throw new NotImplementedException();
                break;
            case GameEventScriptValueKind.Point:
                throw new NotImplementedException();
                break;
            case GameEventScriptValueKind.Float:
                destination.SetFloat(argument.AsNumber());
                break;
            case GameEventScriptValueKind.Integer:
                destination.SetInteger(argument.AsInteger());
                break;
            case GameEventScriptValueKind.Boolean:
                destination.SetBoolean(argument.AsBoolean());
                break;
            case GameEventScriptValueKind.Uuid:
                throw new NotImplementedException();
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
            case GameEventScriptValueKind.Ref:
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
    internal static GameEventScriptValue ToGameEventScriptValue(this ref VmValue a) => a.Kind switch
    {
        Integer => GameEventScriptValueFactory.GesInteger(a.IntegerValue),
        Float => GameEventScriptValueFactory.GesFloat(a.FloatValue),
        Percentage => GameEventScriptValueFactory.GesPercentage(a.FloatValue),
        GameEventScriptBytecodeTypeKind.Boolean => GameEventScriptValueFactory.GesBoolean(a.IsTrue),
        // FIXME this might not work here, since we need to binary to look up strings and tags
        _ => GameEventScriptValueFactory.GesNothing(),
    };
}
