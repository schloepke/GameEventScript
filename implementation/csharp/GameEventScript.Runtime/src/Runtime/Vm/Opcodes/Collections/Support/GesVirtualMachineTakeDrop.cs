// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using GameEventScript.Syntax;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private static GesValue TakeFirstDice(GesVirtualMachine vm, int[] source, short count)
    {
        var dst = new GesValue();
        if (count <= 0)
        {
            dst.SetDice([]);
            return dst;
        }

        var length = count < source.Length ? count : source.Length;
        var dice = new int[length];
        for (var i = 0; i < length; i++) dice[i] = source[i];
        dst.SetDice(dice);
        return dst;
    }

    private static GesValue DropFirstDice(int[] source, short count)
    {
        var dst = new GesValue();
        if (count <= 0)
        {
            dst.SetDice(source);
            return dst;
        }

        var start = count < source.Length ? count : source.Length;
        var length = source.Length - start;
        var dice = new int[length];
        for (var i = 0; i < length; i++) dice[i] = source[start + i];
        dst.SetDice(dice);
        return dst;
    }

    private static GesValue TakeLastDice(int[] source, short count)
    {
        var dst = new GesValue();
        if (count <= 0)
        {
            dst.SetDice([]);
            return dst;
        }

        var length = count < source.Length ? count : source.Length;
        var start = source.Length - length;
        var dice = new int[length];
        for (var i = 0; i < length; i++) dice[i] = source[start + i];
        dst.SetDice(dice);
        return dst;
    }

    private static GesValue DropLastDice(int[] source, short count)
    {
        var dst = new GesValue();
        if (count <= 0)
        {
            dst.SetDice(source);
            return dst;
        }

        var length = count < source.Length ? source.Length - count : 0;
        var dice = new int[length];
        for (var i = 0; i < length; i++) dice[i] = source[i];
        dst.SetDice(dice);
        return dst;
    }

    private static GesValue TakeExtremeList(GesVirtualMachine vm, GesValue[] source, short count, bool highest)
    {
        var dst = new GesValue();
        if (count <= 0 || source.Length == 0)
        {
            dst.SetList(vm.EmptyList);
            return dst;
        }

        var length = count < source.Length ? count : source.Length;
        var selected = new bool[source.Length];
        var list = new GesValue[length];
        for (var i = 0; i < length; i++)
        {
            var best = -1;
            for (var j = 0; j < source.Length; j++)
            {
                if (selected[j]) continue;
                if (best < 0)
                {
                    best = j;
                    continue;
                }

                var comparison = CompareForOrdering(in source[j], in source[best]);
                if (highest ? comparison > 0 : comparison < 0) best = j;
            }

            selected[best] = true;
            list[i] = source[best];
        }

        dst.SetList(list);
        return dst;
    }

    private static GesValue DropExtremeList(GesVirtualMachine vm, GesValue[] source, short count, bool highest)
    {
        var dst = new GesValue();
        if (count <= 0)
        {
            dst.SetList(source);
            return dst;
        }

        var selectedCount = count < source.Length ? count : source.Length;
        if (selectedCount == source.Length)
        {
            dst.SetList(vm.EmptyList);
            return dst;
        }

        var selected = new bool[source.Length];
        SelectExtremeItems(source, source.Length, selected, selectedCount, highest);
        var list = new GesValue[source.Length - selectedCount];
        var target = 0;
        for (var i = 0; i < source.Length; i++)
        {
            if (selected[i]) continue;
            list[target++] = source[i];
        }

        dst.SetList(list);
        return dst;
    }

    private static void SelectExtremeItems(GesValue[] items, int length, bool[] selected, int selectedCount, bool highest)
    {
        for (var i = 0; i < selectedCount; i++)
        {
            var best = -1;
            for (var j = 0; j < length; j++)
            {
                if (selected[j]) continue;
                if (best < 0)
                {
                    best = j;
                    continue;
                }

                var comparison = CompareForOrdering(in items[j], in items[best]);
                if (highest ? comparison > 0 : comparison < 0) best = j;
            }

            selected[best] = true;
        }
    }

    private static GesValue TakeExtremeIterator(GesVirtualMachine vm, IGesIterator iterator, short count, bool highest)
    {
        var dst = new GesValue();
        if (count <= 0)
        {
            if (iterator is IDisposable disposable) disposable.Dispose();
            dst.SetList(vm.EmptyList);
            return dst;
        }

        var buffer = new GesValue[count];
        var itemCount = 0;
        try
        {
            GesIteratorResult item;
            while ((item = iterator.Next()).HasValue)
            {
                if (itemCount < count)
                {
                    itemCount = InsertExtreme(buffer, in item.Value, itemCount, highest);
                    continue;
                }

                var comparison = CompareForOrdering(in item.Value, in buffer[count - 1]);
                if (highest ? comparison > 0 : comparison < 0)
                {
                    itemCount--;
                    itemCount = InsertExtreme(buffer, in item.Value, itemCount, highest);
                }
            }

            return ListFromBuffer(vm, buffer, itemCount);
        }
        finally
        {
            if (iterator is IDisposable disposable) disposable.Dispose();
        }
    }

    private static GesValue DropExtremeIterator(GesVirtualMachine vm, IGesIterator iterator, short count, bool highest)
    {
        var dst = new GesValue();
        var buffer = new GesVmListBuilder(16);
        try
        {
            GesIteratorResult item;
            while ((item = iterator.Next()).HasValue)
            {
                buffer.Add(in item.Value);
            }

            if (count <= 0)
            {
                return ListFromBuffer(vm, buffer.Items, buffer.Count);
            }

            var selectedCount = count < buffer.Count ? count : buffer.Count;
            if (selectedCount == buffer.Count)
            {
                dst.SetList(vm.EmptyList);
                return dst;
            }

            var selected = new bool[buffer.Count];
            SelectExtremeItems(buffer.Items, buffer.Count, selected, selectedCount, highest);
            var list = new GesValue[buffer.Count - selectedCount];
            var target = 0;
            for (var i = 0; i < buffer.Count; i++)
            {
                if (selected[i]) continue;
                list[target++] = buffer[i];
            }

            dst.SetList(list);
            return dst;
        }
        finally
        {
            if (iterator is IDisposable disposable) disposable.Dispose();
        }
    }

    private static int InsertExtreme(GesValue[] buffer, in GesValue item, int itemCount, bool highest)
    {
        var index = itemCount;
        while (index > 0)
        {
            var comparison = CompareForOrdering(in item, in buffer[index - 1]);
            if (highest ? comparison <= 0 : comparison >= 0) break;
            buffer[index] = buffer[index - 1];
            index--;
        }

        buffer[index] = item;
        return itemCount + 1;
    }

    private static GesValue ListFromBuffer(GesVirtualMachine vm, GesValue[] buffer, int length)
    {
        var dst = new GesValue();
        if (length == 0)
        {
            dst.SetList(vm.EmptyList);
            return dst;
        }

        var list = new GesValue[length];
        Array.Copy(buffer, list, length);
        dst.SetList(list);
        return dst;
    }

    private static GesValue DropFirstRange(in GesValue source, GesIntegerRange valueRangeInteger, short count)
    {
        var dst = new GesValue();
        var length = GesRangeMath.GetLength(valueRangeInteger.From, valueRangeInteger.To, valueRangeInteger.Step);
        if (count <= 0)
        {
            return source;
        }

        if (count >= length)
        {
            dst.SetIntegerRange(0, 0, 0);
            return dst;
        }

        if (GesRangeMath.GetTerm(valueRangeInteger.From, valueRangeInteger.To, valueRangeInteger.Step, count + 1L) is { } from) dst.SetIntegerRange(from, valueRangeInteger.To, valueRangeInteger.Step);
        else dst.SetNothing();
        return dst;
    }

    private static GesValue DropLastRange(in GesValue source, GesIntegerRange valueRangeInteger, short count)
    {
        var dst = new GesValue();
        var length = GesRangeMath.GetLength(valueRangeInteger.From, valueRangeInteger.To, valueRangeInteger.Step);
        if (count <= 0)
        {
            return source;
        }

        if (count >= length)
        {
            dst.SetIntegerRange(0, 0, 0);
            return dst;
        }

        if (GesRangeMath.GetTerm(valueRangeInteger.From, valueRangeInteger.To, valueRangeInteger.Step, length - count) is { } to) dst.SetIntegerRange(valueRangeInteger.From, to, valueRangeInteger.Step);
        else dst.SetNothing();
        return dst;
    }

    private static GesValue DropFirstRange(in GesValue source, GesFloatRange range, short count)
    {
        var dst = new GesValue();
        var length = GesRangeMath.GetLength(range.From, range.To, range.Step);
        if (count <= 0)
        {
            return source;
        }

        if (count >= length)
        {
            dst.SetIntegerRange(0, 0, 0);
            return dst;
        }

        if (GesRangeMath.GetTerm(range.From, range.To, range.Step, count + 1L) is { } from) dst.SetFloatRange(from, range.To, range.Step);
        else dst.SetNothing();
        return dst;
    }

    private static GesValue DropLastRange(in GesValue source, GesFloatRange range, short count)
    {
        var dst = new GesValue();
        var length = GesRangeMath.GetLength(range.From, range.To, range.Step);
        if (count <= 0)
        {
            return source;
        }

        if (count >= length)
        {
            dst.SetIntegerRange(0, 0, 0);
            return dst;
        }

        if (GesRangeMath.GetTerm(range.From, range.To, range.Step, length - count) is { } to) dst.SetFloatRange(range.From, to, range.Step);
        else dst.SetNothing();
        return dst;
    }

    private static GesValue TakeExtremeRange(GesVirtualMachine vm, GesIntegerRange valueRangeInteger, short count, bool highest)
    {
        var dst = new GesValue();
        var length = GesRangeMath.GetLength(valueRangeInteger.From, valueRangeInteger.To, valueRangeInteger.Step);
        if (count <= 0 || length <= 0 || valueRangeInteger.Step == 0)
        {
            dst.SetIntegerRange(0, 0, 0);
            return dst;
        }

        var takeLength = count < length ? count : length;
        if (GesRangeMath.GetTerm(valueRangeInteger.From, valueRangeInteger.To, valueRangeInteger.Step, length) is not { } last)
        {
            dst.SetNothing();
            return dst;
        }

        var ascending = valueRangeInteger.Step > 0;
        if (highest)
        {
            if (ascending)
            {
                if (GesRangeMath.GetTerm(last, valueRangeInteger.From, -valueRangeInteger.Step, takeLength) is { } to) dst.SetIntegerRange(last, to, -valueRangeInteger.Step);
                else dst.SetNothing();
            }
            else
            {
                if (GesRangeMath.GetTerm(valueRangeInteger.From, valueRangeInteger.To, valueRangeInteger.Step, takeLength) is { } to) dst.SetIntegerRange(valueRangeInteger.From, to, valueRangeInteger.Step);
                else dst.SetNothing();
            }
        }
        else
        {
            if (ascending)
            {
                if (GesRangeMath.GetTerm(valueRangeInteger.From, valueRangeInteger.To, valueRangeInteger.Step, takeLength) is { } to) dst.SetIntegerRange(valueRangeInteger.From, to, valueRangeInteger.Step);
                else dst.SetNothing();
            }
            else
            {
                if (GesRangeMath.GetTerm(last, valueRangeInteger.From, -valueRangeInteger.Step, takeLength) is { } to) dst.SetIntegerRange(last, to, -valueRangeInteger.Step);
                else dst.SetNothing();
            }
        }
        return dst;
    }

    private static GesValue DropExtremeRange(in GesValue source, GesIntegerRange valueRangeInteger, short count, bool highest)
    {
        if (valueRangeInteger.Step > 0)
        {
            return highest
                ? DropLastRange(in source, valueRangeInteger, count)
                : DropFirstRange(in source, valueRangeInteger, count);
        }

        return highest
            ? DropFirstRange(in source, valueRangeInteger, count)
            : DropLastRange(in source, valueRangeInteger, count);
    }

    private static GesValue TakeExtremeRange(GesVirtualMachine vm, GesFloatRange range, short count, bool highest)
    {
        var dst = new GesValue();
        var length = GesRangeMath.GetLength(range.From, range.To, range.Step);
        if (count <= 0 || length <= 0 || range.Step == 0d)
        {
            dst.SetIntegerRange(0, 0, 0);
            return dst;
        }

        var takeLength = count < length ? count : length;
        if (GesRangeMath.GetTerm(range.From, range.To, range.Step, length) is not { } last)
        {
            dst.SetNothing();
            return dst;
        }

        var ascending = range.Step > 0d;
        if (highest)
        {
            if (ascending)
            {
                if (GesRangeMath.GetTerm(last, range.From, -range.Step, takeLength) is { } to) dst.SetFloatRange(last, to, -range.Step);
                else dst.SetNothing();
            }
            else
            {
                if (GesRangeMath.GetTerm(range.From, range.To, range.Step, takeLength) is { } to) dst.SetFloatRange(range.From, to, range.Step);
                else dst.SetNothing();
            }
        }
        else
        {
            if (ascending)
            {
                if (GesRangeMath.GetTerm(range.From, range.To, range.Step, takeLength) is { } to) dst.SetFloatRange(range.From, to, range.Step);
                else dst.SetNothing();
            }
            else
            {
                if (GesRangeMath.GetTerm(last, range.From, -range.Step, takeLength) is { } to) dst.SetFloatRange(last, to, -range.Step);
                else dst.SetNothing();
            }
        }
        return dst;
    }

    private static GesValue DropExtremeRange(in GesValue source, GesFloatRange range, short count, bool highest)
    {
        if (range.Step > 0d)
        {
            return highest
                ? DropLastRange(in source, range, count)
                : DropFirstRange(in source, range, count);
        }

        return highest
            ? DropFirstRange(in source, range, count)
            : DropLastRange(in source, range, count);
    }

    private static int CompareForOrdering(in GesValue left, in GesValue right)
    {
        if (left.IsNumeric && right.IsNumeric)
        {
            var unitComparison = left.Unit.CompareTo(right.Unit);
            if (unitComparison != 0) return unitComparison;
            return left.AsNumeric.CompareTo(right.AsNumeric);
        }

        var rankComparison = GetOrderingRank(in left).CompareTo(GetOrderingRank(in right));
        if (rankComparison != 0) return rankComparison;

        switch (left.Kind)
        {
            case Text when right.Kind is Text:
            case Tag when right.Kind is Tag:
                return GesText.CompareScalarOrdinal(left.TextValue, right.TextValue);
            case Vector when right.Kind is Vector:
            case Point when right.Kind is Point:
                if (left.Unit != right.Unit) return left.Unit.CompareTo(right.Unit);
                if (left.ObjectValue is GesSpatialValue leftTriplet && right.ObjectValue is GesSpatialValue rightTriplet)
                {
                    var comparison = leftTriplet.X.CompareTo(rightTriplet.X);
                    if (comparison != 0) return comparison;
                    comparison = leftTriplet.Y.CompareTo(rightTriplet.Y);
                    return comparison != 0 ? comparison : leftTriplet.Z.CompareTo(rightTriplet.Z);
                }
                return 0;
            case GesBytecodeTypeKind.Boolean when right.Kind is GesBytecodeTypeKind.Boolean:
                return left.IsTrue.CompareTo(right.IsTrue);
            case Dice when right.Kind is Dice && left.ObjectValue is int[] leftDice && right.ObjectValue is int[] rightDice:
            {
                var countComparison = leftDice.Length.CompareTo(rightDice.Length);
                if (countComparison != 0) return countComparison;
                for (var i = 0; i < leftDice.Length; i++)
                {
                    var comparison = leftDice[i].CompareTo(rightDice[i]);
                    if (comparison != 0) return comparison;
                }

                return 0;
            }
            default:
                return left.Kind.CompareTo(right.Kind);
        }
    }

    private static int GetOrderingRank(in GesValue value)
    {
        if (value.IsNumeric) return 1;
        return value.Kind switch
        {
            Nothing => 0,
            Tag => 1,
            Text => 2,
            Percentage => 3,
            Vector => 4,
            Point => 5,
            GesBytecodeTypeKind.Boolean => 6,
            Series => 7,
            GesBytecodeTypeKind.Range => 8,
            Message => 9,
            Handler => 10,
            List => 11,
            Map or Custom => 12,
            Dice => 13,
            _ => 8
        };
    }
}
