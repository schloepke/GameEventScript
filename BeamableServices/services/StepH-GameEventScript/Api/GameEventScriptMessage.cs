using System;
using System.Collections.Generic;
using System.Text;
using StepH.GameEventScript.Runtime.Values;

namespace StepH.GameEventScript.Api;

/// <summary>
/// Represents a message used within the Game Event Script system.
/// This message serves as a fundamental unit for communication, allowing for
/// event-driven execution between components, scripts, and handlers.
/// </summary>
/// <remarks>
/// A GameEventScriptMessage encapsulates a message name and an optional set
/// of named arguments. Additionally, it generates a unique signature ID for
/// efficient message identification and dispatch during runtime.
/// </remarks>
public sealed class GameEventScriptMessage : IEquatable<GameEventScriptMessage>
{
    /// <summary>
    /// Creates a new instance of <see cref="GameEventScriptMessage"/> using the specified name and arguments.
    /// This method facilitates the construction of a message containing the provided name and its associated
    /// arguments, allowing events to be parameterized and passed within the Game Event Script system.
    /// </summary>
    /// <param name="name">The name of the message. This is used to identify the message within the system.</param>
    /// <param name="arguments">A dictionary containing the named arguments for the message, where keys
    /// represent argument names, and values are the corresponding <see cref="GameEventScriptValue"/> instances.
    /// This parameter can be null if no arguments are provided.</param>
    /// <returns>Returns a <see cref="GameEventScriptMessage"/> object encapsulating the specified name and arguments.</returns>
    public static GameEventScriptMessage Create(string name, IReadOnlyDictionary<string, GameEventScriptValue>? arguments) => new(name, GameEventScriptMessageArguments.Create(arguments));

    /// <summary>
    /// Creates a new message with the provided arguments and delivery tags. Tags are metadata and are not part of the message signature.
    /// </summary>
    /// <param name="name">The message name.</param>
    /// <param name="arguments">The named message arguments.</param>
    /// <param name="tags">Delivery tags associated with the message.</param>
    /// <returns>A new message instance.</returns>
    public static GameEventScriptMessage Create(string name, IReadOnlyDictionary<string, GameEventScriptValue>? arguments, IEnumerable<string>? tags) => new(name, GameEventScriptMessageArguments.Create(arguments), tags);

    /// <summary>
    /// Constructs a <see cref="GameEventScriptMessage"/> instance with a specified name and associated arguments.
    /// This method provides a convenient way to create a message by defining its name
    /// and passing a collection of key-value argument pairs.
    /// </summary>
    /// <param name="name">The name of the message, used to identify its purpose or type.</param>
    /// <param name="arguments">
    /// A collection of key-value pairs where each key represents the argument's name and
    /// the value is a <see cref="GameEventScriptValue"/> associated with that argument.
    /// </param>
    /// <returns>A new instance of the <see cref="GameEventScriptMessage"/> class constructed with the specified name and arguments.</returns>
    public static GameEventScriptMessage Create(string name, params (string name, GameEventScriptValue value)[] arguments)
    {
        if (arguments.Length == 0)
        {
            return new GameEventScriptMessage(name, GameEventScriptMessageArguments.Empty);
        }

        var names = new string[arguments.Length];
        var values = new GameEventScriptValue[arguments.Length];
        for (var index = 0; index < arguments.Length; index++)
        {
            var argument = arguments[index];
            names[index] = argument.name;
            values[index] = argument.value;
        }

        return new GameEventScriptMessage(name, GameEventScriptMessageArguments.CreateOrdered(names, values));
    }

    /// <summary>
    /// Gets the name of the message associated with the GameEventScriptMessage instance.
    /// This name serves as a key identifier for the message, which is normalized to ensure
    /// a consistent format and allow for accurate handling and comparison within the
    /// Game Event Script system.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Represents the named arguments associated with a Game Event Script message.
    /// This property holds a collection of key-value pairs where keys are argument names
    /// and values are their corresponding data. It provides access to additional contextual
    /// information required to process the message during event-driven execution.
    /// </summary>
    /// <remarks>
    /// If no arguments are provided, this property defaults to an empty collection.
    /// The arguments are immutable and are used to construct the message's unique
    /// signature for identification and dispatch purposes.
    /// </remarks>
    public GameEventScriptMessageArguments Arguments { get; }

    /// <summary>
    /// Gets the normalized delivery tags associated with this message. Tags do not affect the message signature.
    /// </summary>
    public IReadOnlyList<string> Tags { get; }

    /// <summary>
    /// Represents a unique identifier for the signature of a <see cref="GameEventScriptMessage"/>.
    /// The value is derived from the normalized name of the message and its associated arguments,
    /// allowing for precise matching and routing within the game event script system.
    /// </summary>
    public string SignatureId { get; }

    /// <summary>
    /// Returns a string representation of the current GameEventScriptMessage instance.
    /// This representation includes the message name and, if present, its associated arguments.
    /// </summary>
    /// <returns>A string that represents the current GameEventScriptMessage instance.</returns>
    public override string ToString()
    {
        var message = Arguments.Count == 0 ? Name : $"{Name}({Arguments})";
        if (Tags.Count == 0)
        {
            return message;
        }

        var builder = new StringBuilder(message);
        builder.Append(" with ");
        for (var index = 0; index < Tags.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(", ");
            }

            builder.Append('#').Append(Tags[index]);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Determines whether the message has the specified normalized delivery tag.
    /// </summary>
    /// <param name="tag">The normalized tag name or a tag literal using a leading '#'.</param>
    /// <returns><c>true</c> when the tag is present; otherwise <c>false</c>.</returns>
    public bool HasTag(string tag)
    {
        var normalized = NormalizeTagName(tag);
        if (string.IsNullOrEmpty(normalized))
        {
            return false;
        }

        for (var index = 0; index < Tags.Count; index++)
        {
            if (string.Equals(Tags[index], normalized, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc />
    public bool Equals(GameEventScriptMessage? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (!string.Equals(SignatureId, other.SignatureId, StringComparison.Ordinal)) return false;
        if (Tags.Count != other.Tags.Count) return false;
        for (var index = 0; index < Tags.Count; index++)
        {
            if (!string.Equals(Tags[index], other.Tags[index], StringComparison.Ordinal))
            {
                return false;
            }
        }

        if (Arguments.Count != other.Arguments.Count) return false;

        for (var index = 0; index < Arguments.Count; index++)
        {
            ref readonly var left = ref Arguments.VmValueAt(index);
            ref readonly var right = ref other.Arguments.VmValueAt(index);
            if (!left.EqualsValue(in right))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => obj is GameEventScriptMessage other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(SignatureId, StringComparer.Ordinal);
        for (var index = 0; index < Arguments.Count; index++)
        {
            hash.Add(Arguments.VmValueAt(index).GetValueHashCode());
        }

        for (var index = 0; index < Tags.Count; index++)
        {
            hash.Add(Tags[index], StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }

    /// <summary>
    /// Creates a copy of this message with the provided delivery tags merged into the existing tag set.
    /// </summary>
    /// <param name="tags">The normalized tag names or tag literals using a leading '#'.</param>
    /// <returns>A message copy with the merged tags.</returns>
    public GameEventScriptMessage WithTags(IEnumerable<string>? tags) => new(Name, Arguments, SignatureId, MergeTags(Tags, tags));

    /// <summary>
    /// Creates a copy of this message with the provided delivery tags merged into the existing tag set.
    /// </summary>
    /// <param name="tags">The normalized tag names or tag literals using a leading '#'.</param>
    /// <returns>A message copy with the merged tags.</returns>
    public GameEventScriptMessage WithTags(params string[] tags) => WithTags((IEnumerable<string>?)tags);

    private GameEventScriptMessage(string name, GameEventScriptMessageArguments? arguments = null, IEnumerable<string>? tags = null)
    {
        Name = GameEventScriptMessageSignature.NormalizeMessageName(name);
        if (Name.Length == 0)
        {
            throw new ArgumentException("Message name must not be null, empty, or whitespace.", nameof(name));
        }

        Arguments = arguments ?? GameEventScriptMessageArguments.Empty;
        SignatureId = GameEventScriptMessageSignature.CreateSignatureId(Name, Arguments.SignatureLabels);
        Tags = NormalizeTags(tags);
    }

    private GameEventScriptMessage(string normalizedName, GameEventScriptMessageArguments arguments, string signatureId, IReadOnlyList<string>? tags = null)
    {
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new ArgumentException("Message name must not be null, empty, or whitespace.", nameof(normalizedName));
        }

        Name = normalizedName;
        Arguments = arguments;
        SignatureId = signatureId;
        Tags = tags ?? [];
    }

    internal static GameEventScriptMessage CreatePrecomputed(string normalizedName, GameEventScriptMessageArguments arguments, string signatureId, IEnumerable<string>? tags = null)
        => new(normalizedName, arguments, signatureId, NormalizeTags(tags));

    internal static string NormalizeTagName(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return string.Empty;
        var normalized = tag.Trim();
        return normalized.Length > 0 && normalized[0] == '#' ? normalized[1..] : normalized;
    }

    internal static IReadOnlyList<string> NormalizeTags(IEnumerable<string>? tags)
    {
        if (tags is null) return [];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var normalizedTags = new List<string>();
        foreach (var tag in tags)
        {
            var normalized = NormalizeTagName(tag);
            if (normalized.Length == 0 || !seen.Add(normalized)) continue;
            normalizedTags.Add(normalized);
        }

        if (normalizedTags.Count == 0)
        {
            return [];
        }

        var result = new string[normalizedTags.Count];
        for (var index = 0; index < normalizedTags.Count; index++)
        {
            result[index] = normalizedTags[index];
        }

        return result;
    }

    private static IReadOnlyList<string> MergeTags(IReadOnlyList<string> existingTags, IEnumerable<string>? newTags)
    {
        if (newTags is null)
        {
            return existingTags;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var mergedTags = new List<string>(existingTags.Count);
        for (var index = 0; index < existingTags.Count; index++)
        {
            var existingTag = existingTags[index];
            if (existingTag.Length == 0 || !seen.Add(existingTag))
            {
                continue;
            }

            mergedTags.Add(existingTag);
        }

        foreach (var tag in newTags)
        {
            var normalized = NormalizeTagName(tag);
            if (normalized.Length == 0 || !seen.Add(normalized))
            {
                continue;
            }

            mergedTags.Add(normalized);
        }

        if (mergedTags.Count == existingTags.Count)
        {
            return existingTags;
        }

        var result = new string[mergedTags.Count];
        for (var index = 0; index < mergedTags.Count; index++)
        {
            result[index] = mergedTags[index];
        }

        return result;
    }
}
