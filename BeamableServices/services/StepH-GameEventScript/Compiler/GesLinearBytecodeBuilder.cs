using System;
using System.Collections.Generic;
using System.Linq;
using StepH.GameEventScript.Api;

namespace StepH.GameEventScript.Compiler;

internal sealed class GesLinearBytecodeBuilder
{
    private readonly Func<string, int> _resolveTypeMetadataIndex;
    private readonly Func<IReadOnlyList<string>, int> _resolveNamedArgumentLayoutIndex;
    private readonly List<GameEventScriptBytecodeInstruction> _code = [];
    private readonly List<GameEventScriptBytecodeOperationLayout> _operationLayouts = [];
    private readonly List<GameEventScriptBytecodePublishLayoutEntry> _publishLayouts = [];
    private readonly List<GameEventScriptBytecodeIterationSourceLayout> _iterationSourceLayouts = [];
    private readonly List<GameEventScriptBytecodeLoopLayout> _loopLayouts = [];
    private readonly List<GameEventScriptBytecodeSeededRandomBlockLayout> _seededRandomBlockLayouts = [];
    private readonly List<GameEventScriptBytecodeSelectorLayout> _selectorLayouts = [];
    private readonly List<GameEventScriptBytecodePipelineLayout> _pipelineLayouts = [];
    private readonly List<GameEventScriptBytecodeGeneratedCollectionLayout> _generatedCollectionLayouts = [];
    private readonly List<GameEventScriptBytecodeGuardedChoiceLayout> _guardedChoiceLayouts = [];
    private readonly List<Action> _deferredHelperEmitters = [];
    private int _currentFrameSlotCount;
    private int _maxFrameSlots = 1;

    public GesLinearBytecodeBuilder(
        Func<string, int>? resolveTypeMetadataIndex = null,
        Func<IReadOnlyList<string>, int>? resolveNamedArgumentLayoutIndex = null)
    {
        _resolveTypeMetadataIndex = resolveTypeMetadataIndex ?? (_ => -1);
        _resolveNamedArgumentLayoutIndex = resolveNamedArgumentLayoutIndex ?? (_ => -1);
    }

    public IReadOnlyList<GameEventScriptBytecodeInstruction> Code => _code;

    public int MaxFrameSlots => _maxFrameSlots;

    public IReadOnlyList<GameEventScriptBytecodeOperationLayout> OperationLayouts => _operationLayouts;

    public IReadOnlyList<GameEventScriptBytecodePublishLayoutEntry> PublishLayouts => _publishLayouts;

    public IReadOnlyList<GameEventScriptBytecodeIterationSourceLayout> IterationSourceLayouts => _iterationSourceLayouts;

    public IReadOnlyList<GameEventScriptBytecodeLoopLayout> LoopLayouts => _loopLayouts;

    public IReadOnlyList<GameEventScriptBytecodeSeededRandomBlockLayout> SeededRandomBlockLayouts => _seededRandomBlockLayouts;

    public IReadOnlyList<GameEventScriptBytecodeSelectorLayout> SelectorLayouts => _selectorLayouts;

    public IReadOnlyList<GameEventScriptBytecodePipelineLayout> PipelineLayouts => _pipelineLayouts;

    public IReadOnlyList<GameEventScriptBytecodeGeneratedCollectionLayout> GeneratedCollectionLayouts => _generatedCollectionLayouts;

    public IReadOnlyList<GameEventScriptBytecodeGuardedChoiceLayout> GuardedChoiceLayouts => _guardedChoiceLayouts;

    public void AddHandlers(IEnumerable<GameEventScriptBytecodeHandler> handlers)
    {
        foreach (var handler in handlers)
        {
            handler.EntryAddress = _code.Count;
            _currentFrameSlotCount = handler.LocalSlotCount;
            _maxFrameSlots = Math.Max(_maxFrameSlots, handler.LocalSlotCount);
            EmitParameterBindings(handler.Parameters, handler.ParameterTypes, handler.ExecutionPlan);
            EmitStatementProgram(handler.ExecutionPlan.StatementProgram, handler.ExecutionPlan);
            Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.Return));
            FlushDeferredHelpers();
            _maxFrameSlots = Math.Max(_maxFrameSlots, _currentFrameSlotCount);
        }
    }

    public void AddCallables(IEnumerable<GameEventScriptBytecodeCallable> callables)
    {
        foreach (var callable in callables)
        {
            callable.EntryAddress = _code.Count;
            callable.LocalSlotCount = Math.Max(callable.Parameters.Count + callable.ExpressionProgram.MaxStackDepth + 1, callable.Parameters.Count + 1);
            _currentFrameSlotCount = callable.LocalSlotCount;
            _maxFrameSlots = Math.Max(_maxFrameSlots, callable.LocalSlotCount);
            for (var index = 0; index < callable.Parameters.Count; index++)
            {
                Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.BindParameter, Dest: index, A: index));
                if (index < callable.ParameterTypes.Count && !string.IsNullOrEmpty(callable.ParameterTypes[index]))
                {
                    Emit(new GameEventScriptBytecodeInstruction(
                        GameEventScriptBytecodeOpCode.CoerceSlot,
                        Dest: index,
                        A: index,
                        Data: ResolveTypeMetadataIndex(callable.ParameterTypes[index])));
                }
            }

            var state = new ExpressionState(callable.Parameters.Count);
            var result = EmitExpression(callable.ExpressionProgram, state);
            callable.ReturnSlot = result;
            Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.Return, A: result));
            FlushDeferredHelpers();
            callable.LocalSlotCount = Math.Max(callable.LocalSlotCount, _currentFrameSlotCount);
            _maxFrameSlots = Math.Max(_maxFrameSlots, callable.LocalSlotCount);
        }
    }

    public void AddTypeDefinitions(IEnumerable<GameEventScriptBytecodeTypeDefinition> types)
    {
        foreach (var field in types.SelectMany(type => type.Fields))
        {
            if (field.MinimumProgram is not null)
            {
                field.MinimumEntryAddress = EmitExpressionEntry(field.MinimumProgram);
            }

            if (field.MaximumProgram is not null)
            {
                field.MaximumEntryAddress = EmitExpressionEntry(field.MaximumProgram);
            }

            if (field.ComputedProgram is not null)
            {
                field.ComputedEntryAddress = EmitExpressionEntry(field.ComputedProgram);
            }
        }

        FlushDeferredHelpers();
    }

    private int EmitExpressionEntry(GameEventScriptBytecodeExpressionProgram program)
        => EmitExpressionEntry(program, new ExpressionState(GetExpressionSlotUpperBound(program)));

    private int EmitExpressionEntry(GameEventScriptBytecodeExpressionProgram program, ExpressionState state)
    {
        var entry = _code.Count;
        program.SetLinearEntryAddress(entry, state.BaseSlot);
        var result = EmitExpression(program, state);
        _maxFrameSlots = Math.Max(_maxFrameSlots, state.NextSlot);
        _currentFrameSlotCount = Math.Max(_currentFrameSlotCount, state.NextSlot);
        Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.Return, A: result));
        return entry;
    }

    private void EmitParameterBindings(
        IReadOnlyList<string> parameters,
        IReadOnlyList<string?> parameterTypes,
        GameEventScriptBytecodeExecutionPlan plan)
    {
        for (var index = 0; index < parameters.Count; index++)
        {
            if (!plan.TryGetSlot(parameters[index], out var slot))
            {
                continue;
            }

            Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.BindParameter, Dest: slot, A: index));
            if (index < parameterTypes.Count && !string.IsNullOrEmpty(parameterTypes[index]))
            {
                Emit(new GameEventScriptBytecodeInstruction(
                    GameEventScriptBytecodeOpCode.CoerceSlot,
                    Dest: slot,
                    A: slot,
                    Data: ResolveTypeMetadataIndex(parameterTypes[index])));
            }
        }
    }

    private void EmitStatementProgram(GameEventScriptBytecodeStatementProgram program, GameEventScriptBytecodeExecutionPlan plan)
    {
        if (program.CreatesScope)
        {
            Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.EnterScope));
        }

        foreach (var statement in program.Statements)
        {
            EmitStatement(statement, plan);
        }

        if (program.CreatesScope)
        {
            Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.ExitScope));
        }
    }

    private void EmitStatement(GameEventScriptBytecodeStatement statement, GameEventScriptBytecodeExecutionPlan plan)
    {
        switch (statement.Kind)
        {
            case GameEventScriptBytecodeStatementKind.Let:
                if (statement.ExpressionProgram is not null &&
                    !string.IsNullOrEmpty(statement.Name) &&
                    plan.TryGetSlot(statement.Name, out var letSlot))
                {
                    var result = EmitExpression(statement.ExpressionProgram, new ExpressionState(plan.SlotCount));
                    DeferExpressionEntry(statement.ExpressionProgram, plan.SlotCount);
                    Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.CopySlot, Dest: letSlot, A: result));
                    if (!string.IsNullOrEmpty(statement.DeclaredType))
                    {
                        Emit(new GameEventScriptBytecodeInstruction(
                            GameEventScriptBytecodeOpCode.CoerceSlot,
                            Dest: letSlot,
                            A: letSlot,
                            Data: ResolveTypeMetadataIndex(statement.DeclaredType)));
                    }
                }
                break;

            case GameEventScriptBytecodeStatementKind.Expression:
                if (statement.ExpressionProgram is not null)
                {
                    _ = EmitExpression(statement.ExpressionProgram, new ExpressionState(plan.SlotCount));
                    DeferExpressionEntry(statement.ExpressionProgram, plan.SlotCount);
                }
                break;

            case GameEventScriptBytecodeStatementKind.Publish:
                EmitPublish(statement, plan);
                break;

            case GameEventScriptBytecodeStatementKind.If:
                EmitIf(statement, plan);
                break;

            case GameEventScriptBytecodeStatementKind.ForRange:
            case GameEventScriptBytecodeStatementKind.ForCollection:
                EmitLoop(statement, plan);
                break;

            case GameEventScriptBytecodeStatementKind.SeededRandom:
                EmitSeededRandom(statement, plan);
                break;
        }
    }

    private void EmitPublish(GameEventScriptBytecodeStatement statement, GameEventScriptBytecodeExecutionPlan plan)
    {
        var state = new ExpressionState(plan.SlotCount);
        var tagSlots = new List<int>(statement.TagPrograms.Length);
        foreach (var tagProgram in statement.TagPrograms)
        {
            tagSlots.Add(EmitExpression(tagProgram, state));
            DeferExpressionEntry(tagProgram, plan.SlotCount);
        }

        if (statement.PublishLayout is not null)
        {
            var argumentSlots = new List<int>(statement.PublishLayout.ArgumentPrograms.Length);
            foreach (var argumentProgram in statement.PublishLayout.ArgumentPrograms)
            {
                argumentSlots.Add(EmitExpression(argumentProgram, state));
                DeferExpressionEntry(argumentProgram, plan.SlotCount);
            }

            var publishLayoutIndex = AddPublishLayout(new GameEventScriptBytecodePublishLayoutEntry(
                statement.PublishKind,
                statement.PublishLayout.MessageName,
                statement.PublishLayout.SignatureId,
                statement.PublishLayout.ArgumentNames,
                argumentSlots,
                tagSlots: tagSlots));
            Emit(new GameEventScriptBytecodeInstruction(
                GameEventScriptBytecodeOpCode.PublishValue,
                A: (int)statement.PublishKind,
                Data: publishLayoutIndex));
            return;
        }

        if (statement.ExpressionProgram is not null)
        {
            var messageSlot = EmitExpression(statement.ExpressionProgram, state);
            DeferExpressionEntry(statement.ExpressionProgram, plan.SlotCount);
            var publishLayoutIndex = AddPublishLayout(new GameEventScriptBytecodePublishLayoutEntry(
                statement.PublishKind,
                messageSlot: messageSlot,
                tagSlots: tagSlots));
            Emit(new GameEventScriptBytecodeInstruction(
                GameEventScriptBytecodeOpCode.PublishMessageValue,
                A: messageSlot,
                B: (int)statement.PublishKind,
                Data: publishLayoutIndex));
        }
    }

    private void EmitIf(GameEventScriptBytecodeStatement statement, GameEventScriptBytecodeExecutionPlan plan)
    {
        if (statement.ExpressionProgram is null || statement.ThenProgram is null)
        {
            return;
        }

        var condition = EmitExpression(statement.ExpressionProgram, new ExpressionState(plan.SlotCount));
        DeferExpressionEntry(statement.ExpressionProgram, plan.SlotCount);
        var jumpToElse = Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.JumpIfNotTrue, A: condition));
        EmitStatementProgram(statement.ThenProgram, plan);
        var jumpToEnd = Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.Jump));
        PatchTarget(jumpToElse, _code.Count);
        if (statement.ElseProgram is not null)
        {
            EmitStatementProgram(statement.ElseProgram, plan);
        }

        PatchTarget(jumpToEnd, _code.Count);
    }

    private void EmitLoop(GameEventScriptBytecodeStatement statement, GameEventScriptBytecodeExecutionPlan plan)
    {
        if (statement.IterationSource is null || statement.BodyProgram is null)
        {
            return;
        }

        var state = new ExpressionState(plan.SlotCount);
        var sourceLayoutIndex = AddIterationSourceLayout(statement.IterationSource, state);
        var identifierSlot = !string.IsNullOrEmpty(statement.Name) && plan.TryGetSlot(statement.Name, out var slot)
            ? slot
            : -1;
        var loopLayoutIndex = AddLoopLayout(new GameEventScriptBytecodeLoopLayout(identifierSlot, sourceLayoutIndex));
        var opCode = statement.Kind == GameEventScriptBytecodeStatementKind.ForRange
            ? GameEventScriptBytecodeOpCode.ForRange
            : GameEventScriptBytecodeOpCode.ForCollection;
        var loopInstruction = Emit(new GameEventScriptBytecodeInstruction(opCode, Data: loopLayoutIndex));
        var bodyAddress = _code.Count;
        EmitStatementProgram(statement.BodyProgram, plan);
        PatchTargets(loopInstruction, bodyAddress, _code.Count);
    }

    private void EmitSeededRandom(GameEventScriptBytecodeStatement statement, GameEventScriptBytecodeExecutionPlan plan)
    {
        var state = new ExpressionState(plan.SlotCount);
        var seedSlot = -1;
        if (statement.ExpressionProgram is not null)
        {
            seedSlot = EmitExpression(statement.ExpressionProgram, state);
            DeferExpressionEntry(statement.ExpressionProgram, plan.SlotCount);
        }

        var layoutIndex = AddSeededRandomBlockLayout(new GameEventScriptBytecodeSeededRandomBlockLayout(seedSlot));
        var blockInstruction = Emit(new GameEventScriptBytecodeInstruction(
            GameEventScriptBytecodeOpCode.SeededRandomBlock,
            Data: layoutIndex));
        var bodyAddress = _code.Count;
        if (statement.BodyProgram is not null)
        {
            EmitStatementProgram(statement.BodyProgram, plan);
        }

        PatchTargets(blockInstruction, bodyAddress, _code.Count);
    }

    private int EmitExpression(GameEventScriptBytecodeExpressionProgram program, ExpressionState state)
    {
        var stack = new Stack<int>();
        foreach (var instruction in program.Instructions)
        {
            EmitExpressionInstruction(instruction, state, stack);
        }

        _maxFrameSlots = Math.Max(_maxFrameSlots, state.NextSlot);
        _currentFrameSlotCount = Math.Max(_currentFrameSlotCount, state.NextSlot);
        return stack.Count > 0 ? stack.Peek() : -1;
    }

    private void EmitExpressionInstruction(
        GameEventScriptBytecodeStackInstruction instruction,
        ExpressionState state,
        Stack<int> stack)
    {
        switch (instruction.OpCode)
        {
            case GameEventScriptBytecodeOpCode.LoadConstant:
                stack.Push(EmitValueInstruction(state, instruction.OpCode, data: instruction.ConstantIndex));
                break;

            case GameEventScriptBytecodeOpCode.LoadSlot:
                stack.Push(EmitValueInstruction(state, instruction.OpCode, a: instruction.A));
                break;

            case GameEventScriptBytecodeOpCode.Cast:
            case GameEventScriptBytecodeOpCode.TypeCheck:
            case GameEventScriptBytecodeOpCode.MemberAccess:
            case GameEventScriptBytecodeOpCode.Unary:
                if (stack.TryPop(out var unarySource))
                {
                    var layoutIndex = AddOperationLayout(instruction, [unarySource], state, count: 1);
                    stack.Push(EmitValueInstruction(state, instruction.OpCode, a: unarySource, data: layoutIndex));
                }
                break;

            case GameEventScriptBytecodeOpCode.ShortCircuitOr:
            case GameEventScriptBytecodeOpCode.ShortCircuitAnd:
            case GameEventScriptBytecodeOpCode.ShortCircuitImplies:
                if (stack.TryPop(out var left))
                {
                    stack.Push(EmitShortCircuitExpression(instruction, state, left));
                }
                break;

            case GameEventScriptBytecodeOpCode.PredicateTest:
                if (stack.TryPop(out var input))
                {
                    var layoutIndex = AddOperationLayout(instruction, [input], state, count: 1);
                    stack.Push(EmitValueInstruction(state, instruction.OpCode, a: input, data: layoutIndex));
                }
                break;

            case GameEventScriptBytecodeOpCode.Dice:
                stack.Push(EmitValueInstruction(state, instruction.OpCode, a: instruction.A, b: instruction.B));
                break;

            case GameEventScriptBytecodeOpCode.Pipeline:
                if (instruction.PipelineProgram is not null)
                {
                    var layoutIndex = AddPipelineLayout(instruction.PipelineProgram, state);
                    stack.Push(EmitValueInstruction(state, instruction.OpCode, data: layoutIndex));
                }
                break;

            case GameEventScriptBytecodeOpCode.GeneratedCollection:
                if (instruction.GeneratedCollectionProgram is not null)
                {
                    var layoutIndex = AddGeneratedCollectionLayout(instruction.GeneratedCollectionProgram, state);
                    stack.Push(EmitValueInstruction(state, instruction.OpCode, data: layoutIndex));
                }
                break;

            case GameEventScriptBytecodeOpCode.GuardedChoice:
                if (instruction.GuardedChoiceProgram is not null)
                {
                    var layoutIndex = AddGuardedChoiceLayout(instruction.GuardedChoiceProgram, state);
                    stack.Push(EmitValueInstruction(state, instruction.OpCode, data: layoutIndex));
                }
                break;

            default:
                EmitDefaultExpressionInstruction(instruction, state, stack);
                break;
        }
    }

    private void EmitDefaultExpressionInstruction(
        GameEventScriptBytecodeStackInstruction instruction,
        ExpressionState state,
        Stack<int> stack)
    {
        var argumentCount = GetStackArgumentCount(instruction);
        var operands = new int[Math.Max(argumentCount, 0)];
        for (var index = operands.Length - 1; index >= 0; index--)
        {
            operands[index] = stack.TryPop(out var operand) ? operand : -1;
        }

        var a = operands.Length > 0 ? operands[0] : instruction.A;
        var b = operands.Length > 1 ? operands[1] : instruction.B;
        var c = operands.Length > 2 ? operands[2] : -1;
        var layoutIndex = NeedsOperationLayout(instruction)
            ? AddOperationLayout(instruction, operands, state, argumentCount)
            : -1;
        stack.Push(EmitValueInstruction(state, instruction.OpCode, a, b, c, data: layoutIndex));
    }

    private int EmitShortCircuitExpression(GameEventScriptBytecodeStackInstruction instruction, ExpressionState state, int left)
    {
        var result = state.Allocate();
        if (instruction.OpCode == GameEventScriptBytecodeOpCode.ShortCircuitImplies)
        {
            Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.ShortCircuitImplies, Dest: result, A: left, B: left));
        }
        else
        {
            Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.CopySlot, Dest: result, A: left));
        }

        var branch = instruction.OpCode switch
        {
            GameEventScriptBytecodeOpCode.ShortCircuitOr => GameEventScriptBytecodeOpCode.JumpIfTrue,
            GameEventScriptBytecodeOpCode.ShortCircuitAnd => GameEventScriptBytecodeOpCode.JumpIfFalse,
            _ => GameEventScriptBytecodeOpCode.JumpIfFalse
        };
        var jump = Emit(new GameEventScriptBytecodeInstruction(branch, A: left));
        if (instruction.ExpressionProgram is not null)
        {
            var right = EmitExpression(instruction.ExpressionProgram, state);
            var opCode = instruction.OpCode switch
            {
                GameEventScriptBytecodeOpCode.ShortCircuitOr => GameEventScriptBytecodeOpCode.Or,
                GameEventScriptBytecodeOpCode.ShortCircuitAnd => GameEventScriptBytecodeOpCode.And,
                _ => GameEventScriptBytecodeOpCode.ShortCircuitImplies
            };
            Emit(new GameEventScriptBytecodeInstruction(opCode, Dest: result, A: left, B: right));
        }

        PatchTarget(jump, _code.Count);
        return result;
    }

    private int AddOperationLayout(
        GameEventScriptBytecodeStackInstruction instruction,
        IReadOnlyList<int> argumentSlots,
        ExpressionState state,
        int count = 0)
    {
        var names = instruction.Names ?? [];
        var parameterSlots = instruction.Slots ??
            (instruction.OpCode == GameEventScriptBytecodeOpCode.PredicateTest && instruction.A >= 0
                ? [instruction.A]
                : null);
        var layoutIndex = _operationLayouts.Count;
        var namedArgumentLayoutIndex = names.Length == 0 ? -1 : _resolveNamedArgumentLayoutIndex(names);
        _operationLayouts.Add(CreateOperationLayout(expressionEntryAddress: -1));
        if (instruction.OpCode == GameEventScriptBytecodeOpCode.SeededRandom &&
            instruction.ExpressionProgram is not null)
        {
            _deferredHelperEmitters.Add(() =>
            {
                var expressionEntryAddress = EmitExpressionEntry(instruction.ExpressionProgram, state);
                _operationLayouts[layoutIndex] = CreateOperationLayout(expressionEntryAddress);
            });
        }

        return layoutIndex;

        GameEventScriptBytecodeOperationLayout CreateOperationLayout(int expressionEntryAddress)
            => new(
                instruction.OpCode,
                instruction.DiagnosticName,
                instruction.DiagnosticArgumentName,
                names,
                argumentSlots,
                parameterSlots,
                instruction.DeclaredTypes,
                instruction.CallableKind,
                instruction.CastKind,
                instruction.B,
                namedArgumentLayoutIndex,
                expressionEntryAddress: expressionEntryAddress,
                count: count);
    }

    private int AddPublishLayout(GameEventScriptBytecodePublishLayoutEntry layout)
    {
        var index = _publishLayouts.Count;
        _publishLayouts.Add(layout);
        return index;
    }

    private int AddIterationSourceLayout(GameEventScriptBytecodeIterationSourceProgram source, ExpressionState state)
    {
        var collectionSlot = -1;
        var rangeFromSlot = -1;
        var rangeToSlot = -1;
        var rangeStepSlot = -1;
        if (source.Kind == GameEventScriptBytecodeIterationSourceKind.Collection)
        {
            collectionSlot = source.CollectionProgram is null
                ? -1
                : EmitExpression(source.CollectionProgram, state);
            DeferExpressionEntry(source.CollectionProgram, state.BaseSlot);
        }
        else
        {
            rangeFromSlot = source.RangeFromProgram is null
                ? -1
                : EmitExpression(source.RangeFromProgram, state);
            DeferExpressionEntry(source.RangeFromProgram, state.BaseSlot);
            rangeToSlot = source.RangeToProgram is null
                ? -1
                : EmitExpression(source.RangeToProgram, state);
            DeferExpressionEntry(source.RangeToProgram, state.BaseSlot);
            rangeStepSlot = source.RangeStepProgram is null
                ? -1
                : EmitExpression(source.RangeStepProgram, state);
            DeferExpressionEntry(source.RangeStepProgram, state.BaseSlot);
        }

        var index = _iterationSourceLayouts.Count;
        _iterationSourceLayouts.Add(new GameEventScriptBytecodeIterationSourceLayout(
            source.Kind,
            collectionSlot,
            rangeFromSlot,
            rangeToSlot,
            rangeStepSlot));
        return index;
    }

    private int AddLoopLayout(GameEventScriptBytecodeLoopLayout layout)
    {
        var index = _loopLayouts.Count;
        _loopLayouts.Add(layout);
        return index;
    }

    private int AddSeededRandomBlockLayout(GameEventScriptBytecodeSeededRandomBlockLayout layout)
    {
        var index = _seededRandomBlockLayouts.Count;
        _seededRandomBlockLayouts.Add(layout);
        return index;
    }

    private int AddSelectorLayout(GameEventScriptBytecodeSelectorProgram selector, ExpressionState state)
    {
        var index = _selectorLayouts.Count;
        _selectorLayouts.Add(new GameEventScriptBytecodeSelectorLayout(
            selector.Kind,
            selector.IdentifierSlot,
            selector.EdgeMode,
            selector.SecondaryMode,
            selector.Count,
            selector.SecondaryIdentifierSlot,
            selector.Flag));
        _deferredHelperEmitters.Add(() =>
        {
            var expressionEntryAddress = selector.ExpressionProgram is null
                ? -1
                : EmitExpressionEntry(selector.ExpressionProgram, state);
            var secondaryExpressionEntryAddress = selector.SecondaryExpressionProgram is null
                ? -1
                : EmitExpressionEntry(selector.SecondaryExpressionProgram, state);
            selector.SetLinearEntryAddresses(expressionEntryAddress, secondaryExpressionEntryAddress);
            _selectorLayouts[index] = new GameEventScriptBytecodeSelectorLayout(
                selector.Kind,
                selector.IdentifierSlot,
                selector.EdgeMode,
                selector.SecondaryMode,
                selector.Count,
                selector.SecondaryIdentifierSlot,
                selector.Flag,
                expressionEntryAddress,
                secondaryExpressionEntryAddress);
        });
        return index;
    }

    private int AddPipelineLayout(GameEventScriptBytecodePipelineProgram program, ExpressionState state)
    {
        var sourceSlot = EmitExpression(program.SourceProgram, state);
        var prefixSelectorIndexes = program.PrefixSelectors
            .Select(selector => AddSelectorLayout(selector, state))
            .ToArray();
        var terminalSelectorIndex = AddSelectorLayout(program.TerminalSelector, state);
        var index = _pipelineLayouts.Count;
        _pipelineLayouts.Add(new GameEventScriptBytecodePipelineLayout(
            sourceSlot,
            prefixSelectorIndexes,
            terminalSelectorIndex));
        return index;
    }

    private int AddGeneratedCollectionLayout(GameEventScriptBytecodeGeneratedCollectionProgram program, ExpressionState state)
    {
        var sourceLayoutIndex = AddIterationSourceLayout(program.Source, state);
        var index = _generatedCollectionLayouts.Count;
        _generatedCollectionLayouts.Add(new GameEventScriptBytecodeGeneratedCollectionLayout(
            program.CollectionType,
            program.IdentifierSlot,
            sourceLayoutIndex));
        _deferredHelperEmitters.Add(() =>
        {
            var predicateEntryAddress = program.PredicateProgram is null
                ? -1
                : EmitExpressionEntry(program.PredicateProgram, state);
            var projectionEntryAddress = EmitExpressionEntry(program.ProjectionProgram, state);
            _generatedCollectionLayouts[index] = new GameEventScriptBytecodeGeneratedCollectionLayout(
                program.CollectionType,
                program.IdentifierSlot,
                sourceLayoutIndex,
                predicateEntryAddress,
                projectionEntryAddress);
        });
        return index;
    }

    private int AddGuardedChoiceLayout(GameEventScriptBytecodeGuardedChoiceProgram program, ExpressionState state)
    {
        var index = _guardedChoiceLayouts.Count;
        _guardedChoiceLayouts.Add(new GameEventScriptBytecodeGuardedChoiceLayout(
            Enumerable.Repeat(-1, program.ValuePrograms.Length).ToArray(),
            Enumerable.Repeat(-1, program.ConditionPrograms.Length).ToArray()));
        _deferredHelperEmitters.Add(() =>
        {
            var valueEntryAddresses = new int[program.ValuePrograms.Length];
            var conditionEntryAddresses = new int[program.ConditionPrograms.Length];
            for (var branchIndex = 0; branchIndex < conditionEntryAddresses.Length; branchIndex++)
            {
                conditionEntryAddresses[branchIndex] = EmitExpressionEntry(program.ConditionPrograms[branchIndex], state);
                valueEntryAddresses[branchIndex] = EmitExpressionEntry(program.ValuePrograms[branchIndex], state);
            }

            var otherwiseEntryAddress = EmitExpressionEntry(program.OtherwiseProgram, state);
            _guardedChoiceLayouts[index] = new GameEventScriptBytecodeGuardedChoiceLayout(
                valueEntryAddresses,
                conditionEntryAddresses,
                otherwiseEntryAddress);
        });
        return index;
    }

    private void FlushDeferredHelpers()
    {
        for (var index = 0; index < _deferredHelperEmitters.Count; index++)
        {
            _deferredHelperEmitters[index]();
        }

        _deferredHelperEmitters.Clear();
    }

    private void DeferExpressionEntry(GameEventScriptBytecodeExpressionProgram? program, int temporaryBaseSlot)
    {
        if (program is null || !CanDeferExpressionEntry(program))
        {
            return;
        }

        _deferredHelperEmitters.Add(() =>
        {
            if (program.LinearEntryAddress < 0)
            {
                _ = EmitExpressionEntry(program, new ExpressionState(temporaryBaseSlot));
            }
        });
    }

    private static bool CanDeferExpressionEntry(GameEventScriptBytecodeExpressionProgram program)
    {
        if (program.Instructions.Length <= 1)
        {
            return false;
        }

        return !ContainsDeferredEntryBlocker(program);
    }

    private static bool ContainsDeferredEntryBlocker(GameEventScriptBytecodeExpressionProgram? program)
    {
        if (program is null)
        {
            return false;
        }

        foreach (var instruction in program.Instructions)
        {
            if (instruction.PipelineProgram is not null ||
                instruction.GeneratedCollectionProgram is not null ||
                instruction.GuardedChoiceProgram is not null ||
                instruction.OpCode == GameEventScriptBytecodeOpCode.SeededRandom ||
                instruction.OpCode == GameEventScriptBytecodeOpCode.PredicateTest ||
                instruction.OpCode == GameEventScriptBytecodeOpCode.Call ||
                (NeedsOperationLayout(instruction) && !CanDeferOperationLayoutExpression(instruction.OpCode)) ||
                ContainsDeferredEntryBlocker(instruction.ExpressionProgram))
            {
                return true;
            }
        }

        return false;
    }

    private static bool CanDeferOperationLayoutExpression(GameEventScriptBytecodeOpCode opCode)
        => opCode is
            GameEventScriptBytecodeOpCode.Cast or
            GameEventScriptBytecodeOpCode.TypeCheck or
            GameEventScriptBytecodeOpCode.MemberAccess or
            GameEventScriptBytecodeOpCode.Unary or
            GameEventScriptBytecodeOpCode.Variadic or
            GameEventScriptBytecodeOpCode.Range or
            GameEventScriptBytecodeOpCode.TypeConstructor or
            GameEventScriptBytecodeOpCode.BuildList or
            GameEventScriptBytecodeOpCode.BuildSequence or
            GameEventScriptBytecodeOpCode.BuildSet or
            GameEventScriptBytecodeOpCode.BuildDictionary or
            GameEventScriptBytecodeOpCode.BuildMessage;

    private static int GetExpressionSlotUpperBound(GameEventScriptBytecodeExpressionProgram? program)
    {
        var maxSlot = -1;
        CollectExpressionSlots(program, ref maxSlot);
        return maxSlot + 1;
    }

    private static void CollectExpressionSlots(GameEventScriptBytecodeExpressionProgram? program, ref int maxSlot)
    {
        if (program is null)
        {
            return;
        }

        foreach (var instruction in program.Instructions)
        {
            if (instruction.OpCode == GameEventScriptBytecodeOpCode.LoadSlot && instruction.A >= 0)
            {
                maxSlot = Math.Max(maxSlot, instruction.A);
            }

            CollectExpressionSlots(instruction.ExpressionProgram, ref maxSlot);
            CollectPipelineSlots(instruction.PipelineProgram, ref maxSlot);
            CollectGeneratedCollectionSlots(instruction.GeneratedCollectionProgram, ref maxSlot);
            CollectGuardedChoiceSlots(instruction.GuardedChoiceProgram, ref maxSlot);
        }
    }

    private static void CollectPipelineSlots(GameEventScriptBytecodePipelineProgram? program, ref int maxSlot)
    {
        if (program is null)
        {
            return;
        }

        CollectExpressionSlots(program.SourceProgram, ref maxSlot);
        foreach (var selector in program.PrefixSelectors)
        {
            CollectSelectorSlots(selector, ref maxSlot);
        }

        CollectSelectorSlots(program.TerminalSelector, ref maxSlot);
    }

    private static void CollectSelectorSlots(GameEventScriptBytecodeSelectorProgram? selector, ref int maxSlot)
    {
        if (selector is null)
        {
            return;
        }

        if (selector.IdentifierSlot >= 0)
        {
            maxSlot = Math.Max(maxSlot, selector.IdentifierSlot);
        }

        if (selector.SecondaryIdentifierSlot >= 0)
        {
            maxSlot = Math.Max(maxSlot, selector.SecondaryIdentifierSlot);
        }

        CollectExpressionSlots(selector.ExpressionProgram, ref maxSlot);
        CollectExpressionSlots(selector.SecondaryExpressionProgram, ref maxSlot);
    }

    private static void CollectGeneratedCollectionSlots(GameEventScriptBytecodeGeneratedCollectionProgram? program, ref int maxSlot)
    {
        if (program is null)
        {
            return;
        }

        if (program.IdentifierSlot >= 0)
        {
            maxSlot = Math.Max(maxSlot, program.IdentifierSlot);
        }

        CollectIterationSourceSlots(program.Source, ref maxSlot);
        CollectExpressionSlots(program.PredicateProgram, ref maxSlot);
        CollectExpressionSlots(program.ProjectionProgram, ref maxSlot);
    }

    private static void CollectGuardedChoiceSlots(GameEventScriptBytecodeGuardedChoiceProgram? program, ref int maxSlot)
    {
        if (program is null)
        {
            return;
        }

        foreach (var condition in program.ConditionPrograms)
        {
            CollectExpressionSlots(condition, ref maxSlot);
        }

        foreach (var value in program.ValuePrograms)
        {
            CollectExpressionSlots(value, ref maxSlot);
        }

        CollectExpressionSlots(program.OtherwiseProgram, ref maxSlot);
    }

    private static void CollectIterationSourceSlots(GameEventScriptBytecodeIterationSourceProgram? source, ref int maxSlot)
    {
        if (source is null)
        {
            return;
        }

        CollectExpressionSlots(source.CollectionProgram, ref maxSlot);
        CollectExpressionSlots(source.RangeFromProgram, ref maxSlot);
        CollectExpressionSlots(source.RangeToProgram, ref maxSlot);
        CollectExpressionSlots(source.RangeStepProgram, ref maxSlot);
    }

    private int EmitValueInstruction(
        ExpressionState state,
        GameEventScriptBytecodeOpCode opCode,
        int a = -1,
        int b = -1,
        int c = -1,
        int data = -1)
    {
        var dest = state.Allocate();
        Emit(new GameEventScriptBytecodeInstruction(opCode, Dest: dest, A: a, B: b, C: c, Data: data));
        return dest;
    }

    private int Emit(GameEventScriptBytecodeInstruction instruction)
    {
        var address = _code.Count;
        _code.Add(instruction);
        return address;
    }

    private void PatchTarget(int address, int target)
        => _code[address] = _code[address] with { Target = target };

    private void PatchTargets(int address, int target, int target2)
        => _code[address] = _code[address] with { Target = target, Target2 = target2 };

    private int ResolveTypeMetadataIndex(string? typeName)
        => string.IsNullOrEmpty(typeName)
            ? -1
            : _resolveTypeMetadataIndex(typeName);

    private static bool NeedsOperationLayout(GameEventScriptBytecodeStackInstruction instruction)
        => instruction.OpCode is
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
            GameEventScriptBytecodeOpCode.Call;

    private static int GetStackArgumentCount(GameEventScriptBytecodeStackInstruction instruction)
        => instruction.OpCode switch
        {
            GameEventScriptBytecodeOpCode.Range => instruction.A,
            GameEventScriptBytecodeOpCode.TypeConstructor => instruction.A,
            GameEventScriptBytecodeOpCode.BuildList => instruction.A,
            GameEventScriptBytecodeOpCode.BuildSequence => instruction.A,
            GameEventScriptBytecodeOpCode.BuildSet => instruction.A,
            GameEventScriptBytecodeOpCode.BuildDictionary => instruction.A,
            GameEventScriptBytecodeOpCode.BuildMessage => instruction.A,
            GameEventScriptBytecodeOpCode.BindHandler => instruction.A + 1,
            GameEventScriptBytecodeOpCode.CallExtension => instruction.A,
            GameEventScriptBytecodeOpCode.Call => instruction.A,
            GameEventScriptBytecodeOpCode.Variadic => instruction.A,
            GameEventScriptBytecodeOpCode.Clamp => 3,
            GameEventScriptBytecodeOpCode.Random => 2,
            GameEventScriptBytecodeOpCode.IndexedAccess => 2,
            GameEventScriptBytecodeOpCode.SeededRandom => 1,
            _ => IsBinary(instruction.OpCode) ? 2 : 0
        };

    private static bool IsBinary(GameEventScriptBytecodeOpCode opCode)
        => opCode is GameEventScriptBytecodeOpCode.Or or
            GameEventScriptBytecodeOpCode.Xor or
            GameEventScriptBytecodeOpCode.And or
            GameEventScriptBytecodeOpCode.Power or
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

    private sealed class ExpressionState(int nextSlot)
    {
        public int BaseSlot { get; } = Math.Max(0, nextSlot);

        public int NextSlot { get; private set; } = Math.Max(0, nextSlot);

        public int Allocate()
            => NextSlot++;
    }
}
