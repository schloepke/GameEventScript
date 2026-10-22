// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GesBytecodeTypeKind;

namespace GameEventScript.Runtime.Vm;

internal partial class GesVirtualMachine
{
    private bool Send(GesBytecodeInstruction instruction, GesContext context)
    {
        long microseconds = 0;
        if (instruction.OpCode is GesBytecodeOpCode.EmitAfter or GesBytecodeOpCode.PublishAfter)
        {
            var delay = Register(instruction.BU);
            if (delay.Kind is not (Integer or Float) || delay.Unit != GesQuantityUnit.UnitSecond) return false;
            if (delay.Kind == Integer)
            {
                if (delay.IntegerValue < 0 || delay.IntegerValue > long.MaxValue / 1_000_000) return false;
                microseconds = delay.IntegerValue * 1_000_000;
            }
            else
            {
                var rounded = Math.Ceiling(delay.FloatValue * 1_000_000);
                if (delay.FloatValue < 0 || double.IsNaN(rounded) || rounded < 0 || rounded >= 9223372036854775808d) return false;
                microseconds = (long)rounded;
            }
        }
        var publish = instruction.OpCode is GesBytecodeOpCode.PublishInstant or GesBytecodeOpCode.PublishAfter;
        var tags = (instruction.InstructionFlags & GesInstructionFlag.WithTags) != 0;
        try
        {
            if ((instruction.InstructionFlags & GesInstructionFlag.Indirect) != 0)
            {
                var message = Register(instruction.XRegister);
                return tags ? EmitOrPublishMessageValueWithTags(message, Program.UInt16IndexLists.Resolve(instruction.AU), publish, context, microseconds)
                    : EmitOrPublishMessageValue(message, publish, context, microseconds);
            }
            return tags ? EmitOrPublishMessageWithTags(instruction.BindId, Program.UInt16IndexLists.Resolve(instruction.YRegister), Program.UInt16IndexLists.Resolve(instruction.AU), publish, context, microseconds)
                : EmitOrPublishMessage(instruction.BindId, Program.UInt16IndexLists.Resolve(instruction.YRegister), publish, context, microseconds);
        }
        catch (ArgumentException) { return false; }
    }

    private bool EmitOrPublishMessage(ushort outboundMessageSignatureIndex, GesUInt16IndexList argumentRegisters, bool publish, GesContext context, long? delay = null)
    {
        if (outboundMessageSignatureIndex >= OutboundMessageSignatures.Length) return false;
        var signature = OutboundMessageSignatures[outboundMessageSignatureIndex];
        if (!signature.IsValid || argumentRegisters.Length != signature.ArgumentNames.Count) return false;

        try
        {
            GesMessageArguments arguments;
            if (argumentRegisters.Length == 0)
            {
                arguments = GesMessageArguments.Empty;
            }
            else
            {
                var values = new GesValue[argumentRegisters.Length];
                for (var index = 0; index < argumentRegisters.Length; index++)
                {
                    values[index] = Register(argumentRegisters[index]);
                }

                arguments = GesMessageArguments.CreatePrecomputed(signature.ArgumentNames, values);
            }

            var message = GameEventScriptMessage.CreatePrecomputed(signature.Name, arguments, signature.SignatureId);
            return delay is { } microseconds ? context.Send(message, publish, microseconds) : publish ? context.Publish(message).AnyAccepted : context.Emit(message);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private bool EmitOrPublishMessageWithTags(
        ushort outboundMessageSignatureIndex,
        GesUInt16IndexList argumentRegisters,
        GesUInt16IndexList tagRegisters,
        bool publish,
        GesContext context, long? delay = null)
    {
        if (outboundMessageSignatureIndex >= OutboundMessageSignatures.Length) return false;
        var signature = OutboundMessageSignatures[outboundMessageSignatureIndex];
        if (!signature.IsValid || argumentRegisters.Length != signature.ArgumentNames.Count) return false;

        var tags = new GesTagBuffer(tagRegisters.Length);
        for (var index = 0; index < tagRegisters.Length; index++)
        {
            AddTagsToBuffer(tags, in Register(tagRegisters[index]));
        }

        try
        {
            GesMessageArguments arguments;
            if (argumentRegisters.Length == 0)
            {
                arguments = GesMessageArguments.Empty;
            }
            else
            {
                var values = new GesValue[argumentRegisters.Length];
                for (var index = 0; index < argumentRegisters.Length; index++)
                {
                    values[index] = Register(argumentRegisters[index]);
                }

                arguments = GesMessageArguments.CreatePrecomputed(signature.ArgumentNames, values);
            }

            var message = GameEventScriptMessage.CreatePrecomputedWithNormalizedTags(signature.Name, arguments, signature.SignatureId, tags.ToArrayOrEmpty());
            return delay is { } microseconds ? context.Send(message, publish, microseconds) : publish ? context.Publish(message).AnyAccepted : context.Emit(message);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private bool EmitOrPublishMessageValue(in GesValue messageValue, bool publish, GesContext context, long? delay = null)
    {
        if (messageValue.Kind is Message && messageValue.ObjectValue is GameEventScriptMessage msg)
        {
            return delay is { } microseconds ? context.Send(msg, publish, microseconds) : publish ? context.Publish(msg).AnyAccepted : context.Emit(msg);
        }
        return false;
    }

    private bool EmitOrPublishMessageValueWithTags(in GesValue messageValue, GesUInt16IndexList tagRegisters, bool publish, GesContext context, long? delay = null)
    {
        if (messageValue.Kind is not Message || messageValue.ObjectValue is not GameEventScriptMessage msg) return false;
        var tags = new GesTagBuffer(tagRegisters.Length);
        for (var index = 0; index < tagRegisters.Length; index++)
        {
            AddTagsToBuffer(tags, in Register(tagRegisters[index]));
        }
        var message = msg.WithNormalizedTags(tags.ToArrayOrEmpty());
        return delay is { } microseconds ? context.Send(message, publish, microseconds) : publish ? context.Publish(message).AnyAccepted : context.Emit(message);
    }

    private static void AddTagsToBuffer(GesTagBuffer tags, in GesValue value)
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
