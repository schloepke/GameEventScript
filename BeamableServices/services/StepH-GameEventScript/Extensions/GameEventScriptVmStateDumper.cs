#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System.Globalization;
using System.Text;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.VirtualMachine;

namespace StepH.GameEventScript.Extensions;

internal static class GameEventScriptVmStateDumper
{
    internal static string Dump(this GesVmState state)
        => Dump(state, includeInstructionAddresses: true, scriptSource: null);

    internal static string Dump(this GesVmState state, string? scriptSource)
        => Dump(state, includeInstructionAddresses: true, scriptSource: scriptSource);

    internal static string Dump(this GesVmState state, bool includeInstructionAddresses, string? scriptSource)
    {
        var builder = new StringBuilder();
        builder
            .AppendLine("VM State")
            .AppendLine("--------")
            .Append("State: ").AppendLine(state.State.ToString());

        if (!string.IsNullOrWhiteSpace(state.ErrorMessage))
        {
            builder.Append("ErrorMessage: ").AppendLine(state.ErrorMessage);
        }

        builder
            .Append("CodeSegmentSize: ").AppendLine(state.CodeSegmentSize.ToString(CultureInfo.InvariantCulture))
            .Append("MaxRegisterSize: ").AppendLine(state.MaxRegisterSlots.ToString(CultureInfo.InvariantCulture))
            .Append("CurrentRegisterArraySize: ").AppendLine(state.RegisterSlots.Length.ToString(CultureInfo.InvariantCulture))
            .Append("CurrentRandomStackSize: ").AppendLine(state.RandomGeneratorsPointer.ToString(CultureInfo.InvariantCulture))
            .AppendLine();

        AppendProcessingMessage(builder, state.ProcessingMessage);
        AppendCallStack(builder, state);
        AppendStageArea(builder, state);
        AppendCurrentExecutionFrame(builder, state);

        builder
            .AppendLine()
            .AppendLine("Binary")
            .AppendLine("------")
            .Append(state.Binary.Dump(includeInstructionAddresses, scriptSource));

        return builder.ToString();
    }

    private static void AppendProcessingMessage(StringBuilder builder, GameEventScriptMessage? message)
    {
        builder
            .AppendLine("ProcessingMessage")
            .AppendLine("-----------------");

        if (message is null)
        {
            builder.AppendLine("<none>").AppendLine();
            return;
        }

        builder
            .Append("Name: ").AppendLine(message.Name)
            .Append("SignatureId: ").AppendLine(message.SignatureId)
            .Append("Tags: ");

        if (message.Tags.Count == 0)
        {
            builder.AppendLine("[]");
        }
        else
        {
            builder.Append('[');
            for (var i = 0; i < message.Tags.Count; i++)
            {
                if (i > 0) builder.Append(", ");
                builder.Append(':').Append(message.Tags[i]);
            }

            builder.AppendLine("]");
        }

        builder
            .Append("ArgumentCount: ")
            .AppendLine(message.Arguments.Count.ToString(CultureInfo.InvariantCulture))
            .AppendLine("Arguments:");

        if (message.Arguments.Count == 0)
        {
            builder.AppendLine("  <empty>").AppendLine();
            return;
        }

        var index = 0;
        foreach (var argument in message.Arguments)
        {
            var value = argument.Value ?? GameEventScriptBoxedValue.Nothing();
            builder
                .Append("  #").Append(index.ToString(CultureInfo.InvariantCulture))
                .Append(' ')
                .Append(argument.Key)
                .Append(": kind=")
                .Append(value.Kind)
                .Append(" value=\"")
                .Append(Escape(value.ToString()))
                .AppendLine("\"");
            index++;
        }

        builder.AppendLine();
    }

    private static void AppendCallStack(StringBuilder builder, GesVmState state)
    {
        builder
            .Append("CallFrames: ").AppendLine(state.CallStackPointer.ToString(CultureInfo.InvariantCulture))
            .AppendLine("-----------");

        if (state.CallStackPointer == 0)
        {
            builder.AppendLine("<empty>").AppendLine();
            return;
        }

        for (var frameIndex = 0; frameIndex < state.CallStackPointer; frameIndex++)
        {
            var frame = state.CallStack[frameIndex];
            builder
                .Append("CallFrame #").AppendLine(frameIndex.ToString(CultureInfo.InvariantCulture))
                .Append("  InstructionPointer: ").AppendLine(frame.InstructionPointer.ToString(CultureInfo.InvariantCulture))
                .Append("  ResultRegisterIndex: ").AppendLine(FormatOptionalRegister(frame.ResultRegisterIndex))
                .Append("  NormalizeResultAsPredicate: ").AppendLine(frame.NormalizeResultAsPredicate.ToString())
                .Append("  RegisterFrameStart: ").AppendLine(frame.RegisterFrameStart.ToString(CultureInfo.InvariantCulture))
                .Append("  RegisterFrameLength: ").AppendLine(frame.RegisterFrameLength.ToString(CultureInfo.InvariantCulture))
                .AppendLine("  Registers:");

            AppendRegisters(builder, state, frame.RegisterFrameStart, frame.RegisterFrameLength, "    ");
            builder.AppendLine();
        }
    }

    private static void AppendStageArea(StringBuilder builder, GesVmState state)
    {
        var stageStart = state.RegisterFrameStart + state.RegisterFrameLength;
        builder
            .AppendLine("StageArea")
            .AppendLine("---------")
            .Append("StageStart: ").AppendLine(stageStart.ToString(CultureInfo.InvariantCulture))
            .Append("StageLength: ").AppendLine(state.StageLength.ToString(CultureInfo.InvariantCulture))
            .AppendLine("Registers:");

        AppendRegisters(builder, state, stageStart, state.StageLength, "  ");
        builder.AppendLine();
    }

    private static void AppendCurrentExecutionFrame(StringBuilder builder, GesVmState state)
    {
        builder
            .AppendLine("CurrentExecutionFrame")
            .AppendLine("---------------------")
            .Append("InstructionPointer: ").AppendLine(state.InstructionPointer.ToString(CultureInfo.InvariantCulture))
            .Append("RegisterFrameStart: ").AppendLine(state.RegisterFrameStart.ToString(CultureInfo.InvariantCulture))
            .Append("RegisterFrameLength: ").AppendLine(state.RegisterFrameLength.ToString(CultureInfo.InvariantCulture))
            .AppendLine("Registers:");

        AppendRegisters(builder, state, state.RegisterFrameStart, state.RegisterFrameLength, "  ");
    }

    private static void AppendRegisters(StringBuilder builder, GesVmState state, int start, int length, string indent)
    {
        if (length == 0)
        {
            builder.Append(indent).AppendLine("<empty>");
            return;
        }

        var end = start + length;
        if (start < 0 || end > state.RegisterSlots.Length)
        {
            builder
                .Append(indent)
                .Append("invalid register range start=")
                .Append(start.ToString(CultureInfo.InvariantCulture))
                .Append(" length=")
                .AppendLine(length.ToString(CultureInfo.InvariantCulture));
            return;
        }

        for (var absoluteIndex = start; absoluteIndex < end; absoluteIndex++)
        {
            var localIndex = absoluteIndex - start;
            builder
                .Append(indent)
                .Append('r').Append(localIndex.ToString(CultureInfo.InvariantCulture))
                .Append(" @").Append(absoluteIndex.ToString(CultureInfo.InvariantCulture))
                .Append(" = ")
                .AppendLine(FormatRegister(state.RegisterSlots[absoluteIndex]));
        }
    }

    private static string FormatRegister(GesVmValue value)
    {
        var builder = new StringBuilder();
        builder
            .Append(value.Kind)
            .Append(" flags=")
            .Append(value.Flags)
            .Append(" unit=")
            .Append(value.Unit)
            .Append(" value=\"")
            .Append(Escape(value.ConvertToText()))
            .Append('"');

        return builder.ToString();
    }

    private static string FormatOptionalRegister(ushort? registerIndex)
        => registerIndex.HasValue ? "r" + registerIndex.Value.ToString(CultureInfo.InvariantCulture) : "none";

    private static string Escape(string text)
        => text.Replace("\\", "\\\\").Replace("\"", "\\\"", System.StringComparison.Ordinal);
}
