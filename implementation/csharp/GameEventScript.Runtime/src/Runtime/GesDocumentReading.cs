// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using GameEventScript.Syntax;
using Code = GameEventScript.Syntax.GesDocumentDiagnostic;

namespace GameEventScript.Runtime;

// Builds the value of a document that the Syntax reader has read; explicit forms construct here.
internal static class GesDocumentReading
{
    /// <exception cref="GesDocumentException">The text is not a valid document or exceeds a literal parsing limit.</exception>
    internal static GesValue Read(string text, out GesDocumentSchema? schema)
    {
        var syntax = GesDocumentReader.Read(text);
        if (syntax.Diagnostic is { } diagnostic)
            throw Error(text, diagnostic.Code, diagnostic.Start, diagnostic.LimitName, diagnostic.LimitName is null ? null : diagnostic.LimitValue);
        schema = syntax.Schema switch
        {
            GesDocumentSchemaSyntax.Type => new GesDocumentSchema(GesDocumentSchemaKind.Type, syntax.SchemaName),
            GesDocumentSchemaSyntax.File => new GesDocumentSchema(GesDocumentSchemaKind.File, syntax.SchemaName),
            _ => null
        };
        GesValue map;
        try
        {
            map = GesLiteralEvaluation.EvaluateDocument(Value(syntax.Root!), out var invalidOffset)
                ?? throw Error(text, Code.InvalidLiteral, invalidOffset);
        }
        catch (GesLiteralLimitException exception)
        {
            throw Error(text, Code.LimitExceeded, exception.Offset, exception.LimitName, exception.Limit);
        }
        if (schema is not { Kind: GesDocumentSchemaKind.Type } type) return map;
        var fields = map.AsMap()!;
        var keys = new string[fields.Length];
        var values = new GesValue[fields.Length];
        for (var i = 0; i < keys.Length; i++)
        {
            keys[i] = fields.KeyAt(i);
            values[i] = fields.ValueAt(i);
        }
        return GesValue.GesRecord(type.Name, keys, values);
    }

    private static GesDocumentException Error(string text, string code, int offset, string? limitName = null, int? limit = null)
    {
        var (line, column) = GesDocumentSyntax.Location(text, offset);
        return new GesDocumentException(code, line, column, limitName, limit);
    }

    // Scalars become values; Maps, forms and messages become literal nodes for document evaluation.
    private static GesValue Value(GesDocumentNode node)
    {
        switch (node.Kind)
        {
            case GesDocumentNodeKind.Text: return GesValue.GesText(node.Name);
            case GesDocumentNodeKind.Boolean: return GesValue.GesBoolean(node.Boolean);
            case GesDocumentNodeKind.Nothing: return GesValue.GesNothing();
            case GesDocumentNodeKind.Tag: return GesValue.GesTag(node.Name);
            case GesDocumentNodeKind.Number: return GesTextNumberCast.Value(node.Number)!.Value;
            case GesDocumentNodeKind.Vector: return GesValue.GesVector(node.X, node.Y, node.Z, node.Unit);
            case GesDocumentNodeKind.Point: return GesValue.GesPoint(node.X, node.Y, node.Z, node.Unit);
            case GesDocumentNodeKind.Dice:
            {
                var dice = new GesValue();
                dice.SetDice(node.Rolls);
                return dice;
            }
            case GesDocumentNodeKind.List:
            {
                var list = new GesValue();
                list.SetList(Values(node.Children));
                return list;
            }
            case GesDocumentNodeKind.Map: return new GesLiteralNode("@map", node.Labels, Values(node.Children)).Value;
            case GesDocumentNodeKind.Form: return new GesLiteralNode(node.Name, node.Labels, Values(node.Children), null, node.Offset).Value;
            case GesDocumentNodeKind.Message: return new GesLiteralNode("@message:" + node.Name, node.Labels, Values(node.Children), null, node.Offset).Value;
            default: return GesValue.GesHandler(GesMessageSignature.Create(node.Name, node.Labels));
        }
    }

    private static GesValue[] Values(GesDocumentNode[] nodes)
    {
        var values = new GesValue[nodes.Length];
        for (var i = 0; i < values.Length; i++) values[i] = Value(nodes[i]);
        return values;
    }
}
