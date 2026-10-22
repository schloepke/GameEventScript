// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodePatternKind;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteTakePattern(ushort destinationRegister, in GesValue source, GesBytecodePatternKind pattern, short count, in GesValue face)
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
                    SetValue(destinationRegister, in result);
                    return;
                case List when source.ObjectValue is GesValue[] list:
                    var matches = 0;
                    for (var i = 0; i < list.Length; i++) if (list[i].EqualsValue(in face)) matches++;
                    result = matches < count
                        ? new GesValue()
                        : SetTakenByFace(list, list.Length, in face, count, diceResult: false);
                    SetValue(destinationRegister, in result);
                    return;
                case Iterator when source.ObjectValue is IGesIterator iterator:
                    if (!iterator.IsPatternSequence)
                    {
                        SetNothing(destinationRegister);
                        return;
                    }
                    var read = ReadIterator(iterator);
                    var iteratorMatches = 0;
                    for (var i = 0; i < read.Length; i++) if (read.Items[i].EqualsValue(in face)) iteratorMatches++;
                    result = iteratorMatches < count
                        ? new GesValue()
                        : SetTakenByFace(read.Items, read.Length, in face, count, diceResult: false);
                    SetValue(destinationRegister, in result);
                    return;
                default:
                    SetNothing(destinationRegister);
                    return;
            }
        }

        switch (source.Kind)
        {
            case Dice when source.ObjectValue is int[] dice:
                var result = TakePatternDice(dice, pattern, count);
                SetValue(destinationRegister, in result);
                return;
            case List when source.ObjectValue is GesValue[] list:
                result = TakePatternList(list, pattern, count);
                SetValue(destinationRegister, in result);
                return;
            case Iterator when source.ObjectValue is IGesIterator iterator:
                if (!iterator.IsPatternSequence)
                {
                    SetNothing(destinationRegister);
                    return;
                }
                var read = ReadIterator(iterator);
                result = TakePatternBuffer(read.Items, read.Length, pattern, count, diceResult: false);
                SetValue(destinationRegister, in result);
                return;
            default:
                SetNothing(destinationRegister);
                return;
        }
    }

    private static GesValue TakePatternDice(int[] dice, GesBytecodePatternKind pattern, short count)
    {
        var buffer = new GesValue[dice.Length];
        for (var i = 0; i < dice.Length; i++) buffer[i].SetInteger(dice[i]);
        return TakePatternBuffer(buffer, dice.Length, pattern, count, diceResult: true);
    }

    private static GesValue TakePatternList(GesValue[] list, GesBytecodePatternKind pattern, short count)
    {
        return TakePatternBuffer(list, list.Length, pattern, count, diceResult: false);
    }

    private static GesValue TakePatternBuffer(GesValue[] items, int length, GesBytecodePatternKind pattern, short count, bool diceResult)
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
}
