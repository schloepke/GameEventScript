#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using StepH.Flow.EventScript.Parser;

namespace StepH.Flow.EventScript.Linker;

public enum LinkedCallableKind
{
    Rule,
    Select
}

public sealed class LinkedCallableDefinition(
    string name,
    IReadOnlyList<ParameterNode> parameterList,
    ExpressionNode expression,
    LinkedCallableKind kind,
    EventScriptSourceLocation? sourceRange = null)
{
    public LinkedCallableDefinition(
        string name,
        IReadOnlyList<string> parameters,
        ExpressionNode expression,
        LinkedCallableKind kind,
        EventScriptSourceLocation? sourceRange = null)
        : this(name, Array.ConvertAll(parameters is null ? [] : parameters.ToArray(), parameter => new ParameterNode(parameter, parameter)), expression, kind, sourceRange)
    {
    }

    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

    public IReadOnlyList<ParameterNode> ParameterList { get; } = parameterList ?? throw new ArgumentNullException(nameof(parameterList));

    public IReadOnlyList<string> Parameters { get; } = parameterList.Select(parameter => parameter.LocalName).ToArray();

    public IReadOnlyList<string> SignatureLabels { get; } = parameterList.Select(parameter => parameter.SignatureLabel).ToArray();

    public ExpressionNode Expression { get; } = expression ?? throw new ArgumentNullException(nameof(expression));

    public LinkedCallableKind Kind { get; } = kind;

    public EventScriptSourceLocation? SourceRange { get; } = sourceRange;
}

public sealed class LinkedEventScriptModule
{
    public LinkedEventScriptModule(
        IReadOnlyDictionary<string, TypeDefinitionNode> typeDefinitions,
        IReadOnlyDictionary<string, LinkedCallableDefinition> callables,
        IReadOnlyDictionary<string, IReadOnlyList<EventHandlerNode>> handlers,
        int sourceCount)
    {
        TypeDefinitions = typeDefinitions ?? throw new ArgumentNullException(nameof(typeDefinitions));
        Callables = callables ?? throw new ArgumentNullException(nameof(callables));
        Handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
        SourceCount = sourceCount;
    }

    public IReadOnlyDictionary<string, TypeDefinitionNode> TypeDefinitions { get; }

    public IReadOnlyDictionary<string, LinkedCallableDefinition> Callables { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<EventHandlerNode>> Handlers { get; }

    public int SourceCount { get; }
}
