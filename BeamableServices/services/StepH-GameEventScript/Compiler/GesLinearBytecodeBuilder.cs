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
    private readonly bool _emitDiagnosticLayouts;
    private readonly List<GameEventScriptBytecodeInstruction> _code = [];
    private readonly List<GameEventScriptBytecodeOperationLayout> _operationLayouts = [];
    private readonly List<GameEventScriptBytecodeDiagnosticLayout> _diagnosticLayouts = [];
    private readonly List<GameEventScriptBytecodePipelinePattern> _pipelinePatternPool = [];
    private readonly List<GameEventScriptBytecodePipelineObjectPattern> _pipelineObjectPatternPool = [];
    private readonly List<GameEventScriptBytecodePipelineSelector> _pipelineSelectorPool = [];
    private readonly List<GameEventScriptBytecodePipeline> _pipelinePool = [];
    private readonly List<Action> _deferredHelperEmitters = [];
    private int _currentFrameSlotCount;
    private int _maxFrameSlots = 1;

    public GesLinearBytecodeBuilder(
        Func<string, int>? resolveStringIndex = null,
        Func<IReadOnlyList<ushort>, int>? resolveUShortListIndex = null,
        Func<GameEventScriptExtensionReference, int>? resolveExternalReferenceIndex = null,
        IReadOnlyDictionary<string, GesCallableDefinition>? sourceCallables = null,
        IReadOnlyDictionary<string, TypeDefinitionNode>? sourceTypeDefinitions = null,
        bool emitDiagnosticLayouts = false)
    {
        _resolveStringIndex = resolveStringIndex ?? (_ => -1);
        _resolveUShortListIndex = resolveUShortListIndex ?? (_ => -1);
        _resolveExternalReferenceIndex = resolveExternalReferenceIndex ?? (_ => -1);
        _sourceCallables = sourceCallables ?? new Dictionary<string, GesCallableDefinition>(StringComparer.Ordinal);
        _sourceTypeDefinitions = sourceTypeDefinitions ?? new Dictionary<string, TypeDefinitionNode>(StringComparer.Ordinal);
        _emitDiagnosticLayouts = emitDiagnosticLayouts;
    }

    public IReadOnlyList<GameEventScriptBytecodeInstruction> Code => _code;

    public int MaxFrameSlots => _maxFrameSlots;

    public IReadOnlyList<GameEventScriptBytecodeOperationLayout> OperationLayouts => _operationLayouts;

    public IReadOnlyList<GameEventScriptBytecodeDiagnosticLayout> DiagnosticLayouts => _diagnosticLayouts;

    public IReadOnlyList<GameEventScriptBytecodePipelinePattern> PipelinePatternPool => _pipelinePatternPool;

    public IReadOnlyList<GameEventScriptBytecodePipelineObjectPattern> PipelineObjectPatternPool => _pipelineObjectPatternPool;

    public IReadOnlyList<GameEventScriptBytecodePipelineSelector> PipelineSelectorPool => _pipelineSelectorPool;

    public IReadOnlyList<GameEventScriptBytecodePipeline> PipelinePool => _pipelinePool;

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
            _currentFrameSlotCount = handler.LocalSlotCount;
            _maxFrameSlots = Math.Max(_maxFrameSlots, handler.LocalSlotCount);
            EmitParameterBindings(handler.Parameters, handler.ParameterTypes, handler.Slots);
            if (sourceHandlers.TryGetValue(handler, out var sourceHandler))
            {
                EmitSourceStatements(sourceHandler.Statements, createsScope: false, handler.Slots);
            }
            else
            {
                throw new GameEventScriptCompileException(
                    $"GameEventScript linear bytecode builder requires source statements for handler '{FormatSignature(handler.Message, handler.SignatureLabels)}'.");
            }

            Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.ReturnNothing));
            FlushDeferredHelpers();
            _maxFrameSlots = Math.Max(_maxFrameSlots, _currentFrameSlotCount);
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

        foreach (var callable in callables)
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
        callable.LocalSlotCount = Math.Max(callable.Parameters.Count + 1, slots.Count);
        _currentFrameSlotCount = callable.LocalSlotCount;
        _maxFrameSlots = Math.Max(_maxFrameSlots, callable.LocalSlotCount);

        for (var index = 0; index < callable.Parameters.Count; index++)
        {
            var parameterSlot = context.RequireSlot(callable.Parameters[index]);
            Emit(CreateInstruction(GameEventScriptBytecodeOpCode.BindParameter, dest: parameterSlot, a: index));
            if (index < callable.ParameterTypes.Count && !string.IsNullOrEmpty(callable.ParameterTypes[index]))
            {
                EmitCastSlot(parameterSlot, parameterSlot, callable.ParameterTypes[index]);
            }
        }

        var state = new ExpressionState(slots.Count);
        var result = EmitSourceExpression(sourceCallable.Expression, context, state);
        callable.ReturnSlot = result;
        Emit(CreateInstruction(GameEventScriptBytecodeOpCode.Return, a: result));
        FlushDeferredHelpers();
        callable.LocalSlotCount = Math.Max(callable.LocalSlotCount, _currentFrameSlotCount);
        _maxFrameSlots = Math.Max(_maxFrameSlots, callable.LocalSlotCount);
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

    private int EmitSourceExpressionEntry(ExpressionNode expression, SourceContext context, ExpressionState state)
    {
        var entry = _code.Count;
        var result = EmitSourceExpression(expression, context, state);
        _maxFrameSlots = Math.Max(_maxFrameSlots, state.NextSlot);
        _currentFrameSlotCount = Math.Max(_currentFrameSlotCount, state.NextSlot);
        Emit(CreateInstruction(GameEventScriptBytecodeOpCode.Return, a: result));
        return entry;
    }

    private void EmitParameterBindings(
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

            Emit(CreateInstruction(GameEventScriptBytecodeOpCode.BindParameter, dest: slot, a: index));
            if (index < parameterTypes.Count && !string.IsNullOrEmpty(parameterTypes[index]))
            {
                EmitCastSlot(slot, slot, parameterTypes[index]);
            }
        }
    }

    private void EmitSourceStatements(
        IReadOnlyList<StatementNode> statements,
        bool createsScope,
        IReadOnlyDictionary<string, int> slots,
        int? temporaryBaseSlot = null)
    {
        if (createsScope)
        {
            Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.EnterScope));
        }

        var context = new SourceContext(slots, temporaryBaseSlot);
        foreach (var statement in statements)
        {
            EmitSourceStatement(statement, context);
        }

        if (createsScope)
        {
            Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.ExitScope));
        }
    }

    private void EmitSourceStatement(StatementNode statement, SourceContext context)
    {
        switch (statement)
        {
            case LetStatementNode let:
            {
                var result = EmitSourceExpression(let.Expression, context, new ExpressionState(context.SlotCount));
                var letSlot = context.RequireSlot(let.Identifier);
                var diagnosticAddress = Emit(CreateInstruction(GameEventScriptBytecodeOpCode.MoveSlot, dest: letSlot, a: result));
                if (!string.IsNullOrEmpty(let.DeclaredType))
                {
                    diagnosticAddress = EmitCastSlot(letSlot, letSlot, let.DeclaredType);
                }

                AddDiagnosticLayout(
                    GameEventScriptBytecodeDiagnosticKind.LetEvaluated,
                    GameEventScriptBytecodeDiagnosticTiming.AfterInstruction,
                    diagnosticAddress,
                    letSlot,
                    let.Identifier);
                AddDiagnosticLayout(
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
                    AddDiagnosticLayout(
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
        var jumpToElse = Emit(CreateInstruction(GameEventScriptBytecodeOpCode.JumpIfNotTrue, c: condition));
        EmitSourceStatements(ifStatement.ThenBody.Statements, ifStatement.ThenBody.IsBlock, context.Slots);
        var jumpToEnd = Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.Jump));
        PatchTarget(jumpToElse, _code.Count);
        if (ifStatement.ElseBody is not null)
        {
            EmitSourceStatements(ifStatement.ElseBody.Statements, ifStatement.ElseBody.IsBlock, context.Slots);
        }

        PatchTarget(jumpToEnd, _code.Count);
    }

    private void EmitSourceLoop(ForStatementNode forStatement, SourceContext context)
    {
        var state = new ExpressionState(context.SlotCount);
        var identifierSlot = context.RequireSlot(forStatement.Identifier);

        Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.EnterScope));
        var iteratorSlot = EmitSourceIterator(forStatement.Source, context, state);
        var itemSlot = AllocateSlot(state);

        var loopAddress = _code.Count;
        var nextInstruction = Emit(CreateInstruction(
            GameEventScriptBytecodeOpCode.IteratorNext,
            dest: itemSlot,
            a: iteratorSlot));
        Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.EnterScope));
        Emit(CreateInstruction(GameEventScriptBytecodeOpCode.MoveSlot, dest: identifierSlot, a: itemSlot));
        EmitSourceStatements(forStatement.Body.Statements, forStatement.Body.IsBlock, context.Slots, state.NextSlot);
        Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.ExitScope));
        Emit(CreateInstruction(GameEventScriptBytecodeOpCode.Jump, a: loopAddress));

        var endAddress = _code.Count;
        PatchB(nextInstruction, endAddress);
        Emit(CreateInstruction(GameEventScriptBytecodeOpCode.IteratorClose, a: iteratorSlot));
        Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.ExitScope));
    }

    private int EmitSourceGeneratedCollection(GeneratedCollectionExpressionNode generatedCollection, SourceContext context, ExpressionState state)
    {
        var builderOpCode = generatedCollection.CollectionType switch
        {
            "list" => GameEventScriptBytecodeOpCode.CollectionBuilderList,
            "set" => GameEventScriptBytecodeOpCode.CollectionBuilderSet,
            _ => throw new GameEventScriptCompileException($"GameEventScript bytecode lowerer does not support generated collection type '{generatedCollection.CollectionType}'.")
        };

        var identifierSlot = context.RequireSlot(generatedCollection.Identifier);
        var builderSlot = AllocateSlot(state);
        Emit(CreateInstruction(builderOpCode, dest: builderSlot));

        Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.EnterScope));
        var iteratorSlot = EmitSourceIterator(generatedCollection.Source, context, state);
        var itemSlot = AllocateSlot(state);

        var loopAddress = _code.Count;
        var nextInstruction = Emit(CreateInstruction(
            GameEventScriptBytecodeOpCode.IteratorNext,
            dest: itemSlot,
            a: iteratorSlot));
        Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.EnterScope));
        Emit(CreateInstruction(GameEventScriptBytecodeOpCode.MoveSlot, dest: identifierSlot, a: itemSlot));

        var skipProjectionJump = -1;
        if (generatedCollection.Predicate is not null)
        {
            var predicateSlot = EmitSourceExpression(generatedCollection.Predicate, context, state);
            skipProjectionJump = Emit(CreateInstruction(GameEventScriptBytecodeOpCode.JumpIfNotTrue, c: predicateSlot));
        }

        var projectionSlot = EmitSourceExpression(generatedCollection.Projection, context, state);
        Emit(CreateInstruction(GameEventScriptBytecodeOpCode.CollectionBuilderAdd, a: builderSlot, b: projectionSlot));
        if (skipProjectionJump >= 0)
        {
            PatchTarget(skipProjectionJump, _code.Count);
        }

        Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.ExitScope));
        Emit(CreateInstruction(GameEventScriptBytecodeOpCode.Jump, a: loopAddress));

        var endAddress = _code.Count;
        PatchB(nextInstruction, endAddress);
        Emit(CreateInstruction(GameEventScriptBytecodeOpCode.IteratorClose, a: iteratorSlot));
        Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.ExitScope));

        var resultSlot = AllocateSlot(state);
        Emit(CreateInstruction(GameEventScriptBytecodeOpCode.CollectionBuilderFinish, dest: resultSlot, a: builderSlot));
        return resultSlot;
    }

    private void EmitSourceSeededRandom(SeededRandomStatementNode seededRandom, SourceContext context)
    {
        var state = new ExpressionState(context.SlotCount);
        EmitRandomPush(seededRandom.SeedExpression, context, state);
        EmitSourceStatements(seededRandom.Body.Statements, seededRandom.Body.IsBlock, context.Slots);
        Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.RandomPop));
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
                return EmitValueInstruction(state, GameEventScriptBytecodeOpCode.MoveSlot, a: context.RequireSlot(identifier.Name));

            case MessageLiteralExpressionNode message:
                return EmitSourceMessage(message, context, state);

            case ExtensionCallExpressionNode extensionCall:
                return EmitSourceExtensionCall(extensionCall, context, state);

            case ListLiteralExpressionNode list:
                return EmitSourceCollectionBuilder(GameEventScriptBytecodeOpCode.BuildList, list.Items, null, context, state);

            case SequenceLiteralExpressionNode sequence:
                return EmitSourceCollectionBuilder(GameEventScriptBytecodeOpCode.BuildSequence, sequence.Items, null, context, state);

            case SetLiteralExpressionNode set:
                return EmitSourceCollectionBuilder(GameEventScriptBytecodeOpCode.BuildSet, set.Items, null, context, state);

            case DictionaryLiteralExpressionNode dictionary:
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
                var opCode = ResolveTypeCheckOpCode(typeCheck.TypeName, out var nameIndex);
                return EmitValueInstruction(state, opCode, a: value, c: nameIndex < 0 ? 0 : nameIndex);
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
                return EmitLoadFloat(
                    state,
                    percentage.Ratio,
                    (byte)GameEventScriptBytecodeInstructionUnit.Percentage);

            case GameEventScriptTextValue text:
                return EmitValueInstruction(state, GameEventScriptBytecodeOpCode.LoadText, c: ResolveStringIndex(text.Value));

            case GameEventScriptTagValue tag:
                return EmitValueInstruction(state, GameEventScriptBytecodeOpCode.LoadTag, c: ResolveStringIndex(tag.Value));

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

        var instruction = new GameEventScriptBytecodeStackInstruction(
            GameEventScriptBytecodeOpCode.BuildMessage,
            A: message.Arguments.Count,
            DiagnosticName: GameEventScriptMessageSignature.NormalizeMessageName(message.Message),
            DiagnosticArgumentName: GameEventScriptMessageSignature.CreateSignatureId(message.Message, argumentNames),
            Names: argumentNames);
        var layoutIndex = AddOperationLayout(instruction, argumentSlots, state, argumentSlots.Length);
        return EmitValueInstruction(
            state,
            GameEventScriptBytecodeOpCode.BuildMessage,
            argumentSlots.Length > 0 ? argumentSlots[0] : 0,
            argumentSlots.Length > 1 ? argumentSlots[1] : 0,
            layoutIndex);
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

        var referenceIndex = _resolveExternalReferenceIndex(new GameEventScriptExtensionReference(
            extensionCall.ExtensionName,
            extensionCall.FunctionName,
            argumentNames));
        var instruction = new GameEventScriptBytecodeStackInstruction(
            GameEventScriptBytecodeOpCode.CallExtension,
            A: argumentSlots.Length,
            B: referenceIndex,
            CallableKind: GameEventScriptBytecodeCallableKind.Function,
            DiagnosticName: extensionCall.ExtensionName,
            DiagnosticArgumentName: extensionCall.FunctionName,
            Names: argumentNames);
        var layoutIndex = AddOperationLayout(instruction, argumentSlots, state, argumentSlots.Length);
        return EmitSourceValueInstruction(GameEventScriptBytecodeOpCode.CallExtension, argumentSlots, state, layoutIndex);
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

        var instruction = new GameEventScriptBytecodeStackInstruction(opCode, A: itemSlots.Length, Names: names);
        var layoutIndex = AddOperationLayout(instruction, itemSlots, state, itemSlots.Length);
        return EmitSourceValueInstruction(opCode, itemSlots, state, layoutIndex);
    }

    private int EmitSourceDictionary(DictionaryLiteralExpressionNode dictionary, SourceContext context, ExpressionState state)
    {
        var names = new string[dictionary.Entries.Count];
        var valueSlots = new int[dictionary.Entries.Count];
        for (var entryIndex = 0; entryIndex < dictionary.Entries.Count; entryIndex++)
        {
            var entry = dictionary.Entries[entryIndex];
            names[entryIndex] = entry.Key;
            valueSlots[entryIndex] = EmitSourceExpression(entry.Value, context, state);
        }

        var instruction = new GameEventScriptBytecodeStackInstruction(
            GameEventScriptBytecodeOpCode.BuildDictionary,
            A: valueSlots.Length,
            Names: names);
        var layoutIndex = AddOperationLayout(instruction, valueSlots, state, valueSlots.Length);
        return EmitSourceValueInstruction(GameEventScriptBytecodeOpCode.BuildDictionary, valueSlots, state, layoutIndex);
    }

    private int EmitSourceVariadic(VariadicTaggedExpressionNode variadic, SourceContext context, ExpressionState state)
    {
        var argumentSlots = new int[variadic.Arguments.Count];
        for (var index = 0; index < variadic.Arguments.Count; index++)
        {
            argumentSlots[index] = EmitSourceExpression(variadic.Arguments[index], context, state);
        }

        var instruction = new GameEventScriptBytecodeStackInstruction(
            GameEventScriptBytecodeOpCode.Variadic,
            A: argumentSlots.Length,
            DiagnosticName: variadic.Operator);
        var layoutIndex = AddOperationLayout(instruction, argumentSlots, state, argumentSlots.Length);
        return EmitSourceValueInstruction(GameEventScriptBytecodeOpCode.Variadic, argumentSlots, state, layoutIndex);
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
        Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.RandomPop));
        return result;
    }

    private void EmitRandomPush(ExpressionNode seedExpression, SourceContext context, ExpressionState state)
    {
        if (TryReadUnitlessIntegerLiteralSeed(seedExpression, out var seed))
        {
            var instruction = new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.RandomPushConstant);
            instruction.U64 = seed;
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
            var jumpToNextBranch = Emit(CreateInstruction(GameEventScriptBytecodeOpCode.JumpIfNotTrue, c: condition));

            var value = EmitSourceExpression(branch.ValueExpression, context, state);
            Emit(CreateInstruction(GameEventScriptBytecodeOpCode.MoveSlot, dest: resultSlot, a: value));
            endJumps.Add(Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.Jump)));

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
        var opCode = binary.Operator switch
        {
            "|" => GameEventScriptBytecodeOpCode.ShortCircuitOr,
            "&" => GameEventScriptBytecodeOpCode.ShortCircuitAnd,
            "->" => GameEventScriptBytecodeOpCode.ShortCircuitImplies,
            _ => (GameEventScriptBytecodeOpCode?)null
        };
        if (opCode is null)
        {
            resultSlot = -1;
            return false;
        }

        var left = EmitSourceExpression(binary.Left, context, state);
        resultSlot = AllocateSlot(state);
        if (opCode == GameEventScriptBytecodeOpCode.ShortCircuitImplies)
        {
            Emit(CreateInstruction(GameEventScriptBytecodeOpCode.ShortCircuitImplies, dest: resultSlot, a: left, b: left));
        }
        else
        {
            Emit(CreateInstruction(GameEventScriptBytecodeOpCode.MoveSlot, dest: resultSlot, a: left));
        }

        var branch = opCode switch
        {
            GameEventScriptBytecodeOpCode.ShortCircuitOr => GameEventScriptBytecodeOpCode.JumpIfTrue,
            GameEventScriptBytecodeOpCode.ShortCircuitAnd => GameEventScriptBytecodeOpCode.JumpIfFalse,
            _ => GameEventScriptBytecodeOpCode.JumpIfFalse
        };
        var jump = Emit(CreateInstruction(branch, c: left));
        var right = EmitSourceExpression(binary.Right, context, state);
        var combineOp = opCode switch
        {
            GameEventScriptBytecodeOpCode.ShortCircuitOr => GameEventScriptBytecodeOpCode.Or,
            GameEventScriptBytecodeOpCode.ShortCircuitAnd => GameEventScriptBytecodeOpCode.And,
            _ => GameEventScriptBytecodeOpCode.ShortCircuitImplies
        };
        Emit(CreateInstruction(combineOp, dest: resultSlot, a: left, b: right));
        PatchTarget(jump, _code.Count);
        return true;
    }

    private int EmitSourcePredicateCall(PredicateCallExpressionNode predicateCall, SourceContext context, ExpressionState state)
    {
        if (!_sourceCallables.TryGetValue(predicateCall.PredicateName, out var callable) ||
            callable.Parameters.Count != 1)
        {
            throw new GameEventScriptCompileException($"GameEventScript bytecode lowerer does not support predicate test '{predicateCall.PredicateName}'.");
        }

        var input = EmitSourceExpression(predicateCall.Value, context, state);
        var parameterSlot = GesBytecodeLowerer.CollectCallableSlots(callable, _sourceCallables, _sourceTypeDefinitions)
            .TryGetValue(callable.Parameters[0], out var slot)
            ? slot
            : 0;
        var instruction = new GameEventScriptBytecodeStackInstruction(
            GameEventScriptBytecodeOpCode.PredicateTest,
            parameterSlot,
            CallableKind: GameEventScriptBytecodeCallableKind.Predicate,
            DiagnosticName: callable.Name,
            DiagnosticArgumentName: callable.Parameters[0],
            DeclaredTypes: new string?[] { callable.ParameterList[0].DeclaredType });
        var layoutIndex = AddOperationLayout(instruction, [input], state, count: 1);
        return EmitValueInstruction(state, GameEventScriptBytecodeOpCode.PredicateTest, a: input, c: layoutIndex);
    }

    private int EmitSourceExtensionPredicate(ExtensionPredicateExpressionNode extensionPredicate, SourceContext context, ExpressionState state)
    {
        var input = EmitSourceExpression(extensionPredicate.Value, context, state);
        var labels = new[] { GameEventScriptMessageSignature.UnlabeledParameterName };
        var referenceIndex = _resolveExternalReferenceIndex(new GameEventScriptExtensionReference(
            extensionPredicate.ExtensionName,
            extensionPredicate.FunctionName,
            labels));
        var instruction = new GameEventScriptBytecodeStackInstruction(
            GameEventScriptBytecodeOpCode.CallExtension,
            A: 1,
            B: referenceIndex,
            CallableKind: GameEventScriptBytecodeCallableKind.Predicate,
            DiagnosticName: extensionPredicate.ExtensionName,
            DiagnosticArgumentName: extensionPredicate.FunctionName,
            Names: labels);
        var layoutIndex = AddOperationLayout(instruction, [input], state, count: 1);
        return EmitValueInstruction(state, GameEventScriptBytecodeOpCode.CallExtension, a: input, c: layoutIndex);
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

            var bindInstruction = new GameEventScriptBytecodeStackInstruction(
                GameEventScriptBytecodeOpCode.BindHandler,
                A: call.ArgumentList.Count,
                Names: argumentNames);
            var bindLayoutIndex = AddOperationLayout(bindInstruction, operandSlots, state, operandSlots.Length);
            return EmitSourceValueInstruction(GameEventScriptBytecodeOpCode.BindHandler, operandSlots, state, bindLayoutIndex);
        }

        var argumentSlots = new int[call.Arguments.Count];
        for (var argumentIndex = 0; argumentIndex < call.Arguments.Count; argumentIndex++)
        {
            argumentSlots[argumentIndex] = EmitSourceExpression(call.Arguments[argumentIndex], context, state);
        }

        var calledSlots = GesBytecodeLowerer.CollectCallableSlots(called, _sourceCallables, _sourceTypeDefinitions);
        var parameterSlots = new int[called.Parameters.Count];
        for (var parameterIndex = 0; parameterIndex < called.Parameters.Count; parameterIndex++)
        {
            parameterSlots[parameterIndex] = calledSlots.TryGetValue(called.Parameters[parameterIndex], out var parameterSlot)
                ? parameterSlot
                : parameterIndex;
        }

        var instruction = new GameEventScriptBytecodeStackInstruction(
            GameEventScriptBytecodeOpCode.Call,
            A: argumentSlots.Length,
            CallableKind: called.Kind == GameEventScriptCallableKind.PredicateCall
                ? GameEventScriptBytecodeCallableKind.Predicate
                : GameEventScriptBytecodeCallableKind.Function,
            DiagnosticName: called.Name,
            Names: called.Parameters.ToArray(),
            Slots: parameterSlots,
            DeclaredTypes: called.ParameterList.Select(parameter => parameter.DeclaredType).ToArray());
        var layoutIndex = AddOperationLayout(instruction, argumentSlots, state, argumentSlots.Length);
        return EmitSourceValueInstruction(GameEventScriptBytecodeOpCode.Call, argumentSlots, state, layoutIndex);
    }

    private int EmitSourceTypeCast(TypeCastExpressionNode typeCast, SourceContext context, ExpressionState state)
    {
        if (!TryGetCastOpCode(typeCast.TypeName, out var opCode))
        {
            throw new GameEventScriptCompileException($"GameEventScript bytecode lowerer does not support type cast '{typeCast.TypeName}'.");
        }

        var value = EmitSourceExpression(typeCast.Value, context, state);
        return EmitValueInstruction(state, opCode, a: value);
    }

    private int EmitSourceTypeConstructor(TypeConstructorExpressionNode typeConstructor, SourceContext context, ExpressionState state)
    {
        if (typeConstructor.Arguments.Count == 1 &&
            typeConstructor.Arguments[0].Label is null &&
            TryGetCastOpCode(typeConstructor.TypeName, out var constructorCastOpCode))
        {
            var value = EmitSourceExpression(typeConstructor.Arguments[0].Expression, context, state);
            return EmitValueInstruction(state, constructorCastOpCode, a: value);
        }

        var argumentNames = new string[typeConstructor.Arguments.Count];
        var argumentSlots = new int[typeConstructor.Arguments.Count];
        for (var argumentIndex = 0; argumentIndex < typeConstructor.Arguments.Count; argumentIndex++)
        {
            var argument = typeConstructor.Arguments[argumentIndex];
            argumentNames[argumentIndex] = argument.Name;
            argumentSlots[argumentIndex] = EmitSourceExpression(argument.Expression, context, state);
        }

        var instruction = new GameEventScriptBytecodeStackInstruction(
            GameEventScriptBytecodeOpCode.TypeConstructor,
            A: argumentSlots.Length,
            DiagnosticName: typeConstructor.TypeName,
            Names: argumentNames);
        var layoutIndex = AddOperationLayout(instruction, argumentSlots, state, argumentSlots.Length);
        return EmitSourceValueInstruction(GameEventScriptBytecodeOpCode.TypeConstructor, argumentSlots, state, layoutIndex);
    }

    private int EmitSourcePipeline(CollectionAccessExpressionNode collectionAccess, SourceContext context, ExpressionState state)
    {
        var layoutIndex = AddSourcePipeline(collectionAccess, context, state);
        return EmitValueInstruction(state, GameEventScriptBytecodeOpCode.Pipeline, c: layoutIndex);
    }

    private int EmitSourceValueInstruction(
        GameEventScriptBytecodeOpCode opCode,
        IReadOnlyList<int> operands,
        ExpressionState state,
        int layoutIndex)
        => EmitValueInstruction(
            state,
            opCode,
            operands.Count > 0 ? operands[0] : 0,
            operands.Count > 1 ? operands[1] : 0,
            layoutIndex);

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
        var nameListIndex = ResolveStringListIndex(names);
        _operationLayouts.Add(CreateOperationLayout(expressionEntryAddress: -1));

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
                instruction.B,
                nameListIndex,
                expressionEntryAddress: expressionEntryAddress,
                count: count);
    }

    private int AddSourceOperationLayout(
        GameEventScriptBytecodeStackInstruction instruction,
        IReadOnlyList<int> argumentSlots,
        ExpressionState state,
        int count,
        ExpressionNode? expressionEntry,
        SourceContext context)
    {
        var names = instruction.Names ?? [];
        var parameterSlots = instruction.Slots ??
            (instruction.OpCode == GameEventScriptBytecodeOpCode.PredicateTest && instruction.A >= 0
                ? [instruction.A]
                : null);
        var layoutIndex = _operationLayouts.Count;
        var nameListIndex = ResolveStringListIndex(names);
        _operationLayouts.Add(CreateOperationLayout(expressionEntryAddress: -1));
        if (expressionEntry is not null)
        {
            _deferredHelperEmitters.Add(() =>
            {
                var expressionEntryAddress = EmitSourceExpressionEntry(expressionEntry, context, state);
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
                instruction.B,
                nameListIndex,
                expressionEntryAddress: expressionEntryAddress,
                count: count);
    }

    private int EmitSourceIterator(IterationSourceNode source, SourceContext context, ExpressionState state)
    {
        switch (source)
        {
            case RangeIterationSourceNode range when TryGetRangeIteratorShort(range.RangeExpression, out var from, out var to, out var step):
            {
                var iteratorSlot = AllocateSlot(state);
                var instruction = new GameEventScriptBytecodeInstruction(
                    GameEventScriptBytecodeOpCode.RangeIteratorShort,
                    dest: ToUShortOperand(iteratorSlot, "iterator slot"));
                instruction.A_I16 = from;
                instruction.B_I16 = to;
                instruction.C_I16 = step;
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

    private void AddDiagnosticLayout(
        GameEventScriptBytecodeDiagnosticKind kind,
        GameEventScriptBytecodeDiagnosticTiming timing,
        int address,
        int slot,
        string name)
    {
        if (!_emitDiagnosticLayouts)
        {
            return;
        }

        _diagnosticLayouts.Add(new GameEventScriptBytecodeDiagnosticLayout(kind, timing, address, slot, name));
    }

    private int AddSourcePipelineSelector(CollectionSelectorNode selector, bool isTerminal, SourceContext context, ExpressionState state)
    {
        var index = _pipelineSelectorPool.Count;
        var selectorData = CreateSourceSelectorData(selector, isTerminal, context);
        var pipelinePatternIndex = selectorData.DicePattern is null
            ? -1
            : AddSourcePipelinePattern(selectorData.DicePattern, context, state);
        var objectPatternIndex = selectorData.ObjectPattern is null
            ? -1
            : AddSourcePipelineObjectPattern(selectorData.ObjectPattern, context, state);
        _pipelineSelectorPool.Add(new GameEventScriptBytecodePipelineSelector(
            selectorData.Kind,
            selectorData.IdentifierSlot,
            selectorData.EdgeMode,
            selectorData.SecondaryMode,
            selectorData.Count,
            selectorData.SecondaryIdentifierSlot,
            selectorData.Flag,
            pipelinePatternIndex: pipelinePatternIndex,
            objectPatternIndex: objectPatternIndex));
        _deferredHelperEmitters.Add(() =>
        {
            var expressionEntryAddress = selectorData.Expression is null
                ? -1
                : EmitSourceExpressionEntry(selectorData.Expression, context, state);
            var secondaryExpressionEntryAddress = selectorData.SecondaryExpression is null
                ? -1
                : EmitSourceExpressionEntry(selectorData.SecondaryExpression, context, state);
            _pipelineSelectorPool[index] = new GameEventScriptBytecodePipelineSelector(
                selectorData.Kind,
                selectorData.IdentifierSlot,
                selectorData.EdgeMode,
                selectorData.SecondaryMode,
                selectorData.Count,
                selectorData.SecondaryIdentifierSlot,
                selectorData.Flag,
                expressionEntryAddress,
                secondaryExpressionEntryAddress,
                pipelinePatternIndex,
                objectPatternIndex);
        });
        return index;
    }

    private SourceSelectorData CreateSourceSelectorData(CollectionSelectorNode selector, bool isTerminal, SourceContext context)
        => selector switch
        {
            FilterSelectorNode filter => new(
                GameEventScriptBytecodePipelineSelectorKind.Filter,
                context.RequireSlot(filter.Identifier),
                filter.Predicate),

            SelectSelectorNode select => new(
                GameEventScriptBytecodePipelineSelectorKind.Select,
                context.RequireSlot(select.Identifier),
                select.Projection),

            PredicateSelectorNode predicate when isTerminal => new(
                GameEventScriptBytecodePipelineSelectorKind.Predicate,
                context.RequireSlot(predicate.Identifier),
                predicate.Predicate,
                predicate.Operator),

            SumSelectorNode sum when isTerminal => new(
                GameEventScriptBytecodePipelineSelectorKind.Sum,
                context.RequireSlot(sum.Identifier),
                sum.Projection),

            AverageSelectorNode average when isTerminal => new(
                GameEventScriptBytecodePipelineSelectorKind.Average,
                context.RequireSlot(average.Identifier),
                average.Projection),

            CountSelectorNode count when isTerminal => new(
                GameEventScriptBytecodePipelineSelectorKind.Count,
                context.RequireSlot(count.Identifier),
                count.Predicate),

            SeriesTermSelectorNode term when isTerminal => new(
                GameEventScriptBytecodePipelineSelectorKind.SeriesTerm,
                -1,
                term.IndexExpression),

            EdgeSelectorNode edge when isTerminal => new(
                GameEventScriptBytecodePipelineSelectorKind.Edge,
                string.IsNullOrEmpty(edge.Identifier) ? -1 : context.RequireSlot(edge.Identifier!),
                edge.Predicate,
                edge.Mode),

            PatternSelectorNode pattern when isTerminal => new(
                GameEventScriptBytecodePipelineSelectorKind.Pattern,
                -1,
                null,
                DicePattern: pattern.Pattern),

            ObjectMatchSelectorNode objectMatch when isTerminal => new(
                GameEventScriptBytecodePipelineSelectorKind.ObjectMatch,
                -1,
                null,
                ObjectPattern: objectMatch.Pattern),

            TakePatternSelectorNode takePattern when isTerminal => new(
                GameEventScriptBytecodePipelineSelectorKind.TakePattern,
                -1,
                null,
                DicePattern: takePattern.Pattern),

            MinSelectorNode min when isTerminal => new(
                GameEventScriptBytecodePipelineSelectorKind.Min,
                context.RequireSlot(min.Identifier),
                min.Projection),

            MaxSelectorNode max when isTerminal => new(
                GameEventScriptBytecodePipelineSelectorKind.Max,
                context.RequireSlot(max.Identifier),
                max.Projection),

            DictionarySelectorNode dictionary when isTerminal => new(
                GameEventScriptBytecodePipelineSelectorKind.Dictionary,
                context.RequireSlot(dictionary.Identifier),
                dictionary.KeyProjection,
                SecondaryExpression: dictionary.ValueProjection),

            ContainsSelectorNode contains when isTerminal => new(
                GameEventScriptBytecodePipelineSelectorKind.Contains,
                -1,
                contains.ValueExpression,
                contains.Mode),

            ChooseSelectorNode choose when isTerminal => new(
                GameEventScriptBytecodePipelineSelectorKind.Choose,
                string.IsNullOrEmpty(choose.Identifier) ? -1 : context.RequireSlot(choose.Identifier!),
                choose.Predicate,
                Count: choose.Count,
                SecondaryIdentifierSlot: string.IsNullOrEmpty(choose.WeightIdentifier) ? -1 : context.RequireSlot(choose.WeightIdentifier!),
                SecondaryExpression: choose.WeightExpression,
                Flag: choose.AtRandom),

            DrawSelectorNode draw when isTerminal => new(
                GameEventScriptBytecodePipelineSelectorKind.Draw,
                -1,
                null,
                Count: draw.Count),

            ShuffleSelectorNode when isTerminal => new(GameEventScriptBytecodePipelineSelectorKind.Shuffle, -1, null),

            SortSelectorNode sort when isTerminal => new(
                GameEventScriptBytecodePipelineSelectorKind.Sort,
                -1,
                null,
                sort.Direction),

            DistinctSelectorNode distinct when isTerminal => new(
                GameEventScriptBytecodePipelineSelectorKind.Distinct,
                string.IsNullOrEmpty(distinct.Identifier) ? -1 : context.RequireSlot(distinct.Identifier!),
                distinct.Projection),

            GroupBySelectorNode groupBy when isTerminal => new(
                GameEventScriptBytecodePipelineSelectorKind.GroupBy,
                context.RequireSlot(groupBy.Identifier),
                groupBy.Projection),

            OrderBySelectorNode orderBy when isTerminal => new(
                GameEventScriptBytecodePipelineSelectorKind.OrderBy,
                context.RequireSlot(orderBy.Identifier),
                orderBy.Projection,
                orderBy.Direction),

            ReverseSelectorNode when isTerminal => new(GameEventScriptBytecodePipelineSelectorKind.Reverse, -1, null),

            SequenceSliceSelectorNode slice when isTerminal => new(
                GameEventScriptBytecodePipelineSelectorKind.SequenceSlice,
                -1,
                null,
                slice.Operation,
                SecondaryMode: slice.Scope,
                Count: slice.Count),

            _ => throw new GameEventScriptCompileException($"GameEventScript bytecode lowerer does not support selector node '{selector.GetType().Name}'.")
        };

    private int AddSourcePipelinePattern(DicePatternNode pattern, SourceContext context, ExpressionState state)
    {
        var index = _pipelinePatternPool.Count;
        switch (pattern)
        {
            case DiceCountPatternNode count:
                _pipelinePatternPool.Add(new GameEventScriptBytecodePipelinePattern(
                    GameEventScriptBytecodePipelinePatternKind.Count,
                    count.Count));
                if (count.Face is not null)
                {
                    _deferredHelperEmitters.Add(() =>
                    {
                        var faceEntryAddress = EmitSourceExpressionEntry(count.Face, context, state);
                        _pipelinePatternPool[index] = new GameEventScriptBytecodePipelinePattern(
                            GameEventScriptBytecodePipelinePatternKind.Count,
                            count.Count,
                            faceEntryAddress);
                    });
                }

                break;

            case DiceFullHousePatternNode:
                _pipelinePatternPool.Add(new GameEventScriptBytecodePipelinePattern(GameEventScriptBytecodePipelinePatternKind.FullHouse));
                break;

            case DiceStraightPatternNode:
                _pipelinePatternPool.Add(new GameEventScriptBytecodePipelinePattern(GameEventScriptBytecodePipelinePatternKind.Straight));
                break;

            default:
                throw new GameEventScriptCompileException($"GameEventScript bytecode lowerer does not support dice pattern '{pattern.GetType().Name}'.");
        }

        return index;
    }

    private int AddSourcePipelineObjectPattern(ObjectMatchPatternNode pattern, SourceContext context, ExpressionState state)
    {
        var index = _pipelineObjectPatternPool.Count;
        _pipelineObjectPatternPool.Add(new GameEventScriptBytecodePipelineObjectPattern([]));

        var entries = new GameEventScriptBytecodePipelineObjectPatternEntry[pattern.Entries.Count];
        var expressionEntries = new List<(int EntryIndex, ObjectMatchExpressionValueNode Value)>();
        for (var entryIndex = 0; entryIndex < pattern.Entries.Count; entryIndex++)
        {
            var entry = pattern.Entries[entryIndex];
            switch (entry.Value)
            {
                case ObjectMatchExpressionValueNode expression:
                    entries[entryIndex] = new GameEventScriptBytecodePipelineObjectPatternEntry(
                        entry.Key,
                        GameEventScriptBytecodePipelineObjectPatternValueKind.Expression);
                    expressionEntries.Add((entryIndex, expression));
                    break;

                case ObjectMatchNestedValueNode nested:
                    entries[entryIndex] = new GameEventScriptBytecodePipelineObjectPatternEntry(
                        entry.Key,
                        GameEventScriptBytecodePipelineObjectPatternValueKind.Nested,
                        nestedPatternIndex: AddSourcePipelineObjectPattern(nested.Pattern, context, state));
                    break;
            }
        }

        _pipelineObjectPatternPool[index] = new GameEventScriptBytecodePipelineObjectPattern(entries);
        _deferredHelperEmitters.Add(() =>
        {
            var updatedEntries = entries.ToArray();
            foreach (var expressionEntry in expressionEntries)
            {
                var original = updatedEntries[expressionEntry.EntryIndex];
                updatedEntries[expressionEntry.EntryIndex] = new GameEventScriptBytecodePipelineObjectPatternEntry(
                    original.Key,
                    GameEventScriptBytecodePipelineObjectPatternValueKind.Expression,
                    EmitSourceExpressionEntry(expressionEntry.Value.Expression, context, state));
            }

            _pipelineObjectPatternPool[index] = new GameEventScriptBytecodePipelineObjectPattern(updatedEntries);
        });
        return index;
    }

    private int AddSourcePipeline(CollectionAccessExpressionNode expression, SourceContext context, ExpressionState state)
    {
        var selectors = new List<CollectionSelectorNode>();
        ExpressionNode source = expression;
        while (source is CollectionAccessExpressionNode collectionAccess)
        {
            selectors.Add(collectionAccess.Selector);
            source = collectionAccess.Target;
        }

        selectors.Reverse();
        var sourceSlot = EmitSourceExpression(source, context, state);
        var prefixSelectorIndexes = new int[Math.Max(0, selectors.Count - 1)];
        for (var index = 0; index < prefixSelectorIndexes.Length; index++)
        {
            prefixSelectorIndexes[index] = AddSourcePipelineSelector(selectors[index], isTerminal: false, context, state);
        }

        var terminalSelectorIndex = AddSourcePipelineSelector(selectors[^1], isTerminal: true, context, state);
        var layoutIndex = _pipelinePool.Count;
        _pipelinePool.Add(new GameEventScriptBytecodePipeline(
            sourceSlot,
            prefixSelectorIndexes,
            terminalSelectorIndex));
        return layoutIndex;
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
        int c = 0)
    {
        var dest = AllocateSlot(state);
        Emit(CreateInstruction(opCode, dest: dest, a: a, b: b, c: c));
        return dest;
    }

    private int EmitCastSlot(int destinationSlot, int sourceSlot, string? typeName)
    {
        var opCode = ResolveCastOpCode(typeName, out var nameIndex);
        return Emit(CreateInstruction(opCode, dest: destinationSlot, a: sourceSlot, c: nameIndex < 0 ? 0 : nameIndex));
    }

    private int AllocateSlot(ExpressionState state)
    {
        var slot = state.Allocate();
        _maxFrameSlots = Math.Max(_maxFrameSlots, state.NextSlot);
        _currentFrameSlotCount = Math.Max(_currentFrameSlotCount, state.NextSlot);
        return slot;
    }

    private int Emit(GameEventScriptBytecodeInstruction instruction)
    {
        var address = _code.Count;
        _code.Add(instruction);
        return address;
    }

    private void PatchTarget(int address, int target)
        => _code[address] = _code[address] with { A_U16 = ToUShortOperand(target, "target address") };

    private void PatchTargets(int address, int target, int target2)
        => _code[address] = _code[address] with
        {
            A_U16 = ToUShortOperand(target, "target address"),
            B_U16 = ToUShortOperand(target2, "target address")
        };

    private void PatchB(int address, int value)
        => _code[address] = _code[address] with { B_U16 = ToUShortOperand(value, "operand") };

    private void PatchC(int address, int value)
        => _code[address] = _code[address] with { C_U16 = ToUShortOperand(value, "operand") };

    private int ResolveStringIndex(string? value)
        => value is null
            ? -1
            : _resolveStringIndex(value);

    private int ResolveStringListIndex(IReadOnlyList<string> values)
    {
        if (values.Count == 0)
        {
            return -1;
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
            return -1;
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
        => new(
            opCode,
            ToUShortOperand(dest, "destination operand"),
            ToUShortOperand(a, "A operand"),
            ToUShortOperand(b, "B operand"),
            ToUShortOperand(c, "C operand"),
            ToUShortOperand(d, "D operand"),
            unitAndFlags);

    private static string FormatSignature(string name, IReadOnlyList<string> labels)
        => GameEventScriptMessageSignature.CreateSignatureId(name, labels);

    private int EmitLoadInteger(
        ExpressionState state,
        long value,
        byte unitAndFlags)
    {
        var dest = AllocateSlot(state);
        Emit(new GameEventScriptBytecodeInstruction(
            GameEventScriptBytecodeOpCode.LoadInteger,
            dest: ToUShortOperand(dest, "destination operand"),
            unitAndFlags: unitAndFlags)
        {
            I64 = value
        });
        return dest;
    }

    private int EmitLoadFloat(
        ExpressionState state,
        double value,
        byte unitAndFlags)
    {
        var dest = AllocateSlot(state);
        Emit(new GameEventScriptBytecodeInstruction(
            GameEventScriptBytecodeOpCode.LoadFloat,
            dest: ToUShortOperand(dest, "destination operand"),
            unitAndFlags: unitAndFlags)
        {
            F64 = value
        });
        return dest;
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
            null => (byte)GameEventScriptBytecodeInstructionUnit.None,
            GameEventScriptNumericUnit.Degree => (byte)GameEventScriptBytecodeInstructionUnit.Degree,
            GameEventScriptNumericUnit.Meter => (byte)GameEventScriptBytecodeInstructionUnit.Meter,
            GameEventScriptNumericUnit.Second => (byte)GameEventScriptBytecodeInstructionUnit.Second,
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unknown GameEventScript numeric unit.")
        };

    private GameEventScriptBytecodeOpCode ResolveCastOpCode(string? typeName, out int nameIndex)
    {
        if (!string.IsNullOrEmpty(typeName) &&
            TryGetCastOpCode(typeName, out var opCode))
        {
            nameIndex = -1;
            return opCode;
        }

        nameIndex = ResolveStringIndex(typeName);
        return GameEventScriptBytecodeOpCode.CastCustom;
    }

    private GameEventScriptBytecodeOpCode ResolveTypeCheckOpCode(string typeName, out int nameIndex)
    {
        if (TryGetTypeCheckOpCode(typeName, out var opCode))
        {
            nameIndex = -1;
            return opCode;
        }

        nameIndex = ResolveStringIndex(typeName);
        return GameEventScriptBytecodeOpCode.TypeCheckCustom;
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

    private static GameEventScriptBytecodeOpCode ToUnaryOpCode(string operation)
        => operation switch
        {
            "-" => GameEventScriptBytecodeOpCode.UnaryNegate,
            "!" => GameEventScriptBytecodeOpCode.UnaryNot,
            "has value" => GameEventScriptBytecodeOpCode.UnaryHasValue,
            "empty" => GameEventScriptBytecodeOpCode.UnaryEmpty,
            "len" => GameEventScriptBytecodeOpCode.UnaryLength,
            "chance" => GameEventScriptBytecodeOpCode.UnaryChance,
            "keys" => GameEventScriptBytecodeOpCode.UnaryKeys,
            "values" => GameEventScriptBytecodeOpCode.UnaryValues,
            "entries" => GameEventScriptBytecodeOpCode.UnaryEntries,
            "abs" => GameEventScriptBytecodeOpCode.UnaryAbs,
            "ln" => GameEventScriptBytecodeOpCode.UnaryNaturalLog,
            _ => throw new GameEventScriptCompileException($"GameEventScript bytecode lowerer does not support unary operator '{operation}'.")
        };

    private static bool TryGetCastOpCode(string typeName, out GameEventScriptBytecodeOpCode opCode)
    {
        opCode = typeName switch
        {
            "nothing" => GameEventScriptBytecodeOpCode.CastNothing,
            "boolean" => GameEventScriptBytecodeOpCode.CastBoolean,
            "integer" => GameEventScriptBytecodeOpCode.CastInteger,
            "float" => GameEventScriptBytecodeOpCode.CastFloat,
            "number" => GameEventScriptBytecodeOpCode.CastNumber,
            "percentage" => GameEventScriptBytecodeOpCode.CastPercentage,
            "degree" => GameEventScriptBytecodeOpCode.CastDegree,
            "meter" => GameEventScriptBytecodeOpCode.CastMeter,
            "second" => GameEventScriptBytecodeOpCode.CastSecond,
            "vector" => GameEventScriptBytecodeOpCode.CastVector,
            "point" => GameEventScriptBytecodeOpCode.CastPoint,
            "uuid" => GameEventScriptBytecodeOpCode.CastUuid,
            "sequence" => GameEventScriptBytecodeOpCode.CastSequence,
            "series" => GameEventScriptBytecodeOpCode.CastSeries,
            "envelope" => GameEventScriptBytecodeOpCode.CastEnvelope,
            "ref" => GameEventScriptBytecodeOpCode.CastRef,
            "tag" => GameEventScriptBytecodeOpCode.CastTag,
            "text" => GameEventScriptBytecodeOpCode.CastText,
            "list" => GameEventScriptBytecodeOpCode.CastList,
            "range" => GameEventScriptBytecodeOpCode.CastRange,
            "message" => GameEventScriptBytecodeOpCode.CastMessage,
            "handler" => GameEventScriptBytecodeOpCode.CastHandler,
            "dictionary" => GameEventScriptBytecodeOpCode.CastDictionary,
            "set" => GameEventScriptBytecodeOpCode.CastSet,
            "dice" => GameEventScriptBytecodeOpCode.CastDice,
            "optional" => GameEventScriptBytecodeOpCode.CastOptional,
            _ => default
        };

        return typeName is "nothing" or "boolean" or "integer" or "float" or "number" or "percentage" or "degree" or "meter" or "second" or "vector" or "point" or "uuid" or "sequence" or "series" or "envelope" or "ref" or "tag" or "text" or "list" or "range" or "message" or "handler" or "dictionary" or "set" or "dice" or "optional";
    }

    private static bool TryGetTypeCheckOpCode(string typeName, out GameEventScriptBytecodeOpCode opCode)
    {
        opCode = typeName switch
        {
            "nothing" => GameEventScriptBytecodeOpCode.TypeCheckNothing,
            "tag" => GameEventScriptBytecodeOpCode.TypeCheckTag,
            "text" => GameEventScriptBytecodeOpCode.TypeCheckText,
            "percentage" => GameEventScriptBytecodeOpCode.TypeCheckPercentage,
            "degree" => GameEventScriptBytecodeOpCode.TypeCheckDegree,
            "meter" => GameEventScriptBytecodeOpCode.TypeCheckMeter,
            "second" => GameEventScriptBytecodeOpCode.TypeCheckSecond,
            "vector" => GameEventScriptBytecodeOpCode.TypeCheckVector,
            "point" => GameEventScriptBytecodeOpCode.TypeCheckPoint,
            "float" => GameEventScriptBytecodeOpCode.TypeCheckFloat,
            "integer" => GameEventScriptBytecodeOpCode.TypeCheckInteger,
            "boolean" => GameEventScriptBytecodeOpCode.TypeCheckBoolean,
            "uuid" => GameEventScriptBytecodeOpCode.TypeCheckUuid,
            "optional" => GameEventScriptBytecodeOpCode.TypeCheckOptional,
            "sequence" => GameEventScriptBytecodeOpCode.TypeCheckSequence,
            "series" => GameEventScriptBytecodeOpCode.TypeCheckSeries,
            "envelope" => GameEventScriptBytecodeOpCode.TypeCheckEnvelope,
            "list" => GameEventScriptBytecodeOpCode.TypeCheckList,
            "range" => GameEventScriptBytecodeOpCode.TypeCheckRange,
            "message" => GameEventScriptBytecodeOpCode.TypeCheckMessage,
            "handler" => GameEventScriptBytecodeOpCode.TypeCheckHandler,
            "ref" => GameEventScriptBytecodeOpCode.TypeCheckRef,
            "dictionary" => GameEventScriptBytecodeOpCode.TypeCheckDictionary,
            "set" => GameEventScriptBytecodeOpCode.TypeCheckSet,
            "dice" => GameEventScriptBytecodeOpCode.TypeCheckDice,
            _ => default
        };

        return typeName is "nothing" or "tag" or "text" or "percentage" or "degree" or "meter" or "second" or "vector" or "point" or "float" or "integer" or "boolean" or "uuid" or "optional" or "sequence" or "series" or "envelope" or "list" or "range" or "message" or "handler" or "ref" or "dictionary" or "set" or "dice";
    }

    private static GameEventScriptBytecodeOpCode ToBinaryOpCode(BinaryExpressionNode expression)
        => ShouldPreferPrimitiveIntegerOp(expression)
            ? ToPrimitiveIntegerOpCode(expression.Operator)
            : ToBinaryOpCode(expression.Operator);

    private static bool ShouldPreferPrimitiveIntegerOp(BinaryExpressionNode expression)
        => expression.Operator switch
        {
            "div" or "mod" or "rem" => IsIntegerCandidate(expression.Left) || IsIntegerCandidate(expression.Right),
            "=" or "==" or "<>" or "<" or ">" or "<=" or ">=" => IsIntegerCandidate(expression.Left) && IsIntegerCandidate(expression.Right),
            "+" or "-" or "*" or "/" => IsIntegerCandidate(expression.Left) &&
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

    private static GameEventScriptBytecodeOpCode ToPrimitiveIntegerOpCode(string operation)
        => operation switch
        {
            "=" or "==" => GameEventScriptBytecodeOpCode.PrimitiveIntegerEqual,
            "<>" => GameEventScriptBytecodeOpCode.PrimitiveIntegerNotEqual,
            "<" => GameEventScriptBytecodeOpCode.PrimitiveIntegerLess,
            ">" => GameEventScriptBytecodeOpCode.PrimitiveIntegerGreater,
            "<=" => GameEventScriptBytecodeOpCode.PrimitiveIntegerLessOrEqual,
            ">=" => GameEventScriptBytecodeOpCode.PrimitiveIntegerGreaterOrEqual,
            "+" => GameEventScriptBytecodeOpCode.PrimitiveIntegerAdd,
            "-" => GameEventScriptBytecodeOpCode.PrimitiveIntegerSubtract,
            "*" => GameEventScriptBytecodeOpCode.PrimitiveIntegerMultiply,
            "/" => GameEventScriptBytecodeOpCode.PrimitiveIntegerDivide,
            "div" => GameEventScriptBytecodeOpCode.PrimitiveIntegerFloorDivide,
            "mod" => GameEventScriptBytecodeOpCode.PrimitiveIntegerModulo,
            "rem" => GameEventScriptBytecodeOpCode.PrimitiveIntegerRemainder,
            _ => ToBinaryOpCode(operation)
        };

    private static GameEventScriptBytecodeOpCode ToBinaryOpCode(string operation)
        => operation switch
        {
            "|" => GameEventScriptBytecodeOpCode.Or,
            "xor" => GameEventScriptBytecodeOpCode.Xor,
            "&" => GameEventScriptBytecodeOpCode.And,
            "->" => GameEventScriptBytecodeOpCode.ShortCircuitImplies,
            "=" or "==" => GameEventScriptBytecodeOpCode.Equal,
            "<>" => GameEventScriptBytecodeOpCode.NotEqual,
            "=~" => GameEventScriptBytecodeOpCode.ApproxEqual,
            "<" => GameEventScriptBytecodeOpCode.Less,
            ">" => GameEventScriptBytecodeOpCode.Greater,
            "<=" => GameEventScriptBytecodeOpCode.LessOrEqual,
            ">=" => GameEventScriptBytecodeOpCode.GreaterOrEqual,
            "+" => GameEventScriptBytecodeOpCode.Add,
            "-" => GameEventScriptBytecodeOpCode.Subtract,
            "*" => GameEventScriptBytecodeOpCode.Multiply,
            "/" => GameEventScriptBytecodeOpCode.Divide,
            "div" => GameEventScriptBytecodeOpCode.IntegerDivide,
            "mod" => GameEventScriptBytecodeOpCode.Modulo,
            "rem" => GameEventScriptBytecodeOpCode.Remainder,
            "^" => GameEventScriptBytecodeOpCode.Power,
            "default" => GameEventScriptBytecodeOpCode.Default,
            "in" => GameEventScriptBytecodeOpCode.Contains,
            "value in" => GameEventScriptBytecodeOpCode.ContainsValue,
            "starts with" => GameEventScriptBytecodeOpCode.StartsWith,
            "ends with" => GameEventScriptBytecodeOpCode.EndsWith,
            "intersect" => GameEventScriptBytecodeOpCode.Intersect,
            "combine" or "merge" => GameEventScriptBytecodeOpCode.Combine,
            "except" => GameEventScriptBytecodeOpCode.Except,
            "zip" => GameEventScriptBytecodeOpCode.Zip,
            _ => throw new GameEventScriptCompileException($"GameEventScript bytecode lowerer does not support binary operator '{operation}'.")
        };

    private sealed record SourceSelectorData(
        GameEventScriptBytecodePipelineSelectorKind Kind,
        int IdentifierSlot,
        ExpressionNode? Expression,
        string? EdgeMode = null,
        string? SecondaryMode = null,
        int Count = 0,
        int SecondaryIdentifierSlot = -1,
        ExpressionNode? SecondaryExpression = null,
        bool Flag = false,
        DicePatternNode? DicePattern = null,
        ObjectMatchPatternNode? ObjectPattern = null);

    private sealed class SourceContext(IReadOnlyDictionary<string, int> slots, int? temporaryBaseSlot = null)
    {
        private readonly IReadOnlyDictionary<string, int> _slots = slots;

        public IReadOnlyDictionary<string, int> Slots => _slots;

        public int SlotCount { get; } = Math.Max(slots.Count, temporaryBaseSlot ?? 0);

        public int RequireSlot(string name)
        {
            if (!_slots.TryGetValue(name, out var slot))
            {
                throw new InvalidOperationException($"Missing GameEventScript bytecode local slot '{name}'.");
            }

            return slot;
        }
    }

    private sealed class ExpressionState(int nextSlot)
    {
        public int BaseSlot { get; } = Math.Max(0, nextSlot);

        public int NextSlot { get; private set; } = Math.Max(0, nextSlot);

        public int Allocate()
            => NextSlot++;
    }
}
