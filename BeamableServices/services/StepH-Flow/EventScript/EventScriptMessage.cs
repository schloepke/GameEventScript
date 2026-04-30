#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System.Collections.Generic;
using System.Linq;
using StepH.Flow.EventScript.Types;

namespace StepH.Flow.EventScript;

public sealed class EventScriptMessage
{
    public static EventScriptMessage EmptyMessage = new(string.Empty);

    public EventScriptMessage(string name, IReadOnlyDictionary<string, EventScriptValue>? arguments) : this(name, EventScriptNamedArguments.Create(arguments))
    {
    }

    public EventScriptMessage(string name, EventScriptNamedArguments? arguments = null)
    {
        Name = EventScriptMessageSignature.NormalizeMessageName(name);
        Arguments = arguments ?? EventScriptNamedArguments.Empty;
        SignatureId = EventScriptMessageSignature.CreateSignatureId(Name, Arguments.SignatureLabels);
    }

    private EventScriptMessage(string normalizedName, EventScriptNamedArguments arguments, string signatureId)
    {
        Name = normalizedName;
        Arguments = arguments;
        SignatureId = signatureId;
    }

    public string Name { get; }

    public EventScriptNamedArguments Arguments { get; }

    public string SignatureId { get; }

    public override string ToString() => Arguments.Count == 0 ? Name : $"{Name}({Arguments})";

    public static EventScriptMessage Message(string name) => new(name);

    public static EventScriptMessage Message(string name, IReadOnlyDictionary<string, EventScriptValue>? arguments) => new(name, EventScriptNamedArguments.Create(arguments));

    public static EventScriptMessage Message(string name, params (string name, EventScriptValue value)[] arguments)
        => new(name, EventScriptNamedArguments.CreateOrdered(arguments
            .Select(pair => new KeyValuePair<string, EventScriptValue>(pair.name, pair.value))
            .ToArray()));

    public static EventScriptMessage Message(string name, params (string name, object? value)[] arguments)
        => new(name, EventScriptNamedArguments.CreateOrdered(arguments
            .Select(pair => new KeyValuePair<string, EventScriptValue>(pair.name, EventScriptValueFactory.FromClr(pair.value)))
            .ToArray()));

    internal static EventScriptMessage CreatePrecomputed(string normalizedName, EventScriptNamedArguments arguments, string signatureId)
        => new(normalizedName, arguments, signatureId);
}
