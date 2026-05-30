using System;
using System.Collections.Generic;
using System.Linq;
using StepH.GameEventScript.Api;

namespace StepH.GameEventScript.BytecodeVM;

internal sealed class GesBytecodeVmLinearExecutable
{
    private GesBytecodeVmLinearExecutable(
        GameEventScriptBytecodeInstruction[] code,
        int maxFrameSlots,
        GesBytecodeVmLinearHandlerEntry[] handlers,
        GesBytecodeVmLinearCallableEntry[] callables,
        IReadOnlyDictionary<int, GesBytecodeVmLinearCallableEntry> callablesByEntryAddress,
        GesBytecodeVmLinearTypeFieldEntry[] typeFields,
        IReadOnlyList<GesBytecodeVmLinearDiagnosticEntry>[] diagnosticsBefore,
        IReadOnlyList<GesBytecodeVmLinearDiagnosticEntry>[] diagnosticsAfter)
    {
        Code = code;
        MaxFrameSlots = maxFrameSlots;
        Handlers = handlers;
        Callables = callables;
        CallablesByEntryAddress = callablesByEntryAddress;
        TypeFields = typeFields;
        DiagnosticsBefore = diagnosticsBefore;
        DiagnosticsAfter = diagnosticsAfter;
    }

    public IReadOnlyList<GameEventScriptBytecodeInstruction> Code { get; }

    public int MaxFrameSlots { get; }

    public IReadOnlyList<GesBytecodeVmLinearHandlerEntry> Handlers { get; }

    public IReadOnlyList<GesBytecodeVmLinearCallableEntry> Callables { get; }

    internal IReadOnlyDictionary<int, GesBytecodeVmLinearCallableEntry> CallablesByEntryAddress { get; }

    public IReadOnlyList<GesBytecodeVmLinearTypeFieldEntry> TypeFields { get; }

    public IReadOnlyList<GesBytecodeVmLinearDiagnosticEntry>[] DiagnosticsBefore { get; }

    public IReadOnlyList<GesBytecodeVmLinearDiagnosticEntry>[] DiagnosticsAfter { get; }

    public static GesBytecodeVmLinearExecutable Build(GameEventScriptCompiled module)
    {
        _ = module ?? throw new ArgumentNullException(nameof(module));
        var code = module.Code.ToArray();
        ValidateCode(module, code);
        ValidateSideTables(module);
        var handlers = module.Handlers.Values
            .SelectMany(group => group)
            .Select(handler =>
            {
                var signatureId = GameEventScriptMessageSignature.CreateSignatureId(handler.Message, handler.SignatureLabels);
                ValidateAddress(module, code, handler.EntryAddress, $"handler '{signatureId}' entry");
                var localSlotCount = ReadEntrySlotCount(module, code, handler.EntryAddress, $"handler '{signatureId}' entry");
                return new GesBytecodeVmLinearHandlerEntry(
                    handler.Message,
                    signatureId,
                    handler.DispatchKind,
                    handler.DeclarationOrder,
                    handler.EntryAddress,
                    localSlotCount,
                    handler.Parameters.ToArray(),
                    handler.ParameterTypes.ToArray(),
                    handler.Slots.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));
            })
            .ToArray();
        var callables = module.Callables.Values
            .Select(callable =>
            {
                var signatureId = GameEventScriptMessageSignature.CreateSignatureId(callable.Name, callable.SignatureLabels);
                ValidateAddress(module, code, callable.EntryAddress, $"callable '{signatureId}' entry");
                var localSlotCount = ReadEntrySlotCount(module, code, callable.EntryAddress, $"callable '{signatureId}' entry");
                ValidateOptionalSlot(module, callable.ReturnSlot, $"callable '{signatureId}' return slot");
                return new GesBytecodeVmLinearCallableEntry(
                    callable.Name,
                    signatureId,
                    callable.Kind,
                    callable.EntryAddress,
                    localSlotCount,
                    callable.ReturnSlot,
                    callable.Parameters.ToArray(),
                    callable.ParameterTypes.ToArray());
            })
            .ToArray();
        var callablesByEntryAddress = callables.ToDictionary(
            callable => callable.EntryAddress,
            callable => callable);
        var typeFields = module.TypeDefinitions.Values
            .SelectMany(type => type.Fields.Select(field =>
            {
                ValidateOptionalEntryAddress(module, code, field.MinimumEntryAddress, $"type '{type.Name}.{field.Name}' minimum entry");
                ValidateOptionalEntryAddress(module, code, field.MaximumEntryAddress, $"type '{type.Name}.{field.Name}' maximum entry");
                ValidateOptionalEntryAddress(module, code, field.ComputedEntryAddress, $"type '{type.Name}.{field.Name}' computed entry");
                return new GesBytecodeVmLinearTypeFieldEntry(
                    type.Name,
                    field.Name,
                    field.TypeName,
                    field.MinimumEntryAddress,
                    field.MaximumEntryAddress,
                    field.ComputedEntryAddress);
            }))
            .ToArray();
        var diagnosticsBefore = BuildDiagnosticSites(module, code, GameEventScriptBytecodeDiagnosticTiming.BeforeInstruction);
        var diagnosticsAfter = BuildDiagnosticSites(module, code, GameEventScriptBytecodeDiagnosticTiming.AfterInstruction);

        return new GesBytecodeVmLinearExecutable(
            code,
            module.MaxFrameSlots,
            handlers,
            callables,
            callablesByEntryAddress,
            typeFields,
            diagnosticsBefore,
            diagnosticsAfter);
    }

    private static IReadOnlyList<GesBytecodeVmLinearDiagnosticEntry>[] BuildDiagnosticSites(
        GameEventScriptCompiled module,
        IReadOnlyList<GameEventScriptBytecodeInstruction> code,
        GameEventScriptBytecodeDiagnosticTiming timing)
    {
        var sites = new List<GesBytecodeVmLinearDiagnosticEntry>?[code.Count + 1];
        foreach (var site in module.DebugSegment.DiagnosticSites.Where(site => site.Timing == timing))
        {
            if (site.Address < 0 || site.Address > code.Count)
            {
                throw InvalidBytecode($"debug diagnostic site '{site.Name}' references address {site.Address}, outside code range 0..{code.Count}.");
            }

            if (timing == GameEventScriptBytecodeDiagnosticTiming.AfterInstruction &&
                site.Address >= code.Count)
            {
                throw InvalidBytecode($"debug diagnostic site '{site.Name}' cannot run after end address {site.Address}.");
            }

            ValidateOptionalSlot(module, site.Slot, $"debug diagnostic site '{site.Name}' slot");
            sites[site.Address] ??= [];
            sites[site.Address]!.Add(new GesBytecodeVmLinearDiagnosticEntry(site.Kind, site.Name, site.Slot));
        }

        return sites
            .Select(site => (IReadOnlyList<GesBytecodeVmLinearDiagnosticEntry>)(site ?? []))
            .ToArray();
    }

    private static void ValidateCode(GameEventScriptCompiled module, IReadOnlyList<GameEventScriptBytecodeInstruction> code)
    {
        for (var address = 0; address < code.Count; address++)
        {
            ValidateInstruction(module, code, address, code[address]);
        }

        ValidateStageSequences(module, code);
    }

    private static void ValidateStageSequences(GameEventScriptCompiled module, IReadOnlyList<GameEventScriptBytecodeInstruction> code)
    {
        var stagedCount = 0;
        var stageStartAddress = -1;
        for (var address = 0; address < code.Count; address++)
        {
            var instruction = code[address];
            if (IsStageInstruction(instruction.OpCode))
            {
                if (stagedCount == 0)
                {
                    stageStartAddress = address;
                }

                stagedCount++;
                continue;
            }

            if (stagedCount == 0)
            {
                if (instruction.OpCode is GameEventScriptBytecodeOpCode.Call)
                {
                    var expected = GetExpectedStagedArgumentCount(module, instruction);
                    if (expected != 0)
                    {
                        throw InvalidBytecode($"instruction @{address.ToString("0000", System.Globalization.CultureInfo.InvariantCulture)} {instruction.OpCode} expects {expected} staged argument(s), but no stage sequence precedes it.");
                    }
                }
                else if (instruction.OpCode is GameEventScriptBytecodeOpCode.CreateMap)
                {
                    var expected = module.UShortListPool[instruction.SecondaryListIndex].Count;
                    if (expected != 0)
                    {
                        throw InvalidBytecode($"instruction @{address.ToString("0000", System.Globalization.CultureInfo.InvariantCulture)} {instruction.OpCode} expects {expected} staged value(s), but no stage sequence precedes it.");
                    }
                }
                else if (instruction.OpCode is GameEventScriptBytecodeOpCode.CreateRecord or GameEventScriptBytecodeOpCode.CreateExternalType)
                {
                    var expected = module.UShortListPool[instruction.ListIndex].Count;
                    if (expected != 0)
                    {
                        throw InvalidBytecode($"instruction @{address.ToString("0000", System.Globalization.CultureInfo.InvariantCulture)} {instruction.OpCode} expects {expected} staged value(s), but no stage sequence precedes it.");
                    }
                }

                continue;
            }

            if (instruction.OpCode is GameEventScriptBytecodeOpCode.CreateVector or GameEventScriptBytecodeOpCode.CreatePoint)
            {
                ValidateSpatialStagedArgumentCount(instruction, stagedCount, address);
                stagedCount = 0;
                stageStartAddress = -1;
                continue;
            }

            if (instruction.OpCode is GameEventScriptBytecodeOpCode.CreateList)
            {
                stagedCount = 0;
                stageStartAddress = -1;
                continue;
            }

            if (instruction.OpCode is GameEventScriptBytecodeOpCode.CreateMap)
            {
                var expectedStagedValues = module.UShortListPool[instruction.SecondaryListIndex].Count;
                if (expectedStagedValues != stagedCount)
                {
                    throw InvalidBytecode($"instruction @{address.ToString("0000", System.Globalization.CultureInfo.InvariantCulture)} {instruction.OpCode} expects {expectedStagedValues} staged value(s), but {stagedCount} value(s) were staged.");
                }

                stagedCount = 0;
                stageStartAddress = -1;
                continue;
            }

            if (instruction.OpCode is GameEventScriptBytecodeOpCode.CreateRecord or GameEventScriptBytecodeOpCode.CreateExternalType)
            {
                var expectedStagedValues = module.UShortListPool[instruction.ListIndex].Count;
                if (expectedStagedValues != stagedCount)
                {
                    throw InvalidBytecode($"instruction @{address.ToString("0000", System.Globalization.CultureInfo.InvariantCulture)} {instruction.OpCode} expects {expectedStagedValues} staged value(s), but {stagedCount} value(s) were staged.");
                }

                stagedCount = 0;
                stageStartAddress = -1;
                continue;
            }

            if (instruction.OpCode is not GameEventScriptBytecodeOpCode.Call)
            {
                throw InvalidBytecode($"stage sequence starting @{stageStartAddress.ToString("0000", System.Globalization.CultureInfo.InvariantCulture)} is interrupted by instruction @{address.ToString("0000", System.Globalization.CultureInfo.InvariantCulture)} {instruction.OpCode}.");
            }

            var expectedStagedCount = GetExpectedStagedArgumentCount(module, instruction);
            if (expectedStagedCount != stagedCount)
            {
                throw InvalidBytecode($"instruction @{address.ToString("0000", System.Globalization.CultureInfo.InvariantCulture)} {instruction.OpCode} expects {expectedStagedCount} staged argument(s), but {stagedCount} argument(s) were staged.");
            }

            stagedCount = 0;
            stageStartAddress = -1;
        }

        if (stagedCount != 0)
        {
            throw InvalidBytecode($"stage sequence starting @{stageStartAddress.ToString("0000", System.Globalization.CultureInfo.InvariantCulture)} has no following stage consumer.");
        }
    }

    private static void ValidateSpatialStagedArgumentCount(
        GameEventScriptBytecodeInstruction instruction,
        int stagedCount,
        int address)
    {
        if (instruction.ImmediateX is < 0 or > 2)
        {
            throw InvalidBytecode($"instruction @{address.ToString("0000", System.Globalization.CultureInfo.InvariantCulture)} {instruction.OpCode} has invalid component start {instruction.ImmediateX}.");
        }

        var maximum = 3 - instruction.ImmediateX;
        if (stagedCount > maximum)
        {
            throw InvalidBytecode($"instruction @{address.ToString("0000", System.Globalization.CultureInfo.InvariantCulture)} {instruction.OpCode} can consume at most {maximum} staged component(s), but {stagedCount} argument(s) were staged.");
        }
    }

    private static void ValidateInstruction(
        GameEventScriptCompiled module,
        IReadOnlyList<GameEventScriptBytecodeInstruction> code,
        int address,
        GameEventScriptBytecodeInstruction instruction)
    {
        var context = $"instruction @{address.ToString("0000", System.Globalization.CultureInfo.InvariantCulture)} {instruction.OpCode}";
        if (!Enum.IsDefined(typeof(GameEventScriptBytecodeOpCode), instruction.OpCode))
        {
            throw InvalidBytecode($"{context} references unsupported opcode '{instruction.OpCode}'.");
        }

        if (HasDestination(instruction.OpCode))
        {
            ValidateSlot(module, instruction.DestinationSlot, $"{context} destination slot");
        }

        switch (instruction.OpCode)
        {
            case GameEventScriptBytecodeOpCode.Nop:
                break;

            case GameEventScriptBytecodeOpCode.SlotLocals:
                ValidateFrameSlotDelta(module, instruction.Count, $"{context} local slot delta");
                break;

            case GameEventScriptBytecodeOpCode.LoadNothing:
            case GameEventScriptBytecodeOpCode.LoadTrue:
            case GameEventScriptBytecodeOpCode.LoadFalse:
                break;

            case GameEventScriptBytecodeOpCode.LoadInteger:
                ValidateNumericUnit(instruction.UnitAndFlags, $"{context} numeric unit");
                break;

            case GameEventScriptBytecodeOpCode.LoadFloat:
                ValidateNumericUnit(instruction.UnitAndFlags, $"{context} numeric unit");
                break;

            case GameEventScriptBytecodeOpCode.LoadPercentage:
                ValidateNoFlags(instruction.UnitAndFlags, $"{context} percentage flags");
                break;

            case GameEventScriptBytecodeOpCode.LoadText:
                ValidateIndex(module.StringPool.Count, instruction.StringIndex, $"{context} text");
                break;

            case GameEventScriptBytecodeOpCode.LoadTag:
                ValidateIndex(module.StringPool.Count, instruction.StringIndex, $"{context} tag");
                break;

            case GameEventScriptBytecodeOpCode.LoadHandler:
                ValidateMessageShape(module, instruction.ListIndex, $"{context} handler shape");
                break;

            case GameEventScriptBytecodeOpCode.MoveSlot:
                ValidateSlot(module, instruction.XSlot, $"{context} source slot");
                break;

            case GameEventScriptBytecodeOpCode.StageRegister:
                ValidateSlot(module, instruction.XSlot, $"{context} source slot");
                break;

            case GameEventScriptBytecodeOpCode.StageNothing:
            case GameEventScriptBytecodeOpCode.StageTrue:
            case GameEventScriptBytecodeOpCode.StageFalse:
                break;

            case GameEventScriptBytecodeOpCode.StageInteger:
                ValidateNumericUnit(instruction.UnitAndFlags, $"{context} numeric unit");
                break;

            case GameEventScriptBytecodeOpCode.StageFloat:
                ValidateNumericUnit(instruction.UnitAndFlags, $"{context} numeric unit");
                break;

            case GameEventScriptBytecodeOpCode.StagePercentage:
                ValidateNoFlags(instruction.UnitAndFlags, $"{context} percentage flags");
                break;

            case GameEventScriptBytecodeOpCode.StageText:
                ValidateIndex(module.StringPool.Count, instruction.StringIndex, $"{context} text");
                break;

            case GameEventScriptBytecodeOpCode.StageTag:
                ValidateIndex(module.StringPool.Count, instruction.StringIndex, $"{context} tag");
                break;

            case GameEventScriptBytecodeOpCode.Jump:
                ValidateAddress(module, code, instruction.TargetAddress, $"{context} target");
                break;

            case GameEventScriptBytecodeOpCode.JumpIfTrue:
            case GameEventScriptBytecodeOpCode.JumpIfFalse:
            case GameEventScriptBytecodeOpCode.JumpIfNotTrue:
                ValidateSlot(module, instruction.ConditionSlot, $"{context} condition slot");
                ValidateAddress(module, code, instruction.TargetAddress, $"{context} target");
                break;

            case GameEventScriptBytecodeOpCode.ReturnVoid:
                break;

            case GameEventScriptBytecodeOpCode.ReturnValue:
                ValidateSlot(module, instruction.XSlot, $"{context} return slot");
                break;

            case GameEventScriptBytecodeOpCode.EmitMessage:
            case GameEventScriptBytecodeOpCode.PublishMessage:
                ValidateOutboundMessageSignature(module, instruction.MessageDestination, $"{context} outbound message signature");
                ValidateOutboundMessageArgumentSlotList(module, instruction.MessageDestination, instruction.ListIndex, $"{context} argument slots");
                break;

            case GameEventScriptBytecodeOpCode.EmitMessageWithTags:
            case GameEventScriptBytecodeOpCode.PublishMessageWithTags:
                ValidateOutboundMessageSignature(module, instruction.MessageDestination, $"{context} outbound message signature");
                ValidateOutboundMessageArgumentSlotList(module, instruction.MessageDestination, instruction.ListIndex, $"{context} argument slots");
                ValidateSlotListIndex(module, instruction.SecondaryListIndex, $"{context} tag slot list");
                break;

            case GameEventScriptBytecodeOpCode.EmitMessageValue:
            case GameEventScriptBytecodeOpCode.PublishMessageValue:
                ValidateSlot(module, instruction.XSlot, $"{context} message slot");
                break;

            case GameEventScriptBytecodeOpCode.EmitMessageValueWithTags:
            case GameEventScriptBytecodeOpCode.PublishMessageValueWithTags:
                ValidateSlot(module, instruction.XSlot, $"{context} message slot");
                ValidateSlotListIndex(module, instruction.ListIndex, $"{context} tag slot list");
                break;

            case GameEventScriptBytecodeOpCode.CreateRangeIterator:
                ValidateSlot(module, instruction.XSlot, $"{context} from slot");
                ValidateSlot(module, instruction.YSlot, $"{context} to slot");
                break;

            case GameEventScriptBytecodeOpCode.CreateRangeIteratorWithStep:
                ValidateSlot(module, instruction.XSlot, $"{context} from slot");
                ValidateSlot(module, instruction.YSlot, $"{context} to slot");
                ValidateSlot(module, instruction.AU, $"{context} step slot");
                break;

            case GameEventScriptBytecodeOpCode.CreateRangeIteratorShort:
                break;

            case GameEventScriptBytecodeOpCode.StreamCreate:
                ValidateSlot(module, instruction.XSlot, $"{context} collection slot");
                break;

            case GameEventScriptBytecodeOpCode.StreamNext:
                ValidateSlot(module, instruction.XSlot, $"{context} iterator slot");
                ValidateAddress(module, code, instruction.TargetAddress, $"{context} no-more target");
                break;

            case GameEventScriptBytecodeOpCode.StreamClose:
                ValidateSlot(module, instruction.XSlot, $"{context} iterator slot");
                break;

            case GameEventScriptBytecodeOpCode.RandomPush:
                ValidateSlot(module, instruction.XSlot, $"{context} seed slot");
                break;

            case GameEventScriptBytecodeOpCode.RandomPushConstant:
            case GameEventScriptBytecodeOpCode.RandomPop:
                break;

            case GameEventScriptBytecodeOpCode.MemberAccess:
                ValidateIndex(module.StringPool.Count, instruction.StringIndex, $"{context} member name");
                ValidateSlot(module, instruction.YSlot, $"{context} source slot");
                break;

            case GameEventScriptBytecodeOpCode.PropertyAccess:
                ValidateSlot(module, instruction.XSlot, $"{context} property slot");
                ValidateSlot(module, instruction.YSlot, $"{context} source slot");
                break;

            case GameEventScriptBytecodeOpCode.PipelineStream:
                ValidateSlot(module, instruction.XSlot, $"{context} source iterator slot");
                ValidateEntryAddress(module, code, instruction.EntryAddress, $"{context} iterator entry");
                ValidateSlot(module, instruction.AU, $"{context} item binding slot");
                ValidateSlotListIndex(module, instruction.BU, $"{context} capture slot list");
                break;

            case GameEventScriptBytecodeOpCode.StreamCollectList:
            case GameEventScriptBytecodeOpCode.StreamCollectFirst:
            case GameEventScriptBytecodeOpCode.StreamCollectLast:
            case GameEventScriptBytecodeOpCode.StreamCollectSingle:
            case GameEventScriptBytecodeOpCode.PipelineHasAny:
            case GameEventScriptBytecodeOpCode.PipelineHasAll:
            case GameEventScriptBytecodeOpCode.PipelineContainsSingle:
            case GameEventScriptBytecodeOpCode.PipelineContainsAny:
            case GameEventScriptBytecodeOpCode.PipelineContainsAll:
            case GameEventScriptBytecodeOpCode.PipelineDistinct:
            case GameEventScriptBytecodeOpCode.PipelineReverse:
            case GameEventScriptBytecodeOpCode.PipelineSortAscending:
            case GameEventScriptBytecodeOpCode.PipelineSortDescending:
            case GameEventScriptBytecodeOpCode.PipelineTakeFirst:
            case GameEventScriptBytecodeOpCode.PipelineTakeLast:
            case GameEventScriptBytecodeOpCode.PipelineTakeHighest:
            case GameEventScriptBytecodeOpCode.PipelineTakeLowest:
            case GameEventScriptBytecodeOpCode.PipelineDropFirst:
            case GameEventScriptBytecodeOpCode.PipelineDropLast:
            case GameEventScriptBytecodeOpCode.PipelineDropHighest:
            case GameEventScriptBytecodeOpCode.PipelineDropLowest:
            case GameEventScriptBytecodeOpCode.PipelineShuffle:
            case GameEventScriptBytecodeOpCode.PipelineDraw:
            case GameEventScriptBytecodeOpCode.PipelineChoose:
            case GameEventScriptBytecodeOpCode.PipelineChooseRandom:
            case GameEventScriptBytecodeOpCode.PipelineDicePatternCountAny:
            case GameEventScriptBytecodeOpCode.PipelineDicePatternFullHouse:
            case GameEventScriptBytecodeOpCode.PipelineDicePatternStraight:
            case GameEventScriptBytecodeOpCode.PipelineTakePatternCountAny:
            case GameEventScriptBytecodeOpCode.PipelineTakePatternFullHouse:
            case GameEventScriptBytecodeOpCode.PipelineTakePatternStraight:
                ValidateSlot(module, instruction.XSlot, $"{context} iterator slot");
                break;

            case GameEventScriptBytecodeOpCode.StreamReduce:
                ValidateSlot(module, instruction.XSlot, $"{context} iterator slot");
                ValidateSlot(module, instruction.YSlot, $"{context} item binding slot");
                ValidateEntryAddress(module, code, instruction.AU, $"{context} reducer entry");
                break;

            case GameEventScriptBytecodeOpCode.StreamReduceOrDefault:
            case GameEventScriptBytecodeOpCode.StreamFold:
                ValidateSlot(module, instruction.XSlot, $"{context} iterator slot");
                ValidateSlot(module, instruction.YSlot, $"{context} seed/default slot");
                ValidateSlot(module, instruction.AU, $"{context} item binding slot");
                ValidateEntryAddress(module, code, instruction.BU, $"{context} reducer entry");
                break;

            case GameEventScriptBytecodeOpCode.StreamCollectMap:
            case GameEventScriptBytecodeOpCode.PipelineDistinctBy:
            case GameEventScriptBytecodeOpCode.PipelineGroupBy:
            case GameEventScriptBytecodeOpCode.PipelineOrderByAscending:
            case GameEventScriptBytecodeOpCode.PipelineOrderByDescending:
                ValidateSlot(module, instruction.XSlot, $"{context} iterator slot");
                ValidateSlot(module, instruction.YSlot, $"{context} item binding slot");
                ValidateEntryAddress(module, code, instruction.AU, $"{context} entry");
                break;

            case GameEventScriptBytecodeOpCode.StreamCollectMapValue:
                ValidateSlot(module, instruction.XSlot, $"{context} iterator slot");
                ValidateSlot(module, instruction.YSlot, $"{context} item binding slot");
                ValidateEntryAddress(module, code, instruction.AU, $"{context} key entry");
                ValidateEntryAddress(module, code, instruction.BU, $"{context} value entry");
                break;

            case GameEventScriptBytecodeOpCode.PipelineChooseWeighted:
                ValidateSlot(module, instruction.XSlot, $"{context} iterator slot");
                ValidateSlot(module, instruction.AU, $"{context} item binding slot");
                ValidateEntryAddress(module, code, instruction.BU, $"{context} weight entry");
                break;

            case GameEventScriptBytecodeOpCode.PipelineDicePatternCountFace:
            case GameEventScriptBytecodeOpCode.PipelineTakePatternCountFace:
                ValidateSlot(module, instruction.XSlot, $"{context} iterator slot");
                ValidateEntryAddress(module, code, instruction.AU, $"{context} face entry");
                break;

            case GameEventScriptBytecodeOpCode.SeriesTerm:
                ValidateSlot(module, instruction.XSlot, $"{context} series slot");
                ValidateSlot(module, instruction.YSlot, $"{context} index slot");
                break;

            case GameEventScriptBytecodeOpCode.SeriesTake:
            case GameEventScriptBytecodeOpCode.SeriesDrop:
                ValidateSlot(module, instruction.XSlot, $"{context} series slot");
                break;

            case GameEventScriptBytecodeOpCode.PipelineListCreateBuilder:
                break;

            case GameEventScriptBytecodeOpCode.PipelineListBuilderAdd:
                ValidateSlot(module, instruction.XSlot, $"{context} collection builder slot");
                ValidateSlot(module, instruction.YSlot, $"{context} collection item slot");
                break;

            case GameEventScriptBytecodeOpCode.PipelineListBuilderFinish:
                ValidateSlot(module, instruction.XSlot, $"{context} collection builder slot");
                break;

            case GameEventScriptBytecodeOpCode.CreateDice:
                ValidateNonNegative(instruction.Count, $"{context} dice count");
                ValidateNonNegative(instruction.ImmediateY, $"{context} dice sides");
                break;

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
                ValidateSlot(module, instruction.XSlot, $"{context} operand slot");
                break;

            case GameEventScriptBytecodeOpCode.CreateRange:
                ValidateSlot(module, instruction.XSlot, $"{context} from slot");
                ValidateSlot(module, instruction.YSlot, $"{context} to slot");
                break;

            case GameEventScriptBytecodeOpCode.CreateRangeWithStep:
                ValidateSlot(module, instruction.XSlot, $"{context} from slot");
                ValidateSlot(module, instruction.YSlot, $"{context} to slot");
                ValidateSlot(module, instruction.AU, $"{context} step slot");
                break;

            case GameEventScriptBytecodeOpCode.Clamp:
                ValidateSlot(module, instruction.XSlot, $"{context} value slot");
                ValidateSlot(module, instruction.YSlot, $"{context} minimum slot");
                ValidateSlot(module, instruction.AU, $"{context} maximum slot");
                break;

            case GameEventScriptBytecodeOpCode.RandomTake:
                ValidateSlot(module, instruction.XSlot, $"{context} from slot");
                ValidateSlot(module, instruction.YSlot, $"{context} to slot");
                break;

            case GameEventScriptBytecodeOpCode.IndexAccess:
                ValidateSlot(module, instruction.YSlot, $"{context} source slot");
                break;

            case GameEventScriptBytecodeOpCode.CreateRecord:
                ValidateIndex(module.StringPool.Count, instruction.StringIndex, $"{context} type name");
                ValidateStringListIndex(module, instruction.ListIndex, $"{context} argument names");
                break;

            case GameEventScriptBytecodeOpCode.CreateExternalType:
                ValidateIndex(module.ExternalTypeConstructorReferences.Count, instruction.ExternalReferenceIndex, $"{context} external type constructor reference");
                ValidateStringListIndex(module, instruction.ListIndex, $"{context} argument names");
                ValidateExternalTypeConstructorArgumentNames(module, instruction.ExternalReferenceIndex, instruction.ListIndex, $"{context} argument names");
                break;

            case GameEventScriptBytecodeOpCode.CreateVector:
            case GameEventScriptBytecodeOpCode.CreatePoint:
                ValidateSpatialComponentStart(instruction.ImmediateX, $"{context} component start");
                break;

            case GameEventScriptBytecodeOpCode.CreateList:
                break;

            case GameEventScriptBytecodeOpCode.CreateMap:
                ValidateStringListIndex(module, instruction.SecondaryListIndex, $"{context} keys");
                break;

            case GameEventScriptBytecodeOpCode.LoadMessage:
                ValidateMessageShape(module, instruction.SecondaryListIndex, $"{context} message shape");
                ValidateMessageArgumentSlotList(module, instruction.SecondaryListIndex, instruction.ListIndex, $"{context} argument slots");
                break;

            case GameEventScriptBytecodeOpCode.BindHandler:
                ValidateSlot(module, instruction.XSlot, $"{context} handler slot");
                ValidateSlotListIndex(module, instruction.ListIndex, $"{context} argument slots");
                break;

            case GameEventScriptBytecodeOpCode.CallStandard:
                ValidateExtensionShape(module, instruction.SecondaryListIndex, $"{context} standard extension shape");
                ValidateSlotListIndex(module, instruction.ListIndex, $"{context} argument slots");
                ValidateExtensionShapeArgumentSlots(module, instruction.SecondaryListIndex, instruction.ListIndex, $"{context} argument slots");
                break;

            case GameEventScriptBytecodeOpCode.CallExternal:
                ValidateIndex(module.ExternalReferences.Count, instruction.ExternalReferenceIndex, $"{context} external reference");
                ValidateSlotListIndex(module, instruction.ListIndex, $"{context} argument slots");
                ValidateExternalReferenceArgumentSlots(module, instruction.ExternalReferenceIndex, instruction.ListIndex, $"{context} argument slots");
                break;

            case GameEventScriptBytecodeOpCode.Call:
                ValidateEntryAddress(module, code, instruction.EntryAddress, $"{context} callable entry");
                if (instruction.HasInstructionFlag(GameEventScriptInstructionFlag.NormalizeResultAsPredicate))
                {
                    ValidatePredicateCallEntry(module, instruction.EntryAddress, $"{context} predicate entry");
                }
                else
                {
                    ValidateCallableEntry(module, instruction.EntryAddress, $"{context} callable entry");
                }
                break;

            default:
                if (IsCastInstruction(instruction.OpCode))
                {
                    ValidateSlot(module, instruction.XSlot, $"{context} source slot");
                    ValidateCastOrTypeCheckOperand(module, instruction, $"{context} type operand");

                    break;
                }

                if (IsTypeCheckInstruction(instruction.OpCode))
                {
                    ValidateSlot(module, instruction.XSlot, $"{context} source slot");
                    ValidateCastOrTypeCheckOperand(module, instruction, $"{context} type operand");

                    break;
                }

                if (IsBinarySlotInstruction(instruction.OpCode))
                {
                    ValidateSlot(module, instruction.XSlot, $"{context} left slot");
                    ValidateSlot(module, instruction.YSlot, $"{context} right slot");
                    break;
                }

                throw InvalidBytecode($"{context} references unsupported opcode '{instruction.OpCode}'.");
        }
    }

    private static void ValidateSideTables(GameEventScriptCompiled module)
    {
        ValidateOutboundMessageSignatures(module);
        ValidateDebugSegment(module);
    }

    private static void ValidateOutboundMessageSignatures(GameEventScriptCompiled module)
    {
        for (var index = 0; index < module.OutboundMessageSignatures.Count; index++)
        {
            var signature = module.OutboundMessageSignatures[index];
            if (string.IsNullOrWhiteSpace(signature.Name))
            {
                throw InvalidBytecode($"outbound message signature #{index} has an empty message name.");
            }

            for (var parameterIndex = 0; parameterIndex < signature.Parameters.Count; parameterIndex++)
            {
                if (string.IsNullOrWhiteSpace(signature.Parameters[parameterIndex]))
                {
                    throw InvalidBytecode($"outbound message signature #{index} has an empty argument name at position {parameterIndex}.");
                }
            }
        }
    }

    private static void ValidateDebugSegment(GameEventScriptCompiled module)
    {
        for (var index = 0; index < module.DebugSegment.DiagnosticSites.Count; index++)
        {
            var site = module.DebugSegment.DiagnosticSites[index];
            var context = $"debug diagnostic site #{index}";
            if (site.Address < 0 || site.Address > module.Code.Count)
            {
                throw InvalidBytecode($"{context} references address {site.Address}, outside code range 0..{module.Code.Count}.");
            }

            if (site.Timing == GameEventScriptBytecodeDiagnosticTiming.AfterInstruction &&
                site.Address >= module.Code.Count)
            {
                throw InvalidBytecode($"{context} cannot run after end address {site.Address}.");
            }

            ValidateOptionalSlot(module, site.Slot, $"{context} slot");
        }
    }

    private static void ValidateMessageShape(GameEventScriptCompiled module, int index, string context)
    {
        ValidateIndex(module.UShortListPool.Count, index, context);
        var shape = module.UShortListPool[index];
        if (shape.Count == 0)
        {
            throw InvalidBytecode($"{context} must contain a message name string-pool index.");
        }

        for (var partIndex = 0; partIndex < shape.Count; partIndex++)
        {
            ValidateIndex(module.StringPool.Count, shape[partIndex], $"{context} string #{partIndex}");
        }
    }

    private static void ValidateMessageArgumentSlotList(GameEventScriptCompiled module, int shapeIndex, int slotListIndex, string context)
    {
        var argumentCount = module.UShortListPool[shapeIndex].Count - 1;
        if (argumentCount == 0)
        {
            ValidateIndex(module.UShortListPool.Count, slotListIndex, context);
            if (module.UShortListPool[slotListIndex].Count != 0)
            {
                throw InvalidBytecode($"{context} must be empty for a message shape without arguments.");
            }

            return;
        }

        ValidateIndex(module.UShortListPool.Count, slotListIndex, context);
        var slots = module.UShortListPool[slotListIndex];
        if (slots.Count != argumentCount)
        {
            throw InvalidBytecode($"{context} count {slots.Count} does not match message shape argument count {argumentCount}.");
        }

        ValidateSlotList(module, slots, context);
    }

    private static void ValidateOutboundMessageSignature(GameEventScriptCompiled module, int index, string context)
    {
        ValidateIndex(module.OutboundMessageSignatures.Count, index, context);
        var signature = module.OutboundMessageSignatures[index];
        if (string.IsNullOrWhiteSpace(signature.Name))
        {
            throw InvalidBytecode($"{context} must contain a message name.");
        }
    }

    private static void ValidateOutboundMessageArgumentSlotList(GameEventScriptCompiled module, int signatureIndex, int slotListIndex, string context)
    {
        ValidateOutboundMessageSignature(module, signatureIndex, context);
        ValidateIndex(module.UShortListPool.Count, slotListIndex, context);
        var argumentCount = module.OutboundMessageSignatures[signatureIndex].Parameters.Count;
        var slots = module.UShortListPool[slotListIndex];
        if (slots.Count != argumentCount)
        {
            throw InvalidBytecode($"{context} count {slots.Count} does not match outbound message argument count {argumentCount}.");
        }

        ValidateSlotList(module, slots, context);
    }

    private static void ValidateExtensionShape(GameEventScriptCompiled module, int index, string context)
    {
        ValidateStringListIndex(module, index, context);
        var shape = module.UShortListPool[index];
        if (shape.Count < 2)
        {
            throw InvalidBytecode($"{context} must contain extension and function string-pool indexes.");
        }
    }

    private static void ValidateExtensionShapeArgumentSlots(GameEventScriptCompiled module, int shapeIndex, int slotListIndex, string context)
    {
        var argumentCount = module.UShortListPool[shapeIndex].Count - 2;
        var slots = module.UShortListPool[slotListIndex];
        if (slots.Count != argumentCount)
        {
            throw InvalidBytecode($"{context} count {slots.Count} does not match extension shape argument count {argumentCount}.");
        }
    }

    private static void ValidateExternalReferenceArgumentSlots(GameEventScriptCompiled module, int referenceIndex, int slotListIndex, string context)
    {
        var argumentCount = module.ExternalReferences[referenceIndex].ArgumentLabels.Count;
        var slots = module.UShortListPool[slotListIndex];
        if (slots.Count != argumentCount)
        {
            throw InvalidBytecode($"{context} count {slots.Count} does not match external reference argument count {argumentCount}.");
        }
    }

    private static void ValidateExternalTypeConstructorArgumentNames(GameEventScriptCompiled module, int referenceIndex, int nameListIndex, string context)
    {
        var argumentCount = module.ExternalTypeConstructorReferences[referenceIndex].ArgumentLabels.Count;
        var names = module.UShortListPool[nameListIndex];
        if (names.Count != argumentCount)
        {
            throw InvalidBytecode($"{context} count {names.Count} does not match external type constructor argument count {argumentCount}.");
        }
    }

    private static void ValidateCallableArgumentSlots(GameEventScriptCompiled module, int entryAddress, int slotListIndex, string context)
    {
        var callable = module.Callables.Values.FirstOrDefault(candidate => candidate.EntryAddress == entryAddress);
        if (callable is null)
        {
            throw InvalidBytecode($"{context} references unknown callable entry address {entryAddress}.");
        }

        var slots = module.UShortListPool[slotListIndex];
        if (slots.Count != callable.Parameters.Count)
        {
            throw InvalidBytecode($"{context} count {slots.Count} does not match callable parameter count {callable.Parameters.Count}.");
        }
    }

    private static void ValidateCallableArgumentCount(GameEventScriptCompiled module, int entryAddress, int argumentCount, string context)
    {
        var callable = module.Callables.Values.FirstOrDefault(candidate => candidate.EntryAddress == entryAddress);
        if (callable is null)
        {
            throw InvalidBytecode($"{context} references unknown callable entry address {entryAddress}.");
        }

        if (argumentCount != callable.Parameters.Count)
        {
            throw InvalidBytecode($"{context} {argumentCount} does not match callable parameter count {callable.Parameters.Count}.");
        }
    }

    private static int GetExpectedStagedArgumentCount(GameEventScriptCompiled module, GameEventScriptBytecodeInstruction instruction)
    {
        var callable = module.Callables.Values.FirstOrDefault(candidate => candidate.EntryAddress == instruction.EntryAddress);
        if (callable is null)
        {
            throw InvalidBytecode($"{instruction.OpCode} references unknown callable entry address {instruction.EntryAddress}.");
        }

        if (instruction.OpCode == GameEventScriptBytecodeOpCode.Call &&
            instruction.HasInstructionFlag(GameEventScriptInstructionFlag.NormalizeResultAsPredicate) &&
            callable.Kind != GameEventScriptBytecodeCallableKind.Predicate)
        {
            throw InvalidBytecode($"{instruction.OpCode} references function '{callable.Name}' instead of a predicate.");
        }

        return callable.Parameters.Count;
    }

    private static void ValidateCallableEntry(GameEventScriptCompiled module, int entryAddress, string context)
    {
        if (module.Callables.Values.All(candidate => candidate.EntryAddress != entryAddress))
        {
            throw InvalidBytecode($"{context} references unknown callable entry address {entryAddress}.");
        }
    }

    private static void ValidatePredicateCallArgumentSlots(GameEventScriptCompiled module, int entryAddress, int slotListIndex, string context)
    {
        var callable = module.Callables.Values.FirstOrDefault(candidate => candidate.EntryAddress == entryAddress);
        if (callable is null)
        {
            throw InvalidBytecode($"{context} references unknown predicate entry address {entryAddress}.");
        }

        if (callable.Kind != GameEventScriptBytecodeCallableKind.Predicate)
        {
            throw InvalidBytecode($"{context} references function '{callable.Name}' instead of a predicate.");
        }

        var slots = module.UShortListPool[slotListIndex];
        if (slots.Count != callable.Parameters.Count)
        {
            throw InvalidBytecode($"{context} count {slots.Count} does not match predicate parameter count {callable.Parameters.Count}.");
        }
    }

    private static void ValidatePredicateCallArgumentCount(GameEventScriptCompiled module, int entryAddress, int argumentCount, string context)
    {
        var callable = module.Callables.Values.FirstOrDefault(candidate => candidate.EntryAddress == entryAddress);
        if (callable is null)
        {
            throw InvalidBytecode($"{context} references unknown predicate entry address {entryAddress}.");
        }

        if (callable.Kind != GameEventScriptBytecodeCallableKind.Predicate)
        {
            throw InvalidBytecode($"{context} references function '{callable.Name}' instead of a predicate.");
        }

        if (argumentCount != callable.Parameters.Count)
        {
            throw InvalidBytecode($"{context} {argumentCount} does not match predicate parameter count {callable.Parameters.Count}.");
        }
    }

    private static void ValidatePredicateCallEntry(GameEventScriptCompiled module, int entryAddress, string context)
    {
        var callable = module.Callables.Values.FirstOrDefault(candidate => candidate.EntryAddress == entryAddress);
        if (callable is null)
        {
            throw InvalidBytecode($"{context} references unknown predicate entry address {entryAddress}.");
        }

        if (callable.Kind != GameEventScriptBytecodeCallableKind.Predicate)
        {
            throw InvalidBytecode($"{context} references function '{callable.Name}' instead of a predicate.");
        }
    }

    private static void ValidateMatchingListCounts(GameEventScriptCompiled module, int leftIndex, int rightIndex, string context)
    {
        var leftCount = module.UShortListPool[leftIndex].Count;
        var rightCount = module.UShortListPool[rightIndex].Count;
        if (leftCount != rightCount)
        {
            throw InvalidBytecode($"{context} count mismatch: {leftCount} name(s) for {rightCount} slot(s).");
        }
    }

    private static void ValidateStringListIndex(GameEventScriptCompiled module, int index, string context)
    {
        ValidateIndex(module.UShortListPool.Count, index, context);
        var values = module.UShortListPool[index];
        for (var itemIndex = 0; itemIndex < values.Count; itemIndex++)
        {
            ValidateIndex(module.StringPool.Count, values[itemIndex], $"{context} string #{itemIndex}");
        }
    }

    private static void ValidateSlotListIndex(GameEventScriptCompiled module, int index, string context)
    {
        ValidateIndex(module.UShortListPool.Count, index, context);
        ValidateSlotList(module, module.UShortListPool[index], context);
    }

    private static void ValidateSlotList(GameEventScriptCompiled module, IReadOnlyList<ushort> slots, string context)
    {
        foreach (var slot in slots)
        {
            ValidateSlot(module, slot, context);
        }
    }

    private static void ValidateFrameSlotCount(GameEventScriptCompiled module, int slotCount, string context)
    {
        if (slotCount < 0 || slotCount > module.MaxFrameSlots)
        {
            throw InvalidBytecode($"{context} is outside max frame slots 0..{module.MaxFrameSlots}.");
        }
    }

    private static void ValidateFrameSlotDelta(GameEventScriptCompiled module, int slotDelta, string context)
    {
        if (Math.Abs(slotDelta) > module.MaxFrameSlots)
        {
            throw InvalidBytecode($"{context} is outside max frame slots -{module.MaxFrameSlots}..{module.MaxFrameSlots}.");
        }
    }

    private static int ReadEntrySlotCount(
        GameEventScriptCompiled module,
        IReadOnlyList<GameEventScriptBytecodeInstruction> code,
        int entryAddress,
        string context)
    {
        if ((uint)entryAddress >= (uint)code.Count)
        {
            throw InvalidBytecode($"{context} references address @{entryAddress}, outside code segment 0..{code.Count - 1}.");
        }

        var prolog = code[entryAddress];
        if (prolog.OpCode != GameEventScriptBytecodeOpCode.SlotLocals)
        {
            throw InvalidBytecode($"{context} must start with SlotLocals.");
        }

        ValidateFrameSlotCount(module, prolog.Count, $"{context} SlotLocals count");
        if (prolog.Count < 0)
        {
            throw InvalidBytecode($"{context} SlotLocals prolog cannot release slots.");
        }

        return prolog.Count;
    }

    private static void ValidateCastOrTypeCheckOperand(GameEventScriptCompiled module, GameEventScriptBytecodeInstruction instruction, string context)
    {
        if (instruction.OpCode is GameEventScriptBytecodeOpCode.CastNumeric or
            GameEventScriptBytecodeOpCode.CheckNumeric or
            GameEventScriptBytecodeOpCode.CheckInteger or
            GameEventScriptBytecodeOpCode.CheckFractional)
        {
            ValidateNoFlags(instruction.UnitAndFlags, $"{context} flags");
            return;
        }

        if (instruction.OpCode is GameEventScriptBytecodeOpCode.CastUnit or GameEventScriptBytecodeOpCode.CheckUnit)
        {
            ValidateNumericUnit(instruction.UnitAndFlags, $"{context} unit");
            if (instruction.Unit == GameEventScriptBytecodeInstructionUnit.UnitNone)
            {
                throw InvalidBytecode($"{context} unit must not be UnitNone.");
            }

            return;
        }

        ValidateNoFlags(instruction.UnitAndFlags, $"{context} flags");
        if (instruction.OpCode is GameEventScriptBytecodeOpCode.CastCustom or GameEventScriptBytecodeOpCode.CheckCustomType)
        {
            ValidateIndex(module.StringPool.Count, instruction.TypeOperand, $"{context} custom type name");
            return;
        }

        var typeKind = (GameEventScriptBytecodeTypeKind)instruction.TypeOperand;
        if (!Enum.IsDefined(typeof(GameEventScriptBytecodeTypeKind), typeKind) ||
            typeKind is GameEventScriptBytecodeTypeKind.Invalid or GameEventScriptBytecodeTypeKind.Custom)
        {
            throw InvalidBytecode($"{context} references unknown type kind {instruction.TypeOperand}.");
        }
    }

    private static void ValidateSlot(GameEventScriptCompiled module, int slot, string context)
    {
        if (slot < 0 || slot >= module.MaxFrameSlots)
        {
            throw InvalidBytecode($"{context} references slot {slot}, outside max frame slots 0..{module.MaxFrameSlots - 1}.");
        }
    }

    private static void ValidateOptionalSlot(GameEventScriptCompiled module, int slot, string context)
    {
        if (slot >= 0)
        {
            ValidateSlot(module, slot, context);
        }
    }

    private static void ValidateAddress(
        GameEventScriptCompiled module,
        IReadOnlyList<GameEventScriptBytecodeInstruction> code,
        int address,
        string context)
    {
        if (address < 0 || address >= code.Count)
        {
            throw InvalidBytecode($"{context} references address @{address}, outside code segment 0..{code.Count - 1}.");
        }
    }

    private static void ValidateEntryAddress(
        GameEventScriptCompiled module,
        IReadOnlyList<GameEventScriptBytecodeInstruction> code,
        int address,
        string context)
    {
        ValidateAddress(module, code, address, context);
        if (code[address].OpCode != GameEventScriptBytecodeOpCode.SlotLocals)
        {
            throw InvalidBytecode($"{context} must reference a SlotLocals entry prolog.");
        }
    }

    private static void ValidateOptionalEntryAddress(
        GameEventScriptCompiled module,
        IReadOnlyList<GameEventScriptBytecodeInstruction> code,
        int address,
        string context)
    {
        if (address >= 0)
        {
            ValidateEntryAddress(module, code, address, context);
        }
    }

    private static void ValidateIndex(int count, int index, string context)
    {
        if (index < 0 || index >= count)
        {
            throw InvalidBytecode($"{context} references index {index}, outside table 0..{count - 1}.");
        }
    }

    private static void ValidateOptionalIndex(int count, int index, string context)
    {
        if (index >= 0)
        {
            ValidateIndex(count, index, context);
        }
    }

    private static void ValidateNonNegative(int value, string context)
    {
        if (value < 0)
        {
            throw InvalidBytecode($"{context} must be non-negative but was {value}.");
        }
    }

    private static void ValidateSpatialComponentStart(int value, string context)
    {
        if (value is < 0 or > 2)
        {
            throw InvalidBytecode($"{context} must be 0, 1, or 2 but was {value}.");
        }
    }

    private static void ValidateNumericUnit(byte value, string context)
    {
        var unit = (GameEventScriptBytecodeInstructionUnit)GameEventScriptBytecodeInstruction.WithoutInstructionFlags(value);
        if (unit is GameEventScriptBytecodeInstructionUnit.UnitNone or
            GameEventScriptBytecodeInstructionUnit.UnitDegree or
            GameEventScriptBytecodeInstructionUnit.UnitMeter or
            GameEventScriptBytecodeInstructionUnit.UnitSecond)
        {
            return;
        }

        if (!Enum.IsDefined(typeof(GameEventScriptBytecodeInstructionUnit), value))
        {
            throw InvalidBytecode($"{context} references unknown unit {value}.");
        }

        throw InvalidBytecode($"{context} cannot use unit '{unit}'.");
    }

    private static void ValidateNoFlags(byte value, string context)
    {
        if (value != 0)
        {
            throw InvalidBytecode($"{context} must be 0 but was {value}.");
        }
    }

    private static InvalidOperationException InvalidBytecode(string message)
        => new($"Invalid GameEventScript linear bytecode: {message}");

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

    private static bool IsBinarySlotInstruction(GameEventScriptBytecodeOpCode opCode)
        => opCode is GameEventScriptBytecodeOpCode.Or or
            GameEventScriptBytecodeOpCode.Xor or
            GameEventScriptBytecodeOpCode.And or
            GameEventScriptBytecodeOpCode.Power or
            GameEventScriptBytecodeOpCode.Implies or
            GameEventScriptBytecodeOpCode.Equal or
            GameEventScriptBytecodeOpCode.NotEqual or
            GameEventScriptBytecodeOpCode.ApproxEqual or
            GameEventScriptBytecodeOpCode.Less or
            GameEventScriptBytecodeOpCode.Greater or
            GameEventScriptBytecodeOpCode.LessOrEqual or
            GameEventScriptBytecodeOpCode.GreaterOrEqual or
            GameEventScriptBytecodeOpCode.Add or
            GameEventScriptBytecodeOpCode.Subtract or
            GameEventScriptBytecodeOpCode.Multiply or
            GameEventScriptBytecodeOpCode.Divide or
            GameEventScriptBytecodeOpCode.IntegerDivide or
            GameEventScriptBytecodeOpCode.Modulo or
            GameEventScriptBytecodeOpCode.Remainder or
            GameEventScriptBytecodeOpCode.Default or
            GameEventScriptBytecodeOpCode.Contains or
            GameEventScriptBytecodeOpCode.ContainsValue or
            GameEventScriptBytecodeOpCode.StartsWith or
            GameEventScriptBytecodeOpCode.EndsWith or
            GameEventScriptBytecodeOpCode.Union or
            GameEventScriptBytecodeOpCode.Intersect or
            GameEventScriptBytecodeOpCode.Zip or
            GameEventScriptBytecodeOpCode.Min or
            GameEventScriptBytecodeOpCode.Max;
}

internal sealed class GesBytecodeVmLinearHandlerEntry(
    string message,
    string signatureId,
    GameEventScriptBytecodeHandlerDispatchKind dispatchKind,
    int declarationOrder,
    int entryAddress,
    int localSlotCount,
    IReadOnlyList<string> parameters,
    IReadOnlyList<string?> parameterTypes,
    IReadOnlyDictionary<string, int> slots)
{
    public string Message { get; } = message;

    public string SignatureId { get; } = signatureId;

    public GameEventScriptBytecodeHandlerDispatchKind DispatchKind { get; } = dispatchKind;

    public int DeclarationOrder { get; } = declarationOrder;

    public int EntryAddress { get; } = entryAddress;

    public int LocalSlotCount { get; } = localSlotCount;

    public IReadOnlyList<string> Parameters { get; } = parameters;

    public IReadOnlyList<string?> ParameterTypes { get; } = parameterTypes;

    public IReadOnlyDictionary<string, int> Slots { get; } = slots;
}

internal sealed class GesBytecodeVmLinearCallableEntry(
    string name,
    string signatureId,
    GameEventScriptBytecodeCallableKind kind,
    int entryAddress,
    int localSlotCount,
    int returnSlot,
    IReadOnlyList<string> parameters,
    IReadOnlyList<string?> parameterTypes)
{
    public string Name { get; } = name;

    public string SignatureId { get; } = signatureId;

    public GameEventScriptBytecodeCallableKind Kind { get; } = kind;

    public int EntryAddress { get; } = entryAddress;

    public int LocalSlotCount { get; } = localSlotCount;

    public int ReturnSlot { get; } = returnSlot;

    public IReadOnlyList<string> Parameters { get; } = parameters;

    public IReadOnlyList<string?> ParameterTypes { get; } = parameterTypes;
}

internal sealed class GesBytecodeVmLinearTypeFieldEntry(
    string typeName,
    string fieldName,
    string fieldTypeName,
    int minimumEntryAddress,
    int maximumEntryAddress,
    int computedEntryAddress)
{
    public string TypeName { get; } = typeName;

    public string FieldName { get; } = fieldName;

    public string FieldTypeName { get; } = fieldTypeName;

    public int MinimumEntryAddress { get; } = minimumEntryAddress;

    public int MaximumEntryAddress { get; } = maximumEntryAddress;

    public int ComputedEntryAddress { get; } = computedEntryAddress;
}

internal sealed class GesBytecodeVmLinearDiagnosticEntry(
    GameEventScriptBytecodeDiagnosticKind kind,
    string name,
    int slot)
{
    public GameEventScriptBytecodeDiagnosticKind Kind { get; } = kind;

    public string Name { get; } = name;

    public int Slot { get; } = slot;
}
