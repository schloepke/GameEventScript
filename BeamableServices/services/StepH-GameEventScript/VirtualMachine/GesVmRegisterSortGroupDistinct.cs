using System;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.VirtualMachine;

internal static class GesVmRegisterSortGroupDistinct
{
    internal static void GesVmDistinct(this GesVmState vmState, ushort destinationRegister, in GesVmValue source)
    {
        var resultValue = new GesVmValue();
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
            case Iterator when source.ObjectValue is IGesVmIterator iterator:
            {
                var item = new GesVmValue();
                var values = new GesVmValue[16];
                var count = 0;
                try
                {
                    while (iterator.TryNext(ref item))
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
                    if (iterator is IDisposable disposable) disposable.Dispose();
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
    internal static void GesVmSortAscending(this GesVmState vmState, ushort destinationRegister, in GesVmValue source)
    {
        var result = new GesVmValue();
        GesVmSort(vmState, ref result, in source, descending: false);
        vmState.SetValue(destinationRegister, in result);
    }
    internal static void GesVmSortDescending(this GesVmState vmState, ushort destinationRegister, in GesVmValue source)
    {
        var result = new GesVmValue();
        GesVmSort(vmState, ref result, in source, descending: true);
        vmState.SetValue(destinationRegister, in result);
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
            case Iterator when source.ObjectValue is IGesVmIterator iterator:
            {
                var item = new GesVmValue();
                var values = new GesVmValue[16];
                var count = 0;
                try
                {
                    while (iterator.TryNext(ref item))
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
                    if (iterator is IDisposable disposable) disposable.Dispose();
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

    internal static bool SortValuesByKeys(GesVmValue[] values, GesVmValue[] keys, int count, bool descending)
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
            groupedValue.SetList(groupedList);
            map.Set(_keys[groupIndex], groupedValue);
        }

        destination.SetMap(map.ToMap());
    }
}

internal sealed class GesVmValueDistinctBuilder
{
    private GesVmValue[] _keys;
    private GesVmValue[] _values;
    private int _count;

    internal GesVmValueDistinctBuilder(int capacity = 0)
    {
        var size = capacity <= 0 ? 4 : capacity;
        _keys = new GesVmValue[size];
        _values = new GesVmValue[size];
    }

    internal void Add(in GesVmValue key, in GesVmValue value)
    {
        for (var i = 0; i < _count; i++)
        {
            var existing = _keys[i];
            var candidate = key;
            if (existing.EqualsValue(ref candidate)) return;
        }

        if (_count == _keys.Length)
        {
            var nextSize = _keys.Length << 1;
            var nextKeys = new GesVmValue[nextSize];
            var nextValues = new GesVmValue[nextSize];
            Array.Copy(_keys, nextKeys, _keys.Length);
            Array.Copy(_values, nextValues, _values.Length);
            _keys = nextKeys;
            _values = nextValues;
        }

        _keys[_count] = key;
        _values[_count] = value;
        _count++;
    }

    internal GesVmValue[] ToList()
    {
        if (_count == 0) return [];
        var result = new GesVmValue[_count];
        for (var i = 0; i < _count; i++) result[i] = _values[i];
        return result;
    }
}

internal sealed class GesVmValueOrderBuilder
{
    private GesVmValue[] _keys;
    private GesVmValue[] _values;
    private int _count;

    internal GesVmValueOrderBuilder(int capacity = 0)
    {
        var size = capacity <= 0 ? 4 : capacity;
        _keys = new GesVmValue[size];
        _values = new GesVmValue[size];
    }

    internal void Add(in GesVmValue key, in GesVmValue value)
    {
        if (_count == _keys.Length)
        {
            var nextSize = _keys.Length << 1;
            var nextKeys = new GesVmValue[nextSize];
            var nextValues = new GesVmValue[nextSize];
            Array.Copy(_keys, nextKeys, _keys.Length);
            Array.Copy(_values, nextValues, _values.Length);
            _keys = nextKeys;
            _values = nextValues;
        }

        _keys[_count] = key;
        _values[_count] = value;
        _count++;
    }

    internal bool TryToList(bool descending, out GesVmValue[] result)
    {
        if (_count == 0)
        {
            result = [];
            return true;
        }

        var keys = new GesVmValue[_count];
        var values = new GesVmValue[_count];
        for (var i = 0; i < _count; i++)
        {
            keys[i] = _keys[i];
            values[i] = _values[i];
        }

        if (!GesVmRegisterSortGroupDistinct.SortValuesByKeys(values, keys, _count, descending))
        {
            result = [];
            return false;
        }

        result = values;
        return true;
    }
}
