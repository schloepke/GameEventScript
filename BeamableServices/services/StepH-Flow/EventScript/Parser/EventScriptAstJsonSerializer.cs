#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StepH.Flow.EventScript.Parser;

public static class EventScriptAstJsonSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string ToJson(this EventScriptModule module, JsonSerializerOptions? options = null)
        => JsonSerializer.Serialize(module, options ?? SerializerOptions);
}
