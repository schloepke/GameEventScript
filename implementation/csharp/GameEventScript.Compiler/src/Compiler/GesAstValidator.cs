// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using GameEventScript.Api;
using GameEventScript.Runtime;

namespace GameEventScript.Compiler;

internal static class GesAstValidator
{
    private sealed class ValidationScope
    {
        private readonly HashSet<string> _variables;
        private readonly Dictionary<string, string> _declaredTypes;
        private readonly IReadOnlyDictionary<string, GesExpressionNode> _constants;

        public ValidationScope(IReadOnlyDictionary<string, GesExpressionNode> constants)
        {
            _variables = new HashSet<string>(StringComparer.Ordinal);
            _declaredTypes = new Dictionary<string, string>(StringComparer.Ordinal);
            _constants = constants;
            foreach (var constant in constants)
            {
                var inferredType = InferImmutableBindingType(constant.Value, constants);
                if (inferredType is not null) _declaredTypes["$" + constant.Key] = inferredType;
            }
        }

        private ValidationScope(ValidationScope parent)
        {
            _variables = new HashSet<string>(StringComparer.Ordinal);
            _declaredTypes = new Dictionary<string, string>(parent._declaredTypes, StringComparer.Ordinal);
            _constants = parent._constants;
        }

        public static ValidationScope Create(IReadOnlyList<GesParameterNode> parameters, IReadOnlyDictionary<string, GesExpressionNode> constants)
        {
            var scope = new ValidationScope(constants);
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
        public IReadOnlyDictionary<string, GesExpressionNode> Constants => _constants;

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

    internal static void ValidateModule(GesParsedScript parsedScript, IReadOnlyDictionary<string, GesCallableDefinition> callables,
        IReadOnlyDictionary<string, GesTypeDefinitionNode> typeDefinitions, IReadOnlyDictionary<string, GesExpressionNode> constants, GesCompileOptions options, GesValidationErrors errors)
    {
        _ = options;
        for (var typeDefinitionIndex = 0; typeDefinitionIndex < parsedScript.TypeDefinitions.Count; typeDefinitionIndex++)
        {
            var typeDefinition = parsedScript.TypeDefinitions[typeDefinitionIndex];
            ValidateRemovedType(parsedScript, typeDefinition.Name, typeDefinition, errors);

            for (var fieldIndex = 0; fieldIndex < typeDefinition.Fields.Count; fieldIndex++)
            {
                var field = typeDefinition.Fields[fieldIndex];
                ValidateArgumentLabelCase(
                    parsedScript,
                    field.Name,
                    field.Name,
                    GesDiagnosticSymbolKind.Variable,
                    "Field names must start with a lowercase ASCII letter and contain only ASCII letters or digits",
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
            ValidateCallableCase(
                    parsedScript,
                    predicateDefinition.Name,
                    predicateDefinition.Name,
                    GesDiagnosticSymbolKind.Predicate,
                    "Predicate names must start with a lowercase ASCII letter and contain only ASCII letters or digits",
                    errors);

            for (var parameterIndex = 0; parameterIndex < predicateDefinition.Parameters.Count; parameterIndex++)
            {
                var parameterNode = predicateDefinition.ParameterList[parameterIndex];
                var parameter = parameterNode.LocalName;
                ValidateIdentifierCase(
                    parsedScript,
                    parameter,
                    predicateDefinition.Name,
                    GesDiagnosticSymbolKind.Predicate,
                    $"Predicate '{predicateDefinition.Name}' declares an invalid parameter name '{parameter}'",
                    errors);
                if (parameterNode.ExternalLabel is not null)
                    ValidateArgumentLabelCase(parsedScript, parameterNode.ExternalLabel, predicateDefinition.Name, GesDiagnosticSymbolKind.Predicate,
                        $"Predicate '{predicateDefinition.Name}' declares an invalid external parameter label '{parameterNode.ExternalLabel}'", errors);
            }

            ValidateParameterTypeHints(parsedScript, "Predicate", predicateDefinition.Name, predicateDefinition.ParameterList, typeDefinitions, errors);
            ValidateDuplicateParameterDefinitions(
                parsedScript,
                predicateDefinition.Name,
                GesDiagnosticSymbolKind.Predicate,
                GesDiagnosticCodes.ValidateDuplicateDefinitionParameter,
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
            ValidateCallableCase(
                    parsedScript,
                    functionDefinition.Name,
                    functionDefinition.Name,
                    GesDiagnosticSymbolKind.Function,
                    "Function names must start with a lowercase ASCII letter and contain only ASCII letters or digits",
                    errors);

            for (var parameterIndex = 0; parameterIndex < functionDefinition.Parameters.Count; parameterIndex++)
            {
                var parameterNode = functionDefinition.ParameterList[parameterIndex];
                var parameter = parameterNode.LocalName;
                ValidateIdentifierCase(
                    parsedScript,
                    parameter,
                    functionDefinition.Name,
                    GesDiagnosticSymbolKind.Function,
                    $"Function '{functionDefinition.Name}' declares an invalid parameter name '{parameter}'",
                    errors);
                if (parameterNode.ExternalLabel is not null)
                    ValidateArgumentLabelCase(parsedScript, parameterNode.ExternalLabel, functionDefinition.Name, GesDiagnosticSymbolKind.Function,
                        $"Function '{functionDefinition.Name}' declares an invalid external parameter label '{parameterNode.ExternalLabel}'", errors);
            }

            ValidateParameterTypeHints(parsedScript, "Function", functionDefinition.Name, functionDefinition.ParameterList, typeDefinitions, errors);
            ValidateDuplicateParameterDefinitions(
                parsedScript,
                functionDefinition.Name,
                GesDiagnosticSymbolKind.Function,
                GesDiagnosticCodes.ValidateDuplicateDefinitionParameter,
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
            if (GesSystemEndpoints.IsInitializationName(handler.Message))
            {
                ValidateInitializationHandler(parsedScript, handler, errors);
            }
            else if (GesSystemEndpoints.IsUndeliverableName(handler.Message))
            {
                ValidateUndeliverableHandler(parsedScript, handler, errors);
            }
            else
            {
                ValidateMessageCase(
                    parsedScript,
                    handler.Message,
                    handler.Message,
                    GesDiagnosticSymbolKind.Handler,
                    "Handler message names must start uppercase and contain only ASCII letters or digits",
                    errors);
                ValidateMessageNameHandler(parsedScript, handler, errors);
            }

            for (var parameterIndex = 0; parameterIndex < handler.Parameters.Count; parameterIndex++)
            {
                var parameterNode = handler.ParameterList[parameterIndex];
                var parameter = parameterNode.LocalName;
                ValidateIdentifierCase(
                    parsedScript,
                    parameter,
                    handler.Message,
                    GesDiagnosticSymbolKind.Handler,
                    $"Handler '{handler.Message}' declares an invalid parameter name '{parameter}'",
                    errors);
                if (parameterNode.ExternalLabel is not null)
                    ValidateArgumentLabelCase(parsedScript, parameterNode.ExternalLabel, handler.Message, GesDiagnosticSymbolKind.Handler,
                        $"Handler '{handler.Message}' declares an invalid external parameter label '{parameterNode.ExternalLabel}'", errors);
            }

            ValidateParameterTypeHints(parsedScript, "Handler", handler.Message, handler.ParameterList, typeDefinitions, errors);
            ValidateDuplicateParameterDefinitions(
                parsedScript,
                handler.Message,
                GesDiagnosticSymbolKind.Handler,
                GesDiagnosticCodes.ValidateDuplicateHandlerParameter,
                "Handler",
                handler.ParameterList,
                errors);

            var handlerScope = ValidationScope.Create(handler.ParameterList, constants);
            for (var statementIndex = 0; statementIndex < handler.Statements.Count; statementIndex++)
            {
                var statement = handler.Statements[statementIndex];
                ValidateStatementReferences(parsedScript, statement, callables, typeDefinitions, errors, handlerScope);
            }
        }

        GesShadowingValidator.Validate(parsedScript, errors);
    }

    private static void ValidateInitializationHandler(GesParsedScript parsedScript, GesEventHandlerNode handler, GesValidationErrors errors)
    {
        if (handler.DispatchKind != GesEventHandlerDispatchKind.ExactSignature ||
            handler.ParameterList.Count != 0)
        {
            errors.Add(
                parsedScript,
                "System endpoint 'initialization' must use parameterless syntax",
                handler.Message,
                GesDiagnosticSymbolKind.Handler,
                GesDiagnosticCodes.ValidateInvalidMessageCase,
                handler);
            return;
        }

        if (handler.MatchingTags.Count != 0 || handler.WithoutTags.Count != 0)
        {
            errors.Add(
                parsedScript,
                "System endpoint 'initialization' cannot use tag filters",
                handler.Message,
                GesDiagnosticSymbolKind.Handler,
                GesDiagnosticCodes.ValidateInvalidMessageCase,
                handler);
        }
    }

    private static void ValidateUndeliverableHandler(GesParsedScript parsedScript, GesEventHandlerNode handler, GesValidationErrors errors)
    {
        if (handler.DispatchKind != GesEventHandlerDispatchKind.MessageName)
        {
            errors.Add(
                parsedScript,
                "System endpoint 'undeliverable' must use 'as message' syntax",
                handler.Message,
                GesDiagnosticSymbolKind.Handler,
                GesDiagnosticCodes.ValidateInvalidMessageCase,
                handler);
            return;
        }

        var parameter = handler.ParameterList[0];
        if (!string.Equals(parameter.SignatureLabel, GesSystemEndpoints.MessageArgumentName, StringComparison.Ordinal) ||
            !string.Equals(parameter.DeclaredType, "message", StringComparison.Ordinal))
        {
            errors.Add(
                parsedScript,
                "System endpoint 'undeliverable' must bind a ':Message' value",
                parameter.LocalName,
                GesDiagnosticSymbolKind.Handler,
                GesDiagnosticCodes.ValidateInvalidMessageCase,
                parameter);
        }
    }

    private static void ValidateMessageNameHandler(GesParsedScript parsedScript, GesEventHandlerNode handler, GesValidationErrors errors)
    {
        if (handler.DispatchKind != GesEventHandlerDispatchKind.MessageName)
        {
            return;
        }

        if (handler.ParameterList.Count != 1)
        {
            errors.Add(
                parsedScript,
                "Message-name handlers expect exactly one message parameter",
                handler.Message,
                GesDiagnosticSymbolKind.Handler,
                GesDiagnosticCodes.ValidateInvalidMessageCase,
                handler);
            return;
        }

        var parameter = handler.ParameterList[0];
        if (!string.Equals(parameter.SignatureLabel, GesSystemEndpoints.MessageArgumentName, StringComparison.Ordinal) ||
            !string.Equals(parameter.DeclaredType, "message", StringComparison.Ordinal))
        {
            errors.Add(
                parsedScript,
                "Message-name handlers must bind a ':Message' value",
                handler.Message,
                GesDiagnosticSymbolKind.Handler,
                GesDiagnosticCodes.ValidateInvalidMessageCase,
                handler);
        }
    }

    private static void ValidateStatementReferences(
        GesParsedScript parsedScriptContext,
        GesStatementNode statement,
        IReadOnlyDictionary<string, GesCallableDefinition> callables,
        IReadOnlyDictionary<string, GesTypeDefinitionNode> typeDefinitions,
        GesValidationErrors errors,
        ValidationScope scope)
    {
        switch (statement)
        {
            case GesPublishStatementNode publish:
                ValidateExpressionReferences(parsedScriptContext, publish.MessageExpression, callables, typeDefinitions, errors, scope.DeclaredTypes);
                for (var tagIndex = 0; tagIndex < publish.TagExpressions.Count; tagIndex++)
                {
                    var tagExpression = publish.TagExpressions[tagIndex];
                    ValidateExpressionReferences(parsedScriptContext, tagExpression, callables, typeDefinitions, errors, scope.DeclaredTypes);
                }

                return;

            case GesAssemblyStatementNode assembly:
                foreach (var line in assembly.Block.Lines)
                    foreach (var operand in line.Operands) ValidateExpressionReferences(parsedScriptContext, operand, callables, typeDefinitions, errors, scope.DeclaredTypes);
                foreach (var declaration in assembly.Block.Declarations)
                    if (declaration.Export)
                        ValidateStatementReferences(parsedScriptContext, new GesLetStatementNode(declaration.Name, new GesNothingLiteralExpressionNode()) { SourceRange = declaration.SourceRange }, callables, typeDefinitions, errors, scope);
                return;

            case GesLetStatementNode let:
                ValidateExpressionReferences(parsedScriptContext, let.Expression, callables, typeDefinitions, errors, scope.DeclaredTypes);
                ValidateIdentifierCase(
                    parsedScriptContext,
                    let.Identifier,
                    let.Identifier,
                    GesDiagnosticSymbolKind.Variable,
                    $"Variable '{let.Identifier}' must start lowercase, contain ASCII letters or digits, and may use one canonical final _index suffix",
                    errors);
                if (scope.ContainsInCurrentScope(let.Identifier))
                {
                    errors.Add(
                        parsedScriptContext,
                        $"Variable '{let.Identifier}' is already declared in the current scope",
                        let.Identifier,
                        GesDiagnosticSymbolKind.Variable,
                        GesDiagnosticCodes.ValidateDuplicateVariable,
                        let);
                    return;
                }

                scope.Declare(let.Identifier, InferImmutableBindingType(let.Expression, scope.Constants));
                return;

            case GesIfStatementNode ifStatement:
                var conditionScope = ValidationScope.CreateChild(scope);
                foreach (var condition in ifStatement.Conditions)
                {
                    if (condition.Binding is { } binding)
                    {
                        ValidateStatementReferences(parsedScriptContext, new GesLetStatementNode(binding, condition.Expression) { SourceRange = condition.SourceRange }, callables, typeDefinitions, errors, conditionScope);
                    }
                    else ValidateExpressionReferences(parsedScriptContext, condition.Expression, callables, typeDefinitions, errors, conditionScope.DeclaredTypes);
                }
                foreach (var nested in ifStatement.ThenBody.Statements)
                    ValidateStatementReferences(parsedScriptContext, nested, callables, typeDefinitions, errors, conditionScope);

                if (ifStatement.ElseBody is null)
                {
                    return;
                }

                var elseScope = ValidationScope.CreateChild(scope);
                foreach (var nested in ifStatement.ElseBody.Statements)
                    ValidateStatementReferences(parsedScriptContext, nested, callables, typeDefinitions, errors, elseScope);

                return;

            case GesForStatementNode forStatement:
                ValidateIterationSourceReferences(parsedScriptContext, forStatement.Source, callables, typeDefinitions, errors, scope.DeclaredTypes);
                ValidateIdentifierCase(
                    parsedScriptContext,
                    forStatement.Identifier,
                    forStatement.Identifier,
                    GesDiagnosticSymbolKind.Variable,
                    $"Loop variable '{forStatement.Identifier}' must start lowercase, contain ASCII letters or digits, and may use one canonical final _index suffix",
                    errors);
                var loopScope = ValidationScope.CreateChild(scope);
                loopScope.Declare(forStatement.Identifier);
                ValidateStatementBodyReferences(parsedScriptContext, forStatement.Body, callables, typeDefinitions, errors, loopScope);

                return;

            case GesSeededRandomStatementNode seededRandom:
                ValidateExpressionReferences(parsedScriptContext, seededRandom.SeedExpression, callables, typeDefinitions, errors, scope.DeclaredTypes);
                ValidateSeedExpression(parsedScriptContext, seededRandom.SeedExpression, callables, typeDefinitions, errors, scope.DeclaredTypes);
                ValidateStatementBodyReferences(parsedScriptContext, seededRandom.Body, callables, typeDefinitions, errors, scope);

                return;

            case GesExpressionStatementNode expressionStatement:
                ValidateExpressionReferences(parsedScriptContext, expressionStatement.Expression, callables, typeDefinitions, errors, scope.DeclaredTypes);
                return;
        }
    }

    private static void ValidateParameterTypeHints(
        GesParsedScript parsedScriptContext,
        string declarationKind,
        string declarationName,
        IReadOnlyList<GesParameterNode> parameters,
        IReadOnlyDictionary<string, GesTypeDefinitionNode> typeDefinitions,
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
                GesDiagnosticSymbolKind.Type,
                GesDiagnosticCodes.ValidateInvalidTypeConstructor,
                parameter);
        }
    }

    private static void ValidateStatementBodyReferences(
        GesParsedScript parsedScriptContext,
        GesStatementBodyNode body,
        IReadOnlyDictionary<string, GesCallableDefinition> callables,
        IReadOnlyDictionary<string, GesTypeDefinitionNode> typeDefinitions,
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
        GesParsedScript parsedScriptContext,
        GesPredicateDefinitionNode predicateDefinition,
        IReadOnlyDictionary<string, GesCallableDefinition> callables,
        IReadOnlyDictionary<string, GesTypeDefinitionNode> typeDefinitions,
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
            $"Predicate '{predicateDefinition.Name}' must return :Boolean or nothing; use 'as :Boolean' for explicit boolean coercion.",
            predicateDefinition.Name,
            GesDiagnosticSymbolKind.Predicate,
            GesDiagnosticCodes.ValidateInvalidPredicate,
            predicateDefinition.Expression);
    }

    private static IReadOnlyDictionary<string, string> BuildDeclaredTypeMap(IReadOnlyList<GesParameterNode> parameters)
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

    private static GesTypeFieldDefinitionNode? FindField(IReadOnlyList<GesTypeFieldDefinitionNode> fields, string name)
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
        GesExpressionNode expression,
        IReadOnlyDictionary<string, GesCallableDefinition> callables,
        IReadOnlyDictionary<string, GesTypeDefinitionNode> typeDefinitions,
        IReadOnlyDictionary<string, string> declaredTypes,
        ISet<string> visitedCallables)
    {
        switch (expression)
        {
            case GesAssemblyExpressionNode { Block.Predicate: true }:
            case GesBooleanLiteralExpressionNode:
                return StaticExpressionInfo.Boolean;

            case GesIntegerLiteralExpressionNode:
                return StaticExpressionInfo.Other("integer");

            case GesUnitIntegerLiteralExpressionNode unitInteger:
                return StaticExpressionInfo.Other(unitInteger.UnitName);

            case GesFloatLiteralExpressionNode:
                return StaticExpressionInfo.Other("float");

            case GesUnitFloatLiteralExpressionNode unitFloat:
                return StaticExpressionInfo.Other(unitFloat.UnitName);

            case GesPercentageLiteralExpressionNode:
                return StaticExpressionInfo.Other("percentage");

            case GesTextLiteralExpressionNode:
                return StaticExpressionInfo.Other("text");

            case GesTagLiteralExpressionNode:
                return StaticExpressionInfo.Other("tag");

            case GesTypeCheckExpressionNode:
            case GesNothingCheckExpressionNode:
            case GesPredicateCallExpressionNode:
            case GesExtensionPredicateExpressionNode:
                return StaticExpressionInfo.Boolean;

            case GesNothingLiteralExpressionNode:
                return StaticExpressionInfo.Nothing;

            case GesIdentifierExpressionNode identifier:
                return declaredTypes.TryGetValue(identifier.Name, out var declaredType)
                    ? FromDeclaredType(declaredType)
                    : StaticExpressionInfo.Unknown;

            case GesMemberAccessExpressionNode memberAccess:
                return ClassifyMemberAccess(memberAccess, callables, typeDefinitions, declaredTypes, visitedCallables);

            case GesCollectionAccessExpressionNode collectionAccess:
                return ClassifyCollectionAccess(collectionAccess);

            case GesCallExpressionNode call:
                return ClassifyCall(call, callables, typeDefinitions, visitedCallables);

            case GesTypeCastExpressionNode typeCast:
                return FromDeclaredType(typeCast.TypeName);

            case GesTypeConstructorExpressionNode typeConstructor:
                return FromDeclaredType(typeConstructor.TypeName);

            case GesBetweenExpressionNode:
            case GesSendExpressionNode:
                return StaticExpressionInfo.Boolean;
            case GesUnaryExpressionNode unary:
                return ClassifyUnary(unary, callables, typeDefinitions, declaredTypes, visitedCallables);

            case GesIntrinsicCallExpressionNode intrinsic:
                if (intrinsic.Function is GesIntrinsicFunction.Hypot or GesIntrinsicFunction.Distance)
                    return StaticExpressionInfo.Other(); // These operations can preserve a runtime quantity unit.
                return intrinsic.Function is GesIntrinsicFunction.Normalize ||
                    intrinsic.Function is GesIntrinsicFunction.Cross && intrinsic.Arguments.Count is 2 or 6
                        ? StaticExpressionInfo.Other("vector")
                        : StaticExpressionInfo.Other("number");

            case GesBinaryExpressionNode binary:
                return ClassifyBinary(binary, callables, typeDefinitions, declaredTypes, visitedCallables);

            case GesGuardedChoiceExpressionNode guardedChoice:
                return ClassifyGuardedChoice(guardedChoice, callables, typeDefinitions, declaredTypes, visitedCallables);

            case GesSeededRandomExpressionNode seededRandom:
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

    private static string? InferImmutableBindingType(GesExpressionNode expression, IReadOnlyDictionary<string, GesExpressionNode> constants)
    {
        if (expression is GesIntegerLiteralExpressionNode)
        {
            return "integer";
        }

        if (expression is GesFloatLiteralExpressionNode floatingPoint && GesNumber.CanRepresentAsInteger(floatingPoint.Value))
        {
            return "integer";
        }

        if (expression is GesTypeCastExpressionNode typeCast)
        {
            return typeCast.TypeName;
        }

        if (expression is GesTypeConstructorExpressionNode typeConstructor)
        {
            return typeConstructor.TypeName;
        }

        if (expression is GesConstantReferenceExpressionNode constant && constants.TryGetValue(constant.Name, out var value))
        {
            return InferImmutableBindingType(value, constants);
        }

        return null;
    }

    private static void ValidateSeedExpression(
        GesParsedScript parsedScriptContext,
        GesExpressionNode seedExpression,
        IReadOnlyDictionary<string, GesCallableDefinition> callables,
        IReadOnlyDictionary<string, GesTypeDefinitionNode> typeDefinitions,
        GesValidationErrors errors,
        IReadOnlyDictionary<string, string> declaredTypes)
    {
        var result = ClassifyExpression(seedExpression, callables, typeDefinitions, declaredTypes, new HashSet<string>(StringComparer.Ordinal));
        if (result.TypeName is "integer" ||
            seedExpression is GesConstantReferenceExpressionNode constant && declaredTypes.TryGetValue("$" + constant.Name, out var constantType) && constantType == "integer" ||
            HasExplicitNumberSeedConversion(seedExpression, declaredTypes) ||
            seedExpression is GesFloatLiteralExpressionNode floatingPoint && GesNumber.CanRepresentAsInteger(floatingPoint.Value))
        {
            return;
        }

        errors.Add(
            parsedScriptContext,
            "A random seed must be statically known as a unitless integer or explicitly converted with 'as :Number'; at runtime the result is used as a seed only when it is an exact unitless signed 64-bit integer.",
            "Number",
            GesDiagnosticSymbolKind.Type,
            GesDiagnosticCodes.ValidateInvalidTypeConstructor,
            seedExpression);
    }

    private static bool HasExplicitNumberSeedConversion(GesExpressionNode expression, IReadOnlyDictionary<string, string> declaredTypes)
    {
        return expression switch
        {
            GesTypeCastExpressionNode { TypeName: "number" } => true,
            GesTypeConstructorExpressionNode { TypeName: "number" } => true,
            GesIdentifierExpressionNode identifier => declaredTypes.TryGetValue(identifier.Name, out var declaredType) && declaredType == "number",
            GesUnaryExpressionNode { Operator: GesUnaryOperator.Negate } unary => HasExplicitNumberSeedConversion(unary.Operand, declaredTypes),
            _ => false
        };
    }

    private static StaticExpressionInfo ClassifyMemberAccess(
        GesMemberAccessExpressionNode memberAccess,
        IReadOnlyDictionary<string, GesCallableDefinition> callables,
        IReadOnlyDictionary<string, GesTypeDefinitionNode> typeDefinitions,
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

    private static StaticExpressionInfo ClassifyCollectionAccess(GesCollectionAccessExpressionNode collectionAccess)
        => collectionAccess.Selector switch
        {
            GesPredicateSelectorNode => StaticExpressionInfo.Boolean,
            GesContainsSelectorNode => StaticExpressionInfo.Boolean,
            _ => StaticExpressionInfo.Unknown
        };

    private static StaticExpressionInfo ClassifyCall(GesCallExpressionNode call, IReadOnlyDictionary<string, GesCallableDefinition> callables, IReadOnlyDictionary<string, GesTypeDefinitionNode> typeDefinitions, ISet<string> visitedCallables)
    {
        var callable = GesCallableSignatures.Resolve(callables, call);
        if (callable is null)
        {
            return StaticExpressionInfo.Unknown;
        }

        if (callable.Kind == GesCallableKind.PredicateCall)
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
        GesUnaryExpressionNode unary,
        IReadOnlyDictionary<string, GesCallableDefinition> callables,
        IReadOnlyDictionary<string, GesTypeDefinitionNode> typeDefinitions,
        IReadOnlyDictionary<string, string> declaredTypes,
        ISet<string> visitedCallables)
    {
        return unary.Operator switch
        {
            GesUnaryOperator.Parse => StaticExpressionInfo.Unknown,
            GesUnaryOperator.HasValue or GesUnaryOperator.Empty or GesUnaryOperator.Chance => StaticExpressionInfo.Boolean,
            GesUnaryOperator.Abs => ClassifyAbsolute(unary.Operand, callables, typeDefinitions, declaredTypes, visitedCallables),
            GesUnaryOperator.NaturalLog or GesUnaryOperator.Exp or
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

    private static StaticExpressionInfo ClassifyAbsolute(
        GesExpressionNode operand,
        IReadOnlyDictionary<string, GesCallableDefinition> callables,
        IReadOnlyDictionary<string, GesTypeDefinitionNode> typeDefinitions,
        IReadOnlyDictionary<string, string> declaredTypes,
        ISet<string> visitedCallables)
    {
        var input = ClassifyExpression(operand, callables, typeDefinitions, declaredTypes, visitedCallables);
        var type = input.TypeName;
        if (type is "s" or "second" or "m" or "meter" or "degree" || type?.StartsWith("quantity:", StringComparison.Ordinal) == true)
            return StaticExpressionInfo.Other(type);
        // Vector magnitude retains the vector's unit, which the nominal type alone does not expose.
        return type is null or "vector" ? StaticExpressionInfo.Other() : StaticExpressionInfo.Other("number");
    }

    private static StaticExpressionInfo ClassifyBinary(
        GesBinaryExpressionNode binary,
        IReadOnlyDictionary<string, GesCallableDefinition> callables,
        IReadOnlyDictionary<string, GesTypeDefinitionNode> typeDefinitions,
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
        GesGuardedChoiceExpressionNode guardedChoice,
        IReadOnlyDictionary<string, GesCallableDefinition> callables,
        IReadOnlyDictionary<string, GesTypeDefinitionNode> typeDefinitions,
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
        GesParsedScript parsedScriptContext,
        GesExpressionNode expression,
        IReadOnlyDictionary<string, GesCallableDefinition> callables,
        IReadOnlyDictionary<string, GesTypeDefinitionNode> typeDefinitions,
        GesValidationErrors errors,
        IReadOnlyDictionary<string, string>? declaredTypes = null)
    {
        declaredTypes ??= new Dictionary<string, string>(StringComparer.Ordinal);
        while (true)
        {
            switch (expression)
            {
                case GesAssemblyExpressionNode assembly:
                    foreach (var line in assembly.Block.Lines)
                        foreach (var operand in line.Operands) ValidateExpressionReferences(parsedScriptContext, operand, callables, typeDefinitions, errors, declaredTypes);
                    return;

                case GesNothingLiteralExpressionNode:
                case GesConstantReferenceExpressionNode:
                    return;

                case GesIdentifierExpressionNode identifierExpression:
                    ValidateIdentifierCase(
                        parsedScriptContext,
                        identifierExpression.Name,
                        identifierExpression.Name,
                        GesDiagnosticSymbolKind.Variable,
                        $"Identifier '{identifierExpression.Name}' must start lowercase, contain ASCII letters or digits, and may use one canonical final _index suffix",
                        errors);
                    return;

                case GesCallExpressionNode call:
                    ValidateCallableCase(
                        parsedScriptContext,
                        call.Name,
                        call.Name,
                        GesDiagnosticSymbolKind.GlobalDefinition,
                        $"Call target '{call.Name}' must use identifier casing (predicate/function names start lowercase)",
                        errors);
                    ValidateDuplicateNamedArguments(parsedScriptContext, call.Name, call.ArgumentList.Arguments, errors);
                    ValidateCallExpression(parsedScriptContext, call, callables, typeDefinitions, errors);
                    for (var argumentIndex = 0; argumentIndex < call.ArgumentList.Arguments.Count; argumentIndex++)
                    {
                        var argument = call.ArgumentList.Arguments[argumentIndex];
                        if (argument.Label is not null)
                        {
                            ValidateArgumentLabelCase(
                                parsedScriptContext,
                                argument.Label,
                                call.Name,
                                GesDiagnosticSymbolKind.GlobalDefinition,
                                $"Call argument label '{argument.Label}' must use identifier casing",
                                errors);
                        }

                        ValidateExpressionReferences(parsedScriptContext, argument.Expression, callables, typeDefinitions, errors, declaredTypes);
                    }

                    return;

                case GesHandlerLiteralExpressionNode handlerLiteral:
                    ValidateMessageCase(
                        parsedScriptContext,
                        handlerLiteral.Message,
                        handlerLiteral.Message,
                        GesDiagnosticSymbolKind.Handler,
                        $"Handler literal '{handlerLiteral.Message}' must start uppercase and contain only ASCII letters or digits",
                        errors);
                    for (var parameterIndex = 0; parameterIndex < handlerLiteral.Parameters.Count; parameterIndex++)
                    {
                        var parameter = handlerLiteral.Parameters[parameterIndex];
                        ValidateArgumentLabelCase(
                            parsedScriptContext,
                            parameter,
                            handlerLiteral.Message,
                            GesDiagnosticSymbolKind.Handler,
                            $"Handler literal '{handlerLiteral.Message}' declares invalid parameter '{parameter}'",
                            errors);
                    }

                    ValidateDuplicateHandlerLiteralParameters(parsedScriptContext, handlerLiteral, errors);
                    return;

                case GesMessageLiteralExpressionNode messageLiteral:
                    ValidateMessageCase(
                        parsedScriptContext,
                        messageLiteral.Message,
                        messageLiteral.Message,
                        GesDiagnosticSymbolKind.Message,
                        $"Message literal '{messageLiteral.Message}' must start uppercase and contain only ASCII letters or digits",
                        errors);
                    ValidateDuplicateNamedArguments(parsedScriptContext, messageLiteral.Message, messageLiteral.Arguments, errors);
                    for (var argumentIndex = 0; argumentIndex < messageLiteral.Arguments.Count; argumentIndex++)
                    {
                        var argument = messageLiteral.Arguments[argumentIndex];
                        if (argument.Label is not null)
                        {
                            ValidateArgumentLabelCase(
                                parsedScriptContext,
                                argument.Label,
                                messageLiteral.Message,
                                GesDiagnosticSymbolKind.Message,
                                $"Message literal '{messageLiteral.Message}' declares invalid argument name '{argument.Label}'",
                                errors);
                        }

                        ValidateExpressionReferences(parsedScriptContext, argument.Expression, callables, typeDefinitions, errors, declaredTypes);
                    }

                    return;

                case GesExtensionCallExpressionNode extensionCall:
                    for (var argumentIndex = 0; argumentIndex < extensionCall.Arguments.Count; argumentIndex++)
                    {
                        var argument = extensionCall.Arguments[argumentIndex];
                        ValidateExpressionReferences(parsedScriptContext, argument.Expression, callables, typeDefinitions, errors, declaredTypes);
                    }

                    return;

                case GesTypeConstructorExpressionNode typeConstructor:
                    ValidateTypeConstructorExpression(parsedScriptContext, typeConstructor, callables, typeDefinitions, errors, declaredTypes);
                    return;

                case GesPredicateCallExpressionNode predicateCall:
                    ValidateCallableCase(
                        parsedScriptContext,
                        predicateCall.PredicateName,
                        predicateCall.PredicateName,
                        GesDiagnosticSymbolKind.Predicate,
                        $"Predicate test target '{predicateCall.PredicateName}' must use identifier casing (start lowercase)",
                        errors);
                    if (GesCallableSignatures.ResolvePredicate(callables, predicateCall.PredicateName, predicateCall.Value is null ? 0 : 1) is null)
                    {
                        errors.Add(
                            parsedScriptContext,
                            $"Predicate '{predicateCall.PredicateName}' must exist and declare exactly {(predicateCall.Value is null ? 0 : 1)} parameters for this 'is' form",
                            predicateCall.PredicateName,
                            GesDiagnosticSymbolKind.Predicate,
                            GesDiagnosticCodes.ValidateInvalidPredicate);
                    }

                    if (predicateCall.Value is null) return;
                    expression = predicateCall.Value;
                    continue;

                case GesExtensionPredicateExpressionNode extensionPredicate:
                    expression = extensionPredicate.Value;
                    continue;

                case GesSendExpressionNode send:
                    ValidateExpressionReferences(parsedScriptContext, send.Message, callables, typeDefinitions, errors, declaredTypes);
                    if (send.Delay is not null)
                    {
                        ValidateExpressionReferences(parsedScriptContext, send.Delay, callables, typeDefinitions, errors, declaredTypes);
                        var delayType = ClassifyExpression(send.Delay, callables, typeDefinitions, declaredTypes, new HashSet<string>(StringComparer.Ordinal));
                        if (delayType.TypeName is not null && delayType.Kind is not (StaticExpressionKind.Unknown or StaticExpressionKind.Nothing) && delayType.TypeName is not ("s" or "second" or "quantity:s" or "quantity:second"))
                            errors.Add(parsedScriptContext, "Send delay requires a time quantity", "Quantity(s)", GesDiagnosticSymbolKind.Type, GesDiagnosticCodes.ValidateInvalidTypeConstructor, send.Delay);
                    }
                    foreach (var tag in send.Tags) ValidateExpressionReferences(parsedScriptContext, tag, callables, typeDefinitions, errors, declaredTypes);
                    return;
                case GesUnaryExpressionNode unary:
                    expression = unary.Operand;
                    continue;

                case GesVariadicTaggedExpressionNode variadic:
                    for (var argumentIndex = 0; argumentIndex < variadic.Arguments.Count; argumentIndex++)
                    {
                        var argument = variadic.Arguments[argumentIndex];
                        ValidateExpressionReferences(parsedScriptContext, argument, callables, typeDefinitions, errors, declaredTypes);
                    }

                    return;

                case GesBetweenExpressionNode between:
                    ValidateExpressionReferences(parsedScriptContext, between.Value, callables, typeDefinitions, errors, declaredTypes);
                    ValidateExpressionReferences(parsedScriptContext, between.Minimum, callables, typeDefinitions, errors, declaredTypes);
                    expression = between.Maximum;
                    continue;

                case GesClampExpressionNode clamp:
                    ValidateExpressionReferences(parsedScriptContext, clamp.Value, callables, typeDefinitions, errors, declaredTypes);
                    ValidateExpressionReferences(parsedScriptContext, clamp.Minimum, callables, typeDefinitions, errors, declaredTypes);
                    expression = clamp.Maximum;
                    continue;

                case GesRangeExpressionNode rangeExpression:
                    ValidateExpressionReferences(parsedScriptContext, rangeExpression.FromExpression, callables, typeDefinitions, errors, declaredTypes);
                    ValidateExpressionReferences(parsedScriptContext, rangeExpression.ToExpression, callables, typeDefinitions, errors, declaredTypes);
                    if (rangeExpression.StepExpression is not null)
                    {
                        expression = rangeExpression.StepExpression;
                        continue;
                    }

                    return;

                case GesRandomExpressionNode random:
                    ValidateExpressionReferences(parsedScriptContext, random.FromExpression, callables, typeDefinitions, errors, declaredTypes);
                    expression = random.ToExpression;
                    continue;

                case GesSeededRandomExpressionNode seededRandom:
                    ValidateExpressionReferences(parsedScriptContext, seededRandom.SeedExpression, callables, typeDefinitions, errors, declaredTypes);
                    ValidateSeedExpression(parsedScriptContext, seededRandom.SeedExpression, callables, typeDefinitions, errors, declaredTypes);
                    expression = seededRandom.BodyExpression;
                    continue;

                case GesGeneratedCollectionExpressionNode generatedCollection:
                    ValidateIdentifierCase(
                        parsedScriptContext,
                        generatedCollection.Identifier,
                        generatedCollection.Identifier,
                        GesDiagnosticSymbolKind.Variable,
                        $"Generated collection identifier '{generatedCollection.Identifier}' must use identifier casing (start lowercase)",
                        errors);
                    ValidateIterationSourceReferences(parsedScriptContext, generatedCollection.Source, callables, typeDefinitions, errors, declaredTypes);
                    if (generatedCollection.Predicate is not null)
                    {
                        ValidateExpressionReferences(parsedScriptContext, generatedCollection.Predicate, callables, typeDefinitions, errors, declaredTypes);
                    }

                    expression = generatedCollection.Projection;
                    continue;

                case GesGuardedChoiceExpressionNode guardedChoice:
                    for (var branchIndex = 0; branchIndex < guardedChoice.Branches.Count; branchIndex++)
                    {
                        var branch = guardedChoice.Branches[branchIndex];
                        ValidateExpressionReferences(parsedScriptContext, branch.ValueExpression, callables, typeDefinitions, errors, declaredTypes);
                        ValidateExpressionReferences(parsedScriptContext, branch.ConditionExpression, callables, typeDefinitions, errors, declaredTypes);
                    }

                    expression = guardedChoice.OtherwiseExpression;
                    continue;

                case GesBinaryExpressionNode binary:
                    ValidateExpressionReferences(parsedScriptContext, binary.Left, callables, typeDefinitions, errors, declaredTypes);
                    expression = binary.Right;
                    continue;

                case GesTypeCheckExpressionNode typeCheck:
                    ValidateRemovedType(parsedScriptContext, typeCheck.TypeName, typeCheck, errors);
                    expression = typeCheck.Value;
                    continue;

                case GesNothingCheckExpressionNode nothingCheck:
                    expression = nothingCheck.Value;
                    continue;

                case GesTypeCastExpressionNode typeCast:
                    ValidateRemovedType(parsedScriptContext, typeCast.TypeName, typeCast, errors);
                    expression = typeCast.Value;
                    continue;

                case GesMemberAccessExpressionNode memberAccess:
                    ValidateArgumentLabelCase(parsedScriptContext, memberAccess.Member, memberAccess.Member, GesDiagnosticSymbolKind.Variable,
                        $"Member name '{memberAccess.Member}' must start with a lowercase ASCII letter and contain only ASCII letters or digits", errors);
                    expression = memberAccess.Target;
                    continue;

                case GesCollectionAccessExpressionNode collectionAccess:
                    ValidateExpressionReferences(parsedScriptContext, collectionAccess.Target, callables, typeDefinitions, errors, declaredTypes);
                    ValidateCollectionSelectorReferences(parsedScriptContext, collectionAccess.Selector, callables, typeDefinitions, errors, declaredTypes);
                    return;

                case GesCombinedCollectionExpressionNode combined:
                    foreach (var source in combined.Sources) ValidateExpressionReferences(parsedScriptContext, source, callables, typeDefinitions, errors, declaredTypes);
                    return;
                case GesListLiteralExpressionNode list:
                    for (var itemIndex = 0; itemIndex < list.Items.Count; itemIndex++)
                    {
                        var item = list.Items[itemIndex];
                        ValidateExpressionReferences(parsedScriptContext, item, callables, typeDefinitions, errors, declaredTypes);
                    }

                    return;

                case GesMapLiteralExpressionNode dictionary:
                    for (var entryIndex = 0; entryIndex < dictionary.Entries.Count; entryIndex++)
                    {
                        var entry = dictionary.Entries[entryIndex];
                        ValidateArgumentLabelCase(parsedScriptContext, entry.Key, entry.Key, GesDiagnosticSymbolKind.Variable,
                            $"Map key '{entry.Key}' must start with a lowercase ASCII letter and contain only ASCII letters or digits", errors);
                        ValidateExpressionReferences(parsedScriptContext, entry.Value, callables, typeDefinitions, errors, declaredTypes);
                    }

                    return;
            }

            break;
        }
    }

    private static void ValidateIterationSourceReferences(
        GesParsedScript parsedScriptContext,
        GesIterationSourceNode source,
        IReadOnlyDictionary<string, GesCallableDefinition> callables,
        IReadOnlyDictionary<string, GesTypeDefinitionNode> typeDefinitions,
        GesValidationErrors errors,
        IReadOnlyDictionary<string, string>? declaredTypes = null)
    {
        switch (source)
        {
            case GesCollectionIterationSourceNode collectionSource:
                ValidateExpressionReferences(parsedScriptContext, collectionSource.Expression, callables, typeDefinitions, errors, declaredTypes);
                return;
            case GesRangeIterationSourceNode rangeSource:
                ValidateExpressionReferences(parsedScriptContext, rangeSource.RangeExpression, callables, typeDefinitions, errors, declaredTypes);
                return;
        }
    }

    private static void ValidateCollectionSelectorReferences(
        GesParsedScript parsedScriptContext,
        GesCollectionSelectorNode selector,
        IReadOnlyDictionary<string, GesCallableDefinition> callables,
        IReadOnlyDictionary<string, GesTypeDefinitionNode> typeDefinitions,
        GesValidationErrors errors,
        IReadOnlyDictionary<string, string>? declaredTypes = null)
    {
        switch (selector)
        {
            case GesExpressionSelectorNode expressionSelector:
                ValidateExpressionReferences(parsedScriptContext, expressionSelector.Expression, callables, typeDefinitions, errors, declaredTypes);
                return;
            case GesSeriesTermSelectorNode seriesTermSelector:
                ValidateExpressionReferences(parsedScriptContext, seriesTermSelector.IndexExpression, callables, typeDefinitions, errors, declaredTypes);
                return;
            case GesPredicateSelectorNode predicateSelector:
                ValidateIdentifierCase(
                    parsedScriptContext,
                    predicateSelector.Identifier,
                    predicateSelector.Identifier,
                    GesDiagnosticSymbolKind.Variable,
                    $"Selector identifier '{predicateSelector.Identifier}' must use identifier casing (start lowercase)",
                    errors);
                ValidateExpressionReferences(parsedScriptContext, predicateSelector.Predicate, callables, typeDefinitions, errors, declaredTypes);
                return;
            case GesCountSelectorNode countSelector:
                ValidateIdentifierCase(
                    parsedScriptContext,
                    countSelector.Identifier,
                    countSelector.Identifier,
                    GesDiagnosticSymbolKind.Variable,
                    $"Selector identifier '{countSelector.Identifier}' must use identifier casing (start lowercase)",
                    errors);
                ValidateExpressionReferences(parsedScriptContext, countSelector.Predicate, callables, typeDefinitions, errors, declaredTypes);
                return;
            case GesChooseSelectorNode chooseSelector:
                if (chooseSelector.Identifier is not null)
                {
                    ValidateIdentifierCase(
                        parsedScriptContext,
                        chooseSelector.Identifier,
                        chooseSelector.Identifier,
                        GesDiagnosticSymbolKind.Variable,
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
                        GesDiagnosticSymbolKind.Variable,
                        $"Selector weight identifier '{chooseSelector.WeightIdentifier}' must use identifier casing (start lowercase)",
                        errors);
                }

                if (chooseSelector.WeightExpression is not null)
                {
                    ValidateExpressionReferences(parsedScriptContext, chooseSelector.WeightExpression, callables, typeDefinitions, errors, declaredTypes);
                }

                return;
            case GesEdgeSelectorNode { Predicate: not null } edgeSelector:
                if (edgeSelector.Identifier is not null)
                {
                    ValidateIdentifierCase(
                        parsedScriptContext,
                        edgeSelector.Identifier,
                        edgeSelector.Identifier,
                        GesDiagnosticSymbolKind.Variable,
                        $"Selector identifier '{edgeSelector.Identifier}' must use identifier casing (start lowercase)",
                        errors);
                }

                ValidateExpressionReferences(parsedScriptContext, edgeSelector.Predicate, callables, typeDefinitions, errors, declaredTypes);
                return;
            case GesForeachSelectorNode each:
                ValidateExpressionReferences(parsedScriptContext, each.Expression, callables, typeDefinitions, errors, declaredTypes);
                return;
            case GesFilterSelectorNode filterSelector:
                ValidateIdentifierCase(
                    parsedScriptContext,
                    filterSelector.Identifier,
                    filterSelector.Identifier,
                    GesDiagnosticSymbolKind.Variable,
                    $"Selector identifier '{filterSelector.Identifier}' must use identifier casing (start lowercase)",
                    errors);
                ValidateExpressionReferences(parsedScriptContext, filterSelector.Predicate, callables, typeDefinitions, errors, declaredTypes);
                return;
            case GesSumSelectorNode sumSelector:
                ValidateIdentifierCase(
                    parsedScriptContext,
                    sumSelector.Identifier,
                    sumSelector.Identifier,
                    GesDiagnosticSymbolKind.Variable,
                    $"Selector identifier '{sumSelector.Identifier}' must use identifier casing (start lowercase)",
                    errors);
                ValidateExpressionReferences(parsedScriptContext, sumSelector.Projection, callables, typeDefinitions, errors, declaredTypes);
                return;
            case GesAverageSelectorNode averageSelector:
                ValidateIdentifierCase(
                    parsedScriptContext,
                    averageSelector.Identifier,
                    averageSelector.Identifier,
                    GesDiagnosticSymbolKind.Variable,
                    $"Selector identifier '{averageSelector.Identifier}' must use identifier casing (start lowercase)",
                    errors);
                ValidateExpressionReferences(parsedScriptContext, averageSelector.Projection, callables, typeDefinitions, errors, declaredTypes);
                return;
            case GesFoldSelectorNode fold:
                ValidateIdentifierCase(parsedScriptContext, fold.Accumulator, fold.Accumulator, GesDiagnosticSymbolKind.Variable, "Accumulator must use identifier casing", errors);
                ValidateIdentifierCase(parsedScriptContext, fold.Identifier, fold.Identifier, GesDiagnosticSymbolKind.Variable, "Element must use identifier casing", errors);
                if (fold.Seed is not null) ValidateExpressionReferences(parsedScriptContext, fold.Seed, callables, typeDefinitions, errors, declaredTypes);
                ValidateExpressionReferences(parsedScriptContext, fold.Projection, callables, typeDefinitions, errors, declaredTypes);
                return;
            case GesSelectSelectorNode selectSelector:
                ValidateIdentifierCase(
                    parsedScriptContext,
                    selectSelector.Identifier,
                    selectSelector.Identifier,
                    GesDiagnosticSymbolKind.Variable,
                    $"Selector identifier '{selectSelector.Identifier}' must use identifier casing (start lowercase)",
                    errors);
                ValidateExpressionReferences(parsedScriptContext, selectSelector.Projection, callables, typeDefinitions, errors, declaredTypes);
                return;
            case GesMapSelectorNode dictionarySelector:
                ValidateIdentifierCase(
                    parsedScriptContext,
                    dictionarySelector.Identifier,
                    dictionarySelector.Identifier,
                    GesDiagnosticSymbolKind.Variable,
                    $"Selector identifier '{dictionarySelector.Identifier}' must use identifier casing (start lowercase)",
                    errors);
                ValidateExpressionReferences(parsedScriptContext, dictionarySelector.KeyProjection, callables, typeDefinitions, errors, declaredTypes);
                if (dictionarySelector.ValueProjection is not null)
                {
                    ValidateExpressionReferences(parsedScriptContext, dictionarySelector.ValueProjection, callables, typeDefinitions, errors, declaredTypes);
                }

                return;
            case GesMinSelectorNode minSelector:
                ValidateIdentifierCase(
                    parsedScriptContext,
                    minSelector.Identifier,
                    minSelector.Identifier,
                    GesDiagnosticSymbolKind.Variable,
                    $"Selector identifier '{minSelector.Identifier}' must use identifier casing (start lowercase)",
                    errors);
                ValidateExpressionReferences(parsedScriptContext, minSelector.Projection, callables, typeDefinitions, errors, declaredTypes);
                return;
            case GesMaxSelectorNode maxSelector:
                ValidateIdentifierCase(
                    parsedScriptContext,
                    maxSelector.Identifier,
                    maxSelector.Identifier,
                    GesDiagnosticSymbolKind.Variable,
                    $"Selector identifier '{maxSelector.Identifier}' must use identifier casing (start lowercase)",
                    errors);
                ValidateExpressionReferences(parsedScriptContext, maxSelector.Projection, callables, typeDefinitions, errors, declaredTypes);
                return;
            case GesContainsSelectorNode containsSelector:
                ValidateExpressionReferences(parsedScriptContext, containsSelector.ValueExpression, callables, typeDefinitions, errors, declaredTypes);
                return;
            case GesDistinctSelectorNode { Projection: not null } distinctSelector:
                if (distinctSelector.Identifier is not null)
                {
                    ValidateIdentifierCase(
                        parsedScriptContext,
                        distinctSelector.Identifier,
                        distinctSelector.Identifier,
                        GesDiagnosticSymbolKind.Variable,
                        $"Selector identifier '{distinctSelector.Identifier}' must use identifier casing (start lowercase)",
                        errors);
                }

                ValidateExpressionReferences(parsedScriptContext, distinctSelector.Projection, callables, typeDefinitions, errors, declaredTypes);
                return;
            case GesGroupBySelectorNode groupBySelector:
                ValidateIdentifierCase(
                    parsedScriptContext,
                    groupBySelector.Identifier,
                    groupBySelector.Identifier,
                    GesDiagnosticSymbolKind.Variable,
                    $"Selector identifier '{groupBySelector.Identifier}' must use identifier casing (start lowercase)",
                    errors);
                ValidateExpressionReferences(parsedScriptContext, groupBySelector.Projection, callables, typeDefinitions, errors, declaredTypes);
                return;
            case GesOrderBySelectorNode orderBySelector:
                ValidateIdentifierCase(
                    parsedScriptContext,
                    orderBySelector.Identifier,
                    orderBySelector.Identifier,
                    GesDiagnosticSymbolKind.Variable,
                    $"Selector identifier '{orderBySelector.Identifier}' must use identifier casing (start lowercase)",
                    errors);
                ValidateExpressionReferences(parsedScriptContext, orderBySelector.Projection, callables, typeDefinitions, errors, declaredTypes);
                return;
        }
    }

    private static void ValidateCallExpression(
        GesParsedScript parsedScriptContext,
        GesCallExpressionNode call,
        IReadOnlyDictionary<string, GesCallableDefinition> callables,
        IReadOnlyDictionary<string, GesTypeDefinitionNode> typeDefinitions,
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
                    callableKind == GesCallableKind.PredicateCall ? GesDiagnosticSymbolKind.Predicate : GesDiagnosticSymbolKind.Function,
                    callableKind == GesCallableKind.PredicateCall ? GesDiagnosticCodes.ValidateWrongPredicateArity : GesDiagnosticCodes.ValidateWrongFunctionArity);
            }
            else if (AllArgumentsUnlabeled(call.ArgumentList.Arguments))
            {
                errors.Add(
                    parsedScriptContext,
                    $"No predicate or function named '{call.Name}' exists",
                    call.Name,
                    GesDiagnosticSymbolKind.GlobalDefinition,
                    GesDiagnosticCodes.ValidateMissingCallable);
            }

            return;
        }

        ValidateCallArity(parsedScriptContext, callable.Kind, call.Name, callable.Parameters.Count, call.Arguments.Count, errors);
        ValidateCallLabels(parsedScriptContext, callable.Kind, call.Name, callable.SignatureLabels, call.ArgumentList.Arguments, errors);
    }

    private static GesCallableKind FindCallableKind(IReadOnlyDictionary<string, GesCallableDefinition> callables, string name)
    {
        foreach (var callable in callables.Values)
            if (string.Equals(callable.Name, name, StringComparison.Ordinal)) return callable.Kind;
        return GesCallableKind.FunctionCall;
    }

    private static void ValidateCallLabels(GesParsedScript parsedScriptContext, GesCallableKind kind, string name, IReadOnlyList<string> expectedLabels, IReadOnlyList<GesArgumentNode> arguments, GesValidationErrors errors)
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
                kind == GesCallableKind.PredicateCall ? GesDiagnosticSymbolKind.Predicate : GesDiagnosticSymbolKind.Function,
                kind == GesCallableKind.PredicateCall ? GesDiagnosticCodes.ValidateWrongPredicateArity : GesDiagnosticCodes.ValidateWrongFunctionArity);
        }
    }

    private static bool AllArgumentsUnlabeled(IReadOnlyList<GesArgumentNode> arguments)
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

    private static int CountUnlabeledArguments(IReadOnlyList<GesArgumentNode> arguments)
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

    private static int CountLabeledArguments(IReadOnlyList<GesArgumentNode> arguments)
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

    private static GesTypeFieldDefinitionNode[] ReadConstructorFields(IReadOnlyList<GesTypeFieldDefinitionNode> fields)
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

        var constructorFields = new GesTypeFieldDefinitionNode[count];
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

    private static int CountUnlabeledConstructorFields(IReadOnlyList<GesTypeFieldDefinitionNode> fields)
    {
        var count = 0;
        for (var index = 0; index < fields.Count; index++)
        {
            if (string.Equals(fields[index].ConstructorLabel, GesMessageSignature.UnlabeledParameterName, StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }

    private static HashSet<string> BuildLabeledConstructorFieldSet(IReadOnlyList<GesTypeFieldDefinitionNode> fields)
    {
        var fieldNames = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < fields.Count; index++)
        {
            var label = fields[index].ConstructorLabel;
            if (label is not null &&
                !string.Equals(label, GesMessageSignature.UnlabeledParameterName, StringComparison.Ordinal))
            {
                fieldNames.Add(label);
            }
        }

        return fieldNames;
    }

    private static void ValidateTypeConstructorExpression(
        GesParsedScript parsedScriptContext,
        GesTypeConstructorExpressionNode constructor,
        IReadOnlyDictionary<string, GesCallableDefinition> callables,
        IReadOnlyDictionary<string, GesTypeDefinitionNode> typeDefinitions,
        GesValidationErrors errors,
        IReadOnlyDictionary<string, string>? declaredTypes = null)
    {
        ValidateDuplicateNamedArguments(parsedScriptContext, constructor.TypeName, constructor.Arguments, errors);
        for (var argumentIndex = 0; argumentIndex < constructor.Arguments.Count; argumentIndex++)
        {
            var argument = constructor.Arguments[argumentIndex];
            if (argument.Label is not null)
            {
                ValidateArgumentLabelCase(
                    parsedScriptContext,
                    argument.Label,
                    constructor.TypeName,
                    GesDiagnosticSymbolKind.Type,
                    $"Type constructor ':{constructor.TypeName}' declares invalid argument label '{argument.Label}'",
                    errors);
            }

            ValidateExpressionReferences(parsedScriptContext, argument.Expression, callables, typeDefinitions, errors, declaredTypes);
        }

        if (constructor.TypeName is "__split" or "__splitWhitespace") return;
        if (constructor.TypeName is "record" or "range" or "series" or "number" or "message" or "nothing" or "percentage" or "boolean" or "text" or "tag" or "list" or "map" or "dice" or "handler")
        {
            var labels = new string[constructor.Arguments.Count];
            for (var i = 0; i < labels.Length; i++) labels[i] = constructor.Arguments[i].Name;
            if (GesDataConstruction.Positions(constructor.TypeName, labels) is null)
                AddTypeConstructorError(parsedScriptContext, constructor.TypeName, "Invalid data constructor arguments", errors);
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

    private static void ValidateBuiltinTypeConstructor(GesParsedScript parsedScriptContext, GesTypeConstructorExpressionNode constructor, GesValidationErrors errors)
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

    private static void ValidateVectorConstructor(GesParsedScript parsedScriptContext, GesTypeConstructorExpressionNode constructor, IReadOnlyList<string> labels, GesValidationErrors errors)
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

    private static void ValidateLabeledVectorConstructor(GesParsedScript parsedScriptContext, GesTypeConstructorExpressionNode constructor, IReadOnlyList<string> labels, GesValidationErrors errors)
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
            GesQuantityUnits.ParseQuantityTypeName(typeName) is not null;

    private static void ValidateRemovedType(GesParsedScript parsedScriptContext, string typeName, GesScriptNode sourceNode, GesValidationErrors errors)
    {
        if (string.Equals(typeName, "optional", StringComparison.Ordinal))
        {
            errors.Add(
                parsedScriptContext,
                "Type ':optional' is not supported; use nothing to represent absence.",
                typeName,
                GesDiagnosticSymbolKind.Type,
                GesDiagnosticCodes.ValidateInvalidTypeConstructor,
                sourceNode);
            return;
        }

        if (string.Equals(typeName, "set", StringComparison.Ordinal))
        {
            errors.Add(
                parsedScriptContext,
                "Type ':set' is not supported; use lists or key-only maps.",
                typeName,
                GesDiagnosticSymbolKind.Type,
                GesDiagnosticCodes.ValidateInvalidTypeConstructor,
                sourceNode);
            return;
        }

        if (string.Equals(typeName, "uuid", StringComparison.Ordinal))
        {
            errors.Add(
                parsedScriptContext,
                "Type ':Uuid' is not supported; use ':Text' or ':Number' ids in scripts.",
                typeName,
                GesDiagnosticSymbolKind.Type,
                GesDiagnosticCodes.ValidateInvalidTypeConstructor,
                sourceNode);
            return;
        }

        if (string.Equals(typeName, "ref", StringComparison.Ordinal))
        {
            errors.Add(
                parsedScriptContext,
                "Type ':ref' is not supported.",
                typeName,
                GesDiagnosticSymbolKind.Type,
                GesDiagnosticCodes.ValidateInvalidTypeConstructor,
                sourceNode);
        }
    }

    private static void AddTypeConstructorError(GesParsedScript parsedScriptContext, string typeName, string message, GesValidationErrors errors)
        => errors.Add(
            parsedScriptContext,
            message,
            typeName,
            GesDiagnosticSymbolKind.Type,
            GesDiagnosticCodes.ValidateInvalidTypeConstructor);

    private static void ValidateCallArity(GesParsedScript parsedScriptContext, GesCallableKind kind, string name, int expectedCount, int actualCount, GesValidationErrors errors)
    {
        if (expectedCount != actualCount)
        {
            errors.Add(
                parsedScriptContext,
                $"{kind} '{name}' expects {expectedCount} argument(s) but received {actualCount}",
                name,
                kind == GesCallableKind.PredicateCall ? GesDiagnosticSymbolKind.Predicate : GesDiagnosticSymbolKind.Function,
                kind == GesCallableKind.PredicateCall ? GesDiagnosticCodes.ValidateWrongPredicateArity : GesDiagnosticCodes.ValidateWrongFunctionArity);
        }
    }

    private static void ValidateDuplicateNamedArguments(GesParsedScript parsedScriptContext, string symbolName, IReadOnlyList<GesArgumentNode> arguments, GesValidationErrors errors)
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
                GesDiagnosticSymbolKind.Handler,
                GesDiagnosticCodes.ValidateDuplicatePublishArgument,
                argument);
        }
    }

    private static void ValidateDuplicateHandlerLiteralParameters(GesParsedScript parsedScriptContext, GesHandlerLiteralExpressionNode handlerLiteral, GesValidationErrors errors)
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
                GesDiagnosticSymbolKind.Handler,
                GesDiagnosticCodes.ValidateDuplicateHandlerParameter,
                parameter);
        }
    }

    private static void ValidateDuplicateParameterDefinitions(
        GesParsedScript parsedScriptContext,
        string declarationName,
        GesDiagnosticSymbolKind symbolKind,
        string errorCode,
        string declarationKind,
        IReadOnlyList<GesParameterNode> parameters,
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

    private static void ValidateIdentifierCase(GesParsedScript parsedScriptContext, string name, string symbol, GesDiagnosticSymbolKind symbolKind, string message, GesValidationErrors errors)
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
            GesDiagnosticCodes.ValidateInvalidIdentifierCase);
    }

    private static void ValidateCallableCase(GesParsedScript parsedScriptContext, string name, string symbol, GesDiagnosticSymbolKind symbolKind, string message, GesValidationErrors errors)
    {
        if (GesText.IsCallableName(name)) return;
        errors.Add(parsedScriptContext, message, symbol, symbolKind, GesDiagnosticCodes.ValidateInvalidIdentifierCase);
    }

    private static void ValidateArgumentLabelCase(GesParsedScript parsedScriptContext, string name, string symbol, GesDiagnosticSymbolKind symbolKind, string message, GesValidationErrors errors)
    {
        if (GesText.IsArgumentLabel(name)) return;
        errors.Add(parsedScriptContext, message, symbol, symbolKind, GesDiagnosticCodes.ValidateInvalidIdentifierCase);
    }

    private static void ValidateMessageCase(GesParsedScript parsedScriptContext, string name, string symbol, GesDiagnosticSymbolKind symbolKind, string message, GesValidationErrors errors)
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
            GesDiagnosticCodes.ValidateInvalidMessageCase);
    }

    private static bool IsIdentifierCase(string name)
        => GesText.IsIdentifier(name);

    private static bool IsMessageCase(string name)
        => GesText.IsMessageName(name);
}
