// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;
using System.Globalization;
using GameEventScript.Api;
using GameEventScript.Runtime.Values;
using static GameEventScript.Api.GameEventScriptBytecodeTypeKind;
using static GameEventScript.Runtime.GesJson;

namespace GameEventScript.Runtime;

internal sealed class GesMessageJsonCodec
{
    private int _items;
    private void Enter(int depth)
    {
        if (depth > 64 || ++_items > 65536)
            throw Error("message.resourceLimit");
    }

    private static GesValue Text(string text) => GesValue.GesText(text);
    private static GesValue Typed(string type, params (string, GesValue)[] fields)
    {
        var result = new (string, GesValue)[fields.Length + 1];
        result[0] = ("type", Text(type));
        System.Array.Copy(fields, 0, result, 1, fields.Length);
        return Object(result);
    }

    internal static GesValue Envelope(GesValue root, string content)
    {
        Fields(root, "version", content);
        var version = Get(root, "version");
        if (version.Kind != Integer || version.IntegerValue != 1)
            throw Error("message.unsupportedVersion");
        return Get(root, content);
    }

    private static GesValue Get(GesValue value, string key) => value.AsMap()?.Get(key) ?? throw Error();
    private static string String(GesValue value) => value.Kind == GameEventScriptBytecodeTypeKind.Text ? value.TextValue : throw Error();
    private static void Fields(GesValue value, params string[] names)
    {
        if (value.Kind != Map || value.AsMap() is not { } map || map.Length != names.Length)
            throw Error();
        foreach (var name in names)
            if (!map.ContainsKey(name))
                throw Error();
    }

    private static GesValue[] List(GesValue value) => value.Kind == GameEventScriptBytecodeTypeKind.List && value.ObjectValue is GesValue[] items ? items : throw Error();
    private static GameEventScriptBytecodeInstructionUnit Unit(GesValue value) => String(value) switch
    {
        "" => GameEventScriptBytecodeInstructionUnit.UnitNone,
        "s" => GameEventScriptBytecodeInstructionUnit.UnitSecond,
        "m" => GameEventScriptBytecodeInstructionUnit.UnitMeter,
        "°" => GameEventScriptBytecodeInstructionUnit.UnitDegree,
        _ => throw Error()
    };
    private static string UnitName(GameEventScriptBytecodeInstructionUnit unit) => unit switch
    {
        GameEventScriptBytecodeInstructionUnit.UnitNone => "",
        GameEventScriptBytecodeInstructionUnit.UnitSecond => "s",
        GameEventScriptBytecodeInstructionUnit.UnitMeter => "m",
        GameEventScriptBytecodeInstructionUnit.UnitDegree => "°",
        _ => throw Error()
    };
    private static GesValue Number(GesValue value)
    {
        var text = String(value);
        if (text.Contains("%"))
            throw Error();
        var number = TextNumberCast.Read(text, 0, text.Length, percentage: false, allowGrouping: false);
        if (number is not { } result || result.Kind is not (Integer or Float) || result.Unit != 0)
            throw Error();
        return result;
    }

    private static string MessageName(GesValue value)
    {
        var name = String(value);
        if (!GameEventScriptText.IsMessageName(name) && name is not ("initialization" or "undeliverable"))
            throw Error();
        return name;
    }

    private static string NumberText(GesValue value) => value.Kind == Integer ? value.IntegerValue.ToString(CultureInfo.InvariantCulture) : GameEventScriptNumber.FormatCanonicalFloat(value.FloatValue);
    internal GesValue EncodeMessage(GameEventScriptMessage message, int depth)
    {
        Enter(depth);
        MessageName(Text(message.Name));
        var args = new GesValue[message.Arguments.Count];
        for (var i = 0; i < args.Length; i++)
            args[i] = Object(("name", Text(message.Arguments.NameAt(i))), ("value", Encode(message.Arguments[i], depth + 1)));
        var tags = new GesValue[message.Tags.Count];
        for (var i = 0; i < tags.Length; i++)
            tags[i] = Text(message.Tags[i]);
        return Object(("name", Text(message.Name)), ("args", Array(args)), ("tags", Array(tags)));
    }

    internal GameEventScriptMessage DecodeMessage(GesValue value, int depth)
    {
        Enter(depth);
        Fields(value, "name", "args", "tags");
        var args = List(Get(value, "args"));
        var arguments = new GameEventScriptMessageArgument[args.Length];
        for (var i = 0; i < args.Length; i++)
        {
            Fields(args[i], "name", "value");
            var label = String(Get(args[i], "name"));
            if (label != "_" && !GameEventScriptText.IsFieldName(label))
                throw Error();
            arguments[i] = new(label, Decode(Get(args[i], "value"), depth + 1));
        }

        var tags = List(Get(value, "tags"));
        var names = new string[tags.Length];
        for (var i = 0; i < tags.Length; i++)
        {
            names[i] = String(tags[i]);
            if (!GameEventScriptTagRules.IsValidTagName(names[i]))
                throw Error();
        }

        try
        {
            return GameEventScriptMessage.Create(MessageName(Get(value, "name")), arguments, names);
        }
        catch (ArgumentException)
        {
            throw Error();
        }
    }

    internal GesValue Encode(GesValue value, int depth)
    {
        Enter(depth);
        switch (value.Kind)
        {
            case Nothing:
                return Typed("Nothing");
            case Integer:
            case Float:
                return Typed("Number", ("value", Text(NumberText(value))), ("unit", Text(UnitName(value.Unit))));
            case Percentage:
                return Typed("Percentage", ("value", Text(GameEventScriptNumber.FormatCanonicalFloat(value.FloatValue))));
            case GameEventScriptBytecodeTypeKind.Boolean:
                return Typed("Boolean", ("value", value));
            case GameEventScriptBytecodeTypeKind.Text:
                return Typed("Text", ("value", value));
            case Tag:
                return Typed("Tag", ("value", Text(value.TextValue)));
            case GameEventScriptBytecodeTypeKind.List:
                var source = value.AsList();
                var values = new GesValue[source.Length];
                for (var i = 0; i < values.Length; i++)
                    values[i] = Encode(source[i], depth + 1);
                return Typed("List", ("items", Array(values)));
            case Map:
            case Custom:
                var map = value.AsMap() ?? throw Error();
                var entries = new GesValue[map.Length];
                for (var i = 0; i < entries.Length; i++)
                    entries[i] = Object(("key", Text(map.KeyAt(i))), ("value", Encode(map.ValueAt(i), depth + 1)));
                if (value.Kind == Map)
                    return Typed("Map", ("entries", Array(entries)));
                if (value.CustomTypeName is not { } name || !GameEventScriptText.IsTypeName(name))
                    throw Error();
                return Typed("Record", ("name", Text(name)), ("entries", Array(entries)));
            case Vector:
            case Point:
                return Typed(
                    value.Kind == Vector ? "Vector" : "Point",
                    ("x", Text(GameEventScriptNumber.FormatCanonicalFloat(value.X))),
                    ("y", Text(GameEventScriptNumber.FormatCanonicalFloat(value.Y))),
                    ("z", Text(GameEventScriptNumber.FormatCanonicalFloat(value.Z))),
                    ("unit", Text(UnitName(value.Unit)))
                );
            case Dice:
                if (value.ObjectValue is not int[] rolls)
                    throw Error();
                var dice = new GesValue[rolls.Length];
                for (var i = 0; i < rolls.Length; i++)
                {
                    if (rolls[i] <= 0)
                        throw Error();
                    dice[i] = Text(rolls[i].ToString(CultureInfo.InvariantCulture));
                }

                return Typed("Dice", ("rolls", Array(dice)));
            case GameEventScriptBytecodeTypeKind.Range:
                if (value.ObjectValue is GesValueRangeInteger integer)
                    return Typed("Range", ("from", Text(integer.From.ToString(CultureInfo.InvariantCulture))), ("to", Text(integer.To.ToString(CultureInfo.InvariantCulture))), ("step", Text(integer.Step.ToString(CultureInfo.InvariantCulture))));
                if (value.ObjectValue is GesValueRangeFloat real)
                    return Typed(
                        "Range",
                        ("from", Text(GameEventScriptNumber.FormatCanonicalFloat(real.From))),
                        ("to", Text(GameEventScriptNumber.FormatCanonicalFloat(real.To))),
                        ("step", Text(GameEventScriptNumber.FormatCanonicalFloat(real.Step)))
                    );
                throw Error();
            case Series:
                if (value.ObjectValue is not GesSeries series || series.SignatureId is not ("fibonacci" or "factorial"))
                    throw Error("message.unsupportedSeries");
                return Typed("Series", ("kind", Text(series.SignatureId)), ("offset", Text(series.Offset.ToString(CultureInfo.InvariantCulture))));
            case Handler:
                var handler = value.Handler ?? throw Error();
                MessageName(Text(handler.Name));
                var labels = new GesValue[handler.Parameters.Count];
                for (var i = 0; i < labels.Length; i++)
                    labels[i] = Text(handler.Parameters[i]);
                return Typed("Handler", ("name", Text(handler.Name)), ("labels", Array(labels)));
            case Message:
                return Typed("Message", ("value", EncodeMessage(value.Message ?? throw Error(), depth + 1)));
            default:
                throw Error();
        }
    }

    internal GesValue Decode(GesValue value, int depth)
    {
        Enter(depth);
        var type = String(Get(value, "type"));
        switch (type)
        {
            case "Nothing":
                Fields(value, "type");
                return default;
            case "Number":
                Fields(value, "type", "value", "unit");
                var number = Number(Get(value, "value"));
                var unit = Unit(Get(value, "unit"));
                return number.Kind == Integer ? GesValue.GesInteger(number.IntegerValue, unit) : GesValue.GesFloat(number.FloatValue, unit);
            case "Percentage":
                Fields(value, "type", "value");
                var ratio = Number(Get(value, "value")).AsNumeric;
                if (double.IsInfinity(ratio))
                    throw Error();
                return GesValue.GesPercentage(ratio);
            case "Text":
                Fields(value, "type", "value");
                return Text(String(Get(value, "value")));
            case "Tag":
                Fields(value, "type", "value");
                var tag = String(Get(value, "value"));
                if (!GameEventScriptTagRules.IsValidTagName(tag))
                    throw Error();
                return GesValue.GesTag(tag);
            case "Boolean":
                Fields(value, "type", "value");
                var boolean = Get(value, "value");
                if (boolean.Kind != GameEventScriptBytecodeTypeKind.Boolean)
                    throw Error();
                return boolean;
            case "List":
                Fields(value, "type", "items");
                var items = List(Get(value, "items"));
                var values = new GesValue[items.Length];
                for (var i = 0; i < items.Length; i++)
                    values[i] = Decode(items[i], depth + 1);
                return Array(values);
            case "Map":
            case "Record":
                if (type == "Map")
                    Fields(value, "type", "entries");
                else
                    Fields(value, "type", "entries", "name");
                var entries = List(Get(value, "entries"));
                var keys = new string[entries.Length];
                var fields = new GesValue[entries.Length];
                var seen = new HashSet<string>(StringComparer.Ordinal);
                for (var i = 0; i < entries.Length; i++)
                {
                    Fields(entries[i], "key", "value");
                    keys[i] = String(Get(entries[i], "key"));
                    if (!seen.Add(keys[i]))
                        throw Error();
                    fields[i] = Decode(Get(entries[i], "value"), depth + 1);
                }

                var map = new GesValueMap(keys, fields, keys.Length);
                var result = new GesValue();
                if (type == "Map")
                    result.SetMap(map);
                else
                {
                    var name = String(Get(value, "name"));
                    if (!GameEventScriptText.IsTypeName(name))
                        throw Error();
                    result.SetRecord(name, map);
                }

                return result;
            case "Vector":
            case "Point":
                Fields(value, "type", "x", "y", "z", "unit");
                var x = Number(Get(value, "x")).AsNumeric;
                var y = Number(Get(value, "y")).AsNumeric;
                var z = Number(Get(value, "z")).AsNumeric;
                var u = Unit(Get(value, "unit"));
                return type == "Vector" ? GesValue.GesVector(x, y, z, u) : GesValue.GesPoint(x, y, z, u);
            case "Dice":
                Fields(value, "type", "rolls");
                var rolls = List(Get(value, "rolls"));
                var dice = new int[rolls.Length];
                for (var i = 0; i < rolls.Length; i++)
                {
                    var roll = Number(rolls[i]);
                    if (roll.Kind != Integer || roll.IntegerValue <= 0 || roll.IntegerValue > int.MaxValue)
                        throw Error();
                    dice[i] = (int)roll.IntegerValue;
                }

                return GesValue.GesDice(dice);
            case "Range":
                Fields(value, "type", "from", "to", "step");
                var from = Number(Get(value, "from"));
                var to = Number(Get(value, "to"));
                var step = Number(Get(value, "step"));
                if (double.IsInfinity(from.AsNumeric) || double.IsInfinity(to.AsNumeric) || double.IsInfinity(step.AsNumeric))
                    throw Error();
                return from.Kind == Integer && to.Kind == Integer && step.Kind == Integer ? GesValue.GesRange(from.IntegerValue, to.IntegerValue, step.IntegerValue) : GesValue.GesRange(from.AsNumeric, to.AsNumeric, step.AsNumeric);
            case "Series":
                Fields(value, "type", "kind", "offset");
                var kind = String(Get(value, "kind"));
                var offset = Number(Get(value, "offset"));
                if (offset.Kind != Integer || offset.IntegerValue < 0)
                    throw Error();
                var series = kind switch
                {
                    "fibonacci" => GesSeries.Fibonacci(),
                    "factorial" => GesSeries.Factorial(),
                    _ => throw Error("message.unsupportedSeries")
                };
                return GesValue.GesSeries(series.Drop(offset.IntegerValue));
            case "Handler":
                Fields(value, "type", "name", "labels");
                var labels = List(Get(value, "labels"));
                var names = new string[labels.Length];
                for (var i = 0; i < labels.Length; i++)
                {
                    names[i] = String(labels[i]);
                    if (names[i] != "_" && !GameEventScriptText.IsFieldName(names[i]))
                        throw Error();
                }

                try
                {
                    return GesValue.GesHandler(GameEventScriptMessageSignature.Create(MessageName(Get(value, "name")), names));
                }
                catch (ArgumentException)
                {
                    throw Error();
                }

            case "Message":
                Fields(value, "type", "value");
                return GesValue.GesMessage(DecodeMessage(Get(value, "value"), depth + 1));
            default:
                throw Error();
        }
    }
}
