// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteUnion(ushort dst, in GesValue a, in GesValue b)
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
                switch (b.Kind)
                {
                    case Map when b.ObjectValue is GesValueMap bMap:
                    {
                        var map = new GesVmMapBuilder(aMap.StorageLength + bMap.StorageLength);
                        for (var i = 0; i < aMap.StorageLength; i++) map.Set(aMap.KeyAt(i), aMap.ValueAt(i));
                        for (var i = 0; i < bMap.StorageLength; i++) map.Set(bMap.KeyAt(i), bMap.ValueAt(i));
                        SetMap(dst, map.ToMap());
                        return;
                    }
                    case List when b.ObjectValue is GesValue[] keys:
                    {
                        var map = new GesVmMapBuilder(aMap.StorageLength + keys.Length);
                        for (var i = 0; i < aMap.StorageLength; i++) map.Set(aMap.KeyAt(i), aMap.ValueAt(i));
                        for (var i = 0; i < keys.Length; i++)
                        {
                            var keyValue = keys[i];
                            if (keyValue.Kind is not (Text or Tag))
                            {
                                SetNothing(dst);
                                return;
                            }

                            var key = keyValue.TextValue;
                            if (map.ContainsKey(key)) continue;
                            var flag = new GesValue();
                            flag.SetBoolean(true);
                            map.Set(key, flag);
                        }

                        SetMap(dst, map.ToMap());
                        return;
                    }
                }

                break;
            }
            case List when a.ObjectValue is GesValue[] aList:
            {
                switch (b.Kind)
                {
                    case List when b.ObjectValue is GesValue[] bList:
                    {
                        var list = new GesValue[aList.Length + bList.Length];
                        for (var i = 0; i < aList.Length; i++) list[i] = aList[i];
                        for (var i = 0; i < bList.Length; i++) list[aList.Length + i] = bList[i];
                        SetList(dst, list);
                        return;
                    }
                    case Dice when b.ObjectValue is int[] bDice:
                    {
                        var list = new GesValue[aList.Length + bDice.Length];
                        for (var i = 0; i < aList.Length; i++) list[i] = aList[i];
                        for (var i = 0; i < bDice.Length; i++) list[aList.Length + i].SetInteger(bDice[i]);
                        SetList(dst, list);
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
                        SetDice(dst, dice);
                        return;
                    }
                    case List when b.ObjectValue is GesValue[] bList:
                    {
                        var list = new GesValue[aDice.Length + bList.Length];
                        for (var i = 0; i < aDice.Length; i++) list[i].SetInteger(aDice[i]);
                        for (var i = 0; i < bList.Length; i++) list[aDice.Length + i] = bList[i];
                        SetList(dst, list);
                        return;
                    }
                }

                break;
            }
        }

        SetNothing(dst);
    }
}
