// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void HasAnyOrAll(ushort dst, in GesValue source, bool requireAll)
    {
        switch (source.Kind)
        {
            case Nothing:
                SetNothing(dst);
                return;
            case Iterator when source.ObjectValue is IGesIterator iterator:
            {
                try
                {
                    GesIteratorResult item;
                    while ((item = iterator.Next()).HasValue)
                    {
                        if (item.Value.IsTrue)
                        {
                            if (!requireAll)
                            {
                                SetBoolean(dst, true);
                                return;
                            }
                        }
                        else if (requireAll)
                        {
                            SetBoolean(dst, false);
                            return;
                        }
                    }

                    SetBoolean(dst, requireAll);
                    return;
                }
                finally
                {
                    if (iterator is IDisposable disposable) disposable.Dispose();
                }
            }
            case List when source.ObjectValue is GesValue[] list:
                for (var i = 0; i < list.Length; i++)
                {
                    var item = list[i];
                    if (item.IsTrue)
                    {
                        if (!requireAll)
                        {
                            SetBoolean(dst, true);
                            return;
                        }
                    }
                    else if (requireAll)
                    {
                        SetBoolean(dst, false);
                        return;
                    }
                }

                SetBoolean(dst, requireAll);
                return;
            case Dice when source.ObjectValue is int[] dice:
                if (dice.Length == 0)
                {
                    SetBoolean(dst, requireAll);
                    return;
                }

                if (!requireAll)
                {
                    for (var i = 0; i < dice.Length; i++)
                    {
                        if (dice[i] == 0) continue;
                        SetBoolean(dst, true);
                        return;
                    }

                    SetBoolean(dst, false);
                    return;
                }

                for (var i = 0; i < dice.Length; i++)
                {
                    if (dice[i] != 0) continue;
                    SetBoolean(dst, false);
                    return;
                }

                SetBoolean(dst, true);
                return;
            case Map when source.ObjectValue is GesValueMap map:
            {
                var list = map.ValueList;
                for (var i = 0; i < list.Length; i++)
                {
                    var item = list[i];
                    if (item.IsTrue)
                    {
                        if (!requireAll)
                        {
                            SetBoolean(dst, true);
                            return;
                        }
                    }
                    else if (requireAll)
                    {
                        SetBoolean(dst, false);
                        return;
                    }
                }

                SetBoolean(dst, requireAll);
                return;
            }
            case Custom when source.ObjectValue is GesCustomObject customObject:
            {
                var list = customObject.Map.ValueList;
                for (var i = 0; i < list.Length; i++)
                {
                    var item = list[i];
                    if (item.IsTrue)
                    {
                        if (!requireAll)
                        {
                            SetBoolean(dst, true);
                            return;
                        }
                    }
                    else if (requireAll)
                    {
                        SetBoolean(dst, false);
                        return;
                    }
                }

                SetBoolean(dst, requireAll);
                return;
            }
            case Text or Tag:
            {
                var text = source.TextValue;
                if (text.Length == 0)
                {
                    SetBoolean(dst, requireAll);
                    return;
                }

                var item = new GesValue();
                for (var utf16Offset = 0; utf16Offset < text.Length;)
                {
                    var scalar = GesText.ScalarAtUtf16Offset(text, utf16Offset);
                    utf16Offset += scalar.Length;
                    item.SetText(scalar, 1);
                    if (item.IsTrue)
                    {
                        if (!requireAll)
                        {
                            SetBoolean(dst, true);
                            return;
                        }
                    }
                    else if (requireAll)
                    {
                        SetBoolean(dst, false);
                        return;
                    }
                }

                SetBoolean(dst, requireAll);
                return;
            }
            case Vector or Point when source.ObjectValue is GesSpatialValue triplet:
                if (triplet.X != 0d && double.IsFinite(triplet.X))
                {
                    if (!requireAll)
                    {
                        SetBoolean(dst, true);
                        return;
                    }
                }
                else if (requireAll)
                {
                    SetBoolean(dst, false);
                    return;
                }

                if (triplet.Y != 0d && double.IsFinite(triplet.Y))
                {
                    if (!requireAll)
                    {
                        SetBoolean(dst, true);
                        return;
                    }
                }
                else if (requireAll)
                {
                    SetBoolean(dst, false);
                    return;
                }

                if (triplet.Z != 0d && double.IsFinite(triplet.Z))
                {
                    SetBoolean(dst, true);
                    return;
                }

                SetBoolean(dst, false);
                return;
            case GesBytecodeTypeKind.Range when source.ObjectValue is GesIntegerRange range:
            {
                var length = GesRangeMath.GetLength(range.From, range.To, range.Step);
                var value = range.From;
                for (var i = 0L; i < length; i++)
                {
                    if (value != 0)
                    {
                        if (!requireAll)
                        {
                            SetBoolean(dst, true);
                            return;
                        }
                    }
                    else if (requireAll)
                    {
                        SetBoolean(dst, false);
                        return;
                    }

                    value += range.Step;
                }

                SetBoolean(dst, requireAll);
                return;
            }
            case GesBytecodeTypeKind.Range when source.ObjectValue is GesFloatRange range:
            {
                var length = GesRangeMath.GetLength(range.From, range.To, range.Step);
                for (var i = 0L; i < length; i++)
                {
                    var value = GesRangeMath.GetFloatTerm(range.From, range.To, range.Step, i);
                    if (value != 0d && double.IsFinite(value))
                    {
                        if (!requireAll)
                        {
                            SetBoolean(dst, true);
                            return;
                        }
                    }
                    else if (requireAll)
                    {
                        SetBoolean(dst, false);
                        return;
                    }
                }

                SetBoolean(dst, requireAll);
                return;
            }
            default:
                SetBoolean(dst, false);
                return;
        }
    }

    private void ContainsAnyOrAll(ushort dst, in GesValue a, in GesValue b, bool requireAll)
    {
        if (b.Kind is Nothing)
        {
            SetNothing(dst);
            return;
        }

        if (b.Kind is Iterator && b.ObjectValue is IGesIterator iterator)
        {
            ContainsAnyOrAllIterator(dst, a, iterator, requireAll);
            return;
        }

        var candidate = new GesValue();
        switch (a.Kind)
        {
            case List when a.ObjectValue is GesValue[] list:
                for (var i = 0; i < list.Length; i++)
                {
                    candidate = list[i];
                    if (ContainsHelper(candidate, b) == true)
                    {
                        if (!requireAll)
                        {
                            SetBoolean(dst, true);
                            return;
                        }
                    }
                    else if (requireAll)
                    {
                        SetBoolean(dst, false);
                        return;
                    }
                }

                SetBoolean(dst, requireAll);
                return;
            case Dice when a.ObjectValue is int[] dice:
                for (var i = 0; i < dice.Length; i++)
                {
                    candidate.SetInteger(dice[i]);
                    if (ContainsHelper(candidate, b) == true)
                    {
                        if (!requireAll)
                        {
                            SetBoolean(dst, true);
                            return;
                        }
                    }
                    else if (requireAll)
                    {
                        SetBoolean(dst, false);
                        return;
                    }
                }

                SetBoolean(dst, requireAll);
                return;
            case Text or Tag:
            {
                var text = a.TextValue;
                for (var utf16Offset = 0; utf16Offset < text.Length;)
                {
                    var scalar = GesText.ScalarAtUtf16Offset(text, utf16Offset);
                    utf16Offset += scalar.Length;
                    candidate.SetText(scalar, 1);
                    if (ContainsHelper(candidate, b) == true)
                    {
                        if (!requireAll)
                        {
                            SetBoolean(dst, true);
                            return;
                        }
                    }
                    else if (requireAll)
                    {
                        SetBoolean(dst, false);
                        return;
                    }
                }

                SetBoolean(dst, requireAll);
                return;
            }
            case GesBytecodeTypeKind.Range when a.ObjectValue is GesIntegerRange range:
            {
                var length = GesRangeMath.GetLength(range.From, range.To, range.Step);
                var value = range.From;
                for (var i = 0L; i < length; i++)
                {
                    candidate.SetInteger(value);
                    value += range.Step;
                    if (ContainsHelper(candidate, b) == true)
                    {
                        if (!requireAll)
                        {
                            SetBoolean(dst, true);
                            return;
                        }
                    }
                    else if (requireAll)
                    {
                        SetBoolean(dst, false);
                        return;
                    }
                }

                SetBoolean(dst, requireAll);
                return;
            }
            case GesBytecodeTypeKind.Range when a.ObjectValue is GesFloatRange range:
            {
                var length = GesRangeMath.GetLength(range.From, range.To, range.Step);
                for (var i = 0L; i < length; i++)
                {
                    var value = GesRangeMath.GetFloatTerm(range.From, range.To, range.Step, i);
                    candidate.SetFloat(value);
                    if (ContainsHelper(candidate, b) == true)
                    {
                        if (!requireAll)
                        {
                            SetBoolean(dst, true);
                            return;
                        }
                    }
                    else if (requireAll)
                    {
                        SetBoolean(dst, false);
                        return;
                    }
                }

                SetBoolean(dst, requireAll);
                return;
            }
            default:
                SetBoolean(dst, requireAll);
                return;
        }
    }

    private void ContainsAnyOrAllIterator(ushort dst, in GesValue a, IGesIterator iterator, bool requireAll)
    {
        var candidates = new GesVmListBuilder(8);

        var candidate = new GesValue();
        switch (a.Kind)
        {
            case List when a.ObjectValue is GesValue[] list:
                for (var i = 0; i < list.Length; i++) candidates.Add(in list[i]);
                break;
            case Dice when a.ObjectValue is int[] dice:
                for (var i = 0; i < dice.Length; i++)
                {
                    candidate.SetInteger(dice[i]);
                    candidates.Add(in candidate);
                }

                break;
            case Text or Tag:
            {
                var text = a.TextValue;
                for (var utf16Offset = 0; utf16Offset < text.Length;)
                {
                    var scalar = GesText.ScalarAtUtf16Offset(text, utf16Offset);
                    utf16Offset += scalar.Length;
                    candidate.SetText(scalar, 1);
                    candidates.Add(in candidate);
                }

                break;
            }
            case GesBytecodeTypeKind.Range when a.ObjectValue is GesIntegerRange range:
            {
                var length = GesRangeMath.GetLength(range.From, range.To, range.Step);
                var value = range.From;
                for (var i = 0L; i < length; i++)
                {
                    candidate.SetInteger(value);
                    value += range.Step;
                    candidates.Add(in candidate);
                }

                break;
            }
            case GesBytecodeTypeKind.Range when a.ObjectValue is GesFloatRange range:
            {
                var length = GesRangeMath.GetLength(range.From, range.To, range.Step);
                for (var i = 0L; i < length; i++)
                {
                    var value = GesRangeMath.GetFloatTerm(range.From, range.To, range.Step, i);
                    candidate.SetFloat(value);
                    candidates.Add(in candidate);
                }

                break;
            }
        }

        try
        {
            if (candidates.Count == 0)
            {
                SetBoolean(dst, requireAll);
                return;
            }

            if (!requireAll)
            {
                GesIteratorResult item;
                while ((item = iterator.Next()).HasValue)
                {
                    for (var i = 0; i < candidates.Count; i++)
                    {
                        if (!item.Value.EqualsValue(candidates[i])) continue;
                        SetBoolean(dst, true);
                        return;
                    }
                }

                SetBoolean(dst, false);
                return;
            }

            var found = new bool[candidates.Count];
            var foundCount = 0;
            GesIteratorResult allItem;
            while ((allItem = iterator.Next()).HasValue)
            {
                for (var i = 0; i < candidates.Count; i++)
                {
                    if (found[i] || !allItem.Value.EqualsValue(candidates[i])) continue;
                    found[i] = true;
                    foundCount++;
                }

                if (foundCount != candidates.Count) continue;
                SetBoolean(dst, true);
                return;
            }

            SetBoolean(dst, false);
        }
        finally
        {
            if (iterator is IDisposable disposable) disposable.Dispose();
        }
    }

    private bool? ContainsHelper(in GesValue a, in GesValue b)
    {
        switch (b.Kind)
        {
            case Text or Tag when a.Kind is Text or Tag:
                return b.TextValue.Contains(a.TextValue, StringComparison.Ordinal);
            case Text or Tag:
                return false;
            case List when b.ObjectValue is GesValue[] list:
                for (var i = 0; i < list.Length; i++)
                {
                    if (!list[i].EqualsValue(a)) continue;
                    return true;
                }

                return false;
            case Iterator when b.ObjectValue is IGesIterator iterator:
                try
                {
                    GesIteratorResult item;
                    while ((item = iterator.Next()).HasValue)
                    {
                        if (!item.Value.EqualsValue(a)) continue;
                        return true;
                    }

                    return false;
                }
                finally
                {
                    if (iterator is IDisposable disposable) disposable.Dispose();
                }
            case Dice when b.ObjectValue is int[] dice:
                if (a.Kind is not Integer || a.Unit is not GesQuantityUnit.UnitNone) return false;
                for (var i = 0; i < dice.Length; i++)
                {
                    if (dice[i] != a.IntegerValue) continue;
                    return true;
                }

                return false;
            case Map when b.ObjectValue is GesValueMap map:
                if (a.Kind is not (Text or Tag)) return false;
                return map.ContainsKey(a.TextValue);
            case Vector or Point when b.ObjectValue is GesSpatialValue triplet:
                if (!a.IsNumeric) return false;
                var value = new GesValue();
                value.SetFloat(triplet.X, b.Unit);
                if (value.EqualsValue(a)) return true;
                value.SetFloat(triplet.Y, b.Unit);
                if (value.EqualsValue(a)) return true;
                value.SetFloat(triplet.Z, b.Unit);
                return value.EqualsValue(a);
            case GesBytecodeTypeKind.Range when b.ObjectValue is GesIntegerRange range:
                if (a.Kind is not Integer || a.Unit is not GesQuantityUnit.UnitNone || range.Step == 0) return false;
                return range.Step > 0
                    ? a.IntegerValue >= range.From && a.IntegerValue <= range.To && unchecked((ulong)a.IntegerValue - (ulong)range.From) % (ulong)range.Step == 0UL
                    : a.IntegerValue <= range.From && a.IntegerValue >= range.To && unchecked((ulong)range.From - (ulong)a.IntegerValue) % unchecked(0UL - (ulong)range.Step) == 0UL;
            case GesBytecodeTypeKind.Range when b.ObjectValue is GesFloatRange range:
                return ContainsFloatRange(in a, range);
            case Nothing:
                return null;
            default:
                return false;
        }
    }
}
