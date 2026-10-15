// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodePatternKind;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteHasPattern(ushort destinationRegister, in GesValue source, GesBytecodePatternKind pattern, short count, in GesValue face)
    {
        if (pattern == CountFace)
        {
            switch (source.Kind)
            {
                case Dice when source.ObjectValue is int[] dice:
                    SetBoolean(destinationRegister, HasPatternDiceFace(dice, in face, count));
                    return;
                case List when source.ObjectValue is GesValue[] list:
                    SetBoolean(destinationRegister, HasPatternListFace(list, in face, count));
                    return;
                case Iterator when source.ObjectValue is IGesIterator iterator:
                    if (!iterator.IsPatternSequence)
                    {
                        SetBoolean(destinationRegister, false);
                        return;
                    }
                    var read = ReadIterator(iterator);
                    SetBoolean(destinationRegister, HasPatternBufferFace(read.Items, read.Length, in face, count));
                    return;
                case Series:
                    SetNothing(destinationRegister);
                    return;
                default:
                    SetBoolean(destinationRegister, false);
                    return;
            }
        }

        switch (source.Kind)
        {
            case Dice when source.ObjectValue is int[] dice:
                SetBoolean(destinationRegister, HasPatternDice(dice, pattern, count));
                return;
            case List when source.ObjectValue is GesValue[] list:
                SetBoolean(destinationRegister, HasPatternList(list, pattern, count));
                return;
            case Iterator when source.ObjectValue is IGesIterator iterator:
                if (!iterator.IsPatternSequence)
                {
                    SetBoolean(destinationRegister, false);
                    return;
                }
                var read = ReadIterator(iterator);
                SetBoolean(destinationRegister, HasPatternBuffer(read.Items, read.Length, pattern, count));
                return;
            case Series:
                SetNothing(destinationRegister);
                return;
            default:
                SetBoolean(destinationRegister, false);
                return;
        }
    }

    private static bool HasPatternDice(int[] dice, GesBytecodePatternKind pattern, short count)
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

    private static bool HasPatternList(GesValue[] list, GesBytecodePatternKind pattern, short count)
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

    private static bool HasPatternBuffer(GesValue[] items, int length, GesBytecodePatternKind pattern, short count)
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
        var unique = new int[dice.Length];
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
}
