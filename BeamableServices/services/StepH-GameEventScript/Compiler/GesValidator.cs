using System;
using System.Collections.Generic;
using System.Linq;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Types;

namespace StepH.GameEventScript.Compiler;

internal static class GesValidator
{
    private sealed class ValidationScope(IEnumerable<string>? names = null)
    {
        private readonly HashSet<string> _variables = names is null ? new HashSet<string>(StringComparer.Ordinal) : new HashSet<string>(names, StringComparer.Ordinal);
        private readonly Dictionary<string, string> _declaredTypes = new(StringComparer.Ordinal);

        public static ValidationScope Create(IEnumerable<ParameterNode> parameters)
        {
            var scope = new ValidationScope(parameters.Select(parameter => parameter.LocalName));
            foreach (var parameter in parameters)
            {
                scope.DeclareType(parameter.LocalName, parameter.DeclaredType);
            }

            return scope;
        }

        public static ValidationScope CreateChild(ValidationScope parent)
        {
            var scope = new ValidationScope();
            foreach (var pair in parent._declaredTypes)
            {
                scope._declaredTypes[pair.Key] = pair.Value;
            }

            return scope;
        }

        public IReadOnlyDictionary<string, string> DeclaredTypes => _declaredTypes;

        public bool ContainsInCurrentScope(string name) => _variables.Contains(name);

        public void Declare(string name, string? declaredType = null)
        {
            _variables.Add(name);
            DeclareType(name, declaredType);
        }

        private void DeclareType(string name, string? declaredType)
        {
            if (!string.IsNullOrWhiteSpace(declaredType))
            {
                _declaredTypes[name] = declaredType!;
            }
        }
    }

    internal static void ValidateModule(ParsedScript parsedScript, IReadOnlyDictionary<string, GesCallableDefinition> callables,
        IReadOnlyDictionary<string, TypeDefinitionNode> typeDefinitions, GameEventScriptCompileOptions options, GesValidationErrors errors)
    {
        _ = options;
        foreach (var typeDefinition in parsedScript.TypeDefinitions)
        {
            foreach (var field in typeDefinition.Fields)
            {
                ValidateIdentifierCase(
                    parsedScript,
                    field.Name,
                    field.Name,
                    GameEventScriptSymbolKind.Variable,
                    "Field names must use identifier casing (start lowercase, letters only, optional final _index suffix)",
                    errors);

                if (field.MinimumExpression is not null)
                {
                    ValidateExpressionReferences(parsedScript, field.MinimumExpression, callables, typeDefinitions, errors);
                }

                if (field.MaximumExpression is not null)
                {
                    ValidateExpressionReferences(parsedScript, field.MaximumExpression, callables, typeDefinitions, errors);
                }

                if (field.ComputedExpression is not null)
                {
                    ValidateExpressionReferences(parsedScript, field.ComputedExpression, callables, typeDefinitions, errors);
                }
            }
        }

        foreach (var predicateDefinition in parsedScript.PredicateDefinitions)
        {
            ValidateIdentifierCase(
                    parsedScript,
                    predicateDefinition.Name,
                    predicateDefinition.Name,
                    GameEventScriptSymbolKind.Predicate,
                    "Predicate names must use identifier casing (start lowercase, letters only, optional final _index suffix)",
                    errors);

            foreach (var parameter in predicateDefinition.Parameters)
            {
                ValidateIdentifierCase(
                    parsedScript,
                    parameter,
                    predicateDefinition.Name,
                    GameEventScriptSymbolKind.Predicate,
                    $"Predicate '{predicateDefinition.Name}' declares an invalid parameter name '{parameter}'",
                    errors);
            }

            ValidateParameterTypeHints(parsedScript, "Predicate", predicateDefinition.Name, predicateDefinition.ParameterList, typeDefinitions, errors);

            var duplicateParameters = predicateDefinition.Parameters
                .GroupBy(parameter => parameter, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key);

            foreach (var duplicateParameter in duplicateParameters)
            {
                var duplicateParameterNode = predicateDefinition.ParameterList.Last(parameter => string.Equals(parameter.LocalName, duplicateParameter, StringComparison.Ordinal));
                errors.Add(
                    parsedScript,
                    $"Predicate '{predicateDefinition.Name}' declares parameter '{duplicateParameter}' more than once",
                    predicateDefinition.Name,
                    GameEventScriptSymbolKind.Predicate,
                    GameEventScriptCompileErrorKind.DuplicateDefinitionParameter,
                    duplicateParameterNode);
            }

            ValidateExpressionReferences(
                parsedScript,
                predicateDefinition.Expression,
                callables,
                typeDefinitions,
                errors,
                BuildDeclaredTypeMap(predicateDefinition.ParameterList));
            ValidatePredicateResultExpression(parsedScript, predicateDefinition, callables, typeDefinitions, errors);
        }

        foreach (var functionDefinition in parsedScript.FunctionDefinitions)
        {
            ValidateIdentifierCase(
                    parsedScript,
                    functionDefinition.Name,
                    functionDefinition.Name,
                    GameEventScriptSymbolKind.Function,
                    "Function names must use identifier casing (start lowercase, letters only, optional final _index suffix)",
                    errors);

            foreach (var parameter in functionDefinition.Parameters)
            {
                ValidateIdentifierCase(
                    parsedScript,
                    parameter,
                    functionDefinition.Name,
                    GameEventScriptSymbolKind.Function,
                    $"Function '{functionDefinition.Name}' declares an invalid parameter name '{parameter}'",
                    errors);
            }

            ValidateParameterTypeHints(parsedScript, "Function", functionDefinition.Name, functionDefinition.ParameterList, typeDefinitions, errors);

            var duplicateParameters = functionDefinition.Parameters
                .GroupBy(parameter => parameter, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key);

            foreach (var duplicateParameter in duplicateParameters)
            {
                var duplicateParameterNode = functionDefinition.ParameterList.Last(parameter => string.Equals(parameter.LocalName, duplicateParameter, StringComparison.Ordinal));
                errors.Add(
                    parsedScript,
                    $"Function '{functionDefinition.Name}' declares parameter '{duplicateParameter}' more than once",
                    functionDefinition.Name,
                    GameEventScriptSymbolKind.Function,
                    GameEventScriptCompileErrorKind.DuplicateDefinitionParameter,
                    duplicateParameterNode);
            }

            ValidateExpressionReferences(
                parsedScript,
                functionDefinition.Expression,
                callables,
                typeDefinitions,
                errors,
                BuildDeclaredTypeMap(functionDefinition.ParameterList));
        }

        foreach (var handler in parsedScript.Handlers)
        {
            if (GameEventScriptSystemEndpoints.IsUndeliverableName(handler.Message))
            {
                ValidateUndeliverableHandler(parsedScript, handler, errors);
            }
            else
            {
                ValidateMessageCase(
                    parsedScript,
                    handler.Message,
                    handler.Message,
                    GameEventScriptSymbolKind.Handler,
                    "Handler message names must use message casing (start uppercase and contain only letters)",
                    errors);
                ValidateMessageEnvelopeHandler(parsedScript, handler, errors);
            }

            foreach (var parameter in handler.Parameters)
            {
                ValidateIdentifierCase(
                    parsedScript,
                    parameter,
                    handler.Message,
                    GameEventScriptSymbolKind.Handler,
                    $"Handler '{handler.Message}' declares an invalid parameter name '{parameter}'",
                    errors);
            }

            ValidateParameterTypeHints(parsedScript, "Handler", handler.Message, handler.ParameterList, typeDefinitions, errors);

            var duplicateParameters = handler.Parameters
                .GroupBy(parameter => parameter, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key);

            foreach (var duplicateParameter in duplicateParameters)
            {
                var duplicateParameterNode = handler.ParameterList.Last(parameter => string.Equals(parameter.LocalName, duplicateParameter, StringComparison.Ordinal));
                errors.Add(
                    parsedScript,
                    $"Handler '{handler.Message}' declares parameter '{duplicateParameter}' more than once",
                    handler.Message,
                    GameEventScriptSymbolKind.Handler,
                    GameEventScriptCompileErrorKind.DuplicateHandlerParameter,
                    duplicateParameterNode);
            }

            var handlerScope = ValidationScope.Create(handler.ParameterList);
            foreach (var statement in handler.Statements)
            {
                ValidateStatementReferences(parsedScript, statement, callables, typeDefinitions, errors, handlerScope);
            }
        }
    }

    private static void ValidateUndeliverableHandler(
        ParsedScript parsedScript,
        EventHandlerNode handler,
        GesValidationErrors errors)
    {
        if (handler.DispatchKind != EventHandlerDispatchKind.MessageEnvelope)
        {
            errors.Add(
                parsedScript,
                "System endpoint 'undeliverable' must use 'as envelope' syntax",
                handler.Message,
                GameEventScriptSymbolKind.Handler,
                GameEventScriptCompileErrorKind.InvalidMessageCase,
                handler);
            return;
        }

        var parameter = handler.ParameterList[0];
        if (!string.Equals(parameter.SignatureLabel, GameEventScriptSystemEndpoints.EnvelopeArgumentName, StringComparison.Ordinal) ||
            !string.Equals(parameter.DeclaredType, GameEventScriptSystemEndpoints.EnvelopeTypeName, StringComparison.Ordinal))
        {
            errors.Add(
                parsedScript,
                "System endpoint 'undeliverable' must bind an ':envelope' value",
                parameter.LocalName,
                GameEventScriptSymbolKind.Handler,
                GameEventScriptCompileErrorKind.InvalidMessageCase,
                parameter);
        }
    }

    private static void ValidateMessageEnvelopeHandler(
        ParsedScript parsedScript,
        EventHandlerNode handler,
        GesValidationErrors errors)
    {
        if (handler.DispatchKind != EventHandlerDispatchKind.MessageEnvelope)
        {
            return;
        }

        if (handler.ParameterList.Count != 1)
        {
            errors.Add(
                parsedScript,
                "Message-envelope handlers expect exactly one envelope parameter",
                handler.Message,
                GameEventScriptSymbolKind.Handler,
                GameEventScriptCompileErrorKind.InvalidMessageCase,
                handler);
            return;
        }

        var parameter = handler.ParameterList[0];
        if (!string.Equals(parameter.SignatureLabel, GameEventScriptSystemEndpoints.EnvelopeArgumentName, StringComparison.Ordinal) ||
            !string.Equals(parameter.DeclaredType, GameEventScriptSystemEndpoints.EnvelopeTypeName, StringComparison.Ordinal))
        {
            errors.Add(
                parsedScript,
                "Message-envelope handlers must bind an ':envelope' value",
                handler.Message,
                GameEventScriptSymbolKind.Handler,
                GameEventScriptCompileErrorKind.InvalidMessageCase,
                handler);
        }
    }

    private static void ValidateStatementReferences(
        ParsedScript parsedScriptContext,
        StatementNode statement,
        IReadOnlyDictionary<string, GesCallableDefinition> callables,
        IReadOnlyDictionary<string, TypeDefinitionNode> typeDefinitions,
        GesValidationErrors errors,
        ValidationScope scope)
    {
        switch (statement)
        {
            case PublishStatementNode publish:
                ValidateExpressionReferences(parsedScriptContext, publish.MessageExpression, callables, typeDefinitions, errors, scope.DeclaredTypes);
                foreach (var tagExpression in publish.TagExpressions)
                {
                    ValidateExpressionReferences(parsedScriptContext, tagExpression, callables, typeDefinitions, errors, scope.DeclaredTypes);
                }

                return;

            case LetStatementNode let:
                ValidateExpressionReferences(parsedScriptContext, let.Expression, callables, typeDefinitions, errors, scope.DeclaredTypes);
                ValidateIdentifierCase(
                    parsedScriptContext,
                    let.Identifier,
                    let.Identifier,
                    GameEventScriptSymbolKind.Variable,
                    $"Variable '{let.Identifier}' must use identifier casing (start lowercase, letters only, optional final _index suffix)",
                    errors);
                if (scope.ContainsInCurrentScope(let.Identifier))
                {
                    errors.Add(
                        parsedScriptContext,
                        $"Variable '{let.Identifier}' is already declared in the current scope",
                        let.Identifier,
                        GameEventScriptSymbolKind.Variable,
                        GameEventScriptCompileErrorKind.DuplicateVariable,
                        let);
                    return;
                }

                scope.Declare(let.Identifier, let.DeclaredType);
                return;

            case IfStatementNode ifStatement:
                ValidateExpressionReferences(parsedScriptContext, ifStatement.Condition, callables, typeDefinitions, errors, scope.DeclaredTypes);
                ValidateStatementBodyReferences(parsedScriptContext, ifStatement.ThenBody, callables, typeDefinitions, errors, scope);

                if (ifStatement.ElseBody is null)
                {
                    return;
                }

                ValidateStatementBodyReferences(parsedScriptContext, ifStatement.ElseBody, callables, typeDefinitions, errors, scope);

                return;

            case ForStatementNode forStatement:
                ValidateIterationSourceReferences(parsedScriptContext, forStatement.Source, callables, typeDefinitions, errors, scope.DeclaredTypes);
                ValidateIdentifierCase(
                    parsedScriptContext,
                    forStatement.Identifier,
                    forStatement.Identifier,
                    GameEventScriptSymbolKind.Variable,
                    $"Loop variable '{forStatement.Identifier}' must use identifier casing (start lowercase, letters only, optional final _index suffix)",
                    errors);
                var loopScope = ValidationScope.CreateChild(scope);
                loopScope.Declare(forStatement.Identifier);
                ValidateStatementBodyReferences(parsedScriptContext, forStatement.Body, callables, typeDefinitions, errors, loopScope);

                return;

            case SeededRandomStatementNode seededRandom:
                ValidateExpressionReferences(parsedScriptContext, seededRandom.SeedExpression, callables, typeDefinitions, errors, scope.DeclaredTypes);
                ValidateSeedExpression(parsedScriptContext, seededRandom.SeedExpression, callables, typeDefinitions, scope.DeclaredTypes, errors);
                ValidateStatementBodyReferences(parsedScriptContext, seededRandom.Body, callables, typeDefinitions, errors, scope);

                return;

            case ExpressionStatementNode expressionStatement:
                ValidateExpressionReferences(parsedScriptContext, expressionStatement.Expression, callables, typeDefinitions, errors, scope.DeclaredTypes);
                return;
        }
    }

    private static void ValidateParameterTypeHints(
        ParsedScript parsedScriptContext,
        string declarationKind,
        string declarationName,
        IReadOnlyList<ParameterNode> parameters,
        IReadOnlyDictionary<string, TypeDefinitionNode> typeDefinitions,
        GesValidationErrors errors)
    {
        foreach (var parameter in parameters)
        {
            if (string.IsNullOrEmpty(parameter.DeclaredType) ||
                IsBuiltinConstructorType(parameter.DeclaredType!) ||
                typeDefinitions.ContainsKey(parameter.DeclaredType!))
            {
                continue;
            }

            errors.Add(
                parsedScriptContext,
                $"{declarationKind} '{declarationName}' parameter '{parameter.LocalName}' uses unknown type ':{parameter.DeclaredType}'",
                parameter.DeclaredType!,
                GameEventScriptSymbolKind.Type,
                GameEventScriptCompileErrorKind.InvalidTypeConstructor,
                parameter);
        }
    }

    private static void ValidateStatementBodyReferences(
        ParsedScript parsedScriptContext,
        StatementBodyNode body,
        IReadOnlyDictionary<string, GesCallableDefinition> callables,
        IReadOnlyDictionary<string, TypeDefinitionNode> typeDefinitions,
        GesValidationErrors errors,
        ValidationScope parentScope)
    {
        var bodyScope = body.IsBlock ? ValidationScope.CreateChild(parentScope) : parentScope;
        foreach (var nested in body.Statements)
        {
            ValidateStatementReferences(parsedScriptContext, nested, callables, typeDefinitions, errors, bodyScope);
        }
    }

    private enum StaticExpressionKind
    {
        Boolean,
        Nothing,
        Other,
        Unknown
    }

    private readonly struct StaticExpressionInfo
    {
        private StaticExpressionInfo(StaticExpressionKind kind, string? typeName)
        {
            Kind = kind;
            TypeName = typeName;
        }

        public StaticExpressionKind Kind { get; }

        public string? TypeName { get; }

        public bool IsPredicateCompatible => Kind is StaticExpressionKind.Boolean or StaticExpressionKind.Nothing;

        public static StaticExpressionInfo Boolean { get; } = new(StaticExpressionKind.Boolean, "boolean");

        public static StaticExpressionInfo Nothing { get; } = new(StaticExpressionKind.Nothing, "nothing");

        public static StaticExpressionInfo Unknown { get; } = new(StaticExpressionKind.Unknown, null);

        public static StaticExpressionInfo Other(string? typeName = null) => new(StaticExpressionKind.Other, typeName);
    }

    private static void ValidatePredicateResultExpression(
        ParsedScript parsedScriptContext,
        PredicateDefinitionNode predicateDefinition,
        IReadOnlyDictionary<string, GesCallableDefinition> callables,
        IReadOnlyDictionary<string, TypeDefinitionNode> typeDefinitions,
        GesValidationErrors errors)
    {
        var parameterTypes = BuildDeclaredTypeMap(predicateDefinition.ParameterList);
        var result = ClassifyExpression(
            predicateDefinition.Expression,
            callables,
            typeDefinitions,
            parameterTypes,
            new HashSet<string>(StringComparer.Ordinal));

        if (result.IsPredicateCompatible)
        {
            return;
        }

        errors.Add(
            parsedScriptContext,
            $"Predicate '{predicateDefinition.Name}' must return :boolean or :nothing; use 'as :boolean' for explicit boolean coercion.",
            predicateDefinition.Name,
            GameEventScriptSymbolKind.Predicate,
            GameEventScriptCompileErrorKind.InvalidPredicate,
            predicateDefinition.Expression);
    }

    private static void ValidateSeedExpression(
        ParsedScript parsedScriptContext,
        ExpressionNode seedExpression,
        IReadOnlyDictionary<string, GesCallableDefinition> callables,
        IReadOnlyDictionary<string, TypeDefinitionNode> typeDefinitions,
        IReadOnlyDictionary<string, string> declaredTypes,
        GesValidationErrors errors)
    {
        var seedType = ClassifyExpression(
            seedExpression,
            callables,
            typeDefinitions,
            declaredTypes,
            new HashSet<string>(StringComparer.Ordinal));
        if (seedType.Kind == StaticExpressionKind.Other &&
            string.Equals(seedType.TypeName, "integer", StringComparison.Ordinal))
        {
            return;
        }

        errors.Add(
            parsedScriptContext,
            "Seeded random seed must statically resolve to unitless :integer; cast dynamic seeds explicitly with 'as :integer'.",
            "random",
            GameEventScriptSymbolKind.Type,
            GameEventScriptCompileErrorKind.InvalidTypeConstructor,
            seedExpression);
    }

    private static IReadOnlyDictionary<string, string> BuildDeclaredTypeMap(IReadOnlyList<ParameterNode> parameters)
    {
        var types = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var parameter in parameters)
        {
            if (!string.IsNullOrWhiteSpace(parameter.DeclaredType))
            {
                types[parameter.LocalName] = parameter.DeclaredType!;
            }
        }

        return types;
    }

    private static StaticExpressionInfo ClassifyExpression(
        ExpressionNode expression,
        IReadOnlyDictionary<string, GesCallableDefinition> callables,
        IReadOnlyDictionary<string, TypeDefinitionNode> typeDefinitions,
        IReadOnlyDictionary<string, string> declaredTypes,
        ISet<string> visitedCallables)
    {
        switch (expression)
        {
            case BooleanLiteralExpressionNode:
                return StaticExpressionInfo.Boolean;

            case IntegerLiteralExpressionNode:
                return StaticExpressionInfo.Other("integer");

            case UnitIntegerLiteralExpressionNode unitInteger:
                return StaticExpressionInfo.Other(unitInteger.UnitName);

            case FloatLiteralExpressionNode:
                return StaticExpressionInfo.Other("float");

            case UnitFloatLiteralExpressionNode unitFloat:
                return StaticExpressionInfo.Other(unitFloat.UnitName);

            case PercentageLiteralExpressionNode:
                return StaticExpressionInfo.Other("percentage");

            case TextLiteralExpressionNode:
                return StaticExpressionInfo.Other("text");

            case TagLiteralExpressionNode:
                return StaticExpressionInfo.Other("tag");

            case TypeCheckExpressionNode:
            case PredicateCallExpressionNode:
            case ExtensionPredicateExpressionNode:
                return StaticExpressionInfo.Boolean;

            case IdentifierExpressionNode identifier:
                return declaredTypes.TryGetValue(identifier.Name, out var declaredType)
                    ? FromDeclaredType(declaredType)
                    : StaticExpressionInfo.Unknown;

            case MemberAccessExpressionNode memberAccess:
                return ClassifyMemberAccess(memberAccess, callables, typeDefinitions, declaredTypes, visitedCallables);

            case CollectionAccessExpressionNode collectionAccess:
                return ClassifyCollectionAccess(collectionAccess);

            case CallExpressionNode call:
                return ClassifyCall(call, callables, typeDefinitions, visitedCallables);

            case TypeCastExpressionNode typeCast:
                return FromDeclaredType(typeCast.TypeName);

            case TypeConstructorExpressionNode typeConstructor:
                return FromDeclaredType(typeConstructor.TypeName);

            case UnaryExpressionNode unary:
                return ClassifyUnary(unary, callables, typeDefinitions, declaredTypes, visitedCallables);

            case BinaryExpressionNode binary:
                return ClassifyBinary(binary, callables, typeDefinitions, declaredTypes, visitedCallables);

            case GuardedChoiceExpressionNode guardedChoice:
                return ClassifyGuardedChoice(guardedChoice, callables, typeDefinitions, declaredTypes, visitedCallables);

            case SeededRandomExpressionNode seededRandom:
                return ClassifyExpression(seededRandom.BodyExpression, callables, typeDefinitions, declaredTypes, visitedCallables);

            default:
                return StaticExpressionInfo.Other();
        }
    }

    private static StaticExpressionInfo FromDeclaredType(string? typeName)
        => typeName switch
        {
            "boolean" => StaticExpressionInfo.Boolean,
            "nothing" => StaticExpressionInfo.Nothing,
            null or "" => StaticExpressionInfo.Unknown,
            _ => StaticExpressionInfo.Other(typeName)
        };

    private static StaticExpressionInfo ClassifyMemberAccess(
        MemberAccessExpressionNode memberAccess,
        IReadOnlyDictionary<string, GesCallableDefinition> callables,
        IReadOnlyDictionary<string, TypeDefinitionNode> typeDefinitions,
        IReadOnlyDictionary<string, string> declaredTypes,
        ISet<string> visitedCallables)
    {
        var target = ClassifyExpression(memberAccess.Target, callables, typeDefinitions, declaredTypes, visitedCallables);
        if (string.IsNullOrWhiteSpace(target.TypeName) ||
            !typeDefinitions.TryGetValue(target.TypeName!, out var typeDefinition))
        {
            return StaticExpressionInfo.Unknown;
        }

        var field = typeDefinition.Fields.FirstOrDefault(candidate => string.Equals(candidate.Name, memberAccess.Member, StringComparison.Ordinal));
        return field is null ? StaticExpressionInfo.Unknown : FromDeclaredType(field.TypeName);
    }

    private static StaticExpressionInfo ClassifyCollectionAccess(CollectionAccessExpressionNode collectionAccess)
        => collectionAccess.Selector switch
        {
            PredicateSelectorNode => StaticExpressionInfo.Boolean,
            ContainsSelectorNode => StaticExpressionInfo.Boolean,
            _ => StaticExpressionInfo.Unknown
        };

    private static StaticExpressionInfo ClassifyCall(
        CallExpressionNode call,
        IReadOnlyDictionary<string, GesCallableDefinition> callables,
        IReadOnlyDictionary<string, TypeDefinitionNode> typeDefinitions,
        ISet<string> visitedCallables)
    {
        if (!callables.TryGetValue(call.Name, out var callable))
        {
            return StaticExpressionInfo.Unknown;
        }

        if (callable.Kind == GameEventScriptCallableKind.PredicateCall)
        {
            return StaticExpressionInfo.Boolean;
        }

        var callableKey = $"{callable.Kind}:{callable.Name}";
        if (!visitedCallables.Add(callableKey))
        {
            return StaticExpressionInfo.Unknown;
        }

        try
        {
            return ClassifyExpression(
                callable.Expression,
                callables,
                typeDefinitions,
                BuildDeclaredTypeMap(callable.ParameterList),
                visitedCallables);
        }
        finally
        {
            visitedCallables.Remove(callableKey);
        }
    }

    private static StaticExpressionInfo ClassifyUnary(
        UnaryExpressionNode unary,
        IReadOnlyDictionary<string, GesCallableDefinition> callables,
        IReadOnlyDictionary<string, TypeDefinitionNode> typeDefinitions,
        IReadOnlyDictionary<string, string> declaredTypes,
        ISet<string> visitedCallables)
    {
        return unary.Operator switch
        {
            GesUnaryOperator.HasValue or GesUnaryOperator.Empty or GesUnaryOperator.Chance => StaticExpressionInfo.Boolean,
            GesUnaryOperator.Not => ClassifyExpression(unary.Operand, callables, typeDefinitions, declaredTypes, visitedCallables).IsPredicateCompatible
                ? StaticExpressionInfo.Boolean
                : StaticExpressionInfo.Unknown,
            _ => StaticExpressionInfo.Other()
        };
    }

    private static StaticExpressionInfo ClassifyBinary(
        BinaryExpressionNode binary,
        IReadOnlyDictionary<string, GesCallableDefinition> callables,
        IReadOnlyDictionary<string, TypeDefinitionNode> typeDefinitions,
        IReadOnlyDictionary<string, string> declaredTypes,
        ISet<string> visitedCallables)
    {
        return binary.Operator switch
        {
            GesBinaryOperator.Equal or
                GesBinaryOperator.NotEqual or
                GesBinaryOperator.ApproxEqual or
                GesBinaryOperator.Less or
                GesBinaryOperator.Greater or
                GesBinaryOperator.LessOrEqual or
                GesBinaryOperator.GreaterOrEqual or
                GesBinaryOperator.Contains or
                GesBinaryOperator.ContainsValue or
                GesBinaryOperator.StartsWith or
                GesBinaryOperator.EndsWith => StaticExpressionInfo.Boolean,
            GesBinaryOperator.And or
                GesBinaryOperator.Or or
                GesBinaryOperator.Xor or
                GesBinaryOperator.Implies => ClassifyExpression(binary.Left, callables, typeDefinitions, declaredTypes, visitedCallables).IsPredicateCompatible &&
                                             ClassifyExpression(binary.Right, callables, typeDefinitions, declaredTypes, visitedCallables).IsPredicateCompatible
                ? StaticExpressionInfo.Boolean
                : StaticExpressionInfo.Unknown,
            GesBinaryOperator.Default => MergePredicateCompatibleResults(
                ClassifyExpression(binary.Left, callables, typeDefinitions, declaredTypes, visitedCallables),
                ClassifyExpression(binary.Right, callables, typeDefinitions, declaredTypes, visitedCallables)),
            _ => StaticExpressionInfo.Other()
        };
    }

    private static StaticExpressionInfo ClassifyGuardedChoice(
        GuardedChoiceExpressionNode guardedChoice,
        IReadOnlyDictionary<string, GesCallableDefinition> callables,
        IReadOnlyDictionary<string, TypeDefinitionNode> typeDefinitions,
        IReadOnlyDictionary<string, string> declaredTypes,
        ISet<string> visitedCallables)
    {
        var result = StaticExpressionInfo.Nothing;
        foreach (var branch in guardedChoice.Branches)
        {
            result = MergePredicateCompatibleResults(
                result,
                ClassifyExpression(branch.ValueExpression, callables, typeDefinitions, declaredTypes, visitedCallables));
            if (!result.IsPredicateCompatible)
            {
                return result;
            }
        }

        return MergePredicateCompatibleResults(
            result,
            ClassifyExpression(guardedChoice.OtherwiseExpression, callables, typeDefinitions, declaredTypes, visitedCallables));
    }

    private static StaticExpressionInfo MergePredicateCompatibleResults(StaticExpressionInfo left, StaticExpressionInfo right)
    {
        if (!left.IsPredicateCompatible || !right.IsPredicateCompatible)
        {
            return left.Kind == StaticExpressionKind.Unknown || right.Kind == StaticExpressionKind.Unknown
                ? StaticExpressionInfo.Unknown
                : StaticExpressionInfo.Other();
        }

        return left.Kind == StaticExpressionKind.Boolean || right.Kind == StaticExpressionKind.Boolean
            ? StaticExpressionInfo.Boolean
            : StaticExpressionInfo.Nothing;
    }

    private static void ValidateExpressionReferences(
        ParsedScript parsedScriptContext,
        ExpressionNode expression,
        IReadOnlyDictionary<string, GesCallableDefinition> callables,
        IReadOnlyDictionary<string, TypeDefinitionNode> typeDefinitions,
        GesValidationErrors errors,
        IReadOnlyDictionary<string, string>? declaredTypes = null)
    {
        declaredTypes ??= new Dictionary<string, string>(StringComparer.Ordinal);
        while (true)
        {
            switch (expression)
            {
                case IdentifierExpressionNode identifierExpression:
                    ValidateIdentifierCase(
                        parsedScriptContext,
                        identifierExpression.Name,
                        identifierExpression.Name,
                        GameEventScriptSymbolKind.Variable,
                        $"Identifier '{identifierExpression.Name}' must use identifier casing (start lowercase, letters only, optional final _index suffix)",
                        errors);
                    return;

                case CallExpressionNode call:
                    ValidateIdentifierCase(
                        parsedScriptContext,
                        call.Name,
                        call.Name,
                        GameEventScriptSymbolKind.GlobalDefinition,
                        $"Call target '{call.Name}' must use identifier casing (predicate/function names start lowercase)",
                        errors);
                    ValidateDuplicateNamedArguments(parsedScriptContext, call.Name, call.ArgumentList.Arguments, errors);
                    ValidateCallExpression(parsedScriptContext, call, callables, typeDefinitions, errors);
                    foreach (var argument in call.ArgumentList.Arguments)
                    {
                        if (argument.Label is not null)
                        {
                            ValidateIdentifierCase(
                                parsedScriptContext,
                                argument.Label,
                                call.Name,
                                GameEventScriptSymbolKind.GlobalDefinition,
                                $"Call argument label '{argument.Label}' must use identifier casing",
                                errors);
                        }

                        ValidateExpressionReferences(parsedScriptContext, argument.Expression, callables, typeDefinitions, errors, declaredTypes);
                    }

                    return;

                case HandlerLiteralExpressionNode handlerLiteral:
                    ValidateMessageCase(
                        parsedScriptContext,
                        handlerLiteral.Message,
                        handlerLiteral.Message,
                        GameEventScriptSymbolKind.Handler,
                        $"Handler literal '{handlerLiteral.Message}' must use message casing (start uppercase and contain only letters)",
                        errors);
                    foreach (var parameter in handlerLiteral.Parameters)
                    {
                        ValidateIdentifierCase(
                            parsedScriptContext,
                            parameter,
                            handlerLiteral.Message,
                            GameEventScriptSymbolKind.Handler,
                            $"Handler literal '{handlerLiteral.Message}' declares invalid parameter '{parameter}'",
                            errors);
                    }

                    ValidateDuplicateHandlerLiteralParameters(parsedScriptContext, handlerLiteral, errors);
                    return;

                case MessageLiteralExpressionNode messageLiteral:
                    ValidateMessageCase(
                        parsedScriptContext,
                        messageLiteral.Message,
                        messageLiteral.Message,
                        GameEventScriptSymbolKind.Message,
                        $"Message literal '{messageLiteral.Message}' must use message casing (start uppercase and contain only letters)",
                        errors);
                    ValidateDuplicateNamedArguments(parsedScriptContext, messageLiteral.Message, messageLiteral.Arguments, errors);
                    foreach (var argument in messageLiteral.Arguments)
                    {
                        if (argument.Label is not null)
                        {
                            ValidateIdentifierCase(
                                parsedScriptContext,
                                argument.Label,
                                messageLiteral.Message,
                                GameEventScriptSymbolKind.Message,
                                $"Message literal '{messageLiteral.Message}' declares invalid argument name '{argument.Label}'",
                                errors);
                        }

                        ValidateExpressionReferences(parsedScriptContext, argument.Expression, callables, typeDefinitions, errors, declaredTypes);
                    }

                    return;

                case ExtensionCallExpressionNode extensionCall:
                    foreach (var argument in extensionCall.Arguments)
                    {
                        ValidateExpressionReferences(parsedScriptContext, argument.Expression, callables, typeDefinitions, errors, declaredTypes);
                    }

                    return;

                case TypeConstructorExpressionNode typeConstructor:
                    ValidateTypeConstructorExpression(parsedScriptContext, typeConstructor, callables, typeDefinitions, errors, declaredTypes);
                    return;

                case PredicateCallExpressionNode predicateCall:
                    ValidateIdentifierCase(
                        parsedScriptContext,
                        predicateCall.PredicateName,
                        predicateCall.PredicateName,
                        GameEventScriptSymbolKind.Predicate,
                        $"Predicate test target '{predicateCall.PredicateName}' must use identifier casing (start lowercase)",
                        errors);
                    if (!callables.TryGetValue(predicateCall.PredicateName, out var callableDefinition) ||
                        callableDefinition.Kind != GameEventScriptCallableKind.PredicateCall ||
                        callableDefinition.Parameters.Count != 1)
                    {
                        errors.Add(
                            parsedScriptContext,
                            $"Predicate '{predicateCall.PredicateName}' must exist and declare exactly one parameter to be used with 'is'",
                            predicateCall.PredicateName,
                            GameEventScriptSymbolKind.Predicate,
                            GameEventScriptCompileErrorKind.InvalidPredicate);
                    }

                    expression = predicateCall.Value;
                    continue;

                case ExtensionPredicateExpressionNode extensionPredicate:
                    expression = extensionPredicate.Value;
                    continue;

                case UnaryExpressionNode unary:
                    expression = unary.Operand;
                    continue;

                case VariadicTaggedExpressionNode variadic:
                    foreach (var argument in variadic.Arguments)
                    {
                        ValidateExpressionReferences(parsedScriptContext, argument, callables, typeDefinitions, errors, declaredTypes);
                    }

                    return;

                case ClampExpressionNode clamp:
                    ValidateExpressionReferences(parsedScriptContext, clamp.Value, callables, typeDefinitions, errors, declaredTypes);
                    ValidateExpressionReferences(parsedScriptContext, clamp.Minimum, callables, typeDefinitions, errors, declaredTypes);
                    expression = clamp.Maximum;
                    continue;

                case RangeExpressionNode rangeExpression:
                    ValidateExpressionReferences(parsedScriptContext, rangeExpression.FromExpression, callables, typeDefinitions, errors, declaredTypes);
                    ValidateExpressionReferences(parsedScriptContext, rangeExpression.ToExpression, callables, typeDefinitions, errors, declaredTypes);
                    if (rangeExpression.StepExpression is not null)
                    {
                        expression = rangeExpression.StepExpression;
                        continue;
                    }

                    return;

                case RandomExpressionNode random:
                    ValidateExpressionReferences(parsedScriptContext, random.FromExpression, callables, typeDefinitions, errors, declaredTypes);
                    expression = random.ToExpression;
                    continue;

                case SeededRandomExpressionNode seededRandom:
                    ValidateExpressionReferences(parsedScriptContext, seededRandom.SeedExpression, callables, typeDefinitions, errors, declaredTypes);
                    ValidateSeedExpression(parsedScriptContext, seededRandom.SeedExpression, callables, typeDefinitions, declaredTypes, errors);
                    expression = seededRandom.BodyExpression;
                    continue;

                case GeneratedCollectionExpressionNode generatedCollection:
                    ValidateIdentifierCase(
                        parsedScriptContext,
                        generatedCollection.Identifier,
                        generatedCollection.Identifier,
                        GameEventScriptSymbolKind.Variable,
                        $"Generated collection identifier '{generatedCollection.Identifier}' must use identifier casing (start lowercase)",
                        errors);
                    ValidateIterationSourceReferences(parsedScriptContext, generatedCollection.Source, callables, typeDefinitions, errors, declaredTypes);
                    if (generatedCollection.Predicate is not null)
                    {
                        ValidateExpressionReferences(parsedScriptContext, generatedCollection.Predicate, callables, typeDefinitions, errors, declaredTypes);
                    }

                    expression = generatedCollection.Projection;
                    continue;

                case GuardedChoiceExpressionNode guardedChoice:
                    foreach (var branch in guardedChoice.Branches)
                    {
                        ValidateExpressionReferences(parsedScriptContext, branch.ValueExpression, callables, typeDefinitions, errors, declaredTypes);
                        ValidateExpressionReferences(parsedScriptContext, branch.ConditionExpression, callables, typeDefinitions, errors, declaredTypes);
                    }

                    expression = guardedChoice.OtherwiseExpression;
                    continue;

                case BinaryExpressionNode binary:
                    ValidateExpressionReferences(parsedScriptContext, binary.Left, callables, typeDefinitions, errors, declaredTypes);
                    expression = binary.Right;
                    continue;

                case TypeCheckExpressionNode typeCheck:
                    ValidateRemovedOptionalType(parsedScriptContext, typeCheck.TypeName, typeCheck, errors);
                    expression = typeCheck.Value;
                    continue;

                case TypeCastExpressionNode typeCast:
                    ValidateRemovedOptionalType(parsedScriptContext, typeCast.TypeName, typeCast, errors);
                    expression = typeCast.Value;
                    continue;

                case MemberAccessExpressionNode memberAccess:
                    expression = memberAccess.Target;
                    continue;

                case CollectionAccessExpressionNode collectionAccess:
                    ValidateExpressionReferences(parsedScriptContext, collectionAccess.Target, callables, typeDefinitions, errors, declaredTypes);
                    ValidateCollectionSelectorReferences(parsedScriptContext, collectionAccess.Selector, callables, typeDefinitions, errors, declaredTypes);
                    return;

                case ListLiteralExpressionNode list:
                    foreach (var item in list.Items)
                    {
                        ValidateExpressionReferences(parsedScriptContext, item, callables, typeDefinitions, errors, declaredTypes);
                    }

                    return;

                case SetLiteralExpressionNode set:
                    foreach (var item in set.Items)
                    {
                        ValidateExpressionReferences(parsedScriptContext, item, callables, typeDefinitions, errors, declaredTypes);
                    }

                    return;

                case MapLiteralExpressionNode dictionary:
                    foreach (var entry in dictionary.Entries)
                    {
                        ValidateExpressionReferences(parsedScriptContext, entry.Value, callables, typeDefinitions, errors, declaredTypes);
                    }

                    return;
            }

            break;
        }
    }

    private static void ValidateIterationSourceReferences(
        ParsedScript parsedScriptContext,
        IterationSourceNode source,
        IReadOnlyDictionary<string, GesCallableDefinition> callables,
        IReadOnlyDictionary<string, TypeDefinitionNode> typeDefinitions,
        GesValidationErrors errors,
        IReadOnlyDictionary<string, string>? declaredTypes = null)
    {
        switch (source)
        {
            case CollectionIterationSourceNode collectionSource:
                ValidateExpressionReferences(parsedScriptContext, collectionSource.Expression, callables, typeDefinitions, errors, declaredTypes);
                return;
            case RangeIterationSourceNode rangeSource:
                ValidateExpressionReferences(parsedScriptContext, rangeSource.RangeExpression, callables, typeDefinitions, errors, declaredTypes);
                return;
        }
    }

    private static void ValidateCollectionSelectorReferences(
        ParsedScript parsedScriptContext,
        CollectionSelectorNode selector,
        IReadOnlyDictionary<string, GesCallableDefinition> callables,
        IReadOnlyDictionary<string, TypeDefinitionNode> typeDefinitions,
        GesValidationErrors errors,
        IReadOnlyDictionary<string, string>? declaredTypes = null)
    {
        switch (selector)
        {
            case ExpressionSelectorNode expressionSelector:
                ValidateExpressionReferences(parsedScriptContext, expressionSelector.Expression, callables, typeDefinitions, errors, declaredTypes);
                return;
            case SeriesTermSelectorNode seriesTermSelector:
                ValidateExpressionReferences(parsedScriptContext, seriesTermSelector.IndexExpression, callables, typeDefinitions, errors, declaredTypes);
                return;
            case PredicateSelectorNode predicateSelector:
                ValidateIdentifierCase(
                    parsedScriptContext,
                    predicateSelector.Identifier,
                    predicateSelector.Identifier,
                    GameEventScriptSymbolKind.Variable,
                    $"Selector identifier '{predicateSelector.Identifier}' must use identifier casing (start lowercase)",
                    errors);
                ValidateExpressionReferences(parsedScriptContext, predicateSelector.Predicate, callables, typeDefinitions, errors, declaredTypes);
                return;
            case CountSelectorNode countSelector:
                ValidateIdentifierCase(
                    parsedScriptContext,
                    countSelector.Identifier,
                    countSelector.Identifier,
                    GameEventScriptSymbolKind.Variable,
                    $"Selector identifier '{countSelector.Identifier}' must use identifier casing (start lowercase)",
                    errors);
                ValidateExpressionReferences(parsedScriptContext, countSelector.Predicate, callables, typeDefinitions, errors, declaredTypes);
                return;
            case ChooseSelectorNode chooseSelector:
                if (chooseSelector.Identifier is not null)
                {
                    ValidateIdentifierCase(
                        parsedScriptContext,
                        chooseSelector.Identifier,
                        chooseSelector.Identifier,
                        GameEventScriptSymbolKind.Variable,
                        $"Selector identifier '{chooseSelector.Identifier}' must use identifier casing (start lowercase)",
                        errors);
                }

                if (chooseSelector.Predicate is not null)
                {
                    ValidateExpressionReferences(parsedScriptContext, chooseSelector.Predicate, callables, typeDefinitions, errors, declaredTypes);
                }

                if (chooseSelector.WeightIdentifier is not null)
                {
                    ValidateIdentifierCase(
                        parsedScriptContext,
                        chooseSelector.WeightIdentifier,
                        chooseSelector.WeightIdentifier,
                        GameEventScriptSymbolKind.Variable,
                        $"Selector weight identifier '{chooseSelector.WeightIdentifier}' must use identifier casing (start lowercase)",
                        errors);
                }

                if (chooseSelector.WeightExpression is not null)
                {
                    ValidateExpressionReferences(parsedScriptContext, chooseSelector.WeightExpression, callables, typeDefinitions, errors, declaredTypes);
                }

                return;
            case EdgeSelectorNode { Predicate: not null } edgeSelector:
                if (edgeSelector.Identifier is not null)
                {
                    ValidateIdentifierCase(
                        parsedScriptContext,
                        edgeSelector.Identifier,
                        edgeSelector.Identifier,
                        GameEventScriptSymbolKind.Variable,
                        $"Selector identifier '{edgeSelector.Identifier}' must use identifier casing (start lowercase)",
                        errors);
                }

                ValidateExpressionReferences(parsedScriptContext, edgeSelector.Predicate, callables, typeDefinitions, errors, declaredTypes);
                return;
            case FilterSelectorNode filterSelector:
                ValidateIdentifierCase(
                    parsedScriptContext,
                    filterSelector.Identifier,
                    filterSelector.Identifier,
                    GameEventScriptSymbolKind.Variable,
                    $"Selector identifier '{filterSelector.Identifier}' must use identifier casing (start lowercase)",
                    errors);
                ValidateExpressionReferences(parsedScriptContext, filterSelector.Predicate, callables, typeDefinitions, errors, declaredTypes);
                return;
            case SumSelectorNode sumSelector:
                ValidateIdentifierCase(
                    parsedScriptContext,
                    sumSelector.Identifier,
                    sumSelector.Identifier,
                    GameEventScriptSymbolKind.Variable,
                    $"Selector identifier '{sumSelector.Identifier}' must use identifier casing (start lowercase)",
                    errors);
                ValidateExpressionReferences(parsedScriptContext, sumSelector.Projection, callables, typeDefinitions, errors, declaredTypes);
                return;
            case AverageSelectorNode averageSelector:
                ValidateIdentifierCase(
                    parsedScriptContext,
                    averageSelector.Identifier,
                    averageSelector.Identifier,
                    GameEventScriptSymbolKind.Variable,
                    $"Selector identifier '{averageSelector.Identifier}' must use identifier casing (start lowercase)",
                    errors);
                ValidateExpressionReferences(parsedScriptContext, averageSelector.Projection, callables, typeDefinitions, errors, declaredTypes);
                return;
            case SelectSelectorNode selectSelector:
                ValidateIdentifierCase(
                    parsedScriptContext,
                    selectSelector.Identifier,
                    selectSelector.Identifier,
                    GameEventScriptSymbolKind.Variable,
                    $"Selector identifier '{selectSelector.Identifier}' must use identifier casing (start lowercase)",
                    errors);
                ValidateExpressionReferences(parsedScriptContext, selectSelector.Projection, callables, typeDefinitions, errors, declaredTypes);
                return;
            case MapSelectorNode dictionarySelector:
                ValidateIdentifierCase(
                    parsedScriptContext,
                    dictionarySelector.Identifier,
                    dictionarySelector.Identifier,
                    GameEventScriptSymbolKind.Variable,
                    $"Selector identifier '{dictionarySelector.Identifier}' must use identifier casing (start lowercase)",
                    errors);
                ValidateExpressionReferences(parsedScriptContext, dictionarySelector.KeyProjection, callables, typeDefinitions, errors, declaredTypes);
                if (dictionarySelector.ValueProjection is not null)
                {
                    ValidateExpressionReferences(parsedScriptContext, dictionarySelector.ValueProjection, callables, typeDefinitions, errors, declaredTypes);
                }

                return;
            case MinSelectorNode minSelector:
                ValidateIdentifierCase(
                    parsedScriptContext,
                    minSelector.Identifier,
                    minSelector.Identifier,
                    GameEventScriptSymbolKind.Variable,
                    $"Selector identifier '{minSelector.Identifier}' must use identifier casing (start lowercase)",
                    errors);
                ValidateExpressionReferences(parsedScriptContext, minSelector.Projection, callables, typeDefinitions, errors, declaredTypes);
                return;
            case MaxSelectorNode maxSelector:
                ValidateIdentifierCase(
                    parsedScriptContext,
                    maxSelector.Identifier,
                    maxSelector.Identifier,
                    GameEventScriptSymbolKind.Variable,
                    $"Selector identifier '{maxSelector.Identifier}' must use identifier casing (start lowercase)",
                    errors);
                ValidateExpressionReferences(parsedScriptContext, maxSelector.Projection, callables, typeDefinitions, errors, declaredTypes);
                return;
            case ContainsSelectorNode containsSelector:
                ValidateExpressionReferences(parsedScriptContext, containsSelector.ValueExpression, callables, typeDefinitions, errors, declaredTypes);
                return;
            case DistinctSelectorNode { Projection: not null } distinctSelector:
                if (distinctSelector.Identifier is not null)
                {
                    ValidateIdentifierCase(
                        parsedScriptContext,
                        distinctSelector.Identifier,
                        distinctSelector.Identifier,
                        GameEventScriptSymbolKind.Variable,
                        $"Selector identifier '{distinctSelector.Identifier}' must use identifier casing (start lowercase)",
                        errors);
                }

                ValidateExpressionReferences(parsedScriptContext, distinctSelector.Projection, callables, typeDefinitions, errors, declaredTypes);
                return;
            case GroupBySelectorNode groupBySelector:
                ValidateIdentifierCase(
                    parsedScriptContext,
                    groupBySelector.Identifier,
                    groupBySelector.Identifier,
                    GameEventScriptSymbolKind.Variable,
                    $"Selector identifier '{groupBySelector.Identifier}' must use identifier casing (start lowercase)",
                    errors);
                ValidateExpressionReferences(parsedScriptContext, groupBySelector.Projection, callables, typeDefinitions, errors, declaredTypes);
                return;
            case OrderBySelectorNode orderBySelector:
                ValidateIdentifierCase(
                    parsedScriptContext,
                    orderBySelector.Identifier,
                    orderBySelector.Identifier,
                    GameEventScriptSymbolKind.Variable,
                    $"Selector identifier '{orderBySelector.Identifier}' must use identifier casing (start lowercase)",
                    errors);
                ValidateExpressionReferences(parsedScriptContext, orderBySelector.Projection, callables, typeDefinitions, errors, declaredTypes);
                return;
        }
    }

    private static void ValidateCallExpression(
        ParsedScript parsedScriptContext,
        CallExpressionNode call,
        IReadOnlyDictionary<string, GesCallableDefinition> callables,
        IReadOnlyDictionary<string, TypeDefinitionNode> typeDefinitions,
        GesValidationErrors errors)
    {
        if (!callables.TryGetValue(call.Name, out var callable))
        {
            if (call.ArgumentList.Arguments.All(argument => argument.Label is null))
            {
                errors.Add(
                    parsedScriptContext,
                    $"No predicate or function named '{call.Name}' exists",
                    call.Name,
                    GameEventScriptSymbolKind.GlobalDefinition,
                    GameEventScriptCompileErrorKind.MissingCallable);
            }

            return;
        }

        ValidateCallArity(parsedScriptContext, callable.Kind, call.Name, callable.Parameters.Count, call.Arguments.Count, errors);
        ValidateCallLabels(parsedScriptContext, callable.Kind, call.Name, callable.SignatureLabels, call.ArgumentList.Arguments, errors);
    }

    private static void ValidateCallLabels(
        ParsedScript parsedScriptContext,
        GameEventScriptCallableKind kind,
        string name,
        IReadOnlyList<string> expectedLabels,
        IReadOnlyList<ArgumentNode> arguments,
        GesValidationErrors errors)
    {
        var count = Math.Min(expectedLabels.Count, arguments.Count);
        for (var index = 0; index < count; index++)
        {
            var expected = expectedLabels[index];
            var actual = arguments[index].Name;
            if (string.Equals(expected, actual, StringComparison.Ordinal))
            {
                continue;
            }

            errors.Add(
                parsedScriptContext,
                $"{kind} '{name}' argument {index + 1} expects label '{expected}' but received '{actual}'",
                name,
                kind == GameEventScriptCallableKind.PredicateCall ? GameEventScriptSymbolKind.Predicate : GameEventScriptSymbolKind.Function,
                kind == GameEventScriptCallableKind.PredicateCall ? GameEventScriptCompileErrorKind.WrongPredicateArity : GameEventScriptCompileErrorKind.WrongFunctionArity);
        }
    }

    private static void ValidateTypeConstructorExpression(
        ParsedScript parsedScriptContext,
        TypeConstructorExpressionNode constructor,
        IReadOnlyDictionary<string, GesCallableDefinition> callables,
        IReadOnlyDictionary<string, TypeDefinitionNode> typeDefinitions,
        GesValidationErrors errors,
        IReadOnlyDictionary<string, string>? declaredTypes = null)
    {
        ValidateDuplicateNamedArguments(parsedScriptContext, constructor.TypeName, constructor.Arguments, errors);
        foreach (var argument in constructor.Arguments)
        {
            if (argument.Label is not null)
            {
                ValidateIdentifierCase(
                    parsedScriptContext,
                    argument.Label,
                    constructor.TypeName,
                    GameEventScriptSymbolKind.Type,
                    $"Type constructor ':{constructor.TypeName}' declares invalid argument label '{argument.Label}'",
                    errors);
            }

            ValidateExpressionReferences(parsedScriptContext, argument.Expression, callables, typeDefinitions, errors, declaredTypes);
        }

        if (IsBuiltinConstructorType(constructor.TypeName))
        {
            ValidateBuiltinTypeConstructor(parsedScriptContext, constructor, typeDefinitions, errors);
            return;
        }

        if (!typeDefinitions.TryGetValue(constructor.TypeName, out var typeDefinition))
        {
            AddTypeConstructorError(parsedScriptContext, constructor.TypeName, $"Unknown type constructor ':{constructor.TypeName}'", errors);
            return;
        }

        if (constructor.Arguments.Any(argument => argument.Label is null))
        {
            AddTypeConstructorError(parsedScriptContext, constructor.TypeName, $"Custom type constructor ':{constructor.TypeName}' requires labeled field arguments", errors);
        }

        var fieldNames = new HashSet<string>(typeDefinition.Fields.Select(field => field.Name), StringComparer.Ordinal);
        foreach (var argument in constructor.Arguments.Where(argument => argument.Label is not null))
        {
            if (!fieldNames.Contains(argument.Label!))
            {
                AddTypeConstructorError(
                    parsedScriptContext,
                    constructor.TypeName,
                    $"Custom type constructor ':{constructor.TypeName}' has unknown field '{argument.Label}'",
                    errors);
            }
        }
    }

    private static void ValidateBuiltinTypeConstructor(
        ParsedScript parsedScriptContext,
        TypeConstructorExpressionNode constructor,
        IReadOnlyDictionary<string, TypeDefinitionNode> typeDefinitions,
        GesValidationErrors errors)
    {
        switch (constructor.TypeName)
        {
            case "vector":
            case "point":
                ValidateVectorConstructor(parsedScriptContext, constructor, ["x", "y", "z"], errors);
                return;
            case "ref":
                ValidateRefConstructor(parsedScriptContext, constructor, typeDefinitions, errors);
                return;
            default:
                if (constructor.Arguments.Count != 1 || constructor.Arguments[0].Label is not null)
                {
                    AddTypeConstructorError(
                        parsedScriptContext,
                        constructor.TypeName,
                        $"Type conversion ':{constructor.TypeName}' expects exactly one unlabeled argument",
                        errors);
                }

                return;
        }
    }

    private static void ValidateRefConstructor(
        ParsedScript parsedScriptContext,
        TypeConstructorExpressionNode constructor,
        IReadOnlyDictionary<string, TypeDefinitionNode> typeDefinitions,
        GesValidationErrors errors)
    {
        if (constructor.Arguments.Count == 1 && constructor.Arguments[0].Label is null)
        {
            return;
        }

        if (constructor.Arguments.Count != 2)
        {
            AddTypeConstructorError(
                parsedScriptContext,
                constructor.TypeName,
                "Type constructor ':ref' expects a target type and id",
                errors);
            return;
        }

        if (!TryGetRefConstructorArgumentIndexes(constructor.Arguments, out var typeIndex, out var idIndex))
        {
            AddTypeConstructorError(
                parsedScriptContext,
                constructor.TypeName,
                "Type constructor ':ref' expects arguments shaped as ':ref(:type, id: value)' or ':ref(type: :type, id: value)'",
                errors);
            return;
        }

        _ = idIndex;
        var typeExpression = constructor.Arguments[typeIndex].Expression;
        if (TryGetLiteralRefTargetTypeName(typeExpression, out var targetTypeName) &&
            !typeDefinitions.ContainsKey(targetTypeName))
        {
            AddTypeConstructorError(
                parsedScriptContext,
                constructor.TypeName,
                $"Type constructor ':ref' target ':{targetTypeName}' must be a record or external type",
                errors);
        }
    }

    private static bool TryGetRefConstructorArgumentIndexes(
        IReadOnlyList<ArgumentNode> arguments,
        out int typeIndex,
        out int idIndex)
    {
        typeIndex = -1;
        idIndex = -1;
        for (var index = 0; index < arguments.Count; index++)
        {
            var label = arguments[index].Label ?? GameEventScriptMessageSignature.UnlabeledParameterName;
            if (string.Equals(label, "type", StringComparison.Ordinal))
            {
                if (typeIndex >= 0)
                {
                    return false;
                }

                typeIndex = index;
                continue;
            }

            if (string.Equals(label, "id", StringComparison.Ordinal))
            {
                if (idIndex >= 0)
                {
                    return false;
                }

                idIndex = index;
                continue;
            }

            if (string.Equals(label, GameEventScriptMessageSignature.UnlabeledParameterName, StringComparison.Ordinal))
            {
                if (typeIndex < 0)
                {
                    typeIndex = index;
                    continue;
                }

                if (idIndex < 0)
                {
                    idIndex = index;
                    continue;
                }
            }

            return false;
        }

        return typeIndex >= 0 && idIndex >= 0 && typeIndex != idIndex;
    }

    private static bool TryGetLiteralRefTargetTypeName(ExpressionNode expression, out string typeName)
    {
        typeName = expression switch
        {
            TagLiteralExpressionNode tag => tag.Name,
            TextLiteralExpressionNode text => text.Value,
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(typeName))
        {
            typeName = string.Empty;
            return false;
        }

        typeName = typeName.Trim();
        if (typeName.StartsWith(":", StringComparison.Ordinal))
        {
            typeName = typeName[1..];
        }

        return typeName.Length > 0;
    }

    private static void ValidateVectorConstructor(
        ParsedScript parsedScriptContext,
        TypeConstructorExpressionNode constructor,
        IReadOnlyList<string> labels,
        GesValidationErrors errors)
    {
        if (constructor.Arguments.Count == 1 && constructor.Arguments[0].Label is null)
        {
            return;
        }

        if (constructor.Arguments.Count is 2 &&
            constructor.Arguments.All(argument => argument.Label is null))
        {
            return;
        }

        var labeledCount = constructor.Arguments.Count(argument => argument.Label is not null);
        if (labeledCount is not 0 && labeledCount != constructor.Arguments.Count)
        {
            AddTypeConstructorError(
                parsedScriptContext,
                constructor.TypeName,
                $"Type constructor ':{constructor.TypeName}' cannot mix labeled and unlabeled component arguments",
                errors);
            return;
        }

        if (labeledCount > 0 || constructor.Arguments.Count == 0)
        {
            ValidateLabeledVectorConstructor(parsedScriptContext, constructor, labels, errors);
            return;
        }

        if (constructor.Arguments.Count is < 1 or > 3)
        {
            AddTypeConstructorError(
                parsedScriptContext,
                constructor.TypeName,
                $"Type constructor ':{constructor.TypeName}' expects up to {labels.Count} component arguments",
                errors);
        }
    }

    private static void ValidateLabeledVectorConstructor(
        ParsedScript parsedScriptContext,
        TypeConstructorExpressionNode constructor,
        IReadOnlyList<string> labels,
        GesValidationErrors errors)
    {
        var previousIndex = -1;
        for (var argumentIndex = 0; argumentIndex < constructor.Arguments.Count; argumentIndex++)
        {
            var label = constructor.Arguments[argumentIndex].Label;
            var componentIndex = IndexOf(labels, label);
            if (componentIndex < 0)
            {
                AddTypeConstructorError(
                    parsedScriptContext,
                    constructor.TypeName,
                    $"Type constructor ':{constructor.TypeName}' has unknown component label '{label}'",
                    errors);
                return;
            }

            if (componentIndex <= previousIndex)
            {
                AddTypeConstructorError(
                    parsedScriptContext,
                    constructor.TypeName,
                    $"Type constructor ':{constructor.TypeName}' component labels must follow x, y, z order",
                    errors);
                return;
            }

            previousIndex = componentIndex;
        }
    }

    private static int IndexOf(IReadOnlyList<string> values, string? value)
    {
        for (var index = 0; index < values.Count; index++)
        {
            if (string.Equals(values[index], value, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool IsBuiltinConstructorType(string typeName)
        => typeName is "nothing" or "tag" or "text" or "percentage" or "degree" or "meter" or "second" or
            "vector" or "point" or "boolean" or "integer" or "float" or "number" or "uuid" or "series" or
            "list" or "range" or "message" or "handler" or "envelope" or "ref" or "map" or "set" or "dice" ||
            GameEventScriptNumericUnits.TryParseQuantityTypeName(typeName, out _);

    private static void ValidateRemovedOptionalType(
        ParsedScript parsedScriptContext,
        string typeName,
        ScriptNode sourceNode,
        GesValidationErrors errors)
    {
        if (!string.Equals(typeName, "optional", StringComparison.Ordinal))
        {
            return;
        }

        errors.Add(
            parsedScriptContext,
            "Type ':optional' has been removed; use ':nothing' to represent absence.",
            typeName,
            GameEventScriptSymbolKind.Type,
            GameEventScriptCompileErrorKind.InvalidTypeConstructor,
            sourceNode);
    }

    private static void AddTypeConstructorError(
        ParsedScript parsedScriptContext,
        string typeName,
        string message,
        GesValidationErrors errors)
        => errors.Add(
            parsedScriptContext,
            message,
            typeName,
            GameEventScriptSymbolKind.Type,
            GameEventScriptCompileErrorKind.InvalidTypeConstructor);

    private static void ValidateCallArity(
        ParsedScript parsedScriptContext,
        GameEventScriptCallableKind kind,
        string name,
        int expectedCount,
        int actualCount,
        GesValidationErrors errors)
    {
        if (expectedCount != actualCount)
        {
            errors.Add(
                parsedScriptContext,
                $"{kind} '{name}' expects {expectedCount} argument(s) but received {actualCount}",
                name,
                kind == GameEventScriptCallableKind.PredicateCall ? GameEventScriptSymbolKind.Predicate : GameEventScriptSymbolKind.Function,
                kind == GameEventScriptCallableKind.PredicateCall ? GameEventScriptCompileErrorKind.WrongPredicateArity : GameEventScriptCompileErrorKind.WrongFunctionArity);
        }
    }

    private static void ValidateDuplicateNamedArguments(
        ParsedScript parsedScriptContext,
        string symbolName,
        IReadOnlyList<ArgumentNode> arguments,
        GesValidationErrors errors)
    {
        var duplicateArguments = arguments
            .Where(argument => argument.Label is not null)
            .GroupBy(argument => argument.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);

        foreach (var duplicateArgument in duplicateArguments)
        {
            var duplicateArgumentNode = arguments.Last(argument => string.Equals(argument.Name, duplicateArgument, StringComparison.Ordinal));
            errors.Add(
                parsedScriptContext,
                $"Named argument '{duplicateArgument}' is declared more than once",
                symbolName,
                GameEventScriptSymbolKind.Handler,
                GameEventScriptCompileErrorKind.DuplicatePublishArgument,
                duplicateArgumentNode);
        }
    }

    private static void ValidateDuplicateHandlerLiteralParameters(
        ParsedScript parsedScriptContext,
        HandlerLiteralExpressionNode handlerLiteral,
        GesValidationErrors errors)
    {
        var duplicateParameters = handlerLiteral.Parameters
            .GroupBy(parameter => parameter, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);

        foreach (var duplicateParameter in duplicateParameters)
        {
            var duplicateParameterNode = handlerLiteral.ParameterList.Last(parameter => string.Equals(parameter.LocalName, duplicateParameter, StringComparison.Ordinal));
            errors.Add(
                parsedScriptContext,
                $"Handler literal '{handlerLiteral.Message}' declares parameter '{duplicateParameter}' more than once",
                handlerLiteral.Message,
                GameEventScriptSymbolKind.Handler,
                GameEventScriptCompileErrorKind.DuplicateHandlerParameter,
                duplicateParameterNode);
        }
    }

    private static void ValidateIdentifierCase(
        ParsedScript parsedScriptContext,
        string name,
        string symbol,
        GameEventScriptSymbolKind symbolKind,
        string message,
        GesValidationErrors errors)
    {
        if (IsIdentifierCase(name))
        {
            return;
        }

        errors.Add(
            parsedScriptContext,
            message,
            symbol,
            symbolKind,
            GameEventScriptCompileErrorKind.InvalidIdentifierCase);
    }

    private static void ValidateMessageCase(
        ParsedScript parsedScriptContext,
        string name,
        string symbol,
        GameEventScriptSymbolKind symbolKind,
        string message,
        GesValidationErrors errors)
    {
        if (IsMessageCase(name))
        {
            return;
        }

        errors.Add(
            parsedScriptContext,
            message,
            symbol,
            symbolKind,
            GameEventScriptCompileErrorKind.InvalidMessageCase);
    }

    private static bool IsIdentifierCase(string name)
    {
        if (string.IsNullOrEmpty(name) || !char.IsLower(name[0]))
        {
            return false;
        }

        var suffixStart = name.LastIndexOf('_');
        var letterEndExclusive = suffixStart < 0 ? name.Length : suffixStart;
        for (var i = 1; i < letterEndExclusive; i++)
        {
            if (!char.IsLetter(name[i]))
            {
                return false;
            }
        }

        if (suffixStart < 0)
        {
            return true;
        }

        return suffixStart > 0 &&
               TryValidateNumericSuffix(name.AsSpan(suffixStart + 1));
    }

    private static bool IsMessageCase(string name)
    {
        if (string.IsNullOrEmpty(name) || !char.IsUpper(name[0]))
        {
            return false;
        }

        for (var i = 1; i < name.Length; i++)
        {
            if (!char.IsLetter(name[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryValidateNumericSuffix(ReadOnlySpan<char> suffix)
    {
        if (suffix.Length == 0)
        {
            return false;
        }

        if (suffix[0] == '0')
        {
            return suffix.Length == 1;
        }

        if (suffix[0] is < '1' or > '9')
        {
            return false;
        }

        for (var index = 1; index < suffix.Length; index++)
        {
            if (!char.IsDigit(suffix[index]))
            {
                return false;
            }
        }

        return true;
    }
}
