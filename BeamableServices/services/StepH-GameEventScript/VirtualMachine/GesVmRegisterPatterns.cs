using System;
using StepH.GameEventScript.Api;
using static StepH.GameEventScript.Api.GameEventScriptBytecodePatternKind;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.VirtualMachine;

internal static class GesVmRegisterPatterns
{
    internal static void GesVmHasPattern(this GesVmState state, ushort destinationRegister, in GesVmValue source, GameEventScriptBytecodePatternKind pattern, short count, ushort faceEntryAddress, IGesVmStreamEntryEvaluator evaluator)
    {
        var result = state.CreateNothing();
        if (pattern == CountFace)
        {
            var face = state.CreateNothing();
            var unused = state.CreateNothing();
            if (!evaluator.TryEvaluateStreamEntry(faceEntryAddress, 0, ref unused, null, ref face))
            {
                state.SetNothing(destinationRegister);
                return;
            }

            switch (source.Kind)
            {
                case Dice when source.ObjectValue is int[] dice:
                    HasPatternDiceFace(ref result, dice, ref face, count);
                    state.SetValue(destinationRegister, in result);
                    return;
                case List when source.ObjectValue is GesVmValue[] list:
                    HasPatternListFace(ref result, list, ref face, count);
                    state.SetValue(destinationRegister, in result);
                    return;
                case Stream when source.ObjectValue is IGesVmStream stream:
                    if (!stream.IsPatternSequence)
                    {
                        state.SetBoolean(destinationRegister, false);
                        return;
                    }
                    if (!ReadStream(state, stream, out var items, out var length)) return;
                    HasPatternBufferFace(ref result, items, length, ref face, count);
                    state.SetValue(destinationRegister, in result);
                    return;
                case Series:
                    state.SetNothing(destinationRegister);
                    return;
                default:
                    state.SetBoolean(destinationRegister, false);
                    return;
            }
        }

        switch (source.Kind)
        {
            case Dice when source.ObjectValue is int[] dice:
                HasPatternDice(state, ref result, dice, pattern, count, faceEntryAddress, evaluator);
                state.SetValue(destinationRegister, in result);
                return;
            case List when source.ObjectValue is GesVmValue[] list:
                HasPatternList(state, ref result, list, pattern, count, faceEntryAddress, evaluator);
                state.SetValue(destinationRegister, in result);
                return;
            case Stream when source.ObjectValue is IGesVmStream stream:
                if (!stream.IsPatternSequence)
                {
                    state.SetBoolean(destinationRegister, false);
                    return;
                }
                if (!ReadStream(state, stream, out var items, out var length)) return;
                HasPatternBuffer(state, ref result, items, length, pattern, count, faceEntryAddress, evaluator);
                state.SetValue(destinationRegister, in result);
                return;
            case Series:
                state.SetNothing(destinationRegister);
                return;
            default:
                state.SetBoolean(destinationRegister, false);
                return;
        }
    }

    internal static void GesVmTakePattern(this GesVmState state, ushort destinationRegister, in GesVmValue source, GameEventScriptBytecodePatternKind pattern, short count, ushort faceEntryAddress, IGesVmStreamEntryEvaluator evaluator)
    {
        var result = state.CreateNothing();
        if (pattern == CountFace)
        {
            var face = state.CreateNothing();
            var unused = state.CreateNothing();
            if (!evaluator.TryEvaluateStreamEntry(faceEntryAddress, 0, ref unused, null, ref face))
            {
                state.SetNothing(destinationRegister);
                return;
            }

            switch (source.Kind)
            {
                case Dice when source.ObjectValue is int[] dice:
                    var buffer = state.CreateList(dice.Length);
                    for (var i = 0; i < dice.Length; i++) buffer[i].SetInteger(dice[i]);
                    var found = 0;
                    for (var i = 0; i < buffer.Length; i++) if (buffer[i].EqualsValue(ref face)) found++;
                    if (found < count) result.SetNothing();
                    else SetTakenByFace(state, ref result, buffer, buffer.Length, ref face, count, diceResult: true);
                    state.SetValue(destinationRegister, in result);
                    return;
                case List when source.ObjectValue is GesVmValue[] list:
                    var matches = 0;
                    for (var i = 0; i < list.Length; i++) if (list[i].EqualsValue(ref face)) matches++;
                    if (matches < count) result.SetNothing();
                    else SetTakenByFace(state, ref result, list, list.Length, ref face, count, diceResult: false);
                    state.SetValue(destinationRegister, in result);
                    return;
                case Stream when source.ObjectValue is IGesVmStream stream:
                    if (!stream.IsPatternSequence)
                    {
                        state.SetNothing(destinationRegister);
                        return;
                    }
                    if (!ReadStream(state, stream, out var items, out var length)) return;
                    var streamMatches = 0;
                    for (var i = 0; i < length; i++) if (items[i].EqualsValue(ref face)) streamMatches++;
                    if (streamMatches < count) result.SetNothing();
                    else SetTakenByFace(state, ref result, items, length, ref face, count, diceResult: false);
                    state.SetValue(destinationRegister, in result);
                    return;
                default:
                    state.SetNothing(destinationRegister);
                    return;
            }
        }

        switch (source.Kind)
        {
            case Dice when source.ObjectValue is int[] dice:
                TakePatternDice(state, ref result, dice, pattern, count, faceEntryAddress, evaluator);
                state.SetValue(destinationRegister, in result);
                return;
            case List when source.ObjectValue is GesVmValue[] list:
                TakePatternList(state, ref result, list, pattern, count, faceEntryAddress, evaluator);
                state.SetValue(destinationRegister, in result);
                return;
            case Stream when source.ObjectValue is IGesVmStream stream:
                if (!stream.IsPatternSequence)
                {
                    state.SetNothing(destinationRegister);
                    return;
                }
                if (!ReadStream(state, stream, out var items, out var length)) return;
                TakePatternBuffer(state, ref result, items, length, pattern, count, faceEntryAddress, evaluator, diceResult: false);
                state.SetValue(destinationRegister, in result);
                return;
            default:
                state.SetNothing(destinationRegister);
                return;
        }
    }

    private static void HasPatternDice(GesVmState state, ref GesVmValue dst, int[] dice, GameEventScriptBytecodePatternKind pattern, short count, ushort faceEntryAddress, IGesVmStreamEntryEvaluator evaluator)
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
            case CountFace:
                var face = state.CreateNothing();
                var unused = state.CreateNothing();
                if (!evaluator.TryEvaluateStreamEntry(faceEntryAddress, 0, ref unused, null, ref face))
                {
                    dst.SetNothing();
                    return;
                }

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

    private static void HasPatternDiceFace(ref GesVmValue dst, int[] dice, ref GesVmValue face, short count)
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

    private static void HasPatternList(GesVmState state, ref GesVmValue dst, GesVmValue[] list, GameEventScriptBytecodePatternKind pattern, short count, ushort faceEntryAddress, IGesVmStreamEntryEvaluator evaluator)
    {
        switch (pattern)
        {
            case CountAny:
                for (var i = 0; i < list.Length; i++)
                {
                    var c = 1;
                    for (var j = i + 1; j < list.Length; j++) if (list[i].EqualsValue(ref list[j])) c++;
                    if (c >= count)
                    {
                        dst.SetBoolean(true);
                        return;
                    }
                }

                dst.SetBoolean(false);
                return;
            case CountFace:
                var face = state.CreateNothing();
                var unused = state.CreateNothing();
                if (!evaluator.TryEvaluateStreamEntry(faceEntryAddress, 0, ref unused, null, ref face))
                {
                    dst.SetNothing();
                    return;
                }

                var matches = 0;
                for (var i = 0; i < list.Length; i++) if (list[i].EqualsValue(ref face)) matches++;
                dst.SetBoolean(matches >= count);
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

    private static void HasPatternListFace(ref GesVmValue dst, GesVmValue[] list, ref GesVmValue face, short count)
    {
        var matches = 0;
        for (var i = 0; i < list.Length; i++) if (list[i].EqualsValue(ref face)) matches++;
        dst.SetBoolean(matches >= count);
    }

    private static void HasPatternBuffer(GesVmState state, ref GesVmValue dst, GesVmValue[] items, int length, GameEventScriptBytecodePatternKind pattern, short count, ushort faceEntryAddress, IGesVmStreamEntryEvaluator evaluator)
    {
        var list = state.CreateList(length);
        for (var i = 0; i < length; i++) list[i] = items[i];
        HasPatternList(state, ref dst, list, pattern, count, faceEntryAddress, evaluator);
    }

    private static void HasPatternBufferFace(ref GesVmValue dst, GesVmValue[] items, int length, ref GesVmValue face, short count)
    {
        var matches = 0;
        for (var i = 0; i < length; i++) if (items[i].EqualsValue(ref face)) matches++;
        dst.SetBoolean(matches >= count);
    }

    private static void TakePatternDice(GesVmState state, ref GesVmValue dst, int[] dice, GameEventScriptBytecodePatternKind pattern, short count, ushort faceEntryAddress, IGesVmStreamEntryEvaluator evaluator)
    {
        var buffer = state.CreateList(dice.Length);
        for (var i = 0; i < dice.Length; i++) buffer[i].SetInteger(dice[i]);
        TakePatternBuffer(state, ref dst, buffer, dice.Length, pattern, count, faceEntryAddress, evaluator, diceResult: true);
    }

    private static void TakePatternList(GesVmState state, ref GesVmValue dst, GesVmValue[] list, GameEventScriptBytecodePatternKind pattern, short count, ushort faceEntryAddress, IGesVmStreamEntryEvaluator evaluator)
    {
        TakePatternBuffer(state, ref dst, list, list.Length, pattern, count, faceEntryAddress, evaluator, diceResult: false);
    }

    private static void TakePatternBuffer(GesVmState state, ref GesVmValue dst, GesVmValue[] items, int length, GameEventScriptBytecodePatternKind pattern, short count, ushort faceEntryAddress, IGesVmStreamEntryEvaluator evaluator, bool diceResult)
    {
        switch (pattern)
        {
            case CountFace:
            {
                var face = state.CreateNothing();
                var unused = state.CreateNothing();
                if (!evaluator.TryEvaluateStreamEntry(faceEntryAddress, 0, ref unused, null, ref face))
                {
                    dst.SetNothing();
                    return;
                }

                var found = 0;
                for (var i = 0; i < length; i++) if (items[i].EqualsValue(ref face)) found++;
                if (found < count)
                {
                    dst.SetNothing();
                    return;
                }

                SetTakenByFace(state, ref dst, items, length, ref face, count, diceResult);
                return;
            }
            case CountAny:
                for (var i = 0; i < length; i++)
                {
                    var found = 1;
                    for (var j = i + 1; j < length; j++) if (items[i].EqualsValue(ref items[j])) found++;
                    if (found >= count)
                    {
                        SetTakenByFace(state, ref dst, items, length, ref items[i], count, diceResult);
                        return;
                    }
                }

                dst.SetNothing();
                return;
            case FullHouse:
                TakeFullHouse(state, ref dst, items, length, diceResult);
                return;
            case Straight:
                TakeStraight(state, ref dst, items, length, diceResult);
                return;
            default:
                dst.SetNothing();
                return;
        }
    }

    private static void SetTakenByFace(GesVmState state, ref GesVmValue dst, GesVmValue[] items, int length, ref GesVmValue face, int count, bool diceResult)
    {
        if (diceResult)
        {
            var dice = new int[count];
            var index = 0;
            for (var i = 0; i < length && index < count; i++)
            {
                if (!items[i].EqualsValue(ref face)) continue;
                dice[index++] = (int)items[i].AsNumeric;
            }
            dst.SetDice(dice);
            return;
        }

        var list = state.CreateList(count);
        var listIndex = 0;
        for (var i = 0; i < length && listIndex < count; i++)
        {
            if (!items[i].EqualsValue(ref face)) continue;
            list[listIndex++] = items[i];
        }

        dst.SetList(list);
    }

    private static void TakeFullHouse(GesVmState state, ref GesVmValue dst, GesVmValue[] items, int length, bool diceResult)
    {
        for (var i = 0; i < length; i++)
        {
            var tripleCount = 1;
            for (var j = i + 1; j < length; j++) if (items[i].EqualsValue(ref items[j])) tripleCount++;
            if (tripleCount < 3) continue;
            for (var p = 0; p < length; p++)
            {
                if (items[p].EqualsValue(ref items[i])) continue;
                var pairCount = 1;
                for (var q = p + 1; q < length; q++) if (items[p].EqualsValue(ref items[q])) pairCount++;
                if (pairCount < 2) continue;

                if (diceResult)
                {
                    var dice = new int[5];
                    var di = 0;
                    for (var k = 0; k < length && di < 3; k++) if (items[k].EqualsValue(ref items[i])) dice[di++] = (int)items[k].AsNumeric;
                    for (var k = 0; k < length && di < 5; k++) if (items[k].EqualsValue(ref items[p])) dice[di++] = (int)items[k].AsNumeric;
                    dst.SetDice(dice);
                    return;
                }

                var list = state.CreateList(5);
                var li = 0;
                for (var k = 0; k < length && li < 3; k++) if (items[k].EqualsValue(ref items[i])) list[li++] = items[k];
                for (var k = 0; k < length && li < 5; k++) if (items[k].EqualsValue(ref items[p])) list[li++] = items[k];
                dst.SetList(list);
                return;
            }
        }

        dst.SetNothing();
    }

    private static void TakeStraight(GesVmState state, ref GesVmValue dst, GesVmValue[] items, int length, bool diceResult)
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

        var list = state.CreateList(uniqueCount);
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
            if (list[i].EqualsValue(ref list[firstIndex])) firstCount++;
            else if (secondIndex < 0)
            {
                secondIndex = i;
                secondCount = 1;
            }
            else if (list[i].EqualsValue(ref list[secondIndex])) secondCount++;
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

    private static bool ReadStream(GesVmState state, IGesVmStream stream, out GesVmValue[] items, out int length)
    {
        var buffer = Array.Empty<GesVmValue>();
        length = 0;
        var item = state.CreateNothing();
        try
        {
            while (stream.TryNext(ref item))
            {
                if (length == buffer.Length) Array.Resize(ref buffer, buffer.Length == 0 ? 8 : buffer.Length * 2);
                buffer[length++] = item;
                item = state.CreateNothing();
            }
        }
        finally
        {
            if (stream is IDisposable disposable) disposable.Dispose();
        }

        items = buffer;
        return true;
    }
}
