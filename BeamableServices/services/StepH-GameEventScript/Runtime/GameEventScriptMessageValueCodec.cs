#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using StepH.GameEventScript.Types;

namespace StepH.GameEventScript.Runtime;

internal static class GameEventScriptMessageValueCodec
{
    public static GameEventScriptValue CreateHandlerValue(GameEventScriptMessageSignature signature)
    {
        _ = signature ?? throw new ArgumentNullException(nameof(signature));
        return GameEventScriptValueFactory.Handler(signature);
    }

    public static GameEventScriptValue CreateMessageValue(GameEventScriptMessage message)
    {
        _ = message ?? throw new ArgumentNullException(nameof(message));
        return GameEventScriptValueFactory.Message(message);
    }

    public static bool TryReadHandlerValue(GameEventScriptValue value, out GameEventScriptMessageSignature signature)
    {
        signature = new GameEventScriptMessageSignature(string.Empty, []);
        if (value is not GameEventScriptHandlerValue handler)
        {
            return false;
        }

        signature = handler.Signature;
        return true;
    }

    public static bool TryReadMessageValue(GameEventScriptValue value, out GameEventScriptMessage message)
    {
        message = new GameEventScriptMessage(string.Empty);
        if (value is not GameEventScriptMessageValue typedMessage)
        {
            return false;
        }

        message = typedMessage.Value;
        return true;
    }

    public static bool TryBindHandlerValue(GameEventScriptValue handlerValue, IReadOnlyDictionary<string, GameEventScriptValue> arguments, out GameEventScriptMessage message)
    {
        message = GameEventScriptMessage.EmptyMessage;
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

        message = new GameEventScriptMessage(handler.Name, normalizedArguments);
        return true;
    }
}
