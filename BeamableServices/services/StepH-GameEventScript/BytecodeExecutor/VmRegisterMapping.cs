#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Types.GameEventScriptValueKind;

namespace StepH.GameEventScript.BytecodeExecutor;

public static class VmRegisterMapping
{
    public static void BindArguments(ref this VmRegister destination, GameEventScriptValue argument)
    {
        switch (argument.Kind)
        {
            case Nothing:
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
            case Float:
                destination.SetFloat(argument.AsNumber());
                break;
            case Integer:
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
}