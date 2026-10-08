// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace GameEventScript.Runtime.VM;

internal static class GesVmCompoundIterators
{
    internal static void GesVmIteratorCreate(this GesVmState state, GameEventScriptBytecodeInstruction instruction, GesRuntimeBudget budget)
    {
        var mode = instruction.UnitAndFlags >> 5;
        if (mode == 0)
        {
            state.GesVmIteratorCreate(instruction.DestinationRegister, state.Register(instruction.XRegister));
            return;
        }
        IGesIterator? iterator = null;
        if (mode == 6)
        {
            var source = state.Register(instruction.XRegister);
            var map = source.ObjectValue switch
            {
                GesValueMap value => value,
                GesCustomObject custom => custom.Map,
                GesExternalValue external => external.ToMap(),
                _ => null
            };
            if (map is not null) iterator = new GesEntriesIterator(map, budget);
        }
        else
        {
            var registers = state.FetchUInt16SliceTableByPointer(instruction.XRegister);
            var sources = new GesValue[registers.Length];
            for (var index = 0; index < sources.Length; index++) sources[index] = state.Register(registers[index]);
            iterator = Create(sources, mode, budget);
        }
        if (iterator is null) state.SetNothing(instruction.DestinationRegister);
        else state.SetIterator(instruction.DestinationRegister, iterator);
    }

    private static IGesIterator? Create(GesValue[] sources, int mode, GesRuntimeBudget budget)
    {
        if (mode is 4 or 5)
        {
            var iterators = new IGesIterator[sources.Length];
            for (var index = 0; index < sources.Length; index++)
            {
                if (sources[index].CreateIterator() is { } iterator) { iterators[index] = iterator; continue; }
                for (var previous = 0; previous < index; previous++)
                    if (iterators[previous] is IDisposable disposable) disposable.Dispose();
                return null;
            }
            return new GesProductIterator(sources, iterators, mode == 5, budget);
        }
        var value = sources[0];
        if (value.Kind == Map)
        {
            for (var index = 1; index < sources.Length; index++)
            {
                value = CombineMap(value, sources[index], mode, budget);
                if (value.Kind != Map || budget.IsExhausted) return null;
            }
            return value.CreateIterator();
        }
        if (value.Kind is not (List or Dice)) return null;
        IGesIterator current = value.CreateIterator()!;
        var kind = value.Kind;
        for (var index = 1; index < sources.Length; index++)
        {
            var right = sources[index];
            var valid = right.Kind is List or Dice || mode == 3 && (kind == List && right.Kind != Map || kind == Dice && right.Kind == Integer && right.Unit == 0);
            if (!valid) { Close(current); return null; }
            if (mode == 1)
            {
                if (kind == Dice && right.Kind == Dice)
                {
                    // Dice union sorts globally. Materialize only this typed operator
                    // boundary, never each Cartesian row or Map entry.
                    var collected = new List<GesValue>();
                    while (current.Next() is { HasValue: true } item)
                    {
                        if (!budget.CheckGeneratedCollectionItemCountWithinLimit((long)collected.Count + 1)) { Close(current); return null; }
                        collected.Add(item.Value);
                    }
                    Close(current);
                    var rightDice = (int[])right.ObjectValue!;
                    if (!budget.CheckGeneratedCollectionItemCountWithinLimit((long)collected.Count + rightDice.Length)) return null;
                    var dice = new int[collected.Count + rightDice.Length];
                    for (var item = 0; item < collected.Count; item++) dice[item] = (int)collected[item].IntegerValue;
                    Array.Copy(rightDice, 0, dice, collected.Count, rightDice.Length);
                    Array.Sort(dice);
                    Array.Reverse(dice);
                    current = new GesIntIterator(dice);
                }
                else if (current is GesUnionIterator union) union.Append(right.CreateIterator()!);
                else current = new GesUnionIterator(current, right.CreateIterator()!);
            }
            else
            {
                var count = right.Kind == List ? ((GesValue[])right.ObjectValue!).Length : right.Kind == Dice ? ((int[])right.ObjectValue!).Length : 1;
                if (!budget.CheckGeneratedCollectionItemCountWithinLimit(count)) { Close(current); return null; }
                GesValue[] values;
                if (right.Kind == List) values = (GesValue[])right.ObjectValue!;
                else if (right.Kind == Dice)
                {
                    var dice = (int[])right.ObjectValue!;
                    values = new GesValue[dice.Length];
                    for (var item = 0; item < dice.Length; item++) values[item].SetInteger(dice[item]);
                }
                else values = [right];
                if (current is GesMultisetIterator multiset) multiset.Append(values);
                else current = new GesMultisetIterator(current, values, mode == 2, budget);
            }
            if (right.Kind == List) kind = List;
        }
        return current;
    }

    private static GesValue CombineMap(GesValue left, GesValue right, int mode, GesRuntimeBudget budget)
    {
        var map = (GesValueMap)left.ObjectValue!;
        var other = right.ObjectValue as GesValueMap;
        var list = right.Kind == List ? right.ObjectValue as GesValue[] : null;
        if (other is null && list is null && !(mode == 3 && right.Kind is Text or Tag)) return default;
        if (list is not null)
            foreach (var key in list)
            {
                if (!budget.ConsumeLoopIterationIfAvailable("Map iterator key validation exceeds the configured limit.")) return default;
                if (key.Kind is not (Text or Tag)) return default;
            }
        var keys = new List<string>();
        var values = new List<GesValue>();
        for (var index = 0; index < map.StorageLength; index++)
        {
            if (!budget.ConsumeLoopIterationIfAvailable("Map iterator candidate exceeds the configured limit.")) return default;
            var key = map.KeyAt(index);
            var found = Find(key);
            if (budget.IsExhausted) return default;
            if (mode == 2 && found < 0 || mode == 3 && found >= 0) continue;
            if (!budget.CheckGeneratedCollectionItemCountWithinLimit((long)keys.Count + 1)) return default;
            keys.Add(key);
            values.Add(mode == 1 && other is not null && found >= 0 ? other.ValueAt(found) : map.ValueAt(index));
        }
        if (mode == 1)
        {
            var count = other?.StorageLength ?? list!.Length;
            for (var index = 0; index < count; index++)
            {
                var key = other is not null ? other.KeyAt(index) : list![index].TextValue;
                var found = false;
                foreach (var existing in keys)
                {
                    if (!budget.ConsumeLoopIterationIfAvailable("Map iterator search exceeds the configured limit.")) return default;
                    if (string.Equals(key, existing, StringComparison.Ordinal)) { found = true; break; }
                }
                if (found) continue;
                if (!budget.CheckGeneratedCollectionItemCountWithinLimit((long)keys.Count + 1)) return default;
                keys.Add(key);
                var flag = default(GesValue);
                flag.SetBoolean(true);
                values.Add(other is not null ? other.ValueAt(index) : flag);
            }
        }
        var result = default(GesValue);
        result.SetMap(new GesValueMap(keys.ToArray(), values.ToArray(), keys.Count));
        return result;

        int Find(string key)
        {
            var count = other?.StorageLength ?? list?.Length ?? 1;
            for (var index = 0; index < count; index++)
            {
                if (!budget.ConsumeLoopIterationIfAvailable("Map iterator search exceeds the configured limit.")) return -1;
                var candidate = other is not null ? other.KeyAt(index) : list is not null ? list[index].TextValue : right.TextValue;
                if (string.Equals(key, candidate, StringComparison.Ordinal)) return index;
            }
            return -1;
        }
    }

    private static void Close(IGesIterator iterator)
    {
        if (iterator is IDisposable disposable) disposable.Dispose();
    }

    internal static bool GesVmIteratorNextComponents(this GesVmState state, GameEventScriptBytecodeInstruction instruction)
    {
        var iterator = state.Register(instruction.XRegister).ObjectValue as IGesIterator;
        var targets = state.FetchUInt16SliceTableByPointer(instruction.DestinationRegister);
        var components = iterator as IGesComponentIterator;
        var item = components is null && iterator is not null ? iterator.Next() : default;
        var hasValue = components is not null ? components.MoveNext() : item.HasValue;
        for (var index = 0; index < targets.Length; index++)
        {
            var value = !hasValue ? default : components is not null ? components.Component(index) : Decompose(item.Value, index);
            state.SetValue(targets[index], value);
        }
        if (!hasValue) state.JumpAddress(instruction.TargetAddress);
        return hasValue;
    }

    private static GesValue Decompose(GesValue item, int index)
        => item.Kind switch
        {
            List when item.ObjectValue is GesValue[] values => index < values.Length ? values[index] : default,
            Map when item.ObjectValue is GesValueMap map => index < map.StorageLength ? map.ValueAt(index) : default,
            _ => index == 0 ? item : default
        };

    internal static void GesVmCartesian(this GesVmState state, ushort destination, in GesValue left, in GesValue right, GesRuntimeBudget budget)
    {
        if (left.Kind != List || right.Kind != List) { state.SetNothing(destination); return; }
        var a = (GesValue[])left.ObjectValue!;
        var b = (GesValue[])right.ObjectValue!;
        var count = (long)a.Length * b.Length;
        if (!budget.CheckGeneratedCollectionItemCountWithinLimit(count) || count > int.MaxValue || count > 0 && !budget.CheckGeneratedCollectionItemCountWithinLimit(2))
        {
            state.SetNothing(destination);
            return;
        }
        var result = new GesValue[(int)count];
        var index = 0;
        foreach (var first in a)
            foreach (var second in b) result[index++].SetList([first, second]);
        state.SetList(destination, result);
    }
}
