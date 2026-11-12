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

    // The depth of a recursive schema's List of itself, 0 for other types. Only such a schema takes the level of its record
    // in the recursion, since its deepest List holds nothing; every other type keeps constant offsets.
    internal int Depth;
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

    // The corners of a Box, as the Points that its Min and Max hold.
    internal List<GesPlanValue> Corners = [];
    internal List<GesPlanValue> Alternatives = [];
    internal GesPlanValue? Element;
    internal GesPlanType? Item;

    // Whether the value refers to a type that is a class.
    internal bool IsClass;

    // Whether the value is the element of a recursive List: its schema's record one level deeper, whose nodes follow the
    // declaring schema's own nodes. In a union element it is the alternative of that schema.
    internal bool Recursive;

    // Whether the value is the item element of a recursive List of a union, whose item takes the level of the record that
    // holds the List.
    internal bool PassesLevel;

    // The depth of a recursive List of the schema that holds it, 0 for every other value. Where `with` copies such a List
    // into another schema, it is an ordinary List there.
    internal int Depth;

    // How an emitter spells the type of a Tag of a named tag set, which is declared once beside the records.
    internal string TagType = "";
}

internal static class GesSchemaPlanner
{
    // The named tag sets the types use, in order of first use, with their members; each Tag of a set gets the type that an
    // emitter spells with the qualifier in front of the set's name.
    internal static List<(string Name, List<string> Tags)> TagSets(List<GesPlanType> types, string qualifier)
    {
        var sets = new List<(string Name, List<string> Tags)>();
        var seen = new HashSet<string>(System.StringComparer.Ordinal);
        var visited = new HashSet<GesPlanType>();
        foreach (var type in types) TagSets(type, qualifier, sets, seen, visited);
        return sets;
    }

    private static void TagSets(GesPlanType type, string qualifier, List<(string Name, List<string> Tags)> sets, HashSet<string> seen, HashSet<GesPlanType> visited)
    {
        if (!visited.Add(type)) return;
        foreach (var field in type.Fields) TagSets(field.Value, qualifier, sets, seen, visited);
    }

    private static void TagSets(GesPlanValue value, string qualifier, List<(string Name, List<string> Tags)> sets, HashSet<string> seen, HashSet<GesPlanType> visited)
    {
        if (value.Encoding.Kind == GesLayoutKind.Tag && value.Encoding.TagSet.Length > 0)
        {
            value.TagType = qualifier + value.Encoding.TagSet;
            if (seen.Add(value.Encoding.TagSet)) sets.Add((value.Encoding.TagSet, value.Encoding.Tags));
        }

        foreach (var alternative in value.Alternatives) TagSets(alternative, qualifier, sets, seen, visited);
        if (value.Element is not null) TagSets(value.Element, qualifier, sets, seen, visited);
        if (value.Item is not null) TagSets(value.Item, qualifier, sets, seen, visited);
    }

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

    // Whether a value holds storage that a read writes into besides the record's own fields: a class schema, a list of classes
    // with its pool or, when lists count as storage, any list.
    internal static bool HoldsStorage(GesPlanValue value, bool lists) => value.Encoding.Kind switch
    {
        GesLayoutKind.Schema => value.IsClass,
        GesLayoutKind.List => lists || value.Element!.IsClass,
        GesLayoutKind.Union => value.Alternatives.Exists(alternative => HoldsStorage(alternative, lists)),
        _ => false,
    };

    // The axes of a Point or Vector that its block leaves out: they are not transmitted, are written only when zero and read as zero.
    internal static List<string> Omitted(GesPlanValue value)
    {
        var omitted = new List<string> { "X", "Y", "Z" };
        foreach (var axis in value.Axes) omitted.Remove(axis.Base);
        return omitted;
    }

    internal static bool Keyed(GesPlanValue value) => value.Encoding.Kind == GesLayoutKind.List && value.Encoding.Keyed;

    internal static bool HasKeyed(GesPlanValue value) => Keyed(value) || value.Alternatives.Exists(HasKeyed);

    // The translator of wire records' computed fields: an optional field on a path is read only where its presence flag is
    // set, and a computed field through its property or exact value.
    internal static GesComputedEmitter Computed(List<GesPlanType> types, string helper, bool swift) => new(swift, helper, new WireAccess(types, swift));

    private sealed class WireAccess(List<GesPlanType> types, bool swift) : IGesComputedAccess
    {
        public GesComputedReach Reach(string root, string owner, string[] path)
        {
            var reach = new GesComputedReach { Text = root };
            var type = Type(owner);
            GesPlanValue? value = null;
            for (var step = 0; step < path.Length; step++)
            {
                var name = path[step];
                var field = type!.Fields.Find(candidate => candidate.Layout.Name == name);
                if (field is null)
                {
                    reach.ComputedOwner = type.Name;
                    reach.ComputedName = name;
                    return reach;
                }

                if (field.Optional) reach.Conditions.Add((step, Join(reach.Text, (swift ? "has" : "Has") + field.Base)));
                reach.Text = Join(reach.Text, Member(field.Base));
                value = field.Value;
                type = value.Encoding.Kind == GesLayoutKind.Schema ? Type(value.Encoding.Schema) : null;
            }

            if (value is not null) Declare(value.Encoding, reach);
            return reach;
        }

        public GesComputedReach Element(string owner, string[] path)
        {
            var list = Value(owner, path);
            var reach = new GesComputedReach { Text = "item" };
            Declare(list.Element!.Encoding, reach);
            return reach;
        }

        public string ElementOwner(string owner, string[] path)
        {
            var element = Value(owner, path).Element!;
            return element.Encoding.Kind == GesLayoutKind.Schema ? element.Encoding.Schema : "";
        }

        public GesResolvedComputed[] Computed(string owner) => Type(owner)!.Layout.Computed;

        // The declared range of a number and the largest count of a list.
        private static void Declare(GesLayoutEncoding encoding, GesComputedReach reach)
        {
            switch (encoding.Kind)
            {
                case GesLayoutKind.Integer:
                    reach.Integer = true;
                    reach.Lower = encoding.Range ? encoding.Lower : long.MinValue;
                    reach.Upper = encoding.Range ? encoding.Lower + (double)encoding.MaxIndex : long.MaxValue;
                    break;
                case GesLayoutKind.Quantized:
                    reach.Lower = encoding.LowerValue;
                    reach.Upper = encoding.UpperValue;
                    break;
                case GesLayoutKind.Float when encoding.Range:
                    reach.Lower = encoding.LowerValue;
                    reach.Upper = encoding.UpperValue;
                    break;
                case GesLayoutKind.List:
                    reach.MaxCount = encoding.MaxCount;
                    break;
            }
        }

        private GesPlanValue Value(string owner, string[] path)
        {
            var type = Type(owner);
            GesPlanValue? value = null;
            foreach (var name in path)
            {
                value = type!.Fields.Find(candidate => candidate.Layout.Name == name)!.Value;
                type = value.Encoding.Kind == GesLayoutKind.Schema ? Type(value.Encoding.Schema) : null;
            }

            return value!;
        }

        private GesPlanType? Type(string name) => name.Length == 0 ? null : types.Find(type => type.Name == name);

        private static string Join(string root, string member) => root.Length == 0 ? member : root + "." + member;

        private string Member(string pascal)
        {
            if (!swift) return pascal;
            var camel = Camel(pascal);
            return GesSwiftEmitter.Keywords.Contains(camel) ? "`" + camel + "`" : camel;
        }
    }

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
            node += Count(context.Layout, field.Encoding, context.Counts, type.Name);
            plan.Fields.Add(planned);
        }

        foreach (var tag in type.Tags) plan.Tags.Add((tag, Member(tag, type.Name), node++));

        // The element of a recursive List and its nodes come after the schema's own nodes, so that every own node keeps its
        // offset at every level and only the element's subtree, whose size depends on the level, varies.
        foreach (var field in plan.Fields)
        {
            if (!Recursive(field.Layout.Encoding, type.Name)) continue;
            plan.Depth = field.Value.Depth = field.Value.Encoding.Depth;
            field.Value.Element!.Node = node;
            if (field.Value.Item is not { } item)
            {
                field.Value.Element.Recursive = true;
                continue;
            }

            // A union of scalars and the schema itself: the item takes the level of the record that holds the List, and the
            // schema's nodes come after the union's own ones, so that every other alternative keeps its offset at every level.
            item.Depth = plan.Depth;
            field.Value.Element.PassesLevel = true;
            SelfLast(context, item.Fields[0].Value, type.Name);
        }

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
            case GesLayoutKind.Box:
                var cornerNode = node + 1;
                foreach (var corner in encoding.Corners)
                {
                    value.Corners.Add(Value(context, corner.Encoding, corner.Name == "lower" ? "Min" : "Max", corner.Name, cornerNode));
                    cornerNode += 1 + corner.Encoding.Axes.Count;
                }

                break;
            case GesLayoutKind.Union:
                var alternativeNode = node + 1;
                foreach (var alternative in encoding.Alternatives)
                {
                    value.Alternatives.Add(Value(context, alternative.Encoding, name + Pascal(alternative.Name), Pascal(alternative.Name), alternativeNode));
                    alternativeNode += Count(context.Layout, alternative.Encoding, context.Counts);
                }

                break;
            case GesLayoutKind.List:
                var element = encoding.Element!.Encoding;
                if (element.Kind is GesLayoutKind.Union or GesLayoutKind.Text or GesLayoutKind.Bytes or GesLayoutKind.List)
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
        GesLayoutKind.Text or GesLayoutKind.Bytes or GesLayoutKind.List => true,
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

    // Plans the alternatives of a recursive union again, with the schema's alternative after the others.
    private static void SelfLast(Context context, GesPlanValue union, string owner)
    {
        var alternatives = union.Encoding.Alternatives;
        var node = union.Node + 1;
        var self = 0;
        for (var index = 0; index < alternatives.Count; index++)
        {
            if (alternatives[index].Encoding.Schema == owner)
            {
                self = index;
                continue;
            }

            union.Alternatives[index] = Value(context, alternatives[index].Encoding, union.Base + Pascal(alternatives[index].Name), Pascal(alternatives[index].Name), node);
            node += Count(context.Layout, alternatives[index].Encoding, context.Counts);
        }

        union.Alternatives[self].Node = node;
        union.Alternatives[self].Recursive = true;
    }

    // Whether an encoding is a recursive List of the schema that holds it, alone or as an alternative of its element.
    private static bool Recursive(GesLayoutEncoding encoding, string owner)
        => encoding.Kind == GesLayoutKind.List && encoding.Depth > 0 && SelfAlternative(encoding.Element!) is not null && SelfAlternative(encoding.Element!)!.Encoding.Schema == owner;

    // The element of a recursive List that is its schema: the element itself or the schema alternative of its union.
    private static GesLayoutField? SelfAlternative(GesLayoutField element)
        => element.Encoding.Kind == GesLayoutKind.Union ? element.Encoding.Alternatives.Find(alternative => alternative.Encoding.Kind == GesLayoutKind.Schema) : element;

    // The nodes of a recursive List's element at one level, without the schema's own: one for a schema, and for a union the
    // union, its other alternatives and the schema's group.
    private static int ElementNodes(GesSchemaLayout layout, GesLayoutEncoding list, string owner, Dictionary<string, int> counts)
    {
        var element = list.Element!.Encoding;
        if (element.Kind != GesLayoutKind.Union) return 1;
        var total = 2;
        foreach (var alternative in element.Alternatives)
            if (alternative.Encoding.Schema != owner) total += Count(layout, alternative.Encoding, counts);
        return total;
    }

    // The number of nodes an encoding occupies in a bound layout; the element of a recursive List of `owner` is counted
    // apart, after its own nodes.
    internal static int Count(GesSchemaLayout layout, GesLayoutEncoding encoding, Dictionary<string, int> counts, string owner = "")
    {
        switch (encoding.Kind)
        {
            case GesLayoutKind.List:
                return Recursive(encoding, owner) ? 1 : 1 + Count(layout, encoding.Element!.Encoding, counts);
            case GesLayoutKind.Point or GesLayoutKind.Vector:
                return 1 + encoding.Axes.Count;
            case GesLayoutKind.Box:
                return 1 + 2 * (1 + encoding.Corners[0].Encoding.Axes.Count);
            case GesLayoutKind.Union:
                var total = 1;
                foreach (var alternative in encoding.Alternatives) total += Count(layout, alternative.Encoding, counts);
                return total;
            case GesLayoutKind.Schema:
                if (!counts.TryGetValue(encoding.Schema, out var nested))
                {
                    nested = Nodes(layout, layout.Type(encoding.Schema), 1, counts);
                    counts[encoding.Schema] = nested;
                }

                return 1 + nested;
            default:
                return 1;
        }
    }

    // The nodes of a schema's fields at a level of its recursion: its own nodes and, below the depth of a recursive List, the
    // element and its nodes one level deeper.
    private static int Nodes(GesSchemaLayout layout, GesLayoutType type, int level, Dictionary<string, int> counts)
    {
        var total = 0;
        GesLayoutEncoding? recursive = null;
        foreach (var field in type.Fields)
        {
            total += Count(layout, field.Encoding, counts, type.Name);
            if (Recursive(field.Encoding, type.Name)) recursive = field.Encoding;
        }

        return recursive is not null && level < recursive.Depth ? total + ElementNodes(layout, recursive, type.Name, counts) + Nodes(layout, type, level + 1, counts) : total;
    }

    // The paths of a type's nodes in order; a nested schema's paths follow its field's path, and a message's tags its fields.
    // A recursive List's element and its paths come last, one level deeper each time.
    private static List<string> Paths(Context context, GesLayoutType type) => Paths(context.Layout, type, context.Paths);

    private static List<string> Paths(GesSchemaLayout layout, GesLayoutType type, Dictionary<string, List<string>> cache)
    {
        if (cache.TryGetValue(type.Name, out var known)) return known;
        var paths = Paths(layout, type, 1, cache);
        cache[type.Name] = paths;
        return paths;
    }

    private static List<string> Paths(GesSchemaLayout layout, GesLayoutType type, int level, Dictionary<string, List<string>> cache)
    {
        var paths = new List<string>();
        foreach (var field in type.Fields) Paths(layout, field.Path, field.Encoding, paths, cache, type.Name);
        foreach (var tag in type.Tags) paths.Add("#" + tag);
        foreach (var field in type.Fields)
        {
            if (!Recursive(field.Encoding, type.Name) || level >= field.Encoding.Depth) continue;
            var element = field.Encoding.Element!;
            paths.Add(element.Path);
            var self = element;
            if (element.Encoding.Kind == GesLayoutKind.Union)
            {
                // The schema's alternative and its nodes come after the other alternatives.
                foreach (var alternative in element.Encoding.Alternatives)
                    if (alternative.Encoding.Schema != type.Name) Paths(layout, alternative.Path, alternative.Encoding, paths, cache);
                self = SelfAlternative(element)!;
                paths.Add(self.Path);
            }

            foreach (var nested in Paths(layout, type, level + 1, cache)) paths.Add(self.Path + "." + nested);
        }

        return paths;
    }

    private static void Paths(GesSchemaLayout layout, string path, GesLayoutEncoding encoding, List<string> paths, Dictionary<string, List<string>> cache, string owner = "")
    {
        paths.Add(path);
        switch (encoding.Kind)
        {
            case GesLayoutKind.List:
                if (!Recursive(encoding, owner)) Paths(layout, encoding.Element!.Path, encoding.Element.Encoding, paths, cache);
                break;
            case GesLayoutKind.Point or GesLayoutKind.Vector:
                foreach (var axis in encoding.Axes) paths.Add(axis.Path);
                break;
            case GesLayoutKind.Box:
                foreach (var corner in encoding.Corners) Paths(layout, corner.Path, corner.Encoding, paths, cache);
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
    // A record's nested types are named after its fields. Each would hide a top-level type or tag set of the same name for
    // every reference inside the record, so that name fails like any other collision; their origins carry this prefix.
    internal const string NestedTypeOrigin = "the nested type ";

    internal static HashSet<string> TopLevel(List<GesPlanType> types, List<(string Name, List<string> Tags)> sets)
    {
        var names = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (var type in types)
            if (!type.IsItem) names.Add(type.Name);
        foreach (var (name, _) in sets) names.Add(name);
        return names;
    }

    internal static void Unique(string scope, List<(string Name, string Origin)> names, ICollection<string> topLevel)
    {
        Unique(scope, names);
        foreach (var (name, origin) in names)
        {
            if (origin.StartsWith(NestedTypeOrigin, System.StringComparison.Ordinal) && topLevel.Contains(name))
                throw new GesGeneratorException(GesGeneratorException.NameCollision, $"In '{scope}', {origin} is generated as '{name}', which hides the top-level type of that name.");
            if (IsRuntimeName(name))
                throw new GesGeneratorException(GesGeneratorException.NameCollision, $"In '{scope}', {origin} is generated as '{name}', a name of the runtime types that generated code uses.");
        }
    }

    // Generated Swift names these standard-library and runtime types without qualification, so a type of the same name in
    // the namespace would hide them; Type, Any, AnyObject, Protocol and Self cannot name a nested type at all. Generated C#
    // qualifies the platform types it uses with global::, so only the runtime's names are reserved there.
    private static readonly string[] SwiftReserved =
    [
        "Any", "AnyObject", "Array", "Bool", "CaseIterable", "Codable", "Double", "Equatable", "Error", "GameEventScriptRuntime", "Hashable",
        "Identifiable", "Int", "Int32", "Int64", "OptionSet", "Protocol", "Result", "Self", "Sendable", "String", "Swift", "Type", "UInt64",
        "UInt8", "UTF8", "UnsafeMutableRawBufferPointer", "UnsafeRawBufferPointer"
    ];

    // Names of the form Ges and a capital letter belong to the runtime types that generated code names without qualification.
    private static bool IsRuntimeName(string name) => name.Length > 3 && name.StartsWith("Ges", System.StringComparison.Ordinal) && name[3] is >= 'A' and <= 'Z';

    // The types declared beside the records must not take a name that the generated code uses for another type.
    internal static void CheckTopLevel(ICollection<string> names, bool swift)
    {
        var sorted = new List<string>(names);
        sorted.Sort(System.StringComparer.Ordinal);
        foreach (var name in sorted)
        {
            if (IsRuntimeName(name))
                throw new GesGeneratorException(GesGeneratorException.NameCollision, $"The type '{name}' takes a name of the runtime types that generated code uses.");
            if (swift && System.Array.IndexOf(SwiftReserved, name) >= 0)
                throw new GesGeneratorException(GesGeneratorException.NameCollision, $"The type '{name}' takes a name of a Swift type that generated code uses.");
        }
    }

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
    // A let as a code literal: a whole value for an integer, otherwise its exact decimal, which C# and Swift round alike.
    internal static string ConstantValue(GesLayoutConstant constant)
    {
        if (!constant.Integer) return Decimal(constant.Significand, constant.Exponent);
        var value = constant.Significand;
        for (var exponent = 0; exponent < constant.Exponent; exponent++) value *= 10;
        return value.ToString(CultureInfo.InvariantCulture);
    }

    // A let as its source declares it, for generated documentation: a Percentage in percent, a Quantity with its symbol.
    internal static string ConstantSource(GesLayoutConstant constant)
        => $"let {constant.Name} be {Decimal(constant.Significand, constant.Exponent + (constant.Suffix == "%" ? 2 : 0))}{constant.Suffix}";

    // Plain digits within 18 places of the point, otherwise <significand>e<exponent>.
    internal static string Decimal(long significand, int exponent)
    {
        var text = significand.ToString(CultureInfo.InvariantCulture);
        var sign = significand < 0 ? "-" : "";
        var digits = significand < 0 ? text.Substring(1) : text;
        if (exponent >= 0 && digits.Length + exponent <= 18) return sign + digits + new string('0', exponent);
        if (exponent < 0 && -exponent <= 18)
        {
            var point = digits.Length + exponent;
            return sign + (point > 0 ? digits.Substring(0, point) + "." + digits.Substring(point) : "0." + new string('0', -point) + digits);
        }

        return sign + digits + "e" + exponent.ToString(CultureInfo.InvariantCulture);
    }

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

    // The suffix of helper type names: unique per single file, and for split output stable across schema and root changes, so
    // that a change touches only the files of the declarations it changes. Split code of one namespace is one generation.
    internal static string Suffix(string header, GesSchemaGenerateOptions options)
        => Hash(options.Split == GesSchemaSplit.None ? header : "split\n" + options.Namespace);

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

        internal Code(int depth = 0) => _depth = depth;

        internal int Depth => _depth;

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
