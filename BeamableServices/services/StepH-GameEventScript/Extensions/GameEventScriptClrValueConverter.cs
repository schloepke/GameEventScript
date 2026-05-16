using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using StepH.GameEventScript.Api;
using StepH.GameEventScript.Types;

namespace StepH.GameEventScript.Extensions;

/// <summary>
/// Provides extension methods to convert CLR objects into instances of
/// <see cref="GameEventScriptValue"/> types used by the game event scripting system.
/// </summary>
/// <remarks>
/// This static class includes logic for converting a wide range of .NET CLR types
/// into their corresponding representations in the Game Event Script domain.
/// Supported types include numeric types, string, boolean, collections, dictionaries,
/// and custom GameEventScript-specific types such as <see cref="GameEventScriptDiceValue"/>.
/// </remarks>
public static class GameEventScriptClrValueConverter
{
    /// <summary>
    /// Converts the given object to a <see cref="GameEventScriptValue"/> type.
    /// </summary>
    /// <param name="value">The object to convert. Can be null, primitive types, strings, collections, or custom objects.</param>
    /// <returns>
    /// Returns a corresponding <see cref="GameEventScriptValue"/> instance based on the provided object's type.
    /// If the object is null, it returns the canonical nothing value.
    /// For unsupported types, it attempts to parse the object into a dictionary or other suitable types.
    /// </returns>
    public static GameEventScriptValue ToGameEventScriptValue(this object? value)
    {
        switch (value)
        {
            case null:
                return GameEventScriptValueFactory.GesNothing();
            case GameEventScriptDiceValue dice:
                return GameEventScriptValueFactory.GesDice(dice);
            case IGameEventScriptSeries series:
                return GameEventScriptValueFactory.GesSeries(series);
            case GameEventScriptValue eventScriptValue:
                return eventScriptValue;
            case string text:
                return GameEventScriptValueFactory.GesText(text);
            case bool boolean:
                return GameEventScriptValueFactory.GesBoolean(boolean);
            case byte number:
                return GameEventScriptValueFactory.GesInteger(number);
            case sbyte number:
                return GameEventScriptValueFactory.GesInteger(number);
            case short number:
                return GameEventScriptValueFactory.GesInteger(number);
            case ushort number:
                return GameEventScriptValueFactory.GesInteger(number);
            case int number:
                return GameEventScriptValueFactory.GesInteger(number);
            case uint number:
                return GameEventScriptValueFactory.GesInteger(number);
            case long number:
                return GameEventScriptValueFactory.GesInteger(number);
            case ulong number and <= long.MaxValue:
                return GameEventScriptValueFactory.GesInteger((long)number);
            case ulong number:
                return GameEventScriptValueFactory.GesFloat(number);
            case float number:
                return GameEventScriptValueFactory.GesFloat(number);
            case double number:
                return GameEventScriptValueFactory.GesFloat(number);
        }

        if (TryExtractStringDictionary(value, out var dictionaryEntries))
            return GameEventScriptValueFactory.GesMap(dictionaryEntries.ToDictionary(x => x.Key, x => x.Value.ToGameEventScriptValue(), StringComparer.Ordinal));
        if (value is not (IEnumerable enumerable and not string)) return GameEventScriptValueFactory.GesMap(ExtractObjectMembers(value));
        var list = (from object? item in enumerable select item.ToGameEventScriptValue()).ToList();
        return GameEventScriptValueFactory.GesList(list);
    }

    private static bool TryExtractStringDictionary(object value, out IReadOnlyList<KeyValuePair<string, object?>> entries)
    {
        if (value is IDictionary genericDictionary)
        {
            var list = new List<KeyValuePair<string, object?>>();
            foreach (DictionaryEntry entry in genericDictionary)
            {
                if (entry.Key is not string key) continue;
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
                var found = (bool)tryGetValue.Invoke(value, parameters);
                if (!found) continue;
                result.Add(new KeyValuePair<string, object?>(key, parameters[1]));
            }

            entries = result;
            return true;
        }

        entries = [];
        return false;
    }

    private static IReadOnlyDictionary<string, GameEventScriptValue> ExtractObjectMembers(object value)
    {
        var map = new Dictionary<string, GameEventScriptValue>(StringComparer.Ordinal);
        var type = value.GetType();

        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (property.GetIndexParameters().Length > 0) continue;
            if (!property.CanRead) continue;
            map[property.Name] = property.GetValue(value).ToGameEventScriptValue();
        }

        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
        {
            map[field.Name] = field.GetValue(value).ToGameEventScriptValue();
        }

        return map;
    }
}
