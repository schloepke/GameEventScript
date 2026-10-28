// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesQuantityUnit;

namespace GameEventScript.Runtime.Vm;

internal class GesVmUnitCalculation
{
    internal static GesQuantityUnit? SameUnit(in GesValue a, in GesValue b)
    {
        if (a.Unit == b.Unit)
        {
            return a.Unit;
        }

        return null;
    }
    internal static GesQuantityUnit? SameUnit(in GesValue a, in GesValue b, in GesValue c)
    {
        if (a.Unit == b.Unit && b.Unit == c.Unit)
        {
            return a.Unit;
        }

        return null;
    }
    // Products and quotients of two quantities follow the closed derivation table; the unitless cases stay inline.
    internal static GesQuantityUnit? ProductUnit(in GesValue a, in GesValue b)
        => a.Unit == UnitNone ? b.Unit : b.Unit == UnitNone ? a.Unit : GesQuantityUnits.Product(a.Unit, b.Unit);

    internal static GesQuantityUnit? QuotientUnit(in GesValue a, in GesValue b)
        => b.Unit == UnitNone ? a.Unit : a.Unit == b.Unit ? UnitNone : GesQuantityUnits.Quotient(a.Unit, b.Unit);

    // Integer division keeps the derivation-free rules: a unitless divisor keeps the unit and equal units cancel.
    internal static GesQuantityUnit? IntegerQuotientUnit(in GesValue a, in GesValue b)
    {
        switch (a.HasUnit)
        {
            case false when !b.HasUnit:
                return UnitNone;
            case true when !b.HasUnit:
                return a.Unit;
            default:
                return a.Unit == b.Unit ? UnitNone : null;
        }
    }
}
