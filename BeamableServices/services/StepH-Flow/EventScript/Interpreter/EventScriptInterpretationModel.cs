#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using StepH.Flow.EventScript.Linker;
using StepH.Flow.EventScript.Parser;
using StepH.Flow.EventScript.Types;

namespace StepH.Flow.EventScript.Interpreter;

internal delegate IReadOnlyDictionary<string, EventScriptValue> CompiledHandlerInvoker(EventScriptInvocationEngine engine, IReadOnlyDictionary<string, EventScriptValue> args);
internal delegate EventScriptValue CompiledGlobalDefinitionInvoker(EventScriptInvocationEngine engine, IReadOnlyList<EventScriptValue> arguments);

public sealed record EventScriptEmittedEvent(string Message, EventScriptNamedArguments Arguments);

public sealed record EventScriptExecutionResult(string Message, IReadOnlyList<EventScriptEmittedEvent> EmittedEvents, IReadOnlyDictionary<string, EventScriptValue> Variables);

public sealed class CompiledEventScript
{
    public CompiledEventScript(LinkedEventScriptModule linkedModule, EventScriptInterpreterCompilationOptions? options = null, IEventScriptRandom? defaultRandom = null)
    {
        _ = linkedModule ?? throw new ArgumentNullException(nameof(linkedModule));
        Options = options ?? new EventScriptInterpreterCompilationOptions();
        DefaultRandom = defaultRandom;

        RuleDefinitions = linkedModule.RuleDefinitions.ToDictionary(
            pair => pair.Key,
            pair => new CompiledGlobalDefinition(pair.Key, pair.Value.Parameters, pair.Value.Expression, GlobalDefinitionKind.Rule, Options.EnableDiagnostics),
            StringComparer.Ordinal);

        SelectDefinitions = linkedModule.SelectDefinitions.ToDictionary(
            pair => pair.Key,
            pair => new CompiledGlobalDefinition(pair.Key, pair.Value.Parameters, pair.Value.Expression, GlobalDefinitionKind.Select, Options.EnableDiagnostics),
            StringComparer.Ordinal);

        TypeDefinitions = linkedModule.TypeDefinitions.ToDictionary(
            pair => pair.Key,
            pair => new CompiledTypeDefinition(pair.Value),
            StringComparer.Ordinal);

        Handlers = linkedModule.Handlers.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<CompiledEventScriptHandler>)pair.Value
                .Select((handler, index) => new CompiledEventScriptHandler(pair.Key, handler, index, Options.EnableDiagnostics))
                .ToArray(),
            StringComparer.Ordinal);
    }

    public EventScriptInterpreterCompilationOptions Options { get; }

    internal IEventScriptRandom? DefaultRandom { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<CompiledEventScriptHandler>> Handlers { get; }

    internal IReadOnlyDictionary<string, CompiledGlobalDefinition> RuleDefinitions { get; }

    internal IReadOnlyDictionary<string, CompiledGlobalDefinition> SelectDefinitions { get; }

    internal IReadOnlyDictionary<string, CompiledTypeDefinition> TypeDefinitions { get; }

    public bool DiagnosticsEnabled => Options.EnableDiagnostics;

    public EventScriptExecutionResult Invoke(string message)
        => Invoke(message, EventScriptArgumentMap.Empty);

    public EventScriptExecutionResult Invoke(string message, IReadOnlyDictionary<string, EventScriptValue> args)
        => Invoke(message, args, invocationContext: null, diagnosticCollector: null);

    public EventScriptExecutionResult Invoke(
        string message,
        IReadOnlyDictionary<string, EventScriptValue> args,
        EventScriptInvocationContext? invocationContext,
        IEventScriptDiagnosticCollector? diagnosticCollector)
    {
        var engine = new EventScriptInvocationEngine(this, invocationContext, diagnosticCollector);
        return engine.InvokeMessage(message, args);
    }

    public EventScriptExecutionResult Emit(string message)
        => Invoke(message);

    public EventScriptExecutionResult Emit(string message, IReadOnlyDictionary<string, EventScriptValue> args)
        => Invoke(message, args);

    public EventScriptExecutionResult Emit(string message, params (string Name, EventScriptValue Value)[] args)
        => Emit(message, args.ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal));

    public EventScriptExecutionResult EmitClr(string message, IReadOnlyDictionary<string, object?> args)
        => Invoke(message, EventScriptArgumentMap.FromClr(args));

    public EventScriptExecutionResult EmitClr(string message, params (string Name, object? Value)[] args)
        => EmitClr(message, args.ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal));

    internal EventScriptExecutionResult InvokeHandler(
        CompiledEventScriptHandler handler,
        IReadOnlyDictionary<string, EventScriptValue> args,
        EventScriptInvocationContext? invocationContext,
        IEventScriptDiagnosticCollector? diagnosticCollector)
    {
        var engine = new EventScriptInvocationEngine(this, invocationContext, diagnosticCollector);
        return engine.InvokeHandler(handler, args);
    }
}

internal enum GlobalDefinitionKind
{
    Rule,
    Select
}

internal sealed class CompiledGlobalDefinition
{
    public CompiledGlobalDefinition(
        string name,
        IReadOnlyList<string> parameters,
        ExpressionNode expression,
        GlobalDefinitionKind kind,
        bool diagnosticsEnabled)
    {
        Name = name;
        Parameters = parameters.ToArray();
        Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        Kind = kind;
        DiagnosticsEnabled = diagnosticsEnabled;
    }

    public string Name { get; }

    public IReadOnlyList<string> Parameters { get; }

    public ExpressionNode Expression { get; }

    public GlobalDefinitionKind Kind { get; }

    public bool DiagnosticsEnabled { get; }
}

internal sealed class CompiledTypeDefinition
{
    public CompiledTypeDefinition(TypeDefinitionNode syntax)
    {
        _ = syntax ?? throw new ArgumentNullException(nameof(syntax));
        Name = syntax.Name;
        Fields = syntax.Fields.Select(field => new CompiledTypeFieldDefinition(field)).ToArray();
    }

    public string Name { get; }

    public IReadOnlyList<CompiledTypeFieldDefinition> Fields { get; }
}

internal sealed class CompiledTypeFieldDefinition
{
    public CompiledTypeFieldDefinition(TypeFieldDefinitionNode syntax)
    {
        _ = syntax ?? throw new ArgumentNullException(nameof(syntax));
        Name = syntax.Name;
        TypeName = syntax.TypeName;
        MinimumExpression = syntax.MinimumExpression;
        MaximumExpression = syntax.MaximumExpression;
        ComputedExpression = syntax.ComputedExpression;
    }

    public string Name { get; }

    public string TypeName { get; }

    public ExpressionNode? MinimumExpression { get; }

    public ExpressionNode? MaximumExpression { get; }

    public ExpressionNode? ComputedExpression { get; }
}

public sealed class CompiledEventScriptHandler
{
    internal CompiledEventScriptHandler(string message, EventHandlerNode syntax, int declarationOrder, bool diagnosticsEnabled)
    {
        _ = syntax ?? throw new ArgumentNullException(nameof(syntax));
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Parameters = syntax.Parameters.ToArray();
        Statements = syntax.Statements.ToArray();
        DeclarationOrder = declarationOrder;
        SignatureKey = EventScriptArgumentMap.CreateSignatureKey(Parameters);
        DiagnosticsEnabled = diagnosticsEnabled;
    }

    internal IReadOnlyList<StatementNode> Statements { get; }

    internal bool DiagnosticsEnabled { get; }

    public string Message { get; }

    public int DeclarationOrder { get; }

    public IReadOnlyList<string> Parameters { get; }

    public string SignatureKey { get; }
}
