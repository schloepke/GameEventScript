using StepH.GameEventScript.Api;
using StepH.GameEventScript.Runtime.Values;

namespace StepH.GameEventScript.Runtime;

internal static class GameEventScriptSystemEndpoints
{
    public const string InitializationName = "initialization";
    public const string UndeliverableName = "undeliverable";
    public const string MessageArgumentName = "message";

    public static readonly string InitializationSignatureId = GameEventScriptMessageSignature.CreateSignatureId(InitializationName, []);

    public static readonly string UndeliverableSignatureId = GameEventScriptMessageSignature.CreateSignatureId(UndeliverableName, [MessageArgumentName]);

    public static bool IsInitializationName(string? name)
        => string.Equals(name, InitializationName, System.StringComparison.Ordinal);

    public static bool IsUndeliverableName(string? name)
        => string.Equals(name, UndeliverableName, System.StringComparison.Ordinal);

    public static bool IsSystemEndpointName(string? name)
        => IsInitializationName(name) || IsUndeliverableName(name);

    public static GameEventScriptMessage CreateInitializationMessage()
        => GameEventScriptMessage.Create(InitializationName);

    public static GameEventScriptMessage CreateMessageDispatchMessage(GameEventScriptMessage original)
        => GameEventScriptMessage.Create(
            original.Name,
            [new GameEventScriptMessageArgument(MessageArgumentName, GesValue.GesMessage(original))],
            original.Tags);

}
