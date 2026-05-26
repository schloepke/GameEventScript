using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using StepH.GameEventScript.Api;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.BytecodeExecutor;

internal static class VmRegisterCollections
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmLength(ref this VmValue dst, ref VmValue a, ref GameEventScriptTextTable textTable)
    {
        switch (a.Kind)
        {
            case Float or Integer or Percentage or GameEventScriptBytecodeTypeKind.Boolean:
                dst.SetInteger(1);
                break;
            case Text or Tag when a.IsStoragePointer:
                dst.SetInteger(textTable.Resolve((ushort)a.IntegerValue).Length);
                break;
            case Text or Tag when a is { IsStorageObject: true, ObjectValue: string text }:
                dst.SetInteger(text.Length);
                break;
            case List or Map or Dice when a.ObjectValue is IVmLengthAccess objectValue:
                dst.SetInteger(objectValue.Length);
                break;
            default:
                dst.SetNothing();
                break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmIteratorNext(ref this VmValue dst, ref VmValue iterator, ushort noMoreAddress, ref VmState state)
    {
        if (iterator is not { Kind: GameEventScriptBytecodeTypeKind.Stream, ObjectValue: IVmStream it }) state.RaiseError("Cannot iterator over non-iterator value");
        else if (!it.TryNext(ref dst)) state.JumpAddress(noMoreAddress);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmIteratorClose(ref this VmValue iterator)
    {
        if (iterator is { Kind: GameEventScriptBytecodeTypeKind.Stream, ObjectValue: IDisposable it }) it.Dispose();
    }
    
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmBuildList(ref this VmValue dst, ushort slotsIndex, ref VmState vmState)
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
    internal static void VmBuildMap(ref this VmValue dst, ushort keySlotsIndex, ushort itemSlotsIndex, ref VmState vmState)
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

}