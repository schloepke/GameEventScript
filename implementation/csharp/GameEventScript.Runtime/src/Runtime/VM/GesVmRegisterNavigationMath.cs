// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Api;
using GameEventScript.Runtime;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GameEventScriptBytecodeInstructionUnit;
using static GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace GameEventScript.Runtime.VM;

internal static class GesVmRegisterNavigationMath
{
    internal static void GesVmSin(this GesVmState vmState, ushort destinationRegister, in GesValue value)
    {
        if (value.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (ReadRadians(in value) is not { } radians)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        vmState.SetFloat(destinationRegister, Math.Sin(radians));
    }

    internal static void GesVmCos(this GesVmState vmState, ushort destinationRegister, in GesValue value)
    {
        if (value.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (ReadRadians(in value) is not { } radians)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        vmState.SetFloat(destinationRegister, Math.Cos(radians));
    }

    internal static void GesVmTan(this GesVmState vmState, ushort destinationRegister, in GesValue value)
    {
        if (value.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (ReadRadians(in value) is not { } radians)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        vmState.SetFloat(destinationRegister, Math.Tan(radians));
    }

    internal static void GesVmAsin(this GesVmState vmState, ushort destinationRegister, in GesValue value)
    {
        if (value.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (ReadUnitlessNumeric(in value) is not { } number || number < -1d || number > 1d)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        vmState.SetFloat(destinationRegister, Math.Asin(number));
    }

    internal static void GesVmAcos(this GesVmState vmState, ushort destinationRegister, in GesValue value)
    {
        if (value.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (ReadUnitlessNumeric(in value) is not { } number || number < -1d || number > 1d)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        vmState.SetFloat(destinationRegister, Math.Acos(number));
    }

    internal static void GesVmAtan(this GesVmState vmState, ushort destinationRegister, in GesValue value)
    {
        if (value.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (ReadUnitlessNumeric(in value) is not { } number)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        vmState.SetFloat(destinationRegister, Math.Atan(number));
    }

    internal static void GesVmAtan2(this GesVmState vmState, ushort destinationRegister, in GesValue y, in GesValue x)
    {
        if (y.Kind is Nothing || x.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        var yValue = ReadNumeric(in y);
        var xValue = ReadNumeric(in x);
        if (yValue is not { } yNumber ||
            xValue is not { } xNumber ||
            yNumber.Unit != xNumber.Unit)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        vmState.SetFloat(destinationRegister, yNumber.Number == 0d && xNumber.Number == 0d ? 0d : Math.Atan2(yNumber.Number, xNumber.Number));
    }

    internal static void GesVmHypot2D(this GesVmState vmState, ushort destinationRegister, in GesValue x, in GesValue y)
    {
        if (x.Kind is Nothing || y.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (ReadSameUnit2(in x, in y) is not { } read)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        vmState.SetFloat(destinationRegister, Math.Sqrt(read.A * read.A + read.B * read.B), read.Unit);
    }

    internal static void GesVmHypot3D(this GesVmState vmState, ushort destinationRegister, in GesValue x, in GesValue y, in GesValue z)
    {
        if (x.Kind is Nothing || y.Kind is Nothing || z.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (ReadSameUnit3(in x, in y, in z) is not { } read)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        vmState.SetFloat(destinationRegister, Math.Sqrt(read.A * read.A + read.B * read.B + read.C * read.C), read.Unit);
    }

    internal static void GesVmDistance(this GesVmState vmState, ushort destinationRegister, in GesValue left, in GesValue right)
    {
        if (left.Kind is Nothing || right.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (left.IsNumeric && right.IsNumeric)
        {
            if (ReadSameUnit2(in left, in right) is not { } scalarRead)
            {
                vmState.SetNothing(destinationRegister);
                return;
            }

            vmState.SetFloat(destinationRegister, Math.Abs(scalarRead.A - scalarRead.B), scalarRead.Unit);
            return;
        }

        if (ReadPair3D(in left, in right) is not { } pair)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        var dx = pair.Lx - pair.Rx;
        var dy = pair.Ly - pair.Ry;
        var dz = pair.Lz - pair.Rz;
        vmState.SetFloat(destinationRegister, Math.Sqrt(dx * dx + dy * dy + dz * dz), left.Unit);
    }

    internal static void GesVmDistance2D(this GesVmState vmState, ushort destinationRegister, in GesValue x1, in GesValue y1, in GesValue x2, in GesValue y2)
    {
        if (x1.Kind is Nothing || y1.Kind is Nothing || x2.Kind is Nothing || y2.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (ReadSameUnit4(in x1, in y1, in x2, in y2) is not { } read)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        var dx = read.A - read.C;
        var dy = read.B - read.D;
        vmState.SetFloat(destinationRegister, Math.Sqrt(dx * dx + dy * dy), read.Unit);
    }

    internal static void GesVmDistance3D(this GesVmState vmState, ushort destinationRegister, in GesValue x1, in GesValue y1, in GesValue z1, in GesValue x2, in GesValue y2, in GesValue z2)
    {
        if (x1.Kind is Nothing || y1.Kind is Nothing || z1.Kind is Nothing || x2.Kind is Nothing || y2.Kind is Nothing || z2.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (ReadSameUnit6(in x1, in y1, in z1, in x2, in y2, in z2) is not { } read)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        var dx = read.A - read.D;
        var dy = read.B - read.E;
        var dz = read.C - read.F;
        vmState.SetFloat(destinationRegister, Math.Sqrt(dx * dx + dy * dy + dz * dz), read.Unit);
    }

    internal static void GesVmDistanceSquared(this GesVmState vmState, ushort destinationRegister, in GesValue left, in GesValue right)
    {
        if (left.Kind is Nothing || right.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (left.IsNumeric && right.IsNumeric)
        {
            if (ReadSameUnit2(in left, in right) is not { } scalarRead)
            {
                vmState.SetNothing(destinationRegister);
                return;
            }

            var d = scalarRead.A - scalarRead.B;
            vmState.SetFloat(destinationRegister, d * d);
            return;
        }

        if (ReadPair3D(in left, in right) is not { } pair)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        var dx = pair.Lx - pair.Rx;
        var dy = pair.Ly - pair.Ry;
        var dz = pair.Lz - pair.Rz;
        vmState.SetFloat(destinationRegister, dx * dx + dy * dy + dz * dz);
    }

    internal static void GesVmDistanceSquared2D(this GesVmState vmState, ushort destinationRegister, in GesValue x1, in GesValue y1, in GesValue x2, in GesValue y2)
    {
        if (x1.Kind is Nothing || y1.Kind is Nothing || x2.Kind is Nothing || y2.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (ReadSameUnit4(in x1, in y1, in x2, in y2) is not { } read)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        var dx = read.A - read.C;
        var dy = read.B - read.D;
        vmState.SetFloat(destinationRegister, dx * dx + dy * dy);
    }

    internal static void GesVmDistanceSquared3D(this GesVmState vmState, ushort destinationRegister, in GesValue x1, in GesValue y1, in GesValue z1, in GesValue x2, in GesValue y2, in GesValue z2)
    {
        if (x1.Kind is Nothing || y1.Kind is Nothing || z1.Kind is Nothing || x2.Kind is Nothing || y2.Kind is Nothing || z2.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (ReadSameUnit6(in x1, in y1, in z1, in x2, in y2, in z2) is not { } read)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        var dx = read.A - read.D;
        var dy = read.B - read.E;
        var dz = read.C - read.F;
        vmState.SetFloat(destinationRegister, dx * dx + dy * dy + dz * dz);
    }

    internal static void GesVmLengthSquared(this GesVmState vmState, ushort destinationRegister, in GesValue value)
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
            case Vector or Point when value.ObjectValue is GesValueVectorPoint triplet:
                vmState.SetFloat(destinationRegister, triplet.X * triplet.X + triplet.Y * triplet.Y + triplet.Z * triplet.Z);
                return;
            default:
                vmState.SetNothing(destinationRegister);
                return;
        }
    }

    internal static void GesVmLengthSquared2D(this GesVmState vmState, ushort destinationRegister, in GesValue x, in GesValue y)
    {
        if (x.Kind is Nothing || y.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (ReadSameUnit2(in x, in y) is not { } read)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        vmState.SetFloat(destinationRegister, read.A * read.A + read.B * read.B);
    }

    internal static void GesVmLengthSquared3D(this GesVmState vmState, ushort destinationRegister, in GesValue x, in GesValue y, in GesValue z)
    {
        if (x.Kind is Nothing || y.Kind is Nothing || z.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (ReadSameUnit3(in x, in y, in z) is not { } read)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        vmState.SetFloat(destinationRegister, read.A * read.A + read.B * read.B + read.C * read.C);
    }

    internal static void GesVmNormalize(this GesVmState vmState, ushort destinationRegister, in GesValue value)
    {
        if (value.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (value.Kind is not (Vector or Point) || value.ObjectValue is not GesValueVectorPoint triplet)
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

    internal static void GesVmNormalize2D(this GesVmState vmState, ushort destinationRegister, in GesValue x, in GesValue y)
    {
        if (x.Kind is Nothing || y.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (ReadSameUnit2(in x, in y) is not { } read)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        var length = Math.Sqrt(read.A * read.A + read.B * read.B);
        if (length == 0d || double.IsNaN(length))
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        vmState.SetVector(destinationRegister, read.A / length, read.B / length, 0d);
    }

    internal static void GesVmNormalize3D(this GesVmState vmState, ushort destinationRegister, in GesValue x, in GesValue y, in GesValue z)
    {
        if (x.Kind is Nothing || y.Kind is Nothing || z.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (ReadSameUnit3(in x, in y, in z) is not { } read)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        var length = Math.Sqrt(read.A * read.A + read.B * read.B + read.C * read.C);
        if (length == 0d || double.IsNaN(length))
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        vmState.SetVector(destinationRegister, read.A / length, read.B / length, read.C / length);
    }

    internal static void GesVmDot(this GesVmState vmState, ushort destinationRegister, in GesValue left, in GesValue right)
    {
        if (left.Kind is Nothing || right.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (ReadPair3D(in left, in right) is not { } pair)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        vmState.SetFloat(destinationRegister, pair.Lx * pair.Rx + pair.Ly * pair.Ry + pair.Lz * pair.Rz);
    }

    internal static void GesVmDot2D(this GesVmState vmState, ushort destinationRegister, in GesValue x1, in GesValue y1, in GesValue x2, in GesValue y2)
    {
        if (x1.Kind is Nothing || y1.Kind is Nothing || x2.Kind is Nothing || y2.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (ReadSameUnit4(in x1, in y1, in x2, in y2) is not { } read)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        vmState.SetFloat(destinationRegister, read.A * read.C + read.B * read.D);
    }

    internal static void GesVmDot3D(this GesVmState vmState, ushort destinationRegister, in GesValue x1, in GesValue y1, in GesValue z1, in GesValue x2, in GesValue y2, in GesValue z2)
    {
        if (x1.Kind is Nothing || y1.Kind is Nothing || z1.Kind is Nothing || x2.Kind is Nothing || y2.Kind is Nothing || z2.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (ReadSameUnit6(in x1, in y1, in z1, in x2, in y2, in z2) is not { } read)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        vmState.SetFloat(destinationRegister, read.A * read.D + read.B * read.E + read.C * read.F);
    }

    internal static void GesVmCross(this GesVmState vmState, ushort destinationRegister, in GesValue left, in GesValue right)
    {
        if (left.Kind is Nothing || right.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (ReadPair3D(in left, in right) is not { } pair)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        vmState.SetVector(destinationRegister, pair.Ly * pair.Rz - pair.Lz * pair.Ry, pair.Lz * pair.Rx - pair.Lx * pair.Rz, pair.Lx * pair.Ry - pair.Ly * pair.Rx);
    }

    internal static void GesVmCross2D(this GesVmState vmState, ushort destinationRegister, in GesValue x1, in GesValue y1, in GesValue x2, in GesValue y2)
    {
        if (x1.Kind is Nothing || y1.Kind is Nothing || x2.Kind is Nothing || y2.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (ReadSameUnit4(in x1, in y1, in x2, in y2) is not { } read)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        vmState.SetFloat(destinationRegister, read.A * read.D - read.B * read.C);
    }

    internal static void GesVmCross3D(this GesVmState vmState, ushort destinationRegister, in GesValue x1, in GesValue y1, in GesValue z1, in GesValue x2, in GesValue y2, in GesValue z2)
    {
        if (x1.Kind is Nothing || y1.Kind is Nothing || z1.Kind is Nothing || x2.Kind is Nothing || y2.Kind is Nothing || z2.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (ReadSameUnit6(in x1, in y1, in z1, in x2, in y2, in z2) is not { } read)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        vmState.SetVector(destinationRegister, read.B * read.F - read.C * read.E, read.C * read.D - read.A * read.F, read.A * read.E - read.B * read.D);
    }

    internal static void GesVmAngleBetween(this GesVmState vmState, ushort destinationRegister, in GesValue left, in GesValue right)
    {
        if (left.Kind is Nothing || right.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (ReadPair3D(in left, in right) is not { } pair)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        SetAngleBetween(vmState, destinationRegister, pair.Lx, pair.Ly, pair.Lz, pair.Rx, pair.Ry, pair.Rz);
    }

    internal static void GesVmAngleBetween2D(this GesVmState vmState, ushort destinationRegister, in GesValue x1, in GesValue y1, in GesValue x2, in GesValue y2)
    {
        if (x1.Kind is Nothing || y1.Kind is Nothing || x2.Kind is Nothing || y2.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (ReadSameUnit4(in x1, in y1, in x2, in y2) is not { } read)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        SetAngleBetween(vmState, destinationRegister, read.A, read.B, 0d, read.C, read.D, 0d);
    }

    internal static void GesVmAngleBetween3D(this GesVmState vmState, ushort destinationRegister, in GesValue x1, in GesValue y1, in GesValue z1, in GesValue x2, in GesValue y2, in GesValue z2)
    {
        if (x1.Kind is Nothing || y1.Kind is Nothing || z1.Kind is Nothing || x2.Kind is Nothing || y2.Kind is Nothing || z2.Kind is Nothing)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        if (ReadSameUnit6(in x1, in y1, in z1, in x2, in y2, in z2) is not { } read)
        {
            vmState.SetNothing(destinationRegister);
            return;
        }

        SetAngleBetween(vmState, destinationRegister, read.A, read.B, read.C, read.D, read.E, read.F);
    }

    private static double? ReadRadians(in GesValue value)
    {
        if (!value.IsNumeric || value.Unit.IsNumericUnit() && value.Unit != UnitDegree)
        {
            return null;
        }

        var number = value.AsNumeric;
        var radians = value.Unit == UnitDegree
            ? number / 180d * GameEventScriptMathConstants.Pi
            : number;
        return double.IsNaN(radians) ? null : radians;
    }

    private static double? ReadUnitlessNumeric(in GesValue value)
    {
        if (!value.IsNumeric || value.Unit.IsNumericUnit())
        {
            return null;
        }

        var number = value.AsNumeric;
        return double.IsNaN(number) ? null : number;
    }

    private static (double Number, GameEventScriptBytecodeInstructionUnit Unit)? ReadNumeric(in GesValue value)
    {
        if (!value.IsNumeric)
        {
            return null;
        }

        var number = value.AsNumeric;
        return double.IsNaN(number) ? null : (number, value.Unit);
    }

    private static (double A, double B, GameEventScriptBytecodeInstructionUnit Unit)? ReadSameUnit2(in GesValue a, in GesValue b)
    {
        var aValue = ReadNumeric(in a);
        var bValue = ReadNumeric(in b);
        if (aValue is not { } av ||
            bValue is not { } bv ||
            av.Unit != bv.Unit)
        {
            return null;
        }

        return (av.Number, bv.Number, av.Unit);
    }

    private static (double A, double B, double C, GameEventScriptBytecodeInstructionUnit Unit)? ReadSameUnit3(in GesValue a, in GesValue b, in GesValue c)
    {
        var aValue = ReadNumeric(in a);
        var bValue = ReadNumeric(in b);
        var cValue = ReadNumeric(in c);
        if (aValue is not { } av ||
            bValue is not { } bv ||
            cValue is not { } cv ||
            av.Unit != bv.Unit ||
            bv.Unit != cv.Unit)
        {
            return null;
        }

        return (av.Number, bv.Number, cv.Number, av.Unit);
    }

    private static (double A, double B, double C, double D, GameEventScriptBytecodeInstructionUnit Unit)? ReadSameUnit4(in GesValue a, in GesValue b, in GesValue c, in GesValue d)
    {
        var aValue = ReadNumeric(in a);
        var bValue = ReadNumeric(in b);
        var cValue = ReadNumeric(in c);
        var dValue = ReadNumeric(in d);
        if (aValue is not { } av ||
            bValue is not { } bv ||
            cValue is not { } cv ||
            dValue is not { } dv ||
            av.Unit != bv.Unit ||
            bv.Unit != cv.Unit ||
            cv.Unit != dv.Unit)
        {
            return null;
        }

        return (av.Number, bv.Number, cv.Number, dv.Number, av.Unit);
    }

    private static (double A, double B, double C, double D, double E, double F, GameEventScriptBytecodeInstructionUnit Unit)? ReadSameUnit6(in GesValue a, in GesValue b, in GesValue c, in GesValue d, in GesValue e, in GesValue f)
    {
        var aValue = ReadNumeric(in a);
        var bValue = ReadNumeric(in b);
        var cValue = ReadNumeric(in c);
        var dValue = ReadNumeric(in d);
        var eValue = ReadNumeric(in e);
        var fValue = ReadNumeric(in f);
        if (aValue is not { } av ||
            bValue is not { } bv ||
            cValue is not { } cv ||
            dValue is not { } dv ||
            eValue is not { } ev ||
            fValue is not { } fv ||
            av.Unit != bv.Unit ||
            bv.Unit != cv.Unit ||
            cv.Unit != dv.Unit ||
            dv.Unit != ev.Unit ||
            ev.Unit != fv.Unit)
        {
            return null;
        }

        return (av.Number, bv.Number, cv.Number, dv.Number, ev.Number, fv.Number, av.Unit);
    }

    private static (double Lx, double Ly, double Lz, double Rx, double Ry, double Rz)? ReadPair3D(in GesValue left, in GesValue right)
    {
        if (left.Unit != right.Unit ||
            left.Kind is not (Vector or Point) ||
            right.Kind is not (Vector or Point) ||
            left.ObjectValue is not GesValueVectorPoint leftTriplet ||
            right.ObjectValue is not GesValueVectorPoint rightTriplet)
        {
            return null;
        }

        return (leftTriplet.X, leftTriplet.Y, leftTriplet.Z, rightTriplet.X, rightTriplet.Y, rightTriplet.Z);
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
