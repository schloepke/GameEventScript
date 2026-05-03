using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using StepH.GameEventScript.Linker;
using StepH.GameEventScript.Parser;

namespace StepH_GameEventScript_Tests.Support;

internal static class GameEventScriptAstJsonSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    internal static string ToJson(this GseModule module, JsonSerializerOptions? options = null)
        => JsonSerializer.Serialize(module, options ?? SerializerOptions);

    internal static string ToJson(this LinkedGseModule module, JsonSerializerOptions? options = null)
        => JsonSerializer.Serialize(module, options ?? SerializerOptions);
}
