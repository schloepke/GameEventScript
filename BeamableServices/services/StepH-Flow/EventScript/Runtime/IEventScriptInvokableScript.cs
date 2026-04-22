#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System.Collections.Generic;
using StepH.Flow.EventScript.Interpreter;

namespace StepH.Flow.EventScript.Runtime;

public interface IEventScriptInvokableScript
{
    EventScriptExecutionResult Invoke(EventScriptMessage message, EventScriptInvocationContext? invocationContext = null, IEventScriptDiagnosticCollector? diagnosticCollector = null);

}

public interface IEventScriptHandlerCollection
{
    IReadOnlyDictionary<string, IReadOnlyList<CompiledEventScriptHandler>> Handlers { get; }
}
