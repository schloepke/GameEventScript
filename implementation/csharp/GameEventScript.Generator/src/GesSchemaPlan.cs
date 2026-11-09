// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GameEventScript.Api;

namespace GameEventScript.Generator;

// What every emitter generates, independent of the language: per type its members in wire order with their node offsets,
// the field paths in node order for errors, and the nested types it declares. Names are bases in PascalCase that each
// emitter spells for its language. A type that holds a Text or List, directly or through a nested type, is a class, whose
// storage grows on demand; other types are structs.
internal sealed class GesPlanType
{
    internal GesLayoutType Layout = new();
    internal string Name = "";
    internal GesSchemaKind Kind;
    internal bool IsClass;

    // A synthesized list element type, which shares the paths of its owner.
    internal bool IsItem;
    internal List<string> Paths = [];
    internal List<GesPlanField> Fields = [];

    // A message's tags, each a Boolean member at its node.
    internal List<(string Tag, string Base, int Node)> Tags = [];

    // A message set's members, named after their messages.
    internal List<string> Members = [];
    internal bool MembersAreClasses;
}

internal sealed class GesPlanField
{
    internal GesLayoutField Layout = new();
    internal string Base = "";
    internal bool Optional;
    internal bool Key;
    internal GesPlanValue Value = new();
}

// One encoding at a node, stored in the member named by Base. A union holds its alternatives, a Point or Vector its axes,
// and a List its element: a value of the element encoding, or a synthesized item type for a union, Text or List.
internal sealed class GesPlanValue
{
    internal string Base = "";
    internal string Label = "";
    internal int Node;
    internal GesLayoutEncoding Encoding = new();
    internal List<GesPlanValue> Axes = [];
    internal List<GesPlanValue> Alternatives = [];
    internal GesPlanValue? Element;
    internal GesPlanType? Item;

    // Whether the value refers to a type that is a class.
    internal bool IsClass;
}

internal static class GesSchemaPlanner
{
    // The schemas that are elements of a keyed list, which compare and read their keys for the list's delta.
    internal static HashSet<string> KeyedElements(List<GesPlanType> types)
    {
        var elements = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (var type in types)
            foreach (var field in type.Fields) KeyedElements(field.Value, elements);
        return elements;
    }

    private static void KeyedElements(GesPlanValue value, HashSet<string> elements)
    {
        if (value.Encoding.Kind == GesLayoutKind.List && value.Encoding.Keyed) elements.Add(value.Element!.Encoding.Schema);
        foreach (var alternative in value.Alternatives) KeyedElements(alternative, elements);
        if (value.Item is not null)
            foreach (var field in value.Item.Fields) KeyedElements(field.Value, elements);
        else if (value.Element is not null) KeyedElements(value.Element, elements);
    }

    // The schemas that a list of classes pools, and whose instances therefore remember their owner.
    internal static HashSet<string> PooledElements(List<GesPlanType> types)
    {
        var elements = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (var type in types)
            foreach (var field in type.Fields) PooledElements(field.Value, elements);
        return elements;
    }

    private static void PooledElements(GesPlanValue value, HashSet<string> elements)
    {
        if (value.Encoding.Kind == GesLayoutKind.List && value.Item is null && value.Element!.IsClass) elements.Add(value.Element.Encoding.Schema);
        foreach (var alternative in value.Alternatives) PooledElements(alternative, elements);
        if (value.Item is not null)
            foreach (var field in value.Item.Fields) PooledElements(field.Value, elements);
        else if (value.Element is not null) PooledElements(value.Element, elements);
    }

    internal static bool Keyed(GesPlanValue value) => value.Encoding.Kind == GesLayoutKind.List && value.Encoding.Keyed;

    internal static bool HasKeyed(GesPlanValue value) => Keyed(value) || value.Alternatives.Exists(HasKeyed);

    internal static List<GesPlanType> Plan(GesSchemaLayout layout)
    {
        var context = new Context(layout);
        var result = new List<GesPlanType>();
        foreach (var type in layout.Types) result.Add(Type(context, type));
        return result;
    }

    private sealed class Context(GesSchemaLayout layout)
    {
        internal GesSchemaLayout Layout { get; } = layout;

        internal Dictionary<string, int> Counts { get; } = new(StringComparer.Ordinal);

        internal Dictionary<string, List<string>> Paths { get; } = new(StringComparer.Ordinal);

        internal Dictionary<string, bool> Classes { get; } = new(StringComparer.Ordinal);
    }

    private static GesPlanType Type(Context context, GesLayoutType type)
    {
        var plan = new GesPlanType { Layout = type, Name = type.Name, Kind = type.Kind, IsClass = IsClass(context, type.Name) };
        if (type.Kind == GesSchemaKind.Messages)
        {
            plan.Members.AddRange(type.Members);
            foreach (var member in type.Members) plan.MembersAreClasses |= IsClass(context, member);
            return plan;
        }

        plan.Paths = Paths(context, type);
        var node = 0;
        foreach (var field in type.Fields)
        {
            var planned = new GesPlanField { Layout = field, Base = Member(field.Name, type.Name), Optional = field.Optional, Key = field.Key };
            planned.Value = Value(context, field.Encoding, planned.Base, "", node);
            node += Count(context.Layout, field.Encoding, context.Counts);
            plan.Fields.Add(planned);
        }

        foreach (var tag in type.Tags) plan.Tags.Add((tag, Member(tag, type.Name), node++));
        return plan;
    }

    private static GesPlanValue Value(Context context, GesLayoutEncoding encoding, string name, string label, int node)
    {
        var value = new GesPlanValue { Base = name, Label = label, Node = node, Encoding = encoding, IsClass = HoldsStorage(context, encoding) };
        switch (encoding.Kind)
        {
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
                    value.Alternatives.Add(Value(context, alternative.Encoding, name + Pascal(alternative.Name), alternative.Name, alternativeNode));
                    alternativeNode += Count(context.Layout, alternative.Encoding, context.Counts);
                }

                break;
            case GesLayoutKind.List:
                var element = encoding.Element!.Encoding;
                if (element.Kind is GesLayoutKind.Union or GesLayoutKind.Text or GesLayoutKind.List)
                {
                    // The item's single field sits at node 0 of the paths its owner passes on.
                    var item = new GesPlanType { Name = name + "Item", Kind = GesSchemaKind.Schema, IsItem = true, IsClass = HoldsStorage(context, element) };
                    item.Fields.Add(new GesPlanField { Layout = encoding.Element, Base = "Value", Value = Value(context, element, "Value", "", 0) });
                    value.Item = item;
                    value.Element = new GesPlanValue { Base = name + "Item", Node = node + 1, Encoding = element, IsClass = item.IsClass };
                }
                else value.Element = Value(context, element, name + "Item", "", node + 1);

                break;
        }

        return value;
    }

    // Whether values of an encoding need storage that grows, so that their type is a class.
    private static bool HoldsStorage(Context context, GesLayoutEncoding encoding) => encoding.Kind switch
    {
        GesLayoutKind.Text or GesLayoutKind.List => true,
        GesLayoutKind.Schema => IsClass(context, encoding.Schema),
        GesLayoutKind.Union => encoding.Alternatives.Exists(alternative => HoldsStorage(context, alternative.Encoding)),
        _ => false
    };

    private static bool IsClass(Context context, string name)
    {
        if (context.Classes.TryGetValue(name, out var known)) return known;
        var type = context.Layout.Type(name);
        var result = type.Kind == GesSchemaKind.Messages
            ? type.Members.Exists(member => IsClass(context, member))
            : type.Fields.Exists(field => HoldsStorage(context, field.Encoding));
        context.Classes[name] = result;
        return result;
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

    // The paths of a type's nodes in order; a nested schema's paths follow its field's path, and a message's tags its fields.
    private static List<string> Paths(Context context, GesLayoutType type) => Paths(context.Layout, type, context.Paths);

    private static List<string> Paths(GesSchemaLayout layout, GesLayoutType type, Dictionary<string, List<string>> cache)
    {
        if (cache.TryGetValue(type.Name, out var known)) return known;
        var paths = new List<string>();
        foreach (var field in type.Fields) Paths(layout, field.Path, field.Encoding, paths, cache);
        foreach (var tag in type.Tags) paths.Add("#" + tag);
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

    // A member that would carry its type's name gets a suffix, since C# forbids such a member.
    private static string Member(string name, string type) => Pascal(name) == type ? Pascal(name) + "Value" : Pascal(name);

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

    // A string literal that stays within printable ASCII: other characters become escapes, \uXXXX or \UXXXXXXXX in C#
    // and \u{X} in Swift, so that both ports write identical text whatever a source holds.
    internal static string Literal(string text, bool swift)
    {
        var result = new StringBuilder("\"");
        for (var index = 0; index < text.Length; index++)
        {
            int scalar = text[index];
            if (char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1])) scalar = char.ConvertToUtf32(text[index], text[++index]);
            switch (scalar)
            {
                case '\\': result.Append("\\\\"); break;
                case '"': result.Append("\\\""); break;
                case '\t': result.Append("\\t"); break;
                case '\r': result.Append("\\r"); break;
                case '\n': result.Append("\\n"); break;
                case >= 0x20 and < 0x7F: result.Append((char)scalar); break;
                default:
                    var hex = scalar.ToString("X", CultureInfo.InvariantCulture);
                    result.Append(swift ? "\\u{" + hex + "}" : scalar > 0xFFFF ? "\\U" + hex.PadLeft(8, '0') : "\\u" + hex.PadLeft(4, '0'));
                    break;
            }
        }

        return result.Append('"').ToString();
    }

    // The FNV-1a 64 hash of a text's UTF-8 bytes in lowercase hex, which names a helper type uniquely per generated file.
    internal static string Hash(string text)
    {
        var hash = 14695981039346656037UL;
        foreach (var value in Encoding.UTF8.GetBytes(text)) hash = unchecked((hash ^ value) * 1099511628211UL);
        return GesSchemaLayoutJson.Hex(hash);
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

        // Indents the following lines, after a line that Close wrote, such as `} else {`.
        internal void Indent() => _depth++;

        // Ends an indentation that Indent began, without writing a line.
        internal void Dedent() => _depth--;

        internal void Close(string line = "}")
        {
            _depth--;
            Line(line);
        }

        internal void Raw(string text) => _text.Append(text);

        public override string ToString() => _text.ToString();
    }
}
