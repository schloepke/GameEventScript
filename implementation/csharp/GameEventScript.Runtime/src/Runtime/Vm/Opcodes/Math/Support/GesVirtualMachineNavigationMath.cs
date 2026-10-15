// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;
using static GameEventScript.Api.GesQuantityUnit;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private static double? ReadRadians(in GesValue value)
    {
        if (!value.IsNumeric || value.Unit.IsNumericUnit() && value.Unit != UnitDegree)
        {
            return null;
        }

        var number = value.AsNumeric;
        var radians = value.Unit == UnitDegree
            ? number / 180d * GesMathConstants.Pi
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

    private static (double Number, GesQuantityUnit Unit)? ReadNumeric(in GesValue value)
    {
        if (!value.IsNumeric)
        {
            return null;
        }

        var number = value.AsNumeric;
        return double.IsNaN(number) ? null : (number, value.Unit);
    }

    private static (double A, double B, GesQuantityUnit Unit)? ReadSameUnit2(in GesValue a, in GesValue b)
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

    private static (double A, double B, double C, GesQuantityUnit Unit)? ReadSameUnit3(in GesValue a, in GesValue b, in GesValue c)
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

    private static (double A, double B, double C, double D, GesQuantityUnit Unit)? ReadSameUnit4(in GesValue a, in GesValue b, in GesValue c, in GesValue d)
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

    private static (double A, double B, double C, double D, double E, double F, GesQuantityUnit Unit)? ReadSameUnit6(in GesValue a, in GesValue b, in GesValue c, in GesValue d, in GesValue e, in GesValue f)
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
            left.ObjectValue is not GesSpatialValue leftTriplet ||
            right.ObjectValue is not GesSpatialValue rightTriplet)
        {
            return null;
        }

        return (leftTriplet.X, leftTriplet.Y, leftTriplet.Z, rightTriplet.X, rightTriplet.Y, rightTriplet.Z);
    }

    private static void SetAngleBetween(GesVirtualMachine vm, ushort destinationRegister, double ax, double ay, double az, double bx, double by, double bz)
    {
        var leftLength = Math.Sqrt(ax * ax + ay * ay + az * az);
        var rightLength = Math.Sqrt(bx * bx + by * by + bz * bz);
        if (leftLength == 0d || rightLength == 0d || double.IsNaN(leftLength) || double.IsNaN(rightLength))
        {
            vm.SetNothing(destinationRegister);
            return;
        }

        var ratio = (ax * bx + ay * by + az * bz) / (leftLength * rightLength);
        if (ratio > 1d) ratio = 1d;
        else if (ratio < -1d) ratio = -1d;
        vm.SetFloat(destinationRegister, Math.Acos(ratio));
    }
}
