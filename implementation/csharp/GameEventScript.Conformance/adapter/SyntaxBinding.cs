// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using GameEventScript.Api;
using GameEventScript.Compiler;

// Binds the syntax tree of every valid source and compares the result with the parser's semantic tree, including
// source ranges and nesting depths. Each source reports `equal`, `invalid` for sources with syntax errors, or its first
// difference.
internal static class SyntaxBinding
{
    internal static object Run(JsonElement cases)
    {
        var sources = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var test in cases.EnumerateArray())
        {
            var index = 0;
            foreach (var source in test.GetProperty("sources").EnumerateArray())
            {
                var id = test.GetProperty("id").GetString()! + "#" + index;
                var text = source.GetProperty("text").GetString()!;
                try
                {
                    var (expected, tree) = GesParser.ParseWithTree(text, "source.ges", (uint)index);
                    try
                    {
                        sources[id] = FirstDifference(Dump(expected), Dump(GesSyntaxBinder.Bind(tree, "source.ges", (uint)index))) ?? "equal";
                    }
                    catch (Exception exception) when (exception is InvalidOperationException or NullReferenceException or ArgumentException or IndexOutOfRangeException)
                    {
                        sources[id] = "binder failed: " + exception.Message;
                    }
                }
                catch (GesCompileException)
                {
                    sources[id] = "invalid";
                }

                index++;
            }
        }

        return sources;
    }

    private static string? FirstDifference(List<string> expected, List<string> actual)
    {
        for (var index = 0; index < Math.Max(expected.Count, actual.Count); index++)
        {
            var left = index < expected.Count ? expected[index] : "<end>";
            var right = index < actual.Count ? actual[index] : "<end>";
            if (left != right) return $"parser {left} | binder {right}";
        }

        return null;
    }

    private static List<string> Dump(object root)
    {
        var lines = new List<string>();
        Dump(root, "script", lines);
        return lines;
    }

    private static void Dump(object? value, string path, List<string> lines)
    {
        switch (value)
        {
            case null: lines.Add(path + " = null"); return;
            case string text: lines.Add(path + " = " + JsonSerializer.Serialize(text)); return;
            case double number: lines.Add(path + " = " + number.ToString("R", CultureInfo.InvariantCulture)); return;
            case Enum or bool or int or long or uint: lines.Add(path + " = " + Convert.ToString(value, CultureInfo.InvariantCulture)); return;
            case IEnumerable items:
                var count = 0;
                foreach (var item in items) Dump(item, path + "[" + count++ + "]", lines);
                lines.Add(path + ".count = " + count);
                return;
        }

        var type = value.GetType();
        lines.Add(path + " : " + type.Name);
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance).OrderBy(property => property.Name, StringComparer.Ordinal))
        {
            // Derived properties without a setter repeat stored children; dumping them would grow exponentially with nesting.
            if (property.GetIndexParameters().Length != 0 || property.SetMethod is null) continue;
            Dump(property.GetValue(value), path + "." + property.Name, lines);
        }
    }
}
