// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Globalization;
using GameEventScript.Api;

namespace GameEventScript.Generator;

// The fields of a packed schema as Swift properties over `rawValue`, as GesCSharpEmitterPacked does for C#: a setter checks
// the value as encoding does and traps with a message, `fits<Field>` tests a value without trapping, and a getter never fails.
internal static partial class GesSwiftEmitter
{
    internal static string PackedRaw(int width) => width switch { 8 => "UInt8", 16 => "UInt16", 32 => "UInt32", _ => "UInt64" };

    // The test `fits<Field>(_:)` of a value, or null when every value of the field's type fits.
    internal static string? PackedFits(GesCSharpEmitter.PackedField packed)
    {
        var encoding = packed.Field.Encoding;
        return encoding.Kind switch
        {
            GesLayoutKind.Integer when !encoding.Wrapping => $"value >= {Long(encoding.Lower)} && UInt64(bitPattern: Int64(value)) &- {Unsigned(unchecked((ulong)encoding.Lower))} <= {Unsigned(encoding.MaxIndex)}",
            GesLayoutKind.Quantized when encoding.Wrapping => "value.isFinite",
            GesLayoutKind.Quantized => $"value >= {Bits(encoding.LowerValue)} && value <= {Bits(encoding.UpperValue)}",
            GesLayoutKind.Flags when encoding.Width < 64 => $"value.rawValue <= {Unsigned(Full(encoding.Width))}",
            _ => null
        };
    }

    // The get and set accessors of one field's property; `tags` names the cases of a Tag in index order.
    internal static void PackedAccessors(GesSchemaPlanner.Code code, GesCSharpEmitter.PackedField packed, string raw, string member, List<string> tags)
    {
        var encoding = packed.Field.Encoding;
        var shift = packed.Shift.ToString(CultureInfo.InvariantCulture);
        var mask = Unsigned(Full(packed.Width));
        var index = $"(UInt64(rawValue) >> {shift}) & {mask}";
        code.Open("get {");
        switch (encoding.Kind)
        {
            case GesLayoutKind.Boolean:
                code.Line($"(UInt64(rawValue) >> {shift}) & 1 != 0");
                break;
            case GesLayoutKind.Integer:
                code.Line($"Int(Int64(bitPattern: {Unsigned(unchecked((ulong)encoding.Lower))} &+ ({index})))");
                break;
            case GesLayoutKind.Quantized:
                code.Line($"GesWireRules.quantizedValue({index}, lower: {Long(encoding.Lower)}, numerator: {Long(encoding.Numerator)}, tenPower: {Double(encoding.TenPower)})");
                break;
            case GesLayoutKind.Schema:
                code.Line($"{packed.Type}(uncheckedRawValue: {packed.NestedRaw}(truncatingIfNeeded: {index}))");
                break;
            case GesLayoutKind.Tag:
                code.Line($"switch {index} {{");
                for (var tag = 0; tag < tags.Count; tag++)
                    code.Line(tag == tags.Count - 1 ? $"default: .{tags[tag]}" : $"case {tag.ToString(CultureInfo.InvariantCulture)}: .{tags[tag]}");
                code.Line("}");
                break;
            default:
                code.Line($"{packed.Type}(rawValue: {index})");
                break;
        }

        code.Close();
        code.Open("set {");
        string wire;
        switch (encoding.Kind)
        {
            case GesLayoutKind.Boolean:
                wire = "(newValue ? 1 : 0)";
                break;
            case GesLayoutKind.Integer when encoding.Wrapping:
                wire = $"GesWireRules.wrappedIndex(Int64(newValue), lower: {Long(encoding.Lower)}, period: {Unsigned(encoding.MaxIndex + 1)})";
                break;
            case GesLayoutKind.Integer:
                wire = $"UInt64(bitPattern: Int64(newValue)) &- {Unsigned(unchecked((ulong)encoding.Lower))}";
                break;
            case GesLayoutKind.Quantized when encoding.Wrapping:
                wire = $"try! GesWireRules.wrappedQuantizedIndex(newValue, tenPower: {Double(encoding.TenPower)}, numerator: {Double(encoding.Numerator)}, lower: {Long(encoding.Lower)}, period: {Unsigned(encoding.MaxIndex + 1)}, path: \"\")";
                break;
            case GesLayoutKind.Quantized:
                wire = $"try! GesWireRules.quantizedIndex(newValue, lowerValue: {Bits(encoding.LowerValue)}, upperValue: {Bits(encoding.UpperValue)}, tenPower: {Double(encoding.TenPower)}, numerator: {Double(encoding.Numerator)}, lower: {Long(encoding.Lower)}, path: \"\")";
                break;
            case GesLayoutKind.Schema:
                wire = "UInt64(newValue.rawValue)";
                break;
            case GesLayoutKind.Tag:
                code.Line("let wire: UInt64");
                code.Line("switch newValue {");
                for (var tag = 0; tag < tags.Count; tag++) code.Line($"case .{tags[tag]}: wire = {tag.ToString(CultureInfo.InvariantCulture)}");
                code.Line("}");
                wire = "wire";
                break;
            default:
                wire = "newValue.rawValue";
                break;
        }

        if (PackedFits(packed) is not null) code.Line($"precondition(Self.fits{GesSchemaPlanner.Pascal(member)}(newValue), \"Outside the range of {packed.Field.Name}.\")");
        code.Line(packed.Width == 0
            ? "_ = newValue"
            : $"rawValue = {raw}(truncatingIfNeeded: (UInt64(rawValue) & ~({mask} << {shift})) | ({wire} << {shift}))");
        code.Close();
    }

    // Whether a word holds a valid index in every field whose bits allow more indices than it has.
    internal static List<string> PackedChecks(List<GesCSharpEmitter.PackedField> fields)
    {
        var checks = new List<string>();
        foreach (var packed in fields)
        {
            var shift = packed.Shift.ToString(CultureInfo.InvariantCulture);
            var index = $"((UInt64(rawValue) >> {shift}) & {Unsigned(Full(packed.Width))})";
            if (packed.Field.Encoding.Kind == GesLayoutKind.Schema) checks.Add($"{packed.Type}.isValid({packed.NestedRaw}(truncatingIfNeeded: {index}))");
            else if (GesCSharpEmitter.PackedLargest(packed.Field.Encoding) is { } largest) checks.Add($"{index} <= {Unsigned(largest)}");
        }

        return checks;
    }

    // Checks a word field by field and, when its type is wider than its bits, that no bit above them is set, because the
    // writer does not mask what it writes.
    private static void PackedChecks(GesSchemaPlanner.Code code, GesPackedWord word, string raw, string path)
    {
        if (word.Bits < word.Width) code.Line($"if ({raw} >> {N(word.Bits)}) != 0 {{ throw GesWireError(code: GesWireError.outOfRange, path: {path}) }}");
        foreach (var (node, shift, width, largest) in word.Leaves)
            if (largest is { } valid) code.Line($"if (({raw} >> {N(shift)}) & {Unsigned(Full(width))}) > {Unsigned(valid)} {{ throw GesWireError(code: GesWireError.outOfRange, path: paths[at + {N(node)}]) }}");
    }

    // Writes a packed record's word: its bits are the record's wire bits. A word taken unchecked is checked field by field
    // first, so that an error names the field as encoding a record does.
    private static void PackedWrite(GesSchemaPlanner.Code code, GesPlanValue value, string target)
    {
        var word = value.Packed!;
        PackedChecks(code, word, $"UInt64({target}.rawValue)", value.Node < 0 ? "\"\"" : Path(value));
        code.Line($"try writer.write(UInt64({target}.rawValue), width: {N(word.Bits)})");
    }

    // Reads a packed record's word field by field, so that an error names the field as decoding a record does.
    private static void PackedReadFields(GesSchemaPlanner.Code code, GesPlanValue value, string word)
    {
        code.Line($"var {word}: UInt64 = 0");
        foreach (var (node, shift, width, largest) in value.Packed!.Leaves)
        {
            if (width == 0) continue;
            var path = $"paths[at + {N(node)}]";
            if (largest is null)
            {
                code.Line($"{word} |= try reader.read({N(width)}, path: {path}) << {N(shift)}");
                continue;
            }

            code.Open("do {");
            code.Line($"let field = try reader.read({N(width)}, path: {path})");
            code.Line($"if field > {Unsigned(largest.Value)} {{ throw GesWireError(code: GesWireError.invalidIndex, path: {path}) }}");
            code.Line($"{word} |= field << {N(shift)}");
            code.Close();
        }
    }

    private static void PackedRead(GesSchemaPlanner.Code code, GesPlanValue value, string target)
    {
        var packed = value.Packed!;
        code.Open("do {");
        PackedReadFields(code, value, "word" + N(value.Node));
        code.Line($"{target} = {packed.Type}(uncheckedRawValue: {PackedRaw(packed.Width)}(truncatingIfNeeded: word{N(value.Node)}))");
        code.Close();
    }

    // The wire members of a packed record that is a wire root, as an extension of its type: it reads, writes and compares its
    // word, and its delta is a change bit and, when it differs, its word.
    private static void PackedRoot(GesSchemaPlanner.Code code, GesPlanType type, string access, string name, Context context, bool shared)
    {
        var word = GesPackedWord.Of(context.Layout, type.Name, -1);
        word.Type = name;
        var raw = PackedRaw(word.Width);
        var self = new GesPlanValue { Node = -1, Packed = word };
        code.Line($"/// The wire members of the packed record `:{type.Name}`, a wire root: its bytes are its word, little-endian.");
        code.Open($"extension {name} {{");
        code.Line("/// The hash over the schema hash and the parameter values this code was generated with.");
        code.Line($"{access}static let bindingHash: UInt64 = 0x{GesSchemaLayoutJson.Hex(type.Layout.BindingHash)}");
        code.Blank();
        code.Line("/// The size of the encoding in bytes, the same for every record.");
        code.Line($"{access}static let maxByteCount = {N((word.Bits + 7) / 8)}");
        code.Blank();
        code.Line($"static let __paths: [String] = [{string.Join(", ", type.Paths.ConvertAll(path => GesSchemaPlanner.Literal(path, true)))}]");
        code.Blank();
        code.Line("/// Creates the record of the word 0, to read into.");
        code.Line($"{access}init() {{ self.init(uncheckedRawValue: 0) }}");
        code.Blank();
        code.Line("/// Writes the record into a buffer and returns the number of bytes written; its bytes are its word, little-endian.");
        code.Line("/// - Throws: `GesWireError` when a word taken unchecked holds an invalid field or the buffer is too small.");
        code.Open($"{access}func write(into buffer: UnsafeMutableRawBufferPointer) throws(GesWireError) -> Int {{");
        code.Line("var writer = GesBitWriter(into: buffer)");
        GesCSharpEmitter.PackedPaths(code, word, "let paths = Self.__paths", "let at = 0");
        PackedWrite(code, self, "self");
        code.Line("return try writer.finish()");
        code.Close();
        code.Blank();
        code.Line("/// Counts the bytes that `write(into:)` writes, which are `maxByteCount`.");
        code.Line("/// - Throws: `GesWireError` when a word taken unchecked holds an invalid field.");
        code.Open($"{access}func encodedByteCount() throws(GesWireError) -> Int {{");
        GesCSharpEmitter.PackedPaths(code, word, "let paths = Self.__paths", "let at = 0");
        PackedChecks(code, word, "UInt64(rawValue)", "\"\"");
        code.Line("return Self.maxByteCount");
        code.Close();
        code.Blank();
        code.Line("/// Writes the record into a new array of its size.");
        code.Line("/// - Throws: `GesWireError` when a word taken unchecked holds an invalid field.");
        code.Open($"{access}func encoded() throws(GesWireError) -> [UInt8] {{");
        code.Line("let buffer = UnsafeMutableRawBufferPointer.allocate(byteCount: Self.maxByteCount, alignment: 8)");
        code.Line("defer { buffer.deallocate() }");
        code.Line("let count = try write(into: buffer)");
        code.Line("return Array(buffer[0..<count])");
        code.Close();
        code.Blank();
        code.Line("/// Reads a record from untrusted data into this instance and returns the number of bytes it occupies.");
        code.Line("/// - Throws: `GesWireError` when the data is no valid record.");
        code.Line("@discardableResult");
        code.Open($"{access}mutating func read(from data: UnsafeRawBufferPointer, byteLimit: Int = .max) throws(GesWireError) -> Int {{");
        code.Line("var reader = GesBitReader(data: data, byteLimit: byteLimit)");
        code.Line("let paths = Self.__paths");
        code.Line("let at = 0");
        PackedReadFields(code, self, "word");
        code.Line($"self = Self(uncheckedRawValue: {raw}(truncatingIfNeeded: word))");
        code.Line("return try reader.finish()");
        code.Close();
        code.Blank();
        code.Line("/// Reads a record from bytes into this instance and returns the number of bytes it occupies.");
        code.Line("/// - Throws: `GesWireError` when the bytes are no valid record.");
        code.Line("@discardableResult");
        code.Open($"{access}mutating func read(from bytes: [UInt8], byteLimit: Int = .max) throws(GesWireError) -> Int {{");
        Copied(code, "read(from: UnsafeRawBufferPointer(rebasing: buffer[0..<bytes.count]), byteLimit: byteLimit)");
        code.Close();
        code.Blank();
        code.Line("/// Whether another record writes the same bytes: whether the words are equal.");
        code.Line($"{access}func wireEquals(_ other: Self) -> Bool {{ rawValue == other.rawValue }}");
        code.Blank();
        code.Line("/// Copies another record's word.");
        code.Line($"{access}mutating func copy(from other: Self) {{ self = other }}");
        code.Blank();
        code.Line("/// Does nothing: a packed record holds only values its fields encode.");
        code.Line($"{access}func quantize() {{}}");
        code.Blank();
        code.Line("/// Writes the delta against a baseline: a change bit and, when the words differ, this word.");
        code.Line("/// - Throws: `GesWireError` when a word taken unchecked holds an invalid field or the buffer is too small.");
        code.Open($"{access}func writeDelta(against baseline: Self, into buffer: UnsafeMutableRawBufferPointer) throws(GesWireError) -> Int {{");
        code.Line("var writer = GesBitWriter(into: buffer)");
        GesCSharpEmitter.PackedPaths(code, word, "let paths = Self.__paths", "let at = 0");
        code.Open("if rawValue == baseline.rawValue {");
        code.Line("try writer.write(0, width: 1)");
        code.Close("} else {");
        code.Indent();
        code.Line("try writer.write(1, width: 1)");
        PackedWrite(code, self, "self");
        code.Close();
        code.Line("return try writer.finish()");
        code.Close();
        code.Blank();
        code.Line("/// Counts the bytes of the delta against a baseline.");
        code.Line($"{access}func deltaEncodedByteCount(against baseline: Self) -> Int {{ rawValue == baseline.rawValue ? 1 : ({N(word.Bits)} + 8) / 8 }}");
        code.Blank();
        code.Line("/// Writes the delta against a baseline into a new array of its size.");
        code.Line("/// - Throws: `GesWireError` when a word taken unchecked holds an invalid field.");
        code.Open($"{access}func deltaEncoded(against baseline: Self) throws(GesWireError) -> [UInt8] {{");
        code.Line("let buffer = UnsafeMutableRawBufferPointer.allocate(byteCount: deltaEncodedByteCount(against: baseline), alignment: 8)");
        code.Line("defer { buffer.deallocate() }");
        code.Line("let count = try writeDelta(against: baseline, into: buffer)");
        code.Line("return Array(buffer[0..<count])");
        code.Close();
        code.Blank();
        code.Line("/// Reads a delta against a baseline into this instance and returns the number of bytes it occupies.");
        code.Line("/// - Throws: `GesWireError` when the data is no valid delta, such as a word equal to the baseline's.");
        code.Line("@discardableResult");
        code.Open($"{access}mutating func readDelta(from data: UnsafeRawBufferPointer, against baseline: Self, byteLimit: Int = .max) throws(GesWireError) -> Int {{");
        code.Line("var reader = GesBitReader(data: data, byteLimit: byteLimit)");
        code.Line("let paths = Self.__paths");
        code.Line("let at = 0");
        code.Open("if try reader.read(1, path: \"\") == 0 {");
        code.Line("self = baseline");
        code.Close("} else {");
        code.Indent();
        PackedReadFields(code, self, "word");
        code.Line($"if {raw}(truncatingIfNeeded: word) == baseline.rawValue {{ throw GesWireError(code: GesWireError.invalidDelta, path: \"\") }}");
        code.Line($"self = Self(uncheckedRawValue: {raw}(truncatingIfNeeded: word))");
        code.Close();
        code.Line("return try reader.finish()");
        code.Close();
        code.Blank();
        code.Line("/// Reads a delta from bytes against a baseline into this instance and returns the number of bytes it occupies.");
        code.Line("/// - Throws: `GesWireError` when the bytes are no valid delta.");
        code.Line("@discardableResult");
        code.Open($"{access}mutating func readDelta(from bytes: [UInt8], against baseline: Self, byteLimit: Int = .max) throws(GesWireError) -> Int {{");
        Copied(code, "readDelta(from: UnsafeRawBufferPointer(rebasing: buffer[0..<bytes.count]), against: baseline, byteLimit: byteLimit)");
        code.Close();
        // A root shared with models converts to and from values through the model's own conversion.
        if (context.Values && !shared) ValueMembers(code, type, access, context.Holder);
        code.Close();
    }

    // Copies bytes into an aligned buffer and calls a read on it, as the other records' byte overloads do.
    private static void Copied(GesSchemaPlanner.Code code, string call)
    {
        code.Line("let buffer = UnsafeMutableRawBufferPointer.allocate(byteCount: Swift.max(bytes.count, 1), alignment: 8)");
        code.Line("defer { buffer.deallocate() }");
        code.Line("buffer.copyBytes(from: bytes)");
        code.Line("return try " + call);
    }

    // Reads a List of packed records a word at a time when the data holds all of them; see GesCSharpEmitter.PackedList.
    private static void PackedList(GesSchemaPlanner.Code code, GesPlanValue value, string target, string count)
    {
        var element = value.Element!;
        var packed = element.Packed!;
        var raw = PackedRaw(packed.Width);
        var bits = N(packed.Bits);
        code.Line($"let whole = reader.remainingBits >= {count} * {bits}");
        code.Open("if whole {");
        code.Line($"{target}.reserveCapacity({count})");
        code.Open($"for _ in 0..<{count} {{");
        if (packed.Full) code.Line($"{target}.append({packed.Type}(uncheckedRawValue: {raw}(truncatingIfNeeded: try reader.read({bits}, path: \"\"))))");
        else
        {
            code.Line("let start = reader.bitPosition");
            code.Line($"let word = {raw}(truncatingIfNeeded: try reader.read({bits}, path: \"\"))");
            code.Open($"if !{packed.Type}.isValid(word) {{");
            code.Line("reader.seek(to: start)");
            code.Open("do {");
            PackedReadFields(code, element, "slow");
            code.Line("_ = slow");
            code.Close();
            code.Close();
            code.Line($"{target}.append({packed.Type}(uncheckedRawValue: word))");
        }

        code.Close();
        code.Close();
        code.Open("if !whole {");
        code.Open($"for _ in 0..<{count} {{");
        PackedReadFields(code, element, "slow");
        code.Line($"{target}.append({packed.Type}(uncheckedRawValue: {raw}(truncatingIfNeeded: slow)))");
        code.Close();
        code.Close();
    }
}
