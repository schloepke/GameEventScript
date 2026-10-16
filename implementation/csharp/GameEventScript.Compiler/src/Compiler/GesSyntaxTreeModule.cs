// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using GameEventScript.Api;
using GameEventScript.Runtime;

namespace GameEventScript.Compiler;

internal enum GesCallableKind
{
    PredicateCall,
    FunctionCall
}

internal sealed class GesSyntaxTreeModule
{
    internal GesSyntaxTreeModule(string moduleName,
        IReadOnlyDictionary<string, GesExpressionNode> constants,
        IReadOnlyDictionary<string, GesTypeDefinitionNode> typeDefinitions, IReadOnlyDictionary<string, GesCallableDefinition> callables,
        IReadOnlyDictionary<string, IReadOnlyList<GesEventHandlerNode>> handlers,
        IGesExternalTypeCatalog? externalTypeDefinitions = null,
        IReadOnlyList<GesSourceDocument>? sources = null)
    {
        ModuleName = moduleName ?? throw new ArgumentNullException(nameof(moduleName));
        Constants = constants ?? throw new ArgumentNullException(nameof(constants));
        TypeDefinitions = typeDefinitions ?? throw new ArgumentNullException(nameof(typeDefinitions));
        Callables = callables ?? throw new ArgumentNullException(nameof(callables));
        Handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
        ExternalTypeDefinitions = externalTypeDefinitions ?? GesEmptyExternalTypeCatalog.Instance;
        Sources = sources ?? [];
    }

    internal string ModuleName { get; }

    internal IReadOnlyDictionary<string, GesExpressionNode> Constants { get; }

    internal IReadOnlyDictionary<string, GesTypeDefinitionNode> TypeDefinitions { get; }

    internal IGesExternalTypeCatalog ExternalTypeDefinitions { get; }

    internal IReadOnlyDictionary<string, GesCallableDefinition> Callables { get; }

    internal IReadOnlyDictionary<string, IReadOnlyList<GesEventHandlerNode>> Handlers { get; }

    internal IReadOnlyList<GesSourceDocument> Sources { get; }
}

internal sealed record GesSourceDocument(uint SourceId, string SourceName, string Text);

internal sealed class GesCallableDefinition(string name, IReadOnlyList<GesParameterNode> parameterList, GesExpressionNode expression, GesCallableKind kind, GesSourceLocation? sourceRange = null)
{
    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

    public IReadOnlyList<GesParameterNode> ParameterList { get; } = parameterList ?? throw new ArgumentNullException(nameof(parameterList));

    public IReadOnlyList<string> Parameters { get; } = GesSyntaxTreeNodeLists.ToParameterNames(parameterList);

    public IReadOnlyList<string> SignatureLabels { get; } = GesSyntaxTreeNodeLists.ToSignatureLabels(parameterList);

    public string SignatureId { get; } = GesCallableSignatures.Create(name, parameterList);

    public GesExpressionNode Expression { get; } = expression ?? throw new ArgumentNullException(nameof(expression));

    public GesCallableKind Kind { get; } = kind;

    public GesSourceLocation? SourceRange { get; } = sourceRange;
}

internal static class GesCallableSignatures
{
    public static string Create(string name, IReadOnlyList<GesParameterNode> parameters)
    {
        var labels = new string[parameters.Count];
        for (var index = 0; index < labels.Length; index++) labels[index] = parameters[index].SignatureLabel;
        return Create(name, labels);
    }

    public static string Create(string name, IReadOnlyList<GesArgumentNode> arguments)
    {
        var labels = new string[arguments.Count];
        for (var index = 0; index < labels.Length; index++) labels[index] = arguments[index].Name;
        return Create(name, labels);
    }

    public static string Create(string name, IReadOnlyList<string> labels) => name + "(" + string.Join(",", labels) + ")";

    public static bool HasName(IReadOnlyDictionary<string, GesCallableDefinition> callables, string name)
    {
        foreach (var callable in callables.Values)
            if (string.Equals(callable.Name, name, StringComparison.Ordinal)) return true;
        return false;
    }

    public static GesCallableDefinition? Resolve(IReadOnlyDictionary<string, GesCallableDefinition> callables, GesCallExpressionNode call)
    {
        callables.TryGetValue(Create(call.Name, call.ArgumentList.Arguments), out var result);
        return result;
    }

    public static GesCallableDefinition? ResolvePredicate(IReadOnlyDictionary<string, GesCallableDefinition> callables, string name, int parameterCount)
    {
        GesCallableDefinition? result = null;
        foreach (var callable in callables.Values)
        {
            if (callable.Kind != GesCallableKind.PredicateCall || callable.Parameters.Count != parameterCount || !string.Equals(callable.Name, name, StringComparison.Ordinal)) continue;
            if (result is not null) return null;
            result = callable;
        }
        return result;
    }
}
