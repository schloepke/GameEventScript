#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using StepH.Flow.EventScript.Interpreter;
using StepH.Flow.EventScript.Linker;
using StepH.Flow.EventScript.Parser;
using StepH.Flow.EventScript.Types;

namespace StepH.Flow.EventScript.Experimental;

public static class ExperimentalEventScriptCompiler
{
    public static ExperimentalCompiledEventScript Compile(LinkedEventScriptModule module, ExperimentalEventScriptCompilationOptions? options = null)
    {
        var compileOptions = options ?? new ExperimentalEventScriptCompilationOptions();
        var errors = new List<EventScriptOpcodeCompilationError>();

        if (module is null)
        {
            errors.Add(new EventScriptOpcodeCompilationError(
                "LinkedEventScriptModule must not be null",
                "UnknownModule",
                "module",
                EventScriptSymbolKind.GlobalDefinition,
                EventScriptOpcodeCompilationErrorKind.InvalidInput,
                new EventScriptSourceLocation("UnknownSource", ModuleName: "UnknownModule")));
            throw new EventScriptOpcodeCompilationException(errors);
        }

        try
        {
            var builder = new CompilerBuilder(module, compileOptions, errors);
            var compiled = builder.Build();
            if (errors.Count > 0)
            {
                throw new EventScriptOpcodeCompilationException(errors);
            }

            return compiled;
        }
        catch (EventScriptOpcodeCompilationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            errors.Add(new EventScriptOpcodeCompilationError(
                $"Internal compiler failure: {ex.Message}",
                "UnknownModule",
                "compiler",
                EventScriptSymbolKind.GlobalDefinition,
                EventScriptOpcodeCompilationErrorKind.InternalCompilerError,
                new EventScriptSourceLocation("UnknownSource", ModuleName: "UnknownModule")));
            throw new EventScriptOpcodeCompilationException(errors);
        }
    }

    private sealed class CompilerBuilder(
        LinkedEventScriptModule module,
        ExperimentalEventScriptCompilationOptions options,
        List<EventScriptOpcodeCompilationError> errors)
    {
        private readonly Dictionary<string, int> _stringIndex = new(StringComparer.Ordinal);
        private readonly List<string> _stringPool = [];
        private readonly List<EventScriptValue> _constantPool = [];
        private readonly List<ExperimentalCompiledExpression> _expressionPool = [];
        private readonly List<ExperimentalCompiledNamedArgumentList> _namedArgumentLists = [];
        private readonly List<ExperimentalCompiledIterationSource> _iterationSources = [];
        private readonly List<ExperimentalEventScript> _programs = [];

        private readonly Dictionary<string, ExperimentalCompiledCallableDefinition> _callables = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ExperimentalCompiledTypeDefinition> _types = new(StringComparer.Ordinal);
        private readonly Dictionary<string, IReadOnlyList<ExperimentalCompiledEventScriptHandler>> _handlers = new(StringComparer.Ordinal);

        public ExperimentalCompiledEventScript Build()
        {
            CompileTypes();
            CompileGlobalDefinitions();
            CompileHandlers();

            return new ExperimentalCompiledEventScript(
                options,
                _stringPool.ToArray(),
                _constantPool.ToArray(),
                _expressionPool.ToArray(),
                _namedArgumentLists.ToArray(),
                _iterationSources.ToArray(),
                _programs.ToArray(),
                _handlers,
                _callables,
                _types);
        }

        private void CompileTypes()
        {
            foreach (var pair in module.TypeDefinitions)
            {
                var fields = pair.Value.Fields.Select(field => new ExperimentalCompiledTypeFieldDefinition(
                    field.Name,
                    field.TypeName,
                    field.MinimumExpression is null ? null : CompileExpression(field.MinimumExpression),
                    field.MaximumExpression is null ? null : CompileExpression(field.MaximumExpression),
                    field.ComputedExpression is null ? null : CompileExpression(field.ComputedExpression))).ToArray();

                _types[pair.Key] = new ExperimentalCompiledTypeDefinition(pair.Key, fields);
            }
        }

        private void CompileGlobalDefinitions()
        {
            foreach (var pair in module.Callables)
            {
                var name = pair.Key;
                var callable = pair.Value;
                var symbolKind = callable.Kind == LinkedCallableKind.Rule
                    ? EventScriptSymbolKind.Rule
                    : EventScriptSymbolKind.Select;
                var kind = callable.Kind == LinkedCallableKind.Rule
                    ? ExperimentalCallableKind.Rule
                    : ExperimentalCallableKind.Select;

                ValidateDuplicateParameters(name, callable.Parameters, symbolKind, callable.SourceRange);
                _callables[name] = new ExperimentalCompiledCallableDefinition(
                    name,
                    callable.Parameters.ToArray(),
                    CompileExpression(callable.Expression),
                    kind,
                    options.EnableDiagnostics);
            }
        }

        private void CompileHandlers()
        {
            foreach (var pair in module.Handlers)
            {
                var compiledHandlers = new List<ExperimentalCompiledEventScriptHandler>(pair.Value.Count);
                for (var declarationOrder = 0; declarationOrder < pair.Value.Count; declarationOrder++)
                {
                    var handler = pair.Value[declarationOrder];
                    ValidateDuplicateParameters(handler.Message, handler.Parameters, EventScriptSymbolKind.Handler, handler.SourceRange);

                    var rootScope = ScopeFrame.CreateRoot(handler.Parameters);
                    var programIndex = CompileStatements(handler.Message, handler.Statements, rootScope);
                    var signatureId = EventScriptMessageSignature.CreateSignatureId(handler.Message, handler.Parameters);
                    compiledHandlers.Add(new ExperimentalCompiledEventScriptHandler(
                        handler.Message,
                        handler.Parameters.ToArray(),
                        signatureId,
                        declarationOrder,
                        programIndex,
                        options.EnableDiagnostics));
                }

                _handlers[pair.Key] = compiledHandlers.ToArray();
            }
        }

        private int CompileStatements(string symbolName, IReadOnlyList<StatementNode> statements, ScopeFrame scope, bool createsScope = false)
        {
            var instructions = new List<ExperimentalInstruction>(statements.Count);
            foreach (var statement in statements)
            {
                CompileStatement(symbolName, statement, scope, instructions);
            }

            var programIndex = _programs.Count;
            _programs.Add(new ExperimentalEventScript(instructions.ToArray(), createsScope));
            return programIndex;
        }

        private void CompileStatement(
            string symbolName,
            StatementNode statement,
            ScopeFrame scope,
            List<ExperimentalInstruction> instructions)
        {
            switch (statement)
            {
                case PublishStatementNode publish:
                {
                    ValidateDuplicatePublishArguments(symbolName, publish.MessageExpression);
                    var messageExpression = CompileExpression(publish.MessageExpression);
                    instructions.Add(new ExperimentalInstruction(
                        ExperimentalOpCode.Publish,
                        AddExpression(messageExpression)));
                    return;
                }

                case LetStatementNode let:
                {
                    if (!scope.TryDeclare(let.Identifier))
                    {
                        errors.Add(BuildError(
                            $"Variable '{let.Identifier}' is already defined in this scope",
                            symbolName,
                            EventScriptSymbolKind.Variable,
                            EventScriptOpcodeCompilationErrorKind.DuplicateVariable,
                            let.SourceRange));
                    }

                    var expression = CompileExpression(let.Expression);
                    var declaredTypeIndex = string.IsNullOrEmpty(let.DeclaredType) ? -1 : AddString(let.DeclaredType!);
                    if (declaredTypeIndex >= 0 &&
                        expression.IsConstant &&
                        expression.ConstantValue is not null &&
                        TryConvertConstantType(expression.ConstantValue, let.DeclaredType!, out var convertedConstant))
                    {
                        _constantPool.Add(convertedConstant);
                        expression = new ExperimentalCompiledExpression(let.Expression, convertedConstant);
                        declaredTypeIndex = -1;
                    }

                    instructions.Add(new ExperimentalInstruction(
                        ExperimentalOpCode.Let,
                        AddString(let.Identifier),
                        declaredTypeIndex,
                        AddExpression(expression)));
                    return;
                }

                case IfStatementNode ifStatement:
                {
                    var conditionExpression = CompileExpression(ifStatement.Condition);
                    var thenProgramIndex = CompileStatementBody(symbolName, ifStatement.ThenBody, scope);
                    var elseProgramIndex = ifStatement.ElseBody is null
                        ? -1
                        : CompileStatementBody(symbolName, ifStatement.ElseBody, scope);
                    instructions.Add(new ExperimentalInstruction(
                        ExperimentalOpCode.If,
                        AddExpression(conditionExpression),
                        thenProgramIndex,
                        elseProgramIndex));
                    return;
                }

                case ForStatementNode forStatement:
                {
                    var loopScope = scope.CreateChild();
                    loopScope.TryDeclare(forStatement.Identifier);
                    var bodyProgramIndex = CompileStatementBody(symbolName, forStatement.Body, loopScope);
                    var sourceIndex = CompileIterationSource(forStatement.Source);
                    instructions.Add(new ExperimentalInstruction(
                        ExperimentalOpCode.ForEach,
                        AddString(forStatement.Identifier),
                        sourceIndex,
                        bodyProgramIndex));
                    return;
                }

                case SeededRandomStatementNode seededRandom:
                {
                    var seedExpression = CompileExpression(seededRandom.SeedExpression);
                    var bodyProgramIndex = CompileStatementBody(symbolName, seededRandom.Body, scope);
                    instructions.Add(new ExperimentalInstruction(
                        ExperimentalOpCode.SeededRandom,
                        AddExpression(seedExpression),
                        bodyProgramIndex));
                    return;
                }

                case ExpressionStatementNode expressionStatement:
                {
                    var expression = CompileExpression(expressionStatement.Expression);
                    instructions.Add(new ExperimentalInstruction(
                        ExperimentalOpCode.EvaluateExpression,
                        AddExpression(expression)));
                    return;
                }

                default:
                    errors.Add(BuildError(
                        $"Unsupported statement syntax: {statement.GetType().Name}",
                        symbolName,
                        EventScriptSymbolKind.Handler,
                        EventScriptOpcodeCompilationErrorKind.UnsupportedSyntax,
                        statement.SourceRange));
                    return;
            }
        }

        private int CompileStatementBody(string symbolName, StatementBodyNode body, ScopeFrame parentScope)
        {
            var scope = body.IsBlock ? parentScope.CreateChild() : parentScope;
            return CompileStatements(symbolName, body.Statements, scope, body.IsBlock);
        }

        private void CompileBodyIntoInstructions(
            string symbolName,
            StatementBodyNode body,
            ScopeFrame parentScope,
            List<ExperimentalInstruction> instructions)
        {
            var scope = body.IsBlock ? parentScope.CreateChild() : parentScope;
            foreach (var nested in body.Statements)
            {
                CompileStatement(symbolName, nested, scope, instructions);
            }
        }

        private int CompileIterationSource(IterationSourceNode source)
        {
            ExperimentalCompiledIterationSource compiledSource = source switch
            {
                CollectionIterationSourceNode collection => new ExperimentalCompiledIterationSource(
                    ExperimentalIterationSourceKind.Collection,
                    CompileExpression(collection.Expression),
                    null,
                    null,
                    null),
                RangeIterationSourceNode range => new ExperimentalCompiledIterationSource(
                    ExperimentalIterationSourceKind.Range,
                    null,
                    CompileExpression(range.RangeExpression.FromExpression),
                    CompileExpression(range.RangeExpression.ToExpression),
                    range.RangeExpression.StepExpression is null ? null : CompileExpression(range.RangeExpression.StepExpression)),
                _ => new ExperimentalCompiledIterationSource(ExperimentalIterationSourceKind.Collection, null, null, null, null)
            };

            var index = _iterationSources.Count;
            _iterationSources.Add(compiledSource);
            return index;
        }

        private ExperimentalCompiledExpression CompileExpression(ExpressionNode expression)
        {
            if (TryFoldConstant(expression, out var constantValue))
            {
                _constantPool.Add(constantValue);
                return new ExperimentalCompiledExpression(expression, constantValue);
            }

            return new ExperimentalCompiledExpression(expression);
        }

        private bool TryFoldConstant(ExpressionNode expression, out EventScriptValue value)
        {
            switch (expression)
            {
                case IntegerLiteralExpressionNode integer:
                    value = EventScriptValueFactory.Integer(integer.Value);
                    return true;
                case DecimalLiteralExpressionNode number:
                    value = EventScriptValueFactory.Decimal(number.Value);
                    return true;
                case PercentageLiteralExpressionNode percentage:
                    value = EventScriptValueFactory.Percentage(percentage.PercentValue / 100m);
                    return true;
                case UnitDecimalLiteralExpressionNode unitDecimal:
                    value = EventScriptDecimalUnits.TryParseTypeName(unitDecimal.UnitName, out var unit)
                        ? EventScriptValueFactory.Decimal(unitDecimal.Value, unit)
                        : EventScriptValueFactory.DecimalNaN();
                    return true;
                case TextLiteralExpressionNode text:
                    value = EventScriptValueFactory.Text(text.Value);
                    return true;
                case TagLiteralExpressionNode tag:
                    value = EventScriptValueFactory.Tag(tag.Name);
                    return true;
                case BooleanLiteralExpressionNode boolean:
                    value = EventScriptValueFactory.Boolean(boolean.Value);
                    return true;
                case ListLiteralExpressionNode list:
                {
                    var items = new List<EventScriptValue>(list.Items.Count);
                    foreach (var item in list.Items)
                    {
                        if (!TryFoldConstant(item, out var constantItem))
                        {
                            value = EventScriptValue.Nothing;
                            return false;
                        }

                        items.Add(constantItem);
                    }

                    value = EventScriptValueFactory.List(items);
                    return true;
                }
                case SetLiteralExpressionNode set:
                {
                    var items = new List<EventScriptValue>(set.Items.Count);
                    foreach (var item in set.Items)
                    {
                        if (!TryFoldConstant(item, out var constantItem))
                        {
                            value = EventScriptValue.Nothing;
                            return false;
                        }

                        items.Add(constantItem);
                    }

                    value = EventScriptValueFactory.Set(items);
                    return true;
                }
                case DictionaryLiteralExpressionNode dictionary:
                {
                    var map = new Dictionary<string, EventScriptValue>(StringComparer.Ordinal);
                    foreach (var entry in dictionary.Entries)
                    {
                        if (!TryFoldConstant(entry.Value, out var constantItem))
                        {
                            value = EventScriptValue.Nothing;
                            return false;
                        }

                        map[entry.Key] = constantItem;
                    }

                    value = EventScriptValueFactory.Dictionary(map);
                    return true;
                }
                case TypeCastExpressionNode typeCast:
                {
                    if (!TryFoldConstant(typeCast.Value, out var sourceValue))
                    {
                        value = EventScriptValue.Nothing;
                        return false;
                    }

                    if (!TryConvertConstantType(sourceValue, typeCast.TypeName, out value))
                    {
                        return false;
                    }

                    return true;
                }
                default:
                    value = EventScriptValue.Nothing;
                    return false;
            }
        }

        private bool TryConvertConstantType(EventScriptValue value, string declaredType, out EventScriptValue converted)
        {
            switch (declaredType)
            {
                case "nothing":
                    converted = EventScriptValue.Nothing;
                    return true;
                case "tag":
                    converted = EventScriptValueFactory.Tag(value.AsText());
                    return true;
                case "text":
                    converted = EventScriptValueFactory.Text(value.AsText());
                    return true;
                case "percentage":
                    converted = ConvertConstantToPercentage(value);
                    return true;
                case "degree":
                    converted = ConvertConstantToDecimalUnit(value, EventScriptDecimalUnit.Degree);
                    return true;
                case "meter":
                    converted = ConvertConstantToDecimalUnit(value, EventScriptDecimalUnit.Meter);
                    return true;
                case "second":
                    converted = ConvertConstantToDecimalUnit(value, EventScriptDecimalUnit.Second);
                    return true;
                case "vector2":
                    converted = ConvertConstantToVector2(value);
                    return true;
                case "vector3":
                    converted = ConvertConstantToVector3(value);
                    return true;
                case "boolean":
                    converted = EventScriptValueFactory.Boolean(value.AsBoolean());
                    return true;
                case "integer":
                    converted = EventScriptValueFactory.Integer(value.AsInteger());
                    return true;
                case "decimal":
                    converted = ConvertConstantToDecimal(value);
                    return true;
                case "list":
                    converted = EventScriptValueFactory.List(value.AsList());
                    return true;
                case "range":
                    converted = value.IsRange() ? value : EventScriptValue.Nothing;
                    return true;
                case "message":
                    converted = value.Kind == EventScriptValueKind.Message
                        ? value
                        : EventScriptMessageValueCodec.TryReadMessageValue(value, out var messageValue)
                            ? EventScriptMessageValueCodec.CreateMessageValue(messageValue)
                            : EventScriptValue.Nothing;
                    return true;
                case "handler":
                    converted = value.Kind == EventScriptValueKind.Handler
                        ? value
                        : EventScriptMessageValueCodec.TryReadHandlerValue(value, out var handlerValue)
                            ? EventScriptMessageValueCodec.CreateHandlerValue(handlerValue)
                            : EventScriptValue.Nothing;
                    return true;
                case "dictionary":
                    converted = EventScriptValueFactory.Dictionary(value.AsDictionary());
                    return true;
                case "set":
                    converted = EventScriptValueFactory.Set(value.AsSet());
                    return true;
                case "dice":
                    converted = EventScriptValueFactory.Dice(value.AsDice());
                    return true;
                case "optional":
                    converted = value.IsOptional()
                        ? value
                        : value.IsNothing() ? EventScriptValueFactory.OptionalNone() : EventScriptValueFactory.OptionalSome(value);
                    return true;
                default:
                    if (_types.ContainsKey(declaredType))
                    {
                        converted = EventScriptValue.Nothing;
                        return false;
                    }

                    converted = value;
                    return true;
            }
        }

        private static EventScriptValue ConvertConstantToDecimal(EventScriptValue value)
        {
            if (!TryUnwrapOptionalForConstant(value, out var unwrapped))
            {
                return EventScriptValueFactory.DecimalNaN();
            }

            if (!TryCoerceNumericForConstant(unwrapped, out var number, out var isFinite))
            {
                return EventScriptValueFactory.DecimalNaN();
            }

            if (isFinite)
            {
                return EventScriptValueFactory.Decimal(number);
            }

            if (unwrapped.IsNaN())
            {
                return EventScriptValueFactory.DecimalNaN();
            }

            return unwrapped.IsNegativeInfinity()
                ? EventScriptValueFactory.DecimalNegativeInfinity()
                : EventScriptValueFactory.DecimalInfinity();
        }

        private static EventScriptValue ConvertConstantToPercentage(EventScriptValue value)
        {
            if (!TryUnwrapOptionalForConstant(value, out var unwrapped))
            {
                return EventScriptValueFactory.DecimalNaN();
            }

            if (unwrapped.IsPercentage())
            {
                return unwrapped;
            }

            if (unwrapped.HasDecimalUnit())
            {
                return EventScriptValueFactory.DecimalNaN();
            }

            if (!TryCoerceNumericForConstant(unwrapped, out var number, out var isFinite) || !isFinite)
            {
                return EventScriptValueFactory.DecimalNaN();
            }

            var ratio = unwrapped.Kind == EventScriptValueKind.Integer
                ? number / 100m
                : number > 1m || number < -1m
                    ? number / 100m
                    : number;
            return EventScriptValueFactory.Percentage(ratio);
        }

        private static EventScriptValue ConvertConstantToDecimalUnit(EventScriptValue value, EventScriptDecimalUnit unit)
        {
            if (!TryUnwrapOptionalForConstant(value, out var unwrapped))
            {
                return EventScriptValueFactory.DecimalNaN();
            }

            if (unwrapped.Kind is EventScriptValueKind.Decimal or EventScriptValueKind.Integer &&
                TryCoerceNumericForConstant(unwrapped, out var number, out var isFinite) &&
                isFinite)
            {
                return EventScriptValueFactory.Decimal(number, unit);
            }

            return EventScriptValueFactory.DecimalNaN();
        }

        private static EventScriptValue ConvertConstantToVector2(EventScriptValue value)
        {
            if (!TryUnwrapOptionalForConstant(value, out var unwrapped))
            {
                return EventScriptValue.Nothing;
            }

            if (unwrapped is EventScriptVector2Value vector2)
            {
                return vector2;
            }

            if (unwrapped is EventScriptVector3Value vector3)
            {
                return EventScriptValueFactory.Vector2(vector3.X, vector3.Y);
            }

            if (TryReadVectorComponent(unwrapped, "x", out var x) &&
                TryReadVectorComponent(unwrapped, "y", out var y))
            {
                return EventScriptValueFactory.Vector2(x, y);
            }

            var items = unwrapped.AsList();
            if (items.Count >= 2 &&
                TryReadVectorComponent(items[0], out x) &&
                TryReadVectorComponent(items[1], out y))
            {
                return EventScriptValueFactory.Vector2(x, y);
            }

            return EventScriptValue.Nothing;
        }

        private static EventScriptValue ConvertConstantToVector3(EventScriptValue value)
        {
            if (!TryUnwrapOptionalForConstant(value, out var unwrapped))
            {
                return EventScriptValue.Nothing;
            }

            if (unwrapped is EventScriptVector3Value vector3)
            {
                return vector3;
            }

            if (unwrapped is EventScriptVector2Value vector2)
            {
                return EventScriptValueFactory.Vector3(vector2.X, vector2.Y, 0m);
            }

            if (TryReadVectorComponent(unwrapped, "x", out var x) &&
                TryReadVectorComponent(unwrapped, "y", out var y))
            {
                var z = TryReadVectorComponent(unwrapped, "z", out var zValue) ? zValue : 0m;
                return EventScriptValueFactory.Vector3(x, y, z);
            }

            var items = unwrapped.AsList();
            if (items.Count >= 2 &&
                TryReadVectorComponent(items[0], out x) &&
                TryReadVectorComponent(items[1], out y))
            {
                var z = items.Count >= 3 && TryReadVectorComponent(items[2], out var zValue) ? zValue : 0m;
                return EventScriptValueFactory.Vector3(x, y, z);
            }

            return EventScriptValue.Nothing;
        }

        private static bool TryReadVectorComponent(EventScriptValue source, string key, out decimal value)
        {
            if (source.TryGetDictionaryMember(key, out var component) &&
                TryReadVectorComponent(component, out value))
            {
                return true;
            }

            value = default;
            return false;
        }

        private static bool TryReadVectorComponent(EventScriptValue component, out decimal value)
        {
            if (!TryUnwrapOptionalForConstant(component, out var unwrapped) ||
                !TryCoerceNumericForConstant(unwrapped, out var number, out var isFinite) ||
                !isFinite)
            {
                value = default;
                return false;
            }

            value = number;
            return true;
        }

        private static bool TryUnwrapOptionalForConstant(EventScriptValue value, out EventScriptValue unwrapped)
        {
            if (!value.IsOptional())
            {
                unwrapped = value;
                return true;
            }

            var optional = value.AsOptional();
            if (!optional.HasValue)
            {
                unwrapped = default!;
                return false;
            }

            unwrapped = optional.Value;
            return true;
        }

        private static bool TryCoerceNumericForConstant(EventScriptValue value, out decimal number, out bool isFinite)
        {
            number = default;
            isFinite = false;

            if (value.IsNothing())
            {
                return false;
            }

            if (value.Kind == EventScriptValueKind.Decimal)
            {
                if (value.IsNaN() || value.IsInfinity())
                {
                    return true;
                }

                number = value.AsNumber();
                isFinite = true;
                return true;
            }

            if (value.Kind == EventScriptValueKind.Integer)
            {
                number = value.AsInteger();
                isFinite = true;
                return true;
            }

            if (value.Kind == EventScriptValueKind.Percentage)
            {
                number = value.AsNumber();
                isFinite = true;
                return true;
            }

            if (value.Kind == EventScriptValueKind.Dice)
            {
                number = value.AsDice().Sum();
                isFinite = true;
                return true;
            }

            if (value.IsText() &&
                decimal.TryParse(value.AsText(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
            {
                number = parsed;
                isFinite = true;
                return true;
            }

            if (value.Kind == EventScriptValueKind.Boolean)
            {
                number = value.AsBoolean() ? 1m : 0m;
                isFinite = true;
                return true;
            }

            return false;
        }

        private bool TryGetBooleanConstant(ExperimentalCompiledExpression expression, out bool value)
        {
            if (!expression.IsConstant || expression.ConstantValue is null)
            {
                value = false;
                return false;
            }

            value = expression.ConstantValue.Kind == EventScriptValueKind.Boolean && expression.ConstantValue.AsBoolean();
            return expression.ConstantValue.Kind == EventScriptValueKind.Boolean;
        }

        private void ValidateDuplicateParameters(
            string symbol,
            IReadOnlyList<string> parameters,
            EventScriptSymbolKind symbolKind,
            EventScriptSourceLocation? sourceLocation)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var parameter in parameters)
            {
                if (seen.Add(parameter))
                {
                    continue;
                }

                var kind = symbolKind == EventScriptSymbolKind.Handler
                    ? EventScriptOpcodeCompilationErrorKind.DuplicateHandlerParameter
                    : EventScriptOpcodeCompilationErrorKind.DuplicateDefinitionParameter;
                errors.Add(BuildError(
                    $"{symbolKind} '{symbol}' declares parameter '{parameter}' more than once",
                    symbol,
                    symbolKind,
                    kind,
                    sourceLocation));
            }
        }

        private void ValidateDuplicatePublishArguments(string symbolName, ExpressionNode publishExpression)
        {
            var arguments = publishExpression switch
            {
                MessageLiteralExpressionNode messageLiteral => messageLiteral.Arguments,
                HandlerBindExpressionNode handlerBind => handlerBind.Arguments,
                _ => null
            };
            if (arguments is null)
            {
                return;
            }

            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var argument in arguments)
            {
                if (names.Add(argument.Name))
                {
                    continue;
                }

                errors.Add(BuildError(
                    $"Publish expression in '{symbolName}' declares argument '{argument.Name}' more than once",
                    symbolName,
                    EventScriptSymbolKind.Handler,
                    EventScriptOpcodeCompilationErrorKind.DuplicatePublishArgument,
                    argument.SourceRange ?? publishExpression.SourceRange));
            }
        }

        private EventScriptOpcodeCompilationError BuildError(
            string message,
            string symbol,
            EventScriptSymbolKind symbolKind,
            EventScriptOpcodeCompilationErrorKind kind,
            EventScriptSourceLocation? sourceLocation = null)
        {
            var resolvedLocation = sourceLocation ?? new EventScriptSourceLocation("UnknownSource", ModuleName: "UnknownModule");
            return new EventScriptOpcodeCompilationError(
                message,
                resolvedLocation.ModuleName,
                symbol,
                symbolKind,
                kind,
                resolvedLocation);
        }

        private int AddString(string value)
        {
            if (_stringIndex.TryGetValue(value, out var existing))
            {
                return existing;
            }

            var index = _stringPool.Count;
            _stringPool.Add(value);
            _stringIndex[value] = index;
            return index;
        }

        private int AddExpression(ExperimentalCompiledExpression expression)
        {
            var index = _expressionPool.Count;
            _expressionPool.Add(expression);
            return index;
        }
    }

    private sealed class ScopeFrame
    {
        private readonly ScopeFrame? _parent;
        private readonly HashSet<string> _names = new(StringComparer.Ordinal);

        private ScopeFrame(ScopeFrame? parent)
        {
            _parent = parent;
        }

        public static ScopeFrame CreateRoot(IReadOnlyList<string> predeclaredNames)
        {
            var frame = new ScopeFrame(parent: null);
            foreach (var name in predeclaredNames)
            {
                frame._names.Add(name);
            }

            return frame;
        }

        public ScopeFrame CreateChild() => new(this);

        public bool TryDeclare(string name) => _names.Add(name);

        public bool IsDeclaredInThisScope(string name) => _names.Contains(name);

        public bool IsDeclaredInAnyScope(string name)
            => IsDeclaredInThisScope(name) || (_parent?.IsDeclaredInAnyScope(name) ?? false);
    }
}
