// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;

internal static class MessageApiObservations
{
    internal static void Validate(JsonElement input)
    {
        var allowed = new[] { "signature", "message", "compareSignature", "compareMessage", "compareHandler", "createArguments", "json", "roundTripJson", "includeNormalized" };
        if (input.ValueKind != JsonValueKind.Object || input.EnumerateObject().Any(p => !allowed.Contains(p.Name))) throw new ArgumentException("Invalid message API fields.");
        if (input.TryGetProperty("json", out var json)) RequireText(json);
        if (input.TryGetProperty("roundTripJson", out var roundTrip)) _ = roundTrip.GetBoolean();
        if (input.TryGetProperty("includeNormalized", out var normalized)) _ = normalized.GetBoolean();
        ValidateSignature(input.GetProperty("signature"));
        ValidateMessage(input.GetProperty("message"));
        foreach (var key in new[] { "compareSignature", "compareHandler" })
            if (input.TryGetProperty(key, out var signature)) ValidateSignature(signature);
        if (input.TryGetProperty("compareMessage", out var message))
        {
            ValidateMessage(message);
            if (message.GetProperty("args").ValueKind != JsonValueKind.Array) throw new ArgumentException("Comparison requires ordered arguments.");
        }
        if (input.TryGetProperty("createArguments", out var args))
            foreach (var value in args.EnumerateArray()) _ = ValueApiObservations.Construct(value);
    }

    private static void ValidateSignature(JsonElement input)
    {
        RuntimeValues.Fields(input, "name", "parameters");
        RequireText(input.GetProperty("name"));
        foreach (var name in input.GetProperty("parameters").EnumerateArray()) RequireText(name);
    }

    private static void RequireText(JsonElement input)
    {
        if (input.ValueKind != JsonValueKind.String) throw new ArgumentException("Text required.");
    }

    private static void ValidateMessage(JsonElement input)
    {
        RuntimeValues.Fields(input, "name", "args", "tags");
        RequireText(input.GetProperty("name"));
        foreach (var tag in input.GetProperty("tags").EnumerateArray()) RequireText(tag);
        var args = input.GetProperty("args");
        if (args.ValueKind == JsonValueKind.Object)
        {
            foreach (var item in args.EnumerateObject()) _ = ValueApiObservations.Construct(item.Value);
            return;
        }
        foreach (var arg in args.EnumerateArray())
        {
            RuntimeValues.Fields(arg, "name", "value");
            RequireText(arg.GetProperty("name"));
            _ = ValueApiObservations.Construct(arg.GetProperty("value"));
        }
    }

    private static GameEventScriptMessageSignature Signature(JsonElement input) => GameEventScriptMessageSignature.Create(input.GetProperty("name").GetString()!, input.GetProperty("parameters").EnumerateArray().Select(p => p.GetString()!).ToArray());

    private static GameEventScriptMessage Message(JsonElement input) => GameEventScriptMessage.Create(
        input.GetProperty("name").GetString()!,
        input.GetProperty("args").EnumerateArray()
            .Select(a => new GameEventScriptMessageArgument(a.GetProperty("name").GetString()!, ValueApiObservations.Construct(a.GetProperty("value"))))
            .ToArray(),
        input.GetProperty("tags").EnumerateArray().Select(t => t.GetString()!).ToArray()
    );

    internal static object Run(JsonElement input)
    {
        try { return RunCore(input); }
        catch (GameEventScriptMessageFormatException exception) { return Error(exception.Code); }
    }

    private static object RunCore(JsonElement input)
    {
        if (input.GetProperty("message").GetProperty("args").ValueKind == JsonValueKind.Object) return Error("invalidArgumentsShape");
        GameEventScriptMessageSignature signature;
        GameEventScriptMessage message;
        try { signature = Signature(input.GetProperty("signature")); }
        catch (ArgumentException) { return Error("invalidMessage"); }
        try { message = input.TryGetProperty("json", out var json) ? GameEventScriptMessageJson.Deserialize(json.GetString()!) : Message(input.GetProperty("message")); }
        catch (ArgumentException)
        {
            // Ask the public normalizer for labels; never classify English exception text.
            try
            {
                var names = input.GetProperty("message").GetProperty("args").EnumerateArray().Select(a => GameEventScriptMessageSignature.NormalizeParameterName(a.GetProperty("name").GetString())).Where(n => n != "_").ToArray();
                if (names.Distinct(StringComparer.Ordinal).Count() != names.Length) return Error("duplicateArgumentName");
            }
            catch (ArgumentException) { return Error("invalidArgument"); }
            return Error("invalidMessage");
        }
        if (input.TryGetProperty("roundTripJson", out var roundTrip) && roundTrip.GetBoolean()) message = GameEventScriptMessageJson.Deserialize(GameEventScriptMessageJson.Serialize(message));
        var jsonMode = input.TryGetProperty("json", out _) || input.TryGetProperty("roundTripJson", out _);
        var result = new Dictionary<string, object?>
        {
            ["name"] = message.Name,
            ["signatureId"] = signature.SignatureId,
            ["messageSignatureId"] = message.SignatureId,
            ["matches"] = signature.Matches(message),
            ["argumentCount"] = message.Arguments.Count
        };
        result[jsonMode ? "json" : "normalized"] = jsonMode ? GameEventScriptMessageJson.Serialize(message) : RuntimeValues.EncodeMessage(message);
        if (input.TryGetProperty("includeNormalized", out var normalized) && normalized.GetBoolean()) result["normalized"] = RuntimeValues.EncodeMessage(message);
        if (input.TryGetProperty("compareSignature", out var signatureInput))
        {
            var other = Signature(signatureInput);
            result["signatureEquals"] = signature.Equals(other);
            result["signatureHashEquals"] = signature.GetHashCode() == other.GetHashCode();
        }
        if (input.TryGetProperty("compareMessage", out var messageInput))
        {
            var other = Message(messageInput);
            result["messageEquals"] = message.Equals(other);
            result["messageHashEquals"] = message.GetHashCode() == other.GetHashCode();
        }
        if (input.TryGetProperty("compareHandler", out var handlerInput))
        {
            var left = GesValue.GesHandler(signature);
            var right = GesValue.GesHandler(Signature(handlerInput));
            result["handlerEquals"] = left.Equals(right);
            result["handlerHashEquals"] = left.GetHashCode() == right.GetHashCode();
        }
        if (input.TryGetProperty("createArguments", out var args)) result["createdMessageSignatureId"] = signature.CreateMessage(args.EnumerateArray().Select(v => ValueApiObservations.Construct(v)).ToArray())?.SignatureId ?? "<null>";
        return new { status = "ok", message = result };
    }

    private static object Error(string code) => new { status = "ok", message = new { error = code } };
}
