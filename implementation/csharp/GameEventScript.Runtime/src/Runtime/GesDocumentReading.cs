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

    // The value an explicit form of a readable document constructs, as the node a schema check reads. Forms a check accepts
    // by their type stay forms; every position inside the result is that of the form.
    internal static GesDocumentNode Resolve(GesDocumentNode form)
    {
        var fields = GesLiteralEvaluation.EvaluateDocument(Value(GesDocumentNode.Map(["v"], [form])), out _)!.Value.AsMap()!;
        return Node(fields.Length == 0 ? GesValue.GesNothing() : fields.ValueAt(0), form.Start);
    }

    private static GesDocumentNode Node(GesValue value, int start)
    {
        var node = value.ValueKind switch
        {
            GesBytecodeTypeKind.Nothing => GesDocumentNode.NothingValue(),
            GesBytecodeTypeKind.Boolean => GesDocumentNode.BooleanValue(value.AsBoolean()),
            GesBytecodeTypeKind.Integer => GesDocumentNode.NumberValue(GesNumberLiteral.Integer(value.AsInteger(), value.ValueUnit)),
            GesBytecodeTypeKind.Float => GesDocumentNode.NumberValue(GesNumberLiteral.Float(value.AsNumeric, value.ValueUnit)),
            GesBytecodeTypeKind.Percentage => GesDocumentNode.NumberValue(GesNumberLiteral.Percentage(value.AsNumeric)),
            GesBytecodeTypeKind.Text => GesDocumentNode.Text(value.AsText()),
            GesBytecodeTypeKind.Tag => GesDocumentNode.Tag(value.AsText()),
            GesBytecodeTypeKind.Vector => GesDocumentNode.Spatial(false, value.X, value.Y, value.Z, value.ValueUnit),
            GesBytecodeTypeKind.Point => GesDocumentNode.Spatial(true, value.X, value.Y, value.Z, value.ValueUnit),
            GesBytecodeTypeKind.Dice => GesDocumentNode.Dice(value.AsDice()),
            GesBytecodeTypeKind.Bytes => GesDocumentNode.BytesValue(value.AsBytes().ToArray()),
            GesBytecodeTypeKind.List => GesDocumentNode.List(Nodes(value.AsList(), start)),
            GesBytecodeTypeKind.Map => MapNode(value.AsMap()!, start),
            GesBytecodeTypeKind.Custom when value.CustomTypeName is { } name => GesDocumentNode.Form("record", ["_", "_"], [GesDocumentNode.Text(name), MapNode(value.AsMap()!, start)], start),
            _ => GesDocumentNode.Form(value.ValueKind.ToString().ToLowerInvariant(), [], [], start)
        };
        node.Start = start;
        return node;
    }

    private static GesDocumentNode[] Nodes(GesValueSlice values, int start)
    {
        var nodes = new GesDocumentNode[values.Length];
        for (var i = 0; i < nodes.Length; i++) nodes[i] = Node(values[i], start);
        return nodes;
    }

    private static GesDocumentNode MapNode(GesValueMap fields, int start)
    {
        var keys = new string[fields.Length];
        var values = new GesDocumentNode[fields.Length];
        var offsets = new int[fields.Length];
        for (var i = 0; i < keys.Length; i++)
        {
            keys[i] = fields.KeyAt(i);
            values[i] = Node(fields.ValueAt(i), start);
            offsets[i] = start;
        }
        return GesDocumentNode.Map(keys, values, offsets);
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
            case GesDocumentNodeKind.Bytes:
            {
                var bytes = new GesValue();
                bytes.SetBytes(node.Bytes);
                return bytes;
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
