#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using StepH.Flow.EventScript.Linker;
using StepH.Flow.EventScript.Parser;
using StepH.Flow.EventScript.Runtime;

namespace StepH.Flow.EventScript.Interpreter;

public sealed class CompiledEventScript : IEventScriptMessageHandlerCollection
{
    private readonly IReadOnlyList<(EventScriptMessageSignature Signature, Action<EventScriptMessage, EventScriptContext> Handler)> _messageHandlers;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<CompiledEventScriptHandler>> _dispatchIndex;

    public CompiledEventScript(LinkedEventScriptModule linkedModule, EventScriptInterpreterCompilationOptions? options = null)
    {
        _ = linkedModule ?? throw new ArgumentNullException(nameof(linkedModule));
        Options = options ?? new EventScriptInterpreterCompilationOptions();

        var callables = linkedModule.Callables.ToDictionary(
            pair => pair.Key,
            pair => new CompiledCallableDefinition(
                pair.Value.Name,
                pair.Value.ParameterList,
                pair.Value.Parameters,
                pair.Value.SignatureLabels,
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

        var handlerList = Handlers.Values.SelectMany(handlerGroup => handlerGroup).ToArray();
        _dispatchIndex = EventScriptInvocationKernel.BuildDispatchIndex(handlerList, handler => handler.SignatureId, handler => handler.DeclarationOrder);
        _messageHandlers = handlerList
            .Select(handler => (handler.Definition, (Action<EventScriptMessage, EventScriptContext>)((message, context) => InvokeHandler(handler, message, context))))
            .ToArray();
    }

    public EventScriptInterpreterCompilationOptions Options { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<CompiledEventScriptHandler>> Handlers { get; }

    internal IReadOnlyDictionary<string, CompiledCallableDefinition> Callables { get; }

    internal IReadOnlyDictionary<string, CompiledTypeDefinition> TypeDefinitions { get; }

    internal IReadOnlyDictionary<string, IReadOnlyList<CompiledEventScriptHandler>> DispatchIndex => _dispatchIndex;

    public bool DiagnosticsEnabled => Options.EnableDiagnostics;

    IEnumerable<(EventScriptMessageSignature Signature, Action<EventScriptMessage, EventScriptContext> Handler)> IEventScriptMessageHandlerCollection.Handlers
        => _messageHandlers;

    public void Invoke(EventScriptMessage message, EventScriptContext context)
        => EventScriptInvocationEngine.InvokeMessage(this, context, message);

    internal void InvokeHandler(CompiledEventScriptHandler handler, EventScriptMessage message, EventScriptContext context)
        => EventScriptInvocationEngine.InvokeHandler(this, context, handler, message.Arguments);
}

internal enum CallableKind
{
    Rule,
    Select
}

internal sealed class CompiledCallableDefinition(string name, IReadOnlyList<ParameterNode> parameterList, IReadOnlyList<string> parameters, IReadOnlyList<string> signatureLabels, ExpressionNode expression, CallableKind kind, bool diagnosticsEnabled)
{
    public string Name { get; } = name;

    public IReadOnlyList<ParameterNode> ParameterList { get; } = parameterList.ToArray();

    public IReadOnlyList<string> Parameters { get; } = parameters.ToArray();

    public IReadOnlyList<string> SignatureLabels { get; } = signatureLabels.ToArray();

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
        SignatureLabels = syntax.SignatureLabels.ToArray();
        SignatureId = EventScriptMessageSignature.CreateSignatureId(Message, SignatureLabels);
        DiagnosticsEnabled = diagnosticsEnabled;
        Definition = new EventScriptMessageSignature(Message, SignatureLabels);
    }

    internal IReadOnlyList<StatementNode> Statements { get; }

    internal bool DiagnosticsEnabled { get; }

    public string Message { get; }

    public int DeclarationOrder { get; }

    public IReadOnlyList<string> Parameters { get; }

    public IReadOnlyList<string> SignatureLabels { get; }

    public string SignatureId { get; }

    public EventScriptMessageSignature Definition { get; }
}
