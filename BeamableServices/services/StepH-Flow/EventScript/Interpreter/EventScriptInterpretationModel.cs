#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using StepH.Flow.EventScript.Linker;
using StepH.Flow.EventScript.Parser;

namespace StepH.Flow.EventScript.Interpreter;

public sealed class CompiledEventScript
{
    public CompiledEventScript(LinkedEventScriptModule linkedModule, EventScriptInterpreterCompilationOptions? options = null)
    {
        LinkedModule = linkedModule ?? throw new ArgumentNullException(nameof(linkedModule));
        Options = options ?? new EventScriptInterpreterCompilationOptions();
        Handlers = linkedModule.Handlers.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<CompiledEventScriptHandler>)pair.Value
                .Select((handler, index) => new CompiledEventScriptHandler(pair.Key, handler, index))
                .ToArray(),
            StringComparer.Ordinal);
    }

    public LinkedEventScriptModule LinkedModule { get; }

    public EventScriptInterpreterCompilationOptions Options { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<CompiledEventScriptHandler>> Handlers { get; }

    public bool DiagnosticsEnabled => Options.EnableDiagnostics;
}

public sealed class CompiledEventScriptHandler
{
    internal CompiledEventScriptHandler(string message, EventHandlerNode syntax, int declarationOrder)
    {
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Syntax = syntax ?? throw new ArgumentNullException(nameof(syntax));
        DeclarationOrder = declarationOrder;
    }

    internal EventHandlerNode Syntax { get; }

    public string Message { get; }

    public int DeclarationOrder { get; }

    public IReadOnlyList<string> Parameters => Syntax.Parameters;
}
