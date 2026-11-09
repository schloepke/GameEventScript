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
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
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
    private static readonly string[] ValueReserved = ["ToValue", "ReadValue", "ToMessage", "ReadMessage"];

    // Members every generated type declares, and those every C# object inherits.
    private static readonly string[] Reserved =
    [
        "SchemaHash", "BindingHash", "MaxByteCount", "Write", "Read", "ReadKey", "ToBytes", "WriteDelta", "ToDeltaBytes", "ReadDelta", "WireEquals", "CopyFrom", "Quantize", "Equals", "GetHashCode",
        "ToString", "GetType"
    ];

    internal static string Emit(GesSchemaLayout layout, List<GesPlanType> types, GesSchemaGenerateOptions options)
    {
        var access = options.Access == GesSchemaAccess.Public ? "public" : "internal";
        var space = GesSchemaPlanner.Namespace(options.Namespace, Keywords);
        var code = new GesSchemaPlanner.Code();
        code.Raw(GesSchemaPlanner.Header(layout));
        code.Blank();
        code.Line("#nullable enable");
        code.Blank();
        code.Line("using System;");
        code.Line("using System.Collections.Generic;");
        code.Line("using GameEventScript.Api;");
        code.Blank();
        if (space.Count > 0)
        {
            code.Line("namespace " + string.Join(".", space));
            code.Open("{");
        }

        var holder = "__GesSchemas" + GesSchemaPlanner.Hash(GesSchemaPlanner.Header(layout));
        var context = new Context(GesSchemaPlanner.KeyedElements(types), "global::" + string.Concat(space.ConvertAll(part => part + ".")), options.Values, holder);
        for (var index = 0; index < types.Count; index++)
        {
            if (index > 0) code.Blank();
            if (types[index].Kind == GesSchemaKind.Messages) MessageSet(code, types[index], access, context);
            else Record(code, types[index], access, context);
        }

        if (options.Values) ValuesHolder(code, layout, holder);

        if (space.Count > 0) code.Close();
        return code.ToString();
    }

    // The keyed-list elements and the qualifier of the top-level types, which a member of the same name cannot hide.
    private sealed class Context(HashSet<string> elements, string global, bool values, string holder)
    {
        internal HashSet<string> Elements { get; } = elements;

        internal string Global { get; } = global;

        // Whether records convert to and from GES values through the helper type that holds the embedded sources.
        internal bool Values { get; } = values;

        internal string Holder { get; } = holder;
    }

    private static string Kind(GesPlanType type) => type.Kind == GesSchemaKind.Message ? "message" : "schema";

    private static void Record(GesSchemaPlanner.Code code, GesPlanType type, string access, Context context)
    {
        var names = new List<(string, string)> { (type.Name, "the type itself") };
        if (!type.IsItem)
            foreach (var reserved in Reserved) names.Add((reserved, "the generated member " + reserved));
        if (!type.IsItem && context.Values)
            foreach (var reserved in ValueReserved) names.Add((reserved, "the generated member " + reserved));
        foreach (var field in type.Fields)
        {
            names.Add((field.Base, "the field " + field.Layout.Name));
            if (field.Optional) names.Add(("Has" + field.Base, "the presence of " + field.Layout.Name));
            if (field.Optional && Plain(field.Value)) names.Add((field.Base + "Optional", "the optional value of " + field.Layout.Name));
            Nested(field.Value, names, field.Layout.Name);
        }

        foreach (var (tag, name, _) in type.Tags) names.Add((name, "the tag #" + tag));
        GesSchemaPlanner.Unique(type.Name, names);

        var visibility = type.IsItem ? "public" : access;
        code.Line(type.IsItem ? "/// <summary>An element of the list.</summary>" : $"/// <summary>The {Kind(type)} <c>:{type.Name}</c>, read and written without allocation{(type.IsClass ? " once its storage has grown" : "")}.</summary>");
        code.Line(type.IsClass ? $"{visibility} sealed partial class {type.Name}" : $"{visibility} partial struct {type.Name}");
        code.Open("{");
        if (!type.IsItem)
        {
            Constants(code, type.Layout);
            code.Blank();
            code.Line("internal static readonly string[] __paths =");
            code.Open("{");
            foreach (var path in type.Paths) code.Line(Quote(path) + ",");
            code.Close("};");
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

        Methods(code, type);
        var deltas = type.Kind == GesSchemaKind.Schema && !type.IsItem;
        if (deltas) SameHelpers(code, type, context.Global);
        Extras(code, type, deltas, context.Elements.Contains(type.Name));
        if (context.Values && !type.IsItem) ValueMembers(code, type, context.Holder);
        foreach (var field in type.Fields) Declarations(code, field.Value, access, context);
        code.Close();
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
    }

    // Whether an optional value also has a nullable view: a value type without storage.
    private static bool Plain(GesPlanValue value) => !value.IsClass && value.Encoding.Kind is not (GesLayoutKind.Union or GesLayoutKind.Text or GesLayoutKind.List);

    // The nested types and members a value declares, for the collision check.
    private static void Nested(GesPlanValue value, List<(string, string)> names, string field)
    {
        var name = NestedName(value);
        if (name.Length > 0) names.Add((name, "the type of " + field));
        switch (value.Encoding.Kind)
        {
            case GesLayoutKind.Text:
                names.Add((value.Base + "Utf8", "the UTF-8 bytes of " + field));
                names.Add(("Set" + value.Base + "Utf8", "the UTF-8 setter of " + field));
                break;
            case GesLayoutKind.List:
                if (value.Item is not null) names.Add((value.Item.Name, "the element type of " + field));
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
        GesLayoutKind.Tag => value.Base + "Tag",
        GesLayoutKind.Point => value.Base + "Point",
        GesLayoutKind.Vector => value.Base + "Vector",
        GesLayoutKind.Union => value.Base + "Alternative",
        _ => ""
    };

    private static string TypeName(GesPlanValue value) => value.Encoding.Kind switch
    {
        GesLayoutKind.Boolean => "bool",
        GesLayoutKind.Integer => "long",
        GesLayoutKind.Quantized or GesLayoutKind.Float => "double",
        GesLayoutKind.Schema => value.Encoding.Schema,
        GesLayoutKind.Text => "string",
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
        if (value.Encoding.Kind != GesLayoutKind.Text)
        {
            code.Line($"/// <summary>{summary}{Description(value)}.</summary>");
            code.Line($"public {TypeName(value)} {value.Base}{Initializer(value)}");
            return;
        }

        var storage = "__" + value.Base;
        var max = value.Encoding.MaxCount.ToString(CultureInfo.InvariantCulture);
        code.Line($"private byte[] {storage} = Array.Empty<byte>();");
        code.Line($"private int {storage}Length;");
        code.Blank();
        code.Line($"/// <summary>{summary} as UTF-8 bytes{(value.Encoding.TagName ? ", a tag name without #" : "")}.</summary>");
        code.Line($"public ReadOnlySpan<byte> {value.Base}Utf8 => new ReadOnlySpan<byte>({storage}, 0, {storage}Length);");
        code.Blank();
        code.Line($"/// <summary>Sets {summary.Substring(0, 1).ToLowerInvariant() + summary.Substring(1)} from UTF-8 bytes, which are checked when the record is written.</summary>");
        code.Line("/// <param name=\"utf8\">The bytes.</param>");
        code.Line($"public void Set{value.Base}Utf8(ReadOnlySpan<byte> utf8)");
        code.Open("{");
        code.Line($"if ({storage}.Length < utf8.Length) Array.Resize(ref {storage}, Math.Max(utf8.Length, Math.Min({max}, {storage}.Length * 2)));");
        code.Line($"utf8.CopyTo({storage});");
        code.Line($"{storage}Length = utf8.Length;");
        code.Close();
        code.Blank();
        code.Line($"/// <summary>{summary} as a string; reading and setting it allocate.</summary>");
        code.Line($"public string {value.Base}");
        code.Open("{");
        code.Line($"get => System.Text.Encoding.UTF8.GetString({storage}, 0, {storage}Length);");
        code.Line($"set => Set{value.Base}Utf8(System.Text.Encoding.UTF8.GetBytes(value));");
        code.Close();
    }

    private static string Description(GesPlanValue value)
    {
        var encoding = value.Encoding;
        return encoding.Kind switch
        {
            GesLayoutKind.Quantized or GesLayoutKind.Float or GesLayoutKind.Integer when encoding.Unit != "none" => $", in {encoding.Unit}",
            GesLayoutKind.Point or GesLayoutKind.Vector when encoding.Unit != "none" => $", in {encoding.Unit}",
            GesLayoutKind.Union => ", the selected alternative",
            GesLayoutKind.List => $", at most {encoding.MaxCount.ToString(CultureInfo.InvariantCulture)} elements",
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

        code.Blank();
        code.Line($"internal {self}void __WriteFields(ref GesBitWriter writer, string[] paths, int at)");
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
        code.Line("internal void __ReadFields(ref GesBitReader reader, string[] paths, int at)");
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
        code.Line($"/// <summary>Writes this {what} into a new array of its exact size.</summary>");
        code.Line("/// <returns>The bytes.</returns>");
        code.Line("/// <exception cref=\"GesWireException\">A value does not fit its field.</exception>");
        code.Line($"public {self}byte[] ToBytes()");
        code.Open("{");
        code.Line("var buffer = new byte[MaxByteCount];");
        code.Line("return buffer.AsSpan(0, Write(buffer)).ToArray();");
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
        var names = new List<(string, string)> { (type.Name, "the type itself"), ("Selected", "the selected member"), ("Member", "the member type") };
        foreach (var reserved in Reserved) names.Add((reserved, "the generated member " + reserved));
        if (context.Values)
            foreach (var reserved in ValueReserved) names.Add((reserved, "the generated member " + reserved));
        foreach (var member in type.Members) names.Add((member, "the member " + member));
        GesSchemaPlanner.Unique(type.Name, names);
        var width = type.Layout.MemberWidth.ToString(CultureInfo.InvariantCulture);
        var last = Unsigned((ulong)type.Members.Count - 1);
        code.Line($"/// <summary>The message set <c>:{type.Name}</c>: one of its messages, read and written without allocation{(type.MembersAreClasses ? " once its storage has grown" : "")}.</summary>");
        code.Line(type.MembersAreClasses ? $"{access} sealed partial class {type.Name}" : $"{access} partial struct {type.Name}");
        code.Open("{");
        Constants(code, type.Layout);
        code.Blank();
        code.Line("/// <summary>The message this record holds.</summary>");
        code.Line("public Member Selected;");
        foreach (var member in type.Members)
        {
            code.Blank();
            code.Line($"/// <summary>The message when <see cref=\"Selected\"/> is <c>{member}</c>.</summary>");
            code.Line($"public {member} {member}{(type.MembersAreClasses ? $" = new {member}();" : ";")}");
        }

        var write = new List<string> { $"if ((ulong)Selected > {last}) throw new GesWireException(GesWireException.OutOfRange, \"\");", $"writer.Write((ulong)Selected, {width});", "switch (Selected)", "{" };
        var read = new List<string> { $"var index = reader.Read({width}, \"\");" };
        if ((ulong)type.Members.Count - 1 < Full(type.Layout.MemberWidth)) read.Add($"if (index > {last}) throw new GesWireException(GesWireException.InvalidIndex, \"\");");
        read.AddRange(["Selected = (Member)index;", "switch (Selected)", "{"]);
        foreach (var member in type.Members)
        {
            write.AddRange([$"    case Member.{member}:", $"        {member}.__WriteFields(ref writer, {member}.__paths, 0);", "        break;"]);
            read.AddRange([$"    case Member.{member}:", $"        {member}.__ReadFields(ref reader, {member}.__paths, 0);", "        break;"]);
        }

        write.Add("}");
        read.Add("}");
        code.Blank();
        PublicMethods(code, type.MembersAreClasses ? "" : "readonly ", write, read, "message");
        if (context.Values) ValueMembers(code, type, context.Holder);
        code.Blank();
        code.Line("/// <summary>The messages of the set, in declared order.</summary>");
        code.Line($"public enum Member : {Underlying(type.Members.Count)}");
        code.Open("{");
        foreach (var member in type.Members)
        {
            code.Line($"/// <summary>The message <c>{member}</c>.</summary>");
            code.Line(member + ",");
        }

        code.Close();
        code.Close();
    }

    private static string Path(GesPlanValue value) => $"paths[at + {value.Node.ToString(CultureInfo.InvariantCulture)}]";

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
                code.Line($"writer.Write(unchecked((ulong)BitConverter.DoubleToInt64Bits({target} == 0 ? 0d : {target})), 64);");
                break;
            case GesLayoutKind.Tag:
                code.Line($"if ((ulong){target} > {Unsigned((ulong)encoding.Tags.Count - 1)}) {Throw("UnknownTag", value)}");
                code.Line($"writer.Write((ulong){target}, {width});");
                break;
            case GesLayoutKind.Text:
                code.Open("{");
                code.Line($"var text = new ReadOnlySpan<byte>(__{value.Base}, 0, __{value.Base}Length);");
                code.Line($"if (text.Length > {max}) {Throw("TooLong", value)}");
                code.Line(encoding.TagName ? $"if (!GesWireRules.IsTagName(text)) {Throw("InvalidTag", value)}" : $"if (!GesWireRules.IsValidUtf8(text)) {Throw("InvalidText", value)}");
                code.Line($"writer.Write((ulong)text.Length, {width});");
                code.Line("writer.WriteBytes(text);");
                code.Close();
                break;
            case GesLayoutKind.List:
                code.Line($"if ({target}.Count > {max}) {Throw("TooLong", value)}");
                code.Line($"writer.Write((ulong){target}.Count, {width});");
                code.Line($"foreach (var item in {target})");
                code.Open("{");
                if (value.Item is not null) code.Line($"item.__WriteFields(ref writer, paths, at + {value.Element!.Node.ToString(CultureInfo.InvariantCulture)});");
                else Write(code, value.Element!, "item");
                code.Close();
                break;
            case GesLayoutKind.Point or GesLayoutKind.Vector:
                foreach (var axis in value.Axes) Write(code, axis, target + "." + axis.Base);
                break;
            case GesLayoutKind.Schema:
                code.Line($"{target}.__WriteFields(ref writer, paths, at + {(value.Node + 1).ToString(CultureInfo.InvariantCulture)});");
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
                code.Line("var value = BitConverter.Int64BitsToDouble(unchecked((long)bits));");
                code.Line($"if (double.IsNaN(value) || bits == 0x8000000000000000UL{(encoding.Range ? " || double.IsInfinity(value)" : "")}) {Throw("InvalidNumber", value)}");
                if (encoding.Range) code.Line($"if (value < {Bits(encoding.LowerValue)} || value > {Bits(encoding.UpperValue)}) {Throw("OutOfRange", value)}");
                code.Line($"{target} = value;");
                code.Close();
                break;
            case GesLayoutKind.Text:
                var storage = "__" + value.Base;
                code.Open("{");
                code.Line($"var count = (int)reader.Read({width}, {Path(value)});");
                code.Line($"if (count > {max}) {Throw("InvalidCount", value)}");
                code.Line($"reader.Require(count * 8L, {Path(value)});");
                code.Line($"if ({storage}.Length < count) Array.Resize(ref {storage}, Math.Max(count, Math.Min({max}, {storage}.Length * 2)));");
                code.Line($"var text = new Span<byte>({storage}, 0, count);");
                code.Line($"reader.ReadBytes(text, {Path(value)});");
                code.Line(encoding.TagName ? $"if (!GesWireRules.IsTagName(text)) {Throw("InvalidTag", value)}" : $"if (!GesWireRules.IsValidUtf8(text)) {Throw("InvalidText", value)}");
                code.Line($"{storage}Length = count;");
                code.Close();
                break;
            case GesLayoutKind.List:
                var element = value.Element!;
                var name = ElementName(value);
                code.Open("{");
                code.Line($"var count = (int)reader.Read({width}, {Path(value)});");
                code.Line($"if (count > {max}) {Throw("InvalidCount", value)}");
                code.Line($"{target}.Clear();");
                code.Line("for (var position = 0; position < count; position++)");
                code.Open("{");
                if (element.IsClass)
                {
                    code.Line($"if (position == __{value.Base}Pool.Count) __{value.Base}Pool.Add(new {name}());");
                    code.Line($"var item = __{value.Base}Pool[position];");
                }
                else if (value.Item is not null || element.Encoding.Kind is GesLayoutKind.Point or GesLayoutKind.Vector or GesLayoutKind.Schema) code.Line($"var item = new {name}();");
                else code.Line($"{name} item;");
                if (value.Item is not null) code.Line($"item.__ReadFields(ref reader, paths, at + {element.Node.ToString(CultureInfo.InvariantCulture)});");
                else Read(code, element, "item");
                code.Line($"{target}.Add(item);");
                code.Close();
                code.Close();
                break;
            case GesLayoutKind.Point or GesLayoutKind.Vector:
                foreach (var axis in value.Axes) Read(code, axis, target + "." + axis.Base);
                break;
            case GesLayoutKind.Schema:
                code.Line($"{target}.__ReadFields(ref reader, paths, at + {(value.Node + 1).ToString(CultureInfo.InvariantCulture)});");
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

    private static void Declarations(GesSchemaPlanner.Code code, GesPlanValue value, string access, Context context)
    {
        var name = NestedName(value);
        switch (value.Encoding.Kind)
        {
            case GesLayoutKind.Tag:
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
            case GesLayoutKind.Point or GesLayoutKind.Vector:
                code.Blank();
                code.Line($"/// <summary>The transmitted axes of the {(value.Encoding.Kind == GesLayoutKind.Point ? "Point" : "Vector")}.</summary>");
                code.Line($"public struct {name}");
                code.Open("{");
                for (var index = 0; index < value.Axes.Count; index++)
                {
                    if (index > 0) code.Blank();
                    code.Line($"/// <summary>The {value.Axes[index].Label} axis.</summary>");
                    code.Line($"public double {value.Axes[index].Base};");
                }

                code.Close();
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

    private static ulong Full(int width) => width >= 64 ? ulong.MaxValue : (1UL << width) - 1;

    private static string Long(long value) => value == long.MinValue ? "long.MinValue" : value.ToString(CultureInfo.InvariantCulture) + "L";

    private static string Unsigned(ulong value) => value.ToString(CultureInfo.InvariantCulture) + "UL";

    // An integral binary64 such as 10^k or p, exact as a literal.
    private static string Double(long value) => value.ToString(CultureInfo.InvariantCulture) + "d";

    private static string Bits(double value) => $"BitConverter.Int64BitsToDouble(unchecked((long)0x{GesSchemaLayoutJson.Hex((ulong)BitConverter.DoubleToInt64Bits(value))}UL))";

    private static string Quote(string text) => "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
