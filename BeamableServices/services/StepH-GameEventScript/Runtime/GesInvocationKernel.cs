using System;
using System.Collections.Generic;
using System.Linq;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Types;

namespace StepH.GameEventScript.Runtime;

internal static class GesInvocationKernel
{
    public static IReadOnlyDictionary<string, IReadOnlyList<THandler>> BuildDispatchIndex<THandler>(IEnumerable<THandler> handlers, Func<THandler, string> signatureIdSelector,
        Func<THandler, int> declarationOrderSelector)
        => handlers.GroupBy(signatureIdSelector, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<THandler>)group.OrderBy(declarationOrderSelector).ToArray(), StringComparer.Ordinal);

    public static IReadOnlyList<THandler> GetMatchingHandlers<THandler>(IReadOnlyDictionary<string, IReadOnlyList<THandler>> dispatchIndex, GameEventScriptMessage message)
        => dispatchIndex.TryGetValue(message.SignatureId, out var matchingHandlers) ? matchingHandlers : [];

    public static void RecordDiagnostic(GameEventScriptContext context, bool diagnosticsEnabled, GameEventScriptDiagnosticEventKind kind, string name,
        IReadOnlyDictionary<string, GameEventScriptValue> arguments, string? detail = null)
    {
        if (!diagnosticsEnabled) return;
        context.RecordDiagnostic(kind, name, arguments, detail);
    }
}