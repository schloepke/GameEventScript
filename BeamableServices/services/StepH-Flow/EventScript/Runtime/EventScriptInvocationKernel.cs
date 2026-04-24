using System;
using System.Collections.Generic;
using System.Linq;
using StepH.Flow.EventScript.Interpreter;

namespace StepH.Flow.EventScript.Runtime;

internal static class EventScriptInvocationKernel
{
    public static IReadOnlyList<THandler> GetMatchingHandlers<THandler>(
        IReadOnlyDictionary<string, IReadOnlyList<THandler>> handlers,
        EventScriptMessage message,
        Func<THandler, string> signatureIdSelector,
        Func<THandler, int> declarationOrderSelector)
    {
        if (!handlers.TryGetValue(message.Name, out var matchingByName))
        {
            return [];
        }

        return matchingByName
            .Where(handler => string.Equals(signatureIdSelector(handler), message.SignatureId, StringComparison.Ordinal))
            .OrderBy(declarationOrderSelector)
            .ToArray();
    }

    public static void RecordDiagnostic(
        EventScriptContext context,
        bool diagnosticsEnabled,
        EventScriptDiagnosticEventKind kind,
        string name,
        IReadOnlyDictionary<string, Types.EventScriptValue> arguments,
        string? detail = null)
    {
        if (!diagnosticsEnabled)
        {
            return;
        }

        context.RecordDiagnostic(kind, name, arguments, detail);
    }
}
