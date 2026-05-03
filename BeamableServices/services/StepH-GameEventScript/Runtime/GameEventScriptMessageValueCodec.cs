#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Types;

namespace StepH.GameEventScript.Runtime;

internal static class GameEventScriptMessageValueCodec
{
    public static GameEventScriptValue CreateHandlerValue(GameEventScriptMessageSignature signature)
    {
        _ = signature ?? throw new ArgumentNullException(nameof(signature));
        return GameEventScriptValueFactory.GesHandler(signature);
    }

    public static GameEventScriptValue CreateMessageValue(GameEventScriptMessage message)
    {
        _ = message ?? throw new ArgumentNullException(nameof(message));
        return GameEventScriptValueFactory.GesMessage(message);
    }

    public static bool TryReadHandlerValue(GameEventScriptValue value, out GameEventScriptMessageSignature signature)
    {
        signature = GameEventScriptMessageSignature.Empty;
        if (value is not GameEventScriptHandlerValue handler)
        {
            return false;
        }

        signature = handler.Signature;
        return true;
    }

    public static bool TryReadMessageValue(GameEventScriptValue value, out GameEventScriptMessage message)
    {
        message = GameEventScriptMessage.Empty;
        if (value is not GameEventScriptMessageValue typedMessage)
        {
            return false;
        }

        message = typedMessage.Value;
        return true;
    }

    public static bool TryBindHandlerValue(GameEventScriptValue handlerValue, IReadOnlyDictionary<string, GameEventScriptValue> arguments, out GameEventScriptMessage message)
    {
        message = GameEventScriptMessage.Empty;
        if (!TryReadHandlerValue(handlerValue, out var handler))
        {
            return false;
        }

        var normalizedArguments = GameEventScriptNamedArguments.Create(arguments);
        var argumentSignatureId = GameEventScriptMessageSignature.CreateSignatureId(handler.Name, normalizedArguments.SignatureLabels);
        if (!string.Equals(argumentSignatureId, handler.SignatureId, StringComparison.Ordinal))
        {
            return false;
        }

        message = GameEventScriptMessage.Create(handler.Name, normalizedArguments);
        return true;
    }
}
