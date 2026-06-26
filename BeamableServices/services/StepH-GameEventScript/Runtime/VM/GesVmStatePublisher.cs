using System;
using System.Collections.Generic;
using StepH.GameEventScript.Api;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.Runtime.VM;

internal static class GesVmStatePublisher
{
    internal static bool GesVmPublishMessage(this GesVmState vmState, ushort outboundMessageSignatureIndex, ReadOnlySpan<ushort> argumentRegisters, bool publish, GameEventScriptSession session)
    {
        if (outboundMessageSignatureIndex >= vmState.OutboundMessageSignatures.Length) return false;
        var signature = vmState.OutboundMessageSignatures[outboundMessageSignatureIndex];
        if (signature.Kind != GameEventScriptBinaryBindKind.OutboundMessage || argumentRegisters.Length != signature.ArgumentNames.Count) return false;
        var messageName = vmState.FetchStringByPointer(signature.Name);
        var pairs = new KeyValuePair<string, GameEventScriptValue>[argumentRegisters.Length];
        for (var index = 0; index < argumentRegisters.Length; index++)
        {
            pairs[index] = new KeyValuePair<string, GameEventScriptValue>(vmState.FetchStringByPointer(signature.ArgumentNames[index]), GameEventScriptValueFactory.FromVmValue(in vmState.Register(argumentRegisters[index])));
        }

        try
        {
            var message = GameEventScriptMessage.Create(messageName, GameEventScriptNamedArguments.CreateOrdered(pairs));
            return publish ? session.Publish(message) : session.Emit(message);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
    internal static bool GesVmPublishMessageWithTags(this GesVmState vmState, ushort outboundMessageSignatureIndex, ReadOnlySpan<ushort> argumentRegisters, ReadOnlySpan<ushort> tagRegisters, bool publish, GameEventScriptSession session)
    {
        if (outboundMessageSignatureIndex >= vmState.OutboundMessageSignatures.Length) return false;
        var signature = vmState.OutboundMessageSignatures[outboundMessageSignatureIndex];
        if (signature.Kind != GameEventScriptBinaryBindKind.OutboundMessage || argumentRegisters.Length != signature.ArgumentNames.Count) return false;
        var messageName = vmState.FetchStringByPointer(signature.Name);
        var pairs = new KeyValuePair<string, GameEventScriptValue>[argumentRegisters.Length];
        for (var index = 0; index < argumentRegisters.Length; index++)
        {
            pairs[index] = new KeyValuePair<string, GameEventScriptValue>(vmState.FetchStringByPointer(signature.ArgumentNames[index]), GameEventScriptValueFactory.FromVmValue(in vmState.Register(argumentRegisters[index])));
        }

        var tags = new List<string>(tagRegisters.Length);
        for (var index = 0; index < tagRegisters.Length; index++)
        {
            AddTagsToList(tags, GameEventScriptValueFactory.FromVmValue(in vmState.Register(tagRegisters[index])));
        }

        try
        {
            var message = GameEventScriptMessage.Create(messageName, GameEventScriptNamedArguments.CreateOrdered(pairs), tags);
            return publish ? session.Publish(message) : session.Emit(message);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
    internal static bool GesVmPublishMessageValue(this GesVmState vmState, ref GesVmValue messageValue, bool publish, GameEventScriptSession session)
    {
        if (messageValue.Kind is Message && messageValue.ObjectValue is GameEventScriptMessage msg)
        {
            return publish ? session.Publish(msg) : session.Emit(msg);
        }
        return false;
    }
    internal static bool GesVmPublishMessageValueWithTags(this GesVmState vmState, ref GesVmValue messageValue, ReadOnlySpan<ushort> tagRegisters, bool publish, GameEventScriptSession session)
    {
        if (messageValue.Kind is not Message || messageValue.ObjectValue is not GameEventScriptMessage msg) return false;
        var tags = new List<string>(tagRegisters.Length);
        for (var index = 0; index < tagRegisters.Length; index++)
        {
            AddTagsToList(tags, GameEventScriptValueFactory.FromVmValue(in vmState.Register(tagRegisters[index])));
        }
        return publish ? session.Publish(msg.WithTags(tags)) : session.Emit(msg.WithTags(tags));
    }
    private static void AddTagsToList(List<string> tags, GameEventScriptValue value)
    {
        if (value.Kind is List)
        {
            foreach (var j in value.AsList()) AddTagsToList(tags, j);
        }
        else
        {
            tags.Add(value.Text);
        }
    }
}
