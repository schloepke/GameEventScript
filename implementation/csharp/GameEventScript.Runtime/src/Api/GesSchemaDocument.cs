// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using GameEventScript.Runtime;
using GameEventScript.Runtime.Values;
using GameEventScript.Syntax;

namespace GameEventScript.Api;

/// <summary>
/// A GES document read against the schema its header names, as generated models read their documents: the header
/// <c>:Schema(:Name)</c> names one of the expected schemas, the document passes the schema's check, and its entries are the
/// plain value that a model converts. Errors carry their position and field path.
/// </summary>
public sealed class GesSchemaDocument
{
    /// <summary>The code of a document whose header names no expected schema.</summary>
    public const string SchemaMismatch = "document.schemaMismatch";

    private readonly GesDocumentNode _root;
    private readonly string _text;

    private GesSchemaDocument(string schema, GesValue value, GesDocumentNode root, string text)
    {
        Schema = schema;
        Value = value;
        _root = root;
        _text = text;
    }

    /// <summary>Gets the schema the header names.</summary>
    public string Schema { get; }

    /// <summary>Gets the document's entries as a Record of <see cref="Schema"/>, which a model of it converts.</summary>
    public GesValue Value { get; }

    /// <summary>Reads document text whose header names one of the expected schemas and checks it against that schema.</summary>
    /// <param name="schemas">The schema set that declares the expected schemas.</param>
    /// <param name="text">The document text.</param>
    /// <param name="expected">The schemas the header may name.</param>
    /// <returns>The checked document.</returns>
    /// <exception cref="GesDocumentException">The text is no document, its header names no expected schema
    /// (<see cref="SchemaMismatch"/>, naming both), or it breaks the schema, with the <c>document.*</c> code, its position and
    /// the field path.</exception>
    public static GesSchemaDocument Read(GesSchemaSet schemas, string text, params string[] expected)
    {
        if (schemas is null) throw new ArgumentNullException(nameof(schemas));
        if (text is null) throw new ArgumentNullException(nameof(text));
        if (expected is null) throw new ArgumentNullException(nameof(expected));
        var value = GesDocumentReading.Read(text, out _);
        var syntax = GesDocumentReader.Read(text);
        var named = syntax.Schema == GesDocumentSchemaSyntax.Type ? syntax.SchemaName : null;
        if (named is null || Array.IndexOf(expected, named) < 0)
        {
            var wanted = string.Join(" or ", Array.ConvertAll(expected, name => ":" + name));
            var found = named is null ? "no schema" : ":" + named;
            throw new GesDocumentException(SchemaMismatch, 1, 1, message: $"The document names {found} where {wanted} is expected.");
        }

        var declaration = schemas.Declaration(named);
        if (GesSchemaDocumentCheck.Failing(text, syntax.Root!, declaration, null) is { } failure) throw failure;
        return new GesSchemaDocument(named, GesSchemaValues.DocumentShapes(declaration, value), syntax.Root!, text);
    }

    /// <summary>
    /// Places an error of converting <see cref="Value"/> into a model at the value of its path in the document: the deepest
    /// entry the path names, and a List for an element of it, since paths name no index.
    /// </summary>
    /// <param name="error">The conversion error.</param>
    /// <returns>A document error with the error's code and path.</returns>
    public GesDocumentException Locate(GesWireException error)
    {
        if (error is null) throw new ArgumentNullException(nameof(error));
        var node = _root;
        foreach (var segment in error.Path.Split('.'))
        {
            var end = segment.IndexOfAny(['[', ':']);
            var name = end < 0 ? segment : segment.Substring(0, end);
            if (Entries(node) is not { } entries) break;
            var index = Array.IndexOf(entries.Labels, name);
            if (index < 0) break;
            node = entries.Children[index];
            if (segment.IndexOf('[') >= 0) break;
        }

        var (line, column) = GesDocumentSyntax.Location(_text, node.Start);
        return new GesDocumentException(error.Code, line, column, path: error.Path, message: $"{error.Message} at line {line}, column {column}.");
    }

    // The entries of a Map or of a Record form.
    private static GesDocumentNode? Entries(GesDocumentNode node)
    {
        if (node.Kind == GesDocumentNodeKind.Map) return node;
        if (node.Kind != GesDocumentNodeKind.Form || node.Name != "record" || GesLiteralForms.Positions("record", node.Labels) is not { } positions) return null;
        var fields = node.Children[Array.IndexOf(positions, 1)];
        return fields.Kind == GesDocumentNodeKind.Map ? fields : null;
    }
}
