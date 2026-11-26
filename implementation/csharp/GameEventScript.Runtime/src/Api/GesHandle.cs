// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Globalization;
using System.Text;
using GameEventScript.Runtime;
using GameEventScript.Runtime.Values;
using GameEventScript.Syntax;

namespace GameEventScript.Api;

/// <summary>
/// An immutable handle: the identity of an object that lives outside the host, of one handle type, and optionally its
/// version or a ref that names one. Equality, hashing and order use the type and the identity and ignore version and ref.
/// </summary>
public sealed class GesHandle : IEquatable<GesHandle>, IComparable<GesHandle>
{
    private GesHandle(string typeName, GesValue id, GesValue version, GesValue reference, GesValue part, bool isComposite)
    {
        TypeName = typeName;
        Id = id;
        Version = version;
        Ref = reference;
        Part = part;
        IsComposite = isComposite;
    }

    /// <summary>Gets the handle type name, without the leading <c>@</c>.</summary>
    public string TypeName { get; }

    /// <summary>Gets the id, an integer Number or a Text; a composite's is its root's.</summary>
    public GesValue Id { get; }

    /// <summary>Gets the version: an integer Number for a revision, Bytes for a hash, otherwise Nothing; a composite's is its root's.</summary>
    public GesValue Version { get; }

    /// <summary>Gets the ref that names a version through the owner, a Tag or a Text, otherwise Nothing.</summary>
    public GesValue Ref { get; }

    /// <summary>Gets the part of a composite, an integer Number or a Text; Nothing for any other handle.</summary>
    public GesValue Part { get; }

    /// <summary>Gets whether the handle is a composite: a part of a root object.</summary>
    public bool IsComposite { get; }

    /// <summary>Gets whether the handle carries a version.</summary>
    public bool HasVersion => !Version.IsNothing;

    /// <summary>Creates a handle without a version.</summary>
    /// <param name="typeName">The handle type name, without the leading <c>@</c>.</param>
    /// <param name="id">The id, an integer Number or a Text.</param>
    /// <returns>The handle.</returns>
    /// <exception cref="ArgumentException">Thrown when the type name or the id is invalid.</exception>
    public static GesHandle Create(string typeName, GesValue id) => new(CheckTypeName(typeName), CheckKey(id, nameof(id)), default, default, default, false);

    /// <summary>Creates a handle with a version.</summary>
    /// <param name="typeName">The handle type name, without the leading <c>@</c>.</param>
    /// <param name="id">The id, an integer Number or a Text.</param>
    /// <param name="version">An integer Number for a revision or 20 or 32 Bytes for a hash; Nothing for none.</param>
    /// <returns>The handle.</returns>
    /// <exception cref="ArgumentException">Thrown when the type name, the id or the version is invalid.</exception>
    public static GesHandle Create(string typeName, GesValue id, GesValue version)
        => new(CheckTypeName(typeName), CheckKey(id, nameof(id)), CheckVersion(version, nameof(version)), default, default, false);

    /// <summary>Creates a composite: a part of the object of a root handle, with the root's id, version and ref.</summary>
    /// <param name="typeName">The composite handle type name, without the leading <c>@</c>.</param>
    /// <param name="root">The root handle, which is no composite.</param>
    /// <param name="part">The part, an integer Number or a Text.</param>
    /// <returns>The handle.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="root"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the type name or the part is invalid or the root is a composite.</exception>
    public static GesHandle CreateComposite(string typeName, GesHandle root, GesValue part)
    {
        if (root is null) throw new ArgumentNullException(nameof(root));
        if (root.IsComposite) throw new ArgumentException("A composite's root is no composite.", nameof(root));
        return new GesHandle(CheckTypeName(typeName), root.Id, root.Version, root.Ref, CheckKey(part, nameof(part)), true);
    }

    /// <summary>Returns the handle with a version instead of its version or ref; a composite takes it as its root's.</summary>
    /// <param name="version">An integer Number for a revision or 20 or 32 Bytes for a hash; Nothing for none.</param>
    /// <returns>The handle with the version.</returns>
    /// <exception cref="ArgumentException">Thrown when the version is invalid.</exception>
    public GesHandle WithVersion(GesValue version) => new(TypeName, Id, CheckVersion(version, nameof(version)), default, Part, IsComposite);

    // A handle from wire parts that its schema already checked.
    internal static GesHandle FromWire(string typeName, GesValue id, GesValue version, GesValue part, bool isComposite) => new(typeName, id, version, default, part, isComposite);

    internal GesHandle WithoutVersion() => Version.IsNothing && Ref.IsNothing ? this : new GesHandle(TypeName, Id, default, default, Part, IsComposite);

    internal static GesHandle CreateWithRef(GesHandle handle, GesValue reference) => new(handle.TypeName, handle.Id, default, reference, handle.Part, handle.IsComposite);

    /// <summary>Reads the text form of a handle, a ref included, such as <c>@Drone:7~120</c> or <c>@Unit:drone.gnabblo~#live</c>.</summary>
    /// <param name="text">The text form.</param>
    /// <returns>The handle.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the text is no handle.</exception>
    public static GesHandle Parse(string text)
    {
        if (text is null) throw new ArgumentNullException(nameof(text));
        return TryRead(text, out var handle) ? handle! : throw new ArgumentException($"'{text}' is no handle.", nameof(text));
    }

    // Reads the text form through the scanner documents share; false for anything else.
    internal static bool TryRead(string text, out GesHandle? handle)
    {
        handle = null;
        if (text.Length == 0 || text[0] != '@' || GesHandleLiteral.Scan(text, 0, out var parts) != text.Length) return false;
        GesValue version = default, reference = default;
        switch (parts.Version)
        {
            case GesHandleLiteral.VersionForm.Revision: version = GesValue.GesInteger(parts.Revision); break;
            case GesHandleLiteral.VersionForm.Hash:
                var hex = parts.VersionText;
                var bytes = new byte[hex.Length / 2];
                for (var index = 0; index < bytes.Length; index++) bytes[index] = (byte)((HexDigit(hex[2 * index]) << 4) | HexDigit(hex[(2 * index) + 1]));
                version = GesValue.GesBytes(bytes);
                break;
            case GesHandleLiteral.VersionForm.Tag: reference = GesValue.GesTag(parts.VersionText); break;
            case GesHandleLiteral.VersionForm.Text: reference = GesValue.GesText(parts.VersionText); break;
        }

        handle = new GesHandle(parts.TypeName, Key(parts.Id), version, reference, parts.HasPart ? Key(parts.Part) : default, parts.HasPart);
        return true;

        static GesValue Key(GesHandleLiteral.Key key) => key.IsInteger ? GesValue.GesInteger(key.Integer) : GesValue.GesText(key.Text);
    }

    /// <summary>Writes the canonical text form, such as <c>@Drone:7~120</c>.</summary>
    /// <returns>The text form.</returns>
    public override string ToString()
    {
        var builder = new StringBuilder();
        Write(builder);
        return builder.ToString();
    }

    internal void Write(StringBuilder builder)
    {
        builder.Append('@').Append(TypeName).Append(':');
        WriteKey(Id, false, builder);
        if (!Version.IsNothing)
        {
            builder.Append('~');
            if (Version.ValueKind == GesBytecodeTypeKind.Integer) builder.Append(Version.AsInteger().ToString(CultureInfo.InvariantCulture));
            else AppendHex(Version.AsBytes(), builder);
        }
        else if (!Ref.IsNothing)
        {
            builder.Append('~');
            if (Ref.ValueKind == GesBytecodeTypeKind.Tag) builder.Append('#').Append(Ref.AsText());
            else AppendQuoted(Ref.AsText(), builder);
        }

        if (!IsComposite) return;
        builder.Append('/');
        WriteKey(Part, true, builder);
    }

    /// <summary>Determines whether another handle has the same type and identity.</summary>
    /// <param name="other">The other handle.</param>
    /// <returns><see langword="true"/> when type and identity are equal.</returns>
    public bool Equals(GesHandle? other)
        => other is not null && (ReferenceEquals(this, other) || (string.Equals(TypeName, other.TypeName, StringComparison.Ordinal) && IsComposite == other.IsComposite
            && KeyEquals(Id, other.Id) && KeyEquals(Part, other.Part)));

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is GesHandle other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(TypeName, StringComparer.Ordinal);
        AddKey(ref hash, Id);
        AddKey(ref hash, Part);
        return hash.ToHashCode();
    }

    /// <summary>Compares in key order: by type name in Unicode scalar order, then the id, integers numerically before Texts in scalar order, then a composite's part.</summary>
    /// <param name="other">The other handle; <see langword="null"/> sorts first.</param>
    /// <returns>A negative number, zero or a positive number.</returns>
    public int CompareTo(GesHandle? other)
    {
        if (other is null) return 1;
        if (ReferenceEquals(this, other)) return 0;
        var comparison = GesText.CompareScalarOrdinal(TypeName, other.TypeName);
        if (comparison != 0) return comparison;
        comparison = CompareKey(Id, other.Id);
        if (comparison != 0) return comparison;
        if (IsComposite != other.IsComposite) return IsComposite ? 1 : -1;
        return CompareKey(Part, other.Part);
    }

    private static string CheckTypeName(string typeName)
    {
        if (typeName is null) throw new ArgumentNullException(nameof(typeName));
        return GesText.IsTypeName(typeName) ? typeName : throw new ArgumentException($"'{typeName}' is no handle type name.", nameof(typeName));
    }

    private static GesValue CheckKey(GesValue key, string parameter)
        => key.ValueKind is GesBytecodeTypeKind.Integer && key.Unit == GesQuantityUnit.UnitNone || key.ValueKind == GesBytecodeTypeKind.Text && GesText.GetInvalidUtf16Offset(key.AsText()) < 0
            ? key
            : throw new ArgumentException("A handle id or part is an integer Number or a Text.", parameter);

    private static GesValue CheckVersion(GesValue version, string parameter)
        => version.IsNothing ? default
            : version.ValueKind == GesBytecodeTypeKind.Integer && version.Unit == GesQuantityUnit.UnitNone || version.ValueKind == GesBytecodeTypeKind.Bytes && version.AsBytes().Length is 20 or 32
                ? version
                : throw new ArgumentException("A handle version is an integer Number or 20 or 32 Bytes.", parameter);

    private static bool KeyEquals(GesValue left, GesValue right)
        => left.ValueKind == right.ValueKind && (left.ValueKind switch
        {
            GesBytecodeTypeKind.Integer => left.AsInteger() == right.AsInteger(),
            GesBytecodeTypeKind.Text => string.Equals(left.AsText(), right.AsText(), StringComparison.Ordinal),
            _ => true
        });

    private static void AddKey(ref HashCode hash, GesValue key)
    {
        if (key.ValueKind == GesBytecodeTypeKind.Integer) hash.Add(key.AsInteger());
        else if (key.ValueKind == GesBytecodeTypeKind.Text) hash.Add(key.AsText(), StringComparer.Ordinal);
    }

    private static int CompareKey(GesValue left, GesValue right)
    {
        if (left.ValueKind != right.ValueKind) return Rank(left).CompareTo(Rank(right));
        return left.ValueKind switch
        {
            GesBytecodeTypeKind.Integer => left.AsInteger().CompareTo(right.AsInteger()),
            GesBytecodeTypeKind.Text => GesText.CompareScalarOrdinal(left.AsText(), right.AsText()),
            _ => 0
        };

        static int Rank(GesValue key) => key.ValueKind switch { GesBytecodeTypeKind.Nothing => 0, GesBytecodeTypeKind.Integer => 1, _ => 2 };
    }

    private static int HexDigit(char character) => character <= '9' ? character - '0' : character - 'a' + 10;

    private static void WriteKey(GesValue key, bool isPart, StringBuilder builder)
    {
        if (key.ValueKind == GesBytecodeTypeKind.Integer)
        {
            builder.Append(key.AsInteger().ToString(CultureInfo.InvariantCulture));
            return;
        }

        var text = key.AsText();
        if (GesHandleLiteral.IsBare(text, isPart)) builder.Append(text);
        else AppendQuoted(text, builder);
    }

    private static void AppendQuoted(string text, StringBuilder builder) => builder.Append('\'').Append(text.Replace("'", "''")).Append('\'');

    internal static void AppendHex(ReadOnlySpan<byte> bytes, StringBuilder builder)
    {
        const string Digits = "0123456789abcdef";
        foreach (var value in bytes) builder.Append(Digits[value >> 4]).Append(Digits[value & 15]);
    }
}
