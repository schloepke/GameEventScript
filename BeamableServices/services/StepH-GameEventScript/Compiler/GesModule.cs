using System;
using System.Collections.Generic;
using System.Linq;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime;

namespace StepH.GameEventScript.Compiler;

internal enum GameEventScriptCallableKind
{
    Rule,
    Select
}

internal sealed class GesModule
{
    internal GesModule(IReadOnlyDictionary<string, TypeDefinitionNode> typeDefinitions, IReadOnlyDictionary<string, GesCallableDefinition> callables,
        IReadOnlyDictionary<string, IReadOnlyList<EventHandlerNode>> handlers,
        IReadOnlyDictionary<string, GameEventScriptExternalTypeDefinition>? externalTypeDefinitions = null)
    {
        TypeDefinitions = typeDefinitions ?? throw new ArgumentNullException(nameof(typeDefinitions));
        Callables = callables ?? throw new ArgumentNullException(nameof(callables));
        Handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
        ExternalTypeDefinitions = externalTypeDefinitions ?? new Dictionary<string, GameEventScriptExternalTypeDefinition>(StringComparer.Ordinal);
    }

    internal IReadOnlyDictionary<string, TypeDefinitionNode> TypeDefinitions { get; }

    internal IReadOnlyDictionary<string, GameEventScriptExternalTypeDefinition> ExternalTypeDefinitions { get; }

    internal IReadOnlyDictionary<string, GesCallableDefinition> Callables { get; }

    internal IReadOnlyDictionary<string, IReadOnlyList<EventHandlerNode>> Handlers { get; }
}

internal sealed class GesCallableDefinition(
    string name,
    IReadOnlyList<ParameterNode> parameterList,
    ExpressionNode expression,
    GameEventScriptCallableKind kind,
    GameEventScriptSourceLocation? sourceRange = null)
{
    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

    public IReadOnlyList<ParameterNode> ParameterList { get; } = parameterList ?? throw new ArgumentNullException(nameof(parameterList));

    public IReadOnlyList<string> Parameters { get; } = parameterList.Select(parameter => parameter.LocalName).ToArray();

    public IReadOnlyList<string> SignatureLabels { get; } = parameterList.Select(parameter => parameter.SignatureLabel).ToArray();

    public ExpressionNode Expression { get; } = expression ?? throw new ArgumentNullException(nameof(expression));

    public GameEventScriptCallableKind Kind { get; } = kind;

    public GameEventScriptSourceLocation? SourceRange { get; } = sourceRange;
}
