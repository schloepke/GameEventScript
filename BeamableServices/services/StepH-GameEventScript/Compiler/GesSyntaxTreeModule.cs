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
        IReadOnlyDictionary<string, TypeDefinitionNode> typeDefinitions, IReadOnlyDictionary<string, GesCallableDefinition> callables,
        IReadOnlyDictionary<string, IReadOnlyList<EventHandlerNode>> handlers,
        IGameEventScriptExternalTypeCatalog? externalTypeDefinitions = null,
        IReadOnlyList<GesSourceDocument>? sources = null)
    {
        ModuleName = string.IsNullOrWhiteSpace(moduleName) ? "UnknownModule" : moduleName;
        TypeDefinitions = typeDefinitions ?? throw new ArgumentNullException(nameof(typeDefinitions));
        Callables = callables ?? throw new ArgumentNullException(nameof(callables));
        Handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
        ExternalTypeDefinitions = externalTypeDefinitions ?? GameEventScriptEmptyExternalTypeCatalog.Instance;
        Sources = sources ?? [];
    }

    internal string ModuleName { get; }

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

    public ExpressionNode Expression { get; } = expression ?? throw new ArgumentNullException(nameof(expression));

    public GameEventScriptCallableKind Kind { get; } = kind;

    public GameEventScriptSourceLocation? SourceRange { get; } = sourceRange;
}
