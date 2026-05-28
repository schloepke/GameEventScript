using System.Runtime.CompilerServices;
using StepH.GameEventScript.Api;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.BytecodeExecutor;

internal static class VmRegisterMemberIndexAccess
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmMemberAccess(ref this VmValue dst, ushort memberNameIndex, ref VmValue obj)
    {
        var key = dst.OwningState.Binary.TextConstantTable.Resolve(memberNameIndex);
        switch (obj.Kind)
        {
            case Map or Custom when obj.ObjectValue is VmMapObject map && map.TryGet(key, out var value):
                dst = value;
                return;
            case Vector or Point when obj.ObjectValue is VmFloatTriplet vp && vp.TryGet(key, out var value):
                dst.SetFloat(value, obj.Unit);
                return;
            case Message when obj.ObjectValue is GameEventScriptMessage message:
                // FIXME: Need to implement the member access for messages.
            case Handler when obj.ObjectValue is GameEventScriptMessageSignature signature:
                // FIXME: Need to implement the member access for message signatures.
            case Envelope:
                // FIXME: Need to implement the member access for envelopes.
            default:
                dst.SetNothing();
                return;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmIndexAccess(ref this VmValue dst, long indexIn, ref VmValue obj)
    {
        if (indexIn <= 0)
        {
            dst.SetNothing();
            return;
        }

        var index = (int)indexIn - 1;
        switch (obj.Kind)
        {
            case List when obj.ObjectValue is VmListObject map && map.TryGet(index, out var value):
                dst = value;
                return;
            case Dice when obj.ObjectValue is int[] dices:
                if (index < dices.Length) dst.SetInteger(dices[index]);
                else dst.SetNothing();
                return;
            case Vector or Point when obj.ObjectValue is VmFloatTriplet vp && vp.TryGet(index, out var value):
                dst.SetFloat(value, obj.Unit);
                return;
            case Range when obj.ObjectValue is VmRange integerRange:
                var intRangeValue = integerRange.from + index * integerRange.step;
                if (integerRange.step > 0 && intRangeValue <= integerRange.to || integerRange.step < 0 && intRangeValue >= integerRange.to) dst.SetInteger(intRangeValue);
                else dst.SetNothing();
                return;
            case Range when obj.ObjectValue is VmFloatRange floatRange:
                var floatRangeValue = floatRange.from + index * floatRange.step;
                if (floatRange.step > 0 && floatRangeValue <= floatRange.to || floatRange.step < 0 && floatRangeValue >= floatRange.to) dst.SetFloat(floatRangeValue);
                else dst.SetNothing();
                return;
            case Text or Tag when obj.IsStoragePointer:
                var resolvedText = dst.OwningState.Binary.TextConstantTable.Resolve((ushort)obj.IntegerValue);
                if (index < resolvedText.Length) dst.SetText(resolvedText[index].ToString());
                else dst.SetNothing();
                return;
            case Text or Tag when obj is { IsStorageObject: true, ObjectValue: string text}:
                if (index < text.Length) dst.SetText(text[index].ToString());
                else dst.SetNothing();
                return;
            case Message when obj.ObjectValue is GameEventScriptMessage message:
            // FIXME: Need to implement the index access for message signatures.
            case Handler when obj.ObjectValue is GameEventScriptMessageSignature signature:
            // FIXME: Need to implement the index access for message signatures.
            case Envelope:
            // FIXME: Need to implement the index access for envelopes.
            default:
                dst.SetNothing();
                return;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void VmPropertyAccess(ref this VmValue dst, ref VmValue property, ref VmValue obj)
    {
        if (property.TryGetInteger(out var indexIn))
        {
            dst.VmIndexAccess(indexIn, ref obj);
            return;
        }

        switch (property.Kind)
        {
            case Text or Tag when property.IsStoragePointer:
                dst.VmMemberAccess((ushort)property.IntegerValue, ref obj);
                return;
            case Text or Tag when property.ObjectValue is string key:
                switch (obj.Kind)
                {
                    case Map or Custom when obj.ObjectValue is VmMapObject map && map.TryGet(key, out var value):
                        dst = value;
                        return;
                    case Vector or Point when obj.ObjectValue is VmFloatTriplet vp && vp.TryGet(key, out var value):
                        dst.SetFloat(value, obj.Unit);
                        return;
                    default:
                        dst.SetNothing();
                        return;
                }
            default:
                dst.SetNothing();
                return;
        }
    }

}
