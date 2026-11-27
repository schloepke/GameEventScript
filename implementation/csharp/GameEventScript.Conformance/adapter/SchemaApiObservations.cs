// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;

// Reads schema sources and binds them, then encodes, decodes and checks documents as a case requests.
internal static class SchemaApiObservations
{
    internal static void Validate(JsonElement input)
    {
        RuntimeValues.Fields(input, "sources", "root", "parameters", "bind", "encode", "encodeExternal", "baselineExternal", "externalFields", "failingField", "baselineFailingField", "failingCode", "encodeBuffer", "baseline", "decode", "decodeLimit", "key", "complete", "dump", "dumpBaseline", "deltaEncode", "deltaDecode", "describe", "document", "readDocument", "network", "handle", "contentHash", "store");
        if (input.GetProperty("encode").ValueKind != JsonValueKind.Null) _ = ValueApiObservations.Construct(input.GetProperty("encode"));
        if (input.GetProperty("baseline").ValueKind != JsonValueKind.Null) _ = ValueApiObservations.Construct(input.GetProperty("baseline"));
        if (input.GetProperty("complete").ValueKind != JsonValueKind.Null) _ = ValueApiObservations.Construct(input.GetProperty("complete"));
        if (input.GetProperty("contentHash").ValueKind != JsonValueKind.Null) _ = ValueApiObservations.Construct(input.GetProperty("contentHash"));
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
                else parameters.With(name, value.AsNumber(), value.ValueUnit);
            }
            var root = input.GetProperty("root").GetString();
            if (root is not null)
            {
                var schema = set.Schema(root);
                result["canonical"] = schema.CanonicalForm;
                result["hash"] = schema.Hash.ToString("x16");
                if (input.GetProperty("describe").GetBoolean()) result["description"] = Describe(schema);
            }

            if (input.GetProperty("network").GetString() is { } networkName)
            {
                var network = set.Network(networkName).Bind(parameters);
                result["network"] = new { canonical = network.CanonicalForm, hash = network.Hash.ToString("x16") };
            }

            if (input.GetProperty("handle").GetString() is { } handleName)
            {
                var handle = set.HandleType(handleName);
                result["handle"] = new { canonical = handle.CanonicalForm, hash = handle.Hash.ToString("x16") };
                if (input.GetProperty("contentHash").ValueKind != JsonValueKind.Null) result["contentHash"] = ContentHash(handle, input.GetProperty("contentHash"));
            }

            if (input.GetProperty("document").GetString() is { } document) result["document"] = Check(set, document, root, parameters);
            if (input.GetProperty("readDocument") is { ValueKind: JsonValueKind.Object } reading) result["readDocument"] = ReadDocument(set, reading);
            if (input.GetProperty("store") is { ValueKind: JsonValueKind.Object } store) result["store"] = Stores(set, store);
            if (input.GetProperty("bind").GetBoolean())
            {
                var bound = set.Schema(root!).Bind(parameters);
                result["bindingHash"] = bound.BindingHash.ToString("x16");
                result["maxByteCount"] = bound.MaxByteCount;
                if (input.GetProperty("encode").ValueKind != JsonValueKind.Null) result["encoded"] = Encode(bound, input);
                if (input.GetProperty("decode").GetString() is { } decode) result["decoded"] = Decode(bound, decode, input);
                if (input.GetProperty("key").GetString() is { } key) result["key"] = GesSchemaValues.ReadKey(bound, Convert.FromHexString(key));
                if (input.GetProperty("complete").ValueKind != JsonValueKind.Null) result["completed"] = Complete(bound, input.GetProperty("complete"));
                if (input.GetProperty("deltaEncode").ValueKind != JsonValueKind.Null) result["deltaEncoded"] = DeltaEncode(bound, input.GetProperty("deltaEncode"));
                if (input.GetProperty("deltaDecode").ValueKind != JsonValueKind.Null) result["deltaDecoded"] = DeltaDecode(bound, input.GetProperty("deltaDecode"));
                if (input.GetProperty("dump").GetString() is { } dump)
                {
                    var read = input.GetProperty("dumpBaseline").GetString() is { } baseline
                        ? GesSchemaDump.Read(bound, Convert.FromHexString(dump), Convert.FromHexString(baseline))
                        : GesSchemaDump.Read(bound, Convert.FromHexString(dump));
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
    private static Dictionary<string, object?> DescribeDefault(GesSchemaDefaultInfo fallback)
    {
        var described = new Dictionary<string, object?> { ["kind"] = fallback.Kind.ToString() };
        if (fallback.Number is { } number) described["value"] = Text(number);
        else if (fallback.Kind == GesSchemaDefaultKind.Boolean) described["value"] = fallback.Boolean;
        else if (fallback.Kind == GesSchemaDefaultKind.Record)
            described["entries"] = fallback.Entries.Select(entry => new Dictionary<string, object?> { ["name"] = entry.Name, ["value"] = DescribeDefault(entry.Value) }).ToArray();
        else if (fallback.Kind != GesSchemaDefaultKind.Empty) described["name"] = fallback.Name;
        return described;
    }

    private static Dictionary<string, object?> Describe(GesSchema schema)
    {
        var result = new Dictionary<string, object?> { ["name"] = schema.Name, ["kind"] = schema.Kind.ToString() };
        if (schema.WireGroup is { } wire) result["wireGroup"] = wire;
        if (schema.ModelGroup is { } model) result["modelGroup"] = model;
        if (schema.IsAbstract) result["abstract"] = true;
        if (schema.PackedWidth > 0)
        {
            result["packedWidth"] = schema.PackedWidth;
            result["packedBits"] = schema.PackedBits;
        }
        if (schema.Takes.Count > 0) result["takes"] = schema.Takes.Select(take =>
        {
            var entry = new Dictionary<string, object?> { ["schema"] = take.Schema.Name };
            if (take.IsAllOptional) entry["allOptional"] = true;
            if (take.Without.Count > 0) entry["without"] = take.Without.ToArray();
            return entry;
        }).ToArray();
        if (schema.Parameters.Count > 0) result["parameters"] = schema.Parameters.Select(parameter => Unit(new Dictionary<string, object?> { ["name"] = parameter.Name, ["kind"] = parameter.Kind.ToString() }, parameter.Unit)).ToArray();
        if (schema.Fields.Count > 0) result["fields"] = schema.Fields.Select(field =>
        {
            var entry = new Dictionary<string, object?> { ["name"] = field.Name, ["alternatives"] = field.Alternatives.Select(Describe).ToArray() };
            if (field.IsUnlabeled) entry["unlabeled"] = true;
            if (field.IsOptional) entry["optional"] = true;
            if (field.IsKey) entry["key"] = true;
            if (field.Default is { } fallback) entry["default"] = DescribeDefault(fallback);

            return entry;
        }).ToArray();
        if (schema.ComputedFields.Count > 0) result["computed"] = schema.ComputedFields.Select(field =>
        {
            var entry = Unit(new Dictionary<string, object?> { ["name"] = field.Name, ["kind"] = field.Kind.ToString() }, field.Unit);
            if (field.IsOptional) entry["optional"] = true;
            entry["expression"] = field.Expression;
            return entry;
        }).ToArray();
        if (schema.Constants.Count > 0) result["constants"] = schema.Constants.Select(constant =>
        {
            var entry = Unit(new Dictionary<string, object?> { ["name"] = constant.Name, ["kind"] = constant.Kind.ToString() }, constant.Unit);
            if (constant.IsInteger) entry["integer"] = true;
            entry["value"] = Text(constant.Value);
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
        if (type.Depth > 0) result["depth"] = type.Depth;
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

    // Reads a document as generated models do: the header names an expected schema, and the entries are checked against it.
    private static object ReadDocument(GesSchemaSet set, JsonElement input)
    {
        RuntimeValues.Fields(input, "text", "expected");
        try
        {
            var document = GesSchemaDocument.Read(set, input.GetProperty("text").GetString()!, input.GetProperty("expected").EnumerateArray().Select(name => name.GetString()!).ToArray());
            return new { schema = document.Schema, value = RuntimeValues.Encode(document.Value) };
        }
        catch (GesDocumentException exception)
        {
            return new { error = new { code = exception.Code, line = exception.Line, column = exception.Column, path = exception.Path } };
        }
    }

    // Reads the stores in order, each with every earlier one as a dependency, from named roots or from root handles: the
    // entries of the last with its sources, the errors of the first that fails, or the code of a resolver failure.
    private static object Stores(GesSchemaSet set, JsonElement input)
    {
        RuntimeValues.Fields(input, "files", "stores", "refs", "latest", "index");
        var table = input.GetProperty("files").EnumerateArray()
            .Select(file => new StoreFile(file.GetProperty("root").GetString()!, file.GetProperty("path").GetString()!, file.GetProperty("text").GetString()!,
                file.GetProperty("version").ValueKind == JsonValueKind.Null ? null : file.GetProperty("version").GetInt64())).ToArray();
        var files = new StoreFiles(table);
        var resolver = new StoreResolver(table, input.GetProperty("refs").EnumerateObject().ToDictionary(entry => entry.Name, entry => entry.Value.GetInt64(), StringComparer.Ordinal),
            input.GetProperty("latest").ValueKind == JsonValueKind.Null ? null : input.GetProperty("latest").GetInt64());
        var read = new List<GesStore>();
        foreach (var store in input.GetProperty("stores").EnumerateArray())
        {
            var type = set.HandleType(store.GetProperty("type").GetString()!);
            try
            {
                if (store.GetProperty("handles").ValueKind == JsonValueKind.Array)
                {
                    var handles = store.GetProperty("handles").EnumerateArray().Select(handle => GesHandle.Parse(handle.GetString()!)).ToArray();
                    read.Add(GesStore.Read(type, handles, resolver, read.ToArray()));
                }
                else
                {
                    var roots = store.GetProperty("roots").EnumerateArray()
                        .Select(root => new GesStoreRoot(root.GetProperty("name").GetString()!, root.GetProperty("path").GetString()!)).ToArray();
                    read.Add(GesStore.Read(type, roots, files, read.ToArray()));
                }
            }
            catch (GesStoreException exception)
            {
                return new { errors = exception.Diagnostics.Select(StoreError).ToArray() };
            }
            catch (GesHandleException exception)
            {
                return new { failure = exception.Code };
            }
        }

        return new
        {
            entries = read[^1].Entries.Select(entry => new
            {
                id = entry.Id,
                handle = entry.Handle.ToString(),
                origin = Location(entry.Origin),
                value = RuntimeValues.Encode(entry.Value)
            }).ToArray(),
            sources = read[^1].Sources.Select(source => source.ToString()).ToArray(),
            indexes = Indexes(read[^1], input)
        };
    }

    // The handles of the last store through each requested projection, by projection name.
    private static Dictionary<string, string[]> Indexes(GesStore store, JsonElement input)
    {
        var result = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (!input.TryGetProperty("index", out var names)) return result;
        foreach (var name in names.EnumerateArray())
        {
            var projection = store.Type.Projections.FirstOrDefault(candidate => candidate.Name == name.GetString())
                ?? throw new ArgumentException($"':{name.GetString()}' is no projection of '@{store.Type.Name}'.");
            result[projection.Name] = store.Index(projection).Select(handle => handle.ToString()).ToArray();
        }

        return result;
    }

    private static Dictionary<string, object?> StoreError(GesStoreDiagnostic diagnostic)
    {
        var error = new Dictionary<string, object?> { ["code"] = diagnostic.Code, ["location"] = Location(diagnostic.Location) };
        if (diagnostic.FieldPath.Length > 0) error["fieldPath"] = diagnostic.FieldPath;
        if (diagnostic.Other is { } other) error["other"] = Location(other);
        if (diagnostic.Suggestion is { } suggestion) error["suggestion"] = suggestion;
        return error;
    }

    private static Dictionary<string, object?> Location(GesSourceLocation location)
    {
        var result = new Dictionary<string, object?> { ["root"] = location.ModuleName, ["path"] = location.SourceName };
        if (location.Line is { } line) result["line"] = line;
        if (location.Column is { } column) result["column"] = column;
        return result;
    }

    private sealed record StoreFile(string Root, string Path, string Text, long? Version);

    private sealed class StoreFiles(StoreFile[] files) : IGesStoreFiles
    {
        public string? Read(string root, string path) => Find(files, root, path)?.Text;

        internal static StoreFile? Find(StoreFile[] files, string root, string path)
        {
            foreach (var file in files)
                if (file.Root == root && file.Path == path) return file;
            return null;
        }
    }

    // Answers a file handle from the table: its root is the root handle's Text id, its path the part. A present file
    // answers with its own version, else the requested one, else the latest; refs name revisions by their text form.
    private sealed class StoreResolver(StoreFile[] files, Dictionary<string, long> refs, long? latest) : IGesHandleResolver
    {
        public void Resolve(GesHandleResolveCall call)
        {
            var handle = call.Handle;
            var root = handle.Id.ValueKind == GesBytecodeTypeKind.Text ? handle.Id.AsText() : handle.Id.AsInteger().ToString(CultureInfo.InvariantCulture);
            if (StoreFiles.Find(files, root, handle.Part.AsText()) is not { } file)
            {
                call.SetMissing();
                return;
            }

            if (file.Version is { } own) call.SetDocument(file.Text, GesValue.GesInteger(own));
            else if (handle.HasVersion) call.SetDocument(file.Text, handle.Version);
            else if (latest is { } number) call.SetDocument(file.Text, GesValue.GesInteger(number));
            else call.SetDocument(file.Text);
        }

        public GesValue ResolveRef(GesHandle handle)
        {
            var name = handle.Ref.ValueKind == GesBytecodeTypeKind.Tag ? "#" + handle.Ref.AsText() : handle.Ref.AsText();
            return refs.TryGetValue(name, out var number) ? GesValue.GesInteger(number) : GesValue.GesNothing();
        }
    }

    private static object Encode(GesBoundSchema bound, JsonElement input)
    {
        var size = input.GetProperty("encodeBuffer").ValueKind == JsonValueKind.Null ? (int)bound.MaxByteCount : input.GetProperty("encodeBuffer").GetInt32();
        var buffer = new byte[size];
        try
        {
            var value = ValueApiObservations.Construct(input.GetProperty("encode"));
            var externalFields = input.GetProperty("externalFields").EnumerateArray().Select(name => name.GetString()!).ToArray();
            if (externalFields.Length > 0)
            {
                value = GesValue.GesRecord(value.CustomTypeName!, value.AsMap()!.Select(entry => (entry.Key, externalFields.Contains(entry.Key) ? GesValue.GesExternal(new ValueApiObservations.ExternalRecord(entry.Value)) : entry.Value)).ToArray());
            }

            if (input.GetProperty("encodeExternal").GetBoolean()) value = GesValue.GesExternal(new ValueApiObservations.ExternalRecord(value, input.GetProperty("failingField").GetString(), input.GetProperty("failingCode").GetString()));
            var length = input.GetProperty("baseline").ValueKind == JsonValueKind.Null
                ? GesSchemaValues.Encode(bound, value, buffer)
                : GesSchemaValues.Encode(bound, value, Baseline(input), buffer);
            return new { bytes = Convert.ToHexString(buffer, 0, length).ToLowerInvariant() };
        }
        catch (GesWireException exception)
        {
            return new { error = new { code = exception.Code, path = exception.Path } };
        }
        catch (GesFatalRuntimeException exception)
        {
            return new { failure = exception.Diagnostic.Code };
        }
    }

    private static GesValue Baseline(JsonElement input)
    {
        var baseline = ValueApiObservations.Construct(input.GetProperty("baseline"));
        return input.GetProperty("baselineExternal").GetBoolean() ? GesValue.GesExternal(new ValueApiObservations.ExternalRecord(baseline, input.GetProperty("baselineFailingField").GetString(), input.GetProperty("failingCode").GetString())) : baseline;
    }

    private static object DeltaEncode(GesBoundSchema bound, JsonElement input)
    {
        var buffer = new byte[bound.MaxByteCount];
        try
        {
            var length = GesSchemaDelta.Encode(bound, Convert.FromHexString(input.GetProperty("record").GetString()!), Convert.FromHexString(input.GetProperty("baseline").GetString()!), buffer);
            return new { bytes = Convert.ToHexString(buffer, 0, length).ToLowerInvariant() };
        }
        catch (GesWireException exception)
        {
            return new { error = new { code = exception.Code, path = exception.Path } };
        }
    }

    private static object DeltaDecode(GesBoundSchema bound, JsonElement input)
    {
        var buffer = new byte[bound.MaxByteCount];
        try
        {
            var decoded = GesSchemaDelta.Decode(bound, Convert.FromHexString(input.GetProperty("delta").GetString()!), Convert.FromHexString(input.GetProperty("baseline").GetString()!), buffer);
            return new { record = Convert.ToHexString(buffer, 0, decoded.ByteCount).ToLowerInvariant(), deltaByteCount = decoded.DeltaByteCount };
        }
        catch (GesWireException exception)
        {
            return new { error = new { code = exception.Code, path = exception.Path } };
        }
    }

    private static object ContentHash(GesHandleType handle, JsonElement input)
    {
        try
        {
            return GesSchemaValues.ContentHash(handle, ValueApiObservations.Construct(input)).ToString();
        }
        catch (GesWireException exception)
        {
            return new { error = new { code = exception.Code, path = exception.Path } };
        }
    }

    // The lenient read of a message, then the strict encoding of the completed message.
    private static object Complete(GesBoundSchema bound, JsonElement input)
    {
        try
        {
            var message = ValueApiObservations.Construct(input).Message ?? throw new InvalidOperationException("complete requires a message");
            var completed = GesSchemaValues.CompleteMessage(bound, message);
            var buffer = new byte[bound.MaxByteCount];
            var length = GesSchemaValues.Encode(bound, GesValue.GesMessage(completed), buffer);
            return new { value = RuntimeValues.Encode(GesValue.GesMessage(completed)), bytes = Convert.ToHexString(buffer, 0, length).ToLowerInvariant() };
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
            var decoded = input.GetProperty("baseline").ValueKind == JsonValueKind.Null
                ? GesSchemaValues.Decode(bound, Convert.FromHexString(hex), limit)
                : GesSchemaValues.Decode(bound, Convert.FromHexString(hex), Baseline(input), limit);
            return new { value = RuntimeValues.Encode(decoded.Value), byteCount = decoded.ByteCount };
        }
        catch (GesWireException exception)
        {
            return new { error = new { code = exception.Code, path = exception.Path } };
        }
        catch (GesFatalRuntimeException exception)
        {
            return new { failure = exception.Diagnostic.Code };
        }
    }
}
