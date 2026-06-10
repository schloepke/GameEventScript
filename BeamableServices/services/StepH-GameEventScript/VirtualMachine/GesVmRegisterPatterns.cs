using System;
using StepH.GameEventScript.Api;
using static StepH.GameEventScript.Api.GameEventScriptBytecodePatternKind;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.VirtualMachine;

internal static class GesVmRegisterPatterns
{
    internal static void VmHasPattern(ref this GesVmValue dst, ref GesVmValue source, GameEventScriptBytecodePatternKind pattern, short count, ushort faceEntryAddress, IGesVmStreamEntryEvaluator evaluator, ushort destinationSlot)
    {
        if (pattern == CountFace)
        {
            var face = dst.OwningState.CreateNothing();
            var unused = dst.OwningState.CreateNothing();
            if (!evaluator.TryEvaluateStreamEntry(faceEntryAddress, 0, ref unused, null, ref face))
            {
                dst.OwningState.Register(destinationSlot).SetNothing();
                return;
            }

            ref var target = ref face.OwningState.Register(destinationSlot);
            switch (source.Kind)
            {
                case Dice when source.ObjectValue is int[] dice:
                    HasPatternDiceFace(ref target, dice, ref face, count);
                    return;
                case List when source.ObjectValue is GesVmListObject list:
                    HasPatternListFace(ref target, list, ref face, count);
                    return;
                case Stream when source.ObjectValue is IGesVmStream stream:
                    if (!stream.IsPatternSequence)
                    {
                        target.SetBoolean(false);
                        return;
                    }
                    if (!ReadStream(ref target, stream, out var items, out var length)) return;
                    HasPatternBufferFace(ref target, items, length, ref face, count);
                    return;
                case Series:
                    target.SetNothing();
                    return;
                default:
                    target.SetBoolean(false);
                    return;
            }
        }

        switch (source.Kind)
        {
            case Dice when source.ObjectValue is int[] dice:
                HasPatternDice(ref dst, dice, pattern, count, faceEntryAddress, evaluator);
                return;
            case List when source.ObjectValue is GesVmListObject list:
                HasPatternList(ref dst, list, pattern, count, faceEntryAddress, evaluator);
                return;
            case Stream when source.ObjectValue is IGesVmStream stream:
                if (!stream.IsPatternSequence)
                {
                    dst.SetBoolean(false);
                    return;
                }
                if (!ReadStream(ref dst, stream, out var items, out var length)) return;
                HasPatternBuffer(ref dst.OwningState.Register((ushort)destinationSlot), items, length, pattern, count, faceEntryAddress, evaluator);
                return;
            case Series:
                dst.SetNothing();
                return;
            default:
                dst.SetBoolean(false);
                return;
        }
    }

    internal static void VmTakePattern(ref this GesVmValue dst, ref GesVmValue source, GameEventScriptBytecodePatternKind pattern, short count, ushort faceEntryAddress, IGesVmStreamEntryEvaluator evaluator, ushort destinationSlot)
    {
        if (pattern == CountFace)
        {
            var face = dst.OwningState.CreateNothing();
            var unused = dst.OwningState.CreateNothing();
            if (!evaluator.TryEvaluateStreamEntry(faceEntryAddress, 0, ref unused, null, ref face))
            {
                dst.OwningState.Register(destinationSlot).SetNothing();
                return;
            }

            ref var target = ref face.OwningState.Register(destinationSlot);
            switch (source.Kind)
            {
                case Dice when source.ObjectValue is int[] dice:
                    var buffer = target.OwningState.CreateList(dice.Length);
                    for (var i = 0; i < dice.Length; i++) buffer.Items[i].SetInteger(dice[i]);
                    var found = 0;
                    for (var i = 0; i < buffer.Length; i++) if (buffer.Items[i].EqualsValue(ref face)) found++;
                    if (found < count) target.SetNothing();
                    else SetTakenByFace(ref target, buffer.Items, buffer.Length, ref face, count, diceResult: true);
                    return;
                case List when source.ObjectValue is GesVmListObject list:
                    var matches = 0;
                    for (var i = 0; i < list.Length; i++) if (list.Items[i].EqualsValue(ref face)) matches++;
                    if (matches < count) target.SetNothing();
                    else SetTakenByFace(ref target, list.Items, list.Length, ref face, count, diceResult: false);
                    return;
                case Stream when source.ObjectValue is IGesVmStream stream:
                    if (!stream.IsPatternSequence)
                    {
                        target.SetNothing();
                        return;
                    }
                    if (!ReadStream(ref target, stream, out var items, out var length)) return;
                    var streamMatches = 0;
                    for (var i = 0; i < length; i++) if (items[i].EqualsValue(ref face)) streamMatches++;
                    if (streamMatches < count) target.SetNothing();
                    else SetTakenByFace(ref target, items, length, ref face, count, diceResult: false);
                    return;
                default:
                    target.SetNothing();
                    return;
            }
        }

        switch (source.Kind)
        {
            case Dice when source.ObjectValue is int[] dice:
                TakePatternDice(ref dst, dice, pattern, count, faceEntryAddress, evaluator);
                return;
            case List when source.ObjectValue is GesVmListObject list:
                TakePatternList(ref dst, list, pattern, count, faceEntryAddress, evaluator);
                return;
            case Stream when source.ObjectValue is IGesVmStream stream:
                if (!stream.IsPatternSequence)
                {
                    dst.SetNothing();
                    return;
                }
                if (!ReadStream(ref dst, stream, out var items, out var length)) return;
                TakePatternBuffer(ref dst.OwningState.Register((ushort)destinationSlot), items, length, pattern, count, faceEntryAddress, evaluator, diceResult: false);
                return;
            default:
                dst.SetNothing();
                return;
        }
    }

    private static void HasPatternDice(ref GesVmValue dst, int[] dice, GameEventScriptBytecodePatternKind pattern, short count, ushort faceEntryAddress, IGesVmStreamEntryEvaluator evaluator)
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
                var face = dst.OwningState.CreateNothing();
                var unused = dst.OwningState.CreateNothing();
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

    private static void HasPatternList(ref GesVmValue dst, GesVmListObject list, GameEventScriptBytecodePatternKind pattern, short count, ushort faceEntryAddress, IGesVmStreamEntryEvaluator evaluator)
    {
        switch (pattern)
        {
            case CountAny:
                for (var i = 0; i < list.Length; i++)
                {
                    var c = 1;
                    for (var j = i + 1; j < list.Length; j++) if (list.Items[i].EqualsValue(ref list.Items[j])) c++;
                    if (c >= count)
                    {
                        dst.SetBoolean(true);
                        return;
                    }
                }

                dst.SetBoolean(false);
                return;
            case CountFace:
                var face = dst.OwningState.CreateNothing();
                var unused = dst.OwningState.CreateNothing();
                if (!evaluator.TryEvaluateStreamEntry(faceEntryAddress, 0, ref unused, null, ref face))
                {
                    dst.SetNothing();
                    return;
                }

                var matches = 0;
                for (var i = 0; i < list.Length; i++) if (list.Items[i].EqualsValue(ref face)) matches++;
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

    private static void HasPatternListFace(ref GesVmValue dst, GesVmListObject list, ref GesVmValue face, short count)
    {
        var matches = 0;
        for (var i = 0; i < list.Length; i++) if (list.Items[i].EqualsValue(ref face)) matches++;
        dst.SetBoolean(matches >= count);
    }

    private static void HasPatternBuffer(ref GesVmValue dst, GesVmValue[] items, int length, GameEventScriptBytecodePatternKind pattern, short count, ushort faceEntryAddress, IGesVmStreamEntryEvaluator evaluator)
    {
        var list = dst.OwningState.CreateList(length);
        for (var i = 0; i < length; i++) list.Items[i] = items[i];
        HasPatternList(ref dst, list, pattern, count, faceEntryAddress, evaluator);
    }

    private static void HasPatternBufferFace(ref GesVmValue dst, GesVmValue[] items, int length, ref GesVmValue face, short count)
    {
        var matches = 0;
        for (var i = 0; i < length; i++) if (items[i].EqualsValue(ref face)) matches++;
        dst.SetBoolean(matches >= count);
    }

    private static void TakePatternDice(ref GesVmValue dst, int[] dice, GameEventScriptBytecodePatternKind pattern, short count, ushort faceEntryAddress, IGesVmStreamEntryEvaluator evaluator)
    {
        var buffer = dst.OwningState.CreateList(dice.Length);
        for (var i = 0; i < dice.Length; i++) buffer.Items[i].SetInteger(dice[i]);
        TakePatternBuffer(ref dst, buffer.Items, dice.Length, pattern, count, faceEntryAddress, evaluator, diceResult: true);
    }

    private static void TakePatternList(ref GesVmValue dst, GesVmListObject list, GameEventScriptBytecodePatternKind pattern, short count, ushort faceEntryAddress, IGesVmStreamEntryEvaluator evaluator)
    {
        TakePatternBuffer(ref dst, list.Items, list.Length, pattern, count, faceEntryAddress, evaluator, diceResult: false);
    }

    private static void TakePatternBuffer(ref GesVmValue dst, GesVmValue[] items, int length, GameEventScriptBytecodePatternKind pattern, short count, ushort faceEntryAddress, IGesVmStreamEntryEvaluator evaluator, bool diceResult)
    {
        switch (pattern)
        {
            case CountFace:
            {
                var face = dst.OwningState.CreateNothing();
                var unused = dst.OwningState.CreateNothing();
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

                SetTakenByFace(ref dst, items, length, ref face, count, diceResult);
                return;
            }
            case CountAny:
                for (var i = 0; i < length; i++)
                {
                    var found = 1;
                    for (var j = i + 1; j < length; j++) if (items[i].EqualsValue(ref items[j])) found++;
                    if (found >= count)
                    {
                        SetTakenByFace(ref dst, items, length, ref items[i], count, diceResult);
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

    private static void SetTakenByFace(ref GesVmValue dst, GesVmValue[] items, int length, ref GesVmValue face, int count, bool diceResult)
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

        var list = dst.OwningState.CreateList(count);
        var listIndex = 0;
        for (var i = 0; i < length && listIndex < count; i++)
        {
            if (!items[i].EqualsValue(ref face)) continue;
            list.Items[listIndex++] = items[i];
        }

        dst.SetList(list);
    }

    private static void TakeFullHouse(ref GesVmValue dst, GesVmValue[] items, int length, bool diceResult)
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

                var list = dst.OwningState.CreateList(5);
                var li = 0;
                for (var k = 0; k < length && li < 3; k++) if (items[k].EqualsValue(ref items[i])) list.Items[li++] = items[k];
                for (var k = 0; k < length && li < 5; k++) if (items[k].EqualsValue(ref items[p])) list.Items[li++] = items[k];
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

        var list = dst.OwningState.CreateList(uniqueCount);
        var listIndex = 0;
        for (var i = 0; i < length; i++)
        {
            var v = (long)items[i].AsNumeric;
            var seen = false;
            for (var j = 0; j < i; j++) if ((long)items[j].AsNumeric == v) seen = true;
            if (!seen) list.Items[listIndex++] = items[i];
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

    private static bool IsFullHouseList(GesVmListObject list)
    {
        if (list.Length != 5) return false;
        var firstIndex = 0;
        var secondIndex = -1;
        var firstCount = 1;
        var secondCount = 0;
        for (var i = 1; i < list.Length; i++)
        {
            if (list.Items[i].EqualsValue(ref list.Items[firstIndex])) firstCount++;
            else if (secondIndex < 0)
            {
                secondIndex = i;
                secondCount = 1;
            }
            else if (list.Items[i].EqualsValue(ref list.Items[secondIndex])) secondCount++;
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

    private static bool IsStraightList(GesVmListObject list) => IsStraightItems(list.Items, list.Length);

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

    private static bool ReadStream(ref GesVmValue dst, IGesVmStream stream, out GesVmValue[] items, out int length)
    {
        var buffer = Array.Empty<GesVmValue>();
        length = 0;
        var item = dst.OwningState.CreateNothing();
        try
        {
            while (stream.TryNext(ref item))
            {
                if (length == buffer.Length) Array.Resize(ref buffer, buffer.Length == 0 ? 8 : buffer.Length * 2);
                buffer[length++] = item;
                item = dst.OwningState.CreateNothing();
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
