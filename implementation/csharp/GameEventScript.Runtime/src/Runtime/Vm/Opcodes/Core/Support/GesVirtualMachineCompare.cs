// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private static bool ValuesEqual(in GesValue a, in GesValue b)
    {
        switch (a.Kind)
        {
            case GesBytecodeTypeKind.Boolean when b.Kind is GesBytecodeTypeKind.Boolean:
                return a.IsTrue == b.IsTrue;
            case Integer when b.Kind is Integer:
                return a.Unit == b.Unit && a.IntegerValue == b.IntegerValue;
            case Float or Percentage when b.Kind is Float or Percentage:
                return a.Unit == b.Unit && GesNumber.EqualsWithinUlps(a.FloatValue, b.FloatValue, GesNumber.RuntimeEqualityUlps);
            case Integer or Float or Percentage when b.Kind is Integer or Float or Percentage:
                return a.Unit == b.Unit && GesNumber.EqualsWithinUlps(a.AsNumeric, b.AsNumeric, GesNumber.RuntimeEqualityUlps);
            case Text or Tag when b.Kind is Text or Tag:
                return a.Kind == b.Kind && string.Equals(a.TextValue, b.TextValue, StringComparison.Ordinal);
            case Vector or Point when b.Kind is Vector or Point && a.ObjectValue is GesSpatialValue av && b.ObjectValue is GesSpatialValue bv:
                return a.Unit == b.Unit && a.Kind == b.Kind &&
                       GesNumber.EqualsWithinUlps(av.X, bv.X, GesNumber.RuntimeEqualityUlps) && GesNumber.EqualsWithinUlps(av.Y, bv.Y, GesNumber.RuntimeEqualityUlps) &&
                       GesNumber.EqualsWithinUlps(av.Z, bv.Z, GesNumber.RuntimeEqualityUlps);
            case Dice when b.Kind is Dice && a.ObjectValue is int[] al && b.ObjectValue is int[] bl:
                return a.IntegerValue == b.IntegerValue;
            case Dice when b.Kind == Integer && a.ObjectValue is int[] al:
                return !b.HasUnit && a.IntegerValue == b.IntegerValue;
            case Dice when b.IsNumeric && a.ObjectValue is int[] al:
                return !b.HasUnit && GesNumber.EqualsWithinUlps(a.IntegerValue, b.AsNumeric, GesNumber.RuntimeEqualityUlps);
            case not Dice when a.IsNumeric && b.Kind is Dice && b.ObjectValue is int[] bl:
                return !a.HasUnit && GesNumber.EqualsWithinUlps(b.IntegerValue, a.AsNumeric, GesNumber.RuntimeEqualityUlps);
            case Handler when b.Kind is Handler && a.ObjectValue is GesMessageSignature asig && b.ObjectValue is GesMessageSignature bsig:
                return asig.Equals(bsig);
            case Message when b.Kind is Message && a.ObjectValue is GameEventScriptMessage amsg && b.ObjectValue is GameEventScriptMessage bmsg:
                return amsg.Equals(bmsg);
            case List when b.Kind is List && a.ObjectValue is GesValue[] av && b.ObjectValue is GesValue[] bv:
                if (av.Length != bv.Length) return false;
                for (var i = 0; i < av.Length; i++)
                {
                    if (av[i].EqualsValue(in bv[i])) continue;
                    return false;
                }

                return true;
            case GesBytecodeTypeKind.Range when b.Kind is GesBytecodeTypeKind.Range && a.ObjectValue is GesIntegerRange ar && b.ObjectValue is GesIntegerRange br:
                return ar.From == br.From && ar.To == br.To && ar.Step == br.Step;
            case GesBytecodeTypeKind.Range when b.Kind is GesBytecodeTypeKind.Range && a.ObjectValue is GesFloatRange ar && b.ObjectValue is GesFloatRange br:
                return GesNumber.EqualsWithinUlps(ar.From, br.From, GesNumber.RuntimeEqualityUlps) &&
                       GesNumber.EqualsWithinUlps(ar.To, br.To, GesNumber.RuntimeEqualityUlps) &&
                       GesNumber.EqualsWithinUlps(ar.Step, br.Step, GesNumber.RuntimeEqualityUlps);
            case Series when b.Kind is Series && a.ObjectValue is GesSeriesValue aseries && b.ObjectValue is GesSeriesValue bseries:
                return aseries.SignatureId == bseries.SignatureId && aseries.Offset == bseries.Offset;
            case Map when b.Kind is Map && a.ObjectValue is GesValueMap am && b.ObjectValue is GesValueMap bm:
                return EqualMaps(am, bm);
            case Custom when b.Kind is Custom && a.ObjectValue is GesCustomObject ac && b.ObjectValue is GesCustomObject bc:
                return string.Equals(ac.TypeName, bc.TypeName, StringComparison.Ordinal) && EqualMaps(ac.Map, bc.Map);
            default:
                if (a.IsNumeric && b.IsNumeric) return GesNumber.EqualsWithinUlps(a.AsNumeric, b.AsNumeric, GesNumber.RuntimeEqualityUlps);
                return false;
        }
    }

    private static bool EqualMaps(GesValueMap am, GesValueMap bm)
    {
        if (am.Length != bm.Length) return false;
        var aKeys = am.KeyList;
        var bKeys = bm.KeyList;
        for (var i = 0; i < aKeys.Length; i++)
        {
            if (ValuesEqual(in aKeys[i], in bKeys[i])) continue;
            return false;
        }

        var aValues = am.ValueList;
        var bValues = bm.ValueList;
        for (var i = 0; i < aValues.Length; i++)
        {
            if (aValues[i].EqualsValue(in bValues[i])) continue;
            return false;
        }

        return true;
    }
}
