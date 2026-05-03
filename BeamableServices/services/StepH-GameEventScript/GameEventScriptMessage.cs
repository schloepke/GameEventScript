#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System.Collections.Generic;
using System.Linq;
using StepH.GameEventScript.Types;

namespace StepH.GameEventScript;

public sealed class GameEventScriptMessage
{
    public static GameEventScriptMessage EmptyMessage = new(string.Empty);

    public GameEventScriptMessage(string name, IReadOnlyDictionary<string, GameEventScriptValue>? arguments) : this(name, GameEventScriptNamedArguments.Create(arguments))
    {
    }

    public GameEventScriptMessage(string name, GameEventScriptNamedArguments? arguments = null)
    {
        Name = GameEventScriptMessageSignature.NormalizeMessageName(name);
        Arguments = arguments ?? GameEventScriptNamedArguments.Empty;
        SignatureId = GameEventScriptMessageSignature.CreateSignatureId(Name, Arguments.SignatureLabels);
    }

    private GameEventScriptMessage(string normalizedName, GameEventScriptNamedArguments arguments, string signatureId)
    {
        Name = normalizedName;
        Arguments = arguments;
        SignatureId = signatureId;
    }

    public string Name { get; }

    public GameEventScriptNamedArguments Arguments { get; }

    public string SignatureId { get; }

    public override string ToString() => Arguments.Count == 0 ? Name : $"{Name}({Arguments})";

    public static GameEventScriptMessage Message(string name) => new(name);

    public static GameEventScriptMessage Message(string name, IReadOnlyDictionary<string, GameEventScriptValue>? arguments) => new(name, GameEventScriptNamedArguments.Create(arguments));

    public static GameEventScriptMessage Message(string name, params (string name, GameEventScriptValue value)[] arguments)
        => new(name, GameEventScriptNamedArguments.CreateOrdered(arguments
            .Select(pair => new KeyValuePair<string, GameEventScriptValue>(pair.name, pair.value))
            .ToArray()));

    public static GameEventScriptMessage Message(string name, params (string name, object? value)[] arguments)
        => new(name, GameEventScriptNamedArguments.CreateOrdered(arguments
            .Select(pair => new KeyValuePair<string, GameEventScriptValue>(pair.name, GameEventScriptValueFactory.FromClr(pair.value)))
            .ToArray()));

    internal static GameEventScriptMessage CreatePrecomputed(string normalizedName, GameEventScriptNamedArguments arguments, string signatureId)
        => new(normalizedName, arguments, signatureId);
}
