using System.Collections.Generic;
using System.Linq;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Types;
using static StepH.GameEventScript.Api.GameEventScriptValueFactory;

namespace StepH.GameEventScript.Runtime;

internal static class GameEventScriptSystemEndpoints
{
    public const string UndeliverableName = "undeliverable";
    public const string EnvelopeArgumentName = "envelope";
    public const string EnvelopeTypeName = "envelope";

    public static readonly string UndeliverableSignatureId =
        GameEventScriptMessageSignature.CreateSignatureId(UndeliverableName, [EnvelopeArgumentName]);

    public static bool IsUndeliverableName(string? name)
        => string.Equals(name, UndeliverableName, System.StringComparison.Ordinal);

    public static GameEventScriptValue CreateEnvelopeValue(GameEventScriptMessage original)
        => GesCustomType(EnvelopeTypeName, new Dictionary<string, GameEventScriptValue>
        {
            ["message"] = GesMessage(original),
            ["tags"] = GesList(original.Tags.Select(GesTag))
        });

    public static GameEventScriptMessage CreateEnvelopeDispatchMessage(GameEventScriptMessage original)
        => GameEventScriptMessage.Create(
            original.Name,
            new Dictionary<string, GameEventScriptValue>
            {
                [EnvelopeArgumentName] = CreateEnvelopeValue(original)
            },
            original.Tags);

    public static GameEventScriptMessage CreateUndeliverableMessage(GameEventScriptMessage original)
    {
        return GameEventScriptMessage.Create(
            UndeliverableName,
            new Dictionary<string, GameEventScriptValue>
            {
                [EnvelopeArgumentName] = CreateEnvelopeValue(original)
            },
            original.Tags);
    }
}
