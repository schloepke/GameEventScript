// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;

namespace StepH.GameEventScript.Compiler;

internal enum GameEventScriptCallableKind
{
    PredicateCall,
    FunctionCall
}

internal sealed class GesSyntaxTreeModule
{
    internal GesSyntaxTreeModule(string moduleName,
        IReadOnlyDictionary<string, ExpressionNode> constants,
        IReadOnlyDictionary<string, TypeDefinitionNode> typeDefinitions, IReadOnlyDictionary<string, GesCallableDefinition> callables,
        IReadOnlyDictionary<string, IReadOnlyList<EventHandlerNode>> handlers,
        IGameEventScriptExternalTypeCatalog? externalTypeDefinitions = null,
        IReadOnlyList<GesSourceDocument>? sources = null)
    {
        ModuleName = moduleName ?? throw new ArgumentNullException(nameof(moduleName));
        Constants = constants ?? throw new ArgumentNullException(nameof(constants));
        TypeDefinitions = typeDefinitions ?? throw new ArgumentNullException(nameof(typeDefinitions));
        Callables = callables ?? throw new ArgumentNullException(nameof(callables));
        Handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
        ExternalTypeDefinitions = externalTypeDefinitions ?? GameEventScriptEmptyExternalTypeCatalog.Instance;
        Sources = sources ?? [];
    }

    internal string ModuleName { get; }

    internal IReadOnlyDictionary<string, ExpressionNode> Constants { get; }

    internal IReadOnlyDictionary<string, TypeDefinitionNode> TypeDefinitions { get; }

    internal IGameEventScriptExternalTypeCatalog ExternalTypeDefinitions { get; }

    internal IReadOnlyDictionary<string, GesCallableDefinition> Callables { get; }

    internal IReadOnlyDictionary<string, IReadOnlyList<EventHandlerNode>> Handlers { get; }

    internal IReadOnlyList<GesSourceDocument> Sources { get; }
}

internal sealed record GesSourceDocument(uint SourceId, string SourceName, string Text);

internal sealed class GesCallableDefinition(string name, IReadOnlyList<ParameterNode> parameterList, ExpressionNode expression, GameEventScriptCallableKind kind, GameEventScriptSourceLocation? sourceRange = null)
{
    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

    public IReadOnlyList<ParameterNode> ParameterList { get; } = parameterList ?? throw new ArgumentNullException(nameof(parameterList));

    public IReadOnlyList<string> Parameters { get; } = GesSyntaxTreeNodeLists.ToParameterNames(parameterList);

    public IReadOnlyList<string> SignatureLabels { get; } = GesSyntaxTreeNodeLists.ToSignatureLabels(parameterList);

    public string SignatureId { get; } = GesCallableSignatures.Create(name, parameterList);

    public ExpressionNode Expression { get; } = expression ?? throw new ArgumentNullException(nameof(expression));

    public GameEventScriptCallableKind Kind { get; } = kind;

    public GameEventScriptSourceLocation? SourceRange { get; } = sourceRange;
}

internal static class GesCallableSignatures
{
    public static string Create(string name, IReadOnlyList<ParameterNode> parameters)
    {
        var labels = new string[parameters.Count];
        for (var index = 0; index < labels.Length; index++) labels[index] = parameters[index].SignatureLabel;
        return Create(name, labels);
    }

    public static string Create(string name, IReadOnlyList<ArgumentNode> arguments)
    {
        var labels = new string[arguments.Count];
        for (var index = 0; index < labels.Length; index++) labels[index] = arguments[index].Name;
        return Create(name, labels);
    }

    public static string Create(string name, IReadOnlyList<string> labels)
        => name + "(" + string.Join(",", labels) + ")";

    public static bool HasName(IReadOnlyDictionary<string, GesCallableDefinition> callables, string name)
    {
        foreach (var callable in callables.Values)
            if (string.Equals(callable.Name, name, StringComparison.Ordinal)) return true;
        return false;
    }

    public static GesCallableDefinition? Resolve(IReadOnlyDictionary<string, GesCallableDefinition> callables, CallExpressionNode call)
    {
        callables.TryGetValue(Create(call.Name, call.ArgumentList.Arguments), out var result);
        return result;
    }

    public static GesCallableDefinition? ResolveSingleParameterPredicate(IReadOnlyDictionary<string, GesCallableDefinition> callables, string name)
    {
        GesCallableDefinition? result = null;
        foreach (var callable in callables.Values)
        {
            if (callable.Kind != GameEventScriptCallableKind.PredicateCall || callable.Parameters.Count != 1 || !string.Equals(callable.Name, name, StringComparison.Ordinal)) continue;
            if (result is not null) return null;
            result = callable;
        }
        return result;
    }
}
