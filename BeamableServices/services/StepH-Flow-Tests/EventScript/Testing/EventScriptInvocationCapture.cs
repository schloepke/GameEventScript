using System;
using System.Collections.Generic;
using System.Linq;
using StepH.Flow.EventScript;
using StepH.Flow.EventScript.Experimental;
using StepH.Flow.EventScript.Interpreter;
using StepH.Flow.EventScript.Runtime;
using StepH.Flow.EventScript.Types;

namespace StepH_Flow_Tests.EventScript.Testing;

public sealed class EventScriptCapturedEvent
{
    public EventScriptCapturedEvent(EventScriptMessage @event)
    {
        Event = @event;
    }

    public EventScriptMessage Event { get; }

    public string Message => Event.Name;

    public EventScriptNamedArguments Arguments => Event.Arguments;
}

public sealed class EventScriptCapturedRun
{
    public EventScriptCapturedRun(EventScriptMessage invocationMessage, IReadOnlyList<EventScriptCapturedEvent> emittedEvents)
    {
        InvocationMessage = invocationMessage;
        EmittedEvents = emittedEvents;
    }

    public EventScriptMessage InvocationMessage { get; }

    public string Message => InvocationMessage.Name;

    public IReadOnlyList<EventScriptCapturedEvent> EmittedEvents { get; }

    public IReadOnlyDictionary<string, EventScriptValue> Variables { get; } = new Dictionary<string, EventScriptValue>(StringComparer.Ordinal);
}

internal static class EventScriptNamedDispatchTestExtensions
{
    extension(CompiledEventScript compiledScript)
    {
        public EventScriptCapturedRun InvokePositional(EventScriptRandomGenerator randomGenerator, string message, params EventScriptValue[] args)
            => compiledScript.InvokePositional(message, randomGenerator, args);

        public EventScriptCapturedRun InvokePositional(EventScriptInvocationContext invocationContext, string message, params EventScriptValue[] args)
            => compiledScript.InvokePositional(message, invocationContext?.Random ?? EventScriptRandomGenerator.Create(), args);

        public EventScriptCapturedRun InvokePositional(string message, params EventScriptValue[] args)
            => compiledScript.InvokePositional(message, EventScriptRandomGenerator.Create(), args);

        public EventScriptCapturedRun Invoke(string message)
            => compiledScript.Invoke(EventScriptMessage.Message(message));

        public EventScriptCapturedRun Invoke(string message, IReadOnlyDictionary<string, EventScriptValue> args, EventScriptInvocationContext? invocationContext = null,
            IEventScriptDiagnosticCollector? diagnosticCollector = null)
            => compiledScript.Invoke(EventScriptMessage.Message(message, args), invocationContext, diagnosticCollector);

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

        public EventScriptCapturedRun InvokeClr(string message, params (string name, object? value)[] arguments)
            => compiledScript.Invoke(EventScriptMessage.Message(message, arguments));
    }

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

    private static EventScriptCapturedRun InvokePositional(this CompiledEventScript compiledScript, string message, EventScriptRandomGenerator randomGenerator, IReadOnlyList<EventScriptValue> args)
        => compiledScript.Invoke(message, BuildNamedArguments(GetCompiledParameterNames(compiledScript, message, args.Count), args),
            new EventScriptInvocationContext { Random = randomGenerator });

    private static IReadOnlyDictionary<string, EventScriptValue> BuildNamedArguments(IReadOnlyList<string> parameterNames, IReadOnlyList<EventScriptValue> args)
        => parameterNames.Select((name, index) => new KeyValuePair<string, EventScriptValue>(name, args[index] ?? EventScriptValue.Nothing))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

    private static IReadOnlyList<string> GetCompiledParameterNames(CompiledEventScript compiledScript, string message, int argumentCount)
    {
        if (argumentCount == 0) return [];
        if (!compiledScript.Handlers.TryGetValue(message, out var matchingHandlers)) throw new InvalidOperationException($"No handler for message '{message}' was found.");
        var matches = (from handler in matchingHandlers where handler.Parameters.Count == argumentCount select handler.Parameters).ToList();
        var distinctMatches = matches.GroupBy(parameters => string.Join("|", parameters), StringComparer.Ordinal).Select(group => group.First()).ToArray();
        return distinctMatches.Length == 1
            ? distinctMatches[0]
            : throw new InvalidOperationException($"Unable to resolve a unique named signature for message '{message}' with {argumentCount} argument(s).");
    }
}
