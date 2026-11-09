// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Text;
using GameEventScript.Api;

namespace GameEventScript.Generator;

// What every emitter generates, independent of the language: per type its members in wire order with their node offsets,
// the field paths in node order for errors, and the nested types it declares. Names are bases in PascalCase that each
// emitter spells for its language.
internal sealed class GesPlanType
{
    internal GesLayoutType Layout = new();
    internal string Name = "";
    internal List<string> Paths = [];
    internal List<GesPlanField> Fields = [];
}

internal sealed class GesPlanField
{
    internal GesLayoutField Layout = new();
    internal string Base = "";
    internal bool Optional;
    internal bool Key;
    internal GesPlanValue Value = new();
}

// One encoding at a node, stored in the member named by Base. A union holds its alternatives, a Point or Vector its axes.
internal sealed class GesPlanValue
{
    internal string Base = "";
    internal string Label = "";
    internal int Node;
    internal GesLayoutEncoding Encoding = new();
    internal List<GesPlanValue> Axes = [];
    internal List<GesPlanValue> Alternatives = [];
}

internal static class GesSchemaPlanner
{
    internal static List<GesPlanType> Plan(GesSchemaLayout layout)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var paths = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var result = new List<GesPlanType>();
        foreach (var type in layout.Types)
        {
            if (type.Kind != GesSchemaKind.Schema)
                throw new GesGeneratorException(GesGeneratorException.Unsupported, $"The {(type.Kind == GesSchemaKind.Message ? "message" : "message set")} '{type.Name}' cannot be generated yet.");
            result.Add(Type(layout, type, counts, paths));
        }

        return result;
    }

    private static GesPlanType Type(GesSchemaLayout layout, GesLayoutType type, Dictionary<string, int> counts, Dictionary<string, List<string>> paths)
    {
        var plan = new GesPlanType { Layout = type, Name = type.Name, Paths = Paths(layout, type, paths) };
        var node = 0;
        foreach (var field in type.Fields)
        {
            var planned = new GesPlanField { Layout = field, Base = Pascal(field.Name), Optional = field.Optional, Key = field.Key };
            planned.Value = Value(layout, type, field.Encoding, planned.Base, "", node, counts);
            node += Count(layout, field.Encoding, counts);
            plan.Fields.Add(planned);
        }

        return plan;
    }

    private static GesPlanValue Value(GesSchemaLayout layout, GesLayoutType owner, GesLayoutEncoding encoding, string name, string label, int node, Dictionary<string, int> counts)
    {
        var value = new GesPlanValue { Base = name, Label = label, Node = node, Encoding = encoding };
        switch (encoding.Kind)
        {
            case GesLayoutKind.Text or GesLayoutKind.List:
                throw new GesGeneratorException(GesGeneratorException.Unsupported, $"The field '{name}' of '{owner.Name}' holds a {(encoding.Kind == GesLayoutKind.Text ? encoding.TagName ? "Tag with max" : "Text" : encoding.Dice ? "Dice" : "List")}, which cannot be generated yet.");
            case GesLayoutKind.Point or GesLayoutKind.Vector:
                var axisNode = node + 1;
                foreach (var axis in encoding.Axes)
                {
                    value.Axes.Add(new GesPlanValue { Base = Pascal(axis.Name), Label = axis.Name, Node = axisNode, Encoding = axis.Encoding });
                    axisNode++;
                }

                break;
            case GesLayoutKind.Union:
                var alternativeNode = node + 1;
                foreach (var alternative in encoding.Alternatives)
                {
                    value.Alternatives.Add(Value(layout, owner, alternative.Encoding, name + Pascal(alternative.Name), alternative.Name, alternativeNode, counts));
                    alternativeNode += Count(layout, alternative.Encoding, counts);
                }

                break;
        }

        return value;
    }

    // The number of nodes an encoding occupies in a bound layout.
    internal static int Count(GesSchemaLayout layout, GesLayoutEncoding encoding, Dictionary<string, int> counts)
    {
        switch (encoding.Kind)
        {
            case GesLayoutKind.List:
                return 1 + Count(layout, encoding.Element!.Encoding, counts);
            case GesLayoutKind.Point or GesLayoutKind.Vector:
                return 1 + encoding.Axes.Count;
            case GesLayoutKind.Union:
                var total = 1;
                foreach (var alternative in encoding.Alternatives) total += Count(layout, alternative.Encoding, counts);
                return total;
            case GesLayoutKind.Schema:
                if (!counts.TryGetValue(encoding.Schema, out var nested))
                {
                    nested = 0;
                    foreach (var field in layout.Type(encoding.Schema).Fields) nested += Count(layout, field.Encoding, counts);
                    counts[encoding.Schema] = nested;
                }

                return 1 + nested;
            default:
                return 1;
        }
    }

    // The paths of a type's nodes in order; a nested schema's paths follow its field's path.
    private static List<string> Paths(GesSchemaLayout layout, GesLayoutType type, Dictionary<string, List<string>> cache)
    {
        if (cache.TryGetValue(type.Name, out var known)) return known;
        var paths = new List<string>();
        foreach (var field in type.Fields) Paths(layout, field.Path, field.Encoding, paths, cache);
        cache[type.Name] = paths;
        return paths;
    }

    private static void Paths(GesSchemaLayout layout, string path, GesLayoutEncoding encoding, List<string> paths, Dictionary<string, List<string>> cache)
    {
        paths.Add(path);
        switch (encoding.Kind)
        {
            case GesLayoutKind.List:
                Paths(layout, encoding.Element!.Path, encoding.Element.Encoding, paths, cache);
                break;
            case GesLayoutKind.Point or GesLayoutKind.Vector:
                foreach (var axis in encoding.Axes) paths.Add(axis.Path);
                break;
            case GesLayoutKind.Union:
                foreach (var alternative in encoding.Alternatives) Paths(layout, alternative.Path, alternative.Encoding, paths, cache);
                break;
            case GesLayoutKind.Schema:
                foreach (var nested in Paths(layout, layout.Type(encoding.Schema), cache)) paths.Add(path + "." + nested);
                break;
        }
    }

    internal static string Pascal(string name) => name.Length == 0 ? name : char.ToUpperInvariant(name[0]) + name.Substring(1);

    internal static string Camel(string name) => name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name.Substring(1);

    // Checks a namespace of dot-separated identifiers that are no keywords of the language.
    internal static List<string> Namespace(string text, ISet<string> keywords)
    {
        var parts = new List<string>();
        if (text.Length == 0) return parts;
        foreach (var part in text.Split('.'))
        {
            var valid = part.Length > 0 && (IsLetter(part[0]) || part[0] == '_') && !keywords.Contains(part);
            foreach (var character in part) valid &= IsLetter(character) || character == '_' || character is >= '0' and <= '9';
            if (!valid) throw new GesGeneratorException(GesGeneratorException.InvalidNamespace, $"The namespace '{text}' is no sequence of identifiers.");
            parts.Add(part);
        }

        return parts;
    }

    private static bool IsLetter(char character) => character is >= 'a' and <= 'z' or >= 'A' and <= 'Z';

    // Fails when two generated members of one scope get the same name.
    internal static void Unique(string scope, List<(string Name, string Origin)> names)
    {
        var seen = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, origin) in names)
        {
            if (seen.TryGetValue(name, out var first))
                throw new GesGeneratorException(GesGeneratorException.NameCollision, $"In '{scope}', {first} and {origin} are both generated as '{name}'.");
            seen.Add(name, origin);
        }
    }

    internal static string Header(GesSchemaLayout layout)
    {
        var text = new StringBuilder();
        text.Append("// <auto-generated>\n");
        text.Append("//     Generated by `ges schema generate` from ").Append(layout.Roots.Count == 1 ? "the root " : "the roots ").Append(string.Join(", ", layout.Roots)).Append(".\n");
        text.Append("//     Do not edit it; generate it again when a schema or a parameter changes.\n");
        foreach (var parameter in layout.Parameters)
            text.Append("//     Parameter ").Append(parameter.Name).Append(" = ").Append(parameter.Value).Append(parameter.Unit == "none" ? "" : " " + parameter.Unit).Append(".\n");
        foreach (var root in layout.Roots)
        {
            var type = layout.Type(root);
            text.Append("//     ").Append(root).Append(": schema hash ").Append(GesSchemaLayoutJson.Hex(type.Hash)).Append(", binding hash ").Append(GesSchemaLayoutJson.Hex(type.BindingHash)).Append(".\n");
        }

        text.Append("// </auto-generated>\n");
        return text.ToString();
    }

    // Writes code with four spaces of indentation per level.
    internal sealed class Code
    {
        private readonly StringBuilder _text = new();
        private int _depth;

        internal void Line(string line)
        {
            if (line.Length > 0) _text.Append(' ', _depth * 4).Append(line);
            _text.Append('\n');
        }

        internal void Blank() => _text.Append('\n');

        internal void Open(string line)
        {
            Line(line);
            _depth++;
        }

        internal void Close(string line = "}")
        {
            _depth--;
            Line(line);
        }

        internal void Raw(string text) => _text.Append(text);

        public override string ToString() => _text.ToString();
    }
}
