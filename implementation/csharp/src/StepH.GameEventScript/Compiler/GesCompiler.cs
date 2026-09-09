// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime.VM;

namespace StepH.GameEventScript.Compiler;

internal static class GesCompiler
{
    public static GameEventScriptProgram Compile(GesSyntaxTreeModule module, GameEventScriptCompileOptions? options = null)
    {
        _ = module ?? throw new ArgumentNullException(nameof(module));
        var compileOptions = options ?? new GameEventScriptCompileOptions();
        var program = new BinaryCompiler(module, compileOptions).Build();
        GameEventScriptProgramValidator.Validate(program);
        return program;
    }

    private static GameEventScriptCompileException CompileFailure(string code, string message, string? symbol = null)
        => new(new GameEventScriptDiagnostic(
            GameEventScriptDiagnosticPhase.Compile,
            code,
            message,
            symbol));

    private sealed class BinaryCompiler(GesSyntaxTreeModule module, GameEventScriptCompileOptions options)
    {
        private readonly GesBinaryBuilder _builder = new GesBinaryBuilder()
            .WithModuleName(module.ModuleName)
            .WithProgramVersion(options.ProgramVersion)
            .WithDebugInfo(options.DebugInfo, module.Sources)
            .WithOptimization();

        private readonly Dictionary<string, GesBindRef> _outboundMessages = new(StringComparer.Ordinal);
        private readonly Dictionary<string, GesBindRef> _extensionCalls = new(StringComparer.Ordinal);
        private readonly Dictionary<string, GesBindRef> _recordConstructors = new(StringComparer.Ordinal);
        private readonly Dictionary<string, GesBindRef> _externalTypeConstructors = new(StringComparer.Ordinal);
        private readonly Dictionary<string, GesLabelRef> _callableEntries = new(StringComparer.Ordinal);
        private int _helperIndex;

        public GameEventScriptProgram Build()
        {
            EmitCallables();
            EmitRecordConstructors();
            EmitHandlers();
            return _builder.Build();
        }

        private static TypeDefinitionNode[] ReadOrderedTypes(IReadOnlyDictionary<string, TypeDefinitionNode> source)
        {
            if (source.Count == 0) return [];
            var result = new TypeDefinitionNode[source.Count];
            var offset = 0;
            if (source is Dictionary<string, TypeDefinitionNode> dictionary)
            {
                var enumerator = dictionary.GetEnumerator();
                while (enumerator.MoveNext()) result[offset++] = enumerator.Current.Value;
            }
            else
            {
                using var enumerator = source.GetEnumerator();
                while (enumerator.MoveNext()) result[offset++] = enumerator.Current.Value;
            }

            Array.Sort(result, static (left, right) => string.Compare(left.Name, right.Name, StringComparison.Ordinal));
            return result;
        }

        private static KeyValuePair<string, IReadOnlyList<EventHandlerNode>>[] ReadOrderedHandlerGroups(IReadOnlyDictionary<string, IReadOnlyList<EventHandlerNode>> source)
        {
            if (source.Count == 0) return [];
            var result = new KeyValuePair<string, IReadOnlyList<EventHandlerNode>>[source.Count];
            var offset = 0;
            if (source is Dictionary<string, IReadOnlyList<EventHandlerNode>> dictionary)
            {
                var enumerator = dictionary.GetEnumerator();
                while (enumerator.MoveNext()) result[offset++] = enumerator.Current;
            }
            else
            {
                using var enumerator = source.GetEnumerator();
                while (enumerator.MoveNext()) result[offset++] = enumerator.Current;
            }

            Array.Sort(result, static (left, right) => string.Compare(left.Key, right.Key, StringComparison.Ordinal));
            return result;
        }

        private static GesCallableDefinition[] ReadOrderedCallables(IReadOnlyDictionary<string, GesCallableDefinition> source)
        {
            if (source.Count == 0) return [];
            var result = new GesCallableDefinition[source.Count];
            var offset = 0;
            if (source is Dictionary<string, GesCallableDefinition> dictionary)
            {
                var enumerator = dictionary.GetEnumerator();
                while (enumerator.MoveNext()) result[offset++] = enumerator.Current.Value;
            }
            else
            {
                using var enumerator = source.GetEnumerator();
                while (enumerator.MoveNext()) result[offset++] = enumerator.Current.Value;
            }

            Array.Sort(result, static (left, right) => string.Compare(left.SignatureId, right.SignatureId, StringComparison.Ordinal));
            return result;
        }

        private static string[] ReadConstructorArgumentNames(IReadOnlyList<TypeFieldDefinitionNode> fields)
        {
            var count = CountConstructorParameters(fields);
            if (count == 0) return [];
            var result = new string[count];
            var offset = 0;
            for (var index = 0; index < fields.Count; index++)
            {
                var field = fields[index];
                if (!field.IsConstructorParameter) continue;
                result[offset++] = field.ConstructorLabel!;
            }

            return result;
        }

        private static string[] ReadFieldNames(IReadOnlyList<TypeFieldDefinitionNode> fields)
        {
            if (fields.Count == 0) return [];
            var result = new string[fields.Count];
            for (var index = 0; index < fields.Count; index++)
            {
                result[index] = fields[index].Name;
            }

            return result;
        }

        private static string[] ReadArgumentNames(IReadOnlyList<ArgumentNode> arguments)
        {
            if (arguments.Count == 0) return [];
            var result = new string[arguments.Count];
            for (var index = 0; index < arguments.Count; index++)
            {
                result[index] = arguments[index].Name;
            }

            return result;
        }

        private static string[] ReadMapKeys(IReadOnlyList<MapEntryNode> entries)
        {
            if (entries.Count == 0) return [];
            var result = new string[entries.Count];
            for (var index = 0; index < entries.Count; index++)
            {
                result[index] = entries[index].Key;
            }

            return result;
        }

        private GesRegisterRef[] EmitExpressionRegisters(IReadOnlyList<ExpressionNode> expressions, LoweringContext context, ExpressionState state)
        {
            if (expressions.Count == 0) return [];
            var result = new GesRegisterRef[expressions.Count];
            for (var index = 0; index < expressions.Count; index++)
            {
                result[index] = EmitExpressionForRead(expressions[index], context, state);
            }

            return result;
        }

        private GesRegisterRef[] EmitArgumentExpressionRegisters(IReadOnlyList<ArgumentNode> arguments, LoweringContext context, ExpressionState state)
        {
            if (arguments.Count == 0) return [];
            var result = new GesRegisterRef[arguments.Count];
            for (var index = 0; index < arguments.Count; index++)
            {
                result[index] = EmitExpressionForRead(arguments[index].Expression, context, state);
            }

            return result;
        }

        private static int CountConstructorParameters(IReadOnlyList<TypeFieldDefinitionNode> fields)
        {
            var count = 0;
            for (var index = 0; index < fields.Count; index++)
            {
                if (fields[index].IsConstructorParameter) count++;
            }

            return count;
        }

        private static ArgumentNode? FindUnlabeledArgument(IReadOnlyList<ArgumentNode> arguments, int unlabeledIndex)
        {
            var currentUnlabeledIndex = 0;
            for (var index = 0; index < arguments.Count; index++)
            {
                var argument = arguments[index];
                if (argument.Label is not null) continue;
                if (currentUnlabeledIndex == unlabeledIndex) return argument;
                currentUnlabeledIndex++;
            }

            return null;
        }

        private static ArgumentNode? FindLabeledArgument(IReadOnlyList<ArgumentNode> arguments, string label)
        {
            for (var index = 0; index < arguments.Count; index++)
            {
                var argument = arguments[index];
                if (argument.Label is not null && string.Equals(argument.Name, label, StringComparison.Ordinal)) return argument;
            }

            return null;
        }

        private static string ReadSingleOrDefault(IReadOnlyList<string> values, string fallback)
        {
            if (values.Count == 0) return fallback;
            if (values.Count == 1) return values[0];
            throw CompileFailure(GameEventScriptDiagnosticCodes.CompileInvalidArity, "Message-name handler can only have one message parameter.");
        }

        private void EmitRecordConstructors()
        {
            var orderedTypes = ReadOrderedTypes(module.TypeDefinitions);
            for (var typeIndex = 0; typeIndex < orderedTypes.Length; typeIndex++)
            {
                var type = orderedTypes[typeIndex];
                using var sourceRange = _builder.SourceRange(type.SourceRange);
                var argumentNames = ReadConstructorArgumentNames(type.Fields);
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

                for (var fieldIndex = 0; fieldIndex < fieldRegisters.Length; fieldIndex++)
                {
                    _builder.StageRegister(fieldRegisters[fieldIndex]);
                }

                var map = state.AllocateTemporary(_builder, context);
                _builder.CreateMap(map, ReadFieldNames(type.Fields));
                var record = state.AllocateTemporary(_builder, context);
                _builder.CreateRecordValue(record, map, type.Name);
                _builder.ReturnValue(record);
            }
        }

        private void EmitHandlers()
        {
            var orderedHandlerGroups = ReadOrderedHandlerGroups(module.Handlers);
            for (var groupIndex = 0; groupIndex < orderedHandlerGroups.Length; groupIndex++)
            {
                var handlerGroup = orderedHandlerGroups[groupIndex];
                for (var handlerIndex = 0; handlerIndex < handlerGroup.Value.Count; handlerIndex++)
                {
                    var handler = handlerGroup.Value[handlerIndex];
                    using var sourceRange = _builder.SourceRange(handler.SourceRange);
                    using var routine = handler.DispatchKind == EventHandlerDispatchKind.MessageName
                        ? _builder.BeginMessageNameHandler(handler.Message, ReadSingleOrDefault(handler.Parameters, "message"), (ushort)handlerIndex, handler.MatchingTags, handler.WithoutTags)
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
            var orderedCallables = ReadOrderedCallables(module.Callables);
            for (var callableIndex = 0; callableIndex < orderedCallables.Length; callableIndex++)
            {
                var callable = orderedCallables[callableIndex];
                var kind = callable.Kind == GameEventScriptCallableKind.PredicateCall
                    ? GameEventScriptBinaryBindKind.Predicate
                    : GameEventScriptBinaryBindKind.Function;
                using var sourceRange = _builder.SourceRange(callable.SourceRange);
                using var routine = kind == GameEventScriptBinaryBindKind.Predicate
                    ? _builder.BeginPredicate(callable.Name, callable.SignatureLabels)
                    : _builder.BeginFunction(callable.Name, callable.SignatureLabels);

                _callableEntries[callable.SignatureId] = routine.EntryLabel;
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
            for (var statementIndex = 0; statementIndex < statements.Count; statementIndex++)
            {
                var statement = statements[statementIndex];
                using var sourceRange = _builder.SourceRange(statement.SourceRange);
                switch (statement)
                {
                    case LetStatementNode let:
                    {
                        var destination = context.Declare(let.Identifier);
                        if (!EmitExpressionToRegister(let.Expression, destination, context, new ExpressionState(context.RegisterCount)))
                        {
                            var value = EmitExpression(let.Expression, context, new ExpressionState(context.RegisterCount));
                            if (value.Id != destination.Id) _builder.Move(destination, value);
                        }

                        if (ClassifyHandlerSignature(let.Expression, context) is { } handlerSignature)
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
                        throw CompileFailure(GameEventScriptDiagnosticCodes.CompileUnsupportedConstruct, $"GameEventScript binary compiler does not support statement node '{statement.GetType().Name}'.");
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
            _builder.IteratorNext(item, iterator, endLabel);
            EmitStatements(forStatement.Body.Statements, forStatement.Body.IsBlock ? loopContext.CreateChild() : loopContext);
            _builder.Jump(loopLabel);
            _builder.MarkLabel(endLabel);
            _builder.IteratorClose(iterator);
        }

        private void EmitPublish(PublishStatementNode publish, LoweringContext context)
        {
            var tags = EmitExpressionRegisters(publish.TagExpressions, context, new ExpressionState(context.RegisterCount));
            if (publish.MessageExpression is MessageLiteralExpressionNode message)
            {
                var argumentNames = ReadArgumentNames(message.Arguments);
                var arguments = EmitArgumentExpressionRegisters(message.Arguments, context, new ExpressionState(context.RegisterCount));
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

        private bool EmitExpressionToRegister(ExpressionNode expression, GesRegisterRef destination, LoweringContext context, ExpressionState state)
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
                    _builder.LoadPercentage(destination, percentage.RatioValue);
                    return true;
                case TextLiteralExpressionNode text:
                    _builder.LoadText(destination, text.Value);
                    return true;
                case TagLiteralExpressionNode tag:
                    _builder.LoadTag(destination, tag.Name);
                    return true;
                case NothingLiteralExpressionNode:
                    _builder.LoadNothing(destination);
                    return true;
                case IdentifierExpressionNode identifier:
                {
                    var source = context.Require(identifier.Name);
                    if (source.Id != destination.Id) _builder.Move(destination, source);
                    return true;
                }
                case ConstantReferenceExpressionNode constant:
                {
                    if (!module.Constants.TryGetValue(constant.Name, out var value))
                    {
                        throw CompileFailure(GameEventScriptDiagnosticCodes.CompileUnresolvedSymbol, $"Constant '${constant.Name}' is not defined.", constant.Name);
                    }

                    return EmitExpressionToRegister(value with { SourceRange = constant.SourceRange }, destination, context, state);
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
                case CallExpressionNode call when GesCallableSignatures.HasName(module.Callables, call.Name):
                    EmitCallInto(call, destination, context, state);
                    return true;
                case CallExpressionNode call:
                    EmitHandlerBindCallInto(call, destination, context, state);
                    return true;
                case MessageLiteralExpressionNode message:
                {
                    var argumentNames = ReadArgumentNames(message.Arguments);
                    var arguments = EmitArgumentExpressionRegisters(message.Arguments, context, state);
                    _builder.LoadMessage(destination, MessageShape(message.Message, argumentNames), arguments);
                    return true;
                }
                case HandlerLiteralExpressionNode handler:
                    _builder.LoadHandler(destination, MessageShape(handler.Message, handler.SignatureLabels));
                    return true;
                case ListLiteralExpressionNode list:
                    EmitStageArguments(list.Items, context, state);
                    _builder.CreateList(destination);
                    return true;
                case MapLiteralExpressionNode map:
                    EmitStageMapValues(map.Entries, context, state);
                    _builder.CreateMap(destination, ReadMapKeys(map.Entries));
                    return true;
                case RangeExpressionNode range:
                {
                    var from = EmitExpressionForRead(range.FromExpression, context, state);
                    var to = EmitExpressionForRead(range.ToExpression, context, state);
                    if (range.StepExpression is null) _builder.CreateRange(destination, from, to);
                    else _builder.CreateRangeWithStep(destination, from, to, EmitExpressionForRead(range.StepExpression, context, state));
                    return true;
                }
                case DiceExpressionNode dice:
                    _builder.CreateDice(destination, ToShort(dice.DiceCount, "dice count"), ToShort(dice.SideCount, "dice side count"));
                    return true;
                case SeriesExpressionNode series:
                    _builder.CreateSeries(destination, series.SeriesKind);
                    return true;
                case ClampExpressionNode clamp:
                    _builder.Clamp(
                        destination,
                        EmitExpressionForRead(clamp.Value, context, state),
                        EmitExpressionForRead(clamp.Minimum, context, state),
                        EmitExpressionForRead(clamp.Maximum, context, state));
                    return true;
                case RandomExpressionNode random:
                {
                    var from = EmitExpressionForRead(random.FromExpression, context, state);
                    var to = EmitExpressionForRead(random.ToExpression, context, state);
                    if (random.FromExpression is FloatLiteralExpressionNode or UnitFloatLiteralExpressionNode or IntegerLiteralExpressionNode { HasDecimalPoint: true } or UnitIntegerLiteralExpressionNode { HasDecimalPoint: true } ||
                        random.ToExpression is FloatLiteralExpressionNode or UnitFloatLiteralExpressionNode or IntegerLiteralExpressionNode { HasDecimalPoint: true } or UnitIntegerLiteralExpressionNode { HasDecimalPoint: true })
                    {
                        _builder.RandomTakeFloat(destination, from, to);
                    }
                    else
                    {
                        _builder.RandomTake(destination, from, to);
                    }

                    return true;
                }
                case SeededRandomExpressionNode seededRandom:
                    EmitRandomPush(seededRandom.SeedExpression, context, state);
                    if (!EmitExpressionToRegister(seededRandom.BodyExpression, destination, context, state))
                    {
                        var result = EmitExpressionForRead(seededRandom.BodyExpression, context, state);
                        if (result.Id != destination.Id) _builder.Move(destination, result);
                    }
                    _builder.RandomPop();
                    return true;
                case GeneratedCollectionExpressionNode generatedCollection:
                    EmitGeneratedCollectionInto(generatedCollection, destination, context, state);
                    return true;
                case GuardedChoiceExpressionNode guardedChoice:
                    EmitGuardedChoiceInto(guardedChoice, destination, context, state);
                    return true;
                case BinaryExpressionNode binary:
                    EmitShortCircuitBinary(binary, destination, context, state);
                    return true;
                case VariadicTaggedExpressionNode variadic:
                    EmitVariadic(variadic, destination, context, state);
                    return true;
                case IntrinsicCallExpressionNode intrinsic:
                    EmitIntrinsicCallInto(intrinsic, destination, context, state);
                    return true;
                case ExtensionCallExpressionNode extensionCall:
                    EmitExtensionCallInto(extensionCall, destination, context, state, isPredicate: false);
                    return true;
                case ExtensionPredicateExpressionNode extensionPredicate:
                    EmitExtensionPredicateInto(extensionPredicate, destination, context, state);
                    return true;
                case TypeCheckExpressionNode check:
                    EmitCheckInto(destination, EmitExpressionForRead(check.Value, context, state), check.TypeName);
                    return true;
                case NothingCheckExpressionNode check:
                    EmitCheckInto(destination, EmitExpressionForRead(check.Value, context, state), "nothing");
                    return true;
                case TypeConstructorExpressionNode constructor:
                    EmitTypeConstructorInto(constructor, destination, context, state);
                    return true;
                case MemberAccessExpressionNode member:
                    _builder.MemberAccess(destination, member.Member, EmitExpressionForRead(member.Target, context, state));
                    return true;
                case CollectionAccessExpressionNode access:
                    EmitCollectionAccessInto(access, destination, context, state);
                    return true;
                default:
                    return false;
            }
        }

        private GesRegisterRef EmitExpression(ExpressionNode expression, LoweringContext context, ExpressionState state)
        {
            using var sourceRange = _builder.SourceRange(expression.SourceRange);
            var destination = state.AllocateTemporary(_builder, context);
            if (EmitExpressionToRegister(expression, destination, context, state)) return destination;
            throw CompileFailure(GameEventScriptDiagnosticCodes.CompileUnsupportedConstruct, $"GameEventScript binary compiler does not support expression node '{expression.GetType().Name}'.");
        }

        private GesRegisterRef EmitExpressionForRead(ExpressionNode expression, LoweringContext context, ExpressionState state)
        {
            return expression is IdentifierExpressionNode identifier
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
                    throw CompileFailure(GameEventScriptDiagnosticCodes.CompileUnsupportedConstruct, $"GameEventScript binary compiler does not support short-circuit operator '{operation.ToSourceText()}'.");
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
                        throw CompileFailure(GameEventScriptDiagnosticCodes.CompileUnsupportedConstruct, $"GameEventScript binary compiler does not support variadic operator '{variadic.Operator}'.");
                }
            }

            return destination;
        }

        private void EmitIntrinsicCallInto(IntrinsicCallExpressionNode intrinsic, GesRegisterRef destination, LoweringContext context, ExpressionState state)
        {
            var arguments = new GesRegisterRef[intrinsic.Arguments.Count];
            for (var index = 0; index < intrinsic.Arguments.Count; index++)
            {
                arguments[index] = EmitExpressionForRead(intrinsic.Arguments[index], context, state);
            }

            switch (intrinsic.Function)
            {
                case GesIntrinsicFunction.Atan2:
                    RequireIntrinsicArity(intrinsic, 2);
                    _builder.Atan2(destination, arguments[0], arguments[1]);
                    return;
                case GesIntrinsicFunction.Hypot:
                    if (arguments.Length == 2) _builder.Hypot2D(destination, arguments[0], arguments[1]);
                    else if (arguments.Length == 3) _builder.Hypot3D(destination, arguments[0], arguments[1], arguments[2]);
                    else ThrowInvalidIntrinsicArity(intrinsic, "2 or 3");
                    return;
                case GesIntrinsicFunction.Distance:
                    if (arguments.Length == 2) _builder.Distance(destination, arguments[0], arguments[1]);
                    else if (arguments.Length == 4) _builder.Distance2D(destination, arguments[0], arguments[1], arguments[2], arguments[3]);
                    else if (arguments.Length == 6) _builder.Distance3D(destination, arguments[0], arguments[1], arguments[2], arguments[3], arguments[4], arguments[5]);
                    else ThrowInvalidIntrinsicArity(intrinsic, "2, 4 or 6");
                    return;
                case GesIntrinsicFunction.DistanceSquared:
                    if (arguments.Length == 2) _builder.DistanceSquared(destination, arguments[0], arguments[1]);
                    else if (arguments.Length == 4) _builder.DistanceSquared2D(destination, arguments[0], arguments[1], arguments[2], arguments[3]);
                    else if (arguments.Length == 6) _builder.DistanceSquared3D(destination, arguments[0], arguments[1], arguments[2], arguments[3], arguments[4], arguments[5]);
                    else ThrowInvalidIntrinsicArity(intrinsic, "2, 4 or 6");
                    return;
                case GesIntrinsicFunction.LengthSquared:
                    if (arguments.Length == 1) _builder.LengthSquared(destination, arguments[0]);
                    else if (arguments.Length == 2) _builder.LengthSquared2D(destination, arguments[0], arguments[1]);
                    else if (arguments.Length == 3) _builder.LengthSquared3D(destination, arguments[0], arguments[1], arguments[2]);
                    else ThrowInvalidIntrinsicArity(intrinsic, "1, 2 or 3");
                    return;
                case GesIntrinsicFunction.Normalize:
                    if (arguments.Length == 1) _builder.Normalize(destination, arguments[0]);
                    else if (arguments.Length == 2) _builder.Normalize2D(destination, arguments[0], arguments[1]);
                    else if (arguments.Length == 3) _builder.Normalize3D(destination, arguments[0], arguments[1], arguments[2]);
                    else ThrowInvalidIntrinsicArity(intrinsic, "1, 2 or 3");
                    return;
                case GesIntrinsicFunction.Dot:
                    if (arguments.Length == 2) _builder.Dot(destination, arguments[0], arguments[1]);
                    else if (arguments.Length == 4) _builder.Dot2D(destination, arguments[0], arguments[1], arguments[2], arguments[3]);
                    else if (arguments.Length == 6) _builder.Dot3D(destination, arguments[0], arguments[1], arguments[2], arguments[3], arguments[4], arguments[5]);
                    else ThrowInvalidIntrinsicArity(intrinsic, "2, 4 or 6");
                    return;
                case GesIntrinsicFunction.Cross:
                    if (arguments.Length == 2) _builder.Cross(destination, arguments[0], arguments[1]);
                    else if (arguments.Length == 4) _builder.Cross2D(destination, arguments[0], arguments[1], arguments[2], arguments[3]);
                    else if (arguments.Length == 6) _builder.Cross3D(destination, arguments[0], arguments[1], arguments[2], arguments[3], arguments[4], arguments[5]);
                    else ThrowInvalidIntrinsicArity(intrinsic, "2, 4 or 6");
                    return;
                case GesIntrinsicFunction.AngleBetween:
                    if (arguments.Length == 2) _builder.AngleBetween(destination, arguments[0], arguments[1]);
                    else if (arguments.Length == 4) _builder.AngleBetween2D(destination, arguments[0], arguments[1], arguments[2], arguments[3]);
                    else if (arguments.Length == 6) _builder.AngleBetween3D(destination, arguments[0], arguments[1], arguments[2], arguments[3], arguments[4], arguments[5]);
                    else ThrowInvalidIntrinsicArity(intrinsic, "2, 4 or 6");
                    return;
                default:
                    throw CompileFailure(GameEventScriptDiagnosticCodes.CompileUnsupportedConstruct, $"GameEventScript binary compiler does not support intrinsic '{intrinsic.Function.ToSourceText()}'.");
            }
        }

        private static void RequireIntrinsicArity(IntrinsicCallExpressionNode intrinsic, int arity)
        {
            if (intrinsic.Arguments.Count == arity) return;
            ThrowInvalidIntrinsicArity(intrinsic, arity.ToString());
        }

        private static void ThrowInvalidIntrinsicArity(IntrinsicCallExpressionNode intrinsic, string expected)
            => throw CompileFailure(GameEventScriptDiagnosticCodes.CompileInvalidArity, $"Intrinsic '{intrinsic.Function.ToSourceText()}' expects {expected} arguments but got {intrinsic.Arguments.Count}.", intrinsic.Function.ToSourceText());

        private void EmitCollectionAccessInto(CollectionAccessExpressionNode access, GesRegisterRef destination, LoweringContext context, ExpressionState state)
        {
            if (EmitCollectionPipelineInto(access, destination, context, state))
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
                case ExpressionSelectorNode { Expression: TagLiteralExpressionNode { Name: "keys" } }:
                    _builder.KeysOfMap(destination, target);
                    return;
                case ExpressionSelectorNode { Expression: TagLiteralExpressionNode { Name: "values" } }:
                    _builder.ValuesOfMap(destination, target);
                    return;
                case ExpressionSelectorNode { Expression: TagLiteralExpressionNode { Name: "entries" } }:
                    _builder.EntriesOfMap(destination, target);
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
                    EmitInlineIteratorPipeline(destination, target, new CollectionSelectorNode[] { edge }, 0, edge, context, state);
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
                    EmitInlineIteratorPipeline(destination, target, new CollectionSelectorNode[] { filter }, 0, filter, context, state);
                    return;
                case SelectSelectorNode select:
                    EmitInlineIteratorPipeline(destination, target, new CollectionSelectorNode[] { select }, 0, select, context, state);
                    return;
                case PredicateSelectorNode predicate:
                    EmitInlineIteratorPipeline(destination, target, new CollectionSelectorNode[] { predicate }, 0, predicate, context, state);
                    return;
                case CountSelectorNode count:
                    if (IsAlwaysTrue(count.Predicate))
                    {
                        _builder.Count(destination, target);
                        return;
                    }

                    EmitInlineIteratorPipeline(destination, target, new CollectionSelectorNode[] { count }, 0, count, context, state);
                    return;
                case SumSelectorNode sum:
                    EmitInlineIteratorPipeline(destination, target, new CollectionSelectorNode[] { sum }, 0, sum, context, state);
                    return;
                case AverageSelectorNode average:
                    EmitInlineIteratorPipeline(destination, target, new CollectionSelectorNode[] { average }, 0, average, context, state);
                    return;
                case MinSelectorNode min:
                    EmitInlineIteratorPipeline(destination, target, new CollectionSelectorNode[] { min }, 0, min, context, state);
                    return;
                case MaxSelectorNode max:
                    EmitInlineIteratorPipeline(destination, target, new CollectionSelectorNode[] { max }, 0, max, context, state);
                    return;
                case MapSelectorNode map:
                    EmitMapSelectorLoop(destination, target, map, context, state);
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
                    EmitInlineIteratorPipeline(destination, target, new CollectionSelectorNode[] { distinct }, 0, distinct, context, state);
                    return;
                case GroupBySelectorNode groupBy:
                    EmitInlineIteratorPipeline(destination, target, new CollectionSelectorNode[] { groupBy }, 0, groupBy, context, state);
                    return;
                case OrderBySelectorNode orderBy:
                    EmitInlineIteratorPipeline(destination, target, new CollectionSelectorNode[] { orderBy }, 0, orderBy, context, state);
                    return;
                case PatternSelectorNode pattern:
                    EmitPattern(destination, target, pattern.Pattern, take: false, context, state);
                    return;
                case TakePatternSelectorNode pattern:
                    EmitPattern(destination, target, pattern.Pattern, take: true, context, state);
                    return;
                case ObjectMatchSelectorNode objectMatch:
                    EmitInlineIteratorPipeline(destination, target, new CollectionSelectorNode[] { objectMatch }, 0, objectMatch, context, state);
                    return;
                default:
                    throw CompileFailure(GameEventScriptDiagnosticCodes.CompileUnsupportedConstruct, $"GameEventScript binary compiler does not support selector node '{access.Selector.GetType().Name}'.");
            }
        }

        private bool EmitCollectionPipelineInto(CollectionAccessExpressionNode access, GesRegisterRef destination, LoweringContext context, ExpressionState state)
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

            if (prefixCount == 0 &&
                terminal is CountSelectorNode count &&
                IsAlwaysTrue(count.Predicate))
            {
                var countSource = EmitExpressionForRead(source, context, state);
                _builder.Count(destination, countSource);
                return true;
            }

            if (!CanEmitInlineIteratorPipelineTerminal(terminal, prefixCount > 0))
            {
                return false;
            }

            var sourceRegister = EmitExpressionForRead(source, context, state);
            EmitInlineIteratorPipeline(destination, sourceRegister, selectors, prefixCount, terminal, context, state);
            return true;
        }

        private static bool CanEmitInlineIteratorPipelineTerminal(CollectionSelectorNode terminal, bool hasPrefix)
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
                    MapSelectorNode or
                    DistinctSelectorNode { Identifier: not null, Projection: not null } or
                    GroupBySelectorNode or
                    OrderBySelectorNode or
                    ObjectMatchSelectorNode => true,
                EdgeSelectorNode edge => hasPrefix || edge.Predicate is not null,
                SequenceSliceSelectorNode => hasPrefix,
                DrawSelectorNode => hasPrefix,
                _ => false
            };

        private static bool CanEmitFirstValueAggregatePipeline(CollectionSelectorNode terminal, int prefixCount)
            => prefixCount == 0 &&
               (terminal switch
               {
                   SumSelectorNode sum => IsIdentityProjection(sum.Identifier, sum.Projection),
                   AverageSelectorNode average => IsIdentityProjection(average.Identifier, average.Projection),
                   _ => false
               });

        private void EmitInlineIteratorPipeline(GesRegisterRef destination, GesRegisterRef source, IReadOnlyList<CollectionSelectorNode> selectors, int prefixCount, CollectionSelectorNode terminal, LoweringContext context, ExpressionState state)
        {
            if (CanEmitFirstValueAggregatePipeline(terminal, prefixCount))
            {
                EmitInlineIteratorAggregatePipeline(destination, source, selectors, prefixCount, terminal, context, state);
                return;
            }

            var iterator = state.AllocateTemporary(_builder, context);
            var item = state.AllocateTemporary(_builder, context);
            var loopLabel = _builder.AddLabel("pipeline_next");
            var endLabel = _builder.AddLabel("pipeline_end");
            var nextLabel = _builder.AddLabel("pipeline_skip");
            var closeLabel = _builder.AddLabel("pipeline_close");
            var invalidIteratorLabel = _builder.AddLabel("pipeline_invalid_iterator");
            var doneLabel = _builder.AddLabel("pipeline_done");

            EmitInlinePipelineSourceGuard(source, terminal, prefixCount, invalidIteratorLabel, context, state);
            _builder.IteratorCreateOrJump(iterator, source, invalidIteratorLabel);

            GesRegisterRef? listBuilder = null;
            GesRegisterRef? mapBuilder = null;
            GesRegisterRef? distinctBuilder = null;
            GesRegisterRef? groupBuilder = null;
            GesRegisterRef? orderBuilder = null;
            GesRegisterRef? count = null;
            GesRegisterRef? one = null;
            GesRegisterRef? hasSum = null;
            GesRegisterRef? sum = null;
            GesRegisterRef? extremaKey = null;
            GesRegisterRef? extremaCompare = null;

            switch (terminal)
            {
                case FilterSelectorNode or SelectSelectorNode:
                    listBuilder = state.AllocateTemporary(_builder, context);
                    _builder.ListBuilderCreate(listBuilder.Value);
                    break;
                case SequenceSliceSelectorNode or DrawSelectorNode:
                    listBuilder = state.AllocateTemporary(_builder, context);
                    _builder.ListBuilderCreate(listBuilder.Value);
                    break;
                case MapSelectorNode:
                    mapBuilder = state.AllocateTemporary(_builder, context);
                    _builder.MapBuilderCreate(mapBuilder.Value);
                    break;
                case DistinctSelectorNode { Identifier: not null, Projection: not null }:
                    distinctBuilder = state.AllocateTemporary(_builder, context);
                    _builder.DistinctBuilderCreate(distinctBuilder.Value);
                    break;
                case GroupBySelectorNode:
                    groupBuilder = state.AllocateTemporary(_builder, context);
                    _builder.GroupBuilderCreate(groupBuilder.Value);
                    break;
                case OrderBySelectorNode:
                    orderBuilder = state.AllocateTemporary(_builder, context);
                    _builder.OrderBuilderCreate(orderBuilder.Value);
                    break;
                case CountSelectorNode or AverageSelectorNode:
                    count = destination;
                    _builder.LoadInteger(count.Value, 0);
                    one = state.AllocateTemporary(_builder, context);
                    _builder.LoadInteger(one.Value, 1);
                    if (terminal is AverageSelectorNode)
                    {
                        hasSum = state.AllocateTemporary(_builder, context);
                        sum = state.AllocateTemporary(_builder, context);
                        _builder.LoadFalse(hasSum.Value);
                        _builder.LoadNothing(sum.Value);
                    }
                    break;
                case PredicateSelectorNode predicate:
                    _builder.LoadBoolean(destination, predicate.Operator == "all");
                    break;
                case SumSelectorNode:
                    hasSum = state.AllocateTemporary(_builder, context);
                    sum = destination;
                    _builder.LoadFalse(hasSum.Value);
                    _builder.LoadNothing(sum.Value);
                    break;
                case MinSelectorNode or MaxSelectorNode:
                    hasSum = state.AllocateTemporary(_builder, context);
                    extremaKey = state.AllocateTemporary(_builder, context);
                    extremaCompare = state.AllocateTemporary(_builder, context);
                    _builder.LoadFalse(hasSum.Value);
                    _builder.LoadNothing(destination);
                    _builder.LoadNothing(extremaKey.Value);
                    break;
                case EdgeSelectorNode { Mode: "single" }:
                    count = state.AllocateTemporary(_builder, context);
                    one = state.AllocateTemporary(_builder, context);
                    _builder.LoadInteger(count.Value, 0);
                    _builder.LoadInteger(one.Value, 1);
                    _builder.LoadNothing(destination);
                    break;
                case EdgeSelectorNode:
                    _builder.LoadNothing(destination);
                    break;
                case ObjectMatchSelectorNode:
                    _builder.LoadFalse(destination);
                    break;
            }

            _builder.MarkLabel(loopLabel);
            _builder.IteratorNext(item, iterator, endLabel);

            var current = item;
            for (var index = 0; index < prefixCount; index++)
            {
                current = EmitInlinePipelineStep(selectors[index], current, nextLabel, context, state);
            }

            EmitInlinePipelineTerminal(destination, current, terminal, nextLabel, endLabel, listBuilder, mapBuilder, distinctBuilder, groupBuilder, orderBuilder, count, one, hasSum, sum, extremaKey, extremaCompare, context, state);
            _builder.MarkLabel(nextLabel);
            _builder.Jump(loopLabel);

            _builder.MarkLabel(endLabel);
            _builder.IteratorClose(iterator);

            switch (terminal)
            {
                case FilterSelectorNode or SelectSelectorNode:
                    _builder.ListBuilderFinish(destination, listBuilder!.Value);
                    break;
                case SequenceSliceSelectorNode slice:
                {
                    var collected = state.AllocateTemporary(_builder, context);
                    _builder.ListBuilderFinish(collected, listBuilder!.Value);
                    EmitSlice(destination, collected, slice);
                    break;
                }
                case DrawSelectorNode draw:
                {
                    var collected = state.AllocateTemporary(_builder, context);
                    _builder.ListBuilderFinish(collected, listBuilder!.Value);
                    if (draw.Count == 1) _builder.First(destination, collected);
                    else _builder.TakeFirst(destination, collected, ToShort(draw.Count, "draw count"));
                    break;
                }
                case MapSelectorNode:
                    _builder.MapBuilderFinish(destination, mapBuilder!.Value);
                    break;
                case DistinctSelectorNode { Identifier: not null, Projection: not null }:
                    _builder.DistinctBuilderFinish(destination, distinctBuilder!.Value);
                    break;
                case GroupBySelectorNode:
                    _builder.GroupBuilderFinish(destination, groupBuilder!.Value);
                    break;
                case OrderBySelectorNode orderBy:
                    if (orderBy.Direction == "descending") _builder.OrderBuilderFinishDescending(destination, orderBuilder!.Value);
                    else _builder.OrderBuilderFinishAscending(destination, orderBuilder!.Value);
                    break;
                case SumSelectorNode:
                    _builder.JumpIfTrue(hasSum!.Value, closeLabel);
                    _builder.LoadInteger(destination, 0);
                    break;
                case AverageSelectorNode:
                {
                    var noAverageLabel = _builder.AddLabel("pipeline_average_empty");
                    _builder.JumpIfNotTrue(count!.Value, noAverageLabel);
                    _builder.Divide(destination, sum!.Value, count.Value);
                    _builder.Jump(closeLabel);
                    _builder.MarkLabel(noAverageLabel);
                    _builder.LoadNothing(destination);
                    break;
                }
            }

            _builder.MarkLabel(closeLabel);
            _builder.Jump(doneLabel);
            _builder.MarkLabel(invalidIteratorLabel);
            _builder.LoadNothing(destination);
            _builder.MarkLabel(doneLabel);
        }

        private void EmitInlinePipelineSourceGuard(GesRegisterRef source, CollectionSelectorNode terminal, int prefixCount, GesLabelRef invalidIteratorLabel, LoweringContext context, ExpressionState state)
        {
            if (prefixCount != 0)
            {
                return;
            }

            switch (terminal)
            {
                case DistinctSelectorNode { Identifier: not null, Projection: not null }:
                case OrderBySelectorNode:
                    EmitSourceTypeGuard(source, invalidIteratorLabel, context, state, GameEventScriptBytecodeTypeKind.List);
                    return;
                case GroupBySelectorNode:
                    EmitSourceTypeGuard(source, invalidIteratorLabel, context, state, GameEventScriptBytecodeTypeKind.List, GameEventScriptBytecodeTypeKind.Map, GameEventScriptBytecodeTypeKind.Custom);
                    return;
            }
        }

        private void EmitSourceTypeGuard(GesRegisterRef source, GesLabelRef invalidIteratorLabel, LoweringContext context, ExpressionState state, params GameEventScriptBytecodeTypeKind[] allowedKinds)
        {
            var validLabel = _builder.AddLabel("pipeline_source_valid");
            var check = state.AllocateTemporary(_builder, context);
            for (var i = 0; i < allowedKinds.Length; i++)
            {
                _builder.CheckType(check, source, allowedKinds[i]);
                _builder.JumpIfTrue(check, validLabel);
            }

            _builder.Jump(invalidIteratorLabel);
            _builder.MarkLabel(validLabel);
        }

        private void EmitInlineIteratorAggregatePipeline(
            GesRegisterRef destination,
            GesRegisterRef source,
            IReadOnlyList<CollectionSelectorNode> selectors,
            int prefixCount,
            CollectionSelectorNode terminal,
            LoweringContext context,
            ExpressionState state)
        {
            var iterator = state.AllocateTemporary(_builder, context);
            var item = state.AllocateTemporary(_builder, context);
            var firstLabel = _builder.AddLabel("pipeline_aggregate_first");
            var loopLabel = _builder.AddLabel("pipeline_aggregate_next");
            var emptyLabel = _builder.AddLabel("pipeline_aggregate_empty");
            var endLabel = _builder.AddLabel("pipeline_aggregate_end");
            var invalidIteratorLabel = _builder.AddLabel("pipeline_aggregate_invalid_iterator");
            var doneLabel = _builder.AddLabel("pipeline_aggregate_done");

            var isAverage = terminal is AverageSelectorNode;
            GesRegisterRef accumulator = destination;
            GesRegisterRef? count = null;
            GesRegisterRef? one = null;
            if (isAverage)
            {
                accumulator = state.AllocateTemporary(_builder, context);
                count = state.AllocateTemporary(_builder, context);
                one = state.AllocateTemporary(_builder, context);
            }

            _builder.IteratorCreateOrJump(iterator, source, invalidIteratorLabel);

            _builder.MarkLabel(firstLabel);
            _builder.IteratorNext(item, iterator, emptyLabel);
            var current = item;
            for (var index = 0; index < prefixCount; index++)
            {
                current = EmitInlinePipelineStep(selectors[index], current, firstLabel, context, state);
            }

            var firstValue = EmitInlineAggregateValue(terminal, current, context, state);
            if (firstValue.Id != accumulator.Id) _builder.Move(accumulator, firstValue);
            if (isAverage)
            {
                _builder.LoadInteger(count!.Value, 1);
                _builder.LoadInteger(one!.Value, 1);
            }

            _builder.Jump(loopLabel);

            _builder.MarkLabel(loopLabel);
            _builder.IteratorNext(item, iterator, endLabel);
            current = item;
            for (var index = 0; index < prefixCount; index++)
            {
                current = EmitInlinePipelineStep(selectors[index], current, loopLabel, context, state);
            }

            var value = EmitInlineAggregateValue(terminal, current, context, state);
            if (isAverage) _builder.Add(count!.Value, count.Value, one!.Value);
            _builder.Add(accumulator, accumulator, value);
            _builder.Jump(loopLabel);

            _builder.MarkLabel(emptyLabel);
            _builder.IteratorClose(iterator);
            if (isAverage) _builder.LoadNothing(destination);
            else _builder.LoadInteger(destination, 0);
            _builder.Jump(doneLabel);

            _builder.MarkLabel(endLabel);
            _builder.IteratorClose(iterator);
            if (isAverage) _builder.Divide(destination, accumulator, count!.Value);
            _builder.Jump(doneLabel);

            _builder.MarkLabel(invalidIteratorLabel);
            _builder.LoadNothing(destination);
            _builder.MarkLabel(doneLabel);
        }

        private GesRegisterRef EmitInlineAggregateValue(CollectionSelectorNode terminal, GesRegisterRef current, LoweringContext context, ExpressionState state)
        {
            return terminal switch
            {
                SumSelectorNode sum => IsIdentityProjection(sum.Identifier, sum.Projection)
                    ? current
                    : EmitSelectorExpressionForRead(sum.Identifier, current, sum.Projection, context, state),
                AverageSelectorNode average => IsIdentityProjection(average.Identifier, average.Projection)
                    ? current
                    : EmitSelectorExpressionForRead(average.Identifier, current, average.Projection, context, state),
                _ => throw CompileFailure(GameEventScriptDiagnosticCodes.CompileUnsupportedConstruct, $"GameEventScript binary compiler cannot aggregate terminal selector node '{terminal.GetType().Name}'.")
            };
        }

        private GesRegisterRef EmitInlinePipelineStep(CollectionSelectorNode selector, GesRegisterRef current, GesLabelRef nextLabel, LoweringContext context, ExpressionState state)
        {
            switch (selector)
            {
                case FilterSelectorNode filter:
                    if (!IsAlwaysTrue(filter.Predicate))
                    {
                        var predicate = EmitSelectorExpressionForRead(filter.Identifier, current, filter.Predicate, context, state);
                        _builder.JumpIfNotTrue(predicate, nextLabel);
                    }
                    return current;
                case SelectSelectorNode select:
                    return IsIdentityProjection(select.Identifier, select.Projection)
                        ? current
                        : EmitSelectorExpressionForRead(select.Identifier, current, select.Projection, context, state);
                default:
                    throw CompileFailure(GameEventScriptDiagnosticCodes.CompileUnsupportedConstruct, $"GameEventScript binary compiler cannot inline non-terminal selector node '{selector.GetType().Name}'.");
            }
        }

        private void EmitInlinePipelineTerminal(
            GesRegisterRef destination,
            GesRegisterRef current,
            CollectionSelectorNode terminal,
            GesLabelRef nextLabel,
            GesLabelRef endLabel,
            GesRegisterRef? listBuilder,
            GesRegisterRef? mapBuilder,
            GesRegisterRef? distinctBuilder,
            GesRegisterRef? groupBuilder,
            GesRegisterRef? orderBuilder,
            GesRegisterRef? count,
            GesRegisterRef? one,
            GesRegisterRef? hasSum,
            GesRegisterRef? sum,
            GesRegisterRef? extremaKey,
            GesRegisterRef? extremaCompare,
            LoweringContext context,
            ExpressionState state)
        {
            switch (terminal)
            {
                case FilterSelectorNode filter:
                    if (!IsAlwaysTrue(filter.Predicate))
                    {
                        var predicate = EmitSelectorExpressionForRead(filter.Identifier, current, filter.Predicate, context, state);
                        _builder.JumpIfNotTrue(predicate, nextLabel);
                    }
                    _builder.ListBuilderAdd(listBuilder!.Value, current);
                    return;
                case SelectSelectorNode select:
                {
                    var projected = IsIdentityProjection(select.Identifier, select.Projection)
                        ? current
                        : EmitSelectorExpressionForRead(select.Identifier, current, select.Projection, context, state);
                    _builder.ListBuilderAdd(listBuilder!.Value, projected);
                    return;
                }
                case SequenceSliceSelectorNode or DrawSelectorNode:
                    _builder.ListBuilderAdd(listBuilder!.Value, current);
                    return;
                case MapSelectorNode map:
                {
                    var key = EmitSelectorExpressionForRead(map.Identifier, current, map.KeyProjection, context, state);
                    var value = map.ValueProjection is null
                        ? current
                        : EmitSelectorExpressionForRead(map.Identifier, current, map.ValueProjection, context, state);
                    _builder.MapBuilderAdd(mapBuilder!.Value, key, value);
                    return;
                }
                case DistinctSelectorNode { Identifier: not null, Projection: not null } distinct:
                {
                    var key = EmitSelectorExpressionForRead(distinct.Identifier, current, distinct.Projection, context, state);
                    _builder.DistinctBuilderAdd(distinctBuilder!.Value, key, current);
                    return;
                }
                case GroupBySelectorNode groupBy:
                {
                    var key = EmitSelectorExpressionForRead(groupBy.Identifier, current, groupBy.Projection, context, state);
                    _builder.GroupBuilderAdd(groupBuilder!.Value, key, current);
                    return;
                }
                case OrderBySelectorNode orderBy:
                {
                    var key = EmitSelectorExpressionForRead(orderBy.Identifier, current, orderBy.Projection, context, state);
                    _builder.OrderBuilderAdd(orderBuilder!.Value, key, current);
                    return;
                }
                case CountSelectorNode countSelector:
                    if (!IsAlwaysTrue(countSelector.Predicate))
                    {
                        var predicate = EmitSelectorExpressionForRead(countSelector.Identifier, current, countSelector.Predicate, context, state);
                        _builder.JumpIfNotTrue(predicate, nextLabel);
                    }
                    _builder.Add(count!.Value, count.Value, one!.Value);
                    return;
                case PredicateSelectorNode predicateSelector:
                {
                    var predicate = EmitSelectorExpressionForRead(predicateSelector.Identifier, current, predicateSelector.Predicate, context, state);
                    if (predicateSelector.Operator == "all")
                    {
                        _builder.JumpIfTrue(predicate, nextLabel);
                        _builder.LoadFalse(destination);
                        _builder.Jump(endLabel);
                        return;
                    }

                    _builder.JumpIfNotTrue(predicate, nextLabel);
                    _builder.LoadTrue(destination);
                    _builder.Jump(endLabel);
                    return;
                }
                case SumSelectorNode sumSelector:
                {
                    var value = IsIdentityProjection(sumSelector.Identifier, sumSelector.Projection)
                        ? current
                        : EmitSelectorExpressionForRead(sumSelector.Identifier, current, sumSelector.Projection, context, state);
                    EmitInlineAccumulateSum(sum!.Value, hasSum!.Value, value, nextLabel);
                    return;
                }
                case AverageSelectorNode averageSelector:
                {
                    var value = IsIdentityProjection(averageSelector.Identifier, averageSelector.Projection)
                        ? current
                        : EmitSelectorExpressionForRead(averageSelector.Identifier, current, averageSelector.Projection, context, state);
                    _builder.Add(count!.Value, count.Value, one!.Value);
                    EmitInlineAccumulateSum(sum!.Value, hasSum!.Value, value, nextLabel);
                    return;
                }
                case MinSelectorNode minSelector:
                    EmitInlineExtremaStep(destination, current, minSelector.Identifier, minSelector.Projection, hasSum!.Value, extremaKey!.Value, extremaCompare!.Value, isMax: false, nextLabel, context, state);
                    return;
                case MaxSelectorNode maxSelector:
                    EmitInlineExtremaStep(destination, current, maxSelector.Identifier, maxSelector.Projection, hasSum!.Value, extremaKey!.Value, extremaCompare!.Value, isMax: true, nextLabel, context, state);
                    return;
                case EdgeSelectorNode edge:
                    if (edge.Predicate is not null && edge.Identifier is not null)
                    {
                        var predicate = EmitSelectorExpressionForRead(edge.Identifier, current, edge.Predicate, context, state);
                        _builder.JumpIfNotTrue(predicate, nextLabel);
                    }

                    switch (edge.Mode)
                    {
                        case "last":
                            if (current.Id != destination.Id) _builder.Move(destination, current);
                            return;
                        case "single":
                        {
                            var duplicateLabel = _builder.AddLabel("pipeline_single_duplicate");
                            _builder.JumpIfTrue(count!.Value, duplicateLabel);
                            if (current.Id != destination.Id) _builder.Move(destination, current);
                            _builder.Add(count.Value, count.Value, one!.Value);
                            _builder.Jump(nextLabel);
                            _builder.MarkLabel(duplicateLabel);
                            _builder.LoadNothing(destination);
                            _builder.Jump(endLabel);
                            return;
                        }
                        default:
                            if (current.Id != destination.Id) _builder.Move(destination, current);
                            _builder.Jump(endLabel);
                            return;
                    }
                case ObjectMatchSelectorNode objectMatch:
                {
                    var match = EmitObjectPatternPredicate(current, objectMatch.Pattern, context, state);
                    _builder.JumpIfNotTrue(match, nextLabel);
                    _builder.LoadTrue(destination);
                    _builder.Jump(endLabel);
                    return;
                }
                default:
                    throw CompileFailure(GameEventScriptDiagnosticCodes.CompileUnsupportedConstruct, $"GameEventScript binary compiler cannot inline terminal selector node '{terminal.GetType().Name}'.");
            }
        }

        private void EmitInlineExtremaStep(
            GesRegisterRef destination,
            GesRegisterRef current,
            string identifier,
            ExpressionNode projectionExpression,
            GesRegisterRef hasWinner,
            GesRegisterRef winnerKey,
            GesRegisterRef isBetter,
            bool isMax,
            GesLabelRef nextLabel,
            LoweringContext context,
            ExpressionState state)
        {
            var projection = IsIdentityProjection(identifier, projectionExpression)
                ? current
                : EmitSelectorExpressionForRead(identifier, current, projectionExpression, context, state);
            var compareLabel = _builder.AddLabel(isMax ? "pipeline_max_compare" : "pipeline_min_compare");
            _builder.JumpIfTrue(hasWinner, compareLabel);
            if (current.Id != destination.Id) _builder.Move(destination, current);
            if (projection.Id != winnerKey.Id) _builder.Move(winnerKey, projection);
            _builder.LoadTrue(hasWinner);
            _builder.Jump(nextLabel);

            _builder.MarkLabel(compareLabel);
            if (isMax) _builder.Greater(isBetter, projection, winnerKey);
            else _builder.Less(isBetter, projection, winnerKey);
            var updateLabel = _builder.AddLabel(isMax ? "pipeline_max_update" : "pipeline_min_update");
            _builder.JumpIfTrue(isBetter, updateLabel);
            _builder.LoadTrue(hasWinner);
            _builder.Jump(nextLabel);
            _builder.MarkLabel(updateLabel);
            if (current.Id != destination.Id) _builder.Move(destination, current);
            if (projection.Id != winnerKey.Id) _builder.Move(winnerKey, projection);
            _builder.LoadTrue(hasWinner);
        }

        private void EmitInlineAccumulateSum(GesRegisterRef sum, GesRegisterRef hasSum, GesRegisterRef value, GesLabelRef nextLabel)
        {
            var addLabel = _builder.AddLabel("pipeline_sum_add");
            _builder.JumpIfTrue(hasSum, addLabel);
            if (value.Id != sum.Id) _builder.Move(sum, value);
            _builder.LoadTrue(hasSum);
            _builder.Jump(nextLabel);
            _builder.MarkLabel(addLabel);
            _builder.Add(sum, sum, value);
        }

        private GesRegisterRef EmitSelectorExpressionForRead(string identifier, GesRegisterRef current, ExpressionNode expression, LoweringContext context, ExpressionState state)
        {
            var selectorContext = context.CreateChild();
            selectorContext.DeclareExisting(identifier, current);
            return EmitExpressionForRead(expression, selectorContext, state);
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
                    var face = EmitExpressionForRead(count.Face!, context, state);
                    if (take) _builder.TakePattern(destination, target, GameEventScriptBytecodePatternKind.CountFace, ToShort(count.Count, "pattern count"), face);
                    else _builder.HasPattern(destination, target, GameEventScriptBytecodePatternKind.CountFace, ToShort(count.Count, "pattern count"), face);
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
                    throw CompileFailure(GameEventScriptDiagnosticCodes.CompileUnsupportedConstruct, $"GameEventScript binary compiler does not support dice pattern node '{pattern.GetType().Name}'.");
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

        private GesLabelRef EmitSelectorHelperExpression(string name, string identifier, ExpressionNode expression, LoweringContext parentContext, ExpressionState parentState, GesRegisterRef itemBinding)
        {
            using var sourceRange = _builder.SourceRange(expression.SourceRange);
            using var helper = _builder.BeginHelper($"{name}_{_helperIndex++}");
            var helperContext = LoweringContext.ForRoutineWithParent(helper, parentContext);
            helperContext.DeclareExisting(identifier, itemBinding);
            var result = EmitExpressionForRead(expression, helperContext, new ExpressionState(Math.Max(parentState.NextRegister, itemBinding.Id + 1)));
            _builder.ReturnValue(result);
            return helper.EntryLabel;
        }

        private void EmitChoose(GesRegisterRef destination, GesRegisterRef target, ChooseSelectorNode choose, LoweringContext context, ExpressionState state)
        {
            var source = target;
            if (choose.WeightExpression is not null && !string.IsNullOrEmpty(choose.WeightIdentifier))
            {
                EmitInlineWeightedChoose(destination, target, choose, context, state);
                return;
            }

            if (choose.Predicate is not null && !string.IsNullOrEmpty(choose.Identifier))
            {
                var filter = new FilterSelectorNode(choose.Identifier!, choose.Predicate);
                source = state.AllocateTemporary(_builder, context);
                EmitInlineIteratorPipeline(source, target, new CollectionSelectorNode[] { filter }, 0, filter, context, state);
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

        private void EmitInlineWeightedChoose(GesRegisterRef destination, GesRegisterRef source, ChooseSelectorNode choose, LoweringContext context, ExpressionState state)
        {
            var iterator = state.AllocateTemporary(_builder, context);
            var item = state.AllocateTemporary(_builder, context);
            var itemsBuilder = state.AllocateTemporary(_builder, context);
            var weightsBuilder = state.AllocateTemporary(_builder, context);
            var items = state.AllocateTemporary(_builder, context);
            var weights = state.AllocateTemporary(_builder, context);
            var zero = state.AllocateTemporary(_builder, context);
            var infinity = state.AllocateTemporary(_builder, context);
            var isPositive = state.AllocateTemporary(_builder, context);
            var isFinite = state.AllocateTemporary(_builder, context);

            var loopLabel = _builder.AddLabel("weighted_next");
            var skipLabel = _builder.AddLabel("weighted_skip");
            var endLabel = _builder.AddLabel("weighted_end");
            var invalidIteratorLabel = _builder.AddLabel("weighted_invalid_iterator");
            var doneLabel = _builder.AddLabel("weighted_done");

            _builder.IteratorCreateOrJump(iterator, source, invalidIteratorLabel);
            _builder.ListBuilderCreate(itemsBuilder);
            _builder.ListBuilderCreate(weightsBuilder);

            _builder.MarkLabel(loopLabel);
            _builder.IteratorNext(item, iterator, endLabel);

            if (choose.Predicate is not null && !string.IsNullOrEmpty(choose.Identifier))
            {
                var predicate = EmitSelectorExpressionForRead(choose.Identifier!, item, choose.Predicate, context, state);
                _builder.JumpIfNotTrue(predicate, skipLabel);
            }

            var weight = EmitSelectorExpressionForRead(choose.WeightIdentifier!, item, choose.WeightExpression!, context, state);
            _builder.LoadInteger(zero, 0);
            _builder.LoadFloat(infinity, double.PositiveInfinity);
            _builder.Greater(isPositive, weight, zero);
            _builder.JumpIfNotTrue(isPositive, skipLabel);
            _builder.Less(isFinite, weight, infinity);
            _builder.JumpIfNotTrue(isFinite, skipLabel);

            _builder.ListBuilderAdd(itemsBuilder, item);
            _builder.ListBuilderAdd(weightsBuilder, weight);

            _builder.MarkLabel(skipLabel);
            _builder.Jump(loopLabel);

            _builder.MarkLabel(endLabel);
            _builder.IteratorClose(iterator);
            _builder.ListBuilderFinish(items, itemsBuilder);
            _builder.ListBuilderFinish(weights, weightsBuilder);
            if (choose.Count == 1) _builder.OneWeighted(destination, items, weights);
            else _builder.TakeWeighted(destination, items, weights, ToShort(choose.Count, "choose count"));
            _builder.Jump(doneLabel);

            _builder.MarkLabel(invalidIteratorLabel);
            _builder.LoadNothing(destination);
            _builder.MarkLabel(doneLabel);
        }

        private void EmitMapSelectorLoop(GesRegisterRef destination, GesRegisterRef target, MapSelectorNode map, LoweringContext context, ExpressionState state)
        {
            EmitInlineIteratorPipeline(destination, target, new CollectionSelectorNode[] { map }, 0, map, context, state);
        }

        private GesRegisterRef EmitObjectPatternPredicate(GesRegisterRef target, ObjectMatchPatternNode pattern, LoweringContext context, ExpressionState state)
        {
            var result = state.AllocateTemporary(_builder, context);
            _builder.LoadTrue(result);
            for (var entryIndex = 0; entryIndex < pattern.Entries.Count; entryIndex++)
            {
                var entry = pattern.Entries[entryIndex];
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
                        throw CompileFailure(GameEventScriptDiagnosticCodes.CompileUnsupportedConstruct, $"GameEventScript binary compiler does not support object match value '{entry.Value.GetType().Name}'.");
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
                    throw CompileFailure(GameEventScriptDiagnosticCodes.CompileUnsupportedConstruct, $"GameEventScript binary compiler does not support slice selector '{slice.Operation} {slice.Scope}'.");
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
                throw CompileFailure(GameEventScriptDiagnosticCodes.CompileUnsupportedConstruct, $"GameEventScript binary compiler does not support generated collection type '{generatedCollection.CollectionType}'.");
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
            _builder.IteratorNext(item, iterator, endLabel);
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
            _builder.IteratorClose(iterator);
            _builder.ListBuilderFinish(destination, builderRegister);
        }

        private void EmitGuardedChoiceInto(GuardedChoiceExpressionNode guardedChoice, GesRegisterRef destination, LoweringContext context, ExpressionState state)
        {
            var endLabel = _builder.AddLabel("choice_end");
            for (var branchIndex = 0; branchIndex < guardedChoice.Branches.Count; branchIndex++)
            {
                var branch = guardedChoice.Branches[branchIndex];
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
                case RangeIterationSourceNode range when GetRangeIteratorShort(range.RangeExpression) is { } shortRange:
                {
                    var iterator = state.AllocateTemporary(_builder, context);
                    _builder.CreateRangeIteratorShort(iterator, shortRange.From, shortRange.To, shortRange.Step);
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
                    _builder.IteratorCreate(iterator, collectionRegister);
                    return iterator;
                }

                default:
                    throw CompileFailure(GameEventScriptDiagnosticCodes.CompileUnsupportedConstruct, $"GameEventScript binary compiler does not support iteration source '{source.GetType().Name}'.");
            }
        }

        private StageArgumentPlan PrepareStageArgument(ExpressionNode expression, LoweringContext context, ExpressionState state)
        {
            return IsStageConstant(expression)
                ? StageArgumentPlan.FromExpression(expression)
                : StageArgumentPlan.FromRegister(EmitExpressionForRead(expression, context, state));
        }

        private void EmitStageArguments(IReadOnlyList<ExpressionNode> expressions, LoweringContext context, ExpressionState state)
        {
            if (expressions.Count == 0) return;
            var arguments = new StageArgumentPlan[expressions.Count];
            for (var index = 0; index < expressions.Count; index++)
            {
                arguments[index] = PrepareStageArgument(expressions[index], context, state);
            }

            EmitPreparedStageArguments(arguments);
        }

        private void EmitStageArgumentNodes(IReadOnlyList<ArgumentNode> arguments, LoweringContext context, ExpressionState state)
        {
            if (arguments.Count == 0) return;
            var prepared = new StageArgumentPlan[arguments.Count];
            for (var index = 0; index < arguments.Count; index++)
            {
                prepared[index] = PrepareStageArgument(arguments[index].Expression, context, state);
            }

            EmitPreparedStageArguments(prepared);
        }

        private void EmitStageMapValues(IReadOnlyList<MapEntryNode> entries, LoweringContext context, ExpressionState state)
        {
            if (entries.Count == 0) return;
            var arguments = new StageArgumentPlan[entries.Count];
            for (var index = 0; index < entries.Count; index++)
            {
                arguments[index] = PrepareStageArgument(entries[index].Value, context, state);
            }

            EmitPreparedStageArguments(arguments);
        }

        private void EmitPreparedStageArguments(IReadOnlyList<StageArgumentPlan> arguments)
        {
            for (var index = 0; index < arguments.Count; index++)
            {
                EmitStageArgument(arguments[index]);
            }
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
                NothingLiteralExpressionNode;

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
                    _builder.StagePercentage(percentage.RatioValue);
                    return;
                case TextLiteralExpressionNode text:
                    _builder.StageText(text.Value);
                    return;
                case TagLiteralExpressionNode tag:
                    _builder.StageTag(tag.Name);
                    return;
                case NothingLiteralExpressionNode:
                    _builder.StageNothing();
                    return;
                default:
                    throw CompileFailure(GameEventScriptDiagnosticCodes.CompileUnsupportedConstruct, $"GameEventScript binary compiler cannot stage non-constant expression '{expression.GetType().Name}' without preparing it first.");
            }
        }

        private void EmitRandomPush(ExpressionNode seedExpression, LoweringContext context, ExpressionState state)
        {
            if (ReadUnitlessIntegerLiteralSeed(seedExpression) is { } seed)
            {
                _builder.RandomPushConstant(seed);
                return;
            }

            var source = EmitExpressionForRead(seedExpression, context, state);
            _builder.RandomPush(source);
        }

        private void EmitCallInto(CallExpressionNode call, GesRegisterRef destination, LoweringContext context, ExpressionState state)
        {
            var callable = GesCallableSignatures.Resolve(module.Callables, call);
            if (callable is null || !_callableEntries.TryGetValue(callable.SignatureId, out var entry))
            {
                throw CompileFailure(GameEventScriptDiagnosticCodes.CompileUnresolvedSymbol, $"GameEventScript binary compiler could not resolve callable '{call.Name}'.", call.Name);
            }

            EmitStageArguments(call.Arguments, context, state);

            _builder.Call(destination, entry, callable.Kind == GameEventScriptCallableKind.PredicateCall ? GameEventScriptInstructionFlag.NormalizeResultAsPredicate : GameEventScriptInstructionFlag.None);
        }

        private void EmitHandlerBindCallInto(CallExpressionNode call, GesRegisterRef destination, LoweringContext context, ExpressionState state)
        {
            var handler = context.Require(call.Name);
            if (context.ResolveHandlerSignature(call.Name) is { } signature &&
                !CallArgumentsMatchHandlerSignature(call.ArgumentList.Arguments, signature))
            {
                for (var argumentIndex = 0; argumentIndex < call.ArgumentList.Arguments.Count; argumentIndex++)
                {
                    var argument = call.ArgumentList.Arguments[argumentIndex];
                    EmitExpressionForRead(argument.Expression, context, state);
                }

                _builder.LoadNothing(destination);
                return;
            }

            var arguments = EmitExpressionRegisters(call.Arguments, context, state);
            _builder.BindHandler(destination, handler, arguments);
        }

        private void EmitPredicateCallInto(PredicateCallExpressionNode predicate, GesRegisterRef destination, LoweringContext context, ExpressionState state)
        {
            var callable = GesCallableSignatures.ResolveSingleParameterPredicate(module.Callables, predicate.PredicateName);
            if (callable is null || !_callableEntries.TryGetValue(callable.SignatureId, out var entry))
            {
                throw CompileFailure(GameEventScriptDiagnosticCodes.CompileUnresolvedSymbol, $"GameEventScript binary compiler could not resolve predicate '{predicate.PredicateName}'.", predicate.PredicateName);
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
            var argumentNames = ReadArgumentNames(extensionCall.Arguments);
            var arguments = EmitArgumentExpressionRegisters(extensionCall.Arguments, context, state);
            var reference = new GameEventScriptExtensionReference(extensionCall.ExtensionName, extensionCall.FunctionName, argumentNames);
            EmitExtensionReferenceInto(reference, destination, arguments, isPredicate);
        }

        private void EmitExtensionReferenceInto(GameEventScriptExtensionReference reference, GesRegisterRef destination, IReadOnlyList<GesRegisterRef> arguments, bool isPredicate)
        {
            _builder.CallExternal(destination, ResolveExtensionCall(reference), arguments, isPredicate ? GameEventScriptInstructionFlag.NormalizeResultAsPredicate : GameEventScriptInstructionFlag.None);
        }

        private void EmitTypeConstructorInto(TypeConstructorExpressionNode constructor, GesRegisterRef destination, LoweringContext context, ExpressionState state)
        {
            if (constructor.TypeName is "vector" or "point" && EmitSpatialConstructor(constructor, destination, context, state)) return;

            if (constructor.Arguments.Count == 1 && constructor.Arguments[0].Label is null && IsBuiltInCastType(constructor.TypeName))
            {
                EmitCastInto(destination, EmitExpressionForRead(constructor.Arguments[0].Expression, context, state), constructor.TypeName);
                return;
            }

            if (module.TypeDefinitions.TryGetValue(constructor.TypeName, out var type))
            {
                var unlabeledIndex = 0;
                var stagedArgumentCount = CountConstructorParameters(type.Fields);
                var stagedArguments = stagedArgumentCount == 0 ? [] : new StageArgumentPlan[stagedArgumentCount];
                var stagedArgumentIndex = 0;
                for (var fieldIndex = 0; fieldIndex < type.Fields.Count; fieldIndex++)
                {
                    var field = type.Fields[fieldIndex];
                    if (!field.IsConstructorParameter) continue;
                    if (field.ConstructorLabel == GameEventScriptMessageSignature.UnlabeledParameterName)
                    {
                        var argument = FindUnlabeledArgument(constructor.Arguments, unlabeledIndex);
                        if (argument is null)
                        {
                            stagedArguments[stagedArgumentIndex++] = StageArgumentPlan.Nothing;
                        }
                        else
                        {
                            unlabeledIndex++;
                            stagedArguments[stagedArgumentIndex++] = PrepareStageArgument(argument.Expression, context, state);
                        }
                    }
                    else if (FindLabeledArgument(constructor.Arguments, field.ConstructorLabel!) is { } argument)
                    {
                        stagedArguments[stagedArgumentIndex++] = PrepareStageArgument(argument.Expression, context, state);
                    }
                    else
                    {
                        stagedArguments[stagedArgumentIndex++] = StageArgumentPlan.Nothing;
                    }
                }

                EmitPreparedStageArguments(stagedArguments);
                _builder.CreateRecord(destination, ResolveRecordConstructor(type));
                return;
            }

            if (module.ExternalTypeDefinitions.Resolve(constructor.TypeName) is not null)
            {
                var argumentNames = ReadArgumentNames(constructor.Arguments);
                EmitStageArgumentNodes(constructor.Arguments, context, state);
                _builder.CreateExternalType(destination, ResolveExternalTypeConstructor(constructor.TypeName, argumentNames), argumentNames);
                return;
            }

            throw CompileFailure(GameEventScriptDiagnosticCodes.CompileUnsupportedConstruct, $"GameEventScript binary compiler does not support type constructor ':{constructor.TypeName}'.");
        }

        private bool EmitSpatialConstructor(TypeConstructorExpressionNode constructor, GesRegisterRef destination, LoweringContext context, ExpressionState state)
        {
            if (GetSpatialConstructorStageShape(constructor.Arguments) is not { } shape) return false;
            EmitStageArgumentNodes(shape.Arguments, context, state);
            if (constructor.TypeName == "point") _builder.CreatePoint(destination, (short)shape.StartComponent);
            else _builder.CreateVector(destination, (short)shape.StartComponent);
            return true;
        }

        private void EmitCastInto(GesRegisterRef destination, GesRegisterRef value, string? typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                throw CompileFailure(GameEventScriptDiagnosticCodes.CompileUnsupportedConstruct, "GameEventScript binary compiler requires a type name.");
            }

            if (GetQuantityUnit(typeName) is { } unit)
            {
                _builder.CastUnit(destination, value, unit);
            }
            else if (GameEventScriptBytecodeInstructionUnits.IsQuantityTypeName(typeName))
            {
                throw CompileFailure(GameEventScriptDiagnosticCodes.CompileUnsupportedConstruct, $"GameEventScript binary compiler does not support quantity type '{typeName}'.");
            }
            else if (typeName is "number" or "numeric" or "numeric:integer" or "numeric:fractional")
            {
                _builder.CastNumeric(destination, value);
            }
            else if (GetBytecodeTypeKind(typeName) is { } kind)
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
                throw CompileFailure(GameEventScriptDiagnosticCodes.CompileUnsupportedConstruct, "GameEventScript binary compiler requires a type name.");
            }

            if (GetQuantityUnit(typeName) is { } unit)
            {
                _builder.CheckUnit(destination, value, unit);
            }
            else if (GameEventScriptBytecodeInstructionUnits.IsQuantityTypeName(typeName))
            {
                throw CompileFailure(GameEventScriptDiagnosticCodes.CompileUnsupportedConstruct, $"GameEventScript binary compiler does not support quantity type '{typeName}'.");
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
            else if (GetBytecodeTypeKind(typeName) is { } kind)
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
                case GesUnaryOperator.Parse:
                    _builder.ParseLiteral(destination, operand);
                    break;
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
                case GesUnaryOperator.Exp:
                    _builder.Exp(destination, operand);
                    return;
                case GesUnaryOperator.Floor:
                    _builder.Floor(destination, operand);
                    return;
                case GesUnaryOperator.Ceil:
                    _builder.Ceil(destination, operand);
                    return;
                case GesUnaryOperator.Truncate:
                    _builder.Truncate(destination, operand);
                    return;
                case GesUnaryOperator.RoundHalfEven:
                    _builder.RoundHalfEven(destination, operand);
                    return;
                case GesUnaryOperator.RoundHalfUp:
                    _builder.RoundHalfUp(destination, operand);
                    return;
                case GesUnaryOperator.RoundHalfDown:
                    _builder.RoundHalfDown(destination, operand);
                    return;
                case GesUnaryOperator.DegreeToRadians:
                    _builder.DegreeToRadians(destination, operand);
                    return;
                case GesUnaryOperator.DegreeFromRadians:
                    _builder.DegreeFromRadians(destination, operand);
                    return;
                case GesUnaryOperator.WrapDegree:
                    _builder.WrapDegree(destination, operand);
                    return;
                case GesUnaryOperator.Sin:
                    _builder.Sin(destination, operand);
                    return;
                case GesUnaryOperator.Cos:
                    _builder.Cos(destination, operand);
                    return;
                case GesUnaryOperator.Tan:
                    _builder.Tan(destination, operand);
                    return;
                case GesUnaryOperator.Asin:
                    _builder.Asin(destination, operand);
                    return;
                case GesUnaryOperator.Acos:
                    _builder.Acos(destination, operand);
                    return;
                case GesUnaryOperator.Atan:
                    _builder.Atan(destination, operand);
                    return;
                default:
                    throw CompileFailure(GameEventScriptDiagnosticCodes.CompileUnsupportedConstruct, $"GameEventScript binary compiler does not support unary operator '{operation.ToSourceText()}'.");
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
                    throw CompileFailure(GameEventScriptDiagnosticCodes.CompileUnsupportedConstruct, $"GameEventScript binary compiler does not support binary operator '{operation.ToSourceText()}'.");
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
            throw CompileFailure(GameEventScriptDiagnosticCodes.CompileUnresolvedSymbol, $"GameEventScript record constructor ':{type.Name}' was not emitted.", type.Name);
        }

        private GesBindRef ResolveExternalTypeConstructor(string typeName, IReadOnlyList<string> argumentNames)
        {
            var reference = new GameEventScriptExternalTypeConstructorReference(typeName, argumentNames);
            if (module.ExternalTypeDefinitions.Resolve(reference.TypeName) is not { } typeDefinition)
            {
                throw CompileFailure(GameEventScriptDiagnosticCodes.CompileUnresolvedSymbol, $"GameEventScript external type ':{reference.TypeName}' is not registered.", reference.TypeName);
            }

            if (!typeDefinition.HasConstructor(reference.ArgumentLabels))
            {
                throw CompileFailure(GameEventScriptDiagnosticCodes.CompileUnresolvedSymbol, $"GameEventScript external type constructor ':{reference.SignatureId}' is not registered.", reference.SignatureId);
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

        private static GameEventScriptMessageSignature? ClassifyHandlerSignature(ExpressionNode expression, LoweringContext context)
        {
            switch (expression)
            {
                case HandlerLiteralExpressionNode handler:
                    return GameEventScriptMessageSignature.Create(handler.Message, handler.SignatureLabels);
                case IdentifierExpressionNode identifier:
                    return context.ResolveHandlerSignature(identifier.Name);
                default:
                    return null;
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

        private static long? ReadUnitlessIntegerLiteralSeed(ExpressionNode expression)
        {
            if (expression is IntegerLiteralExpressionNode integer)
            {
                return integer.Value;
            }

            if (expression is UnaryExpressionNode { Operator: GesUnaryOperator.Negate, Operand: IntegerLiteralExpressionNode negated } &&
                negated.Value != long.MinValue)
            {
                return -negated.Value;
            }

            return null;
        }

        private static (short From, short To, short Step)? GetRangeIteratorShort(RangeExpressionNode range)
        {
            if (GetShortIntegerLiteral(range.FromExpression) is { } from &&
                GetShortIntegerLiteral(range.ToExpression) is { } to)
            {
                if (range.StepExpression is null) return (from, to, 1);
                if (GetShortIntegerLiteral(range.StepExpression) is { } step) return (from, to, step);
            }

            return null;
        }

        private static short? GetShortIntegerLiteral(ExpressionNode expression)
        {
            if (expression is IntegerLiteralExpressionNode { Value: >= short.MinValue and <= short.MaxValue } integer)
            {
                return (short)integer.Value;
            }

            return null;
        }

        private static (int StartComponent, IReadOnlyList<ArgumentNode> Arguments)? GetSpatialConstructorStageShape(IReadOnlyList<ArgumentNode> sourceArguments)
        {
            if (sourceArguments.Count == 0) return (0, sourceArguments);
            var labeledCount = 0;
            for (var index = 0; index < sourceArguments.Count; index++)
            {
                if (sourceArguments[index].Label is not null) labeledCount++;
            }

            if (labeledCount == 0) return (0, sourceArguments);
            if (labeledCount != sourceArguments.Count) return null;
            var firstComponent = GetSpatialComponentIndex(sourceArguments[0].Label);
            var lastComponent = GetSpatialComponentIndex(sourceArguments[^1].Label);
            if (firstComponent < 0 || lastComponent < 0) return null;
            if (lastComponent - firstComponent + 1 != sourceArguments.Count) return null;
            if (firstComponent == 0 && sourceArguments.Count == 1) return null;
            for (var index = 0; index < sourceArguments.Count; index++)
            {
                if (GetSpatialComponentIndex(sourceArguments[index].Label) != firstComponent + index) return null;
            }

            return (firstComponent, sourceArguments);
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
                ? throw CompileFailure(GameEventScriptDiagnosticCodes.CompileNumericLimitExceeded, $"GameEventScript binary compiler {name} must fit into Int16.", name)
                : (short)value;

        private static GameEventScriptBytecodeInstructionUnit ResolveUnitOrNone(string unitName)
            => GameEventScriptBytecodeInstructionUnits.ParseTypeName(unitName) ?? GameEventScriptBytecodeInstructionUnit.UnitNone;

        private static bool IsBuiltInCastType(string typeName)
            => typeName is "number" or "numeric" or "numeric:integer" or "numeric:fractional" ||
               GameEventScriptBytecodeInstructionUnits.IsQuantityTypeName(typeName) ||
               GetBytecodeTypeKind(typeName) is not null;

        private static GameEventScriptBytecodeInstructionUnit? GetQuantityUnit(string typeName)
            => GameEventScriptBytecodeInstructionUnits.ParseQuantityTypeName(typeName);

        private static GameEventScriptBytecodeTypeKind? GetBytecodeTypeKind(string typeName)
        {
            var typeKind = typeName switch
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
                _ => (GameEventScriptBytecodeTypeKind)0
            };

            return typeName == "nothing" || typeKind != 0 ? typeKind : null;
        }

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
            throw CompileFailure(GameEventScriptDiagnosticCodes.CompileUnresolvedSymbol, $"GameEventScript binary compiler could not resolve identifier '{name}'.", name);
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

        public GameEventScriptMessageSignature? ResolveHandlerSignature(string name)
        {
            if (_handlerSignatures.TryGetValue(name, out var signature)) return signature;
            if (_handlerSignatureShadows.Contains(name))
            {
                return null;
            }

            return _parent?.ResolveHandlerSignature(name);
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
