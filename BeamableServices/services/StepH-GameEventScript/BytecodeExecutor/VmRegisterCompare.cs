using System;
using System.Runtime.CompilerServices;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.BytecodeExecutor;

internal static class VmRegisterCompare
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmEqual(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        if (a.Kind is Nothing || b.Kind is Nothing) dst.SetNothing();
        else dst.SetBoolean(a.Equ(ref b));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Equ(this VmValue a, ref VmValue b)
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
                return string.Equals(a.ReadTextOrTag(), b.ReadTextOrTag(), StringComparison.Ordinal);
            case Vector or Point when b.Kind is Vector or Point && a.ObjectValue is VmFloatTriplet av && b.ObjectValue is VmFloatTriplet bv:
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
            case List when b.Kind is List && a.ObjectValue is VmListObject av && b.ObjectValue is VmListObject bv:
                if (av.Length != bv.Length) return false;
                for (var i = 0; i < av.Length; i++)
                {
                    if (av.Items[i].Equ(ref bv.Items[i])) continue;
                    return false;
                }

                return true;
            case GameEventScriptBytecodeTypeKind.Range when b.Kind is GameEventScriptBytecodeTypeKind.Range && a.ObjectValue is VmRange ar && b.ObjectValue is VmRange br:
                return ar.from == br.from && ar.to == br.to && ar.step == br.step;
            case GameEventScriptBytecodeTypeKind.Range when b.Kind is GameEventScriptBytecodeTypeKind.Range && a.ObjectValue is VmFloatRange ar && b.ObjectValue is VmFloatRange br:
                return DoubleEqualsUlp(ar.from, br.from) && DoubleEqualsUlp(ar.to, br.to) && DoubleEqualsUlp(ar.step, br.step);
            case Series when b.Kind is Series && a.ObjectValue is GameEventScriptSeriesValue aseries && b.ObjectValue is GameEventScriptSeriesValue bseries:
                return aseries.SignatureId == bseries.SignatureId && aseries.Offset == bseries.Offset;
            case Map or Custom when b.Kind is Map or Custom && a.ObjectValue is VmMapObject am && b.ObjectValue is VmMapObject bm:
                if (am.Length != bm.Length) return false;
                var aKeys = am.KeyList;
                var bKeys = bm.KeyList;
                for (var i = 0; i < aKeys.Length; i++)
                {
                    if (aKeys.Items[i].Equ(ref bKeys.Items[i])) continue;
                    return false;
                }

                var aValues = am.ValueList;
                var bValues = bm.ValueList;
                for (var i = 0; i < aValues.Length; i++)
                {
                    if (aValues.Items[i].Equ(ref bValues.Items[i])) continue;
                    return false;
                }

                return true;
            default:
                if(a.IsNumeric && b.IsNumeric) return DoubleEqualsUlp(a.AsNumeric, b.AsNumeric);
                return false;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long Sum(int[] arr)
    {
        long sum = 0;
        foreach (var v in arr) sum += v;
        return sum;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmNotEqual(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        if (a.Kind is Nothing || b.Kind is Nothing) dst.SetNothing();
        else dst.SetBoolean(!a.Equ(ref b));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmLess(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        if (a.Kind is Nothing || b.Kind is Nothing || a.Unit != b.Unit)
        {
            dst.SetNothing();
        }
        else if (a.Kind is Float or Integer && b.Kind is Float or Integer)
        {
            dst.SetBoolean(a.AsNumeric < b.AsNumeric);
        }
        else if (a.Kind is GameEventScriptBytecodeTypeKind.Boolean && b.Kind is GameEventScriptBytecodeTypeKind.Boolean)
        {
            dst.SetBoolean(a.IsFalse && b.IsTrue);
        }
        else
        {
            dst.SetBoolean(false);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmGreater(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        if (a.Kind is Nothing || b.Kind is Nothing || a.Unit != b.Unit)
        {
            dst.SetNothing();
        }
        else if (a.Kind is Float or Integer && b.Kind is Float or Integer)
        {
            dst.SetBoolean(a.AsNumeric > b.AsNumeric);
        }
        else if (a.Kind is GameEventScriptBytecodeTypeKind.Boolean && b.Kind is GameEventScriptBytecodeTypeKind.Boolean)
        {
            dst.SetBoolean(a.IsTrue && b.IsFalse);
        }
        else
        {
            dst.SetBoolean(false);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmLessOrEqual(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        if (a.Kind is Nothing || b.Kind is Nothing || a.Unit != b.Unit)
        {
            dst.SetNothing();
        }
        else if (a.Kind is Float or Integer && b.Kind is Float or Integer)
        {
            dst.SetBoolean(a.AsNumeric <= b.AsNumeric);
        }
        else if (a.Kind is GameEventScriptBytecodeTypeKind.Boolean && b.Kind is GameEventScriptBytecodeTypeKind.Boolean)
        {
            dst.SetBoolean(a.IsFalse && b.IsTrue || a.IsTrue == b.IsTrue);
        }
        else
        {
            dst.SetBoolean(false);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmGreaterOrEqual(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        if (a.Kind is Nothing || b.Kind is Nothing || a.Unit != b.Unit)
        {
            dst.SetNothing();
        }
        else if (a.Kind is Float or Integer && b.Kind is Float or Integer)
        {
            dst.SetBoolean(a.AsNumeric >= b.AsNumeric);
        }
        else if (a.Kind is GameEventScriptBytecodeTypeKind.Boolean && b.Kind is GameEventScriptBytecodeTypeKind.Boolean)
        {
            dst.SetBoolean(a.IsTrue && b.IsFalse || a.IsTrue == b.IsTrue);
        }
        else
        {
            dst.SetBoolean(false);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
