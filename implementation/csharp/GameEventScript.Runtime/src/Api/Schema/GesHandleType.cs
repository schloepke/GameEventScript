// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Text;
using GameEventScript.Syntax;

namespace GameEventScript.Api;

/// <summary>The form of a handle type's id or a composite's part: an integer with a closed range or a Text with a maximum.</summary>
public readonly struct GesHandleKey
{
    internal GesHandleKey(bool isText, long minimum, long maximum, long maxLength)
    {
        IsText = isText;
        Minimum = minimum;
        Maximum = maximum;
        MaxLength = maxLength;
    }

    /// <summary>Gets whether the key is a Text; otherwise it is an integer Number.</summary>
    public bool IsText { get; }

    /// <summary>Gets the smallest integer key; 0 for a Text.</summary>
    public long Minimum { get; }

    /// <summary>Gets the largest integer key; 0 for a Text.</summary>
    public long Maximum { get; }

    /// <summary>Gets the maximum of a Text key in UTF-8 bytes; 0 for an integer.</summary>
    public long MaxLength { get; }
}

/// <summary>
/// The declarative description of one <c>handle</c> declaration of a schema set: a reference type to objects that live outside
/// the host, the schema of their state and the form of their identity and version.
/// </summary>
public sealed class GesHandleType
{
    private readonly GesResolvedHandle _handle;
    private readonly IReadOnlyDictionary<string, GesResolvedDeclaration> _declarations;

    internal GesHandleType(GesResolvedHandle handle, IReadOnlyDictionary<string, GesResolvedDeclaration> declarations)
    {
        _handle = handle;
        _declarations = declarations;
        Name = handle.Name;
        StateName = handle.State?.Name ?? "";
        var key = Key(handle.Id);
        if (handle.Root is null) Id = key;
        else Part = key;
        Version = handle.VersionKind;
        CanonicalForm = GesSchemaCanonical.HandleForm(handle);
        Hash = GesSchemaCanonical.Hash(CanonicalForm);
    }

    /// <summary>Gets the name, without the leading <c>@</c>.</summary>
    public string Name { get; }

    /// <summary>Gets the name of the state schema, without the leading <c>:</c>; empty for a store file type.</summary>
    public string StateName { get; }

    /// <summary>Gets the form of the id; null for a composite, whose identity is its root's id and its part.</summary>
    public GesHandleKey? Id { get; }

    /// <summary>Gets the form of a composite's part; null for any other type.</summary>
    public GesHandleKey? Part { get; }

    /// <summary>Gets the kind of version; a composite has its root's.</summary>
    public GesHandleVersionKind Version { get; }

    /// <summary>Gets the root type of a composite; null for any other type.</summary>
    public GesHandleType? Root => _handle.Root?.Public;

    /// <summary>
    /// Gets the handle type of the store whose files this store file type names, <c>:Store(@Type)</c>; null for any other
    /// type (specs/Schemas.md#handles).
    /// </summary>
    public GesHandleType? StoreOf => _handle.Store?.Public;

    /// <summary>Gets the projections of the state schema in the schema set, the projections a handle of this type can read
    /// through, in ascending name order; empty for a store file type (specs/Schemas.md#projections).</summary>
    public IReadOnlyList<GesSchema> Projections { get; private set; } = [];

    /// <summary>Gets the handle hash: FNV-1a 64 over the canonical form.</summary>
    public ulong Hash { get; }

    /// <summary>Gets the canonical form, which the hash runs over: the declaration and, for a composite, its root's.</summary>
    public string CanonicalForm { get; }

    /// <summary>
    /// Gets the link hash a Program records for the type: FNV-1a 64 over the canonical form followed by the line
    /// <c>state</c> and the state schema's hash, so a changed state schema fails to link as well (specs/Schemas.md#canonical-form).
    /// </summary>
    public ulong LinkHash { get; private set; }

    internal GesResolvedHandle Resolved => _handle;

    // The store file type of this type's store with a name, or null when none of that name names it.
    internal GesHandleType? FileType(string name)
    {
        foreach (var file in _handle.Files)
            if (file.Name == name) return file.Public;
        return null;
    }

    // Called once the state schema has its hash, after every schema of the set is complete.
    // A store file type has no state, so its link hash is its handle hash.
    // Each projection of the state adds its name and schema hash, so a Program and a host agree on what it reads.
    internal void CompleteLinkHash()
    {
        if (_handle.State is null)
        {
            LinkHash = Hash;
            return;
        }

        var projections = new List<GesSchema>();
        foreach (var declaration in _declarations.Values)
            if (declaration.Narrows(_handle.State)) projections.Add(declaration.Public);
        projections.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));
        Projections = new GesReadOnlyArray<GesSchema>(projections);
        var text = new StringBuilder(CanonicalForm).Append("state ").Append(GesSchemaCanonical.Hex(_handle.State.Public.Hash)).Append('\n');
        foreach (var projection in projections) text.Append("projection ").Append(projection.Name).Append(' ').Append(GesSchemaCanonical.Hex(projection.Hash)).Append('\n');
        LinkHash = GesSchemaCanonical.Hash(text.ToString());
    }

    // The projection of this type's state with a name, or null when none of that name narrows it.
    internal GesSchema? FindProjection(string name)
    {
        foreach (var projection in Projections)
            if (projection.Name == name) return projection;
        return null;
    }

    // The declarations of the schema set, by name, so that a nested record of the state finds its schema.
    internal IReadOnlyDictionary<string, GesResolvedDeclaration> Declarations => _declarations;


    private static GesHandleKey Key(GesResolvedType type)
        => type.Kind == GesSchemaTypeKind.Text ? new GesHandleKey(true, 0, 0, type.Max ?? 0)
            : new GesHandleKey(false, Integer(type.Constraints.Lower!), Integer(type.Constraints.Upper!), 0);

    private static long Integer(GesResolvedValue value)
    {
        var number = value.Value;
        var result = number.Significand;
        for (var exponent = 0; exponent < number.Exponent; exponent++) result *= 10;
        return result;
    }
}

/// <summary>A resolved <c>handle</c> declaration.</summary>
internal sealed class GesResolvedHandle
{
    internal string Name { get; init; } = "";

    internal int Offset { get; init; }

    internal GesFileScope Scope { get; init; } = null!;

    internal GesSchemaDeclarationSyntax Syntax { get; init; } = null!;

    // The state schema; null for a store file type, which has none.
    internal GesResolvedDeclaration State { get; set; } = null!;

    // The handle type of the store whose files a store file type names; null for any other type.
    internal GesResolvedHandle? Store { get; set; }

    // The store file types that name this type's store, in declaration order.
    internal List<GesResolvedHandle> Files { get; } = [];

    // The root of a composite, which is not composite itself.
    internal GesResolvedHandle? Root { get; set; }

    // The id, or a composite's part.
    internal GesResolvedType Id { get; set; } = null!;

    // The declared version, or for a composite its root's.
    internal GesHandleVersionKind VersionKind => Root?.VersionKind ?? Syntax.Version;

    internal bool Versioned => VersionKind != GesHandleVersionKind.None;

    // The type of a revision, an integer Number with an optional range.
    internal GesResolvedType? Revision { get; set; }

    internal GesHandleType Public { get; set; } = null!;

    internal void WriteCanonical(StringBuilder builder)
    {
        builder.Append("handle ").Append(Name);
        if (Root is { } root)
        {
            builder.Append(" composite ").Append(root.Name).Append('\n');
            if (Store is { } store) builder.Append("store ").Append(store.Name).Append('\n');
            builder.Append("part ");
            GesSchemaCanonical.WriteHandleType(Id, builder);
            builder.Append('\n');
            return;
        }

        builder.Append('\n').Append("id ");
        GesSchemaCanonical.WriteHandleType(Id, builder);
        builder.Append('\n').Append("version ");
        switch (Syntax.Version)
        {
            case GesHandleVersionKind.None:
                builder.Append("Nothing");
                break;
            case GesHandleVersionKind.ContentHash:
                builder.Append("ContentHash");
                break;
            case GesHandleVersionKind.Sha1:
                builder.Append("Hash(#sha1)");
                break;
            case GesHandleVersionKind.Sha256:
                builder.Append("Hash(#sha256)");
                break;
            default:
                GesSchemaCanonical.WriteHandleType(Revision!, builder);
                break;
        }

        builder.Append('\n');
    }
}
