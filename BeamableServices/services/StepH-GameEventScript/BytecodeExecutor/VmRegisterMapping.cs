using System;
using System.Runtime.CompilerServices;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeInstructionUnit;
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
            {
                var vector = (GameEventScriptVectorValue)argument;
                destination.SetObject(Vector, new VmFloatTriplet(vector.X, vector.Y, vector.Z), EncodeUnit(vector.Unit));
                break;
            }
            case GameEventScriptValueKind.Point:
            {
                var point = (GameEventScriptPointValue)argument;
                destination.SetObject(Point, new VmFloatTriplet(point.X, point.Y, point.Z), EncodeUnit(point.Unit));
                break;
            }
            case GameEventScriptValueKind.Number:
            {
                var unit = EncodeUnit(argument is GameEventScriptNumberValue number ? number.Unit : null);
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
    internal static GameEventScriptValue ToGameEventScriptValue(this ref VmValue a) => a.Kind switch
    {
        Integer => GameEventScriptValueFactory.GesInteger(a.IntegerValue),
        Float => GameEventScriptValueFactory.GesFloat(a.FloatValue),
        Percentage => GameEventScriptValueFactory.GesPercentage(a.FloatValue),
        Vector when a.ObjectValue is VmFloatTriplet vector => GameEventScriptValueFactory.GesVector(vector.X, vector.Y, vector.Z, DecodeUnit(a.Unit)),
        Point when a.ObjectValue is VmFloatTriplet point => GameEventScriptValueFactory.GesPoint(point.X, point.Y, point.Z, DecodeUnit(a.Unit)),
        GameEventScriptBytecodeTypeKind.Boolean => GameEventScriptValueFactory.GesBoolean(a.IsTrue),
        // FIXME this might not work here, since we need to binary to look up strings and tags
        _ => GameEventScriptValueFactory.GesNothing(),
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static GameEventScriptBytecodeInstructionUnit EncodeUnit(GameEventScriptNumericUnit? unit)
        => unit switch
        {
            GameEventScriptNumericUnit.Degree => UnitDegree,
            GameEventScriptNumericUnit.Meter => UnitMeter,
            GameEventScriptNumericUnit.Second => UnitSecond,
            _ => UnitNone
        };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static GameEventScriptNumericUnit? DecodeUnit(GameEventScriptBytecodeInstructionUnit unit)
        => unit switch
        {
            UnitDegree => GameEventScriptNumericUnit.Degree,
            UnitMeter => GameEventScriptNumericUnit.Meter,
            UnitSecond => GameEventScriptNumericUnit.Second,
            _ => null
        };
}
