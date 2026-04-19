#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using StepH.Flow.EventScript.Parser;

namespace StepH.Flow.EventScript.Linker;

public sealed class LinkedEventScriptModule(
    IReadOnlyDictionary<string, TypeDefinitionNode> typeDefinitions,
    IReadOnlyDictionary<string, RuleDefinitionNode> ruleDefinitions,
    IReadOnlyDictionary<string, SelectDefinitionNode> selectDefinitions,
    IReadOnlyDictionary<string, IReadOnlyList<EventHandlerNode>> handlers,
    int sourceCount)
{
    public IReadOnlyDictionary<string, TypeDefinitionNode> TypeDefinitions { get; } = typeDefinitions ?? throw new ArgumentNullException(nameof(typeDefinitions));

    public IReadOnlyDictionary<string, RuleDefinitionNode> RuleDefinitions { get; } = ruleDefinitions ?? throw new ArgumentNullException(nameof(ruleDefinitions));

    public IReadOnlyDictionary<string, SelectDefinitionNode> SelectDefinitions { get; } = selectDefinitions ?? throw new ArgumentNullException(nameof(selectDefinitions));

    public IReadOnlyDictionary<string, IReadOnlyList<EventHandlerNode>> Handlers { get; } = handlers ?? throw new ArgumentNullException(nameof(handlers));

    public int SourceCount { get; } = sourceCount;
}
