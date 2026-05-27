using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.BytecodeExecutor;

internal static class VmRegisterCollections
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmIteratorNext(ref this VmValue dst, ref VmValue iterator, ushort noMoreAddress, ref VmState state)
    {
        if (iterator is not { Kind: Stream, ObjectValue: IVmStream it }) state.RaiseError("Cannot iterator over non-iterator value");
        else if (!it.TryNext(ref dst)) state.JumpAddress(noMoreAddress);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmIteratorClose(ref this VmValue iterator)
    {
        if (iterator is { Kind: Stream, ObjectValue: IDisposable it }) it.Dispose();
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmCreateList(ref this VmValue dst, ushort slotsIndex, ref VmState vmState)
    {
        var itemSlots = vmState.Binary.Uint16ConstantTable.Resolve(slotsIndex);
        var list = new VmListObject(itemSlots.Length);
        for (var i = 0; i < itemSlots.Length; i++)
        {
            list.Items[i] = vmState.Register(itemSlots[i]);
        }
        dst.SetList(list);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmCreateMap(ref this VmValue dst, ushort keySlotsIndex, ushort itemSlotsIndex, ref VmState vmState)
    {
        var keyNames = vmState.Binary.Uint16ConstantTable.Resolve(keySlotsIndex);
        var itemSlots = vmState.Binary.Uint16ConstantTable.Resolve(itemSlotsIndex);
        if (keyNames.Length != itemSlots.Length)
        {
            dst.SetNothing();
            return;
        }
        var map = new Dictionary<string, VmValue>(itemSlots.Length);
        for (var i = 0; i < itemSlots.Length; i++)
        {
            map[vmState.Binary.TextConstantTable.Resolve(keyNames[i])] = vmState.Register(itemSlots[i]);
        }
        dst.SetMap(new VmMapObject(map));
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
            var list = new VmListObject(builder.Count);
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