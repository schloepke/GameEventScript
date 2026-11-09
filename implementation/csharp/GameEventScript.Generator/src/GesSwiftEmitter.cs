// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Globalization;

namespace GameEventScript.Generator;

// Writes Swift 6 without Foundation: a Sendable struct per fixed-width schema with properties in wire order, write and read
// over the bit primitives, and constants inlined as literals or bit patterns.
internal static class GesSwiftEmitter
{
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "Any", "Protocol", "Self", "Type", "as", "associatedtype", "break", "case", "catch", "class", "continue", "default",
        "defer", "deinit", "do", "else", "enum", "extension", "fallthrough", "false", "fileprivate", "for", "func", "guard",
        "if", "import", "in", "init", "inout", "internal", "is", "let", "nil", "open", "operator", "private", "precedencegroup",
        "protocol", "public", "repeat", "rethrows", "return", "self", "static", "struct", "subscript", "super", "switch",
        "throw", "throws", "true", "try", "typealias", "var", "where", "while"
    };

    private static readonly string[] Reserved = ["schemaHash", "bindingHash", "maxByteCount", "write", "read", "readKey", "encoded"];

    internal static string Emit(GesSchemaLayout layout, List<GesPlanType> types, GesSchemaGenerateOptions options)
    {
        var access = options.Access == GesSchemaAccess.Public ? "public " : "";
        var space = GesSchemaPlanner.Namespace(options.Namespace, Keywords);
        var code = new GesSchemaPlanner.Code();
        code.Raw(GesSchemaPlanner.Header(layout));
        code.Blank();
        code.Line("import GameEventScriptRuntime");
        code.Blank();
        foreach (var part in space)
        {
            code.Line($"/// The namespace `{part}` of generated schema types.");
            code.Open($"{access}enum {part} {{");
        }

        for (var index = 0; index < types.Count; index++)
        {
            if (index > 0) code.Blank();
            Type(code, types[index], access);
        }

        foreach (var _ in space) code.Close();
        return code.ToString();
    }

    private static string Name(string name) => Keywords.Contains(name) ? "`" + name + "`" : name;

    private static string Member(string pascal) => Name(GesSchemaPlanner.Camel(pascal));

    private static void Type(GesSchemaPlanner.Code code, GesPlanType type, string access)
    {
        var names = new List<(string, string)>();
        foreach (var reserved in Reserved) names.Add((reserved, "the generated member " + reserved));
        foreach (var field in type.Fields)
        {
            names.Add((GesSchemaPlanner.Camel(field.Base), "the field " + field.Layout.Name));
            if (field.Optional) names.Add(("has" + field.Base, "the presence of " + field.Layout.Name));
            Nested(field.Value, names, field.Layout.Name);
        }

        GesSchemaPlanner.Unique(type.Name, names);

        code.Line($"/// The schema `:{type.Name}`, read and written without allocation.");
        code.Open($"{access}struct {type.Name}: Sendable {{");
        code.Line("/// The schema hash, independent of parameter values.");
        code.Line($"{access}static let schemaHash: UInt64 = 0x{GesSchemaLayoutJson.Hex(type.Layout.Hash)}");
        code.Line("/// The hash over the schema hash and the parameter values this code was generated with.");
        code.Line($"{access}static let bindingHash: UInt64 = 0x{GesSchemaLayoutJson.Hex(type.Layout.BindingHash)}");
        code.Line("/// The size of the largest encoding in bytes.");
        code.Line($"{access}static let maxByteCount = {type.Layout.MaxByteCount.ToString(CultureInfo.InvariantCulture)}");
        code.Line("static let __paths: [String] = [");
        foreach (var path in type.Paths) code.Line("    " + Quote(path) + ",");
        code.Line("]");
        foreach (var field in type.Fields) Members(code, field, access);
        code.Blank();
        code.Line("/// Creates a record with every field at its zero value.");
        code.Line($"{access}init() {{}}");
        code.Blank();
        Methods(code, type, access);
        foreach (var field in type.Fields) Declarations(code, field.Value, access);
        code.Close();
    }

    private static void Nested(GesPlanValue value, List<(string, string)> names, string field)
    {
        var name = NestedName(value);
        if (name.Length > 0) names.Add((name, "the type of " + field));
        if (value.Encoding.Kind != GesLayoutKind.Union) return;
        foreach (var alternative in value.Alternatives)
        {
            names.Add((GesSchemaPlanner.Camel(alternative.Base), $"the alternative {alternative.Label} of {field}"));
            Nested(alternative, names, field);
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
        GesLayoutKind.Boolean => "Bool",
        GesLayoutKind.Integer => "Int64",
        GesLayoutKind.Quantized or GesLayoutKind.Float => "Double",
        GesLayoutKind.Schema => value.Encoding.Schema,
        _ => NestedName(value)
    };

    private static string Zero(GesPlanValue value) => value.Encoding.Kind switch
    {
        GesLayoutKind.Boolean => "false",
        GesLayoutKind.Integer => "0",
        GesLayoutKind.Quantized or GesLayoutKind.Float => "0.0",
        GesLayoutKind.Tag => "." + Name(value.Encoding.Tags[0]),
        GesLayoutKind.Union => "." + Member(value.Alternatives[0].Label),
        _ => TypeName(value) + "()"
    };

    private static void Members(GesSchemaPlanner.Code code, GesPlanField field, string access)
    {
        code.Blank();
        if (field.Optional)
        {
            code.Line($"/// Whether `{field.Layout.Name}` is present.");
            code.Line($"{access}var has{field.Base} = false");
        }

        code.Line($"/// The field `{field.Layout.Name}`{Description(field.Value)}.");
        code.Line($"{access}var {Member(field.Base)}: {TypeName(field.Value)} = {Zero(field.Value)}");
        if (field.Value.Encoding.Kind != GesLayoutKind.Union) return;
        foreach (var alternative in field.Value.Alternatives)
        {
            if (alternative.Encoding.Kind == GesLayoutKind.Nothing) continue;
            code.Line($"/// The value of `{field.Layout.Name}` when its alternative is `{alternative.Label}`{Description(alternative)}.");
            code.Line($"{access}var {Member(alternative.Base)}: {TypeName(alternative)} = {Zero(alternative)}");
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

    private static void Methods(GesSchemaPlanner.Code code, GesPlanType type, string access)
    {
        code.Line("/// Writes this record into a buffer; `maxByteCount` bytes always suffice.");
        code.Line("/// - Returns: The number of bytes written.");
        code.Line("/// - Throws: `GesWireError` when a value does not fit its field or the buffer is too small.");
        code.Open($"{access}func write(into buffer: UnsafeMutableRawBufferPointer) throws(GesWireError) -> Int {{");
        code.Line("var writer = GesBitWriter(into: buffer)");
        code.Line("try __writeFields(&writer, Self.__paths, 0)");
        code.Line("return try writer.finish()");
        code.Close();
        code.Blank();
        code.Line("/// Writes this record into a new array of its exact size.");
        code.Line("/// - Throws: `GesWireError` when a value does not fit its field.");
        code.Open($"{access}func encoded() throws(GesWireError) -> [UInt8] {{");
        code.Line("let buffer = UnsafeMutableRawBufferPointer.allocate(byteCount: Swift.max(Self.maxByteCount, 1), alignment: 8)");
        code.Line("defer { buffer.deallocate() }");
        code.Line("let count = try write(into: buffer)");
        code.Line("return Array(buffer[0..<count])");
        code.Close();
        code.Blank();
        code.Line("/// Reads a record from untrusted data into this instance. After an error its values are unspecified.");
        code.Line("/// - Parameters:");
        code.Line("///   - data: The data; it may continue after the record.");
        code.Line("///   - byteLimit: An optional limit on the bytes of this record.");
        code.Line("/// - Returns: The number of bytes the record occupies.");
        code.Line("/// - Throws: `GesWireError` when the data is no valid record.");
        code.Line("@discardableResult");
        code.Open($"{access}mutating func read(from data: UnsafeRawBufferPointer, byteLimit: Int = .max) throws(GesWireError) -> Int {{");
        code.Line("var reader = GesBitReader(data: data, byteLimit: byteLimit)");
        code.Line("try __readFields(&reader, Self.__paths, 0)");
        code.Line("return try reader.finish()");
        code.Close();
        code.Blank();
        code.Line("/// Reads a record from bytes into this instance. After an error its values are unspecified.");
        code.Line("/// - Returns: The number of bytes the record occupies.");
        code.Line("/// - Throws: `GesWireError` when the bytes are no valid record.");
        code.Line("@discardableResult");
        code.Open($"{access}mutating func read(from bytes: [UInt8], byteLimit: Int = .max) throws(GesWireError) -> Int {{");
        code.Line("let buffer = UnsafeMutableRawBufferPointer.allocate(byteCount: Swift.max(bytes.count, 1), alignment: 8)");
        code.Line("defer { buffer.deallocate() }");
        code.Line("buffer.copyBytes(from: bytes)");
        code.Line("return try read(from: UnsafeRawBufferPointer(rebasing: buffer[0..<bytes.count]), byteLimit: byteLimit)");
        code.Close();
        var key = type.Fields.Count > 0 && type.Fields[0].Key ? type.Fields[0] : null;
        if (key is not null)
        {
            code.Blank();
            code.Line("/// Reads the key, which comes first, before a caller chooses a baseline.");
            code.Line("/// - Throws: `GesWireError` when the data ends before the key does or holds an invalid key.");
            code.Open($"{access}static func readKey(from data: UnsafeRawBufferPointer) throws(GesWireError) -> {TypeName(key.Value)} {{");
            code.Line("var reader = GesBitReader(data: data)");
            code.Line("let paths = Self.__paths");
            code.Line("let at = 0");
            code.Line($"var key: {TypeName(key.Value)} = {Zero(key.Value)}");
            Read(code, key.Value, "key");
            code.Line("return key");
            code.Close();
        }

        code.Blank();
        code.Open("func __writeFields(_ writer: inout GesBitWriter, _ paths: [String], _ at: Int) throws(GesWireError) {");
        foreach (var field in type.Fields)
        {
            var target = "self." + Member(field.Base);
            if (field.Optional)
            {
                code.Open($"if self.has{field.Base} {{");
                code.Line("try writer.write(1, width: 1)");
                Write(code, field.Value, target);
                code.Close("} else {");
                code.Line("    try writer.write(0, width: 1)");
                code.Line("}");
            }
            else Write(code, field.Value, target);
        }

        code.Close();
        code.Blank();
        code.Open("mutating func __readFields(_ reader: inout GesBitReader, _ paths: [String], _ at: Int) throws(GesWireError) {");
        foreach (var field in type.Fields)
        {
            var target = "self." + Member(field.Base);
            if (field.Optional)
            {
                code.Line($"self.has{field.Base} = try reader.read(1, path: {Path(field.Value)}) != 0");
                code.Open($"if self.has{field.Base} {{");
                Read(code, field.Value, target);
                code.Close("} else {");
                code.Line($"    {target} = {Zero(field.Value)}");
                code.Line("}");
            }
            else Read(code, field.Value, target);
        }

        code.Close();
    }

    private static string Path(GesPlanValue value) => $"paths[at + {value.Node.ToString(CultureInfo.InvariantCulture)}]";

    private static string Throw(string code, GesPlanValue value) => $"throw GesWireError(code: GesWireError.{code}, path: {Path(value)})";

    private static void Write(GesSchemaPlanner.Code code, GesPlanValue value, string target)
    {
        var encoding = value.Encoding;
        var width = encoding.Width.ToString(CultureInfo.InvariantCulture);
        switch (encoding.Kind)
        {
            case GesLayoutKind.Boolean:
                code.Line($"try writer.write({target} ? 1 : 0, width: 1)");
                break;
            case GesLayoutKind.Integer when !encoding.Range:
                code.Line($"try writer.write(UInt64(bitPattern: {target}), width: 64)");
                break;
            case GesLayoutKind.Integer when encoding.Wrapping:
                code.Line($"try writer.write(GesWireRules.wrappedIndex({target}, lower: {Long(encoding.Lower)}, period: {Unsigned(encoding.MaxIndex + 1)}), width: {width})");
                break;
            case GesLayoutKind.Integer:
                var lower = Unsigned(unchecked((ulong)encoding.Lower));
                code.Line($"if {target} < {Long(encoding.Lower)} || UInt64(bitPattern: {target}) &- {lower} > {Unsigned(encoding.MaxIndex)} {{ {Throw("outOfRange", value)} }}");
                code.Line($"try writer.write(UInt64(bitPattern: {target}) &- {lower}, width: {width})");
                break;
            case GesLayoutKind.Quantized when encoding.Wrapping:
                code.Line($"try writer.write(");
                code.Line($"    try GesWireRules.wrappedQuantizedIndex({target}, tenPower: {Double(encoding.TenPower)}, numerator: {Double(encoding.Numerator)}, lower: {Long(encoding.Lower)}, period: {Unsigned(encoding.MaxIndex + 1)}, path: {Path(value)}),");
                code.Line($"    width: {width}");
                code.Line(")");
                break;
            case GesLayoutKind.Quantized:
                code.Line($"try writer.write(");
                code.Line($"    try GesWireRules.quantizedIndex({target}, lowerValue: {Bits(encoding.LowerValue)}, upperValue: {Bits(encoding.UpperValue)}, tenPower: {Double(encoding.TenPower)}, numerator: {Double(encoding.Numerator)}, lower: {Long(encoding.Lower)}, path: {Path(value)}),");
                code.Line($"    width: {width}");
                code.Line(")");
                break;
            case GesLayoutKind.Float:
                code.Line($"if {target}.isNaN {{ {Throw("notRepresentable", value)} }}");
                if (encoding.Range) code.Line($"if {target} < {Bits(encoding.LowerValue)} || {target} > {Bits(encoding.UpperValue)} {{ {Throw("outOfRange", value)} }}");
                code.Line($"try writer.write(({target} == 0 ? 0.0 : {target}).bitPattern, width: 64)");
                break;
            case GesLayoutKind.Tag:
                code.Line($"try writer.write(UInt64({target}.rawValue), width: {width})");
                break;
            case GesLayoutKind.Point or GesLayoutKind.Vector:
                foreach (var axis in value.Axes) Write(code, axis, target + "." + Member(axis.Base));
                break;
            case GesLayoutKind.Schema:
                code.Line($"try {target}.__writeFields(&writer, paths, at + {(value.Node + 1).ToString(CultureInfo.InvariantCulture)})");
                break;
            case GesLayoutKind.Union:
                code.Line($"switch {target} {{");
                for (var index = 0; index < value.Alternatives.Count; index++)
                {
                    var alternative = value.Alternatives[index];
                    code.Open($"case .{Member(alternative.Label)}:");
                    code.Line($"try writer.write({index.ToString(CultureInfo.InvariantCulture)}, width: {width})");
                    if (alternative.Encoding.Kind != GesLayoutKind.Nothing) Write(code, alternative, "self." + Member(alternative.Base));
                    code.Close("");
                }

                code.Line("}");
                break;
        }
    }

    private static void Read(GesSchemaPlanner.Code code, GesPlanValue value, string target)
    {
        var encoding = value.Encoding;
        var width = encoding.Width.ToString(CultureInfo.InvariantCulture);
        switch (encoding.Kind)
        {
            case GesLayoutKind.Boolean:
                code.Line($"{target} = try reader.read(1, path: {Path(value)}) != 0");
                break;
            case GesLayoutKind.Integer when !encoding.Range:
                code.Line($"{target} = Int64(bitPattern: try reader.read(64, path: {Path(value)}))");
                break;
            case GesLayoutKind.Integer or GesLayoutKind.Quantized or GesLayoutKind.Tag:
                code.Open("do {");
                code.Line($"let index = try reader.read({width}, path: {Path(value)})");
                var largest = encoding.Kind == GesLayoutKind.Tag ? (ulong)encoding.Tags.Count - 1 : encoding.MaxIndex;
                if (largest < Full(encoding.Width)) code.Line($"if index > {Unsigned(largest)} {{ {Throw("invalidIndex", value)} }}");
                code.Line(encoding.Kind switch
                {
                    GesLayoutKind.Integer => $"{target} = Int64(bitPattern: {Unsigned(unchecked((ulong)encoding.Lower))} &+ index)",
                    GesLayoutKind.Quantized => $"{target} = GesWireRules.quantizedValue(index, lower: {Long(encoding.Lower)}, numerator: {Long(encoding.Numerator)}, tenPower: {Double(encoding.TenPower)})",
                    _ => $"{target} = {NestedName(value)}(rawValue: {Underlying(encoding.Tags.Count)}(index))!"
                });
                code.Close();
                break;
            case GesLayoutKind.Float:
                code.Open("do {");
                code.Line($"let bits = try reader.read(64, path: {Path(value)})");
                code.Line("let value = Double(bitPattern: bits)");
                code.Line($"if value.isNaN || bits == 0x8000_0000_0000_0000{(encoding.Range ? " || value.isInfinite" : "")} {{ {Throw("invalidNumber", value)} }}");
                if (encoding.Range) code.Line($"if value < {Bits(encoding.LowerValue)} || value > {Bits(encoding.UpperValue)} {{ {Throw("outOfRange", value)} }}");
                code.Line($"{target} = value");
                code.Close();
                break;
            case GesLayoutKind.Point or GesLayoutKind.Vector:
                foreach (var axis in value.Axes) Read(code, axis, target + "." + Member(axis.Base));
                break;
            case GesLayoutKind.Schema:
                code.Line($"try {target}.__readFields(&reader, paths, at + {(value.Node + 1).ToString(CultureInfo.InvariantCulture)})");
                break;
            case GesLayoutKind.Union:
                code.Open("do {");
                code.Line($"let choice = try reader.read({width}, path: {Path(value)})");
                if ((ulong)value.Alternatives.Count - 1 < Full(encoding.Width)) code.Line($"if choice > {Unsigned((ulong)value.Alternatives.Count - 1)} {{ {Throw("invalidIndex", value)} }}");
                code.Line($"{target} = {NestedName(value)}(rawValue: {Underlying(value.Alternatives.Count)}(choice))!");
                code.Line("switch choice {");
                for (var index = 0; index < value.Alternatives.Count; index++)
                {
                    var alternative = value.Alternatives[index];
                    if (alternative.Encoding.Kind == GesLayoutKind.Nothing) continue;
                    code.Open($"case {index.ToString(CultureInfo.InvariantCulture)}:");
                    Read(code, alternative, "self." + Member(alternative.Base));
                    code.Close("");
                }

                code.Open("default:");
                code.Line("break");
                code.Close("}");
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
                code.Line("/// The tags of the field, in declared order.");
                code.Open($"{access}enum {name}: {Underlying(value.Encoding.Tags.Count)}, Sendable {{");
                foreach (var tag in value.Encoding.Tags)
                {
                    code.Line($"/// The tag `#{tag}`.");
                    code.Line($"case {Name(tag)}");
                }

                code.Close();
                break;
            case GesLayoutKind.Point or GesLayoutKind.Vector:
                code.Blank();
                code.Line($"/// The transmitted axes of the {(value.Encoding.Kind == GesLayoutKind.Point ? "Point" : "Vector")}.");
                code.Open($"{access}struct {name}: Sendable {{");
                foreach (var axis in value.Axes)
                {
                    code.Line($"/// The {axis.Label} axis.");
                    code.Line($"{access}var {Member(axis.Base)} = 0.0");
                }

                code.Blank();
                code.Line("/// Creates a value with every axis at zero.");
                code.Line($"{access}init() {{}}");
                code.Close();
                break;
            case GesLayoutKind.Union:
                code.Blank();
                code.Line("/// The alternatives of the field, in declared order.");
                code.Open($"{access}enum {name}: {Underlying(value.Alternatives.Count)}, Sendable {{");
                foreach (var alternative in value.Alternatives)
                {
                    code.Line($"/// The alternative `:{alternative.Label}`.");
                    code.Line($"case {Member(alternative.Label)}");
                }

                code.Close();
                foreach (var alternative in value.Alternatives) Declarations(code, alternative, access);
                break;
        }
    }

    private static string Underlying(int count) => count <= 256 ? "UInt8" : count <= 65536 ? "UInt16" : "UInt32";

    private static ulong Full(int width) => width >= 64 ? ulong.MaxValue : (1UL << width) - 1;

    private static string Long(long value) => value == long.MinValue ? "Int64.min" : value.ToString(CultureInfo.InvariantCulture);

    private static string Unsigned(ulong value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Double(long value) => value.ToString(CultureInfo.InvariantCulture) + ".0";

    private static string Bits(double value) => $"Double(bitPattern: 0x{GesSchemaLayoutJson.Hex((ulong)BitConverter.DoubleToInt64Bits(value))})";

    private static string Quote(string text) => "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
