using System;
using StepH.GameEventScript.Api;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;
using StepH.GameEventScript.Runtime.Values;

namespace StepH.GameEventScript.Runtime.VM;

internal static class GesVmRegisterCompare
{
    internal static void GesVmEqual(this GesVmState vmState, ushort destinationRegister, in GesValue a, in GesValue b)
    {
        if (a.Kind is Nothing || b.Kind is Nothing) vmState.SetNothing(destinationRegister);
        else vmState.SetBoolean(destinationRegister, a.Equ(in b));
    }
    private static bool Equ(this in GesValue a, in GesValue b)
    {
        switch (a.Kind)
        {
            case GameEventScriptBytecodeTypeKind.Boolean when b.Kind is GameEventScriptBytecodeTypeKind.Boolean:
                return a.IsTrue == b.IsTrue;
            case Integer when b.Kind is Integer:
                return a.Unit == b.Unit && a.IntegerValue == b.IntegerValue;
            case Float or Percentage when b.Kind is Float or Percentage:
                return a.Unit == b.Unit && GameEventScriptNumber.EqualsWithinUlps(a.FloatValue, b.FloatValue, GameEventScriptNumber.RuntimeEqualityUlps);
            case Integer or Float or Percentage when b.Kind is Integer or Float or Percentage:
                return a.Unit == b.Unit && GameEventScriptNumber.EqualsWithinUlps(a.AsNumeric, b.AsNumeric, GameEventScriptNumber.RuntimeEqualityUlps);
            case Text or Tag when b.Kind is Text or Tag:
                if(a.IsNumeric && b.IsNumeric) return GameEventScriptNumber.EqualsWithinUlps(a.AsNumeric, b.AsNumeric, GameEventScriptNumber.RuntimeEqualityUlps);
                return string.Equals(a.TextValue, b.TextValue, StringComparison.Ordinal);
            case Vector or Point when b.Kind is Vector or Point && a.ObjectValue is GesValueVectorPoint av && b.ObjectValue is GesValueVectorPoint bv:
                return a.Unit == b.Unit && a.Kind == b.Kind && GameEventScriptNumber.EqualsWithinUlps(av.X, bv.X, GameEventScriptNumber.RuntimeEqualityUlps) && GameEventScriptNumber.EqualsWithinUlps(av.Y, bv.Y, GameEventScriptNumber.RuntimeEqualityUlps) && GameEventScriptNumber.EqualsWithinUlps(av.Z, bv.Z, GameEventScriptNumber.RuntimeEqualityUlps);
            case Dice when b.Kind is Dice && a.ObjectValue is int[] al && b.ObjectValue is int[] bl:
                return Sum(al) == Sum(bl);
            case Dice when b.Kind == Integer && a.ObjectValue is int[] al:
                return !b.HasUnit && Sum(al) == b.IntegerValue;
            case Dice when b.IsNumeric && a.ObjectValue is int[] al:
                return !b.HasUnit && GameEventScriptNumber.EqualsWithinUlps(Sum(al), b.AsNumeric, GameEventScriptNumber.RuntimeEqualityUlps);
            case not Dice when a.IsNumeric && b.Kind is Dice && b.ObjectValue is int[] bl:
                return !a.HasUnit && GameEventScriptNumber.EqualsWithinUlps(Sum(bl), a.AsNumeric, GameEventScriptNumber.RuntimeEqualityUlps);
            case Handler when b.Kind is Handler && a.ObjectValue is GameEventScriptMessageSignature asig && b.ObjectValue is GameEventScriptMessageSignature bsig:
                return asig.Equals(bsig);
            case Message when b.Kind is Message && a.ObjectValue is GameEventScriptMessage amsg && b.ObjectValue is GameEventScriptMessage bmsg:
                return amsg.Equals(bmsg);
            case List when b.Kind is List && a.ObjectValue is GesValue[] av && b.ObjectValue is GesValue[] bv:
                if (av.Length != bv.Length) return false;
                for (var i = 0; i < av.Length; i++)
                {
                    if (av[i].Equ(in bv[i])) continue;
                    return false;
                }

                return true;
            case GameEventScriptBytecodeTypeKind.Range when b.Kind is GameEventScriptBytecodeTypeKind.Range && a.ObjectValue is GesValueRangeInteger ar && b.ObjectValue is GesValueRangeInteger br:
                return ar.From == br.From && ar.To == br.To && ar.Step == br.Step;
            case GameEventScriptBytecodeTypeKind.Range when b.Kind is GameEventScriptBytecodeTypeKind.Range && a.ObjectValue is GesValueRangeFloat ar && b.ObjectValue is GesValueRangeFloat br:
                return GameEventScriptNumber.EqualsWithinUlps(ar.From, br.From, GameEventScriptNumber.RuntimeEqualityUlps) && GameEventScriptNumber.EqualsWithinUlps(ar.To, br.To, GameEventScriptNumber.RuntimeEqualityUlps) && GameEventScriptNumber.EqualsWithinUlps(ar.Step, br.Step, GameEventScriptNumber.RuntimeEqualityUlps);
            case Series when b.Kind is Series && a.ObjectValue is GesSeries aseries && b.ObjectValue is GesSeries bseries:
                return aseries.SignatureId == bseries.SignatureId && aseries.Offset == bseries.Offset;
            case Map when b.Kind is Map && a.ObjectValue is GesValueMap am && b.ObjectValue is GesValueMap bm:
                return EqualMaps(am, bm);
            case Custom when b.Kind is Custom && a.ObjectValue is GesCustomObject ac && b.ObjectValue is GesCustomObject bc:
                return string.Equals(ac.TypeName, bc.TypeName, StringComparison.Ordinal) && EqualMaps(ac.Map, bc.Map);
            case Map when b.Kind is Custom && a.ObjectValue is GesValueMap am && b.ObjectValue is GesCustomObject bc:
                return EqualMaps(am, bc.Map);
            case Custom when b.Kind is Map && a.ObjectValue is GesCustomObject ac && b.ObjectValue is GesValueMap bm:
                return EqualMaps(ac.Map, bm);
            default:
                if(a.IsNumeric && b.IsNumeric) return GameEventScriptNumber.EqualsWithinUlps(a.AsNumeric, b.AsNumeric, GameEventScriptNumber.RuntimeEqualityUlps);
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
                    if (aKeys[i].Equ(in bKeys[i])) continue;
                    return false;
                }

                var aValues = am.ValueList;
                var bValues = bm.ValueList;
                for (var i = 0; i < aValues.Length; i++)
                {
                    if (aValues[i].Equ(in bValues[i])) continue;
                    return false;
                }

                return true;
    }
    private static long Sum(int[] arr)
    {
        long sum = 0;
        foreach (var v in arr) sum += v;
        return sum;
    }
    internal static void GesVmNotEqual(this GesVmState vmState, ushort destinationRegister, in GesValue a, in GesValue b)
    {
        if (a.Kind is Nothing || b.Kind is Nothing) vmState.SetNothing(destinationRegister);
        else vmState.SetBoolean(destinationRegister, !a.Equ(in b));
    }
    internal static void GesVmLess(this GesVmState vmState, ushort destinationRegister, in GesValue a, in GesValue b)
    {
        switch (a.Kind)
        {
            case Nothing:
                vmState.SetNothing(destinationRegister);
                return;
            case Integer when b.Kind is Integer:
                vmState.SetBoolean(destinationRegister, a.Unit == b.Unit && a.IntegerValue < b.IntegerValue);
                return;
            case Float when b.Kind is Float:
                vmState.SetBoolean(destinationRegister, a.Unit == b.Unit && a.FloatValue < b.FloatValue);
                return;
            default:
                if (b.Kind is Nothing)
                {
                    vmState.SetNothing(destinationRegister);
                    return;
                }

                if (a.IsNumeric && b.IsNumeric)
                {
                    vmState.SetBoolean(destinationRegister, a.Unit == b.Unit && a.AsNumeric < b.AsNumeric);
                    return;
                }

                vmState.SetBoolean(destinationRegister, false);
                return;
        }
    }
    internal static void GesVmGreater(this GesVmState vmState, ushort destinationRegister, in GesValue a, in GesValue b)
    {
        switch (a.Kind)
        {
            case Nothing:
                vmState.SetNothing(destinationRegister);
                return;
            case Integer when b.Kind is Integer:
                vmState.SetBoolean(destinationRegister, a.Unit == b.Unit && a.IntegerValue > b.IntegerValue);
                return;
            case Float when b.Kind is Float:
                vmState.SetBoolean(destinationRegister, a.Unit == b.Unit && a.FloatValue > b.FloatValue);
                return;
            default:
                if (b.Kind is Nothing)
                {
                    vmState.SetNothing(destinationRegister);
                    return;
                }

                if (a.IsNumeric && b.IsNumeric)
                {
                    vmState.SetBoolean(destinationRegister, a.Unit == b.Unit && a.AsNumeric > b.AsNumeric);
                    return;
                }

                vmState.SetBoolean(destinationRegister, false);
                return;
        }
    }
    internal static void GesVmLessOrEqual(this GesVmState vmState, ushort destinationRegister, in GesValue a, in GesValue b)
    {
        switch (a.Kind)
        {
            case Nothing:
                vmState.SetNothing(destinationRegister);
                return;
            case Integer when b.Kind is Integer:
                vmState.SetBoolean(destinationRegister, a.Unit == b.Unit && a.IntegerValue <= b.IntegerValue);
                return;
            case Float when b.Kind is Float:
                vmState.SetBoolean(destinationRegister, a.Unit == b.Unit && a.FloatValue <= b.FloatValue);
                return;
            default:
                if (b.Kind is Nothing)
                {
                    vmState.SetNothing(destinationRegister);
                    return;
                }

                if (a.IsNumeric && b.IsNumeric)
                {
                    vmState.SetBoolean(destinationRegister, a.Unit == b.Unit && a.AsNumeric <= b.AsNumeric);
                    return;
                }

                vmState.SetBoolean(destinationRegister, false);
                return;
        }
    }
    internal static void GesVmGreaterOrEqual(this GesVmState vmState, ushort destinationRegister, in GesValue a, in GesValue b)
    {
        switch (a.Kind)
        {
            case Nothing:
                vmState.SetNothing(destinationRegister);
                return;
            case Integer when b.Kind is Integer:
                vmState.SetBoolean(destinationRegister, a.Unit == b.Unit && a.IntegerValue >= b.IntegerValue);
                return;
            case Float when b.Kind is Float:
                vmState.SetBoolean(destinationRegister, a.Unit == b.Unit && a.FloatValue >= b.FloatValue);
                return;
            default:
                if (b.Kind is Nothing)
                {
                    vmState.SetNothing(destinationRegister);
                    return;
                }

                if (a.IsNumeric && b.IsNumeric)
                {
                    vmState.SetBoolean(destinationRegister, a.Unit == b.Unit && a.AsNumeric >= b.AsNumeric);
                    return;
                }

                vmState.SetBoolean(destinationRegister, false);
                return;
        }
    }
}
