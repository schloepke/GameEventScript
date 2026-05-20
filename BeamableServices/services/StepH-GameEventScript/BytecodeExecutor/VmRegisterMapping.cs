using System;
using System.Runtime.CompilerServices;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.BytecodeExecutor.VmValue;
using static StepH.GameEventScript.Types.GameEventScriptValueKind;

namespace StepH.GameEventScript.BytecodeExecutor;

internal static class VmRegisterMapping
{

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmDefault(ref this VmValue dst, ref VmValue a, ref VmValue b) => dst = a.Kind == VmValueKind.Nothing ? b : a;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void BindArguments(ref this VmValue destination, GameEventScriptValue argument)
    {
        switch (argument.Kind)
        {
            case GameEventScriptValueKind.Nothing:
                destination.SetNothing();
                break;
            case Tag:
                throw new NotImplementedException();
                break;
            case Text:
                throw new NotImplementedException();
                break;
            case Percentage:
                destination.SetPercentage(argument.AsNumber());
                break;
            case Vector:
                throw new NotImplementedException();
                break;
            case Point:
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
            case Uuid:
                throw new NotImplementedException();
                break;
            case Series:
                throw new NotImplementedException();
                break;
            case GameEventScriptValueKind.Range:
                throw new NotImplementedException();
                break;
            case Message:
                throw new NotImplementedException();
                break;
            case Handler:
                throw new NotImplementedException();
                break;
            case Ref:
                throw new NotImplementedException();
                break;
            case List:
                throw new NotImplementedException();
                break;
            case Map:
                throw new NotImplementedException();
                break;
            case Dice:
                throw new NotImplementedException();
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static GameEventScriptValue ToGameEventScriptValue(this ref VmValue a) => a.Kind switch
    {
        VmValueKind.Integer => GameEventScriptValueFactory.GesInteger(a.IntegerValue),
        VmValueKind.Float => GameEventScriptValueFactory.GesFloat(a.FloatValue),
        VmValueKind.Percentage => GameEventScriptValueFactory.GesPercentage(a.FloatValue),
        VmValueKind.Boolean => GameEventScriptValueFactory.GesBoolean(a.AsBooleanValue),
        // FIXME this might not work here, since we need to binary to look up strings and tags
        _ => GameEventScriptValueFactory.GesNothing(),
    };
}
