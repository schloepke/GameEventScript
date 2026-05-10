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
    private readonly Func<IReadOnlyList<string>, int> _resolveNamedArgumentLayoutIndex;
    private readonly Func<GameEventScriptValue, int> _resolveConstantIndex;
    private readonly Func<GameEventScriptExtensionReference, int> _resolveExternalReferenceIndex;
    private readonly IReadOnlyDictionary<string, GesCallableDefinition> _sourceCallables;
    private readonly IReadOnlyDictionary<string, TypeDefinitionNode> _sourceTypeDefinitions;
    private readonly bool _emitDiagnosticLayouts;
    private readonly List<GameEventScriptBytecodeInstruction> _code = [];
    private readonly List<GameEventScriptBytecodeOperationLayout> _operationLayouts = [];
    private readonly List<GameEventScriptBytecodeDiagnosticLayout> _diagnosticLayouts = [];
    private readonly List<GameEventScriptBytecodePublishLayoutEntry> _publishLayouts = [];
    private readonly List<GameEventScriptBytecodeIterationSourceLayout> _iterationSourceLayouts = [];
    private readonly List<GameEventScriptBytecodeLoopLayout> _loopLayouts = [];
    private readonly List<GameEventScriptBytecodeSeededRandomBlockLayout> _seededRandomBlockLayouts = [];
    private readonly List<GameEventScriptBytecodeDicePatternLayout> _dicePatternLayouts = [];
    private readonly List<GameEventScriptBytecodeObjectMatchPatternLayout> _objectMatchPatternLayouts = [];
    private readonly List<GameEventScriptBytecodeSelectorLayout> _selectorLayouts = [];
    private readonly List<GameEventScriptBytecodePipelineLayout> _pipelineLayouts = [];
    private readonly List<GameEventScriptBytecodeGeneratedCollectionLayout> _generatedCollectionLayouts = [];
    private readonly List<GameEventScriptBytecodeGuardedChoiceLayout> _guardedChoiceLayouts = [];
    private readonly List<Action> _deferredHelperEmitters = [];
    private int _currentFrameSlotCount;
    private int _maxFrameSlots = 1;

    public GesLinearBytecodeBuilder(
        Func<string, int>? resolveStringIndex = null,
        Func<IReadOnlyList<string>, int>? resolveNamedArgumentLayoutIndex = null,
        Func<GameEventScriptValue, int>? resolveConstantIndex = null,
        Func<GameEventScriptExtensionReference, int>? resolveExternalReferenceIndex = null,
        IReadOnlyDictionary<string, GesCallableDefinition>? sourceCallables = null,
        IReadOnlyDictionary<string, TypeDefinitionNode>? sourceTypeDefinitions = null,
        bool emitDiagnosticLayouts = false)
    {
        _resolveStringIndex = resolveStringIndex ?? (_ => -1);
        _resolveNamedArgumentLayoutIndex = resolveNamedArgumentLayoutIndex ?? (_ => -1);
        _resolveConstantIndex = resolveConstantIndex ?? (_ => -1);
        _resolveExternalReferenceIndex = resolveExternalReferenceIndex ?? (_ => -1);
        _sourceCallables = sourceCallables ?? new Dictionary<string, GesCallableDefinition>(StringComparer.Ordinal);
        _sourceTypeDefinitions = sourceTypeDefinitions ?? new Dictionary<string, TypeDefinitionNode>(StringComparer.Ordinal);
        _emitDiagnosticLayouts = emitDiagnosticLayouts;
    }

    public IReadOnlyList<GameEventScriptBytecodeInstruction> Code => _code;

    public int MaxFrameSlots => _maxFrameSlots;

    public IReadOnlyList<GameEventScriptBytecodeOperationLayout> OperationLayouts => _operationLayouts;

    public IReadOnlyList<GameEventScriptBytecodeDiagnosticLayout> DiagnosticLayouts => _diagnosticLayouts;

    public IReadOnlyList<GameEventScriptBytecodePublishLayoutEntry> PublishLayouts => _publishLayouts;

    public IReadOnlyList<GameEventScriptBytecodeIterationSourceLayout> IterationSourceLayouts => _iterationSourceLayouts;

    public IReadOnlyList<GameEventScriptBytecodeLoopLayout> LoopLayouts => _loopLayouts;

    public IReadOnlyList<GameEventScriptBytecodeSeededRandomBlockLayout> SeededRandomBlockLayouts => _seededRandomBlockLayouts;

    public IReadOnlyList<GameEventScriptBytecodeDicePatternLayout> DicePatternLayouts => _dicePatternLayouts;

    public IReadOnlyList<GameEventScriptBytecodeObjectMatchPatternLayout> ObjectMatchPatternLayouts => _objectMatchPatternLayouts;

    public IReadOnlyList<GameEventScriptBytecodeSelectorLayout> SelectorLayouts => _selectorLayouts;

    public IReadOnlyList<GameEventScriptBytecodePipelineLayout> PipelineLayouts => _pipelineLayouts;

    public IReadOnlyList<GameEventScriptBytecodeGeneratedCollectionLayout> GeneratedCollectionLayouts => _generatedCollectionLayouts;

    public IReadOnlyList<GameEventScriptBytecodeGuardedChoiceLayout> GuardedChoiceLayouts => _guardedChoiceLayouts;

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
                    $"GameEventScript linear bytecode builder requires source statements for handler '{handler.SignatureId}'.");
            }

            Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.Return));
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
                $"GameEventScript linear bytecode builder requires source callable body for '{callable.SignatureId}'.");
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
            Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.BindParameter, Dest: parameterSlot, A: index));
            if (index < callable.ParameterTypes.Count && !string.IsNullOrEmpty(callable.ParameterTypes[index]))
            {
                EmitCastSlot(parameterSlot, parameterSlot, callable.ParameterTypes[index]);
            }
        }

        var state = new ExpressionState(slots.Count);
        var result = EmitSourceExpression(sourceCallable.Expression, context, state);
        callable.ReturnSlot = result;
        Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.Return, A: result));
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
        Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.Return, A: result));
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

            Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.BindParameter, Dest: slot, A: index));
            if (index < parameterTypes.Count && !string.IsNullOrEmpty(parameterTypes[index]))
            {
                EmitCastSlot(slot, slot, parameterTypes[index]);
            }
        }
    }

    private void EmitSourceStatements(
        IReadOnlyList<StatementNode> statements,
        bool createsScope,
        IReadOnlyDictionary<string, int> slots)
    {
        if (createsScope)
        {
            Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.EnterScope));
        }

        var context = new SourceContext(slots);
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
                var diagnosticAddress = Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.CopySlot, Dest: letSlot, A: result));
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

        var publishKind = publish.Kind == PublishStatementKind.Publish
            ? GameEventScriptBytecodePublishKind.Publish
            : GameEventScriptBytecodePublishKind.Emit;
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

            var publishLayoutIndex = AddPublishLayout(new GameEventScriptBytecodePublishLayoutEntry(
                publishKind,
                GameEventScriptMessageSignature.NormalizeMessageName(message.Message),
                GameEventScriptMessageSignature.CreateSignatureId(message.Message, argumentNames),
                argumentNames,
                argumentSlots,
                tagSlots: tagSlots));
            Emit(new GameEventScriptBytecodeInstruction(
                GameEventScriptBytecodeOpCode.PublishValue,
                A: (int)publishKind,
                C: publishLayoutIndex));
            return;
        }

        var messageSlot = EmitSourceExpression(publish.MessageExpression, context, state);
        var messagePublishLayoutIndex = AddPublishLayout(new GameEventScriptBytecodePublishLayoutEntry(
            publishKind,
            messageSlot: messageSlot,
            tagSlots: tagSlots));
        Emit(new GameEventScriptBytecodeInstruction(
            GameEventScriptBytecodeOpCode.PublishMessageValue,
            A: messageSlot,
            B: (int)publishKind,
            C: messagePublishLayoutIndex));
    }

    private void EmitSourceIf(IfStatementNode ifStatement, SourceContext context)
    {
        var condition = EmitSourceExpression(ifStatement.Condition, context, new ExpressionState(context.SlotCount));
        var jumpToElse = Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.JumpIfNotTrue, C: condition));
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
        var sourceLayoutIndex = AddSourceIterationSourceLayout(forStatement.Source, context, state);
        var identifierSlot = context.RequireSlot(forStatement.Identifier);
        var loopLayoutIndex = AddLoopLayout(new GameEventScriptBytecodeLoopLayout(identifierSlot, sourceLayoutIndex));
        var opCode = forStatement.Source is RangeIterationSourceNode
            ? GameEventScriptBytecodeOpCode.ForRange
            : GameEventScriptBytecodeOpCode.ForCollection;
        var loopInstruction = Emit(new GameEventScriptBytecodeInstruction(opCode, C: loopLayoutIndex));
        var bodyAddress = _code.Count;
        EmitSourceStatements(forStatement.Body.Statements, forStatement.Body.IsBlock, context.Slots);
        PatchTargets(loopInstruction, bodyAddress, _code.Count);
    }

    private void EmitSourceSeededRandom(SeededRandomStatementNode seededRandom, SourceContext context)
    {
        var state = new ExpressionState(context.SlotCount);
        var seedSlot = EmitSourceExpression(seededRandom.SeedExpression, context, state);
        var layoutIndex = AddSeededRandomBlockLayout(new GameEventScriptBytecodeSeededRandomBlockLayout(seedSlot));
        var blockInstruction = Emit(new GameEventScriptBytecodeInstruction(
            GameEventScriptBytecodeOpCode.SeededRandomBlock,
            C: layoutIndex));
        var bodyAddress = _code.Count;
        EmitSourceStatements(seededRandom.Body.Statements, seededRandom.Body.IsBlock, context.Slots);
        PatchTargets(blockInstruction, bodyAddress, _code.Count);
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
                return EmitValueInstruction(state, GameEventScriptBytecodeOpCode.LoadSlot, a: context.RequireSlot(identifier.Name));

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
            {
                var layoutIndex = AddSourceGeneratedCollectionLayout(generatedCollection, context, state);
                return EmitValueInstruction(state, GameEventScriptBytecodeOpCode.GeneratedCollection, c: layoutIndex);
            }

            case GuardedChoiceExpressionNode guardedChoice:
            {
                var layoutIndex = AddSourceGuardedChoiceLayout(guardedChoice, context, state);
                return EmitValueInstruction(state, GameEventScriptBytecodeOpCode.GuardedChoice, c: layoutIndex);
            }

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
                return EmitValueInstruction(state, opCode, a: value, c: nameIndex);
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
        => EmitValueInstruction(state, GameEventScriptBytecodeOpCode.LoadConstant, c: _resolveConstantIndex(value));

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
            argumentSlots.Length > 0 ? argumentSlots[0] : -1,
            argumentSlots.Length > 1 ? argumentSlots[1] : -1,
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
        var seed = EmitSourceExpression(seededRandom.SeedExpression, context, state);
        var destination = AllocateSlot(state);
        var instructionAddress = Emit(new GameEventScriptBytecodeInstruction(
            GameEventScriptBytecodeOpCode.SeededRandom,
            Dest: destination,
            A: seed));
        _deferredHelperEmitters.Add(() =>
        {
            var expressionEntryAddress = EmitSourceExpressionEntry(seededRandom.BodyExpression, context, state);
            PatchC(instructionAddress, expressionEntryAddress);
        });

        return destination;
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
            Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.ShortCircuitImplies, Dest: resultSlot, A: left, B: left));
        }
        else
        {
            Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.CopySlot, Dest: resultSlot, A: left));
        }

        var branch = opCode switch
        {
            GameEventScriptBytecodeOpCode.ShortCircuitOr => GameEventScriptBytecodeOpCode.JumpIfTrue,
            GameEventScriptBytecodeOpCode.ShortCircuitAnd => GameEventScriptBytecodeOpCode.JumpIfFalse,
            _ => GameEventScriptBytecodeOpCode.JumpIfFalse
        };
        var jump = Emit(new GameEventScriptBytecodeInstruction(branch, C: left));
        var right = EmitSourceExpression(binary.Right, context, state);
        var combineOp = opCode switch
        {
            GameEventScriptBytecodeOpCode.ShortCircuitOr => GameEventScriptBytecodeOpCode.Or,
            GameEventScriptBytecodeOpCode.ShortCircuitAnd => GameEventScriptBytecodeOpCode.And,
            _ => GameEventScriptBytecodeOpCode.ShortCircuitImplies
        };
        Emit(new GameEventScriptBytecodeInstruction(combineOp, Dest: resultSlot, A: left, B: right));
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
            operandSlots[0] = EmitValueInstruction(state, GameEventScriptBytecodeOpCode.LoadSlot, a: handlerSlot);
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
        var layoutIndex = AddSourcePipelineLayout(collectionAccess, context, state);
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
            operands.Count > 0 ? operands[0] : -1,
            operands.Count > 1 ? operands[1] : -1,
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
        var namedArgumentLayoutIndex = names.Length == 0 ? -1 : _resolveNamedArgumentLayoutIndex(names);
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
                namedArgumentLayoutIndex,
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
        var namedArgumentLayoutIndex = names.Length == 0 ? -1 : _resolveNamedArgumentLayoutIndex(names);
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

    private int AddSourceIterationSourceLayout(IterationSourceNode source, SourceContext context, ExpressionState state)
    {
        var collectionSlot = -1;
        var rangeFromSlot = -1;
        var rangeToSlot = -1;
        var rangeStepSlot = -1;
        var kind = GameEventScriptBytecodeIterationSourceKind.Collection;
        switch (source)
        {
            case CollectionIterationSourceNode collection:
                collectionSlot = EmitSourceExpression(collection.Expression, context, state);
                break;

            case RangeIterationSourceNode range:
                kind = GameEventScriptBytecodeIterationSourceKind.Range;
                rangeFromSlot = EmitSourceExpression(range.RangeExpression.FromExpression, context, state);
                rangeToSlot = EmitSourceExpression(range.RangeExpression.ToExpression, context, state);
                rangeStepSlot = range.RangeExpression.StepExpression is null
                    ? -1
                    : EmitSourceExpression(range.RangeExpression.StepExpression, context, state);
                break;

            default:
                throw new GameEventScriptCompileException($"GameEventScript bytecode lowerer does not support iteration source '{source.GetType().Name}'.");
        }

        var index = _iterationSourceLayouts.Count;
        _iterationSourceLayouts.Add(new GameEventScriptBytecodeIterationSourceLayout(
            kind,
            collectionSlot,
            rangeFromSlot,
            rangeToSlot,
            rangeStepSlot));
        return index;
    }

    private int AddSourceSelectorLayout(CollectionSelectorNode selector, bool isTerminal, SourceContext context, ExpressionState state)
    {
        var index = _selectorLayouts.Count;
        var selectorData = CreateSourceSelectorData(selector, isTerminal, context);
        var dicePatternLayoutIndex = selectorData.DicePattern is null
            ? -1
            : AddSourceDicePatternLayout(selectorData.DicePattern, context, state);
        var objectMatchPatternLayoutIndex = selectorData.ObjectPattern is null
            ? -1
            : AddSourceObjectMatchPatternLayout(selectorData.ObjectPattern, context, state);
        _selectorLayouts.Add(new GameEventScriptBytecodeSelectorLayout(
            selectorData.Kind,
            selectorData.IdentifierSlot,
            selectorData.EdgeMode,
            selectorData.SecondaryMode,
            selectorData.Count,
            selectorData.SecondaryIdentifierSlot,
            selectorData.Flag,
            dicePatternLayoutIndex: dicePatternLayoutIndex,
            objectMatchPatternLayoutIndex: objectMatchPatternLayoutIndex));
        _deferredHelperEmitters.Add(() =>
        {
            var expressionEntryAddress = selectorData.Expression is null
                ? -1
                : EmitSourceExpressionEntry(selectorData.Expression, context, state);
            var secondaryExpressionEntryAddress = selectorData.SecondaryExpression is null
                ? -1
                : EmitSourceExpressionEntry(selectorData.SecondaryExpression, context, state);
            _selectorLayouts[index] = new GameEventScriptBytecodeSelectorLayout(
                selectorData.Kind,
                selectorData.IdentifierSlot,
                selectorData.EdgeMode,
                selectorData.SecondaryMode,
                selectorData.Count,
                selectorData.SecondaryIdentifierSlot,
                selectorData.Flag,
                expressionEntryAddress,
                secondaryExpressionEntryAddress,
                dicePatternLayoutIndex,
                objectMatchPatternLayoutIndex);
        });
        return index;
    }

    private SourceSelectorData CreateSourceSelectorData(CollectionSelectorNode selector, bool isTerminal, SourceContext context)
        => selector switch
        {
            FilterSelectorNode filter => new(
                GameEventScriptBytecodeSelectorKind.Filter,
                context.RequireSlot(filter.Identifier),
                filter.Predicate),

            SelectSelectorNode select => new(
                GameEventScriptBytecodeSelectorKind.Select,
                context.RequireSlot(select.Identifier),
                select.Projection),

            PredicateSelectorNode predicate when isTerminal => new(
                GameEventScriptBytecodeSelectorKind.Predicate,
                context.RequireSlot(predicate.Identifier),
                predicate.Predicate,
                predicate.Operator),

            SumSelectorNode sum when isTerminal => new(
                GameEventScriptBytecodeSelectorKind.Sum,
                context.RequireSlot(sum.Identifier),
                sum.Projection),

            AverageSelectorNode average when isTerminal => new(
                GameEventScriptBytecodeSelectorKind.Average,
                context.RequireSlot(average.Identifier),
                average.Projection),

            CountSelectorNode count when isTerminal => new(
                GameEventScriptBytecodeSelectorKind.Count,
                context.RequireSlot(count.Identifier),
                count.Predicate),

            SeriesTermSelectorNode term when isTerminal => new(
                GameEventScriptBytecodeSelectorKind.SeriesTerm,
                -1,
                term.IndexExpression),

            EdgeSelectorNode edge when isTerminal => new(
                GameEventScriptBytecodeSelectorKind.Edge,
                string.IsNullOrEmpty(edge.Identifier) ? -1 : context.RequireSlot(edge.Identifier!),
                edge.Predicate,
                edge.Mode),

            PatternSelectorNode pattern when isTerminal => new(
                GameEventScriptBytecodeSelectorKind.Pattern,
                -1,
                null,
                DicePattern: pattern.Pattern),

            ObjectMatchSelectorNode objectMatch when isTerminal => new(
                GameEventScriptBytecodeSelectorKind.ObjectMatch,
                -1,
                null,
                ObjectPattern: objectMatch.Pattern),

            TakePatternSelectorNode takePattern when isTerminal => new(
                GameEventScriptBytecodeSelectorKind.TakePattern,
                -1,
                null,
                DicePattern: takePattern.Pattern),

            MinSelectorNode min when isTerminal => new(
                GameEventScriptBytecodeSelectorKind.Min,
                context.RequireSlot(min.Identifier),
                min.Projection),

            MaxSelectorNode max when isTerminal => new(
                GameEventScriptBytecodeSelectorKind.Max,
                context.RequireSlot(max.Identifier),
                max.Projection),

            DictionarySelectorNode dictionary when isTerminal => new(
                GameEventScriptBytecodeSelectorKind.Dictionary,
                context.RequireSlot(dictionary.Identifier),
                dictionary.KeyProjection,
                SecondaryExpression: dictionary.ValueProjection),

            ContainsSelectorNode contains when isTerminal => new(
                GameEventScriptBytecodeSelectorKind.Contains,
                -1,
                contains.ValueExpression,
                contains.Mode),

            ChooseSelectorNode choose when isTerminal => new(
                GameEventScriptBytecodeSelectorKind.Choose,
                string.IsNullOrEmpty(choose.Identifier) ? -1 : context.RequireSlot(choose.Identifier!),
                choose.Predicate,
                Count: choose.Count,
                SecondaryIdentifierSlot: string.IsNullOrEmpty(choose.WeightIdentifier) ? -1 : context.RequireSlot(choose.WeightIdentifier!),
                SecondaryExpression: choose.WeightExpression,
                Flag: choose.AtRandom),

            DrawSelectorNode draw when isTerminal => new(
                GameEventScriptBytecodeSelectorKind.Draw,
                -1,
                null,
                Count: draw.Count),

            ShuffleSelectorNode when isTerminal => new(GameEventScriptBytecodeSelectorKind.Shuffle, -1, null),

            SortSelectorNode sort when isTerminal => new(
                GameEventScriptBytecodeSelectorKind.Sort,
                -1,
                null,
                sort.Direction),

            DistinctSelectorNode distinct when isTerminal => new(
                GameEventScriptBytecodeSelectorKind.Distinct,
                string.IsNullOrEmpty(distinct.Identifier) ? -1 : context.RequireSlot(distinct.Identifier!),
                distinct.Projection),

            GroupBySelectorNode groupBy when isTerminal => new(
                GameEventScriptBytecodeSelectorKind.GroupBy,
                context.RequireSlot(groupBy.Identifier),
                groupBy.Projection),

            OrderBySelectorNode orderBy when isTerminal => new(
                GameEventScriptBytecodeSelectorKind.OrderBy,
                context.RequireSlot(orderBy.Identifier),
                orderBy.Projection,
                orderBy.Direction),

            ReverseSelectorNode when isTerminal => new(GameEventScriptBytecodeSelectorKind.Reverse, -1, null),

            SequenceSliceSelectorNode slice when isTerminal => new(
                GameEventScriptBytecodeSelectorKind.SequenceSlice,
                -1,
                null,
                slice.Operation,
                SecondaryMode: slice.Scope,
                Count: slice.Count),

            _ => throw new GameEventScriptCompileException($"GameEventScript bytecode lowerer does not support selector node '{selector.GetType().Name}'.")
        };

    private int AddSourceDicePatternLayout(DicePatternNode pattern, SourceContext context, ExpressionState state)
    {
        var index = _dicePatternLayouts.Count;
        switch (pattern)
        {
            case DiceCountPatternNode count:
                _dicePatternLayouts.Add(new GameEventScriptBytecodeDicePatternLayout(
                    GameEventScriptBytecodeDicePatternKind.Count,
                    count.Count));
                if (count.Face is not null)
                {
                    _deferredHelperEmitters.Add(() =>
                    {
                        var faceEntryAddress = EmitSourceExpressionEntry(count.Face, context, state);
                        _dicePatternLayouts[index] = new GameEventScriptBytecodeDicePatternLayout(
                            GameEventScriptBytecodeDicePatternKind.Count,
                            count.Count,
                            faceEntryAddress);
                    });
                }

                break;

            case DiceFullHousePatternNode:
                _dicePatternLayouts.Add(new GameEventScriptBytecodeDicePatternLayout(GameEventScriptBytecodeDicePatternKind.FullHouse));
                break;

            case DiceStraightPatternNode:
                _dicePatternLayouts.Add(new GameEventScriptBytecodeDicePatternLayout(GameEventScriptBytecodeDicePatternKind.Straight));
                break;

            default:
                throw new GameEventScriptCompileException($"GameEventScript bytecode lowerer does not support dice pattern '{pattern.GetType().Name}'.");
        }

        return index;
    }

    private int AddSourceObjectMatchPatternLayout(ObjectMatchPatternNode pattern, SourceContext context, ExpressionState state)
    {
        var index = _objectMatchPatternLayouts.Count;
        _objectMatchPatternLayouts.Add(new GameEventScriptBytecodeObjectMatchPatternLayout([]));

        var entries = new GameEventScriptBytecodeObjectMatchEntryLayout[pattern.Entries.Count];
        var expressionEntries = new List<(int EntryIndex, ObjectMatchExpressionValueNode Value)>();
        for (var entryIndex = 0; entryIndex < pattern.Entries.Count; entryIndex++)
        {
            var entry = pattern.Entries[entryIndex];
            switch (entry.Value)
            {
                case ObjectMatchExpressionValueNode expression:
                    entries[entryIndex] = new GameEventScriptBytecodeObjectMatchEntryLayout(
                        entry.Key,
                        GameEventScriptBytecodeObjectMatchValueKind.Expression);
                    expressionEntries.Add((entryIndex, expression));
                    break;

                case ObjectMatchNestedValueNode nested:
                    entries[entryIndex] = new GameEventScriptBytecodeObjectMatchEntryLayout(
                        entry.Key,
                        GameEventScriptBytecodeObjectMatchValueKind.Nested,
                        nestedPatternLayoutIndex: AddSourceObjectMatchPatternLayout(nested.Pattern, context, state));
                    break;
            }
        }

        _objectMatchPatternLayouts[index] = new GameEventScriptBytecodeObjectMatchPatternLayout(entries);
        _deferredHelperEmitters.Add(() =>
        {
            var updatedEntries = entries.ToArray();
            foreach (var expressionEntry in expressionEntries)
            {
                var original = updatedEntries[expressionEntry.EntryIndex];
                updatedEntries[expressionEntry.EntryIndex] = new GameEventScriptBytecodeObjectMatchEntryLayout(
                    original.Key,
                    GameEventScriptBytecodeObjectMatchValueKind.Expression,
                    EmitSourceExpressionEntry(expressionEntry.Value.Expression, context, state));
            }

            _objectMatchPatternLayouts[index] = new GameEventScriptBytecodeObjectMatchPatternLayout(updatedEntries);
        });
        return index;
    }

    private int AddSourcePipelineLayout(CollectionAccessExpressionNode expression, SourceContext context, ExpressionState state)
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
            prefixSelectorIndexes[index] = AddSourceSelectorLayout(selectors[index], isTerminal: false, context, state);
        }

        var terminalSelectorIndex = AddSourceSelectorLayout(selectors[^1], isTerminal: true, context, state);
        var layoutIndex = _pipelineLayouts.Count;
        _pipelineLayouts.Add(new GameEventScriptBytecodePipelineLayout(
            sourceSlot,
            prefixSelectorIndexes,
            terminalSelectorIndex));
        return layoutIndex;
    }

    private int AddSourceGeneratedCollectionLayout(GeneratedCollectionExpressionNode generatedCollection, SourceContext context, ExpressionState state)
    {
        var sourceLayoutIndex = AddSourceIterationSourceLayout(generatedCollection.Source, context, state);
        var index = _generatedCollectionLayouts.Count;
        _generatedCollectionLayouts.Add(new GameEventScriptBytecodeGeneratedCollectionLayout(
            generatedCollection.CollectionType,
            context.RequireSlot(generatedCollection.Identifier),
            sourceLayoutIndex));
        _deferredHelperEmitters.Add(() =>
        {
            var predicateEntryAddress = generatedCollection.Predicate is null
                ? -1
                : EmitSourceExpressionEntry(generatedCollection.Predicate, context, state);
            var projectionEntryAddress = EmitSourceExpressionEntry(generatedCollection.Projection, context, state);
            _generatedCollectionLayouts[index] = new GameEventScriptBytecodeGeneratedCollectionLayout(
                generatedCollection.CollectionType,
                context.RequireSlot(generatedCollection.Identifier),
                sourceLayoutIndex,
                predicateEntryAddress,
                projectionEntryAddress);
        });
        return index;
    }

    private int AddSourceGuardedChoiceLayout(GuardedChoiceExpressionNode guardedChoice, SourceContext context, ExpressionState state)
    {
        var index = _guardedChoiceLayouts.Count;
        _guardedChoiceLayouts.Add(new GameEventScriptBytecodeGuardedChoiceLayout(
            Enumerable.Repeat(-1, guardedChoice.Branches.Count).ToArray(),
            Enumerable.Repeat(-1, guardedChoice.Branches.Count).ToArray()));
        _deferredHelperEmitters.Add(() =>
        {
            var valueEntryAddresses = new int[guardedChoice.Branches.Count];
            var conditionEntryAddresses = new int[guardedChoice.Branches.Count];
            for (var branchIndex = 0; branchIndex < guardedChoice.Branches.Count; branchIndex++)
            {
                var branch = guardedChoice.Branches[branchIndex];
                conditionEntryAddresses[branchIndex] = EmitSourceExpressionEntry(branch.ConditionExpression, context, state);
                valueEntryAddresses[branchIndex] = EmitSourceExpressionEntry(branch.ValueExpression, context, state);
            }

            var otherwiseEntryAddress = EmitSourceExpressionEntry(guardedChoice.OtherwiseExpression, context, state);
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

    private int EmitValueInstruction(
        ExpressionState state,
        GameEventScriptBytecodeOpCode opCode,
        int a = -1,
        int b = -1,
        int c = -1)
    {
        var dest = AllocateSlot(state);
        Emit(new GameEventScriptBytecodeInstruction(opCode, Dest: dest, A: a, B: b, C: c));
        return dest;
    }

    private int EmitCastSlot(int destinationSlot, int sourceSlot, string? typeName)
    {
        var opCode = ResolveCastOpCode(typeName, out var nameIndex);
        return Emit(new GameEventScriptBytecodeInstruction(opCode, Dest: destinationSlot, A: sourceSlot, C: nameIndex));
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
        => _code[address] = _code[address] with { A = target };

    private void PatchTargets(int address, int target, int target2)
        => _code[address] = _code[address] with { A = target, B = target2 };

    private void PatchC(int address, int value)
        => _code[address] = _code[address] with { C = value };

    private int ResolveStringIndex(string? value)
        => string.IsNullOrEmpty(value)
            ? -1
            : _resolveStringIndex(value);

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
        GameEventScriptBytecodeSelectorKind Kind,
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

    private sealed class SourceContext(IReadOnlyDictionary<string, int> slots)
    {
        private readonly IReadOnlyDictionary<string, int> _slots = slots;

        public IReadOnlyDictionary<string, int> Slots => _slots;

        public int SlotCount => _slots.Count;

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
