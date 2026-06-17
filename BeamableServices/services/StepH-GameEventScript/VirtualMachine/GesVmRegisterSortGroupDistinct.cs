using System;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;
using static StepH.GameEventScript.VirtualMachine.GesVmMathConstants;

namespace StepH.GameEventScript.VirtualMachine;

internal static class GesVmRegisterSortGroupDistinct
{
    internal static void GesVmDistinct(this GesVmState vmState, ushort destinationRegister, in GesVmValue source)
    {
        var resultValue = new GesVmValue();
        resultValue.SetNothing();
        switch (source.Kind)
        {
            case Nothing:
                vmState.SetNothing(destinationRegister);
                return;
            case List when source.ObjectValue is GesVmValue[] list:
            {
                if (list.Length == 0)
                {
                    vmState.SetList(destinationRegister, vmState.EmptyList);
                    return;
                }

                var result = new GesVmValue[list.Length];
                var resultLength = 0;
                for (var i = 0; i < list.Length; i++)
                {
                    var item = list[i];
                    var found = false;
                    for (var j = 0; j < resultLength; j++)
                    {
                        if (!result[j].EqualsValue(ref item)) continue;
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

                var compact = new GesVmValue[resultLength];
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
            case Stream when source.ObjectValue is IGesVmStream stream:
            {
                var item = new GesVmValue();
                item.SetNothing();
                var values = new GesVmValue[16];
                var count = 0;
                try
                {
                    while (stream.TryNext(ref item))
                    {
                        var found = false;
                        for (var i = 0; i < count; i++)
                        {
                            if (!values[i].EqualsValue(ref item)) continue;
                            found = true;
                            break;
                        }

                        if (found) continue;
                        if (count == values.Length)
                        {
                            var resized = new GesVmValue[values.Length << 1];
                            Array.Copy(values, resized, values.Length);
                            values = resized;
                        }

                        values[count++] = item;
                    }
                }
                finally
                {
                    if (stream is IDisposable disposable) disposable.Dispose();
                }

                var result = new GesVmValue[count];
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
    internal static void GesVmDistinctBy(this GesVmState vmState, ushort destinationRegister, in GesVmValue source, ushort itemSlot, ushort keyEntryAddress, IGesVmStreamEntryEvaluator evaluator)
    {
        switch (source.Kind)
        {
            case List when source.ObjectValue is GesVmValue[] list:
            {
                if (list.Length == 0)
                {
                    vmState.SetList(destinationRegister, vmState.EmptyList);
                    return;
                }

                var values = new GesVmValue[list.Length];
                var keys = new GesVmValue[list.Length];
                var key = new GesVmValue();
                key.SetNothing();
                var count = 0;
                for (var i = 0; i < list.Length; i++)
                {
                    var item = list[i];
                    if (!evaluator.TryEvaluateStreamEntry(keyEntryAddress, itemSlot, ref item, null, ref key))
                    {
                        vmState.SetNothing(destinationRegister);
                        return;
                    }

                    var found = false;
                    for (var j = 0; j < count; j++)
                    {
                        if (!keys[j].EqualsValue(ref key)) continue;
                        found = true;
                        break;
                    }

                    if (found) continue;
                    values[count] = item;
                    keys[count] = key;
                    count++;
                }

                if (count == list.Length)
                {
                    vmState.SetList(destinationRegister, values);
                    return;
                }

                var compact = new GesVmValue[count];
                for (var i = 0; i < count; i++) compact[i] = values[i];
                vmState.SetList(destinationRegister, compact);
                return;
            }
            case Stream when source.ObjectValue is IGesVmStream stream:
            {
                var item = new GesVmValue();
                item.SetNothing();
                var key = new GesVmValue();
                key.SetNothing();
                var values = new GesVmValue[16];
                var keys = new GesVmValue[16];
                var count = 0;
                try
                {
                    while (stream.TryNext(ref item))
                    {
                        if (!evaluator.TryEvaluateStreamEntry(keyEntryAddress, itemSlot, ref item, null, ref key))
                        {
                            vmState.SetNothing(destinationRegister);
                            return;
                        }

                        var found = false;
                        for (var i = 0; i < count; i++)
                        {
                            if (!keys[i].EqualsValue(ref key)) continue;
                            found = true;
                            break;
                        }

                        if (found) continue;
                        if (count == values.Length)
                        {
                            var resizedValues = new GesVmValue[values.Length << 1];
                            var resizedKeys = new GesVmValue[keys.Length << 1];
                            Array.Copy(values, resizedValues, values.Length);
                            Array.Copy(keys, resizedKeys, keys.Length);
                            values = resizedValues;
                            keys = resizedKeys;
                        }

                        values[count] = item;
                        keys[count] = key;
                        count++;
                    }
                }
                finally
                {
                    if (stream is IDisposable disposable) disposable.Dispose();
                }

                var result = new GesVmValue[count];
                for (var i = 0; i < count; i++) result[i] = values[i];
                vmState.SetList(destinationRegister, result);
                return;
            }
            default:
                vmState.SetNothing(destinationRegister);
                return;
        }
    }
    internal static void GesVmGroupBy(this GesVmState vmState, ushort destinationRegister, in GesVmValue source, ushort itemSlot, ushort keyEntryAddress, IGesVmStreamEntryEvaluator evaluator)
    {
        var result = new GesVmValue();
        result.SetNothing();
        switch (source.Kind)
        {
            case List when source.ObjectValue is GesVmValue[] list:
            {
                var groups = new GesVmValueGroupBuilder(vmState, list.Length);
                var key = new GesVmValue();
                key.SetNothing();
                for (var i = 0; i < list.Length; i++)
                {
                    var item = list[i];
                    if (!evaluator.TryEvaluateStreamEntry(keyEntryAddress, itemSlot, ref item, null, ref key))
                    {
                        vmState.SetNothing(destinationRegister);
                        return;
                    }

                    var keyText = key.Kind is Text or Tag ? key.TextValue : key.ConvertToText();
                    groups.Add(keyText, item);
                }

                groups.WriteTo(ref result);
                vmState.SetValue(destinationRegister, in result);
                return;
            }
            case Map or Custom when source.ObjectValue is GesVmValueMap mapSource:
            {
                var values = mapSource.ValueList;
                var groups = new GesVmValueGroupBuilder(vmState, values.Length);
                var key = new GesVmValue();
                key.SetNothing();
                for (var i = 0; i < values.Length; i++)
                {
                    var item = values[i];
                    if (!evaluator.TryEvaluateStreamEntry(keyEntryAddress, itemSlot, ref item, null, ref key))
                    {
                        vmState.SetNothing(destinationRegister);
                        return;
                    }

                    var keyText = key.Kind is Text or Tag ? key.TextValue : key.ConvertToText();
                    groups.Add(keyText, item);
                }

                groups.WriteTo(ref result);
                vmState.SetValue(destinationRegister, in result);
                return;
            }
            case Stream when source.ObjectValue is IGesVmStream stream:
            {
                var groups = new GesVmValueGroupBuilder(vmState);
                var item = new GesVmValue();
                item.SetNothing();
                var key = new GesVmValue();
                key.SetNothing();
                try
                {
                    while (stream.TryNext(ref item))
                    {
                        if (!evaluator.TryEvaluateStreamEntry(keyEntryAddress, itemSlot, ref item, null, ref key))
                        {
                            vmState.SetNothing(destinationRegister);
                            return;
                        }

                        var keyText = key.Kind is Text or Tag ? key.TextValue : key.ConvertToText();
                        groups.Add(keyText, item);
                    }
                }
                finally
                {
                    if (stream is IDisposable disposable) disposable.Dispose();
                }

                groups.WriteTo(ref result);
                vmState.SetValue(destinationRegister, in result);
                return;
            }
            default:
                vmState.SetNothing(destinationRegister);
                return;
        }
    }
    internal static void GesVmSortAscending(this GesVmState vmState, ushort destinationRegister, in GesVmValue source)
    {
        var result = new GesVmValue();
        result.SetNothing();
        GesVmSort(vmState, ref result, in source, descending: false);
        vmState.SetValue(destinationRegister, in result);
    }
    internal static void GesVmSortDescending(this GesVmState vmState, ushort destinationRegister, in GesVmValue source)
    {
        var result = new GesVmValue();
        result.SetNothing();
        GesVmSort(vmState, ref result, in source, descending: true);
        vmState.SetValue(destinationRegister, in result);
    }
    internal static void GesVmOrderByAscending(this GesVmState vmState, ushort destinationRegister, in GesVmValue source, ushort itemSlot, ushort keyEntryAddress, IGesVmStreamEntryEvaluator evaluator)
    {
        GesVmOrderBy(vmState, destinationRegister, in source, itemSlot, keyEntryAddress, evaluator, descending: false);
    }
    internal static void GesVmOrderByDescending(this GesVmState vmState, ushort destinationRegister, in GesVmValue source, ushort itemSlot, ushort keyEntryAddress, IGesVmStreamEntryEvaluator evaluator)
    {
        GesVmOrderBy(vmState, destinationRegister, in source, itemSlot, keyEntryAddress, evaluator, descending: true);
    }

    private static void GesVmSort(GesVmState vmState, ref GesVmValue dst, in GesVmValue source, bool descending)
    {
        switch (source.Kind)
        {
            case List when source.ObjectValue is GesVmValue[] list:
            {
                if (list.Length == 0)
                {
                    dst.SetList(vmState.EmptyList);
                    return;
                }

                var result = new GesVmValue[list.Length];
                for (var i = 0; i < list.Length; i++) result[i] = list[i];
                if (!SortValues(result, list.Length, descending))
                {
                    dst.SetNothing();
                    return;
                }

                dst.SetList(result);
                return;
            }
            case Dice when source.ObjectValue is int[] dice:
            {
                var result = new GesVmValue[dice.Length];
                if (descending)
                {
                    for (var i = 0; i < dice.Length; i++) result[i].SetInteger(dice[i]);
                }
                else
                {
                    for (var i = 0; i < dice.Length; i++) result[i].SetInteger(dice[dice.Length - i - 1]);
                }

                dst.SetList(result);
                return;
            }
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesVmValueRangeInteger range:
            {
                var length = GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step);
                if (length == 0)
                {
                    dst.SetRange(0, 0, 0);
                    return;
                }

                var sourceDescending = range.Step < 0;
                if (sourceDescending == descending)
                {
                    dst.SetRange(range.From, range.To, range.Step);
                    return;
                }

                if (GameEventScriptRangeMath.TryGetTerm(range.From, range.To, range.Step, length, out var last))
                {
                    dst.SetRange(last, range.From, -range.Step);
                    return;
                }

                dst.SetNothing();
                return;
            }
            case GameEventScriptBytecodeTypeKind.Range when source.ObjectValue is GesVmValueRangeFloat range:
            {
                var length = GameEventScriptRangeMath.GetLength(range.From, range.To, range.Step);
                if (length == 0)
                {
                    dst.SetRange(0, 0, 0);
                    return;
                }

                var sourceDescending = range.Step < 0d;
                if (sourceDescending == descending)
                {
                    dst.SetRange(range.From, range.To, range.Step);
                    return;
                }

                if (GameEventScriptRangeMath.TryGetTerm(range.From, range.To, range.Step, length, out var last))
                {
                    dst.SetRange(last, range.From, -range.Step);
                    return;
                }

                dst.SetNothing();
                return;
            }
            case Stream when source.ObjectValue is IGesVmStream stream:
            {
                var item = new GesVmValue();
                item.SetNothing();
                var values = new GesVmValue[16];
                var count = 0;
                try
                {
                    while (stream.TryNext(ref item))
                    {
                        if (count == values.Length)
                        {
                            var resized = new GesVmValue[values.Length << 1];
                            Array.Copy(values, resized, values.Length);
                            values = resized;
                        }

                        values[count++] = item;
                    }
                }
                finally
                {
                    if (stream is IDisposable disposable) disposable.Dispose();
                }

                if (!SortValues(values, count, descending))
                {
                    dst.SetNothing();
                    return;
                }

                var result = new GesVmValue[count];
                for (var i = 0; i < count; i++) result[i] = values[i];
                dst.SetList(result);
                return;
            }
            default:
                dst.SetNothing();
                return;
        }
    }

    private static void GesVmOrderBy(GesVmState vmState, ushort destinationRegister, in GesVmValue source, ushort itemSlot, ushort keyEntryAddress, IGesVmStreamEntryEvaluator evaluator, bool descending)
    {
        switch (source.Kind)
        {
            case List when source.ObjectValue is GesVmValue[] list:
            {
                if (list.Length == 0)
                {
                    vmState.SetList(destinationRegister, vmState.EmptyList);
                    return;
                }

                var values = new GesVmValue[list.Length];
                var keys = new GesVmValue[list.Length];
                var key = new GesVmValue();
                key.SetNothing();
                for (var i = 0; i < list.Length; i++)
                {
                    var item = list[i];
                    if (!evaluator.TryEvaluateStreamEntry(keyEntryAddress, itemSlot, ref item, null, ref key))
                    {
                        vmState.SetNothing(destinationRegister);
                        return;
                    }

                    values[i] = item;
                    keys[i] = key;
                }

                if (!SortValuesByKeys(values, keys, list.Length, descending))
                {
                    vmState.SetNothing(destinationRegister);
                    return;
                }

                var result = new GesVmValue[list.Length];
                for (var i = 0; i < list.Length; i++) result[i] = values[i];
                vmState.SetList(destinationRegister, result);
                return;
            }
            case Stream when source.ObjectValue is IGesVmStream stream:
            {
                var item = new GesVmValue();
                item.SetNothing();
                var key = new GesVmValue();
                key.SetNothing();
                var values = new GesVmValue[16];
                var keys = new GesVmValue[16];
                var count = 0;
                try
                {
                    while (stream.TryNext(ref item))
                    {
                        if (count == values.Length)
                        {
                            var resizedValues = new GesVmValue[values.Length << 1];
                            var resizedKeys = new GesVmValue[keys.Length << 1];
                            Array.Copy(values, resizedValues, values.Length);
                            Array.Copy(keys, resizedKeys, keys.Length);
                            values = resizedValues;
                            keys = resizedKeys;
                        }

                        if (!evaluator.TryEvaluateStreamEntry(keyEntryAddress, itemSlot, ref item, null, ref key))
                        {
                            vmState.SetNothing(destinationRegister);
                            return;
                        }

                        values[count] = item;
                        keys[count] = key;
                        count++;
                    }
                }
                finally
                {
                    if (stream is IDisposable disposable) disposable.Dispose();
                }

                if (!SortValuesByKeys(values, keys, count, descending))
                {
                    vmState.SetNothing(destinationRegister);
                    return;
                }

                var result = new GesVmValue[count];
                for (var i = 0; i < count; i++) result[i] = values[i];
                vmState.SetList(destinationRegister, result);
                return;
            }
            default:
                vmState.SetNothing(destinationRegister);
                return;
        }
    }

    private static bool SortValues(GesVmValue[] values, int count, bool descending)
    {
        for (var i = 1; i < count; i++)
        {
            var value = values[i];
            var j = i - 1;
            while (j >= 0)
            {
                if (!TryCompareAscending(ref values[j], ref value, out var comparison)) return false;
                if ((descending ? -comparison : comparison) <= 0) break;
                values[j + 1] = values[j];
                j--;
            }

            values[j + 1] = value;
        }

        return true;
    }

    private static bool SortValuesByKeys(GesVmValue[] values, GesVmValue[] keys, int count, bool descending)
    {
        for (var i = 1; i < count; i++)
        {
            var value = values[i];
            var key = keys[i];
            var j = i - 1;
            while (j >= 0)
            {
                if (!TryCompareAscending(ref keys[j], ref key, out var comparison)) return false;
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

    private static bool TryCompareAscending(ref GesVmValue a, ref GesVmValue b, out int comparison)
    {
        comparison = 0;
        if (a.Kind is Nothing || b.Kind is Nothing)
        {
            return false;
        }

        if (a.IsNumeric && b.IsNumeric)
        {
            if (a.Unit != b.Unit)
            {
                return false;
            }

            var left = a.AsNumeric;
            var right = b.AsNumeric;
            if (double.IsNaN(left) || double.IsNaN(right))
            {
                return false;
            }

            comparison = left.CompareTo(right);
            return true;
        }

        var leftRank = GetStableRank(ref a);
        var rightRank = GetStableRank(ref b);
        if (leftRank != rightRank)
        {
            comparison = leftRank.CompareTo(rightRank);
            return true;
        }

        switch (a.Kind)
        {
            case Text when b.Kind is Text:
            case Tag when b.Kind is Tag:
                comparison = StringComparer.Ordinal.Compare(a.TextValue, b.TextValue);
                return true;
            case Vector when b.Kind is Vector:
            case Point when b.Kind is Point:
                if (a.Unit != b.Unit ||
                    a.ObjectValue is not GesVmValueVectorPoint leftTriplet ||
                    b.ObjectValue is not GesVmValueVectorPoint rightTriplet)
                {
                    return false;
                }

                comparison = leftTriplet.X.CompareTo(rightTriplet.X);
                if (comparison == 0) comparison = leftTriplet.Y.CompareTo(rightTriplet.Y);
                if (comparison == 0) comparison = leftTriplet.Z.CompareTo(rightTriplet.Z);
                return true;
            case GameEventScriptBytecodeTypeKind.Boolean when b.Kind is GameEventScriptBytecodeTypeKind.Boolean:
                comparison = a.IsTrue.CompareTo(b.IsTrue);
                return true;
            default:
                comparison = 0;
                return true;
        }
    }
    private static int GetStableRank(ref GesVmValue value)
    {
        switch (value.Kind)
        {
            case Integer:
            case Float:
            case Percentage:
                return 1;
            case Tag:
            {
                var text = value.TextValue;
                return IsNumericTag(text) ? 1 : 3;
            }
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

internal sealed class GesVmValueGroupBuilder
{
    private readonly GesVmState _state;
    private string[] _keys;
    private GesVmValue[][] _buckets;
    private int[] _counts;
    private int _count;

    internal GesVmValueGroupBuilder(GesVmState state, int capacity = 0)
    {
        _state = state;
        var size = capacity <= 0 ? 4 : capacity;
        _keys = new string[size];
        _buckets = new GesVmValue[size][];
        _counts = new int[size];
    }

    internal void Add(string key, GesVmValue value)
    {
        var groupIndex = -1;
        for (var i = 0; i < _count; i++)
        {
            if (!string.Equals(_keys[i], key, StringComparison.Ordinal)) continue;
            groupIndex = i;
            break;
        }

        if (groupIndex < 0)
        {
            if (_count == _keys.Length)
            {
                var nextSize = _keys.Length << 1;
                var nextKeys = new string[nextSize];
                var nextBuckets = new GesVmValue[nextSize][];
                var nextCounts = new int[nextSize];
                Array.Copy(_keys, nextKeys, _keys.Length);
                Array.Copy(_buckets, nextBuckets, _buckets.Length);
                Array.Copy(_counts, nextCounts, _counts.Length);
                _keys = nextKeys;
                _buckets = nextBuckets;
                _counts = nextCounts;
            }

            groupIndex = _count++;
            _keys[groupIndex] = key;
            _buckets[groupIndex] = new GesVmValue[4];
        }

        var bucket = _buckets[groupIndex];
        var itemCount = _counts[groupIndex];
        if (itemCount == bucket.Length)
        {
            var resized = new GesVmValue[bucket.Length << 1];
            Array.Copy(bucket, resized, bucket.Length);
            bucket = resized;
            _buckets[groupIndex] = bucket;
        }

        bucket[itemCount] = value;
        _counts[groupIndex] = itemCount + 1;
    }

    internal void WriteTo(ref GesVmValue destination)
    {
        var map = new GesVmValueMapBuilder(_count);
        for (var groupIndex = 0; groupIndex < _count; groupIndex++)
        {
            var itemCount = _counts[groupIndex];
            var groupedList = new GesVmValue[itemCount];
            for (var i = 0; i < itemCount; i++) groupedList[i] = _buckets[groupIndex][i];
            var groupedValue = new GesVmValue();
            groupedValue.SetNothing();
            groupedValue.SetList(groupedList);
            map.Set(_keys[groupIndex], groupedValue);
        }

        destination.SetMap(map.ToMap());
    }
}
