using System;
using System.Runtime.CompilerServices;
using StepH.GameEventScript.Api;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;
using static StepH.GameEventScript.BytecodeExecutor.VmRegisterTypeCastCheck.NumericKind;
using static StepH.GameEventScript.BytecodeExecutor.VmRegisterUnitCalculation;

namespace StepH.GameEventScript.BytecodeExecutor;

internal static class VmRegisterMath
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmAdd(ref this VmValue dst, ref VmValue a, ref VmValue b, ref GameEventScriptTextTable textTable)
    {
        switch (a.Kind)
        {
            case Vector when b.Kind is Vector:
            {
                if (a.ObjectValue is VmFloatTriplet av && b.ObjectValue is VmFloatTriplet bv && TrySameUnit(ref a, ref b, out var vectorUnit)) dst.SetObject(Vector, new VmFloatTriplet(av.X + bv.X, av.Y + bv.Y, av.Z + bv.Z), vectorUnit);
                else dst.SetFloat(double.NaN);
                return;
            }
            case Vector:
                if (b.Kind is Point) dst.SetFloat(double.NaN);
                else dst.SetNothing();
                return;
            case Point when b.Kind is Vector:
            {
                if (a.ObjectValue is VmFloatTriplet ap && b.ObjectValue is VmFloatTriplet bv && TrySameUnit(ref a, ref b, out var pointUnit)) dst.SetObject(Point, new VmFloatTriplet(ap.X + bv.X, ap.Y + bv.Y, ap.Z + bv.Z), pointUnit);
                else dst.SetFloat(double.NaN);
                return;
            }
            case Point:
                dst.SetFloat(double.NaN);
                return;
            case Integer when b.Kind is Integer:
                if (TrySameUnit(ref a, ref b, out var unit)) dst.SetInteger(a.IntegerValue + b.IntegerValue, unit);
                else dst.SetFloat(double.NaN);
                return;
            case Integer or Float or Percentage when b.Kind is Vector or Point:
                dst.SetFloat(double.NaN);
                return;
            case Float or Percentage when b.Kind is Float or Percentage:
                if (TrySameUnit(ref a, ref b, out unit)) dst.SetFloat(a.FloatValue + b.FloatValue, unit);
                else dst.SetFloat(double.NaN);
                return;
            case Float or Percentage or Integer when b.Kind is Float or Percentage or Integer:
                if (TrySameUnit(ref a, ref b, out unit)) dst.SetFloat(a.AsNumberValue + b.AsNumberValue, unit);
                else dst.SetFloat(double.NaN);
                return;
            case Float or Percentage or Integer:
                switch (b.ReadNumeric(ref textTable, out var bNumber))
                {
                    case NumericFinite:
                        if (TrySameUnit(ref a, ref b, out unit)) dst.SetFloat(a.AsNumberValue + bNumber, unit);
                        else dst.SetFloat(double.NaN);
                        break;
                    case NumericPositiveInfinity or NumericNegativeInfinity or NumericNaN:
                        dst.SetFloat(double.NaN);
                        break;
                    default:
                        dst.SetNothing();
                        break;
                }
                return;
            default:
                switch (b.ReadNumeric(ref textTable, out var aNumber))
                {
                    case NumericFinite:
                        if (TrySameUnit(ref a, ref b, out unit))
                        {
                            switch (b.ReadNumeric(ref textTable, out var bbNumber))
                            {
                                case NumericFinite:
                                    if (TrySameUnit(ref a, ref b, out unit)) dst.SetFloat(aNumber + bbNumber, unit);
                                    else dst.SetFloat(double.NaN);
                                    break;
                                case NumericPositiveInfinity or NumericNegativeInfinity or NumericNaN:
                                    dst.SetFloat(double.NaN);
                                    break;
                                default:
                                    dst.SetNothing();
                                    break;
                            }
                        }
                        break;
                    case NumericPositiveInfinity or NumericNegativeInfinity or NumericNaN:
                        dst.SetFloat(double.NaN);
                        break;
                    default:
                        dst.SetNothing();
                        break;
                }
                return;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmSubtract(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        switch (a.Kind)
        {
            case Vector when b.Kind is Vector:
            {
                if (a.ObjectValue is VmFloatTriplet av && b.ObjectValue is VmFloatTriplet bv && TrySameUnit(ref a, ref b, out var vectorUnit)) dst.SetObject(Vector, new VmFloatTriplet(av.X - bv.X, av.Y - bv.Y, av.Z - bv.Z), vectorUnit);
                else dst.SetFloat(double.NaN);
                return;
            }
            case Vector:
                dst.SetFloat(double.NaN);
                return;
            case Point when b.Kind is Vector:
            {
                if (a.ObjectValue is VmFloatTriplet ap && b.ObjectValue is VmFloatTriplet bv && TrySameUnit(ref a, ref b, out var pointUnit)) dst.SetObject(Point, new VmFloatTriplet(ap.X - bv.X, ap.Y - bv.Y, ap.Z - bv.Z), pointUnit);
                else dst.SetFloat(double.NaN);
                return;
            }
            case Point when b.Kind is Point:
            {
                if (a.ObjectValue is VmFloatTriplet ap && b.ObjectValue is VmFloatTriplet bp && TrySameUnit(ref a, ref b, out var pointUnit)) dst.SetObject(Vector, new VmFloatTriplet(ap.X - bp.X, ap.Y - bp.Y, ap.Z - bp.Z), pointUnit);
                else dst.SetFloat(double.NaN);
                return;
            }
            case Point:
                dst.SetFloat(double.NaN);
                return;
            case Integer or Float or Percentage when b.Kind is Vector or Point:
                dst.SetFloat(double.NaN);
                return;
        }

        if (TrySameUnit(ref a, ref b, out var unit))
        {
            var left = a.AsNumberValue;
            if (!double.IsNaN(left))
            {
                var right = b.AsNumberValue;
                if (!double.IsNaN(right))
                {
                    dst.SetFloat(left - right, unit);
                    return;
                }
            }
        }

        dst.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmMultiply(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        if (a.Kind is Point || b.Kind is Point || (a.Kind is Vector && b.Kind is Vector))
        {
            dst.SetFloat(double.NaN);
            return;
        }

        if (a.Kind is Vector || b.Kind is Vector)
        {
            var vector = a.Kind is Vector ? a.ObjectValue as VmFloatTriplet : b.ObjectValue as VmFloatTriplet;
            var scalar = a.Kind is Vector ? b.AsNumberValue : a.AsNumberValue;
            if (vector is null || double.IsNaN(scalar) || !double.IsFinite(scalar) || !TryProductUnit(ref a, ref b, out var vectorUnit))
            {
                dst.SetFloat(double.NaN);
                return;
            }

            dst.SetObject(Vector, new VmFloatTriplet(vector.X * scalar, vector.Y * scalar, vector.Z * scalar), vectorUnit);
            return;
        }

        if (TryProductUnit(ref a, ref b, out var unit))
        {
            var left = a.AsNumberValue;
            if (!double.IsNaN(left))
            {
                var right = b.AsNumberValue;
                if (!double.IsNaN(right))
                {
                    dst.SetFloat(left * right, unit);
                    return;
                }
            }
        }

        dst.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmDivide(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        if (a.Kind is Point || b.Kind is Vector or Point)
        {
            dst.SetFloat(double.NaN);
            return;
        }

        if (a.Kind is Vector)
        {
            var scalar = b.AsNumberValue;
            if (a.ObjectValue is not VmFloatTriplet vector || double.IsNaN(scalar) || !double.IsFinite(scalar) || scalar == 0.0d || !TryQuotientUnit(ref a, ref b, out var vectorUnit))
            {
                dst.SetFloat(double.NaN);
                return;
            }

            dst.SetObject(Vector, new VmFloatTriplet(vector.X / scalar, vector.Y / scalar, vector.Z / scalar), vectorUnit);
            return;
        }

        if (TryQuotientUnit(ref a, ref b, out var unit))
        {
            var left = a.AsNumberValue;
            if (!double.IsNaN(left))
            {
                var right = b.AsNumberValue;
                if (!double.IsNaN(right) && right != 0.0)
                {
                    dst.SetFloat(left / right, unit);
                    return;
                }
            }
        }

        dst.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmIntegerDivide(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        if (TryQuotientUnit(ref a, ref b, out var unit))
        {
            var left = a.AsNumberValue;
            if (!double.IsNaN(left))
            {
                var right = b.AsNumberValue;
                if (!double.IsNaN(right))
                {
                    var result = Math.Floor(left / right);
                    if (double.IsFinite(result) && result >= long.MinValue && result <= long.MaxValue) dst.SetInteger((long)result, unit);
                    else dst.SetFloat(result, unit);
                    return;
                }
            }
        }

        dst.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmPower(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        var right = b.AsNumberValue;
        if (!double.IsNaN(right))
        {
            if (TryPowerUnit(ref a, right, out var unit))
            {
                var left = a.AsNumberValue;
                if (!double.IsNaN(left))
                {
                    dst.SetFloat(Math.Pow(left, right), unit);
                    return;
                }
            }
        }

        dst.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmModulo(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        if (a.Kind is Vector or Point || b.Kind is Vector or Point)
        {
            dst.SetFloat(double.NaN);
            return;
        }

        if (TrySameUnit(ref a, ref b, out var unit))
        {
            var left = a.AsNumberValue;
            if (!double.IsNaN(left))
            {
                var right = b.AsNumberValue;
                if (!double.IsNaN(right))
                {
                    dst.SetFloat(left - right * Math.Floor(left / right), unit);
                    return;
                }
            }
        }

        dst.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmRemainder(ref this VmValue dst, ref VmValue a, ref VmValue b)
    {
        if (a.Kind is Vector or Point || b.Kind is Vector or Point)
        {
            dst.SetFloat(double.NaN);
            return;
        }

        if (TrySameUnit(ref a, ref b, out var unit))
        {
            var left = a.AsNumberValue;
            if (!double.IsNaN(left))
            {
                var right = b.AsNumberValue;
                if (!double.IsNaN(right))
                {
                    dst.SetFloat(left - right * Math.Truncate(left / right), unit);
                    return;
                }
            }
        }

        dst.SetNothing();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmNegate(ref this VmValue dst, ref VmValue a)
    {
        switch (a.Kind)
        {
            case Vector:
                if (a.ObjectValue is VmFloatTriplet vector) dst.SetObject(Vector, new VmFloatTriplet(-vector.X, -vector.Y, -vector.Z), a.Unit);
                else dst.SetFloat(double.NaN);
                break;
            case Point:
                dst.SetFloat(double.NaN);
                break;
            case Integer:
                dst.SetInteger(-a.IntegerValue, a.Unit);
                break;
            case Float or Percentage:
                dst.SetFloat(-a.FloatValue, a.Unit);
                break;
            default:
                dst.SetNothing();
                break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmAbs(ref this VmValue dst, ref VmValue a)
    {
        switch (a.Kind)
        {
            case Vector:
                if (a.ObjectValue is not VmFloatTriplet vector)
                {
                    dst.SetFloat(double.NaN);
                    break;
                }

                var length = Math.Sqrt(vector.X * vector.X + vector.Y * vector.Y + vector.Z * vector.Z);
                if (double.IsNaN(length) || double.IsInfinity(length)) dst.SetFloat(double.NaN);
                else dst.SetFloat(length, a.Unit);
                break;
            case Point:
                dst.SetFloat(double.NaN);
                break;
            case Integer:
                dst.SetInteger(Math.Abs(a.IntegerValue), a.Unit);
                break;
            case Float or Percentage:
                dst.SetFloat(Math.Abs(a.FloatValue), a.Unit);
                break;
            default:
                dst.SetNothing();
                break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmNaturalLog(ref this VmValue dst, ref VmValue a)
    {
        if (a.Kind is Nothing)
        {
            dst.SetNothing();
        }
        else if (a.HasUnit)
        {
            dst.SetFloat(double.NaN);
        }
        else
        {
            var x = a.AsNumberValue;
            if (double.IsNaN(x) || x < 0d || double.IsNegativeInfinity(x)) dst.SetFloat(double.NaN);
            else if (double.IsPositiveInfinity(x)) dst.SetFloat(double.PositiveInfinity);
            else dst.SetFloat(Math.Log(x));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmClamp(ref this VmValue dst, ref VmValue value, ref VmValue min, ref VmValue max)
    {
        if (TrySameUnit(ref value, ref min, ref max, out var unit))
        {
            if (value.Kind is Integer && min.Kind is Integer && max.Kind is Integer)
            {
                dst.SetInteger(Math.Clamp(value.IntegerValue, min.IntegerValue, max.IntegerValue), unit);
            }
            else
            {
                var x = value.AsNumberValue;
                var a = min.AsNumberValue;
                var b = max.AsNumberValue;
                if (double.IsNaN(x) || double.IsNaN(a) || double.IsNaN(b)) dst.SetNothing();
                else dst.SetFloat(Math.Clamp(x, a, b), unit);
            }
        }
        else
        {
            dst.SetFloat(double.NaN);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmRandom(ref this VmValue dst, ref VmValue from, ref VmValue to, GameEventScriptRandomGenerator randomGenerator)
    {
        switch (from.Kind)
        {
            case Integer when to.Kind is Integer:
                if (TrySameUnit(ref from, ref to, out var unit)) dst.SetInteger(randomGenerator.NextInclusiveInt((int)from.IntegerValue, (int)to.IntegerValue), unit);
                else dst.SetFloat(double.NaN);
                break;
            case Float or Integer or Percentage when to.Kind is Float or Integer or Percentage:
            {
                if (TrySameUnit(ref from, ref to, out unit))
                {
                    var a = from.FloatValue;
                    var b = to.FloatValue;
                    if (double.IsNaN(a) || double.IsNaN(b)) dst.SetFloat(double.NaN);
                    else dst.SetFloat(randomGenerator.NextInclusiveFloat(a, b), unit);
                }
                else dst.SetFloat(double.NaN);

                break;
            }
            default:
                dst.SetNothing();
                break;
        }
    }
}