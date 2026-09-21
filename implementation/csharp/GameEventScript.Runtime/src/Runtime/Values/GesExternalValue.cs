// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;

namespace GameEventScript.Runtime.Values;

internal sealed class GesExternalValue
{
    private GesValueMap? _map;

    internal GesExternalValue(IGameEventScriptExternalValue value)
    {
        Value = value ?? throw new System.ArgumentNullException(nameof(value));
        Definition = value.Definition ?? throw new System.ArgumentException("External value does not provide a type definition.", nameof(value));
    }

    internal IGameEventScriptExternalValue Value { get; }

    internal GameEventScriptExternalTypeDefinition Definition { get; }

    internal string CustomTypeName => Definition.Name;

    internal GesValueMap ToMap()
    {
        if (_map is not null)
        {
            return _map;
        }

        var keys = new string[Definition.Fields.Count];
        var values = new GesValue[Definition.Fields.Count];
        var count = 0;
        foreach (var field in Definition.Fields)
        {
            GesValue? sourceValue;
            try
            {
                sourceValue = Value.GetField(field.Name);
            }
            catch (GameEventScriptFatalRuntimeException)
            {
                // A deliberate GameEventScriptExtensionFaultException, or another
                // internal fatal signal, is reported as-is by the caller.
                throw;
            }
            catch (System.Exception exception)
            {
                throw new GameEventScriptCallbackFailureException(
                    GameEventScriptDiagnosticCodes.RuntimeExternalFieldAccessFailed,
                    $"External value '{Definition.Name}' field '{field.Name}' failed unexpectedly.",
                    symbol: Definition.Name + "." + field.Name, exception);
            }

            if (!sourceValue.HasValue)
            {
                continue;
            }

            keys[count] = field.Name;
            values[count] = sourceValue.Value;
            count++;
        }

        _map = new GesValueMap(keys, values, count);
        return _map;
    }
}
