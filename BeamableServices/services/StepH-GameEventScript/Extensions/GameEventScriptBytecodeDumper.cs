using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using StepH.GameEventScript.Api;

namespace StepH.GameEventScript.Extensions;

/// <summary>
/// Provides functionality to generate a human-readable representation of the bytecode
/// within a <see cref="GameEventScriptCompiled"/> instance. This utility is useful for debugging
/// and analyzing the structure, references, and instructions of compiled game event scripts.
/// </summary>
public static class GameEventScriptBytecodeDumper
{
    /// <summary>
    /// Dumps the bytecode of the specified <see cref="GameEventScriptCompiled"/> instance
    /// into a readable string format for debugging and analysis.
    /// </summary>
    public static string DumpBytecode(this GameEventScriptCompiled module)
    {
        var builder = new StringBuilder();
        builder.AppendLine("gameeventscript bytecode v1");
        builder.Append("diagnostics: ").AppendLine(module.Options.EnableDiagnostics ? "on" : "off");
        builder.Append("maxFrameSlots: ").AppendLine(module.MaxFrameSlots.ToString(CultureInfo.InvariantCulture));
        AppendPool(builder, "strings", module.StringPool);
        AppendPool(builder, "signatures", module.Signatures);
        AppendPool(builder, "types", module.TypeMetadata);
        AppendExternalReferences(builder, module);
        AppendConstants(builder, module);
        AppendNamedArgumentLayouts(builder, module);
        AppendSideTables(builder, module);
        AppendCode(builder, module);
        AppendCallables(builder, module);
        AppendTypeDefinitions(builder, module);
        AppendHandlers(builder, module);
        return builder.ToString();
    }

    private static void AppendExternalReferences(StringBuilder builder, GameEventScriptCompiled module)
    {
        builder.Append("externalReferences[").Append(module.ExternalReferences.Count.ToString(CultureInfo.InvariantCulture)).AppendLine("]");
        for (var i = 0; i < module.ExternalReferences.Count; i++)
        {
            builder.Append("    #").Append(i.ToString("D3", CultureInfo.InvariantCulture)).Append(": ")
                .AppendLine(module.ExternalReferences[i].SignatureId);
        }
    }

    private static void AppendConstants(StringBuilder builder, GameEventScriptCompiled module)
    {
        builder.Append("constants[").Append(module.ConstantPool.Count.ToString(CultureInfo.InvariantCulture)).AppendLine("]");
        for (var i = 0; i < module.ConstantPool.Count; i++)
        {
            builder.Append("    #").Append(i.ToString("D3", CultureInfo.InvariantCulture)).Append(": ")
                .AppendLine(module.ConstantPool[i].ToString());
        }
    }

    private static void AppendNamedArgumentLayouts(StringBuilder builder, GameEventScriptCompiled module)
    {
        builder.Append("namedArgumentLayouts[").Append(module.NamedArgumentLayouts.Count.ToString(CultureInfo.InvariantCulture)).AppendLine("]");
        for (var i = 0; i < module.NamedArgumentLayouts.Count; i++)
        {
            builder.Append("    #").Append(i.ToString("D3", CultureInfo.InvariantCulture)).Append(": ")
                .AppendLine(string.Join(", ", module.NamedArgumentLayouts[i]));
        }
    }

    private static void AppendSideTables(StringBuilder builder, GameEventScriptCompiled module)
    {
        AppendOperationLayouts(builder, module);
        AppendPublishLayouts(builder, module);
        AppendIterationSourceLayouts(builder, module);
        AppendLoopLayouts(builder, module);
        AppendSeededRandomBlockLayouts(builder, module);
        AppendSelectorLayouts(builder, module);
        AppendPipelineLayouts(builder, module);
        AppendGeneratedCollectionLayouts(builder, module);
        AppendGuardedChoiceLayouts(builder, module);
    }

    private static void AppendOperationLayouts(StringBuilder builder, GameEventScriptCompiled module)
    {
        builder.Append("operationLayouts[").Append(module.OperationLayouts.Count.ToString(CultureInfo.InvariantCulture)).AppendLine("]");
        for (var i = 0; i < module.OperationLayouts.Count; i++)
        {
            var layout = module.OperationLayouts[i];
            builder.Append("    #").Append(i.ToString("D3", CultureInfo.InvariantCulture)).Append(": ").Append(layout.OpCode);
            AppendText(builder, "name", layout.Name);
            AppendText(builder, "argument", layout.ArgumentName);
            AppendIndex(builder, "external", layout.ExternalReferenceIndex);
            AppendIndex(builder, "namedLayout", layout.NamedArgumentLayoutIndex);
            AppendAddress(builder, "expr", layout.ExpressionEntryAddress);
            AppendAddress(builder, "secondaryExpr", layout.SecondaryExpressionEntryAddress);
            if (layout.Count != 0) AppendIndex(builder, "count", layout.Count);
            if (layout.Flag) builder.Append(" flag=true");
            AppendSlotList(builder, "args", layout.ArgumentSlots);
            AppendSlotList(builder, "params", layout.ParameterSlots);
            AppendStringList(builder, "names", layout.Names);
            AppendNullableStringList(builder, "types", layout.DeclaredTypes);
            builder.Append(" callable=").Append(layout.CallableKind).Append(" cast=").AppendLine(layout.CastKind.ToString());
        }
    }

    private static void AppendPublishLayouts(StringBuilder builder, GameEventScriptCompiled module)
    {
        builder.Append("publishLayouts[").Append(module.PublishLayouts.Count.ToString(CultureInfo.InvariantCulture)).AppendLine("]");
        for (var i = 0; i < module.PublishLayouts.Count; i++)
        {
            var layout = module.PublishLayouts[i];
            builder.Append("    #").Append(i.ToString("D3", CultureInfo.InvariantCulture)).Append(": kind=").Append(layout.Kind);
            AppendText(builder, "message", layout.MessageName);
            AppendText(builder, "signature", layout.SignatureId);
            AppendSlot(builder, "messageSlot", layout.MessageSlot);
            AppendStringList(builder, "args", layout.ArgumentNames);
            AppendSlotList(builder, "argSlots", layout.ArgumentSlots);
            AppendSlotList(builder, "tags", layout.TagSlots);
            builder.AppendLine();
        }
    }

    private static void AppendIterationSourceLayouts(StringBuilder builder, GameEventScriptCompiled module)
    {
        builder.Append("iterationSourceLayouts[").Append(module.IterationSourceLayouts.Count.ToString(CultureInfo.InvariantCulture)).AppendLine("]");
        for (var i = 0; i < module.IterationSourceLayouts.Count; i++)
        {
            var layout = module.IterationSourceLayouts[i];
            builder.Append("    #").Append(i.ToString("D3", CultureInfo.InvariantCulture)).Append(": kind=").Append(layout.Kind);
            AppendSlot(builder, "collection", layout.CollectionSlot);
            AppendSlot(builder, "from", layout.RangeFromSlot);
            AppendSlot(builder, "to", layout.RangeToSlot);
            AppendSlot(builder, "step", layout.RangeStepSlot);
            builder.AppendLine();
        }
    }

    private static void AppendLoopLayouts(StringBuilder builder, GameEventScriptCompiled module)
    {
        builder.Append("loopLayouts[").Append(module.LoopLayouts.Count.ToString(CultureInfo.InvariantCulture)).AppendLine("]");
        for (var i = 0; i < module.LoopLayouts.Count; i++)
        {
            var layout = module.LoopLayouts[i];
            builder.Append("    #").Append(i.ToString("D3", CultureInfo.InvariantCulture)).Append(':');
            AppendSlot(builder, "identifier", layout.IdentifierSlot);
            AppendIndex(builder, "source", layout.IterationSourceLayoutIndex);
            builder.AppendLine();
        }
    }

    private static void AppendSeededRandomBlockLayouts(StringBuilder builder, GameEventScriptCompiled module)
    {
        builder.Append("seededRandomBlockLayouts[").Append(module.SeededRandomBlockLayouts.Count.ToString(CultureInfo.InvariantCulture)).AppendLine("]");
        for (var i = 0; i < module.SeededRandomBlockLayouts.Count; i++)
        {
            builder.Append("    #").Append(i.ToString("D3", CultureInfo.InvariantCulture)).Append(':');
            AppendSlot(builder, "seed", module.SeededRandomBlockLayouts[i].SeedSlot);
            builder.AppendLine();
        }
    }

    private static void AppendSelectorLayouts(StringBuilder builder, GameEventScriptCompiled module)
    {
        builder.Append("selectorLayouts[").Append(module.SelectorLayouts.Count.ToString(CultureInfo.InvariantCulture)).AppendLine("]");
        for (var i = 0; i < module.SelectorLayouts.Count; i++)
        {
            var layout = module.SelectorLayouts[i];
            builder.Append("    #").Append(i.ToString("D3", CultureInfo.InvariantCulture)).Append(": ").Append($"{layout.Kind,-7}");
            AppendSlot(builder, "identifier", layout.IdentifierSlot);
            AppendText(builder, "edge", layout.EdgeMode);
            AppendText(builder, "secondary", layout.SecondaryMode);
            AppendIndex(builder, "count", layout.Count);
            AppendSlot(builder, "secondaryIdentifier", layout.SecondaryIdentifierSlot);
            AppendAddress(builder, "expr", layout.ExpressionEntryAddress);
            AppendAddress(builder, "secondaryExpr", layout.SecondaryExpressionEntryAddress);
            if (layout.Flag)
            {
                builder.Append(" flag=true");
            }

            builder.AppendLine();
        }
    }

    private static void AppendPipelineLayouts(StringBuilder builder, GameEventScriptCompiled module)
    {
        builder.Append("pipelineLayouts[").Append(module.PipelineLayouts.Count.ToString(CultureInfo.InvariantCulture)).AppendLine("]");
        for (var i = 0; i < module.PipelineLayouts.Count; i++)
        {
            var layout = module.PipelineLayouts[i];
            builder.Append("    #").Append(i.ToString("D3", CultureInfo.InvariantCulture)).Append(':');
            AppendSlot(builder, "source", layout.SourceSlot);
            AppendIndexList(builder, "prefixSelectors", layout.PrefixSelectorLayoutIndexes);
            AppendIndex(builder, "terminalSelector", layout.TerminalSelectorLayoutIndex);
            builder.AppendLine();
        }
    }

    private static void AppendGeneratedCollectionLayouts(StringBuilder builder, GameEventScriptCompiled module)
    {
        builder.Append("generatedCollectionLayouts[").Append(module.GeneratedCollectionLayouts.Count.ToString(CultureInfo.InvariantCulture)).AppendLine("]");
        for (var i = 0; i < module.GeneratedCollectionLayouts.Count; i++)
        {
            var layout = module.GeneratedCollectionLayouts[i];
            builder.Append("    #").Append(i.ToString("D3", CultureInfo.InvariantCulture)).Append(": collection=").Append(layout.CollectionType);
            AppendSlot(builder, "identifier", layout.IdentifierSlot);
            AppendIndex(builder, "source", layout.IterationSourceLayoutIndex);
            AppendAddress(builder, "predicate", layout.PredicateEntryAddress);
            AppendAddress(builder, "projection", layout.ProjectionEntryAddress);
            builder.AppendLine();
        }
    }

    private static void AppendGuardedChoiceLayouts(StringBuilder builder, GameEventScriptCompiled module)
    {
        builder.Append("guardedChoiceLayouts[").Append(module.GuardedChoiceLayouts.Count.ToString(CultureInfo.InvariantCulture)).AppendLine("]");
        for (var i = 0; i < module.GuardedChoiceLayouts.Count; i++)
        {
            var layout = module.GuardedChoiceLayouts[i];
            builder.Append("    #").Append(i.ToString("D3", CultureInfo.InvariantCulture)).Append(':');
            AppendIndexList(builder, "values", layout.ValueEntryAddresses);
            AppendIndexList(builder, "conditions", layout.ConditionEntryAddresses);
            AppendAddress(builder, "otherwise", layout.OtherwiseEntryAddress);
            builder.AppendLine();
        }
    }

    private static void AppendCode(StringBuilder builder, GameEventScriptCompiled module)
    {
        builder.Append("code[").Append(module.Code.Count.ToString(CultureInfo.InvariantCulture)).AppendLine("]");
        for (var index = 0; index < module.Code.Count; index++)
        {
            var instruction = module.Code[index];
            builder.Append("    @").Append(index.ToString("0000", CultureInfo.InvariantCulture)).Append(' ').Append($"{instruction.OpCode,-30:G}");
            AppendLinearInstructionOperands(builder, module, instruction);
            builder.AppendLine();
        }
    }

    private static void AppendCallables(StringBuilder builder, GameEventScriptCompiled module)
    {
        var callables = module.Callables.Values.OrderBy(callable => callable.SignatureId, StringComparer.Ordinal).ToArray();
        builder.Append("callables[").Append(callables.Length.ToString(CultureInfo.InvariantCulture)).AppendLine("]");
        for (var i = 0; i < callables.Length; i++)
        {
            var callable = callables[i];
            builder.Append("  callable #").Append(i.ToString(CultureInfo.InvariantCulture))
                .Append(' ').Append(callable.Kind)
                .Append(' ').Append(callable.SignatureId)
                .Append(" entry=@").Append(FormatAddress(callable.EntryAddress))
                .Append(" slots=").Append(callable.LocalSlotCount.ToString(CultureInfo.InvariantCulture))
                .Append(" return=s").Append(callable.ReturnSlot.ToString(CultureInfo.InvariantCulture))
                .Append(" params=[").Append(FormatParameters(callable.Parameters, callable.ParameterTypes)).AppendLine("]");
        }
    }

    private static void AppendTypeDefinitions(StringBuilder builder, GameEventScriptCompiled module)
    {
        var types = module.TypeDefinitions.Values.OrderBy(type => type.Name, StringComparer.Ordinal).ToArray();
        builder.Append("typeDefinitions[").Append(types.Length.ToString(CultureInfo.InvariantCulture)).AppendLine("]");
        for (var i = 0; i < types.Length; i++)
        {
            var type = types[i];
            builder.Append("  type #").Append(i.ToString(CultureInfo.InvariantCulture))
                .Append(' ').Append(type.Name)
                .Append(" fields=").AppendLine(type.Fields.Count.ToString(CultureInfo.InvariantCulture));
            foreach (var field in type.Fields)
            {
                builder.Append("    field ").Append(field.Name).Append(": ").Append(field.TypeName);
                AppendAddress(builder, "minimum", field.MinimumEntryAddress);
                AppendAddress(builder, "maximum", field.MaximumEntryAddress);
                AppendAddress(builder, "computed", field.ComputedEntryAddress);
                builder.AppendLine();
            }
        }
    }

    private static void AppendHandlers(StringBuilder builder, GameEventScriptCompiled module)
    {
        var handlers = module.Handlers.Values
            .SelectMany(group => group)
            .OrderBy(handler => handler.SignatureId, StringComparer.Ordinal)
            .ThenBy(handler => handler.DeclarationOrder)
            .ToArray();
        builder.Append("handlers[").Append(handlers.Length.ToString(CultureInfo.InvariantCulture)).AppendLine("]");
        for (var i = 0; i < handlers.Length; i++)
        {
            var handler = handlers[i];
            builder.Append("  handler #").Append(i.ToString(CultureInfo.InvariantCulture))
                .Append(' ').Append(handler.SignatureId)
                .Append(" dispatch=").Append(handler.DispatchKind)
                .Append(" declarationOrder=").Append(handler.DeclarationOrder.ToString(CultureInfo.InvariantCulture))
                .Append(" params=[").Append(FormatParameters(handler.Parameters, handler.ParameterTypes)).Append(']')
                .Append(" matching=[").Append(FormatTags(handler.RequiredTags)).Append(']')
                .Append(" without=[").Append(FormatTags(handler.ExcludedTags)).Append(']')
                .Append(" entry=@").Append(FormatAddress(handler.EntryAddress))
                .Append(" slots=").AppendLine(handler.LocalSlotCount.ToString(CultureInfo.InvariantCulture));
            if (handler.Slots.Count > 0)
            {
                builder.Append("    slots: ")
                    .AppendLine(string.Join(", ", handler.Slots.OrderBy(pair => pair.Value).Select(pair => $"{pair.Key}=s{pair.Value.ToString(CultureInfo.InvariantCulture)}")));
            }
        }
    }

    private static void AppendLinearInstructionOperands(StringBuilder builder, GameEventScriptCompiled module, GameEventScriptBytecodeInstruction instruction)
    {
        AppendSlot(builder, "dst", instruction.Dest);
        switch (instruction.OpCode)
        {
            case GameEventScriptBytecodeOpCode.BindParameter:
                AppendIndex(builder, "parameter", instruction.A);
                break;
            case GameEventScriptBytecodeOpCode.LoadConstant:
                break;
            case GameEventScriptBytecodeOpCode.LoadSlot:
            case GameEventScriptBytecodeOpCode.CoerceSlot:
            case GameEventScriptBytecodeOpCode.CopySlot:
            case GameEventScriptBytecodeOpCode.Cast:
            case GameEventScriptBytecodeOpCode.TypeCheck:
            case GameEventScriptBytecodeOpCode.MemberAccess:
            case GameEventScriptBytecodeOpCode.Unary:
            case GameEventScriptBytecodeOpCode.PredicateTest:
            case GameEventScriptBytecodeOpCode.JumpIfTrue:
            case GameEventScriptBytecodeOpCode.JumpIfFalse:
            case GameEventScriptBytecodeOpCode.JumpIfNotTrue:
            case GameEventScriptBytecodeOpCode.PublishMessageValue:
                AppendSlot(builder, "src", instruction.A);
                break;
            case GameEventScriptBytecodeOpCode.Return:
                AppendSlot(builder, "src", instruction.A);
                break;
            default:
                AppendSlot(builder, "a", instruction.A);
                AppendSlot(builder, "b", instruction.B);
                AppendSlot(builder, "c", instruction.C);
                break;
        }

        AppendAddress(builder, "target", instruction.Target);
        AppendAddress(builder, "target2", instruction.Target2);
        switch (instruction.OpCode)
        {
            case GameEventScriptBytecodeOpCode.LoadConstant:
                AppendConstant(builder, module, instruction.Data);
                break;
            case GameEventScriptBytecodeOpCode.PublishValue or GameEventScriptBytecodeOpCode.PublishMessageValue:
                AppendIndex(builder, "publishLayout", instruction.Data);
                break;
            case GameEventScriptBytecodeOpCode.ForRange or GameEventScriptBytecodeOpCode.ForCollection:
                AppendIndex(builder, "loopLayout", instruction.Data);
                break;
            case GameEventScriptBytecodeOpCode.SeededRandomBlock:
                AppendIndex(builder, "seededRandomBlockLayout", instruction.Data);
                break;
            case GameEventScriptBytecodeOpCode.Pipeline:
                AppendIndex(builder, "pipelineLayout", instruction.Data);
                break;
            case GameEventScriptBytecodeOpCode.GeneratedCollection:
                AppendIndex(builder, "generatedCollectionLayout", instruction.Data);
                break;
            case GameEventScriptBytecodeOpCode.GuardedChoice:
                AppendIndex(builder, "guardedChoiceLayout", instruction.Data);
                break;
            default:
            {
                if (IsOperationLayoutInstruction(instruction.OpCode))
                {
                    AppendIndex(builder, "operationLayout", instruction.Data);
                }
                else if (instruction.OpCode == GameEventScriptBytecodeOpCode.CoerceSlot)
                {
                    AppendIndex(builder, "type", instruction.Data);
                }
                else
                {
                    AppendIndex(builder, "data", instruction.Data);
                }

                break;
            }
        }
    }

    private static string FormatParameters(IReadOnlyList<string> parameters, IReadOnlyList<string?> parameterTypes)
    {
        var formatted = new string[parameters.Count];
        for (var index = 0; index < parameters.Count; index++)
        {
            var parameter = parameters[index];
            var parameterType = index < parameterTypes.Count ? parameterTypes[index] : null;
            formatted[index] = string.IsNullOrEmpty(parameterType)
                ? parameter
                : $"{parameter} as :{parameterType}";
        }

        return string.Join(", ", formatted);
    }

    private static string FormatTags(IReadOnlyList<string> tags)
        => string.Join(", ", tags.Select(tag => $":{tag}"));

    private static void AppendConstant(StringBuilder builder, GameEventScriptCompiled module, int index)
    {
        if (index < 0)
        {
            return;
        }

        builder.Append(" constant=#").Append(index.ToString(CultureInfo.InvariantCulture));
        if ((uint)index < (uint)module.ConstantPool.Count)
        {
            builder.Append('(').Append(module.ConstantPool[index]).Append(')');
        }
    }

    private static void AppendIndex(StringBuilder builder, string name, int value)
    {
        if (value >= 0)
        {
            builder.Append(' ').Append(name).Append('=').Append(value.ToString(CultureInfo.InvariantCulture));
        }
    }

    private static void AppendText(StringBuilder builder, string name, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            builder.Append(' ').Append(name).Append('=').Append(value);
        }
    }

    private static void AppendSlot(StringBuilder builder, string name, int value)
    {
        if (value >= 0)
        {
            builder.Append(' ').Append(name).Append("=S[").Append($"{value:D2}]");
        }
    }

    private static void AppendSlotList(StringBuilder builder, string name, IReadOnlyList<int> values)
    {
        if (values.Count > 0)
        {
            builder.Append(' ').Append(name).Append("=[")
                .Append(string.Join(", ", values.Select(value => value < 0 ? "none" : $"s{value.ToString(CultureInfo.InvariantCulture)}")))
                .Append(']');
        }
    }

    private static void AppendIndexList(StringBuilder builder, string name, IReadOnlyList<int> values)
    {
        if (values.Count > 0)
        {
            builder.Append(' ').Append(name).Append("=[")
                .Append(string.Join(", ", values.Select(value => value < 0 ? "none" : value.ToString(CultureInfo.InvariantCulture))))
                .Append(']');
        }
    }

    private static void AppendStringList(StringBuilder builder, string name, IReadOnlyList<string> values)
    {
        if (values.Count > 0)
        {
            builder.Append(' ').Append(name).Append("=[")
                .Append(string.Join(", ", values))
                .Append(']');
        }
    }

    private static void AppendNullableStringList(StringBuilder builder, string name, IReadOnlyList<string?> values)
    {
        if (values.Count > 0)
        {
            builder.Append(' ').Append(name).Append("=[")
                .Append(string.Join(", ", values.Select(value => string.IsNullOrEmpty(value) ? "none" : value)))
                .Append(']');
        }
    }

    private static void AppendAddress(StringBuilder builder, string name, int value)
    {
        if (value >= 0)
        {
            builder.Append(' ').Append(name).Append("=@").Append(FormatAddress(value));
        }
    }

    private static string FormatAddress(int value) => value < 0 ? "none" : value.ToString("0000", CultureInfo.InvariantCulture);

    private static bool IsOperationLayoutInstruction(GameEventScriptBytecodeOpCode opCode)
        => opCode is
            GameEventScriptBytecodeOpCode.Cast or
            GameEventScriptBytecodeOpCode.TypeCheck or
            GameEventScriptBytecodeOpCode.MemberAccess or
            GameEventScriptBytecodeOpCode.Unary or
            GameEventScriptBytecodeOpCode.Variadic or
            GameEventScriptBytecodeOpCode.Range or
            GameEventScriptBytecodeOpCode.SeededRandom or
            GameEventScriptBytecodeOpCode.TypeConstructor or
            GameEventScriptBytecodeOpCode.BuildList or
            GameEventScriptBytecodeOpCode.BuildSequence or
            GameEventScriptBytecodeOpCode.BuildSet or
            GameEventScriptBytecodeOpCode.BuildDictionary or
            GameEventScriptBytecodeOpCode.BuildMessage or
            GameEventScriptBytecodeOpCode.BindHandler or
            GameEventScriptBytecodeOpCode.CallExtension or
            GameEventScriptBytecodeOpCode.Call or
            GameEventScriptBytecodeOpCode.PredicateTest;

    private static void AppendPool(StringBuilder builder, string name, IReadOnlyList<string> values)
    {
        builder.Append(name).Append('[').Append(values.Count.ToString(CultureInfo.InvariantCulture)).AppendLine("]");
        for (var i = 0; i < values.Count; i++)
        {
            builder.Append("    #").Append(i.ToString("D3", CultureInfo.InvariantCulture)).Append(": ").AppendLine(values[i]);
        }
    }
}
