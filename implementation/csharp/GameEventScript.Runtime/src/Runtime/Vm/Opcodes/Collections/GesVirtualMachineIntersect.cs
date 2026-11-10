// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using GameEventScript.Syntax;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteIntersect(ushort dst, in GesValue a, in GesValue b)
    {
        if (a.Kind is Nothing || b.Kind is Nothing)
        {
            SetNothing(dst);
            return;
        }

        switch (a.Kind)
        {
            case Map when a.ObjectValue is GesValueMap aMap:
            {
                var map = new GesVmMapBuilder(aMap.Length);
                switch (b.Kind)
                {
                    case Map when b.ObjectValue is GesValueMap bMap:
                    {
                        var ai = 0;
                        var bi = 0;
                        while (ai < aMap.Length && bi < bMap.Length)
                        {
                            var comparison = GesText.CompareScalarOrdinal(aMap.KeyAt(ai), bMap.KeyAt(bi));
                            if (comparison == 0)
                            {
                                map.Set(aMap.KeyAt(ai), aMap.ValueAt(ai));
                                ai++;
                                bi++;
                            }
                            else if (comparison < 0)
                            {
                                ai++;
                            }
                            else
                            {
                                bi++;
                            }
                        }

                        break;
                    }
                    case List when b.ObjectValue is GesValue[] keyList:
                    {
                        var keys = new string[keyList.Length];
                        for (var i = 0; i < keyList.Length; i++)
                        {
                            var keyValue = keyList[i];
                            if (keyValue.Kind is not (Text or Tag))
                            {
                                SetNothing(dst);
                                return;
                            }

                            keys[i] = keyValue.TextValue;
                        }

                        Array.Sort(keys, GesText.ScalarComparer);
                        var ai = 0;
                        var bi = 0;
                        while (ai < aMap.Length && bi < keys.Length)
                        {
                            var comparison = GesText.CompareScalarOrdinal(aMap.KeyAt(ai), keys[bi]);
                            if (comparison == 0)
                            {
                                map.Set(aMap.KeyAt(ai), aMap.ValueAt(ai));
                                ai++;
                                bi++;
                                while (bi < keys.Length && string.Equals(keys[bi], keys[bi - 1], StringComparison.Ordinal)) bi++;
                            }
                            else if (comparison < 0)
                            {
                                ai++;
                            }
                            else
                            {
                                bi++;
                            }
                        }

                        break;
                    }
                    default:
                        SetNothing(dst);
                        return;
                }

                SetMap(dst, map.ToMap());
                return;
            }
            case List when a.ObjectValue is GesValue[] leftList:
            {
                switch (b.Kind)
                {
                    case List when b.ObjectValue is GesValue[] rightList:
                    {
                        var removed = new bool[rightList.Length];
                        var resultLength = 0;
                        for (var i = 0; i < leftList.Length; i++)
                        {
                            for (var j = 0; j < rightList.Length; j++)
                            {
                                if (removed[j] || !leftList[i].EqualsValue(rightList[j])) continue;
                                removed[j] = true;
                                resultLength++;
                                break;
                            }
                        }

                        var list = new GesValue[resultLength];
                        var index = 0;
                        Array.Clear(removed, 0, removed.Length);
                        for (var i = 0; i < leftList.Length; i++)
                        {
                            for (var j = 0; j < rightList.Length; j++)
                            {
                                if (removed[j] || !leftList[i].EqualsValue(rightList[j])) continue;
                                removed[j] = true;
                                list[index++] = leftList[i];
                                break;
                            }
                        }

                        SetList(dst, list);
                        return;
                    }
                    case Dice when b.ObjectValue is int[] rightDice:
                    {
                        var removed = new bool[rightDice.Length];
                        var resultLength = 0;
                        for (var i = 0; i < leftList.Length; i++)
                        {
                            ref var item = ref leftList[i];
                            if (item.Kind is not Integer || item.Unit is not GesQuantityUnit.UnitNone) continue;
                            for (var j = 0; j < rightDice.Length; j++)
                            {
                                if (removed[j] || item.IntegerValue != rightDice[j]) continue;
                                removed[j] = true;
                                resultLength++;
                                break;
                            }
                        }

                        var list = new GesValue[resultLength];
                        var index = 0;
                        Array.Clear(removed, 0, removed.Length);
                        for (var i = 0; i < leftList.Length; i++)
                        {
                            ref var item = ref leftList[i];
                            if (item.Kind is not Integer || item.Unit is not GesQuantityUnit.UnitNone) continue;
                            for (var j = 0; j < rightDice.Length; j++)
                            {
                                if (removed[j] || item.IntegerValue != rightDice[j]) continue;
                                removed[j] = true;
                                list[index++] = item;
                                break;
                            }
                        }

                        SetList(dst, list);
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

                        SetDice(dst, dice);
                        return;
                    case List when b.ObjectValue is GesValue[] rightList:
                        var removed1 = new bool[rightList.Length];
                        var resultLength1 = 0;
                        for (var i = 0; i < leftDice.Length; i++)
                        {
                            for (var j = 0; j < rightList.Length; j++)
                            {
                                ref var item = ref rightList[j];
                                if (removed1[j] || item.Kind is not Integer || item.Unit is not GesQuantityUnit.UnitNone || item.IntegerValue != leftDice[i]) continue;
                                removed1[j] = true;
                                resultLength1++;
                                break;
                            }
                        }

                        var list = new GesValue[resultLength1];
                        var index1 = 0;
                        Array.Clear(removed1, 0, removed1.Length);
                        for (var i = 0; i < leftDice.Length; i++)
                        {
                            for (var j = 0; j < rightList.Length; j++)
                            {
                                ref var item = ref rightList[j];
                                if (removed1[j] || item.Kind is not Integer || item.Unit is not GesQuantityUnit.UnitNone || item.IntegerValue != leftDice[i]) continue;
                                removed1[j] = true;
                                list[index1++].SetInteger(leftDice[i]);
                                break;
                            }
                        }

                        SetList(dst, list);
                        return;
                }

                break;
            }
        }

        SetNothing(dst);
    }
}
