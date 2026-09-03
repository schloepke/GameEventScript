using System;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Runtime.Values;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.Runtime.VM;

internal static class GesVmRegisterIteratorTerminals
{
    internal static void GesVmCount(this GesVmState vmState, ushort destinationRegister, in GesValue source)
    {
        switch (source.Kind)
        {
            case List when source.ObjectValue is GesValue[] list:
                vmState.SetInteger(destinationRegister, list.Length);
                return;
            case Map when source.ObjectValue is GesValueMap map:
                vmState.SetInteger(destinationRegister, map.Length);
                return;
            case Dice when source.ObjectValue is int[] dice:
                vmState.SetInteger(destinationRegister, dice.Length);
                return;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesValueRangeInteger range:
                vmState.SetInteger(destinationRegister, GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step));
                return;
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesValueRangeFloat range:
                vmState.SetInteger(destinationRegister, GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step));
                return;
            case Vector or Point when source.ObjectValue is GesValueVectorPoint:
                vmState.SetInteger(destinationRegister, 3);
                return;
            case Text or Tag:
                vmState.SetInteger(destinationRegister, source.IntegerValue);
                return;
            case Nothing:
                vmState.SetInteger(destinationRegister, 0);
                return;
        }

        IGesIterator iterator;
        if (source is { Kind: Iterator, ObjectValue: IGesIterator sourceIterator }) iterator = sourceIterator;
        else if (source.CreateIterator() is not { } createdIterator)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }
        else
        {
            iterator = createdIterator;
        }

        long count = 0;
        try
        {
            while (iterator.Next().HasValue) count++;
            vmState.SetInteger(destinationRegister, count);
        }
        finally
        {
            if (iterator is IDisposable disposable) disposable.Dispose();
        }
    }

}
