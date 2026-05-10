using System;
using System.Collections.Generic;
using System.Linq;
using StepH.GameEventScript.Api;

namespace StepH.GameEventScript.BytecodeVM;

internal enum GesBytecodeVmLinearProjectionFastKind
{
    None,
    Operand,
    PredicateTest,
    Binary,
    BinaryThenBinary,
    BinaryThenBinaryThenBinary
}

internal sealed class GesBytecodeVmLinearExecutable
{
    private GesBytecodeVmLinearExecutable(
        GameEventScriptBytecodeInstruction[] code,
        int maxFrameSlots,
        GesBytecodeVmLinearHandlerEntry[] handlers,
        GesBytecodeVmLinearCallableEntry[] callables,
        GesBytecodeVmLinearTypeFieldEntry[] typeFields,
        GesBytecodeVmLinearProjectionFastKind[] projectionFastKinds,
        IReadOnlyList<GesBytecodeVmLinearDiagnosticEntry>[] diagnosticsBefore,
        IReadOnlyList<GesBytecodeVmLinearDiagnosticEntry>[] diagnosticsAfter)
    {
        Code = code;
        MaxFrameSlots = maxFrameSlots;
        Handlers = handlers;
        Callables = callables;
        TypeFields = typeFields;
        ProjectionFastKinds = projectionFastKinds;
        DiagnosticsBefore = diagnosticsBefore;
        DiagnosticsAfter = diagnosticsAfter;
    }

    public IReadOnlyList<GameEventScriptBytecodeInstruction> Code { get; }

    public int MaxFrameSlots { get; }

    public IReadOnlyList<GesBytecodeVmLinearHandlerEntry> Handlers { get; }

    public IReadOnlyList<GesBytecodeVmLinearCallableEntry> Callables { get; }

    public IReadOnlyList<GesBytecodeVmLinearTypeFieldEntry> TypeFields { get; }

    internal IReadOnlyList<GesBytecodeVmLinearProjectionFastKind> ProjectionFastKinds { get; }

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
                ValidateFrameSlotCount(module, handler.LocalSlotCount, $"handler '{signatureId}' local slot count");
                return new GesBytecodeVmLinearHandlerEntry(
                    handler.Message,
                    signatureId,
                    handler.DispatchKind,
                    handler.DeclarationOrder,
                    handler.EntryAddress,
                    handler.LocalSlotCount,
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
                ValidateFrameSlotCount(module, callable.LocalSlotCount, $"callable '{signatureId}' local slot count");
                ValidateOptionalSlot(module, callable.ReturnSlot, $"callable '{signatureId}' return slot");
                return new GesBytecodeVmLinearCallableEntry(
                    callable.Name,
                    signatureId,
                    callable.Kind,
                    callable.EntryAddress,
                    callable.LocalSlotCount,
                    callable.ReturnSlot,
                    callable.Parameters.ToArray(),
                    callable.ParameterTypes.ToArray());
            })
            .ToArray();
        var typeFields = module.TypeDefinitions.Values
            .SelectMany(type => type.Fields.Select(field =>
            {
                ValidateOptionalAddress(module, code, field.MinimumEntryAddress, $"type '{type.Name}.{field.Name}' minimum entry");
                ValidateOptionalAddress(module, code, field.MaximumEntryAddress, $"type '{type.Name}.{field.Name}' maximum entry");
                ValidateOptionalAddress(module, code, field.ComputedEntryAddress, $"type '{type.Name}.{field.Name}' computed entry");
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

        var projectionFastKinds = BuildProjectionFastKinds(code);

        return new GesBytecodeVmLinearExecutable(
            code,
            module.MaxFrameSlots,
            handlers,
            callables,
            typeFields,
            projectionFastKinds,
            diagnosticsBefore,
            diagnosticsAfter);
    }

    private static GesBytecodeVmLinearProjectionFastKind[] BuildProjectionFastKinds(IReadOnlyList<GameEventScriptBytecodeInstruction> code)
    {
        var kinds = new GesBytecodeVmLinearProjectionFastKind[code.Count];
        for (var entryAddress = 0; entryAddress < code.Count; entryAddress++)
        {
            kinds[entryAddress] = IdentifyProjectionFastKind(code, entryAddress);
        }

        return kinds;
    }

    private static GesBytecodeVmLinearProjectionFastKind IdentifyProjectionFastKind(
        IReadOnlyList<GameEventScriptBytecodeInstruction> code,
        int entryAddress)
    {
        if (TryMatchLinearReturn(code, entryAddress, offset: 1, out var returnInstruction) &&
            returnInstruction.A == code[entryAddress].Dest &&
            IsLinearProjectionOperandInstruction(code[entryAddress]))
        {
            return GesBytecodeVmLinearProjectionFastKind.Operand;
        }

        if (TryMatchLinearReturn(code, entryAddress, offset: 2, out returnInstruction) &&
            code[entryAddress + 1].OpCode == GameEventScriptBytecodeOpCode.PredicateTest &&
            code[entryAddress + 1].A == code[entryAddress].Dest &&
            returnInstruction.A == code[entryAddress + 1].Dest &&
            IsLinearProjectionOperandInstruction(code[entryAddress]))
        {
            return GesBytecodeVmLinearProjectionFastKind.PredicateTest;
        }

        if (TryMatchLinearReturn(code, entryAddress, offset: 3, out returnInstruction) &&
            IsProjectionBinaryOp(code[entryAddress + 2].OpCode) &&
            returnInstruction.A == code[entryAddress + 2].Dest &&
            code[entryAddress + 2].A == code[entryAddress].Dest &&
            code[entryAddress + 2].B == code[entryAddress + 1].Dest &&
            IsLinearProjectionOperandInstruction(code[entryAddress]) &&
            IsLinearProjectionOperandInstruction(code[entryAddress + 1]))
        {
            return GesBytecodeVmLinearProjectionFastKind.Binary;
        }

        if (TryMatchLinearReturn(code, entryAddress, offset: 5, out returnInstruction) &&
            IsProjectionBinaryOp(code[entryAddress + 2].OpCode) &&
            IsProjectionBinaryOp(code[entryAddress + 4].OpCode) &&
            returnInstruction.A == code[entryAddress + 4].Dest &&
            code[entryAddress + 2].A == code[entryAddress].Dest &&
            code[entryAddress + 2].B == code[entryAddress + 1].Dest &&
            code[entryAddress + 4].A == code[entryAddress + 2].Dest &&
            code[entryAddress + 4].B == code[entryAddress + 3].Dest &&
            IsLinearProjectionOperandInstruction(code[entryAddress]) &&
            IsLinearProjectionOperandInstruction(code[entryAddress + 1]) &&
            IsLinearProjectionOperandInstruction(code[entryAddress + 3]))
        {
            return GesBytecodeVmLinearProjectionFastKind.BinaryThenBinary;
        }

        if (TryMatchLinearReturn(code, entryAddress, offset: 7, out returnInstruction) &&
            IsProjectionBinaryOp(code[entryAddress + 2].OpCode) &&
            IsProjectionBinaryOp(code[entryAddress + 4].OpCode) &&
            IsProjectionBinaryOp(code[entryAddress + 6].OpCode) &&
            returnInstruction.A == code[entryAddress + 6].Dest &&
            code[entryAddress + 2].A == code[entryAddress].Dest &&
            code[entryAddress + 2].B == code[entryAddress + 1].Dest &&
            code[entryAddress + 4].A == code[entryAddress + 2].Dest &&
            code[entryAddress + 4].B == code[entryAddress + 3].Dest &&
            code[entryAddress + 6].A == code[entryAddress + 4].Dest &&
            code[entryAddress + 6].B == code[entryAddress + 5].Dest &&
            IsLinearProjectionOperandInstruction(code[entryAddress]) &&
            IsLinearProjectionOperandInstruction(code[entryAddress + 1]) &&
            IsLinearProjectionOperandInstruction(code[entryAddress + 3]) &&
            IsLinearProjectionOperandInstruction(code[entryAddress + 5]))
        {
            return GesBytecodeVmLinearProjectionFastKind.BinaryThenBinaryThenBinary;
        }

        return GesBytecodeVmLinearProjectionFastKind.None;
    }

    private static bool TryMatchLinearReturn(
        IReadOnlyList<GameEventScriptBytecodeInstruction> code,
        int entryAddress,
        int offset,
        out GameEventScriptBytecodeInstruction returnInstruction)
    {
        var returnAddress = entryAddress + offset;
        if ((uint)returnAddress < (uint)code.Count &&
            code[returnAddress].OpCode == GameEventScriptBytecodeOpCode.Return)
        {
            returnInstruction = code[returnAddress];
            return true;
        }

        returnInstruction = default;
        return false;
    }

    private static bool IsLinearProjectionOperandInstruction(GameEventScriptBytecodeInstruction instruction)
        => instruction.OpCode == GameEventScriptBytecodeOpCode.MoveSlot ||
           IsInlineConstantInstruction(instruction.OpCode);

    private static bool IsProjectionBinaryOp(GameEventScriptBytecodeOpCode opCode)
        => opCode is
            GameEventScriptBytecodeOpCode.Default or
            GameEventScriptBytecodeOpCode.Add or
            GameEventScriptBytecodeOpCode.Subtract or
            GameEventScriptBytecodeOpCode.Multiply or
            GameEventScriptBytecodeOpCode.Divide or
            GameEventScriptBytecodeOpCode.IntegerDivide or
            GameEventScriptBytecodeOpCode.Modulo or
            GameEventScriptBytecodeOpCode.Remainder or
            GameEventScriptBytecodeOpCode.Power or
            GameEventScriptBytecodeOpCode.Equal or
            GameEventScriptBytecodeOpCode.NotEqual or
            GameEventScriptBytecodeOpCode.ApproxEqual or
            GameEventScriptBytecodeOpCode.Less or
            GameEventScriptBytecodeOpCode.Greater or
            GameEventScriptBytecodeOpCode.LessOrEqual or
            GameEventScriptBytecodeOpCode.GreaterOrEqual or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerEqual or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerNotEqual or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerLess or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerGreater or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerLessOrEqual or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerGreaterOrEqual or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerAdd or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerSubtract or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerMultiply or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerDivide or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerFloorDivide or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerModulo or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerRemainder;

    private static IReadOnlyList<GesBytecodeVmLinearDiagnosticEntry>[] BuildDiagnosticSites(
        GameEventScriptCompiled module,
        IReadOnlyList<GameEventScriptBytecodeInstruction> code,
        GameEventScriptBytecodeDiagnosticTiming timing)
    {
        var sites = new List<GesBytecodeVmLinearDiagnosticEntry>?[code.Count + 1];
        foreach (var layout in module.DiagnosticLayouts.Where(layout => layout.Timing == timing))
        {
            if (layout.Address < 0 || layout.Address > code.Count)
            {
                throw InvalidBytecode($"diagnostic layout '{layout.Name}' references address {layout.Address}, outside code range 0..{code.Count}.");
            }

            if (timing == GameEventScriptBytecodeDiagnosticTiming.AfterInstruction &&
                layout.Address >= code.Count)
            {
                throw InvalidBytecode($"diagnostic layout '{layout.Name}' cannot run after end address {layout.Address}.");
            }

            ValidateOptionalSlot(module, layout.Slot, $"diagnostic layout '{layout.Name}' slot");
            sites[layout.Address] ??= [];
            sites[layout.Address]!.Add(new GesBytecodeVmLinearDiagnosticEntry(layout.Kind, layout.Name, layout.Slot));
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
    }

    private static void ValidateInstruction(
        GameEventScriptCompiled module,
        IReadOnlyList<GameEventScriptBytecodeInstruction> code,
        int address,
        GameEventScriptBytecodeInstruction instruction)
    {
        var context = $"instruction @{address.ToString("0000", System.Globalization.CultureInfo.InvariantCulture)} {instruction.OpCode}";
        ValidateOptionalSlot(module, instruction.Dest, $"{context} destination slot");
        switch (instruction.OpCode)
        {
            case GameEventScriptBytecodeOpCode.Nop:
            case GameEventScriptBytecodeOpCode.EnterScope:
            case GameEventScriptBytecodeOpCode.ExitScope:
                break;

            case GameEventScriptBytecodeOpCode.LoadNothing:
            case GameEventScriptBytecodeOpCode.LoadTrue:
            case GameEventScriptBytecodeOpCode.LoadFalse:
                break;

            case GameEventScriptBytecodeOpCode.LoadInteger:
                ValidateNumericUnit(instruction.UnitAndFlags, allowPercentage: false, $"{context} numeric unit");
                break;

            case GameEventScriptBytecodeOpCode.LoadFloat:
                ValidateNumericUnit(instruction.UnitAndFlags, allowPercentage: true, $"{context} numeric unit");
                break;

            case GameEventScriptBytecodeOpCode.LoadText:
                ValidateIndex(module.StringPool.Count, instruction.C, $"{context} text");
                break;

            case GameEventScriptBytecodeOpCode.LoadTag:
                ValidateIndex(module.StringPool.Count, instruction.C, $"{context} tag");
                break;

            case GameEventScriptBytecodeOpCode.LoadHandler:
                ValidateMessageShape(module, instruction.A, $"{context} handler shape");
                break;

            case GameEventScriptBytecodeOpCode.MoveSlot:
                ValidateSlot(module, instruction.A, $"{context} source slot");
                break;

            case GameEventScriptBytecodeOpCode.BindParameter:
                ValidateNonNegative(instruction.A, $"{context} parameter index");
                break;

            case GameEventScriptBytecodeOpCode.Jump:
                ValidateAddress(module, code, instruction.A, $"{context} target");
                break;

            case GameEventScriptBytecodeOpCode.JumpIfTrue:
            case GameEventScriptBytecodeOpCode.JumpIfFalse:
            case GameEventScriptBytecodeOpCode.JumpIfNotTrue:
                ValidateSlot(module, instruction.C, $"{context} condition slot");
                ValidateAddress(module, code, instruction.A, $"{context} target");
                break;

            case GameEventScriptBytecodeOpCode.Return:
                ValidateOptionalSlot(module, instruction.A, $"{context} return slot");
                break;

            case GameEventScriptBytecodeOpCode.EmitMessage:
            case GameEventScriptBytecodeOpCode.PublishMessage:
                ValidateMessageShape(module, instruction.A, $"{context} message shape");
                ValidateMessageArgumentSlotList(module, instruction.A, instruction.B, $"{context} argument slots");
                ValidateOptionalSlotList(module, instruction.C, $"{context} tag slot list");
                break;

            case GameEventScriptBytecodeOpCode.EmitMessageValue:
            case GameEventScriptBytecodeOpCode.PublishMessageValue:
                ValidateSlot(module, instruction.A, $"{context} message slot");
                ValidateOptionalSlotList(module, instruction.C, $"{context} tag slot list");
                break;

            case GameEventScriptBytecodeOpCode.ForRange:
            case GameEventScriptBytecodeOpCode.ForCollection:
                ValidateIndex(module.LoopLayouts.Count, instruction.C, $"{context} loop layout");
                ValidateAddress(module, code, instruction.A, $"{context} body target");
                ValidateAddress(module, code, instruction.B, $"{context} end target");
                break;

            case GameEventScriptBytecodeOpCode.RandomPush:
                ValidateSlot(module, instruction.A, $"{context} seed slot");
                break;

            case GameEventScriptBytecodeOpCode.RandomPushConstant:
            case GameEventScriptBytecodeOpCode.RandomPop:
                break;

            case GameEventScriptBytecodeOpCode.MemberAccess:
                ValidateSlot(module, instruction.A, $"{context} source slot");
                ValidateIndex(module.StringPool.Count, instruction.C, $"{context} member name");
                break;

            case GameEventScriptBytecodeOpCode.Pipeline:
                ValidateIndex(module.PipelinePool.Count, instruction.C, $"{context} pipeline");
                break;

            case GameEventScriptBytecodeOpCode.GeneratedCollection:
                ValidateIndex(module.GeneratedCollectionLayouts.Count, instruction.C, $"{context} generated collection layout");
                break;

            case GameEventScriptBytecodeOpCode.GuardedChoice:
                ValidateIndex(module.GuardedChoiceLayouts.Count, instruction.C, $"{context} guarded choice layout");
                break;

            case GameEventScriptBytecodeOpCode.Dice:
                ValidateNonNegative(instruction.A, $"{context} dice count");
                ValidateNonNegative(instruction.B, $"{context} dice sides");
                break;

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
                ValidateSlot(module, instruction.A, $"{context} operand slot");
                break;

            case GameEventScriptBytecodeOpCode.Range:
                ValidateSlot(module, instruction.A, $"{context} from slot");
                ValidateSlot(module, instruction.B, $"{context} to slot");
                break;

            case GameEventScriptBytecodeOpCode.RangeWithStep:
                ValidateSlot(module, instruction.A, $"{context} from slot");
                ValidateSlot(module, instruction.B, $"{context} to slot");
                ValidateSlot(module, instruction.C, $"{context} step slot");
                break;

            default:
                if (IsCastInstruction(instruction.OpCode))
                {
                    ValidateSlot(module, instruction.A, $"{context} source slot");
                    if (instruction.OpCode == GameEventScriptBytecodeOpCode.CastCustom)
                    {
                        ValidateIndex(module.StringPool.Count, instruction.C, $"{context} custom type name");
                    }

                    break;
                }

                if (IsTypeCheckInstruction(instruction.OpCode))
                {
                    ValidateSlot(module, instruction.A, $"{context} source slot");
                    if (instruction.OpCode == GameEventScriptBytecodeOpCode.TypeCheckCustom)
                    {
                        ValidateIndex(module.StringPool.Count, instruction.C, $"{context} custom type name");
                    }

                    break;
                }

                if (IsOperationLayoutInstruction(instruction.OpCode))
                {
                    ValidateIndex(module.OperationLayouts.Count, instruction.C, $"{context} operation layout");
                    break;
                }

                if (IsBinarySlotInstruction(instruction.OpCode))
                {
                    ValidateSlot(module, instruction.A, $"{context} left slot");
                    ValidateSlot(module, instruction.B, $"{context} right slot");
                    break;
                }

                if (instruction.A >= 0)
                {
                    ValidateOptionalSlot(module, instruction.A, $"{context} a slot");
                }

                if (instruction.B >= 0)
                {
                    ValidateOptionalSlot(module, instruction.B, $"{context} b slot");
                }

                if (instruction.C >= 0)
                {
                    ValidateOptionalSlot(module, instruction.C, $"{context} c slot");
                }
                break;
        }
    }

    private static void ValidateSideTables(GameEventScriptCompiled module)
    {
        ValidateOperationLayouts(module);
        ValidateDiagnosticLayouts(module);
        ValidateIterationSourceLayouts(module);
        ValidateLoopLayouts(module);
        ValidatePipelinePatternPool(module);
        ValidatePipelineObjectPatternPool(module);
        ValidatePipelineSelectorPool(module);
        ValidatePipelinePool(module);
        ValidateGeneratedCollectionLayouts(module);
        ValidateGuardedChoiceLayouts(module);
    }

    private static void ValidateDiagnosticLayouts(GameEventScriptCompiled module)
    {
        for (var index = 0; index < module.DiagnosticLayouts.Count; index++)
        {
            var layout = module.DiagnosticLayouts[index];
            var context = $"diagnostic layout #{index}";
            if (layout.Address < 0 || layout.Address > module.Code.Count)
            {
                throw InvalidBytecode($"{context} references address {layout.Address}, outside code range 0..{module.Code.Count}.");
            }

            if (layout.Timing == GameEventScriptBytecodeDiagnosticTiming.AfterInstruction &&
                layout.Address >= module.Code.Count)
            {
                throw InvalidBytecode($"{context} cannot run after end address {layout.Address}.");
            }

            ValidateOptionalSlot(module, layout.Slot, $"{context} slot");
        }
    }

    private static void ValidateOperationLayouts(GameEventScriptCompiled module)
    {
        for (var index = 0; index < module.OperationLayouts.Count; index++)
        {
            var layout = module.OperationLayouts[index];
            var context = $"operation layout #{index}";
            if (!IsOperationLayoutInstruction(layout.OpCode))
            {
                throw InvalidBytecode($"{context} references unsupported opcode '{layout.OpCode}'.");
            }

            ValidateOptionalIndex(module.ExternalReferences.Count, layout.ExternalReferenceIndex, $"{context} external reference");
            ValidateOptionalStringList(module, layout.NameListIndex, $"{context} name list");
            ValidateOptionalAddress(module, module.Code, layout.ExpressionEntryAddress, $"{context} expression entry");
            ValidateOptionalAddress(module, module.Code, layout.SecondaryExpressionEntryAddress, $"{context} secondary expression entry");
            ValidateNonNegative(layout.Count, $"{context} count");
            ValidateSlots(module, layout.ArgumentSlots, $"{context} argument slot");
            ValidateSlots(module, layout.ParameterSlots, $"{context} parameter slot");
        }
    }

    private static void ValidateIterationSourceLayouts(GameEventScriptCompiled module)
    {
        for (var index = 0; index < module.IterationSourceLayouts.Count; index++)
        {
            var layout = module.IterationSourceLayouts[index];
            var context = $"iteration source layout #{index}";
            ValidateOptionalSlot(module, layout.CollectionSlot, $"{context} collection slot");
            ValidateOptionalSlot(module, layout.RangeFromSlot, $"{context} range from slot");
            ValidateOptionalSlot(module, layout.RangeToSlot, $"{context} range to slot");
            ValidateOptionalSlot(module, layout.RangeStepSlot, $"{context} range step slot");
        }
    }

    private static void ValidateLoopLayouts(GameEventScriptCompiled module)
    {
        for (var index = 0; index < module.LoopLayouts.Count; index++)
        {
            var layout = module.LoopLayouts[index];
            var context = $"loop layout #{index}";
            ValidateOptionalSlot(module, layout.IdentifierSlot, $"{context} identifier slot");
            ValidateIndex(module.IterationSourceLayouts.Count, layout.IterationSourceLayoutIndex, $"{context} iteration source layout");
        }
    }

    private static void ValidatePipelineSelectorPool(GameEventScriptCompiled module)
    {
        for (var index = 0; index < module.PipelineSelectorPool.Count; index++)
        {
            var layout = module.PipelineSelectorPool[index];
            var context = $"pipeline selector #{index}";
            ValidateOptionalSlot(module, layout.IdentifierSlot, $"{context} identifier slot");
            ValidateOptionalSlot(module, layout.SecondaryIdentifierSlot, $"{context} secondary identifier slot");
            ValidateOptionalAddress(module, module.Code, layout.ExpressionEntryAddress, $"{context} expression entry");
            ValidateOptionalAddress(module, module.Code, layout.SecondaryExpressionEntryAddress, $"{context} secondary expression entry");
            ValidateOptionalIndex(module.PipelinePatternPool.Count, layout.PipelinePatternIndex, $"{context} pattern");
            ValidateOptionalIndex(module.PipelineObjectPatternPool.Count, layout.ObjectPatternIndex, $"{context} object pattern");
            ValidateNonNegative(layout.Count, $"{context} count");
        }
    }

    private static void ValidatePipelinePatternPool(GameEventScriptCompiled module)
    {
        for (var index = 0; index < module.PipelinePatternPool.Count; index++)
        {
            var layout = module.PipelinePatternPool[index];
            var context = $"pipeline pattern #{index}";
            ValidateNonNegative(layout.Count, $"{context} count");
            ValidateOptionalAddress(module, module.Code, layout.FaceEntryAddress, $"{context} face entry");
        }
    }

    private static void ValidatePipelineObjectPatternPool(GameEventScriptCompiled module)
    {
        for (var index = 0; index < module.PipelineObjectPatternPool.Count; index++)
        {
            var layout = module.PipelineObjectPatternPool[index];
            for (var entryIndex = 0; entryIndex < layout.Entries.Count; entryIndex++)
            {
                var entry = layout.Entries[entryIndex];
                var context = $"pipeline object pattern #{index} entry #{entryIndex}";
                if (string.IsNullOrEmpty(entry.Key))
                {
                    throw InvalidBytecode($"{context} has no key.");
                }

                switch (entry.ValueKind)
                {
                    case GameEventScriptBytecodePipelineObjectPatternValueKind.Expression:
                        ValidateOptionalAddress(module, module.Code, entry.ExpressionEntryAddress, $"{context} expression entry");
                        break;

                    case GameEventScriptBytecodePipelineObjectPatternValueKind.Nested:
                        ValidateIndex(module.PipelineObjectPatternPool.Count, entry.NestedPatternIndex, $"{context} nested object pattern");
                        break;
                }
            }
        }
    }

    private static void ValidatePipelinePool(GameEventScriptCompiled module)
    {
        for (var index = 0; index < module.PipelinePool.Count; index++)
        {
            var layout = module.PipelinePool[index];
            var context = $"pipeline #{index}";
            ValidateSlot(module, layout.SourceSlot, $"{context} source slot");
            var prefixSelectorIndexes = layout.PrefixSelectorIndexes;
            for (var prefixIndex = 0; prefixIndex < prefixSelectorIndexes.Count; prefixIndex++)
            {
                var selectorIndex = prefixSelectorIndexes[prefixIndex];
                ValidateIndex(module.PipelineSelectorPool.Count, selectorIndex, $"{context} prefix selector");
            }

            ValidateIndex(module.PipelineSelectorPool.Count, layout.TerminalSelectorIndex, $"{context} terminal selector");
        }
    }

    private static void ValidateGeneratedCollectionLayouts(GameEventScriptCompiled module)
    {
        for (var index = 0; index < module.GeneratedCollectionLayouts.Count; index++)
        {
            var layout = module.GeneratedCollectionLayouts[index];
            var context = $"generated collection layout #{index}";
            if (string.IsNullOrEmpty(layout.CollectionType))
            {
                throw InvalidBytecode($"{context} has no collection type.");
            }

            ValidateOptionalSlot(module, layout.IdentifierSlot, $"{context} identifier slot");
            ValidateIndex(module.IterationSourceLayouts.Count, layout.IterationSourceLayoutIndex, $"{context} iteration source layout");
            ValidateOptionalAddress(module, module.Code, layout.PredicateEntryAddress, $"{context} predicate entry");
            ValidateOptionalAddress(module, module.Code, layout.ProjectionEntryAddress, $"{context} projection entry");
        }
    }

    private static void ValidateGuardedChoiceLayouts(GameEventScriptCompiled module)
    {
        for (var index = 0; index < module.GuardedChoiceLayouts.Count; index++)
        {
            var layout = module.GuardedChoiceLayouts[index];
            foreach (var entryAddress in layout.ValueEntryAddresses)
            {
                ValidateOptionalAddress(module, module.Code, entryAddress, $"guarded choice layout #{index} value entry");
            }

            foreach (var entryAddress in layout.ConditionEntryAddresses)
            {
                ValidateOptionalAddress(module, module.Code, entryAddress, $"guarded choice layout #{index} condition entry");
            }

            ValidateOptionalAddress(module, module.Code, layout.OtherwiseEntryAddress, $"guarded choice layout #{index} otherwise entry");
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
            ValidateOptionalIndex(module.UShortListPool.Count, slotListIndex, context);
            if (slotListIndex >= 0 && module.UShortListPool[slotListIndex].Count != 0)
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

    private static void ValidateOptionalStringList(GameEventScriptCompiled module, int index, string context)
    {
        if (index < 0)
        {
            return;
        }

        ValidateIndex(module.UShortListPool.Count, index, context);
        var values = module.UShortListPool[index];
        for (var itemIndex = 0; itemIndex < values.Count; itemIndex++)
        {
            ValidateIndex(module.StringPool.Count, values[itemIndex], $"{context} string #{itemIndex}");
        }
    }

    private static void ValidateOptionalSlotList(GameEventScriptCompiled module, int index, string context)
    {
        if (index < 0)
        {
            return;
        }

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

    private static void ValidateSlots(GameEventScriptCompiled module, IReadOnlyList<int> slots, string context)
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

    private static void ValidateOptionalAddress(
        GameEventScriptCompiled module,
        IReadOnlyList<GameEventScriptBytecodeInstruction> code,
        int address,
        string context)
    {
        if (address >= 0)
        {
            ValidateAddress(module, code, address, context);
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

    private static void ValidateNumericUnit(byte value, bool allowPercentage, string context)
    {
        var unit = (GameEventScriptBytecodeInstructionUnit)value;
        if (unit is GameEventScriptBytecodeInstructionUnit.None or
            GameEventScriptBytecodeInstructionUnit.Degree or
            GameEventScriptBytecodeInstructionUnit.Meter or
            GameEventScriptBytecodeInstructionUnit.Second)
        {
            return;
        }

        if (allowPercentage && unit == GameEventScriptBytecodeInstructionUnit.Percentage)
        {
            return;
        }

        if (!Enum.IsDefined(typeof(GameEventScriptBytecodeInstructionUnit), value))
        {
            throw InvalidBytecode($"{context} references unknown unit {value}.");
        }

        throw InvalidBytecode($"{context} cannot use unit '{unit}'.");
    }

    private static InvalidOperationException InvalidBytecode(string message)
        => new($"Invalid GameEventScript linear bytecode: {message}");

    private static bool IsOperationLayoutInstruction(GameEventScriptBytecodeOpCode opCode)
        => opCode is
            GameEventScriptBytecodeOpCode.Variadic or
            GameEventScriptBytecodeOpCode.TypeConstructor or
            GameEventScriptBytecodeOpCode.PredicateTest or
            GameEventScriptBytecodeOpCode.BuildList or
            GameEventScriptBytecodeOpCode.BuildSequence or
            GameEventScriptBytecodeOpCode.BuildSet or
            GameEventScriptBytecodeOpCode.BuildDictionary or
            GameEventScriptBytecodeOpCode.BuildMessage or
            GameEventScriptBytecodeOpCode.BindHandler or
            GameEventScriptBytecodeOpCode.CallExtension or
            GameEventScriptBytecodeOpCode.Call;

    private static bool IsCastInstruction(GameEventScriptBytecodeOpCode opCode)
        => opCode is GameEventScriptBytecodeOpCode.CastNothing or
            GameEventScriptBytecodeOpCode.CastBoolean or
            GameEventScriptBytecodeOpCode.CastInteger or
            GameEventScriptBytecodeOpCode.CastFloat or
            GameEventScriptBytecodeOpCode.CastNumber or
            GameEventScriptBytecodeOpCode.CastPercentage or
            GameEventScriptBytecodeOpCode.CastDegree or
            GameEventScriptBytecodeOpCode.CastMeter or
            GameEventScriptBytecodeOpCode.CastSecond or
            GameEventScriptBytecodeOpCode.CastVector or
            GameEventScriptBytecodeOpCode.CastPoint or
            GameEventScriptBytecodeOpCode.CastUuid or
            GameEventScriptBytecodeOpCode.CastSequence or
            GameEventScriptBytecodeOpCode.CastSeries or
            GameEventScriptBytecodeOpCode.CastEnvelope or
            GameEventScriptBytecodeOpCode.CastRef or
            GameEventScriptBytecodeOpCode.CastTag or
            GameEventScriptBytecodeOpCode.CastText or
            GameEventScriptBytecodeOpCode.CastList or
            GameEventScriptBytecodeOpCode.CastRange or
            GameEventScriptBytecodeOpCode.CastMessage or
            GameEventScriptBytecodeOpCode.CastHandler or
            GameEventScriptBytecodeOpCode.CastDictionary or
            GameEventScriptBytecodeOpCode.CastSet or
            GameEventScriptBytecodeOpCode.CastDice or
            GameEventScriptBytecodeOpCode.CastOptional or
            GameEventScriptBytecodeOpCode.CastCustom;

    private static bool IsInlineConstantInstruction(GameEventScriptBytecodeOpCode opCode)
        => opCode is GameEventScriptBytecodeOpCode.LoadNothing or
            GameEventScriptBytecodeOpCode.LoadTrue or
            GameEventScriptBytecodeOpCode.LoadFalse or
            GameEventScriptBytecodeOpCode.LoadInteger or
            GameEventScriptBytecodeOpCode.LoadFloat or
            GameEventScriptBytecodeOpCode.LoadText or
            GameEventScriptBytecodeOpCode.LoadTag or
            GameEventScriptBytecodeOpCode.LoadHandler;

    private static bool IsTypeCheckInstruction(GameEventScriptBytecodeOpCode opCode)
        => opCode is GameEventScriptBytecodeOpCode.TypeCheckNothing or
            GameEventScriptBytecodeOpCode.TypeCheckTag or
            GameEventScriptBytecodeOpCode.TypeCheckText or
            GameEventScriptBytecodeOpCode.TypeCheckPercentage or
            GameEventScriptBytecodeOpCode.TypeCheckDegree or
            GameEventScriptBytecodeOpCode.TypeCheckMeter or
            GameEventScriptBytecodeOpCode.TypeCheckSecond or
            GameEventScriptBytecodeOpCode.TypeCheckVector or
            GameEventScriptBytecodeOpCode.TypeCheckPoint or
            GameEventScriptBytecodeOpCode.TypeCheckFloat or
            GameEventScriptBytecodeOpCode.TypeCheckInteger or
            GameEventScriptBytecodeOpCode.TypeCheckBoolean or
            GameEventScriptBytecodeOpCode.TypeCheckUuid or
            GameEventScriptBytecodeOpCode.TypeCheckOptional or
            GameEventScriptBytecodeOpCode.TypeCheckSequence or
            GameEventScriptBytecodeOpCode.TypeCheckSeries or
            GameEventScriptBytecodeOpCode.TypeCheckEnvelope or
            GameEventScriptBytecodeOpCode.TypeCheckList or
            GameEventScriptBytecodeOpCode.TypeCheckRange or
            GameEventScriptBytecodeOpCode.TypeCheckMessage or
            GameEventScriptBytecodeOpCode.TypeCheckHandler or
            GameEventScriptBytecodeOpCode.TypeCheckRef or
            GameEventScriptBytecodeOpCode.TypeCheckDictionary or
            GameEventScriptBytecodeOpCode.TypeCheckSet or
            GameEventScriptBytecodeOpCode.TypeCheckDice or
            GameEventScriptBytecodeOpCode.TypeCheckCustom;

    private static bool IsBinarySlotInstruction(GameEventScriptBytecodeOpCode opCode)
        => opCode is GameEventScriptBytecodeOpCode.Or or
            GameEventScriptBytecodeOpCode.Xor or
            GameEventScriptBytecodeOpCode.And or
            GameEventScriptBytecodeOpCode.Power or
            GameEventScriptBytecodeOpCode.ShortCircuitImplies or
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
            GameEventScriptBytecodeOpCode.PrimitiveIntegerEqual or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerNotEqual or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerLess or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerGreater or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerLessOrEqual or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerGreaterOrEqual or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerAdd or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerSubtract or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerMultiply or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerDivide or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerFloorDivide or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerModulo or
            GameEventScriptBytecodeOpCode.PrimitiveIntegerRemainder or
            GameEventScriptBytecodeOpCode.Default or
            GameEventScriptBytecodeOpCode.Contains or
            GameEventScriptBytecodeOpCode.ContainsValue or
            GameEventScriptBytecodeOpCode.StartsWith or
            GameEventScriptBytecodeOpCode.EndsWith or
            GameEventScriptBytecodeOpCode.Intersect or
            GameEventScriptBytecodeOpCode.Combine or
            GameEventScriptBytecodeOpCode.Except or
            GameEventScriptBytecodeOpCode.Zip;
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
