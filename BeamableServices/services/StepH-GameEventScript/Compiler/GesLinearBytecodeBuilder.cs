using System;
using System.Collections.Generic;
using System.Linq;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Types;

namespace StepH.GameEventScript.Compiler;

internal sealed class GesLinearBytecodeBuilder
{
    private readonly Func<string, int> _resolveStringIndex;
    private readonly Func<IReadOnlyList<ushort>, int> _resolveUShortListIndex;
    private readonly Func<GameEventScriptExtensionReference, int> _resolveExternalReferenceIndex;
    private readonly IReadOnlyDictionary<string, GesCallableDefinition> _sourceCallables;
    private readonly IReadOnlyDictionary<string, TypeDefinitionNode> _sourceTypeDefinitions;
    private readonly bool _emitDebugInfo;
    private readonly List<GameEventScriptBytecodeInstruction> _code = [];
    private readonly List<GameEventScriptBytecodeDebugDiagnosticSite> _debugDiagnosticSites = [];
    private readonly List<Action> _deferredHelperEmitters = [];
    private readonly Dictionary<string, GameEventScriptBytecodeCallable> _bytecodeCallables = new(StringComparer.Ordinal);
    private readonly List<(int Address, string CallableName)> _deferredCallableAddressPatches = [];
    private readonly Stack<ScopeBuildState> _scopeBuildStates = new();
    private readonly List<ScopeBuildState> _deferredRootScopePatches = [];
    private int _currentFrameSlotCount;
    private int _maxFrameSlots = 1;

    public GesLinearBytecodeBuilder(
        Func<string, int>? resolveStringIndex = null,
        Func<IReadOnlyList<ushort>, int>? resolveUShortListIndex = null,
        Func<GameEventScriptExtensionReference, int>? resolveExternalReferenceIndex = null,
        IReadOnlyDictionary<string, GesCallableDefinition>? sourceCallables = null,
        IReadOnlyDictionary<string, TypeDefinitionNode>? sourceTypeDefinitions = null,
        bool emitDebugInfo = false)
    {
        _resolveStringIndex = resolveStringIndex ?? (_ => -1);
        _resolveUShortListIndex = resolveUShortListIndex ?? (_ => -1);
        _resolveExternalReferenceIndex = resolveExternalReferenceIndex ?? (_ => -1);
        _sourceCallables = sourceCallables ?? new Dictionary<string, GesCallableDefinition>(StringComparer.Ordinal);
        _sourceTypeDefinitions = sourceTypeDefinitions ?? new Dictionary<string, TypeDefinitionNode>(StringComparer.Ordinal);
        _emitDebugInfo = emitDebugInfo;
    }

    public IReadOnlyList<GameEventScriptBytecodeInstruction> Code => _code;

    public int MaxFrameSlots => _maxFrameSlots;

    public GameEventScriptBytecodeDebugSegment DebugSegment => new(_debugDiagnosticSites);

    public void AddHandlers(
        IEnumerable<GameEventScriptBytecodeHandler> handlers,
        IReadOnlyDictionary<GameEventScriptBytecodeHandler, EventHandlerNode>? sourceHandlers = null)
    {
        if (sourceHandlers is null)
        {
            throw new GameEventScriptCompileException("GameEventScript linear bytecode builder requires source handler statements.");
        }

        foreach (var handler in handlers)
        {
            handler.EntryAddress = _code.Count;
            var reserveSlotsAddress = EmitReserveSlots(0);
            var initialSlotCount = handler.Parameters.Count;
            _currentFrameSlotCount = GetSlotCount(handler.Slots);
            _maxFrameSlots = Math.Max(_maxFrameSlots, _currentFrameSlotCount);
            EmitParameterCasts(handler.Parameters, handler.ParameterTypes, handler.Slots);
            if (sourceHandlers.TryGetValue(handler, out var sourceHandler))
            {
                EmitSourceStatements(sourceHandler.Statements, createsScope: false, new SourceContext(handler.Slots));
            }
            else
            {
                throw new GameEventScriptCompileException(
                    $"GameEventScript linear bytecode builder requires source statements for handler '{FormatSignature(handler.Message, handler.SignatureLabels)}'.");
            }

            EmitReturnVoid();
            var handlerSlotCount = _currentFrameSlotCount;
            PatchDeferredRootScopes(handlerSlotCount);
            FlushDeferredHelpers();
            PatchReserveSlots(reserveSlotsAddress, handlerSlotCount - initialSlotCount);
            _maxFrameSlots = Math.Max(_maxFrameSlots, handlerSlotCount);
        }
    }

    public void AddCallables(
        IEnumerable<GameEventScriptBytecodeCallable> callables,
        IReadOnlyDictionary<string, GesCallableDefinition>? sourceCallables = null)
    {
        if (sourceCallables is null)
        {
            throw new GameEventScriptCompileException("GameEventScript linear bytecode builder requires source callable bodies.");
        }

        var callableArray = callables.ToArray();
        foreach (var callable in callableArray)
        {
            _bytecodeCallables[callable.Name] = callable;
        }

        foreach (var callable in callableArray)
        {
            if (sourceCallables.TryGetValue(callable.Name, out var sourceCallable))
            {
                AddSourceCallable(callable, sourceCallable);
                continue;
            }

            throw new GameEventScriptCompileException(
                $"GameEventScript linear bytecode builder requires source callable body for '{FormatSignature(callable.Name, callable.SignatureLabels)}'.");
        }
    }

    public void PatchDeferredCallableAddresses()
    {
        foreach (var (address, callableName) in _deferredCallableAddressPatches)
        {
            if (!_bytecodeCallables.TryGetValue(callableName, out var callable) ||
                callable.EntryAddress < 0)
            {
                throw new GameEventScriptCompileException($"GameEventScript bytecode lowerer could not resolve callable entry address for '{callableName}'.");
            }

            _code[address] = _code[address] with
            {
                EntryAddress = ToUShortOperand(callable.EntryAddress, "callable entry address")
            };
        }

        _deferredCallableAddressPatches.Clear();
    }

    public void AddTypeDefinitions(
        IEnumerable<GameEventScriptBytecodeTypeDefinition> types,
        IReadOnlyDictionary<string, TypeDefinitionNode>? sourceTypes = null)
    {
        if (sourceTypes is null)
        {
            throw new GameEventScriptCompileException("GameEventScript linear bytecode builder requires source type definitions.");
        }

        AddSourceTypeDefinitions(types, sourceTypes);
    }

    private void AddSourceCallable(GameEventScriptBytecodeCallable callable, GesCallableDefinition sourceCallable)
    {
        var slots = GesBytecodeLowerer.CollectCallableSlots(sourceCallable, _sourceCallables, _sourceTypeDefinitions);
        var context = new SourceContext(slots);
        callable.EntryAddress = _code.Count;
        var reserveSlotsAddress = EmitReserveSlots(0);
        var initialSlotCount = callable.Parameters.Count;
        var callableSlotCount = Math.Max(callable.Parameters.Count + 1, context.SlotCount);
        _currentFrameSlotCount = callableSlotCount;
        _maxFrameSlots = Math.Max(_maxFrameSlots, callableSlotCount);

        for (var index = 0; index < callable.Parameters.Count; index++)
        {
            var parameterSlot = context.RequireSlot(callable.Parameters[index]);
            if (index < callable.ParameterTypes.Count && !string.IsNullOrEmpty(callable.ParameterTypes[index]))
            {
                EmitCastSlot(parameterSlot, parameterSlot, callable.ParameterTypes[index]);
            }
        }

        var state = new ExpressionState(context.SlotCount);
        var result = EmitSourceExpression(sourceCallable.Expression, context, state);
        callable.ReturnSlot = result;
        EmitReturnValue(result);
        callableSlotCount = Math.Max(callableSlotCount, _currentFrameSlotCount);
        PatchDeferredRootScopes(callableSlotCount);
        FlushDeferredHelpers();
        PatchReserveSlots(reserveSlotsAddress, callableSlotCount - initialSlotCount);
        _maxFrameSlots = Math.Max(_maxFrameSlots, callableSlotCount);
    }

    private void AddSourceTypeDefinitions(
        IEnumerable<GameEventScriptBytecodeTypeDefinition> types,
        IReadOnlyDictionary<string, TypeDefinitionNode> sourceTypes)
    {
        var slots = GesBytecodeLowerer.CollectTypeDefinitionSlots(_sourceCallables, sourceTypes);
        var context = new SourceContext(slots);
        foreach (var type in types)
        {
            if (!sourceTypes.TryGetValue(type.Name, out var sourceType))
            {
                continue;
            }

            var sourceFields = sourceType.Fields.ToDictionary(field => field.Name, StringComparer.Ordinal);
            foreach (var field in type.Fields)
            {
                if (!sourceFields.TryGetValue(field.Name, out var sourceField))
                {
                    continue;
                }

                if (sourceField.MinimumExpression is not null)
                {
                    field.MinimumEntryAddress = EmitSourceExpressionEntry(sourceField.MinimumExpression, context);
                }

                if (sourceField.MaximumExpression is not null)
                {
                    field.MaximumEntryAddress = EmitSourceExpressionEntry(sourceField.MaximumExpression, context);
                }

                if (sourceField.ComputedExpression is not null)
                {
                    field.ComputedEntryAddress = EmitSourceExpressionEntry(sourceField.ComputedExpression, context);
                }
            }
        }

        FlushDeferredHelpers();
    }

    private int EmitSourceExpressionEntry(ExpressionNode expression, SourceContext context)
        => EmitSourceExpressionEntry(expression, context, new ExpressionState(context.SlotCount));

    private int EmitSourceExpressionEntry(
        ExpressionNode expression,
        SourceContext context,
        ExpressionState state,
        int? initialSlotCount = null)
    {
        var entry = _code.Count;
        var reserveSlotsAddress = EmitReserveSlots(0);
        var initialSlots = initialSlotCount ?? context.SlotCount;
        var result = EmitSourceExpression(expression, context, state);
        _maxFrameSlots = Math.Max(_maxFrameSlots, state.NextSlot);
        _currentFrameSlotCount = Math.Max(_currentFrameSlotCount, state.NextSlot);
        EmitReturnValue(result);
        PatchDeferredRootScopes(state.NextSlot);
        PatchReserveSlots(reserveSlotsAddress, state.NextSlot - initialSlots);
        return entry;
    }

    private int EmitReserveSlots(int count)
        => Emit(CreateInstruction(GameEventScriptBytecodeOpCode.ReserveSlots, a: count));

    private int BeginScope(int activeSlotCount)
    {
        var address = EmitReserveSlots(0);
        _scopeBuildStates.Push(new ScopeBuildState(address, activeSlotCount, isRootScope: _scopeBuildStates.Count == 0));
        return address;
    }

    private void EndScope(int address)
    {
        if (_scopeBuildStates.Count == 0 ||
            _scopeBuildStates.Peek().Address != address)
        {
            throw new GameEventScriptCompileException("GameEventScript bytecode lowerer could not close scope.");
        }

        var scope = _scopeBuildStates.Pop();
        var exitAddress = EmitReleaseSlots(0);
        scope.SetExitAddress(exitAddress);
        if (scope.IsRootScope)
        {
            _deferredRootScopePatches.Add(scope);
        }
        else
        {
            PatchScope(scope, scope.BaseSlotCount);
        }
    }

    private void PatchDeferredRootScopes(int rootSlotCount)
    {
        foreach (var scope in _deferredRootScopePatches)
        {
            PatchScope(scope, rootSlotCount);
        }

        _deferredRootScopePatches.Clear();
    }

    private void PatchScope(ScopeBuildState scope, int activeSlotCount)
    {
        var localSlotCount = Math.Max(0, scope.MaxSlotCount - activeSlotCount);
        if (localSlotCount == 0)
        {
            _code[scope.Address] = new GameEventScriptBytecodeInstruction { OpCode = GameEventScriptBytecodeOpCode.Nop };
            _code[scope.ExitAddress] = new GameEventScriptBytecodeInstruction { OpCode = GameEventScriptBytecodeOpCode.Nop };
            return;
        }

        _code[scope.Address] = CreateInstruction(GameEventScriptBytecodeOpCode.ReserveSlots, a: localSlotCount);
        _code[scope.ExitAddress] = CreateInstruction(GameEventScriptBytecodeOpCode.ReleaseSlots, a: localSlotCount);
    }

    private int AllocateScopedLocalSlot()
    {
        if (_scopeBuildStates.Count > 0)
        {
            var scope = _scopeBuildStates.Peek();
            var scopedSlot = scope.AllocateSlot();
            MarkAllocatedSlotCount(scopedSlot + 1);
            return scopedSlot;
        }

        var slot = _currentFrameSlotCount;
        MarkAllocatedSlotCount(slot + 1);
        return slot;
    }

    private static int GetSlotCount(IReadOnlyDictionary<string, int> slots)
        => slots.Count == 0
            ? 1
            : slots.Values.Max() + 1;

    private void PatchReserveSlots(int address, int count)
    {
        if ((uint)address >= (uint)_code.Count ||
            _code[address].OpCode != GameEventScriptBytecodeOpCode.ReserveSlots)
        {
            throw new GameEventScriptCompileException("GameEventScript bytecode lowerer could not patch ReserveSlots prolog.");
        }

        _code[address] = _code[address] with
        {
            Count = ToUShortOperand(Math.Max(0, count), "local slot reserve count")
        };
    }

    private int EmitReleaseSlots(int count)
        => Emit(CreateInstruction(GameEventScriptBytecodeOpCode.ReleaseSlots, a: count));

    private void EmitParameterCasts(
        IReadOnlyList<string> parameters,
        IReadOnlyList<string?> parameterTypes,
        IReadOnlyDictionary<string, int> slots)
    {
        for (var index = 0; index < parameters.Count; index++)
        {
            if (!slots.TryGetValue(parameters[index], out var slot))
            {
                continue;
            }

            if (index < parameterTypes.Count && !string.IsNullOrEmpty(parameterTypes[index]))
            {
                EmitCastSlot(slot, slot, parameterTypes[index]);
            }
        }
    }

    private void EmitReturnVoid()
    {
        RemoveTrailingReleaseSlots();
        Emit(new GameEventScriptBytecodeInstruction { OpCode = GameEventScriptBytecodeOpCode.ReturnVoid });
    }

    private void EmitReturnValue(int slot)
    {
        RemoveTrailingReleaseSlots();
        Emit(CreateInstruction(GameEventScriptBytecodeOpCode.ReturnValue, a: slot));
    }

    private void RemoveTrailingReleaseSlots()
    {
        for (var index = _code.Count - 1; index >= 0; index--)
        {
            if (_code[index].OpCode == GameEventScriptBytecodeOpCode.Nop)
            {
                continue;
            }

            if (_code[index].OpCode != GameEventScriptBytecodeOpCode.ReleaseSlots)
            {
                break;
            }

            _code[index] = new GameEventScriptBytecodeInstruction { OpCode = GameEventScriptBytecodeOpCode.Nop };
        }
    }

    private void EmitSourceStatements(
        IReadOnlyList<StatementNode> statements,
        bool createsScope,
        SourceContext context,
        int? temporaryBaseSlot = null)
    {
        if (temporaryBaseSlot is not null)
        {
            context = context.WithTemporaryBaseSlot(temporaryBaseSlot.Value);
        }

        var scopeAddress = -1;
        if (createsScope)
        {
            scopeAddress = BeginScope(context.SlotCount);
            context = context.CreateScope(context.SlotCount, AllocateScopedLocalSlot);
        }

        foreach (var statement in statements)
        {
            EmitSourceStatement(statement, context);
        }

        if (createsScope)
        {
            EndScope(scopeAddress);
        }
    }

    private void EmitSourceStatement(StatementNode statement, SourceContext context)
    {
        switch (statement)
        {
            case LetStatementNode let:
            {
                var state = new ExpressionState(context.SlotCount);
                var canDeclareBeforeExpression = !ExpressionReferencesIdentifier(let.Expression, let.Identifier);
                var letSlot = canDeclareBeforeExpression ? context.DeclareSlot(let.Identifier) : -1;
                if (canDeclareBeforeExpression)
                {
                    state.EnsureNextSlot(context.SlotCount);
                }

                int diagnosticAddress;
                if (canDeclareBeforeExpression &&
                    TryEmitSourceExpressionToSlot(let.Expression, letSlot, context, state, out diagnosticAddress))
                {
                    // Expression emitted directly into the declared local.
                }
                else
                {
                    var result = EmitSourceExpression(let.Expression, context, state);
                    if (letSlot < 0)
                    {
                        letSlot = context.DeclareSlot(let.Identifier);
                    }

                    diagnosticAddress = result == letSlot
                        ? _code.Count - 1
                        : Emit(CreateInstruction(GameEventScriptBytecodeOpCode.MoveSlot, dest: letSlot, a: result));
                }

                if (!string.IsNullOrEmpty(let.DeclaredType))
                {
                    diagnosticAddress = EmitCastSlot(letSlot, letSlot, let.DeclaredType);
                }

                AddDebugDiagnosticSite(
                    GameEventScriptBytecodeDiagnosticKind.LetEvaluated,
                    GameEventScriptBytecodeDiagnosticTiming.AfterInstruction,
                    diagnosticAddress,
                    letSlot,
                    let.Identifier);
                AddDebugDiagnosticSite(
                    GameEventScriptBytecodeDiagnosticKind.ExpressionEvaluatedToNothing,
                    GameEventScriptBytecodeDiagnosticTiming.AfterInstruction,
                    diagnosticAddress,
                    letSlot,
                    let.Identifier);
                break;
            }

            case ExpressionStatementNode expressionStatement:
            {
                var result = EmitSourceExpression(expressionStatement.Expression, context, new ExpressionState(context.SlotCount));
                if (_code.Count > 0)
                {
                    AddDebugDiagnosticSite(
                        GameEventScriptBytecodeDiagnosticKind.ExpressionEvaluatedToNothing,
                        GameEventScriptBytecodeDiagnosticTiming.AfterInstruction,
                        _code.Count - 1,
                        result,
                        expressionStatement.Expression.GetType().Name);
                }

                break;
            }

            case PublishStatementNode publish:
                EmitSourcePublish(publish, context);
                break;

            case IfStatementNode ifStatement:
                EmitSourceIf(ifStatement, context);
                break;

            case ForStatementNode forStatement:
                EmitSourceLoop(forStatement, context);
                break;

            case SeededRandomStatementNode seededRandom:
                EmitSourceSeededRandom(seededRandom, context);
                break;
        }
    }

    private void EmitSourcePublish(PublishStatementNode publish, SourceContext context)
    {
        var state = new ExpressionState(context.SlotCount);
        var tagSlots = new List<int>(publish.TagExpressions.Count);
        foreach (var tagExpression in publish.TagExpressions)
        {
            tagSlots.Add(EmitSourceExpression(tagExpression, context, state));
        }

        var hasTags = tagSlots.Count > 0;
        var tagSlotListIndex = hasTags ? ResolveSlotListIndex(tagSlots) : 0;
        if (publish.MessageExpression is MessageLiteralExpressionNode message)
        {
            var argumentNames = new string[message.Arguments.Count];
            var argumentSlots = new List<int>(message.Arguments.Count);
            for (var argumentIndex = 0; argumentIndex < message.Arguments.Count; argumentIndex++)
            {
                var argument = message.Arguments[argumentIndex];
                argumentNames[argumentIndex] = argument.Name;
                argumentSlots.Add(EmitSourceExpression(argument.Expression, context, state));
            }

            var opCode = publish.Kind switch
            {
                PublishStatementKind.Publish when hasTags => GameEventScriptBytecodeOpCode.PublishMessageWithTags,
                PublishStatementKind.Publish => GameEventScriptBytecodeOpCode.PublishMessage,
                _ when hasTags => GameEventScriptBytecodeOpCode.EmitMessageWithTags,
                _ => GameEventScriptBytecodeOpCode.EmitMessage
            };
            Emit(CreateInstruction(
                opCode,
                a: ResolveMessageShapeIndex(message.Message, argumentNames),
                b: ResolveRequiredSlotListIndex(argumentSlots),
                c: tagSlotListIndex));
            return;
        }

        var messageSlot = EmitSourceExpression(publish.MessageExpression, context, state);
        var messageValueOpCode = publish.Kind switch
        {
            PublishStatementKind.Publish when hasTags => GameEventScriptBytecodeOpCode.PublishMessageValueWithTags,
            PublishStatementKind.Publish => GameEventScriptBytecodeOpCode.PublishMessageValue,
            _ when hasTags => GameEventScriptBytecodeOpCode.EmitMessageValueWithTags,
            _ => GameEventScriptBytecodeOpCode.EmitMessageValue
        };
        Emit(CreateInstruction(
            messageValueOpCode,
            a: messageSlot,
            c: tagSlotListIndex));
    }

    private void EmitSourceIf(IfStatementNode ifStatement, SourceContext context)
    {
        var condition = EmitSourceExpression(ifStatement.Condition, context, new ExpressionState(context.SlotCount));
        var jumpToElse = Emit(CreateJumpInstruction(GameEventScriptBytecodeOpCode.JumpIfNotTrue, conditionSlot: condition));
        EmitSourceStatements(ifStatement.ThenBody.Statements, ifStatement.ThenBody.IsBlock, context);
        if (ifStatement.ElseBody is null)
        {
            PatchTarget(jumpToElse, _code.Count);
        }
        else
        {
            var jumpToEnd = Emit(CreateJumpInstruction(GameEventScriptBytecodeOpCode.Jump));
            PatchTarget(jumpToElse, _code.Count);
            EmitSourceStatements(ifStatement.ElseBody.Statements, ifStatement.ElseBody.IsBlock, context);
            PatchTarget(jumpToEnd, _code.Count);
        }
    }

    private void EmitSourceLoop(ForStatementNode forStatement, SourceContext context)
    {
        var outerScopeAddress = BeginScope(context.SlotCount);
        var loopContext = context.CreateScope(context.SlotCount, AllocateScopedLocalSlot);
        var identifierSlot = loopContext.DeclareSlot(forStatement.Identifier);
        var state = new ExpressionState(loopContext.SlotCount);
        var iteratorSlot = EmitSourceIterator(forStatement.Source, context, state);

        var loopAddress = _code.Count;
        var nextInstruction = Emit(CreateInstruction(
            GameEventScriptBytecodeOpCode.IteratorNext,
            dest: identifierSlot,
            a: iteratorSlot));
        if (forStatement.Body.IsBlock)
        {
            EmitSourceStatements(forStatement.Body.Statements, createsScope: true, loopContext, state.NextSlot);
        }
        else
        {
            var iterationScopeAddress = BeginScope(state.NextSlot);
            var iterationContext = loopContext.CreateScope(state.NextSlot, AllocateScopedLocalSlot);
            EmitSourceStatements(forStatement.Body.Statements, createsScope: false, iterationContext, state.NextSlot);
            EndScope(iterationScopeAddress);
        }

        Emit(CreateJumpInstruction(GameEventScriptBytecodeOpCode.Jump, targetAddress: loopAddress));

        var endAddress = _code.Count;
        PatchB(nextInstruction, endAddress);
        Emit(CreateInstruction(GameEventScriptBytecodeOpCode.IteratorClose, a: iteratorSlot));
        EndScope(outerScopeAddress);
    }

    private int EmitSourceGeneratedCollection(GeneratedCollectionExpressionNode generatedCollection, SourceContext context, ExpressionState state)
    {
        var builderOpCode = generatedCollection.CollectionType == "list"
            ? GameEventScriptBytecodeOpCode.CollectionBuilderList
            : throw new GameEventScriptCompileException($"GameEventScript bytecode lowerer does not support generated collection type '{generatedCollection.CollectionType}'.");

        var builderSlot = AllocateSlot(state);
        Emit(CreateInstruction(builderOpCode, dest: builderSlot));

        var outerScopeAddress = BeginScope(state.NextSlot);
        var collectionContext = context.CreateScope(state.NextSlot, AllocateScopedLocalSlot);
        var identifierSlot = collectionContext.DeclareSlot(generatedCollection.Identifier);
        state.EnsureNextSlot(collectionContext.SlotCount);
        var iteratorSlot = EmitSourceIterator(generatedCollection.Source, context, state);
        var itemSlot = AllocateSlot(state);

        var loopAddress = _code.Count;
        var nextInstruction = Emit(CreateInstruction(
            GameEventScriptBytecodeOpCode.IteratorNext,
            dest: itemSlot,
            a: iteratorSlot));
        var iterationScopeAddress = BeginScope(state.NextSlot);
        var iterationContext = collectionContext.CreateScope(state.NextSlot, AllocateScopedLocalSlot);
        Emit(CreateInstruction(GameEventScriptBytecodeOpCode.MoveSlot, dest: identifierSlot, a: itemSlot));

        var skipProjectionJump = -1;
        if (generatedCollection.Predicate is not null)
        {
            var predicateSlot = EmitSourceExpression(generatedCollection.Predicate, iterationContext, state);
            skipProjectionJump = Emit(CreateJumpInstruction(GameEventScriptBytecodeOpCode.JumpIfNotTrue, conditionSlot: predicateSlot));
        }

        var projectionSlot = EmitSourceExpression(generatedCollection.Projection, iterationContext, state);
        Emit(CreateInstruction(GameEventScriptBytecodeOpCode.CollectionBuilderAdd, a: builderSlot, b: projectionSlot));
        if (skipProjectionJump >= 0)
        {
            PatchTarget(skipProjectionJump, _code.Count);
        }

        EndScope(iterationScopeAddress);
        Emit(CreateJumpInstruction(GameEventScriptBytecodeOpCode.Jump, targetAddress: loopAddress));

        var endAddress = _code.Count;
        PatchB(nextInstruction, endAddress);
        Emit(CreateInstruction(GameEventScriptBytecodeOpCode.IteratorClose, a: iteratorSlot));
        EndScope(outerScopeAddress);

        var resultSlot = AllocateSlot(state);
        Emit(CreateInstruction(GameEventScriptBytecodeOpCode.CollectionBuilderFinish, dest: resultSlot, a: builderSlot));
        return resultSlot;
    }

    private void EmitSourceSeededRandom(SeededRandomStatementNode seededRandom, SourceContext context)
    {
        var state = new ExpressionState(context.SlotCount);
        EmitRandomPush(seededRandom.SeedExpression, context, state);
        EmitSourceStatements(seededRandom.Body.Statements, seededRandom.Body.IsBlock, context);
        Emit(new GameEventScriptBytecodeInstruction { OpCode = GameEventScriptBytecodeOpCode.RandomPop });
    }

    private int EmitSourceExpression(ExpressionNode expression, SourceContext context, ExpressionState state)
    {
        switch (expression)
        {
            case BooleanLiteralExpressionNode boolean:
                return EmitSourceConstant(state, GameEventScriptValueFactory.GesBoolean(boolean.Value));

            case IntegerLiteralExpressionNode integer:
                return EmitSourceConstant(state, GameEventScriptValueFactory.GesInteger(integer.Value));

            case UnitIntegerLiteralExpressionNode unitInteger:
                return EmitSourceConstant(
                    state,
                    GameEventScriptNumericUnits.TryParseTypeName(unitInteger.UnitName, out var integerUnit)
                        ? GameEventScriptValueFactory.GesInteger(unitInteger.Value, integerUnit)
                        : GameEventScriptValueFactory.GesFloatNaN());

            case FloatLiteralExpressionNode floatLiteral:
                return EmitSourceConstant(state, GameEventScriptValueFactory.GesFloat(floatLiteral.Value));

            case PercentageLiteralExpressionNode percentage:
                return EmitSourceConstant(state, GameEventScriptValueFactory.GesPercentage(percentage.PercentValue / 100d));

            case UnitFloatLiteralExpressionNode unitFloat:
                return EmitSourceConstant(
                    state,
                    GameEventScriptNumericUnits.TryParseTypeName(unitFloat.UnitName, out var unit)
                        ? GameEventScriptValueFactory.GesFloat(unitFloat.Value, unit)
                        : GameEventScriptValueFactory.GesFloatNaN());

            case TextLiteralExpressionNode text:
                return EmitSourceConstant(state, GameEventScriptValueFactory.GesText(text.Value));

            case TagLiteralExpressionNode tag:
                return EmitSourceConstant(state, GameEventScriptValueFactory.GesTag(tag.Name));

            case HandlerLiteralExpressionNode handler:
                return EmitSourceConstant(
                    state,
                    GameEventScriptValueFactory.GesHandler(GameEventScriptMessageSignature.Create(handler.Message, handler.SignatureLabels)));

            case IdentifierExpressionNode identifier:
                return context.RequireSlot(identifier.Name);

            case MessageLiteralExpressionNode message:
                return EmitSourceMessage(message, context, state);

            case ExtensionCallExpressionNode extensionCall:
                return EmitSourceExtensionCall(extensionCall, context, state);

            case ListLiteralExpressionNode list:
                return EmitSourceCollectionBuilder(GameEventScriptBytecodeOpCode.BuildList, list.Items, null, context, state);

            case MapLiteralExpressionNode dictionary:
                return EmitSourceDictionary(dictionary, context, state);

            case UnaryExpressionNode unary:
            {
                var operand = EmitSourceExpression(unary.Operand, context, state);
                return EmitValueInstruction(state, ToUnaryOpCode(unary.Operator), a: operand);
            }

            case VariadicTaggedExpressionNode variadic:
                return EmitSourceVariadic(variadic, context, state);

            case ClampExpressionNode clamp:
            {
                var value = EmitSourceExpression(clamp.Value, context, state);
                var minimum = EmitSourceExpression(clamp.Minimum, context, state);
                var maximum = EmitSourceExpression(clamp.Maximum, context, state);
                return EmitValueInstruction(state, GameEventScriptBytecodeOpCode.Clamp, value, minimum, maximum);
            }

            case RandomExpressionNode random:
            {
                var from = EmitSourceExpression(random.FromExpression, context, state);
                var to = EmitSourceExpression(random.ToExpression, context, state);
                return EmitValueInstruction(state, GameEventScriptBytecodeOpCode.Random, from, to);
            }

            case RangeExpressionNode range:
                return EmitSourceRange(range, context, state);

            case DiceExpressionNode dice:
                return EmitValueInstruction(state, GameEventScriptBytecodeOpCode.Dice, a: dice.DiceCount, b: dice.SideCount);

            case SeededRandomExpressionNode seededRandom:
                return EmitSourceSeededRandomExpression(seededRandom, context, state);

            case GeneratedCollectionExpressionNode generatedCollection:
                return EmitSourceGeneratedCollection(generatedCollection, context, state);

            case GuardedChoiceExpressionNode guardedChoice:
                return EmitSourceGuardedChoice(guardedChoice, context, state);

            case BinaryExpressionNode binary:
                return EmitSourceBinary(binary, context, state);

            case PredicateCallExpressionNode predicateCall:
                return EmitSourcePredicateCall(predicateCall, context, state);

            case ExtensionPredicateExpressionNode extensionPredicate:
                return EmitSourceExtensionPredicate(extensionPredicate, context, state);

            case CallExpressionNode call:
                return EmitSourceCall(call, context, state);

            case TypeCastExpressionNode typeCast:
                return EmitSourceTypeCast(typeCast, context, state);

            case TypeConstructorExpressionNode typeConstructor:
                return EmitSourceTypeConstructor(typeConstructor, context, state);

            case TypeCheckExpressionNode typeCheck:
            {
                var value = EmitSourceExpression(typeCheck.Value, context, state);
                var operand = ResolveDeclaredTypeOperand(typeCheck.TypeName);
                return EmitValueInstruction(state, operand.TypeCheckOpCode, a: value, b: operand.TypeKind, c: operand.CustomTypeNameIndex, unitAndFlags: operand.UnitAndFlags);
            }

            case MemberAccessExpressionNode memberAccess:
            {
                var target = EmitSourceExpression(memberAccess.Target, context, state);
                return EmitValueInstruction(state, GameEventScriptBytecodeOpCode.MemberAccess, a: target, c: ResolveStringIndex(memberAccess.Member));
            }

            case CollectionAccessExpressionNode collectionAccess:
                if (collectionAccess.Selector is ExpressionSelectorNode selector)
                {
                    var target = EmitSourceExpression(collectionAccess.Target, context, state);
                    var index = EmitSourceExpression(selector.Expression, context, state);
                    return EmitValueInstruction(state, GameEventScriptBytecodeOpCode.IndexedAccess, target, index);
                }

                return EmitSourcePipeline(collectionAccess, context, state);

            default:
                throw new GameEventScriptCompileException($"GameEventScript bytecode lowerer does not support expression node '{expression.GetType().Name}'.");
        }
    }

    private bool TryEmitSourceExpressionToSlot(
        ExpressionNode expression,
        int destinationSlot,
        SourceContext context,
        ExpressionState state,
        out int instructionAddress)
    {
        instructionAddress = -1;
        switch (expression)
        {
            case BooleanLiteralExpressionNode boolean:
                instructionAddress = Emit(CreateInstruction(
                    boolean.Value ? GameEventScriptBytecodeOpCode.LoadTrue : GameEventScriptBytecodeOpCode.LoadFalse,
                    dest: destinationSlot));
                return true;

            case IntegerLiteralExpressionNode integer:
                instructionAddress = EmitLoadIntegerToSlot(
                    destinationSlot,
                    integer.Value,
                    (byte)GameEventScriptBytecodeInstructionUnit.UnitNone);
                return true;

            case UnitIntegerLiteralExpressionNode unitInteger:
                instructionAddress = EmitLoadIntegerToSlot(
                    destinationSlot,
                    unitInteger.Value,
                    GameEventScriptNumericUnits.TryParseTypeName(unitInteger.UnitName, out var integerUnit)
                        ? EncodeNumericUnitAndFlags(integerUnit)
                        : (byte)GameEventScriptBytecodeInstructionUnit.UnitNone);
                return true;

            case FloatLiteralExpressionNode floatLiteral:
                instructionAddress = EmitLoadFloatToSlot(
                    destinationSlot,
                    floatLiteral.Value,
                    (byte)GameEventScriptBytecodeInstructionUnit.UnitNone);
                return true;

            case PercentageLiteralExpressionNode percentage:
                instructionAddress = EmitLoadPercentageToSlot(
                    destinationSlot,
                    percentage.PercentValue / 100d);
                return true;

            case UnitFloatLiteralExpressionNode unitFloat:
                instructionAddress = EmitLoadFloatToSlot(
                    destinationSlot,
                    unitFloat.Value,
                    GameEventScriptNumericUnits.TryParseTypeName(unitFloat.UnitName, out var unit)
                        ? EncodeNumericUnitAndFlags(unit)
                        : (byte)GameEventScriptBytecodeInstructionUnit.UnitNone);
                return true;

            case TextLiteralExpressionNode text:
                instructionAddress = Emit(CreateInstruction(
                    GameEventScriptBytecodeOpCode.LoadText,
                    dest: destinationSlot,
                    a: ResolveStringIndex(text.Value)));
                return true;

            case TagLiteralExpressionNode tag:
                instructionAddress = Emit(CreateInstruction(
                    GameEventScriptBytecodeOpCode.LoadTag,
                    dest: destinationSlot,
                    a: ResolveStringIndex(tag.Name)));
                return true;

            case IdentifierExpressionNode identifier:
            {
                var sourceSlot = context.RequireSlot(identifier.Name);
                if (sourceSlot == destinationSlot)
                {
                    return true;
                }

                instructionAddress = Emit(CreateInstruction(
                    GameEventScriptBytecodeOpCode.MoveSlot,
                    dest: destinationSlot,
                    a: sourceSlot));
                return true;
            }

            case UnaryExpressionNode unary:
            {
                var operand = EmitSourceExpression(unary.Operand, context, state);
                instructionAddress = Emit(CreateInstruction(
                    ToUnaryOpCode(unary.Operator),
                    dest: destinationSlot,
                    a: operand));
                return true;
            }

            case BinaryExpressionNode binary
                when binary.Operator is not (GesBinaryOperator.Or or GesBinaryOperator.And or GesBinaryOperator.Implies):
            {
                var left = EmitSourceExpression(binary.Left, context, state);
                var right = EmitSourceExpression(binary.Right, context, state);
                instructionAddress = Emit(CreateInstruction(
                    ToBinaryOpCode(binary),
                    dest: destinationSlot,
                    a: left,
                    b: right));
                return true;
            }

            case PredicateCallExpressionNode predicateCall:
                return TryEmitSourcePredicateCallToSlot(predicateCall, destinationSlot, context, state, out instructionAddress);

            case CallExpressionNode call:
                return TryEmitSourceCallToSlot(call, destinationSlot, context, state, out instructionAddress);

            case TypeCastExpressionNode typeCast:
            {
                var value = EmitSourceExpression(typeCast.Value, context, state);
                instructionAddress = EmitCastSlot(destinationSlot, value, typeCast.TypeName);
                return true;
            }

            default:
                return false;
        }
    }

    private int EmitSourceConstant(ExpressionState state, GameEventScriptValue value)
    {
        if (value is null || value.IsNothing())
        {
            return EmitValueInstruction(state, GameEventScriptBytecodeOpCode.LoadNothing);
        }

        switch (value)
        {
            case GameEventScriptBooleanValue boolean:
                return EmitValueInstruction(state, boolean.Value
                    ? GameEventScriptBytecodeOpCode.LoadTrue
                    : GameEventScriptBytecodeOpCode.LoadFalse);

            case GameEventScriptIntegerValue integer:
                return EmitLoadInteger(
                    state,
                    integer.Value,
                    EncodeNumericUnitAndFlags(integer.Unit));

            case GameEventScriptFloatValue floatValue:
                return EmitLoadFloat(
                    state,
                    EncodeFloatPayload(floatValue),
                    EncodeNumericUnitAndFlags(floatValue.Unit));

            case GameEventScriptPercentageValue percentage:
                return EmitLoadPercentage(
                    state,
                    percentage.Ratio);

            case GameEventScriptTextValue text:
                return EmitValueInstruction(state, GameEventScriptBytecodeOpCode.LoadText, a: ResolveStringIndex(text.Value));

            case GameEventScriptTagValue tag:
                return EmitValueInstruction(state, GameEventScriptBytecodeOpCode.LoadTag, a: ResolveStringIndex(tag.Value));

            case GameEventScriptHandlerValue handler:
            {
                var labels = handler.Signature.Parameters;
                return EmitValueInstruction(
                    state,
                    GameEventScriptBytecodeOpCode.LoadHandler,
                    a: ResolveMessageShapeIndex(handler.Signature.Name, labels));
            }

            default:
                throw new GameEventScriptCompileException($"GameEventScript bytecode inline constants do not support value kind '{value.Kind}'.");
        }
    }

    private StageArgumentPlan PrepareStageArgument(ExpressionNode expression, SourceContext context, ExpressionState state)
    {
        if (TryCreateStageConstantInstruction(expression, out var instruction))
        {
            return StageArgumentPlan.FromInstruction(instruction);
        }

        return StageArgumentPlan.FromSlot(EmitSourceExpression(expression, context, state));
    }

    private void EmitStageArgument(StageArgumentPlan argument)
    {
        if (argument.HasInstruction)
        {
            Emit(argument.Instruction);
            return;
        }

        Emit(CreateInstruction(GameEventScriptBytecodeOpCode.StageRegister, a: argument.Slot));
    }

    private bool TryCreateStageConstantInstruction(ExpressionNode expression, out GameEventScriptBytecodeInstruction instruction)
    {
        switch (expression)
        {
            case BooleanLiteralExpressionNode boolean:
                instruction = new GameEventScriptBytecodeInstruction { OpCode = boolean.Value
                    ? GameEventScriptBytecodeOpCode.StageTrue
                    : GameEventScriptBytecodeOpCode.StageFalse };
                return true;

            case IntegerLiteralExpressionNode integer:
                instruction = new GameEventScriptBytecodeInstruction {
                    OpCode = GameEventScriptBytecodeOpCode.StageInteger, 
                    I64 = integer.Value
                };
                return true;

            case UnitIntegerLiteralExpressionNode unitInteger:
                instruction = new GameEventScriptBytecodeInstruction
                {
                    OpCode = GameEventScriptBytecodeOpCode.StageInteger,
                    UnitAndFlags = GameEventScriptNumericUnits.TryParseTypeName(unitInteger.UnitName, out var integerUnit) ? EncodeNumericUnitAndFlags(integerUnit) : (byte)GameEventScriptBytecodeInstructionUnit.UnitNone,
                    I64 = unitInteger.Value
                };
                if (!GameEventScriptNumericUnits.TryParseTypeName(unitInteger.UnitName, out _))
                {
                    instruction = new GameEventScriptBytecodeInstruction { 
                        OpCode = GameEventScriptBytecodeOpCode.StageFloat,
                        F64 = double.NaN
                    };
                }

                return true;

            case FloatLiteralExpressionNode floatLiteral:
                instruction = new GameEventScriptBytecodeInstruction { 
                    OpCode = GameEventScriptBytecodeOpCode.StageFloat,
                    F64 = floatLiteral.Value
                };
                return true;

            case PercentageLiteralExpressionNode percentage:
                instruction = new GameEventScriptBytecodeInstruction { OpCode = GameEventScriptBytecodeOpCode.StagePercentage,
                    F64 = percentage.PercentValue / 100d
                };
                return true;

            case UnitFloatLiteralExpressionNode unitFloat:
                if (GameEventScriptNumericUnits.TryParseTypeName(unitFloat.UnitName, out var unit))
                {
                    instruction = new GameEventScriptBytecodeInstruction { 
                        OpCode = GameEventScriptBytecodeOpCode.StageFloat,
                        UnitAndFlags = EncodeNumericUnitAndFlags(unit),
                        F64 = unitFloat.Value
                    };
                }
                else
                {
                    instruction = new GameEventScriptBytecodeInstruction { OpCode = GameEventScriptBytecodeOpCode.StageFloat,
                        F64 = double.NaN
                    };
                }

                return true;

            case TextLiteralExpressionNode text:
                instruction = CreateInstruction(GameEventScriptBytecodeOpCode.StageText, a: ResolveStringIndex(text.Value));
                return true;

            case TagLiteralExpressionNode tag:
                instruction = CreateInstruction(GameEventScriptBytecodeOpCode.StageTag, a: ResolveStringIndex(tag.Name));
                return true;

            default:
                instruction = default;
                return false;
        }
    }

    private int EmitSourceMessage(MessageLiteralExpressionNode message, SourceContext context, ExpressionState state)
    {
        var argumentNames = new string[message.Arguments.Count];
        var argumentSlots = new int[message.Arguments.Count];
        for (var argumentIndex = 0; argumentIndex < message.Arguments.Count; argumentIndex++)
        {
            var argument = message.Arguments[argumentIndex];
            argumentNames[argumentIndex] = argument.Name;
            argumentSlots[argumentIndex] = EmitSourceExpression(argument.Expression, context, state);
        }

        var messageShapeIndex = ResolveMessageShapeIndex(message.Message, argumentNames);
        var argumentSlotListIndex = ResolveSlotListIndex(argumentSlots);
        return EmitValueInstruction(
            state,
            GameEventScriptBytecodeOpCode.BuildMessage,
            a: messageShapeIndex,
            b: argumentSlotListIndex);
    }

    private int EmitSourceExtensionCall(ExtensionCallExpressionNode extensionCall, SourceContext context, ExpressionState state)
    {
        var argumentNames = new string[extensionCall.Arguments.Count];
        var argumentSlots = new int[extensionCall.Arguments.Count];
        for (var argumentIndex = 0; argumentIndex < extensionCall.Arguments.Count; argumentIndex++)
        {
            var argument = extensionCall.Arguments[argumentIndex];
            argumentNames[argumentIndex] = argument.Name;
            argumentSlots[argumentIndex] = EmitSourceExpression(argument.Expression, context, state);
        }

        var reference = new GameEventScriptExtensionReference(
            extensionCall.ExtensionName,
            extensionCall.FunctionName,
            argumentNames);
        var argumentSlotListIndex = ResolveSlotListIndex(argumentSlots);
        if (GesStandardExtensions.IsStandardReference(reference))
        {
            return EmitValueInstruction(
                state,
                GameEventScriptBytecodeOpCode.CallStandard,
                a: ResolveExtensionShapeIndex(reference),
                b: argumentSlotListIndex);
        }

        var referenceIndex = _resolveExternalReferenceIndex(reference);
        if (referenceIndex < 0)
        {
            throw new GameEventScriptCompileException($"GameEventScript bytecode lowerer could not resolve extension reference '{reference.SignatureId}'.");
        }

        return EmitValueInstruction(
            state,
            GameEventScriptBytecodeOpCode.CallExternal,
            a: referenceIndex,
            b: argumentSlotListIndex);
    }

    private int EmitSourceCollectionBuilder(
        GameEventScriptBytecodeOpCode opCode,
        IReadOnlyList<ExpressionNode> items,
        string[]? names,
        SourceContext context,
        ExpressionState state)
    {
        var itemSlots = new int[items.Count];
        for (var index = 0; index < items.Count; index++)
        {
            itemSlots[index] = EmitSourceExpression(items[index], context, state);
        }

        var itemSlotListIndex = ResolveSlotListIndex(itemSlots);
        return EmitValueInstruction(state, opCode, a: itemSlotListIndex);
    }

    private int EmitSourceDictionary(MapLiteralExpressionNode dictionary, SourceContext context, ExpressionState state)
    {
        var names = new string[dictionary.Entries.Count];
        var valueSlots = new int[dictionary.Entries.Count];
        for (var entryIndex = 0; entryIndex < dictionary.Entries.Count; entryIndex++)
        {
            var entry = dictionary.Entries[entryIndex];
            names[entryIndex] = entry.Key;
            valueSlots[entryIndex] = EmitSourceExpression(entry.Value, context, state);
        }

        var nameListIndex = ResolveStringListIndex(names);
        var valueSlotListIndex = ResolveSlotListIndex(valueSlots);
        return EmitValueInstruction(
            state,
            GameEventScriptBytecodeOpCode.BuildMap,
            a: nameListIndex,
            b: valueSlotListIndex);
    }

    private int EmitSourceVariadic(VariadicTaggedExpressionNode variadic, SourceContext context, ExpressionState state)
    {
        if (variadic.Arguments.Count == 0)
        {
            return EmitValueInstruction(state, GameEventScriptBytecodeOpCode.LoadNothing);
        }

        var opCode = variadic.Operator switch
        {
            "min" => GameEventScriptBytecodeOpCode.Min,
            "max" => GameEventScriptBytecodeOpCode.Max,
            _ => throw new GameEventScriptCompileException($"GameEventScript bytecode lowerer does not support variadic operator '{variadic.Operator}'.")
        };

        var current = EmitSourceExpression(variadic.Arguments[0], context, state);
        for (var index = 1; index < variadic.Arguments.Count; index++)
        {
            var next = EmitSourceExpression(variadic.Arguments[index], context, state);
            current = EmitValueInstruction(state, opCode, current, next);
        }

        return current;
    }

    private int EmitSourceRange(RangeExpressionNode range, SourceContext context, ExpressionState state)
    {
        var from = EmitSourceExpression(range.FromExpression, context, state);
        var to = EmitSourceExpression(range.ToExpression, context, state);
        var step = range.StepExpression is null ? -1 : EmitSourceExpression(range.StepExpression, context, state);
        return step >= 0
            ? EmitValueInstruction(state, GameEventScriptBytecodeOpCode.RangeWithStep, from, to, step)
            : EmitValueInstruction(state, GameEventScriptBytecodeOpCode.Range, from, to);
    }

    private int EmitSourceSeededRandomExpression(SeededRandomExpressionNode seededRandom, SourceContext context, ExpressionState state)
    {
        EmitRandomPush(seededRandom.SeedExpression, context, state);
        var result = EmitSourceExpression(seededRandom.BodyExpression, context, state);
        Emit(new GameEventScriptBytecodeInstruction { OpCode = GameEventScriptBytecodeOpCode.RandomPop });
        return result;
    }

    private void EmitRandomPush(ExpressionNode seedExpression, SourceContext context, ExpressionState state)
    {
        if (TryReadUnitlessIntegerLiteralSeed(seedExpression, out var seed))
        {
            var instruction = new GameEventScriptBytecodeInstruction
            {
                OpCode = GameEventScriptBytecodeOpCode.RandomPushConstant,
                U64 = seed
            };
            Emit(instruction);
            return;
        }

        var seedSlot = EmitSourceExpression(seedExpression, context, state);
        Emit(CreateInstruction(GameEventScriptBytecodeOpCode.RandomPush, a: seedSlot));
    }

    private static bool TryReadUnitlessIntegerLiteralSeed(ExpressionNode expression, out ulong seed)
    {
        if (expression is IntegerLiteralExpressionNode integer)
        {
            seed = unchecked((ulong)integer.Value);
            return true;
        }

        seed = default;
        return false;
    }

    private int EmitSourceGuardedChoice(GuardedChoiceExpressionNode guardedChoice, SourceContext context, ExpressionState state)
    {
        var resultSlot = AllocateSlot(state);
        var endJumps = new List<int>(guardedChoice.Branches.Count);

        foreach (var branch in guardedChoice.Branches)
        {
            var condition = EmitSourceExpression(branch.ConditionExpression, context, state);
            var jumpToNextBranch = Emit(CreateJumpInstruction(GameEventScriptBytecodeOpCode.JumpIfNotTrue, conditionSlot: condition));

            var value = EmitSourceExpression(branch.ValueExpression, context, state);
            Emit(CreateInstruction(GameEventScriptBytecodeOpCode.MoveSlot, dest: resultSlot, a: value));
            endJumps.Add(Emit(CreateJumpInstruction(GameEventScriptBytecodeOpCode.Jump)));

            PatchTarget(jumpToNextBranch, _code.Count);
        }

        var otherwiseValue = EmitSourceExpression(guardedChoice.OtherwiseExpression, context, state);
        Emit(CreateInstruction(GameEventScriptBytecodeOpCode.MoveSlot, dest: resultSlot, a: otherwiseValue));

        var endAddress = _code.Count;
        foreach (var jump in endJumps)
        {
            PatchTarget(jump, endAddress);
        }

        return resultSlot;
    }

    private int EmitSourceBinary(BinaryExpressionNode binary, SourceContext context, ExpressionState state)
    {
        if (TryEmitSourceShortCircuitBinary(binary, context, state, out var shortCircuitSlot))
        {
            return shortCircuitSlot;
        }

        var left = EmitSourceExpression(binary.Left, context, state);
        var right = EmitSourceExpression(binary.Right, context, state);
        return EmitValueInstruction(state, ToBinaryOpCode(binary), left, right);
    }

    private bool TryEmitSourceShortCircuitBinary(
        BinaryExpressionNode binary,
        SourceContext context,
        ExpressionState state,
        out int resultSlot)
    {
        if (binary.Operator is not (GesBinaryOperator.Or or GesBinaryOperator.And or GesBinaryOperator.Implies))
        {
            resultSlot = -1;
            return false;
        }

        var left = EmitSourceExpression(binary.Left, context, state);
        resultSlot = AllocateSlot(state);
        if (binary.Operator == GesBinaryOperator.Implies)
        {
            Emit(CreateInstruction(GameEventScriptBytecodeOpCode.Implies, dest: resultSlot, a: left, b: left));
        }
        else
        {
            Emit(CreateInstruction(GameEventScriptBytecodeOpCode.MoveSlot, dest: resultSlot, a: left));
        }

        var branch = binary.Operator switch
        {
            GesBinaryOperator.Or => GameEventScriptBytecodeOpCode.JumpIfTrue,
            GesBinaryOperator.And => GameEventScriptBytecodeOpCode.JumpIfFalse,
            _ => GameEventScriptBytecodeOpCode.JumpIfFalse
        };
        var jump = Emit(CreateJumpInstruction(branch, conditionSlot: left));
        var right = EmitSourceExpression(binary.Right, context, state);
        var combineOp = binary.Operator switch
        {
            GesBinaryOperator.Or => GameEventScriptBytecodeOpCode.Or,
            GesBinaryOperator.And => GameEventScriptBytecodeOpCode.And,
            _ => GameEventScriptBytecodeOpCode.Implies
        };
        Emit(CreateInstruction(combineOp, dest: resultSlot, a: left, b: right));
        PatchTarget(jump, _code.Count);
        return true;
    }

    private int EmitSourcePredicateCall(PredicateCallExpressionNode predicateCall, SourceContext context, ExpressionState state)
    {
        var destinationSlot = AllocateSlot(state);
        if (!TryEmitSourcePredicateCallToSlot(predicateCall, destinationSlot, context, state, out _))
        {
            throw new GameEventScriptCompileException($"GameEventScript bytecode lowerer does not support predicate test '{predicateCall.PredicateName}'.");
        }

        return destinationSlot;
    }

    private bool TryEmitSourcePredicateCallToSlot(
        PredicateCallExpressionNode predicateCall,
        int destinationSlot,
        SourceContext context,
        ExpressionState state,
        out int instructionAddress)
    {
        if (!_sourceCallables.TryGetValue(predicateCall.PredicateName, out var callable) ||
            callable.Parameters.Count != 1)
        {
            instructionAddress = -1;
            return false;
        }

        var argument = PrepareStageArgument(predicateCall.Value, context, state);
        EmitStageArgument(argument);
        instructionAddress = Emit(CreateInstruction(
            GameEventScriptBytecodeOpCode.CallPredicate,
            dest: destinationSlot));
        _deferredCallableAddressPatches.Add((instructionAddress, callable.Name));
        return true;
    }

    private int EmitSourceExtensionPredicate(ExtensionPredicateExpressionNode extensionPredicate, SourceContext context, ExpressionState state)
    {
        var input = EmitSourceExpression(extensionPredicate.Value, context, state);
        var labels = new[] { GameEventScriptMessageSignature.UnlabeledParameterName };
        var reference = new GameEventScriptExtensionReference(
            extensionPredicate.ExtensionName,
            extensionPredicate.FunctionName,
            labels);
        var argumentSlotListIndex = ResolveSlotListIndex([input]);
        if (GesStandardExtensions.IsStandardReference(reference))
        {
            return EmitValueInstruction(
                state,
                GameEventScriptBytecodeOpCode.CallStandardPredicate,
                a: ResolveExtensionShapeIndex(reference),
                b: argumentSlotListIndex);
        }

        var referenceIndex = _resolveExternalReferenceIndex(reference);
        if (referenceIndex < 0)
        {
            throw new GameEventScriptCompileException($"GameEventScript bytecode lowerer could not resolve extension reference '{reference.SignatureId}'.");
        }

        return EmitValueInstruction(
            state,
            GameEventScriptBytecodeOpCode.CallExternalPredicate,
            a: referenceIndex,
            b: argumentSlotListIndex);
    }

    private int EmitSourceCall(CallExpressionNode call, SourceContext context, ExpressionState state)
    {
        if (!_sourceCallables.TryGetValue(call.Name, out var called))
        {
            var handlerSlot = context.RequireSlot(call.Name);
            var argumentNames = new string[call.ArgumentList.Count];
            var operandSlots = new int[call.ArgumentList.Count + 1];
            operandSlots[0] = EmitValueInstruction(state, GameEventScriptBytecodeOpCode.MoveSlot, a: handlerSlot);
            for (var argumentIndex = 0; argumentIndex < call.ArgumentList.Count; argumentIndex++)
            {
                var argument = call.ArgumentList.Arguments[argumentIndex];
                argumentNames[argumentIndex] = argument.Name;
                operandSlots[argumentIndex + 1] = EmitSourceExpression(argument.Expression, context, state);
            }

            var operandSlotListIndex = ResolveSlotListIndex(operandSlots);
            var argumentNameListIndex = ResolveStringListIndex(argumentNames);
            return EmitValueInstruction(
                state,
                GameEventScriptBytecodeOpCode.BindHandler,
                a: operandSlotListIndex,
                b: argumentNameListIndex);
        }

        var destinationSlot = AllocateSlot(state);
        if (!TryEmitSourceCallToSlot(call, destinationSlot, context, state, out _))
        {
            throw new GameEventScriptCompileException($"GameEventScript bytecode lowerer does not support callable '{call.Name}'.");
        }

        return destinationSlot;
    }

    private bool TryEmitSourceCallToSlot(
        CallExpressionNode call,
        int destinationSlot,
        SourceContext context,
        ExpressionState state,
        out int instructionAddress)
    {
        instructionAddress = -1;
        if (!_sourceCallables.TryGetValue(call.Name, out var called))
        {
            return false;
        }

        var arguments = new StageArgumentPlan[call.Arguments.Count];
        for (var argumentIndex = 0; argumentIndex < call.Arguments.Count; argumentIndex++)
        {
            arguments[argumentIndex] = PrepareStageArgument(call.Arguments[argumentIndex], context, state);
        }

        for (var argumentIndex = 0; argumentIndex < arguments.Length; argumentIndex++)
        {
            EmitStageArgument(arguments[argumentIndex]);
        }

        var opCode = called.Kind == GameEventScriptCallableKind.PredicateCall
            ? GameEventScriptBytecodeOpCode.CallPredicate
            : GameEventScriptBytecodeOpCode.Call;
        instructionAddress = Emit(CreateInstruction(
            opCode,
            dest: destinationSlot));
        _deferredCallableAddressPatches.Add((instructionAddress, called.Name));
        return true;
    }

    private int EmitSourceTypeCast(TypeCastExpressionNode typeCast, SourceContext context, ExpressionState state)
    {
        if (!IsBuiltInCastType(typeCast.TypeName))
        {
            throw new GameEventScriptCompileException($"GameEventScript bytecode lowerer does not support type cast '{typeCast.TypeName}'.");
        }

        var value = EmitSourceExpression(typeCast.Value, context, state);
        var operand = ResolveDeclaredTypeOperand(typeCast.TypeName);
        return EmitValueInstruction(state, operand.CastOpCode, a: value, b: operand.TypeKind, c: operand.CustomTypeNameIndex, unitAndFlags: operand.UnitAndFlags);
    }

    private int EmitSourceTypeConstructor(TypeConstructorExpressionNode typeConstructor, SourceContext context, ExpressionState state)
    {
        if (typeConstructor.Arguments.Count == 1 &&
            typeConstructor.Arguments[0].Label is null &&
            IsBuiltInCastType(typeConstructor.TypeName))
        {
            var value = EmitSourceExpression(typeConstructor.Arguments[0].Expression, context, state);
            var operand = ResolveDeclaredTypeOperand(typeConstructor.TypeName);
            return EmitValueInstruction(state, operand.CastOpCode, a: value, b: operand.TypeKind, c: operand.CustomTypeNameIndex, unitAndFlags: operand.UnitAndFlags);
        }

        var argumentNames = new string[typeConstructor.Arguments.Count];
        var argumentSlots = new int[typeConstructor.Arguments.Count];
        for (var argumentIndex = 0; argumentIndex < typeConstructor.Arguments.Count; argumentIndex++)
        {
            var argument = typeConstructor.Arguments[argumentIndex];
            argumentNames[argumentIndex] = argument.Name;
            argumentSlots[argumentIndex] = EmitSourceExpression(argument.Expression, context, state);
        }

        var typeNameIndex = ResolveStringIndex(typeConstructor.TypeName);
        var argumentNameListIndex = ResolveStringListIndex(argumentNames);
        var argumentSlotListIndex = ResolveSlotListIndex(argumentSlots);
        return EmitValueInstruction(
            state,
            GameEventScriptBytecodeOpCode.TypeConstructor,
            a: typeNameIndex,
            b: argumentNameListIndex,
            c: argumentSlotListIndex);
    }

    private int EmitSourcePipeline(CollectionAccessExpressionNode collectionAccess, SourceContext context, ExpressionState state)
    {
        var selectors = new List<CollectionSelectorNode>();
        ExpressionNode source = collectionAccess;
        while (source is CollectionAccessExpressionNode access)
        {
            selectors.Add(access.Selector);
            source = access.Target;
        }

        selectors.Reverse();
        if (selectors.Count == 0)
        {
            return EmitSourceExpression(source, context, state);
        }

        var sourceSlot = EmitSourceExpression(source, context, state);
        var terminal = selectors[^1];
        var prefixCount = selectors.Count - 1;

        if (prefixCount == 0)
        {
            switch (terminal)
            {
                case SeriesTermSelectorNode term:
                {
                    var indexSlot = EmitSourceExpression(term.IndexExpression, context, state);
                    return EmitValueInstruction(state, GameEventScriptBytecodeOpCode.SeriesTerm, a: sourceSlot, b: indexSlot);
                }

                case SequenceSliceSelectorNode { Scope: "first" } slice when
                    string.Equals(slice.Operation, "take", StringComparison.Ordinal) ||
                    string.Equals(slice.Operation, "drop", StringComparison.Ordinal):
                {
                    return EmitValueInstruction(
                        state,
                        string.Equals(slice.Operation, "take", StringComparison.Ordinal)
                            ? GameEventScriptBytecodeOpCode.SeriesTake
                            : GameEventScriptBytecodeOpCode.SeriesDrop,
                        a: sourceSlot,
                        b: slice.Count);
                }
            }
        }

        var iteratorSlot = AllocateSlot(state);
        Emit(CreateInstruction(GameEventScriptBytecodeOpCode.CollectionIterator, dest: iteratorSlot, a: sourceSlot));
        for (var index = 0; index < prefixCount; index++)
        {
            iteratorSlot = EmitPipelinePrefixIterator(iteratorSlot, selectors[index], context, state);
        }

        return EmitPipelineTerminal(iteratorSlot, terminal, context, state);
    }

    private int EmitPipelinePrefixIterator(
        int sourceIteratorSlot,
        CollectionSelectorNode selector,
        SourceContext context,
        ExpressionState state)
        => selector switch
        {
            FilterSelectorNode filter => EmitPipelineIterator(
                sourceIteratorSlot,
                filter.Identifier,
                filter.Predicate,
                PipelineIteratorEntryKind.Filter,
                context,
                state),
            SelectSelectorNode select => EmitPipelineIterator(
                sourceIteratorSlot,
                select.Identifier,
                select.Projection,
                PipelineIteratorEntryKind.Select,
                context,
                state),
            _ => throw new GameEventScriptCompileException($"GameEventScript bytecode lowerer does not support non-terminal selector node '{selector.GetType().Name}'.")
        };

    private int EmitPipelineIterator(
        int sourceIteratorSlot,
        string itemIdentifier,
        ExpressionNode expression,
        PipelineIteratorEntryKind kind,
        SourceContext context,
        ExpressionState state)
    {
        var iteratorSlot = AllocateSlot(state);
        var captures = ResolvePipelineCaptures(expression, itemIdentifier, context);
        var captureSlotListIndex = ResolveSlotListIndex(captures.Select(capture => capture.SourceSlot).ToArray());
        var instructionAddress = Emit(CreateInstruction(
            GameEventScriptBytecodeOpCode.PipelineIterator,
            dest: iteratorSlot,
            a: sourceIteratorSlot,
            c: 0,
            d: captureSlotListIndex));
        _deferredHelperEmitters.Add(() =>
        {
            var helperContext = CreatePipelineHelperContext(itemIdentifier, captures);
            var helperState = new ExpressionState(helperContext.SlotCount);
            var entryAddress = kind == PipelineIteratorEntryKind.Filter
                ? EmitPipelineFilterEntry(0, expression, helperContext, helperState)
                : EmitSourceExpressionEntry(expression, helperContext, helperState);
            PatchB(instructionAddress, entryAddress);
        });
        return iteratorSlot;
    }

    private int EmitPipelineFilterEntry(int itemSlot, ExpressionNode predicate, SourceContext context, ExpressionState state)
    {
        var entry = _code.Count;
        var reserveSlotsAddress = EmitReserveSlots(0);
        var predicateSlot = EmitSourceExpression(predicate, context, state);
        var skipYield = Emit(CreateJumpInstruction(GameEventScriptBytecodeOpCode.JumpIfNotTrue, conditionSlot: predicateSlot));
        EmitReturnValue(itemSlot);
        PatchTarget(skipYield, _code.Count);
        EmitReturnVoid();
        _maxFrameSlots = Math.Max(_maxFrameSlots, state.NextSlot);
        _currentFrameSlotCount = Math.Max(_currentFrameSlotCount, state.NextSlot);
        PatchReserveSlots(reserveSlotsAddress, state.NextSlot - context.SlotCount);
        return entry;
    }

    private int EmitPipelineTerminal(
        int iteratorSlot,
        CollectionSelectorNode terminal,
        SourceContext context,
        ExpressionState state)
    {
        switch (terminal)
        {
            case SelectSelectorNode select:
                return EmitPipelineCollect(
                    EmitPipelineIterator(iteratorSlot, select.Identifier, select.Projection, PipelineIteratorEntryKind.Select, context, state),
                    state);

            case FilterSelectorNode filter:
                return EmitPipelineCollect(
                    EmitPipelineIterator(iteratorSlot, filter.Identifier, filter.Predicate, PipelineIteratorEntryKind.Filter, context, state),
                    state);

            case PredicateSelectorNode predicate:
            {
                var predicateIterator = EmitPipelineIterator(
                    iteratorSlot,
                    predicate.Identifier,
                    predicate.Predicate,
                    PipelineIteratorEntryKind.Select,
                    context,
                    state);
                return EmitValueInstruction(
                    state,
                    string.Equals(predicate.Operator, "all", StringComparison.Ordinal)
                        ? GameEventScriptBytecodeOpCode.PipelineHasAll
                        : GameEventScriptBytecodeOpCode.PipelineHasAny,
                    a: predicateIterator);
            }

            case EdgeSelectorNode edge:
            {
                if (edge.Predicate is not null && !string.IsNullOrEmpty(edge.Identifier))
                {
                    iteratorSlot = EmitPipelineIterator(
                        iteratorSlot,
                        edge.Identifier!,
                        edge.Predicate,
                        PipelineIteratorEntryKind.Filter,
                        context,
                        state);
                }

                return EmitValueInstruction(
                    state,
                    edge.Mode switch
                    {
                        "last" => GameEventScriptBytecodeOpCode.PipelineLast,
                        "single" => GameEventScriptBytecodeOpCode.PipelineSingle,
                        _ => GameEventScriptBytecodeOpCode.PipelineFirst
                    },
                    a: iteratorSlot);
            }

            case CountSelectorNode count:
            {
                var filteredIterator = EmitPipelineIterator(
                    iteratorSlot,
                    count.Identifier,
                    count.Predicate,
                    PipelineIteratorEntryKind.Filter,
                    context,
                    state);
                return EmitPipelineCount(filteredIterator, state);
            }

            case SumSelectorNode sum:
            {
                var projectedIterator = EmitPipelineIterator(
                    iteratorSlot,
                    sum.Identifier,
                    sum.Projection,
                    PipelineIteratorEntryKind.Select,
                    context,
                    state);
                return EmitPipelineSum(projectedIterator, state);
            }

            case AverageSelectorNode average:
            {
                var projectedIterator = EmitPipelineIterator(
                    iteratorSlot,
                    average.Identifier,
                    average.Projection,
                    PipelineIteratorEntryKind.Select,
                    context,
                    state);
                return EmitPipelineAverage(projectedIterator, state);
            }

            case MinSelectorNode min:
                return EmitPipelineExtrema(iteratorSlot, context.RequireSlot(min.Identifier), min.Projection, isMax: false, context, state);

            case MaxSelectorNode max:
                return EmitPipelineExtrema(iteratorSlot, context.RequireSlot(max.Identifier), max.Projection, isMax: true, context, state);

            case MapSelectorNode dictionary:
                return EmitPipelineMap(
                    iteratorSlot,
                    context.RequireSlot(dictionary.Identifier),
                    dictionary.KeyProjection,
                    dictionary.ValueProjection,
                    context,
                    state);

            case ContainsSelectorNode contains:
            {
                var needleSlot = EmitSourceExpression(contains.ValueExpression, context, state);
                return EmitValueInstruction(
                    state,
                    contains.Mode switch
                    {
                        "all" => GameEventScriptBytecodeOpCode.PipelineContainsAll,
                        "any" => GameEventScriptBytecodeOpCode.PipelineContainsAny,
                        _ => GameEventScriptBytecodeOpCode.PipelineContainsSingle
                    },
                    a: iteratorSlot,
                    b: needleSlot);
            }

            case DistinctSelectorNode distinct:
                return EmitPipelineDistinct(iteratorSlot, distinct, context, state);

            case GroupBySelectorNode groupBy:
                return EmitPipelineEntryTerminal(
                    GameEventScriptBytecodeOpCode.PipelineGroupBy,
                    iteratorSlot,
                    context.RequireSlot(groupBy.Identifier),
                    groupBy.Projection,
                    context,
                    state);

            case OrderBySelectorNode orderBy:
                return EmitPipelineEntryTerminal(
                    string.Equals(orderBy.Direction, "descending", StringComparison.Ordinal)
                        ? GameEventScriptBytecodeOpCode.PipelineOrderByDescending
                        : GameEventScriptBytecodeOpCode.PipelineOrderByAscending,
                    iteratorSlot,
                    context.RequireSlot(orderBy.Identifier),
                    orderBy.Projection,
                    context,
                    state);

            case SortSelectorNode sort:
                return EmitValueInstruction(
                    state,
                    string.Equals(sort.Direction, "descending", StringComparison.Ordinal)
                        ? GameEventScriptBytecodeOpCode.PipelineSortDescending
                        : GameEventScriptBytecodeOpCode.PipelineSortAscending,
                    a: iteratorSlot);

            case ReverseSelectorNode:
                return EmitValueInstruction(state, GameEventScriptBytecodeOpCode.PipelineReverse, a: iteratorSlot);

            case SequenceSliceSelectorNode slice:
                return EmitPipelineSequenceSlice(iteratorSlot, slice, state);

            case ShuffleSelectorNode:
                return EmitValueInstruction(state, GameEventScriptBytecodeOpCode.PipelineShuffle, a: iteratorSlot);

            case DrawSelectorNode draw:
                return EmitValueInstruction(state, GameEventScriptBytecodeOpCode.PipelineDraw, a: iteratorSlot, b: draw.Count);

            case ChooseSelectorNode choose:
                return EmitPipelineChoose(iteratorSlot, choose, context, state);

            case PatternSelectorNode pattern:
                return EmitPipelineDicePattern(iteratorSlot, pattern.Pattern, take: false, context, state);

            case TakePatternSelectorNode takePattern:
                return EmitPipelineDicePattern(iteratorSlot, takePattern.Pattern, take: true, context, state);

            case ObjectMatchSelectorNode objectMatch:
                return EmitPipelineObjectMatch(iteratorSlot, objectMatch.Pattern, context, state);

            default:
                throw new GameEventScriptCompileException($"GameEventScript bytecode lowerer does not support terminal selector node '{terminal.GetType().Name}'.");
        }
    }

    private int EmitPipelineCollect(int iteratorSlot, ExpressionState state)
        => EmitValueInstruction(
            state,
            GameEventScriptBytecodeOpCode.PipelineCollectList,
            a: iteratorSlot);

    private int EmitPipelineEntryTerminal(
        GameEventScriptBytecodeOpCode opCode,
        int iteratorSlot,
        int itemSlot,
        ExpressionNode expression,
        SourceContext context,
        ExpressionState state)
    {
        var resultSlot = AllocateSlot(state);
        var instructionAddress = Emit(CreateInstruction(opCode, dest: resultSlot, a: iteratorSlot, b: itemSlot));
        var helperBaseSlot = state.NextSlot;
        _deferredHelperEmitters.Add(() =>
        {
            var helperState = CreateHelperState(helperBaseSlot, iteratorSlot, resultSlot, itemSlot);
            var entryAddress = EmitSourceExpressionEntry(expression, context, helperState, helperState.BaseSlot);
            PatchC(instructionAddress, entryAddress);
        });
        return resultSlot;
    }

    private int EmitPipelineMap(
        int iteratorSlot,
        int itemSlot,
        ExpressionNode keyProjection,
        ExpressionNode? valueProjection,
        SourceContext context,
        ExpressionState state)
    {
        var resultSlot = AllocateSlot(state);
        var instructionAddress = Emit(CreateInstruction(
            valueProjection is null ? GameEventScriptBytecodeOpCode.PipelineMap : GameEventScriptBytecodeOpCode.PipelineMapValue,
            dest: resultSlot,
            a: iteratorSlot,
            b: itemSlot));
        var helperBaseSlot = state.NextSlot;
        _deferredHelperEmitters.Add(() =>
        {
            var keyEntryAddress = EmitSourceExpressionEntry(
                keyProjection,
                context,
                CreateHelperState(helperBaseSlot, iteratorSlot, resultSlot, itemSlot),
                helperBaseSlot);
            PatchC(instructionAddress, keyEntryAddress);
            if (valueProjection is not null)
            {
                var valueEntryAddress = EmitSourceExpressionEntry(
                    valueProjection,
                    context,
                    CreateHelperState(helperBaseSlot, iteratorSlot, resultSlot, itemSlot),
                    helperBaseSlot);
                PatchD(instructionAddress, valueEntryAddress);
            }
        });
        return resultSlot;
    }

    private int EmitPipelineDistinct(
        int iteratorSlot,
        DistinctSelectorNode distinct,
        SourceContext context,
        ExpressionState state)
    {
        if (distinct.Projection is null || string.IsNullOrEmpty(distinct.Identifier))
        {
            return EmitValueInstruction(state, GameEventScriptBytecodeOpCode.PipelineDistinct, a: iteratorSlot);
        }

        return EmitPipelineEntryTerminal(
            GameEventScriptBytecodeOpCode.PipelineDistinctBy,
            iteratorSlot,
            context.RequireSlot(distinct.Identifier!),
            distinct.Projection,
            context,
            state);
    }

    private int EmitPipelineSequenceSlice(int iteratorSlot, SequenceSliceSelectorNode slice, ExpressionState state)
    {
        var opCode = (slice.Operation, slice.Scope) switch
        {
            ("take", "last") => GameEventScriptBytecodeOpCode.PipelineTakeLast,
            ("take", "highest") => GameEventScriptBytecodeOpCode.PipelineTakeHighest,
            ("take", "lowest") => GameEventScriptBytecodeOpCode.PipelineTakeLowest,
            ("drop", "first") => GameEventScriptBytecodeOpCode.PipelineDropFirst,
            ("drop", "last") => GameEventScriptBytecodeOpCode.PipelineDropLast,
            ("drop", "highest") => GameEventScriptBytecodeOpCode.PipelineDropHighest,
            ("drop", "lowest") => GameEventScriptBytecodeOpCode.PipelineDropLowest,
            _ => GameEventScriptBytecodeOpCode.PipelineTakeFirst
        };
        return EmitValueInstruction(state, opCode, a: iteratorSlot, b: slice.Count);
    }

    private int EmitPipelineChoose(
        int iteratorSlot,
        ChooseSelectorNode choose,
        SourceContext context,
        ExpressionState state)
    {
        if (choose.Predicate is not null && !string.IsNullOrEmpty(choose.Identifier))
        {
            iteratorSlot = EmitPipelineIterator(
                iteratorSlot,
                choose.Identifier!,
                choose.Predicate,
                PipelineIteratorEntryKind.Filter,
                context,
                state);
        }

        if (choose.WeightExpression is null || string.IsNullOrEmpty(choose.WeightIdentifier))
        {
            return EmitValueInstruction(
                state,
                choose.AtRandom ? GameEventScriptBytecodeOpCode.PipelineChooseRandom : GameEventScriptBytecodeOpCode.PipelineChoose,
                a: iteratorSlot,
                b: choose.Count);
        }

        var resultSlot = AllocateSlot(state);
        var itemSlot = context.RequireSlot(choose.WeightIdentifier!);
        var instructionAddress = Emit(CreateInstruction(
            GameEventScriptBytecodeOpCode.PipelineChooseWeighted,
            dest: resultSlot,
            a: iteratorSlot,
            b: choose.Count,
            c: itemSlot));
        var helperBaseSlot = state.NextSlot;
        _deferredHelperEmitters.Add(() =>
        {
            var helperState = CreateHelperState(helperBaseSlot, iteratorSlot, resultSlot, itemSlot);
            var entryAddress = EmitSourceExpressionEntry(choose.WeightExpression, context, helperState, helperState.BaseSlot);
            PatchD(instructionAddress, entryAddress);
        });
        return resultSlot;
    }

    private int EmitPipelineCount(int iteratorSlot, ExpressionState state)
    {
        var seedSlot = EmitLoadInteger(state, 0L, unitAndFlags: 0);
        var itemSlot = AllocateSlot(state);
        var resultSlot = AllocateSlot(state);
        var instructionAddress = Emit(CreateInstruction(
            GameEventScriptBytecodeOpCode.IteratorFold,
            dest: resultSlot,
            a: iteratorSlot,
            b: seedSlot,
            c: itemSlot));
        var helperBaseSlot = state.NextSlot;
        _deferredHelperEmitters.Add(() =>
        {
            var helperState = CreateHelperState(helperBaseSlot, iteratorSlot, seedSlot, itemSlot, resultSlot);
            var entryAddress = EmitPipelineCountReducerEntry(resultSlot, helperState);
            PatchD(instructionAddress, entryAddress);
        });
        return resultSlot;
    }

    private int EmitPipelineSum(int iteratorSlot, ExpressionState state)
    {
        var defaultSlot = EmitLoadFloat(state, 0d, unitAndFlags: 0);
        var itemSlot = AllocateSlot(state);
        var resultSlot = AllocateSlot(state);
        var instructionAddress = Emit(CreateInstruction(
            GameEventScriptBytecodeOpCode.IteratorReduceOrDefault,
            dest: resultSlot,
            a: iteratorSlot,
            b: defaultSlot,
            c: itemSlot));
        var helperBaseSlot = state.NextSlot;
        _deferredHelperEmitters.Add(() =>
        {
            var helperState = CreateHelperState(helperBaseSlot, iteratorSlot, defaultSlot, itemSlot, resultSlot);
            var entryAddress = EmitPipelineAddReducerEntry(resultSlot, itemSlot, helperState);
            PatchD(instructionAddress, entryAddress);
        });
        return resultSlot;
    }

    private int EmitPipelineAverage(int iteratorSlot, ExpressionState state)
    {
        var zeroSlot = EmitLoadFloat(state, 0d, unitAndFlags: 0);
        var countSlot = EmitLoadInteger(state, 0L, unitAndFlags: 0);
        var itemSlot = AllocateSlot(state);
        var resultSlot = AllocateSlot(state);
        var instructionAddress = Emit(CreateInstruction(
            GameEventScriptBytecodeOpCode.IteratorFold,
            dest: resultSlot,
            a: iteratorSlot,
            b: zeroSlot,
            c: itemSlot));
        var helperBaseSlot = state.NextSlot;
        _deferredHelperEmitters.Add(() =>
        {
            var helperState = CreateHelperState(helperBaseSlot, iteratorSlot, zeroSlot, countSlot, itemSlot, resultSlot);
            var entryAddress = EmitPipelineAverageReducerEntry(resultSlot, itemSlot, countSlot, helperState);
            PatchD(instructionAddress, entryAddress);
        });

        var hasNoItems = EmitValueInstruction(state, GameEventScriptBytecodeOpCode.Equal, countSlot, zeroSlot);
        var jumpToDivide = Emit(CreateJumpInstruction(GameEventScriptBytecodeOpCode.JumpIfFalse, conditionSlot: hasNoItems));
        Emit(CreateInstruction(GameEventScriptBytecodeOpCode.LoadNothing, dest: resultSlot));
        var jumpToEnd = Emit(CreateJumpInstruction(GameEventScriptBytecodeOpCode.Jump));
        PatchTarget(jumpToDivide, _code.Count);
        Emit(CreateInstruction(GameEventScriptBytecodeOpCode.Divide, dest: resultSlot, a: resultSlot, b: countSlot));
        PatchTarget(jumpToEnd, _code.Count);
        return resultSlot;
    }

    private int EmitPipelineExtrema(
        int iteratorSlot,
        int identifierSlot,
        ExpressionNode projection,
        bool isMax,
        SourceContext context,
        ExpressionState state)
    {
        var itemSlot = identifierSlot;
        var resultSlot = AllocateSlot(state);
        var instructionAddress = Emit(CreateInstruction(
            GameEventScriptBytecodeOpCode.IteratorReduce,
            dest: resultSlot,
            a: iteratorSlot,
            b: itemSlot));
        var helperBaseSlot = state.NextSlot;
        _deferredHelperEmitters.Add(() =>
        {
            var helperState = CreateHelperState(helperBaseSlot, iteratorSlot, itemSlot, resultSlot, identifierSlot);
            var entryAddress = EmitPipelineExtremaReducerEntry(resultSlot, itemSlot, identifierSlot, projection, isMax, context, helperState);
            PatchC(instructionAddress, entryAddress);
        });
        return resultSlot;
    }

    private int EmitPipelineAddReducerEntry(int accumulatorSlot, int itemSlot, ExpressionState state)
    {
        var entry = _code.Count;
        var reserveSlotsAddress = EmitReserveSlots(0);
        var sumSlot = EmitValueInstruction(state, GameEventScriptBytecodeOpCode.Add, accumulatorSlot, itemSlot);
        EmitReturnValue(sumSlot);
        PatchReserveSlots(reserveSlotsAddress, state.NextSlot - state.BaseSlot);
        return entry;
    }

    private int EmitPipelineCountReducerEntry(int accumulatorSlot, ExpressionState state)
    {
        var entry = _code.Count;
        var reserveSlotsAddress = EmitReserveSlots(0);
        var oneSlot = EmitLoadInteger(state, 1L, unitAndFlags: 0);
        var nextCountSlot = EmitValueInstruction(state, GameEventScriptBytecodeOpCode.IntAdd, accumulatorSlot, oneSlot);
        EmitReturnValue(nextCountSlot);
        PatchReserveSlots(reserveSlotsAddress, state.NextSlot - state.BaseSlot);
        return entry;
    }

    private int EmitPipelineAverageReducerEntry(int accumulatorSlot, int itemSlot, int countSlot, ExpressionState state)
    {
        var entry = _code.Count;
        var reserveSlotsAddress = EmitReserveSlots(0);
        var oneSlot = EmitLoadInteger(state, 1L, unitAndFlags: 0);
        var nextCountSlot = EmitValueInstruction(state, GameEventScriptBytecodeOpCode.IntAdd, countSlot, oneSlot);
        Emit(CreateInstruction(GameEventScriptBytecodeOpCode.MoveSlot, dest: countSlot, a: nextCountSlot));
        var sumSlot = EmitValueInstruction(state, GameEventScriptBytecodeOpCode.Add, accumulatorSlot, itemSlot);
        EmitReturnValue(sumSlot);
        PatchReserveSlots(reserveSlotsAddress, state.NextSlot - state.BaseSlot);
        return entry;
    }

    private int EmitPipelineExtremaReducerEntry(
        int accumulatorSlot,
        int itemSlot,
        int identifierSlot,
        ExpressionNode projection,
        bool isMax,
        SourceContext context,
        ExpressionState state)
    {
        var entry = _code.Count;
        var reserveSlotsAddress = EmitReserveSlots(0);
        var currentItemSlot = AllocateSlot(state);
        Emit(CreateInstruction(GameEventScriptBytecodeOpCode.MoveSlot, dest: currentItemSlot, a: itemSlot));
        Emit(CreateInstruction(GameEventScriptBytecodeOpCode.MoveSlot, dest: identifierSlot, a: accumulatorSlot));
        var accumulatorProjectionSlot = EmitSourceExpression(projection, context, state);
        if (accumulatorProjectionSlot == identifierSlot)
        {
            var projectedAccumulatorSlot = AllocateSlot(state);
            Emit(CreateInstruction(GameEventScriptBytecodeOpCode.MoveSlot, dest: projectedAccumulatorSlot, a: accumulatorProjectionSlot));
            accumulatorProjectionSlot = projectedAccumulatorSlot;
        }

        Emit(CreateInstruction(GameEventScriptBytecodeOpCode.MoveSlot, dest: identifierSlot, a: currentItemSlot));
        var currentProjectionSlot = EmitSourceExpression(projection, context, state);
        var comparisonSlot = EmitValueInstruction(
            state,
            isMax ? GameEventScriptBytecodeOpCode.Greater : GameEventScriptBytecodeOpCode.Less,
            currentProjectionSlot,
            accumulatorProjectionSlot);
        var keepAccumulatorJump = Emit(CreateJumpInstruction(GameEventScriptBytecodeOpCode.JumpIfNotTrue, conditionSlot: comparisonSlot));
        EmitReturnValue(currentItemSlot);
        PatchTarget(keepAccumulatorJump, _code.Count);
        EmitReturnValue(accumulatorSlot);
        PatchReserveSlots(reserveSlotsAddress, state.NextSlot - state.BaseSlot);
        return entry;
    }

    private int EmitPipelineDicePattern(
        int iteratorSlot,
        DicePatternNode pattern,
        bool take,
        SourceContext context,
        ExpressionState state)
    {
        var resultSlot = AllocateSlot(state);
        switch (pattern)
        {
            case DiceCountPatternNode count:
            {
                var opCode = take
                    ? count.Face is null ? GameEventScriptBytecodeOpCode.PipelineTakePatternCountAny : GameEventScriptBytecodeOpCode.PipelineTakePatternCountFace
                    : count.Face is null ? GameEventScriptBytecodeOpCode.PipelineDicePatternCountAny : GameEventScriptBytecodeOpCode.PipelineDicePatternCountFace;
                var instructionAddress = Emit(CreateInstruction(opCode, dest: resultSlot, a: iteratorSlot, b: count.Count));
                if (count.Face is not null)
                {
                    var helperBaseSlot = state.NextSlot;
                    _deferredHelperEmitters.Add(() =>
                    {
                        var helperState = CreateHelperState(helperBaseSlot, iteratorSlot, resultSlot);
                        var entryAddress = EmitSourceExpressionEntry(count.Face, context, helperState, helperState.BaseSlot);
                        PatchC(instructionAddress, entryAddress);
                    });
                }

                return resultSlot;
            }

            case DiceFullHousePatternNode:
                Emit(CreateInstruction(
                    take ? GameEventScriptBytecodeOpCode.PipelineTakePatternFullHouse : GameEventScriptBytecodeOpCode.PipelineDicePatternFullHouse,
                    dest: resultSlot,
                    a: iteratorSlot));
                return resultSlot;

            case DiceStraightPatternNode:
                Emit(CreateInstruction(
                    take ? GameEventScriptBytecodeOpCode.PipelineTakePatternStraight : GameEventScriptBytecodeOpCode.PipelineDicePatternStraight,
                    dest: resultSlot,
                    a: iteratorSlot));
                return resultSlot;

            default:
                throw new GameEventScriptCompileException($"GameEventScript bytecode lowerer does not support dice pattern '{pattern.GetType().Name}'.");
        }
    }

    private int EmitPipelineObjectMatch(
        int iteratorSlot,
        ObjectMatchPatternNode pattern,
        SourceContext context,
        ExpressionState state)
    {
        var matchIterator = AllocateSlot(state);
        var captures = ResolvePipelineCaptures(pattern, context);
        var captureSlotListIndex = ResolveSlotListIndex(captures.Select(capture => capture.SourceSlot).ToArray());
        var instructionAddress = Emit(CreateInstruction(
            GameEventScriptBytecodeOpCode.PipelineIterator,
            dest: matchIterator,
            a: iteratorSlot,
            c: 0,
            d: captureSlotListIndex));
        _deferredHelperEmitters.Add(() =>
        {
            var helperContext = CreatePipelineHelperContext(captures);
            var helperState = new ExpressionState(helperContext.SlotCount);
            var entryAddress = EmitPipelineObjectMatchEntry(0, pattern, helperContext, helperState);
            PatchB(instructionAddress, entryAddress);
        });
        return EmitValueInstruction(state, GameEventScriptBytecodeOpCode.PipelineHasAny, a: matchIterator);
    }

    private int EmitPipelineObjectMatchEntry(int itemSlot, ObjectMatchPatternNode pattern, SourceContext context, ExpressionState state)
    {
        var entry = _code.Count;
        var reserveSlotsAddress = EmitReserveSlots(0);
        var matchSlot = EmitObjectPatternPredicate(itemSlot, pattern, context, state);
        var skipYield = Emit(CreateJumpInstruction(GameEventScriptBytecodeOpCode.JumpIfNotTrue, conditionSlot: matchSlot));
        EmitReturnValue(matchSlot);
        PatchTarget(skipYield, _code.Count);
        EmitReturnVoid();
        PatchReserveSlots(reserveSlotsAddress, state.NextSlot - context.SlotCount);
        return entry;
    }

    private int EmitObjectPatternPredicate(int targetSlot, ObjectMatchPatternNode pattern, SourceContext context, ExpressionState state)
    {
        var resultSlot = EmitValueInstruction(state, GameEventScriptBytecodeOpCode.LoadTrue);
        foreach (var entry in pattern.Entries)
        {
            var memberSlot = EmitValueInstruction(state, GameEventScriptBytecodeOpCode.MemberAccess, a: targetSlot, c: ResolveStringIndex(entry.Key));
            int entryMatchSlot;
            switch (entry.Value)
            {
                case ObjectMatchExpressionValueNode expression:
                {
                    var expectedSlot = EmitSourceExpression(expression.Expression, context, state);
                    entryMatchSlot = EmitValueInstruction(state, GameEventScriptBytecodeOpCode.Equal, memberSlot, expectedSlot);
                    break;
                }

                case ObjectMatchNestedValueNode nested:
                    entryMatchSlot = EmitObjectPatternPredicate(memberSlot, nested.Pattern, context, state);
                    break;

                default:
                    throw new GameEventScriptCompileException($"GameEventScript bytecode lowerer does not support object match value '{entry.Value.GetType().Name}'.");
            }

            resultSlot = EmitValueInstruction(state, GameEventScriptBytecodeOpCode.And, resultSlot, entryMatchSlot);
        }

        return resultSlot;
    }

    private int EmitSourceIterator(IterationSourceNode source, SourceContext context, ExpressionState state)
    {
        switch (source)
        {
            case RangeIterationSourceNode range when TryGetRangeIteratorShort(range.RangeExpression, out var from, out var to, out var step):
            {
                var iteratorSlot = AllocateSlot(state);
                var instruction = new GameEventScriptBytecodeInstruction { 
                    OpCode = GameEventScriptBytecodeOpCode.RangeIteratorShort,
                    DestinationSlot = ToUShortOperand(iteratorSlot, "iterator slot"),
                    X_I16 = from,
                    Y_I16 = to,
                    A_I16 = step
                };
                Emit(instruction);
                return iteratorSlot;
            }

            case RangeIterationSourceNode range:
            {
                var fromSlot = EmitSourceExpression(range.RangeExpression.FromExpression, context, state);
                var toSlot = EmitSourceExpression(range.RangeExpression.ToExpression, context, state);
                var stepSlot = range.RangeExpression.StepExpression is null
                    ? -1
                    : EmitSourceExpression(range.RangeExpression.StepExpression, context, state);
                var iteratorSlot = AllocateSlot(state);
                Emit(stepSlot >= 0
                    ? CreateInstruction(GameEventScriptBytecodeOpCode.RangeIteratorWithStep, dest: iteratorSlot, a: fromSlot, b: toSlot, c: stepSlot)
                    : CreateInstruction(GameEventScriptBytecodeOpCode.RangeIterator, dest: iteratorSlot, a: fromSlot, b: toSlot));
                return iteratorSlot;
            }

            case CollectionIterationSourceNode collection:
            {
                var collectionSlot = EmitSourceExpression(collection.Expression, context, state);
                var iteratorSlot = AllocateSlot(state);
                Emit(CreateInstruction(GameEventScriptBytecodeOpCode.CollectionIterator, dest: iteratorSlot, a: collectionSlot));
                return iteratorSlot;
            }

            default:
                throw new GameEventScriptCompileException($"GameEventScript bytecode lowerer does not support iteration source '{source.GetType().Name}'.");
        }
    }

    private static bool TryGetRangeIteratorShort(RangeExpressionNode range, out short from, out short to, out short step)
    {
        step = 0;
        if (TryGetShortIntegerLiteral(range.FromExpression, out from) &&
            TryGetShortIntegerLiteral(range.ToExpression, out to) &&
            (range.StepExpression is null || TryGetShortIntegerLiteral(range.StepExpression, out step)))
        {
            if (range.StepExpression is null)
            {
                step = 1;
            }

            return true;
        }

        from = 0;
        to = 0;
        return false;
    }

    private static bool TryGetShortIntegerLiteral(ExpressionNode expression, out short value)
    {
        if (expression is IntegerLiteralExpressionNode integer &&
            integer.Value >= short.MinValue &&
            integer.Value <= short.MaxValue)
        {
            value = (short)integer.Value;
            return true;
        }

        value = 0;
        return false;
    }

    private void AddDebugDiagnosticSite(
        GameEventScriptBytecodeDiagnosticKind kind,
        GameEventScriptBytecodeDiagnosticTiming timing,
        int address,
        int slot,
        string name)
    {
        if (!_emitDebugInfo)
        {
            return;
        }

        _debugDiagnosticSites.Add(new GameEventScriptBytecodeDebugDiagnosticSite(kind, timing, address, slot, name));
    }

    private void FlushDeferredHelpers()
    {
        for (var index = 0; index < _deferredHelperEmitters.Count; index++)
        {
            _deferredHelperEmitters[index]();
        }

        _deferredHelperEmitters.Clear();
    }

    private int EmitValueInstruction(
        ExpressionState state,
        GameEventScriptBytecodeOpCode opCode,
        int a = 0,
        int b = 0,
        int c = 0,
        byte unitAndFlags = 0)
    {
        var dest = AllocateSlot(state);
        Emit(CreateInstruction(opCode, dest: dest, a: a, b: b, c: c, unitAndFlags: unitAndFlags));
        return dest;
    }

    private int EmitCastSlot(int destinationSlot, int sourceSlot, string? typeName)
    {
        var operand = ResolveDeclaredTypeOperand(typeName);
        return Emit(CreateInstruction(operand.CastOpCode, dest: destinationSlot, a: sourceSlot, b: operand.TypeKind, c: operand.CustomTypeNameIndex, unitAndFlags: operand.UnitAndFlags));
    }

    private int AllocateSlot(ExpressionState state)
    {
        var slot = state.Allocate();
        MarkAllocatedSlotCount(state.NextSlot);
        return slot;
    }

    private void MarkAllocatedSlotCount(int slotCount)
    {
        _maxFrameSlots = Math.Max(_maxFrameSlots, slotCount);
        if (_scopeBuildStates.Count == 0)
        {
            _currentFrameSlotCount = Math.Max(_currentFrameSlotCount, slotCount);
        }

        if (_scopeBuildStates.Count > 0)
        {
            _scopeBuildStates.Peek().MarkSlotCount(slotCount);
        }
    }

    private int Emit(GameEventScriptBytecodeInstruction instruction)
    {
        var address = _code.Count;
        _code.Add(instruction);
        return address;
    }

    private void PatchTarget(int address, int target)
        => _code[address] = _code[address] with { TargetAddress = ToUShortOperand(target, "target address") };

    private void PatchA(int address, int value)
        => _code[address] = _code[address] with { XSlot = ToUShortOperand(value, "operand") };

    private void PatchTargets(int address, int target, int target2)
        => _code[address] = _code[address] with
        {
            TargetAddress = ToUShortOperand(target, "target address"),
            B_U16 = ToUShortOperand(target2, "target address")
        };

    private void PatchB(int address, int value)
        => _code[address] = _code[address] with { YSlot = ToUShortOperand(value, "operand") };

    private void PatchC(int address, int value)
        => _code[address] = _code[address] with { A_U16 = ToUShortOperand(value, "operand") };

    private void PatchD(int address, int value)
        => _code[address] = _code[address] with { B_U16 = ToUShortOperand(value, "operand") };

    private int ResolveStringIndex(string? value)
        => value is null
            ? -1
            : _resolveStringIndex(value);

    private int ResolveStringListIndex(IReadOnlyList<string> values)
    {
        if (values.Count == 0)
        {
            return _resolveUShortListIndex([]);
        }

        var indexes = new ushort[values.Count];
        for (var index = 0; index < values.Count; index++)
        {
            indexes[index] = ToUShortOperand(ResolveStringIndex(values[index]), "string pool index");
        }

        return _resolveUShortListIndex(indexes);
    }

    private int ResolveSlotListIndex(IReadOnlyList<int> slots)
    {
        if (slots.Count == 0)
        {
            return _resolveUShortListIndex([]);
        }

        return ResolveRequiredSlotListIndex(slots);
    }

    private int ResolveRequiredSlotListIndex(IReadOnlyList<int> slots)
    {
        var indexes = new ushort[slots.Count];
        for (var index = 0; index < slots.Count; index++)
        {
            indexes[index] = ToUShortOperand(slots[index], "slot index");
        }

        return _resolveUShortListIndex(indexes);
    }

    private int ResolveMessageShapeIndex(string messageName, IReadOnlyList<string> argumentNames)
    {
        var shape = new ushort[argumentNames.Count + 1];
        shape[0] = ToUShortOperand(ResolveStringIndex(GameEventScriptMessageSignature.NormalizeMessageName(messageName)), "message name string pool index");
        for (var index = 0; index < argumentNames.Count; index++)
        {
            shape[index + 1] = ToUShortOperand(ResolveStringIndex(argumentNames[index]), "message argument string pool index");
        }

        return _resolveUShortListIndex(shape);
    }

    private int ResolveExtensionShapeIndex(GameEventScriptExtensionReference reference)
    {
        var shape = new ushort[reference.ArgumentLabels.Count + 2];
        shape[0] = ToUShortOperand(ResolveStringIndex(reference.ExtensionName), "extension name string pool index");
        shape[1] = ToUShortOperand(ResolveStringIndex(reference.FunctionName), "extension function string pool index");
        for (var index = 0; index < reference.ArgumentLabels.Count; index++)
        {
            shape[index + 2] = ToUShortOperand(ResolveStringIndex(reference.ArgumentLabels[index]), "extension argument string pool index");
        }

        return _resolveUShortListIndex(shape);
    }

    private static ushort ToUShortOperand(int value, string name)
    {
        if (value < 0 || value > ushort.MaxValue)
        {
            throw new GameEventScriptCompileException($"GameEventScript bytecode {name} must fit into an unsigned 16-bit operand.");
        }

        return (ushort)value;
    }

    private static GameEventScriptBytecodeInstruction CreateInstruction(
        GameEventScriptBytecodeOpCode opCode,
        int dest = 0,
        int a = 0,
        int b = 0,
        int c = 0,
        int d = 0,
        byte unitAndFlags = 0)
        => new GameEventScriptBytecodeInstruction { 
            OpCode = opCode,
            UnitAndFlags = unitAndFlags,
            DestinationSlot = ToUShortOperand(dest, "destination operand"),
            XSlot = ToUShortOperand(a, "X operand"),
            YSlot = ToUShortOperand(b, "Y operand"),
            A_U16 = ToUShortOperand(c, "C operand"),
            B_U16 = ToUShortOperand(d, "D operand")
        };

    private static GameEventScriptBytecodeInstruction CreateJumpInstruction(
        GameEventScriptBytecodeOpCode opCode,
        int conditionSlot = 0,
        int targetAddress = 0)
        => new()
        {
            OpCode = opCode,
            ConditionSlot = ToUShortOperand(conditionSlot, "condition slot"),
            TargetAddress = ToUShortOperand(targetAddress, "target address")
        };

    private static string FormatSignature(string name, IReadOnlyList<string> labels)
        => GameEventScriptMessageSignature.CreateSignatureId(name, labels);

    private int EmitLoadInteger(
        ExpressionState state,
        long value,
        byte unitAndFlags)
    {
        var dest = AllocateSlot(state);
        EmitLoadIntegerToSlot(dest, value, unitAndFlags);
        return dest;
    }

    private int EmitLoadIntegerToSlot(
        int destinationSlot,
        long value,
        byte unitAndFlags)
    {
        Emit(new GameEventScriptBytecodeInstruction { 
            OpCode = GameEventScriptBytecodeOpCode.LoadInteger,
            UnitAndFlags = unitAndFlags,
            DestinationSlot = ToUShortOperand(destinationSlot, "destination operand"),
            I64 = value
        });
        return _code.Count - 1;
    }

    private int EmitLoadFloat(
        ExpressionState state,
        double value,
        byte unitAndFlags)
    {
        var dest = AllocateSlot(state);
        EmitLoadFloatToSlot(dest, value, unitAndFlags);
        return dest;
    }

    private int EmitLoadFloatToSlot(
        int destinationSlot,
        double value,
        byte unitAndFlags)
    {
        Emit(new GameEventScriptBytecodeInstruction { 
            OpCode = GameEventScriptBytecodeOpCode.LoadFloat,
            DestinationSlot = ToUShortOperand(destinationSlot, "destination operand"),
            UnitAndFlags = unitAndFlags,
            F64 = value
        });
        return _code.Count - 1;
    }

    private int EmitLoadPercentage(
        ExpressionState state,
        double ratio)
    {
        var dest = AllocateSlot(state);
        EmitLoadPercentageToSlot(dest, ratio);
        return dest;
    }

    private int EmitLoadPercentageToSlot(
        int destinationSlot,
        double ratio)
    {
        Emit(new GameEventScriptBytecodeInstruction  { 
            OpCode = GameEventScriptBytecodeOpCode.LoadPercentage,
            DestinationSlot = ToUShortOperand(destinationSlot, "destination operand"),
            F64 = ratio
        });
        return _code.Count - 1;
    }

    private static double EncodeFloatPayload(GameEventScriptFloatValue value)
    {
        if (value.IsNaNValue)
        {
            return double.NaN;
        }

        if (value.IsInfinityValue)
        {
            return value.IsNegativeInfinityValue
                ? double.NegativeInfinity
                : double.PositiveInfinity;
        }

        return value.Value;
    }

    private static byte EncodeNumericUnitAndFlags(GameEventScriptNumericUnit? unit)
        => unit switch
        {
            null => (byte)GameEventScriptBytecodeInstructionUnit.UnitNone,
            GameEventScriptNumericUnit.Degree => (byte)GameEventScriptBytecodeInstructionUnit.UnitDegree,
            GameEventScriptNumericUnit.Meter => (byte)GameEventScriptBytecodeInstructionUnit.UnitMeter,
            GameEventScriptNumericUnit.Second => (byte)GameEventScriptBytecodeInstructionUnit.UnitSecond,
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unknown GameEventScript numeric unit.")
        };

    private (GameEventScriptBytecodeOpCode CastOpCode, GameEventScriptBytecodeOpCode TypeCheckOpCode, ushort TypeKind, ushort CustomTypeNameIndex, byte UnitAndFlags) ResolveDeclaredTypeOperand(string? typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            throw new GameEventScriptCompileException("GameEventScript bytecode lowerer requires a declared type name operand.");
        }

        if (TryGetQuantityUnit(typeName, out var unit))
        {
            return (
                GameEventScriptBytecodeOpCode.CastUnit,
                GameEventScriptBytecodeOpCode.CheckUnit,
                0,
                0,
                EncodeNumericUnitAndFlags(unit));
        }

        if (TryGetBytecodeTypeKind(typeName, out var typeKind))
        {
            return (
                GameEventScriptBytecodeOpCode.Cast,
                GameEventScriptBytecodeOpCode.TypeCheck,
                (ushort)typeKind,
                0,
                0);
        }

        return (
            GameEventScriptBytecodeOpCode.Cast,
            GameEventScriptBytecodeOpCode.TypeCheck,
            (ushort)GameEventScriptBytecodeTypeKind.Custom,
            ToUShortOperand(ResolveStringIndex(typeName), "custom type name string-pool operand"),
            0);
    }

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
            GameEventScriptBytecodeOpCode.IntEqual or
            GameEventScriptBytecodeOpCode.IntNotEqual or
            GameEventScriptBytecodeOpCode.IntLess or
            GameEventScriptBytecodeOpCode.IntGreater or
            GameEventScriptBytecodeOpCode.IntLessOrEqual or
            GameEventScriptBytecodeOpCode.IntGreaterOrEqual or
            GameEventScriptBytecodeOpCode.IntAdd or
            GameEventScriptBytecodeOpCode.IntSubtract or
            GameEventScriptBytecodeOpCode.IntMultiply or
            GameEventScriptBytecodeOpCode.IntDivide or
            GameEventScriptBytecodeOpCode.IntFloorDivide or
            GameEventScriptBytecodeOpCode.IntModulo or
            GameEventScriptBytecodeOpCode.IntRemainder or
            GameEventScriptBytecodeOpCode.Default or
            GameEventScriptBytecodeOpCode.Contains or
            GameEventScriptBytecodeOpCode.ContainsValue or
            GameEventScriptBytecodeOpCode.StartsWith or
            GameEventScriptBytecodeOpCode.EndsWith or
            GameEventScriptBytecodeOpCode.Intersect or
            GameEventScriptBytecodeOpCode.Combine or
            GameEventScriptBytecodeOpCode.Except or
            GameEventScriptBytecodeOpCode.Zip or
            GameEventScriptBytecodeOpCode.Min or
            GameEventScriptBytecodeOpCode.Max;

    private static GameEventScriptBytecodeOpCode ToUnaryOpCode(GesUnaryOperator operation)
        => operation switch
        {
            GesUnaryOperator.Negate => GameEventScriptBytecodeOpCode.UnaryNegate,
            GesUnaryOperator.Not => GameEventScriptBytecodeOpCode.UnaryNot,
            GesUnaryOperator.HasValue => GameEventScriptBytecodeOpCode.UnaryHasValue,
            GesUnaryOperator.Empty => GameEventScriptBytecodeOpCode.UnaryEmpty,
            GesUnaryOperator.Length => GameEventScriptBytecodeOpCode.UnaryLength,
            GesUnaryOperator.Chance => GameEventScriptBytecodeOpCode.UnaryChance,
            GesUnaryOperator.Keys => GameEventScriptBytecodeOpCode.UnaryKeys,
            GesUnaryOperator.Values => GameEventScriptBytecodeOpCode.UnaryValues,
            GesUnaryOperator.Entries => GameEventScriptBytecodeOpCode.UnaryEntries,
            GesUnaryOperator.Abs => GameEventScriptBytecodeOpCode.UnaryAbs,
            GesUnaryOperator.NaturalLog => GameEventScriptBytecodeOpCode.UnaryNaturalLog,
            _ => throw new GameEventScriptCompileException($"GameEventScript bytecode lowerer does not support unary operator '{operation.ToSourceText()}'.")
        };

    private static bool IsBuiltInCastType(string typeName)
    {
        return TryGetQuantityUnit(typeName, out _) ||
               TryGetBytecodeTypeKind(typeName, out _);
    }

    private static bool TryGetBytecodeTypeKind(string typeName, out GameEventScriptBytecodeTypeKind typeKind)
    {
        typeKind = typeName switch
        {
            "nothing" => GameEventScriptBytecodeTypeKind.Nothing,
            "boolean" => GameEventScriptBytecodeTypeKind.Boolean,
            "integer" => GameEventScriptBytecodeTypeKind.Integer,
            "float" => GameEventScriptBytecodeTypeKind.Float,
            "number" => GameEventScriptBytecodeTypeKind.Number,
            "percentage" => GameEventScriptBytecodeTypeKind.Percentage,
            "vector" => GameEventScriptBytecodeTypeKind.Vector,
            "point" => GameEventScriptBytecodeTypeKind.Point,
            "uuid" => GameEventScriptBytecodeTypeKind.Uuid,
            "series" => GameEventScriptBytecodeTypeKind.Series,
            "envelope" => GameEventScriptBytecodeTypeKind.Envelope,
            "ref" => GameEventScriptBytecodeTypeKind.Ref,
            "tag" => GameEventScriptBytecodeTypeKind.Tag,
            "text" => GameEventScriptBytecodeTypeKind.Text,
            "list" => GameEventScriptBytecodeTypeKind.List,
            "range" => GameEventScriptBytecodeTypeKind.Range,
            "message" => GameEventScriptBytecodeTypeKind.Message,
            "handler" => GameEventScriptBytecodeTypeKind.Handler,
            "map" => GameEventScriptBytecodeTypeKind.Map,
            "dice" => GameEventScriptBytecodeTypeKind.Dice,
            _ => GameEventScriptBytecodeTypeKind.Invalid
        };

        return typeKind != GameEventScriptBytecodeTypeKind.Invalid;
    }

    private static bool TryGetQuantityUnit(string typeName, out GameEventScriptNumericUnit unit)
    {
        if (GameEventScriptNumericUnits.TryParseQuantityTypeName(typeName, out unit))
        {
            return true;
        }

        return GameEventScriptNumericUnits.TryParseTypeName(typeName, out unit);
    }

    private static GameEventScriptBytecodeOpCode ToBinaryOpCode(BinaryExpressionNode expression)
        => ShouldPreferPrimitiveIntegerOp(expression)
            ? ToPrimitiveIntegerOpCode(expression.Operator)
            : ToBinaryOpCode(expression.Operator);

    private static bool ShouldPreferPrimitiveIntegerOp(BinaryExpressionNode expression)
        => expression.Operator switch
        {
            GesBinaryOperator.IntegerDivide or
                GesBinaryOperator.Modulo or
                GesBinaryOperator.Remainder => IsIntegerCandidate(expression.Left) || IsIntegerCandidate(expression.Right),
            GesBinaryOperator.Equal or
                GesBinaryOperator.NotEqual or
                GesBinaryOperator.Less or
                GesBinaryOperator.Greater or
                GesBinaryOperator.LessOrEqual or
                GesBinaryOperator.GreaterOrEqual => IsIntegerCandidate(expression.Left) && IsIntegerCandidate(expression.Right),
            GesBinaryOperator.Add or
                GesBinaryOperator.Subtract or
                GesBinaryOperator.Multiply or
                GesBinaryOperator.Divide => IsIntegerCandidate(expression.Left) &&
                                            IsIntegerCandidate(expression.Right) &&
                                            !HasExplicitNonIntegerNumeric(expression.Left) &&
                                            !HasExplicitNonIntegerNumeric(expression.Right),
            _ => false
        };

    private static bool IsIntegerCandidate(ExpressionNode expression)
        => expression switch
        {
            IntegerLiteralExpressionNode => true,
            UnitIntegerLiteralExpressionNode => true,
            IdentifierExpressionNode => true,
            BinaryExpressionNode binary => ShouldPreferPrimitiveIntegerOp(binary),
            TypeCastExpressionNode { TypeName: "integer" } => true,
            TypeConstructorExpressionNode { TypeName: "integer" } => true,
            _ => false
        };

    private static bool HasExplicitNonIntegerNumeric(ExpressionNode expression)
        => expression switch
        {
            FloatLiteralExpressionNode => true,
            PercentageLiteralExpressionNode => true,
            UnitFloatLiteralExpressionNode => true,
            BinaryExpressionNode binary => HasExplicitNonIntegerNumeric(binary.Left) || HasExplicitNonIntegerNumeric(binary.Right),
            TypeCastExpressionNode { TypeName: "float" or "number" or "percentage" or "degree" or "meter" or "second" } => true,
            TypeConstructorExpressionNode { TypeName: "float" or "number" or "percentage" or "degree" or "meter" or "second" } => true,
            _ => false
        };

    private static GameEventScriptBytecodeOpCode ToPrimitiveIntegerOpCode(GesBinaryOperator operation)
        => operation switch
        {
            GesBinaryOperator.Equal => GameEventScriptBytecodeOpCode.IntEqual,
            GesBinaryOperator.NotEqual => GameEventScriptBytecodeOpCode.IntNotEqual,
            GesBinaryOperator.Less => GameEventScriptBytecodeOpCode.IntLess,
            GesBinaryOperator.Greater => GameEventScriptBytecodeOpCode.IntGreater,
            GesBinaryOperator.LessOrEqual => GameEventScriptBytecodeOpCode.IntLessOrEqual,
            GesBinaryOperator.GreaterOrEqual => GameEventScriptBytecodeOpCode.IntGreaterOrEqual,
            GesBinaryOperator.Add => GameEventScriptBytecodeOpCode.IntAdd,
            GesBinaryOperator.Subtract => GameEventScriptBytecodeOpCode.IntSubtract,
            GesBinaryOperator.Multiply => GameEventScriptBytecodeOpCode.IntMultiply,
            GesBinaryOperator.Divide => GameEventScriptBytecodeOpCode.IntDivide,
            GesBinaryOperator.IntegerDivide => GameEventScriptBytecodeOpCode.IntFloorDivide,
            GesBinaryOperator.Modulo => GameEventScriptBytecodeOpCode.IntModulo,
            GesBinaryOperator.Remainder => GameEventScriptBytecodeOpCode.IntRemainder,
            _ => ToBinaryOpCode(operation)
        };

    private static GameEventScriptBytecodeOpCode ToBinaryOpCode(GesBinaryOperator operation)
        => operation switch
        {
            GesBinaryOperator.Or => GameEventScriptBytecodeOpCode.Or,
            GesBinaryOperator.Xor => GameEventScriptBytecodeOpCode.Xor,
            GesBinaryOperator.And => GameEventScriptBytecodeOpCode.And,
            GesBinaryOperator.Implies => GameEventScriptBytecodeOpCode.Implies,
            GesBinaryOperator.Equal => GameEventScriptBytecodeOpCode.Equal,
            GesBinaryOperator.NotEqual => GameEventScriptBytecodeOpCode.NotEqual,
            GesBinaryOperator.ApproxEqual => GameEventScriptBytecodeOpCode.ApproxEqual,
            GesBinaryOperator.Less => GameEventScriptBytecodeOpCode.Less,
            GesBinaryOperator.Greater => GameEventScriptBytecodeOpCode.Greater,
            GesBinaryOperator.LessOrEqual => GameEventScriptBytecodeOpCode.LessOrEqual,
            GesBinaryOperator.GreaterOrEqual => GameEventScriptBytecodeOpCode.GreaterOrEqual,
            GesBinaryOperator.Add => GameEventScriptBytecodeOpCode.Add,
            GesBinaryOperator.Subtract => GameEventScriptBytecodeOpCode.Subtract,
            GesBinaryOperator.Multiply => GameEventScriptBytecodeOpCode.Multiply,
            GesBinaryOperator.Divide => GameEventScriptBytecodeOpCode.Divide,
            GesBinaryOperator.IntegerDivide => GameEventScriptBytecodeOpCode.IntegerDivide,
            GesBinaryOperator.Modulo => GameEventScriptBytecodeOpCode.Modulo,
            GesBinaryOperator.Remainder => GameEventScriptBytecodeOpCode.Remainder,
            GesBinaryOperator.Power => GameEventScriptBytecodeOpCode.Power,
            GesBinaryOperator.Default => GameEventScriptBytecodeOpCode.Default,
            GesBinaryOperator.Contains => GameEventScriptBytecodeOpCode.Contains,
            GesBinaryOperator.ContainsValue => GameEventScriptBytecodeOpCode.ContainsValue,
            GesBinaryOperator.StartsWith => GameEventScriptBytecodeOpCode.StartsWith,
            GesBinaryOperator.EndsWith => GameEventScriptBytecodeOpCode.EndsWith,
            GesBinaryOperator.Intersect => GameEventScriptBytecodeOpCode.Intersect,
            GesBinaryOperator.Combine or GesBinaryOperator.Merge => GameEventScriptBytecodeOpCode.Combine,
            GesBinaryOperator.Except => GameEventScriptBytecodeOpCode.Except,
            GesBinaryOperator.Zip => GameEventScriptBytecodeOpCode.Zip,
            _ => throw new GameEventScriptCompileException($"GameEventScript bytecode lowerer does not support binary operator '{operation.ToSourceText()}'.")
        };

    private enum PipelineIteratorEntryKind
    {
        Filter,
        Select
    }

    private IReadOnlyList<PipelineCapture> ResolvePipelineCaptures(
        ExpressionNode expression,
        string itemIdentifier,
        SourceContext context)
    {
        var identifiers = new List<string>();
        CollectReferencedIdentifiers(expression, identifiers, new HashSet<string>(StringComparer.Ordinal));
        return ResolvePipelineCaptures(identifiers, itemIdentifier, context);
    }

    private IReadOnlyList<PipelineCapture> ResolvePipelineCaptures(
        ObjectMatchPatternNode pattern,
        SourceContext context)
    {
        var identifiers = new List<string>();
        CollectReferencedIdentifiers(pattern, identifiers, new HashSet<string>(StringComparer.Ordinal));
        return ResolvePipelineCaptures(identifiers, itemIdentifier: null, context);
    }

    private IReadOnlyList<PipelineCapture> ResolvePipelineCaptures(
        IReadOnlyList<string> identifiers,
        string? itemIdentifier,
        SourceContext context)
    {
        var captures = new List<PipelineCapture>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < identifiers.Count; index++)
        {
            var identifier = identifiers[index];
            if ((itemIdentifier is not null && string.Equals(identifier, itemIdentifier, StringComparison.Ordinal)) ||
                !seen.Add(identifier))
            {
                continue;
            }

            captures.Add(new PipelineCapture(
                identifier,
                context.RequireSlot(identifier),
                captures.Count + 1));
        }

        return captures;
    }

    private static SourceContext CreatePipelineHelperContext(
        string itemIdentifier,
        IReadOnlyList<PipelineCapture> captures)
    {
        var slots = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [itemIdentifier] = 0
        };
        for (var index = 0; index < captures.Count; index++)
        {
            slots[captures[index].Name] = captures[index].HelperSlot;
        }

        return new SourceContext(slots);
    }

    private static SourceContext CreatePipelineHelperContext(IReadOnlyList<PipelineCapture> captures)
    {
        var slots = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < captures.Count; index++)
        {
            slots[captures[index].Name] = captures[index].HelperSlot;
        }

        return new SourceContext(slots, temporaryBaseSlot: 1 + captures.Count);
    }

    private static ExpressionState CreateHelperState(int baseSlot, params int[] referencedSlots)
    {
        var nextSlot = Math.Max(0, baseSlot);
        for (var index = 0; index < referencedSlots.Length; index++)
        {
            var slot = referencedSlots[index];
            if (slot >= nextSlot)
            {
                nextSlot = slot + 1;
            }
        }

        return new ExpressionState(nextSlot);
    }

    private readonly record struct PipelineCapture(string Name, int SourceSlot, int HelperSlot);

    private static void CollectReferencedIdentifiers(
        ExpressionNode expression,
        List<string> identifiers,
        ISet<string> bound)
    {
        switch (expression)
        {
            case IdentifierExpressionNode identifier:
                if (!bound.Contains(identifier.Name))
                {
                    identifiers.Add(identifier.Name);
                }

                break;

            case MessageLiteralExpressionNode message:
                foreach (var argument in message.Arguments)
                {
                    CollectReferencedIdentifiers(argument.Expression, identifiers, bound);
                }

                break;

            case ListLiteralExpressionNode list:
                foreach (var item in list.Items)
                {
                    CollectReferencedIdentifiers(item, identifiers, bound);
                }

                break;

            case MapLiteralExpressionNode dictionary:
                foreach (var entry in dictionary.Entries)
                {
                    CollectReferencedIdentifiers(entry.Value, identifiers, bound);
                }

                break;

            case UnaryExpressionNode unary:
                CollectReferencedIdentifiers(unary.Operand, identifiers, bound);
                break;

            case VariadicTaggedExpressionNode variadic:
                foreach (var argument in variadic.Arguments)
                {
                    CollectReferencedIdentifiers(argument, identifiers, bound);
                }

                break;

            case ClampExpressionNode clamp:
                CollectReferencedIdentifiers(clamp.Value, identifiers, bound);
                CollectReferencedIdentifiers(clamp.Minimum, identifiers, bound);
                CollectReferencedIdentifiers(clamp.Maximum, identifiers, bound);
                break;

            case RandomExpressionNode random:
                CollectReferencedIdentifiers(random.FromExpression, identifiers, bound);
                CollectReferencedIdentifiers(random.ToExpression, identifiers, bound);
                break;

            case RangeExpressionNode range:
                CollectReferencedIdentifiers(range.FromExpression, identifiers, bound);
                CollectReferencedIdentifiers(range.ToExpression, identifiers, bound);
                if (range.StepExpression is not null)
                {
                    CollectReferencedIdentifiers(range.StepExpression, identifiers, bound);
                }

                break;

            case SeededRandomExpressionNode seededRandom:
                CollectReferencedIdentifiers(seededRandom.SeedExpression, identifiers, bound);
                CollectReferencedIdentifiers(seededRandom.BodyExpression, identifiers, bound);
                break;

            case GeneratedCollectionExpressionNode generatedCollection:
            {
                CollectReferencedIdentifiers(generatedCollection.Source, identifiers, bound);
                var collectionBound = WithBound(bound, generatedCollection.Identifier);
                if (generatedCollection.Predicate is not null)
                {
                    CollectReferencedIdentifiers(generatedCollection.Predicate, identifiers, collectionBound);
                }

                CollectReferencedIdentifiers(generatedCollection.Projection, identifiers, collectionBound);
                break;
            }

            case GuardedChoiceExpressionNode guardedChoice:
                foreach (var branch in guardedChoice.Branches)
                {
                    CollectReferencedIdentifiers(branch.ConditionExpression, identifiers, bound);
                    CollectReferencedIdentifiers(branch.ValueExpression, identifiers, bound);
                }

                CollectReferencedIdentifiers(guardedChoice.OtherwiseExpression, identifiers, bound);
                break;

            case BinaryExpressionNode binary:
                CollectReferencedIdentifiers(binary.Left, identifiers, bound);
                CollectReferencedIdentifiers(binary.Right, identifiers, bound);
                break;

            case PredicateCallExpressionNode predicateCall:
                CollectReferencedIdentifiers(predicateCall.Value, identifiers, bound);
                break;

            case ExtensionPredicateExpressionNode extensionPredicate:
                CollectReferencedIdentifiers(extensionPredicate.Value, identifiers, bound);
                break;

            case CallExpressionNode call:
                foreach (var argument in call.Arguments)
                {
                    CollectReferencedIdentifiers(argument, identifiers, bound);
                }

                break;

            case TypeCastExpressionNode typeCast:
                CollectReferencedIdentifiers(typeCast.Value, identifiers, bound);
                break;

            case TypeConstructorExpressionNode typeConstructor:
                foreach (var argument in typeConstructor.Arguments)
                {
                    CollectReferencedIdentifiers(argument.Expression, identifiers, bound);
                }

                break;

            case TypeCheckExpressionNode typeCheck:
                CollectReferencedIdentifiers(typeCheck.Value, identifiers, bound);
                break;

            case MemberAccessExpressionNode memberAccess:
                CollectReferencedIdentifiers(memberAccess.Target, identifiers, bound);
                break;

            case CollectionAccessExpressionNode collectionAccess:
                CollectReferencedIdentifiers(collectionAccess.Target, identifiers, bound);
                CollectReferencedIdentifiers(collectionAccess.Selector, identifiers, bound);
                break;

            case ExtensionCallExpressionNode extensionCall:
                foreach (var argument in extensionCall.Arguments)
                {
                    CollectReferencedIdentifiers(argument.Expression, identifiers, bound);
                }

                break;
        }
    }

    private static void CollectReferencedIdentifiers(
        IterationSourceNode source,
        List<string> identifiers,
        ISet<string> bound)
    {
        switch (source)
        {
            case CollectionIterationSourceNode collection:
                CollectReferencedIdentifiers(collection.Expression, identifiers, bound);
                break;

            case RangeIterationSourceNode range:
                CollectReferencedIdentifiers(range.RangeExpression, identifiers, bound);
                break;
        }
    }

    private static void CollectReferencedIdentifiers(
        CollectionSelectorNode selector,
        List<string> identifiers,
        ISet<string> bound)
    {
        switch (selector)
        {
            case ExpressionSelectorNode expression:
                CollectReferencedIdentifiers(expression.Expression, identifiers, bound);
                break;

            case FilterSelectorNode filter:
                CollectReferencedIdentifiers(filter.Predicate, identifiers, WithBound(bound, filter.Identifier));
                break;

            case SelectSelectorNode select:
                CollectReferencedIdentifiers(select.Projection, identifiers, WithBound(bound, select.Identifier));
                break;

            case SumSelectorNode sum:
                CollectReferencedIdentifiers(sum.Projection, identifiers, WithBound(bound, sum.Identifier));
                break;

            case AverageSelectorNode average:
                CollectReferencedIdentifiers(average.Projection, identifiers, WithBound(bound, average.Identifier));
                break;

            case CountSelectorNode count:
                CollectReferencedIdentifiers(count.Predicate, identifiers, WithBound(bound, count.Identifier));
                break;

            case PredicateSelectorNode predicate:
                CollectReferencedIdentifiers(predicate.Predicate, identifiers, WithBound(bound, predicate.Identifier));
                break;

            case EdgeSelectorNode edge when edge.Predicate is not null && !string.IsNullOrEmpty(edge.Identifier):
                CollectReferencedIdentifiers(edge.Predicate, identifiers, WithBound(bound, edge.Identifier!));
                break;

            case MinSelectorNode min:
                CollectReferencedIdentifiers(min.Projection, identifiers, WithBound(bound, min.Identifier));
                break;

            case MaxSelectorNode max:
                CollectReferencedIdentifiers(max.Projection, identifiers, WithBound(bound, max.Identifier));
                break;

            case MapSelectorNode dictionary:
            {
                var dictionaryBound = WithBound(bound, dictionary.Identifier);
                CollectReferencedIdentifiers(dictionary.KeyProjection, identifiers, dictionaryBound);
                if (dictionary.ValueProjection is not null)
                {
                    CollectReferencedIdentifiers(dictionary.ValueProjection, identifiers, dictionaryBound);
                }

                break;
            }

            case ContainsSelectorNode contains:
                CollectReferencedIdentifiers(contains.ValueExpression, identifiers, bound);
                break;

            case ChooseSelectorNode choose:
                if (choose.Predicate is not null && !string.IsNullOrEmpty(choose.Identifier))
                {
                    CollectReferencedIdentifiers(choose.Predicate, identifiers, WithBound(bound, choose.Identifier!));
                }

                if (choose.WeightExpression is not null && !string.IsNullOrEmpty(choose.WeightIdentifier))
                {
                    CollectReferencedIdentifiers(choose.WeightExpression, identifiers, WithBound(bound, choose.WeightIdentifier!));
                }

                break;

            case DistinctSelectorNode distinct when distinct.Projection is not null && !string.IsNullOrEmpty(distinct.Identifier):
                CollectReferencedIdentifiers(distinct.Projection, identifiers, WithBound(bound, distinct.Identifier!));
                break;

            case GroupBySelectorNode groupBy:
                CollectReferencedIdentifiers(groupBy.Projection, identifiers, WithBound(bound, groupBy.Identifier));
                break;

            case OrderBySelectorNode orderBy:
                CollectReferencedIdentifiers(orderBy.Projection, identifiers, WithBound(bound, orderBy.Identifier));
                break;

            case PatternSelectorNode pattern:
                CollectReferencedIdentifiers(pattern.Pattern, identifiers, bound);
                break;

            case TakePatternSelectorNode takePattern:
                CollectReferencedIdentifiers(takePattern.Pattern, identifiers, bound);
                break;

            case ObjectMatchSelectorNode objectMatch:
                CollectReferencedIdentifiers(objectMatch.Pattern, identifiers, bound);
                break;
        }
    }

    private static void CollectReferencedIdentifiers(
        DicePatternNode pattern,
        List<string> identifiers,
        ISet<string> bound)
    {
        if (pattern is DiceCountPatternNode { Face: { } face })
        {
            CollectReferencedIdentifiers(face, identifiers, bound);
        }
    }

    private static void CollectReferencedIdentifiers(
        ObjectMatchPatternNode pattern,
        List<string> identifiers,
        ISet<string> bound)
    {
        foreach (var entry in pattern.Entries)
        {
            switch (entry.Value)
            {
                case ObjectMatchExpressionValueNode expression:
                    CollectReferencedIdentifiers(expression.Expression, identifiers, bound);
                    break;

                case ObjectMatchNestedValueNode nested:
                    CollectReferencedIdentifiers(nested.Pattern, identifiers, bound);
                    break;
            }
        }
    }

    private static HashSet<string> WithBound(ISet<string> bound, string identifier)
    {
        var copy = new HashSet<string>(bound, StringComparer.Ordinal)
        {
            identifier
        };
        return copy;
    }

    private sealed class SourceContext
    {
        private readonly IReadOnlyDictionary<string, int> _slots;
        private readonly SourceContext? _parent;
        private readonly Func<int>? _allocateScopedSlot;
        private readonly Dictionary<string, int> _locals = new(StringComparer.Ordinal);

        public SourceContext(IReadOnlyDictionary<string, int> slots, int? temporaryBaseSlot = null)
        {
            _slots = slots;
            SlotCount = Math.Max(GetSlotCount(slots), temporaryBaseSlot ?? 0);
        }

        private SourceContext(IReadOnlyDictionary<string, int> slots, SourceContext parent, int slotCount, Func<int> allocateScopedSlot)
        {
            _slots = slots;
            _parent = parent;
            _allocateScopedSlot = allocateScopedSlot;
            SlotCount = slotCount;
        }

        public IReadOnlyDictionary<string, int> Slots => _slots;

        public int SlotCount { get; private set; }

        public SourceContext CreateScope(int slotCount, Func<int> allocateScopedSlot)
            => new(_slots, this, slotCount, allocateScopedSlot);

        public SourceContext WithTemporaryBaseSlot(int temporaryBaseSlot)
        {
            SlotCount = Math.Max(SlotCount, temporaryBaseSlot);
            return this;
        }

        public int DeclareSlot(string name)
        {
            if (_allocateScopedSlot is null)
            {
                return RequireSlot(name);
            }

            if (!_locals.TryGetValue(name, out var slot))
            {
                slot = _allocateScopedSlot();
                _locals[name] = slot;
                SlotCount = Math.Max(SlotCount, slot + 1);
            }

            return slot;
        }

        public int RequireSlot(string name)
        {
            if (TryResolveScopedSlot(name, out var slot) ||
                _slots.TryGetValue(name, out slot))
            {
                return slot;
            }

            throw new InvalidOperationException($"Missing GameEventScript bytecode local slot '{name}'.");
        }

        private bool TryResolveScopedSlot(string name, out int slot)
        {
            if (_locals.TryGetValue(name, out slot))
            {
                return true;
            }

            if (_parent is not null)
            {
                return _parent.TryResolveScopedSlot(name, out slot);
            }

            slot = 0;
            return false;
        }
    }

    private sealed class ExpressionState(int nextSlot)
    {
        public int BaseSlot { get; } = Math.Max(0, nextSlot);

        public int NextSlot { get; private set; } = Math.Max(0, nextSlot);

        public int Allocate()
            => NextSlot++;

        public void EnsureNextSlot(int slot)
        {
            if (slot > NextSlot)
            {
                NextSlot = slot;
            }
        }
    }

    private static bool ExpressionReferencesIdentifier(ExpressionNode expression, string identifier)
    {
        switch (expression)
        {
            case IdentifierExpressionNode candidate:
                return string.Equals(candidate.Name, identifier, StringComparison.Ordinal);

            case MessageLiteralExpressionNode message:
                return message.Arguments.Any(argument => ExpressionReferencesIdentifier(argument.Expression, identifier));

            case ListLiteralExpressionNode list:
                return list.Items.Any(item => ExpressionReferencesIdentifier(item, identifier));

            case MapLiteralExpressionNode dictionary:
                return dictionary.Entries.Any(entry => ExpressionReferencesIdentifier(entry.Value, identifier));

            case UnaryExpressionNode unary:
                return ExpressionReferencesIdentifier(unary.Operand, identifier);

            case VariadicTaggedExpressionNode variadic:
                return variadic.Arguments.Any(argument => ExpressionReferencesIdentifier(argument, identifier));

            case ClampExpressionNode clamp:
                return ExpressionReferencesIdentifier(clamp.Value, identifier) ||
                       ExpressionReferencesIdentifier(clamp.Minimum, identifier) ||
                       ExpressionReferencesIdentifier(clamp.Maximum, identifier);

            case RandomExpressionNode random:
                return ExpressionReferencesIdentifier(random.FromExpression, identifier) ||
                       ExpressionReferencesIdentifier(random.ToExpression, identifier);

            case RangeExpressionNode range:
                return ExpressionReferencesIdentifier(range.FromExpression, identifier) ||
                       ExpressionReferencesIdentifier(range.ToExpression, identifier) ||
                       (range.StepExpression is not null && ExpressionReferencesIdentifier(range.StepExpression, identifier));

            case SeededRandomExpressionNode seededRandom:
                return ExpressionReferencesIdentifier(seededRandom.SeedExpression, identifier) ||
                       ExpressionReferencesIdentifier(seededRandom.BodyExpression, identifier);

            case GeneratedCollectionExpressionNode generatedCollection:
                return ExpressionReferencesIdentifier(generatedCollection.Source, identifier) ||
                       (generatedCollection.Predicate is not null && ExpressionReferencesIdentifier(generatedCollection.Predicate, identifier)) ||
                       ExpressionReferencesIdentifier(generatedCollection.Projection, identifier);

            case GuardedChoiceExpressionNode guardedChoice:
                return guardedChoice.Branches.Any(branch =>
                           ExpressionReferencesIdentifier(branch.ConditionExpression, identifier) ||
                           ExpressionReferencesIdentifier(branch.ValueExpression, identifier)) ||
                       ExpressionReferencesIdentifier(guardedChoice.OtherwiseExpression, identifier);

            case BinaryExpressionNode binary:
                return ExpressionReferencesIdentifier(binary.Left, identifier) ||
                       ExpressionReferencesIdentifier(binary.Right, identifier);

            case PredicateCallExpressionNode predicateCall:
                return ExpressionReferencesIdentifier(predicateCall.Value, identifier);

            case ExtensionPredicateExpressionNode extensionPredicate:
                return ExpressionReferencesIdentifier(extensionPredicate.Value, identifier);

            case CallExpressionNode call:
                return call.Arguments.Any(argument => ExpressionReferencesIdentifier(argument, identifier));

            case TypeCastExpressionNode typeCast:
                return ExpressionReferencesIdentifier(typeCast.Value, identifier);

            case TypeConstructorExpressionNode typeConstructor:
                return typeConstructor.Arguments.Any(argument => ExpressionReferencesIdentifier(argument.Expression, identifier));

            case TypeCheckExpressionNode typeCheck:
                return ExpressionReferencesIdentifier(typeCheck.Value, identifier);

            case MemberAccessExpressionNode memberAccess:
                return ExpressionReferencesIdentifier(memberAccess.Target, identifier);

            case CollectionAccessExpressionNode collectionAccess:
                return ExpressionReferencesIdentifier(collectionAccess.Target, identifier) ||
                       ExpressionReferencesIdentifier(collectionAccess.Selector, identifier);

            case ExtensionCallExpressionNode extensionCall:
                return extensionCall.Arguments.Any(argument => ExpressionReferencesIdentifier(argument.Expression, identifier));

            default:
                return false;
        }
    }

    private static bool ExpressionReferencesIdentifier(IterationSourceNode source, string identifier)
        => source switch
        {
            CollectionIterationSourceNode collection => ExpressionReferencesIdentifier(collection.Expression, identifier),
            RangeIterationSourceNode range => ExpressionReferencesIdentifier(range.RangeExpression, identifier),
            _ => false
        };

    private static bool ExpressionReferencesIdentifier(CollectionSelectorNode selector, string identifier)
        => selector switch
        {
            ExpressionSelectorNode expression => ExpressionReferencesIdentifier(expression.Expression, identifier),
            FilterSelectorNode filter => ExpressionReferencesIdentifier(filter.Predicate, identifier),
            SelectSelectorNode select => ExpressionReferencesIdentifier(select.Projection, identifier),
            SumSelectorNode sum => ExpressionReferencesIdentifier(sum.Projection, identifier),
            AverageSelectorNode average => ExpressionReferencesIdentifier(average.Projection, identifier),
            CountSelectorNode count => ExpressionReferencesIdentifier(count.Predicate, identifier),
            PredicateSelectorNode predicate => ExpressionReferencesIdentifier(predicate.Predicate, identifier),
            EdgeSelectorNode edge => edge.Predicate is not null && ExpressionReferencesIdentifier(edge.Predicate, identifier),
            MinSelectorNode min => ExpressionReferencesIdentifier(min.Projection, identifier),
            MaxSelectorNode max => ExpressionReferencesIdentifier(max.Projection, identifier),
            MapSelectorNode dictionary => ExpressionReferencesIdentifier(dictionary.KeyProjection, identifier) ||
                                                 (dictionary.ValueProjection is not null && ExpressionReferencesIdentifier(dictionary.ValueProjection, identifier)),
            ContainsSelectorNode contains => ExpressionReferencesIdentifier(contains.ValueExpression, identifier),
            ChooseSelectorNode choose => (choose.Predicate is not null && ExpressionReferencesIdentifier(choose.Predicate, identifier)) ||
                                         (choose.WeightExpression is not null && ExpressionReferencesIdentifier(choose.WeightExpression, identifier)),
            DistinctSelectorNode distinct => distinct.Projection is not null && ExpressionReferencesIdentifier(distinct.Projection, identifier),
            GroupBySelectorNode groupBy => ExpressionReferencesIdentifier(groupBy.Projection, identifier),
            OrderBySelectorNode orderBy => ExpressionReferencesIdentifier(orderBy.Projection, identifier),
            _ => false
        };

    private sealed class ScopeBuildState(int address, int baseSlotCount, bool isRootScope)
    {
        public int Address { get; } = address;

        public int ExitAddress { get; private set; } = -1;

        public int BaseSlotCount { get; } = baseSlotCount;

        public bool IsRootScope { get; } = isRootScope;

        public void SetExitAddress(int exitAddress)
            => ExitAddress = exitAddress;

        private int NextLocalSlot { get; set; } = baseSlotCount;

        public int MaxSlotCount { get; private set; } = baseSlotCount;

        public void MarkSlotCount(int slotCount)
            => MaxSlotCount = Math.Max(MaxSlotCount, slotCount);

        public int AllocateSlot()
        {
            var slot = NextLocalSlot;
            NextLocalSlot++;
            MarkSlotCount(NextLocalSlot);
            return slot;
        }
    }

    private readonly struct StageArgumentPlan
    {
        private StageArgumentPlan(GameEventScriptBytecodeInstruction instruction, int slot, bool hasInstruction)
        {
            Instruction = instruction;
            Slot = slot;
            HasInstruction = hasInstruction;
        }

        public GameEventScriptBytecodeInstruction Instruction { get; }

        public int Slot { get; }

        public bool HasInstruction { get; }

        public static StageArgumentPlan FromInstruction(GameEventScriptBytecodeInstruction instruction)
            => new(instruction, 0, hasInstruction: true);

        public static StageArgumentPlan FromSlot(int slot)
            => new(default, slot, hasInstruction: false);
    }
}
