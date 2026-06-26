using System;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.Runtime.VM;

internal static class GesVmRegisterIteratorTerminals
{
    internal static void GesVmCount(this GesVmState vmState, ushort destinationRegister, in GesVmValue source)
    {
        switch (source.Kind)
        {
            case List when source.ObjectValue is GesVmValue[] list:
                vmState.SetInteger(destinationRegister, list.Length);
                return;
            case Map when source.ObjectValue is GesVmValueMap map:
                vmState.SetInteger(destinationRegister, map.Length);
                return;
            case Dice when source.ObjectValue is int[] dice:
                vmState.SetInteger(destinationRegister, dice.Length);
                return;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesVmValueRangeInteger range:
                vmState.SetInteger(destinationRegister, GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step));
                return;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesVmValueRangeFloat range:
                vmState.SetInteger(destinationRegister, GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step));
                return;
            case Vector or Point when source.ObjectValue is GesVmValueVectorPoint:
                vmState.SetInteger(destinationRegister, 3);
                return;
            case Text or Tag:
                vmState.SetInteger(destinationRegister, source.TextValue.Length);
                return;
            case Nothing:
                vmState.SetInteger(destinationRegister, 0);
                return;
        }

        IGesVmIterator iterator;
        if (source is { Kind: Iterator, ObjectValue: IGesVmIterator sourceIterator }) iterator = sourceIterator;
        else if (!source.TryCreateIterator(out iterator))
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        long count = 0;
        var item = new GesVmValue();
        try
        {
            while (iterator.TryNext(ref item)) count++;
            vmState.SetInteger(destinationRegister, count);
        }
        finally
        {
            if (iterator is IDisposable disposable) disposable.Dispose();
        }
    }

}
