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

    public static GameEventScriptMessage CreateUndeliverableMessage(GameEventScriptMessage original)
    {
        var envelope = GesCustomType(EnvelopeTypeName, new Dictionary<string, GameEventScriptValue>
        {
            ["message"] = GesMessage(original),
            ["tags"] = GesList(original.Tags.Select(GesTag))
        });

        return GameEventScriptMessage.Create(
            UndeliverableName,
            new Dictionary<string, GameEventScriptValue>
            {
                [EnvelopeArgumentName] = envelope
            },
            original.Tags);
    }
}
