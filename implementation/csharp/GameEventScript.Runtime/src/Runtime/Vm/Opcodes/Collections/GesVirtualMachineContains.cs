// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteContains(ushort dst, in GesValue a, in GesValue b)
    {
        switch (b.Kind)
        {
            case Text or Tag when a.Kind is Text or Tag:
                SetBoolean(dst, b.TextValue.Contains(a.TextValue, StringComparison.Ordinal));
                return;
            case Text or Tag:
                SetBoolean(dst, false);
                return;
            case List when b.ObjectValue is GesValue[] list:
                for (var i = 0; i < list.Length; i++)
                {
                    if (!list[i].EqualsValue(a)) continue;
                    SetBoolean(dst, true);
                    return;
                }

                SetBoolean(dst, false);
                return;
            case Iterator when b.ObjectValue is IGesIterator iterator:
                try
                {
                    GesIteratorResult item;
                    while ((item = iterator.Next()).HasValue)
                    {
                        if (!item.Value.EqualsValue(a)) continue;
                        SetBoolean(dst, true);
                        return;
                    }

                    SetBoolean(dst, false);
                    return;
                }
                finally
                {
                    if (iterator is IDisposable disposable) disposable.Dispose();
                }
            case Dice when b.ObjectValue is int[] dice:
                if (a.Kind is not Integer || a.Unit is not GesQuantityUnit.UnitNone)
                {
                    SetBoolean(dst, false);
                    return;
                }

                for (var i = 0; i < dice.Length; i++)
                {
                    if (dice[i] != a.IntegerValue) continue;
                    SetBoolean(dst, true);
                    return;
                }

                SetBoolean(dst, false);
                return;
            case Map when b.ObjectValue is GesValueMap map:
                if (a.Kind is not (Text or Tag))
                {
                    SetBoolean(dst, false);
                    return;
                }

                SetBoolean(dst, map.ContainsKey(a.TextValue));
                return;
            case Vector or Point when b.ObjectValue is GesSpatialValue triplet:
                if (!a.IsNumeric)
                {
                    SetBoolean(dst, false);
                    return;
                }

                var value = new GesValue();
                value.SetFloat(triplet.X, b.Unit);
                if (value.EqualsValue(a))
                {
                    SetBoolean(dst, true);
                    return;
                }

                value.SetFloat(triplet.Y, b.Unit);
                if (value.EqualsValue(a))
                {
                    SetBoolean(dst, true);
                    return;
                }

                value.SetFloat(triplet.Z, b.Unit);
                SetBoolean(dst, value.EqualsValue(a));
                return;
            case GesBytecodeTypeKind.Range when b.ObjectValue is GesIntegerRange range:
                if (a.Kind is not Integer || a.Unit is not GesQuantityUnit.UnitNone || range.Step == 0)
                {
                    SetBoolean(dst, false);
                    return;
                }

                SetBoolean(dst, range.Step > 0
                    ? a.IntegerValue >= range.From && a.IntegerValue <= range.To && unchecked((ulong)a.IntegerValue - (ulong)range.From) % (ulong)range.Step == 0UL
                    : a.IntegerValue <= range.From && a.IntegerValue >= range.To && unchecked((ulong)range.From - (ulong)a.IntegerValue) % unchecked(0UL - (ulong)range.Step) == 0UL);
                return;
            case GesBytecodeTypeKind.Range when b.ObjectValue is GesFloatRange range:
                SetBoolean(dst, ContainsFloatRange(in a, range));
                return;
            case Nothing:
                SetNothing(dst);
                return;
            default:
                SetBoolean(dst, false);
                return;
        }
    }

    private static bool ContainsFloatRange(in GesValue value, GesFloatRange range)
    {
        if (value.Kind is not (Integer or Float) || value.Unit != GesQuantityUnit.UnitNone) return false;
        var number = value.AsNumeric;
        // Converting a large Int64 to Binary64 must not make a different integer appear contained.
        if (value.Kind == Integer && (!GesNumber.CanRepresentAsInteger(number) || (long)number != value.IntegerValue)) return false;
        return GesRangeMath.Contains(range.From, range.To, range.Step, number);
    }
}
