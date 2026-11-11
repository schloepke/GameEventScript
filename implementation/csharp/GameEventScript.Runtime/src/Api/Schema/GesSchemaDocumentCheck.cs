// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GameEventScript.Runtime;
using GameEventScript.Syntax;

namespace GameEventScript.Api;

// Checks a read document against a schema on the document's nodes, so errors keep their positions. It needs no VM.
internal sealed class GesSchemaDocumentCheck
{
    internal const string MissingField = "document.missingField";
    internal const string UnknownField = "document.unknownField";
    internal const string FieldType = "document.fieldType";
    internal const string OutOfRange = "document.outOfRange";

    private sealed class Failure(string code, int offset) : Exception(code)
    {
        internal string Code { get; } = code;

        internal int Offset { get; } = offset;
    }

    private readonly GesSchemaParameters? _parameters;

    // What each alternative of a union answered and what each form constructed, so nested unions stay linear.
    private readonly Dictionary<(GesResolvedType Type, GesDocumentNode Value), bool> _holds = new();
    private readonly Dictionary<GesDocumentNode, GesDocumentNode> _resolved = new();

    private GesSchemaDocumentCheck(GesSchemaParameters? parameters) => _parameters = parameters;

    internal static void Check(string text, GesDocumentNode root, GesResolvedDeclaration schema, GesSchemaParameters? parameters)
    {
        if (parameters is not null) GesSchemaBinder.CheckParameters(schema, parameters);
        try
        {
            new GesSchemaDocumentCheck(parameters).Fields(root, schema, 0);
        }
        catch (Failure failure)
        {
            var (line, column) = GesDocumentSyntax.Location(text, failure.Offset);
            throw new GesSchemaException(failure.Code, "", line, column, $"The document breaks its schema ({failure.Code}) at line {line}, column {column}.");
        }
    }

    // A missing field is reported where the value that lacks it starts.
    private void Fields(GesDocumentNode map, GesResolvedDeclaration schema, int start)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < map.Labels.Length; index++)
        {
            var field = Array.Find(schema.Fields, candidate => candidate.Name == map.Labels[index]);
            if (field is null) throw new Failure(UnknownField, map.KeyOffsets[index]);
            seen.Add(field.Name);
            Union(field, map.Children[index]);
        }

        foreach (var field in schema.Fields)
            if (!seen.Contains(field.Name) && field.Marker != GesSchemaFieldMarker.Optional && !Array.Exists(field.Alternatives, alternative => alternative.Kind == GesSchemaTypeKind.Nothing)) throw new Failure(MissingField, start);
    }

    private void Union(GesResolvedField field, GesDocumentNode value)
    {
        value = Resolved(value);
        if (field.Marker == GesSchemaFieldMarker.Optional && value.Kind == GesDocumentNodeKind.Nothing) return;
        Union(field.Alternatives, value);
    }

    // The first alternative that holds decides; a single type reports its own error.
    private void Union(GesResolvedType[] alternatives, GesDocumentNode value)
    {
        value = Resolved(value);
        if (alternatives.Length == 1)
        {
            Type(alternatives[0], value);
            return;
        }

        foreach (var alternative in alternatives)
        {
            if (!_holds.TryGetValue((alternative, value), out var holds))
            {
                try
                {
                    Type(alternative, value);
                    holds = true;
                }
                catch (Failure)
                {
                    // The next alternative may hold.
                }

                _holds[(alternative, value)] = holds;
            }

            if (holds) return;
        }

        throw new Failure(FieldType, value.Start);
    }

    private void Type(GesResolvedType type, GesDocumentNode value)
    {
        switch (type.Kind)
        {
            case GesSchemaTypeKind.Boolean: Require(value, value.Kind == GesDocumentNodeKind.Boolean); break;
            case GesSchemaTypeKind.Number:
            case GesSchemaTypeKind.Percentage:
            case GesSchemaTypeKind.Quantity: Number(type, value); break;
            case GesSchemaTypeKind.Point: Spatial(type, value, GesDocumentNodeKind.Point); break;
            case GesSchemaTypeKind.Vector: Spatial(type, value, GesDocumentNodeKind.Vector); break;
            case GesSchemaTypeKind.Tag:
                Require(value, value.Kind == GesDocumentNodeKind.Tag && (type.Tags is null || Array.IndexOf(type.Tags, value.Name) >= 0));
                if (type.Max is { } tagMax && Encoding.UTF8.GetByteCount(value.Name) > tagMax) throw new Failure(OutOfRange, value.Start);
                break;
            case GesSchemaTypeKind.Text:
                Require(value, value.Kind == GesDocumentNodeKind.Text);
                if (type.Max is { } textMax && Encoding.UTF8.GetByteCount(value.Name) > textMax) throw new Failure(OutOfRange, value.Start);
                break;
            case GesSchemaTypeKind.Bytes:
                Require(value, value.Kind == GesDocumentNodeKind.Bytes);
                if (type.Max is { } bytesMax && value.Bytes.Length > bytesMax) throw new Failure(OutOfRange, value.Start);
                break;
            case GesSchemaTypeKind.List:
                Require(value, value.Kind == GesDocumentNodeKind.List);
                if (type.Max is { } listMax && value.Children.Length > listMax) throw new Failure(OutOfRange, value.Start);
                if (type.Element is { } element)
                    foreach (var item in value.Children) Union(element, item);
                break;
            case GesSchemaTypeKind.Reference:
                Reference(type, value);
                break;
            case GesSchemaTypeKind.Map: Require(value, value.Kind == GesDocumentNodeKind.Map); break;
            case GesSchemaTypeKind.Range: Require(value, IsForm(value, "range")); break;
            case GesSchemaTypeKind.Series: Require(value, IsForm(value, "series")); break;
            case GesSchemaTypeKind.Dice:
                Require(value, value.Kind == GesDocumentNodeKind.Dice);
                if (type.Max is { } diceMax && value.Rolls.Length > diceMax) throw new Failure(OutOfRange, value.Start);
                foreach (var roll in value.Rolls) Numeric(type.Constraints, true, roll, roll, value);
                break;
            case GesSchemaTypeKind.Message: Require(value, value.Kind == GesDocumentNodeKind.Message || IsForm(value, "message")); break;
            case GesSchemaTypeKind.Handler: Require(value, value.Kind == GesDocumentNodeKind.Handler || IsForm(value, "handler")); break;
            default: Require(value, value.Kind == GesDocumentNodeKind.Nothing); break;
        }
    }

    private void Reference(GesResolvedType type, GesDocumentNode value)
    {
        var fields = value;
        if (IsForm(value, "record"))
        {
            // Labels may come in any order.
            var positions = GesLiteralForms.Positions("record", value.Labels);
            Require(value, positions is not null);
            var name = Resolved(value.Children[Array.IndexOf(positions!, 0)]);
            Require(value, name.Kind == GesDocumentNodeKind.Text && name.Name == type.ReferenceName);
            fields = value.Children[Array.IndexOf(positions!, 1)];
        }

        fields = Resolved(fields);
        Require(value, fields.Kind == GesDocumentNodeKind.Map);
        Fields(fields, type.Reference!, value.Start);
    }

    private void Number(GesResolvedType type, GesDocumentNode value)
    {
        Require(value, value.Kind == GesDocumentNodeKind.Number);
        var number = value.Number;
        var percent = number.Kind == GesNumberLiteralKind.Percentage;
        Require(value, percent == (type.Kind == GesSchemaTypeKind.Percentage) && number.Unit == type.Unit);
        Numeric(type.Constraints, number.Kind == GesNumberLiteralKind.Integer, number.IntegerValue, number.FloatValue, value);
    }

    private void Spatial(GesResolvedType type, GesDocumentNode value, GesDocumentNodeKind kind)
    {
        Require(value, value.Kind == kind && value.Unit == type.Unit);
        if (type.Axes is not { } axes) return;
        foreach (var axis in "xyz")
        {
            var component = axis == 'x' ? value.X : axis == 'y' ? value.Y : value.Z;
            var declared = Array.Find(axes, candidate => candidate.Axis == axis);
            if (declared is null) Require(value, component == 0);
            else Numeric(declared.Constraints, false, 0, component, value);
        }
    }

    private void Numeric(GesResolvedConstraints constraints, bool isInteger, long integer, double number, GesDocumentNode value)
    {
        if (constraints.Integer)
        {
            if (!isInteger && (number != Math.Floor(number) || number >= 9223372036854775808d || number < -9223372036854775808d)) throw new Failure(FieldType, value.Start);
            if (!isInteger) integer = (long)number;
            isInteger = true;
        }

        if (constraints.Wrapping)
        {
            if (!isInteger && (double.IsNaN(number) || double.IsInfinity(number))) throw new Failure(OutOfRange, value.Start);
            return;
        }

        if (Bound(constraints.Lower) is { } lower && Below(isInteger, integer, number, lower)) throw new Failure(OutOfRange, value.Start);
        if (Bound(constraints.Upper) is { } upper && Above(isInteger, integer, number, upper)) throw new Failure(OutOfRange, value.Start);
    }

    private static bool Below(bool isInteger, long integer, double number, GesSchemaDecimal bound)
        => isInteger ? GesSchemaBinder.Compare(new GesSchemaDecimal(integer, 0), bound) < 0 : number < Binary(bound);

    private static bool Above(bool isInteger, long integer, double number, GesSchemaDecimal bound)
        => isInteger ? GesSchemaBinder.Compare(new GesSchemaDecimal(integer, 0), bound) > 0 : number > Binary(bound);

    private static double Binary(GesSchemaDecimal value) => double.Parse(value.Canonical, CultureInfo.InvariantCulture);

    // A bound whose parameter has no value is not checked.
    private GesSchemaDecimal? Bound(GesResolvedValue? value)
    {
        if (value is null) return null;
        if (!value.IsParameter) return value.Value;
        return _parameters is not null && _parameters.TryGet(value.Parameter, out var bound) ? bound.Number : null;
    }

    // A form counts as the value it constructs; these five the check accepts by their type.
    private GesDocumentNode Resolved(GesDocumentNode value)
    {
        if (value.Kind != GesDocumentNodeKind.Form || value.Name is "record" or "range" or "series" or "message" or "handler") return value;
        if (!_resolved.TryGetValue(value, out var resolved)) _resolved[value] = resolved = GesDocumentReading.Resolve(value);
        return resolved;
    }

    private static bool IsForm(GesDocumentNode value, string name) => value.Kind == GesDocumentNodeKind.Form && value.Name == name;

    private static void Require(GesDocumentNode value, bool holds)
    {
        if (!holds) throw new Failure(FieldType, value.Start);
    }
}
