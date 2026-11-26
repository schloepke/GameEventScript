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
        if (instruction.OpCode is GesBytecodeOpCode.EmitAfter or GesBytecodeOpCode.PublishAfter && !TryReadDelay(instruction.BU, out microseconds)) return false;
        var publish = instruction.OpCode is GesBytecodeOpCode.PublishInstant or GesBytecodeOpCode.PublishAfter;
        return SendResolved(instruction, publish, context, microseconds, null, false);
    }

    private bool SendNamed(GesBytecodeInstruction instruction, GesContext context)
    {
        if (!TryReadDelay(instruction.BU, out var microseconds) || !TryReadTimerName(instruction.CU, out var name)) return false;
        var repeat = (instruction.InstructionFlags & GesInstructionFlag.Repeat) != 0;
        if (repeat && microseconds == 0) return false;
        return SendResolved(instruction, instruction.OpCode == GesBytecodeOpCode.PublishNamed, context, microseconds, name, repeat);
    }

    private bool ExecuteCancelTimers(GesBytecodeInstruction instruction, GesContext context)
    {
        object? name = null;
        if (instruction.AU == 0 && !TryReadTimerName(instruction.XRegister, out name)) return false;
        return context.CancelTimers(instruction.AU, name);
    }

    // A Text or Tag names a timer by its text, a handle by itself (specs/HostRuntime.md#named-timers).
    private bool TryReadTimerName(ushort register, out object name)
    {
        ref readonly var key = ref Register(register);
        if (key.Kind is Text or Tag)
        {
            name = key.TextValue;
            return true;
        }

        name = key.ObjectValue as GesHandle ?? (object)"";
        return key.Kind is Handle && key.ObjectValue is GesHandle;
    }

    // A delay is a seconds Quantity rounded up to whole microseconds; negative or unrepresentable delays fail.
    private bool TryReadDelay(ushort register, out long microseconds)
    {
        microseconds = 0;
        ref readonly var delay = ref Register(register);
        if (delay.Kind is not (Integer or Float) || delay.Unit != GesQuantityUnit.UnitSecond) return false;
        if (delay.Kind == Integer)
        {
            if (delay.IntegerValue < 0 || delay.IntegerValue > long.MaxValue / 1_000_000) return false;
            microseconds = delay.IntegerValue * 1_000_000;
            return true;
        }
        var rounded = Math.Ceiling(delay.FloatValue * 1_000_000);
        if (delay.FloatValue < 0 || double.IsNaN(rounded) || rounded < 0 || rounded >= 9223372036854775808d) return false;
        microseconds = (long)rounded;
        return true;
    }

    private bool SendResolved(GesBytecodeInstruction instruction, bool publish, GesContext context, long? delay, object? name, bool repeat)
    {
        var tags = (instruction.InstructionFlags & GesInstructionFlag.WithTags) != 0;
        try
        {
            if ((instruction.InstructionFlags & GesInstructionFlag.Indirect) != 0)
            {
                var message = Register(instruction.XRegister);
                return tags ? EmitOrPublishMessageValueWithTags(message, Program.UInt16IndexLists.Resolve(instruction.AU), publish, context, delay, name, repeat)
                    : EmitOrPublishMessageValue(message, publish, context, delay, name, repeat);
            }
            return tags ? EmitOrPublishMessageWithTags(instruction.BindId, Program.UInt16IndexLists.Resolve(instruction.YRegister), Program.UInt16IndexLists.Resolve(instruction.AU), publish, context, delay, name, repeat)
                : EmitOrPublishMessage(instruction.BindId, Program.UInt16IndexLists.Resolve(instruction.YRegister), publish, context, delay, name, repeat);
        }
        catch (ArgumentException) { return false; }
    }

    private static bool Deliver(GameEventScriptMessage message, bool publish, GesContext context, long? delay, object? name, bool repeat)
        => name is not null ? context.SendNamed(message, publish, delay ?? 0, name, repeat)
            : delay is { } microseconds ? context.Send(message, publish, microseconds)
            : publish ? context.PublishUnchecked(message).AnyAccepted : context.EmitUnchecked(message);

    private bool EmitOrPublishMessage(ushort outboundMessageSignatureIndex, GesUInt16IndexList argumentRegisters, bool publish, GesContext context, long? delay = null, object? name = null, bool repeat = false)
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
            return Deliver(message, publish, context, delay, name, repeat);
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
        GesContext context, long? delay = null, object? name = null, bool repeat = false)
    {
        if (outboundMessageSignatureIndex >= OutboundMessageSignatures.Length) return false;
        var signature = OutboundMessageSignatures[outboundMessageSignatureIndex];
        if (!signature.IsValid || argumentRegisters.Length != signature.ArgumentNames.Count) return false;

        try
        {
            var tags = new GesTagBuffer(tagRegisters.Length);
            for (var index = 0; index < tagRegisters.Length; index++)
            {
                AddTagsToBuffer(tags, in Register(tagRegisters[index]));
            }

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
            return Deliver(message, publish, context, delay, name, repeat);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private bool EmitOrPublishMessageValue(in GesValue messageValue, bool publish, GesContext context, long? delay = null, object? name = null, bool repeat = false)
    {
        if (messageValue.Kind is Message && messageValue.ObjectValue is GameEventScriptMessage msg)
        {
            return Deliver(msg, publish, context, delay, name, repeat);
        }
        return false;
    }

    private bool EmitOrPublishMessageValueWithTags(in GesValue messageValue, GesUInt16IndexList tagRegisters, bool publish, GesContext context, long? delay = null, object? name = null, bool repeat = false)
    {
        if (messageValue.Kind is not Message || messageValue.ObjectValue is not GameEventScriptMessage msg) return false;
        try
        {
            var tags = new GesTagBuffer(tagRegisters.Length);
            for (var index = 0; index < tagRegisters.Length; index++)
            {
                AddTagsToBuffer(tags, in Register(tagRegisters[index]));
            }
            var message = msg.WithNormalizedTags(tags.ToArrayOrEmpty());
            return Deliver(message, publish, context, delay, name, repeat);
        }
        catch (ArgumentException)
        {
            return false;
        }
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
            // `_` is no tag name, so `nothing` makes the tag clause invalid.
            tags.Add(value.IsNothing ? "_" : value.TextValue.Length > 0 || value.Kind is Text or Tag ? value.TextValue : value.ToText);
        }
    }
}
