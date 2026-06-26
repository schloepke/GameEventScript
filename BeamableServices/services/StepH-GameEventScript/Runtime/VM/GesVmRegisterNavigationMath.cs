using System;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeInstructionUnit;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.Runtime.VM;

internal static class GesVmRegisterNavigationMath
{
    internal static void GesVmSin(this GesVmState vmState, ushort destinationRegister, in GesVmValue value)
    {
        if (value.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (!TryReadRadians(in value, out var radians))
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        vmState.SetFloat(destinationRegister, Math.Sin(radians));
    }

    internal static void GesVmCos(this GesVmState vmState, ushort destinationRegister, in GesVmValue value)
    {
        if (value.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (!TryReadRadians(in value, out var radians))
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        vmState.SetFloat(destinationRegister, Math.Cos(radians));
    }

    internal static void GesVmTan(this GesVmState vmState, ushort destinationRegister, in GesVmValue value)
    {
        if (value.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (!TryReadRadians(in value, out var radians))
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        vmState.SetFloat(destinationRegister, Math.Tan(radians));
    }

    internal static void GesVmAsin(this GesVmState vmState, ushort destinationRegister, in GesVmValue value)
    {
        if (value.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (!TryReadUnitlessNumeric(in value, out var number) || number < -1d || number > 1d)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        vmState.SetFloat(destinationRegister, Math.Asin(number));
    }

    internal static void GesVmAcos(this GesVmState vmState, ushort destinationRegister, in GesVmValue value)
    {
        if (value.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (!TryReadUnitlessNumeric(in value, out var number) || number < -1d || number > 1d)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        vmState.SetFloat(destinationRegister, Math.Acos(number));
    }

    internal static void GesVmAtan(this GesVmState vmState, ushort destinationRegister, in GesVmValue value)
    {
        if (value.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (!TryReadUnitlessNumeric(in value, out var number))
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        vmState.SetFloat(destinationRegister, Math.Atan(number));
    }

    internal static void GesVmAtan2(this GesVmState vmState, ushort destinationRegister, in GesVmValue y, in GesVmValue x)
    {
        if (y.Kind is Nothing || x.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (!TryReadNumeric(in y, out var yNumber, out var yUnit) ||
            !TryReadNumeric(in x, out var xNumber, out var xUnit) ||
            yUnit != xUnit)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        vmState.SetFloat(destinationRegister, yNumber == 0d && xNumber == 0d ? 0d : Math.Atan2(yNumber, xNumber));
    }

    internal static void GesVmHypot2D(this GesVmState vmState, ushort destinationRegister, in GesVmValue x, in GesVmValue y)
    {
        if (x.Kind is Nothing || y.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (!TryReadSameUnit2(in x, in y, out var xNumber, out var yNumber, out var unit))
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        vmState.SetFloat(destinationRegister, Math.Sqrt(xNumber * xNumber + yNumber * yNumber), unit);
    }

    internal static void GesVmHypot3D(this GesVmState vmState, ushort destinationRegister, in GesVmValue x, in GesVmValue y, in GesVmValue z)
    {
        if (x.Kind is Nothing || y.Kind is Nothing || z.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (!TryReadSameUnit3(in x, in y, in z, out var xNumber, out var yNumber, out var zNumber, out var unit))
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        vmState.SetFloat(destinationRegister, Math.Sqrt(xNumber * xNumber + yNumber * yNumber + zNumber * zNumber), unit);
    }

    internal static void GesVmDistance(this GesVmState vmState, ushort destinationRegister, in GesVmValue left, in GesVmValue right)
    {
        if (left.Kind is Nothing || right.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (left.IsNumeric && right.IsNumeric)
        {
            if (!TryReadSameUnit2(in left, in right, out var a, out var b, out var unit))
            {
                vmState.SetNothing(destinationRegister);
                return;
            }

            vmState.SetFloat(destinationRegister, Math.Abs(a - b), unit);
            return;
        }

        if (!TryReadPair3D(in left, in right, out var lx, out var ly, out var lz, out var rx, out var ry, out var rz))
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        var dx = lx - rx;
        var dy = ly - ry;
        var dz = lz - rz;
        vmState.SetFloat(destinationRegister, Math.Sqrt(dx * dx + dy * dy + dz * dz), left.Unit);
    }

    internal static void GesVmDistance2D(this GesVmState vmState, ushort destinationRegister, in GesVmValue x1, in GesVmValue y1, in GesVmValue x2, in GesVmValue y2)
    {
        if (x1.Kind is Nothing || y1.Kind is Nothing || x2.Kind is Nothing || y2.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (!TryReadSameUnit4(in x1, in y1, in x2, in y2, out var ax, out var ay, out var bx, out var by, out var unit))
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        var dx = ax - bx;
        var dy = ay - by;
        vmState.SetFloat(destinationRegister, Math.Sqrt(dx * dx + dy * dy), unit);
    }

    internal static void GesVmDistance3D(this GesVmState vmState, ushort destinationRegister, in GesVmValue x1, in GesVmValue y1, in GesVmValue z1, in GesVmValue x2, in GesVmValue y2, in GesVmValue z2)
    {
        if (x1.Kind is Nothing || y1.Kind is Nothing || z1.Kind is Nothing || x2.Kind is Nothing || y2.Kind is Nothing || z2.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (!TryReadSameUnit6(in x1, in y1, in z1, in x2, in y2, in z2, out var ax, out var ay, out var az, out var bx, out var by, out var bz, out var unit))
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        var dx = ax - bx;
        var dy = ay - by;
        var dz = az - bz;
        vmState.SetFloat(destinationRegister, Math.Sqrt(dx * dx + dy * dy + dz * dz), unit);
    }

    internal static void GesVmDistanceSquared(this GesVmState vmState, ushort destinationRegister, in GesVmValue left, in GesVmValue right)
    {
        if (left.Kind is Nothing || right.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (left.IsNumeric && right.IsNumeric)
        {
            if (!TryReadSameUnit2(in left, in right, out var a, out var b, out _))
            {
                vmState.SetNothing(destinationRegister);
                return;
            }

            var d = a - b;
            vmState.SetFloat(destinationRegister, d * d);
            return;
        }

        if (!TryReadPair3D(in left, in right, out var lx, out var ly, out var lz, out var rx, out var ry, out var rz))
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        var dx = lx - rx;
        var dy = ly - ry;
        var dz = lz - rz;
        vmState.SetFloat(destinationRegister, dx * dx + dy * dy + dz * dz);
    }

    internal static void GesVmDistanceSquared2D(this GesVmState vmState, ushort destinationRegister, in GesVmValue x1, in GesVmValue y1, in GesVmValue x2, in GesVmValue y2)
    {
        if (x1.Kind is Nothing || y1.Kind is Nothing || x2.Kind is Nothing || y2.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (!TryReadSameUnit4(in x1, in y1, in x2, in y2, out var ax, out var ay, out var bx, out var by, out _))
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        var dx = ax - bx;
        var dy = ay - by;
        vmState.SetFloat(destinationRegister, dx * dx + dy * dy);
    }

    internal static void GesVmDistanceSquared3D(this GesVmState vmState, ushort destinationRegister, in GesVmValue x1, in GesVmValue y1, in GesVmValue z1, in GesVmValue x2, in GesVmValue y2, in GesVmValue z2)
    {
        if (x1.Kind is Nothing || y1.Kind is Nothing || z1.Kind is Nothing || x2.Kind is Nothing || y2.Kind is Nothing || z2.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (!TryReadSameUnit6(in x1, in y1, in z1, in x2, in y2, in z2, out var ax, out var ay, out var az, out var bx, out var by, out var bz, out _))
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        var dx = ax - bx;
        var dy = ay - by;
        var dz = az - bz;
        vmState.SetFloat(destinationRegister, dx * dx + dy * dy + dz * dz);
    }

    internal static void GesVmLengthSquared(this GesVmState vmState, ushort destinationRegister, in GesVmValue value)
    {
        if (value.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        switch (value.Kind)
        {
            case Integer or Float or Percentage or GameEventScriptBytecodeTypeKind.Boolean or Dice:
                if (!value.IsNumeric)
                {
                    vmState.SetNothing(destinationRegister);
                    return;
                }

                var n = value.AsNumeric;
                vmState.SetFloat(destinationRegister, n * n);
                return;
            case Vector or Point when value.ObjectValue is GesVmValueVectorPoint triplet:
                vmState.SetFloat(destinationRegister, triplet.X * triplet.X + triplet.Y * triplet.Y + triplet.Z * triplet.Z);
                return;
            default:
                vmState.SetNothing(destinationRegister);
                return;
        }
    }

    internal static void GesVmLengthSquared2D(this GesVmState vmState, ushort destinationRegister, in GesVmValue x, in GesVmValue y)
    {
        if (x.Kind is Nothing || y.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (!TryReadSameUnit2(in x, in y, out var xNumber, out var yNumber, out _))
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        vmState.SetFloat(destinationRegister, xNumber * xNumber + yNumber * yNumber);
    }

    internal static void GesVmLengthSquared3D(this GesVmState vmState, ushort destinationRegister, in GesVmValue x, in GesVmValue y, in GesVmValue z)
    {
        if (x.Kind is Nothing || y.Kind is Nothing || z.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (!TryReadSameUnit3(in x, in y, in z, out var xNumber, out var yNumber, out var zNumber, out _))
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        vmState.SetFloat(destinationRegister, xNumber * xNumber + yNumber * yNumber + zNumber * zNumber);
    }

    internal static void GesVmNormalize(this GesVmState vmState, ushort destinationRegister, in GesVmValue value)
    {
        if (value.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (value.Kind is not (Vector or Point) || value.ObjectValue is not GesVmValueVectorPoint triplet)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        var length = Math.Sqrt(triplet.X * triplet.X + triplet.Y * triplet.Y + triplet.Z * triplet.Z);
        if (length == 0d || double.IsNaN(length))
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        vmState.SetVector(destinationRegister, triplet.X / length, triplet.Y / length, triplet.Z / length);
    }

    internal static void GesVmNormalize2D(this GesVmState vmState, ushort destinationRegister, in GesVmValue x, in GesVmValue y)
    {
        if (x.Kind is Nothing || y.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (!TryReadSameUnit2(in x, in y, out var xNumber, out var yNumber, out _))
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        var length = Math.Sqrt(xNumber * xNumber + yNumber * yNumber);
        if (length == 0d || double.IsNaN(length))
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        vmState.SetVector(destinationRegister, xNumber / length, yNumber / length, 0d);
    }

    internal static void GesVmNormalize3D(this GesVmState vmState, ushort destinationRegister, in GesVmValue x, in GesVmValue y, in GesVmValue z)
    {
        if (x.Kind is Nothing || y.Kind is Nothing || z.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (!TryReadSameUnit3(in x, in y, in z, out var xNumber, out var yNumber, out var zNumber, out _))
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        var length = Math.Sqrt(xNumber * xNumber + yNumber * yNumber + zNumber * zNumber);
        if (length == 0d || double.IsNaN(length))
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        vmState.SetVector(destinationRegister, xNumber / length, yNumber / length, zNumber / length);
    }

    internal static void GesVmDot(this GesVmState vmState, ushort destinationRegister, in GesVmValue left, in GesVmValue right)
    {
        if (left.Kind is Nothing || right.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (!TryReadPair3D(in left, in right, out var lx, out var ly, out var lz, out var rx, out var ry, out var rz))
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        vmState.SetFloat(destinationRegister, lx * rx + ly * ry + lz * rz);
    }

    internal static void GesVmDot2D(this GesVmState vmState, ushort destinationRegister, in GesVmValue x1, in GesVmValue y1, in GesVmValue x2, in GesVmValue y2)
    {
        if (x1.Kind is Nothing || y1.Kind is Nothing || x2.Kind is Nothing || y2.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (!TryReadSameUnit4(in x1, in y1, in x2, in y2, out var ax, out var ay, out var bx, out var by, out _))
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        vmState.SetFloat(destinationRegister, ax * bx + ay * by);
    }

    internal static void GesVmDot3D(this GesVmState vmState, ushort destinationRegister, in GesVmValue x1, in GesVmValue y1, in GesVmValue z1, in GesVmValue x2, in GesVmValue y2, in GesVmValue z2)
    {
        if (x1.Kind is Nothing || y1.Kind is Nothing || z1.Kind is Nothing || x2.Kind is Nothing || y2.Kind is Nothing || z2.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (!TryReadSameUnit6(in x1, in y1, in z1, in x2, in y2, in z2, out var ax, out var ay, out var az, out var bx, out var by, out var bz, out _))
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        vmState.SetFloat(destinationRegister, ax * bx + ay * by + az * bz);
    }

    internal static void GesVmCross(this GesVmState vmState, ushort destinationRegister, in GesVmValue left, in GesVmValue right)
    {
        if (left.Kind is Nothing || right.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (!TryReadPair3D(in left, in right, out var lx, out var ly, out var lz, out var rx, out var ry, out var rz))
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        vmState.SetVector(destinationRegister, ly * rz - lz * ry, lz * rx - lx * rz, lx * ry - ly * rx);
    }

    internal static void GesVmCross2D(this GesVmState vmState, ushort destinationRegister, in GesVmValue x1, in GesVmValue y1, in GesVmValue x2, in GesVmValue y2)
    {
        if (x1.Kind is Nothing || y1.Kind is Nothing || x2.Kind is Nothing || y2.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (!TryReadSameUnit4(in x1, in y1, in x2, in y2, out var ax, out var ay, out var bx, out var by, out _))
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        vmState.SetFloat(destinationRegister, ax * by - ay * bx);
    }

    internal static void GesVmCross3D(this GesVmState vmState, ushort destinationRegister, in GesVmValue x1, in GesVmValue y1, in GesVmValue z1, in GesVmValue x2, in GesVmValue y2, in GesVmValue z2)
    {
        if (x1.Kind is Nothing || y1.Kind is Nothing || z1.Kind is Nothing || x2.Kind is Nothing || y2.Kind is Nothing || z2.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (!TryReadSameUnit6(in x1, in y1, in z1, in x2, in y2, in z2, out var ax, out var ay, out var az, out var bx, out var by, out var bz, out _))
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        vmState.SetVector(destinationRegister, ay * bz - az * by, az * bx - ax * bz, ax * by - ay * bx);
    }

    internal static void GesVmAngleBetween(this GesVmState vmState, ushort destinationRegister, in GesVmValue left, in GesVmValue right)
    {
        if (left.Kind is Nothing || right.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (!TryReadPair3D(in left, in right, out var lx, out var ly, out var lz, out var rx, out var ry, out var rz))
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        SetAngleBetween(vmState, destinationRegister, lx, ly, lz, rx, ry, rz);
    }

    internal static void GesVmAngleBetween2D(this GesVmState vmState, ushort destinationRegister, in GesVmValue x1, in GesVmValue y1, in GesVmValue x2, in GesVmValue y2)
    {
        if (x1.Kind is Nothing || y1.Kind is Nothing || x2.Kind is Nothing || y2.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (!TryReadSameUnit4(in x1, in y1, in x2, in y2, out var ax, out var ay, out var bx, out var by, out _))
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        SetAngleBetween(vmState, destinationRegister, ax, ay, 0d, bx, by, 0d);
    }

    internal static void GesVmAngleBetween3D(this GesVmState vmState, ushort destinationRegister, in GesVmValue x1, in GesVmValue y1, in GesVmValue z1, in GesVmValue x2, in GesVmValue y2, in GesVmValue z2)
    {
        if (x1.Kind is Nothing || y1.Kind is Nothing || z1.Kind is Nothing || x2.Kind is Nothing || y2.Kind is Nothing || z2.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (!TryReadSameUnit6(in x1, in y1, in z1, in x2, in y2, in z2, out var ax, out var ay, out var az, out var bx, out var by, out var bz, out _))
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        SetAngleBetween(vmState, destinationRegister, ax, ay, az, bx, by, bz);
    }

    private static bool TryReadRadians(in GesVmValue value, out double radians)
    {
        if (!value.IsNumeric || value.Unit.IsNumericUnit() && value.Unit != UnitDegree)
        {
            radians = 0d;
            return false;
        }

        var number = value.AsNumeric;
        radians = value.Unit == UnitDegree
            ? number / 180d * GameEventScriptMathConstants.Pi
            : number;
        return !double.IsNaN(radians);
    }

    private static bool TryReadUnitlessNumeric(in GesVmValue value, out double number)
    {
        if (!value.IsNumeric || value.Unit.IsNumericUnit())
        {
            number = 0d;
            return false;
        }

        number = value.AsNumeric;
        return !double.IsNaN(number);
    }

    private static bool TryReadNumeric(in GesVmValue value, out double number, out GameEventScriptBytecodeInstructionUnit unit)
    {
        if (!value.IsNumeric)
        {
            number = 0d;
            unit = UnitNone;
            return false;
        }

        number = value.AsNumeric;
        unit = value.Unit;
        return !double.IsNaN(number);
    }

    private static bool TryReadSameUnit2(
        in GesVmValue a,
        in GesVmValue b,
        out double aNumber,
        out double bNumber,
        out GameEventScriptBytecodeInstructionUnit unit)
    {
        if (!TryReadNumeric(in a, out aNumber, out var aUnit) ||
            !TryReadNumeric(in b, out bNumber, out var bUnit) ||
            aUnit != bUnit)
        {
            aNumber = 0d;
            bNumber = 0d;
            unit = UnitNone;
            return false;
        }

        unit = aUnit;
        return true;
    }

    private static bool TryReadSameUnit3(
        in GesVmValue a,
        in GesVmValue b,
        in GesVmValue c,
        out double aNumber,
        out double bNumber,
        out double cNumber,
        out GameEventScriptBytecodeInstructionUnit unit)
    {
        if (!TryReadNumeric(in a, out aNumber, out var aUnit) ||
            !TryReadNumeric(in b, out bNumber, out var bUnit) ||
            !TryReadNumeric(in c, out cNumber, out var cUnit) ||
            aUnit != bUnit ||
            bUnit != cUnit)
        {
            aNumber = 0d;
            bNumber = 0d;
            cNumber = 0d;
            unit = UnitNone;
            return false;
        }

        unit = aUnit;
        return true;
    }

    private static bool TryReadSameUnit4(
        in GesVmValue a,
        in GesVmValue b,
        in GesVmValue c,
        in GesVmValue d,
        out double aNumber,
        out double bNumber,
        out double cNumber,
        out double dNumber,
        out GameEventScriptBytecodeInstructionUnit unit)
    {
        if (!TryReadNumeric(in a, out aNumber, out var aUnit) ||
            !TryReadNumeric(in b, out bNumber, out var bUnit) ||
            !TryReadNumeric(in c, out cNumber, out var cUnit) ||
            !TryReadNumeric(in d, out dNumber, out var dUnit) ||
            aUnit != bUnit ||
            bUnit != cUnit ||
            cUnit != dUnit)
        {
            aNumber = 0d;
            bNumber = 0d;
            cNumber = 0d;
            dNumber = 0d;
            unit = UnitNone;
            return false;
        }

        unit = aUnit;
        return true;
    }

    private static bool TryReadSameUnit6(
        in GesVmValue a,
        in GesVmValue b,
        in GesVmValue c,
        in GesVmValue d,
        in GesVmValue e,
        in GesVmValue f,
        out double aNumber,
        out double bNumber,
        out double cNumber,
        out double dNumber,
        out double eNumber,
        out double fNumber,
        out GameEventScriptBytecodeInstructionUnit unit)
    {
        if (!TryReadNumeric(in a, out aNumber, out var aUnit) ||
            !TryReadNumeric(in b, out bNumber, out var bUnit) ||
            !TryReadNumeric(in c, out cNumber, out var cUnit) ||
            !TryReadNumeric(in d, out dNumber, out var dUnit) ||
            !TryReadNumeric(in e, out eNumber, out var eUnit) ||
            !TryReadNumeric(in f, out fNumber, out var fUnit) ||
            aUnit != bUnit ||
            bUnit != cUnit ||
            cUnit != dUnit ||
            dUnit != eUnit ||
            eUnit != fUnit)
        {
            aNumber = 0d;
            bNumber = 0d;
            cNumber = 0d;
            dNumber = 0d;
            eNumber = 0d;
            fNumber = 0d;
            unit = UnitNone;
            return false;
        }

        unit = aUnit;
        return true;
    }

    private static bool TryReadPair3D(
        in GesVmValue left,
        in GesVmValue right,
        out double lx,
        out double ly,
        out double lz,
        out double rx,
        out double ry,
        out double rz)
    {
        if (left.Unit != right.Unit ||
            left.Kind is not (Vector or Point) ||
            right.Kind is not (Vector or Point) ||
            left.ObjectValue is not GesVmValueVectorPoint leftTriplet ||
            right.ObjectValue is not GesVmValueVectorPoint rightTriplet)
        {
            lx = ly = lz = rx = ry = rz = 0d;
            return false;
        }

        lx = leftTriplet.X;
        ly = leftTriplet.Y;
        lz = leftTriplet.Z;
        rx = rightTriplet.X;
        ry = rightTriplet.Y;
        rz = rightTriplet.Z;
        return true;
    }

    private static void SetAngleBetween(GesVmState vmState, ushort destinationRegister, double ax, double ay, double az, double bx, double by, double bz)
    {
        var leftLength = Math.Sqrt(ax * ax + ay * ay + az * az);
        var rightLength = Math.Sqrt(bx * bx + by * by + bz * bz);
        if (leftLength == 0d || rightLength == 0d || double.IsNaN(leftLength) || double.IsNaN(rightLength))
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        var ratio = (ax * bx + ay * by + az * bz) / (leftLength * rightLength);
        if (ratio > 1d) ratio = 1d;
        else if (ratio < -1d) ratio = -1d;
        vmState.SetFloat(destinationRegister, Math.Acos(ratio));
    }
}
