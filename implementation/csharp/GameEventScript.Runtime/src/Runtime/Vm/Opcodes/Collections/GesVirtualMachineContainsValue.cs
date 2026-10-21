// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private void ExecuteContainsValue(ushort dst, in GesValue a, in GesValue b)
    {
        switch (b.Kind)
        {
            case Map when b.ObjectValue is GesValueMap map:
                for (var i = 0; i < map.Length; i++)
                {
                    var mapValue = map.ValueAt(i);
                    if (!mapValue.EqualsValue(a)) continue;
                    SetBoolean(dst, true);
                    return;
                }

                SetBoolean(dst, false);
                return;
            case Custom when b.ObjectValue is GesCustomObject customObject:
                var customMap = customObject.Map;
                for (var i = 0; i < customMap.Length; i++)
                {
                    var mapValue = customMap.ValueAt(i);
                    if (!mapValue.EqualsValue(a)) continue;
                    SetBoolean(dst, true);
                    return;
                }

                SetBoolean(dst, false);
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
            case Nothing:
                SetNothing(dst);
                return;
            default:
                SetBoolean(dst, false);
                return;
        }
    }
}
