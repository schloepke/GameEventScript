#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Text;

namespace StepH.GameEventScript.Api;

public interface IGameEventScriptExtensionRegistry
{
    IGameEventScriptExtensionFunction? Resolve(GameEventScriptExtensionReference reference);
}

public interface IGameEventScriptExtensionFunction
{
    GameEventScriptValue Invoke(GameEventScriptExtensionContext context, GameEventScriptValueSlice arguments);
}

internal sealed class GameEventScriptEmptyExtensionRegistry : IGameEventScriptExtensionRegistry
{
    public static readonly GameEventScriptEmptyExtensionRegistry Instance = new();

    private GameEventScriptEmptyExtensionRegistry()
    {
    }

    public IGameEventScriptExtensionFunction? Resolve(GameEventScriptExtensionReference reference) => null;
}

public sealed class GameEventScriptExtensionReference
{
    public GameEventScriptExtensionReference(string? extensionName, string? functionName, IEnumerable<string?>? argumentLabels)
    {
        ExtensionName = NormalizeName(extensionName);
        FunctionName = NormalizeName(functionName);
        ArgumentLabels = NormalizeArgumentLabels(argumentLabels);
        SignatureId = CreateSignatureId(ExtensionName, FunctionName, ArgumentLabels);
    }

    public string ExtensionName { get; }

    public string FunctionName { get; }

    public IReadOnlyList<string> ArgumentLabels { get; }

    public string SignatureId { get; }

    private static string NormalizeName(string? name) => string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim();

    private static string[] NormalizeArgumentLabels(IEnumerable<string?>? argumentLabels)
    {
        if (argumentLabels is null)
        {
            return [];
        }

        if (argumentLabels is IReadOnlyCollection<string?> collection)
        {
            if (collection.Count == 0)
            {
                return [];
            }

            var values = new string[collection.Count];
            var index = 0;
            foreach (var label in collection)
            {
                values[index++] = GameEventScriptMessageSignature.NormalizeParameterName(label);
            }

            return values;
        }

        var list = new List<string>();
        foreach (var label in argumentLabels)
        {
            list.Add(GameEventScriptMessageSignature.NormalizeParameterName(label));
        }

        if (list.Count == 0)
        {
            return [];
        }

        var result = new string[list.Count];
        for (var index = 0; index < list.Count; index++)
        {
            result[index] = list[index];
        }

        return result;
    }

    private static string CreateSignatureId(string extensionName, string functionName, IReadOnlyList<string> argumentLabels)
    {
        var builder = new StringBuilder();
        builder.Append(extensionName).Append('.').Append(functionName).Append('(');
        for (var index = 0; index < argumentLabels.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append(argumentLabels[index]);
        }

        return builder.Append(')').ToString();
    }
}

public sealed class GameEventScriptExtensionContext(GameEventScriptSession runtimeSession)
{
    public GameEventScriptSession RuntimeSession { get; } = runtimeSession ?? throw new ArgumentNullException(nameof(runtimeSession));

    public GameEventScriptRandomGenerator Random => RuntimeSession.Random;

    public GameEventScriptRuntimeLimits RuntimeLimits => RuntimeSession.RuntimeLimits;
}
