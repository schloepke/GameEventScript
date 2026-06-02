using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using StepH.GameEventScript.Api;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.BytecodeExecutor;

internal static class VmRegisterCollectionOperators
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmLength(ref this VmValue dst, ref VmValue a, ref GameEventScriptTextTable textTable)
    {
        switch (a.Kind)
        {
            case Text or Tag when a.IsStoragePointer:
                dst.SetInteger(textTable.Resolve((ushort)a.IntegerValue).Length);
                break;
            case Text or Tag when a is { IsStorageObject: true, ObjectValue: string text }:
                dst.SetInteger(text.Length);
                break;
            case List or Map or Dice or GameEventScriptBytecodeTypeKind.Range:
                dst.SetInteger(a.IntegerValue);
                break;
            case Nothing:
                dst.SetInteger(0);
                break;
            default:
                dst.SetNothing();
                break;
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmStartsWith(ref this VmValue dst, ref VmValue a, ref VmValue b, ref GameEventScriptTextTable textTable)
    {
        switch (a.Kind)
        {
            case Text or Tag when b.Kind is Text or Tag:
                dst.SetBoolean(a.ReadTextOrTag().StartsWith(b.ReadTextOrTag(), StringComparison.Ordinal));
                return;
            case Nothing:
                dst.SetNothing();
                return;
            case not (List or Dice or GameEventScriptBytecodeTypeKind.Range):
                dst.SetBoolean(false);
                return;
        }

        if (b.Kind is not (List or Dice or GameEventScriptBytecodeTypeKind.Range))
        {
            dst.SetBoolean(false);
            return;
        }

        VmListObject? leftList = null;
        VmListObject? rightList = null;
        int[]? leftDice = null;
        int[]? rightDice = null;
        VmRange? leftRange = null;
        VmRange? rightRange = null;
        VmFloatRange? leftFloatRange = null;
        VmFloatRange? rightFloatRange = null;

        switch (a.Kind)
        {
            case List when a.ObjectValue is VmListObject value:
                leftList = value;
                break;
            case Dice when a.ObjectValue is int[] value:
                leftDice = value;
                break;
            case GameEventScriptBytecodeTypeKind.Range when a.ObjectValue is VmRange value:
                leftRange = value;
                break;
            case GameEventScriptBytecodeTypeKind.Range when a.ObjectValue is VmFloatRange value:
                leftFloatRange = value;
                break;
            default:
                dst.SetBoolean(false);
                return;
        }

        switch (b.Kind)
        {
            case List when b.ObjectValue is VmListObject value:
                rightList = value;
                break;
            case Dice when b.ObjectValue is int[] value:
                rightDice = value;
                break;
            case GameEventScriptBytecodeTypeKind.Range when b.ObjectValue is VmRange value:
                rightRange = value;
                break;
            case GameEventScriptBytecodeTypeKind.Range when b.ObjectValue is VmFloatRange value:
                rightFloatRange = value;
                break;
            default:
                dst.SetBoolean(false);
                return;
        }

        if (b.IntegerValue > a.IntegerValue)
        {
            dst.SetBoolean(false);
            return;
        }

        var leftInteger = leftRange?.from ?? 0;
        var rightInteger = rightRange?.from ?? 0;
        var leftFloat = leftFloatRange?.from ?? 0d;
        var rightFloat = rightFloatRange?.from ?? 0d;

        for (var i = 0; i < b.IntegerValue; i++)
        {
            var left = dst.OwningState.CreateNothing();
            if (leftList is not null) left = leftList.Items[i];
            else if (leftDice is not null) left.SetInteger(leftDice[i]);
            else if (leftRange is not null)
            {
                left.SetInteger(leftInteger);
                leftInteger += leftRange.step;
            }
            else if (leftFloatRange is not null)
            {
                left.SetFloat(leftFloat);
                leftFloat += leftFloatRange.step;
            }

            var right = dst.OwningState.CreateNothing();
            if (rightList is not null) right = rightList.Items[i];
            else if (rightDice is not null) right.SetInteger(rightDice[i]);
            else if (rightRange is not null)
            {
                right.SetInteger(rightInteger);
                rightInteger += rightRange.step;
            }
            else if (rightFloatRange is not null)
            {
                right.SetFloat(rightFloat);
                rightFloat += rightFloatRange.step;
            }

            if (left.EqualsValue(ref right)) continue;
            dst.SetBoolean(false);
            return;
        }

        dst.SetBoolean(true);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmEndsWith(ref this VmValue dst, ref VmValue a, ref VmValue b, ref GameEventScriptTextTable textTable)
    {
        switch (a.Kind)
        {
            case Text or Tag when b.Kind is Text or Tag:
                dst.SetBoolean(a.ReadTextOrTag().EndsWith(b.ReadTextOrTag(), StringComparison.Ordinal));
                return;
            case Nothing:
                dst.SetNothing();
                return;
            case not (List or Dice or GameEventScriptBytecodeTypeKind.Range):
                dst.SetBoolean(false);
                return;
        }

        if (b.Kind is not (List or Dice or GameEventScriptBytecodeTypeKind.Range))
        {
            dst.SetBoolean(false);
            return;
        }

        VmListObject? leftList = null;
        VmListObject? rightList = null;
        int[]? leftDice = null;
        int[]? rightDice = null;
        VmRange? leftRange = null;
        VmRange? rightRange = null;
        VmFloatRange? leftFloatRange = null;
        VmFloatRange? rightFloatRange = null;

        switch (a.Kind)
        {
            case List when a.ObjectValue is VmListObject value:
                leftList = value;
                break;
            case Dice when a.ObjectValue is int[] value:
                leftDice = value;
                break;
            case GameEventScriptBytecodeTypeKind.Range when a.ObjectValue is VmRange value:
                leftRange = value;
                break;
            case GameEventScriptBytecodeTypeKind.Range when a.ObjectValue is VmFloatRange value:
                leftFloatRange = value;
                break;
            default:
                dst.SetBoolean(false);
                return;
        }

        switch (b.Kind)
        {
            case List when b.ObjectValue is VmListObject value:
                rightList = value;
                break;
            case Dice when b.ObjectValue is int[] value:
                rightDice = value;
                break;
            case GameEventScriptBytecodeTypeKind.Range when b.ObjectValue is VmRange value:
                rightRange = value;
                break;
            case GameEventScriptBytecodeTypeKind.Range when b.ObjectValue is VmFloatRange value:
                rightFloatRange = value;
                break;
            default:
                dst.SetBoolean(false);
                return;
        }

        if (b.IntegerValue > a.IntegerValue)
        {
            dst.SetBoolean(false);
            return;
        }

        var leftOffset = a.IntegerValue - b.IntegerValue;
        var leftInteger = leftRange is not null ? leftRange.from + leftRange.step * leftOffset : 0;
        var rightInteger = rightRange?.from ?? 0;
        var leftFloat = leftFloatRange is not null ? leftFloatRange.from + leftFloatRange.step * leftOffset : 0d;
        var rightFloat = rightFloatRange?.from ?? 0d;

        for (var i = 0; i < b.IntegerValue; i++)
        {
            var leftIndex = leftOffset + i;
            var left = dst.OwningState.CreateNothing();
            if (leftList is not null) left = leftList.Items[leftIndex];
            else if (leftDice is not null) left.SetInteger(leftDice[leftIndex]);
            else if (leftRange is not null)
            {
                left.SetInteger(leftInteger);
                leftInteger += leftRange.step;
            }
            else if (leftFloatRange is not null)
            {
                left.SetFloat(leftFloat);
                leftFloat += leftFloatRange.step;
            }

            var right = dst.OwningState.CreateNothing();
            if (rightList is not null) right = rightList.Items[i];
            else if (rightDice is not null) right.SetInteger(rightDice[i]);
            else if (rightRange is not null)
            {
                right.SetInteger(rightInteger);
                rightInteger += rightRange.step;
            }
            else if (rightFloatRange is not null)
            {
                right.SetFloat(rightFloat);
                rightFloat += rightFloatRange.step;
            }

            if (left.EqualsValue(ref right)) continue;
            dst.SetBoolean(false);
            return;
        }

        dst.SetBoolean(true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmContains(ref this VmValue dst, ref VmValue a, ref VmValue b, ref GameEventScriptTextTable textTable)
    {
        switch (b.Kind)
        {
            case Text or Tag when a.Kind is Text or Tag:
                dst.SetBoolean(b.ReadTextOrTag().Contains(a.ReadTextOrTag(), StringComparison.Ordinal));
                return;
            case Text or Tag:
                dst.SetBoolean(false);
                return;
            case List when b.ObjectValue is VmListObject list:
                for (var i = 0; i < list.Length; i++)
                {
                    if (!list.Items[i].EqualsValue(ref a)) continue;
                    dst.SetBoolean(true);
                    return;
                }

                dst.SetBoolean(false);
                return;
            case Dice when b.ObjectValue is int[] dice:
                if (a.Kind is not Integer || a.Unit is not GameEventScriptBytecodeInstructionUnit.UnitNone)
                {
                    dst.SetBoolean(false);
                    return;
                }

                for (var i = 0; i < dice.Length; i++)
                {
                    if (dice[i] != a.IntegerValue) continue;
                    dst.SetBoolean(true);
                    return;
                }

                dst.SetBoolean(false);
                return;
            case Map when b.ObjectValue is VmMapObject map:
                if (a.Kind is not (Text or Tag))
                {
                    dst.SetBoolean(false);
                    return;
                }

                var key = a.ReadTextOrTag();
                dst.SetBoolean(!key.StartsWith("_", StringComparison.Ordinal) && map.Entries.ContainsKey(key));
                return;
            case Vector or Point when b.ObjectValue is VmFloatTriplet triplet:
                if (!a.IsNumeric)
                {
                    dst.SetBoolean(false);
                    return;
                }

                var value = dst.OwningState.CreateNothing();
                value.SetFloat(triplet.X, b.Unit);
                if (value.EqualsValue(ref a))
                {
                    dst.SetBoolean(true);
                    return;
                }

                value.SetFloat(triplet.Y, b.Unit);
                if (value.EqualsValue(ref a))
                {
                    dst.SetBoolean(true);
                    return;
                }

                value.SetFloat(triplet.Z, b.Unit);
                dst.SetBoolean(value.EqualsValue(ref a));
                return;
            case GameEventScriptBytecodeTypeKind.Range when b.ObjectValue is VmRange range:
                if (a.Kind is not Integer || a.Unit is not GameEventScriptBytecodeInstructionUnit.UnitNone || range.step == 0)
                {
                    dst.SetBoolean(false);
                    return;
                }

                dst.SetBoolean(range.step > 0
                    ? a.IntegerValue >= range.from && a.IntegerValue <= range.to && unchecked((ulong)a.IntegerValue - (ulong)range.from) % (ulong)range.step == 0UL
                    : a.IntegerValue <= range.from && a.IntegerValue >= range.to && unchecked((ulong)range.from - (ulong)a.IntegerValue) % unchecked(0UL - (ulong)range.step) == 0UL);
                return;
            case GameEventScriptBytecodeTypeKind.Range when b.ObjectValue is VmFloatRange range:
                if (!a.IsNumeric || range.step == 0d)
                {
                    dst.SetBoolean(false);
                    return;
                }

                var number = a.AsNumeric;
                if (!double.IsFinite(number))
                {
                    dst.SetBoolean(false);
                    return;
                }

                if (range.step > 0d)
                {
                    var quotient = (number - range.from) / range.step;
                    dst.SetBoolean(number >= range.from && number <= range.to && quotient == Math.Truncate(quotient));
                    return;
                }

                var descendingQuotient = (range.from - number) / -range.step;
                dst.SetBoolean(number <= range.from && number >= range.to && descendingQuotient == Math.Truncate(descendingQuotient));
                return;
            case Nothing:
                dst.SetNothing();
                return;
            default:
                dst.SetBoolean(false);
                return;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmContainsValue(ref this VmValue dst, ref VmValue a, ref VmValue b, ref GameEventScriptTextTable textTable)
    {
        switch (b.Kind)
        {
            case Map or Custom when b.ObjectValue is VmMapObject map:
                foreach (var pair in map.Entries)
                {
                    if (pair.Key.StartsWith("_", StringComparison.Ordinal) || !pair.Value.EqualsValue(ref a)) continue;
                    dst.SetBoolean(true);
                    return;
                }

                dst.SetBoolean(false);
                return;
            case Vector or Point when b.ObjectValue is VmFloatTriplet triplet:
                if (!a.IsNumeric)
                {
                    dst.SetBoolean(false);
                    return;
                }

                var value = dst.OwningState.CreateNothing();
                value.SetFloat(triplet.X, b.Unit);
                if (value.EqualsValue(ref a))
                {
                    dst.SetBoolean(true);
                    return;
                }

                value.SetFloat(triplet.Y, b.Unit);
                if (value.EqualsValue(ref a))
                {
                    dst.SetBoolean(true);
                    return;
                }

                value.SetFloat(triplet.Z, b.Unit);
                dst.SetBoolean(value.EqualsValue(ref a));
                return;
            case Nothing:
                dst.SetNothing();
                return;
            default:
                dst.SetBoolean(false);
                return;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmUnion(ref this VmValue dst, ref VmValue a, ref VmValue b, ref GameEventScriptTextTable textTable)
    {
        if (a.Kind is Nothing || b.Kind is Nothing)
        {
            dst.SetNothing();
            return;
        }

        switch (a.Kind)
        {
            case Map when a.ObjectValue is VmMapObject aMap:
            {
                switch (b.Kind)
                {
                    case Map when b.ObjectValue is VmMapObject bMap:
                    {
                        var map = new Dictionary<string, VmValue>(aMap.Entries, StringComparer.Ordinal);
                        foreach (var pair in bMap.Entries) map[pair.Key] = pair.Value;
                        dst.SetMap(new VmMapObject(dst.OwningState, map));
                        return;
                    }
                    case List when b.ObjectValue is VmListObject keys:
                    {
                        var map = new Dictionary<string, VmValue>(aMap.Entries, StringComparer.Ordinal);
                        for (var i = 0; i < keys.Length; i++)
                        {
                            var keyValue = keys.Items[i];
                            if (keyValue.Kind is not (Text or Tag))
                            {
                                dst.SetNothing();
                                return;
                            }

                            var key = keyValue.ReadTextOrTag();
                            if (map.ContainsKey(key)) continue;
                            var flag = dst.OwningState.CreateBoolean(true);
                            map[key] = flag;
                        }

                        dst.SetMap(new VmMapObject(dst.OwningState, map));
                        return;
                    }
                }

                break;
            }
            case List when a.ObjectValue is VmListObject aList:
            {
                switch (b.Kind)
                {
                    case List when b.ObjectValue is VmListObject bList:
                    {
                        var list = new VmListObject(dst.OwningState, aList.Length + bList.Length);
                        for (var i = 0; i < aList.Length; i++) list.Items[i] = aList.Items[i];
                        for (var i = 0; i < bList.Length; i++) list.Items[aList.Length + i] = bList.Items[i];
                        dst.SetList(list);
                        return;
                    }
                    case Dice when b.ObjectValue is int[] bDice:
                    {
                        var list = new VmListObject(dst.OwningState, aList.Length + bDice.Length);
                        for (var i = 0; i < aList.Length; i++) list.Items[i] = aList.Items[i];
                        for (var i = 0; i < bDice.Length; i++) list.Items[aList.Length + i].SetInteger(bDice[i]);
                        dst.SetList(list);
                        return;
                    }
                }

                break;
            }
            case Dice when a.ObjectValue is int[] aDice:
            {
                switch (b.Kind)
                {
                    case Dice when b.ObjectValue is int[] bDice:
                    {
                        var dice = new int[aDice.Length + bDice.Length];
                        Array.Copy(aDice, dice, aDice.Length);
                        Array.Copy(bDice, 0, dice, aDice.Length, bDice.Length);
                        dst.SetDice(dice);
                        return;
                    }
                    case List when b.ObjectValue is VmListObject bList:
                    {
                        var list = new VmListObject(dst.OwningState, aDice.Length + bList.Length);
                        for (var i = 0; i < aDice.Length; i++) list.Items[i].SetInteger(aDice[i]);
                        for (var i = 0; i < bList.Length; i++) list.Items[aDice.Length + i] = bList.Items[i];
                        dst.SetList(list);
                        return;
                    }
                }

                break;
            }
        }

        dst.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmIntersect(ref this VmValue dst, ref VmValue a, ref VmValue b, ref GameEventScriptTextTable textTable)
    {
        if (a.Kind is Nothing || b.Kind is Nothing)
        {
            dst.SetNothing();
            return;
        }

        switch (a.Kind)
        {
            case Map when a.ObjectValue is VmMapObject aMap:
            {
                var keys = new HashSet<string>(StringComparer.Ordinal);
                switch (b.Kind)
                {
                    case Map when b.ObjectValue is VmMapObject bMap:
                    {
                        foreach (var key in bMap.Entries.Keys) keys.Add(key);
                        break;
                    }
                    case List when b.ObjectValue is VmListObject keyList:
                    {
                        for (var i = 0; i < keyList.Length; i++)
                        {
                            var keyValue = keyList.Items[i];
                            if (keyValue.Kind is not (Text or Tag))
                            {
                                dst.SetNothing();
                                return;
                            }

                            keys.Add(keyValue.ReadTextOrTag());
                        }
                        break;
                    }
                    default:
                        dst.SetNothing();
                        return;
                }
                var map = new Dictionary<string, VmValue>(StringComparer.Ordinal);
                foreach (var pair in aMap.Entries) if (keys.Contains(pair.Key)) map[pair.Key] = pair.Value;
                dst.SetMap(new VmMapObject(dst.OwningState, map));
                return;
            }
            case List when a.ObjectValue is VmListObject leftList:
            {
                switch (b.Kind)
                {
                    case List when b.ObjectValue is VmListObject rightList:
                    {
                        var removed = new bool[rightList.Length];
                        var resultLength = 0;
                        for (var i = 0; i < leftList.Length; i++)
                        {
                            for (var j = 0; j < rightList.Length; j++)
                            {
                                if (removed[j] || !leftList.Items[i].EqualsValue(ref rightList.Items[j])) continue;
                                removed[j] = true;
                                resultLength++;
                                break;
                            }
                        }
                        var list = new VmListObject(dst.OwningState, resultLength);
                        var index = 0;
                        Array.Clear(removed, 0, removed.Length);
                        for (var i = 0; i < leftList.Length; i++)
                        {
                            for (var j = 0; j < rightList.Length; j++)
                            {
                                if (removed[j] || !leftList.Items[i].EqualsValue(ref rightList.Items[j])) continue;
                                removed[j] = true;
                                list.Items[index++] = leftList.Items[i];
                                break;
                            }
                        }
                        dst.SetList(list);
                        return;
                    }
                    case Dice when b.ObjectValue is int[] rightDice:
                    {
                        var removed = new bool[rightDice.Length];
                        var resultLength = 0;
                        for (var i = 0; i < leftList.Length; i++)
                        {
                            ref var item = ref leftList.Items[i];
                            if (item.Kind is not Integer || item.Unit is not GameEventScriptBytecodeInstructionUnit.UnitNone) continue;
                            for (var j = 0; j < rightDice.Length; j++)
                            {
                                if (removed[j] || item.IntegerValue != rightDice[j]) continue;
                                removed[j] = true;
                                resultLength++;
                                break;
                            }
                        }
                        var list = new VmListObject(dst.OwningState, resultLength);
                        var index = 0;
                        Array.Clear(removed, 0, removed.Length);
                        for (var i = 0; i < leftList.Length; i++)
                        {
                            ref var item = ref leftList.Items[i];
                            if (item.Kind is not Integer || item.Unit is not GameEventScriptBytecodeInstructionUnit.UnitNone) continue;
                            for (var j = 0; j < rightDice.Length; j++)
                            {
                                if (removed[j] || item.IntegerValue != rightDice[j]) continue;
                                removed[j] = true;
                                list.Items[index++] = item;
                                break;
                            }
                        }
                        dst.SetList(list);
                        return;
                    }
                }

                break;
            }
            case Dice when a.ObjectValue is int[] leftDice:
            {
                switch (b.Kind)
                {
                    case Dice when b.ObjectValue is int[] rightDice:
                        var removed = new bool[rightDice.Length];
                        var resultLength = 0;
                        for (var i = 0; i < leftDice.Length; i++)
                        {
                            for (var j = 0; j < rightDice.Length; j++)
                            {
                                if (removed[j] || leftDice[i] != rightDice[j]) continue;
                                removed[j] = true;
                                resultLength++;
                                break;
                            }
                        }
                        var dice = new int[resultLength];
                        var index = 0;
                        Array.Clear(removed, 0, removed.Length);
                        for (var i = 0; i < leftDice.Length; i++)
                        {
                            for (var j = 0; j < rightDice.Length; j++)
                            {
                                if (removed[j] || leftDice[i] != rightDice[j]) continue;
                                removed[j] = true;
                                dice[index++] = leftDice[i];
                                break;
                            }
                        }
                        dst.SetDice(dice);
                        return;
                    case List when b.ObjectValue is VmListObject rightList:
                        var removed1 = new bool[rightList.Length];
                        var resultLength1 = 0;
                        for (var i = 0; i < leftDice.Length; i++)
                        {
                            for (var j = 0; j < rightList.Length; j++)
                            {
                                ref var item = ref rightList.Items[j];
                                if (removed1[j] || item.Kind is not Integer || item.Unit is not GameEventScriptBytecodeInstructionUnit.UnitNone || item.IntegerValue != leftDice[i]) continue;
                                removed1[j] = true;
                                resultLength1++;
                                break;
                            }
                        }
                        var list = new VmListObject(dst.OwningState, resultLength1);
                        var index1 = 0;
                        Array.Clear(removed1, 0, removed1.Length);
                        for (var i = 0; i < leftDice.Length; i++)
                        {
                            for (var j = 0; j < rightList.Length; j++)
                            {
                                ref var item = ref rightList.Items[j];
                                if (removed1[j] || item.Kind is not Integer || item.Unit is not GameEventScriptBytecodeInstructionUnit.UnitNone || item.IntegerValue != leftDice[i]) continue;
                                removed1[j] = true;
                                list.Items[index1++].SetInteger(leftDice[i]);
                                break;
                            }
                        }
                        dst.SetList(list);
                        return;
                }
                break;
            }
        }

        dst.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmZip(ref this VmValue dst, ref VmValue a, ref VmValue b, ref GameEventScriptTextTable textTable)
    {
        if (a.Kind is not List || b.Kind is not List || a.ObjectValue is not VmListObject aList || b.ObjectValue is not VmListObject bList)
        {
            dst.SetNothing();
            return;
        }

        var length = Math.Min(aList.Length, bList.Length);
        var list = new VmListObject(dst.OwningState, length);
        for (var i = 0; i < length; i++)
        {
            list.Items[i].SetMap(new VmMapObject(dst.OwningState, new Dictionary<string, VmValue>
            {
                ["left"] = aList.Items[i],
                ["right"] = bList.Items[i]
            }));
        }

        dst.SetList(list);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmValues(ref this VmValue dst, ref VmValue a)
    {
        switch (a.Kind)
        {
            case Map or Custom when a.ObjectValue is VmMapObject map:
                dst.SetList(map.ValueList);
                break;
            default:
                dst.SetNothing();
                break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmKeys(ref this VmValue dst, ref VmValue a)
    {
        switch (a.Kind)
        {
            case Map or Custom when a.ObjectValue is VmMapObject map:
                dst.SetList(map.KeyList);
                break;
            default:
                dst.SetNothing();
                break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmEntries(ref this VmValue dst, ref VmValue a)
    {
        switch (a.Kind)
        {
            case Map or Custom when a.ObjectValue is VmMapObject map:
                dst.SetList(map.EntryList);
                break;
            default:
                dst.SetNothing();
                break;
        }
    }

}
