#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System.Collections.Generic;
using System.Linq;
using StepH.GameEventScript.Types;

namespace StepH.GameEventScript;

public sealed class GseMessage
{
    public static GseMessage EmptyMessage = new(string.Empty);

    public GseMessage(string name, IReadOnlyDictionary<string, GseValue>? arguments) : this(name, GseNamedArguments.Create(arguments))
    {
    }

    public GseMessage(string name, GseNamedArguments? arguments = null)
    {
        Name = GseMessageSignature.NormalizeMessageName(name);
        Arguments = arguments ?? GseNamedArguments.Empty;
        SignatureId = GseMessageSignature.CreateSignatureId(Name, Arguments.SignatureLabels);
    }

    private GseMessage(string normalizedName, GseNamedArguments arguments, string signatureId)
    {
        Name = normalizedName;
        Arguments = arguments;
        SignatureId = signatureId;
    }

    public string Name { get; }

    public GseNamedArguments Arguments { get; }

    public string SignatureId { get; }

    public override string ToString() => Arguments.Count == 0 ? Name : $"{Name}({Arguments})";

    public static GseMessage Message(string name) => new(name);

    public static GseMessage Message(string name, IReadOnlyDictionary<string, GseValue>? arguments) => new(name, GseNamedArguments.Create(arguments));

    public static GseMessage Message(string name, params (string name, GseValue value)[] arguments)
        => new(name, GseNamedArguments.CreateOrdered(arguments
            .Select(pair => new KeyValuePair<string, GseValue>(pair.name, pair.value))
            .ToArray()));

    public static GseMessage Message(string name, params (string name, object? value)[] arguments)
        => new(name, GseNamedArguments.CreateOrdered(arguments
            .Select(pair => new KeyValuePair<string, GseValue>(pair.name, GseValueFactory.FromClr(pair.value)))
            .ToArray()));

    internal static GseMessage CreatePrecomputed(string normalizedName, GseNamedArguments arguments, string signatureId)
        => new(normalizedName, arguments, signatureId);
}
