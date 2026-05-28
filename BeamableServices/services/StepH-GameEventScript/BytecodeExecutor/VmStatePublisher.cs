using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.BytecodeExecutor;

internal static class VmStatePublisher
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool VmPublishMessage(this VmState vmState, ReadOnlySpan<ushort> shape, ReadOnlySpan<ushort> argumentSlots, bool publish, GameEventScriptSession session)
    {
        if (shape.Length == 0 || argumentSlots.Length != shape.Length - 1) return false;
        var messageName = vmState.Binary.TextConstantTable.Resolve(shape[0]);
        var pairs = new KeyValuePair<string, GameEventScriptValue>[argumentSlots.Length];
        for (var index = 0; index < argumentSlots.Length; index++)
        {
            pairs[index] = new KeyValuePair<string, GameEventScriptValue>(vmState.Binary.TextConstantTable.Resolve(shape[index + 1]), vmState.Register(argumentSlots[index]).ToGameEventScriptValue());
        }

        var message = GameEventScriptMessage.Create(messageName, GameEventScriptNamedArguments.CreateOrdered(pairs));
        return publish ? session.Publish(message) : session.Emit(message);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool VmPublishMessageWithTags(this VmState vmState, ReadOnlySpan<ushort> shape, ReadOnlySpan<ushort> argumentSlots, ReadOnlySpan<ushort> tagSlots, bool publish, GameEventScriptSession session)
    {
        if (shape.Length == 0 || argumentSlots.Length != shape.Length - 1) return false;
        var messageName = vmState.Binary.TextConstantTable.Resolve(shape[0]);
        var pairs = new KeyValuePair<string, GameEventScriptValue>[argumentSlots.Length];
        for (var index = 0; index < argumentSlots.Length; index++)
        {
            pairs[index] = new KeyValuePair<string, GameEventScriptValue>(vmState.Binary.TextConstantTable.Resolve(shape[index + 1]), vmState.Register(argumentSlots[index]).ToGameEventScriptValue());
        }

        var tags = new List<string>(tagSlots.Length);
        for (var index = 0; index < tagSlots.Length; index++)
        {
            AddTagsToList(tags, vmState.Register(argumentSlots[index]).ToGameEventScriptValue());
        }

        var message = GameEventScriptMessage.Create(messageName, GameEventScriptNamedArguments.CreateOrdered(pairs), tags);
        return publish ? session.Publish(message) : session.Emit(message);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool VmPublishMessageValue(this VmState vmState, ref VmValue messageSlot, bool publish, GameEventScriptSession session)
    {
        if (messageSlot.Kind is Message && messageSlot.ObjectValue is GameEventScriptMessage msg)
        {
            return publish ? session.Publish(msg) : session.Emit(msg);
        }
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool VmPublishMessageValueWithTags(this VmState vmState, ref VmValue messageSlot, ReadOnlySpan<ushort> tagSlots, bool publish, GameEventScriptSession session)
    {
        if (messageSlot.Kind is not Message || messageSlot.ObjectValue is not GameEventScriptMessage msg) return false;
        var tags = new List<string>(tagSlots.Length);
        for (var index = 0; index < tagSlots.Length; index++)
        {
            AddTagsToList(tags, vmState.Register(tagSlots[index]).ToGameEventScriptValue());
        }
        return publish ? session.Publish(msg.WithTags(tags)) : session.Emit(msg.WithTags(tags));
    }
    

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddTagsToList(List<string> tags, GameEventScriptValue value)
    {
        if (value.IsList())
        {
            foreach (var j in value.AsEnumerable()) AddTagsToList(tags, j);
        }
        else
        {
            tags.Add(value.AsText());
        }
    }
}