// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Text;
using GameEventScript.Syntax;

namespace GameEventScript.Api;

/// <summary>The form of a handle type's id or part: an integer with a closed range or a Text with a maximum.</summary>
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
        IsKeyed = handle.IsKeyed;
        var key = Key(handle.Id);
        if (handle.HasPart) Part = key;
        else Id = key;
        Version = handle.VersionKind;
        CanonicalForm = GesSchemaCanonical.HandleForm(handle);
        Hash = GesSchemaCanonical.Hash(CanonicalForm);
    }

    /// <summary>Gets the name, without the leading <c>@</c>.</summary>
    public string Name { get; }

    /// <summary>Gets the name of the state schema, without the leading <c>:</c>; empty for a type without state.</summary>
    public string StateName { get; }

    /// <summary>Gets whether the type has a state schema; a type without one only names objects and their versions.</summary>
    public bool HasState => StateName.Length > 0;

    /// <summary>
    /// Gets the form of the id; for a type with a root and without a part its root's, since its identity is the root's id; null
    /// for a type with a part, whose identity is its root's id and its part.
    /// </summary>
    public GesHandleKey? Id { get; }

    /// <summary>Gets the form of the part of a type attached to or keyed by a root; null when it has none.</summary>
    public GesHandleKey? Part { get; }

    /// <summary>Gets the kind of version: a type attached to a root has its root's, a keyed type its own.</summary>
    public GesHandleVersionKind Version { get; }

    /// <summary>Gets the root a type is attached to or keyed by; null for a type without a root.</summary>
    public GesHandleType? Root => _handle.Root?.Public;

    /// <summary>
    /// Gets whether the type is keyed by its root (<c>keyed by @Root</c>): its objects have their own version, lifetime and source;
    /// otherwise a type with a root is attached to it (specs/Schemas.md#handles).
    /// </summary>
    public bool IsKeyed { get; }

    /// <summary>Gets the projections of the state schema in the schema set, the projections a handle of this type can read
    /// through, in ascending name order; empty for a type without state (specs/Schemas.md#projections).</summary>
    public IReadOnlyList<GesSchema> Projections { get; private set; } = [];

    /// <summary>Gets the handle hash: FNV-1a 64 over the canonical form.</summary>
    public ulong Hash { get; }

    /// <summary>Gets the canonical form, which the hash runs over: the declaration and, for a type with a root, its root's.</summary>
    public string CanonicalForm { get; }

    /// <summary>
    /// Gets the link hash a Program records for the type: FNV-1a 64 over the canonical form followed by the line
    /// <c>state</c> and the state schema's hash, so a changed state schema fails to link as well (specs/Schemas.md#canonical-form).
    /// </summary>
    public ulong LinkHash { get; private set; }

    internal GesResolvedHandle Resolved => _handle;

    // Called once the state schema has its hash, after every schema of the set is complete.
    // A type without state has its handle hash as its link hash.
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

    // The state schema; null for a type without state.
    internal GesResolvedDeclaration? State { get; set; }

    // The root a type is attached to or keyed by, which has no root itself.
    internal GesResolvedHandle? Root { get; set; }

    // The id, or the part of a type with a root; a type with a root and without a part has its root's id.
    internal GesResolvedType Id
    {
        get => _id ?? Root!.Id;
        set => _id = value;
    }

    private GesResolvedType? _id;

    // Whether a type with a root names a part of it; one without a part is one per root and keyed by the root's id.
    internal bool HasPart => Root is not null && Syntax.Id is not null;

    internal bool IsKeyed => Syntax.Form == GesSchemaHandleForm.Keyed;

    internal bool Attached => Syntax.Form == GesSchemaHandleForm.Attached;

    // The declared version, or for a type attached to a root its root's.
    internal GesHandleVersionKind VersionKind => Syntax.Form == GesSchemaHandleForm.Attached ? Root!.VersionKind : Syntax.Version;

    // The type whose version a handle carries: an attached type's root, otherwise the type itself.
    internal GesResolvedHandle VersionOwner => Syntax.Form == GesSchemaHandleForm.Attached ? Root! : this;

    internal bool Versioned => VersionKind != GesHandleVersionKind.None;

    // The type of a revision, an integer Number with an optional range.
    internal GesResolvedType? Revision { get; set; }

    internal GesHandleType Public { get; set; } = null!;

    // `handle X` with its form and root, the id or part, the version of a type that is not attached and `stateless` for a type
    // without state; the state's own hash joins only the link hash.
    internal void WriteCanonical(StringBuilder builder)
    {
        builder.Append("handle ").Append(Name);
        switch (Syntax.Form)
        {
            case GesSchemaHandleForm.Attached:
                builder.Append(" attached ").Append(Root!.Name);
                break;
            case GesSchemaHandleForm.Keyed:
                builder.Append(" keyed ").Append(Root!.Name);
                break;
        }

        builder.Append('\n');
        if (State is null) builder.Append("stateless\n");
        if (Root is null || HasPart)
        {
            builder.Append(Root is null ? "id " : "part ");
            GesSchemaCanonical.WriteHandleType(Id, builder);
            builder.Append('\n');
        }

        if (Syntax.Form == GesSchemaHandleForm.Attached) return;
        builder.Append("version ");
        switch (Syntax.Version)
        {
            case GesHandleVersionKind.None:
                builder.Append("none");
                break;
            case GesHandleVersionKind.GesHash:
                builder.Append("hash(#ges)");
                break;
            case GesHandleVersionKind.Sha1:
                builder.Append("hash(#sha1)");
                break;
            case GesHandleVersionKind.Sha256:
                builder.Append("hash(#sha256)");
                break;
            default:
                builder.Append("revision ");
                GesSchemaCanonical.WriteHandleType(Revision!, builder);
                break;
        }

        builder.Append('\n');
    }
}
