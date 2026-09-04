// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Text;
using StepH.GameEventScript.Runtime;
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
    /// <param name="arguments">The ordered argument pairs. Their order is part of the message signature.</param>
    /// <returns>Returns a <see cref="GameEventScriptMessage"/> object encapsulating the specified name and arguments.</returns>
    public static GameEventScriptMessage Create(string name, IReadOnlyList<GameEventScriptMessageArgument>? arguments) => new(name, GameEventScriptMessageArguments.Create(arguments));

    /// <summary>
    /// Creates a message without arguments.
    /// </summary>
    public static GameEventScriptMessage Create(string name) => new(name, GameEventScriptMessageArguments.Empty);

    /// <summary>
    /// Creates a new message with the provided arguments and delivery tags. Tags are metadata and are not part of the message signature.
    /// </summary>
    /// <param name="name">The message name.</param>
    /// <param name="arguments">The ordered message arguments.</param>
    /// <param name="tags">Delivery tags associated with the message.</param>
    /// <returns>A new message instance.</returns>
    public static GameEventScriptMessage Create(string name, IReadOnlyList<GameEventScriptMessageArgument>? arguments, IEnumerable<string>? tags) => new(name, GameEventScriptMessageArguments.Create(arguments), tags);

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

    internal GameEventScriptMessage WithNormalizedTags(IReadOnlyList<string>? tags) => new(Name, Arguments, SignatureId, MergeNormalizedTags(Tags, tags));

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

    internal static GameEventScriptMessage CreatePrecomputedWithNormalizedTags(string normalizedName, GameEventScriptMessageArguments arguments, string signatureId, IReadOnlyList<string>? tags = null)
        => new(normalizedName, arguments, signatureId, tags ?? []);

    internal static string NormalizeTagName(string? tag)
    {
        if (tag is null) return string.Empty;
        GameEventScriptText.RequireValidUnicode(tag, nameof(tag));
        var normalized = GameEventScriptText.TrimAsciiWhitespace(tag);
        if (normalized.Length > 0 && normalized[0] == '#') normalized = normalized[1..];
        if (normalized.Length == 0) return string.Empty;
        if (!GameEventScriptText.IsTagName(normalized))
            throw new ArgumentException("Tag names must use the portable lowercase ASCII name grammar.", nameof(tag));
        return normalized;
    }

    internal static IReadOnlyList<string> NormalizeTags(IEnumerable<string>? tags)
    {
        if (tags is null) return [];

        var buffer = new GameEventScriptTagBuffer(GetKnownCount(tags));
        foreach (var tag in tags)
        {
            buffer.Add(tag);
        }

        return buffer.ToArrayOrEmpty();
    }

    private static IReadOnlyList<string> MergeTags(IReadOnlyList<string> existingTags, IEnumerable<string>? newTags)
    {
        if (newTags is null)
        {
            return existingTags;
        }

        var buffer = new GameEventScriptTagBuffer(existingTags.Count + GetKnownCount(newTags));
        for (var index = 0; index < existingTags.Count; index++)
        {
            buffer.Add(existingTags[index]);
        }

        var existingCount = buffer.Count;
        foreach (var tag in newTags)
        {
            buffer.Add(tag);
        }

        if (buffer.Count == existingCount)
        {
            return existingTags;
        }

        return buffer.ToArrayOrEmpty();
    }

    private static IReadOnlyList<string> MergeNormalizedTags(IReadOnlyList<string> existingTags, IReadOnlyList<string>? newTags)
    {
        if (newTags is null || newTags.Count == 0)
        {
            return existingTags;
        }

        string[]? result = null;
        var count = existingTags.Count;
        for (var tagIndex = 0; tagIndex < newTags.Count; tagIndex++)
        {
            var tag = newTags[tagIndex];
            if (string.IsNullOrEmpty(tag))
            {
                continue;
            }

            var exists = false;
            for (var index = 0; index < count; index++)
            {
                var existingTag = result is null ? existingTags[index] : result[index];
                if (!string.Equals(existingTag, tag, StringComparison.Ordinal))
                {
                    continue;
                }

                exists = true;
                break;
            }

            if (exists)
            {
                continue;
            }

            if (result is null)
            {
                result = new string[existingTags.Count + newTags.Count - tagIndex];
                for (var index = 0; index < existingTags.Count; index++)
                {
                    result[index] = existingTags[index];
                }
            }

            if (count == result.Length)
            {
                var next = new string[result.Length << 1];
                Array.Copy(result, next, result.Length);
                result = next;
            }

            result[count++] = tag;
        }

        if (result is null)
        {
            return existingTags;
        }

        if (count == result.Length)
        {
            return result;
        }

        var compact = new string[count];
        Array.Copy(result, compact, count);
        return compact;
    }

    private static int GetKnownCount(IEnumerable<string> values)
    {
        if (values is ICollection<string> collection)
        {
            return collection.Count;
        }

        return values is IReadOnlyCollection<string> readOnlyCollection ? readOnlyCollection.Count : 4;
    }
}
