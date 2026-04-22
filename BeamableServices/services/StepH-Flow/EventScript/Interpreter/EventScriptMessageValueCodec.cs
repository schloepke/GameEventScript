#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Linq;
using StepH.Flow.EventScript.Types;

namespace StepH.Flow.EventScript.Interpreter;

internal static class EventScriptMessageValueCodec
{
    private const string MessageTypeName = "message";
    private const string HandlerTypeName = "handler";
    private const string NameKey = "name";
    private const string ParametersKey = "parameters";
    private const string ArgumentsKey = "arguments";
    private const string SignatureIdKey = "signatureid";

    public static EventScriptValue CreateHandlerValue(EventScriptMessageSignature signature)
    {
        _ = signature ?? throw new ArgumentNullException(nameof(signature));
        var values = new Dictionary<string, EventScriptValue>(StringComparer.Ordinal)
        {
            [NameKey] = EventScriptValue.Text(signature.Name),
            [ParametersKey] = EventScriptValue.List(signature.Parameters.Select(parameter => EventScriptValue.Text(parameter))),
            [SignatureIdKey] = EventScriptValue.Text(signature.SignatureId)
        };

        return EventScriptValue.CustomType(HandlerTypeName, values);
    }

    public static EventScriptValue CreateMessageValue(EventScriptMessage message)
    {
        _ = message ?? throw new ArgumentNullException(nameof(message));
        var values = new Dictionary<string, EventScriptValue>(StringComparer.Ordinal)
        {
            [NameKey] = EventScriptValue.Text(message.Name),
            [ArgumentsKey] = EventScriptValue.Dictionary(message.Arguments.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)),
            [SignatureIdKey] = EventScriptValue.Text(message.SignatureId)
        };

        return EventScriptValue.CustomType(MessageTypeName, values);
    }

    public static bool TryReadHandlerValue(EventScriptValue value, out EventScriptMessageSignature signature)
    {
        signature = new EventScriptMessageSignature(string.Empty, []);
        if (!value.TryGetCustomTypeName(out var typeName) ||
            !string.Equals(typeName, HandlerTypeName, StringComparison.Ordinal))
        {
            return false;
        }

        var map = value.AsDictionary();
        if (!map.TryGetValue(NameKey, out var nameValue))
        {
            return false;
        }

        var name = nameValue.AsText();
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        IReadOnlyList<string> parameters = [];
        if (map.TryGetValue(ParametersKey, out var parametersValue))
        {
            parameters = parametersValue.AsList()
                .Select(parameter => parameter.AsText())
                .Where(parameter => !string.IsNullOrWhiteSpace(parameter))
                .ToArray();
        }

        signature = new EventScriptMessageSignature(name, parameters);
        return true;
    }

    public static bool TryReadMessageValue(EventScriptValue value, out EventScriptMessage message)
    {
        message = new EventScriptMessage(string.Empty);
        if (!value.TryGetCustomTypeName(out var typeName) ||
            !string.Equals(typeName, MessageTypeName, StringComparison.Ordinal))
        {
            return false;
        }

        var map = value.AsDictionary();
        if (!map.TryGetValue(NameKey, out var nameValue))
        {
            return false;
        }

        var name = nameValue.AsText();
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var arguments = map.TryGetValue(ArgumentsKey, out var argumentsValue)
            ? argumentsValue.AsDictionary().ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)
            : new Dictionary<string, EventScriptValue>(StringComparer.Ordinal);
        message = new EventScriptMessage(name, arguments);
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
