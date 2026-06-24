using System;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.VirtualMachine;

internal static class GesVmRegisterStreamTerminals
{
    internal static void GesVmCount(this GesVmState vmState, ushort destinationRegister, in GesVmValue iterator)
    {
        switch (iterator.Kind)
        {
            case List when iterator.ObjectValue is GesVmValue[] list:
                vmState.SetInteger(destinationRegister, list.Length);
                return;
            case Map when iterator.ObjectValue is GesVmValueMap map:
                vmState.SetInteger(destinationRegister, map.Length);
                return;
            case Dice when iterator.ObjectValue is int[] dice:
                vmState.SetInteger(destinationRegister, dice.Length);
                return;
            case GameEventScriptBytecodeTypeKind.Range when iterator.ObjectValue is GesVmValueRangeInteger range:
                vmState.SetInteger(destinationRegister, GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step));
                return;
            case GameEventScriptBytecodeTypeKind.Range when iterator.ObjectValue is GesVmValueRangeFloat range:
                vmState.SetInteger(destinationRegister, GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step));
                return;
            case Vector or Point when iterator.ObjectValue is GesVmValueVectorPoint:
                vmState.SetInteger(destinationRegister, 3);
                return;
            case Text or Tag:
                vmState.SetInteger(destinationRegister, iterator.TextValue.Length);
                return;
            case Nothing:
                vmState.SetInteger(destinationRegister, 0);
                return;
        }

        IGesVmStream stream;
        if (iterator is { Kind: Stream, ObjectValue: IGesVmStream sourceStream }) stream = sourceStream;
        else if (!iterator.TryCreateStream(out stream))
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        long count = 0;
        var item = new GesVmValue();
        try
        {
            while (stream.TryNext(ref item)) count++;
            vmState.SetInteger(destinationRegister, count);
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }
    }

}
