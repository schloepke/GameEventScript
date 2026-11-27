// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Globalization;
using GameEventScript.Api;

namespace GameEventScript.Generator;

// The language-neutral model layout: every schema reached from the model roots, described without binding, since a model
// type is a plain value that is never encoded. Model emitters read only this, and its JSON form is compared between ports.
internal sealed class GesModelLayout
{
    internal const int Version = 1;

    internal GesModelLayout(List<string> roots, List<GesModelType> types)
    {
        Roots = roots;
        Types = types;
    }

    // The handle types the types reach, ordered by name, each with its composite root.
    internal List<GesModelHandle> Handles { get; } = [];

    internal List<string> Roots { get; }

    internal List<GesModelType> Types { get; }

    // The schema sources, with their file names only, which generated code that reads documents embeds.
    internal List<GesSchemaSource> Sources { get; } = [];

    // The schemas whose fields reached types take unchanged with `with`, directly or through each other, by name.
    internal List<GesModelProtocol> Protocols { get; } = [];

    internal GesModelType Type(string name) => Types.Find(type => type.Name == name) ?? throw new InvalidOperationException($"Type '{name}' is not in the model.");
}

internal sealed class GesModelType
{
    internal string Name = "";
    internal ulong Hash;

    // A schema, a message reached by a model set or a model set; the name a schema set looks it up by.
    internal GesSchemaKind Kind = GesSchemaKind.Schema;
    internal string Lookup = "";

    // The tags of a message and the members of a set, in declared order.
    internal List<string> Tags = [];
    internal List<string> Members = [];
    internal List<GesModelField> Fields = [];
    internal List<GesLayoutConstant> Constants = [];

    // The schemas it takes unchanged with `with`, whose protocols or interfaces it adopts, in declared order.
    internal List<string> Takes = [];

    // The fields written `computed by`, which models derive from their other fields.
    internal GesResolvedComputed[] Computed = [];

    // The wire layout of a packed schema, whose fields' encodings place them in its word; null for other types.
    internal GesSchemaLayout? Packed;

    // The conversions of a projection from the schemas it narrows that the layout holds, nearest first.
    internal List<GesModelConversion> Conversions = [];
}

// A projection's conversion from one schema it narrows: per taken field, the field names of its path within that schema.
internal sealed class GesModelConversion
{
    internal string Source = "";
    internal List<List<string>> Paths = [];
}

// A schema that types take with `with`: generated as a protocol (Swift) or interface (C#) with read-only properties. It
// refines the protocols of the schemas it takes unchanged and requires its other fields whose type is shared by name, as
// every field's type is except a union, Flags and a closed Tag written in place, which generated code declares per field.
// A handle type: its identity's form, which a model type `<Name>Handle` checks, and the state schema, a type of the layout.
internal sealed class GesModelHandle
{
    internal string Name = "";
    internal ulong Hash;
    internal string State = "";
    internal GesHandleVersionKind Version;

    // The id of a plain handle type, or the part and the root's name of a composite. A part-less composite has its root's
    // id and version as its own and builds like a plain handle; PartlessRoot names its root.
    internal GesHandleKey Id;
    internal string? Root;
    internal GesHandleKey Part;
    internal string? PartlessRoot;

    // The projections of the state that the layout holds, by name.
    internal List<string> Projections = [];

    internal bool IsComposite => Root is not null;

    // The key that a composite's equality compares after its root's id: its part; the id for any other.
    internal GesHandleKey Key => IsComposite ? Part : Id;

    internal static GesModelHandle Of(GesHandleType handle)
        => new() { Name = handle.Name, Hash = handle.Hash, State = handle.StateName, Version = handle.Version, Id = handle.Id ?? default, Root = handle.Part is null ? null : handle.Root?.Name, Part = handle.Part ?? default, PartlessRoot = handle.Part is null ? handle.Root?.Name : null };
}

internal sealed class GesModelProtocol
{
    internal string Name = "";
    internal bool Abstract;
    internal List<string> Takes = [];
    internal List<GesModelField> Fields = [];
}

internal sealed class GesModelField
{
    internal string Name = "";
    internal string Path = "";
    internal bool Optional;
    internal bool Key;
    internal GesLayoutDefault? Default;
    internal GesModelValue Value = new();

    // Whether a message argument is written without a label, so that its message carries the label `_`.
    internal bool Unlabeled;

    // Whether the member takes the suffix Value because a schema lends the field with `with` and the field is named like that
    // schema or one that takes it, so that the interface or protocol and every type taking it name it alike.
    internal bool Renamed;
}

internal enum GesModelKind
{
    Boolean,
    Number,
    Tag,
    Text,
    Bytes,
    List,
    Dice,
    Point,
    Vector,
    Schema,
    Union,
    Nothing,
    Flags,
    Box,
    Poly,
    Disc,

    // Free data: a Map of any values, and a List without an element type, whose elements are any values.
    Map,
    Values,

    // A handle, and a Map from handles of one type to values, whose value is its Element.
    Handle,
    HandleMap
}

internal sealed class GesModelValue
{
    internal GesModelKind Kind;

    // Numbers: Number, Quantity or Percentage, the unit and the documented limits as canonical decimals or parameter names.
    internal string Type = "";
    internal string Unit = "none";
    internal bool Integer;
    internal bool Wrapping;
    internal string? Lower;
    internal string? Upper;
    internal string? Step;

    // A closed tag set, empty for any tag, or the members of Flags; the documented maximum of a Tag, Text, List or Dice.
    internal List<string> Tags = [];
    internal string TagSet = "";
    internal long? Max;

    // A List of exactly Max elements.
    internal bool Fixed;
    internal GesModelField? Element;

    // The depth of a recursive List, 0 for every other List.
    internal int Depth;

    // The axes a Point or Vector declares, empty without an axis block; union alternatives; a schema reference.
    internal List<string> Axes = [];
    internal List<GesModelField> Alternatives = [];
    internal string Schema = "";

    // Whether the schema is packed, so that its type is a value type over one word.
    internal bool Packed;

    // The handle type of a handle or a handle Map's key, and whether a handle carries its version.
    internal string Handle = "";
    internal bool Versioned;
}

internal static class GesModelLayoutBuilder
{
    internal static GesModelLayout Build(GesSchemaSet set, IReadOnlyList<string> roots)
    {
        if (roots.Count == 0) throw new GesGeneratorException(GesGeneratorException.Usage, "Specify at least one root.");
        var reached = new SortedDictionary<string, GesSchema>(StringComparer.Ordinal);
        var handles = new SortedDictionary<string, GesHandleType>(StringComparer.Ordinal);
        var rootNames = new List<string>();
        foreach (var root in roots)
        {
            var schema = set.Schema(root);
            if (rootNames.Contains(schema.Name)) continue;
            if (schema.Kind == GesSchemaKind.Message) throw new GesGeneratorException(GesGeneratorException.Usage, $"The model root '{root}' is a message; a model root is a schema or message set.");
            if (schema.IsAbstract) throw new GesGeneratorException(GesGeneratorException.Usage, $"The model root '{root}' is abstract; only its protocol is generated, where a model takes it.");
            rootNames.Add(schema.Name);
            Reach(schema, reached, handles);
        }

        // A model root that is the state of a handle type with a Text id of its own reaches that type, whose store holds it.
        foreach (var handle in set.HandleTypes)
            if (handle.Root is null && handle.StoreOf is null && handle.Id is { IsText: true } && rootNames.Contains(handle.StateName)
                && reached.TryGetValue(handle.StateName, out var state) && state.Declaration == handle.Resolved.State)
                Reach(handle, handles);

        var types = new List<GesModelType>();
        foreach (var schema in reached.Values)
        {
            var type = new GesModelType { Name = schema.Name, Hash = schema.Hash, Kind = schema.Kind, Lookup = set.RootName(schema), Constants = GesLayoutConstant.Of(schema), Computed = schema.Declaration.Computed };
            if (schema.PackedWidth > 0) type.Packed = GesSchemaLayoutBuilder.Build(set, [set.RootName(schema)], new GesSchemaParameters());
            foreach (var info in schema.Fields)
            {
                var field = Field(info.Name, info.Name, info.Alternatives);
                field.Optional = info.IsOptional;
                field.Key = info.IsKey;
                field.Unlabeled = schema.Kind != GesSchemaKind.Schema && info.IsUnlabeled;
                field.Default = GesLayoutDefault.Of(info);
                type.Fields.Add(field);
            }

            foreach (var take in schema.Takes)
                if (take.IsUnchanged) type.Takes.Add(take.Schema.Name);
            if (schema.Kind == GesSchemaKind.Message) type.Tags.AddRange(schema.Tags);
            foreach (var member in schema.Members) type.Members.Add(member.Name);
            Conversions(schema.Declaration, reached, type.Conversions);
            types.Add(type);
        }

        var layout = new GesModelLayout(rootNames, types);
        foreach (var handle in handles.Values)
        {
            var model = GesModelHandle.Of(handle);
            foreach (var projection in handle.Projections)
                if (reached.TryGetValue(projection.Name, out var held) && held.Declaration == projection.Declaration) model.Projections.Add(projection.Name);
            layout.Handles.Add(model);
        }

        foreach (var source in set.Sources) layout.Sources.Add(new GesSchemaSource(source.Name.Substring(source.Name.LastIndexOfAny(['/', '\\']) + 1), source.Text));
        var protocols = new SortedDictionary<string, GesSchema>(StringComparer.Ordinal);
        foreach (var schema in reached.Values) Protocols(schema, protocols);
        foreach (var schema in protocols.Values)
        {
            var protocol = new GesModelProtocol { Name = schema.Name, Abstract = schema.IsAbstract };
            var inherited = new HashSet<string>(StringComparer.Ordinal);
            foreach (var take in schema.Takes)
            {
                if (!take.IsUnchanged) continue;
                protocol.Takes.Add(take.Schema.Name);
                foreach (var field in take.Schema.Fields) inherited.Add(field.Name);
            }

            foreach (var info in schema.Fields)
            {
                if (inherited.Contains(info.Name)) continue;
                var field = Field(info.Name, info.Name, info.Alternatives);
                field.Optional = info.IsOptional;
                field.Key = info.IsKey;
                if (Shared(field.Value)) protocol.Fields.Add(field);
            }

            layout.Protocols.Add(protocol);
        }

        RenameLent(layout);
        return layout;
    }

    // A projection converts from its source and every schema below it that the layout holds. Each path starts in the nearest
    // source; a source that is itself a projection replaces a path's first field by that field's path one level down.
    private static void Conversions(GesResolvedDeclaration projection, SortedDictionary<string, GesSchema> reached, List<GesModelConversion> conversions)
    {
        if (projection.Source is not { } source) return;
        var paths = new List<List<string>>();
        foreach (var path in projection.Paths) paths.Add(new List<string>(Array.ConvertAll(path, field => field.Name)));
        while (true)
        {
            if (reached.TryGetValue(source.Name, out var held) && held.Declaration == source)
                conversions.Add(new GesModelConversion { Source = source.Name, Paths = paths.ConvertAll(path => new List<string>(path)) });
            if (source.Source is not { } next) return;
            foreach (var path in paths)
            {
                var below = source.Paths[Array.FindIndex(source.Fields, field => field.Name == path[0])];
                path.RemoveAt(0);
                path.InsertRange(0, Array.ConvertAll(below, field => field.Name));
            }

            source = next;
        }
    }

    // The models that wire code generated with `--models` converts to. Each model group is generated with `--model` like any
    // other, and those groups whose roots reach a wire type lie in the models' namespace: their types, laid out as the group's
    // generation lays them out, may not share a name there. A group that reaches no wire type is not part of that namespace,
    // so its types count for nothing, even where they share a name with a model the wire code converts to.
    internal static GesModelLayout? Converted(GesSchemaSet set, GesSchemaLayout wire)
    {
        var wanted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var type in wire.Types) wanted.Add(type.Lookup);
        var groups = new List<string>();
        foreach (var schema in set.Schemas)
            if (schema.ModelGroup is { } group && !groups.Contains(group)) groups.Add(group);
        var types = new SortedDictionary<string, GesModelType>(StringComparer.Ordinal);
        var protocols = new SortedDictionary<string, GesModelProtocol>(StringComparer.Ordinal);
        var handles = new SortedDictionary<string, GesModelHandle>(StringComparer.Ordinal);
        var owners = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var group in groups)
        {
            var roots = GesSchemaGenerator.ModelRoots(set, group);
            var seen = new HashSet<GesSchema>();
            foreach (var root in roots) Collect(set.Schema(root), seen);

            var converts = false;
            foreach (var schema in seen) converts |= wanted.Contains(set.RootName(schema));
            if (!converts) continue;
            var layout = Build(set, roots);
            foreach (var type in layout.Types)
            {
                if (types.TryGetValue(type.Name, out var known))
                {
                    // One schema in several groups is one model type; two schemas of one name are two.
                    if (known.Lookup == type.Lookup) continue;
                    throw new GesGeneratorException(GesGeneratorException.NameCollision, $"The wire code converts to the models of the groups '{owners[known.Name]}' and '{group}', whose model types of {Describe(set, known)} and {Describe(set, type)} are both named '{type.Name}' in the models' namespace; generate the wire roots that reach each group separately, each with the namespace of that group's models in --models.");
                }

                types.Add(type.Name, type);
                owners.Add(type.Name, group);
            }

            foreach (var protocol in layout.Protocols)
                if (!protocols.ContainsKey(protocol.Name)) protocols.Add(protocol.Name, protocol);
            foreach (var handle in layout.Handles)
                if (!handles.ContainsKey(handle.Name)) handles.Add(handle.Name, handle);
        }

        if (types.Count == 0) return null;
        var converted = new GesModelLayout([], [.. types.Values]);
        converted.Protocols.AddRange(protocols.Values);
        converted.Handles.AddRange(handles.Values);
        return converted;
    }

    // Every schema that a model of the schema names, as Reach finds them, without the check of their names.
    private static void Collect(GesSchema schema, HashSet<GesSchema> seen)
    {
        if (!seen.Add(schema)) return;
        foreach (var field in schema.Fields)
            foreach (var type in field.Alternatives) Collect(type, seen);
        foreach (var member in schema.Members) Collect(member, seen);
    }

    private static void Collect(GesSchemaType type, HashSet<GesSchema> seen)
    {
        if (type.Schema is { } schema) Collect(schema, seen);
        foreach (var element in type.Element) Collect(element, seen);
    }

    private static string Describe(GesSchemaSet set, GesModelType type) => Describe(set.Schema(type.Lookup));

    // A field that a schema lends with `with` takes one member name in its interface or protocol and in every type that takes
    // it: like a field named after its own type, it is renamed where it is named like the lending schema or one taking it.
    private static void RenameLent(GesModelLayout layout)
    {
        foreach (var protocol in layout.Protocols)
        {
            var owners = new HashSet<string>(StringComparer.Ordinal) { protocol.Name };
            for (var grew = true; grew;)
            {
                grew = false;
                foreach (var other in layout.Protocols)
                    if (!owners.Contains(other.Name) && other.Takes.Exists(owners.Contains)) grew |= owners.Add(other.Name);
                foreach (var type in layout.Types)
                    if (!owners.Contains(type.Name) && type.Takes.Exists(owners.Contains)) grew |= owners.Add(type.Name);
            }

            foreach (var field in protocol.Fields)
            {
                if (!owners.Contains(GesSchemaPlanner.Pascal(field.Name))) continue;
                field.Renamed = true;
                foreach (var type in layout.Types)
                    if (owners.Contains(type.Name))
                        foreach (var taken in type.Fields)
                            if (taken.Name == field.Name) taken.Renamed = true;
            }
        }
    }

    private static void Protocols(GesSchema schema, SortedDictionary<string, GesSchema> protocols)
    {
        foreach (var take in schema.Takes)
            if (take.IsUnchanged && !protocols.ContainsKey(take.Schema.Name))
            {
                protocols.Add(take.Schema.Name, take.Schema);
                Protocols(take.Schema, protocols);
            }
    }

    // Whether generated code names the field's type alike in every type: not a union, Flags or a closed Tag written in place.
    private static bool Shared(GesModelValue value)
    {
        switch (value.Kind)
        {
            case GesModelKind.Union:
                var present = value.Alternatives.FindAll(alternative => alternative.Value.Kind != GesModelKind.Nothing);
                return present.Count == 1 && Shared(present[0].Value);
            case GesModelKind.Flags:
                return false;
            case GesModelKind.Tag:
                return value.Tags.Count == 0 || value.TagSet.Length > 0;
            case GesModelKind.List:
                return value.Element is null || Shared(value.Element.Value);
            default:
                return true;
        }
    }

    private static void Reach(GesSchema schema, SortedDictionary<string, GesSchema> reached, SortedDictionary<string, GesHandleType> handles)
    {
        if (reached.TryGetValue(schema.Name, out var known))
        {
            // Model types share one namespace: a message beside a schema of its name has no second type name.
            if (ReferenceEquals(known, schema)) return;
            throw new GesGeneratorException(GesGeneratorException.NameCollision, $"The model types of {Describe(known)} and {Describe(schema)} are both named '{schema.Name}'; models take declared names without a message suffix, so put the roots that reach them into different model groups, generated into different namespaces.");
        }

        reached.Add(schema.Name, schema);
        foreach (var field in schema.Fields)
            foreach (var type in field.Alternatives) Reach(type, reached, handles);
        foreach (var member in schema.Members) Reach(member, reached, handles);
    }

    private static string Describe(GesSchema schema) => schema.Kind switch
    {
        GesSchemaKind.Message => "the message " + schema.Name,
        GesSchemaKind.Messages => "the message set " + schema.Name,
        _ => "the schema :" + schema.Name
    };

    private static void Reach(GesSchemaType type, SortedDictionary<string, GesSchema> reached, SortedDictionary<string, GesHandleType> handles)
    {
        if (type.Schema is { } schema) Reach(schema, reached, handles);
        if (type.HandleType is { } handle) Reach(handle, handles);
        foreach (var element in type.Element) Reach(element, reached, handles);
    }

    // A handle type brings its `<Name>Handle` and a composite's root handle type, whose `<Name>Handle` it holds. It never brings
    // its state schema: the generated handle names the state in documentation only, so the state is a model type only where
    // a root reaches it otherwise, as in the wire layout.
    private static void Reach(GesHandleType handle, SortedDictionary<string, GesHandleType> handles)
    {
        if (handles.ContainsKey(handle.Name)) return;
        handles.Add(handle.Name, handle);
        if (handle.Root is { } root) Reach(root, handles);
    }

    private static GesModelField Field(string name, string path, GesReadOnlyArray<GesSchemaType> alternatives)
    {
        var field = new GesModelField { Name = name, Path = path };
        if (alternatives.Count == 1)
        {
            field.Value = Value(alternatives[0], path);
            return field;
        }

        field.Value = new GesModelValue { Kind = GesModelKind.Union };
        foreach (var alternative in alternatives)
        {
            var type = alternative.Kind switch
            {
                GesSchemaTypeKind.Reference => alternative.Schema!.Name,
                GesSchemaTypeKind.Handle => "@" + alternative.HandleType!.Name,
                _ => alternative.Kind.ToString()
            };
            var label = alternative.Label.Length > 0 ? alternative.Label : type.TrimStart('@');
            field.Value.Alternatives.Add(new GesModelField { Name = label, Path = path + ":" + type, Value = Value(alternative, path + ":" + type) });
        }

        return field;
    }

    private static GesModelValue Value(GesSchemaType type, string path)
    {
        var value = new GesModelValue();
        switch (type.Kind)
        {
            case GesSchemaTypeKind.Boolean:
                value.Kind = GesModelKind.Boolean;
                break;
            case GesSchemaTypeKind.Number or GesSchemaTypeKind.Percentage or GesSchemaTypeKind.Quantity:
                value.Kind = GesModelKind.Number;
                value.Type = GesSchemaLayoutBuilder.TypeName(type.Kind);
                value.Unit = GesSchemaLayoutBuilder.UnitName(type.Kind, type.Unit);
                value.Integer = type.IsInteger;
                value.Wrapping = type.IsWrapping;
                value.Lower = Bound(type.Lower);
                value.Upper = Bound(type.Upper);
                value.Step = Bound(type.Step);
                break;
            case GesSchemaTypeKind.Tag:
                value.Kind = GesModelKind.Tag;
                value.Tags.AddRange(type.Tags);
                value.TagSet = type.TagSet;
                value.Max = type.Max;
                break;
            case GesSchemaTypeKind.Flags:
                value.Kind = GesModelKind.Flags;
                value.Tags.AddRange(type.Tags);
                break;
            case GesSchemaTypeKind.Text:
                value.Kind = GesModelKind.Text;
                value.Max = type.Max;
                break;
            case GesSchemaTypeKind.Bytes:
                value.Kind = GesModelKind.Bytes;
                value.Max = type.Max;
                break;
            case GesSchemaTypeKind.List when type.Element.Count > 0:
                value.Kind = GesModelKind.List;
                value.Max = type.Max;
                value.Fixed = type.IsExact;
                value.Depth = type.Depth;
                value.Element = Field("[]", path + "[]", type.Element);
                break;
            case GesSchemaTypeKind.List:
                value.Kind = GesModelKind.Values;
                break;
            case GesSchemaTypeKind.Map when type.HandleType is { } key:
                value.Kind = GesModelKind.HandleMap;
                value.Handle = key.Name;
                value.Max = type.Max;
                value.Element = Field("[]", path + "[].value", type.Element);
                break;
            case GesSchemaTypeKind.Map:
                value.Kind = GesModelKind.Map;
                break;
            case GesSchemaTypeKind.Handle:
                value.Kind = GesModelKind.Handle;
                value.Handle = type.HandleType!.Name;
                value.Versioned = type.IsVersioned;
                break;
            case GesSchemaTypeKind.Dice:
                value.Kind = GesModelKind.Dice;
                value.Max = type.Max;
                value.Lower = Bound(type.Lower);
                value.Upper = Bound(type.Upper);
                break;
            case GesSchemaTypeKind.Point or GesSchemaTypeKind.Vector or GesSchemaTypeKind.Box or GesSchemaTypeKind.Poly or GesSchemaTypeKind.Disc:
                value.Kind = type.Kind switch
                {
                    GesSchemaTypeKind.Point => GesModelKind.Point,
                    GesSchemaTypeKind.Vector => GesModelKind.Vector,
                    GesSchemaTypeKind.Box => GesModelKind.Box,
                    GesSchemaTypeKind.Disc => GesModelKind.Disc,
                    _ => GesModelKind.Poly
                };
                value.Unit = GesSchemaLayoutBuilder.UnitName(type.Kind, type.Unit);
                value.Max = type.Max;
                foreach (var axis in type.Axes) value.Axes.Add(axis.Name);
                break;
            case GesSchemaTypeKind.Reference:
                value.Kind = GesModelKind.Schema;
                value.Schema = type.Schema!.Name;
                value.Packed = type.Schema.PackedWidth > 0;
                break;
            case GesSchemaTypeKind.Nothing:
                value.Kind = GesModelKind.Nothing;
                break;
            default:
                throw new GesGeneratorException(GesGeneratorException.Unsupported, $"A {type.Kind} at '{path}' has no model type.");
        }

        return value;
    }

    private static string? Bound(GesSchemaBound? bound)
    {
        if (bound is not { } value) return null;
        return value.IsParameter ? value.Parameter : value.Significand.ToString(CultureInfo.InvariantCulture) + "e" + value.Exponent.ToString(CultureInfo.InvariantCulture);
    }
}

// The canonical JSON form of a model layout, in the manner of the generation layout.
internal static class GesModelLayoutJson
{
    internal static string Write(GesModelLayout layout)
    {
        var json = new GesSchemaLayoutJson.Writer();
        json.BeginObject();
        json.Number("model", GesModelLayout.Version);
        json.Strings("roots", layout.Roots);
        json.BeginArray("types");
        // Only a generation with a model set tells its kinds of types apart, so that other layouts stay unchanged.
        var kinds = layout.Types.Exists(type => type.Kind == GesSchemaKind.Messages);
        foreach (var type in layout.Types)
        {
            json.BeginObject();
            json.String("name", type.Name);
            if (kinds) json.String("kind", type.Kind switch { GesSchemaKind.Message => "message", GesSchemaKind.Messages => "messages", _ => "schema" });
            json.String("hash", GesSchemaLayoutJson.Hex(type.Hash));
            if (type.Packed?.Type(type.Name) is { } packed)
            {
                json.Number("packedWidth", packed.PackedWidth);
                json.Number("packedBits", packed.PackedBits);
            }

            if (type.Kind != GesSchemaKind.Messages || type.Fields.Count > 0)
            {
                json.BeginArray("fields");
                foreach (var field in type.Fields) Field(json, field, member: true);
                json.EndArray();
            }

            if (type.Kind == GesSchemaKind.Message) json.Strings("tags", type.Tags);
            if (type.Kind == GesSchemaKind.Messages) json.Strings("members", type.Members);
            GesSchemaLayoutJson.Constants(json, type.Constants);
            GesSchemaLayoutJson.Computed(json, type.Computed);
            if (type.Takes.Count > 0) json.Strings("takes", type.Takes);
            if (type.Conversions.Count > 0)
            {
                json.BeginArray("conversions");
                foreach (var conversion in type.Conversions)
                {
                    json.BeginObject();
                    json.String("source", conversion.Source);
                    json.Strings("paths", conversion.Paths.ConvertAll(path => string.Join(".", path)));
                    json.EndObject();
                }

                json.EndArray();
            }

            json.EndObject();
        }

        json.EndArray();
        if (layout.Protocols.Count > 0)
        {
            json.BeginArray("protocols");
            foreach (var protocol in layout.Protocols)
            {
                json.BeginObject();
                json.String("name", protocol.Name);
                json.Boolean("abstract", protocol.Abstract);
                json.Strings("takes", protocol.Takes);
                json.BeginArray("fields");
                foreach (var field in protocol.Fields) Field(json, field, member: true);
                json.EndArray();
                json.EndObject();
            }

            json.EndArray();
        }

        // Only a model that reaches handle types names them, so that other layouts stay unchanged.
        if (layout.Handles.Count > 0)
        {
            json.BeginArray("handles");
            foreach (var handle in layout.Handles)
            {
                json.BeginObject();
                json.String("name", handle.Name);
                json.String("hash", GesSchemaLayoutJson.Hex(handle.Hash));
                json.String("state", handle.State);
                json.String("version", GesLayoutHandle.VersionName(handle.Version));
                if (handle.Root is { } root)
                {
                    json.String("root", root);
                    json.Key("part");
                    Key(json, handle.Part);
                }
                else
                {
                    if (handle.PartlessRoot is { } partless) json.String("root", partless);
                    json.Key("id");
                    Key(json, handle.Id);
                }

                if (handle.Projections.Count > 0) json.Strings("projections", handle.Projections);
                json.EndObject();
            }

            json.EndArray();
        }

        json.EndObject();
        return json.ToString();
    }

    private static void Key(GesSchemaLayoutJson.Writer json, GesHandleKey key)
    {
        json.BeginValueObject();
        if (key.IsText)
        {
            json.String("kind", "text");
            json.Number("max", key.MaxLength);
        }
        else
        {
            json.String("kind", "integer");
            json.Number("lower", key.Minimum);
            json.Number("upper", key.Maximum);
        }

        json.EndObject();
    }

    private static void Field(GesSchemaLayoutJson.Writer json, GesModelField field, bool member, bool value = false)
    {
        if (value) json.BeginValueObject();
        else json.BeginObject();
        json.String("name", field.Name);
        json.String("path", field.Path);
        if (member)
        {
            json.Boolean("optional", field.Optional);
            json.Boolean("key", field.Key);
            if (field.Unlabeled) json.Boolean("unlabeled", true);
            field.Default?.Write(json);
        }

        json.Key("type");
        Value(json, field.Value);
        json.EndObject();
    }

    private static void Value(GesSchemaLayoutJson.Writer json, GesModelValue value)
    {
        json.BeginValueObject();
        json.String("kind", Kind(value.Kind));
        switch (value.Kind)
        {
            case GesModelKind.Number:
                json.String("type", value.Type);
                json.String("unit", value.Unit);
                json.Boolean("integer", value.Integer);
                json.Boolean("wrapping", value.Wrapping);
                Optional(json, "lower", value.Lower);
                Optional(json, "upper", value.Upper);
                Optional(json, "step", value.Step);
                break;
            case GesModelKind.Tag:
                json.Strings("tags", value.Tags);
                if (value.TagSet.Length > 0) json.String("tagSet", value.TagSet);
                Optional(json, "max", value.Max);
                break;
            case GesModelKind.Flags:
                json.Strings("tags", value.Tags);
                break;
            case GesModelKind.Text or GesModelKind.Bytes:
                Optional(json, "max", value.Max);
                break;
            case GesModelKind.List:
                Optional(json, "max", value.Max);
                if (value.Fixed) json.Boolean("fixed", true);
                if (value.Depth > 0) json.Number("depth", value.Depth);
                json.Key("element");
                Field(json, value.Element!, member: false, value: true);
                break;
            case GesModelKind.Dice:
                Optional(json, "max", value.Max);
                Optional(json, "lower", value.Lower);
                Optional(json, "upper", value.Upper);
                break;
            case GesModelKind.Point or GesModelKind.Vector or GesModelKind.Box or GesModelKind.Disc:
                json.String("unit", value.Unit);
                json.Strings("axes", value.Axes);
                break;
            case GesModelKind.Poly:
                json.String("unit", value.Unit);
                json.Strings("axes", value.Axes);
                Optional(json, "max", value.Max);
                break;
            case GesModelKind.Schema:
                json.String("name", value.Schema);
                break;
            case GesModelKind.Union:
                json.BeginArray("alternatives");
                foreach (var alternative in value.Alternatives) Field(json, alternative, member: false);
                json.EndArray();
                break;
            case GesModelKind.Handle:
                json.String("name", value.Handle);
                json.Boolean("versioned", value.Versioned);
                break;
            case GesModelKind.HandleMap:
                json.String("key", value.Handle);
                Optional(json, "max", value.Max);
                json.Key("value");
                Field(json, value.Element!, member: false, value: true);
                break;
        }

        json.EndObject();
    }

    private static void Optional(GesSchemaLayoutJson.Writer json, string key, string? value)
    {
        if (value is null) json.Null(key);
        else json.String(key, value);
    }

    private static void Optional(GesSchemaLayoutJson.Writer json, string key, long? value)
    {
        if (value is { } number) json.Number(key, number);
        else json.Null(key);
    }

    internal static string Kind(GesModelKind kind) => kind switch
    {
        GesModelKind.Boolean => "boolean",
        GesModelKind.Number => "number",
        GesModelKind.Tag => "tag",
        GesModelKind.Flags => "flags",
        GesModelKind.Text => "text",
        GesModelKind.Bytes => "bytes",
        GesModelKind.List => "list",
        GesModelKind.Dice => "dice",
        GesModelKind.Point => "point",
        GesModelKind.Vector => "vector",
        GesModelKind.Box => "box",
        GesModelKind.Poly => "poly",
        GesModelKind.Disc => "disc",
        GesModelKind.Schema => "schema",
        GesModelKind.Union => "union",
        GesModelKind.Map => "map",
        GesModelKind.Values => "values",
        GesModelKind.Handle => "handle",
        GesModelKind.HandleMap => "handleMap",
        _ => "nothing"
    };
}
