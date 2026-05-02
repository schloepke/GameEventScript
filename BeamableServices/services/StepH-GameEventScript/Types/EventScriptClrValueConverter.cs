#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace StepH.GameEventScript.Types;

internal static class EventScriptClrValueConverter
{
    public static EventScriptValue FromClr(object? value)
    {
        if (value is null)
        {
            return EventScriptValueFactory.OptionalNone();
        }

        if (value is EventScriptValue eventScriptValue)
        {
            return eventScriptValue;
        }

        if (value is EventScriptDiceValue dice)
        {
            return EventScriptValueFactory.Dice(dice);
        }

        switch (value)
        {
            case string text:
                return EventScriptValueFactory.Text(text);
            case bool boolean:
                return EventScriptValueFactory.Boolean(boolean);
            case byte number:
                return EventScriptValueFactory.Integer(number);
            case sbyte number:
                return EventScriptValueFactory.Integer(number);
            case short number:
                return EventScriptValueFactory.Integer(number);
            case ushort number:
                return EventScriptValueFactory.Integer(number);
            case int number:
                return EventScriptValueFactory.Integer(number);
            case uint number:
                return EventScriptValueFactory.Integer(number);
            case long number:
                return EventScriptValueFactory.Integer(number);
            case ulong number when number <= long.MaxValue:
                return EventScriptValueFactory.Integer((long)number);
            case ulong number:
                return EventScriptValueFactory.Decimal((decimal)number);
            case float number:
                return EventScriptDecimalValue.EventScriptDecimal(number);
            case double number:
                return EventScriptDecimalValue.EventScriptDecimal(number);
            case decimal number:
                return EventScriptValueFactory.Decimal(number);
        }

        if (TryExtractStringDictionary(value, out var dictionaryEntries))
        {
            return EventScriptValueFactory.Dictionary(dictionaryEntries.ToDictionary(x => x.Key, x => FromClr(x.Value), StringComparer.Ordinal));
        }

        if (value is ISet<EventScriptValue> typedSet)
        {
            return EventScriptValueFactory.Set(typedSet);
        }

        if (value is IEnumerable enumerable and not string)
        {
            var list = new List<EventScriptValue>();
            foreach (var item in enumerable)
            {
                list.Add(FromClr(item));
            }

            return EventScriptValueFactory.List(list);
        }

        return EventScriptValueFactory.Dictionary(ExtractObjectMembers(value));
    }

    public static IReadOnlyList<EventScriptValue> FromClrList(IEnumerable<object?>? values)
        => values?.Select(FromClr).ToArray() ?? Array.Empty<EventScriptValue>();

    private static bool TryExtractStringDictionary(object value, out IReadOnlyList<KeyValuePair<string, object?>> entries)
    {
        if (value is IDictionary genericDictionary)
        {
            var list = new List<KeyValuePair<string, object?>>();
            foreach (DictionaryEntry entry in genericDictionary)
            {
                if (entry.Key is not string key)
                {
                    continue;
                }

                list.Add(new KeyValuePair<string, object?>(key, entry.Value));
            }

            entries = list;
            return true;
        }

        foreach (var interfaceType in value.GetType().GetInterfaces())
        {
            if (!interfaceType.IsGenericType) continue;
            var genericType = interfaceType.GetGenericTypeDefinition();
            if (genericType != typeof(IDictionary<,>) && genericType != typeof(IReadOnlyDictionary<,>)) continue;
            if (interfaceType.GetGenericArguments()[0] != typeof(string)) continue;

            var result = new List<KeyValuePair<string, object?>>();
            var keyProperty = interfaceType.GetProperty("Keys");
            if (keyProperty == null) continue;
            if (keyProperty.GetValue(value) is not IEnumerable keys) continue;

            var tryGetValue = interfaceType.GetMethod("TryGetValue");
            if (tryGetValue == null) continue;

            foreach (var keyObject in keys)
            {
                if (keyObject is not string key)
                {
                    continue;
                }

                var parameters = new object?[] { key, null };
                var found = (bool)tryGetValue.Invoke(value, parameters)!;
                if (!found) continue;
                result.Add(new KeyValuePair<string, object?>(key, parameters[1]));
            }

            entries = result;
            return true;
        }

        entries = Array.Empty<KeyValuePair<string, object?>>();
        return false;
    }

    private static IReadOnlyDictionary<string, EventScriptValue> ExtractObjectMembers(object value)
    {
        var map = new Dictionary<string, EventScriptValue>(StringComparer.Ordinal);
        var type = value.GetType();

        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (property.GetIndexParameters().Length > 0) continue;
            if (!property.CanRead) continue;
            map[property.Name] = FromClr(property.GetValue(value));
        }

        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
        {
            map[field.Name] = FromClr(field.GetValue(value));
        }

        return map;
    }
}
