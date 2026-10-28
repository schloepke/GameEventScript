// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using GameEventScript.Api;

// Reads documents through GesDocument.Parse or builds them from a value, then optionally prints and rereads them.
internal static class DocumentApiObservations
{
    internal static void Validate(JsonElement input)
    {
        if (input.TryGetProperty("text", out var text))
        {
            RuntimeValues.Fields(input, "text", "print");
            _ = text.GetString() ?? throw new ArgumentException("Document text required.");
        }
        else
        {
            RuntimeValues.Fields(input, "value", "schema", "print");
            _ = ValueApiObservations.Construct(input.GetProperty("value"));
            if (input.GetProperty("schema").ValueKind != JsonValueKind.Null) _ = Schema(input.GetProperty("schema"));
            if (input.GetProperty("print").ValueKind == JsonValueKind.Null) throw new ArgumentException("A built document is printed.");
        }
        if (input.GetProperty("print").ValueKind != JsonValueKind.Null) _ = Format(input.GetProperty("print"));
    }

    internal static object Run(JsonElement input)
    {
        var result = new Dictionary<string, object?>();
        GesDocument? document;
        if (input.TryGetProperty("text", out var text))
        {
            document = Read(text.GetString()!, out var read);
            result["read"] = read;
        }
        else
        {
            var schema = input.GetProperty("schema").ValueKind == JsonValueKind.Null ? null : Schema(input.GetProperty("schema"));
            try
            {
                document = new GesDocument(ValueApiObservations.Construct(input.GetProperty("value")), schema);
            }
            catch (ArgumentException)
            {
                return new { status = "ok", document = new { rejected = true } };
            }
        }
        if (document is not null && input.GetProperty("print").ValueKind != JsonValueKind.Null)
        {
            var printed = document.Print(Format(input.GetProperty("print")));
            result["printed"] = printed;
            _ = Read(printed, out var reread);
            result["reread"] = reread;
        }
        return new { status = "ok", document = result };
    }

    private static GesDocument? Read(string text, out object observation)
    {
        try
        {
            var document = GesDocument.Parse(text);
            observation = new { value = RuntimeValues.Encode(document.Value), schema = Encode(document.Schema) };
            return document;
        }
        catch (GesDocumentException exception)
        {
            var code = exception.Code.Substring("document.".Length);
            observation = exception.LimitName is null
                ? new { error = (object)new { code, line = exception.Line, column = exception.Column } }
                : new { error = (object)new { code, line = exception.Line, column = exception.Column, limitName = exception.LimitName, limit = exception.Limit } };
            return null;
        }
    }

    private static object? Encode(GesDocumentSchema? schema)
        => schema is null ? null : new { kind = schema.Kind == GesDocumentSchemaKind.Type ? "type" : "file", name = schema.Name };

    private static GesDocumentSchema Schema(JsonElement input)
    {
        RuntimeValues.Fields(input, "kind", "name");
        var kind = input.GetProperty("kind").GetString() switch
        {
            "type" => GesDocumentSchemaKind.Type,
            "file" => GesDocumentSchemaKind.File,
            _ => throw new ArgumentException("Invalid schema kind.")
        };
        return new GesDocumentSchema(kind, input.GetProperty("name").GetString()!);
    }

    private static GesDocumentFormat Format(JsonElement input)
    {
        if (input.ValueKind != JsonValueKind.Object || input.EnumerateObject().Any(p => p.Name is not ("indentation" or "lineWidth" or "prefixedUnits")))
            throw new ArgumentException("Invalid document print format.");
        var format = new GesDocumentFormat();
        if (input.TryGetProperty("indentation", out var indentation)) format = format with { Indentation = indentation.GetInt32() };
        if (input.TryGetProperty("lineWidth", out var width)) format = format with { LineWidth = width.GetInt32() };
        if (input.TryGetProperty("prefixedUnits", out var prefixed)) format = format with { PrefixedUnits = prefixed.GetBoolean() };
        return format;
    }
}
