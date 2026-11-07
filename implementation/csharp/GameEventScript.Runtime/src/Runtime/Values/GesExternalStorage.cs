// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using GameEventScript.Api;

namespace GameEventScript.Runtime.Values;

internal sealed class GesExternalStorage
{
    internal GesExternalStorage(IGesExternalValue value)
    {
        Value = value ?? throw new System.ArgumentNullException(nameof(value));
        Definition = value.Definition ?? throw new System.ArgumentException("External value does not provide a type definition.", nameof(value));
    }

    internal IGesExternalValue Value { get; }

    internal GesExternalTypeDefinition Definition { get; }

    internal string CustomTypeName => Definition.Name;

    // Runs one getter; the undeclared name and a getter's absence are both absent. Nothing is kept, so a value can
    // change between handlers.
    internal GesValue? ReadField(string name)
    {
        var fields = Definition.Fields;
        for (var index = 0; index < fields.Count; index++)
        {
            if (string.Equals(fields[index].Name, name, System.StringComparison.Ordinal)) return ReadField(fields[index]);
        }

        return null;
    }

    // Runs every getter on each call, for the same reason, in declaration order, so the first failure is deterministic.
    internal GesValueMap ToMap()
    {
        var fields = Definition.Fields;
        var positions = Definition.CanonicalPositions;
        var values = new GesValue[positions.Length];
        bool[]? absent = null;
        for (var index = 0; index < positions.Length; index++)
        {
            if (ReadField(fields[index]) is { } value) values[positions[index]] = value;
            else (absent ??= new bool[positions.Length])[positions[index]] = true;
        }

        return absent is null ? new GesValueMap(Definition.CanonicalKeys, values) : WithoutAbsent(values, absent);
    }

    private GesValueMap WithoutAbsent(GesValue[] values, bool[] absent)
    {
        var count = 0;
        foreach (var isAbsent in absent)
        {
            if (!isAbsent) count++;
        }

        var presentKeys = new string[count];
        var presentValues = new GesValue[count];
        count = 0;
        for (var index = 0; index < absent.Length; index++)
        {
            if (absent[index]) continue;
            presentKeys[count] = Definition.CanonicalKeys[index];
            presentValues[count++] = values[index];
        }

        return new GesValueMap(presentKeys, presentValues);
    }

    private GesValue? ReadField(GesExternalTypeFieldDefinition field)
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

        return sourceValue.HasValue ? GesExternalTypeValueConverter.CoerceToDeclaredType(sourceValue.Value, field) : null;
    }
}
