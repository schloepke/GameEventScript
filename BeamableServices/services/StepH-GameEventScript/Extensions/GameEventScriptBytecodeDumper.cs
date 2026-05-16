using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Types;

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
        builder.Append("debugInfo: ").AppendLine(module.Options.EnableDebugInfo ? "on" : "off");
        builder.Append("maxFrameSlots: ").AppendLine(module.MaxFrameSlots.ToString(CultureInfo.InvariantCulture));
        AppendPool(builder, "strings", module.StringPool);
        AppendUShortListPool(builder, module);
        AppendOutboundMessageSignatures(builder, module);
        AppendExternalReferences(builder, module);
        AppendDebugSegment(builder, module);
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

    private static void AppendUShortListPool(StringBuilder builder, GameEventScriptCompiled module)
    {
        builder.Append("uShortListPool[").Append(module.UShortListPool.Count.ToString(CultureInfo.InvariantCulture)).AppendLine("]");
        for (var i = 0; i < module.UShortListPool.Count; i++)
        {
            builder.Append("    #").Append(i.ToString("D3", CultureInfo.InvariantCulture)).Append(": ")
                .AppendLine(string.Join(", ", module.UShortListPool[i].Select(value => value.ToString(CultureInfo.InvariantCulture))));
            }
    }

    private static void AppendOutboundMessageSignatures(StringBuilder builder, GameEventScriptCompiled module)
    {
        builder.Append("outboundMessageSignatures[").Append(module.OutboundMessageSignatures.Count.ToString(CultureInfo.InvariantCulture)).AppendLine("]");
        for (var i = 0; i < module.OutboundMessageSignatures.Count; i++)
        {
            var shapeIndex = module.OutboundMessageSignatures[i];
            builder.Append("    #").Append(i.ToString("D3", CultureInfo.InvariantCulture))
                .Append(": shape=").Append(shapeIndex.ToString(CultureInfo.InvariantCulture));
            if (shapeIndex < module.UShortListPool.Count)
            {
                var signature = module.UShortListPool[shapeIndex]
                    .Select(index => index < module.StringPool.Count ? module.StringPool[index] : "?")
                    .ToArray();
                builder.Append(" [").Append(string.Join(", ", signature)).Append(']');
            }

            builder.AppendLine();
        }
    }

    private static void AppendDebugSegment(StringBuilder builder, GameEventScriptCompiled module)
    {
        builder.Append("debugDiagnosticSites[").Append(module.DebugSegment.DiagnosticSites.Count.ToString(CultureInfo.InvariantCulture)).AppendLine("]");
        for (var i = 0; i < module.DebugSegment.DiagnosticSites.Count; i++)
        {
            var site = module.DebugSegment.DiagnosticSites[i];
            builder.Append("    #").Append(i.ToString("D3", CultureInfo.InvariantCulture)).Append(": ")
                .Append(site.Kind)
                .Append(" timing=").Append(site.Timing)
                .Append(" address=@").Append(site.Address.ToString("D4", CultureInfo.InvariantCulture));
            AppendSlot(builder, "slot", site.Slot);
            AppendText(builder, "name", site.Name);
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
        var callables = module.Callables.Values
            .OrderBy(callable => FormatSignature(callable.Name, callable.SignatureLabels), StringComparer.Ordinal)
            .ToArray();
        builder.Append("callables[").Append(callables.Length.ToString(CultureInfo.InvariantCulture)).AppendLine("]");
        for (var i = 0; i < callables.Length; i++)
        {
            var callable = callables[i];
            var signatureId = FormatSignature(callable.Name, callable.SignatureLabels);
            builder.Append("  callable #").Append(i.ToString(CultureInfo.InvariantCulture))
                .Append(' ').Append(callable.Kind)
                .Append(' ').Append(signatureId)
                .Append(" entry=@").Append(FormatAddress(callable.EntryAddress))
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
            .OrderBy(handler => FormatSignature(handler.Message, handler.SignatureLabels), StringComparer.Ordinal)
            .ThenBy(handler => handler.DeclarationOrder)
            .ToArray();
        builder.Append("handlers[").Append(handlers.Length.ToString(CultureInfo.InvariantCulture)).AppendLine("]");
        for (var i = 0; i < handlers.Length; i++)
        {
            var handler = handlers[i];
            var signatureId = FormatSignature(handler.Message, handler.SignatureLabels);
            builder.Append("  handler #").Append(i.ToString(CultureInfo.InvariantCulture))
                .Append(' ').Append(signatureId)
                .Append(" dispatch=").Append(handler.DispatchKind)
                .Append(" declarationOrder=").Append(handler.DeclarationOrder.ToString(CultureInfo.InvariantCulture))
                .Append(" params=[").Append(FormatParameters(handler.Parameters, handler.ParameterTypes)).Append(']')
                .Append(" matching=[").Append(FormatTags(handler.RequiredTags)).Append(']')
                .Append(" without=[").Append(FormatTags(handler.ExcludedTags)).Append(']')
                .Append(" entry=@").AppendLine(FormatAddress(handler.EntryAddress));
        }
    }

    private static void AppendLinearInstructionOperands(
        StringBuilder builder,
        GameEventScriptCompiled module,
        GameEventScriptBytecodeInstruction instruction)
    {
        if (HasDestination(instruction.OpCode))
        {
            AppendSlot(builder, "dst", instruction.Dest_U16);
        }

        switch (instruction.OpCode)
        {
            case GameEventScriptBytecodeOpCode.Nop:
                break;

            case GameEventScriptBytecodeOpCode.ReserveSlots:
                AppendIndex(builder, "locals+=", instruction.A_U16);
                break;

            case GameEventScriptBytecodeOpCode.ReleaseSlots:
                AppendIndex(builder, "locals-=", instruction.A_U16);
                break;

            case GameEventScriptBytecodeOpCode.LoadNothing:
                builder.Append(" value=nothing");
                break;

            case GameEventScriptBytecodeOpCode.LoadTrue:
                builder.Append(" value=true");
                break;

            case GameEventScriptBytecodeOpCode.LoadFalse:
                builder.Append(" value=false");
                break;

            case GameEventScriptBytecodeOpCode.LoadInteger:
                AppendInt64Constant(builder, instruction);
                break;

            case GameEventScriptBytecodeOpCode.LoadFloat:
                AppendDoubleConstant(
                    builder,
                    "value",
                    instruction);
                AppendNumericUnit(builder, instruction.UnitAndFlags);
                break;

            case GameEventScriptBytecodeOpCode.LoadPercentage:
                AppendDoubleConstant(builder, "ratio", instruction);
                break;

            case GameEventScriptBytecodeOpCode.LoadText:
                AppendPoolIndex(builder, "text", module.StringPool, instruction.C_U16);
                break;

            case GameEventScriptBytecodeOpCode.LoadTag:
                AppendPoolIndex(builder, "tag", module.StringPool, instruction.C_U16);
                break;

            case GameEventScriptBytecodeOpCode.LoadHandler:
                AppendIndex(builder, "shape", instruction.A_U16);
                break;

            case GameEventScriptBytecodeOpCode.StageRegister:
                AppendSlot(builder, "src", instruction.A_U16);
                break;

            case GameEventScriptBytecodeOpCode.StageNothing:
                builder.Append(" value=nothing");
                break;

            case GameEventScriptBytecodeOpCode.StageTrue:
                builder.Append(" value=true");
                break;

            case GameEventScriptBytecodeOpCode.StageFalse:
                builder.Append(" value=false");
                break;

            case GameEventScriptBytecodeOpCode.StageInteger:
                AppendInt64Constant(builder, instruction);
                break;

            case GameEventScriptBytecodeOpCode.StageFloat:
                AppendDoubleConstant(
                    builder,
                    "value",
                    instruction);
                AppendNumericUnit(builder, instruction.UnitAndFlags);
                break;

            case GameEventScriptBytecodeOpCode.StagePercentage:
                AppendDoubleConstant(builder, "ratio", instruction);
                break;

            case GameEventScriptBytecodeOpCode.StageText:
                AppendPoolIndex(builder, "text", module.StringPool, instruction.C_U16);
                break;

            case GameEventScriptBytecodeOpCode.StageTag:
                AppendPoolIndex(builder, "tag", module.StringPool, instruction.C_U16);
                break;

            case GameEventScriptBytecodeOpCode.Jump:
                AppendAddress(builder, "target", instruction.A_U16);
                break;

            case GameEventScriptBytecodeOpCode.JumpIfTrue:
            case GameEventScriptBytecodeOpCode.JumpIfFalse:
            case GameEventScriptBytecodeOpCode.JumpIfNotTrue:
                AppendAddress(builder, "target", instruction.A_U16);
                AppendSlot(builder, "cond", instruction.C_U16);
                break;

            case GameEventScriptBytecodeOpCode.RandomPush:
                AppendSlot(builder, "seed", instruction.A_U16);
                break;

            case GameEventScriptBytecodeOpCode.RandomPushConstant:
                builder.Append(" seed=").Append(instruction.U64.ToString(CultureInfo.InvariantCulture));
                break;

            case GameEventScriptBytecodeOpCode.RandomPop:
                break;

            case GameEventScriptBytecodeOpCode.RangeIterator:
                AppendSlot(builder, "from", instruction.A_U16);
                AppendSlot(builder, "to", instruction.B_U16);
                break;

            case GameEventScriptBytecodeOpCode.RangeIteratorWithStep:
                AppendSlot(builder, "from", instruction.A_U16);
                AppendSlot(builder, "to", instruction.B_U16);
                AppendSlot(builder, "step", instruction.C_U16);
                break;

            case GameEventScriptBytecodeOpCode.RangeIteratorShort:
                AppendSignedImmediate(builder, "from", instruction.A_I16);
                AppendSignedImmediate(builder, "to", instruction.B_I16);
                AppendSignedImmediate(builder, "step", instruction.C_I16);
                break;

            case GameEventScriptBytecodeOpCode.CollectionIterator:
                AppendSlot(builder, "collection", instruction.A_U16);
                break;

            case GameEventScriptBytecodeOpCode.IteratorNext:
                AppendSlot(builder, "iterator", instruction.A_U16);
                AppendAddress(builder, "noMore", instruction.B_U16);
                break;

            case GameEventScriptBytecodeOpCode.IteratorClose:
                AppendSlot(builder, "iterator", instruction.A_U16);
                break;

            case GameEventScriptBytecodeOpCode.CollectionBuilderList:
                break;

            case GameEventScriptBytecodeOpCode.CollectionBuilderAdd:
                AppendSlot(builder, "builder", instruction.A_U16);
                AppendSlot(builder, "item", instruction.B_U16);
                break;

            case GameEventScriptBytecodeOpCode.CollectionBuilderFinish:
                AppendSlot(builder, "builder", instruction.A_U16);
                break;

            case GameEventScriptBytecodeOpCode.MoveSlot:
            case GameEventScriptBytecodeOpCode.MemberAccess:
            case GameEventScriptBytecodeOpCode.UnaryNegate:
            case GameEventScriptBytecodeOpCode.UnaryNot:
            case GameEventScriptBytecodeOpCode.UnaryHasValue:
            case GameEventScriptBytecodeOpCode.UnaryEmpty:
            case GameEventScriptBytecodeOpCode.UnaryLength:
            case GameEventScriptBytecodeOpCode.UnaryChance:
            case GameEventScriptBytecodeOpCode.UnaryKeys:
            case GameEventScriptBytecodeOpCode.UnaryValues:
            case GameEventScriptBytecodeOpCode.UnaryEntries:
            case GameEventScriptBytecodeOpCode.UnaryAbs:
            case GameEventScriptBytecodeOpCode.UnaryNaturalLog:
                AppendSlot(builder, "src", instruction.A_U16);
                break;

            case var opCode when IsCastInstruction(opCode) || IsTypeCheckInstruction(opCode):
                AppendSlot(builder, "src", instruction.A_U16);
                break;

            case GameEventScriptBytecodeOpCode.ReturnVoid:
                break;

            case GameEventScriptBytecodeOpCode.ReturnValue:
                AppendSlot(builder, "src", instruction.A_U16);
                break;

            case GameEventScriptBytecodeOpCode.Dice:
                AppendIndex(builder, "dice", instruction.A_U16);
                AppendIndex(builder, "sides", instruction.B_U16);
                break;

            case GameEventScriptBytecodeOpCode.EmitMessage:
            case GameEventScriptBytecodeOpCode.PublishMessage:
                AppendIndex(builder, "shape", instruction.A_U16);
                AppendIndex(builder, "args", instruction.B_U16);
                break;

            case GameEventScriptBytecodeOpCode.EmitMessageWithTags:
            case GameEventScriptBytecodeOpCode.PublishMessageWithTags:
                AppendIndex(builder, "shape", instruction.A_U16);
                AppendIndex(builder, "args", instruction.B_U16);
                AppendIndex(builder, "tags", instruction.C_U16);
                break;

            case GameEventScriptBytecodeOpCode.EmitMessageValue:
            case GameEventScriptBytecodeOpCode.PublishMessageValue:
                AppendSlot(builder, "src", instruction.A_U16);
                break;

            case GameEventScriptBytecodeOpCode.EmitMessageValueWithTags:
            case GameEventScriptBytecodeOpCode.PublishMessageValueWithTags:
                AppendSlot(builder, "src", instruction.A_U16);
                AppendIndex(builder, "tags", instruction.C_U16);
                break;

            case GameEventScriptBytecodeOpCode.Range:
                AppendSlot(builder, "from", instruction.A_U16);
                AppendSlot(builder, "to", instruction.B_U16);
                break;

            case GameEventScriptBytecodeOpCode.RangeWithStep:
                AppendSlot(builder, "from", instruction.A_U16);
                AppendSlot(builder, "to", instruction.B_U16);
                AppendSlot(builder, "step", instruction.C_U16);
                break;

            case GameEventScriptBytecodeOpCode.Variadic:
                AppendPoolIndex(builder, "operation", module.StringPool, instruction.A_U16);
                AppendSlotListPoolIndex(builder, "args", module, instruction.B_U16);
                break;

            case GameEventScriptBytecodeOpCode.TypeConstructor:
                AppendPoolIndex(builder, "type", module.StringPool, instruction.A_U16);
                AppendStringListPoolIndex(builder, "names", module, instruction.B_U16);
                AppendSlotListPoolIndex(builder, "args", module, instruction.C_U16);
                break;

            case GameEventScriptBytecodeOpCode.BuildList:
                AppendSlotListPoolIndex(builder, "items", module, instruction.A_U16);
                break;

            case GameEventScriptBytecodeOpCode.BuildMap:
                AppendStringListPoolIndex(builder, "keys", module, instruction.A_U16);
                AppendSlotListPoolIndex(builder, "values", module, instruction.B_U16);
                break;

            case GameEventScriptBytecodeOpCode.BuildMessage:
                AppendStringListPoolIndex(builder, "shape", module, instruction.A_U16);
                AppendSlotListPoolIndex(builder, "args", module, instruction.B_U16);
                break;

            case GameEventScriptBytecodeOpCode.BindHandler:
                AppendSlotListPoolIndex(builder, "operands", module, instruction.A_U16);
                AppendStringListPoolIndex(builder, "names", module, instruction.B_U16);
                break;

            case GameEventScriptBytecodeOpCode.CallStandard:
            case GameEventScriptBytecodeOpCode.CallStandardPredicate:
                AppendStringListPoolIndex(builder, "shape", module, instruction.A_U16);
                AppendSlotListPoolIndex(builder, "args", module, instruction.B_U16);
                break;

            case GameEventScriptBytecodeOpCode.CallExternal:
            case GameEventScriptBytecodeOpCode.CallExternalPredicate:
                AppendIndex(builder, "external", instruction.A_U16);
                AppendSlotListPoolIndex(builder, "args", module, instruction.B_U16);
                break;

            case GameEventScriptBytecodeOpCode.Call:
            case GameEventScriptBytecodeOpCode.CallPredicate:
                AppendAddress(builder, "target", instruction.A_U16);
                break;

            case GameEventScriptBytecodeOpCode.PipelineIterator:
                AppendSlot(builder, "source", instruction.A_U16);
                AppendAddress(builder, "entry", instruction.B_U16);
                AppendSlot(builder, "item", instruction.C_U16);
                AppendSlotListPoolIndex(builder, "captures", module, instruction.D_U16);
                break;

            case GameEventScriptBytecodeOpCode.PipelineCollectList:
            case GameEventScriptBytecodeOpCode.PipelineFirst:
            case GameEventScriptBytecodeOpCode.PipelineLast:
            case GameEventScriptBytecodeOpCode.PipelineSingle:
            case GameEventScriptBytecodeOpCode.PipelineHasAny:
            case GameEventScriptBytecodeOpCode.PipelineHasAll:
            case GameEventScriptBytecodeOpCode.PipelineDistinct:
            case GameEventScriptBytecodeOpCode.PipelineReverse:
            case GameEventScriptBytecodeOpCode.PipelineSortAscending:
            case GameEventScriptBytecodeOpCode.PipelineSortDescending:
            case GameEventScriptBytecodeOpCode.PipelineShuffle:
                AppendSlot(builder, "iterator", instruction.A_U16);
                break;

            case GameEventScriptBytecodeOpCode.IteratorReduce:
                AppendSlot(builder, "iterator", instruction.A_U16);
                AppendSlot(builder, "item", instruction.B_U16);
                AppendAddress(builder, "reducer", instruction.C_U16);
                break;

            case GameEventScriptBytecodeOpCode.IteratorReduceOrDefault:
                AppendSlot(builder, "iterator", instruction.A_U16);
                AppendSlot(builder, "default", instruction.B_U16);
                AppendSlot(builder, "item", instruction.C_U16);
                AppendAddress(builder, "reducer", instruction.D_U16);
                break;

            case GameEventScriptBytecodeOpCode.IteratorFold:
                AppendSlot(builder, "iterator", instruction.A_U16);
                AppendSlot(builder, "seed", instruction.B_U16);
                AppendSlot(builder, "item", instruction.C_U16);
                AppendAddress(builder, "reducer", instruction.D_U16);
                break;

            case GameEventScriptBytecodeOpCode.PipelineContainsSingle:
            case GameEventScriptBytecodeOpCode.PipelineContainsAny:
            case GameEventScriptBytecodeOpCode.PipelineContainsAll:
                AppendSlot(builder, "iterator", instruction.A_U16);
                AppendSlot(builder, "needle", instruction.B_U16);
                break;

            case GameEventScriptBytecodeOpCode.PipelineMap:
            case GameEventScriptBytecodeOpCode.PipelineDistinctBy:
            case GameEventScriptBytecodeOpCode.PipelineGroupBy:
            case GameEventScriptBytecodeOpCode.PipelineOrderByAscending:
            case GameEventScriptBytecodeOpCode.PipelineOrderByDescending:
                AppendSlot(builder, "iterator", instruction.A_U16);
                AppendSlot(builder, "item", instruction.B_U16);
                AppendAddress(builder, "entry", instruction.C_U16);
                break;

            case GameEventScriptBytecodeOpCode.PipelineMapValue:
                AppendSlot(builder, "iterator", instruction.A_U16);
                AppendSlot(builder, "item", instruction.B_U16);
                AppendAddress(builder, "keyEntry", instruction.C_U16);
                AppendAddress(builder, "valueEntry", instruction.D_U16);
                break;

            case GameEventScriptBytecodeOpCode.PipelineTakeFirst:
            case GameEventScriptBytecodeOpCode.PipelineTakeLast:
            case GameEventScriptBytecodeOpCode.PipelineTakeHighest:
            case GameEventScriptBytecodeOpCode.PipelineTakeLowest:
            case GameEventScriptBytecodeOpCode.PipelineDropFirst:
            case GameEventScriptBytecodeOpCode.PipelineDropLast:
            case GameEventScriptBytecodeOpCode.PipelineDropHighest:
            case GameEventScriptBytecodeOpCode.PipelineDropLowest:
            case GameEventScriptBytecodeOpCode.PipelineDraw:
            case GameEventScriptBytecodeOpCode.PipelineChoose:
            case GameEventScriptBytecodeOpCode.PipelineChooseRandom:
                AppendSlot(builder, "iterator", instruction.A_U16);
                AppendIndex(builder, "count", instruction.B_U16);
                break;

            case GameEventScriptBytecodeOpCode.PipelineChooseWeighted:
                AppendSlot(builder, "iterator", instruction.A_U16);
                AppendIndex(builder, "count", instruction.B_U16);
                AppendSlot(builder, "item", instruction.C_U16);
                AppendAddress(builder, "weightEntry", instruction.D_U16);
                break;

            case GameEventScriptBytecodeOpCode.PipelineDicePatternCountAny:
            case GameEventScriptBytecodeOpCode.PipelineTakePatternCountAny:
                AppendSlot(builder, "iterator", instruction.A_U16);
                AppendIndex(builder, "count", instruction.B_U16);
                break;

            case GameEventScriptBytecodeOpCode.PipelineDicePatternCountFace:
            case GameEventScriptBytecodeOpCode.PipelineTakePatternCountFace:
                AppendSlot(builder, "iterator", instruction.A_U16);
                AppendIndex(builder, "count", instruction.B_U16);
                AppendAddress(builder, "faceEntry", instruction.C_U16);
                break;

            case GameEventScriptBytecodeOpCode.PipelineDicePatternFullHouse:
            case GameEventScriptBytecodeOpCode.PipelineDicePatternStraight:
            case GameEventScriptBytecodeOpCode.PipelineTakePatternFullHouse:
            case GameEventScriptBytecodeOpCode.PipelineTakePatternStraight:
                AppendSlot(builder, "iterator", instruction.A_U16);
                break;

            case GameEventScriptBytecodeOpCode.SeriesTerm:
                AppendSlot(builder, "series", instruction.A_U16);
                AppendSlot(builder, "index", instruction.B_U16);
                break;

            case GameEventScriptBytecodeOpCode.SeriesTake:
            case GameEventScriptBytecodeOpCode.SeriesDrop:
                AppendSlot(builder, "source", instruction.A_U16);
                AppendIndex(builder, "count", instruction.B_U16);
                break;
            case GameEventScriptBytecodeOpCode.IntFloorDivide:
            case GameEventScriptBytecodeOpCode.IntDivide:
            case GameEventScriptBytecodeOpCode.IntRemainder:
            case GameEventScriptBytecodeOpCode.IntModulo:
            case GameEventScriptBytecodeOpCode.IntMultiply:
            case GameEventScriptBytecodeOpCode.IntSubtract:
            case GameEventScriptBytecodeOpCode.IntAdd:
                AppendSlot(builder, "a", instruction.A_U16);
                AppendSlot(builder, "b", instruction.B_U16);
                break;
            default:
                AppendSlot(builder, "a", instruction.A_U16);
                AppendSlot(builder, "b", instruction.B_U16);
                AppendSlot(builder, "c", instruction.C_U16);
                break;
        }

        switch (instruction.OpCode)
        {
            case GameEventScriptBytecodeOpCode.MemberAccess:
                AppendPoolIndex(builder, "member", module.StringPool, instruction.C_U16);
                break;
            case GameEventScriptBytecodeOpCode.CastCustom or GameEventScriptBytecodeOpCode.TypeCheckCustom:
                AppendPoolIndex(builder, "type", module.StringPool, instruction.C_U16);
                break;
            default:
                break;
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

    private static string FormatSignature(string name, IReadOnlyList<string> labels)
        => GameEventScriptMessageSignature.CreateSignatureId(name, labels);

    private static void AppendInt64Constant(StringBuilder builder, GameEventScriptBytecodeInstruction instruction)
    {
        builder.Append(" value=").Append(instruction.I64.ToString(CultureInfo.InvariantCulture));
        AppendNumericUnit(builder, instruction.UnitAndFlags);
    }

    private static void AppendDoubleConstant(StringBuilder builder, string name, GameEventScriptBytecodeInstruction instruction)
        => builder.Append(' ')
            .Append(name)
            .Append('=')
            .Append(instruction.F64.ToString(CultureInfo.InvariantCulture));

    private static void AppendNumericUnit(StringBuilder builder, byte value)
    {
        switch ((GameEventScriptBytecodeInstructionUnit)value)
        {
            case GameEventScriptBytecodeInstructionUnit.UnitDegree:
                builder.Append(" unit=").Append(GameEventScriptNumericUnit.Degree.ToTypeName());
                break;
            case GameEventScriptBytecodeInstructionUnit.UnitMeter:
                builder.Append(" unit=").Append(GameEventScriptNumericUnit.Meter.ToTypeName());
                break;
            case GameEventScriptBytecodeInstructionUnit.UnitSecond:
                builder.Append(" unit=").Append(GameEventScriptNumericUnit.Second.ToTypeName());
                break;
        }
    }

    private static bool HasDestination(GameEventScriptBytecodeOpCode opCode)
        => opCode is not (
            GameEventScriptBytecodeOpCode.Nop or
            GameEventScriptBytecodeOpCode.ReserveSlots or
            GameEventScriptBytecodeOpCode.Jump or
            GameEventScriptBytecodeOpCode.JumpIfTrue or
            GameEventScriptBytecodeOpCode.JumpIfFalse or
            GameEventScriptBytecodeOpCode.JumpIfNotTrue or
            GameEventScriptBytecodeOpCode.ReleaseSlots or
            GameEventScriptBytecodeOpCode.ReturnVoid or
            GameEventScriptBytecodeOpCode.ReturnValue or
            GameEventScriptBytecodeOpCode.EmitMessage or
            GameEventScriptBytecodeOpCode.EmitMessageWithTags or
            GameEventScriptBytecodeOpCode.PublishMessage or
            GameEventScriptBytecodeOpCode.PublishMessageWithTags or
            GameEventScriptBytecodeOpCode.EmitMessageValue or
            GameEventScriptBytecodeOpCode.EmitMessageValueWithTags or
            GameEventScriptBytecodeOpCode.PublishMessageValue or
            GameEventScriptBytecodeOpCode.PublishMessageValueWithTags or
            GameEventScriptBytecodeOpCode.CollectionBuilderAdd or
            GameEventScriptBytecodeOpCode.IteratorClose or
            GameEventScriptBytecodeOpCode.RandomPush or
            GameEventScriptBytecodeOpCode.RandomPushConstant or
            GameEventScriptBytecodeOpCode.RandomPop or
            GameEventScriptBytecodeOpCode.StageRegister or
            GameEventScriptBytecodeOpCode.StageNothing or
            GameEventScriptBytecodeOpCode.StageTrue or
            GameEventScriptBytecodeOpCode.StageFalse or
            GameEventScriptBytecodeOpCode.StageInteger or
            GameEventScriptBytecodeOpCode.StageFloat or
            GameEventScriptBytecodeOpCode.StagePercentage or
            GameEventScriptBytecodeOpCode.StageText or
            GameEventScriptBytecodeOpCode.StageTag);

    private static bool IsStageInstruction(GameEventScriptBytecodeOpCode opCode)
        => opCode is GameEventScriptBytecodeOpCode.StageRegister or
            GameEventScriptBytecodeOpCode.StageNothing or
            GameEventScriptBytecodeOpCode.StageTrue or
            GameEventScriptBytecodeOpCode.StageFalse or
            GameEventScriptBytecodeOpCode.StageInteger or
            GameEventScriptBytecodeOpCode.StageFloat or
            GameEventScriptBytecodeOpCode.StagePercentage or
            GameEventScriptBytecodeOpCode.StageText or
            GameEventScriptBytecodeOpCode.StageTag;

    private static void AppendPoolIndex(StringBuilder builder, string name, IReadOnlyList<string> pool, int index)
    {
        if (index < 0)
        {
            return;
        }

        builder.Append(' ').Append(name).Append('=').Append(index.ToString(CultureInfo.InvariantCulture));
        if ((uint)index < (uint)pool.Count)
        {
            builder.Append('(').Append(pool[index]).Append(')');
        }
    }

    private static void AppendStringListPoolIndex(StringBuilder builder, string name, GameEventScriptCompiled module, int index)
    {
        if (index < 0)
        {
            return;
        }

        builder.Append(' ').Append(name).Append('=').Append(index.ToString(CultureInfo.InvariantCulture));
        if ((uint)index >= (uint)module.UShortListPool.Count)
        {
            return;
        }

        var values = module.UShortListPool[index]
            .Select(value => (uint)value < (uint)module.StringPool.Count ? module.StringPool[value] : "#" + value.ToString(CultureInfo.InvariantCulture))
            .ToArray();
        if (values.Length > 0)
        {
            builder.Append('(').Append(string.Join(", ", values)).Append(')');
        }
    }

    private static void AppendSlotListPoolIndex(StringBuilder builder, string name, GameEventScriptCompiled module, int index)
    {
        if (index < 0)
        {
            return;
        }

        builder.Append(' ').Append(name).Append('=').Append(index.ToString(CultureInfo.InvariantCulture));
        if ((uint)index >= (uint)module.UShortListPool.Count)
        {
            return;
        }

        var values = module.UShortListPool[index]
            .Select(value => "s" + value.ToString(CultureInfo.InvariantCulture))
            .ToArray();
        if (values.Length > 0)
        {
            builder.Append('(').Append(string.Join(", ", values)).Append(')');
        }
    }

    private static void AppendIndex(StringBuilder builder, string name, int value)
    {
        if (value >= 0)
        {
            builder.Append(' ').Append(name).Append('=').Append(value.ToString(CultureInfo.InvariantCulture));
        }
    }

    private static void AppendSignedImmediate(StringBuilder builder, string name, int value)
        => builder.Append(' ').Append(name).Append('=').Append(value.ToString(CultureInfo.InvariantCulture));

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

    private static void AppendAddress(StringBuilder builder, string name, int value)
    {
        if (value >= 0)
        {
            builder.Append(' ').Append(name).Append("=@").Append(FormatAddress(value));
        }
    }

    private static string FormatAddress(int value) => value < 0 ? "none" : value.ToString("0000", CultureInfo.InvariantCulture);

    private static bool IsCastInstruction(GameEventScriptBytecodeOpCode opCode)
        => opCode is GameEventScriptBytecodeOpCode.CastNothing or
            GameEventScriptBytecodeOpCode.CastBoolean or
            GameEventScriptBytecodeOpCode.CastInteger or
            GameEventScriptBytecodeOpCode.CastFloat or
            GameEventScriptBytecodeOpCode.CastNumber or
            GameEventScriptBytecodeOpCode.CastPercentage or
            GameEventScriptBytecodeOpCode.CastUnit or
            GameEventScriptBytecodeOpCode.CastVector or
            GameEventScriptBytecodeOpCode.CastPoint or
            GameEventScriptBytecodeOpCode.CastUuid or
            GameEventScriptBytecodeOpCode.CastSeries or
            GameEventScriptBytecodeOpCode.CastEnvelope or
            GameEventScriptBytecodeOpCode.CastRef or
            GameEventScriptBytecodeOpCode.CastTag or
            GameEventScriptBytecodeOpCode.CastText or
            GameEventScriptBytecodeOpCode.CastList or
            GameEventScriptBytecodeOpCode.CastRange or
            GameEventScriptBytecodeOpCode.CastMessage or
            GameEventScriptBytecodeOpCode.CastHandler or
            GameEventScriptBytecodeOpCode.CastMap or
            GameEventScriptBytecodeOpCode.CastDice or
            GameEventScriptBytecodeOpCode.CastCustom;

    private static bool IsTypeCheckInstruction(GameEventScriptBytecodeOpCode opCode)
        => opCode is GameEventScriptBytecodeOpCode.TypeCheckNothing or
            GameEventScriptBytecodeOpCode.TypeCheckTag or
            GameEventScriptBytecodeOpCode.TypeCheckText or
            GameEventScriptBytecodeOpCode.TypeCheckPercentage or
            GameEventScriptBytecodeOpCode.TypeCheckUnit or
            GameEventScriptBytecodeOpCode.TypeCheckVector or
            GameEventScriptBytecodeOpCode.TypeCheckPoint or
            GameEventScriptBytecodeOpCode.TypeCheckFloat or
            GameEventScriptBytecodeOpCode.TypeCheckInteger or
            GameEventScriptBytecodeOpCode.TypeCheckBoolean or
            GameEventScriptBytecodeOpCode.TypeCheckUuid or
            GameEventScriptBytecodeOpCode.TypeCheckSeries or
            GameEventScriptBytecodeOpCode.TypeCheckEnvelope or
            GameEventScriptBytecodeOpCode.TypeCheckList or
            GameEventScriptBytecodeOpCode.TypeCheckRange or
            GameEventScriptBytecodeOpCode.TypeCheckMessage or
            GameEventScriptBytecodeOpCode.TypeCheckHandler or
            GameEventScriptBytecodeOpCode.TypeCheckRef or
            GameEventScriptBytecodeOpCode.TypeCheckMap or
            GameEventScriptBytecodeOpCode.TypeCheckDice or
            GameEventScriptBytecodeOpCode.TypeCheckCustom;

    private static void AppendPool(StringBuilder builder, string name, IReadOnlyList<string> values)
    {
        builder.Append(name).Append('[').Append(values.Count.ToString(CultureInfo.InvariantCulture)).AppendLine("]");
        for (var i = 0; i < values.Count; i++)
        {
            builder.Append("    #").Append(i.ToString("D3", CultureInfo.InvariantCulture)).Append(": ").AppendLine(values[i]);
        }
    }
}
