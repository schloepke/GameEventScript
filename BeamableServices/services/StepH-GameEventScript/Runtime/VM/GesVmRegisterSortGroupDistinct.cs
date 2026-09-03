using System;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Runtime.Values;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.Runtime.VM;

internal static class GesVmRegisterSortGroupDistinct
{
    internal static void GesVmDistinct(this GesVmState vmState, ushort destinationRegister, in GesValue source)
    {
        var resultValue = new GesValue();
        switch (source.Kind)
        {
            case Nothing:
                vmState.SetNothing(destinationRegister);
                return;
            case List when source.ObjectValue is GesValue[] list:
            {
                if (list.Length == 0)
                {
                    vmState.SetList(destinationRegister, vmState.EmptyList);
                    return;
                }

                var result = new GesValue[list.Length];
                var resultLength = 0;
                for (var i = 0; i < list.Length; i++)
                {
                    var item = list[i];
                    var found = false;
                    for (var j = 0; j < resultLength; j++)
                    {
                        if (!result[j].EqualsValue(in item)) continue;
                        found = true;
                        break;
                    }

                    if (found) continue;
                    result[resultLength++] = item;
                }

                if (resultLength == list.Length)
                {
                    vmState.SetList(destinationRegister, result);
                    return;
                }

                var compact = new GesValue[resultLength];
                for (var i = 0; i < resultLength; i++) compact[i] = result[i];
                vmState.SetList(destinationRegister, compact);
                return;
            }
            case Dice when source.ObjectValue is int[] dice:
            {
                if (dice.Length == 0)
                {
                    vmState.SetDice(destinationRegister, []);
                    return;
                }

                var values = new int[dice.Length];
                var count = 0;
                var previous = 0;
                for (var i = 0; i < dice.Length; i++)
                {
                    var value = dice[i];
                    if (count > 0 && value == previous) continue;
                    values[count++] = value;
                    previous = value;
                }

                if (count == values.Length)
                {
                    vmState.SetDice(destinationRegister, values);
                    return;
                }

                var compact = new int[count];
                Array.Copy(values, compact, count);
                vmState.SetDice(destinationRegister, compact);
                return;
            }
            case Iterator when source.ObjectValue is IGesIterator iterator:
            {
                var values = new GesValue[16];
                var count = 0;
                try
                {
                    GesIteratorResult item;
                    while ((item = iterator.Next()).HasValue)
                    {
                        var found = false;
                        for (var i = 0; i < count; i++)
                        {
                            if (!values[i].EqualsValue(in item.Value)) continue;
                            found = true;
                            break;
                        }

                        if (found) continue;
                        if (count == values.Length)
                        {
                            var resized = new GesValue[values.Length << 1];
                            Array.Copy(values, resized, values.Length);
                            values = resized;
                        }

                        values[count++] = item.Value;
                    }
                }
                finally
                {
                    if (iterator is IDisposable disposable) disposable.Dispose();
                }

                var result = new GesValue[count];
                for (var i = 0; i < count; i++) result[i] = values[i];
                vmState.SetList(destinationRegister, result);
                return;
            }
            default:
                resultValue.SetNothing();
                vmState.SetValue(destinationRegister, in resultValue);
                return;
        }
    }
    internal static void GesVmSortAscending(this GesVmState vmState, ushort destinationRegister, in GesValue source)
    {
        GesVmSort(vmState, destinationRegister, in source, descending: false);
    }
    internal static void GesVmSortDescending(this GesVmState vmState, ushort destinationRegister, in GesValue source)
    {
        GesVmSort(vmState, destinationRegister, in source, descending: true);
    }
    private static void GesVmSort(GesVmState vmState, ushort destinationRegister, in GesValue source, bool descending)
    {
        switch (source.Kind)
        {
            case List when source.ObjectValue is GesValue[] list:
            {
                if (list.Length == 0)
                {
                    vmState.SetList(destinationRegister, vmState.EmptyList);
                    return;
                }

                var result = new GesValue[list.Length];
                for (var i = 0; i < list.Length; i++) result[i] = list[i];
                if (!SortValues(result, list.Length, descending))
                {
                    vmState.SetNothing(destinationRegister);
                    return;
                }

                vmState.SetList(destinationRegister, result);
                return;
            }
            case Dice when source.ObjectValue is int[] dice:
            {
                var result = new GesValue[dice.Length];
                if (descending)
                {
                    for (var i = 0; i < dice.Length; i++) result[i].SetInteger(dice[i]);
                }
                else
                {
                    for (var i = 0; i < dice.Length; i++) result[i].SetInteger(dice[dice.Length - i - 1]);
                }

                vmState.SetList(destinationRegister, result);
                return;
            }
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesValueRangeInteger range:
            {
                var length = GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step);
                if (length == 0)
                {
                    vmState.SetRange(destinationRegister, 0, 0, 0);
                    return;
                }

                var sourceDescending = range.Step < 0;
                if (sourceDescending == descending)
                {
                    vmState.SetRange(destinationRegister, range.From, range.To, range.Step);
                    return;
                }

                if (GameEventScriptRangeMath.GetTerm(range.From, range.To, range.Step, length) is { } last)
                {
                    vmState.SetRange(destinationRegister, last, range.From, -range.Step);
                    return;
                }

                vmState.SetNothing(destinationRegister);
                return;
            }
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesValueRangeFloat range:
            {
                var length = GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step);
                if (length == 0)
                {
                    vmState.SetRange(destinationRegister, 0, 0, 0);
                    return;
                }

                var sourceDescending = range.Step < 0d;
                if (sourceDescending == descending)
                {
                    vmState.SetRange(destinationRegister, range.From, range.To, range.Step);
                    return;
                }

                if (GameEventScriptRangeMath.GetTerm(range.From, range.To, range.Step, length) is { } last)
                {
                    vmState.SetRange(destinationRegister, last, range.From, -range.Step);
                    return;
                }

                vmState.SetNothing(destinationRegister);
                return;
            }
            case Iterator when source.ObjectValue is IGesIterator iterator:
            {
                var values = new GesValue[16];
                var count = 0;
                try
                {
                    GesIteratorResult item;
                    while ((item = iterator.Next()).HasValue)
                    {
                        if (count == values.Length)
                        {
                            var resized = new GesValue[values.Length << 1];
                            Array.Copy(values, resized, values.Length);
                            values = resized;
                        }

                        values[count++] = item.Value;
                    }
                }
                finally
                {
                    if (iterator is IDisposable disposable) disposable.Dispose();
                }

                if (!SortValues(values, count, descending))
                {
                    vmState.SetNothing(destinationRegister);
                    return;
                }

                var result = new GesValue[count];
                for (var i = 0; i < count; i++) result[i] = values[i];
                vmState.SetList(destinationRegister, result);
                return;
            }
            default:
                vmState.SetNothing(destinationRegister);
                return;
        }
    }

    private static bool SortValues(GesValue[] values, int count, bool descending)
    {
        for (var i = 1; i < count; i++)
        {
            var value = values[i];
            var j = i - 1;
            while (j >= 0)
            {
                if (CompareAscending(in values[j], in value) is not { } comparison) return false;
                if ((descending ? -comparison : comparison) <= 0) break;
                values[j + 1] = values[j];
                j--;
            }

            values[j + 1] = value;
        }

        return true;
    }

    internal static bool SortValuesByKeys(GesValue[] values, GesValue[] keys, int count, bool descending)
    {
        for (var i = 1; i < count; i++)
        {
            var value = values[i];
            var key = keys[i];
            var j = i - 1;
            while (j >= 0)
            {
                if (CompareAscending(in keys[j], in key) is not { } comparison) return false;
                if ((descending ? -comparison : comparison) <= 0) break;
                values[j + 1] = values[j];
                keys[j + 1] = keys[j];
                j--;
            }

            values[j + 1] = value;
            keys[j + 1] = key;
        }

        return true;
    }

    private static int? CompareAscending(in GesValue a, in GesValue b)
    {
        if (a.Kind is Nothing || b.Kind is Nothing)
        {
            return null;
        }

        if (a.IsNumeric && b.IsNumeric)
        {
            if (a.Unit != b.Unit)
            {
                return null;
            }

            var left = a.AsNumeric;
            var right = b.AsNumeric;
            if (double.IsNaN(left) || double.IsNaN(right))
            {
                return null;
            }

            return left.CompareTo(right);
        }

        var leftRank = GetStableRank(in a);
        var rightRank = GetStableRank(in b);
        if (leftRank != rightRank)
        {
            return leftRank.CompareTo(rightRank);
        }

        switch (a.Kind)
        {
            case Text when b.Kind is Text:
            case Tag when b.Kind is Tag:
                return GameEventScriptText.CompareScalarOrdinal(a.TextValue, b.TextValue);
            case Vector when b.Kind is Vector:
            case Point when b.Kind is Point:
                if (a.Unit != b.Unit ||
                    a.ObjectValue is not GesValueVectorPoint leftTriplet ||
                    b.ObjectValue is not GesValueVectorPoint rightTriplet)
                {
                    return null;
                }

                var comparison = leftTriplet.X.CompareTo(rightTriplet.X);
                if (comparison == 0) comparison = leftTriplet.Y.CompareTo(rightTriplet.Y);
                if (comparison == 0) comparison = leftTriplet.Z.CompareTo(rightTriplet.Z);
                return comparison;
            case GameEventScriptBytecodeTypeKind.Boolean when b.Kind is GameEventScriptBytecodeTypeKind.Boolean:
                return a.IsTrue.CompareTo(b.IsTrue);
            default:
                return 0;
        }
    }
    private static int GetStableRank(in GesValue value)
    {
        switch (value.Kind)
        {
            case Integer:
            case Float:
            case Percentage:
                return 1;
            case Tag:
                return 3;
            case Text:
                return 2;
            case Vector:
                return 4;
            case Point:
                return 5;
            case GameEventScriptBytecodeTypeKind.Boolean:
                return 6;
            case GameEventScriptBytecodeTypeKind.Range:
                return 8;
            case Message:
                return 9;
            case Handler:
                return 10;
            case List:
                return 11;
            case Map:
                return 12;
            case Dice:
                return 13;
            default:
                return 8;
        }
    }
}
