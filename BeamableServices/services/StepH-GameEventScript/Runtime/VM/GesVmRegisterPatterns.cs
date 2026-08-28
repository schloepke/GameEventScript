using System;
using StepH.GameEventScript.Api;
using static StepH.GameEventScript.Api.GameEventScriptBytecodePatternKind;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;
using StepH.GameEventScript.Runtime.Values;

namespace StepH.GameEventScript.Runtime.VM;

internal static class GesVmRegisterPatterns
{
    internal static void GesVmHasPattern(this GesVmState vmState, ushort destinationRegister, in GesValue source, GameEventScriptBytecodePatternKind pattern, short count, in GesValue face)
    {
        if (pattern == CountFace)
        {
            switch (source.Kind)
            {
                case Dice when source.ObjectValue is int[] dice:
                    vmState.SetBoolean(destinationRegister, HasPatternDiceFace(dice, in face, count));
                    return;
                case List when source.ObjectValue is GesValue[] list:
                    vmState.SetBoolean(destinationRegister, HasPatternListFace(list, in face, count));
                    return;
                case Iterator when source.ObjectValue is IGesIterator iterator:
                    if (!iterator.IsPatternSequence)
                    {
                        vmState.SetBoolean(destinationRegister, false);
                        return;
                    }
                    var read = ReadIterator(iterator);
                    vmState.SetBoolean(destinationRegister, HasPatternBufferFace(read.Items, read.Length, in face, count));
                    return;
                case Series:
                    vmState.SetNothing(destinationRegister);
                    return;
                default:
                    vmState.SetBoolean(destinationRegister, false);
                    return;
            }
        }

        switch (source.Kind)
        {
            case Dice when source.ObjectValue is int[] dice:
                vmState.SetBoolean(destinationRegister, HasPatternDice(dice, pattern, count));
                return;
            case List when source.ObjectValue is GesValue[] list:
                vmState.SetBoolean(destinationRegister, HasPatternList(list, pattern, count));
                return;
            case Iterator when source.ObjectValue is IGesIterator iterator:
                if (!iterator.IsPatternSequence)
                {
                    vmState.SetBoolean(destinationRegister, false);
                    return;
                }
                var read = ReadIterator(iterator);
                vmState.SetBoolean(destinationRegister, HasPatternBuffer(read.Items, read.Length, pattern, count));
                return;
            case Series:
                vmState.SetNothing(destinationRegister);
                return;
            default:
                vmState.SetBoolean(destinationRegister, false);
                return;
        }
    }

    internal static void GesVmTakePattern(this GesVmState vmState, ushort destinationRegister, in GesValue source, GameEventScriptBytecodePatternKind pattern, short count, in GesValue face)
    {
        if (pattern == CountFace)
        {
            switch (source.Kind)
            {
                case Dice when source.ObjectValue is int[] dice:
                    var buffer = new GesValue[dice.Length];
                    for (var i = 0; i < dice.Length; i++) buffer[i].SetInteger(dice[i]);
                    var found = 0;
                    for (var i = 0; i < buffer.Length; i++) if (buffer[i].EqualsValue(in face)) found++;
                    var result = found < count
                        ? new GesValue()
                        : SetTakenByFace(buffer, buffer.Length, in face, count, diceResult: true);
                    vmState.SetValue(destinationRegister, in result);
                    return;
                case List when source.ObjectValue is GesValue[] list:
                    var matches = 0;
                    for (var i = 0; i < list.Length; i++) if (list[i].EqualsValue(in face)) matches++;
                    result = matches < count
                        ? new GesValue()
                        : SetTakenByFace(list, list.Length, in face, count, diceResult: false);
                    vmState.SetValue(destinationRegister, in result);
                    return;
                case Iterator when source.ObjectValue is IGesIterator iterator:
                    if (!iterator.IsPatternSequence)
                    {
                        vmState.SetNothing(destinationRegister);
                        return;
                    }
                    var read = ReadIterator(iterator);
                    var iteratorMatches = 0;
                    for (var i = 0; i < read.Length; i++) if (read.Items[i].EqualsValue(in face)) iteratorMatches++;
                    result = iteratorMatches < count
                        ? new GesValue()
                        : SetTakenByFace(read.Items, read.Length, in face, count, diceResult: false);
                    vmState.SetValue(destinationRegister, in result);
                    return;
                default:
                    vmState.SetNothing(destinationRegister);
                    return;
            }
        }

        switch (source.Kind)
        {
            case Dice when source.ObjectValue is int[] dice:
                var result = TakePatternDice(dice, pattern, count);
                vmState.SetValue(destinationRegister, in result);
                return;
            case List when source.ObjectValue is GesValue[] list:
                result = TakePatternList(list, pattern, count);
                vmState.SetValue(destinationRegister, in result);
                return;
            case Iterator when source.ObjectValue is IGesIterator iterator:
                if (!iterator.IsPatternSequence)
                {
                    vmState.SetNothing(destinationRegister);
                    return;
                }
                var read = ReadIterator(iterator);
                result = TakePatternBuffer(read.Items, read.Length, pattern, count, diceResult: false);
                vmState.SetValue(destinationRegister, in result);
                return;
            default:
                vmState.SetNothing(destinationRegister);
                return;
        }
    }

    private static bool HasPatternDice(int[] dice, GameEventScriptBytecodePatternKind pattern, short count)
    {
        switch (pattern)
        {
            case CountAny:
                for (var i = 0; i < dice.Length; i++)
                {
                    var c = 1;
                    for (var j = i + 1; j < dice.Length; j++) if (dice[j] == dice[i]) c++;
                    if (c >= count)
                    {
                        return true;
                    }
                }

                return false;
            case FullHouse:
                return IsFullHouseDice(dice);
            case Straight:
                return IsStraightDice(dice);
            default:
                return false;
        }
    }

    private static bool HasPatternDiceFace(int[] dice, in GesValue face, short count)
    {
        var faceNumber = face.AsNumeric;
        if (!double.IsFinite(faceNumber)) return false;

        var faceInteger = (long)faceNumber;
        var matches = 0;
        for (var i = 0; i < dice.Length; i++) if (dice[i] == faceInteger) matches++;
        return matches >= count;
    }

    private static bool HasPatternList(GesValue[] list, GameEventScriptBytecodePatternKind pattern, short count)
    {
        switch (pattern)
        {
            case CountAny:
                for (var i = 0; i < list.Length; i++)
                {
                    var c = 1;
                    for (var j = i + 1; j < list.Length; j++) if (list[i].EqualsValue(in list[j])) c++;
                    if (c >= count)
                    {
                        return true;
                    }
                }

                return false;
            case FullHouse:
                return IsFullHouseList(list);
            case Straight:
                return IsStraightList(list);
            default:
                return false;
        }
    }

    private static bool HasPatternListFace(GesValue[] list, in GesValue face, short count)
    {
        var matches = 0;
        for (var i = 0; i < list.Length; i++) if (list[i].EqualsValue(in face)) matches++;
        return matches >= count;
    }

    private static bool HasPatternBuffer(GesValue[] items, int length, GameEventScriptBytecodePatternKind pattern, short count)
    {
        var list = new GesValue[length];
        for (var i = 0; i < length; i++) list[i] = items[i];
        return HasPatternList(list, pattern, count);
    }

    private static bool HasPatternBufferFace(GesValue[] items, int length, in GesValue face, short count)
    {
        var matches = 0;
        for (var i = 0; i < length; i++) if (items[i].EqualsValue(in face)) matches++;
        return matches >= count;
    }

    private static GesValue TakePatternDice(int[] dice, GameEventScriptBytecodePatternKind pattern, short count)
    {
        var buffer = new GesValue[dice.Length];
        for (var i = 0; i < dice.Length; i++) buffer[i].SetInteger(dice[i]);
        return TakePatternBuffer(buffer, dice.Length, pattern, count, diceResult: true);
    }

    private static GesValue TakePatternList(GesValue[] list, GameEventScriptBytecodePatternKind pattern, short count)
    {
        return TakePatternBuffer(list, list.Length, pattern, count, diceResult: false);
    }

    private static GesValue TakePatternBuffer(GesValue[] items, int length, GameEventScriptBytecodePatternKind pattern, short count, bool diceResult)
    {
        switch (pattern)
        {
            case CountAny:
                for (var i = 0; i < length; i++)
                {
                    var found = 1;
                    for (var j = i + 1; j < length; j++) if (items[i].EqualsValue(in items[j])) found++;
                    if (found >= count)
                    {
                        return SetTakenByFace(items, length, in items[i], count, diceResult);
                    }
                }

                return new GesValue();
            case FullHouse:
                return TakeFullHouse(items, length, diceResult);
            case Straight:
                return TakeStraight(items, length, diceResult);
            default:
                return new GesValue();
        }
    }

    private static GesValue SetTakenByFace(GesValue[] items, int length, in GesValue face, int count, bool diceResult)
    {
        var result = new GesValue();
        if (diceResult)
        {
            var dice = new int[count];
            var index = 0;
            for (var i = 0; i < length && index < count; i++)
            {
                if (!items[i].EqualsValue(in face)) continue;
                dice[index++] = (int)items[i].AsNumeric;
            }
            result.SetDice(dice);
            return result;
        }

        var list = new GesValue[count];
        var listIndex = 0;
        for (var i = 0; i < length && listIndex < count; i++)
        {
            if (!items[i].EqualsValue(in face)) continue;
            list[listIndex++] = items[i];
        }

        result.SetList(list);
        return result;
    }

    private static GesValue TakeFullHouse(GesValue[] items, int length, bool diceResult)
    {
        for (var i = 0; i < length; i++)
        {
            var tripleCount = 1;
            for (var j = i + 1; j < length; j++) if (items[i].EqualsValue(in items[j])) tripleCount++;
            if (tripleCount < 3) continue;
            for (var p = 0; p < length; p++)
            {
                if (items[p].EqualsValue(in items[i])) continue;
                var pairCount = 1;
                for (var q = p + 1; q < length; q++) if (items[p].EqualsValue(in items[q])) pairCount++;
                if (pairCount < 2) continue;

                if (diceResult)
                {
                    var result = new GesValue();
                    var dice = new int[5];
                    var di = 0;
                    for (var k = 0; k < length && di < 3; k++) if (items[k].EqualsValue(in items[i])) dice[di++] = (int)items[k].AsNumeric;
                    for (var k = 0; k < length && di < 5; k++) if (items[k].EqualsValue(in items[p])) dice[di++] = (int)items[k].AsNumeric;
                    result.SetDice(dice);
                    return result;
                }

                var value = new GesValue();
                var list = new GesValue[5];
                var li = 0;
                for (var k = 0; k < length && li < 3; k++) if (items[k].EqualsValue(in items[i])) list[li++] = items[k];
                for (var k = 0; k < length && li < 5; k++) if (items[k].EqualsValue(in items[p])) list[li++] = items[k];
                value.SetList(list);
                return value;
            }
        }

        return new GesValue();
    }

    private static GesValue TakeStraight(GesValue[] items, int length, bool diceResult)
    {
        if (!IsStraightItems(items, length))
        {
            return new GesValue();
        }

        var uniqueCount = 0;
        for (var i = 0; i < length; i++)
        {
            var v = (long)items[i].AsNumeric;
            var seen = false;
            for (var j = 0; j < i; j++)
            {
                if ((long)items[j].AsNumeric == v)
                {
                    seen = true;
                    break;
                }
            }
            if (!seen) uniqueCount++;
        }

        if (diceResult)
        {
            var result = new GesValue();
            var dice = new int[uniqueCount];
            var index = 0;
            for (var i = 0; i < length; i++)
            {
                var v = (long)items[i].AsNumeric;
                var seen = false;
                for (var j = 0; j < i; j++) if ((long)items[j].AsNumeric == v) seen = true;
                if (!seen) dice[index++] = (int)v;
            }
            result.SetDice(dice);
            return result;
        }

        var value = new GesValue();
        var list = new GesValue[uniqueCount];
        var listIndex = 0;
        for (var i = 0; i < length; i++)
        {
            var v = (long)items[i].AsNumeric;
            var seen = false;
            for (var j = 0; j < i; j++) if ((long)items[j].AsNumeric == v) seen = true;
            if (!seen) list[listIndex++] = items[i];
        }
        value.SetList(list);
        return value;
    }
   
    private static bool IsFullHouseDice(int[] dice)
    {
        if (dice.Length != 5) return false;
        var first = dice[0];
        var firstCount = 1;
        var second = 0;
        var secondCount = 0;
        for (var i = 1; i < dice.Length; i++)
        {
            if (dice[i] == first) firstCount++;
            else if (secondCount == 0)
            {
                second = dice[i];
                secondCount = 1;
            }
            else if (dice[i] == second) secondCount++;
            else return false;
        }

        return firstCount == 3 && secondCount == 2 || firstCount == 2 && secondCount == 3;
    }

    private static bool IsFullHouseList(GesValue[] list)
    {
        if (list.Length != 5) return false;
        var firstIndex = 0;
        var secondIndex = -1;
        var firstCount = 1;
        var secondCount = 0;
        for (var i = 1; i < list.Length; i++)
        {
            if (list[i].EqualsValue(in list[firstIndex])) firstCount++;
            else if (secondIndex < 0)
            {
                secondIndex = i;
                secondCount = 1;
            }
            else if (list[i].EqualsValue(in list[secondIndex])) secondCount++;
            else return false;
        }

        return firstCount == 3 && secondCount == 2 || firstCount == 2 && secondCount == 3;
    }

    private static bool IsStraightDice(int[] dice)
    {
        if (dice.Length < 2) return false;
        Span<int> unique = stackalloc int[dice.Length];
        var count = 0;
        for (var i = 0; i < dice.Length; i++)
        {
            var exists = false;
            for (var j = 0; j < count; j++) if (unique[j] == dice[i]) exists = true;
            if (!exists) unique[count++] = dice[i];
        }

        if (count < 2) return false;
        for (var i = 1; i < count; i++)
        {
            var value = unique[i];
            var j = i - 1;
            while (j >= 0 && unique[j] > value)
            {
                unique[j + 1] = unique[j];
                j--;
            }
            unique[j + 1] = value;
        }
        for (var i = 1; i < count; i++) if (unique[i - 1] + 1 != unique[i]) return false;
        return true;
    }

    private static bool IsStraightList(GesValue[] list) => IsStraightItems(list, list.Length);

    private static bool IsStraightItems(GesValue[] items, int length)
    {
        if (length < 2) return false;
        Span<long> unique = stackalloc long[length];
        var count = 0;
        for (var i = 0; i < length; i++)
        {
            var number = items[i].AsNumeric;
            if (!double.IsFinite(number)) return false;
            var value = (long)number;
            var exists = false;
            for (var j = 0; j < count; j++) if (unique[j] == value) exists = true;
            if (!exists) unique[count++] = value;
        }

        if (count < 2) return false;
        for (var i = 1; i < count; i++)
        {
            var value = unique[i];
            var j = i - 1;
            while (j >= 0 && unique[j] > value)
            {
                unique[j + 1] = unique[j];
                j--;
            }
            unique[j + 1] = value;
        }
        for (var i = 1; i < count; i++) if (unique[i - 1] + 1 != unique[i]) return false;
        return true;
    }

    private static IteratorBuffer ReadIterator(IGesIterator iterator)
    {
        var buffer = Array.Empty<GesValue>();
        var length = 0;
        try
        {
            GesIteratorResult item;
            while ((item = iterator.Next()).HasValue)
            {
                if (length == buffer.Length) Array.Resize(ref buffer, buffer.Length == 0 ? 8 : buffer.Length * 2);
                buffer[length++] = item.Value;
            }
        }
        finally
        {
            if (iterator is IDisposable disposable) disposable.Dispose();
        }

        return new IteratorBuffer(buffer, length);
    }

    private readonly record struct IteratorBuffer(GesValue[] Items, int Length);
}
