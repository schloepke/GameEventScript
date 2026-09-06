// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime.Values;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace StepH.GameEventScript.Runtime.VM;

internal static class GesVmStatePublisher
{
    internal static bool GesVmPublishMessage(this GesVmState vmState, ushort outboundMessageSignatureIndex, GameEventScriptUInt16IndexList argumentRegisters, bool publish, GameEventScriptContext context)
    {
        if (outboundMessageSignatureIndex >= vmState.OutboundMessageSignatures.Length) return false;
        var signature = vmState.OutboundMessageSignatures[outboundMessageSignatureIndex];
        if (!signature.IsValid || argumentRegisters.Length != signature.ArgumentNames.Length) return false;

        try
        {
            GameEventScriptMessageArguments arguments;
            if (argumentRegisters.Length == 0)
            {
                arguments = GameEventScriptMessageArguments.Empty;
            }
            else
            {
                var values = new GesValue[argumentRegisters.Length];
                for (var index = 0; index < argumentRegisters.Length; index++)
                {
                    values[index] = vmState.Register(argumentRegisters[index]);
                }

                arguments = GameEventScriptMessageArguments.CreatePrecomputed(signature.ArgumentNames, values);
            }

            var message = GameEventScriptMessage.CreatePrecomputed(signature.Name, arguments, signature.SignatureId);
            return publish ? context.Publish(message).AnyAccepted : context.Emit(message);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
    internal static bool GesVmPublishMessageWithTags(
        this GesVmState vmState,
        ushort outboundMessageSignatureIndex,
        GameEventScriptUInt16IndexList argumentRegisters,
        GameEventScriptUInt16IndexList tagRegisters,
        bool publish,
        GameEventScriptContext context)
    {
        if (outboundMessageSignatureIndex >= vmState.OutboundMessageSignatures.Length) return false;
        var signature = vmState.OutboundMessageSignatures[outboundMessageSignatureIndex];
        if (!signature.IsValid || argumentRegisters.Length != signature.ArgumentNames.Length) return false;

        var tags = new GameEventScriptTagBuffer(tagRegisters.Length);
        for (var index = 0; index < tagRegisters.Length; index++)
        {
            AddTagsToBuffer(tags, in vmState.Register(tagRegisters[index]));
        }

        try
        {
            GameEventScriptMessageArguments arguments;
            if (argumentRegisters.Length == 0)
            {
                arguments = GameEventScriptMessageArguments.Empty;
            }
            else
            {
                var values = new GesValue[argumentRegisters.Length];
                for (var index = 0; index < argumentRegisters.Length; index++)
                {
                    values[index] = vmState.Register(argumentRegisters[index]);
                }

                arguments = GameEventScriptMessageArguments.CreatePrecomputed(signature.ArgumentNames, values);
            }

            var message = GameEventScriptMessage.CreatePrecomputedWithNormalizedTags(signature.Name, arguments, signature.SignatureId, tags.ToArrayOrEmpty());
            return publish ? context.Publish(message).AnyAccepted : context.Emit(message);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
    internal static bool GesVmPublishMessageValue(this GesVmState vmState, in GesValue messageValue, bool publish, GameEventScriptContext context)
    {
        if (messageValue.Kind is Message && messageValue.ObjectValue is GameEventScriptMessage msg)
        {
            return publish ? context.Publish(msg).AnyAccepted : context.Emit(msg);
        }
        return false;
    }
    internal static bool GesVmPublishMessageValueWithTags(this GesVmState vmState, in GesValue messageValue, GameEventScriptUInt16IndexList tagRegisters, bool publish, GameEventScriptContext context)
    {
        if (messageValue.Kind is not Message || messageValue.ObjectValue is not GameEventScriptMessage msg) return false;
        var tags = new GameEventScriptTagBuffer(tagRegisters.Length);
        for (var index = 0; index < tagRegisters.Length; index++)
        {
            AddTagsToBuffer(tags, in vmState.Register(tagRegisters[index]));
        }
        var message = msg.WithNormalizedTags(tags.ToArrayOrEmpty());
        return publish ? context.Publish(message).AnyAccepted : context.Emit(message);
    }

    private static void AddTagsToBuffer(GameEventScriptTagBuffer tags, in GesValue value)
    {
        if (value.Kind is List && value.ObjectValue is GesValue[] values)
        {
            for (var index = 0; index < values.Length; index++)
            {
                AddTagsToBuffer(tags, in values[index]);
            }
        }
        else
        {
            tags.Add(value.TextValue.Length > 0 || value.Kind is Text or Tag ? value.TextValue : value.ToText);
        }
    }
}
