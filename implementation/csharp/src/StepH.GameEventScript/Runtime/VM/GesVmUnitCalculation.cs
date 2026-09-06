// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime.Values;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeInstructionUnit;

namespace StepH.GameEventScript.Runtime.VM;

internal class GesVmUnitCalculation
{
    internal static GameEventScriptBytecodeInstructionUnit? SameUnit(in GesValue a, in GesValue b)
    {
        if (a.Unit == b.Unit)
        {
            return a.Unit;
        }

        return null;
    }
    internal static GameEventScriptBytecodeInstructionUnit? SameUnit(in GesValue a, in GesValue b, in GesValue c)
    {
        if (a.Unit == b.Unit && b.Unit == c.Unit)
        {
            return a.Unit;
        }

        return null;
    }
    internal static GameEventScriptBytecodeInstructionUnit? ProductUnit(in GesValue a, in GesValue b)
    {
        if (a.HasUnit && b.HasUnit)
        {
            return null;
        }

        return a.Unit is UnitNone ? b.Unit : a.Unit;
    }
    internal static GameEventScriptBytecodeInstructionUnit? QuotientUnit(in GesValue a, in GesValue b)
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
