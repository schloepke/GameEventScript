using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using StepH.GameEventScript.Types;

namespace StepH.GameEventScript.Api;

/// <summary>
/// Represents a message signature used in GameEventScript to uniquely define the structure
/// of a message, including its name and ordered parameter list. This class provides utilities
/// to normalize, validate, and match message signatures accurately.
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[SuppressMessage("ReSharper", "UnusedMember.Global")]
public sealed class GameEventScriptMessageSignature : IEquatable<GameEventScriptMessageSignature>
{
    /// <summary>
    /// Represents the default name assigned to a parameter when no explicit label or name
    /// is provided. This constant is primarily used to normalize and handle unlabeled parameters
    /// in message signatures within the GameEventScript framework.
    /// </summary>
    public const string UnlabeledParameterName = "_";

    /// <summary>
    /// Represents an empty and default instance of the <see cref="GameEventScriptMessageSignature"/> class.
    /// This instance serves as a placeholder or null-equivalent for signatures where no specific definition is required.
    /// </summary>
    public static readonly GameEventScriptMessageSignature Empty = new(string.Empty, []);

    /// <summary>
    /// Creates a new instance of <see cref="GameEventScriptMessageSignature"/> representing a message signature
    /// with the specified name and parameters.
    /// </summary>
    /// <param name="name">The name of the message. This must be a non-null and non-empty string.</param>
    /// <param name="parameters">An optional collection of parameter names associated with the message. Can be null if the message does not require parameters.</param>
    /// <returns>A new <see cref="GameEventScriptMessageSignature"/> instance representing the provided message signature.</returns>
    public static GameEventScriptMessageSignature Create(string name, IEnumerable<string>? parameters) => new(name, parameters);

    /// <summary>
    /// Normalizes the given message name by trimming whitespace and ensuring it is not null or empty.
    /// </summary>
    /// <param name="name">The message name to normalize. Can be null or empty, in which case an empty string is returned.</param>
    /// <returns>A normalized string representation of the message name, or an empty string if the input was null or only contained whitespace.</returns>
    public static string NormalizeMessageName(string? name) => string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim();

    /// <summary>
    /// Normalizes a parameter name by trimming whitespace and replacing null or empty values with the default unlabeled parameter name.
    /// </summary>
    /// <param name="name">The parameter name to normalize. This can be null, empty, or whitespace.</param>
    /// <returns>
    /// A normalized parameter name. If the input is null, empty, or whitespace, the unlabeled parameter name is returned.
    /// Otherwise, the trimmed version of the input is returned.
    /// </returns>
    public static string NormalizeParameterName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return UnlabeledParameterName;
        var trimmed = name.Trim();
        return trimmed.Length == 0 ? UnlabeledParameterName : trimmed;
    }

    /// <summary>
    /// Generates a unique signature identifier for a message based on its normalized name
    /// and associated parameter names.
    /// </summary>
    /// <param name="name">The name of the message. It cannot be null or empty and will be normalized
    /// by trimming whitespace.</param>
    /// <param name="parameterNames">An optional collection of parameter names associated with the message.
    /// If null, an empty list of parameters is considered, and it will also normalize each parameter name.</param>
    /// <returns>A string representing the signature ID, which combines the normalized message name and
    /// a comma-separated list of its parameter names enclosed in parentheses.</returns>
    public static string CreateSignatureId(string name, IEnumerable<string>? parameterNames)
    {
        var normalizedName = NormalizeMessageName(name);
        IReadOnlyList<string> normalizedParameters = parameterNames is null ? [] : parameterNames.Select(NormalizeParameterName).ToArray();
        return $"{normalizedName}({string.Join(",", normalizedParameters)})";
    }

    /// <summary>
    /// Gets the normalized name of the message signature.
    /// The name uniquely identifies the signature within a GameEventScript context.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the collection of normalized parameter names that define the structure of the message.
    /// Each parameter name in this collection is standardized for use in the message signature
    /// and helps uniquely identify the signature when paired with the message name.
    /// </summary>
    public IReadOnlyList<string> Parameters { get; }

    /// <summary>
    /// Represents the unique identifier of the message signature within the <see cref="GameEventScriptMessageSignature"/> class.
    /// This identifier is generated based on the name and the parameters of the signature, ensuring distinctness for
    /// different combinations of message definitions.
    /// </summary>
    public string SignatureId { get; }

    /// <summary>
    /// Determines whether the specified <see cref="GameEventScriptMessage"/> matches the current
    /// <see cref="GameEventScriptMessageSignature"/> based on the message name and, when enabled, the argument signature.
    /// </summary>
    /// <param name="message">The <see cref="GameEventScriptMessage"/> to check against the current signature.
    /// The message must contain a name and a signature ID for comparison.</param>
    /// <returns>True if the name and signature ID of the provided message match those of the current signature; otherwise, false.</returns>
    public bool Matches(GameEventScriptMessage message) => string.Equals(Name, message.Name, StringComparison.Ordinal) && string.Equals(SignatureId, message.SignatureId, StringComparison.Ordinal);

    /// <inheritdoc />
    public bool Equals(GameEventScriptMessageSignature? other)
        => other is not null && string.Equals(SignatureId, other.SignatureId, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => obj is GameEventScriptMessageSignature other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
        => StringComparer.Ordinal.GetHashCode(SignatureId);

    /// <summary>
    /// Creates a message by binding the provided values to this signature's parameters in declaration order.
    /// </summary>
    /// <param name="arguments">The ordered argument values to bind to the signature parameters.</param>
    /// <returns>A message with this signature's name and parameter names.</returns>
    public GameEventScriptMessage WithArguments(params GameEventScriptValue[] arguments) => !TryCreateMessage(arguments, out var message)
        ? throw new ArgumentException($"Message signature '{SignatureId}' expects {Parameters.Count} argument(s) but received {arguments.Length}.", nameof(arguments))
        : message;

    /// <summary>
    /// Attempts to create a message by binding ordered values to this signature's parameter names.
    /// </summary>
    /// <param name="arguments">The ordered argument values to bind to the signature parameters.</param>
    /// <param name="message">The created message when the argument count matches; otherwise undefined.</param>
    /// <returns><c>true</c> when the argument count matches the signature; otherwise <c>false</c>.</returns>
    public bool TryCreateMessage(IReadOnlyList<GameEventScriptValue> arguments, out GameEventScriptMessage message)
    {
        message = default!;
        if (Name.Length == 0) return false;
        if (arguments.Count != Parameters.Count) return false;
        if (arguments.Count == 0)
        {
            message = GameEventScriptMessage.CreatePrecomputed(Name, GameEventScriptNamedArguments.Empty, SignatureId);
            return true;
        }
        var pairs = new KeyValuePair<string, GameEventScriptValue>[arguments.Count];
        for (var index = 0; index < arguments.Count; index++)
        {
            pairs[index] = new KeyValuePair<string, GameEventScriptValue>(Parameters[index], arguments[index] ?? GameEventScriptNothingValue.Instance);
        }

        message = GameEventScriptMessage.CreatePrecomputed(Name, GameEventScriptNamedArguments.CreateOrdered(pairs, Parameters), SignatureId);
        return true;
    }

    private GameEventScriptMessageSignature(string name, IEnumerable<string>? parameters)
    {
        Name = NormalizeMessageName(name);
        Parameters = parameters is not null ? parameters.Select(NormalizeParameterName).ToArray() : [];
        SignatureId = CreateSignatureId(Name, Parameters);
    }
}
