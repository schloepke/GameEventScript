#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using StepH.Flow.EventScript.Interpreter;
using StepH.Flow.EventScript.Runtime;
using StepH.Flow.EventScript.Types;

namespace StepH.Flow.EventScript.Experimental;

public sealed class ExperimentalCompiledEventScript : IEventScriptInvokableScript
{
    internal ExperimentalCompiledEventScript(
        ExperimentalEventScriptCompilationOptions options,
        IReadOnlyList<string> stringPool,
        IReadOnlyList<EventScriptValue> constantPool,
        IReadOnlyList<ExperimentalCompiledExpression> expressionPool,
        IReadOnlyList<ExperimentalCompiledNamedArgumentList> namedArgumentLists,
        IReadOnlyList<ExperimentalCompiledIterationSource> iterationSources,
        IReadOnlyList<ExperimentalEventScript> programs,
        IReadOnlyDictionary<string, IReadOnlyList<ExperimentalCompiledEventScriptHandler>> handlers,
        IReadOnlyDictionary<string, ExperimentalCompiledCallableDefinition> callables,
        IReadOnlyDictionary<string, ExperimentalCompiledTypeDefinition> typeDefinitions)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        StringPool = stringPool ?? throw new ArgumentNullException(nameof(stringPool));
        ConstantPool = constantPool ?? throw new ArgumentNullException(nameof(constantPool));
        ExpressionPool = expressionPool ?? throw new ArgumentNullException(nameof(expressionPool));
        NamedArgumentLists = namedArgumentLists ?? throw new ArgumentNullException(nameof(namedArgumentLists));
        IterationSources = iterationSources ?? throw new ArgumentNullException(nameof(iterationSources));
        Programs = programs ?? throw new ArgumentNullException(nameof(programs));
        Handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
        Callables = callables ?? throw new ArgumentNullException(nameof(callables));
        TypeDefinitions = typeDefinitions ?? throw new ArgumentNullException(nameof(typeDefinitions));
    }

    public ExperimentalEventScriptCompilationOptions Options { get; }

    public bool DiagnosticsEnabled => Options.EnableDiagnostics;

    public IReadOnlyDictionary<string, IReadOnlyList<ExperimentalCompiledEventScriptHandler>> Handlers { get; }

    internal IReadOnlyList<string> StringPool { get; }

    internal IReadOnlyList<EventScriptValue> ConstantPool { get; }

    internal IReadOnlyList<ExperimentalCompiledExpression> ExpressionPool { get; }

    internal IReadOnlyList<ExperimentalCompiledNamedArgumentList> NamedArgumentLists { get; }

    internal IReadOnlyList<ExperimentalCompiledIterationSource> IterationSources { get; }

    internal IReadOnlyList<ExperimentalEventScript> Programs { get; }

    internal IReadOnlyDictionary<string, ExperimentalCompiledCallableDefinition> Callables { get; }

    internal IReadOnlyDictionary<string, ExperimentalCompiledTypeDefinition> TypeDefinitions { get; }

    public EventScriptExecutionResult Invoke(EventScriptMessage message, EventScriptInvocationContext? invocationContext = null, IEventScriptDiagnosticCollector? diagnosticCollector = null)
        => new ExperimentalOpcodeInvocationEngine(this, invocationContext, diagnosticCollector).InvokeMessage(message);
}
