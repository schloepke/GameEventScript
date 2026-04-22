using StepH.Flow.EventScript;
using StepH.Flow.EventScript.Interpreter;
using StepH.Flow.EventScript.Types;

namespace StepH_Flow_Tests.EventScript.Testing;

internal static class EventScriptNamedDispatchTestExtensions
{
    extension(CompiledEventScript compiledScript)
    {
        public EventScriptExecutionResult InvokePositional(EventScriptRandomGenerator randomGenerator, string message, params EventScriptValue[] args)
            => compiledScript.InvokePositional(new EventScriptInvocationContext { Random = randomGenerator }, message, args);

        public EventScriptExecutionResult InvokePositional(EventScriptInvocationContext invocationContext, string message, params EventScriptValue[] args)
            => compiledScript.Invoke(message, BuildNamedArguments(GetCompiledParameterNames(compiledScript, message, args.Length), args), invocationContext, diagnosticCollector: null);

        public EventScriptExecutionResult InvokePositional(string message, params EventScriptValue[] args)
            => compiledScript.Invoke(message, BuildNamedArguments(GetCompiledParameterNames(compiledScript, message, args.Length), args));

        public EventScriptExecutionResult Invoke(string message)
            => compiledScript.Invoke(new EventScriptMessage(message));

        public EventScriptExecutionResult Invoke(string message, IReadOnlyDictionary<string, EventScriptValue> args, EventScriptInvocationContext? invocationContext = null,
            IEventScriptDiagnosticCollector? diagnosticCollector = null)
            => compiledScript.Invoke(new EventScriptMessage(message, args), invocationContext, diagnosticCollector);

        public EventScriptExecutionResult InvokeClr(string message, params (string name, object? value)[] arguments)
            => compiledScript.Invoke(EventScriptMessage.Message(message, arguments));
    }

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