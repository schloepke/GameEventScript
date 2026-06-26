using StepH.GameEventScript.Api;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeInstructionUnit;

namespace StepH.GameEventScript.Runtime.VM;

internal class GesVmUnitCalculation
{
    internal static bool TrySameUnit(in GesVmValue a, in GesVmValue b, out GameEventScriptBytecodeInstructionUnit unit)
    {
        if (a.Unit == b.Unit)
        {
            unit = a.Unit;
            return true;
        }

        unit = UnitNone;
        return false;
    }
    internal static bool TrySameUnit(in GesVmValue a, in GesVmValue b, in GesVmValue c, out GameEventScriptBytecodeInstructionUnit unit)
    {
        if (a.Unit == b.Unit && b.Unit == c.Unit)
        {
            unit = a.Unit;
            return true;
        }

        unit = UnitNone;
        return false;
    }
    internal static bool TryProductUnit(in GesVmValue a, in GesVmValue b, out GameEventScriptBytecodeInstructionUnit unit)
    {
        if (a.HasUnit && b.HasUnit)
        {
            unit = UnitNone;
            return false;
        }

        unit = a.Unit is UnitNone ? b.Unit : a.Unit;
        return true;
    }
    internal static bool TryQuotientUnit(in GesVmValue a, in GesVmValue b, out GameEventScriptBytecodeInstructionUnit unit)
    {
        switch (a.HasUnit)
        {
            case false when !b.HasUnit:
                unit = UnitNone;
                return true;
            case true when !b.HasUnit:
                unit = a.Unit;
                return true;
            default:
                unit = UnitNone;
                return a.Unit == b.Unit;
        }
    }

}
