// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GameEventScriptBytecodeTypeKind;

namespace GameEventScript.Runtime.VM;

internal static class GesVmStatePublisher
{
    internal static bool Send(this GesVmState vmState, GameEventScriptBytecodeInstruction instruction, GameEventScriptContext context)
    {
        long microseconds = 0;
        if (instruction.OpCode is GameEventScriptBytecodeOpCode.EmitAfter or GameEventScriptBytecodeOpCode.PublishAfter)
        {
            var delay = vmState.Register(instruction.BU);
            if (delay.Kind is not (Integer or Float) || delay.Unit != GameEventScriptBytecodeInstructionUnit.UnitSecond) return false;
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
        var publish = instruction.OpCode is GameEventScriptBytecodeOpCode.PublishInstant or GameEventScriptBytecodeOpCode.PublishAfter;
        var tags = (instruction.InstructionFlags & GameEventScriptInstructionFlag.WithTags) != 0;
        try
        {
            if ((instruction.InstructionFlags & GameEventScriptInstructionFlag.Indirect) != 0)
            {
                var message = vmState.Register(instruction.XRegister);
                return tags ? vmState.GesVmPublishMessageValueWithTags(message, vmState.Program.UInt16IndexLists.Resolve(instruction.AU), publish, context, microseconds)
                    : vmState.GesVmPublishMessageValue(message, publish, context, microseconds);
            }
            return tags ? vmState.GesVmPublishMessageWithTags(instruction.BindId, vmState.Program.UInt16IndexLists.Resolve(instruction.YRegister), vmState.Program.UInt16IndexLists.Resolve(instruction.AU), publish, context, microseconds)
                : vmState.GesVmPublishMessage(instruction.BindId, vmState.Program.UInt16IndexLists.Resolve(instruction.YRegister), publish, context, microseconds);
        }
        catch (ArgumentException) { return false; }
    }

    internal static bool GesVmPublishMessage(this GesVmState vmState, ushort outboundMessageSignatureIndex, GameEventScriptUInt16IndexList argumentRegisters, bool publish, GameEventScriptContext context, long? delay = null)
    {
        if (outboundMessageSignatureIndex >= vmState.OutboundMessageSignatures.Length) return false;
        var signature = vmState.OutboundMessageSignatures[outboundMessageSignatureIndex];
        if (!signature.IsValid || argumentRegisters.Length != signature.ArgumentNames.Count) return false;

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
            return delay is { } microseconds ? context.Send(message, publish, microseconds) : publish ? context.Publish(message).AnyAccepted : context.Emit(message);
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
        GameEventScriptContext context, long? delay = null)
    {
        if (outboundMessageSignatureIndex >= vmState.OutboundMessageSignatures.Length) return false;
        var signature = vmState.OutboundMessageSignatures[outboundMessageSignatureIndex];
        if (!signature.IsValid || argumentRegisters.Length != signature.ArgumentNames.Count) return false;

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
            return delay is { } microseconds ? context.Send(message, publish, microseconds) : publish ? context.Publish(message).AnyAccepted : context.Emit(message);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
    internal static bool GesVmPublishMessageValue(this GesVmState vmState, in GesValue messageValue, bool publish, GameEventScriptContext context, long? delay = null)
    {
        if (messageValue.Kind is Message && messageValue.ObjectValue is GameEventScriptMessage msg)
        {
            return delay is { } microseconds ? context.Send(msg, publish, microseconds) : publish ? context.Publish(msg).AnyAccepted : context.Emit(msg);
        }
        return false;
    }
    internal static bool GesVmPublishMessageValueWithTags(this GesVmState vmState, in GesValue messageValue, GameEventScriptUInt16IndexList tagRegisters, bool publish, GameEventScriptContext context, long? delay = null)
    {
        if (messageValue.Kind is not Message || messageValue.ObjectValue is not GameEventScriptMessage msg) return false;
        var tags = new GameEventScriptTagBuffer(tagRegisters.Length);
        for (var index = 0; index < tagRegisters.Length; index++)
        {
            AddTagsToBuffer(tags, in vmState.Register(tagRegisters[index]));
        }
        var message = msg.WithNormalizedTags(tags.ToArrayOrEmpty());
        return delay is { } microseconds ? context.Send(message, publish, microseconds) : publish ? context.Publish(message).AnyAccepted : context.Emit(message);
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
