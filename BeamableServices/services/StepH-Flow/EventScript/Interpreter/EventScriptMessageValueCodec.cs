#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using StepH.Flow.EventScript.Types;

namespace StepH.Flow.EventScript.Interpreter;

internal static class EventScriptMessageValueCodec
{
    public static EventScriptValue CreateHandlerValue(EventScriptMessageSignature signature)
    {
        _ = signature ?? throw new ArgumentNullException(nameof(signature));
        return EventScriptValue.Handler(signature);
    }

    public static EventScriptValue CreateMessageValue(EventScriptMessage message)
    {
        _ = message ?? throw new ArgumentNullException(nameof(message));
        return EventScriptValue.Message(message);
    }

    public static bool TryReadHandlerValue(EventScriptValue value, out EventScriptMessageSignature signature)
    {
        signature = new EventScriptMessageSignature(string.Empty, []);
        if (value is not EventScriptHandlerValue handler)
        {
            return false;
        }

        signature = handler.Signature;
        return true;
    }

    public static bool TryReadMessageValue(EventScriptValue value, out EventScriptMessage message)
    {
        message = new EventScriptMessage(string.Empty);
        if (value is not EventScriptMessageValue typedMessage)
        {
            return false;
        }

        message = typedMessage.Value;
        return true;
    }

    public static bool TryBindHandlerValue(EventScriptValue handlerValue, IReadOnlyDictionary<string, EventScriptValue> arguments, out EventScriptMessage message)
    {
        message = EventScriptMessage.EmptyMessage;
        if (!TryReadHandlerValue(handlerValue, out var handler))
        {
            return false;
        }

        var normalizedArguments = EventScriptNamedArguments.Create(arguments);
        var argumentSignatureId = EventScriptMessageSignature.CreateSignatureId(handler.Name, normalizedArguments.Keys);
        if (!string.Equals(argumentSignatureId, handler.SignatureId, StringComparison.Ordinal))
        {
            return false;
        }

        message = new EventScriptMessage(handler.Name, normalizedArguments);
        return true;
    }
}
