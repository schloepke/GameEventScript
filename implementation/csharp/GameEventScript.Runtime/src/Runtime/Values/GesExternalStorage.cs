// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;

namespace GameEventScript.Runtime.Values;

internal sealed class GesExternalStorage
{
    private GesValueMap? _map;

    internal GesExternalStorage(IGesExternalValue value)
    {
        Value = value ?? throw new System.ArgumentNullException(nameof(value));
        Definition = value.Definition ?? throw new System.ArgumentException("External value does not provide a type definition.", nameof(value));
    }

    internal IGesExternalValue Value { get; }

    internal GesExternalTypeDefinition Definition { get; }

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
            catch (GesFatalRuntimeException)
            {
                // A deliberate GesExtensionFaultException, or another
                // internal fatal signal, is reported as-is by the caller.
                throw;
            }
            catch (System.Exception exception)
            {
                throw new GesCallbackFailureException(
                    GesDiagnosticCodes.RuntimeExternalFieldAccessFailed,
                    $"External value '{Definition.Name}' field '{field.Name}' failed unexpectedly.",
                    symbol: Definition.Name + "." + field.Name, exception);
            }

            if (!sourceValue.HasValue)
            {
                continue;
            }

            keys[count] = field.Name;
            values[count] = GesExternalTypeValueConverter.CoerceToDeclaredType(sourceValue.Value, field);
            count++;
        }

        _map = new GesValueMap(keys, values, count);
        return _map;
    }
}
