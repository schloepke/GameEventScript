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
        builder.Append("maxStackDepth: ").AppendLine(module.MaxStackDepth.ToString(CultureInfo.InvariantCulture));
        AppendPool(builder, "strings", module.StringPool);
        AppendPool(builder, "signatures", module.Signatures);
        AppendPool(builder, "types", module.TypeMetadata);
        AppendExternalReferences(builder, module);
        AppendConstants(builder, module);
        AppendNamedArgumentLayouts(builder, module);
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
            builder.Append("  #").Append(i.ToString(CultureInfo.InvariantCulture)).Append(": ")
                .AppendLine(module.ExternalReferences[i].SignatureId);
        }
    }

    private static void AppendConstants(StringBuilder builder, GameEventScriptCompiled module)
    {
        builder.Append("constants[").Append(module.ConstantPool.Count.ToString(CultureInfo.InvariantCulture)).AppendLine("]");
        for (var i = 0; i < module.ConstantPool.Count; i++)
        {
            builder.Append("  #").Append(i.ToString(CultureInfo.InvariantCulture)).Append(": ")
                .AppendLine(module.ConstantPool[i].ToString());
        }
    }

    private static void AppendNamedArgumentLayouts(StringBuilder builder, GameEventScriptCompiled module)
    {
        builder.Append("namedArgumentLayouts[").Append(module.NamedArgumentLayouts.Count.ToString(CultureInfo.InvariantCulture)).AppendLine("]");
        for (var i = 0; i < module.NamedArgumentLayouts.Count; i++)
        {
            builder.Append("  #").Append(i.ToString(CultureInfo.InvariantCulture)).Append(": ")
                .AppendLine(string.Join(", ", module.NamedArgumentLayouts[i]));
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
                .Append(" params=[").Append(string.Join(", ", callable.Parameters)).AppendLine("]");
            AppendExpressionProgram(builder, module, callable.ExpressionProgram, 4, "expression");
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
                builder.Append("    field ").Append(field.Name).Append(": ").AppendLine(field.TypeName);
                AppendExpressionProgram(builder, module, field.MinimumProgram, 6, "minimum");
                AppendExpressionProgram(builder, module, field.MaximumProgram, 6, "maximum");
                AppendExpressionProgram(builder, module, field.ComputedProgram, 6, "computed");
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
                .Append(" declarationOrder=").Append(handler.DeclarationOrder.ToString(CultureInfo.InvariantCulture))
                .Append(" slots=").AppendLine(handler.ExecutionPlan.SlotCount.ToString(CultureInfo.InvariantCulture));
            if (handler.ExecutionPlan.Slots.Count > 0)
            {
                builder.Append("    slots: ")
                    .AppendLine(string.Join(", ", handler.ExecutionPlan.Slots.OrderBy(pair => pair.Value).Select(pair => $"{pair.Key}=s{pair.Value.ToString(CultureInfo.InvariantCulture)}")));
            }

            AppendStatementProgram(builder, module, handler.ExecutionPlan.StatementProgram, 4, "statements");
        }
    }

    private static void AppendStatementProgram(
        StringBuilder builder,
        GameEventScriptCompiled module,
        GameEventScriptBytecodeStatementProgram? program,
        int indent,
        string label)
    {
        if (program is null)
        {
            return;
        }

        AppendIndent(builder, indent)
            .Append(label)
            .Append('[').Append(program.Statements.Length.ToString(CultureInfo.InvariantCulture)).Append(']')
            .Append(" scope=").Append(program.CreatesScope ? "block" : "shared")
            .AppendLine();

        for (var index = 0; index < program.Statements.Length; index++)
        {
            var statement = program.Statements[index];
            AppendIndent(builder, indent + 2)
                .Append(index.ToString("0000", CultureInfo.InvariantCulture))
                .Append(' ')
                .Append(statement.Kind);
            AppendStatementOperands(builder, statement);
            builder.AppendLine();

            AppendExpressionProgram(builder, module, statement.ExpressionProgram, indent + 4, "expression");
            AppendPublishLayout(builder, module, statement.PublishLayout, indent + 4);
            AppendIterationSource(builder, module, statement.IterationSource, indent + 4);
            AppendStatementProgram(builder, module, statement.ThenProgram, indent + 4, "then");
            AppendStatementProgram(builder, module, statement.ElseProgram, indent + 4, "else");
            AppendStatementProgram(builder, module, statement.BodyProgram, indent + 4, "body");
        }
    }

    private static void AppendStatementOperands(StringBuilder builder, GameEventScriptBytecodeStatement statement)
    {
        AppendValue(builder, "name", statement.Name);
        AppendValue(builder, "type", statement.DeclaredType);
        AppendValue(builder, "diagnostic", statement.DiagnosticName);
    }

    private static void AppendPublishLayout(
        StringBuilder builder,
        GameEventScriptCompiled module,
        GameEventScriptBytecodePublishLayout? layout,
        int indent)
    {
        if (layout is null)
        {
            return;
        }

        AppendIndent(builder, indent)
            .Append("Publish ")
            .Append(layout.SignatureId)
            .Append(" args=[")
            .Append(string.Join(", ", layout.ArgumentNames))
            .AppendLine("]");
        for (var index = 0; index < layout.ArgumentPrograms.Length; index++)
        {
            AppendExpressionProgram(builder, module, layout.ArgumentPrograms[index], indent + 2, $"arg:{layout.ArgumentNames[index]}");
        }
    }

    private static void AppendIterationSource(
        StringBuilder builder,
        GameEventScriptCompiled module,
        GameEventScriptBytecodeIterationSourceProgram? source,
        int indent)
    {
        if (source is null)
        {
            return;
        }

        AppendIndent(builder, indent).Append("iteration ").AppendLine(source.Kind.ToString());
        AppendExpressionProgram(builder, module, source.CollectionProgram, indent + 2, "collection");
        AppendExpressionProgram(builder, module, source.RangeFromProgram, indent + 2, "from");
        AppendExpressionProgram(builder, module, source.RangeToProgram, indent + 2, "to");
        AppendExpressionProgram(builder, module, source.RangeStepProgram, indent + 2, "step");
    }

    private static void AppendExpressionProgram(
        StringBuilder builder,
        GameEventScriptCompiled module,
        GameEventScriptBytecodeExpressionProgram? program,
        int indent,
        string label)
    {
        if (program is null)
        {
            return;
        }

        AppendIndent(builder, indent)
            .Append(label)
            .Append(" instructions=").Append(program.Instructions.Length.ToString(CultureInfo.InvariantCulture))
            .Append(" maxStack=").AppendLine(program.MaxStackDepth.ToString(CultureInfo.InvariantCulture));
        for (var index = 0; index < program.Instructions.Length; index++)
        {
            var instruction = program.Instructions[index];
            AppendIndent(builder, indent + 2)
                .Append(index.ToString("0000", CultureInfo.InvariantCulture))
                .Append(' ')
                .Append(instruction.OpCode);
            AppendInstructionOperands(builder, module, instruction);
            builder.AppendLine();
            AppendExpressionProgram(builder, module, instruction.ExpressionProgram, indent + 4, "nestedExpression");
            AppendPipelineProgram(builder, module, instruction.PipelineProgram, indent + 4);
            AppendGeneratedCollectionProgram(builder, module, instruction.GeneratedCollectionProgram, indent + 4);
            AppendGuardedChoiceProgram(builder, module, instruction.GuardedChoiceProgram, indent + 4);
        }
    }

    private static void AppendInstructionOperands(StringBuilder builder, GameEventScriptCompiled module, GameEventScriptBytecodeInstruction instruction)
    {
        AppendIndex(builder, "a", instruction.A);
        AppendIndex(builder, "b", instruction.B);
        AppendConstant(builder, module, instruction.ConstantIndex);
        if (instruction.OpCode == GameEventScriptBytecodeOpCode.Cast)
        {
            builder.Append(" cast=").Append(instruction.CastKind);
        }

        if (instruction.OpCode == GameEventScriptBytecodeOpCode.Call)
        {
            builder.Append(" callableKind=").Append(instruction.CallableKind);
        }

        AppendValue(builder, "name", instruction.DiagnosticName);
        AppendValue(builder, "arg", instruction.DiagnosticArgumentName);
        if (instruction.Names is { Length: > 0 })
        {
            builder.Append(" names=[").Append(string.Join(", ", instruction.Names)).Append(']');
        }

        if (instruction.Slots is { Length: > 0 })
        {
            builder.Append(" slots=[").Append(string.Join(", ", instruction.Slots.Select(slot => slot.ToString(CultureInfo.InvariantCulture)))).Append(']');
        }
    }

    private static void AppendPipelineProgram(
        StringBuilder builder,
        GameEventScriptCompiled module,
        GameEventScriptBytecodePipelineProgram? program,
        int indent)
    {
        if (program is null)
        {
            return;
        }

        AppendIndent(builder, indent).AppendLine("pipeline");
        AppendExpressionProgram(builder, module, program.SourceProgram, indent + 2, "source");
        foreach (var selector in program.PrefixSelectors)
        {
            AppendSelector(builder, module, selector, indent + 2, "prefix");
        }

        AppendSelector(builder, module, program.TerminalSelector, indent + 2, "terminal");
    }

    private static void AppendSelector(
        StringBuilder builder,
        GameEventScriptCompiled module,
        GameEventScriptBytecodeSelectorProgram selector,
        int indent,
        string label)
    {
        AppendIndent(builder, indent)
            .Append(label)
            .Append(' ')
            .Append(selector.Kind)
            .Append(" slot=").Append(selector.IdentifierSlot.ToString(CultureInfo.InvariantCulture));
        AppendValue(builder, "mode", selector.EdgeMode);
        AppendValue(builder, "secondaryMode", selector.SecondaryMode);
        if (selector.Count != 0)
        {
            builder.Append(" count=").Append(selector.Count.ToString(CultureInfo.InvariantCulture));
        }

        builder.AppendLine();
        AppendExpressionProgram(builder, module, selector.ExpressionProgram, indent + 2, "expression");
        AppendExpressionProgram(builder, module, selector.SecondaryExpressionProgram, indent + 2, "secondary");
        AppendDicePattern(builder, module, selector.DicePattern, indent + 2);
        AppendObjectPattern(builder, module, selector.ObjectPattern, indent + 2);
    }

    private static void AppendGeneratedCollectionProgram(
        StringBuilder builder,
        GameEventScriptCompiled module,
        GameEventScriptBytecodeGeneratedCollectionProgram? program,
        int indent)
    {
        if (program is null)
        {
            return;
        }

        AppendIndent(builder, indent)
            .Append("generatedCollection ")
            .Append(program.CollectionType)
            .Append(" slot=").AppendLine(program.IdentifierSlot.ToString(CultureInfo.InvariantCulture));
        AppendIterationSource(builder, module, program.Source, indent + 2);
        AppendExpressionProgram(builder, module, program.PredicateProgram, indent + 2, "predicate");
        AppendExpressionProgram(builder, module, program.ProjectionProgram, indent + 2, "projection");
    }

    private static void AppendGuardedChoiceProgram(
        StringBuilder builder,
        GameEventScriptCompiled module,
        GameEventScriptBytecodeGuardedChoiceProgram? program,
        int indent)
    {
        if (program is null)
        {
            return;
        }

        AppendIndent(builder, indent)
            .Append("guardedChoice branches=")
            .AppendLine(program.ValuePrograms.Length.ToString(CultureInfo.InvariantCulture));
        for (var index = 0; index < program.ValuePrograms.Length; index++)
        {
            AppendExpressionProgram(builder, module, program.ConditionPrograms[index], indent + 2, $"condition:{index}");
            AppendExpressionProgram(builder, module, program.ValuePrograms[index], indent + 2, $"value:{index}");
        }

        AppendExpressionProgram(builder, module, program.OtherwiseProgram, indent + 2, "otherwise");
    }

    private static void AppendDicePattern(
        StringBuilder builder,
        GameEventScriptCompiled module,
        GameEventScriptBytecodeDicePattern? pattern,
        int indent)
    {
        switch (pattern)
        {
            case null:
                return;
            case GameEventScriptBytecodeFullHousePattern:
                AppendIndent(builder, indent).AppendLine("dicePattern fullHouse");
                return;
            case GameEventScriptBytecodeStraightPattern:
                AppendIndent(builder, indent).AppendLine("dicePattern straight");
                return;
            case GameEventScriptBytecodeDiceCountPattern count:
                AppendIndent(builder, indent)
                    .Append("dicePattern count=")
                    .AppendLine(count.Count.ToString(CultureInfo.InvariantCulture));
                AppendExpressionProgram(builder, module, count.FaceProgram, indent + 2, "face");
                return;
        }
    }

    private static void AppendObjectPattern(
        StringBuilder builder,
        GameEventScriptCompiled module,
        GameEventScriptBytecodeObjectMatchPattern? pattern,
        int indent)
    {
        if (pattern is null)
        {
            return;
        }

        AppendIndent(builder, indent)
            .Append("objectPattern entries=")
            .AppendLine(pattern.Entries.Length.ToString(CultureInfo.InvariantCulture));
        foreach (var entry in pattern.Entries)
        {
            AppendIndent(builder, indent + 2).Append(entry.Key).AppendLine();
            switch (entry.Value)
            {
                case GameEventScriptBytecodeObjectMatchExpressionValue expression:
                    AppendExpressionProgram(builder, module, expression.ExpressionProgram, indent + 4, "expression");
                    break;
                case GameEventScriptBytecodeObjectMatchNestedValue nested:
                    AppendObjectPattern(builder, module, nested.Pattern, indent + 4);
                    break;
            }
        }
    }

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

    private static void AppendValue(StringBuilder builder, string name, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            builder.Append(' ').Append(name).Append('=').Append(value);
        }
    }

    private static StringBuilder AppendIndent(StringBuilder builder, int indent)
        => builder.Append(' ', indent);

    private static void AppendPool(StringBuilder builder, string name, IReadOnlyList<string> values)
    {
        builder.Append(name).Append('[').Append(values.Count.ToString(CultureInfo.InvariantCulture)).AppendLine("]");
        for (var i = 0; i < values.Count; i++)
        {
            builder.Append("  #").Append(i.ToString(CultureInfo.InvariantCulture)).Append(": ").AppendLine(values[i]);
        }
    }
}
