// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using GameEventScript.Api;

// Reads schema sources and binds them, then encodes, decodes and checks documents as a case requests.
internal static class SchemaApiObservations
{
    internal static void Validate(JsonElement input)
    {
        RuntimeValues.Fields(input, "sources", "root", "parameters", "bind", "encode", "encodeBuffer", "decode", "decodeLimit", "key", "dump", "describe", "document");
        if (input.GetProperty("encode").ValueKind != JsonValueKind.Null) _ = ValueApiObservations.Construct(input.GetProperty("encode"));
    }

    internal static object Run(JsonElement input)
    {
        var result = new Dictionary<string, object?>();
        try
        {
            var sources = input.GetProperty("sources").EnumerateArray().Select((text, index) => new GesSchemaSource($"schema{index}.gess", text.GetString()!)).ToArray();
            var set = GesSchemaSet.Parse(sources);
            var parameters = new GesSchemaParameters();
            foreach (var parameter in input.GetProperty("parameters").EnumerateArray())
            {
                var name = parameter.GetProperty("name").GetString()!;
                if (parameter.TryGetProperty("literal", out var literal))
                {
                    parameters.With(name, literal.GetString()!);
                    continue;
                }

                var value = RuntimeValues.Decode(parameter.GetProperty("value"));
                if (value.ValueKind == GesBytecodeTypeKind.Integer) parameters.With(name, value.AsInteger(), value.ValueUnit);
                else parameters.With(name, value.AsNumeric, value.ValueUnit);
            }
            var root = input.GetProperty("root").GetString();
            if (root is not null)
            {
                var schema = set.Schema(root);
                result["canonical"] = schema.CanonicalForm;
                result["hash"] = schema.Hash.ToString("x16");
                if (input.GetProperty("describe").GetBoolean()) result["description"] = Describe(schema);
            }

            if (input.GetProperty("document").GetString() is { } document) result["document"] = Check(set, document, root, parameters);
            if (input.GetProperty("bind").GetBoolean())
            {
                var bound = set.Schema(root!).Bind(parameters);
                result["bindingHash"] = bound.BindingHash.ToString("x16");
                result["maxByteCount"] = bound.MaxByteCount;
                if (input.GetProperty("encode").ValueKind != JsonValueKind.Null) result["encoded"] = Encode(bound, input);
                if (input.GetProperty("decode").GetString() is { } decode) result["decoded"] = Decode(bound, decode, input);
                if (input.GetProperty("key").GetString() is { } key) result["key"] = GesSchemaValues.ReadKey(bound, Convert.FromHexString(key));
                if (input.GetProperty("dump").GetString() is { } dump)
                {
                    var read = GesSchemaDump.Read(bound, Convert.FromHexString(dump));
                    result["dump"] = new { text = read.Text, valid = read.IsValid };
                }
            }
        }
        catch (GesSchemaException exception)
        {
            return new { status = "ok", schema = new { error = new { code = exception.Code, line = exception.Line, column = exception.Column } } };
        }

        return new { status = "ok", schema = result };
    }

    // Only what differs from the default, so that expectations stay readable.
    private static Dictionary<string, object?> Describe(GesSchema schema)
    {
        var result = new Dictionary<string, object?> { ["name"] = schema.Name, ["kind"] = schema.Kind.ToString() };
        if (schema.Parameters.Count > 0) result["parameters"] = schema.Parameters.Select(parameter => Unit(new Dictionary<string, object?> { ["name"] = parameter.Name, ["kind"] = parameter.Kind.ToString() }, parameter.Unit)).ToArray();
        if (schema.Fields.Count > 0) result["fields"] = schema.Fields.Select(field =>
        {
            var entry = new Dictionary<string, object?> { ["name"] = field.Name, ["alternatives"] = field.Alternatives.Select(Describe).ToArray() };
            if (field.IsUnlabeled) entry["unlabeled"] = true;
            if (field.IsOptional) entry["optional"] = true;
            if (field.IsKey) entry["key"] = true;
            return entry;
        }).ToArray();
        if (schema.Tags.Count > 0) result["tags"] = schema.Tags.ToArray();
        if (schema.Members.Count > 0) result["members"] = schema.Members.Select(member => member.Name).ToArray();
        return result;
    }

    private static Dictionary<string, object?> Describe(GesSchemaType type)
    {
        var result = Unit(new Dictionary<string, object?> { ["kind"] = type.Kind.ToString() }, type.Unit);
        Limits(result, type.IsInteger, type.IsWrapping, type.Lower, type.Upper, type.Step, type.Delta);
        if (type.Tags.Count > 0) result["tags"] = type.Tags.ToArray();
        if (type.Max is { } max) result["max"] = max;
        if (type.Element.Count > 0) result["element"] = type.Element.Select(Describe).ToArray();
        if (type.Axes.Count > 0) result["axes"] = type.Axes.Select(axis =>
        {
            var entry = new Dictionary<string, object?> { ["name"] = axis.Name };
            Limits(entry, axis.IsInteger, axis.IsWrapping, axis.Lower, axis.Upper, axis.Step, axis.Delta);
            return entry;
        }).ToArray();
        if (type.Schema is { } schema) result["schema"] = schema.Name;
        return result;
    }

    private static Dictionary<string, object?> Unit(Dictionary<string, object?> result, GesQuantityUnit unit)
    {
        if (unit != GesQuantityUnit.UnitNone) result["unit"] = unit.ToTypeName();
        return result;
    }

    private static void Limits(Dictionary<string, object?> result, bool integer, bool wrapping, GesSchemaBound? lower, GesSchemaBound? upper, GesSchemaBound? step, GesSchemaBound? delta)
    {
        if (integer) result["integer"] = true;
        if (wrapping) result["wrapping"] = true;
        if (lower is { } first) result["lower"] = Text(first);
        if (upper is { } last) result["upper"] = Text(last);
        if (step is { } size) result["step"] = Text(size);
        if (delta is { } range) result["delta"] = Text(range);
    }

    private static string Text(GesSchemaBound bound) => bound.IsParameter ? "parameter " + bound.Parameter : $"{bound.Significand}e{bound.Exponent}";

    private static object Check(GesSchemaSet set, string document, string? root, GesSchemaParameters parameters)
    {
        try
        {
            if (root is null) set.CheckDocument(document, parameters);
            else set.Schema(root).CheckDocument(document, parameters);
            return new { ok = true };
        }
        catch (GesSchemaException exception)
        {
            return new { error = new { code = exception.Code, line = exception.Line, column = exception.Column } };
        }
    }

    private static object Encode(GesBoundSchema bound, JsonElement input)
    {
        var size = input.GetProperty("encodeBuffer").ValueKind == JsonValueKind.Null ? (int)bound.MaxByteCount : input.GetProperty("encodeBuffer").GetInt32();
        var buffer = new byte[size];
        try
        {
            var length = GesSchemaValues.Encode(bound, ValueApiObservations.Construct(input.GetProperty("encode")), buffer);
            return new { bytes = Convert.ToHexString(buffer, 0, length).ToLowerInvariant() };
        }
        catch (GesWireException exception)
        {
            return new { error = new { code = exception.Code, path = exception.Path } };
        }
    }

    private static object Decode(GesBoundSchema bound, string hex, JsonElement input)
    {
        var limit = input.GetProperty("decodeLimit").ValueKind == JsonValueKind.Null ? long.MaxValue : input.GetProperty("decodeLimit").GetInt64();
        try
        {
            var decoded = GesSchemaValues.Decode(bound, Convert.FromHexString(hex), limit);
            return new { value = RuntimeValues.Encode(decoded.Value), byteCount = decoded.ByteCount };
        }
        catch (GesWireException exception)
        {
            return new { error = new { code = exception.Code, path = exception.Path } };
        }
    }
}
