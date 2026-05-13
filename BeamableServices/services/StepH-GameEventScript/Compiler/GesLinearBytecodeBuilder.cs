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
            _currentFrameSlotCount = GetSlotCount(handler.Slots);
            _maxFrameSlots = Math.Max(_maxFrameSlots, _currentFrameSlotCount);
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

            Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.ReturnVoid));
            FlushDeferredHelpers();
            PatchReserveSlots(reserveSlotsAddress, _currentFrameSlotCount);
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
                A_U16 = ToUShortOperand(callable.EntryAddress, "callable entry address")
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
        var callableSlotCount = Math.Max(callable.Parameters.Count + 1, slots.Count);
        _currentFrameSlotCount = callableSlotCount;
        _maxFrameSlots = Math.Max(_maxFrameSlots, callableSlotCount);

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
        Emit(CreateInstruction(GameEventScriptBytecodeOpCode.ReturnValue, a: result));
        FlushDeferredHelpers();
        callableSlotCount = Math.Max(callableSlotCount, _currentFrameSlotCount);
        PatchReserveSlots(reserveSlotsAddress, callableSlotCount);
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

    private int EmitSourceExpressionEntry(ExpressionNode expression, SourceContext context, ExpressionState state)
    {
        var entry = _code.Count;
        var reserveSlotsAddress = EmitReserveSlots(0);
        var result = EmitSourceExpression(expression, context, state);
        _maxFrameSlots = Math.Max(_maxFrameSlots, state.NextSlot);
        _currentFrameSlotCount = Math.Max(_currentFrameSlotCount, state.NextSlot);
        Emit(CreateInstruction(GameEventScriptBytecodeOpCode.ReturnValue, a: result));
        PatchReserveSlots(reserveSlotsAddress, state.NextSlot);
        return entry;
    }

    private int EmitReserveSlots(int count)
        => Emit(CreateInstruction(GameEventScriptBytecodeOpCode.ReserveSlots, a: count));

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
            A_U16 = ToUShortOperand(Math.Max(1, count), "entry frame slot count")
        };
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
        var jumpToElse = Emit(CreateInstruction(GameEventScriptBytecodeOpCode.JumpIfNotTrue, c: condition));
        EmitSourceStatements(ifStatement.ThenBody.Statements, ifStatement.ThenBody.IsBlock, context.Slots);
        if (ifStatement.ElseBody is null)
        {
            PatchTarget(jumpToElse, _code.Count);
        }
        else
        {
            var jumpToEnd = Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.Jump));
            PatchTarget(jumpToElse, _code.Count);
            EmitSourceStatements(ifStatement.ElseBody.Statements, ifStatement.ElseBody.IsBlock, context.Slots);
            PatchTarget(jumpToEnd, _code.Count);
        }
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

        var nameListIndex = ResolveStringListIndex(names);
        var valueSlotListIndex = ResolveSlotListIndex(valueSlots);
        return EmitValueInstruction(
            state,
            GameEventScriptBytecodeOpCode.BuildDictionary,
            a: nameListIndex,
            b: valueSlotListIndex);
    }

    private int EmitSourceVariadic(VariadicTaggedExpressionNode variadic, SourceContext context, ExpressionState state)
    {
        var argumentSlots = new int[variadic.Arguments.Count];
        for (var index = 0; index < variadic.Arguments.Count; index++)
        {
            argumentSlots[index] = EmitSourceExpression(variadic.Arguments[index], context, state);
        }

        var operatorIndex = ResolveStringIndex(variadic.Operator);
        var argumentSlotListIndex = ResolveSlotListIndex(argumentSlots);
        return EmitValueInstruction(
            state,
            GameEventScriptBytecodeOpCode.Variadic,
            a: operatorIndex,
            b: argumentSlotListIndex);
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
        var destinationSlot = AllocateSlot(state);
        var address = Emit(CreateInstruction(
            GameEventScriptBytecodeOpCode.CallPredicate,
            dest: destinationSlot,
            b: ResolveSlotListIndex([input])));
        _deferredCallableAddressPatches.Add((address, callable.Name));
        return destinationSlot;
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

        var argumentSlots = new int[call.Arguments.Count];
        for (var argumentIndex = 0; argumentIndex < call.Arguments.Count; argumentIndex++)
        {
            argumentSlots[argumentIndex] = EmitSourceExpression(call.Arguments[argumentIndex], context, state);
        }

        var destinationSlot = AllocateSlot(state);
        var opCode = called.Kind == GameEventScriptCallableKind.PredicateCall
            ? GameEventScriptBytecodeOpCode.CallPredicate
            : GameEventScriptBytecodeOpCode.Call;
        var address = Emit(CreateInstruction(
            opCode,
            dest: destinationSlot,
            b: ResolveSlotListIndex(argumentSlots)));
        _deferredCallableAddressPatches.Add((address, called.Name));
        return destinationSlot;
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
                context.RequireSlot(filter.Identifier),
                filter.Predicate,
                PipelineIteratorEntryKind.Filter,
                context,
                state),
            SelectSelectorNode select => EmitPipelineIterator(
                sourceIteratorSlot,
                context.RequireSlot(select.Identifier),
                select.Projection,
                PipelineIteratorEntryKind.Select,
                context,
                state),
            _ => throw new GameEventScriptCompileException($"GameEventScript bytecode lowerer does not support non-terminal selector node '{selector.GetType().Name}'.")
        };

    private int EmitPipelineIterator(
        int sourceIteratorSlot,
        int itemSlot,
        ExpressionNode expression,
        PipelineIteratorEntryKind kind,
        SourceContext context,
        ExpressionState state)
    {
        var iteratorSlot = AllocateSlot(state);
        var instructionAddress = Emit(CreateInstruction(
            GameEventScriptBytecodeOpCode.PipelineIterator,
            dest: iteratorSlot,
            a: sourceIteratorSlot,
            c: itemSlot));
        _deferredHelperEmitters.Add(() =>
        {
            var entryAddress = kind == PipelineIteratorEntryKind.Filter
                ? EmitPipelineFilterEntry(itemSlot, expression, context, state)
                : EmitSourceExpressionEntry(expression, context, state);
            PatchB(instructionAddress, entryAddress);
        });
        return iteratorSlot;
    }

    private int EmitPipelineFilterEntry(int itemSlot, ExpressionNode predicate, SourceContext context, ExpressionState state)
    {
        var entry = _code.Count;
        var reserveSlotsAddress = EmitReserveSlots(0);
        var predicateSlot = EmitSourceExpression(predicate, context, state);
        var skipYield = Emit(CreateInstruction(GameEventScriptBytecodeOpCode.JumpIfNotTrue, c: predicateSlot));
        Emit(CreateInstruction(GameEventScriptBytecodeOpCode.ReturnValue, a: itemSlot));
        PatchTarget(skipYield, _code.Count);
        Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.ReturnVoid));
        _maxFrameSlots = Math.Max(_maxFrameSlots, state.NextSlot);
        _currentFrameSlotCount = Math.Max(_currentFrameSlotCount, state.NextSlot);
        PatchReserveSlots(reserveSlotsAddress, state.NextSlot);
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
                    EmitPipelineIterator(iteratorSlot, context.RequireSlot(select.Identifier), select.Projection, PipelineIteratorEntryKind.Select, context, state),
                    isSet: false,
                    state);

            case FilterSelectorNode filter:
                return EmitPipelineCollect(
                    EmitPipelineIterator(iteratorSlot, context.RequireSlot(filter.Identifier), filter.Predicate, PipelineIteratorEntryKind.Filter, context, state),
                    isSet: false,
                    state);

            case PredicateSelectorNode predicate:
            {
                var predicateIterator = EmitPipelineIterator(
                    iteratorSlot,
                    context.RequireSlot(predicate.Identifier),
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
                        context.RequireSlot(edge.Identifier!),
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
                    context.RequireSlot(count.Identifier),
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
                    context.RequireSlot(sum.Identifier),
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
                    context.RequireSlot(average.Identifier),
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

            case DictionarySelectorNode dictionary:
                return EmitPipelineDictionary(
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

    private int EmitPipelineCollect(int iteratorSlot, bool isSet, ExpressionState state)
        => EmitValueInstruction(
            state,
            isSet ? GameEventScriptBytecodeOpCode.PipelineCollectSet : GameEventScriptBytecodeOpCode.PipelineCollectList,
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
        _deferredHelperEmitters.Add(() =>
        {
            var entryAddress = EmitSourceExpressionEntry(expression, context, state);
            PatchC(instructionAddress, entryAddress);
        });
        return resultSlot;
    }

    private int EmitPipelineDictionary(
        int iteratorSlot,
        int itemSlot,
        ExpressionNode keyProjection,
        ExpressionNode? valueProjection,
        SourceContext context,
        ExpressionState state)
    {
        var resultSlot = AllocateSlot(state);
        var instructionAddress = Emit(CreateInstruction(
            valueProjection is null ? GameEventScriptBytecodeOpCode.PipelineDictionary : GameEventScriptBytecodeOpCode.PipelineDictionaryValue,
            dest: resultSlot,
            a: iteratorSlot,
            b: itemSlot));
        _deferredHelperEmitters.Add(() =>
        {
            var keyEntryAddress = EmitSourceExpressionEntry(keyProjection, context, state);
            PatchC(instructionAddress, keyEntryAddress);
            if (valueProjection is not null)
            {
                var valueEntryAddress = EmitSourceExpressionEntry(valueProjection, context, state);
                PatchD(instructionAddress, valueEntryAddress);
            }
        });
        return resultSlot;
    }

    private int EmitPipelineDistinct(int iteratorSlot, DistinctSelectorNode distinct, SourceContext context, ExpressionState state)
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

    private int EmitPipelineChoose(int iteratorSlot, ChooseSelectorNode choose, SourceContext context, ExpressionState state)
    {
        if (choose.Predicate is not null && !string.IsNullOrEmpty(choose.Identifier))
        {
            iteratorSlot = EmitPipelineIterator(
                iteratorSlot,
                context.RequireSlot(choose.Identifier!),
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
        _deferredHelperEmitters.Add(() =>
        {
            var entryAddress = EmitSourceExpressionEntry(choose.WeightExpression, context, state);
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
        _deferredHelperEmitters.Add(() =>
        {
            var entryAddress = EmitPipelineCountReducerEntry(resultSlot, state);
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
        _deferredHelperEmitters.Add(() =>
        {
            var entryAddress = EmitPipelineAddReducerEntry(resultSlot, itemSlot, state);
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
        _deferredHelperEmitters.Add(() =>
        {
            var entryAddress = EmitPipelineAverageReducerEntry(resultSlot, itemSlot, countSlot, state);
            PatchD(instructionAddress, entryAddress);
        });

        var hasNoItems = EmitValueInstruction(state, GameEventScriptBytecodeOpCode.Equal, countSlot, zeroSlot);
        var jumpToDivide = Emit(CreateInstruction(GameEventScriptBytecodeOpCode.JumpIfFalse, c: hasNoItems));
        Emit(CreateInstruction(GameEventScriptBytecodeOpCode.LoadNothing, dest: resultSlot));
        var jumpToEnd = Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.Jump));
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
        _deferredHelperEmitters.Add(() =>
        {
            var entryAddress = EmitPipelineExtremaReducerEntry(resultSlot, itemSlot, identifierSlot, projection, isMax, context, state);
            PatchC(instructionAddress, entryAddress);
        });
        return resultSlot;
    }

    private int EmitPipelineAddReducerEntry(int accumulatorSlot, int itemSlot, ExpressionState state)
    {
        var entry = _code.Count;
        var reserveSlotsAddress = EmitReserveSlots(0);
        var sumSlot = EmitValueInstruction(state, GameEventScriptBytecodeOpCode.Add, accumulatorSlot, itemSlot);
        Emit(CreateInstruction(GameEventScriptBytecodeOpCode.ReturnValue, a: sumSlot));
        PatchReserveSlots(reserveSlotsAddress, state.NextSlot);
        return entry;
    }

    private int EmitPipelineCountReducerEntry(int accumulatorSlot, ExpressionState state)
    {
        var entry = _code.Count;
        var reserveSlotsAddress = EmitReserveSlots(0);
        var oneSlot = EmitLoadInteger(state, 1L, unitAndFlags: 0);
        var nextCountSlot = EmitValueInstruction(state, GameEventScriptBytecodeOpCode.PrimitiveIntegerAdd, accumulatorSlot, oneSlot);
        Emit(CreateInstruction(GameEventScriptBytecodeOpCode.ReturnValue, a: nextCountSlot));
        PatchReserveSlots(reserveSlotsAddress, state.NextSlot);
        return entry;
    }

    private int EmitPipelineAverageReducerEntry(int accumulatorSlot, int itemSlot, int countSlot, ExpressionState state)
    {
        var entry = _code.Count;
        var reserveSlotsAddress = EmitReserveSlots(0);
        var oneSlot = EmitLoadInteger(state, 1L, unitAndFlags: 0);
        var nextCountSlot = EmitValueInstruction(state, GameEventScriptBytecodeOpCode.PrimitiveIntegerAdd, countSlot, oneSlot);
        Emit(CreateInstruction(GameEventScriptBytecodeOpCode.MoveSlot, dest: countSlot, a: nextCountSlot));
        var sumSlot = EmitValueInstruction(state, GameEventScriptBytecodeOpCode.Add, accumulatorSlot, itemSlot);
        Emit(CreateInstruction(GameEventScriptBytecodeOpCode.ReturnValue, a: sumSlot));
        PatchReserveSlots(reserveSlotsAddress, state.NextSlot);
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
        Emit(CreateInstruction(GameEventScriptBytecodeOpCode.MoveSlot, dest: identifierSlot, a: currentItemSlot));
        var currentProjectionSlot = EmitSourceExpression(projection, context, state);
        var comparisonSlot = EmitValueInstruction(
            state,
            isMax ? GameEventScriptBytecodeOpCode.Greater : GameEventScriptBytecodeOpCode.Less,
            currentProjectionSlot,
            accumulatorProjectionSlot);
        var keepAccumulatorJump = Emit(CreateInstruction(GameEventScriptBytecodeOpCode.JumpIfNotTrue, c: comparisonSlot));
        Emit(CreateInstruction(GameEventScriptBytecodeOpCode.ReturnValue, a: currentItemSlot));
        PatchTarget(keepAccumulatorJump, _code.Count);
        Emit(CreateInstruction(GameEventScriptBytecodeOpCode.ReturnValue, a: accumulatorSlot));
        PatchReserveSlots(reserveSlotsAddress, state.NextSlot);
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
                    _deferredHelperEmitters.Add(() =>
                    {
                        var entryAddress = EmitSourceExpressionEntry(count.Face, context, state);
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

    private int EmitPipelineObjectMatch(int iteratorSlot, ObjectMatchPatternNode pattern, SourceContext context, ExpressionState state)
    {
        var itemSlot = AllocateSlot(state);
        var matchIterator = AllocateSlot(state);
        var instructionAddress = Emit(CreateInstruction(
            GameEventScriptBytecodeOpCode.PipelineIterator,
            dest: matchIterator,
            a: iteratorSlot,
            c: itemSlot));
        _deferredHelperEmitters.Add(() =>
        {
            var entryAddress = EmitPipelineObjectMatchEntry(itemSlot, pattern, context, state);
            PatchB(instructionAddress, entryAddress);
        });
        return EmitValueInstruction(state, GameEventScriptBytecodeOpCode.PipelineHasAny, a: matchIterator);
    }

    private int EmitPipelineObjectMatchEntry(int itemSlot, ObjectMatchPatternNode pattern, SourceContext context, ExpressionState state)
    {
        var entry = _code.Count;
        var reserveSlotsAddress = EmitReserveSlots(0);
        var matchSlot = EmitObjectPatternPredicate(itemSlot, pattern, context, state);
        var skipYield = Emit(CreateInstruction(GameEventScriptBytecodeOpCode.JumpIfNotTrue, c: matchSlot));
        Emit(CreateInstruction(GameEventScriptBytecodeOpCode.ReturnValue, a: matchSlot));
        PatchTarget(skipYield, _code.Count);
        Emit(new GameEventScriptBytecodeInstruction(GameEventScriptBytecodeOpCode.ReturnVoid));
        PatchReserveSlots(reserveSlotsAddress, state.NextSlot);
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

    private void PatchD(int address, int value)
        => _code[address] = _code[address] with { D_U16 = ToUShortOperand(value, "operand") };

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

    private enum PipelineIteratorEntryKind
    {
        Filter,
        Select
    }

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
