// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Globalization;
using GameEventScript.Api;

namespace GameEventScript.Generator;

// Writes C# 9 for Unity and .NET: a struct per fixed-width schema and a class per schema with Text or List storage, with
// fields in wire order, Write and Read over the bit primitives, and constants inlined as literals or bit patterns.
internal static partial class GesCSharpEmitter
{
    internal static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const", "continue",
        "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern", "false", "finally",
        "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock", "long",
        "namespace", "new", "null", "object", "operator", "out", "override", "params", "private", "protected", "public",
        "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string", "struct", "switch",
        "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void",
        "volatile", "while"
    };

    // Members that converting to and from GES values adds.
    private static readonly string[] ValueReserved = ["ToValue", "ReadValue", "ToMessage", "ReadMessage", "ReadMessageLenient"];

    // Members every generated type declares, and those every C# object inherits.
    private static readonly string[] Reserved =
    [
        "SchemaHash", "BindingHash", "MaxByteCount", "Write", "Read", "ReadKey", "ToBytes", "ByteCount", "WriteDelta", "ToDeltaBytes", "DeltaByteCount", "ReadDelta", "WireEquals", "CopyFrom", "Quantize", "Equals", "GetHashCode",
        "ToString", "GetType"
    ];

    internal static GesCodeDocument Emit(GesSchemaLayout layout, List<GesPlanType> types, GesSchemaGenerateOptions options, GesModelLayout? packed, GesModelLayout? modelLayout)
    {
        var access = options.Access == GesSchemaAccess.Public ? "public" : "internal";
        var space = GesSchemaPlanner.Namespace(options.Namespace, Keywords);
        var prologue = new GesSchemaPlanner.Code();
        prologue.Blank();
        prologue.Line("#nullable enable");
        prologue.Blank();
        prologue.Line("using System;");
        prologue.Line("using System.Collections.Generic;");
        prologue.Line("using GameEventScript.Api;");
        if (layout.Networks.Count > 0) prologue.Line("using GameEventScript.Network;");
        prologue.Blank();
        var header = GesSchemaPlanner.Header(layout);
        var schemaSet = GesSchemaSetReference.From(options);
        var holder = schemaSet?.Qualified ?? "__GesSchemas" + GesSchemaPlanner.Suffix(header, options);
        var context = new Context(GesSchemaPlanner.KeyedElements(types), "global::" + string.Concat(space.ConvertAll(part => part + ".")), options.Values, holder)
        {
            Computed = GesSchemaPlanner.Computed(types, GesComputedEmitter.Helper(options.Namespace, GesSchemaPlanner.Suffix(header, options)), swift: false)
        };
        context.Layout = layout;
        var document = new GesCodeDocument { Header = header, Prologue = prologue.ToString(), Space = space, Parameters = layout.Parameters.Count > 0 };
        // Packed records are the model types over one word, the shared ones of the model namespace or declared here.
        var models = options.Models is { } shared ? "global::" + string.Concat(GesSchemaPlanner.Namespace(shared, Keywords).ConvertAll(part => part + ".")) : context.Global;
        foreach (var type in types)
            foreach (var field in type.Fields) GesSchemaPlanner.QualifyPacked(field.Value, models);
        // Handle types are the models' where they share a namespace, else declared here.
        var handles = options.Models is not null ? models : context.Global;
        foreach (var type in types)
            foreach (var field in type.Fields) GesSchemaPlanner.QualifyHandles(field.Value, handles);
        if (options.Models is null)
        {
            var declared = layout.Handles.ConvertAll(handle => handle.Model);
            foreach (var handle in layout.Handles) document.Units.Add(new GesCodeUnit(GesModelPlanner.HandleName(handle.Name), code => GesCSharpModelEmitter.Handle(code, declared, handle.Model, access), handle: handle.Name));
        }

        if (modelLayout is not null) context.Models = new ModelConversion(modelLayout, models);
        foreach (var type in types)
        {
            if (type.Layout.PackedWidth > 0)
            {
                var root = layout.Roots.Contains(type.Name);
                if (packed is not null)
                    document.Units.Add(new GesCodeUnit(type.Name, code => GesCSharpModelEmitter.PackedType(code, packed, type.Name, access, context.Global, root ? members => PackedRoot(members, type, context) : null)));
                else if (root)
                    document.Units.Add(new GesCodeUnit(type.Name, code =>
                    {
                        // A root shared with models gets its wire members as extension methods of the model's struct.
                        code.Line($"/// <summary>The wire members of the packed record <c>:{type.Layout.Declared}</c>, a wire root whose type the models declare: its bytes are its word, little-endian.</summary>");
                        code.Line($"{access} static class {type.Name}Wire");
                        code.Open("{");
                        PackedRoot(code, type, context, models + type.Name);
                        code.Close();
                    }));
                continue;
            }

            document.Units.Add(new GesCodeUnit(type.Name, code =>
            {
                if (type.Kind == GesSchemaKind.Messages) MessageSet(code, type, access, context);
                else Record(code, type, access, context);
            }));
        }
        var sets = GesSchemaPlanner.TagSets(types, context.Global);
        foreach (var (name, tags) in sets) document.Units.Add(new GesCodeUnit(name, code => TagSetDeclaration(code, name, tags, access)));
        foreach (var network in layout.Networks) document.Units.Add(new GesCodeUnit(network.Name, code => Network(code, network, access, schemaSet?.Qualified, root =>
        {
            // A packed root that the models declare is their struct; its wire members extend it.
            var type = types.Find(candidate => candidate.Name == root)!;
            var shared = type.Layout.PackedWidth > 0 && packed is null;
            return new NetworkRoot((shared ? models : context.Global) + root, context.Global + root + (shared ? "Wire" : ""), type);
        })));
        context.TopLevel = GesSchemaPlanner.TopLevel(types, sets, layout.Networks);
        if (options.Models is null)
            foreach (var handle in layout.Handles) context.TopLevel.Add(GesModelPlanner.HandleName(handle.Name));
        if (options.Models is not null) GesSchemaPlanner.ReserveSharedRoots(layout, types, context.TopLevel);
        schemaSet?.Reserve(context.TopLevel);
        GesSchemaPlanner.CheckTopLevel(context.TopLevel, swift: false);
        foreach (var network in layout.Networks) GesNetworkEndPlan.CheckNested(network, context.TopLevel, swift: false);
        if (schemaSet?.Declared is { } setName)
            document.Units.Add(new GesCodeUnit("", code => GesSchemaSetReference.WriteCSharp(code, setName, access, layout.Sources, layout.Parameters, layout.Networks.Count > 0)));
        else if (options.Values && schemaSet is null) document.Units.Add(new GesCodeUnit("", code => ValuesHolder(code, layout, holder, options.Split != GesSchemaSplit.None)));
        if (layout.Types.Exists(type => type.Computed.Length > 0))
            document.Units.Add(new GesCodeUnit("", code => GesComputedLibrary.Write(code, context.Computed.HelperName, swift: false, context.Computed.NeedsValues(types.ConvertAll(type => (type.Name, type.Layout.Computed))), GesComputedEmitter.UsesMath(types.ConvertAll(type => type.Layout.Computed)), GesComputedEmitter.UsesSpatial(types.ConvertAll(type => type.Layout.Computed)))));
        return document;
    }

    // The keyed-list elements and the qualifier of the top-level types, which a member of the same name cannot hide.
    private sealed class Context(HashSet<string> elements, string global, bool values, string holder)
    {
        internal GesSchemaLayout Layout { get; set; } = null!;

        internal HashSet<string> Elements { get; } = elements;

        internal string Global { get; } = global;

        // Whether records convert to and from GES values through the helper type that holds the embedded sources.
        internal bool Values { get; } = values;

        internal string Holder { get; } = holder;

        internal GesComputedEmitter Computed { get; set; } = null!;

        // The records and tag sets declared beside each other, which a record's nested type must not hide.
        internal HashSet<string> TopLevel { get; set; } = null!;

        // The models that records convert to and from directly, null without `--models`.
        internal ModelConversion? Models { get; set; }
    }

    private static string Kind(GesPlanType type) => type.Kind == GesSchemaKind.Message ? "message" : "schema";

    // A message name stands without the colon that marks schemas.
    private static string Written(GesPlanType type) => type.Kind == GesSchemaKind.Message ? type.Layout.Declared : ":" + type.Layout.Declared;

    private static void Record(GesSchemaPlanner.Code code, GesPlanType type, string access, Context context)
    {
        var names = new List<(string, string)> { (type.Name, "the type itself") };
        if (!type.IsItem)
            foreach (var reserved in Reserved) names.Add((reserved, "the generated member " + reserved));
        if (!type.IsItem && context.Values)
            foreach (var reserved in ValueReserved) names.Add((reserved, "the generated member " + reserved));
        FieldNames(type, names);
        foreach (var (tag, name, _) in type.Tags) names.Add((name, "the tag #" + tag));
        if (!type.IsItem)
            foreach (var constant in type.Layout.Constants) names.Add((GesSchemaPlanner.Pascal(constant.Name), "the let " + constant.Name));
        if (!type.IsItem) GesComputedEmitter.Names(type.Layout.Computed, type.Name, swift: false, names);
        var model = type.IsItem ? null : context.Models?.Type(type);
        if (model is not null) names.Add(("ToModel", "the generated member ToModel"));
        GesSchemaPlanner.Unique(type.Name, names, context.TopLevel);

        var visibility = type.IsItem ? "public" : access;
        code.Line(type.IsItem ? "/// <summary>An element of the list.</summary>" : $"/// <summary>The {Kind(type)} <c>{Written(type)}</c>, read and written without allocation{(type.IsClass ? " once its storage has grown" : "")}.</summary>");
        code.Line(type.IsClass ? $"{visibility} sealed partial class {type.Name}" : $"{visibility} partial struct {type.Name}");
        code.Open("{");
        if (!type.IsItem)
        {
            Constants(code, type.Layout);
            Paths(code, type);
        }

        var first = type.IsItem;
        foreach (var field in type.Fields)
        {
            Members(code, field, first);
            first = false;
        }

        foreach (var (tag, name, _) in type.Tags)
        {
            code.Blank();
            code.Line($"/// <summary>Whether the tag <c>#{tag}</c> is set.</summary>");
            code.Line($"public bool {name};");
        }

        ArgumentInitializer(code, type);
        foreach (var field in type.Fields) ListAccess(code, field);
        if (!type.IsItem) context.Computed.Members(code, type.Name, type.Layout.Computed, "public");
        Methods(code, type);
        if (type.IsClass && (type.Kind == GesSchemaKind.Schema || type.IsItem)) Sharing(code, type);
        var deltas = type.Kind == GesSchemaKind.Schema && !type.IsItem;
        if (deltas) SameHelpers(code, type, context.Global);
        Extras(code, type, deltas, context.Elements.Contains(type.Name));
        if (context.Values && !type.IsItem) ValueMembers(code, type, context.Holder, Lenient(type.Layout, context.Layout));
        if (model is not null) ModelMembers(code, type, model, context.Models!);
        foreach (var field in type.Fields) Declarations(code, field.Value, access, context);
        code.Close();
    }

    // The paths of a type's nodes in node order, which errors name.
    private static void Paths(GesSchemaPlanner.Code code, GesPlanType type)
    {
        code.Blank();
        code.Line("internal static readonly string[] __paths =");
        code.Open("{");
        foreach (var path in type.Paths) code.Line(Quote(path) + ",");
        code.Close("};");
    }

    // The members that a type's fields declare, for the collision check.
    private static void FieldNames(GesPlanType type, List<(string, string)> names)
    {
        foreach (var field in type.Fields)
        {
            names.Add((field.Base, "the field " + field.Layout.Name));
            if (field.Optional) names.Add(("Has" + field.Base, "the presence of " + field.Layout.Name));
            if (field.Optional && Plain(field.Value)) names.Add((field.Base + "Optional", "the optional value of " + field.Layout.Name));
            Nested(field.Value, names, field.Layout.Name);
            if (IsPooledList(field)) names.Add(("Set" + field.Base + "Count", "the element count of " + field.Layout.Name));
            if (IsTextList(field)) names.Add((field.Base + "Values", "the texts of " + field.Layout.Name));
        }
    }

    private static void Constants(GesSchemaPlanner.Code code, GesLayoutType layout)
    {
        code.Line("/// <summary>The schema hash, independent of parameter values.</summary>");
        code.Line($"public const ulong SchemaHash = 0x{GesSchemaLayoutJson.Hex(layout.Hash)}UL;");
        code.Blank();
        code.Line("/// <summary>The hash over the schema hash and the parameter values this code was generated with.</summary>");
        code.Line($"public const ulong BindingHash = 0x{GesSchemaLayoutJson.Hex(layout.BindingHash)}UL;");
        code.Blank();
        code.Line("/// <summary>The size of the largest encoding in bytes.</summary>");
        code.Line($"public const int MaxByteCount = {layout.MaxByteCount.ToString(CultureInfo.InvariantCulture)};");
        Lets(code, layout.Constants);
    }

    // The lets of a schema, in base units; a Percentage holds its ratio.
    internal static void Lets(GesSchemaPlanner.Code code, List<GesLayoutConstant> constants)
    {
        foreach (var constant in constants)
        {
            code.Blank();
            code.Line($"/// <summary><c>{GesSchemaPlanner.ConstantSource(constant)}</c></summary>");
            code.Line($"public const {(constant.Integer ? "long" : "double")} {GesSchemaPlanner.Pascal(constant.Name)} = {GesSchemaPlanner.ConstantValue(constant)};");
        }
    }

    // Whether an optional value also has a nullable view: a value type without storage.
    private static bool Plain(GesPlanValue value) => !value.IsClass && value.Encoding.Kind is not (GesLayoutKind.Union or GesLayoutKind.Text or GesLayoutKind.Bytes or GesLayoutKind.List);

    // The nested types and members a value declares, for the collision check.
    private static void Nested(GesPlanValue value, List<(string, string)> names, string field)
    {
        var name = NestedName(value);
        if (name.Length > 0 && value.TagType.Length == 0) names.Add((name, GesSchemaPlanner.NestedTypeOrigin + "of " + field));
        switch (value.Encoding.Kind)
        {
            case GesLayoutKind.Text:
                names.Add((value.Base + "Utf8", "the UTF-8 bytes of " + field));
                names.Add(("Set" + value.Base + "Utf8", "the UTF-8 setter of " + field));
                break;
            case GesLayoutKind.Bytes:
                names.Add(("Set" + value.Base, "the setter of " + field));
                break;
            case GesLayoutKind.List:
                if (value.Encoding.Poly) names.Add((value.Base + "Poly", "the polygon view of " + field));
                if (value.Item is not null) names.Add((value.Item.Name, GesSchemaPlanner.NestedTypeOrigin + "of the elements of " + field));
                else Nested(value.Element!, names, field);
                break;
            case GesLayoutKind.Union:
                foreach (var alternative in value.Alternatives)
                {
                    if (alternative.Encoding.Kind != GesLayoutKind.Nothing) names.Add((alternative.Base, $"the alternative {alternative.Label} of {field}"));
                    Nested(alternative, names, field);
                }

                break;
        }
    }

    private static string NestedName(GesPlanValue value) => value.Encoding.Kind switch
    {
        GesLayoutKind.Tag => value.TagType.Length > 0 ? value.TagType : value.Base + "Tag",
        GesLayoutKind.Flags => value.Base + "Flags",
        GesLayoutKind.Union => value.Base + "Alternative",
        _ => ""
    };

    private static string TypeName(GesPlanValue value) => value.Encoding.Kind switch
    {
        GesLayoutKind.Boolean => "bool",
        GesLayoutKind.Integer => "long",
        GesLayoutKind.Quantized or GesLayoutKind.Float => "double",
        GesLayoutKind.Schema => value.Packed?.Type ?? value.Encoding.Schema,
        GesLayoutKind.Text => "string",
        GesLayoutKind.Bytes => "byte[]",
        GesLayoutKind.Point => "GesPoint",
        GesLayoutKind.Vector => "GesVector",
        GesLayoutKind.Box => "GesBox",
        GesLayoutKind.Disc => "GesDisc",
        GesLayoutKind.Handle => value.HandleType,
        GesLayoutKind.List => $"List<{ElementName(value)}>",
        _ => NestedName(value)
    };

    private static string ElementName(GesPlanValue list) => list.Item is not null ? list.Item.Name : TypeName(list.Element!);

    // A class reference starts as an instance, so that a record can be filled without allocating later.
    private static string Initializer(GesPlanValue value) => value.Encoding.Kind switch
    {
        GesLayoutKind.Schema when value.IsClass => " = new " + value.Encoding.Schema + "();",
        GesLayoutKind.List => $" = new List<{ElementName(value)}>();",
        _ => ";"
    };

    // A Bytes value grows its storage as a Text does, without UTF-8 conveniences.
    private static void BytesMember(GesSchemaPlanner.Code code, GesPlanValue value, string summary)
    {
        var storage = "__" + value.Base;
        var max = value.Encoding.MaxCount.ToString(CultureInfo.InvariantCulture);
        code.Line($"private byte[] {storage} = global::System.Array.Empty<byte>();");
        code.Line($"private int {storage}Length;");
        code.Blank();
        code.Line($"/// <summary>{summary}, at most {max} bytes.</summary>");
        code.Line($"public ReadOnlySpan<byte> {value.Base} => new ReadOnlySpan<byte>({storage}, 0, {storage}Length);");
        code.Blank();
        code.Line($"/// <summary>Sets {summary.Substring(0, 1).ToLowerInvariant() + summary.Substring(1)}; its length is checked when the record is written.</summary>");
        code.Line("/// <param name=\"bytes\">The bytes.</param>");
        code.Line($"public void Set{value.Base}(ReadOnlySpan<byte> bytes)");
        code.Open("{");
        code.Line($"if ({storage}.Length < bytes.Length) global::System.Array.Resize(ref {storage}, global::System.Math.Max(bytes.Length, global::System.Math.Min({max}, {storage}.Length * 2)));");
        code.Line($"bytes.CopyTo({storage});");
        code.Line($"{storage}Length = bytes.Length;");
        code.Close();
    }

    private static void Members(GesSchemaPlanner.Code code, GesPlanField field, bool first)
    {
        if (!first) code.Blank();
        if (field.Optional)
        {
            code.Line($"/// <summary>Whether <c>{field.Layout.Name}</c> is present.</summary>");
            code.Line($"public bool Has{field.Base};");
            code.Blank();
        }

        Member(code, field.Value, $"The field <c>{field.Layout.Name}</c>");
        if (field.Optional && Plain(field.Value))
        {
            var type = TypeName(field.Value);
            code.Blank();
            code.Line($"/// <summary>The field <c>{field.Layout.Name}</c> as a nullable value, null when absent.</summary>");
            code.Line($"public {type}? {field.Base}Optional");
            code.Open("{");
            code.Line($"get => Has{field.Base} ? {field.Base} : ({type}?)null;");
            code.Line("set");
            code.Open("{");
            code.Line($"Has{field.Base} = value.HasValue;");
            code.Line($"{field.Base} = value.GetValueOrDefault();");
            code.Close();
            code.Close();
        }

        if (field.Value.Encoding.Kind != GesLayoutKind.Union) return;
        foreach (var alternative in field.Value.Alternatives)
        {
            if (alternative.Encoding.Kind == GesLayoutKind.Nothing) continue;
            code.Blank();
            Member(code, alternative, $"The value of <c>{field.Layout.Name}</c> when its alternative is <c>{alternative.Label}</c>");
        }
    }

    // Declares the member that stores a value, with its storage and conveniences.
    private static void Member(GesSchemaPlanner.Code code, GesPlanValue value, string summary)
    {
        if (value.Encoding.Kind == GesLayoutKind.Bytes)
        {
            BytesMember(code, value, summary);
            return;
        }

        if (value.Encoding.Kind != GesLayoutKind.Text)
        {
            code.Line($"/// <summary>{summary}{Description(value)}.</summary>");
            code.Line($"public {TypeName(value)} {value.Base}{Initializer(value)}");
            if (!value.Encoding.Poly) return;
            code.Blank();
            code.Line($"/// <summary>{summary} as a polygon; reading it allocates.</summary>");
            code.Line($"public GesPoly {value.Base}Poly => new GesPoly({value.Base});");
            return;
        }

        var storage = "__" + value.Base;
        var max = value.Encoding.MaxCount.ToString(CultureInfo.InvariantCulture);
        code.Line($"private byte[] {storage} = global::System.Array.Empty<byte>();");
        code.Line($"private int {storage}Length;");
        code.Blank();
        code.Line($"/// <summary>{summary} as UTF-8 bytes{(value.Encoding.TagName ? ", a tag name without #" : "")}.</summary>");
        code.Line($"public ReadOnlySpan<byte> {value.Base}Utf8 => new ReadOnlySpan<byte>({storage}, 0, {storage}Length);");
        code.Blank();
        code.Line($"/// <summary>Sets {summary.Substring(0, 1).ToLowerInvariant() + summary.Substring(1)} from UTF-8 bytes, which are checked when the record is written.</summary>");
        code.Line("/// <param name=\"utf8\">The bytes.</param>");
        code.Line($"public void Set{value.Base}Utf8(ReadOnlySpan<byte> utf8)");
        code.Open("{");
        code.Line($"if ({storage}.Length < utf8.Length) global::System.Array.Resize(ref {storage}, global::System.Math.Max(utf8.Length, global::System.Math.Min({max}, {storage}.Length * 2)));");
        code.Line($"utf8.CopyTo({storage});");
        code.Line($"{storage}Length = utf8.Length;");
        code.Close();
        code.Blank();
        code.Line($"/// <summary>{summary} as a string; reading and setting it allocate.</summary>");
        code.Line($"public string {value.Base}");
        code.Open("{");
        code.Line($"get => global::System.Text.Encoding.UTF8.GetString({storage}, 0, {storage}Length);");
        code.Line($"set => Set{value.Base}Utf8(global::System.Text.Encoding.UTF8.GetBytes(value));");
        code.Close();
    }

    private static string Description(GesPlanValue value)
    {
        var encoding = value.Encoding;
        return encoding.Kind switch
        {
            GesLayoutKind.Quantized or GesLayoutKind.Float or GesLayoutKind.Integer when encoding.Unit != "none" => $", in {encoding.Unit}",
            GesLayoutKind.Point or GesLayoutKind.Vector or GesLayoutKind.Box or GesLayoutKind.Disc when encoding.Unit != "none" => $", in {encoding.Unit}",
            GesLayoutKind.List when encoding.Poly => $", the points of a polygon{(encoding.Unit != "none" ? " in " + encoding.Unit : "")}, at most {encoding.MaxCount.ToString(CultureInfo.InvariantCulture)}",
            GesLayoutKind.Union => ", the selected alternative",
            GesLayoutKind.List => $", {(encoding.Fixed ? "exactly" : "at most")} {encoding.MaxCount.ToString(CultureInfo.InvariantCulture)} elements",
            _ => ""
        };
    }

    private static void Methods(GesSchemaPlanner.Code code, GesPlanType type)
    {
        var self = type.IsClass ? "" : "readonly ";
        if (!type.IsItem)
        {
            code.Blank();
            PublicMethods(code, self, ["__WriteFields(ref writer, __paths, 0);"], ["__ReadFields(ref reader, __paths, 0);"], "record");
            var key = type.Fields.Count > 0 && type.Fields[0].Key ? type.Fields[0] : null;
            if (key is not null)
            {
                code.Blank();
                code.Line("/// <summary>Reads the key, which comes first, before a caller chooses a baseline.</summary>");
                code.Line("/// <param name=\"data\">The data.</param>");
                code.Line("/// <returns>The key.</returns>");
                code.Line("/// <exception cref=\"GesWireException\">The data ends before the key does or holds an invalid key.</exception>");
                code.Line($"public static {TypeName(key.Value)} ReadKey(ReadOnlySpan<byte> data)");
                code.Open("{");
                code.Line("var reader = new GesBitReader(data);");
                code.Line("var paths = __paths;");
                code.Line("const int at = 0;");
                code.Line($"{TypeName(key.Value)} key;");
                Read(code, key.Value, "key");
                code.Line("return key;");
                code.Close();
            }
        }

        FieldMethods(code, type);
    }

    // Writes and reads a type's fields in wire order, at the paths that start at `at`.
    private static void FieldMethods(GesSchemaPlanner.Code code, GesPlanType type)
    {
        var self = type.IsClass || type.Kind == GesSchemaKind.Messages && type.MembersAreClasses ? "" : "readonly ";
        code.Blank();
        code.Line($"internal {self}void __WriteFields(ref GesBitWriter writer, string[] paths, int at{LevelParameter(type)})");
        code.Open("{");
        foreach (var field in type.Fields)
        {
            if (field.Optional)
            {
                code.Line($"if (Has{field.Base})");
                code.Open("{");
                code.Line("writer.Write(1, 1);");
                Write(code, field.Value, field.Base);
                code.Close();
                code.Line("else writer.Write(0, 1);");
            }
            else Write(code, field.Value, field.Base);
        }

        foreach (var (_, name, _) in type.Tags) code.Line($"writer.Write({name} ? 1UL : 0UL, 1);");
        code.Close();
        code.Blank();
        code.Line($"internal void __ReadFields(ref GesBitReader reader, string[] paths, int at{LevelParameter(type)})");
        code.Open("{");
        foreach (var field in type.Fields)
        {
            if (field.Optional)
            {
                code.Line($"Has{field.Base} = reader.Read(1, {Path(field.Value)}) != 0;");
                code.Line($"if (Has{field.Base})");
                code.Open("{");
                Read(code, field.Value, field.Base);
                code.Close();
                if (Plain(field.Value)) code.Line($"else {field.Base} = default;");
            }
            else Read(code, field.Value, field.Base);
        }

        foreach (var (_, name, node) in type.Tags) code.Line($"{name} = reader.Read(1, paths[at + {node.ToString(CultureInfo.InvariantCulture)}]) != 0;");
        code.Close();
    }

    private static void PublicMethods(GesSchemaPlanner.Code code, string self, List<string> write, List<string> read, string what)
    {
        code.Line($"/// <summary>Writes this {what} into a buffer; <see cref=\"MaxByteCount\"/> bytes always suffice.</summary>");
        code.Line("/// <param name=\"buffer\">The buffer.</param>");
        code.Line("/// <returns>The number of bytes written.</returns>");
        code.Line("/// <exception cref=\"GesWireException\">A value does not fit its field, or the buffer is too small.</exception>");
        code.Line($"public {self}int Write(Span<byte> buffer)");
        code.Open("{");
        code.Line("var writer = new GesBitWriter(buffer);");
        foreach (var line in write) code.Line(line);
        code.Line("return writer.Finish();");
        code.Close();
        code.Blank();
        code.Line($"/// <summary>Counts the bytes that <see cref=\"Write\"/> writes for this {what}, without writing them.</summary>");
        code.Line("/// <returns>The exact byte count.</returns>");
        code.Line("/// <exception cref=\"GesWireException\">A value does not fit its field.</exception>");
        code.Line($"public {self}int ByteCount()");
        code.Open("{");
        code.Line("var writer = GesBitWriter.Counting();");
        foreach (var line in write) code.Line(line);
        code.Line("return writer.Finish();");
        code.Close();
        code.Blank();
        code.Line($"/// <summary>Writes this {what} into a new array of its exact size, which <see cref=\"ByteCount\"/> counts first.</summary>");
        code.Line("/// <returns>The bytes.</returns>");
        code.Line("/// <exception cref=\"GesWireException\">A value does not fit its field.</exception>");
        code.Line($"public {self}byte[] ToBytes()");
        code.Open("{");
        code.Line("var buffer = new byte[ByteCount()];");
        code.Line("Write(buffer);");
        code.Line("return buffer;");
        code.Close();
        code.Blank();
        code.Line($"/// <summary>Reads a {what} from untrusted data into this instance. After an error its values are unspecified.</summary>");
        code.Line("/// <param name=\"data\">The data; it may continue after the record.</param>");
        code.Line("/// <param name=\"byteLimit\">An optional limit on the bytes of this record.</param>");
        code.Line("/// <returns>The number of bytes the record occupies.</returns>");
        code.Line($"/// <exception cref=\"GesWireException\">The data is no valid {what}.</exception>");
        code.Line("public int Read(ReadOnlySpan<byte> data, long byteLimit = long.MaxValue)");
        code.Open("{");
        code.Line("var reader = new GesBitReader(data, byteLimit);");
        foreach (var line in read) code.Line(line);
        code.Line("return reader.Finish();");
        code.Close();
    }

    private static void MessageSet(GesSchemaPlanner.Code code, GesPlanType type, string access, Context context)
    {
        var names = new List<(string, string)>
        {
            (type.Name, "the type itself"), ("Selected", "the selected member"), ("Member", GesSchemaPlanner.NestedTypeOrigin + "of the members"),
            ("Case", GesSchemaPlanner.NestedTypeOrigin + "of the cases"), ("ToCase", "the generated member ToCase"), ("Assign", "the generated member Assign"),
            ("Value", "the value of a case")
        };
        foreach (var reserved in Reserved) names.Add((reserved, "the generated member " + reserved));
        if (context.Values)
            foreach (var reserved in ValueReserved) names.Add((reserved, "the generated member " + reserved));
        foreach (var member in type.MemberNames) names.Add((member, "the member " + member));
        FieldNames(type, names);
        GesSchemaPlanner.Unique(type.Name, names, context.TopLevel);
        var width = type.Layout.MemberWidth.ToString(CultureInfo.InvariantCulture);
        var last = Unsigned((ulong)type.Members.Count - 1);
        var parameters = type.Fields.Count > 0;
        code.Line($"/// <summary>The message set <c>{type.Layout.Declared}</c>: {(parameters ? "its parameters and " : "")}one of its messages, read and written without allocation{(type.MembersAreClasses ? " once its storage has grown" : "")}.</summary>");
        code.Line(type.MembersAreClasses ? $"{access} sealed partial class {type.Name}" : $"{access} partial struct {type.Name}");
        code.Open("{");
        Constants(code, type.Layout);
        if (parameters)
        {
            Paths(code, type);
            foreach (var field in type.Fields) Members(code, field, false);
            foreach (var field in type.Fields) ListAccess(code, field);
        }

        code.Blank();
        code.Line("/// <summary>The message this record holds.</summary>");
        code.Line("public Member Selected;");
        for (var index = 0; index < type.Members.Count; index++)
        {
            var (member, record) = (type.MemberNames[index], type.Members[index]);
            code.Blank();
            code.Line($"/// <summary>The message when <see cref=\"Selected\"/> is <c>{member}</c>.</summary>");
            code.Line($"public {record} {member}{(type.MembersAreClasses ? $" = new {record}();" : ";")}");
        }

        var write = new List<string> { $"if ((ulong)Selected > {last}) throw new GesWireException(GesWireException.OutOfRange, \"\");" };
        var read = new List<string>();
        if (parameters)
        {
            write.Add("__WriteFields(ref writer, __paths, 0);");
            read.Add("__ReadFields(ref reader, __paths, 0);");
        }

        write.AddRange([$"writer.Write((ulong)Selected, {width});", "switch (Selected)", "{"]);
        read.Add($"var index = reader.Read({width}, \"\");");
        if ((ulong)type.Members.Count - 1 < Full(type.Layout.MemberWidth)) read.Add($"if (index > {last}) throw new GesWireException(GesWireException.InvalidIndex, \"\");");
        read.AddRange(["Selected = (Member)index;", "switch (Selected)", "{"]);
        // The paths are static members of the message type, whose name differs from the member's with a message suffix.
        for (var index = 0; index < type.Members.Count; index++)
        {
            var (member, record) = (type.MemberNames[index], type.Members[index]);
            write.AddRange([$"    case Member.{member}:", $"        {member}.__WriteFields(ref writer, {record}.__paths, 0);", "        break;"]);
            read.AddRange([$"    case Member.{member}:", $"        {member}.__ReadFields(ref reader, {record}.__paths, 0);", "        break;"]);
        }

        write.Add("}");
        read.Add("}");
        code.Blank();
        PublicMethods(code, type.MembersAreClasses ? "" : "readonly ", write, read, "message");
        if (parameters) FieldMethods(code, type);
        if (context.Values) ValueMembers(code, type, context.Holder, Lenient(type.Layout, context.Layout));
        if (context.Models?.Type(type) is { } model) ModelMembers(code, type, model, context.Models);
        code.Blank();
        MessageCases(code, type, context);
        code.Blank();
        code.Line("/// <summary>The messages of the set, in declared order.</summary>");
        code.Line($"public enum Member : {Underlying(type.Members.Count)}");
        code.Open("{");
        foreach (var member in type.MemberNames)
        {
            code.Line($"/// <summary>The message <c>{member}</c>.</summary>");
            code.Line(member + ",");
        }

        code.Close();
        foreach (var field in type.Fields) Declarations(code, field.Value, access, context);
        code.Close();
    }

    // The cases of a message set, an abstract class with one sealed subclass per message for pattern matching, and the
    // conversions between them and the record. A typed end hands its cached cases, so receiving allocates nothing.
    private static void MessageCases(GesSchemaPlanner.Code code, GesPlanType type, Context context)
    {
        var self = type.MembersAreClasses ? "" : "readonly ";
        code.Blank();
        code.Line("/// <summary>Returns the selected message as a new case that holds the member record.</summary>");
        code.Line("/// <returns>The case.</returns>");
        code.Line("/// <exception cref=\"GesWireException\"><see cref=\"Selected\"/> names no message of the set.</exception>");
        code.Line($"public {self}Case ToCase() => Selected switch");
        code.Open("{");
        foreach (var member in type.MemberNames) code.Line($"Member.{member} => new Case.{member}({member}),");
        code.Line("_ => throw new GesWireException(GesWireException.OutOfRange, \"\"),");
        code.Close("};");
        code.Blank();
        code.Line("/// <summary>Selects the message of a case and takes its member record.</summary>");
        code.Line("/// <param name=\"value\">The case.</param>");
        code.Line("/// <exception cref=\"global::System.ArgumentNullException\"><paramref name=\"value\"/> is null.</exception>");
        code.Line("public void Assign(Case value)");
        code.Open("{");
        code.Line("switch (value)");
        code.Open("{");
        foreach (var member in type.MemberNames)
        {
            var local = GesSchemaPlanner.Camel(member);
            code.Line($"case Case.{member} @{local}:");
            code.Line($"    Selected = Member.{member};");
            code.Line($"    {member} = @{local}.Value;");
            code.Line("    break;");
        }

        code.Line("default:");
        code.Line("    throw new global::System.ArgumentNullException(nameof(value));");
        code.Close();
        code.Close();
        code.Blank();
        code.Line($"/// <summary>One message of the set as a case: match <c>case {type.Name}.Case.{type.MemberNames[0]} value</c>.</summary>");
        code.Line("public abstract class Case");
        code.Open("{");
        code.Line("private protected Case()");
        code.Open("{");
        code.Close();
        code.Blank();
        code.Line("/// <summary>Gets the message of the case.</summary>");
        code.Line("public abstract Member Selected { get; }");
        for (var index = 0; index < type.Members.Count; index++)
        {
            var (member, record) = (type.MemberNames[index], context.Global + type.Members[index]);
            code.Blank();
            code.Line($"/// <summary>The message <c>{member}</c>.</summary>");
            code.Line($"public sealed class {member} : Case");
            code.Open("{");
            code.Line("/// <summary>Creates the case with an empty record.</summary>");
            code.Line($"public {member}()");
            code.Open("{");
            code.Close();
            code.Blank();
            code.Line("/// <summary>Creates the case with a record.</summary>");
            code.Line("/// <param name=\"value\">The record.</param>");
            code.Line($"public {member}({record} value) => Value = value;");
            code.Blank();
            code.Line("/// <summary>The record of the message.</summary>");
            code.Line($"public {record} Value{(type.MembersAreClasses ? $" = new {record}();" : ";")}");
            code.Blank();
            code.Line("/// <inheritdoc/>");
            code.Line($"public override Member Selected => Member.{member};");
            code.Close();
        }

        code.Close();
    }

    private static string Path(GesPlanValue value) => $"paths[at + {value.Node.ToString(CultureInfo.InvariantCulture)}]";

    // A recursive schema's methods take the level of the record, 1 where a caller enters the recursion.
    private static string LevelParameter(GesPlanType type) => type.Depth > 0 ? ", int level = 1" : "";

    // The level argument of a call into a record: one deeper for the element of a recursive List.
    private static string NextLevel(GesPlanValue value) => value.Recursive ? ", level + 1" : "";

    // The level that the item of a recursive union takes: that of the record holding the List.
    private static string ItemLevel(GesPlanValue element) => element.PassesLevel ? ", level" : "";

    // Opens the levels of a recursive List that hold elements; at its depth the List holds nothing, so data there is deeper.
    private static void OpenLevels(GesSchemaPlanner.Code code, GesPlanValue value, string count)
    {
        var depth = value.Depth.ToString(CultureInfo.InvariantCulture);
        code.Line($"if (level >= {depth})");
        code.Open("{");
        code.Line($"if ({count} > 0) throw new GesWireException(GesWireException.TooDeep, {Path(value)}, \"deeper than depth {depth}\");");
        code.Close();
        code.Line("else");
        code.Open("{");
    }

    private static string Throw(string code, GesPlanValue value) => $"throw new GesWireException(GesWireException.{code}, {Path(value)});";

    // Writes the value in `target`, which the caller has validated as present.
    private static void Write(GesSchemaPlanner.Code code, GesPlanValue value, string target)
    {
        var encoding = value.Encoding;
        var width = encoding.Width.ToString(CultureInfo.InvariantCulture);
        var max = encoding.MaxCount.ToString(CultureInfo.InvariantCulture);
        switch (encoding.Kind)
        {
            case GesLayoutKind.Boolean:
                code.Line($"writer.Write({target} ? 1UL : 0UL, 1);");
                break;
            case GesLayoutKind.Integer when !encoding.Range:
                code.Line($"writer.Write(unchecked((ulong){target}), 64);");
                break;
            case GesLayoutKind.Integer when encoding.Wrapping:
                code.Line($"writer.Write(GesWireRules.WrappedIndex({target}, {Long(encoding.Lower)}, {Unsigned(encoding.MaxIndex + 1)}), {width});");
                break;
            case GesLayoutKind.Integer:
                code.Line($"if ({target} < {Long(encoding.Lower)} || unchecked((ulong){target} - {Unsigned(unchecked((ulong)encoding.Lower))}) > {Unsigned(encoding.MaxIndex)}) {Throw("OutOfRange", value)}");
                code.Line($"writer.Write(unchecked((ulong){target} - {Unsigned(unchecked((ulong)encoding.Lower))}), {width});");
                break;
            case GesLayoutKind.Quantized when encoding.Wrapping:
                code.Line($"writer.Write(GesWireRules.WrappedQuantizedIndex({target}, {Double(encoding.TenPower)}, {Double(encoding.Numerator)}, {Long(encoding.Lower)}, {Unsigned(encoding.MaxIndex + 1)}, {Path(value)}), {width});");
                break;
            case GesLayoutKind.Quantized:
                code.Line($"writer.Write(GesWireRules.QuantizedIndex({target}, {Bits(encoding.LowerValue)}, {Bits(encoding.UpperValue)}, {Double(encoding.TenPower)}, {Double(encoding.Numerator)}, {Long(encoding.Lower)}, {Path(value)}), {width});");
                break;
            case GesLayoutKind.Float:
                code.Line($"if (double.IsNaN({target})) {Throw("NotRepresentable", value)}");
                if (encoding.Range) code.Line($"if ({target} < {Bits(encoding.LowerValue)} || {target} > {Bits(encoding.UpperValue)}) {Throw("OutOfRange", value)}");
                code.Line($"writer.Write(unchecked((ulong)global::System.BitConverter.DoubleToInt64Bits({target} == 0 ? 0d : {target})), 64);");
                break;
            case GesLayoutKind.Tag:
                code.Line($"if ((ulong){target} > {Unsigned((ulong)encoding.Tags.Count - 1)}) {Throw("UnknownTag", value)}");
                code.Line($"writer.Write((ulong){target}, {width});");
                break;
            case GesLayoutKind.Flags:
                if (encoding.Width < 64) code.Line($"if ((ulong){target} > {Unsigned(Full(encoding.Width))}) {Throw("UnknownTag", value)}");
                code.Line($"writer.Write((ulong){target}, {width});");
                break;
            case GesLayoutKind.Text or GesLayoutKind.Bytes:
                code.Open("{");
                code.Line($"var text = new ReadOnlySpan<byte>(__{value.Base}, 0, __{value.Base}Length);");
                code.Line($"if (text.Length > {max}) {Throw("TooLong", value)}");
                if (encoding.Kind == GesLayoutKind.Text)
                    code.Line(encoding.TagName ? $"if (!GesWireRules.IsTagName(text)) {Throw("InvalidTag", value)}" : $"if (!GesWireRules.IsValidUtf8(text)) {Throw("InvalidText", value)}");
                code.Line($"writer.Write((ulong)text.Length, {width});");
                code.Line("writer.WriteBytes(text);");
                code.Close();
                break;
            case GesLayoutKind.Handle:
                WriteHandle(code, value, target);
                break;
            case GesLayoutKind.List:
                if (value.Depth > 0) OpenLevels(code, value, target + ".Count");
                code.Line($"if ({target}.Count > {max}) {Throw("TooLong", value)}");
                if (encoding.Fixed) code.Line($"if ({target}.Count < {max}) {Throw("TooShort", value)}");
                else code.Line($"writer.Write((ulong){target}.Count, {width});");
                if (value.HandleMap) code.Line($"for (var position = 1; position < {target}.Count; position++) if ({target}[position].Key.CompareTo({target}[position - 1].Key) <= 0) throw new GesWireException(GesWireException.InvalidKeyOrder, {HandleKeyPath(value)});");
                code.Line($"foreach (var item in {target})");
                code.Open("{");
                if (value.Item is not null) code.Line($"item.__WriteFields(ref writer, paths, at + {value.Element!.Node.ToString(CultureInfo.InvariantCulture)}{ItemLevel(value.Element)});");
                else Write(code, value.Element!, "item");
                code.Close();
                if (value.Depth > 0) code.Close();
                break;
            case GesLayoutKind.Point or GesLayoutKind.Vector:
                foreach (var axis in value.Axes) Write(code, axis, target + "." + axis.Base);
                foreach (var axis in GesSchemaPlanner.Omitted(value)) code.Line($"if ({target}.{axis} != 0) {Throw("TypeMismatch", value)}");
                break;
            case GesLayoutKind.Box:
                foreach (var corner in value.Corners) Write(code, corner, target + "." + corner.Base);
                break;
            case GesLayoutKind.Disc:
                foreach (var part in value.Corners) Write(code, part, target + "." + part.Base);
                if (value.Corners.Count == 1) code.Line($"if ({target}.Radius != 0) {Throw("TypeMismatch", value)}");
                break;
            case GesLayoutKind.Schema when value.Packed is not null:
                PackedWrite(code, value, target);
                break;
            case GesLayoutKind.Schema:
                code.Line($"{target}.__WriteFields(ref writer, paths, at + {(value.Node + 1).ToString(CultureInfo.InvariantCulture)}{NextLevel(value)});");
                break;
            case GesLayoutKind.Union:
                code.Line($"switch ({target})");
                code.Open("{");
                for (var index = 0; index < value.Alternatives.Count; index++)
                {
                    var alternative = value.Alternatives[index];
                    code.Line($"case {NestedName(value)}.{alternative.Label}:");
                    code.Open("{");
                    code.Line($"writer.Write({index.ToString(CultureInfo.InvariantCulture)}, {width});");
                    if (alternative.Encoding.Kind != GesLayoutKind.Nothing) Write(code, alternative, alternative.Base);
                    code.Line("break;");
                    code.Close();
                }

                code.Line("default:");
                code.Open("{");
                code.Line(Throw("OutOfRange", value));
                code.Close();
                code.Close();
                break;
        }
    }

    // Reads into `target`, after the caller has read a presence bit.
    private static void Read(GesSchemaPlanner.Code code, GesPlanValue value, string target)
    {
        var encoding = value.Encoding;
        var width = encoding.Width.ToString(CultureInfo.InvariantCulture);
        var max = encoding.MaxCount.ToString(CultureInfo.InvariantCulture);
        switch (encoding.Kind)
        {
            case GesLayoutKind.Boolean:
                code.Line($"{target} = reader.Read(1, {Path(value)}) != 0;");
                break;
            case GesLayoutKind.Integer when !encoding.Range:
                code.Line($"{target} = unchecked((long)reader.Read(64, {Path(value)}));");
                break;
            case GesLayoutKind.Flags:
                code.Line($"{target} = ({NestedName(value)})reader.Read({width}, {Path(value)});");
                break;
            case GesLayoutKind.Integer or GesLayoutKind.Quantized or GesLayoutKind.Tag:
                code.Open("{");
                code.Line($"var index = reader.Read({width}, {Path(value)});");
                var largest = encoding.Kind == GesLayoutKind.Tag ? (ulong)encoding.Tags.Count - 1 : encoding.MaxIndex;
                if (largest < Full(encoding.Width)) code.Line($"if (index > {Unsigned(largest)}) {Throw("InvalidIndex", value)}");
                code.Line(encoding.Kind switch
                {
                    GesLayoutKind.Integer => $"{target} = unchecked((long)({Unsigned(unchecked((ulong)encoding.Lower))} + index));",
                    GesLayoutKind.Quantized => $"{target} = GesWireRules.QuantizedValue(index, {Long(encoding.Lower)}, {Long(encoding.Numerator)}, {Double(encoding.TenPower)});",
                    _ => $"{target} = ({NestedName(value)})index;"
                });
                code.Close();
                break;
            case GesLayoutKind.Float:
                code.Open("{");
                code.Line($"var bits = reader.Read(64, {Path(value)});");
                code.Line("var value = global::System.BitConverter.Int64BitsToDouble(unchecked((long)bits));");
                code.Line($"if (double.IsNaN(value) || bits == 0x8000000000000000UL{(encoding.Range ? " || double.IsInfinity(value)" : "")}) {Throw("InvalidNumber", value)}");
                if (encoding.Range) code.Line($"if (value < {Bits(encoding.LowerValue)} || value > {Bits(encoding.UpperValue)}) {Throw("OutOfRange", value)}");
                code.Line($"{target} = value;");
                code.Close();
                break;
            case GesLayoutKind.Text or GesLayoutKind.Bytes:
                var storage = "__" + value.Base;
                code.Open("{");
                code.Line($"var count = (int)reader.Read({width}, {Path(value)});");
                code.Line($"if (count > {max}) {Throw("InvalidCount", value)}");
                code.Line($"reader.Require(count * 8L, {Path(value)});");
                code.Line($"if ({storage}.Length < count) global::System.Array.Resize(ref {storage}, global::System.Math.Max(count, global::System.Math.Min({max}, {storage}.Length * 2)));");
                code.Line($"var text = new Span<byte>({storage}, 0, count);");
                code.Line($"reader.ReadBytes(text, {Path(value)});");
                if (encoding.Kind == GesLayoutKind.Text)
                    code.Line(encoding.TagName ? $"if (!GesWireRules.IsTagName(text)) {Throw("InvalidTag", value)}" : $"if (!GesWireRules.IsValidUtf8(text)) {Throw("InvalidText", value)}");
                code.Line($"{storage}Length = count;");
                code.Close();
                break;
            case GesLayoutKind.List:
                var element = value.Element!;
                var name = ElementName(value);
                if (value.Depth > 0)
                {
                    // The deepest level reads no count, since its List takes no bits.
                    code.Line($"if (level >= {value.Depth.ToString(CultureInfo.InvariantCulture)}) {target}.Clear();");
                    code.Line("else");
                }

                code.Open("{");
                if (encoding.Fixed) code.Line($"const int count = {max};");
                else
                {
                    code.Line($"var count = (int)reader.Read({width}, {Path(value)});");
                    code.Line($"if (count > {max}) {Throw("InvalidCount", value)}");
                }

                code.Line($"{target}.Clear();");
                if (element.Packed is not null)
                {
                    PackedList(code, value, target, "count");
                    code.Close();
                    break;
                }

                code.Line("for (var position = 0; position < count; position++)");
                code.Open("{");
                if (element.IsClass)
                {
                    code.Line($"if (position == __{value.Base}Pool.Count) __{value.Base}Pool.Add(new {name}());");
                    code.Line($"var item = __{value.Base}Pool[position];");
                }
                // A handle element starts empty, because its read passes the current id along.
                else if (value.Item is not null || element.Encoding.Kind is GesLayoutKind.Point or GesLayoutKind.Vector or GesLayoutKind.Schema or GesLayoutKind.Handle) code.Line($"var item = new {name}();");
                else code.Line($"{name} item;");
                if (value.Item is not null) code.Line($"item.__ReadFields(ref reader, paths, at + {element.Node.ToString(CultureInfo.InvariantCulture)}{ItemLevel(element)});");
                else Read(code, element, "item");
                if (value.HandleMap) code.Line($"if (position > 0 && item.Key.CompareTo({target}[position - 1].Key) <= 0) throw new GesWireException(GesWireException.InvalidKeyOrder, {HandleKeyPath(value)});");
                code.Line($"{target}.Add(item);");
                code.Close();
                code.Close();
                break;
            case GesLayoutKind.Handle:
                ReadHandle(code, value, target);
                break;
            case GesLayoutKind.Point or GesLayoutKind.Vector:
                foreach (var axis in value.Axes) Read(code, axis, target + "." + axis.Base);
                foreach (var axis in GesSchemaPlanner.Omitted(value)) code.Line($"{target}.{axis} = 0;");
                break;
            case GesLayoutKind.Box:
                // The corners arrive as two Points; the box orders them.
                code.Open("{");
                foreach (var corner in value.Corners)
                {
                    code.Line($"var {corner.Label}{N(value.Node)} = new GesPoint();");
                    Read(code, corner, corner.Label + N(value.Node));
                }

                code.Line($"{target} = new GesBox(lower{N(value.Node)}, upper{N(value.Node)});");
                code.Close();
                break;
            case GesLayoutKind.Disc:
                // The centre and the radius, which a block can leave out; the disc takes the radius's magnitude.
                code.Open("{");
                code.Line($"var center{N(value.Node)} = new GesPoint();");
                code.Line($"var radius{N(value.Node)} = 0d;");
                foreach (var part in value.Corners) Read(code, part, part.Label + N(value.Node));
                code.Line($"{target} = new GesDisc(center{N(value.Node)}, radius{N(value.Node)});");
                code.Close();
                break;
            case GesLayoutKind.Schema when value.Packed is not null:
                PackedRead(code, value, target);
                break;
            case GesLayoutKind.Schema:
                code.Line($"{target}.__ReadFields(ref reader, paths, at + {(value.Node + 1).ToString(CultureInfo.InvariantCulture)}{NextLevel(value)});");
                break;
            case GesLayoutKind.Union:
                code.Open("{");
                code.Line($"var choice = reader.Read({width}, {Path(value)});");
                if ((ulong)value.Alternatives.Count - 1 < Full(encoding.Width)) code.Line($"if (choice > {Unsigned((ulong)value.Alternatives.Count - 1)}) {Throw("InvalidIndex", value)}");
                code.Line($"{target} = ({NestedName(value)})choice;");
                code.Line("switch (choice)");
                code.Open("{");
                for (var index = 0; index < value.Alternatives.Count; index++)
                {
                    var alternative = value.Alternatives[index];
                    if (alternative.Encoding.Kind == GesLayoutKind.Nothing) continue;
                    code.Line($"case {index.ToString(CultureInfo.InvariantCulture)}:");
                    code.Open("{");
                    Read(code, alternative, alternative.Base);
                    code.Line("break;");
                    code.Close();
                }

                code.Close();
                code.Close();
                break;
        }
    }

    // The path of a handle map's key: the node after its entry's group.
    private static string HandleKeyPath(GesPlanValue map) => $"paths[at + {(map.Element!.Node + 1).ToString(CultureInfo.InvariantCulture)}]";

    // The plan values of a handle's id, its version where the field has one, and the part of a type with one, in node order.
    // The id of a type with a part is its root's; the version an attached type's root's, a keyed type's its own.
    internal static (GesPlanValue Id, GesPlanValue? Version, GesPlanValue? Part) HandleParts(GesPlanValue value)
    {
        var handle = value.Handle!;
        var idOwner = value.HandleRoot ?? handle;
        var owner = handle.Keyed ? handle : idOwner;
        var node = value.Node + 1;
        var id = new GesPlanValue { Base = "Id", Node = node++, Encoding = idOwner.Id! };
        GesPlanValue? version = null;
        if (value.Encoding.Versioned)
            version = new GesPlanValue
            {
                Base = "Version",
                Node = node++,
                Encoding = owner.Version == GesHandleVersionKind.Revision ? owner.Revision! : new GesLayoutEncoding { Kind = GesLayoutKind.Bytes, MaxCount = owner.Version == GesHandleVersionKind.Sha1 ? 20 : 32 }
            };
        var part = handle.HasPart ? new GesPlanValue { Base = "Part", Node = node, Encoding = handle.Part! } : null;
        return (id, version, part);
    }

    // The type of the root of a type with a part, beside the handle's own type.
    internal static string HandleRootType(GesPlanValue value)
        => value.HandleType.Substring(0, value.HandleType.Length - (value.Handle!.Name + "Handle").Length) + value.HandleRoot!.Name + "Handle";

    // Builds a handle from the locals `id`, `version` (where versioned) and `part`: a type with a part builds its root from the id,
    // with the version for an attached type, whose version is its root's, while a keyed type takes it as its own.
    internal static string HandleConstruction(GesPlanValue value, bool versioned, bool swift)
    {
        var keyed = value.Handle!.Keyed && versioned;
        var version = versioned ? swift ? ", version: version" : ", version" : "";
        var rootVersion = keyed ? "" : version;
        if (swift)
            return value.HandleRoot is not null
                ? $"{value.HandleType}(uncheckedRoot: {HandleRootType(value)}(uncheckedId: id{rootVersion}), part: part{(keyed ? version : "")})"
                : $"{value.HandleType}(uncheckedId: id{version})";
        return value.HandleRoot is not null
            ? $"new {value.HandleType}(new {HandleRootType(value)}(id{rootVersion}), part{(keyed ? version : "")})"
            : $"new {value.HandleType}(id{version})";
    }

    // The version type of a hash: GesHash20 or GesHash32.
    internal static string HashType(GesPlanValue version) => version.Encoding.MaxCount == 20 ? "GesHash20" : "GesHash32";

    // Writes a handle's id, version and part; a field without a version leaves the handle's out.
    private static void WriteHandle(GesSchemaPlanner.Code code, GesPlanValue value, string target)
    {
        var (id, version, part) = HandleParts(value);
        code.Open("{");
        WriteHandleKey(code, id, target + (value.HandleRoot is not null ? ".Root.Id" : ".Id"));
        if (version is not null)
        {
            code.Line($"if ({target}.Version is not {{ }} version) {Throw("MissingVersion", version)}");
            if (version.Encoding.Kind == GesLayoutKind.Bytes)
            {
                code.Line($"Span<byte> hash = stackalloc byte[{HashType(version)}.Length];");
                code.Line("version.CopyTo(hash);");
                code.Line("for (var index = 0; index < hash.Length; index++) writer.Write(hash[index], 8);");
            }
            else Write(code, version, "version");
        }

        if (part is not null) WriteHandleKey(code, part, target + ".Part");
        code.Close();
    }

    private static void WriteHandleKey(GesSchemaPlanner.Code code, GesPlanValue key, string target)
    {
        if (key.Encoding.Kind != GesLayoutKind.Text)
        {
            Write(code, key, target);
            return;
        }

        code.Line($"if ({target} is null) {Throw("TypeMismatch", key)}");
        code.Line($"GesWireRules.WriteText(ref writer, {target}, {key.Encoding.Width.ToString(CultureInfo.InvariantCulture)}, {key.Encoding.MaxCount.ToString(CultureInfo.InvariantCulture)}, {Path(key)});");
    }

    // Reads a handle into locals and builds it; a text id that equals the held one keeps its string.
    private static void ReadHandle(GesSchemaPlanner.Code code, GesPlanValue value, string target)
    {
        var (id, version, part) = HandleParts(value);
        code.Open("{");
        ReadHandleKey(code, id, "id", target + (value.HandleRoot is not null ? ".Root.Id" : ".Id"));
        if (version is not null)
        {
            if (version.Encoding.Kind == GesLayoutKind.Bytes)
            {
                code.Line($"Span<byte> hash = stackalloc byte[{HashType(version)}.Length];");
                code.Line($"reader.Require(hash.Length * 8L, {Path(version)});");
                code.Line($"for (var index = 0; index < hash.Length; index++) hash[index] = (byte)reader.Read(8, {Path(version)});");
                code.Line($"var version = {HashType(version)}.FromBytes(hash);");
            }
            else
            {
                code.Line("long version;");
                Read(code, version, "version");
            }
        }

        if (part is not null) ReadHandleKey(code, part, "part", target + ".Part");
        code.Line($"{target} = {HandleConstruction(value, version is not null, swift: false)};");
        code.Close();
    }

    private static void ReadHandleKey(GesSchemaPlanner.Code code, GesPlanValue key, string local, string current)
    {
        if (key.Encoding.Kind == GesLayoutKind.Text)
        {
            code.Line($"var {local} = GesWireRules.ReadText(ref reader, {key.Encoding.Width.ToString(CultureInfo.InvariantCulture)}, {key.Encoding.MaxCount.ToString(CultureInfo.InvariantCulture)}, {current}, {Path(key)});");
            return;
        }

        code.Line($"long {local};");
        Read(code, key, local);
    }

    private static void Declarations(GesSchemaPlanner.Code code, GesPlanValue value, string access, Context context)
    {
        var name = NestedName(value);
        switch (value.Encoding.Kind)
        {
            case GesLayoutKind.Tag:
                if (value.TagType.Length > 0) break;
                code.Blank();
                code.Line("/// <summary>The tags of the field, in declared order.</summary>");
                code.Line($"public enum {name} : {Underlying(value.Encoding.Tags.Count)}");
                code.Open("{");
                foreach (var tag in value.Encoding.Tags)
                {
                    code.Line($"/// <summary>The tag <c>#{tag}</c>.</summary>");
                    code.Line(GesSchemaPlanner.Pascal(tag) + ",");
                }

                code.Close();
                break;
            case GesLayoutKind.Flags:
                FlagsDeclaration(code, name, value.Encoding.Tags);
                break;
            case GesLayoutKind.Union:
                code.Blank();
                code.Line("/// <summary>The alternatives of the field, in declared order.</summary>");
                code.Line($"public enum {name} : {Underlying(value.Alternatives.Count)}");
                code.Open("{");
                foreach (var alternative in value.Alternatives)
                {
                    code.Line($"/// <summary>The alternative <c>:{alternative.Label}</c>.</summary>");
                    code.Line(alternative.Label + ",");
                }

                code.Close();
                foreach (var alternative in value.Alternatives) Declarations(code, alternative, access, context);
                break;
            case GesLayoutKind.List:
                if (value.Element!.IsClass)
                {
                    code.Blank();
                    code.Line($"private readonly List<{ElementName(value)}> __{value.Base}Pool = new List<{ElementName(value)}>();");
                }

                if (value.Item is not null)
                {
                    code.Blank();
                    Record(code, value.Item, access, context);
                }
                else Declarations(code, value.Element!, access, context);
                break;
        }
    }

    private static string Underlying(int count) => count <= 256 ? "byte" : count <= 65536 ? "ushort" : "uint";

    // A route root as a network sees it: its type, the type that holds its MaxByteCount and its plan.
    private sealed class NetworkRoot(string type, string owner, GesPlanType plan)
    {
        internal string Type { get; } = type;

        internal string Owner { get; } = owner;

        internal GesPlanType Plan { get; } = plan;

        // A message set without parameters, whose routes take and hand its cases; one with parameters is sent as its record,
        // which holds them.
        internal bool IsSet => Plan.Kind == GesSchemaKind.Messages && Plan.Fields.Count == 0;
    }

    // The constants of a network, its hash, the index of each route and the rate of each topic that has one, a function
    // per route that encodes its root and sends or publishes it, and the typed ends with their handler interfaces.
    private static void Network(GesSchemaPlanner.Code code, GesLayoutNetwork network, string access, string? schemaSet, Func<string, NetworkRoot> roots)
    {
        var client = GesNetworkEndPlan.Plan(network, client: true);
        var server = GesNetworkEndPlan.Plan(network, client: false);
        code.Line($"/// <summary>The network <c>:{network.Name}</c>: its hash, the index of each route, the rate of each topic that has one, a typed function per route and the typed ends.</summary>");
        code.Line($"{access} static class {network.Name}");
        code.Open("{");
        code.Line("/// <summary>The network hash over the routes and their bound roots, which the handshake compares.</summary>");
        code.Line($"public const ulong Hash = 0x{GesSchemaLayoutJson.Hex(network.Hash)}UL;");
        if (schemaSet is not null)
        {
            code.Blank();
            code.Line("private static GesBoundNetwork? __bound;");
            code.Blank();
            code.Line($"/// <summary>Gets the network bound with the parameters of the schema set, checked against <see cref=\"Hash\"/> on first access.</summary>");
            code.Line("/// <exception cref=\"GesSchemaException\">The sources no longer parse or the network no longer binds.</exception>");
            code.Line("/// <exception cref=\"global::System.InvalidOperationException\">The bound network hash differs from <see cref=\"Hash\"/>.</exception>");
            code.Line($"public static GesBoundNetwork Bound => __bound ??= {schemaSet}.BindNetwork({GesSchemaPlanner.Literal(network.Name, false)}, Hash);");
        }

        foreach (var route in network.Routes)
        {
            code.Blank();
            code.Line($"/// <summary>The index of the route <c>{route.Side} {route.Verb} {route.Written} {route.Class}</c>.</summary>");
            code.Line($"public const int {route.Name} = {route.Index};");
            if (route.RateMicroseconds == 0) continue;
            code.Blank();
            code.Line($"/// <summary>The shortest interval between two updates of the topic <c>{route.Written}</c> in microseconds.</summary>");
            code.Line($"public const long {route.Name}EveryMicroseconds = {route.RateMicroseconds};");
        }

        foreach (var route in network.Routes)
        {
            var publishes = route.Verb == "publishes";
            var verb = publishes ? "Publish" : "Send";
            code.Blank();
            code.Line($"/// <summary>Encodes <paramref name=\"value\"/> into <paramref name=\"buffer\"/> and {(publishes ? "publishes" : "sends")} it on the route <c>{route.Side} {route.Verb} {route.Written} {route.Class}</c>.</summary>");
            code.Line($"/// <param name=\"connection\">The {route.Side}'s connection.</param>");
            code.Line($"/// <param name=\"value\">The record.</param>");
            code.Line($"/// <param name=\"buffer\">The buffer the record is encoded into; its <c>MaxByteCount</c> bytes always suffice.</param>");
            code.Line($"/// <returns>The status of <see cref=\"GesNetworkConnection.{verb}(int, ReadOnlySpan{{byte}})\"/>.</returns>");
            code.Line("/// <exception cref=\"GesWireException\">A value does not fit its field, or the buffer is too small.</exception>");
            code.Line($"public static GesNetworkSendStatus {GesSchemaPlanner.Pascal(route.Side)}{verb}{route.Root}(GesNetworkConnection connection, in {roots(route.Type).Type} value, Span<byte> buffer)");
            code.Line($"    => connection.{verb}({route.Name}, buffer.Slice(0, value.Write(buffer)));");
        }

        foreach (var end in new[] { client, server })
        {
            TypedEnd(code, network, end, roots);
            EndHandler(code, network, end, roots);
        }

        code.Close();
    }

    // The receiving of a route: the type its handler method takes.
    private static string EventParameter(NetworkRoot root) => root.IsSet ? root.Type + ".Case" : "in " + root.Type;

    // A typed end: it encodes the roots of its side into one buffer, reads the roots it receives into one record per route
    // and calls the handler's typed methods.
    private static void TypedEnd(GesSchemaPlanner.Code code, GesLayoutNetwork network, GesNetworkEndPlan end, Func<string, NetworkRoot> roots)
    {
        var isClient = end.Client;
        var endType = isClient ? "GesNetworkClientEnd" : "GesNetworkServerEnd";
        var handler = $"I{end.Name}Handler";
        var side = isClient ? "client" : "server";
        // Distinct prefixes keep the scratch record of an outgoing message set, the cached cases and the internal fields
        // (__send_buffer, __tick_count, __tick_open: an underscore before a lowercase letter, which no root name has)
        // apart from the record of each received route, whatever its root is called.
        string Field(string name) => "__" + GesSchemaPlanner.Camel(name);
        string Outgoing(GesLayoutRoute route) => "__out_" + GesSchemaPlanner.Camel((route.Verb == "sends" ? "Send" : "Publish") + route.Root);
        string CaseField(string name, string member) => "__case_" + GesSchemaPlanner.Camel(name) + "__" + member;
        code.Blank();
        code.Line($"/// <summary>");
        code.Line($"/// The typed {side} end of <c>:{network.Name}</c>: it encodes the records the {side} sends and publishes and hands the records it");
        code.Line($"/// receives to an <see cref=\"{handler}\"/>. Received records are borrowed: they stay valid until the handler method returns.");
        code.Line($"/// </summary>");
        code.Line($"public sealed class {end.Name}");
        code.Open("{");
        code.Line("private readonly byte[] __send_buffer;");
        foreach (var route in end.Outgoing)
        {
            var root = roots(route.Type);
            if (!root.IsSet) continue;
            code.Line($"private {root.Type} {Outgoing(route)}{(root.Plan.MembersAreClasses ? " = new();" : ";")}");
        }

        foreach (var (route, name) in end.Incoming)
        {
            var root = roots(route.Type);
            code.Line($"private {root.Type} {Field(name)}{(root.Plan.IsClass || root.Plan.MembersAreClasses ? " = new();" : ";")}");
            if (!root.IsSet) continue;
            foreach (var member in root.Plan.MemberNames) code.Line($"private readonly {root.Type}.Case.{member} {CaseField(name, member)} = new();");
        }

        code.Blank();
        code.Line($"/// <summary>Wraps an end of the network.</summary>");
        code.Line($"/// <param name=\"end\">The end; its bound network must have the hash <see cref=\"Hash\"/>.</param>");
        code.Line("/// <exception cref=\"global::System.ArgumentNullException\"><paramref name=\"end\"/> is null.</exception>");
        code.Line("/// <exception cref=\"global::System.ArgumentException\">The end's network has another hash.</exception>");
        code.Line($"public {end.Name}({endType} end)");
        code.Open("{");
        code.Line("End = end ?? throw new global::System.ArgumentNullException(nameof(end));");
        code.Line($"if (end.Network.Hash != Hash) throw new global::System.ArgumentException(\"The end's network is no :{network.Name}.\", nameof(end));");
        var sizes = new List<string>();
        foreach (var route in end.Outgoing)
        {
            var owner = roots(route.Type).Owner + ".MaxByteCount";
            if (!sizes.Contains(owner)) sizes.Add(owner);
        }

        var size = sizes.Count == 0 ? "0" : sizes[0];
        for (var index = 1; index < sizes.Count; index++) size = $"global::System.Math.Max({size}, {sizes[index]})";
        code.Line($"__send_buffer = new byte[{size}];");
        code.Close();
        code.Blank();
        code.Line("/// <summary>Gets the wrapped end.</summary>");
        code.Line($"public {endType} End {{ get; }}");
        if (isClient)
        {
            code.Blank();
            code.Line("/// <summary>Gets the end's connection.</summary>");
            code.Line("public GesNetworkConnection Connection => End.Connection;");
        }
        else
        {
            code.Blank();
            code.Line("/// <summary>Gets the number of connecting and connected clients.</summary>");
            code.Line("public int ClientCount => End.ClientCount;");
            code.Blank();
            code.Line("/// <summary>Gets the connection of a client.</summary>");
            code.Line("/// <param name=\"client\">The client.</param>");
            code.Line("/// <returns>The connection.</returns>");
            code.Line("/// <exception cref=\"global::System.ArgumentException\">The client is not connected to the end.</exception>");
            code.Line("public GesNetworkConnection Connection(int client) => End.Connection(client);");
        }

        foreach (var route in end.Outgoing)
        {
            var root = roots(route.Type);
            var publishes = route.Verb == "publishes";
            var verb = publishes ? "Publish" : "Send";
            var what = $"<c>{route.Side} {route.Verb} {route.Written} {route.Class}</c>";
            var targets = new List<(string Parameters, string Arguments, string Doc)> { ("", "", publishes ? "to the subscribed clients" : isClient ? "to the server" : "to every client") };
            if (!isClient && !publishes) targets.Add((", int client", ", client", "to one client"));
            foreach (var (parameters, arguments, doc) in targets)
            {
                code.Blank();
                code.Line($"/// <summary>Encodes a record and {(publishes ? "publishes" : "sends")} it on the route {what}{(isClient ? "" : ", " + doc)}.</summary>");
                code.Line("/// <param name=\"value\">The record.</param>");
                if (arguments.Length > 0) code.Line("/// <param name=\"client\">The client.</param>");
                code.Line($"/// <returns>The status of <see cref=\"{endType}.{verb}(int, ReadOnlySpan{{byte}}{(arguments.Length > 0 ? ", int" : "")})\"/>.</returns>");
                code.Line("/// <exception cref=\"GesWireException\">A value does not fit its field.</exception>");
                code.Line($"public GesNetworkSendStatus {verb}(in {root.Type} value{parameters}) => End.{verb}({route.Name}, __send_buffer.AsSpan(0, value.Write(__send_buffer)){arguments});");
                if (!root.IsSet) continue;
                var scratch = Outgoing(route);
                code.Blank();
                code.Line($"/// <summary>Encodes a message and {(publishes ? "publishes" : "sends")} it on the route {what}{(isClient ? "" : ", " + doc)}.</summary>");
                code.Line("/// <param name=\"value\">The message.</param>");
                if (arguments.Length > 0) code.Line("/// <param name=\"client\">The client.</param>");
                code.Line($"/// <returns>The status of <see cref=\"{endType}.{verb}(int, ReadOnlySpan{{byte}}{(arguments.Length > 0 ? ", int" : "")})\"/>.</returns>");
                code.Line("/// <exception cref=\"global::System.ArgumentNullException\"><paramref name=\"value\"/> is null.</exception>");
                code.Line("/// <exception cref=\"GesWireException\">A value does not fit its field.</exception>");
                code.Line($"public GesNetworkSendStatus {verb}({root.Type}.Case value{parameters})");
                code.Open("{");
                code.Line($"{scratch}.Assign(value);");
                code.Line($"return {verb}(in {scratch}{arguments});");
                code.Close();
            }
        }

        if (!isClient)
        {
            code.Blank();
            code.Line("/// <summary>Refuses a connecting client; call it while the handler handles its <c>OnConnecting</c>.</summary>");
            code.Line("/// <param name=\"client\">The client.</param>");
            code.Line("/// <param name=\"detail\">The detail sent to the client.</param>");
            code.Line("public void Refuse(int client, string detail) => End.Refuse(client, detail);");
            code.Blank();
            code.Line("/// <summary>Closes the connection of one client with a detail.</summary>");
            code.Line("/// <param name=\"client\">The client.</param>");
            code.Line("/// <param name=\"detail\">The detail sent to the client.</param>");
            code.Line("public void Close(int client, string detail) => End.Close(client, detail);");
        }

        code.Blank();
        code.Line(isClient ? "/// <summary>Closes the connection with a detail.</summary>" : "/// <summary>Closes every current connection with a detail; the end still accepts new clients.</summary>");
        code.Line($"/// <param name=\"detail\">The detail sent to the {(isClient ? "server" : "clients")}.</param>");
        code.Line("public void Close(string detail) => End.Close(detail);");
        if (end.HasTopics)
        {
            if (!isClient)
            {
                code.Blank();
                code.Line("/// <summary>Subscribes a client to a topic.</summary>");
                code.Line("/// <param name=\"client\">The client.</param>");
                code.Line("/// <param name=\"topic\">The topic.</param>");
                code.Line("public void Subscribe(int client, Topic topic) => End.Subscribe(client, (int)topic);");
                code.Blank();
                code.Line("/// <summary>Unsubscribes a client from a topic.</summary>");
                code.Line("/// <param name=\"client\">The client.</param>");
                code.Line("/// <param name=\"topic\">The topic.</param>");
                code.Line("public void Unsubscribe(int client, Topic topic) => End.Unsubscribe(client, (int)topic);");
            }

            code.Blank();
            code.Line("private int __tick_count;");
            code.Line("private bool __tick_open;");
            code.Blank();
            code.Line("/// <summary>Opens a tick; disposing the returned tick closes it.</summary>");
            code.Line("/// <returns>The open tick.</returns>");
            code.Line("/// <exception cref=\"global::System.InvalidOperationException\">A tick is open already.</exception>");
            code.Line("public Tick BeginTick()");
            code.Open("{");
            code.Line("End.BeginTick();");
            code.Line("__tick_open = true;");
            code.Line("return new Tick(this, unchecked(++__tick_count));");
            code.Close();
            code.Blank();
            code.Line($"/// <summary>The topics the {side} publishes.</summary>");
            code.Line("public enum Topic");
            code.Open("{");
            foreach (var route in end.Topics)
            {
                code.Line($"/// <summary>The topic <c>{route.Side} {route.Verb} {route.Written} {route.Class}</c>.</summary>");
                code.Line($"{route.Root} = {route.Name},");
            }

            code.Close();
            code.Blank();
            code.Line("/// <summary>An open tick: its publications arrive together. <see cref=\"Dispose\"/> closes it.</summary>");
            code.Line("public readonly struct Tick : global::System.IDisposable");
            code.Open("{");
            code.Line($"private readonly {end.Name} __end;");
            code.Line("private readonly int __number;");
            code.Blank();
            code.Line($"internal Tick({end.Name} end, int number)");
            code.Open("{");
            code.Line("__end = end;");
            code.Line("__number = number;");
            code.Close();
            foreach (var route in end.Topics)
            {
                var root = roots(route.Type);
                foreach (var parameter in root.IsSet ? new[] { "in " + root.Type, root.Type + ".Case" } : new[] { "in " + root.Type })
                {
                    code.Blank();
                    code.Line($"/// <summary>Publishes a {(parameter.StartsWith("in ", StringComparison.Ordinal) ? "record" : "message")} on the topic <c>{route.Written}</c> as a member of the tick.</summary>");
                    code.Line("/// <param name=\"value\">The value.</param>");
                    code.Line("/// <returns>The status of the publication.</returns>");
                    code.Line("/// <exception cref=\"GesWireException\">A value does not fit its field.</exception>");
                    code.Line($"public GesNetworkSendStatus Publish({parameter} value) => __end.Publish({(parameter.StartsWith("in ", StringComparison.Ordinal) ? "in " : "")}value);");
                }
            }

            code.Blank();
            code.Line("/// <summary>Makes a latest topic a member of the tick with its last published state.</summary>");
            code.Line("/// <param name=\"topic\">The topic.</param>");
            code.Line("/// <exception cref=\"global::System.InvalidOperationException\">The topic has no published state.</exception>");
            code.Line("/// <exception cref=\"global::System.ArgumentException\">The topic is no latest topic.</exception>");
            code.Line("public void Include(Topic topic) => __end.End.Include((int)topic);");
            code.Blank();
            code.Line("/// <summary>Closes the tick. A further call, also on a copy, does nothing.</summary>");
            code.Line("public void Dispose()");
            code.Open("{");
            code.Line("if (__end is null || !__end.__tick_open || __end.__tick_count != __number) return;");
            code.Line("__end.__tick_open = false;");
            code.Line("__end.End.EndTick();");
            code.Close();
            code.Close();
        }

        code.Blank();
        code.Line("/// <summary>Polls the end and hands every event to the handler's typed methods. Never blocks.</summary>");
        code.Line("/// <typeparam name=\"THandler\">The handler type; a struct that implements every method is called without allocating.</typeparam>");
        code.Line("/// <param name=\"handler\">The handler.</param>");
        code.Line($"/// <exception cref=\"global::System.ObjectDisposedException\">The end is disposed.</exception>");
        code.Line($"public void Poll<THandler>(ref THandler handler) where THandler : {handler}");
        code.Open("{");
        code.Line("var adapter = new __Adapter<THandler>(this, handler);");
        code.Line("try");
        code.Open("{");
        code.Line("End.Poll(ref adapter);");
        code.Close();
        code.Line("finally");
        code.Open("{");
        code.Line("handler = adapter.Handler;");
        code.Close();
        code.Close();
        code.Blank();
        code.Line($"private void __Handle<THandler>(ref THandler handler, GesNetworkEndEvent networkEvent) where THandler : {handler}");
        code.Open("{");
        var client = isClient ? "" : "networkEvent.Client, ";
        code.Line("switch (networkEvent.Kind)");
        code.Open("{");
        if (isClient)
        {
            code.Line("case GesNetworkEndEventKind.Connected:");
            code.Line("    handler.OnConnected(networkEvent.Security);");
            code.Line("    break;");
            code.Line("case GesNetworkEndEventKind.Disconnected:");
            code.Line("    handler.OnDisconnected(networkEvent.Code, networkEvent.Detail, networkEvent.ByPeer);");
            code.Line("    break;");
        }
        else
        {
            code.Line("case GesNetworkEndEventKind.Connecting:");
            code.Line("    handler.OnConnecting(networkEvent.Client, networkEvent.Payload);");
            code.Line("    break;");
            code.Line("case GesNetworkEndEventKind.ClientConnected:");
            code.Line("    handler.OnClientConnected(networkEvent.Client, networkEvent.Security);");
            code.Line("    break;");
            code.Line("case GesNetworkEndEventKind.ClientDisconnected:");
            code.Line("    handler.OnClientDisconnected(networkEvent.Client, networkEvent.Code, networkEvent.Detail, networkEvent.ByPeer);");
            code.Line("    break;");
        }

        if (end.Incoming.Count > 0)
        {
            code.Line("case GesNetworkEndEventKind.Message:");
            code.Line("    switch (networkEvent.Route)");
            code.Line("    {");
            foreach (var (route, name) in end.Incoming)
            {
                var root = roots(route.Type);
                var field = Field(name);
                code.Line($"        case {route.Name}:");
                code.Line("            try");
                code.Line("            {");
                code.Line($"                {field}.Read(networkEvent.Payload);");
                code.Line("            }");
                code.Line("            catch (GesWireException)");
                code.Line("            {");
                code.Line($"                End.Close({client}\"undecodable {route.Written}\");");
                code.Line("                break;");
                code.Line("            }");
                code.Line("");
                if (!root.IsSet) code.Line($"            handler.On{name}({client}in {field});");
                else
                {
                    code.Line($"            switch ({field}.Selected)");
                    code.Line("            {");
                    foreach (var member in root.Plan.MemberNames)
                    {
                        code.Line($"                case {root.Type}.Member.{member}:");
                        code.Line($"                    {CaseField(name, member)}.Value = {field}.{member};");
                        code.Line($"                    handler.On{name}({client}{CaseField(name, member)});");
                        code.Line("                    break;");
                    }

                    code.Line("            }");
                    code.Line("");
                }

                code.Line("            break;");
            }

            code.Line("    }");
            code.Line("");
            code.Line("    break;");
        }

        code.Line("case GesNetworkEndEventKind.Tick:");
        code.Line($"    handler.OnTick({client}networkEvent.TickNumber);");
        code.Line("    break;");
        code.Line("case GesNetworkEndEventKind.TransferProgress:");
        code.Line($"    handler.OnTransferProgress({client}networkEvent.Route, networkEvent.TransferId, networkEvent.Bytes, networkEvent.Total);");
        code.Line("    break;");
        code.Line("case GesNetworkEndEventKind.TransferCancelled:");
        code.Line($"    handler.OnTransferCancelled({client}networkEvent.Route, networkEvent.TransferId, networkEvent.Code);");
        code.Line("    break;");
        code.Line("case GesNetworkEndEventKind.Received:");
        code.Line("    handler.OnReceived();");
        code.Line("    break;");
        code.Close();
        code.Close();
        code.Blank();
        code.Line($"private struct __Adapter<THandler> : IGesNetworkEndHandler where THandler : {handler}");
        code.Open("{");
        code.Line($"private readonly {end.Name} __end;");
        code.Line("internal THandler Handler;");
        code.Blank();
        code.Line($"internal __Adapter({end.Name} end, THandler handler)");
        code.Open("{");
        code.Line("__end = end;");
        code.Line("Handler = handler;");
        code.Close();
        code.Blank();
        code.Line("public void Handle(GesNetworkEndEvent networkEvent) => __end.__Handle(ref Handler, networkEvent);");
        code.Close();
        code.Close();
    }

    // The handler interface of a typed end: one method per event, each empty by default.
    private static void EndHandler(GesSchemaPlanner.Code code, GesLayoutNetwork network, GesNetworkEndPlan end, Func<string, NetworkRoot> roots)
    {
        var isClient = end.Client;
        var client = isClient ? "" : "int client, ";
        var clientDoc = "/// <param name=\"client\">The client.</param>";
        void Method(string summary, string signature, params string[] parameters)
        {
            code.Blank();
            code.Line($"/// <summary>{summary}</summary>");
            if (!isClient && signature.Contains("int client", StringComparison.Ordinal)) code.Line(clientDoc);
            foreach (var parameter in parameters) code.Line(parameter);
            code.Line($"void {signature}");
            code.Open("{");
            code.Close();
        }

        code.Blank();
        code.Line("/// <summary>");
        code.Line($"/// The events of the typed {(isClient ? "client" : "server")} end <see cref=\"{end.Name}\"/>; every method does nothing by default. A struct handler that");
        code.Line("/// leaves a method to its default is boxed when that method is called.");
        code.Line("/// </summary>");
        code.Line($"public interface I{end.Name}Handler");
        code.Open("{");
        if (isClient)
        {
            Method("The session is established.", "OnConnected(GesNetworkSecurity security)", "/// <param name=\"security\">The reached security.</param>");
            Method("The connection closed.", "OnDisconnected(string code, string detail, bool byPeer)", "/// <param name=\"code\">The close code.</param>", "/// <param name=\"detail\">The detail.</param>", "/// <param name=\"byPeer\">Whether the server closed it.</param>");
        }
        else
        {
            Method("A client asks to connect; refuse it with <see cref=\"Server.Refuse\"/> before the method returns, or it is accepted.", "OnConnecting(int client, ReadOnlySpan<byte> token)", "/// <param name=\"token\">The client's token, valid until the method returns.</param>");
            Method("A client is connected.", "OnClientConnected(int client, GesNetworkSecurity security)", "/// <param name=\"security\">The reached security.</param>");
            Method("A client's connection closed; the client leaves the end.", "OnClientDisconnected(int client, string code, string detail, bool byPeer)", "/// <param name=\"code\">The close code.</param>", "/// <param name=\"detail\">The detail.</param>", "/// <param name=\"byPeer\">Whether the client closed it.</param>");
        }

        foreach (var (route, name) in end.Incoming)
        {
            var root = roots(route.Type);
            Method($"A {(root.IsSet ? "message" : "record")} arrived on the route <c>{route.Side} {route.Verb} {route.Written} {route.Class}</c>.", $"On{name}({client}{EventParameter(root)} value)", $"/// <param name=\"value\">The {(root.IsSet ? "message, whose record" : "record, which")} is valid until the method returns.</param>");
        }

        Method("A tick is complete; the events of its changed members precede it.", $"OnTick({client}int number)", "/// <param name=\"number\">The tick number.</param>");
        Method("A transfer on a bulk route advanced.", $"OnTransferProgress({client}int route, int transferId, long bytes, long total)", "/// <param name=\"route\">The route index.</param>", "/// <param name=\"transferId\">The transfer.</param>", "/// <param name=\"bytes\">The bytes received so far.</param>", "/// <param name=\"total\">The total bytes.</param>");
        Method("A transfer was cancelled; the session continues.", $"OnTransferCancelled({client}int route, int transferId, string code)", "/// <param name=\"route\">The route index.</param>", "/// <param name=\"transferId\">The transfer.</param>", "/// <param name=\"code\">The cancel code.</param>");
        Method("The last event of a poll that reported at least one message or tick.", "OnReceived()");
        code.Close();
    }

    // A named tag set, declared once beside the records that use it.
    internal static void TagSetDeclaration(GesSchemaPlanner.Code code, string name, List<string> tags, string access)
    {
        code.Blank();
        code.Line($"/// <summary>The tag set <c>:{name}</c>, in declared order.</summary>");
        code.Line($"{access} enum {name} : {Underlying(tags.Count)}");
        code.Open("{");
        foreach (var tag in tags)
        {
            code.Line($"/// <summary>The tag <c>#{tag}</c>.</summary>");
            code.Line(GesSchemaPlanner.Pascal(tag) + ",");
        }

        code.Close();
    }

    // A required list whose elements are classes the record reuses.
    internal static bool IsPooledList(GesPlanField field) => !field.Optional && field.Value.Encoding.Kind == GesLayoutKind.List && field.Value.Element!.IsClass;

    // A pooled list of Text or open Tag elements, whose element type holds one text.
    internal static bool IsTextList(GesPlanField field) => IsPooledList(field) && field.Value.Item is not null && field.Value.Element!.Encoding.Kind == GesLayoutKind.Text;

    // Sets a pooled list to a count of reused elements, as a read does, and for texts offers them as strings.
    private static void ListAccess(GesSchemaPlanner.Code code, GesPlanField field)
    {
        if (!IsPooledList(field)) return;
        var value = field.Value;
        code.Blank();
        code.Line($"/// <summary>Sets <c>{field.Layout.Name}</c> to <paramref name=\"count\"/> elements that the record reuses, as a read does; their fields keep earlier values until they are set.</summary>");
        code.Line("/// <param name=\"count\">The element count.</param>");
        code.Line($"public void Set{value.Base}Count(int count)");
        code.Open("{");
        code.Line("if (count < 0) throw new global::System.ArgumentOutOfRangeException(nameof(count));");
        code.Line($"{value.Base}.Clear();");
        code.Line("for (var position = 0; position < count; position++)");
        code.Open("{");
        code.Line($"if (position == __{value.Base}Pool.Count) __{value.Base}Pool.Add(new {ElementName(value)}());");
        code.Line($"{value.Base}.Add(__{value.Base}Pool[position]);");
        code.Close();
        code.Close();
        if (!IsTextList(field)) return;
        var text = value.Item!.Fields[0].Base;
        code.Blank();
        code.Line($"/// <summary>The elements of <c>{field.Layout.Name}</c> as strings; reading and setting allocate, and setting reuses the elements.</summary>");
        code.Line($"public string[] {value.Base}Values");
        code.Open("{");
        code.Line("get");
        code.Open("{");
        code.Line($"var values = new string[{value.Base}.Count];");
        code.Line($"for (var index = 0; index < values.Length; index++) values[index] = {value.Base}[index].{text};");
        code.Line("return values;");
        code.Close();
        code.Blank();
        code.Line("set");
        code.Open("{");
        code.Line("if (value is null) throw new global::System.ArgumentNullException(nameof(value));");
        code.Line($"Set{value.Base}Count(value.Length);");
        code.Line($"for (var index = 0; index < value.Length; index++) {value.Base}[index].{text} = value[index];");
        code.Close();
        code.Close();
    }

    // One bit per member in declared order, so the enum value is the wire mask; the empty value needs no name.
    internal static void FlagsDeclaration(GesSchemaPlanner.Code code, string name, List<string> members)
    {
        code.Blank();
        code.Line("/// <summary>The members of the field, one bit each in declared order.</summary>");
        code.Line("[global::System.Flags]");
        code.Line($"public enum {name} : ulong");
        code.Open("{");
        for (var index = 0; index < members.Count; index++)
        {
            code.Line($"/// <summary>The member <c>#{members[index]}</c>.</summary>");
            code.Line($"{GesSchemaPlanner.Pascal(members[index])} = 1UL << {index.ToString(CultureInfo.InvariantCulture)},");
        }

        code.Close();
    }

    private static ulong Full(int width) => width >= 64 ? ulong.MaxValue : (1UL << width) - 1;

    private static string Long(long value) => value == long.MinValue ? "long.MinValue" : value.ToString(CultureInfo.InvariantCulture) + "L";

    private static string Unsigned(ulong value) => value.ToString(CultureInfo.InvariantCulture) + "UL";

    // An integral binary64 such as 10^k or p, exact as a literal.
    private static string Double(long value) => value.ToString(CultureInfo.InvariantCulture) + "d";

    private static string Bits(double value) => $"global::System.BitConverter.Int64BitsToDouble(unchecked((long)0x{GesSchemaLayoutJson.Hex((ulong)global::System.BitConverter.DoubleToInt64Bits(value))}UL))";

    private static string Quote(string text) => "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
