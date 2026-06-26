using System;
using StepH.GameEventScript.Api;
using static StepH.GameEventScript.Api.GameEventScriptBytecodePatternKind;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;
using StepH.GameEventScript.Runtime.Values;

namespace StepH.GameEventScript.Runtime.VM;

internal static class GesVmRegisterPatterns
{
    internal static void GesVmHasPattern(this GesVmState vmState, ushort destinationRegister, in GesVmValue source, GameEventScriptBytecodePatternKind pattern, short count, in GesVmValue face)
    {
        var result = new GesVmValue();
        if (pattern == CountFace)
        {
            switch (source.Kind)
            {
                case Dice when source.ObjectValue is int[] dice:
                    HasPatternDiceFace(ref result, dice, in face, count);
                    vmState.SetValue(destinationRegister, in result);
                    return;
                case List when source.ObjectValue is GesVmValue[] list:
                    HasPatternListFace(ref result, list, in face, count);
                    vmState.SetValue(destinationRegister, in result);
                    return;
                case Iterator when source.ObjectValue is IGesVmIterator iterator:
                    if (!iterator.IsPatternSequence)
                    {
                        vmState.SetBoolean(destinationRegister, false);
                        return;
                    }
                    if (!ReadIterator(vmState, iterator, out var items, out var length)) return;
                    HasPatternBufferFace(ref result, items, length, in face, count);
                    vmState.SetValue(destinationRegister, in result);
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
                HasPatternDice(ref result, dice, pattern, count);
                vmState.SetValue(destinationRegister, in result);
                return;
            case List when source.ObjectValue is GesVmValue[] list:
                HasPatternList(ref result, list, pattern, count);
                vmState.SetValue(destinationRegister, in result);
                return;
            case Iterator when source.ObjectValue is IGesVmIterator iterator:
                if (!iterator.IsPatternSequence)
                {
                    vmState.SetBoolean(destinationRegister, false);
                    return;
                }
                if (!ReadIterator(vmState, iterator, out var items, out var length)) return;
                HasPatternBuffer(ref result, items, length, pattern, count);
                vmState.SetValue(destinationRegister, in result);
                return;
            case Series:
                vmState.SetNothing(destinationRegister);
                return;
            default:
                vmState.SetBoolean(destinationRegister, false);
                return;
        }
    }

    internal static void GesVmTakePattern(this GesVmState vmState, ushort destinationRegister, in GesVmValue source, GameEventScriptBytecodePatternKind pattern, short count, in GesVmValue face)
    {
        var result = new GesVmValue();
        if (pattern == CountFace)
        {
            switch (source.Kind)
            {
                case Dice when source.ObjectValue is int[] dice:
                    var buffer = new GesVmValue[dice.Length];
                    for (var i = 0; i < dice.Length; i++) buffer[i].SetInteger(dice[i]);
                    var found = 0;
                    for (var i = 0; i < buffer.Length; i++) if (buffer[i].EqualsValue(in face)) found++;
                    if (found < count) result.SetNothing();
                    else SetTakenByFace(ref result, buffer, buffer.Length, in face, count, diceResult: true);
                    vmState.SetValue(destinationRegister, in result);
                    return;
                case List when source.ObjectValue is GesVmValue[] list:
                    var matches = 0;
                    for (var i = 0; i < list.Length; i++) if (list[i].EqualsValue(in face)) matches++;
                    if (matches < count) result.SetNothing();
                    else SetTakenByFace(ref result, list, list.Length, in face, count, diceResult: false);
                    vmState.SetValue(destinationRegister, in result);
                    return;
                case Iterator when source.ObjectValue is IGesVmIterator iterator:
                    if (!iterator.IsPatternSequence)
                    {
                        vmState.SetNothing(destinationRegister);
                        return;
                    }
                    if (!ReadIterator(vmState, iterator, out var items, out var length)) return;
                    var iteratorMatches = 0;
                    for (var i = 0; i < length; i++) if (items[i].EqualsValue(in face)) iteratorMatches++;
                    if (iteratorMatches < count) result.SetNothing();
                    else SetTakenByFace(ref result, items, length, in face, count, diceResult: false);
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
                TakePatternDice(ref result, dice, pattern, count);
                vmState.SetValue(destinationRegister, in result);
                return;
            case List when source.ObjectValue is GesVmValue[] list:
                TakePatternList(ref result, list, pattern, count);
                vmState.SetValue(destinationRegister, in result);
                return;
            case Iterator when source.ObjectValue is IGesVmIterator iterator:
                if (!iterator.IsPatternSequence)
                {
                    vmState.SetNothing(destinationRegister);
                    return;
                }
                if (!ReadIterator(vmState, iterator, out var items, out var length)) return;
                TakePatternBuffer(ref result, items, length, pattern, count, diceResult: false);
                vmState.SetValue(destinationRegister, in result);
                return;
            default:
                vmState.SetNothing(destinationRegister);
                return;
        }
    }

    private static void HasPatternDice(ref GesVmValue dst, int[] dice, GameEventScriptBytecodePatternKind pattern, short count)
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
                        dst.SetBoolean(true);
                        return;
                    }
                }

                dst.SetBoolean(false);
                return;
            case FullHouse:
                dst.SetBoolean(IsFullHouseDice(dice));
                return;
            case Straight:
                dst.SetBoolean(IsStraightDice(dice));
                return;
            default:
                dst.SetBoolean(false);
                return;
        }
    }

    private static void HasPatternDiceFace(ref GesVmValue dst, int[] dice, in GesVmValue face, short count)
    {
        var faceNumber = face.AsNumeric;
        if (!double.IsFinite(faceNumber))
        {
            dst.SetBoolean(false);
            return;
        }

        var faceInteger = (long)faceNumber;
        var matches = 0;
        for (var i = 0; i < dice.Length; i++) if (dice[i] == faceInteger) matches++;
        dst.SetBoolean(matches >= count);
    }

    private static void HasPatternList(ref GesVmValue dst, GesVmValue[] list, GameEventScriptBytecodePatternKind pattern, short count)
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
                        dst.SetBoolean(true);
                        return;
                    }
                }

                dst.SetBoolean(false);
                return;
            case FullHouse:
                dst.SetBoolean(IsFullHouseList(list));
                return;
            case Straight:
                dst.SetBoolean(IsStraightList(list));
                return;
            default:
                dst.SetBoolean(false);
                return;
        }
    }

    private static void HasPatternListFace(ref GesVmValue dst, GesVmValue[] list, in GesVmValue face, short count)
    {
        var matches = 0;
        for (var i = 0; i < list.Length; i++) if (list[i].EqualsValue(in face)) matches++;
        dst.SetBoolean(matches >= count);
    }

    private static void HasPatternBuffer(ref GesVmValue dst, GesVmValue[] items, int length, GameEventScriptBytecodePatternKind pattern, short count)
    {
        var list = new GesVmValue[length];
        for (var i = 0; i < length; i++) list[i] = items[i];
        HasPatternList(ref dst, list, pattern, count);
    }

    private static void HasPatternBufferFace(ref GesVmValue dst, GesVmValue[] items, int length, in GesVmValue face, short count)
    {
        var matches = 0;
        for (var i = 0; i < length; i++) if (items[i].EqualsValue(in face)) matches++;
        dst.SetBoolean(matches >= count);
    }

    private static void TakePatternDice(ref GesVmValue dst, int[] dice, GameEventScriptBytecodePatternKind pattern, short count)
    {
        var buffer = new GesVmValue[dice.Length];
        for (var i = 0; i < dice.Length; i++) buffer[i].SetInteger(dice[i]);
        TakePatternBuffer(ref dst, buffer, dice.Length, pattern, count, diceResult: true);
    }

    private static void TakePatternList(ref GesVmValue dst, GesVmValue[] list, GameEventScriptBytecodePatternKind pattern, short count)
    {
        TakePatternBuffer(ref dst, list, list.Length, pattern, count, diceResult: false);
    }

    private static void TakePatternBuffer(ref GesVmValue dst, GesVmValue[] items, int length, GameEventScriptBytecodePatternKind pattern, short count, bool diceResult)
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
                        SetTakenByFace(ref dst, items, length, in items[i], count, diceResult);
                        return;
                    }
                }

                dst.SetNothing();
                return;
            case FullHouse:
                TakeFullHouse(ref dst, items, length, diceResult);
                return;
            case Straight:
                TakeStraight(ref dst, items, length, diceResult);
                return;
            default:
                dst.SetNothing();
                return;
        }
    }

    private static void SetTakenByFace(ref GesVmValue dst, GesVmValue[] items, int length, in GesVmValue face, int count, bool diceResult)
    {
        if (diceResult)
        {
            var dice = new int[count];
            var index = 0;
            for (var i = 0; i < length && index < count; i++)
            {
                if (!items[i].EqualsValue(in face)) continue;
                dice[index++] = (int)items[i].AsNumeric;
            }
            dst.SetDice(dice);
            return;
        }

        var list = new GesVmValue[count];
        var listIndex = 0;
        for (var i = 0; i < length && listIndex < count; i++)
        {
            if (!items[i].EqualsValue(in face)) continue;
            list[listIndex++] = items[i];
        }

        dst.SetList(list);
    }

    private static void TakeFullHouse(ref GesVmValue dst, GesVmValue[] items, int length, bool diceResult)
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
                    var dice = new int[5];
                    var di = 0;
                    for (var k = 0; k < length && di < 3; k++) if (items[k].EqualsValue(in items[i])) dice[di++] = (int)items[k].AsNumeric;
                    for (var k = 0; k < length && di < 5; k++) if (items[k].EqualsValue(in items[p])) dice[di++] = (int)items[k].AsNumeric;
                    dst.SetDice(dice);
                    return;
                }

                var list = new GesVmValue[5];
                var li = 0;
                for (var k = 0; k < length && li < 3; k++) if (items[k].EqualsValue(in items[i])) list[li++] = items[k];
                for (var k = 0; k < length && li < 5; k++) if (items[k].EqualsValue(in items[p])) list[li++] = items[k];
                dst.SetList(list);
                return;
            }
        }

        dst.SetNothing();
    }

    private static void TakeStraight(ref GesVmValue dst, GesVmValue[] items, int length, bool diceResult)
    {
        if (!IsStraightItems(items, length))
        {
            dst.SetNothing();
            return;
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
            var dice = new int[uniqueCount];
            var index = 0;
            for (var i = 0; i < length; i++)
            {
                var v = (long)items[i].AsNumeric;
                var seen = false;
                for (var j = 0; j < i; j++) if ((long)items[j].AsNumeric == v) seen = true;
                if (!seen) dice[index++] = (int)v;
            }
            dst.SetDice(dice);
            return;
        }

        var list = new GesVmValue[uniqueCount];
        var listIndex = 0;
        for (var i = 0; i < length; i++)
        {
            var v = (long)items[i].AsNumeric;
            var seen = false;
            for (var j = 0; j < i; j++) if ((long)items[j].AsNumeric == v) seen = true;
            if (!seen) list[listIndex++] = items[i];
        }
        dst.SetList(list);
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

    private static bool IsFullHouseList(GesVmValue[] list)
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

    private static bool IsStraightList(GesVmValue[] list) => IsStraightItems(list, list.Length);

    private static bool IsStraightItems(GesVmValue[] items, int length)
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

    private static bool ReadIterator(GesVmState vmState, IGesVmIterator iterator, out GesVmValue[] items, out int length)
    {
        var buffer = Array.Empty<GesVmValue>();
        length = 0;
        var item = new GesVmValue();
        try
        {
            while (iterator.TryNext(ref item))
            {
                if (length == buffer.Length) Array.Resize(ref buffer, buffer.Length == 0 ? 8 : buffer.Length * 2);
                buffer[length++] = item;
                item = new GesVmValue();
                item.SetNothing();
            }
        }
        finally
        {
            if (iterator is IDisposable disposable) disposable.Dispose();
        }

        items = buffer;
        return true;
    }
}
