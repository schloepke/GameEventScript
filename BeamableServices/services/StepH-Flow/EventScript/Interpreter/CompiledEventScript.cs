#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using StepH.Flow.EventScript.Linker;
using StepH.Flow.EventScript.Parser;
using StepH.Flow.EventScript.Runtime;
using StepH.Flow.EventScript.Types;

namespace StepH.Flow.EventScript.Interpreter;

internal delegate IReadOnlyDictionary<string, EventScriptValue> CompiledHandlerInvoker(EventScriptInvocationEngine engine, IReadOnlyDictionary<string, EventScriptValue> args);

internal delegate EventScriptValue CompiledCallableDefinitionInvoker(EventScriptInvocationEngine engine, IReadOnlyList<EventScriptValue> arguments);

public sealed class EventScriptEmittedEvent
{
    public EventScriptEmittedEvent(string message, EventScriptNamedArguments arguments)
        : this(new EventScriptMessage(message, arguments))
    {
    }

    public EventScriptEmittedEvent(EventScriptMessage @event)
    {
        Event = @event ?? new EventScriptMessage(string.Empty);
    }

    public EventScriptMessage Event { get; }

    public string Message => Event.Name;

    public EventScriptNamedArguments Arguments => Event.Arguments;
}

public sealed class EventScriptExecutionResult
{
    public EventScriptExecutionResult(string message, IReadOnlyList<EventScriptEmittedEvent> emittedEvents, IReadOnlyDictionary<string, EventScriptValue> variables)
        : this(new EventScriptMessage(message), emittedEvents, variables)
    {
    }

    public EventScriptExecutionResult(EventScriptMessage invocationMessage, IReadOnlyList<EventScriptEmittedEvent> emittedEvents, IReadOnlyDictionary<string, EventScriptValue> variables)
    {
        InvocationMessage = invocationMessage ?? new EventScriptMessage(string.Empty);
        EmittedEvents = emittedEvents ?? Array.Empty<EventScriptEmittedEvent>();
        Variables = variables ?? new Dictionary<string, EventScriptValue>(StringComparer.Ordinal);
    }

    public EventScriptMessage InvocationMessage { get; }

    public string Message => InvocationMessage.Name;

    public IReadOnlyList<EventScriptEmittedEvent> EmittedEvents { get; }

    public IReadOnlyDictionary<string, EventScriptValue> Variables { get; }
}

public sealed class CompiledEventScript : IEventScriptInvokableScript, IEventScriptHandlerCollection
{
    public CompiledEventScript(LinkedEventScriptModule linkedModule, EventScriptInterpreterCompilationOptions? options = null)
    {
        _ = linkedModule ?? throw new ArgumentNullException(nameof(linkedModule));
        Options = options ?? new EventScriptInterpreterCompilationOptions();

        var callables = linkedModule.Callables.ToDictionary(
            pair => pair.Key,
            pair => new CompiledCallableDefinition(
                pair.Value.Name,
                pair.Value.Parameters,
                pair.Value.Expression,
                pair.Value.Kind == LinkedCallableKind.Rule ? CallableKind.Rule : CallableKind.Select,
                Options.EnableDiagnostics),
            StringComparer.Ordinal);

        Callables = callables;

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

    public IReadOnlyDictionary<string, IReadOnlyList<CompiledEventScriptHandler>> Handlers { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<EventScriptMessageSignature>> MessageDefinitions
        => Handlers.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<EventScriptMessageSignature>)pair.Value.Select(handler => handler.Definition).ToArray(),
            StringComparer.Ordinal);

    internal IReadOnlyDictionary<string, CompiledCallableDefinition> Callables { get; }

    internal IReadOnlyDictionary<string, CompiledTypeDefinition> TypeDefinitions { get; }

    public bool DiagnosticsEnabled => Options.EnableDiagnostics;

    public EventScriptExecutionResult Invoke(EventScriptMessage message, EventScriptInvocationContext? invocationContext = null, IEventScriptDiagnosticCollector? diagnosticCollector = null)
        => new EventScriptInvocationEngine(this, invocationContext, diagnosticCollector).InvokeMessage(message);

    internal EventScriptExecutionResult InvokeHandler(CompiledEventScriptHandler handler, IReadOnlyDictionary<string, EventScriptValue> args, EventScriptInvocationContext? invocationContext,
        IEventScriptDiagnosticCollector? diagnosticCollector)
        => new EventScriptInvocationEngine(this, invocationContext, diagnosticCollector).InvokeHandler(handler, args);
}

internal enum CallableKind
{
    Rule,
    Select
}

internal sealed class CompiledCallableDefinition(string name, IReadOnlyList<string> parameters, ExpressionNode expression, CallableKind kind, bool diagnosticsEnabled)
{
    public string Name { get; } = name;

    public IReadOnlyList<string> Parameters { get; } = parameters.ToArray();

    public ExpressionNode Expression { get; } = expression ?? throw new ArgumentNullException(nameof(expression));

    public CallableKind Kind { get; } = kind;

    public bool DiagnosticsEnabled { get; } = diagnosticsEnabled;
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
        SignatureId = EventScriptMessageSignature.CreateSignatureId(Message, Parameters);
        DiagnosticsEnabled = diagnosticsEnabled;
        Definition = new EventScriptMessageSignature(Message, Parameters);
    }

    internal IReadOnlyList<StatementNode> Statements { get; }

    internal bool DiagnosticsEnabled { get; }

    public string Message { get; }

    public int DeclarationOrder { get; }

    public IReadOnlyList<string> Parameters { get; }

    public string SignatureId { get; }

    public EventScriptMessageSignature Definition { get; }
}
