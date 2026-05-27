using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.BytecodeExecutor;

internal static class VmRegisterMapping
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void BindArguments(ref this VmValue destination, GameEventScriptValue argument, ref GameEventScriptTextTable textTable)
    {
        switch (argument.Kind)
        {
            case GameEventScriptValueKind.Tag:
                destination.SetTag(argument.AsText());
                break;
            case GameEventScriptValueKind.Text:
                destination.SetText(argument.AsText());
                break;
            case GameEventScriptValueKind.Percentage:
                destination.SetPercentage(argument.AsNumber());
                break;
            case GameEventScriptValueKind.Vector when argument is GameEventScriptVectorValue vector:
                destination.SetVector(vector.X, vector.Y, vector.Z, vector.Unit);
                break;
            case GameEventScriptValueKind.Point when argument is GameEventScriptPointValue point:
                destination.SetPoint(point.X, point.Y, point.Z, point.Unit);
                break;
            case GameEventScriptValueKind.Number:
                if (argument.IsInteger()) destination.SetInteger(argument.AsInteger(), argument.Unit);
                else destination.SetFloat(argument.AsNumber(), argument.Unit);
                break;
            case GameEventScriptValueKind.Boolean:
                destination.SetBoolean(argument.AsBoolean());
                break;
            case GameEventScriptValueKind.Range when argument is GameEventScriptRangeValue range:
                if (range.IsIntegerRange) destination.SetRange(range.From, range.To, range.Step);
                else destination.SetRange(range.FromNumber, range.ToNumber, range.StepNumber);
                break;
            case GameEventScriptValueKind.Message when argument is GameEventScriptMessageValue message:
                destination.SetMessage(message.Value);
                break;
            case GameEventScriptValueKind.Handler when argument is GameEventScriptHandlerValue handler:
                destination.SetMessageHandler(handler.Signature);
                break;
            case GameEventScriptValueKind.List:
            case GameEventScriptValueKind.Map:
            case GameEventScriptValueKind.Dice:
            case GameEventScriptValueKind.Series:
            case GameEventScriptValueKind.Nothing:
            default:
                destination.SetNothing();
                break;
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
        List when a.ObjectValue is VmListObject list => GameEventScriptValueFactory.GesList(list.ToGameEventScriptValues(ref textTable)),
        Map when a.ObjectValue is VmMapObject map => GameEventScriptValueFactory.GesMap(map.ToGameEventScriptValues(ref textTable)),
        Dice when a.ObjectValue is int[] dice => GameEventScriptValueFactory.GesDice(dice),
        GameEventScriptBytecodeTypeKind.Range when a.ObjectValue is VmRange r => GameEventScriptValueFactory.GesRange(r.from, r.to, r.step),
        GameEventScriptBytecodeTypeKind.Range when a.ObjectValue is VmFloatRange r => GameEventScriptValueFactory.GesRange(r.from, r.to, r.step),
        Handler when a.ObjectValue is GameEventScriptMessageSignature signature => GameEventScriptValueFactory.GesHandler(signature),
        Message when a.ObjectValue is GameEventScriptMessage message => GameEventScriptValueFactory.GesMessage(message),
        _ => GameEventScriptValueFactory.GesNothing(),
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static List<GameEventScriptValue> ToGameEventScriptValues(this VmListObject list, ref GameEventScriptTextTable textTable)
    {
        var result = new List<GameEventScriptValue>();
        for (var i = 0; i < list.Items.Length; i++)
        {
            result.Add(list.Items[i].ToGameEventScriptValue(ref textTable));
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Dictionary<string, GameEventScriptValue> ToGameEventScriptValues(this VmMapObject map, ref GameEventScriptTextTable textTable)
    {
        var result = new Dictionary<string, GameEventScriptValue>();
        foreach (var (key, value) in map.Entries)
        {
            var x = value;
            result.Add(key, x.ToGameEventScriptValue(ref textTable));
        }
        
        return result;
    }


}
