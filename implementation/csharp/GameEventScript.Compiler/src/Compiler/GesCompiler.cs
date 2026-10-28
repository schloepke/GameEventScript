// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using GameEventScript.Api;
using GameEventScript.Runtime.Vm;

namespace GameEventScript.Compiler;

internal static partial class GesCompiler
{
    public static GameEventScriptProgram Compile(GesSyntaxTreeModule module, GesCompileOptions? options = null)
    {
        _ = module ?? throw new ArgumentNullException(nameof(module));
        var compileOptions = options ?? new GesCompileOptions();
        var program = new BinaryCompiler(module, compileOptions).Build();
        program.Validate();
        return program;
    }

    private static GesCompileException CompileFailure(string code, string message, string? symbol = null) => new(new GesDiagnostic(GesDiagnosticPhase.Compile, code, message, symbol));

    private sealed partial class BinaryCompiler(GesSyntaxTreeModule module, GesCompileOptions options)
    {
        private readonly GesBinaryBuilder _builder = new GesBinaryBuilder()
            .WithModuleName(module.ModuleName)
            .WithProgramVersion(options.ProgramVersion)
            .WithDebugInfo(options.DebugInfo, module.Sources)
            .WithOptimization();

        private readonly Dictionary<string, GesBindRef> _outboundMessages = new(StringComparer.Ordinal);
        private readonly Dictionary<string, GesBindRef> _extensionCalls = new(StringComparer.Ordinal);
        private readonly Dictionary<string, GesBinaryBuilder.RoutinePlan> _recordConstructors = new(StringComparer.Ordinal);
        private readonly Dictionary<string, GesBindRef> _externalTypeConstructors = new(StringComparer.Ordinal);
        private readonly Dictionary<string, GesBinaryBuilder.RoutinePlan> _callableRoutines = new(StringComparer.Ordinal);
        private int _helperIndex;

        // A lowering failure at the construct being lowered, so compile diagnostics carry a source position as validation does.
        private GesCompileException CompileFailure(string code, string message, string? symbol = null)
            => new(new GesDiagnostic(GesDiagnosticPhase.Compile, code, message, symbol, SourceLocation: _builder.CurrentSourceRange, ProgramName: module.ModuleName));

        public GameEventScriptProgram Build()
        {
            var callables = Ordered(module.Callables, static (left, right) => string.CompareOrdinal(left.SignatureId, right.SignatureId));
            var types = Ordered(module.TypeDefinitions, static (left, right) => string.CompareOrdinal(left.Name, right.Name));
            for (var index = 0; index < callables.Length; index++)
            {
                var callable = callables[index];
                var kind = callable.Kind == GesCallableKind.PredicateCall ? GesBinaryBindKind.Predicate : GesBinaryBindKind.Function;
                var routine = _builder.DeclareRoutine(kind, callable.Name, callable.SignatureLabels);
                routine.SourceRange = callable.SourceRange;
                _callableRoutines.Add(callable.SignatureId, routine);
            }

            for (var index = 0; index < types.Length; index++)
            {
                var type = types[index];
                var routine = _builder.DeclareRoutine(GesBinaryBindKind.Record, type.Name, ReadConstructorArgumentNames(type.Fields));
                routine.SourceRange = type.SourceRange;
                _recordConstructors.Add(type.Name, routine);
            }

            EmitCallables(callables);
            EmitRecordConstructors(types);
            EmitHandlers();
            return _builder.Build();
        }

        // Values ordered by their own ordinal key (the type name, callable signature ID or handler message name, which is also
        // the dictionary key), so emission never depends on dictionary insertion order.
        private static TValue[] Ordered<TValue>(IReadOnlyDictionary<string, TValue> source, Comparison<TValue> compare)
        {
            if (source.Count == 0) return [];
            var values = new TValue[source.Count];
            var offset = 0;
            if (source is Dictionary<string, TValue> dictionary)
            {
                foreach (var pair in dictionary) values[offset++] = pair.Value;
            }
            else
            {
                foreach (var pair in source) values[offset++] = pair.Value;
            }

            Array.Sort(values, compare);
            return values;
        }

        private static string[] ReadConstructorArgumentNames(IReadOnlyList<GesTypeFieldDefinitionNode> fields)
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

        private static string[] ReadFieldNames(IReadOnlyList<GesTypeFieldDefinitionNode> fields) => Project(fields, static field => field.Name);

        private static string[] ReadArgumentNames(IReadOnlyList<GesArgumentNode> arguments) => Project(arguments, static argument => argument.Name);

        private static string[] ReadMapKeys(IReadOnlyList<GesMapEntryNode> entries) => Project(entries, static entry => entry.Key);

        private static TResult[] Project<TSource, TResult>(IReadOnlyList<TSource> source, Func<TSource, TResult> selector)
        {
            if (source.Count == 0) return [];
            var result = new TResult[source.Count];
            for (var index = 0; index < source.Count; index++) result[index] = selector(source[index]);
            return result;
        }

        private GesRegisterRef[] EmitExpressionRegisters(IReadOnlyList<GesExpressionNode> expressions, LoweringContext context, ExpressionState state)
        {
            if (expressions.Count == 0) return [];
            var result = new GesRegisterRef[expressions.Count];
            for (var index = 0; index < expressions.Count; index++) result[index] = EmitExpressionForRead(expressions[index], context, state);

            return result;
        }

        private GesRegisterRef[] EmitArgumentExpressionRegisters(IReadOnlyList<GesArgumentNode> arguments, LoweringContext context, ExpressionState state)
        {
            if (arguments.Count == 0) return [];
            var result = new GesRegisterRef[arguments.Count];
            for (var index = 0; index < arguments.Count; index++) result[index] = EmitExpressionForRead(arguments[index].Expression, context, state);

            return result;
        }

        private static int CountConstructorParameters(IReadOnlyList<GesTypeFieldDefinitionNode> fields)
        {
            var count = 0;
            for (var index = 0; index < fields.Count; index++)
            {
                if (fields[index].IsConstructorParameter) count++;
            }

            return count;
        }

        private static GesArgumentNode? FindUnlabeledArgument(IReadOnlyList<GesArgumentNode> arguments, int unlabeledIndex)
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

        private static GesArgumentNode? FindLabeledArgument(IReadOnlyList<GesArgumentNode> arguments, string label)
        {
            for (var index = 0; index < arguments.Count; index++)
            {
                var argument = arguments[index];
                if (argument.Label is not null && string.Equals(argument.Name, label, StringComparison.Ordinal)) return argument;
            }

            return null;
        }

        private string ReadSingleOrDefault(IReadOnlyList<string> values, string fallback)
        {
            if (values.Count == 0) return fallback;
            if (values.Count == 1) return values[0];
            throw CompileFailure(GesDiagnosticCodes.CompileInvalidArity, "Message-name handler can only have one message parameter.");
        }

        private void EmitRecordConstructors(GesTypeDefinitionNode[] orderedTypes)
        {
            for (var typeIndex = 0; typeIndex < orderedTypes.Length; typeIndex++)
            {
                var type = orderedTypes[typeIndex];
                using var sourceRange = _builder.SourceRange(type.SourceRange);
                using var routine = _builder.BeginRoutine(_recordConstructors[type.Name]);

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
                        var computed = fieldRegister;
                        if (field.ComputedExpression is GesAssemblyExpressionNode assembly) EmitAssembly(assembly.Block, fieldRegister, context);
                        else computed = EmitExpressionForRead(field.ComputedExpression, context, state);
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

                for (var fieldIndex = 0; fieldIndex < fieldRegisters.Length; fieldIndex++) _builder.StageRegister(fieldRegisters[fieldIndex]);

                var map = state.AllocateTemporary(_builder, context);
                _builder.CreateMap(map, ReadFieldNames(type.Fields));
                var record = state.AllocateTemporary(_builder, context);
                _builder.CreateRecordValue(record, map, type.Name);
                _builder.ReturnValue(record);
            }
        }

        private void EmitHandlers()
        {
            // Groups are keyed by their handlers' message name and never empty.
            var orderedHandlerGroups = Ordered(module.Handlers, static (left, right) => string.CompareOrdinal(left[0].Message, right[0].Message));
            for (var groupIndex = 0; groupIndex < orderedHandlerGroups.Length; groupIndex++)
            {
                var handlerGroup = orderedHandlerGroups[groupIndex];
                for (var handlerIndex = 0; handlerIndex < handlerGroup.Count; handlerIndex++)
                {
                    var handler = handlerGroup[handlerIndex];
                    using var sourceRange = _builder.SourceRange(handler.SourceRange);
                    using var routine = handler.DispatchKind == GesEventHandlerDispatchKind.MessageName
                        ? _builder.BeginMessageNameHandler(handler.Message, ReadSingleOrDefault(handler.Parameters, "message"), (ushort)handlerIndex, handler.MatchingTags, handler.WithoutTags)
                        : _builder.BeginHandler(handler.Message, handler.SignatureLabels, (ushort)handlerIndex, handler.MatchingTags, handler.WithoutTags);
                    var context = LoweringContext.ForRoutine(routine);
                    for (var index = 0; index < handler.Parameters.Count; index++)
                    {
                        context.DeclareExisting(handler.Parameters[index], routine.Arguments[index]);
                        if (!string.IsNullOrEmpty(handler.ParameterList[index].DeclaredType)) EmitCastInto(routine.Arguments[index], routine.Arguments[index], handler.ParameterList[index].DeclaredType);
                    }

                    EmitStatements(handler.Statements, context);
                    _builder.ReturnVoid();
                }
            }
        }

        private void EmitCallables(GesCallableDefinition[] orderedCallables)
        {
            for (var callableIndex = 0; callableIndex < orderedCallables.Length; callableIndex++)
            {
                var callable = orderedCallables[callableIndex];
                using var sourceRange = _builder.SourceRange(callable.SourceRange);
                using var routine = _builder.BeginRoutine(_callableRoutines[callable.SignatureId]);
                var context = LoweringContext.ForRoutine(routine);
                for (var index = 0; index < callable.Parameters.Count; index++)
                {
                    context.DeclareExisting(callable.Parameters[index], routine.Arguments[index]);
                    if (!string.IsNullOrEmpty(callable.ParameterList[index].DeclaredType)) EmitCastInto(routine.Arguments[index], routine.Arguments[index], callable.ParameterList[index].DeclaredType);
                }

                var result = EmitExpressionForRead(callable.Expression, context, new ExpressionState(context.RegisterCount));
                _builder.ReturnValue(result);
            }
        }

        private void EmitStatements(IReadOnlyList<GesStatementNode> statements, LoweringContext context)
        {
            for (var statementIndex = 0; statementIndex < statements.Count; statementIndex++)
            {
                var statement = statements[statementIndex];
                using var sourceRange = _builder.SourceRange(statement.SourceRange);
                switch (statement)
                {
                    case GesAssemblyStatementNode assembly: EmitAssembly(assembly.Block, null, context); break;

                    case GesLetStatementNode let:
                    {
                        var destination = context.Declare(let.Identifier);
                        if (!EmitExpressionToRegister(let.Expression, destination, context, new ExpressionState(context.RegisterCount)))
                        {
                            var value = EmitExpression(let.Expression, context, new ExpressionState(context.RegisterCount));
                            if (value.Id != destination.Id) _builder.Move(destination, value);
                        }

                        if (IsKnownNonList(let.Expression, context)) _nonListBindings.Add(destination.Id);
                        if (ClassifyHandlerSignature(let.Expression, context) is { } handlerSignature) context.DeclareHandlerSignature(let.Identifier, handlerSignature);
                        else context.ClearHandlerSignature(let.Identifier);

                        break;
                    }

                    case GesPublishStatementNode publish: EmitPublish(publish, context); break;

                    case GesExpressionStatementNode expression: EmitExpressionForRead(expression.Expression, context, new ExpressionState(context.RegisterCount)); break;

                    case GesIfStatementNode ifStatement: EmitIf(ifStatement, context); break;

                    case GesForStatementNode forStatement: EmitFor(forStatement, context); break;

                    case GesSeededRandomStatementNode seededRandom:
                    {
                        EmitRandomPush(seededRandom.SeedExpression, context, new ExpressionState(context.RegisterCount));
                        EmitStatements(seededRandom.Body.Statements, context.CreateChild());
                        _builder.RandomPop();
                        break;
                    }

                    default: throw CompileFailure(GesDiagnosticCodes.CompileUnsupportedConstruct, $"GameEventScript binary compiler does not support statement node '{statement.GetType().Name}'.");
                }
            }
        }

        private void EmitIf(GesIfStatementNode ifStatement, LoweringContext context)
        {
            var elseLabel = _builder.AddLabel("if_else");
            var endLabel = _builder.AddLabel("if_end");
            var conditionContext = context.CreateChild();
            foreach (var step in ifStatement.Conditions)
            {
                GesRegisterRef condition;
                if (step.Binding is { } binding)
                {
                    EmitStatements([new GesLetStatementNode(binding, step.Expression) { SourceRange = step.SourceRange }], conditionContext);
                    condition = EmitExpressionForRead(new GesUnaryExpressionNode(GesUnaryOperator.HasValue, new GesIdentifierExpressionNode(binding)), conditionContext, new ExpressionState(conditionContext.RegisterCount));
                }
                else condition = EmitExpressionForRead(step.Expression, conditionContext, new ExpressionState(conditionContext.RegisterCount));
                _builder.JumpIfNotTrue(condition, elseLabel);
            }
            EmitStatements(ifStatement.ThenBody.Statements, conditionContext);
            _builder.Jump(endLabel);
            _builder.MarkLabel(elseLabel);
            if (ifStatement.ElseBody is not null) EmitStatements(ifStatement.ElseBody.Statements, context.CreateChild());

            _builder.MarkLabel(endLabel);
        }

        private void EmitFor(GesForStatementNode forStatement, LoweringContext context)
        {
            var loopContext = context.CreateChild();
            var names = forStatement.BindingNames.Count > 0 ? forStatement.BindingNames : new[] { forStatement.Identifier };
            var items = new GesRegisterRef[names.Count];
            for (var index = 0; index < names.Count; index++) items[index] = loopContext.Declare(names[index]);
            var state = new ExpressionState(loopContext.RegisterCount);
            var iterator = EmitIterator(forStatement.Source, context, state);
            var loopLabel = _builder.AddLabel("for_next");
            var endLabel = _builder.AddLabel("for_end");

            _builder.MarkLabel(loopLabel);
            if (items.Length == 1) _builder.IteratorNext(items[0], iterator, endLabel);
            else _builder.IteratorNextComponents(items, iterator, endLabel);
            EmitStatements(forStatement.Body.Statements, forStatement.Body.IsBlock ? loopContext.CreateChild() : loopContext);
            _builder.Jump(loopLabel);
            _builder.MarkLabel(endLabel);
            _builder.IteratorClose(iterator);
        }

        private void EmitSend(GesRegisterRef destination, GesSendExpressionNode send, LoweringContext context, ExpressionState state)
        {
            var delay = send.Delay is null ? (GesRegisterRef?)null : EmitExpressionForRead(send.Delay, context, state);
            var tags = EmitExpressionRegisters(send.Tags, context, state);
            GesOperand messageOperand;
            GesOperand arguments = default;
            var flags = tags.Length > 0 ? GesInstructionFlag.WithTags : GesInstructionFlag.None;
            if (send.Message is GesMessageLiteralExpressionNode message)
            {
                var registers = EmitArgumentExpressionRegisters(message.Arguments, context, state);
                messageOperand = GesOperand.Bind(ResolveOutboundMessage(message.Message, ReadArgumentNames(message.Arguments)));
                arguments = GesOperand.RegisterList(registers);
            }
            else
            {
                messageOperand = GesOperand.Register(EmitExpressionForRead(send.Message, context, state));
                flags |= GesInstructionFlag.Indirect;
            }
            var opcode = send.Kind == GesPublishStatementKind.Publish
                ? delay is null ? GesBytecodeOpCode.PublishInstant : GesBytecodeOpCode.PublishAfter
                : delay is null ? GesBytecodeOpCode.EmitInstant : GesBytecodeOpCode.EmitAfter;
            _builder.AddOpcode(opcode, flags: flags, dst: GesOperand.Register(destination), x: messageOperand, y: arguments,
                a: tags.Length > 0 ? GesOperand.RegisterList(tags) : default, b: delay is { } time ? GesOperand.Register(time) : default);
        }

        private void EmitPublish(GesPublishStatementNode publish, LoweringContext context)
        {
            var tags = EmitExpressionRegisters(publish.TagExpressions, context, new ExpressionState(context.RegisterCount));
            if (publish.MessageExpression is GesMessageLiteralExpressionNode message)
            {
                var argumentNames = ReadArgumentNames(message.Arguments);
                var arguments = EmitArgumentExpressionRegisters(message.Arguments, context, new ExpressionState(context.RegisterCount));
                var bind = ResolveOutboundMessage(message.Message, argumentNames);
                if (publish.Kind == GesPublishStatementKind.Publish)
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
            if (publish.Kind == GesPublishStatementKind.Publish)
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

        private bool EmitExpressionToRegister(GesExpressionNode expression, GesRegisterRef destination, LoweringContext context, ExpressionState state)
        {
            using var sourceRange = _builder.SourceRange(expression.SourceRange);
            switch (expression)
            {
                case GesAssemblyExpressionNode assembly: EmitAssembly(assembly.Block, destination, context); return true;
                case GesBooleanLiteralExpressionNode boolean: _builder.LoadBoolean(destination, boolean.Value); return true;
                case GesIntegerLiteralExpressionNode integer: _builder.LoadInteger(destination, integer.Value); return true;
                case GesUnitIntegerLiteralExpressionNode integer: _builder.LoadInteger(destination, integer.Value, ResolveUnitOrNone(integer.UnitName)); return true;
                case GesFloatLiteralExpressionNode number: _builder.LoadFloat(destination, number.Value); return true;
                case GesUnitFloatLiteralExpressionNode number: _builder.LoadFloat(destination, number.Value, ResolveUnitOrNone(number.UnitName)); return true;
                case GesPercentageLiteralExpressionNode percentage: _builder.LoadPercentage(destination, percentage.RatioValue); return true;
                case GesTextLiteralExpressionNode text: _builder.LoadText(destination, text.Value); return true;
                case GesTagLiteralExpressionNode tag: _builder.LoadTag(destination, tag.Name); return true;
                case GesNothingLiteralExpressionNode: _builder.LoadNothing(destination); return true;
                case GesIdentifierExpressionNode identifier:
                {
                    var source = context.Require(identifier.Name, identifier.SourceRange);
                    if (source.Id != destination.Id) _builder.Move(destination, source);
                    return true;
                }
                case GesConstantReferenceExpressionNode constant:
                {
                    if (!module.Constants.TryGetValue(constant.Name, out var value)) throw CompileFailure(GesDiagnosticCodes.CompileUnresolvedSymbol, $"Constant '${constant.Name}' is not defined.", constant.Name);

                    return EmitExpressionToRegister(value with { SourceRange = constant.SourceRange }, destination, context, state);
                }
                case GesSendExpressionNode send: EmitSend(destination, send, context, state); return true;
                case GesUnaryExpressionNode unary:
                {
                    var operand = EmitExpressionForRead(unary.Operand, context, state);
                    EmitUnary(destination, unary.Operator, operand);
                    return true;
                }
                case GesBinaryExpressionNode { Operator: not (GesBinaryOperator.Or or GesBinaryOperator.And or GesBinaryOperator.Implies) } binary:
                {
                    var left = EmitExpressionForRead(binary.Left, context, state);
                    var right = EmitExpressionForRead(binary.Right, context, state);
                    if (binary.Operator == GesBinaryOperator.Multiply && !IsKnownNonList(binary.Left, context) && !IsKnownNonList(binary.Right, context))
                    {
                        var numeric = _builder.AddLabel("multiply_numeric");
                        var done = _builder.AddLabel("multiply_done");
                        var check = state.AllocateTemporary(_builder, context);
                        _builder.CheckType(check, left, GesBytecodeTypeKind.List);
                        _builder.JumpIfNotTrue(check, numeric);
                        _builder.CheckType(check, right, GesBytecodeTypeKind.List);
                        _builder.JumpIfNotTrue(check, numeric);
                        _builder.Cartesian(destination, left, right);
                        _builder.Jump(done);
                        _builder.MarkLabel(numeric);
                        _builder.Multiply(destination, left, right);
                        _builder.MarkLabel(done);
                    }
                    else EmitBinary(destination, binary.Operator, left, right);
                    return true;
                }
                case GesTypeCastExpressionNode cast:
                {
                    var value = EmitExpressionForRead(cast.Value, context, state);
                    EmitCastInto(destination, value, cast.TypeName);
                    return true;
                }
                case GesPredicateCallExpressionNode predicate: EmitPredicateCallInto(predicate, destination, context, state); return true;
                case GesCallExpressionNode call when GesCallableSignatures.HasName(module.Callables, call.Name): EmitCallInto(call, destination, context, state); return true;
                case GesCallExpressionNode call: EmitHandlerBindCallInto(call, destination, context, state); return true;
                case GesMessageLiteralExpressionNode message:
                {
                    var argumentNames = ReadArgumentNames(message.Arguments);
                    var arguments = EmitArgumentExpressionRegisters(message.Arguments, context, state);
                    _builder.LoadMessage(destination, MessageShape(message.Message, argumentNames), arguments);
                    return true;
                }
                case GesHandlerLiteralExpressionNode handler: _builder.LoadHandler(destination, MessageShape(handler.Message, handler.SignatureLabels)); return true;
                case GesListLiteralExpressionNode list:
                    EmitStageArguments(list.Items, context, state);
                    _builder.CreateList(destination);
                    return true;
                case GesMapLiteralExpressionNode map:
                    EmitStageMapValues(map.Entries, context, state);
                    _builder.CreateMap(destination, ReadMapKeys(map.Entries));
                    return true;
                case GesRangeExpressionNode range:
                {
                    var from = EmitExpressionForRead(range.FromExpression, context, state);
                    var to = EmitExpressionForRead(range.ToExpression, context, state);
                    if (range.StepExpression is null) _builder.CreateRange(destination, from, to);
                    else _builder.CreateRangeWithStep(destination, from, to, EmitExpressionForRead(range.StepExpression, context, state));
                    return true;
                }
                case GesDiceExpressionNode dice: _builder.CreateDice(destination, ToShort(dice.DiceCount, "dice count"), ToShort(dice.SideCount, "dice side count")); return true;
                case GesSeriesExpressionNode series: _builder.CreateSeries(destination, series.SeriesKind); return true;
                case GesBetweenExpressionNode between:
                {
                    var value = EmitExpressionForRead(between.Value, context, state);
                    var minimum = EmitExpressionForRead(between.Minimum, context, state);
                    var lower = state.AllocateTemporary(_builder, context);
                    _builder.GreaterOrEqual(lower, value, minimum);
                    _builder.And(destination, lower, lower);
                    var end = _builder.AddLabel("between_end");
                    _builder.JumpIfFalse(lower, end);
                    var maximum = EmitExpressionForRead(between.Maximum, context, state);
                    var upper = state.AllocateTemporary(_builder, context);
                    _builder.LessOrEqual(upper, value, maximum);
                    _builder.And(destination, lower, upper);
                    _builder.MarkLabel(end);
                    return true;
                }
                case GesClampExpressionNode clamp:
                    _builder.Clamp(destination, EmitExpressionForRead(clamp.Value, context, state), EmitExpressionForRead(clamp.Minimum, context, state), EmitExpressionForRead(clamp.Maximum, context, state));
                    return true;
                case GesRandomExpressionNode random:
                {
                    var from = EmitExpressionForRead(random.FromExpression, context, state);
                    var to = EmitExpressionForRead(random.ToExpression, context, state);
                    // A decimal literal bound draws a fractional number; other bounds draw integers.
                    static bool IsDecimal(GesExpressionNode bound)
                        => bound is GesFloatLiteralExpressionNode or GesUnitFloatLiteralExpressionNode or GesIntegerLiteralExpressionNode { HasDecimalPoint: true } or GesUnitIntegerLiteralExpressionNode { HasDecimalPoint: true };
                    if (IsDecimal(random.FromExpression) || IsDecimal(random.ToExpression)) _builder.RandomTakeFloat(destination, from, to);
                    else _builder.RandomTake(destination, from, to);
                    return true;
                }
                case GesSeededRandomExpressionNode seededRandom:
                    EmitRandomPush(seededRandom.SeedExpression, context, state);
                    if (!EmitExpressionToRegister(seededRandom.BodyExpression, destination, context, state))
                    {
                        var result = EmitExpressionForRead(seededRandom.BodyExpression, context, state);
                        if (result.Id != destination.Id) _builder.Move(destination, result);
                    }
                    _builder.RandomPop();
                    return true;
                case GesGeneratedCollectionExpressionNode generatedCollection: EmitGeneratedCollectionInto(generatedCollection, destination, context, state); return true;
                case GesGuardedChoiceExpressionNode guardedChoice: EmitGuardedChoiceInto(guardedChoice, destination, context, state); return true;
                case GesBinaryExpressionNode binary: EmitShortCircuitBinary(binary, destination, context, state); return true;
                case GesVariadicTaggedExpressionNode variadic: EmitVariadic(variadic, destination, context, state); return true;
                case GesIntrinsicCallExpressionNode intrinsic: EmitIntrinsicCallInto(intrinsic, destination, context, state); return true;
                case GesExtensionCallExpressionNode extensionCall: EmitExtensionCallInto(extensionCall, destination, context, state, isPredicate: false); return true;
                case GesExtensionPredicateExpressionNode extensionPredicate: EmitExtensionPredicateInto(extensionPredicate, destination, context, state); return true;
                case GesTypeCheckExpressionNode check: EmitCheckInto(destination, EmitExpressionForRead(check.Value, context, state), check.TypeName); return true;
                case GesNothingCheckExpressionNode check: EmitCheckInto(destination, EmitExpressionForRead(check.Value, context, state), "nothing"); return true;
                case GesTypeConstructorExpressionNode constructor: EmitTypeConstructorInto(constructor, destination, context, state); return true;
                case GesMemberAccessExpressionNode member: _builder.MemberAccess(destination, member.Member, EmitExpressionForRead(member.Target, context, state)); return true;
                case GesCombinedCollectionExpressionNode combined: EmitCombinedCollection(destination, combined, context, state); return true;
                case GesCollectionAccessExpressionNode access: EmitCollectionAccessInto(access, destination, context, state); return true;
                default: return false;
            }
        }

        private GesRegisterRef EmitExpression(GesExpressionNode expression, LoweringContext context, ExpressionState state)
        {
            using var sourceRange = _builder.SourceRange(expression.SourceRange);
            var destination = state.AllocateTemporary(_builder, context);
            if (EmitExpressionToRegister(expression, destination, context, state)) return destination;
            throw CompileFailure(GesDiagnosticCodes.CompileUnsupportedConstruct, $"GameEventScript binary compiler does not support expression node '{expression.GetType().Name}'.");
        }

        private GesRegisterRef EmitExpressionForRead(GesExpressionNode expression, LoweringContext context, ExpressionState state)
        {
            return expression is GesIdentifierExpressionNode identifier
                ? context.Require(identifier.Name, identifier.SourceRange)
                : EmitExpression(expression, context, state);
        }

        private GesRegisterRef EmitShortCircuitBinary(GesBinaryExpressionNode binary, GesRegisterRef destination, LoweringContext context, ExpressionState state)
        {
            var left = EmitExpressionForRead(binary.Left, context, state);
            EmitShortCircuitCombine(binary.Operator, destination, left, left);
            var end = _builder.AddLabel("short_circuit_end");
            switch (binary.Operator)
            {
                case GesBinaryOperator.Or: _builder.JumpIfTrue(left, end); break;
                case GesBinaryOperator.And:
                case GesBinaryOperator.Implies: _builder.JumpIfFalse(left, end); break;
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
                case GesBinaryOperator.Or: _builder.Or(destination, left, right); return;
                case GesBinaryOperator.And: _builder.And(destination, left, right); return;
                case GesBinaryOperator.Implies: _builder.Implies(destination, left, right); return;
                default: throw CompileFailure(GesDiagnosticCodes.CompileUnsupportedConstruct, $"GameEventScript binary compiler does not support short-circuit operator '{operation.ToSourceText()}'.");
            }
        }

        private GesRegisterRef EmitVariadic(GesVariadicTaggedExpressionNode variadic, GesRegisterRef destination, LoweringContext context, ExpressionState state)
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
                    case "min": _builder.Min(destination, destination, next); break;
                    case "max": _builder.Max(destination, destination, next); break;
                    default: throw CompileFailure(GesDiagnosticCodes.CompileUnsupportedConstruct, $"GameEventScript binary compiler does not support variadic operator '{variadic.Operator}'.");
                }
            }

            return destination;
        }

        private void EmitIntrinsicCallInto(GesIntrinsicCallExpressionNode intrinsic, GesRegisterRef destination, LoweringContext context, ExpressionState state)
        {
            var arguments = new GesRegisterRef[intrinsic.Arguments.Count];
            for (var index = 0; index < intrinsic.Arguments.Count; index++) arguments[index] = EmitExpressionForRead(intrinsic.Arguments[index], context, state);

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
                default: throw CompileFailure(GesDiagnosticCodes.CompileUnsupportedConstruct, $"GameEventScript binary compiler does not support intrinsic '{intrinsic.Function.ToSourceText()}'.");
            }
        }

        private void RequireIntrinsicArity(GesIntrinsicCallExpressionNode intrinsic, int arity)
        {
            if (intrinsic.Arguments.Count == arity) return;
            ThrowInvalidIntrinsicArity(intrinsic, arity.ToString());
        }

        private void ThrowInvalidIntrinsicArity(GesIntrinsicCallExpressionNode intrinsic, string expected)
            => throw CompileFailure(GesDiagnosticCodes.CompileInvalidArity, $"Intrinsic '{intrinsic.Function.ToSourceText()}' expects {expected} arguments but got {intrinsic.Arguments.Count}.", intrinsic.Function.ToSourceText());

        private void EmitCollectionAccessInto(GesCollectionAccessExpressionNode access, GesRegisterRef destination, LoweringContext context, ExpressionState state)
        {
            if (access.Selector is GesFoldSelectorNode directFold && (access.Target is GesCombinedCollectionExpressionNode || IsEntriesSource(access.Target)))
            {
                EmitFold(destination, default, directFold, context, state, access.Target);
                return;
            }

            if (EmitCollectionPipelineInto(access, destination, context, state)) return;

            var target = EmitExpressionForRead(access.Target, context, state);
            switch (access.Selector)
            {
                case GesExpressionSelectorNode { Expression: GesIntegerLiteralExpressionNode { Value: >= 0 and <= ushort.MaxValue } integer }: _builder.IndexAccess(destination, (ushort)integer.Value, target); return;
                case GesExpressionSelectorNode { Expression: GesTextLiteralExpressionNode text }: _builder.MemberAccess(destination, text.Value, target); return;
                case GesExpressionSelectorNode { Expression: GesTagLiteralExpressionNode { Name: "keys" } }: _builder.KeysOfMap(destination, target); return;
                case GesExpressionSelectorNode { Expression: GesTagLiteralExpressionNode { Name: "values" } }: _builder.ValuesOfMap(destination, target); return;
                case GesExpressionSelectorNode { Expression: GesTagLiteralExpressionNode { Name: "entries" } }: _builder.EntriesOfMap(destination, target); return;
                case GesExpressionSelectorNode { Expression: GesTagLiteralExpressionNode tag }: _builder.MemberAccess(destination, tag.Name, target); return;
                case GesExpressionSelectorNode selector: _builder.PropertyAccess(destination, EmitExpressionForRead(selector.Expression, context, state), target); return;
                case GesSeriesTermSelectorNode term: _builder.Term(destination, target, EmitExpressionForRead(term.IndexExpression, context, state)); return;
                case GesSequenceSliceSelectorNode slice: EmitSlice(destination, target, slice); return;
                case GesEdgeSelectorNode { Predicate: null } edge:
                    if (edge.Mode == "last") _builder.Last(destination, target);
                    else if (edge.Mode == "single") _builder.Single(destination, target);
                    else _builder.First(destination, target);
                    return;
                case GesEdgeSelectorNode { Predicate: not null, Identifier: not null } edge: EmitInlineIteratorPipeline(destination, target, new GesCollectionSelectorNode[] { edge }, 0, edge, context, state); return;
                case GesDrawSelectorNode draw:
                    if (draw.Count == 1) _builder.First(destination, target);
                    else _builder.TakeFirst(destination, target, ToShort(draw.Count, "draw count"));
                    return;
                case GesChooseSelectorNode choose: EmitChoose(destination, target, choose, context, state); return;
                case GesContainsSelectorNode contains: EmitContains(destination, target, contains, context, state); return;
                case GesFilterSelectorNode filter: EmitInlineIteratorPipeline(destination, target, new GesCollectionSelectorNode[] { filter }, 0, filter, context, state); return;
                case GesFoldSelectorNode fold: EmitFold(destination, target, fold, context, state); return;
                case GesSelectSelectorNode select: EmitInlineIteratorPipeline(destination, target, new GesCollectionSelectorNode[] { select }, 0, select, context, state); return;
                case GesPredicateSelectorNode predicate: EmitInlineIteratorPipeline(destination, target, new GesCollectionSelectorNode[] { predicate }, 0, predicate, context, state); return;
                case GesCountSelectorNode count:
                    if (IsAlwaysTrue(count.Predicate))
                    {
                        _builder.Count(destination, target);
                        return;
                    }

                    EmitInlineIteratorPipeline(destination, target, new GesCollectionSelectorNode[] { count }, 0, count, context, state);
                    return;
                case GesSumSelectorNode sum: EmitInlineIteratorPipeline(destination, target, new GesCollectionSelectorNode[] { sum }, 0, sum, context, state); return;
                case GesAverageSelectorNode average: EmitInlineIteratorPipeline(destination, target, new GesCollectionSelectorNode[] { average }, 0, average, context, state); return;
                case GesMinSelectorNode min: EmitInlineIteratorPipeline(destination, target, new GesCollectionSelectorNode[] { min }, 0, min, context, state); return;
                case GesMaxSelectorNode max: EmitInlineIteratorPipeline(destination, target, new GesCollectionSelectorNode[] { max }, 0, max, context, state); return;
                case GesMapSelectorNode map: EmitMapSelectorLoop(destination, target, map, context, state); return;
                case GesSortSelectorNode sort:
                    if (sort.Direction == "descending") _builder.SortDescending(destination, target);
                    else _builder.SortAscending(destination, target);
                    return;
                case GesReverseSelectorNode: _builder.Reverse(destination, target); return;
                case GesShuffleSelectorNode: _builder.Shuffle(destination, target); return;
                case GesDistinctSelectorNode { Identifier: null, Projection: null }: _builder.Distinct(destination, target); return;
                case GesDistinctSelectorNode { Identifier: not null, Projection: not null } distinct: EmitInlineIteratorPipeline(destination, target, new GesCollectionSelectorNode[] { distinct }, 0, distinct, context, state); return;
                case GesGroupBySelectorNode groupBy: EmitInlineIteratorPipeline(destination, target, new GesCollectionSelectorNode[] { groupBy }, 0, groupBy, context, state); return;
                case GesOrderBySelectorNode orderBy: EmitInlineIteratorPipeline(destination, target, new GesCollectionSelectorNode[] { orderBy }, 0, orderBy, context, state); return;
                case GesPatternSelectorNode pattern: EmitPattern(destination, target, pattern.Pattern, take: false, context, state); return;
                case GesTakePatternSelectorNode pattern: EmitPattern(destination, target, pattern.Pattern, take: true, context, state); return;
                case GesObjectMatchSelectorNode objectMatch: EmitInlineIteratorPipeline(destination, target, new GesCollectionSelectorNode[] { objectMatch }, 0, objectMatch, context, state); return;
                default: throw CompileFailure(GesDiagnosticCodes.CompileUnsupportedConstruct, $"GameEventScript binary compiler does not support selector node '{access.Selector.GetType().Name}'.");
            }
        }

        private bool EmitCollectionPipelineInto(GesCollectionAccessExpressionNode access, GesRegisterRef destination, LoweringContext context, ExpressionState state)
        {
            var selectors = new List<GesCollectionSelectorNode>();
            GesExpressionNode source = access;
            while (source is GesCollectionAccessExpressionNode collectionAccess)
            {
                if (collectionAccess.Selector is GesExpressionSelectorNode { Expression: GesTagLiteralExpressionNode { Name: "entries" } }) break;
                selectors.Add(collectionAccess.Selector);
                source = collectionAccess.Target;
            }

            selectors.Reverse();
            if (selectors.Count == 0) return false;

            var terminal = selectors[^1];
            var prefixCount = selectors.Count - 1;
            for (var index = 0; index < prefixCount; index++)
            {
                if (selectors[index] is not (GesFilterSelectorNode or GesSelectSelectorNode)) return false;
            }

            if (source is not GesCombinedCollectionExpressionNode && !IsEntriesSource(source) && prefixCount == 0 &&
                terminal is GesCountSelectorNode count &&
                IsAlwaysTrue(count.Predicate))
            {
                var countSource = EmitExpressionForRead(source, context, state);
                _builder.Count(destination, countSource);
                return true;
            }

            // Slices preserve Dice and reject Maps unless a filter/select has produced a List.
            if (prefixCount == 0 && source is GesCombinedCollectionExpressionNode { Operation: "union" or "intersect" or "difference" } &&
                terminal is GesSequenceSliceSelectorNode or GesDrawSelectorNode)
            {
                return false;
            }

            if (!CanEmitInlineIteratorPipelineTerminal(terminal, prefixCount > 0 || source is GesCombinedCollectionExpressionNode || IsEntriesSource(source))) return false;

            if (source is GesCombinedCollectionExpressionNode || IsEntriesSource(source))
            {
                EmitInlineIteratorPipeline(destination, default, selectors, prefixCount, terminal, context, state, source);
            }
            else
            {
                var sourceRegister = EmitExpressionForRead(source, context, state);
                EmitInlineIteratorPipeline(destination, sourceRegister, selectors, prefixCount, terminal, context, state);
            }
            return true;
        }

        private static bool CanEmitInlineIteratorPipelineTerminal(GesCollectionSelectorNode terminal, bool hasPrefix)
            => terminal switch
            {
                GesForeachSelectorNode or
                    GesFilterSelectorNode or
                    GesSelectSelectorNode or
                    GesPredicateSelectorNode or
                    GesCountSelectorNode or
                    GesSumSelectorNode or
                    GesAverageSelectorNode or
                    GesMinSelectorNode or
                    GesMaxSelectorNode or
                    GesMapSelectorNode or
                    GesDistinctSelectorNode { Identifier: not null, Projection: not null } or
                    GesGroupBySelectorNode or
                    GesOrderBySelectorNode or
                    GesObjectMatchSelectorNode => true,
                GesEdgeSelectorNode edge => hasPrefix || edge.Predicate is not null,
                GesSequenceSliceSelectorNode => hasPrefix,
                GesDrawSelectorNode => hasPrefix,
                _ => false
            };

        private static bool CanEmitFirstValueAggregatePipeline(GesCollectionSelectorNode terminal, int prefixCount)
            => prefixCount == 0 &&
               (terminal switch
               {
                   GesSumSelectorNode sum => IsIdentityProjection(sum.Identifier, sum.Projection, sum.BindingNames),
                   GesAverageSelectorNode average => IsIdentityProjection(average.Identifier, average.Projection, average.BindingNames),
                   _ => false
               });

        private void EmitInlineIteratorPipeline(
            GesRegisterRef destination,
            GesRegisterRef source,
            IReadOnlyList<GesCollectionSelectorNode> selectors,
            int prefixCount,
            GesCollectionSelectorNode terminal,
            LoweringContext context,
            ExpressionState state,
            GesExpressionNode? iteratorSource = null)
        {
            if (iteratorSource is null && CanEmitFirstValueAggregatePipeline(terminal, prefixCount))
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

            ComponentRow? row = null;
            if (iteratorSource is null)
            {
                EmitInlinePipelineSourceGuard(source, terminal, prefixCount, invalidIteratorLabel, context, state);
                _builder.IteratorCreateOrJump(iterator, source, invalidIteratorLabel);
            }
            else row = EmitComponentIterator(iterator, iteratorSource, invalidIteratorLabel, context, state, prefixCount == 0 ? terminal : null);

            // Each collecting terminal uses exactly one builder kind.
            GesRegisterRef? builder = null;
            GesRegisterRef? count = null;
            GesRegisterRef? one = null;
            GesRegisterRef? hasSum = null;
            GesRegisterRef? sum = null;
            GesRegisterRef? extremaKey = null;
            GesRegisterRef? extremaCompare = null;

            switch (terminal)
            {
                case GesFilterSelectorNode or GesSelectSelectorNode or GesSequenceSliceSelectorNode or GesDrawSelectorNode:
                    builder = state.AllocateTemporary(_builder, context);
                    _builder.ListBuilderCreate(builder.Value);
                    break;
                case GesMapSelectorNode:
                    builder = state.AllocateTemporary(_builder, context);
                    _builder.MapBuilderCreate(builder.Value);
                    break;
                case GesDistinctSelectorNode { Identifier: not null, Projection: not null }:
                    builder = state.AllocateTemporary(_builder, context);
                    _builder.DistinctBuilderCreate(builder.Value);
                    break;
                case GesGroupBySelectorNode:
                    builder = state.AllocateTemporary(_builder, context);
                    _builder.GroupBuilderCreate(builder.Value);
                    break;
                case GesOrderBySelectorNode:
                    builder = state.AllocateTemporary(_builder, context);
                    _builder.OrderBuilderCreate(builder.Value);
                    break;
                case GesCountSelectorNode or GesAverageSelectorNode:
                    count = destination;
                    _builder.LoadInteger(count.Value, 0);
                    one = state.AllocateTemporary(_builder, context);
                    _builder.LoadInteger(one.Value, 1);
                    if (terminal is GesAverageSelectorNode)
                    {
                        hasSum = state.AllocateTemporary(_builder, context);
                        sum = state.AllocateTemporary(_builder, context);
                        _builder.LoadFalse(hasSum.Value);
                        _builder.LoadNothing(sum.Value);
                    }
                    break;
                case GesPredicateSelectorNode predicate: _builder.LoadBoolean(destination, predicate.Operator == "all"); break;
                case GesSumSelectorNode:
                    hasSum = state.AllocateTemporary(_builder, context);
                    sum = destination;
                    _builder.LoadFalse(hasSum.Value);
                    _builder.LoadNothing(sum.Value);
                    break;
                case GesMinSelectorNode or GesMaxSelectorNode:
                    hasSum = state.AllocateTemporary(_builder, context);
                    extremaKey = state.AllocateTemporary(_builder, context);
                    extremaCompare = state.AllocateTemporary(_builder, context);
                    _builder.LoadFalse(hasSum.Value);
                    _builder.LoadNothing(destination);
                    _builder.LoadNothing(extremaKey.Value);
                    break;
                case GesEdgeSelectorNode { Mode: "single" }:
                    count = state.AllocateTemporary(_builder, context);
                    one = state.AllocateTemporary(_builder, context);
                    _builder.LoadInteger(count.Value, 0);
                    _builder.LoadInteger(one.Value, 1);
                    _builder.LoadNothing(destination);
                    break;
                case GesForeachSelectorNode:
                case GesEdgeSelectorNode: _builder.LoadNothing(destination); break;
                case GesObjectMatchSelectorNode: _builder.LoadFalse(destination); break;
            }

            _builder.MarkLabel(loopLabel);
            if (row is null) _builder.IteratorNext(item, iterator, endLabel);
            else
            {
                _builder.IteratorNextComponents(row.Components, iterator, endLabel);
                _componentRows.Add(item.Id, row);
            }

            var current = item;
            for (var index = 0; index < prefixCount; index++) current = EmitInlinePipelineStep(selectors[index], current, nextLabel, context, state);

            if (NeedsWholeRow(terminal)) MaterializeRow(current);
            EmitInlinePipelineTerminal(destination, current, terminal, nextLabel, endLabel, builder, count, one, hasSum, sum, extremaKey, extremaCompare, context, state);
            if (row is not null) _componentRows.Remove(item.Id);
            _builder.MarkLabel(nextLabel);
            _builder.Jump(loopLabel);

            _builder.MarkLabel(endLabel);
            _builder.IteratorClose(iterator);

            switch (terminal)
            {
                case GesFilterSelectorNode or GesSelectSelectorNode: _builder.ListBuilderFinish(destination, builder!.Value); break;
                case GesSequenceSliceSelectorNode slice:
                {
                    var collected = state.AllocateTemporary(_builder, context);
                    _builder.ListBuilderFinish(collected, builder!.Value);
                    EmitSlice(destination, collected, slice);
                    break;
                }
                case GesDrawSelectorNode draw:
                {
                    var collected = state.AllocateTemporary(_builder, context);
                    _builder.ListBuilderFinish(collected, builder!.Value);
                    if (draw.Count == 1) _builder.First(destination, collected);
                    else _builder.TakeFirst(destination, collected, ToShort(draw.Count, "draw count"));
                    break;
                }
                case GesMapSelectorNode: _builder.MapBuilderFinish(destination, builder!.Value); break;
                case GesDistinctSelectorNode { Identifier: not null, Projection: not null }: _builder.DistinctBuilderFinish(destination, builder!.Value); break;
                case GesGroupBySelectorNode: _builder.GroupBuilderFinish(destination, builder!.Value); break;
                case GesOrderBySelectorNode orderBy:
                    if (orderBy.Direction == "descending") _builder.OrderBuilderFinishDescending(destination, builder!.Value);
                    else _builder.OrderBuilderFinishAscending(destination, builder!.Value);
                    break;
                case GesSumSelectorNode:
                    _builder.JumpIfTrue(hasSum!.Value, closeLabel);
                    _builder.LoadInteger(destination, 0);
                    break;
                case GesAverageSelectorNode:
                {
                    var noAverageLabel = _builder.AddLabel("pipeline_average_empty");
                    _builder.JumpIfNotTrue(count!.Value, noAverageLabel);
                    _builder.NumericBinary(GesBytecodeOpCode.Divide, destination, sum!.Value, count.Value);
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

        private void EmitInlinePipelineSourceGuard(GesRegisterRef source, GesCollectionSelectorNode terminal, int prefixCount, GesLabelRef invalidIteratorLabel, LoweringContext context, ExpressionState state)
        {
            if (prefixCount != 0) return;

            switch (terminal)
            {
                case GesDistinctSelectorNode { Identifier: not null, Projection: not null }:
                case GesOrderBySelectorNode: EmitSourceTypeGuard(source, invalidIteratorLabel, context, state, GesBytecodeTypeKind.List); return;
                case GesGroupBySelectorNode: EmitSourceTypeGuard(source, invalidIteratorLabel, context, state, GesBytecodeTypeKind.List, GesBytecodeTypeKind.Map, GesBytecodeTypeKind.Custom); return;
            }
        }

        private void EmitSourceTypeGuard(GesRegisterRef source, GesLabelRef invalidIteratorLabel, LoweringContext context, ExpressionState state, params GesBytecodeTypeKind[] allowedKinds)
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
            IReadOnlyList<GesCollectionSelectorNode> selectors,
            int prefixCount,
            GesCollectionSelectorNode terminal,
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

            var isAverage = terminal is GesAverageSelectorNode;
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
            for (var index = 0; index < prefixCount; index++) current = EmitInlinePipelineStep(selectors[index], current, firstLabel, context, state);

            var firstValue = EmitInlineAggregateValue(terminal, current, context, state);
            EmitNumericCopy(accumulator, firstValue);
            if (isAverage)
            {
                _builder.LoadInteger(count!.Value, 1);
                _builder.LoadInteger(one!.Value, 1);
            }

            _builder.Jump(loopLabel);

            _builder.MarkLabel(loopLabel);
            _builder.IteratorNext(item, iterator, endLabel);
            current = item;
            for (var index = 0; index < prefixCount; index++) current = EmitInlinePipelineStep(selectors[index], current, loopLabel, context, state);

            var value = EmitInlineAggregateValue(terminal, current, context, state);
            if (isAverage) _builder.Add(count!.Value, count.Value, one!.Value);
            _builder.NumericBinary(GesBytecodeOpCode.Add, accumulator, accumulator, value);
            _builder.Jump(loopLabel);

            _builder.MarkLabel(emptyLabel);
            _builder.IteratorClose(iterator);
            if (isAverage) _builder.LoadNothing(destination);
            else _builder.LoadInteger(destination, 0);
            _builder.Jump(doneLabel);

            _builder.MarkLabel(endLabel);
            _builder.IteratorClose(iterator);
            if (isAverage) _builder.NumericBinary(GesBytecodeOpCode.Divide, destination, accumulator, count!.Value);
            _builder.Jump(doneLabel);

            _builder.MarkLabel(invalidIteratorLabel);
            _builder.LoadNothing(destination);
            _builder.MarkLabel(doneLabel);
        }

        private GesRegisterRef EmitInlineAggregateValue(GesCollectionSelectorNode terminal, GesRegisterRef current, LoweringContext context, ExpressionState state)
        {
            return terminal switch
            {
                GesSumSelectorNode sum => EmitProjectionForRead(sum.Identifier, current, sum.Projection, context, state, sum.BindingNames),
                GesAverageSelectorNode average => EmitProjectionForRead(average.Identifier, current, average.Projection, context, state, average.BindingNames),
                _ => throw CompileFailure(GesDiagnosticCodes.CompileUnsupportedConstruct, $"GameEventScript binary compiler cannot aggregate terminal selector node '{terminal.GetType().Name}'.")
            };
        }

        private GesRegisterRef EmitInlinePipelineStep(GesCollectionSelectorNode selector, GesRegisterRef current, GesLabelRef nextLabel, LoweringContext context, ExpressionState state)
        {
            switch (selector)
            {
                case GesFilterSelectorNode filter:
                    EmitSkipUnless(filter.Identifier, current, filter.Predicate, nextLabel, context, state, filter.BindingNames);
                    return current;
                case GesSelectSelectorNode select:
                    if (IsIdentityProjection(select.Identifier, select.Projection, select.BindingNames)) MaterializeRow(current);
                    return EmitProjectionForRead(select.Identifier, current, select.Projection, context, state, select.BindingNames);
                default: throw CompileFailure(GesDiagnosticCodes.CompileUnsupportedConstruct, $"GameEventScript binary compiler cannot inline non-terminal selector node '{selector.GetType().Name}'.");
            }
        }

        private void EmitInlinePipelineTerminal(
            GesRegisterRef destination,
            GesRegisterRef current,
            GesCollectionSelectorNode terminal,
            GesLabelRef nextLabel,
            GesLabelRef endLabel,
            GesRegisterRef? builder,
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
                case GesForeachSelectorNode each: _ = EmitSelectorExpressionForRead(each.Identifier, current, each.Expression, context, state, each.BindingNames); return;
                case GesFilterSelectorNode filter:
                    EmitSkipUnless(filter.Identifier, current, filter.Predicate, nextLabel, context, state, filter.BindingNames);
                    _builder.ListBuilderAdd(builder!.Value, current);
                    return;
                case GesSelectSelectorNode select: _builder.ListBuilderAdd(builder!.Value, EmitProjectionForRead(select.Identifier, current, select.Projection, context, state, select.BindingNames)); return;
                case GesSequenceSliceSelectorNode or GesDrawSelectorNode: _builder.ListBuilderAdd(builder!.Value, current); return;
                case GesMapSelectorNode map:
                {
                    var key = EmitSelectorExpressionForRead(map.Identifier, current, map.KeyProjection, context, state, map.BindingNames);
                    var value = map.ValueProjection is null ? current : EmitSelectorExpressionForRead(map.Identifier, current, map.ValueProjection, context, state, map.BindingNames);
                    _builder.MapBuilderAdd(builder!.Value, key, value);
                    return;
                }
                case GesDistinctSelectorNode { Identifier: not null, Projection: not null } distinct:
                    _builder.DistinctBuilderAdd(builder!.Value, EmitSelectorExpressionForRead(distinct.Identifier, current, distinct.Projection, context, state, distinct.BindingNames), current);
                    return;
                case GesGroupBySelectorNode groupBy: _builder.GroupBuilderAdd(builder!.Value, EmitSelectorExpressionForRead(groupBy.Identifier, current, groupBy.Projection, context, state, groupBy.BindingNames), current); return;
                case GesOrderBySelectorNode orderBy: _builder.OrderBuilderAdd(builder!.Value, EmitSelectorExpressionForRead(orderBy.Identifier, current, orderBy.Projection, context, state, orderBy.BindingNames), current); return;
                case GesCountSelectorNode countSelector:
                    EmitSkipUnless(countSelector.Identifier, current, countSelector.Predicate, nextLabel, context, state, countSelector.BindingNames);
                    _builder.Add(count!.Value, count.Value, one!.Value);
                    return;
                case GesPredicateSelectorNode predicateSelector:
                {
                    // :all stops at the first item that fails, :any at the first that holds.
                    var predicate = EmitSelectorExpressionForRead(predicateSelector.Identifier, current, predicateSelector.Predicate, context, state, predicateSelector.BindingNames);
                    var all = predicateSelector.Operator == "all";
                    if (all) _builder.JumpIfTrue(predicate, nextLabel);
                    else _builder.JumpIfNotTrue(predicate, nextLabel);
                    _builder.LoadBoolean(destination, !all);
                    _builder.Jump(endLabel);
                    return;
                }
                case GesSumSelectorNode sumSelector:
                    EmitInlineAccumulateSum(sum!.Value, hasSum!.Value, EmitProjectionForRead(sumSelector.Identifier, current, sumSelector.Projection, context, state, sumSelector.BindingNames), nextLabel, context, state);
                    return;
                case GesAverageSelectorNode averageSelector:
                {
                    var value = EmitProjectionForRead(averageSelector.Identifier, current, averageSelector.Projection, context, state, averageSelector.BindingNames);
                    _builder.Add(count!.Value, count.Value, one!.Value);
                    EmitInlineAccumulateSum(sum!.Value, hasSum!.Value, value, nextLabel, context, state);
                    return;
                }
                case GesMinSelectorNode minSelector:
                    EmitInlineExtremaStep(destination, current, minSelector.Identifier, minSelector.Projection, minSelector.BindingNames, hasSum!.Value, extremaKey!.Value, extremaCompare!.Value, isMax: false, nextLabel, context, state);
                    return;
                case GesMaxSelectorNode maxSelector:
                    EmitInlineExtremaStep(destination, current, maxSelector.Identifier, maxSelector.Projection, maxSelector.BindingNames, hasSum!.Value, extremaKey!.Value, extremaCompare!.Value, isMax: true, nextLabel, context, state);
                    return;
                case GesEdgeSelectorNode edge:
                    if (edge.Predicate is not null && edge.Identifier is not null)
                    {
                        var predicate = EmitSelectorExpressionForRead(edge.Identifier, current, edge.Predicate, context, state, edge.BindingNames);
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
                case GesObjectMatchSelectorNode objectMatch:
                {
                    var match = EmitObjectPatternPredicate(current, objectMatch.Pattern, context, state);
                    _builder.JumpIfNotTrue(match, nextLabel);
                    _builder.LoadTrue(destination);
                    _builder.Jump(endLabel);
                    return;
                }
                default: throw CompileFailure(GesDiagnosticCodes.CompileUnsupportedConstruct, $"GameEventScript binary compiler cannot inline terminal selector node '{terminal.GetType().Name}'.");
            }
        }

        private void EmitInlineExtremaStep(
            GesRegisterRef destination,
            GesRegisterRef current,
            string identifier,
            GesExpressionNode projectionExpression,
            IReadOnlyList<string> names,
            GesRegisterRef hasWinner,
            GesRegisterRef winnerKey,
            GesRegisterRef isBetter,
            bool isMax,
            GesLabelRef nextLabel,
            LoweringContext context,
            ExpressionState state)
        {
            var projection = EmitProjectionForRead(identifier, current, projectionExpression, context, state, names);
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

        private void EmitNumericCopy(GesRegisterRef destination, GesRegisterRef value)
        {
            _builder.NumericUnary(GesBytecodeOpCode.Move, destination, value);
        }

        private void EmitInlineAccumulateSum(GesRegisterRef sum, GesRegisterRef hasSum, GesRegisterRef value, GesLabelRef nextLabel, LoweringContext context, ExpressionState state)
        {
            var addLabel = _builder.AddLabel("pipeline_sum_add");
            _builder.JumpIfTrue(hasSum, addLabel);
            EmitNumericCopy(sum, value);
            _builder.LoadTrue(hasSum);
            _builder.Jump(nextLabel);
            _builder.MarkLabel(addLabel);
            _builder.NumericBinary(GesBytecodeOpCode.Add, sum, sum, value);
        }

        private void EmitFold(GesRegisterRef destination, GesRegisterRef source, GesFoldSelectorNode fold, LoweringContext context, ExpressionState state, GesExpressionNode? iteratorSource = null)
        {
            var accumulator = state.AllocateTemporary(_builder, context);
            var iterator = state.AllocateTemporary(_builder, context);
            var item = state.AllocateTemporary(_builder, context);
            var loop = _builder.AddLabel("fold_next");
            var end = _builder.AddLabel("fold_end");
            var invalid = _builder.AddLabel("fold_invalid");
            var done = _builder.AddLabel("fold_done");
            ComponentRow? row = null;
            GesRegisterRef? validSource = null;
            if (iteratorSource is not null)
            {
                validSource = state.AllocateTemporary(_builder, context);
                var prepared = _builder.AddLabel("fold_source_prepared");
                _builder.LoadFalse(validSource.Value);
                row = EmitComponentIterator(iterator, iteratorSource, prepared, context, state);
                _builder.LoadTrue(validSource.Value);
                _builder.MarkLabel(prepared);
            }
            if (fold.Seed is null) _builder.LoadNothing(accumulator);
            else
            {
                var seed = EmitExpressionForRead(fold.Seed, context, state);
                _builder.Move(accumulator, seed);
            }
            if (validSource is { } valid) _builder.JumpIfNotTrue(valid, invalid);
            else _builder.IteratorCreateOrJump(iterator, source, invalid);
            if (fold.Seed is null) _builder.IteratorNext(accumulator, iterator, end);
            _builder.MarkLabel(loop);
            if (row is null) _builder.IteratorNext(item, iterator, end);
            else
            {
                _builder.IteratorNextComponents(row.Components, iterator, end);
                _componentRows.Add(item.Id, row);
            }
            var child = context.CreateChild();
            child.DeclareExisting(fold.Accumulator, accumulator);
            BindComponents(child, fold.Identifier, fold.BindingNames, item, state);
            var result = EmitExpressionForRead(fold.Projection, child, state);
            if (row is not null) _componentRows.Remove(item.Id);
            if (result.Id != accumulator.Id) _builder.Move(accumulator, result);
            _builder.Jump(loop);
            _builder.MarkLabel(end);
            _builder.IteratorClose(iterator);
            _builder.Move(destination, accumulator);
            _builder.Jump(done);
            _builder.MarkLabel(invalid);
            _builder.LoadNothing(destination);
            _builder.MarkLabel(done);
        }

        private GesRegisterRef EmitSelectorExpressionForRead(string identifier, GesRegisterRef current, GesExpressionNode expression, LoweringContext context, ExpressionState state, IReadOnlyList<string>? names = null)
        {
            var selectorContext = context.CreateChild();
            BindComponents(selectorContext, identifier, names, current, state);
            return EmitExpressionForRead(expression, selectorContext, state);
        }

        // The bound item itself for an identity projection, otherwise the projection evaluated against it.
        private GesRegisterRef EmitProjectionForRead(string identifier, GesRegisterRef current, GesExpressionNode projection, LoweringContext context, ExpressionState state, IReadOnlyList<string>? names)
            => IsIdentityProjection(identifier, projection, names) ? current : EmitSelectorExpressionForRead(identifier, current, projection, context, state, names);

        // Skips to nextLabel unless the predicate holds; a literal true predicate emits nothing.
        private void EmitSkipUnless(string identifier, GesRegisterRef current, GesExpressionNode predicate, GesLabelRef nextLabel, LoweringContext context, ExpressionState state, IReadOnlyList<string> names)
        {
            if (!IsAlwaysTrue(predicate)) _builder.JumpIfNotTrue(EmitSelectorExpressionForRead(identifier, current, predicate, context, state, names), nextLabel);
        }

        private static bool IsIdentityProjection(string identifier, GesExpressionNode expression, IReadOnlyList<string>? names = null)
            => (names is null || names.Count < 2) && expression is GesIdentifierExpressionNode projection && string.Equals(projection.Name, identifier, StringComparison.Ordinal);

        private static bool IsAlwaysTrue(GesExpressionNode expression) => expression is GesBooleanLiteralExpressionNode { Value: true };

        private void EmitPattern(GesRegisterRef destination, GesRegisterRef target, GesDicePatternNode pattern, bool take, LoweringContext context, ExpressionState state)
        {
            switch (pattern)
            {
                case GesDiceCountPatternNode { Face: null } count:
                    if (take) _builder.TakePattern(destination, target, GesBytecodePatternKind.CountAny, ToShort(count.Count, "pattern count"));
                    else _builder.HasPattern(destination, target, GesBytecodePatternKind.CountAny, ToShort(count.Count, "pattern count"));
                    return;
                case GesDiceCountPatternNode count:
                {
                    var face = EmitExpressionForRead(count.Face!, context, state);
                    if (take) _builder.TakePattern(destination, target, GesBytecodePatternKind.CountFace, ToShort(count.Count, "pattern count"), face);
                    else _builder.HasPattern(destination, target, GesBytecodePatternKind.CountFace, ToShort(count.Count, "pattern count"), face);
                    return;
                }
                case GesDiceFullHousePatternNode:
                    if (take) _builder.TakePattern(destination, target, GesBytecodePatternKind.FullHouse);
                    else _builder.HasPattern(destination, target, GesBytecodePatternKind.FullHouse);
                    return;
                case GesDiceStraightPatternNode:
                    if (take) _builder.TakePattern(destination, target, GesBytecodePatternKind.Straight);
                    else _builder.HasPattern(destination, target, GesBytecodePatternKind.Straight);
                    return;
                default: throw CompileFailure(GesDiagnosticCodes.CompileUnsupportedConstruct, $"GameEventScript binary compiler does not support dice pattern node '{pattern.GetType().Name}'.");
            }
        }

        private GesLabelRef EmitHelperExpression(string name, GesExpressionNode expression, LoweringContext parentContext, ExpressionState parentState)
        {
            using var sourceRange = _builder.SourceRange(expression.SourceRange);
            using var helper = _builder.BeginHelper($"{name}_{_helperIndex++}");
            var helperContext = LoweringContext.ForRoutineWithParent(helper, parentContext);
            var result = EmitExpressionForRead(expression, helperContext, new ExpressionState(parentState.NextRegister));
            _builder.ReturnValue(result);
            return helper.EntryLabel;
        }

        private GesLabelRef EmitSelectorHelperExpression(string name, string identifier, GesExpressionNode expression, LoweringContext parentContext, ExpressionState parentState, GesRegisterRef itemBinding)
        {
            using var sourceRange = _builder.SourceRange(expression.SourceRange);
            using var helper = _builder.BeginHelper($"{name}_{_helperIndex++}");
            var helperContext = LoweringContext.ForRoutineWithParent(helper, parentContext);
            helperContext.DeclareExisting(identifier, itemBinding);
            var result = EmitExpressionForRead(expression, helperContext, new ExpressionState(Math.Max(parentState.NextRegister, itemBinding.Id + 1)));
            _builder.ReturnValue(result);
            return helper.EntryLabel;
        }

        private void EmitChoose(GesRegisterRef destination, GesRegisterRef target, GesChooseSelectorNode choose, LoweringContext context, ExpressionState state)
        {
            var source = target;
            if (choose.WeightExpression is not null && !string.IsNullOrEmpty(choose.WeightIdentifier))
            {
                EmitInlineWeightedChoose(destination, target, choose, context, state);
                return;
            }

            if (choose.Predicate is not null && !string.IsNullOrEmpty(choose.Identifier))
            {
                var filter = new GesFilterSelectorNode(choose.Identifier!, choose.Predicate) { BindingNames = choose.BindingNames };
                source = state.AllocateTemporary(_builder, context);
                EmitInlineIteratorPipeline(source, target, new GesCollectionSelectorNode[] { filter }, 0, filter, context, state);
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

        private void EmitInlineWeightedChoose(GesRegisterRef destination, GesRegisterRef source, GesChooseSelectorNode choose, LoweringContext context, ExpressionState state)
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
                var predicate = EmitSelectorExpressionForRead(choose.Identifier!, item, choose.Predicate, context, state, choose.BindingNames);
                _builder.JumpIfNotTrue(predicate, skipLabel);
            }

            var weight = EmitSelectorExpressionForRead(choose.WeightIdentifier!, item, choose.WeightExpression!, context, state, choose.WeightBindingNames);
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

        private void EmitMapSelectorLoop(GesRegisterRef destination, GesRegisterRef target, GesMapSelectorNode map, LoweringContext context, ExpressionState state)
        {
            EmitInlineIteratorPipeline(destination, target, new GesCollectionSelectorNode[] { map }, 0, map, context, state);
        }

        private GesRegisterRef EmitObjectPatternPredicate(GesRegisterRef target, GesObjectMatchPatternNode pattern, LoweringContext context, ExpressionState state)
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
                    case GesObjectMatchExpressionValueNode expression:
                    {
                        var expected = EmitExpressionForRead(expression.Expression, context, state);
                        entryMatch = state.AllocateTemporary(_builder, context);
                        _builder.Equal(entryMatch, member, expected);
                        break;
                    }
                    case GesObjectMatchNestedValueNode nested: entryMatch = EmitObjectPatternPredicate(member, nested.Pattern, context, state); break;
                    default: throw CompileFailure(GesDiagnosticCodes.CompileUnsupportedConstruct, $"GameEventScript binary compiler does not support object match value '{entry.Value.GetType().Name}'.");
                }

                var combined = state.AllocateTemporary(_builder, context);
                _builder.And(combined, result, entryMatch);
                result = combined;
            }

            return result;
        }

        private void EmitSlice(GesRegisterRef destination, GesRegisterRef target, GesSequenceSliceSelectorNode slice)
        {
            var count = ToShort(slice.Count, "slice count");
            switch (slice.Operation, slice.Scope)
            {
                case ("take", "first"): _builder.TakeFirst(destination, target, count); return;
                case ("take", "last"): _builder.TakeLast(destination, target, count); return;
                case ("take", "highest"): _builder.TakeHighest(destination, target, count); return;
                case ("take", "lowest"): _builder.TakeLowest(destination, target, count); return;
                case ("drop", "first"): _builder.DropFirst(destination, target, count); return;
                case ("drop", "last"): _builder.DropLast(destination, target, count); return;
                case ("drop", "highest"): _builder.DropHighest(destination, target, count); return;
                case ("drop", "lowest"): _builder.DropLowest(destination, target, count); return;
                default: throw CompileFailure(GesDiagnosticCodes.CompileUnsupportedConstruct, $"GameEventScript binary compiler does not support slice selector '{slice.Operation} {slice.Scope}'.");
            }
        }

        private void EmitContains(GesRegisterRef destination, GesRegisterRef target, GesContainsSelectorNode contains, LoweringContext context, ExpressionState state)
        {
            var needle = EmitExpressionForRead(contains.ValueExpression, context, state);
            switch (contains.Mode)
            {
                case "all": _builder.ContainsAll(destination, needle, target); break;
                case "any": _builder.ContainsAny(destination, needle, target); break;
                default: _builder.Contains(destination, needle, target); break;
            }
        }

        private void EmitGeneratedCollectionInto(GesGeneratedCollectionExpressionNode generatedCollection, GesRegisterRef destination, LoweringContext context, ExpressionState state)
        {
            if (generatedCollection.CollectionType != "list") throw CompileFailure(GesDiagnosticCodes.CompileUnsupportedConstruct, $"GameEventScript binary compiler does not support generated collection type '{generatedCollection.CollectionType}'.");

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
            if (skipProjectionLabel.HasValue) _builder.MarkLabel(skipProjectionLabel.Value);

            _builder.Jump(loopLabel);
            _builder.MarkLabel(endLabel);
            _builder.IteratorClose(iterator);
            _builder.ListBuilderFinish(destination, builderRegister);
        }

        private void EmitGuardedChoiceInto(GesGuardedChoiceExpressionNode guardedChoice, GesRegisterRef destination, LoweringContext context, ExpressionState state)
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

        private GesRegisterRef EmitIterator(GesIterationSourceNode source, LoweringContext context, ExpressionState state)
        {
            switch (source)
            {
                case GesRangeIterationSourceNode range when GetRangeIteratorShort(range.RangeExpression) is { } shortRange:
                {
                    var iterator = state.AllocateTemporary(_builder, context);
                    _builder.CreateRangeIteratorShort(iterator, shortRange.From, shortRange.To, shortRange.Step);
                    return iterator;
                }

                case GesRangeIterationSourceNode range:
                {
                    var from = EmitExpressionForRead(range.RangeExpression.FromExpression, context, state);
                    var to = EmitExpressionForRead(range.RangeExpression.ToExpression, context, state);
                    var iterator = state.AllocateTemporary(_builder, context);
                    if (range.RangeExpression.StepExpression is null) _builder.CreateRangeIterator(iterator, from, to);
                    else _builder.CreateRangeIteratorWithStep(iterator, from, to, EmitExpressionForRead(range.RangeExpression.StepExpression, context, state));

                    return iterator;
                }

                case GesCollectionIterationSourceNode collection:
                {
                    var iterator = state.AllocateTemporary(_builder, context);
                    if (collection.Expression is GesCombinedCollectionExpressionNode || IsEntriesSource(collection.Expression))
                    {
                        var done = _builder.AddLabel("for_source_done");
                        _ = EmitComponentIterator(iterator, collection.Expression, done, context, state);
                        _builder.MarkLabel(done);
                    }
                    else
                    {
                        var collectionRegister = EmitExpressionForRead(collection.Expression, context, state);
                        _builder.IteratorCreate(iterator, collectionRegister);
                    }
                    return iterator;
                }

                default: throw CompileFailure(GesDiagnosticCodes.CompileUnsupportedConstruct, $"GameEventScript binary compiler does not support iteration source '{source.GetType().Name}'.");
            }
        }

        private StageArgumentPlan PrepareStageArgument(GesExpressionNode expression, LoweringContext context, ExpressionState state)
        {
            return IsStageConstant(expression)
                ? StageArgumentPlan.FromExpression(expression)
                : StageArgumentPlan.FromRegister(EmitExpressionForRead(expression, context, state));
        }

        private void EmitStageArguments(IReadOnlyList<GesExpressionNode> expressions, LoweringContext context, ExpressionState state)
        {
            if (expressions.Count == 0) return;
            var arguments = new StageArgumentPlan[expressions.Count];
            for (var index = 0; index < expressions.Count; index++) arguments[index] = PrepareStageArgument(expressions[index], context, state);

            EmitPreparedStageArguments(arguments);
        }

        private void EmitStageArgumentNodes(IReadOnlyList<GesArgumentNode> arguments, LoweringContext context, ExpressionState state)
        {
            if (arguments.Count == 0) return;
            var prepared = new StageArgumentPlan[arguments.Count];
            for (var index = 0; index < arguments.Count; index++) prepared[index] = PrepareStageArgument(arguments[index].Expression, context, state);

            EmitPreparedStageArguments(prepared);
        }

        private void EmitStageMapValues(IReadOnlyList<GesMapEntryNode> entries, LoweringContext context, ExpressionState state)
        {
            if (entries.Count == 0) return;
            var arguments = new StageArgumentPlan[entries.Count];
            for (var index = 0; index < entries.Count; index++) arguments[index] = PrepareStageArgument(entries[index].Value, context, state);

            EmitPreparedStageArguments(arguments);
        }

        private void EmitPreparedStageArguments(IReadOnlyList<StageArgumentPlan> arguments)
        {
            for (var index = 0; index < arguments.Count; index++) EmitStageArgument(arguments[index]);
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

        private static bool IsStageConstant(GesExpressionNode expression)
            => expression is GesBooleanLiteralExpressionNode or
                GesIntegerLiteralExpressionNode or
                GesUnitIntegerLiteralExpressionNode or
                GesFloatLiteralExpressionNode or
                GesUnitFloatLiteralExpressionNode or
                GesPercentageLiteralExpressionNode or
                GesTextLiteralExpressionNode or
                GesTagLiteralExpressionNode or
                GesNothingLiteralExpressionNode;

        private void EmitStageConstant(GesExpressionNode expression)
        {
            using var sourceRange = _builder.SourceRange(expression.SourceRange);
            switch (expression)
            {
                case GesBooleanLiteralExpressionNode boolean: _builder.StageBoolean(boolean.Value); return;
                case GesIntegerLiteralExpressionNode integer: _builder.StageInteger(integer.Value); return;
                case GesUnitIntegerLiteralExpressionNode integer: _builder.StageInteger(integer.Value, ResolveUnitOrNone(integer.UnitName)); return;
                case GesFloatLiteralExpressionNode number: _builder.StageFloat(number.Value); return;
                case GesUnitFloatLiteralExpressionNode number: _builder.StageFloat(number.Value, ResolveUnitOrNone(number.UnitName)); return;
                case GesPercentageLiteralExpressionNode percentage: _builder.StagePercentage(percentage.RatioValue); return;
                case GesTextLiteralExpressionNode text: _builder.StageText(text.Value); return;
                case GesTagLiteralExpressionNode tag: _builder.StageTag(tag.Name); return;
                case GesNothingLiteralExpressionNode: _builder.StageNothing(); return;
                default: throw CompileFailure(GesDiagnosticCodes.CompileUnsupportedConstruct, $"GameEventScript binary compiler cannot stage non-constant expression '{expression.GetType().Name}' without preparing it first.");
            }
        }

        private void EmitRandomPush(GesExpressionNode seedExpression, LoweringContext context, ExpressionState state)
        {
            if (ReadUnitlessIntegerLiteralSeed(seedExpression) is { } seed)
            {
                _builder.RandomPushConstant(seed);
                return;
            }

            var source = EmitExpressionForRead(seedExpression, context, state);
            _builder.RandomPush(source);
        }

        private void EmitCallInto(GesCallExpressionNode call, GesRegisterRef destination, LoweringContext context, ExpressionState state)
        {
            var callable = GesCallableSignatures.Resolve(module.Callables, call);
            if (callable is null || !_callableRoutines.TryGetValue(callable.SignatureId, out var entry))
            {
                throw CompileFailure(GesDiagnosticCodes.CompileUnresolvedSymbol, $"GameEventScript binary compiler could not resolve callable '{call.Name}'.", call.Name);
            }

            EmitStageArguments(call.Arguments, context, state);

            _builder.Call(destination, entry.EntryLabel, callable.Kind == GesCallableKind.PredicateCall ? GesInstructionFlag.NormalizeResultAsPredicate : GesInstructionFlag.None);
        }

        private void EmitHandlerBindCallInto(GesCallExpressionNode call, GesRegisterRef destination, LoweringContext context, ExpressionState state)
        {
            var handler = context.Require(call.Name, call.SourceRange);
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

        private void EmitPredicateCallInto(GesPredicateCallExpressionNode predicate, GesRegisterRef destination, LoweringContext context, ExpressionState state)
        {
            var callable = GesCallableSignatures.ResolvePredicate(module.Callables, predicate.PredicateName, predicate.Value is null ? 0 : 1);
            if (callable is null || !_callableRoutines.TryGetValue(callable.SignatureId, out var entry))
            {
                throw CompileFailure(GesDiagnosticCodes.CompileUnresolvedSymbol, $"GameEventScript binary compiler could not resolve predicate '{predicate.PredicateName}'.", predicate.PredicateName);
            }

            if (predicate.Value is not null) EmitStageArgument(PrepareStageArgument(predicate.Value, context, state));
            _builder.Call(destination, entry.EntryLabel, GesInstructionFlag.NormalizeResultAsPredicate);
        }

        private void EmitExtensionPredicateInto(GesExtensionPredicateExpressionNode extensionPredicate, GesRegisterRef destination, LoweringContext context, ExpressionState state)
        {
            var reference = new GesExtensionReference(extensionPredicate.ExtensionName, extensionPredicate.FunctionName, [GesMessageSignature.UnlabeledParameterName]);
            var argument = EmitExpressionForRead(extensionPredicate.Value, context, state);
            EmitExtensionReferenceInto(reference, destination, [argument], isPredicate: true);
        }

        private void EmitExtensionCallInto(GesExtensionCallExpressionNode extensionCall, GesRegisterRef destination, LoweringContext context, ExpressionState state, bool isPredicate)
        {
            var argumentNames = ReadArgumentNames(extensionCall.Arguments);
            var arguments = EmitArgumentExpressionRegisters(extensionCall.Arguments, context, state);
            var reference = new GesExtensionReference(extensionCall.ExtensionName, extensionCall.FunctionName, argumentNames);
            EmitExtensionReferenceInto(reference, destination, arguments, isPredicate);
        }

        private void EmitExtensionReferenceInto(GesExtensionReference reference, GesRegisterRef destination, IReadOnlyList<GesRegisterRef> arguments, bool isPredicate)
        {
            _builder.CallExternal(destination, ResolveExtensionCall(reference), arguments, isPredicate ? GesInstructionFlag.NormalizeResultAsPredicate : GesInstructionFlag.None);
        }

        private void EmitTypeConstructorInto(GesTypeConstructorExpressionNode constructor, GesRegisterRef destination, LoweringContext context, ExpressionState state)
        {
            if (constructor.TypeName is "__split" or "__splitWhitespace")
            {
                var inputs = EmitArgumentExpressionRegisters(constructor.Arguments, context, state);
                _builder.AddOpcode(
                    GesBytecodeOpCode.SplitText,
                    dst: GesOperand.Register(destination),
                    x: GesOperand.Register(inputs[0]),
                    y: inputs.Length > 1 ? GesOperand.Register(inputs[1]) : default,
                    a: GesOperand.U16((ushort)(inputs.Length == 1 ? 1 : 0))
                );
                return;
            }
            if (constructor.TypeName == "record" || constructor.TypeName is "number" or "range" or "series" or "message" or "nothing" or "percentage" or "boolean" or "text" or "tag" or "list" or "map" or "dice" or "handler" &&
                (constructor.Arguments.Count != 1 || constructor.Arguments[0].Label is not null || constructor.TypeName == "series"))
            {
                var inputs = EmitArgumentExpressionRegisters(constructor.Arguments, context, state);
                _builder.AddOpcode(GesBytecodeOpCode.ConstructData, dst: GesOperand.Register(destination), x: GesOperand.Text(constructor.TypeName),
                    y: GesOperand.RegisterList(inputs), a: GesOperand.TextList(ReadArgumentNames(constructor.Arguments)));
                return;
            }
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
                    if (field.ConstructorLabel == GesMessageSignature.UnlabeledParameterName)
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

            throw CompileFailure(GesDiagnosticCodes.CompileUnsupportedConstruct, $"GameEventScript binary compiler does not support type constructor ':{constructor.TypeName}'.");
        }

        private bool EmitSpatialConstructor(GesTypeConstructorExpressionNode constructor, GesRegisterRef destination, LoweringContext context, ExpressionState state)
        {
            if (GetSpatialConstructorStageShape(constructor.Arguments) is not { } shape) return false;
            EmitStageArgumentNodes(shape.Arguments, context, state);
            if (constructor.TypeName == "point") _builder.CreatePoint(destination, (short)shape.StartComponent);
            else _builder.CreateVector(destination, (short)shape.StartComponent);
            return true;
        }

        private void EmitCastInto(GesRegisterRef destination, GesRegisterRef value, string? typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName)) throw CompileFailure(GesDiagnosticCodes.CompileUnsupportedConstruct, "GameEventScript binary compiler requires a type name.");

            if (GetQuantityUnit(typeName) is { } unit) _builder.CastUnit(destination, value, unit);
            else if (GesQuantityUnits.IsQuantityTypeName(typeName)) throw CompileFailure(GesDiagnosticCodes.CompileUnsupportedConstruct, $"GameEventScript binary compiler does not support quantity type '{typeName}'.");
            else if (typeName is "number" or "numeric" or "numeric:integer" or "numeric:fractional") _builder.CastNumeric(destination, value);
            else if (GetBytecodeTypeKind(typeName) is { } kind) _builder.Cast(destination, value, kind);
            else _builder.CastCustom(destination, value, typeName);
        }

        private void EmitCheckInto(GesRegisterRef destination, GesRegisterRef value, string? typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName)) throw CompileFailure(GesDiagnosticCodes.CompileUnsupportedConstruct, "GameEventScript binary compiler requires a type name.");

            if (GetQuantityUnit(typeName) is { } unit) _builder.CheckUnit(destination, value, unit);
            else if (GesQuantityUnits.IsQuantityTypeName(typeName)) throw CompileFailure(GesDiagnosticCodes.CompileUnsupportedConstruct, $"GameEventScript binary compiler does not support quantity type '{typeName}'.");
            else if (typeName is "number" or "numeric") _builder.CheckNumeric(destination, value);
            else if (typeName == "numeric:integer") _builder.CheckInteger(destination, value);
            else if (typeName == "numeric:fractional") _builder.CheckFractional(destination, value);
            else if (GetBytecodeTypeKind(typeName) is { } kind) _builder.CheckType(destination, value, kind);
            else _builder.CheckCustomType(destination, value, typeName);
        }

        private void EmitUnary(GesRegisterRef destination, GesUnaryOperator operation, GesRegisterRef operand)
        {
            switch (operation)
            {
                case GesUnaryOperator.Parse: _builder.ParseLiteral(destination, operand); break;
                case GesUnaryOperator.ParseStrict: _builder.ParseLiteral(destination, operand, strict: true); break;
                case GesUnaryOperator.Negate: _builder.Negate(destination, operand); return;
                case GesUnaryOperator.Not: _builder.Not(destination, operand); return;
                case GesUnaryOperator.HasValue: _builder.HasValue(destination, operand); return;
                case GesUnaryOperator.Empty: _builder.IsEmpty(destination, operand); return;
                case GesUnaryOperator.Chance: _builder.Chance(destination, operand); return;
                case GesUnaryOperator.Keys: _builder.KeysOfMap(destination, operand); return;
                case GesUnaryOperator.Values: _builder.ValuesOfMap(destination, operand); return;
                case GesUnaryOperator.Entries: _builder.EntriesOfMap(destination, operand); return;
                case GesUnaryOperator.Abs: _builder.Abs(destination, operand); return;
                case GesUnaryOperator.NaturalLog: _builder.LogN(destination, operand); return;
                case GesUnaryOperator.Exp: _builder.Exp(destination, operand); return;
                case GesUnaryOperator.Floor: _builder.Floor(destination, operand); return;
                case GesUnaryOperator.Ceil: _builder.Ceil(destination, operand); return;
                case GesUnaryOperator.Truncate: _builder.Truncate(destination, operand); return;
                case GesUnaryOperator.RoundHalfEven: _builder.RoundHalfEven(destination, operand); return;
                case GesUnaryOperator.RoundHalfUp: _builder.RoundHalfUp(destination, operand); return;
                case GesUnaryOperator.RoundHalfDown: _builder.RoundHalfDown(destination, operand); return;
                case GesUnaryOperator.DegreeToRadians: _builder.DegreeToRadians(destination, operand); return;
                case GesUnaryOperator.DegreeFromRadians: _builder.DegreeFromRadians(destination, operand); return;
                case GesUnaryOperator.WrapDegree: _builder.WrapDegree(destination, operand); return;
                case GesUnaryOperator.Sin: _builder.Sin(destination, operand); return;
                case GesUnaryOperator.Cos: _builder.Cos(destination, operand); return;
                case GesUnaryOperator.Tan: _builder.Tan(destination, operand); return;
                case GesUnaryOperator.Asin: _builder.Asin(destination, operand); return;
                case GesUnaryOperator.Acos: _builder.Acos(destination, operand); return;
                case GesUnaryOperator.Atan: _builder.Atan(destination, operand); return;
                default: throw CompileFailure(GesDiagnosticCodes.CompileUnsupportedConstruct, $"GameEventScript binary compiler does not support unary operator '{operation.ToSourceText()}'.");
            }
        }

        private void EmitBinary(GesRegisterRef destination, GesBinaryOperator operation, GesRegisterRef left, GesRegisterRef right)
        {
            switch (operation)
            {
                case GesBinaryOperator.Or: _builder.Or(destination, left, right); return;
                case GesBinaryOperator.Xor: _builder.Xor(destination, left, right); return;
                case GesBinaryOperator.And: _builder.And(destination, left, right); return;
                case GesBinaryOperator.Implies: _builder.Implies(destination, left, right); return;
                case GesBinaryOperator.Equal: _builder.Equal(destination, left, right); return;
                case GesBinaryOperator.NotEqual: _builder.NotEqual(destination, left, right); return;
                case GesBinaryOperator.Less: _builder.Less(destination, left, right); return;
                case GesBinaryOperator.Greater: _builder.Greater(destination, left, right); return;
                case GesBinaryOperator.LessOrEqual: _builder.LessOrEqual(destination, left, right); return;
                case GesBinaryOperator.GreaterOrEqual: _builder.GreaterOrEqual(destination, left, right); return;
                case GesBinaryOperator.Add: _builder.Add(destination, left, right); return;
                case GesBinaryOperator.Subtract: _builder.Subtract(destination, left, right); return;
                case GesBinaryOperator.Multiply: _builder.Multiply(destination, left, right); return;
                case GesBinaryOperator.Divide: _builder.Divide(destination, left, right); return;
                case GesBinaryOperator.IntegerDivide: _builder.IntegerDivide(destination, left, right); return;
                case GesBinaryOperator.Modulo: _builder.Modulo(destination, left, right); return;
                case GesBinaryOperator.Remainder: _builder.Remainder(destination, left, right); return;
                case GesBinaryOperator.Power: _builder.Power(destination, left, right); return;
                case GesBinaryOperator.Default: _builder.Default(destination, left, right); return;
                case GesBinaryOperator.Contains: _builder.Contains(destination, left, right); return;
                case GesBinaryOperator.ContainsValue: _builder.ContainsValue(destination, left, right); return;
                case GesBinaryOperator.StartsWith: _builder.StartsWith(destination, left, right); return;
                case GesBinaryOperator.EndsWith: _builder.EndsWith(destination, left, right); return;
                case GesBinaryOperator.Union: _builder.Union(destination, left, right); return;
                case GesBinaryOperator.Intersect: _builder.Intersect(destination, left, right); return;
                case GesBinaryOperator.Zip: _builder.Zip(destination, left, right); return;
                default: throw CompileFailure(GesDiagnosticCodes.CompileUnsupportedConstruct, $"GameEventScript binary compiler does not support binary operator '{operation.ToSourceText()}'.");
            }
        }

        private GesBindRef ResolveOutboundMessage(string name, IReadOnlyList<string> argumentNames)
        {
            var key = GesMessageSignature.CreateSignatureId(name, argumentNames);
            if (_outboundMessages.TryGetValue(key, out var bind)) return bind;
            bind = _builder.AddBind(GesBinaryBindKind.OutboundMessage, name, argumentNames);
            _outboundMessages[key] = bind;
            return bind;
        }

        private GesBindRef ResolveExtensionCall(GesExtensionReference reference)
        {
            if (_extensionCalls.TryGetValue(reference.SignatureId, out var bind)) return bind;
            bind = _builder.AddBind(GesBinaryBindKind.ExtensionCall, reference.ExtensionName + "." + reference.FunctionName, reference.ArgumentLabels);
            _extensionCalls[reference.SignatureId] = bind;
            return bind;
        }

        private GesBindRef ResolveRecordConstructor(GesTypeDefinitionNode type)
        {
            if (_recordConstructors.TryGetValue(type.Name, out var routine)) return routine.Bind!.Value;
            throw CompileFailure(GesDiagnosticCodes.CompileUnresolvedSymbol, $"GameEventScript record constructor ':{type.Name}' was not declared.", type.Name);
        }

        private GesBindRef ResolveExternalTypeConstructor(string typeName, IReadOnlyList<string> argumentNames)
        {
            var reference = new GesExternalTypeConstructorReference(typeName, argumentNames);
            if (module.ExternalTypeDefinitions.Resolve(reference.TypeName) is not { } typeDefinition)
            {
                throw CompileFailure(GesDiagnosticCodes.CompileUnresolvedSymbol, $"GameEventScript external type ':{reference.TypeName}' is not registered.", reference.TypeName);
            }

            if (!typeDefinition.HasConstructor(reference.ArgumentLabels))
            {
                throw CompileFailure(GesDiagnosticCodes.CompileUnresolvedSymbol, $"GameEventScript external type constructor ':{reference.SignatureId}' is not registered.", reference.SignatureId);
            }

            if (_externalTypeConstructors.TryGetValue(reference.SignatureId, out var bind)) return bind;
            bind = _builder.AddBind(GesBinaryBindKind.ExternalType, typeName, argumentNames);
            _externalTypeConstructors[reference.SignatureId] = bind;
            return bind;
        }

        private static IReadOnlyList<string> MessageShape(string name, IReadOnlyList<string> argumentNames)
        {
            var shape = new string[argumentNames.Count + 1];
            shape[0] = GesMessageSignature.NormalizeMessageName(name);
            for (var index = 0; index < argumentNames.Count; index++) shape[index + 1] = argumentNames[index];
            return shape;
        }

        private static GesMessageSignature? ClassifyHandlerSignature(GesExpressionNode expression, LoweringContext context)
        {
            switch (expression)
            {
                case GesHandlerLiteralExpressionNode handler: return GesMessageSignature.Create(handler.Message, handler.SignatureLabels);
                case GesIdentifierExpressionNode identifier: return context.ResolveHandlerSignature(identifier.Name);
                default: return null;
            }
        }

        private static bool CallArgumentsMatchHandlerSignature(IReadOnlyList<GesArgumentNode> arguments, GesMessageSignature signature)
        {
            if (arguments.Count != signature.Parameters.Count) return false;
            for (var index = 0; index < arguments.Count; index++)
            {
                var label = arguments[index].Label;
                if (label is null) continue;
                if (!string.Equals(
                        GesMessageSignature.NormalizeParameterName(label),
                        signature.Parameters[index],
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static long? ReadUnitlessIntegerLiteralSeed(GesExpressionNode expression)
        {
            if (expression is GesIntegerLiteralExpressionNode integer) return integer.Value;

            if (expression is GesUnaryExpressionNode { Operator: GesUnaryOperator.Negate, Operand: GesIntegerLiteralExpressionNode negated } &&
                negated.Value != long.MinValue)
            {
                return -negated.Value;
            }

            return null;
        }

        private static (short From, short To, short Step)? GetRangeIteratorShort(GesRangeExpressionNode range)
        {
            if (GetShortIntegerLiteral(range.FromExpression) is { } from &&
                GetShortIntegerLiteral(range.ToExpression) is { } to)
            {
                if (range.StepExpression is null) return (from, to, 1);
                if (GetShortIntegerLiteral(range.StepExpression) is { } step) return (from, to, step);
            }

            return null;
        }

        private static short? GetShortIntegerLiteral(GesExpressionNode expression)
        {
            if (expression is GesIntegerLiteralExpressionNode { Value: >= short.MinValue and <= short.MaxValue } integer) return (short)integer.Value;

            return null;
        }

        private static (int StartComponent, IReadOnlyList<GesArgumentNode> Arguments)? GetSpatialConstructorStageShape(IReadOnlyList<GesArgumentNode> sourceArguments)
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

        private short ToShort(int value, string name)
            => value is < short.MinValue or > short.MaxValue
                ? throw CompileFailure(GesDiagnosticCodes.CompileNumericLimitExceeded, $"GameEventScript binary compiler {name} must fit into Int16.", name)
                : (short)value;

        private static GesQuantityUnit ResolveUnitOrNone(string unitName) => GesQuantityUnits.ParseTypeName(unitName) ?? GesQuantityUnit.UnitNone;

        private static bool IsBuiltInCastType(string typeName)
            => typeName is "number" or "numeric" or "numeric:integer" or "numeric:fractional" ||
               GesQuantityUnits.IsQuantityTypeName(typeName) ||
               GetBytecodeTypeKind(typeName) is not null;

        private static GesQuantityUnit? GetQuantityUnit(string typeName) => GesQuantityUnits.ParseQuantityTypeName(typeName);

        private static GesBytecodeTypeKind? GetBytecodeTypeKind(string typeName)
        {
            var typeKind = typeName switch
            {
                "nothing" => GesBytecodeTypeKind.Nothing,
                "boolean" => GesBytecodeTypeKind.Boolean,
                "percentage" => GesBytecodeTypeKind.Percentage,
                "vector" => GesBytecodeTypeKind.Vector,
                "point" => GesBytecodeTypeKind.Point,
                "series" => GesBytecodeTypeKind.Series,
                "tag" => GesBytecodeTypeKind.Tag,
                "text" => GesBytecodeTypeKind.Text,
                "list" => GesBytecodeTypeKind.List,
                "range" => GesBytecodeTypeKind.Range,
                "message" => GesBytecodeTypeKind.Message,
                "handler" => GesBytecodeTypeKind.Handler,
                "map" => GesBytecodeTypeKind.Map,
                "dice" => GesBytecodeTypeKind.Dice,
                _ => (GesBytecodeTypeKind)0
            };

            return typeName == "nothing" || typeKind != 0 ? typeKind : null;
        }

    }

    private sealed class LoweringContext
    {
        private readonly Dictionary<string, GesRegisterRef> _registers;
        private readonly Dictionary<string, GesMessageSignature> _handlerSignatures;
        private readonly HashSet<string> _handlerSignatureShadows;
        private readonly LoweringContext? _parent;
        private readonly GesBinaryBuilder.GesBinaryRoutineScope _routine;

        private LoweringContext(GesBinaryBuilder.GesBinaryRoutineScope routine, LoweringContext? parent = null)
        {
            _routine = routine;
            _parent = parent;
            _registers = new Dictionary<string, GesRegisterRef>(StringComparer.Ordinal);
            _handlerSignatures = new Dictionary<string, GesMessageSignature>(StringComparer.Ordinal);
            _handlerSignatureShadows = new HashSet<string>(StringComparer.Ordinal);
            RegisterCount = parent?.RegisterCount ?? routine.Arguments.Count;
        }

        public int RegisterCount { get; private set; }

        public static LoweringContext ForRoutine(GesBinaryBuilder.GesBinaryRoutineScope routine) => new(routine);

        public static LoweringContext ForRoutineWithParent(GesBinaryBuilder.GesBinaryRoutineScope routine, LoweringContext parent) => new(routine, parent);

        public LoweringContext CreateChild() => new(_routine, this);

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

        public bool Contains(string name) => _registers.ContainsKey(name) || _parent?.Contains(name) == true;

        // An unknown name fails at the referencing identifier when its range is given, otherwise at the construct being lowered.
        public GesRegisterRef Require(string name, GesSourceLocation? location = null)
        {
            if (_registers.TryGetValue(name, out var register)) return register;
            if (_parent is not null) return _parent.Require(name, location);
            throw new GesCompileException(new GesDiagnostic(GesDiagnosticPhase.Compile, GesDiagnosticCodes.CompileUnresolvedSymbol, $"GameEventScript binary compiler could not resolve identifier '{name}'.", name,
                SourceLocation: location ?? _routine.Builder.CurrentSourceRange));
        }

        public void DeclareHandlerSignature(string name, GesMessageSignature signature)
        {
            _handlerSignatures[name] = signature;
            _handlerSignatureShadows.Remove(name);
        }

        public void ClearHandlerSignature(string name)
        {
            _handlerSignatures.Remove(name);
            _handlerSignatureShadows.Add(name);
        }

        public GesMessageSignature? ResolveHandlerSignature(string name)
        {
            if (_handlerSignatures.TryGetValue(name, out var signature)) return signature;
            if (_handlerSignatureShadows.Contains(name)) return null;

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

    private readonly record struct StageArgumentPlan(StageArgumentKind Kind, GesExpressionNode? Expression, GesRegisterRef? Register)
    {
        public static StageArgumentPlan Nothing { get; } = new(StageArgumentKind.Nothing, null, null);

        public static StageArgumentPlan FromExpression(GesExpressionNode expression) => new(StageArgumentKind.Expression, expression, null);

        public static StageArgumentPlan FromRegister(GesRegisterRef register) => new(StageArgumentKind.Register, null, register);
    }
}
