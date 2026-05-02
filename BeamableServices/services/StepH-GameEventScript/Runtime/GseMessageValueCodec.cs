#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using StepH.GameEventScript.Types;

namespace StepH.GameEventScript.Runtime;

internal static class GseMessageValueCodec
{
    public static GseValue CreateHandlerValue(GseMessageSignature signature)
    {
        _ = signature ?? throw new ArgumentNullException(nameof(signature));
        return GseValueFactory.Handler(signature);
    }

    public static GseValue CreateMessageValue(GseMessage message)
    {
        _ = message ?? throw new ArgumentNullException(nameof(message));
        return GseValueFactory.Message(message);
    }

    public static bool TryReadHandlerValue(GseValue value, out GseMessageSignature signature)
    {
        signature = new GseMessageSignature(string.Empty, []);
        if (value is not GseHandlerValue handler)
        {
            return false;
        }

        signature = handler.Signature;
        return true;
    }

    public static bool TryReadMessageValue(GseValue value, out GseMessage message)
    {
        message = new GseMessage(string.Empty);
        if (value is not GseMessageValue typedMessage)
        {
            return false;
        }

        message = typedMessage.Value;
        return true;
    }

    public static bool TryBindHandlerValue(GseValue handlerValue, IReadOnlyDictionary<string, GseValue> arguments, out GseMessage message)
    {
        message = GseMessage.EmptyMessage;
        if (!TryReadHandlerValue(handlerValue, out var handler))
        {
            return false;
        }

        var normalizedArguments = GseNamedArguments.Create(arguments);
        var argumentSignatureId = GseMessageSignature.CreateSignatureId(handler.Name, normalizedArguments.SignatureLabels);
        if (!string.Equals(argumentSignatureId, handler.SignatureId, StringComparison.Ordinal))
        {
            return false;
        }

        message = new GseMessage(handler.Name, normalizedArguments);
        return true;
    }
}
