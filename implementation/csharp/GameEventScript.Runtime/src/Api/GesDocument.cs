// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Runtime;
using GameEventScript.Runtime.Values;
using GameEventScript.Syntax;

namespace GameEventScript.Api;

/// <summary>
/// An immutable GES document: a Map or Record root with an optional schema reference, read from and printed to
/// human-editable <c>.gesd</c> text. Comments and layout are not part of a document.
/// </summary>
public sealed class GesDocument : IEquatable<GesDocument>
{
    /// <summary>Wraps a Map or Record root.</summary>
    /// <param name="value">The root: a Map, or a Record whose type name becomes the record type schema.</param>
    /// <param name="schema">A file schema for a Map root; for a Record root, <see langword="null"/> or the record
    /// type schema of its type name.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is neither a Map nor a Record with a valid,
    /// non-built-in type name and field-name keys, contains a value without readable literal text (an External value, a Dice roll below 1
    /// or a nested Record whose type name is not a type name), or <paramref name="schema"/> does not match the root.</exception>
    public GesDocument(GesValue value, GesDocumentSchema? schema = null)
    {
        if (value.Kind == GesBytecodeTypeKind.Map)
        {
            if (schema is { Kind: GesDocumentSchemaKind.Type })
                throw new ArgumentException("A record type schema requires a Record root.", nameof(schema));
        }
        else if (value.ObjectValue is GesCustomObject record)
        {
            if (!GesDocumentSchema.IsRecordTypeName(record.TypeName))
                throw new ArgumentException("A document Record root needs a non-built-in type name.", nameof(value));
            for (var i = 0; i < record.Map.Length; i++)
                if (!GesText.IsFieldName(record.Map.KeyAt(i)))
                    throw new ArgumentException("A document Record root needs field names as keys.", nameof(value));
            var own = new GesDocumentSchema(GesDocumentSchemaKind.Type, record.TypeName);
            if (schema is not null && schema != own)
                throw new ArgumentException("A Record root carries the record type schema of its own type name.", nameof(schema));
            schema = own;
        }
        else throw new ArgumentException("A document root must be a Map or a Record.", nameof(value));
        if (!GesDocumentFormatter.IsPrintable(in value))
            throw new ArgumentException("A document cannot contain External values, Dice rolls below 1 or Records without a type name.", nameof(value));
        Value = value;
        Schema = schema;
    }

    private GesDocument(GesValue value, GesDocumentSchema? schema, bool _)
    {
        Value = value;
        Schema = schema;
    }

    /// <summary>The Map or Record root.</summary>
    public GesValue Value { get; }

    /// <summary>The schema reference, or <see langword="null"/> when the document names none.</summary>
    public GesDocumentSchema? Schema { get; }

    /// <summary>The root entries: the Map entries or the Record fields, in Map order.</summary>
    public GesValueMap Entries => Value.AsMap()!;

    /// <summary>Reads GES document text.</summary>
    /// <param name="text">The document text, for example the contents of a <c>.gesd</c> file.</param>
    /// <returns>The document with its evaluated root and schema reference.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="text"/> contains an unpaired surrogate.</exception>
    /// <exception cref="GesDocumentException">The text is not a valid document or exceeds a literal parsing limit.</exception>
    public static GesDocument Parse(string text)
    {
        var input = GesValue.GesText(text ?? throw new ArgumentNullException(nameof(text)));
        var root = GesLiteralParser.ParseDocument(in input, out var schema);
        return new GesDocument(root, schema, false);
    }

    /// <summary>Prints the document as text that <see cref="Parse"/> reads back to an equal document.</summary>
    /// <param name="format">Layout options, or <see langword="null"/> for the defaults.</param>
    /// <returns>LF-terminated document text with the schema header first, or empty text for an empty document
    /// without a schema.</returns>
    public string Print(GesDocumentFormat? format = null) => GesDocumentFormatter.Print(this, format ?? new GesDocumentFormat());

    /// <summary>Compares the root values and schema references.</summary>
    /// <param name="other">The other document.</param>
    /// <returns><see langword="true"/> when both roots and schema references are equal.</returns>
    public bool Equals(GesDocument? other) => other is not null && Value.Equals(other.Value) && Schema == other.Schema;

    /// <summary>Compares the root values and schema references with another object.</summary>
    /// <param name="obj">The other object.</param>
    /// <returns><see langword="true"/> for an equal document.</returns>
    public override bool Equals(object? obj) => obj is GesDocument other && Equals(other);

    /// <summary>Returns a hash code consistent with <see cref="Equals(GesDocument)"/>.</summary>
    /// <returns>The combined hash of the root and the schema reference.</returns>
    public override int GetHashCode() => HashCode.Combine(Value, Schema);
}
