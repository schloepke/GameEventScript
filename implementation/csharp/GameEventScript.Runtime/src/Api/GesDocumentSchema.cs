// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Runtime;

namespace GameEventScript.Api;

/// <summary>The kind of schema a GES document references.</summary>
public enum GesDocumentSchemaKind
{
    /// <summary>A record type, written <c>:Schema(:TypeName)</c>; the document root is a Record of that type.</summary>
    Type,

    /// <summary>A schema file, written <c>:Schema('path.gess')</c>; the host interprets the path and the root is a Map.</summary>
    File,
}

/// <summary>
/// The schema reference of a GES document. It is a reference only: GES neither loads schema files nor validates
/// documents against record definitions.
/// </summary>
public sealed record GesDocumentSchema
{
    /// <summary>Creates a schema reference.</summary>
    /// <param name="kind">Whether <paramref name="name"/> is a record type name or a schema file path.</param>
    /// <param name="name">A record type name following the source <c>TypeName</c> grammar and naming no built-in type,
    /// or a non-empty schema file path.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is not valid for <paramref name="kind"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is not defined.</exception>
    public GesDocumentSchema(GesDocumentSchemaKind kind, string name)
    {
        if (name is null) throw new ArgumentNullException(nameof(name));
        switch (kind)
        {
            case GesDocumentSchemaKind.Type:
                if (!IsRecordTypeName(name)) throw new ArgumentException("A schema record type must be a non-built-in type name.", nameof(name));
                break;
            case GesDocumentSchemaKind.File:
                if (name.Length == 0) throw new ArgumentException("A schema file path must not be empty.", nameof(name));
                GesText.RequireValidUnicode(name, nameof(name));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
        Kind = kind;
        Name = name;
    }

    /// <summary>Whether <see cref="Name"/> is a record type name or a schema file path.</summary>
    public GesDocumentSchemaKind Kind { get; }

    /// <summary>The record type name or the schema file path.</summary>
    public string Name { get; }

    internal static bool IsRecordTypeName(string name)
    {
        if (!GesText.IsTypeName(name)) return false;
        var builtin = name.ToLowerInvariant();
        return !(GesDataConstruction.IsType(builtin) && name == char.ToUpperInvariant(builtin[0]) + builtin.Substring(1));
    }
}
