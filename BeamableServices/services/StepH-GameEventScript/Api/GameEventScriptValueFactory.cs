using System;
using System.Collections.Generic;
using StepH.GameEventScript.Runtime;
using StepH.GameEventScript.VirtualMachine;
using static StepH.GameEventScript.Api.GameEventScriptBytecodeInstructionUnit;

namespace StepH.GameEventScript.Api;

public static class GameEventScriptValueFactory
{
    public static GameEventScriptValue GesNothing()
    {
        var value = new GesVmValue();
        return new GameEventScriptValue(in value);
    }

    public static GameEventScriptValue GesBoolean(bool boolean)
    {
        var value = new GesVmValue();
        value.SetBoolean(boolean);
        return new GameEventScriptValue(in value);
    }

    public static GameEventScriptValue GesInteger(long integer, GameEventScriptBytecodeInstructionUnit unit = UnitNone)
    {
        var value = new GesVmValue();
        value.SetInteger(integer, unit);
        return new GameEventScriptValue(in value);
    }

    public static GameEventScriptValue GesFloat(double number, GameEventScriptBytecodeInstructionUnit unit = UnitNone)
    {
        var value = new GesVmValue();
        value.SetFloat(number, unit);
        return new GameEventScriptValue(in value);
    }

    public static GameEventScriptValue GesNumber(double number, GameEventScriptBytecodeInstructionUnit unit = UnitNone)
        => GesFloat(number, unit);

    public static GameEventScriptValue GesPercentage(double ratio)
    {
        var value = new GesVmValue();
        value.SetPercentage(ratio);
        return new GameEventScriptValue(in value);
    }

    public static GameEventScriptValue GesText(string text)
    {
        var value = new GesVmValue();
        value.SetText(text ?? string.Empty);
        return new GameEventScriptValue(in value);
    }

    public static GameEventScriptValue GesTag(string tag)
    {
        var value = new GesVmValue();
        value.SetTag(tag ?? string.Empty);
        return new GameEventScriptValue(in value);
    }

    public static GameEventScriptValue GesVector(double x, double y = 0d, double z = 0d, GameEventScriptBytecodeInstructionUnit unit = UnitNone)
    {
        var value = new GesVmValue();
        value.SetVector(x, y, z, unit);
        return new GameEventScriptValue(in value);
    }

    public static GameEventScriptValue GesPoint(double x, double y = 0d, double z = 0d, GameEventScriptBytecodeInstructionUnit unit = UnitNone)
    {
        var value = new GesVmValue();
        value.SetPoint(x, y, z, unit);
        return new GameEventScriptValue(in value);
    }

    public static GameEventScriptValue GesList(IEnumerable<GameEventScriptValue>? items)
    {
        var source = items is null ? [] : ToArray(items);
        var list = new GesVmValue[source.Length];
        for (var index = 0; index < source.Length; index++)
        {
            list[index] = source[index].GetVmValue();
        }

        var value = new GesVmValue();
        value.SetList(list);
        return new GameEventScriptValue(in value);
    }

    public static GameEventScriptValue GesMap(IEnumerable<KeyValuePair<string, GameEventScriptValue>>? entries)
    {
        var builder = new GesVmValueMapBuilder();
        if (entries is not null)
        {
            foreach (var entry in entries)
            {
                builder.Set(entry.Key, entry.Value.GetVmValue());
            }
        }

        var value = new GesVmValue();
        value.SetMap(builder.ToMap());
        return new GameEventScriptValue(in value);
    }

    public static GameEventScriptValue GesRecord(string typeName, IEnumerable<KeyValuePair<string, GameEventScriptValue>>? fields)
    {
        var builder = new GesVmValueMapBuilder();
        var marker = new GesVmValue();
        marker.SetTag(typeName ?? string.Empty);
        builder.Set(GesVmValueMap.HiddenRecordTypeField, marker);
        if (fields is not null)
        {
            foreach (var field in fields)
            {
                if (string.Equals(field.Key, GesVmValueMap.HiddenRecordTypeField, StringComparison.Ordinal))
                {
                    continue;
                }

                builder.Set(field.Key, field.Value.GetVmValue());
            }
        }

        var value = new GesVmValue();
        value.SetRecord(builder.ToMap());
        return new GameEventScriptValue(in value);
    }

    public static GameEventScriptValue GesDice(IEnumerable<int>? rolls)
    {
        var values = rolls is null ? [] : ToArray(rolls);
        var value = new GesVmValue();
        value.SetDice(values);
        return new GameEventScriptValue(in value);
    }

    public static GameEventScriptValue GesRange(long from, long to, long step = 1)
    {
        var value = new GesVmValue();
        value.SetRange(from, to, step);
        return new GameEventScriptValue(in value);
    }

    public static GameEventScriptValue GesRange(double from, double to, double step = 1d)
    {
        var value = new GesVmValue();
        value.SetRange(from, to, step);
        return new GameEventScriptValue(in value);
    }

    public static GameEventScriptValue GesMessage(GameEventScriptMessage message)
    {
        var value = new GesVmValue();
        value.SetMessage(message);
        return new GameEventScriptValue(in value);
    }

    public static GameEventScriptValue GesHandler(GameEventScriptMessageSignature signature)
    {
        var value = new GesVmValue();
        value.SetMessageHandler(signature);
        return new GameEventScriptValue(in value);
    }

    internal static GameEventScriptValue GesExternalObject(object instance, GameEventScriptExternalTypeDefinition definition)
    {
        var value = new GesVmValue();
        value.SetExternalCustomType(new GesVmExternalObject(instance, definition));
        return new GameEventScriptValue(in value);
    }

    internal static GameEventScriptValue GesSeries(GesVmSeries series)
    {
        var value = new GesVmValue();
        value.SetSeries(series);
        return new GameEventScriptValue(in value);
    }

    internal static GameEventScriptValue FromVmValue(in GesVmValue value) => new(in value);

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
}
