using System;
using System.Runtime.CompilerServices;
using StepH.GameEventScript.Api;
using static StepH.GameEventScript.BytecodeExecutor.VmValue.VmValueKind;

namespace StepH.GameEventScript.BytecodeExecutor;

internal static class VmRegisterCollections
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmLength(ref this VmValue dst, ref VmValue a, ref GameEventScriptTextTable textTable)
    {
        switch (a.Kind)
        {
            case Float or Integer or Percentage or VmValue.VmValueKind.Boolean:
                dst.SetInteger(1);
                break;
            case Text or Tag when a.IsStoragePointer():
                dst.SetInteger(textTable.Resolve((ushort)dst.IntegerValue).Length);
                break;
            case Text or Tag when a.IsStorageObject() && a.ObjectValue is string text:
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
        if (iterator is not { Kind: Iterator, ObjectValue: IVmIterator it }) state.RaiseError("Cannot iterator over non-iterator value");
        else if (!it.TryNext(ref dst)) state.JumpAddress(noMoreAddress);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmIteratorClose(ref this VmValue iterator)
    {
        if (iterator is { Kind: Iterator, ObjectValue: IDisposable it }) it.Dispose();
    }
}