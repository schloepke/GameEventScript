using System;
using System.Collections.Generic;
using StepH.GameEventScript.Api;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;
using StepH.GameEventScript.Runtime.Values;

namespace StepH.GameEventScript.Runtime.VM;

internal static class GesVmStatePublisher
{
    internal static bool GesVmPublishMessage(this GesVmState vmState, ushort outboundMessageSignatureIndex, GameEventScriptUInt16Slice argumentRegisters, bool publish, GameEventScriptSession session)
    {
        if (outboundMessageSignatureIndex >= vmState.OutboundMessageSignatures.Length) return false;
        var signature = vmState.OutboundMessageSignatures[outboundMessageSignatureIndex];
        if (!signature.IsValid || argumentRegisters.Length != signature.ArgumentNames.Length) return false;
        var values = new GameEventScriptValue[argumentRegisters.Length];
        for (var index = 0; index < argumentRegisters.Length; index++)
        {
            values[index] = GameEventScriptValueFactory.FromVmValue(in vmState.Register(argumentRegisters[index]));
        }

        try
        {
            var arguments = GameEventScriptNamedArguments.CreatePrecomputed(signature.ArgumentNames, values);
            var message = GameEventScriptMessage.CreatePrecomputed(signature.Name, arguments, signature.SignatureId);
            return publish ? session.Publish(message) : session.Emit(message);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
    internal static bool GesVmPublishMessageWithTags(this GesVmState vmState, ushort outboundMessageSignatureIndex, GameEventScriptUInt16Slice argumentRegisters, GameEventScriptUInt16Slice tagRegisters, bool publish, GameEventScriptSession session)
    {
        if (outboundMessageSignatureIndex >= vmState.OutboundMessageSignatures.Length) return false;
        var signature = vmState.OutboundMessageSignatures[outboundMessageSignatureIndex];
        if (!signature.IsValid || argumentRegisters.Length != signature.ArgumentNames.Length) return false;
        var values = new GameEventScriptValue[argumentRegisters.Length];
        for (var index = 0; index < argumentRegisters.Length; index++)
        {
            values[index] = GameEventScriptValueFactory.FromVmValue(in vmState.Register(argumentRegisters[index]));
        }

        var tags = new List<string>(tagRegisters.Length);
        for (var index = 0; index < tagRegisters.Length; index++)
        {
            AddTagsToList(tags, in vmState.Register(tagRegisters[index]));
        }

        try
        {
            var arguments = GameEventScriptNamedArguments.CreatePrecomputed(signature.ArgumentNames, values);
            var message = GameEventScriptMessage.CreatePrecomputed(signature.Name, arguments, signature.SignatureId, tags);
            return publish ? session.Publish(message) : session.Emit(message);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
    internal static bool GesVmPublishMessageValue(this GesVmState vmState, in GesValue messageValue, bool publish, GameEventScriptSession session)
    {
        if (messageValue.Kind is Message && messageValue.ObjectValue is GameEventScriptMessage msg)
        {
            return publish ? session.Publish(msg) : session.Emit(msg);
        }
        return false;
    }
    internal static bool GesVmPublishMessageValueWithTags(this GesVmState vmState, in GesValue messageValue, GameEventScriptUInt16Slice tagRegisters, bool publish, GameEventScriptSession session)
    {
        if (messageValue.Kind is not Message || messageValue.ObjectValue is not GameEventScriptMessage msg) return false;
        var tags = new List<string>(tagRegisters.Length);
        for (var index = 0; index < tagRegisters.Length; index++)
        {
            AddTagsToList(tags, in vmState.Register(tagRegisters[index]));
        }
        return publish ? session.Publish(msg.WithTags(tags)) : session.Emit(msg.WithTags(tags));
    }

    private static void AddTagsToList(List<string> tags, in GesValue value)
    {
        if (value.Kind is List && value.ObjectValue is GesValue[] values)
        {
            for (var index = 0; index < values.Length; index++)
            {
                AddTagsToList(tags, in values[index]);
            }
        }
        else
        {
            tags.Add(value.TextValue.Length > 0 || value.Kind is Text or Tag ? value.TextValue : value.ToText);
        }
    }
}
