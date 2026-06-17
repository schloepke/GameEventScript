using System;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.VirtualMachine;

internal static class GesVmRegisterCompare
{
    internal static void GesVmEqual(this GesVmState vmState, ushort destinationRegister, in GesVmValue a, in GesVmValue b)
    {
        if (a.Kind is Nothing || b.Kind is Nothing) vmState.SetNothing(destinationRegister);
        else vmState.SetBoolean(destinationRegister, a.Equ(in b));
    }
    private static bool Equ(this in GesVmValue a, in GesVmValue b)
    {
        switch (a.Kind)
        {
            case GameEventScriptBytecodeTypeKind.Boolean when b.Kind is GameEventScriptBytecodeTypeKind.Boolean:
                return a.IsTrue == b.IsTrue;
            case Integer when b.Kind is Integer:
                return a.Unit == b.Unit && a.IntegerValue == b.IntegerValue;
            case Float or Percentage when b.Kind is Float or Percentage:
                return a.Unit == b.Unit && DoubleEqualsUlp(a.FloatValue, b.FloatValue);
            case Integer or Float or Percentage when b.Kind is Integer or Float or Percentage:
                return a.Unit == b.Unit && DoubleEqualsUlp(a.AsNumeric, b.AsNumeric);
            case Text or Tag when b.Kind is Text or Tag:
                if(a.IsNumeric && b.IsNumeric) return DoubleEqualsUlp(a.AsNumeric, b.AsNumeric);
                return string.Equals(a.TextValue, b.TextValue, StringComparison.Ordinal);
            case Vector or Point when b.Kind is Vector or Point && a.ObjectValue is GesVmValueVectorPoint av && b.ObjectValue is GesVmValueVectorPoint bv:
                return a.Unit == b.Unit && a.Kind == b.Kind && DoubleEqualsUlp(av.X, bv.X) && DoubleEqualsUlp(av.Y, bv.Y) && DoubleEqualsUlp(av.Z, bv.Z);
            case Dice when b.Kind is Dice && a.ObjectValue is int[] al && b.ObjectValue is int[] bl:
                return Sum(al) == Sum(bl);
            case Dice when b.Kind == Integer && a.ObjectValue is int[] al:
                return !b.HasUnit && Sum(al) == b.IntegerValue;
            case Dice when b.IsNumeric && a.ObjectValue is int[] al:
                return !b.HasUnit && DoubleEqualsUlp(Sum(al), b.AsNumeric);
            case not Dice when a.IsNumeric && b.Kind is Dice && b.ObjectValue is int[] bl:
                return !a.HasUnit && DoubleEqualsUlp(Sum(bl), a.AsNumeric);
            case Handler when b.Kind is Handler && a.ObjectValue is GameEventScriptMessageSignature asig && b.ObjectValue is GameEventScriptMessageSignature bsig:
                return asig.Equals(bsig);
            case Message when b.Kind is Message && a.ObjectValue is GameEventScriptMessage amsg && b.ObjectValue is GameEventScriptMessage bmsg:
                return amsg.Equals(bmsg);
            case List when b.Kind is List && a.ObjectValue is GesVmValue[] av && b.ObjectValue is GesVmValue[] bv:
                if (av.Length != bv.Length) return false;
                for (var i = 0; i < av.Length; i++)
                {
                    if (av[i].Equ(in bv[i])) continue;
                    return false;
                }

                return true;
            case GameEventScriptBytecodeTypeKind.Range when b.Kind is GameEventScriptBytecodeTypeKind.Range && a.ObjectValue is GesVmValueRangeInteger ar && b.ObjectValue is GesVmValueRangeInteger br:
                return ar.From == br.From && ar.To == br.To && ar.Step == br.Step;
            case GameEventScriptBytecodeTypeKind.Range when b.Kind is GameEventScriptBytecodeTypeKind.Range && a.ObjectValue is GesVmValueRangeFloat ar && b.ObjectValue is GesVmValueRangeFloat br:
                return DoubleEqualsUlp(ar.From, br.From) && DoubleEqualsUlp(ar.To, br.To) && DoubleEqualsUlp(ar.Step, br.Step);
            case Series when b.Kind is Series && a.ObjectValue is GameEventScriptSeriesValue aseries && b.ObjectValue is GameEventScriptSeriesValue bseries:
                return aseries.SignatureId == bseries.SignatureId && aseries.Offset == bseries.Offset;
            case Map or Custom when b.Kind is Map or Custom && a.ObjectValue is GesVmValueMap am && b.ObjectValue is GesVmValueMap bm:
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
            default:
                if(a.IsNumeric && b.IsNumeric) return DoubleEqualsUlp(a.AsNumeric, b.AsNumeric);
                return false;
        }
    }
    private static long Sum(int[] arr)
    {
        long sum = 0;
        foreach (var v in arr) sum += v;
        return sum;
    }
    internal static void GesVmNotEqual(this GesVmState vmState, ushort destinationRegister, in GesVmValue a, in GesVmValue b)
    {
        if (a.Kind is Nothing || b.Kind is Nothing) vmState.SetNothing(destinationRegister);
        else vmState.SetBoolean(destinationRegister, !a.Equ(in b));
    }
    internal static void GesVmLess(this GesVmState vmState, ushort destinationRegister, in GesVmValue a, in GesVmValue b)
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
    internal static void GesVmGreater(this GesVmState vmState, ushort destinationRegister, in GesVmValue a, in GesVmValue b)
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
    internal static void GesVmLessOrEqual(this GesVmState vmState, ushort destinationRegister, in GesVmValue a, in GesVmValue b)
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
    internal static void GesVmGreaterOrEqual(this GesVmState vmState, ushort destinationRegister, in GesVmValue a, in GesVmValue b)
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
    private static bool DoubleEqualsUlp(double a, double b)
    {
        var aBits = BitConverter.DoubleToInt64Bits(a);
        var bBits = BitConverter.DoubleToInt64Bits(b);
        
        if(double.IsNaN(a) || double.IsNaN(b)) return false;
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (double.IsInfinity(a) || double.IsInfinity(b)) return a == b;
        if (aBits == bBits) return true;
        if (aBits < 0) aBits = long.MinValue - aBits;
        if (bBits < 0) bBits = long.MinValue - bBits;
        return (aBits > bBits ? (ulong)(aBits - bBits) : (ulong)(bBits - aBits)) <= 2;
    }
}
