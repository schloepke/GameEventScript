using System.Runtime.CompilerServices;
using static StepH.GameEventScript.BytecodeExecutor.VmListObject;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.BytecodeExecutor;

internal static class VmRegisterDice
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmDice(ref this VmValue dst, short count, short sides, ref VmState state)
    {
        if (count <= 0 || sides <= 0)
        {
            dst.SetObject(List, Empty);
            return;
        }

        var list = new VmListObject(count);
        for (var i = 0; i < count; i++) list.Items[i].SetInteger(state.RandomGenerator.NextInclusiveInt(1, sides));
        dst.SetObject(List, list);
    }

}
