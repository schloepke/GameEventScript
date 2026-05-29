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
            case Text when a.IsStoragePointer:
                dst.SetInteger(textTable.Resolve((ushort)a.IntegerValue).Length);
                break;
            case Text when a is { IsStorageObject: true, ObjectValue: string text }:
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
            case Text when a.IsStoragePointer && b.Kind is Text && b.IsStoragePointer:
                dst.SetBoolean(textTable.Resolve((ushort)a.IntegerValue).StartsWith(textTable.Resolve((ushort)b.IntegerValue)));
                break;
            case Text when a.IsStoragePointer && b.Kind is Text && b is { IsStorageObject: true, ObjectValue: string startsWith }:
                dst.SetBoolean(textTable.Resolve((ushort)a.IntegerValue).StartsWith(startsWith));
                break;
            case Text when a is { IsStorageObject: true, ObjectValue: string text } && b.Kind is Text && b.IsStoragePointer:
                dst.SetBoolean(text.StartsWith(textTable.Resolve((ushort)b.IntegerValue)));
                break;
            case Text when a is { IsStorageObject: true, ObjectValue: string text } && b.Kind is Text && b is { IsStorageObject: true, ObjectValue: string startsWith }:
                dst.SetBoolean(text.StartsWith(startsWith));
                break;
            case List or Dice or GameEventScriptBytecodeTypeKind.Range:
                dst.SetBoolean(false);
                break;
            default:
                dst.SetBoolean(false);
                break;
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmEndsWith(ref this VmValue dst, ref VmValue a, ref VmValue b, ref GameEventScriptTextTable textTable)
    {
        switch (a.Kind)
        {
            case Text when a.IsStoragePointer && b.Kind is Text && b.IsStoragePointer:
                dst.SetBoolean(textTable.Resolve((ushort)a.IntegerValue).EndsWith(textTable.Resolve((ushort)b.IntegerValue)));
                break;
            case Text when a.IsStoragePointer && b.Kind is Text && b is { IsStorageObject: true, ObjectValue: string startsWith }:
                dst.SetBoolean(textTable.Resolve((ushort)a.IntegerValue).EndsWith(startsWith));
                break;
            case Text when a is { IsStorageObject: true, ObjectValue: string text } && b.Kind is Text && b.IsStoragePointer:
                dst.SetBoolean(text.EndsWith(textTable.Resolve((ushort)b.IntegerValue)));
                break;
            case Text when a is { IsStorageObject: true, ObjectValue: string text } && b.Kind is Text && b is { IsStorageObject: true, ObjectValue: string startsWith }:
                dst.SetBoolean(text.EndsWith(startsWith));
                break;
            case List or Dice or GameEventScriptBytecodeTypeKind.Range:
                dst.SetBoolean(false);
                break;
            default:
                dst.SetBoolean(false);
                break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmContains(ref this VmValue dst, ref VmValue a, ref VmValue b, ref GameEventScriptTextTable textTable)
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmContainsValue(ref this VmValue dst, ref VmValue a, ref VmValue b, ref GameEventScriptTextTable textTable)
    {
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

                            var key = ReadTextOrTag(ref keyValue, ref textTable);
                            if (map.ContainsKey(key)) continue;
                            var flag = default(VmValue);
                            flag.SetBoolean(true);
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

                            keys.Add(ReadTextOrTag(ref keyValue, ref textTable));
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
                        for (var i = 0; i < leftList.Length; i++) if (TryConsumeListMatch(ref leftList.Items[i], rightList, removed, ref textTable)) resultLength++;
                        var list = new VmListObject(dst.OwningState, resultLength);
                        var index = 0;
                        Array.Clear(removed, 0, removed.Length);
                        for (var i = 0; i < leftList.Length; i++) if (TryConsumeListMatch(ref leftList.Items[i], rightList, removed, ref textTable)) list.Items[index++] = leftList.Items[i];
                        dst.SetList(list);
                        return;
                    }
                    case Dice when b.ObjectValue is int[] rightDice:
                    {
                        var removed = new bool[rightDice.Length];
                        var resultLength = 0;
                        for (var i = 0; i < leftList.Length; i++) if (TryConsumeDiceMatch(dst.OwningState, ref leftList.Items[i], rightDice, removed, ref textTable)) resultLength++;
                        var list = new VmListObject(dst.OwningState, resultLength);
                        var index = 0;
                        Array.Clear(removed, 0, removed.Length);
                        for (var i = 0; i < leftList.Length; i++) if (TryConsumeDiceMatch(dst.OwningState, ref leftList.Items[i], rightDice, removed, ref textTable)) list.Items[index++] = leftList.Items[i];
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
                        for (var i = 0; i < leftDice.Length; i++) if (TryConsumeDiceInteger(leftDice[i], rightDice, removed)) resultLength++;
                        var dice = new int[resultLength];
                        var index = 0;
                        Array.Clear(removed, 0, removed.Length);
                        for (var i = 0; i < leftDice.Length; i++) if (TryConsumeDiceInteger(leftDice[i], rightDice, removed)) dice[index++] = leftDice[i];
                        dst.SetDice(dice);
                        return;
                    case List when b.ObjectValue is VmListObject rightList:
                        var removed1 = new bool[rightList.Length];
                        var resultLength1 = 0;
                        for (var i = 0; i < leftDice.Length; i++)
                        {
                            var item = dst.OwningState.CreateInteger(leftDice[i]);
                            if (TryConsumeListMatch(ref item, rightList, removed1, ref textTable)) resultLength1++;
                        }
                        var list = new VmListObject(dst.OwningState, resultLength1);
                        var index1 = 0;
                        Array.Clear(removed1, 0, removed1.Length);
                        for (var i = 0; i < leftDice.Length; i++)
                        {
                            var item = dst.OwningState.CreateInteger(leftDice[i]);
                            if (TryConsumeListMatch(ref item, rightList, removed1, ref textTable)) list.Items[index1++].SetInteger(leftDice[i]);
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
            case Map when a.ObjectValue is VmMapObject map:
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
            case Map when a.ObjectValue is VmMapObject map:
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
            case Map when a.ObjectValue is VmMapObject map:
                dst.SetList(map.EntryList);
                break;
            default:
                dst.SetNothing();
                break;
        }
    }

    private static bool TryConsumeListMatch(ref VmValue item, VmListObject right, bool[] removed, ref GameEventScriptTextTable textTable)
    {
        for (var i = 0; i < right.Length; i++)
        {
            if (!removed[i] && VmValueEquals(ref item, ref right.Items[i], ref textTable))
            {
                removed[i] = true;
                return true;
            }
        }

        return false;
    }

    private static bool TryConsumeDiceMatch(VmState ownerState, ref VmValue item, int[] right, bool[] removed, ref GameEventScriptTextTable textTable)
    {
        for (var i = 0; i < right.Length; i++)
        {
            var diceValue = ownerState.CreateInteger(right[i]);
            if (!removed[i] && VmValueEquals(ref item, ref diceValue, ref textTable))
            {
                removed[i] = true;
                return true;
            }
        }

        return false;
    }

    private static bool TryConsumeDiceInteger(int item, int[] right, bool[] removed)
    {
        for (var i = 0; i < right.Length; i++)
        {
            if (!removed[i] && item == right[i])
            {
                removed[i] = true;
                return true;
            }
        }

        return false;
    }

    private static bool VmValueEquals(ref VmValue a, ref VmValue b, ref GameEventScriptTextTable textTable)
    {
        if (a.Unit != b.Unit || a.Kind != b.Kind) return false;
        return a.Kind switch
        {
            Integer => a.IntegerValue == b.IntegerValue,
            Float or Percentage => a.FloatValue == b.FloatValue,
            GameEventScriptBytecodeTypeKind.Boolean => a.IsTrue == b.IsTrue,
            Text or Tag => string.Equals(ReadTextOrTag(ref a, ref textTable), ReadTextOrTag(ref b, ref textTable), StringComparison.Ordinal),
            _ => ReferenceEquals(a.ObjectValue, b.ObjectValue)
        };
    }

    private static string ReadTextOrTag(ref VmValue value, ref GameEventScriptTextTable textTable)
        => value.IsStoragePointer
            ? textTable.Resolve((ushort)value.IntegerValue)
            : value.ObjectValue as string ?? string.Empty;
}
