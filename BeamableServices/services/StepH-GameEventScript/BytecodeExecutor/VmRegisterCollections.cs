using System.Collections.Generic;
using System.Runtime.CompilerServices;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.BytecodeExecutor;

internal static class VmRegisterCollections
{

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmCreateList(ref this VmValue dst)
    {
        var state = dst.OwningState;
        var list = new VmListObject(dst.OwningState, state.StageLength);
        for (ushort i = 0; i < state.StageLength; i++)
        {
            list.Items[i] = state.RegisterStaged(i);
        }
        dst.SetList(list);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmCreateMap(ref this VmValue dst, ushort keyNamesIndex)
    {
        var state = dst.OwningState;
        var keyNames = dst.OwningState.Binary.Uint16ConstantTable.Resolve(keyNamesIndex);
        if (keyNames.Length != state.StageLength)
        {
            dst.SetNothing();
            return;
        }
        var map = new Dictionary<string, VmValue>(state.StageLength);
        for (ushort i = 0; i < state.StageLength; i++)
        {
            map[dst.OwningState.Binary.TextConstantTable.Resolve(keyNames[i])] = state.RegisterStaged(i);
        }
        dst.SetMap(new VmMapObject(dst.OwningState, map));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmCreateListBuilder(ref this VmValue dst)
    {
        dst.CreateListBuilder();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmListBuilderAdd(ref this VmValue listBuilder, ref VmValue value)
    {
        if (listBuilder.Kind is not ListBuilder && listBuilder.ObjectValue is List<VmValue> builder)
        {
            builder.Add(value);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmListBuilderFinish(ref this VmValue dst, ref VmValue listBuilder)
    {
        if (listBuilder.Kind is not ListBuilder && listBuilder.ObjectValue is List<VmValue> builder)
        {
            var list = new VmListObject(dst.OwningState, builder.Count);
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
