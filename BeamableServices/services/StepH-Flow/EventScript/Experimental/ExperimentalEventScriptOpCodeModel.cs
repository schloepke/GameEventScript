#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using StepH.Flow.EventScript.Interpreter;
using StepH.Flow.EventScript.Parser;
using StepH.Flow.EventScript.Types;

namespace StepH.Flow.EventScript.Experimental;

internal readonly record struct ExperimentalInstruction(
    ExperimentalOpCode OpCode,
    int A = -1,
    int B = -1,
    int C = -1,
    int D = -1);

internal sealed class ExperimentalEventScript(IReadOnlyList<ExperimentalInstruction> instructions)
{
    public IReadOnlyList<ExperimentalInstruction> Instructions { get; } = instructions ?? throw new ArgumentNullException(nameof(instructions));
}

internal sealed class ExperimentalCompiledExpression(ExpressionNode expressionNode, EventScriptValue? constantValue = null)
{
    public ExpressionNode ExpressionNode { get; } = expressionNode ?? throw new ArgumentNullException(nameof(expressionNode));

    public EventScriptValue? ConstantValue { get; } = constantValue;

    public bool IsConstant => ConstantValue is not null;
}

internal sealed class ExperimentalCompiledNamedArgument(string name, ExperimentalCompiledExpression expression)
{
    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

    public ExperimentalCompiledExpression Expression { get; } = expression ?? throw new ArgumentNullException(nameof(expression));
}

internal sealed class ExperimentalCompiledNamedArgumentList(IReadOnlyList<ExperimentalCompiledNamedArgument> arguments)
{
    public IReadOnlyList<ExperimentalCompiledNamedArgument> Arguments { get; } = arguments ?? throw new ArgumentNullException(nameof(arguments));
}

internal enum ExperimentalIterationSourceKind
{
    Collection,
    Range
}

internal sealed class ExperimentalCompiledIterationSource(ExperimentalIterationSourceKind kind, ExperimentalCompiledExpression? collectionExpression, ExperimentalCompiledExpression? fromExpression, ExperimentalCompiledExpression? toExpression, ExperimentalCompiledExpression? stepExpression)
{
    public ExperimentalIterationSourceKind Kind { get; } = kind;

    public ExperimentalCompiledExpression? CollectionExpression { get; } = collectionExpression;

    public ExperimentalCompiledExpression? FromExpression { get; } = fromExpression;

    public ExperimentalCompiledExpression? ToExpression { get; } = toExpression;

    public ExperimentalCompiledExpression? StepExpression { get; } = stepExpression;
}

internal enum ExperimentalGlobalDefinitionKind
{
    Rule,
    Select
}

internal sealed class ExperimentalCompiledGlobalDefinition(string name, IReadOnlyList<string> parameters, ExperimentalCompiledExpression expression, ExperimentalGlobalDefinitionKind kind, bool diagnosticsEnabled)
{
    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

    public IReadOnlyList<string> Parameters { get; } = parameters ?? throw new ArgumentNullException(nameof(parameters));

    public ExperimentalCompiledExpression Expression { get; } = expression ?? throw new ArgumentNullException(nameof(expression));

    public ExperimentalGlobalDefinitionKind Kind { get; } = kind;

    public bool DiagnosticsEnabled { get; } = diagnosticsEnabled;
}

internal sealed class ExperimentalCompiledTypeDefinition(string name, IReadOnlyList<ExperimentalCompiledTypeFieldDefinition> fields)
{
    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

    public IReadOnlyList<ExperimentalCompiledTypeFieldDefinition> Fields { get; } = fields ?? throw new ArgumentNullException(nameof(fields));
}

internal sealed class ExperimentalCompiledTypeFieldDefinition(string name, string typeName, ExperimentalCompiledExpression? minimumExpression, ExperimentalCompiledExpression? maximumExpression, ExperimentalCompiledExpression? computedExpression)
{
    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

    public string TypeName { get; } = typeName ?? throw new ArgumentNullException(nameof(typeName));

    public ExperimentalCompiledExpression? MinimumExpression { get; } = minimumExpression;

    public ExperimentalCompiledExpression? MaximumExpression { get; } = maximumExpression;

    public ExperimentalCompiledExpression? ComputedExpression { get; } = computedExpression;
}
