using System.Collections.Generic;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.VirtualMachine;

internal static class GesVmRegisterCollections
{
    internal static void GesVmCreateList(ref this GesVmValue dst)
    {
        var state = dst.OwningState;
        var list = new GesVmListObject(dst.OwningState, state.StageLength);
        for (ushort i = 0; i < state.StageLength; i++) list.Items[i] = state.RegisterStaged(i);
        dst.SetList(list);
    }
    internal static void GesVmCreateMap(ref this GesVmValue dst, ushort keyNamesIndex)
    {
        var state = dst.OwningState;
        var keyNames = dst.OwningState.Binary.Uint16ConstantTable.Resolve(keyNamesIndex);
        if (keyNames.Length != state.StageLength)
        {
            dst.SetNothing();
            return;
        }
        var map = new Dictionary<string, GesVmValue>(state.StageLength);
        for (ushort i = 0; i < state.StageLength; i++)
        {
            map[dst.OwningState.Binary.TextConstantTable.Resolve(keyNames[i])] = state.RegisterStaged(i);
        }
        dst.SetMap(new GesVmMapObject(dst.OwningState, map));
    }
    internal static void GesVmCreateListBuilder(ref this GesVmValue dst)
    {
        dst.CreateListBuilder();
    }
    internal static void GesVmListBuilderAdd(ref this GesVmValue listBuilder, ref GesVmValue value)
    {
        if (listBuilder.Kind is ListBuilder && listBuilder.ObjectValue is List<GesVmValue> builder) builder.Add(value);
    }
    internal static void GesVmListBuilderFinish(ref this GesVmValue dst, ref GesVmValue listBuilder)
    {
        if (listBuilder.Kind is ListBuilder && listBuilder.ObjectValue is List<GesVmValue> builder)
        {
            var list = new GesVmListObject(dst.OwningState, builder.Count);
            for (var i = 0; i < builder.Count; i++)
            {
                list.Items[i] = builder[i];
            }
            dst.SetList(list);
            return;
        }
        
        dst.SetNothing();
    }
    
}
