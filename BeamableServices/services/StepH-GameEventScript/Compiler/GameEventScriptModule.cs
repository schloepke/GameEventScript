using System;
using System.Collections.Generic;
using System.Linq;

namespace StepH.GameEventScript.Compiler;

internal enum GameEventScriptCallableKind
{
    Rule,
    Select
}

/// <summary>
/// Represents a compiled module of Game Event Scripts, containing type definitions,
/// callable definitions, and event handler definitions. Designed to facilitate
/// execution or further compilation processes for game event scripting.
/// </summary>
public sealed class GameEventScriptModule
{
    internal GameEventScriptModule(IReadOnlyDictionary<string, TypeDefinitionNode> typeDefinitions, IReadOnlyDictionary<string, GameEventScriptCallableDefinition> callables,
        IReadOnlyDictionary<string, IReadOnlyList<EventHandlerNode>> handlers)
    {
        TypeDefinitions = typeDefinitions ?? throw new ArgumentNullException(nameof(typeDefinitions));
        Callables = callables ?? throw new ArgumentNullException(nameof(callables));
        Handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
    }

    internal IReadOnlyDictionary<string, TypeDefinitionNode> TypeDefinitions { get; }

    internal IReadOnlyDictionary<string, GameEventScriptCallableDefinition> Callables { get; }

    internal IReadOnlyDictionary<string, IReadOnlyList<EventHandlerNode>> Handlers { get; }
}

internal sealed class GameEventScriptCallableDefinition(
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