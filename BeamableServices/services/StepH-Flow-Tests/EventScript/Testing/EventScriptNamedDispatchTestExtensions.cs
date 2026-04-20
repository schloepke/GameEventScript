using System.Reflection;
using StepH.Flow.EventScript.Interpreter;
using StepH.Flow.EventScript.Runtime;
using StepH.Flow.EventScript.Types;

namespace StepH_Flow_Tests.EventScript.Testing;

internal static class EventScriptNamedDispatchTestExtensions
{
    public static EventScriptExecutionResult Emit(this EventScriptInterpreter interpreter, string message, params EventScriptValue[] args)
        => interpreter.Emit(message, BuildNamedArguments(GetInterpreterParameterNames(interpreter, message, args.Length), args));

    public static EventScriptExecutionResult Emit(this EventScriptInterpreter interpreter, string message, params object?[] args)
        => interpreter.Emit(message, BuildNamedArguments(GetInterpreterParameterNames(interpreter, message, args.Length), EventScriptValue.FromClrList(args).ToArray()));

    public static EventScriptExecutionResult EmitClr(this EventScriptInterpreter interpreter, string message, params object?[] args)
        => interpreter.EmitClr(message, BuildClrNamedArguments(GetInterpreterParameterNames(interpreter, message, args.Length), args));

    public static EventScriptDiagnosticInvocationResult Invoke(this EventScriptDiagnosticInterpreter interpreter, string message, params EventScriptValue[] args)
        => interpreter.Invoke(message, BuildNamedArguments(GetDiagnosticParameterNames(interpreter, message, args.Length), args));

    public static EventScriptExecutionResult Emit(this EventScriptHost host, string message, params EventScriptValue[] args)
        => host.Emit(message, BuildNamedArguments(GetHostParameterNames(host, message, args.Length), args));

    public static EventScriptHost.EventScriptRun Enqueue(this EventScriptHost host, string message, params EventScriptValue[] args)
        => host.Enqueue(message, BuildNamedArguments(GetHostParameterNames(host, message, args.Length), args));

    public static EventScriptHost BindExternal(this EventScriptHost host, string message, Action<IReadOnlyList<EventScriptValue>> handler, int parameterCount, int? priority = null)
        => host.BindExternal(
            message,
            Enumerable.Range(1, parameterCount).Select(index => $"arg{index}").ToArray(),
            arguments => handler(Enumerable.Range(1, parameterCount).Select(index => arguments[$"arg{index}"]).ToArray()),
            priority);

    private static IReadOnlyDictionary<string, EventScriptValue> BuildNamedArguments(IReadOnlyList<string> parameterNames, IReadOnlyList<EventScriptValue> args)
        => parameterNames.Select((name, index) => new KeyValuePair<string, EventScriptValue>(name, args[index] ?? EventScriptValue.Nothing))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, object?> BuildClrNamedArguments(IReadOnlyList<string> parameterNames, IReadOnlyList<object?> args)
        => parameterNames.Select((name, index) => new KeyValuePair<string, object?>(name, args[index]))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

    private static IReadOnlyList<string> GetInterpreterParameterNames(EventScriptInterpreter interpreter, string message, int argumentCount)
    {
        var engine = GetField(interpreter, "_engine");
        var handlers = GetField(engine, "_handlers") as System.Collections.IDictionary
            ?? throw new InvalidOperationException("Interpreter handler table not found.");
        return ResolveParameterNames(handlers, message, argumentCount);
    }

    private static IReadOnlyList<string> GetDiagnosticParameterNames(EventScriptDiagnosticInterpreter interpreter, string message, int argumentCount)
    {
        var compiledScript = GetField(interpreter, "_compiledScript");
        var handlers = compiledScript.GetType().GetProperty("Handlers", BindingFlags.Instance | BindingFlags.Public)?.GetValue(compiledScript) as System.Collections.IDictionary
            ?? throw new InvalidOperationException("Diagnostic interpreter handler table not found.");
        return ResolveParameterNames(handlers, message, argumentCount);
    }

    private static IReadOnlyList<string> GetHostParameterNames(EventScriptHost host, string message, int argumentCount)
    {
        var subscriptions = GetField(host, "_subscriptions") as System.Collections.IDictionary
            ?? throw new InvalidOperationException("Host subscription table not found.");
        if (subscriptions[message] is not System.Collections.IEnumerable matchingSubscriptions)
        {
            if (argumentCount == 0)
            {
                return [];
            }

            throw new InvalidOperationException($"No host subscription for message '{message}' was found.");
        }

        var matches = new List<IReadOnlyList<string>>();
        foreach (var subscription in matchingSubscriptions)
        {
            var handler = subscription.GetType().GetProperty("Handler", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(subscription);
            var parameters = handler?.GetType().GetProperty("Parameters", BindingFlags.Instance | BindingFlags.Public)?.GetValue(handler) as IReadOnlyList<string>;
            if (parameters is not null && parameters.Count == argumentCount)
            {
                matches.Add(parameters);
            }
        }

        var distinctMatches = matches
            .GroupBy(parameters => string.Join("|", parameters), StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();

        if (distinctMatches.Length == 1)
        {
            return distinctMatches[0];
        }

        if (argumentCount == 0)
        {
            return [];
        }

        throw new InvalidOperationException($"Unable to resolve a unique host signature for message '{message}' with {argumentCount} argument(s).");
    }

    private static IReadOnlyList<string> ResolveParameterNames(System.Collections.IDictionary handlers, string message, int argumentCount)
    {
        if (handlers[message] is not System.Collections.IEnumerable matchingHandlers)
        {
            if (argumentCount == 0)
            {
                return [];
            }

            throw new InvalidOperationException($"No handler for message '{message}' was found.");
        }

        var matches = new List<IReadOnlyList<string>>();
        foreach (var handler in matchingHandlers)
        {
            var parameters = handler.GetType().GetProperty("Parameters", BindingFlags.Instance | BindingFlags.Public)?.GetValue(handler) as IReadOnlyList<string>;
            if (parameters is not null && parameters.Count == argumentCount)
            {
                matches.Add(parameters);
            }
        }

        var distinctMatches = matches
            .GroupBy(parameters => string.Join("|", parameters), StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();

        if (distinctMatches.Length == 1)
        {
            return distinctMatches[0];
        }

        if (argumentCount == 0)
        {
            return [];
        }

        throw new InvalidOperationException($"Unable to resolve a unique named signature for message '{message}' with {argumentCount} argument(s).");
    }

    private static object GetField(object target, string fieldName)
        => target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(target)
           ?? throw new InvalidOperationException($"Field '{fieldName}' was not found on '{target.GetType().FullName}'.");
}
