// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;

namespace StepH.GameEventScript.Compiler;

internal static class GesAstValidator
{
    private sealed class ValidationScope
    {
        private readonly HashSet<string> _variables;
        private readonly Dictionary<string, string> _declaredTypes;

        public ValidationScope()
        {
            _variables = new HashSet<string>(StringComparer.Ordinal);
            _declaredTypes = new Dictionary<string, string>(StringComparer.Ordinal);
        }

        private ValidationScope(ValidationScope parent)
        {
            _variables = new HashSet<string>(StringComparer.Ordinal);
            _declaredTypes = new Dictionary<string, string>(parent._declaredTypes, StringComparer.Ordinal);
        }

        public static ValidationScope Create(IReadOnlyList<ParameterNode> parameters)
        {
            var scope = new ValidationScope();
            for (var index = 0; index < parameters.Count; index++)
            {
                var parameter = parameters[index];
                scope._variables.Add(parameter.LocalName);
                scope.DeclareType(parameter.LocalName, parameter.DeclaredType);
            }

            return scope;
        }

        public static ValidationScope CreateChild(ValidationScope parent)
            => new(parent);

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
        for (var typeDefinitionIndex = 0; typeDefinitionIndex < parsedScript.TypeDefinitions.Count; typeDefinitionIndex++)
        {
            var typeDefinition = parsedScript.TypeDefinitions[typeDefinitionIndex];
            ValidateRemovedType(parsedScript, typeDefinition.Name, typeDefinition, errors);

            for (var fieldIndex = 0; fieldIndex < typeDefinition.Fields.Count; fieldIndex++)
            {
                var field = typeDefinition.Fields[fieldIndex];
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

        for (var predicateIndex = 0; predicateIndex < parsedScript.PredicateDefinitions.Count; predicateIndex++)
        {
            var predicateDefinition = parsedScript.PredicateDefinitions[predicateIndex];
            ValidateIdentifierCase(
                    parsedScript,
                    predicateDefinition.Name,
                    predicateDefinition.Name,
                    GameEventScriptSymbolKind.Predicate,
                    "Predicate names must use identifier casing (start lowercase, letters only, optional final _index suffix)",
                    errors);

            for (var parameterIndex = 0; parameterIndex < predicateDefinition.Parameters.Count; parameterIndex++)
            {
                var parameter = predicateDefinition.Parameters[parameterIndex];
                ValidateIdentifierCase(
                    parsedScript,
                    parameter,
                    predicateDefinition.Name,
                    GameEventScriptSymbolKind.Predicate,
                    $"Predicate '{predicateDefinition.Name}' declares an invalid parameter name '{parameter}'",
                    errors);
            }

            ValidateParameterTypeHints(parsedScript, "Predicate", predicateDefinition.Name, predicateDefinition.ParameterList, typeDefinitions, errors);
            ValidateDuplicateParameterDefinitions(
                parsedScript,
                predicateDefinition.Name,
                GameEventScriptSymbolKind.Predicate,
                GameEventScriptDiagnosticCodes.ValidateDuplicateDefinitionParameter,
                "Predicate",
                predicateDefinition.ParameterList,
                errors);

            ValidateExpressionReferences(
                parsedScript,
                predicateDefinition.Expression,
                callables,
                typeDefinitions,
                errors,
                BuildDeclaredTypeMap(predicateDefinition.ParameterList));
            ValidatePredicateResultExpression(parsedScript, predicateDefinition, callables, typeDefinitions, errors);
        }

        for (var functionIndex = 0; functionIndex < parsedScript.FunctionDefinitions.Count; functionIndex++)
        {
            var functionDefinition = parsedScript.FunctionDefinitions[functionIndex];
            ValidateIdentifierCase(
                    parsedScript,
                    functionDefinition.Name,
                    functionDefinition.Name,
                    GameEventScriptSymbolKind.Function,
                    "Function names must use identifier casing (start lowercase, letters only, optional final _index suffix)",
                    errors);

            for (var parameterIndex = 0; parameterIndex < functionDefinition.Parameters.Count; parameterIndex++)
            {
                var parameter = functionDefinition.Parameters[parameterIndex];
                ValidateIdentifierCase(
                    parsedScript,
                    parameter,
                    functionDefinition.Name,
                    GameEventScriptSymbolKind.Function,
                    $"Function '{functionDefinition.Name}' declares an invalid parameter name '{parameter}'",
                    errors);
            }

            ValidateParameterTypeHints(parsedScript, "Function", functionDefinition.Name, functionDefinition.ParameterList, typeDefinitions, errors);
            ValidateDuplicateParameterDefinitions(
                parsedScript,
                functionDefinition.Name,
                GameEventScriptSymbolKind.Function,
                GameEventScriptDiagnosticCodes.ValidateDuplicateDefinitionParameter,
                "Function",
                functionDefinition.ParameterList,
                errors);

            ValidateExpressionReferences(
                parsedScript,
                functionDefinition.Expression,
                callables,
                typeDefinitions,
                errors,
                BuildDeclaredTypeMap(functionDefinition.ParameterList));
        }

        for (var handlerIndex = 0; handlerIndex < parsedScript.Handlers.Count; handlerIndex++)
        {
            var handler = parsedScript.Handlers[handlerIndex];
            if (GameEventScriptSystemEndpoints.IsInitializationName(handler.Message))
            {
                ValidateInitializationHandler(parsedScript, handler, errors);
            }
            else if (GameEventScriptSystemEndpoints.IsUndeliverableName(handler.Message))
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
                ValidateMessageNameHandler(parsedScript, handler, errors);
            }

            for (var parameterIndex = 0; parameterIndex < handler.Parameters.Count; parameterIndex++)
            {
                var parameter = handler.Parameters[parameterIndex];
                ValidateIdentifierCase(
                    parsedScript,
                    parameter,
                    handler.Message,
                    GameEventScriptSymbolKind.Handler,
                    $"Handler '{handler.Message}' declares an invalid parameter name '{parameter}'",
                    errors);
            }

            ValidateParameterTypeHints(parsedScript, "Handler", handler.Message, handler.ParameterList, typeDefinitions, errors);
            ValidateDuplicateParameterDefinitions(
                parsedScript,
                handler.Message,
                GameEventScriptSymbolKind.Handler,
                GameEventScriptDiagnosticCodes.ValidateDuplicateHandlerParameter,
                "Handler",
                handler.ParameterList,
                errors);

            var handlerScope = ValidationScope.Create(handler.ParameterList);
            for (var statementIndex = 0; statementIndex < handler.Statements.Count; statementIndex++)
            {
                var statement = handler.Statements[statementIndex];
                ValidateStatementReferences(parsedScript, statement, callables, typeDefinitions, errors, handlerScope);
            }
        }

        GesShadowingValidator.Validate(parsedScript, errors);
    }

    private static void ValidateInitializationHandler(ParsedScript parsedScript, EventHandlerNode handler, GesValidationErrors errors)
    {
        if (handler.DispatchKind != EventHandlerDispatchKind.ExactSignature ||
            handler.ParameterList.Count != 0)
        {
            errors.Add(
                parsedScript,
                "System endpoint 'initialization' must use parameterless syntax",
                handler.Message,
                GameEventScriptSymbolKind.Handler,
                GameEventScriptDiagnosticCodes.ValidateInvalidMessageCase,
                handler);
            return;
        }

        if (handler.MatchingTags.Count != 0 || handler.WithoutTags.Count != 0)
        {
            errors.Add(
                parsedScript,
                "System endpoint 'initialization' cannot use tag filters",
                handler.Message,
                GameEventScriptSymbolKind.Handler,
                GameEventScriptDiagnosticCodes.ValidateInvalidMessageCase,
                handler);
        }
    }

    private static void ValidateUndeliverableHandler(ParsedScript parsedScript, EventHandlerNode handler, GesValidationErrors errors)
    {
        if (handler.DispatchKind != EventHandlerDispatchKind.MessageName)
        {
            errors.Add(
                parsedScript,
                "System endpoint 'undeliverable' must use 'as message' syntax",
                handler.Message,
                GameEventScriptSymbolKind.Handler,
                GameEventScriptDiagnosticCodes.ValidateInvalidMessageCase,
                handler);
            return;
        }

        var parameter = handler.ParameterList[0];
        if (!string.Equals(parameter.SignatureLabel, GameEventScriptSystemEndpoints.MessageArgumentName, StringComparison.Ordinal) ||
            !string.Equals(parameter.DeclaredType, "message", StringComparison.Ordinal))
        {
            errors.Add(
                parsedScript,
                "System endpoint 'undeliverable' must bind a ':message' value",
                parameter.LocalName,
                GameEventScriptSymbolKind.Handler,
                GameEventScriptDiagnosticCodes.ValidateInvalidMessageCase,
                parameter);
        }
    }

    private static void ValidateMessageNameHandler(ParsedScript parsedScript, EventHandlerNode handler, GesValidationErrors errors)
    {
        if (handler.DispatchKind != EventHandlerDispatchKind.MessageName)
        {
            return;
        }

        if (handler.ParameterList.Count != 1)
        {
            errors.Add(
                parsedScript,
                "Message-name handlers expect exactly one message parameter",
                handler.Message,
                GameEventScriptSymbolKind.Handler,
                GameEventScriptDiagnosticCodes.ValidateInvalidMessageCase,
                handler);
            return;
        }

        var parameter = handler.ParameterList[0];
        if (!string.Equals(parameter.SignatureLabel, GameEventScriptSystemEndpoints.MessageArgumentName, StringComparison.Ordinal) ||
            !string.Equals(parameter.DeclaredType, "message", StringComparison.Ordinal))
        {
            errors.Add(
                parsedScript,
                "Message-name handlers must bind a ':message' value",
                handler.Message,
                GameEventScriptSymbolKind.Handler,
                GameEventScriptDiagnosticCodes.ValidateInvalidMessageCase,
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
                for (var tagIndex = 0; tagIndex < publish.TagExpressions.Count; tagIndex++)
                {
                    var tagExpression = publish.TagExpressions[tagIndex];
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
                        GameEventScriptDiagnosticCodes.ValidateDuplicateVariable,
                        let);
                    return;
                }

                scope.Declare(let.Identifier, let.DeclaredType ?? InferImmutableBindingType(let.Expression));
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
                ValidateSeedExpression(parsedScriptContext, seededRandom.SeedExpression, callables, typeDefinitions, errors, scope.DeclaredTypes);
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
        for (var index = 0; index < parameters.Count; index++)
        {
            var parameter = parameters[index];
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
                GameEventScriptDiagnosticCodes.ValidateInvalidTypeConstructor,
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
        for (var index = 0; index < body.Statements.Count; index++)
        {
            var nested = body.Statements[index];
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
            $"Predicate '{predicateDefinition.Name}' must return :boolean or nothing; use 'as :boolean' for explicit boolean coercion.",
            predicateDefinition.Name,
            GameEventScriptSymbolKind.Predicate,
            GameEventScriptDiagnosticCodes.ValidateInvalidPredicate,
            predicateDefinition.Expression);
    }

    private static IReadOnlyDictionary<string, string> BuildDeclaredTypeMap(IReadOnlyList<ParameterNode> parameters)
    {
        var types = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < parameters.Count; index++)
        {
            var parameter = parameters[index];
            if (!string.IsNullOrWhiteSpace(parameter.DeclaredType))
            {
                types[parameter.LocalName] = parameter.DeclaredType!;
            }
        }

        return types;
    }

    private static TypeFieldDefinitionNode? FindField(IReadOnlyList<TypeFieldDefinitionNode> fields, string name)
    {
        for (var index = 0; index < fields.Count; index++)
        {
            if (string.Equals(fields[index].Name, name, StringComparison.Ordinal))
            {
                return fields[index];
            }
        }

        return null;
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

            case NothingLiteralExpressionNode:
                return StaticExpressionInfo.Nothing;

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

            case IntrinsicCallExpressionNode intrinsic:
                return intrinsic.Function is GesIntrinsicFunction.Normalize ||
                    intrinsic.Function is GesIntrinsicFunction.Cross && intrinsic.Arguments.Count is 2 or 6
                        ? StaticExpressionInfo.Other("vector")
                        : StaticExpressionInfo.Other("number");

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

    private static string? InferImmutableBindingType(ExpressionNode expression)
    {
        if (expression is IntegerLiteralExpressionNode)
        {
            return "integer";
        }

        if (expression is FloatLiteralExpressionNode floatingPoint && GameEventScriptNumber.CanRepresentAsInteger(floatingPoint.Value))
        {
            return "integer";
        }

        return null;
    }

    private static void ValidateSeedExpression(
        ParsedScript parsedScriptContext,
        ExpressionNode seedExpression,
        IReadOnlyDictionary<string, GesCallableDefinition> callables,
        IReadOnlyDictionary<string, TypeDefinitionNode> typeDefinitions,
        GesValidationErrors errors,
        IReadOnlyDictionary<string, string> declaredTypes)
    {
        var result = ClassifyExpression(seedExpression, callables, typeDefinitions, declaredTypes, new HashSet<string>(StringComparer.Ordinal));
        if (result.TypeName is "integer" ||
            HasExplicitNumberSeedConversion(seedExpression, declaredTypes) ||
            seedExpression is FloatLiteralExpressionNode floatingPoint && GameEventScriptNumber.CanRepresentAsInteger(floatingPoint.Value))
        {
            return;
        }

        errors.Add(
            parsedScriptContext,
            "A random seed must be statically known as a unitless integer or explicitly converted with 'as :number'; at runtime the result is used as a seed only when it is an exact unitless signed 64-bit integer.",
            "number",
            GameEventScriptSymbolKind.Type,
            GameEventScriptDiagnosticCodes.ValidateInvalidTypeConstructor,
            seedExpression);
    }

    private static bool HasExplicitNumberSeedConversion(ExpressionNode expression, IReadOnlyDictionary<string, string> declaredTypes)
    {
        return expression switch
        {
            TypeCastExpressionNode { TypeName: "number" } => true,
            TypeConstructorExpressionNode { TypeName: "number" } => true,
            IdentifierExpressionNode identifier => declaredTypes.TryGetValue(identifier.Name, out var declaredType) && declaredType == "number",
            UnaryExpressionNode { Operator: GesUnaryOperator.Negate } unary => HasExplicitNumberSeedConversion(unary.Operand, declaredTypes),
            _ => false
        };
    }

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

        var field = FindField(typeDefinition.Fields, memberAccess.Member);
        return field is null ? StaticExpressionInfo.Unknown : FromDeclaredType(field.TypeName);
    }

    private static StaticExpressionInfo ClassifyCollectionAccess(CollectionAccessExpressionNode collectionAccess)
        => collectionAccess.Selector switch
        {
            PredicateSelectorNode => StaticExpressionInfo.Boolean,
            ContainsSelectorNode => StaticExpressionInfo.Boolean,
            _ => StaticExpressionInfo.Unknown
        };

    private static StaticExpressionInfo ClassifyCall(CallExpressionNode call, IReadOnlyDictionary<string, GesCallableDefinition> callables, IReadOnlyDictionary<string, TypeDefinitionNode> typeDefinitions, ISet<string> visitedCallables)
    {
        var callable = GesCallableSignatures.Resolve(callables, call);
        if (callable is null)
        {
            return StaticExpressionInfo.Unknown;
        }

        if (callable.Kind == GameEventScriptCallableKind.PredicateCall)
        {
            return StaticExpressionInfo.Boolean;
        }

        var callableKey = $"{callable.Kind}:{callable.SignatureId}";
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
            GesUnaryOperator.Abs or GesUnaryOperator.NaturalLog or GesUnaryOperator.Exp or
                GesUnaryOperator.Floor or GesUnaryOperator.Ceil or GesUnaryOperator.Truncate or
                GesUnaryOperator.RoundHalfEven or GesUnaryOperator.RoundHalfUp or GesUnaryOperator.RoundHalfDown or
                GesUnaryOperator.DegreeToRadians or GesUnaryOperator.DegreeFromRadians or GesUnaryOperator.WrapDegree or
                GesUnaryOperator.Sin or GesUnaryOperator.Cos or GesUnaryOperator.Tan or
                GesUnaryOperator.Asin or GesUnaryOperator.Acos or GesUnaryOperator.Atan => StaticExpressionInfo.Other("number"),
            GesUnaryOperator.Negate => ClassifyExpression(unary.Operand, callables, typeDefinitions, declaredTypes, visitedCallables),
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
        for (var index = 0; index < guardedChoice.Branches.Count; index++)
        {
            var branch = guardedChoice.Branches[index];
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
                case NothingLiteralExpressionNode:
                    return;

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
                    for (var argumentIndex = 0; argumentIndex < call.ArgumentList.Arguments.Count; argumentIndex++)
                    {
                        var argument = call.ArgumentList.Arguments[argumentIndex];
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
                    for (var parameterIndex = 0; parameterIndex < handlerLiteral.Parameters.Count; parameterIndex++)
                    {
                        var parameter = handlerLiteral.Parameters[parameterIndex];
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
                    for (var argumentIndex = 0; argumentIndex < messageLiteral.Arguments.Count; argumentIndex++)
                    {
                        var argument = messageLiteral.Arguments[argumentIndex];
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
                    for (var argumentIndex = 0; argumentIndex < extensionCall.Arguments.Count; argumentIndex++)
                    {
                        var argument = extensionCall.Arguments[argumentIndex];
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
                    if (GesCallableSignatures.ResolveSingleParameterPredicate(callables, predicateCall.PredicateName) is null)
                    {
                        errors.Add(
                            parsedScriptContext,
                            $"Predicate '{predicateCall.PredicateName}' must exist and declare exactly one parameter to be used with 'is'",
                            predicateCall.PredicateName,
                            GameEventScriptSymbolKind.Predicate,
                            GameEventScriptDiagnosticCodes.ValidateInvalidPredicate);
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
                    for (var argumentIndex = 0; argumentIndex < variadic.Arguments.Count; argumentIndex++)
                    {
                        var argument = variadic.Arguments[argumentIndex];
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
                    ValidateSeedExpression(parsedScriptContext, seededRandom.SeedExpression, callables, typeDefinitions, errors, declaredTypes);
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
                    for (var branchIndex = 0; branchIndex < guardedChoice.Branches.Count; branchIndex++)
                    {
                        var branch = guardedChoice.Branches[branchIndex];
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
                    ValidateRemovedType(parsedScriptContext, typeCheck.TypeName, typeCheck, errors);
                    expression = typeCheck.Value;
                    continue;

                case TypeCastExpressionNode typeCast:
                    ValidateRemovedType(parsedScriptContext, typeCast.TypeName, typeCast, errors);
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
                    for (var itemIndex = 0; itemIndex < list.Items.Count; itemIndex++)
                    {
                        var item = list.Items[itemIndex];
                        ValidateExpressionReferences(parsedScriptContext, item, callables, typeDefinitions, errors, declaredTypes);
                    }

                    return;

                case MapLiteralExpressionNode dictionary:
                    for (var entryIndex = 0; entryIndex < dictionary.Entries.Count; entryIndex++)
                    {
                        var entry = dictionary.Entries[entryIndex];
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
        var callable = GesCallableSignatures.Resolve(callables, call);
        if (callable is null)
        {
            if (GesCallableSignatures.HasName(callables, call.Name))
            {
                var callableKind = FindCallableKind(callables, call.Name);
                errors.Add(
                    parsedScriptContext,
                    $"No overload of '{call.Name}' matches signature '{GesCallableSignatures.Create(call.Name, call.ArgumentList.Arguments)}'",
                    call.Name,
                    callableKind == GameEventScriptCallableKind.PredicateCall ? GameEventScriptSymbolKind.Predicate : GameEventScriptSymbolKind.Function,
                    callableKind == GameEventScriptCallableKind.PredicateCall ? GameEventScriptDiagnosticCodes.ValidateWrongPredicateArity : GameEventScriptDiagnosticCodes.ValidateWrongFunctionArity);
            }
            else if (AllArgumentsUnlabeled(call.ArgumentList.Arguments))
            {
                errors.Add(
                    parsedScriptContext,
                    $"No predicate or function named '{call.Name}' exists",
                    call.Name,
                    GameEventScriptSymbolKind.GlobalDefinition,
                    GameEventScriptDiagnosticCodes.ValidateMissingCallable);
            }

            return;
        }

        ValidateCallArity(parsedScriptContext, callable.Kind, call.Name, callable.Parameters.Count, call.Arguments.Count, errors);
        ValidateCallLabels(parsedScriptContext, callable.Kind, call.Name, callable.SignatureLabels, call.ArgumentList.Arguments, errors);
    }

    private static GameEventScriptCallableKind FindCallableKind(IReadOnlyDictionary<string, GesCallableDefinition> callables, string name)
    {
        foreach (var callable in callables.Values)
            if (string.Equals(callable.Name, name, StringComparison.Ordinal)) return callable.Kind;
        return GameEventScriptCallableKind.FunctionCall;
    }

    private static void ValidateCallLabels(ParsedScript parsedScriptContext, GameEventScriptCallableKind kind, string name, IReadOnlyList<string> expectedLabels, IReadOnlyList<ArgumentNode> arguments, GesValidationErrors errors)
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
                kind == GameEventScriptCallableKind.PredicateCall ? GameEventScriptDiagnosticCodes.ValidateWrongPredicateArity : GameEventScriptDiagnosticCodes.ValidateWrongFunctionArity);
        }
    }

    private static bool AllArgumentsUnlabeled(IReadOnlyList<ArgumentNode> arguments)
    {
        for (var index = 0; index < arguments.Count; index++)
        {
            if (arguments[index].Label is not null)
            {
                return false;
            }
        }

        return true;
    }

    private static int CountUnlabeledArguments(IReadOnlyList<ArgumentNode> arguments)
    {
        var count = 0;
        for (var index = 0; index < arguments.Count; index++)
        {
            if (arguments[index].Label is null)
            {
                count++;
            }
        }

        return count;
    }

    private static int CountLabeledArguments(IReadOnlyList<ArgumentNode> arguments)
    {
        var count = 0;
        for (var index = 0; index < arguments.Count; index++)
        {
            if (arguments[index].Label is not null)
            {
                count++;
            }
        }

        return count;
    }

    private static TypeFieldDefinitionNode[] ReadConstructorFields(IReadOnlyList<TypeFieldDefinitionNode> fields)
    {
        var count = 0;
        for (var index = 0; index < fields.Count; index++)
        {
            if (fields[index].IsConstructorParameter)
            {
                count++;
            }
        }

        if (count == 0)
        {
            return [];
        }

        var constructorFields = new TypeFieldDefinitionNode[count];
        var targetIndex = 0;
        for (var index = 0; index < fields.Count; index++)
        {
            var field = fields[index];
            if (field.IsConstructorParameter)
            {
                constructorFields[targetIndex++] = field;
            }
        }

        return constructorFields;
    }

    private static int CountUnlabeledConstructorFields(IReadOnlyList<TypeFieldDefinitionNode> fields)
    {
        var count = 0;
        for (var index = 0; index < fields.Count; index++)
        {
            if (string.Equals(fields[index].ConstructorLabel, GameEventScriptMessageSignature.UnlabeledParameterName, StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }

    private static HashSet<string> BuildLabeledConstructorFieldSet(IReadOnlyList<TypeFieldDefinitionNode> fields)
    {
        var fieldNames = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < fields.Count; index++)
        {
            var label = fields[index].ConstructorLabel;
            if (label is not null &&
                !string.Equals(label, GameEventScriptMessageSignature.UnlabeledParameterName, StringComparison.Ordinal))
            {
                fieldNames.Add(label);
            }
        }

        return fieldNames;
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
        for (var argumentIndex = 0; argumentIndex < constructor.Arguments.Count; argumentIndex++)
        {
            var argument = constructor.Arguments[argumentIndex];
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

        if (constructor.TypeName is "optional" or "set" or "uuid" or "ref")
        {
            ValidateRemovedType(parsedScriptContext, constructor.TypeName, constructor, errors);
            return;
        }

        if (IsBuiltinConstructorType(constructor.TypeName))
        {
            ValidateBuiltinTypeConstructor(parsedScriptContext, constructor, errors);
            return;
        }

        if (!typeDefinitions.TryGetValue(constructor.TypeName, out var typeDefinition))
        {
            AddTypeConstructorError(parsedScriptContext, constructor.TypeName, $"Unknown type constructor ':{constructor.TypeName}'", errors);
            return;
        }

        var constructorFields = ReadConstructorFields(typeDefinition.Fields);
        var unlabeledConstructorFieldCount = CountUnlabeledConstructorFields(constructorFields);
        var unlabeledArgumentCount = CountUnlabeledArguments(constructor.Arguments);
        if (unlabeledArgumentCount > unlabeledConstructorFieldCount)
        {
            AddTypeConstructorError(parsedScriptContext, constructor.TypeName, $"Custom type constructor ':{constructor.TypeName}' requires labeled field arguments", errors);
        }

        var fieldNames = BuildLabeledConstructorFieldSet(constructorFields);
        for (var argumentIndex = 0; argumentIndex < constructor.Arguments.Count; argumentIndex++)
        {
            var argument = constructor.Arguments[argumentIndex];
            if (argument.Label is null)
            {
                continue;
            }

            if (!fieldNames.Contains(argument.Label))
            {
                AddTypeConstructorError(
                    parsedScriptContext,
                    constructor.TypeName,
                    $"Custom type constructor ':{constructor.TypeName}' has unknown constructor field '{argument.Label}'",
                    errors);
            }
        }
    }

    private static void ValidateBuiltinTypeConstructor(ParsedScript parsedScriptContext, TypeConstructorExpressionNode constructor, GesValidationErrors errors)
    {
        switch (constructor.TypeName)
        {
            case "vector":
            case "point":
                ValidateVectorConstructor(parsedScriptContext, constructor, ["x", "y", "z"], errors);
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

    private static void ValidateVectorConstructor(ParsedScript parsedScriptContext, TypeConstructorExpressionNode constructor, IReadOnlyList<string> labels, GesValidationErrors errors)
    {
        if (constructor.Arguments.Count == 1 && constructor.Arguments[0].Label is null)
        {
            return;
        }

        if (constructor.Arguments.Count is 2 &&
            AllArgumentsUnlabeled(constructor.Arguments))
        {
            return;
        }

        var labeledCount = CountLabeledArguments(constructor.Arguments);
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

    private static void ValidateLabeledVectorConstructor(ParsedScript parsedScriptContext, TypeConstructorExpressionNode constructor, IReadOnlyList<string> labels, GesValidationErrors errors)
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
        => typeName is "nothing" or "tag" or "text" or "percentage" or
            "vector" or "point" or "boolean" or "number" or "numeric" or "series" or
            "list" or "range" or "message" or "handler" or "map" or "dice" ||
            GameEventScriptBytecodeInstructionUnits.ParseQuantityTypeName(typeName) is not null;

    private static void ValidateRemovedType(ParsedScript parsedScriptContext, string typeName, ScriptNode sourceNode, GesValidationErrors errors)
    {
        if (string.Equals(typeName, "optional", StringComparison.Ordinal))
        {
            errors.Add(
                parsedScriptContext,
                "Type ':optional' has been removed; use nothing to represent absence.",
                typeName,
                GameEventScriptSymbolKind.Type,
                GameEventScriptDiagnosticCodes.ValidateInvalidTypeConstructor,
                sourceNode);
            return;
        }

        if (string.Equals(typeName, "set", StringComparison.Ordinal))
        {
            errors.Add(
                parsedScriptContext,
                "Type ':set' has been removed; use lists or key-only maps.",
                typeName,
                GameEventScriptSymbolKind.Type,
                GameEventScriptDiagnosticCodes.ValidateInvalidTypeConstructor,
                sourceNode);
            return;
        }

        if (string.Equals(typeName, "uuid", StringComparison.Ordinal))
        {
            errors.Add(
                parsedScriptContext,
                "Type ':uuid' has been removed; use ':text' or ':integer' ids in scripts.",
                typeName,
                GameEventScriptSymbolKind.Type,
                GameEventScriptDiagnosticCodes.ValidateInvalidTypeConstructor,
                sourceNode);
            return;
        }

        if (string.Equals(typeName, "ref", StringComparison.Ordinal))
        {
            errors.Add(
                parsedScriptContext,
                "Type ':ref' has been removed; future mutation handles will be table/agent based.",
                typeName,
                GameEventScriptSymbolKind.Type,
                GameEventScriptDiagnosticCodes.ValidateInvalidTypeConstructor,
                sourceNode);
        }
    }

    private static void AddTypeConstructorError(ParsedScript parsedScriptContext, string typeName, string message, GesValidationErrors errors)
        => errors.Add(
            parsedScriptContext,
            message,
            typeName,
            GameEventScriptSymbolKind.Type,
            GameEventScriptDiagnosticCodes.ValidateInvalidTypeConstructor);

    private static void ValidateCallArity(ParsedScript parsedScriptContext, GameEventScriptCallableKind kind, string name, int expectedCount, int actualCount, GesValidationErrors errors)
    {
        if (expectedCount != actualCount)
        {
            errors.Add(
                parsedScriptContext,
                $"{kind} '{name}' expects {expectedCount} argument(s) but received {actualCount}",
                name,
                kind == GameEventScriptCallableKind.PredicateCall ? GameEventScriptSymbolKind.Predicate : GameEventScriptSymbolKind.Function,
                kind == GameEventScriptCallableKind.PredicateCall ? GameEventScriptDiagnosticCodes.ValidateWrongPredicateArity : GameEventScriptDiagnosticCodes.ValidateWrongFunctionArity);
        }
    }

    private static void ValidateDuplicateNamedArguments(ParsedScript parsedScriptContext, string symbolName, IReadOnlyList<ArgumentNode> arguments, GesValidationErrors errors)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var reported = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];
            if (argument.Label is null || seen.Add(argument.Name) || !reported.Add(argument.Name))
            {
                continue;
            }

            errors.Add(
                parsedScriptContext,
                $"Named argument '{argument.Name}' is declared more than once",
                symbolName,
                GameEventScriptSymbolKind.Handler,
                GameEventScriptDiagnosticCodes.ValidateDuplicatePublishArgument,
                argument);
        }
    }

    private static void ValidateDuplicateHandlerLiteralParameters(ParsedScript parsedScriptContext, HandlerLiteralExpressionNode handlerLiteral, GesValidationErrors errors)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var reported = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < handlerLiteral.ParameterList.Count; index++)
        {
            var parameter = handlerLiteral.ParameterList[index];
            if (seen.Add(parameter.LocalName) || !reported.Add(parameter.LocalName))
            {
                continue;
            }

            errors.Add(
                parsedScriptContext,
                $"Handler literal '{handlerLiteral.Message}' declares parameter '{parameter.LocalName}' more than once",
                handlerLiteral.Message,
                GameEventScriptSymbolKind.Handler,
                GameEventScriptDiagnosticCodes.ValidateDuplicateHandlerParameter,
                parameter);
        }
    }

    private static void ValidateDuplicateParameterDefinitions(
        ParsedScript parsedScriptContext,
        string declarationName,
        GameEventScriptSymbolKind symbolKind,
        string errorCode,
        string declarationKind,
        IReadOnlyList<ParameterNode> parameters,
        GesValidationErrors errors)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var reported = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < parameters.Count; index++)
        {
            var parameter = parameters[index];
            if (seen.Add(parameter.LocalName) || !reported.Add(parameter.LocalName))
            {
                continue;
            }

            errors.Add(
                parsedScriptContext,
                $"{declarationKind} '{declarationName}' declares parameter '{parameter.LocalName}' more than once",
                declarationName,
                symbolKind,
                errorCode,
                parameter);
        }
    }

    private static void ValidateIdentifierCase(ParsedScript parsedScriptContext, string name, string symbol, GameEventScriptSymbolKind symbolKind, string message, GesValidationErrors errors)
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
            GameEventScriptDiagnosticCodes.ValidateInvalidIdentifierCase);
    }

    private static void ValidateMessageCase(ParsedScript parsedScriptContext, string name, string symbol, GameEventScriptSymbolKind symbolKind, string message, GesValidationErrors errors)
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
            GameEventScriptDiagnosticCodes.ValidateInvalidMessageCase);
    }

    private static bool IsIdentifierCase(string name)
        => GameEventScriptText.IsIdentifier(name);

    private static bool IsMessageCase(string name)
    {
        if (string.IsNullOrEmpty(name) || !GameEventScriptText.IsAsciiUpper(name[0])) return false;
        for (var index = 1; index < name.Length; index++)
            if (!GameEventScriptText.IsAsciiLetter(name[index])) return false;
        return true;
    }
}
