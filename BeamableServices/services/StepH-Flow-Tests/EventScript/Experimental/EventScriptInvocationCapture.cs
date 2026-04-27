using StepH.Flow.EventScript;
using StepH.Flow.EventScript.Experimental;
using StepH.Flow.EventScript.Interpreter;
using StepH.Flow.EventScript.Runtime;
using StepH.Flow.EventScript.Types;

namespace StepH_Flow_Tests.EventScript.Experimental;

public sealed class EventScriptCapturedEvent(EventScriptMessage @event)
{
    public EventScriptMessage Event { get; } = @event;

    public string Message => Event.Name;

    public EventScriptNamedArguments Arguments => Event.Arguments;
}

public sealed class EventScriptCapturedRun(EventScriptMessage invocationMessage, IReadOnlyList<EventScriptCapturedEvent> emittedEvents)
{
    public EventScriptMessage InvocationMessage { get; } = invocationMessage;

    public string Message => InvocationMessage.Name;

    public IReadOnlyList<EventScriptCapturedEvent> EmittedEvents { get; } = emittedEvents;

    public IReadOnlyDictionary<string, EventScriptValue> Variables { get; } = new Dictionary<string, EventScriptValue>(StringComparer.Ordinal);
}

internal static class EventScriptNamedDispatchTestExtensions
{
    extension(ExperimentalCompiledEventScript compiledScript)
    {
        public EventScriptCapturedRun Invoke(EventScriptMessage message, EventScriptInvocationContext? invocationContext = null, IEventScriptDiagnosticCollector? diagnosticCollector = null)
        {
            var emittedEvents = new List<EventScriptCapturedEvent>();
            var context = new EventScriptContext(
                invocationContext?.Random ?? EventScriptRandomGenerator.Create(),
                published => emittedEvents.Add(new EventScriptCapturedEvent(published)),
                diagnosticCollector);
            compiledScript.Invoke(message, context);
            return new EventScriptCapturedRun(message, emittedEvents);
        }
    }
}
