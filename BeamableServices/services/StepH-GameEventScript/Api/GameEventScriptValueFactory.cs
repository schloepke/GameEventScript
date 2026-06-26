using System;
using System.Collections.Generic;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.Runtime.VM;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeInstructionUnit;
using StepH.GameEventScript.Runtime.Values;

namespace StepH.GameEventScript.Api;

public static class GameEventScriptValueFactory
{
    public static GameEventScriptValue GesNothing()
    {
        var value = new GesValue();
        return new GameEventScriptValue(in value);
    }

    public static GameEventScriptValue GesBoolean(bool boolean)
    {
        var value = new GesValue();
        value.SetBoolean(boolean);
        return new GameEventScriptValue(in value);
    }

    public static GameEventScriptValue GesInteger(long integer, GameEventScriptBytecodeInstructionUnit unit = UnitNone)
    {
        var value = new GesValue();
        value.SetInteger(integer, unit);
        return new GameEventScriptValue(in value);
    }

    public static GameEventScriptValue GesFloat(double number, GameEventScriptBytecodeInstructionUnit unit = UnitNone)
    {
        var value = new GesValue();
        value.SetFloat(number, unit);
        return new GameEventScriptValue(in value);
    }

    public static GameEventScriptValue GesNumber(double number, GameEventScriptBytecodeInstructionUnit unit = UnitNone)
        => GesFloat(number, unit);

    public static GameEventScriptValue GesPercentage(double ratio)
    {
        var value = new GesValue();
        value.SetPercentage(ratio);
        return new GameEventScriptValue(in value);
    }

    public static GameEventScriptValue GesText(string text)
    {
        var value = new GesValue();
        value.SetText(text ?? string.Empty);
        return new GameEventScriptValue(in value);
    }

    public static GameEventScriptValue GesTag(string tag)
    {
        var value = new GesValue();
        value.SetTag(tag ?? string.Empty);
        return new GameEventScriptValue(in value);
    }

    public static GameEventScriptValue GesVector(double x, double y = 0d, double z = 0d, GameEventScriptBytecodeInstructionUnit unit = UnitNone)
    {
        var value = new GesValue();
        value.SetVector(x, y, z, unit);
        return new GameEventScriptValue(in value);
    }

    public static GameEventScriptValue GesPoint(double x, double y = 0d, double z = 0d, GameEventScriptBytecodeInstructionUnit unit = UnitNone)
    {
        var value = new GesValue();
        value.SetPoint(x, y, z, unit);
        return new GameEventScriptValue(in value);
    }

    public static GameEventScriptValue GesList(IEnumerable<GameEventScriptValue>? items)
    {
        var source = items is null ? [] : ToArray(items);
        var list = new GesValue[source.Length];
        for (var index = 0; index < source.Length; index++)
        {
            list[index] = source[index].GetVmValue();
        }

        var value = new GesValue();
        value.SetList(list);
        return new GameEventScriptValue(in value);
    }

    public static GameEventScriptValue GesMap(IEnumerable<KeyValuePair<string, GameEventScriptValue>>? entries)
    {
        var value = new GesValue();
        value.SetMap(CreateMap(entries));
        return new GameEventScriptValue(in value);
    }

    public static GameEventScriptValue GesRecord(string typeName, IEnumerable<KeyValuePair<string, GameEventScriptValue>>? fields)
    {
        var value = new GesValue();
        value.SetRecord(typeName ?? string.Empty, CreateMap(fields));
        return new GameEventScriptValue(in value);
    }

    public static GameEventScriptValue GesDice(IEnumerable<int>? rolls)
    {
        var values = rolls is null ? [] : ToArray(rolls);
        var value = new GesValue();
        value.SetDice(values);
        return new GameEventScriptValue(in value);
    }

    public static GameEventScriptValue GesRange(long from, long to, long step = 1)
    {
        var value = new GesValue();
        value.SetRange(from, to, step);
        return new GameEventScriptValue(in value);
    }

    public static GameEventScriptValue GesRange(double from, double to, double step = 1d)
    {
        var value = new GesValue();
        value.SetRange(from, to, step);
        return new GameEventScriptValue(in value);
    }

    public static GameEventScriptValue GesMessage(GameEventScriptMessage message)
    {
        var value = new GesValue();
        value.SetMessage(message);
        return new GameEventScriptValue(in value);
    }

    public static GameEventScriptValue GesHandler(GameEventScriptMessageSignature signature)
    {
        var value = new GesValue();
        value.SetMessageHandler(signature);
        return new GameEventScriptValue(in value);
    }

    internal static GameEventScriptValue GesExternalObject(object instance, GameEventScriptExternalTypeDefinition definition)
    {
        var value = new GesValue();
        value.SetExternalCustomType(new GesExternalObject(instance, definition));
        return new GameEventScriptValue(in value);
    }

    internal static GameEventScriptValue GesSeries(GesSeries series)
    {
        var value = new GesValue();
        value.SetSeries(series);
        return new GameEventScriptValue(in value);
    }

    internal static GameEventScriptValue FromVmValue(in GesValue value) => new(in value);

    private static GameEventScriptValue[] ToArray(IEnumerable<GameEventScriptValue> values)
    {
        switch (values)
        {
            case GameEventScriptValue[] array:
                return array;
            case ICollection<GameEventScriptValue> collection:
            {
                var result = new GameEventScriptValue[collection.Count];
                collection.CopyTo(result, 0);
                return result;
            }
        }

        var list = new List<GameEventScriptValue>();
        foreach (var value in values) list.Add(value);
        return list.ToArray();
    }

    private static int[] ToArray(IEnumerable<int> values)
    {
        switch (values)
        {
            case int[] array:
            {
                var copy = new int[array.Length];
                Array.Copy(array, copy, array.Length);
                return copy;
            }
            case ICollection<int> collection:
            {
                var result = new int[collection.Count];
                collection.CopyTo(result, 0);
                return result;
            }
        }

        var list = new List<int>();
        foreach (var value in values) list.Add(value);
        return list.ToArray();
    }

    private static GesValueMap CreateMap(IEnumerable<KeyValuePair<string, GameEventScriptValue>>? entries)
    {
        if (entries is null) return new GesValueMap([], [], 0);

        var keys = new string[4];
        var values = new GesValue[4];
        var count = 0;
        foreach (var entry in entries)
        {
            var existingIndex = -1;
            for (var i = 0; i < count; i++)
            {
                if (!string.Equals(keys[i], entry.Key, StringComparison.Ordinal)) continue;
                existingIndex = i;
                break;
            }

            if (existingIndex >= 0)
            {
                values[existingIndex] = entry.Value.GetVmValue();
                continue;
            }

            if (count == keys.Length)
            {
                var nextKeys = new string[keys.Length << 1];
                var nextValues = new GesValue[values.Length << 1];
                Array.Copy(keys, nextKeys, keys.Length);
                Array.Copy(values, nextValues, values.Length);
                keys = nextKeys;
                values = nextValues;
            }

            keys[count] = entry.Key;
            values[count] = entry.Value.GetVmValue();
            count++;
        }

        return new GesValueMap(keys, values, count);
    }
}
