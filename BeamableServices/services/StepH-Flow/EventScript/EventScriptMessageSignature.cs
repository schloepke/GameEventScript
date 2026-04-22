#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using StepH.Flow.EventScript.Types;

namespace StepH.Flow.EventScript;

public sealed class EventScriptMessageSignature
{
    public static EventScriptMessageSignature MessageSignature(string name, IEnumerable<string>? parameters)
        => new(name, parameters);

    public static string CreateSignatureId(IEnumerable<string> names)
        => string.Join("|", names.OrderBy(name => name, StringComparer.Ordinal));

    public EventScriptMessageSignature(string name, IEnumerable<string>? parameters)
    {
        Name = string.IsNullOrWhiteSpace(name) ? string.Empty : name;
        Parameters = parameters is null ? [] : parameters.Where(parameter => !string.IsNullOrWhiteSpace(parameter)).ToArray();
        SignatureId = CreateSignatureId(Parameters);
    }

    public string Name { get; }

    public IReadOnlyList<string> Parameters { get; }

    public string SignatureId { get; }

    public bool Matches(EventScriptMessage message)
        => string.Equals(Name, message.Name, StringComparison.Ordinal) && string.Equals(SignatureId, message.SignatureId, StringComparison.Ordinal);

    public EventScriptMessage CreateMessage(params (string name, EventScriptValue value)[] arguments)
        => EventScriptMessage.Message(Name, arguments);
}