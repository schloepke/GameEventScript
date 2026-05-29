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
        AppendHandlers(builder, module);
        AppendCallables(builder, module);
        AppendTypeDefinitions(builder, module);
        AppendPool(builder, "strings", module.StringPool);
        AppendUShortListPool(builder, module);
        AppendOutboundMessageSignatures(builder, module);
        AppendExternalReferences(builder, module);
        AppendCode(builder, module);
        AppendDebugSegment(builder, module);
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
            AppendSlot(builder, "dst", instruction.DestinationSlot);
        }

        switch (instruction.OpCode)
        {
            case GameEventScriptBytecodeOpCode.Nop:
                break;

            case GameEventScriptBytecodeOpCode.SlotLocals:
                if (instruction.Count >= 0)
                {
                    AppendIndex(builder, "locals+=", instruction.Count);
                }
                else
                {
                    AppendIndex(builder, "locals-=", -instruction.Count);
                }

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
                AppendPoolIndex(builder, "text", module.StringPool, instruction.StringIndex);
                break;

            case GameEventScriptBytecodeOpCode.LoadTag:
                AppendPoolIndex(builder, "tag", module.StringPool, instruction.StringIndex);
                break;

            case GameEventScriptBytecodeOpCode.LoadHandler:
                AppendIndex(builder, "shape", instruction.ListIndex);
                break;

            case GameEventScriptBytecodeOpCode.StageRegister:
                AppendSlot(builder, "src", instruction.XSlot);
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
                AppendPoolIndex(builder, "text", module.StringPool, instruction.StringIndex);
                break;

            case GameEventScriptBytecodeOpCode.StageTag:
                AppendPoolIndex(builder, "tag", module.StringPool, instruction.StringIndex);
                break;

            case GameEventScriptBytecodeOpCode.Jump:
                AppendAddress(builder, "target", instruction.TargetAddress);
                break;

            case GameEventScriptBytecodeOpCode.JumpIfTrue:
            case GameEventScriptBytecodeOpCode.JumpIfFalse:
            case GameEventScriptBytecodeOpCode.JumpIfNotTrue:
                AppendAddress(builder, "target", instruction.TargetAddress);
                AppendSlot(builder, "cond", instruction.ConditionSlot);
                break;

            case GameEventScriptBytecodeOpCode.RandomPush:
                AppendSlot(builder, "seed", instruction.XSlot);
                break;

            case GameEventScriptBytecodeOpCode.RandomPushConstant:
                builder.Append(" seed=").Append(instruction.I64.ToString(CultureInfo.InvariantCulture));
                break;

            case GameEventScriptBytecodeOpCode.RandomPop:
                break;

            case GameEventScriptBytecodeOpCode.CreateRangeIterator:
                AppendSlot(builder, "from", instruction.XSlot);
                AppendSlot(builder, "to", instruction.YSlot);
                break;

            case GameEventScriptBytecodeOpCode.CreateRangeIteratorWithStep:
                AppendSlot(builder, "from", instruction.XSlot);
                AppendSlot(builder, "to", instruction.YSlot);
                AppendSlot(builder, "step", instruction.AU);
                break;

            case GameEventScriptBytecodeOpCode.CreateRangeIteratorShort:
                AppendSignedImmediate(builder, "from", instruction.ImmediateX);
                AppendSignedImmediate(builder, "to", instruction.ImmediateY);
                AppendSignedImmediate(builder, "step", instruction.AS);
                break;

            case GameEventScriptBytecodeOpCode.StreamCreate:
                AppendSlot(builder, "collection", instruction.XSlot);
                break;

            case GameEventScriptBytecodeOpCode.StreamNext:
                AppendSlot(builder, "iterator", instruction.XSlot);
                AppendAddress(builder, "noMore", instruction.TargetAddress);
                break;

            case GameEventScriptBytecodeOpCode.StreamClose:
                AppendSlot(builder, "iterator", instruction.XSlot);
                break;

            case GameEventScriptBytecodeOpCode.PipelineListCreateBuilder:
                break;

            case GameEventScriptBytecodeOpCode.PipelineListBuilderAdd:
                AppendSlot(builder, "builder", instruction.XSlot);
                AppendSlot(builder, "item", instruction.YSlot);
                break;

            case GameEventScriptBytecodeOpCode.PipelineListBuilderFinish:
                AppendSlot(builder, "builder", instruction.XSlot);
                break;

            case GameEventScriptBytecodeOpCode.MemberAccess:
                AppendPoolIndex(builder, "member", module.StringPool, instruction.StringIndex);
                AppendSlot(builder, "object", instruction.YSlot);
                break;

            case GameEventScriptBytecodeOpCode.IndexAccess:
                builder.Append(" index=").Append(instruction.Index);
                AppendSlot(builder, "object", instruction.YSlot);
                break;

            case GameEventScriptBytecodeOpCode.PropertyAccess:
                AppendSlot(builder, "property", instruction.XSlot);
                AppendSlot(builder, "object", instruction.YSlot);
                break;

            case GameEventScriptBytecodeOpCode.MoveSlot:
            case GameEventScriptBytecodeOpCode.Negate:
            case GameEventScriptBytecodeOpCode.Not:
            case GameEventScriptBytecodeOpCode.HasValue:
            case GameEventScriptBytecodeOpCode.IsEmpty:
            case GameEventScriptBytecodeOpCode.Length:
            case GameEventScriptBytecodeOpCode.Chance:
            case GameEventScriptBytecodeOpCode.KeysOfMap:
            case GameEventScriptBytecodeOpCode.ValuesOfMap:
            case GameEventScriptBytecodeOpCode.EntriesOfMap:
            case GameEventScriptBytecodeOpCode.Abs:
            case GameEventScriptBytecodeOpCode.LogN:
                AppendSlot(builder, "src", instruction.XSlot);
                break;

            case var opCode when IsCastInstruction(opCode) || IsTypeCheckInstruction(opCode):
                AppendSlot(builder, "src", instruction.XSlot);
                AppendDeclaredTypeOperand(builder, module, instruction);
                break;

            case GameEventScriptBytecodeOpCode.ReturnVoid:
                break;

            case GameEventScriptBytecodeOpCode.ReturnValue:
                AppendSlot(builder, "src", instruction.XSlot);
                break;

            case GameEventScriptBytecodeOpCode.CreateDice:
                AppendSignedImmediate(builder, "dice", instruction.Count);
                AppendSignedImmediate(builder, "sides", instruction.ImmediateY);
                break;

            case GameEventScriptBytecodeOpCode.EmitMessage:
            case GameEventScriptBytecodeOpCode.PublishMessage:
                AppendIndex(builder, "shape", instruction.MessageDestination);
                AppendIndex(builder, "args", instruction.ListIndex);
                break;

            case GameEventScriptBytecodeOpCode.EmitMessageWithTags:
            case GameEventScriptBytecodeOpCode.PublishMessageWithTags:
                AppendIndex(builder, "shape", instruction.MessageDestination);
                AppendIndex(builder, "args", instruction.ListIndex);
                AppendIndex(builder, "tags", instruction.SecondaryListIndex);
                break;

            case GameEventScriptBytecodeOpCode.EmitMessageValue:
            case GameEventScriptBytecodeOpCode.PublishMessageValue:
                AppendSlot(builder, "src", instruction.XSlot);
                break;

            case GameEventScriptBytecodeOpCode.EmitMessageValueWithTags:
            case GameEventScriptBytecodeOpCode.PublishMessageValueWithTags:
                AppendSlot(builder, "src", instruction.XSlot);
                AppendIndex(builder, "tags", instruction.ListIndex);
                break;

            case GameEventScriptBytecodeOpCode.CreateRange:
                AppendSlot(builder, "from", instruction.XSlot);
                AppendSlot(builder, "to", instruction.YSlot);
                break;

            case GameEventScriptBytecodeOpCode.CreateRangeWithStep:
                AppendSlot(builder, "from", instruction.XSlot);
                AppendSlot(builder, "to", instruction.YSlot);
                AppendSlot(builder, "step", instruction.AU);
                break;

            case GameEventScriptBytecodeOpCode.CreateRecord:
                AppendPoolIndex(builder, "type", module.StringPool, instruction.StringIndex);
                AppendStringListPoolIndex(builder, "names", module, instruction.ListIndex);
                break;

            case GameEventScriptBytecodeOpCode.CreateExternalType:
                AppendExternalTypeConstructorReference(builder, "externalType", module, instruction.ExternalReferenceIndex);
                AppendStringListPoolIndex(builder, "names", module, instruction.ListIndex);
                break;

            case GameEventScriptBytecodeOpCode.CreateVector:
            case GameEventScriptBytecodeOpCode.CreatePoint:
                AppendSignedImmediate(builder, "immediateX", instruction.ImmediateX);
                break;

            case GameEventScriptBytecodeOpCode.CreateMap:
                AppendStringListPoolIndex(builder, "keys", module, instruction.SecondaryListIndex);
                break;

            case GameEventScriptBytecodeOpCode.LoadMessage:
                AppendStringListPoolIndex(builder, "shape", module, instruction.SecondaryListIndex);
                AppendSlotListPoolIndex(builder, "args", module, instruction.ListIndex);
                break;

            case GameEventScriptBytecodeOpCode.BindHandler:
                AppendSlot(builder, "handler", instruction.XSlot);
                AppendSlotListPoolIndex(builder, "args", module, instruction.ListIndex);
                break;

            case GameEventScriptBytecodeOpCode.CallStandard:
                AppendStringListPoolIndex(builder, "shape", module, instruction.SecondaryListIndex);
                AppendSlotListPoolIndex(builder, "args", module, instruction.ListIndex);
                break;

            case GameEventScriptBytecodeOpCode.CallExternal:
                AppendIndex(builder, "external", instruction.ExternalReferenceIndex);
                AppendSlotListPoolIndex(builder, "args", module, instruction.ListIndex);
                break;

            case GameEventScriptBytecodeOpCode.Call:
                AppendAddress(builder, "target", instruction.EntryAddress);
                break;

            case GameEventScriptBytecodeOpCode.PipelineStream:
                AppendSlot(builder, "source", instruction.XSlot);
                AppendAddress(builder, "entry", instruction.EntryAddress);
                AppendSlot(builder, "item", instruction.AU);
                AppendSlotListPoolIndex(builder, "captures", module, instruction.BU);
                break;

            case GameEventScriptBytecodeOpCode.StreamCollectList:
            case GameEventScriptBytecodeOpCode.StreamCollectFirst:
            case GameEventScriptBytecodeOpCode.StreamCollectLast:
            case GameEventScriptBytecodeOpCode.StreamCollectSingle:
            case GameEventScriptBytecodeOpCode.PipelineHasAny:
            case GameEventScriptBytecodeOpCode.PipelineHasAll:
            case GameEventScriptBytecodeOpCode.PipelineDistinct:
            case GameEventScriptBytecodeOpCode.PipelineReverse:
            case GameEventScriptBytecodeOpCode.PipelineSortAscending:
            case GameEventScriptBytecodeOpCode.PipelineSortDescending:
            case GameEventScriptBytecodeOpCode.PipelineShuffle:
                AppendSlot(builder, "iterator", instruction.XSlot);
                break;

            case GameEventScriptBytecodeOpCode.StreamReduce:
                AppendSlot(builder, "iterator", instruction.XSlot);
                AppendSlot(builder, "item", instruction.YSlot);
                AppendAddress(builder, "reducer", instruction.AU);
                break;

            case GameEventScriptBytecodeOpCode.StreamReduceOrDefault:
                AppendSlot(builder, "iterator", instruction.XSlot);
                AppendSlot(builder, "default", instruction.YSlot);
                AppendSlot(builder, "item", instruction.AU);
                AppendAddress(builder, "reducer", instruction.BU);
                break;

            case GameEventScriptBytecodeOpCode.StreamFold:
                AppendSlot(builder, "iterator", instruction.XSlot);
                AppendSlot(builder, "seed", instruction.YSlot);
                AppendSlot(builder, "item", instruction.AU);
                AppendAddress(builder, "reducer", instruction.BU);
                break;

            case GameEventScriptBytecodeOpCode.PipelineContainsSingle:
            case GameEventScriptBytecodeOpCode.PipelineContainsAny:
            case GameEventScriptBytecodeOpCode.PipelineContainsAll:
                AppendSlot(builder, "iterator", instruction.XSlot);
                AppendSlot(builder, "needle", instruction.YSlot);
                break;

            case GameEventScriptBytecodeOpCode.StreamCollectMap:
            case GameEventScriptBytecodeOpCode.PipelineDistinctBy:
            case GameEventScriptBytecodeOpCode.PipelineGroupBy:
            case GameEventScriptBytecodeOpCode.PipelineOrderByAscending:
            case GameEventScriptBytecodeOpCode.PipelineOrderByDescending:
                AppendSlot(builder, "iterator", instruction.XSlot);
                AppendSlot(builder, "item", instruction.YSlot);
                AppendAddress(builder, "entry", instruction.AU);
                break;

            case GameEventScriptBytecodeOpCode.StreamCollectMapValue:
                AppendSlot(builder, "iterator", instruction.XSlot);
                AppendSlot(builder, "item", instruction.YSlot);
                AppendAddress(builder, "keyEntry", instruction.AU);
                AppendAddress(builder, "valueEntry", instruction.BU);
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
                AppendSlot(builder, "iterator", instruction.XSlot);
                AppendIndex(builder, "count", instruction.ImmediateY);
                break;

            case GameEventScriptBytecodeOpCode.PipelineChooseWeighted:
                AppendSlot(builder, "iterator", instruction.XSlot);
                AppendIndex(builder, "count", instruction.ImmediateY);
                AppendSlot(builder, "item", instruction.AU);
                AppendAddress(builder, "weightEntry", instruction.BU);
                break;

            case GameEventScriptBytecodeOpCode.PipelineDicePatternCountAny:
            case GameEventScriptBytecodeOpCode.PipelineTakePatternCountAny:
                AppendSlot(builder, "iterator", instruction.XSlot);
                AppendIndex(builder, "count", instruction.ImmediateY);
                break;

            case GameEventScriptBytecodeOpCode.PipelineDicePatternCountFace:
            case GameEventScriptBytecodeOpCode.PipelineTakePatternCountFace:
                AppendSlot(builder, "iterator", instruction.XSlot);
                AppendIndex(builder, "count", instruction.ImmediateY);
                AppendAddress(builder, "faceEntry", instruction.AU);
                break;

            case GameEventScriptBytecodeOpCode.PipelineDicePatternFullHouse:
            case GameEventScriptBytecodeOpCode.PipelineDicePatternStraight:
            case GameEventScriptBytecodeOpCode.PipelineTakePatternFullHouse:
            case GameEventScriptBytecodeOpCode.PipelineTakePatternStraight:
                AppendSlot(builder, "iterator", instruction.XSlot);
                break;

            case GameEventScriptBytecodeOpCode.SeriesTerm:
                AppendSlot(builder, "series", instruction.XSlot);
                AppendSlot(builder, "index", instruction.YSlot);
                break;

            case GameEventScriptBytecodeOpCode.SeriesTake:
            case GameEventScriptBytecodeOpCode.SeriesDrop:
                AppendSlot(builder, "source", instruction.XSlot);
                AppendIndex(builder, "count", instruction.ImmediateY);
                break;
            default:
                AppendSlot(builder, "a", instruction.XSlot);
                AppendSlot(builder, "b", instruction.YSlot);
                AppendSlot(builder, "c", instruction.AU);
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
        switch (GameEventScriptBytecodeInstruction.DecodeUnit(value))
        {
            case GameEventScriptBytecodeInstructionUnit.UnitDegree:
                builder.Append(" unit=").Append(GameEventScriptBytecodeInstructionUnit.UnitDegree.ToTypeName());
                break;
            case GameEventScriptBytecodeInstructionUnit.UnitMeter:
                builder.Append(" unit=").Append(GameEventScriptBytecodeInstructionUnit.UnitMeter.ToTypeName());
                break;
            case GameEventScriptBytecodeInstructionUnit.UnitSecond:
                builder.Append(" unit=").Append(GameEventScriptBytecodeInstructionUnit.UnitSecond.ToTypeName());
                break;
        }
    }

    private static bool HasDestination(GameEventScriptBytecodeOpCode opCode)
        => opCode is not (
            GameEventScriptBytecodeOpCode.Nop or
            GameEventScriptBytecodeOpCode.SlotLocals or
            GameEventScriptBytecodeOpCode.Jump or
            GameEventScriptBytecodeOpCode.JumpIfTrue or
            GameEventScriptBytecodeOpCode.JumpIfFalse or
            GameEventScriptBytecodeOpCode.JumpIfNotTrue or
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
            GameEventScriptBytecodeOpCode.PipelineListBuilderAdd or
            GameEventScriptBytecodeOpCode.StreamClose or
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

    private static void AppendExternalTypeConstructorReference(StringBuilder builder, string name, GameEventScriptCompiled module, int index)
    {
        if (index < 0)
        {
            return;
        }

        builder.Append(' ').Append(name).Append('=').Append(index.ToString(CultureInfo.InvariantCulture));
        if ((uint)index < (uint)module.ExternalTypeConstructorReferences.Count)
        {
            builder.Append('(').Append(module.ExternalTypeConstructorReferences[index].SignatureId).Append(')');
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
        => opCode is GameEventScriptBytecodeOpCode.Cast or
            GameEventScriptBytecodeOpCode.CastNumeric or
            GameEventScriptBytecodeOpCode.CastCustom or
            GameEventScriptBytecodeOpCode.CastUnit;

    private static bool IsTypeCheckInstruction(GameEventScriptBytecodeOpCode opCode)
        => opCode is GameEventScriptBytecodeOpCode.CheckType or
            GameEventScriptBytecodeOpCode.CheckNumeric or
            GameEventScriptBytecodeOpCode.CheckInteger or
            GameEventScriptBytecodeOpCode.CheckFractional or
            GameEventScriptBytecodeOpCode.CheckCustomType or
            GameEventScriptBytecodeOpCode.CheckUnit;

    private static void AppendDeclaredTypeOperand(StringBuilder builder, GameEventScriptCompiled module, GameEventScriptBytecodeInstruction instruction)
    {
        if (instruction.OpCode is GameEventScriptBytecodeOpCode.CastNumeric or GameEventScriptBytecodeOpCode.CheckNumeric)
        {
            builder.Append(" kind=numeric");
            return;
        }

        if (instruction.OpCode == GameEventScriptBytecodeOpCode.CheckInteger)
        {
            builder.Append(" kind=integer");
            return;
        }

        if (instruction.OpCode == GameEventScriptBytecodeOpCode.CheckFractional)
        {
            builder.Append(" kind=fractional");
            return;
        }

        if (instruction.OpCode is GameEventScriptBytecodeOpCode.CastUnit or GameEventScriptBytecodeOpCode.CheckUnit)
        {
            AppendNumericUnit(builder, instruction.UnitAndFlags);
            return;
        }

        if (instruction.OpCode is GameEventScriptBytecodeOpCode.CastCustom or GameEventScriptBytecodeOpCode.CheckCustomType)
        {
            AppendPoolIndex(builder, "type", module.StringPool, instruction.TypeOperand);
            return;
        }

        var typeKind = (GameEventScriptBytecodeTypeKind)instruction.TypeOperand;
        builder.Append(" kind=").Append(typeKind);
    }

    private static void AppendPool(StringBuilder builder, string name, IReadOnlyList<string> values)
    {
        builder.Append(name).Append('[').Append(values.Count.ToString(CultureInfo.InvariantCulture)).AppendLine("]");
        for (var i = 0; i < values.Count; i++)
        {
            builder.Append("    #").Append(i.ToString("D3", CultureInfo.InvariantCulture)).Append(": ").AppendLine(values[i]);
        }
    }
}
