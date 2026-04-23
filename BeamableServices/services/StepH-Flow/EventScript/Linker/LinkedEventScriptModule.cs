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

public sealed class LinkedCallableDefinition(string name, IReadOnlyList<string> parameters, ExpressionNode expression, LinkedCallableKind kind)
{
    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

    public IReadOnlyList<string> Parameters { get; } = parameters ?? throw new ArgumentNullException(nameof(parameters));

    public ExpressionNode Expression { get; } = expression ?? throw new ArgumentNullException(nameof(expression));

    public LinkedCallableKind Kind { get; } = kind;
}

public sealed class LinkedEventScriptModule
{
    public LinkedEventScriptModule(
        IReadOnlyDictionary<string, TypeDefinitionNode> typeDefinitions,
        IReadOnlyDictionary<string, RuleDefinitionNode> ruleDefinitions,
        IReadOnlyDictionary<string, SelectDefinitionNode> selectDefinitions,
        IReadOnlyDictionary<string, IReadOnlyList<EventHandlerNode>> handlers,
        int sourceCount)
    {
        TypeDefinitions = typeDefinitions ?? throw new ArgumentNullException(nameof(typeDefinitions));
        RuleDefinitions = ruleDefinitions ?? throw new ArgumentNullException(nameof(ruleDefinitions));
        SelectDefinitions = selectDefinitions ?? throw new ArgumentNullException(nameof(selectDefinitions));
        Handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
        SourceCount = sourceCount;
        Callables = BuildCallables(RuleDefinitions, SelectDefinitions);
    }

    public IReadOnlyDictionary<string, TypeDefinitionNode> TypeDefinitions { get; }

    public IReadOnlyDictionary<string, RuleDefinitionNode> RuleDefinitions { get; }

    public IReadOnlyDictionary<string, SelectDefinitionNode> SelectDefinitions { get; }

    public IReadOnlyDictionary<string, LinkedCallableDefinition> Callables { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<EventHandlerNode>> Handlers { get; }

    public int SourceCount { get; }

    private static IReadOnlyDictionary<string, LinkedCallableDefinition> BuildCallables(
        IReadOnlyDictionary<string, RuleDefinitionNode> ruleDefinitions,
        IReadOnlyDictionary<string, SelectDefinitionNode> selectDefinitions)
    {
        var map = new Dictionary<string, LinkedCallableDefinition>(StringComparer.Ordinal);
        foreach (var pair in ruleDefinitions)
        {
            map[pair.Key] = new LinkedCallableDefinition(
                pair.Key,
                pair.Value.Parameters.ToArray(),
                pair.Value.Expression,
                LinkedCallableKind.Rule);
        }

        foreach (var pair in selectDefinitions)
        {
            map[pair.Key] = new LinkedCallableDefinition(
                pair.Key,
                pair.Value.Parameters.ToArray(),
                pair.Value.Expression,
                LinkedCallableKind.Select);
        }

        return map;
    }
}
