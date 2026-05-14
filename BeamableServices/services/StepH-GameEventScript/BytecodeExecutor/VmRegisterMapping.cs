#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.BytecodeExecutor.VmValue;
using static StepH.GameEventScript.Types.GameEventScriptValueKind;

namespace StepH.GameEventScript.BytecodeExecutor;

public static class VmRegisterMapping
{
    public static void BindArguments(ref this VmValue destination, GameEventScriptValue argument)
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
                destination.SetFloat(argument.AsNumber(), GameEventScriptBytecodeInstructionUnit.Percentage);
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
            case Optional:
                throw new NotImplementedException();
                break;
            case Sequence:
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
            case Dictionary:
                throw new NotImplementedException();
                break;
            case Set:
                throw new NotImplementedException();
                break;
            case Dice:
                throw new NotImplementedException();
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public static GameEventScriptValue ToGameEventScriptValue(this ref VmValue a) => a.Kind switch
    {
        VmValueKind.Integer => GameEventScriptValueFactory.GesInteger(a.AsIntegerValue),
        VmValueKind.Float => GameEventScriptValueFactory.GesFloat(a.AsFloatValue),
        VmValueKind.Boolean => GameEventScriptValueFactory.GesBoolean(a.AsBooleanValue),
        // FIXME this might not work here, since we need to binary to look up strings and tags
        _ => GameEventScriptValueFactory.GesNothing(),
    };
}