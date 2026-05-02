#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using StepH.GameEventScript.Linker;

namespace StepH.GameEventScript.Parser;

public static class GseAstJsonSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string ToJson(this GseModule module, JsonSerializerOptions? options = null)
        => JsonSerializer.Serialize(module, options ?? SerializerOptions);
    
    public static string ToJson(this LinkedGseModule module, JsonSerializerOptions? options = null)
        => JsonSerializer.Serialize(module, options ?? SerializerOptions);

}
