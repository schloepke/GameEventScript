using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.VirtualMachine;

internal static class GesVmRegisterCollections
{
    internal static void GesVmCreateList(ref this GesVmValue dst)
    {
        var state = dst.OwningState;
        var list = dst.OwningState.CreateList(state.StageLength);
        for (ushort i = 0; i < state.StageLength; i++) list[i] = state.RegisterStaged(i);
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
        var map = new GesVmValueMapBuilder(dst.OwningState, state.StageLength);
        for (ushort i = 0; i < state.StageLength; i++)
        {
            map.Set(dst.OwningState.Binary.TextConstantTable.Resolve(keyNames[i]), state.RegisterStaged(i));
        }
        dst.SetMap(map.ToMap());
    }
    internal static void GesVmCreateListBuilder(ref this GesVmValue dst)
    {
        dst.CreateListBuilder();
    }
    internal static void GesVmListBuilderAdd(ref this GesVmValue listBuilder, ref GesVmValue value)
    {
        if (listBuilder.Kind is ListBuilder && listBuilder.ObjectValue is GesVmValueListBuilder builder) builder.Add(value);
    }
    internal static void GesVmListBuilderFinish(ref this GesVmValue dst, ref GesVmValue listBuilder)
    {
        if (listBuilder.Kind is ListBuilder && listBuilder.ObjectValue is GesVmValueListBuilder builder)
        {
            dst.SetList(builder.ToList());
            return;
        }
        
        dst.SetNothing();
    }
    
}
