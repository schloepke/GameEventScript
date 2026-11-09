// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Globalization;

namespace GameEventScript.Generator;

// Writes C# 9 for Unity and .NET: a struct per fixed-width schema with fields in wire order, Write and Read over the bit
// primitives, and constants inlined as literals or bit patterns.
internal static class GesCSharpEmitter
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

    // Members every generated type declares, and those every C# struct inherits.
    private static readonly string[] Reserved = ["SchemaHash", "BindingHash", "MaxByteCount", "Write", "Read", "ReadKey", "ToBytes", "Equals", "GetHashCode", "ToString", "GetType"];

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
        code.Line("using GameEventScript.Api;");
        code.Blank();
        if (space.Count > 0)
        {
            code.Line("namespace " + string.Join(".", space));
            code.Open("{");
        }

        for (var index = 0; index < types.Count; index++)
        {
            if (index > 0) code.Blank();
            Type(code, types[index], access);
        }

        if (space.Count > 0) code.Close();
        return code.ToString();
    }

    private static void Type(GesSchemaPlanner.Code code, GesPlanType type, string access)
    {
        var names = new List<(string, string)> { (type.Name, "the type itself") };
        foreach (var reserved in Reserved) names.Add((reserved, "the generated member " + reserved));
        foreach (var field in type.Fields)
        {
            names.Add((field.Base, "the field " + field.Layout.Name));
            if (field.Optional) names.Add(("Has" + field.Base, "the presence of " + field.Layout.Name));
            Nested(field.Value, names, field.Layout.Name);
        }

        GesSchemaPlanner.Unique(type.Name, names);

        code.Line($"/// <summary>The schema <c>:{type.Name}</c>, read and written without allocation.</summary>");
        code.Line($"{access} partial struct {type.Name}");
        code.Open("{");
        code.Line("/// <summary>The schema hash, independent of parameter values.</summary>");
        code.Line($"public const ulong SchemaHash = 0x{GesSchemaLayoutJson.Hex(type.Layout.Hash)}UL;");
        code.Blank();
        code.Line("/// <summary>The hash over the schema hash and the parameter values this code was generated with.</summary>");
        code.Line($"public const ulong BindingHash = 0x{GesSchemaLayoutJson.Hex(type.Layout.BindingHash)}UL;");
        code.Blank();
        code.Line("/// <summary>The size of the largest encoding in bytes.</summary>");
        code.Line($"public const int MaxByteCount = {type.Layout.MaxByteCount.ToString(CultureInfo.InvariantCulture)};");
        code.Blank();
        code.Line("internal static readonly string[] __paths =");
        code.Open("{");
        foreach (var path in type.Paths) code.Line(Quote(path) + ",");
        code.Close("};");
        foreach (var field in type.Fields) Members(code, field);
        code.Blank();
        Methods(code, type);
        foreach (var field in type.Fields) Declarations(code, field.Value, access);
        code.Close();
    }

    // The nested types a value declares, for the collision check.
    private static void Nested(GesPlanValue value, List<(string, string)> names, string field)
    {
        var name = NestedName(value);
        if (name.Length > 0) names.Add((name, "the type of " + field));
        if (value.Encoding.Kind == GesLayoutKind.Union)
        {
            foreach (var alternative in value.Alternatives)
            {
                names.Add((alternative.Base, $"the alternative {alternative.Label} of {field}"));
                Nested(alternative, names, field);
            }
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
        _ => NestedName(value)
    };

    private static void Members(GesSchemaPlanner.Code code, GesPlanField field)
    {
        code.Blank();
        if (field.Optional)
        {
            code.Line($"/// <summary>Whether <c>{field.Layout.Name}</c> is present.</summary>");
            code.Line($"public bool Has{field.Base};");
            code.Blank();
        }

        code.Line($"/// <summary>The field <c>{field.Layout.Name}</c>{Description(field.Value)}.</summary>");
        code.Line($"public {TypeName(field.Value)} {field.Base};");
        if (field.Value.Encoding.Kind != GesLayoutKind.Union) return;
        foreach (var alternative in field.Value.Alternatives)
        {
            if (alternative.Encoding.Kind == GesLayoutKind.Nothing) continue;
            code.Blank();
            code.Line($"/// <summary>The value of <c>{field.Layout.Name}</c> when its alternative is <c>{alternative.Label}</c>{Description(alternative)}.</summary>");
            code.Line($"public {TypeName(alternative)} {alternative.Base};");
        }
    }

    private static string Description(GesPlanValue value)
    {
        var encoding = value.Encoding;
        return encoding.Kind switch
        {
            GesLayoutKind.Quantized or GesLayoutKind.Float or GesLayoutKind.Integer when encoding.Unit != "none" => $", in {encoding.Unit}",
            GesLayoutKind.Point or GesLayoutKind.Vector when encoding.Unit != "none" => $", in {encoding.Unit}",
            GesLayoutKind.Union => ", the selected alternative",
            _ => ""
        };
    }

    private static void Methods(GesSchemaPlanner.Code code, GesPlanType type)
    {
        code.Line("/// <summary>Writes this record into a buffer; <see cref=\"MaxByteCount\"/> bytes always suffice.</summary>");
        code.Line("/// <param name=\"buffer\">The buffer.</param>");
        code.Line("/// <returns>The number of bytes written.</returns>");
        code.Line("/// <exception cref=\"GesWireException\">A value does not fit its field, or the buffer is too small.</exception>");
        code.Line("public readonly int Write(Span<byte> buffer)");
        code.Open("{");
        code.Line("var writer = new GesBitWriter(buffer);");
        code.Line("__WriteFields(ref writer, __paths, 0);");
        code.Line("return writer.Finish();");
        code.Close();
        code.Blank();
        code.Line("/// <summary>Writes this record into a new array of its exact size.</summary>");
        code.Line("/// <returns>The bytes.</returns>");
        code.Line("/// <exception cref=\"GesWireException\">A value does not fit its field.</exception>");
        code.Line("public readonly byte[] ToBytes()");
        code.Open("{");
        code.Line("var buffer = new byte[MaxByteCount];");
        code.Line("return buffer.AsSpan(0, Write(buffer)).ToArray();");
        code.Close();
        code.Blank();
        code.Line("/// <summary>Reads a record from untrusted data into this instance. After an error its values are unspecified.</summary>");
        code.Line("/// <param name=\"data\">The data; it may continue after the record.</param>");
        code.Line("/// <param name=\"byteLimit\">An optional limit on the bytes of this record.</param>");
        code.Line("/// <returns>The number of bytes the record occupies.</returns>");
        code.Line("/// <exception cref=\"GesWireException\">The data is no valid record.</exception>");
        code.Line("public int Read(ReadOnlySpan<byte> data, long byteLimit = long.MaxValue)");
        code.Open("{");
        code.Line("var reader = new GesBitReader(data, byteLimit);");
        code.Line("__ReadFields(ref reader, __paths, 0);");
        code.Line("return reader.Finish();");
        code.Close();
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

        code.Blank();
        code.Line("internal readonly void __WriteFields(ref GesBitWriter writer, string[] paths, int at)");
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

        code.Close();
        code.Blank();
        code.Line("internal void __ReadFields(ref GesBitReader reader, string[] paths, int at)");
        code.Open("{");
        foreach (var field in type.Fields)
        {
            if (field.Optional)
            {
                code.Line($"Has{field.Base} = reader.Read(1, {Path(field.Value)}) != 0;");
                code.Line($"if (!Has{field.Base}) {field.Base} = default;");
                code.Line("else");
                code.Open("{");
                Read(code, field.Value, field.Base);
                code.Close();
            }
            else Read(code, field.Value, field.Base);
        }

        code.Close();
    }

    private static string Path(GesPlanValue value) => $"paths[at + {value.Node.ToString(CultureInfo.InvariantCulture)}]";

    private static string Throw(string code, GesPlanValue value) => $"throw new GesWireException(GesWireException.{code}, {Path(value)});";

    // Writes the value in `target`, which the caller has validated as present.
    private static void Write(GesSchemaPlanner.Code code, GesPlanValue value, string target)
    {
        var encoding = value.Encoding;
        var width = encoding.Width.ToString(CultureInfo.InvariantCulture);
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

    private static void Declarations(GesSchemaPlanner.Code code, GesPlanValue value, string access)
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
                foreach (var alternative in value.Alternatives) Declarations(code, alternative, access);
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
