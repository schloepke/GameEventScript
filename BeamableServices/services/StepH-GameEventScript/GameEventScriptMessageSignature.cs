#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using StepH.GameEventScript.Types;

namespace StepH.GameEventScript;

public sealed class GameEventScriptMessageSignature
{
    public const string UnlabeledParameterName = "_";

    public static readonly GameEventScriptMessageSignature Empty = new(string.Empty, []);

    public static GameEventScriptMessageSignature MessageSignature(string name, IEnumerable<string>? parameters) => new(name, parameters);

    public GameEventScriptMessage CreateMessage(params (string name, GameEventScriptValue value)[] arguments) => GameEventScriptMessage.Message(Name, arguments);

    public static string NormalizeMessageName(string? name) => string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim();

    public static string NormalizeParameterName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return UnlabeledParameterName;
        }

        var trimmed = name.Trim();
        return trimmed.Length == 0 ? UnlabeledParameterName : trimmed;
    }

    public static IReadOnlyList<string> NormalizeParameterNames(IEnumerable<string>? names)
        => names is null
            ? []
            : names
                .Select(NormalizeParameterName)
                .ToArray();

    public static string CreateSignatureId(string name, IEnumerable<string>? parameterNames)
    {
        var normalizedName = NormalizeMessageName(name);
        var normalizedParameters = NormalizeParameterNames(parameterNames);
        return $"{normalizedName}({string.Join(",", normalizedParameters)})";
    }

    public GameEventScriptMessageSignature(string name, IEnumerable<string>? parameters)
    {
        Name = NormalizeMessageName(name);
        Parameters = NormalizeParameterNames(parameters);
        SignatureId = CreateSignatureId(Name, Parameters);
    }

    public string Name { get; }

    public IReadOnlyList<string> Parameters { get; }

    public string SignatureId { get; }

    public bool Matches(GameEventScriptMessage message) => string.Equals(Name, message.Name, StringComparison.Ordinal) && string.Equals(SignatureId, message.SignatureId, StringComparison.Ordinal);

}
