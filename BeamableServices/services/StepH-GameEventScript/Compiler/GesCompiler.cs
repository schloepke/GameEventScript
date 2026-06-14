#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptBinaryHeader;

namespace StepH.GameEventScript.Compiler;

internal static class GesCompiler
{
    public static GameEventScriptBinary Compile(GesSyntaxTreeModule module, GameEventScriptCompileOptions? options = null)
    {
        _ = module ?? throw new ArgumentNullException(nameof(module));
        var compileOptions = options ?? new GameEventScriptCompileOptions();
        return new BinaryCompiler(module, compileOptions).Build();
    }

    private sealed class BinaryCompiler(GesSyntaxTreeModule module, GameEventScriptCompileOptions options)
    {
        private readonly GesBinaryBuilder _builder = new GesBinaryBuilder()
            .WithModuleName(module.ModuleName)
            .WithOptimization(options.Optimize)
            .WithFlag(GameEventScriptBinaryFlags.Optimization, options.Optimize)
            .WithFlag(GameEventScriptBinaryFlags.Debug, options.EnableDebugInfo);

        private readonly Dictionary<string, GesBindRef> _outboundMessages = new(StringComparer.Ordinal);
        private readonly Dictionary<string, GesBindRef> _extensionCalls = new(StringComparer.Ordinal);
        private readonly Dictionary<string, GesBindRef> _recordConstructors = new(StringComparer.Ordinal);
        private readonly Dictionary<string, GesBindRef> _externalTypeConstructors = new(StringComparer.Ordinal);
        private readonly Dictionary<string, GesLabelRef> _callableEntries = new(StringComparer.Ordinal);
        private int _helperIndex;

        public GameEventScriptBinary Build()
        {
            EmitCallables();
            EmitRecordConstructors();
            EmitHandlers();
            return _builder.Build();
        }

        private void EmitRecordConstructors()
        {
            foreach (var type in module.TypeDefinitions.Values.OrderBy(type => type.Name, StringComparer.Ordinal))
            {
                using var sourceRange = _builder.SourceRange(type.SourceRange);
                var argumentNames = type.Fields.Where(field => field.IsConstructorParameter).Select(field => field.ConstructorLabel!).ToArray();
                using var routine = _builder.BeginRecordConstructor(type.Name, argumentNames);
                _recordConstructors[type.Name] = routine.Bind;

                var context = LoweringContext.ForRoutine(routine);
                var fieldRegisters = new GesRegisterRef[type.Fields.Count];
                var constructorParameterIndex = 0;
                for (var fieldIndex = 0; fieldIndex < type.Fields.Count; fieldIndex++)
                {
                    var field = type.Fields[fieldIndex];
                    var fieldRegister = field.IsConstructorParameter
                        ? routine.Arguments[constructorParameterIndex++]
                        : context.Declare(field.Name);
                    fieldRegisters[fieldIndex] = fieldRegister;
                    context.DeclareExisting(field.Name, fieldRegister);
                }

                var state = new ExpressionState(context.RegisterCount);
                for (var fieldIndex = 0; fieldIndex < type.Fields.Count; fieldIndex++)
                {
                    var field = type.Fields[fieldIndex];
                    using var fieldSourceRange = _builder.SourceRange(field.SourceRange);
                    var fieldRegister = fieldRegisters[fieldIndex];
                    if (field.ComputedExpression is not null)
                    {
                        var computed = EmitExpressionForRead(field.ComputedExpression, context, state);
                        EmitCastInto(fieldRegister, computed, field.TypeName);
                        continue;
                    }

                    EmitCastInto(fieldRegister, fieldRegister, field.TypeName);
                    if (field.MinimumExpression is not null && field.MaximumExpression is not null)
                    {
                        var minimum = EmitExpressionForRead(field.MinimumExpression, context, state);
                        var maximum = EmitExpressionForRead(field.MaximumExpression, context, state);
                        _builder.Clamp(fieldRegister, fieldRegister, minimum, maximum);
                        EmitCastInto(fieldRegister, fieldRegister, field.TypeName);
                    }
                }

                foreach (var fieldRegister in fieldRegisters)
                {
                    _builder.StageRegister(fieldRegister);
                }

                _builder.StageTag(type.Name);
                var map = state.AllocateTemporary(_builder, context);
                _builder.CreateMap(map, type.Fields.Select(field => field.Name).Concat([GameEventScriptValue.HiddenTypeKey]).ToArray());
                var record = state.AllocateTemporary(_builder, context);
                _builder.CastCustom(record, map, type.Name);
                _builder.ReturnValue(record);
            }
        }

        private void EmitHandlers()
        {
            foreach (var handlerGroup in module.Handlers.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                for (var handlerIndex = 0; handlerIndex < handlerGroup.Value.Count; handlerIndex++)
                {
                    var handler = handlerGroup.Value[handlerIndex];
                    using var sourceRange = _builder.SourceRange(handler.SourceRange);
                    using var routine = handler.DispatchKind == EventHandlerDispatchKind.MessageName
                        ? _builder.BeginMessageNameHandler(handler.Message, handler.Parameters.SingleOrDefault() ?? "message", (ushort)handlerIndex, handler.MatchingTags, handler.WithoutTags)
                        : _builder.BeginHandler(handler.Message, handler.SignatureLabels, (ushort)handlerIndex, handler.MatchingTags, handler.WithoutTags);
                    var context = LoweringContext.ForRoutine(routine);
                    for (var index = 0; index < handler.Parameters.Count; index++)
                    {
                        context.DeclareExisting(handler.Parameters[index], routine.Arguments[index]);
                        if (!string.IsNullOrEmpty(handler.ParameterList[index].DeclaredType))
                        {
                            EmitCastInto(routine.Arguments[index], routine.Arguments[index], handler.ParameterList[index].DeclaredType);
                        }
                    }

                    EmitStatements(handler.Statements, context);
                    _builder.ReturnVoid();
                }
            }
        }

        private void EmitCallables()
        {
            foreach (var callable in module.Callables.Values.OrderBy(callable => callable.Name, StringComparer.Ordinal))
            {
                var kind = callable.Kind == GameEventScriptCallableKind.PredicateCall
                    ? GameEventScriptBinaryBindKind.Predicate
                    : GameEventScriptBinaryBindKind.Function;
                using var sourceRange = _builder.SourceRange(callable.SourceRange);
                using var routine = kind == GameEventScriptBinaryBindKind.Predicate
                    ? _builder.BeginPredicate(callable.Name, callable.SignatureLabels)
                    : _builder.BeginFunction(callable.Name, callable.SignatureLabels);

                _callableEntries[callable.Name] = routine.EntryLabel;
                var context = LoweringContext.ForRoutine(routine);
                for (var index = 0; index < callable.Parameters.Count; index++)
                {
                    context.DeclareExisting(callable.Parameters[index], routine.Arguments[index]);
                    if (!string.IsNullOrEmpty(callable.ParameterList[index].DeclaredType))
                    {
                        EmitCastInto(routine.Arguments[index], routine.Arguments[index], callable.ParameterList[index].DeclaredType);
                    }
                }

                var result = EmitExpressionForRead(callable.Expression, context, new ExpressionState(context.RegisterCount));
                _builder.ReturnValue(result);
            }
        }

        private void EmitStatements(IReadOnlyList<StatementNode> statements, LoweringContext context)
        {
            foreach (var statement in statements)
            {
                using var sourceRange = _builder.SourceRange(statement.SourceRange);
                switch (statement)
                {
                    case LetStatementNode let:
                    {
                        var destination = context.Declare(let.Identifier);
                        if (!TryEmitExpressionToRegister(let.Expression, destination, context, new ExpressionState(context.RegisterCount)))
                        {
                            var value = EmitExpression(let.Expression, context, new ExpressionState(context.RegisterCount));
                            if (value.Id != destination.Id) _builder.Move(destination, value);
                        }

                        if (!string.IsNullOrEmpty(let.DeclaredType)) EmitCastInto(destination, destination, let.DeclaredType);
                        if (TryClassifyHandlerSignature(let.Expression, context, out var handlerSignature))
                        {
                            context.DeclareHandlerSignature(let.Identifier, handlerSignature);
                        }
                        else
                        {
                            context.ClearHandlerSignature(let.Identifier);
                        }

                        break;
                    }

                    case PublishStatementNode publish:
                        EmitPublish(publish, context);
                        break;

                    case ExpressionStatementNode expression:
                        EmitExpressionForRead(expression.Expression, context, new ExpressionState(context.RegisterCount));
                        break;

                    case IfStatementNode ifStatement:
                        EmitIf(ifStatement, context);
                        break;

                    case ForStatementNode forStatement:
                        EmitFor(forStatement, context);
                        break;

                    case SeededRandomStatementNode seededRandom:
                    {
                        EmitRandomPush(seededRandom.SeedExpression, context, new ExpressionState(context.RegisterCount));
                        EmitStatements(seededRandom.Body.Statements, context.CreateChild());
                        _builder.RandomPop();
                        break;
                    }

                    default:
                        throw new GameEventScriptCompileException($"GameEventScript binary compiler does not support statement node '{statement.GetType().Name}'.");
                }
            }
        }

        private void EmitIf(IfStatementNode ifStatement, LoweringContext context)
        {
            var condition = EmitExpressionForRead(ifStatement.Condition, context, new ExpressionState(context.RegisterCount));
            var elseLabel = _builder.AddLabel("if_else");
            var endLabel = _builder.AddLabel("if_end");
            _builder.JumpIfNotTrue(condition, elseLabel);
            EmitStatements(ifStatement.ThenBody.Statements, ifStatement.ThenBody.IsBlock ? context.CreateChild() : context);
            _builder.Jump(endLabel);
            _builder.MarkLabel(elseLabel);
            if (ifStatement.ElseBody is not null)
            {
                EmitStatements(ifStatement.ElseBody.Statements, ifStatement.ElseBody.IsBlock ? context.CreateChild() : context);
            }

            _builder.MarkLabel(endLabel);
        }

        private void EmitFor(ForStatementNode forStatement, LoweringContext context)
        {
            var loopContext = context.CreateChild();
            var item = loopContext.Declare(forStatement.Identifier);
            var state = new ExpressionState(loopContext.RegisterCount);
            var iterator = EmitIterator(forStatement.Source, context, state);
            var loopLabel = _builder.AddLabel("for_next");
            var endLabel = _builder.AddLabel("for_end");

            _builder.MarkLabel(loopLabel);
            _builder.StreamNext(item, iterator, endLabel);
            EmitStatements(forStatement.Body.Statements, forStatement.Body.IsBlock ? loopContext.CreateChild() : loopContext);
            _builder.Jump(loopLabel);
            _builder.MarkLabel(endLabel);
            _builder.StreamClose(iterator);
        }

        private void EmitPublish(PublishStatementNode publish, LoweringContext context)
        {
            var tags = publish.TagExpressions.Select(expression => EmitExpressionForRead(expression, context, new ExpressionState(context.RegisterCount))).ToArray();
            if (publish.MessageExpression is MessageLiteralExpressionNode message)
            {
                var argumentNames = message.Arguments.Select(argument => argument.Name).ToArray();
                var arguments = message.Arguments.Select(argument => EmitExpressionForRead(argument.Expression, context, new ExpressionState(context.RegisterCount))).ToArray();
                var bind = ResolveOutboundMessage(message.Message, argumentNames);
                if (publish.Kind == PublishStatementKind.Publish)
                {
                    if (tags.Length == 0) _builder.PublishMessage(bind, arguments);
                    else _builder.PublishMessageWithTags(bind, arguments, tags);
                }
                else
                {
                    if (tags.Length == 0) _builder.EmitMessage(bind, arguments);
                    else _builder.EmitMessageWithTags(bind, arguments, tags);
                }

                return;
            }

            var messageValue = EmitExpressionForRead(publish.MessageExpression, context, new ExpressionState(context.RegisterCount));
            if (publish.Kind == PublishStatementKind.Publish)
            {
                if (tags.Length == 0) _builder.PublishMessageValue(messageValue);
                else _builder.PublishMessageValueWithTags(messageValue, tags);
            }
            else
            {
                if (tags.Length == 0) _builder.EmitMessageValue(messageValue);
                else _builder.EmitMessageValueWithTags(messageValue, tags);
            }
        }

        private bool TryEmitExpressionToRegister(ExpressionNode expression, GesRegisterRef destination, LoweringContext context, ExpressionState state)
        {
            using var sourceRange = _builder.SourceRange(expression.SourceRange);
            switch (expression)
            {
                case BooleanLiteralExpressionNode boolean:
                    _builder.LoadBoolean(destination, boolean.Value);
                    return true;
                case IntegerLiteralExpressionNode integer:
                    _builder.LoadInteger(destination, integer.Value);
                    return true;
                case UnitIntegerLiteralExpressionNode integer:
                    _builder.LoadInteger(destination, integer.Value, ResolveUnitOrNone(integer.UnitName));
                    return true;
                case FloatLiteralExpressionNode number:
                    _builder.LoadFloat(destination, number.Value);
                    return true;
                case UnitFloatLiteralExpressionNode number:
                    _builder.LoadFloat(destination, number.Value, ResolveUnitOrNone(number.UnitName));
                    return true;
                case PercentageLiteralExpressionNode percentage:
                    _builder.LoadPercentage(destination, percentage.PercentValue / 100d);
                    return true;
                case TextLiteralExpressionNode text:
                    _builder.LoadText(destination, text.Value);
                    return true;
                case TagLiteralExpressionNode tag:
                    _builder.LoadTag(destination, tag.Name);
                    return true;
                case IdentifierExpressionNode { Name: "nothing" }:
                    _builder.LoadNothing(destination);
                    return true;
                case IdentifierExpressionNode identifier:
                {
                    var source = context.Require(identifier.Name);
                    if (source.Id != destination.Id) _builder.Move(destination, source);
                    return true;
                }
                case UnaryExpressionNode unary:
                {
                    var operand = EmitExpressionForRead(unary.Operand, context, state);
                    EmitUnary(destination, unary.Operator, operand);
                    return true;
                }
                case BinaryExpressionNode { Operator: not (GesBinaryOperator.Or or GesBinaryOperator.And or GesBinaryOperator.Implies) } binary:
                {
                    var left = EmitExpressionForRead(binary.Left, context, state);
                    var right = EmitExpressionForRead(binary.Right, context, state);
                    EmitBinary(destination, binary.Operator, left, right);
                    return true;
                }
                case TypeCastExpressionNode cast:
                {
                    var value = EmitExpressionForRead(cast.Value, context, state);
                    EmitCastInto(destination, value, cast.TypeName);
                    return true;
                }
                case PredicateCallExpressionNode predicate:
                    EmitPredicateCallInto(predicate, destination, context, state);
                    return true;
                case CallExpressionNode call when module.Callables.ContainsKey(call.Name):
                    EmitCallInto(call, destination, context, state);
                    return true;
                case CallExpressionNode call:
                    EmitHandlerBindCallInto(call, destination, context, state);
                    return true;
                default:
                    return false;
            }
        }

        private GesRegisterRef EmitExpression(ExpressionNode expression, LoweringContext context, ExpressionState state)
        {
            using var sourceRange = _builder.SourceRange(expression.SourceRange);
            var destination = state.AllocateTemporary(_builder, context);
            if (TryEmitExpressionToRegister(expression, destination, context, state)) return destination;

            switch (expression)
            {
                case MessageLiteralExpressionNode message:
                {
                    var argumentNames = message.Arguments.Select(argument => argument.Name).ToArray();
                    var arguments = message.Arguments.Select(argument => EmitExpressionForRead(argument.Expression, context, state)).ToArray();
                    _builder.LoadMessage(destination, MessageShape(message.Message, argumentNames), arguments);
                    return destination;
                }

                case HandlerLiteralExpressionNode handler:
                    _builder.LoadHandler(destination, MessageShape(handler.Message, handler.SignatureLabels));
                    return destination;

                case ListLiteralExpressionNode list:
                    EmitStageArguments(list.Items, context, state);
                    _builder.CreateList(destination);
                    return destination;

                case MapLiteralExpressionNode map:
                    EmitStageArguments(map.Entries.Select(entry => entry.Value), context, state);
                    _builder.CreateMap(destination, map.Entries.Select(entry => entry.Key).ToArray());
                    return destination;

                case RangeExpressionNode range:
                {
                    var from = EmitExpressionForRead(range.FromExpression, context, state);
                    var to = EmitExpressionForRead(range.ToExpression, context, state);
                    if (range.StepExpression is null) _builder.CreateRange(destination, from, to);
                    else _builder.CreateRangeWithStep(destination, from, to, EmitExpressionForRead(range.StepExpression, context, state));
                    return destination;
                }

                case DiceExpressionNode dice:
                    _builder.CreateDice(destination, ToShort(dice.DiceCount, "dice count"), ToShort(dice.SideCount, "dice side count"));
                    return destination;

                case ClampExpressionNode clamp:
                    _builder.Clamp(
                        destination,
                        EmitExpressionForRead(clamp.Value, context, state),
                        EmitExpressionForRead(clamp.Minimum, context, state),
                        EmitExpressionForRead(clamp.Maximum, context, state));
                    return destination;

                case RandomExpressionNode random:
                {
                    var from = EmitExpressionForRead(random.FromExpression, context, state);
                    var to = EmitExpressionForRead(random.ToExpression, context, state);
                    if (random.FromExpression is FloatLiteralExpressionNode or UnitFloatLiteralExpressionNode ||
                        random.ToExpression is FloatLiteralExpressionNode or UnitFloatLiteralExpressionNode)
                    {
                        _builder.RandomTakeFloat(destination, from, to);
                    }
                    else
                    {
                        _builder.RandomTake(destination, from, to);
                    }

                    return destination;
                }

                case SeededRandomExpressionNode seededRandom:
                    EmitRandomPush(seededRandom.SeedExpression, context, state);
                    var result = EmitExpressionForRead(seededRandom.BodyExpression, context, state);
                    _builder.Move(destination, result);
                    _builder.RandomPop();
                    return destination;

                case GeneratedCollectionExpressionNode generatedCollection:
                    EmitGeneratedCollectionInto(generatedCollection, destination, context, state);
                    return destination;

                case GuardedChoiceExpressionNode guardedChoice:
                    EmitGuardedChoiceInto(guardedChoice, destination, context, state);
                    return destination;

                case BinaryExpressionNode binary:
                    return EmitShortCircuitBinary(binary, destination, context, state);

                case VariadicTaggedExpressionNode variadic:
                    return EmitVariadic(variadic, destination, context, state);

                case ExtensionCallExpressionNode extensionCall:
                    EmitExtensionCallInto(extensionCall, destination, context, state, isPredicate: false);
                    return destination;

                case ExtensionPredicateExpressionNode extensionPredicate:
                    EmitExtensionPredicateInto(extensionPredicate, destination, context, state);
                    return destination;

                case TypeCheckExpressionNode check:
                    EmitCheckInto(destination, EmitExpressionForRead(check.Value, context, state), check.TypeName);
                    return destination;

                case TypeConstructorExpressionNode constructor:
                    EmitTypeConstructorInto(constructor, destination, context, state);
                    return destination;

                case MemberAccessExpressionNode member:
                    _builder.MemberAccess(destination, member.Member, EmitExpressionForRead(member.Target, context, state));
                    return destination;

                case CollectionAccessExpressionNode access:
                    EmitCollectionAccessInto(access, destination, context, state);
                    return destination;

                default:
                    throw new GameEventScriptCompileException($"GameEventScript binary compiler does not support expression node '{expression.GetType().Name}'.");
            }
        }

        private GesRegisterRef EmitExpressionForRead(ExpressionNode expression, LoweringContext context, ExpressionState state)
        {
            return expression is IdentifierExpressionNode { Name: not "nothing" } identifier
                ? context.Require(identifier.Name)
                : EmitExpression(expression, context, state);
        }

        private GesRegisterRef EmitShortCircuitBinary(BinaryExpressionNode binary, GesRegisterRef destination, LoweringContext context, ExpressionState state)
        {
            var left = EmitExpressionForRead(binary.Left, context, state);
            EmitShortCircuitCombine(binary.Operator, destination, left, left);
            var end = _builder.AddLabel("short_circuit_end");
            switch (binary.Operator)
            {
                case GesBinaryOperator.Or:
                    _builder.JumpIfTrue(left, end);
                    break;
                case GesBinaryOperator.And:
                case GesBinaryOperator.Implies:
                    _builder.JumpIfFalse(left, end);
                    break;
            }

            var right = EmitExpressionForRead(binary.Right, context, state);
            EmitShortCircuitCombine(binary.Operator, destination, left, right);
            _builder.MarkLabel(end);
            return destination;
        }

        private void EmitShortCircuitCombine(GesBinaryOperator operation, GesRegisterRef destination, GesRegisterRef left, GesRegisterRef right)
        {
            switch (operation)
            {
                case GesBinaryOperator.Or:
                    _builder.Or(destination, left, right);
                    return;
                case GesBinaryOperator.And:
                    _builder.And(destination, left, right);
                    return;
                case GesBinaryOperator.Implies:
                    _builder.Implies(destination, left, right);
                    return;
                default:
                    throw new GameEventScriptCompileException($"GameEventScript binary compiler does not support short-circuit operator '{operation.ToSourceText()}'.");
            }
        }

        private GesRegisterRef EmitVariadic(VariadicTaggedExpressionNode variadic, GesRegisterRef destination, LoweringContext context, ExpressionState state)
        {
            if (variadic.Arguments.Count == 0)
            {
                _builder.LoadNothing(destination);
                return destination;
            }

            var current = EmitExpressionForRead(variadic.Arguments[0], context, state);
            if (current.Id != destination.Id) _builder.Move(destination, current);
            for (var index = 1; index < variadic.Arguments.Count; index++)
            {
                var next = EmitExpressionForRead(variadic.Arguments[index], context, state);
                switch (variadic.Operator)
                {
                    case "min":
                        _builder.Min(destination, destination, next);
                        break;
                    case "max":
                        _builder.Max(destination, destination, next);
                        break;
                    default:
                        throw new GameEventScriptCompileException($"GameEventScript binary compiler does not support variadic operator '{variadic.Operator}'.");
                }
            }

            return destination;
        }

        private void EmitCollectionAccessInto(CollectionAccessExpressionNode access, GesRegisterRef destination, LoweringContext context, ExpressionState state)
        {
            if (TryEmitCollectionPipelineInto(access, destination, context, state))
            {
                return;
            }

            var target = EmitExpressionForRead(access.Target, context, state);
            switch (access.Selector)
            {
                case ExpressionSelectorNode { Expression: IntegerLiteralExpressionNode { Value: >= 0 and <= ushort.MaxValue } integer }:
                    _builder.IndexAccess(destination, (ushort)integer.Value, target);
                    return;
                case ExpressionSelectorNode { Expression: TextLiteralExpressionNode text }:
                    _builder.MemberAccess(destination, text.Value, target);
                    return;
                case ExpressionSelectorNode { Expression: TagLiteralExpressionNode tag }:
                    _builder.MemberAccess(destination, tag.Name, target);
                    return;
                case ExpressionSelectorNode selector:
                    _builder.PropertyAccess(destination, EmitExpressionForRead(selector.Expression, context, state), target);
                    return;
                case SeriesTermSelectorNode term:
                    _builder.Term(destination, target, EmitExpressionForRead(term.IndexExpression, context, state));
                    return;
                case SequenceSliceSelectorNode slice:
                    EmitSlice(destination, target, slice);
                    return;
                case EdgeSelectorNode { Predicate: null } edge:
                    if (edge.Mode == "last") _builder.Last(destination, target);
                    else if (edge.Mode == "single") _builder.Single(destination, target);
                    else _builder.First(destination, target);
                    return;
                case EdgeSelectorNode { Predicate: not null, Identifier: not null } edge:
                    EmitFilteredEdge(destination, target, edge.Identifier, edge.Predicate, edge.Mode, context, state);
                    return;
                case DrawSelectorNode draw:
                    if (draw.Count == 1) _builder.First(destination, target);
                    else _builder.TakeFirst(destination, target, ToShort(draw.Count, "draw count"));
                    return;
                case ChooseSelectorNode choose:
                    EmitChoose(destination, target, choose, context, state);
                    return;
                case ContainsSelectorNode contains:
                    EmitContains(destination, target, contains, context, state);
                    return;
                case FilterSelectorNode filter:
                    EmitStreamTransformCollect(destination, target, filter.Identifier, filter.Predicate, filter: true, context, state);
                    return;
                case SelectSelectorNode select:
                    EmitStreamTransformCollect(destination, target, select.Identifier, select.Projection, filter: false, context, state);
                    return;
                case PredicateSelectorNode predicate:
                    EmitPredicateTerminal(destination, target, predicate.Identifier, predicate.Predicate, predicate.Operator, context, state);
                    return;
                case CountSelectorNode count:
                    EmitStreamTransformTerminal(destination, target, count.Identifier, count.Predicate, GameEventScriptBytecodeOpCode.Count, filter: true, context, state);
                    return;
                case SumSelectorNode sum:
                    EmitStreamTransformTerminal(destination, target, sum.Identifier, sum.Projection, GameEventScriptBytecodeOpCode.Sum, filter: false, context, state);
                    return;
                case AverageSelectorNode average:
                    EmitStreamTransformTerminal(destination, target, average.Identifier, average.Projection, GameEventScriptBytecodeOpCode.Average, filter: false, context, state);
                    return;
                case MinSelectorNode min:
                    EmitStreamExtrema(destination, target, min.Identifier, min.Projection, isMax: false, context, state);
                    return;
                case MaxSelectorNode max:
                    EmitStreamExtrema(destination, target, max.Identifier, max.Projection, isMax: true, context, state);
                    return;
                case MapSelectorNode map:
                    EmitStreamCollectMap(destination, target, map, context, state);
                    return;
                case SortSelectorNode sort:
                    if (sort.Direction == "descending") _builder.SortDescending(destination, target);
                    else _builder.SortAscending(destination, target);
                    return;
                case ReverseSelectorNode:
                    _builder.Reverse(destination, target);
                    return;
                case ShuffleSelectorNode:
                    _builder.Shuffle(destination, target);
                    return;
                case DistinctSelectorNode { Identifier: null, Projection: null }:
                    _builder.Distinct(destination, target);
                    return;
                case DistinctSelectorNode { Identifier: not null, Projection: not null } distinct:
                {
                    var keyEntry = EmitSelectorHelperExpression("distinct_by", distinct.Identifier, distinct.Projection, context, state, out var itemBinding);
                    _builder.DistinctBy(destination, target, itemBinding, keyEntry);
                    return;
                }
                case GroupBySelectorNode groupBy:
                {
                    var keyEntry = EmitSelectorHelperExpression("group_by", groupBy.Identifier, groupBy.Projection, context, state, out var itemBinding);
                    _builder.GroupBy(destination, target, itemBinding, keyEntry);
                    return;
                }
                case OrderBySelectorNode orderBy:
                {
                    var keyEntry = EmitSelectorHelperExpression("order_by", orderBy.Identifier, orderBy.Projection, context, state, out var itemBinding);
                    if (orderBy.Direction == "descending") _builder.OrderByDescending(destination, target, itemBinding, keyEntry);
                    else _builder.OrderByAscending(destination, target, itemBinding, keyEntry);
                    return;
                }
                case PatternSelectorNode pattern:
                    EmitPattern(destination, target, pattern.Pattern, take: false, context, state);
                    return;
                case TakePatternSelectorNode pattern:
                    EmitPattern(destination, target, pattern.Pattern, take: true, context, state);
                    return;
                case ObjectMatchSelectorNode objectMatch:
                    EmitObjectMatch(destination, target, objectMatch.Pattern, context, state);
                    return;
                default:
                    throw new GameEventScriptCompileException($"GameEventScript binary compiler does not support selector node '{access.Selector.GetType().Name}'.");
            }
        }

        private bool TryEmitCollectionPipelineInto(CollectionAccessExpressionNode access, GesRegisterRef destination, LoweringContext context, ExpressionState state)
        {
            var selectors = new List<CollectionSelectorNode>();
            ExpressionNode source = access;
            while (source is CollectionAccessExpressionNode collectionAccess)
            {
                selectors.Add(collectionAccess.Selector);
                source = collectionAccess.Target;
            }

            selectors.Reverse();
            if (selectors.Count == 0)
            {
                return false;
            }

            var terminal = selectors[^1];
            var prefixCount = selectors.Count - 1;
            for (var index = 0; index < prefixCount; index++)
            {
                if (selectors[index] is not (FilterSelectorNode or SelectSelectorNode))
                {
                    return false;
                }
            }

            if (!CanEmitStreamPipelineTerminal(terminal, prefixCount > 0))
            {
                return false;
            }

            var sourceRegister = EmitExpressionForRead(source, context, state);
            if (prefixCount == 0 &&
                terminal is CountSelectorNode count &&
                IsAlwaysTrue(count.Predicate))
            {
                _builder.Count(destination, sourceRegister);
                return true;
            }

            if (prefixCount == 0 &&
                terminal is SumSelectorNode sum &&
                IsIdentityProjection(sum.Identifier, sum.Projection))
            {
                _builder.Sum(destination, sourceRegister);
                return true;
            }

            if (prefixCount == 0 &&
                terminal is AverageSelectorNode average &&
                IsIdentityProjection(average.Identifier, average.Projection))
            {
                _builder.Average(destination, sourceRegister);
                return true;
            }

            var iterator = state.AllocateTemporary(_builder, context);
            _builder.StreamCreate(iterator, sourceRegister);
            for (var index = 0; index < prefixCount; index++)
            {
                iterator = EmitStreamTransformIterator(iterator, selectors[index], context, state);
            }

            EmitStreamPipelineTerminal(destination, iterator, terminal, context, state);
            return true;
        }

        private static bool CanEmitStreamPipelineTerminal(CollectionSelectorNode terminal, bool hasPrefix)
            => terminal switch
            {
                FilterSelectorNode or
                    SelectSelectorNode or
                    PredicateSelectorNode or
                    CountSelectorNode or
                    SumSelectorNode or
                    AverageSelectorNode or
                    MinSelectorNode or
                    MaxSelectorNode or
                    MapSelectorNode => true,
                EdgeSelectorNode edge => hasPrefix || edge.Predicate is not null,
                SequenceSliceSelectorNode => hasPrefix,
                DrawSelectorNode => hasPrefix,
                _ => false
            };

        private GesRegisterRef EmitStreamTransformIterator(
            GesRegisterRef iterator,
            CollectionSelectorNode selector,
            LoweringContext context,
            ExpressionState state)
        {
            switch (selector)
            {
                case FilterSelectorNode filter:
                    if (IsAlwaysTrue(filter.Predicate))
                    {
                        return iterator;
                    }

                    var filteredIterator = state.AllocateTemporary(_builder, context);
                    var filterEntry = EmitStreamSelectorHelperExpression("filter", filter.Identifier, filter.Predicate, context, out var filterItemBinding, out var filterCaptures);
                    _builder.StreamFilter(filteredIterator, iterator, filterEntry, filterItemBinding, filterCaptures);
                    return filteredIterator;
                case SelectSelectorNode select:
                    if (IsIdentityProjection(select.Identifier, select.Projection))
                    {
                        return iterator;
                    }

                    var mappedIterator = state.AllocateTemporary(_builder, context);
                    var mapEntry = EmitStreamSelectorHelperExpression("select", select.Identifier, select.Projection, context, out var mapItemBinding, out var mapCaptures);
                    _builder.StreamMap(mappedIterator, iterator, mapEntry, mapItemBinding, mapCaptures);
                    return mappedIterator;
                default:
                    throw new GameEventScriptCompileException($"GameEventScript binary compiler does not support non-terminal selector node '{selector.GetType().Name}'.");
            }
        }

        private void EmitStreamPipelineTerminal(
            GesRegisterRef destination,
            GesRegisterRef iterator,
            CollectionSelectorNode terminal,
            LoweringContext context,
            ExpressionState state)
        {
            switch (terminal)
            {
                case FilterSelectorNode filter:
                    _builder.StreamCollectList(destination, EmitStreamTransformIterator(iterator, filter, context, state));
                    return;
                case SelectSelectorNode select:
                    _builder.StreamCollectList(destination, EmitStreamTransformIterator(iterator, select, context, state));
                    return;
                case PredicateSelectorNode predicate:
                {
                    var predicateIterator = IsIdentityProjection(predicate.Identifier, predicate.Predicate)
                        ? iterator
                        : EmitStreamTransformIterator(iterator, new SelectSelectorNode(predicate.Identifier, predicate.Predicate), context, state);
                    if (predicate.Operator == "all") _builder.HasAll(destination, predicateIterator);
                    else _builder.HasAny(destination, predicateIterator);
                    return;
                }
                case CountSelectorNode count:
                {
                    var countIterator = IsAlwaysTrue(count.Predicate)
                        ? iterator
                        : EmitStreamTransformIterator(iterator, new FilterSelectorNode(count.Identifier, count.Predicate), context, state);
                    _builder.Count(destination, countIterator);
                    return;
                }
                case SumSelectorNode sum:
                {
                    var sumIterator = IsIdentityProjection(sum.Identifier, sum.Projection)
                        ? iterator
                        : EmitStreamTransformIterator(iterator, new SelectSelectorNode(sum.Identifier, sum.Projection), context, state);
                    _builder.Sum(destination, sumIterator);
                    return;
                }
                case AverageSelectorNode average:
                {
                    var averageIterator = IsIdentityProjection(average.Identifier, average.Projection)
                        ? iterator
                        : EmitStreamTransformIterator(iterator, new SelectSelectorNode(average.Identifier, average.Projection), context, state);
                    _builder.Average(destination, averageIterator);
                    return;
                }
                case MinSelectorNode min:
                    EmitStreamExtremaFromIterator(destination, iterator, min.Identifier, min.Projection, isMax: false, context, state);
                    return;
                case MaxSelectorNode max:
                    EmitStreamExtremaFromIterator(destination, iterator, max.Identifier, max.Projection, isMax: true, context, state);
                    return;
                case MapSelectorNode map:
                    EmitStreamCollectMapFromIterator(destination, iterator, map, context, state);
                    return;
                case EdgeSelectorNode { Predicate: null } edge:
                    if (edge.Mode == "last") _builder.Last(destination, iterator);
                    else if (edge.Mode == "single") _builder.Single(destination, iterator);
                    else _builder.First(destination, iterator);
                    return;
                case EdgeSelectorNode { Predicate: not null, Identifier: not null } edge:
                {
                    var filteredIterator = EmitStreamTransformIterator(iterator, new FilterSelectorNode(edge.Identifier, edge.Predicate), context, state);
                    if (edge.Mode == "last") _builder.Last(destination, filteredIterator);
                    else if (edge.Mode == "single") _builder.Single(destination, filteredIterator);
                    else _builder.First(destination, filteredIterator);
                    return;
                }
                case SequenceSliceSelectorNode slice:
                    EmitSlice(destination, iterator, slice);
                    return;
                case DrawSelectorNode draw:
                    if (draw.Count == 1) _builder.First(destination, iterator);
                    else _builder.TakeFirst(destination, iterator, ToShort(draw.Count, "draw count"));
                    return;
                default:
                    throw new GameEventScriptCompileException($"GameEventScript binary compiler does not support stream terminal selector node '{terminal.GetType().Name}'.");
            }
        }

        private static bool IsIdentityProjection(string identifier, ExpressionNode expression)
            => expression is IdentifierExpressionNode projection && string.Equals(projection.Name, identifier, StringComparison.Ordinal);

        private static bool IsAlwaysTrue(ExpressionNode expression)
            => expression is BooleanLiteralExpressionNode { Value: true };

        private void EmitPattern(GesRegisterRef destination, GesRegisterRef target, DicePatternNode pattern, bool take, LoweringContext context, ExpressionState state)
        {
            switch (pattern)
            {
                case DiceCountPatternNode { Face: null } count:
                    if (take) _builder.TakePattern(destination, target, GameEventScriptBytecodePatternKind.CountAny, ToShort(count.Count, "pattern count"));
                    else _builder.HasPattern(destination, target, GameEventScriptBytecodePatternKind.CountAny, ToShort(count.Count, "pattern count"));
                    return;
                case DiceCountPatternNode count:
                {
                    var faceEntry = EmitHelperExpression("pattern_face", count.Face, context, state);
                    if (take) _builder.TakePattern(destination, target, GameEventScriptBytecodePatternKind.CountFace, ToShort(count.Count, "pattern count"), faceEntry);
                    else _builder.HasPattern(destination, target, GameEventScriptBytecodePatternKind.CountFace, ToShort(count.Count, "pattern count"), faceEntry);
                    return;
                }
                case DiceFullHousePatternNode:
                    if (take) _builder.TakePattern(destination, target, GameEventScriptBytecodePatternKind.FullHouse);
                    else _builder.HasPattern(destination, target, GameEventScriptBytecodePatternKind.FullHouse);
                    return;
                case DiceStraightPatternNode:
                    if (take) _builder.TakePattern(destination, target, GameEventScriptBytecodePatternKind.Straight);
                    else _builder.HasPattern(destination, target, GameEventScriptBytecodePatternKind.Straight);
                    return;
                default:
                    throw new GameEventScriptCompileException($"GameEventScript binary compiler does not support dice pattern node '{pattern.GetType().Name}'.");
            }
        }

        private GesLabelRef EmitHelperExpression(string name, ExpressionNode expression, LoweringContext parentContext, ExpressionState parentState)
        {
            using var sourceRange = _builder.SourceRange(expression.SourceRange);
            using var helper = _builder.BeginHelper($"{name}_{_helperIndex++}");
            var helperContext = LoweringContext.ForRoutineWithParent(helper, parentContext);
            var result = EmitExpressionForRead(expression, helperContext, new ExpressionState(parentState.NextRegister));
            _builder.ReturnValue(result);
            return helper.EntryLabel;
        }

        private GesLabelRef EmitObjectMatchHelperExpression(
            ObjectMatchPatternNode pattern,
            LoweringContext parentContext,
            out GesRegisterRef itemBinding,
            out IReadOnlyList<GesRegisterRef> captureRegisters)
        {
            var captures = ResolveStreamCaptures(pattern, parentContext);
            captureRegisters = captures.Select(capture => capture.SourceRegister).ToArray();
            using var sourceRange = _builder.SourceRange(pattern.SourceRange);
            using var helper = _builder.BeginHelper($"object_match_{_helperIndex++}", new[] { "item" }.Concat(captures.Select(capture => capture.Name)).ToArray());
            itemBinding = helper.Arguments[0];
            var helperContext = LoweringContext.ForRoutine(helper);
            helperContext.DeclareExisting("item", itemBinding);
            for (var index = 0; index < captures.Count; index++)
            {
                helperContext.DeclareExisting(captures[index].Name, helper.Arguments[index + 1]);
            }

            var result = EmitObjectPatternPredicate(itemBinding, pattern, helperContext, new ExpressionState(helperContext.RegisterCount));
            _builder.ReturnValue(result);
            return helper.EntryLabel;
        }

        private GesLabelRef EmitSelectorHelperExpression(
            string name,
            string identifier,
            ExpressionNode expression,
            LoweringContext parentContext,
            ExpressionState parentState,
            out GesRegisterRef itemBinding)
        {
            itemBinding = parentContext.AddTemporary(identifier);
            using var sourceRange = _builder.SourceRange(expression.SourceRange);
            using var helper = _builder.BeginHelper($"{name}_{_helperIndex++}");
            var helperContext = LoweringContext.ForRoutineWithParent(helper, parentContext);
            helperContext.DeclareExisting(identifier, itemBinding);
            var result = EmitExpressionForRead(expression, helperContext, new ExpressionState(parentState.NextRegister));
            _builder.ReturnValue(result);
            return helper.EntryLabel;
        }

        private GesLabelRef EmitSelectorHelperExpression(
            string name,
            string identifier,
            ExpressionNode expression,
            LoweringContext parentContext,
            ExpressionState parentState,
            GesRegisterRef itemBinding)
        {
            using var sourceRange = _builder.SourceRange(expression.SourceRange);
            using var helper = _builder.BeginHelper($"{name}_{_helperIndex++}");
            var helperContext = LoweringContext.ForRoutineWithParent(helper, parentContext);
            helperContext.DeclareExisting(identifier, itemBinding);
            var result = EmitExpressionForRead(expression, helperContext, new ExpressionState(Math.Max(parentState.NextRegister, itemBinding.Id + 1)));
            _builder.ReturnValue(result);
            return helper.EntryLabel;
        }

        private GesLabelRef EmitStreamSelectorHelperExpression(
            string name,
            string identifier,
            ExpressionNode expression,
            LoweringContext parentContext,
            out GesRegisterRef itemBinding,
            out IReadOnlyList<GesRegisterRef> captureRegisters)
        {
            var captures = ResolveStreamCaptures(expression, identifier, parentContext);
            captureRegisters = captures.Select(capture => capture.SourceRegister).ToArray();
            using var sourceRange = _builder.SourceRange(expression.SourceRange);
            using var helper = _builder.BeginHelper($"{name}_{_helperIndex++}", new[] { identifier }.Concat(captures.Select(capture => capture.Name)).ToArray());
            itemBinding = helper.Arguments[0];
            var helperContext = LoweringContext.ForRoutine(helper);
            helperContext.DeclareExisting(identifier, itemBinding);
            for (var index = 0; index < captures.Count; index++)
            {
                helperContext.DeclareExisting(captures[index].Name, helper.Arguments[index + 1]);
            }

            var result = EmitExpressionForRead(expression, helperContext, new ExpressionState(helperContext.RegisterCount));
            _builder.ReturnValue(result);
            return helper.EntryLabel;
        }

        private void EmitChoose(GesRegisterRef destination, GesRegisterRef target, ChooseSelectorNode choose, LoweringContext context, ExpressionState state)
        {
            var source = target;
            if (choose.Predicate is not null && !string.IsNullOrEmpty(choose.Identifier))
            {
                var iterator = state.AllocateTemporary(_builder, context);
                var filteredIterator = state.AllocateTemporary(_builder, context);
                var predicateEntry = EmitStreamSelectorHelperExpression("choose_filter", choose.Identifier!, choose.Predicate, context, out var predicateItemBinding, out var predicateCaptures);
                _builder.StreamCreate(iterator, target);
                _builder.StreamFilter(filteredIterator, iterator, predicateEntry, predicateItemBinding, predicateCaptures);
                source = filteredIterator;
            }

            if (choose.WeightExpression is not null && !string.IsNullOrEmpty(choose.WeightIdentifier))
            {
                var iterator = source;
                if (choose.Predicate is null)
                {
                    iterator = state.AllocateTemporary(_builder, context);
                    _builder.StreamCreate(iterator, source);
                }

                var weightEntry = EmitStreamSelectorHelperExpression("choose_weight", choose.WeightIdentifier!, choose.WeightExpression, context, out var weightItemBinding, out var weightCaptures);
                if (choose.Count == 1) _builder.StreamOneWeighted(destination, iterator, weightItemBinding, weightEntry, weightCaptures);
                else _builder.StreamTakeWeighted(destination, iterator, ToShort(choose.Count, "choose count"), weightItemBinding, weightEntry, weightCaptures);
                return;
            }

            if (choose.AtRandom)
            {
                if (choose.Count == 1) _builder.OneRandom(destination, source);
                else _builder.TakeRandom(destination, source, ToShort(choose.Count, "choose count"));
                return;
            }

            if (choose.Count == 1) _builder.First(destination, source);
            else _builder.TakeFirst(destination, source, ToShort(choose.Count, "choose count"));
        }

        private void EmitStreamTransformCollect(
            GesRegisterRef destination,
            GesRegisterRef target,
            string identifier,
            ExpressionNode expression,
            bool filter,
            LoweringContext context,
            ExpressionState state)
        {
            var iterator = state.AllocateTemporary(_builder, context);
            var transformedIterator = state.AllocateTemporary(_builder, context);
            var entry = EmitStreamSelectorHelperExpression(filter ? "filter" : "select", identifier, expression, context, out var itemBinding, out var captures);
            _builder.StreamCreate(iterator, target);
            if (filter) _builder.StreamFilter(transformedIterator, iterator, entry, itemBinding, captures);
            else _builder.StreamMap(transformedIterator, iterator, entry, itemBinding, captures);
            _builder.StreamCollectList(destination, transformedIterator);
        }

        private void EmitPredicateTerminal(
            GesRegisterRef destination,
            GesRegisterRef target,
            string identifier,
            ExpressionNode predicate,
            string op,
            LoweringContext context,
            ExpressionState state)
        {
            var iterator = state.AllocateTemporary(_builder, context);
            var predicateIterator = state.AllocateTemporary(_builder, context);
            var entry = EmitStreamSelectorHelperExpression(op, identifier, predicate, context, out var itemBinding, out var captures);
            _builder.StreamCreate(iterator, target);
            _builder.StreamMap(predicateIterator, iterator, entry, itemBinding, captures);
            if (op == "all") _builder.HasAll(destination, predicateIterator);
            else _builder.HasAny(destination, predicateIterator);
        }

        private void EmitFilteredEdge(
            GesRegisterRef destination,
            GesRegisterRef target,
            string identifier,
            ExpressionNode predicate,
            string mode,
            LoweringContext context,
            ExpressionState state)
        {
            var iterator = state.AllocateTemporary(_builder, context);
            var filteredIterator = state.AllocateTemporary(_builder, context);
            var entry = EmitStreamSelectorHelperExpression(mode, identifier, predicate, context, out var itemBinding, out var captures);
            _builder.StreamCreate(iterator, target);
            _builder.StreamFilter(filteredIterator, iterator, entry, itemBinding, captures);
            if (mode == "last") _builder.Last(destination, filteredIterator);
            else if (mode == "single") _builder.Single(destination, filteredIterator);
            else _builder.First(destination, filteredIterator);
        }

        private void EmitStreamTransformTerminal(
            GesRegisterRef destination,
            GesRegisterRef target,
            string identifier,
            ExpressionNode expression,
            GameEventScriptBytecodeOpCode terminal,
            bool filter,
            LoweringContext context,
            ExpressionState state)
        {
            if (filter && terminal == GameEventScriptBytecodeOpCode.Count && IsAlwaysTrue(expression))
            {
                _builder.Count(destination, target);
                return;
            }

            if (!filter && terminal == GameEventScriptBytecodeOpCode.Sum && IsIdentityProjection(identifier, expression))
            {
                _builder.Sum(destination, target);
                return;
            }

            if (!filter && terminal == GameEventScriptBytecodeOpCode.Average && IsIdentityProjection(identifier, expression))
            {
                _builder.Average(destination, target);
                return;
            }

            var iterator = state.AllocateTemporary(_builder, context);
            var transformedIterator = state.AllocateTemporary(_builder, context);
            var entry = EmitStreamSelectorHelperExpression(filter ? "count" : terminal.ToString(), identifier, expression, context, out var itemBinding, out var captures);
            _builder.StreamCreate(iterator, target);
            if (filter) _builder.StreamFilter(transformedIterator, iterator, entry, itemBinding, captures);
            else _builder.StreamMap(transformedIterator, iterator, entry, itemBinding, captures);
            switch (terminal)
            {
                case GameEventScriptBytecodeOpCode.Count:
                    _builder.Count(destination, transformedIterator);
                    return;
                case GameEventScriptBytecodeOpCode.Sum:
                    _builder.Sum(destination, transformedIterator);
                    return;
                case GameEventScriptBytecodeOpCode.Average:
                    _builder.Average(destination, transformedIterator);
                    return;
                default:
                    throw new GameEventScriptCompileException($"GameEventScript binary compiler does not support stream terminal '{terminal}'.");
            }
        }

        private void EmitStreamExtrema(GesRegisterRef destination, GesRegisterRef target, string identifier, ExpressionNode projection, bool isMax, LoweringContext context, ExpressionState state)
        {
            var iterator = state.AllocateTemporary(_builder, context);
            _builder.StreamCreate(iterator, target);
            EmitStreamExtremaFromIterator(destination, iterator, identifier, projection, isMax, context, state);
        }

        private void EmitStreamExtremaFromIterator(GesRegisterRef destination, GesRegisterRef iterator, string identifier, ExpressionNode projection, bool isMax, LoweringContext context, ExpressionState state)
        {
            var entry = EmitSelectorHelperExpression(isMax ? "max" : "min", identifier, projection, context, state, out var itemBinding);
            if (isMax) _builder.StreamMax(destination, iterator, itemBinding, entry);
            else _builder.StreamMin(destination, iterator, itemBinding, entry);
        }

        private void EmitStreamCollectMap(GesRegisterRef destination, GesRegisterRef target, MapSelectorNode map, LoweringContext context, ExpressionState state)
        {
            var iterator = state.AllocateTemporary(_builder, context);
            _builder.StreamCreate(iterator, target);
            EmitStreamCollectMapFromIterator(destination, iterator, map, context, state);
        }

        private void EmitStreamCollectMapFromIterator(GesRegisterRef destination, GesRegisterRef iterator, MapSelectorNode map, LoweringContext context, ExpressionState state)
        {
            var itemBinding = state.AllocateTemporary(_builder, context);
            var keyEntry = EmitSelectorHelperExpression("map_key", map.Identifier, map.KeyProjection, context, state, itemBinding);
            if (map.ValueProjection is null)
            {
                _builder.StreamCollectMap(destination, iterator, itemBinding, keyEntry);
                return;
            }

            var valueEntry = EmitSelectorHelperExpression("map_value", map.Identifier, map.ValueProjection, context, state, itemBinding);
            _builder.StreamCollectMapValue(destination, iterator, itemBinding, keyEntry, valueEntry);
        }

        private void EmitObjectMatch(GesRegisterRef destination, GesRegisterRef target, ObjectMatchPatternNode pattern, LoweringContext context, ExpressionState state)
        {
            var iterator = state.AllocateTemporary(_builder, context);
            var matchIterator = state.AllocateTemporary(_builder, context);
            var entry = EmitObjectMatchHelperExpression(pattern, context, out var itemBinding, out var captures);
            _builder.StreamCreate(iterator, target);
            _builder.StreamMap(matchIterator, iterator, entry, itemBinding, captures);
            _builder.HasAny(destination, matchIterator);
        }

        private GesRegisterRef EmitObjectPatternPredicate(GesRegisterRef target, ObjectMatchPatternNode pattern, LoweringContext context, ExpressionState state)
        {
            var result = state.AllocateTemporary(_builder, context);
            _builder.LoadTrue(result);
            foreach (var entry in pattern.Entries)
            {
                var member = state.AllocateTemporary(_builder, context);
                _builder.MemberAccess(member, entry.Key, target);
                GesRegisterRef entryMatch;
                switch (entry.Value)
                {
                    case ObjectMatchExpressionValueNode expression:
                    {
                        var expected = EmitExpressionForRead(expression.Expression, context, state);
                        entryMatch = state.AllocateTemporary(_builder, context);
                        _builder.Equal(entryMatch, member, expected);
                        break;
                    }
                    case ObjectMatchNestedValueNode nested:
                        entryMatch = EmitObjectPatternPredicate(member, nested.Pattern, context, state);
                        break;
                    default:
                        throw new GameEventScriptCompileException($"GameEventScript binary compiler does not support object match value '{entry.Value.GetType().Name}'.");
                }

                var combined = state.AllocateTemporary(_builder, context);
                _builder.And(combined, result, entryMatch);
                result = combined;
            }

            return result;
        }

        private void EmitSlice(GesRegisterRef destination, GesRegisterRef target, SequenceSliceSelectorNode slice)
        {
            var count = ToShort(slice.Count, "slice count");
            switch (slice.Operation, slice.Scope)
            {
                case ("take", "first"):
                    _builder.TakeFirst(destination, target, count);
                    return;
                case ("take", "last"):
                    _builder.TakeLast(destination, target, count);
                    return;
                case ("take", "highest"):
                    _builder.TakeHighest(destination, target, count);
                    return;
                case ("take", "lowest"):
                    _builder.TakeLowest(destination, target, count);
                    return;
                case ("drop", "first"):
                    _builder.DropFirst(destination, target, count);
                    return;
                case ("drop", "last"):
                    _builder.DropLast(destination, target, count);
                    return;
                case ("drop", "highest"):
                    _builder.DropHighest(destination, target, count);
                    return;
                case ("drop", "lowest"):
                    _builder.DropLowest(destination, target, count);
                    return;
                default:
                    throw new GameEventScriptCompileException($"GameEventScript binary compiler does not support slice selector '{slice.Operation} {slice.Scope}'.");
            }
        }

        private void EmitContains(GesRegisterRef destination, GesRegisterRef target, ContainsSelectorNode contains, LoweringContext context, ExpressionState state)
        {
            var needle = EmitExpressionForRead(contains.ValueExpression, context, state);
            switch (contains.Mode)
            {
                case "all":
                    _builder.ContainsAll(destination, needle, target);
                    break;
                case "any":
                    _builder.ContainsAny(destination, needle, target);
                    break;
                default:
                    _builder.Contains(destination, needle, target);
                    break;
            }
        }

        private void EmitGeneratedCollectionInto(GeneratedCollectionExpressionNode generatedCollection, GesRegisterRef destination, LoweringContext context, ExpressionState state)
        {
            if (generatedCollection.CollectionType != "list")
            {
                throw new GameEventScriptCompileException($"GameEventScript binary compiler does not support generated collection type '{generatedCollection.CollectionType}'.");
            }

            var builderRegister = state.AllocateTemporary(_builder, context);
            _builder.ListBuilderCreate(builderRegister);
            var collectionContext = context.CreateChild();
            var item = collectionContext.Declare(generatedCollection.Identifier);
            var iterator = EmitIterator(generatedCollection.Source, context, state);
            var loopLabel = _builder.AddLabel("generated_next");
            var skipProjectionLabel = generatedCollection.Predicate is null ? (GesLabelRef?)null : _builder.AddLabel("generated_skip");
            var endLabel = _builder.AddLabel("generated_end");

            _builder.MarkLabel(loopLabel);
            _builder.StreamNext(item, iterator, endLabel);
            if (generatedCollection.Predicate is not null)
            {
                var predicate = EmitExpressionForRead(generatedCollection.Predicate, collectionContext, state);
                _builder.JumpIfNotTrue(predicate, skipProjectionLabel!.Value);
            }

            var projection = EmitExpressionForRead(generatedCollection.Projection, collectionContext, state);
            _builder.ListBuilderAdd(builderRegister, projection);
            if (skipProjectionLabel.HasValue)
            {
                _builder.MarkLabel(skipProjectionLabel.Value);
            }

            _builder.Jump(loopLabel);
            _builder.MarkLabel(endLabel);
            _builder.StreamClose(iterator);
            _builder.ListBuilderFinish(destination, builderRegister);
        }

        private void EmitGuardedChoiceInto(GuardedChoiceExpressionNode guardedChoice, GesRegisterRef destination, LoweringContext context, ExpressionState state)
        {
            var endLabel = _builder.AddLabel("choice_end");
            foreach (var branch in guardedChoice.Branches)
            {
                var nextBranchLabel = _builder.AddLabel("choice_next");
                var condition = EmitExpressionForRead(branch.ConditionExpression, context, state);
                _builder.JumpIfNotTrue(condition, nextBranchLabel);
                var value = EmitExpressionForRead(branch.ValueExpression, context, state);
                if (value.Id != destination.Id) _builder.Move(destination, value);
                _builder.Jump(endLabel);
                _builder.MarkLabel(nextBranchLabel);
            }

            var otherwiseValue = EmitExpressionForRead(guardedChoice.OtherwiseExpression, context, state);
            if (otherwiseValue.Id != destination.Id) _builder.Move(destination, otherwiseValue);
            _builder.MarkLabel(endLabel);
        }

        private GesRegisterRef EmitIterator(IterationSourceNode source, LoweringContext context, ExpressionState state)
        {
            switch (source)
            {
                case RangeIterationSourceNode range when TryGetRangeIteratorShort(range.RangeExpression, out var from, out var to, out var step):
                {
                    var iterator = state.AllocateTemporary(_builder, context);
                    _builder.CreateRangeIteratorShort(iterator, from, to, step);
                    return iterator;
                }

                case RangeIterationSourceNode range:
                {
                    var from = EmitExpressionForRead(range.RangeExpression.FromExpression, context, state);
                    var to = EmitExpressionForRead(range.RangeExpression.ToExpression, context, state);
                    var iterator = state.AllocateTemporary(_builder, context);
                    if (range.RangeExpression.StepExpression is null)
                    {
                        _builder.CreateRangeIterator(iterator, from, to);
                    }
                    else
                    {
                        _builder.CreateRangeIteratorWithStep(iterator, from, to, EmitExpressionForRead(range.RangeExpression.StepExpression, context, state));
                    }

                    return iterator;
                }

                case CollectionIterationSourceNode collection:
                {
                    var collectionRegister = EmitExpressionForRead(collection.Expression, context, state);
                    var iterator = state.AllocateTemporary(_builder, context);
                    _builder.StreamCreate(iterator, collectionRegister);
                    return iterator;
                }

                default:
                    throw new GameEventScriptCompileException($"GameEventScript binary compiler does not support iteration source '{source.GetType().Name}'.");
            }
        }

        private StageArgumentPlan PrepareStageArgument(ExpressionNode expression, LoweringContext context, ExpressionState state)
        {
            return IsStageConstant(expression)
                ? StageArgumentPlan.FromExpression(expression)
                : StageArgumentPlan.FromRegister(EmitExpressionForRead(expression, context, state));
        }

        private void EmitStageArguments(IEnumerable<ExpressionNode> expressions, LoweringContext context, ExpressionState state)
        {
            var arguments = expressions.Select(expression => PrepareStageArgument(expression, context, state)).ToArray();
            foreach (var argument in arguments) EmitStageArgument(argument);
        }

        private void EmitStageArgument(StageArgumentPlan argument)
        {
            if (argument.Kind == StageArgumentKind.Nothing)
            {
                _builder.StageNothing();
                return;
            }

            if (argument.Register.HasValue)
            {
                _builder.StageRegister(argument.Register.Value);
                return;
            }

            EmitStageConstant(argument.Expression!);
        }

        private static bool IsStageConstant(ExpressionNode expression)
            => expression is BooleanLiteralExpressionNode or
                IntegerLiteralExpressionNode or
                UnitIntegerLiteralExpressionNode or
                FloatLiteralExpressionNode or
                UnitFloatLiteralExpressionNode or
                PercentageLiteralExpressionNode or
                TextLiteralExpressionNode or
                TagLiteralExpressionNode or
                IdentifierExpressionNode { Name: "nothing" };

        private void EmitStageConstant(ExpressionNode expression)
        {
            using var sourceRange = _builder.SourceRange(expression.SourceRange);
            switch (expression)
            {
                case BooleanLiteralExpressionNode boolean:
                    _builder.StageBoolean(boolean.Value);
                    return;
                case IntegerLiteralExpressionNode integer:
                    _builder.StageInteger(integer.Value);
                    return;
                case UnitIntegerLiteralExpressionNode integer:
                    _builder.StageInteger(integer.Value, ResolveUnitOrNone(integer.UnitName));
                    return;
                case FloatLiteralExpressionNode number:
                    _builder.StageFloat(number.Value);
                    return;
                case UnitFloatLiteralExpressionNode number:
                    _builder.StageFloat(number.Value, ResolveUnitOrNone(number.UnitName));
                    return;
                case PercentageLiteralExpressionNode percentage:
                    _builder.StagePercentage(percentage.PercentValue / 100d);
                    return;
                case TextLiteralExpressionNode text:
                    _builder.StageText(text.Value);
                    return;
                case TagLiteralExpressionNode tag:
                    _builder.StageTag(tag.Name);
                    return;
                case IdentifierExpressionNode { Name: "nothing" }:
                    _builder.StageNothing();
                    return;
                default:
                    throw new GameEventScriptCompileException($"GameEventScript binary compiler cannot stage non-constant expression '{expression.GetType().Name}' without preparing it first.");
            }
        }

        private void EmitRandomPush(ExpressionNode seedExpression, LoweringContext context, ExpressionState state)
        {
            if (TryReadUnitlessIntegerLiteralSeed(seedExpression, out var seed))
            {
                _builder.RandomPushConstant(seed);
                return;
            }

            _builder.RandomPush(EmitExpressionForRead(seedExpression, context, state));
        }

        private void EmitCallInto(CallExpressionNode call, GesRegisterRef destination, LoweringContext context, ExpressionState state)
        {
            if (!module.Callables.TryGetValue(call.Name, out var callable) || !_callableEntries.TryGetValue(call.Name, out var entry))
            {
                throw new GameEventScriptCompileException($"GameEventScript binary compiler could not resolve callable '{call.Name}'.");
            }

            EmitStageArguments(call.Arguments, context, state);

            _builder.Call(destination, entry, callable.Kind == GameEventScriptCallableKind.PredicateCall ? GameEventScriptInstructionFlag.NormalizeResultAsPredicate : GameEventScriptInstructionFlag.None);
        }

        private void EmitHandlerBindCallInto(CallExpressionNode call, GesRegisterRef destination, LoweringContext context, ExpressionState state)
        {
            var handler = context.Require(call.Name);
            if (context.TryResolveHandlerSignature(call.Name, out var signature) &&
                !CallArgumentsMatchHandlerSignature(call.ArgumentList.Arguments, signature))
            {
                foreach (var argument in call.ArgumentList.Arguments)
                {
                    EmitExpressionForRead(argument.Expression, context, state);
                }

                _builder.LoadNothing(destination);
                return;
            }

            var arguments = call.Arguments.Select(argument => EmitExpressionForRead(argument, context, state)).ToArray();
            _builder.BindHandler(destination, handler, arguments);
        }

        private void EmitPredicateCallInto(PredicateCallExpressionNode predicate, GesRegisterRef destination, LoweringContext context, ExpressionState state)
        {
            if (!module.Callables.TryGetValue(predicate.PredicateName, out var callable) ||
                callable.Kind != GameEventScriptCallableKind.PredicateCall ||
                !_callableEntries.TryGetValue(predicate.PredicateName, out var entry))
            {
                throw new GameEventScriptCompileException($"GameEventScript binary compiler could not resolve predicate '{predicate.PredicateName}'.");
            }

            EmitStageArgument(PrepareStageArgument(predicate.Value, context, state));
            _builder.Call(destination, entry, GameEventScriptInstructionFlag.NormalizeResultAsPredicate);
        }

        private void EmitExtensionPredicateInto(ExtensionPredicateExpressionNode extensionPredicate, GesRegisterRef destination, LoweringContext context, ExpressionState state)
        {
            var reference = new GameEventScriptExtensionReference(
                extensionPredicate.ExtensionName,
                extensionPredicate.FunctionName,
                [GameEventScriptMessageSignature.UnlabeledParameterName]);
            var argument = EmitExpressionForRead(extensionPredicate.Value, context, state);
            EmitExtensionReferenceInto(reference, destination, [argument], isPredicate: true);
        }

        private void EmitExtensionCallInto(ExtensionCallExpressionNode extensionCall, GesRegisterRef destination, LoweringContext context, ExpressionState state, bool isPredicate)
        {
            var argumentNames = extensionCall.Arguments.Select(argument => argument.Name).ToArray();
            var arguments = extensionCall.Arguments.Select(argument => EmitExpressionForRead(argument.Expression, context, state)).ToArray();
            var reference = new GameEventScriptExtensionReference(extensionCall.ExtensionName, extensionCall.FunctionName, argumentNames);
            EmitExtensionReferenceInto(reference, destination, arguments, isPredicate);
        }

        private void EmitExtensionReferenceInto(GameEventScriptExtensionReference reference, GesRegisterRef destination, IReadOnlyList<GesRegisterRef> arguments, bool isPredicate)
        {
            if (GesStandardExtensions.IsStandardReference(reference))
            {
                _builder.CallStandard(destination, ExtensionShape(reference), arguments, isPredicate ? GameEventScriptInstructionFlag.NormalizeResultAsPredicate : GameEventScriptInstructionFlag.None);
                return;
            }

            _builder.CallExternal(destination, ResolveExtensionCall(reference), arguments, isPredicate ? GameEventScriptInstructionFlag.NormalizeResultAsPredicate : GameEventScriptInstructionFlag.None);
        }

        private void EmitTypeConstructorInto(TypeConstructorExpressionNode constructor, GesRegisterRef destination, LoweringContext context, ExpressionState state)
        {
            if (constructor.TypeName is "vector" or "point" && TryEmitSpatialConstructor(constructor, destination, context, state)) return;

            if (constructor.Arguments.Count == 1 && constructor.Arguments[0].Label is null && IsBuiltInCastType(constructor.TypeName))
            {
                EmitCastInto(destination, EmitExpressionForRead(constructor.Arguments[0].Expression, context, state), constructor.TypeName);
                return;
            }

            if (module.TypeDefinitions.TryGetValue(constructor.TypeName, out var type))
            {
                var labeled = constructor.Arguments.Where(argument => argument.Label is not null).ToDictionary(argument => argument.Name, StringComparer.Ordinal);
                var unlabeled = constructor.Arguments.Where(argument => argument.Label is null).ToArray();
                var unlabeledIndex = 0;
                var stagedArguments = new List<StageArgumentPlan>();
                foreach (var field in type.Fields)
                {
                    if (!field.IsConstructorParameter) continue;
                    if (field.ConstructorLabel == GameEventScriptMessageSignature.UnlabeledParameterName)
                    {
                        stagedArguments.Add(unlabeledIndex < unlabeled.Length ? PrepareStageArgument(unlabeled[unlabeledIndex++].Expression, context, state) : StageArgumentPlan.Nothing);
                    }
                    else if (labeled.TryGetValue(field.ConstructorLabel!, out var argument))
                    {
                        stagedArguments.Add(PrepareStageArgument(argument.Expression, context, state));
                    }
                    else
                    {
                        stagedArguments.Add(StageArgumentPlan.Nothing);
                    }
                }

                foreach (var argument in stagedArguments) EmitStageArgument(argument);
                _builder.CreateRecord(destination, ResolveRecordConstructor(type));
                return;
            }

            if (module.ExternalTypeDefinitions.ContainsKey(constructor.TypeName))
            {
                var argumentNames = constructor.Arguments.Select(argument => argument.Name).ToArray();
                EmitStageArguments(constructor.Arguments.Select(argument => argument.Expression), context, state);
                _builder.CreateExternalType(destination, ResolveExternalTypeConstructor(constructor.TypeName, argumentNames), argumentNames);
                return;
            }

            throw new GameEventScriptCompileException($"GameEventScript binary compiler does not support type constructor ':{constructor.TypeName}'.");
        }

        private bool TryEmitSpatialConstructor(TypeConstructorExpressionNode constructor, GesRegisterRef destination, LoweringContext context, ExpressionState state)
        {
            if (!TryGetSpatialConstructorStageShape(constructor.Arguments, out var startComponent, out var arguments)) return false;
            EmitStageArguments(arguments.Select(argument => argument.Expression), context, state);
            if (constructor.TypeName == "point") _builder.CreatePoint(destination, (short)startComponent);
            else _builder.CreateVector(destination, (short)startComponent);
            return true;
        }

        private void EmitCastInto(GesRegisterRef destination, GesRegisterRef value, string? typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                throw new GameEventScriptCompileException("GameEventScript binary compiler requires a type name.");
            }

            if (TryGetQuantityUnit(typeName, out var unit))
            {
                _builder.CastUnit(destination, value, unit);
            }
            else if (GameEventScriptBytecodeInstructionUnits.IsQuantityTypeName(typeName))
            {
                throw new GameEventScriptCompileException($"GameEventScript binary compiler does not support quantity type '{typeName}'.");
            }
            else if (typeName is "number" or "numeric" or "numeric:integer" or "numeric:fractional")
            {
                _builder.CastNumeric(destination, value);
            }
            else if (TryGetBytecodeTypeKind(typeName, out var kind))
            {
                _builder.Cast(destination, value, kind);
            }
            else
            {
                _builder.CastCustom(destination, value, typeName);
            }
        }

        private void EmitCheckInto(GesRegisterRef destination, GesRegisterRef value, string? typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                throw new GameEventScriptCompileException("GameEventScript binary compiler requires a type name.");
            }

            if (TryGetQuantityUnit(typeName, out var unit))
            {
                _builder.CheckUnit(destination, value, unit);
            }
            else if (GameEventScriptBytecodeInstructionUnits.IsQuantityTypeName(typeName))
            {
                throw new GameEventScriptCompileException($"GameEventScript binary compiler does not support quantity type '{typeName}'.");
            }
            else if (typeName is "number" or "numeric")
            {
                _builder.CheckNumeric(destination, value);
            }
            else if (typeName == "numeric:integer")
            {
                _builder.CheckInteger(destination, value);
            }
            else if (typeName == "numeric:fractional")
            {
                _builder.CheckFractional(destination, value);
            }
            else if (TryGetBytecodeTypeKind(typeName, out var kind))
            {
                _builder.CheckType(destination, value, kind);
            }
            else
            {
                _builder.CheckCustomType(destination, value, typeName);
            }
        }

        private void EmitUnary(GesRegisterRef destination, GesUnaryOperator operation, GesRegisterRef operand)
        {
            switch (operation)
            {
                case GesUnaryOperator.Negate:
                    _builder.Negate(destination, operand);
                    return;
                case GesUnaryOperator.Not:
                    _builder.Not(destination, operand);
                    return;
                case GesUnaryOperator.HasValue:
                    _builder.HasValue(destination, operand);
                    return;
                case GesUnaryOperator.Empty:
                    _builder.IsEmpty(destination, operand);
                    return;
                case GesUnaryOperator.Length:
                    _builder.Length(destination, operand);
                    return;
                case GesUnaryOperator.Chance:
                    _builder.Chance(destination, operand);
                    return;
                case GesUnaryOperator.Keys:
                    _builder.KeysOfMap(destination, operand);
                    return;
                case GesUnaryOperator.Values:
                    _builder.ValuesOfMap(destination, operand);
                    return;
                case GesUnaryOperator.Entries:
                    _builder.EntriesOfMap(destination, operand);
                    return;
                case GesUnaryOperator.Abs:
                    _builder.Abs(destination, operand);
                    return;
                case GesUnaryOperator.NaturalLog:
                    _builder.LogN(destination, operand);
                    return;
                default:
                    throw new GameEventScriptCompileException($"GameEventScript binary compiler does not support unary operator '{operation.ToSourceText()}'.");
            }
        }

        private void EmitBinary(GesRegisterRef destination, GesBinaryOperator operation, GesRegisterRef left, GesRegisterRef right)
        {
            switch (operation)
            {
                case GesBinaryOperator.Or:
                    _builder.Or(destination, left, right);
                    return;
                case GesBinaryOperator.Xor:
                    _builder.Xor(destination, left, right);
                    return;
                case GesBinaryOperator.And:
                    _builder.And(destination, left, right);
                    return;
                case GesBinaryOperator.Implies:
                    _builder.Implies(destination, left, right);
                    return;
                case GesBinaryOperator.Equal:
                    _builder.Equal(destination, left, right);
                    return;
                case GesBinaryOperator.NotEqual:
                    _builder.NotEqual(destination, left, right);
                    return;
                case GesBinaryOperator.Less:
                    _builder.Less(destination, left, right);
                    return;
                case GesBinaryOperator.Greater:
                    _builder.Greater(destination, left, right);
                    return;
                case GesBinaryOperator.LessOrEqual:
                    _builder.LessOrEqual(destination, left, right);
                    return;
                case GesBinaryOperator.GreaterOrEqual:
                    _builder.GreaterOrEqual(destination, left, right);
                    return;
                case GesBinaryOperator.Add:
                    _builder.Add(destination, left, right);
                    return;
                case GesBinaryOperator.Subtract:
                    _builder.Subtract(destination, left, right);
                    return;
                case GesBinaryOperator.Multiply:
                    _builder.Multiply(destination, left, right);
                    return;
                case GesBinaryOperator.Divide:
                    _builder.Divide(destination, left, right);
                    return;
                case GesBinaryOperator.IntegerDivide:
                    _builder.IntegerDivide(destination, left, right);
                    return;
                case GesBinaryOperator.Modulo:
                    _builder.Modulo(destination, left, right);
                    return;
                case GesBinaryOperator.Remainder:
                    _builder.Remainder(destination, left, right);
                    return;
                case GesBinaryOperator.Power:
                    _builder.Power(destination, left, right);
                    return;
                case GesBinaryOperator.Default:
                    _builder.Default(destination, left, right);
                    return;
                case GesBinaryOperator.Contains:
                    _builder.Contains(destination, left, right);
                    return;
                case GesBinaryOperator.ContainsValue:
                    _builder.ContainsValue(destination, left, right);
                    return;
                case GesBinaryOperator.StartsWith:
                    _builder.StartsWith(destination, left, right);
                    return;
                case GesBinaryOperator.EndsWith:
                    _builder.EndsWith(destination, left, right);
                    return;
                case GesBinaryOperator.Union:
                    _builder.Union(destination, left, right);
                    return;
                case GesBinaryOperator.Intersect:
                    _builder.Intersect(destination, left, right);
                    return;
                case GesBinaryOperator.Zip:
                    _builder.Zip(destination, left, right);
                    return;
                default:
                    throw new GameEventScriptCompileException($"GameEventScript binary compiler does not support binary operator '{operation.ToSourceText()}'.");
            }
        }

        private GesBindRef ResolveOutboundMessage(string name, IReadOnlyList<string> argumentNames)
        {
            var key = GameEventScriptMessageSignature.CreateSignatureId(name, argumentNames);
            if (_outboundMessages.TryGetValue(key, out var bind)) return bind;
            bind = _builder.AddBind(GameEventScriptBinaryBindKind.OutboundMessage, name, argumentNames);
            _outboundMessages[key] = bind;
            return bind;
        }

        private GesBindRef ResolveExtensionCall(GameEventScriptExtensionReference reference)
        {
            if (_extensionCalls.TryGetValue(reference.SignatureId, out var bind)) return bind;
            bind = _builder.AddBind(GameEventScriptBinaryBindKind.ExtensionCall, reference.ExtensionName + "." + reference.FunctionName, reference.ArgumentLabels);
            _extensionCalls[reference.SignatureId] = bind;
            return bind;
        }

        private GesBindRef ResolveRecordConstructor(TypeDefinitionNode type)
        {
            if (_recordConstructors.TryGetValue(type.Name, out var bind)) return bind;
            throw new GameEventScriptCompileException($"GameEventScript record constructor ':{type.Name}' was not emitted.");
        }

        private GesBindRef ResolveExternalTypeConstructor(string typeName, IReadOnlyList<string> argumentNames)
        {
            var reference = new GameEventScriptExternalTypeConstructorReference(typeName, argumentNames);
            if (!module.ExternalTypeDefinitions.TryGetValue(reference.TypeName, out var typeDefinition))
            {
                throw new GameEventScriptCompileException($"GameEventScript external type ':{reference.TypeName}' is not registered.");
            }

            if (!typeDefinition.TryGetConstructor(reference.ArgumentLabels, out _))
            {
                throw new GameEventScriptCompileException($"GameEventScript external type constructor ':{reference.SignatureId}' is not registered.");
            }

            if (_externalTypeConstructors.TryGetValue(reference.SignatureId, out var bind)) return bind;
            bind = _builder.AddBind(GameEventScriptBinaryBindKind.ExternalType, typeName, argumentNames);
            _externalTypeConstructors[reference.SignatureId] = bind;
            return bind;
        }

        private static IReadOnlyList<string> MessageShape(string name, IReadOnlyList<string> argumentNames)
        {
            var shape = new string[argumentNames.Count + 1];
            shape[0] = GameEventScriptMessageSignature.NormalizeMessageName(name);
            for (var index = 0; index < argumentNames.Count; index++) shape[index + 1] = argumentNames[index];
            return shape;
        }

        private static bool TryClassifyHandlerSignature(ExpressionNode expression, LoweringContext context, out GameEventScriptMessageSignature signature)
        {
            switch (expression)
            {
                case HandlerLiteralExpressionNode handler:
                    signature = GameEventScriptMessageSignature.Create(handler.Message, handler.SignatureLabels);
                    return true;
                case IdentifierExpressionNode identifier when context.TryResolveHandlerSignature(identifier.Name, out signature):
                    return true;
                default:
                    signature = GameEventScriptMessageSignature.Empty;
                    return false;
            }
        }

        private static bool CallArgumentsMatchHandlerSignature(IReadOnlyList<ArgumentNode> arguments, GameEventScriptMessageSignature signature)
        {
            if (arguments.Count != signature.Parameters.Count) return false;
            for (var index = 0; index < arguments.Count; index++)
            {
                var label = arguments[index].Label;
                if (label is null) continue;
                if (!string.Equals(
                        GameEventScriptMessageSignature.NormalizeParameterName(label),
                        signature.Parameters[index],
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static IReadOnlyList<string> ExtensionShape(GameEventScriptExtensionReference reference)
        {
            var shape = new string[reference.ArgumentLabels.Count + 2];
            shape[0] = reference.ExtensionName;
            shape[1] = reference.FunctionName;
            for (var index = 0; index < reference.ArgumentLabels.Count; index++) shape[index + 2] = reference.ArgumentLabels[index];
            return shape;
        }

        private static bool TryReadUnitlessIntegerLiteralSeed(ExpressionNode expression, out long seed)
        {
            if (expression is IntegerLiteralExpressionNode integer)
            {
                seed = integer.Value;
                return true;
            }

            if (expression is UnaryExpressionNode { Operator: GesUnaryOperator.Negate, Operand: IntegerLiteralExpressionNode negated } &&
                negated.Value != long.MinValue)
            {
                seed = -negated.Value;
                return true;
            }

            seed = 0;
            return false;
        }

        private static bool TryGetRangeIteratorShort(RangeExpressionNode range, out short from, out short to, out short step)
        {
            step = 0;
            if (TryGetShortIntegerLiteral(range.FromExpression, out from) &&
                TryGetShortIntegerLiteral(range.ToExpression, out to) &&
                (range.StepExpression is null || TryGetShortIntegerLiteral(range.StepExpression, out step)))
            {
                if (range.StepExpression is null) step = 1;
                return true;
            }

            from = 0;
            to = 0;
            return false;
        }

        private static bool TryGetShortIntegerLiteral(ExpressionNode expression, out short value)
        {
            if (expression is IntegerLiteralExpressionNode { Value: >= short.MinValue and <= short.MaxValue } integer)
            {
                value = (short)integer.Value;
                return true;
            }

            value = 0;
            return false;
        }

        private static bool TryGetSpatialConstructorStageShape(
            IReadOnlyList<ArgumentNode> sourceArguments,
            out int startComponent,
            out IReadOnlyList<ArgumentNode> stagedArguments)
        {
            startComponent = 0;
            stagedArguments = sourceArguments;
            if (sourceArguments.Count == 0) return true;
            var labeledCount = sourceArguments.Count(argument => argument.Label is not null);
            if (labeledCount == 0) return true;
            if (labeledCount != sourceArguments.Count) return false;
            var firstComponent = GetSpatialComponentIndex(sourceArguments[0].Label);
            var lastComponent = GetSpatialComponentIndex(sourceArguments[^1].Label);
            if (firstComponent < 0 || lastComponent < 0) return false;
            if (lastComponent - firstComponent + 1 != sourceArguments.Count) return false;
            if (firstComponent == 0 && sourceArguments.Count == 1) return false;
            for (var index = 0; index < sourceArguments.Count; index++)
            {
                if (GetSpatialComponentIndex(sourceArguments[index].Label) != firstComponent + index) return false;
            }

            startComponent = firstComponent;
            return true;
        }

        private static int GetSpatialComponentIndex(string? label)
            => label switch
            {
                "x" => 0,
                "y" => 1,
                "z" => 2,
                _ => -1
            };

        private static short ToShort(int value, string name)
            => value is < short.MinValue or > short.MaxValue
                ? throw new GameEventScriptCompileException($"GameEventScript binary compiler {name} must fit into Int16.")
                : (short)value;

        private static GameEventScriptBytecodeInstructionUnit ResolveUnitOrNone(string unitName)
            => GameEventScriptBytecodeInstructionUnits.TryParseTypeName(unitName, out var unit)
                ? unit
                : GameEventScriptBytecodeInstructionUnit.UnitNone;

        private static bool IsBuiltInCastType(string typeName)
            => typeName is "number" or "numeric" or "numeric:integer" or "numeric:fractional" ||
               GameEventScriptBytecodeInstructionUnits.IsQuantityTypeName(typeName) ||
               TryGetBytecodeTypeKind(typeName, out _);

        private static bool TryGetQuantityUnit(string typeName, out GameEventScriptBytecodeInstructionUnit unit)
            => GameEventScriptBytecodeInstructionUnits.TryParseQuantityTypeName(typeName, out unit);

        private static bool TryGetBytecodeTypeKind(string typeName, out GameEventScriptBytecodeTypeKind typeKind)
        {
            typeKind = typeName switch
            {
                "nothing" => GameEventScriptBytecodeTypeKind.Nothing,
                "boolean" => GameEventScriptBytecodeTypeKind.Boolean,
                "percentage" => GameEventScriptBytecodeTypeKind.Percentage,
                "vector" => GameEventScriptBytecodeTypeKind.Vector,
                "point" => GameEventScriptBytecodeTypeKind.Point,
                "series" => GameEventScriptBytecodeTypeKind.Series,
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

        private IReadOnlyList<StreamCapture> ResolveStreamCaptures(ExpressionNode expression, string itemIdentifier, LoweringContext context)
        {
            var identifiers = new List<string>();
            CollectReferencedIdentifiers(expression, identifiers, WithBound(new HashSet<string>(StringComparer.Ordinal), itemIdentifier));
            return ResolveStreamCaptures(identifiers, context);
        }

        private IReadOnlyList<StreamCapture> ResolveStreamCaptures(ObjectMatchPatternNode pattern, LoweringContext context)
        {
            var identifiers = new List<string>();
            CollectReferencedIdentifiers(pattern, identifiers, new HashSet<string>(StringComparer.Ordinal));
            return ResolveStreamCaptures(identifiers, context);
        }

        private IReadOnlyList<StreamCapture> ResolveStreamCaptures(IReadOnlyList<string> identifiers, LoweringContext context)
        {
            var captures = new List<StreamCapture>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var identifier in identifiers)
            {
                if (!seen.Add(identifier)) continue;
                captures.Add(new StreamCapture(identifier, context.Require(identifier)));
            }

            return captures;
        }

        private static void CollectReferencedIdentifiers(ExpressionNode expression, List<string> identifiers, ISet<string> bound)
        {
            switch (expression)
            {
                case IdentifierExpressionNode { Name: "nothing" }:
                    break;
                case IdentifierExpressionNode identifier:
                    if (!bound.Contains(identifier.Name)) identifiers.Add(identifier.Name);
                    break;
                case MessageLiteralExpressionNode message:
                    foreach (var argument in message.Arguments) CollectReferencedIdentifiers(argument.Expression, identifiers, bound);
                    break;
                case ListLiteralExpressionNode list:
                    foreach (var item in list.Items) CollectReferencedIdentifiers(item, identifiers, bound);
                    break;
                case MapLiteralExpressionNode map:
                    foreach (var entry in map.Entries) CollectReferencedIdentifiers(entry.Value, identifiers, bound);
                    break;
                case UnaryExpressionNode unary:
                    CollectReferencedIdentifiers(unary.Operand, identifiers, bound);
                    break;
                case VariadicTaggedExpressionNode variadic:
                    foreach (var argument in variadic.Arguments) CollectReferencedIdentifiers(argument, identifiers, bound);
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
                    if (range.StepExpression is not null) CollectReferencedIdentifiers(range.StepExpression, identifiers, bound);
                    break;
                case SeededRandomExpressionNode seededRandom:
                    CollectReferencedIdentifiers(seededRandom.SeedExpression, identifiers, bound);
                    CollectReferencedIdentifiers(seededRandom.BodyExpression, identifiers, bound);
                    break;
                case GeneratedCollectionExpressionNode generatedCollection:
                {
                    CollectReferencedIdentifiers(generatedCollection.Source, identifiers, bound);
                    var collectionBound = WithBound(bound, generatedCollection.Identifier);
                    if (generatedCollection.Predicate is not null) CollectReferencedIdentifiers(generatedCollection.Predicate, identifiers, collectionBound);
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
                    foreach (var argument in call.Arguments) CollectReferencedIdentifiers(argument, identifiers, bound);
                    break;
                case TypeCastExpressionNode typeCast:
                    CollectReferencedIdentifiers(typeCast.Value, identifiers, bound);
                    break;
                case TypeConstructorExpressionNode typeConstructor:
                    foreach (var argument in typeConstructor.Arguments) CollectReferencedIdentifiers(argument.Expression, identifiers, bound);
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
                    foreach (var argument in extensionCall.Arguments) CollectReferencedIdentifiers(argument.Expression, identifiers, bound);
                    break;
            }
        }

        private static void CollectReferencedIdentifiers(CollectionSelectorNode selector, List<string> identifiers, ISet<string> bound)
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
                case EdgeSelectorNode { Predicate: not null } edge when !string.IsNullOrEmpty(edge.Identifier):
                    CollectReferencedIdentifiers(edge.Predicate, identifiers, WithBound(bound, edge.Identifier!));
                    break;
                case MinSelectorNode min:
                    CollectReferencedIdentifiers(min.Projection, identifiers, WithBound(bound, min.Identifier));
                    break;
                case MaxSelectorNode max:
                    CollectReferencedIdentifiers(max.Projection, identifiers, WithBound(bound, max.Identifier));
                    break;
                case MapSelectorNode map:
                {
                    var mapBound = WithBound(bound, map.Identifier);
                    CollectReferencedIdentifiers(map.KeyProjection, identifiers, mapBound);
                    if (map.ValueProjection is not null) CollectReferencedIdentifiers(map.ValueProjection, identifiers, mapBound);
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
                case DistinctSelectorNode { Projection: not null } distinct when !string.IsNullOrEmpty(distinct.Identifier):
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

        private static void CollectReferencedIdentifiers(IterationSourceNode source, List<string> identifiers, ISet<string> bound)
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

        private static void CollectReferencedIdentifiers(DicePatternNode pattern, List<string> identifiers, ISet<string> bound)
        {
            if (pattern is DiceCountPatternNode { Face: { } face }) CollectReferencedIdentifiers(face, identifiers, bound);
        }

        private static void CollectReferencedIdentifiers(ObjectMatchPatternNode pattern, List<string> identifiers, ISet<string> bound)
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

        private readonly record struct StreamCapture(string Name, GesRegisterRef SourceRegister);
    }

    private sealed class LoweringContext
    {
        private readonly Dictionary<string, GesRegisterRef> _registers;
        private readonly Dictionary<string, GameEventScriptMessageSignature> _handlerSignatures;
        private readonly HashSet<string> _handlerSignatureShadows;
        private readonly LoweringContext? _parent;
        private readonly GesBinaryBuilder.GesBinaryRoutineScope _routine;

        private LoweringContext(GesBinaryBuilder.GesBinaryRoutineScope routine, LoweringContext? parent = null)
        {
            _routine = routine;
            _parent = parent;
            _registers = new Dictionary<string, GesRegisterRef>(StringComparer.Ordinal);
            _handlerSignatures = new Dictionary<string, GameEventScriptMessageSignature>(StringComparer.Ordinal);
            _handlerSignatureShadows = new HashSet<string>(StringComparer.Ordinal);
            RegisterCount = parent?.RegisterCount ?? routine.Arguments.Count;
        }

        public int RegisterCount { get; private set; }

        public static LoweringContext ForRoutine(GesBinaryBuilder.GesBinaryRoutineScope routine)
            => new(routine);

        public static LoweringContext ForRoutineWithParent(GesBinaryBuilder.GesBinaryRoutineScope routine, LoweringContext parent)
            => new(routine, parent);

        public LoweringContext CreateChild()
            => new(_routine, this);

        public void DeclareExisting(string name, GesRegisterRef register)
        {
            _registers[name] = register;
            RegisterCount = Math.Max(RegisterCount, register.Id + 1);
        }

        public GesRegisterRef Declare(string name)
        {
            var register = _routine.AddRegister(name);
            _registers[name] = register;
            RegisterCount++;
            return register;
        }

        public GesRegisterRef AddTemporary(string? name = null)
        {
            RegisterCount++;
            return _routine.AddTemporaryRegister(name);
        }

        public GesRegisterRef Require(string name)
        {
            if (_registers.TryGetValue(name, out var register)) return register;
            if (_parent is not null) return _parent.Require(name);
            throw new GameEventScriptCompileException($"GameEventScript binary compiler could not resolve identifier '{name}'.");
        }

        public void DeclareHandlerSignature(string name, GameEventScriptMessageSignature signature)
        {
            _handlerSignatures[name] = signature;
            _handlerSignatureShadows.Remove(name);
        }

        public void ClearHandlerSignature(string name)
        {
            _handlerSignatures.Remove(name);
            _handlerSignatureShadows.Add(name);
        }

        public bool TryResolveHandlerSignature(string name, out GameEventScriptMessageSignature signature)
        {
            if (_handlerSignatures.TryGetValue(name, out signature)) return true;
            if (_handlerSignatureShadows.Contains(name))
            {
                signature = GameEventScriptMessageSignature.Empty;
                return false;
            }

            if (_parent is not null) return _parent.TryResolveHandlerSignature(name, out signature);
            signature = GameEventScriptMessageSignature.Empty;
            return false;
        }
    }

    private sealed class ExpressionState(int nextRegister)
    {
        private int _nextRegister = Math.Max(0, nextRegister);

        public int NextRegister => _nextRegister;

        public GesRegisterRef AllocateTemporary(GesBinaryBuilder builder, LoweringContext context)
        {
            _nextRegister++;
            return context.AddTemporary();
        }
    }

    private enum StageArgumentKind
    {
        Nothing,
        Expression,
        Register
    }

    private readonly record struct StageArgumentPlan(StageArgumentKind Kind, ExpressionNode? Expression, GesRegisterRef? Register)
    {
        public static StageArgumentPlan Nothing { get; } = new(StageArgumentKind.Nothing, null, null);

        public static StageArgumentPlan FromExpression(ExpressionNode expression)
            => new(StageArgumentKind.Expression, expression, null);

        public static StageArgumentPlan FromRegister(GesRegisterRef register)
            => new(StageArgumentKind.Register, null, register);
    }
}
