// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Globalization;
using GameEventScript.Api;

namespace GameEventScript.Generator;

// The fields of a packed schema as properties over one word: each field's wire index sits at its bit offset, in declared
// order from the least significant bit, as the record's wire bytes hold it. A setter checks the value as encoding does and
// throws ArgumentOutOfRangeException; a getter never fails, since only valid words are stored, unless one was taken unchecked.
internal static partial class GesCSharpEmitter
{
    // The unsigned C# type of a word of 8, 16, 32 or 64 bits.
    internal static string PackedRaw(int width) => width switch { 8 => "byte", 16 => "ushort", 32 => "uint", _ => "ulong" };

    // A field of a packed word: its encoding, its first bit, and the C# type and member that hold it.
    internal sealed class PackedField(GesLayoutField field, int shift, int width, string member, string type, string? nestedRaw)
    {
        internal GesLayoutField Field { get; } = field;

        internal int Shift { get; } = shift;

        internal int Width { get; } = width;

        internal string Member { get; } = member;

        internal string Type { get; } = type;

        // The word type of a nested packed schema, null for other fields.
        internal string? NestedRaw { get; } = nestedRaw;
    }

    // The bits of each field in declared order; a nested packed schema takes the bits of its fields.
    internal static List<(GesLayoutField Field, int Shift, int Width)> PackedLayout(GesSchemaLayout layout, GesLayoutType type)
    {
        var result = new List<(GesLayoutField, int, int)>();
        var shift = 0;
        foreach (var field in type.Fields)
        {
            var width = field.Encoding.Kind == GesLayoutKind.Schema ? layout.Type(field.Encoding.Schema).PackedBits : field.Encoding.Bits;
            result.Add((field, shift, width));
            shift += width;
        }

        return result;
    }

    // The largest valid index of a field, below which a word of its bits is valid; null when every index of its bits is.
    internal static ulong? PackedLargest(GesLayoutEncoding encoding)
    {
        var largest = encoding.Kind switch
        {
            GesLayoutKind.Tag => (ulong)encoding.Tags.Count - 1,
            GesLayoutKind.Integer or GesLayoutKind.Quantized => encoding.MaxIndex,
            _ => Full(encoding.Width)
        };
        return largest < Full(encoding.Width) ? largest : null;
    }

    // The get and set accessors of one field's property.
    internal static void PackedAccessors(GesSchemaPlanner.Code code, PackedField packed, string raw)
    {
        var encoding = packed.Field.Encoding;
        var shift = packed.Shift.ToString(CultureInfo.InvariantCulture);
        var mask = Unsigned(Full(packed.Width));
        var index = $"((ulong)_raw >> {shift}) & {mask}";
        var getter = encoding.Kind switch
        {
            GesLayoutKind.Boolean => $"(((ulong)_raw >> {shift}) & 1UL) != 0",
            GesLayoutKind.Integer => $"unchecked((long)({Unsigned(unchecked((ulong)encoding.Lower))} + ({index})))",
            GesLayoutKind.Quantized => $"GesWireRules.QuantizedValue({index}, {Long(encoding.Lower)}, {Long(encoding.Numerator)}, {Double(encoding.TenPower)})",
            GesLayoutKind.Schema => $"{packed.Type}.FromRawUnchecked(({packed.NestedRaw})({index}))",
            GesLayoutKind.Tag => $"({packed.Type})(int)({index})",
            _ => $"({packed.Type})({index})"
        };
        code.Line($"readonly get => {getter};");
        code.Line("set");
        code.Open("{");
        string wire;
        switch (encoding.Kind)
        {
            case GesLayoutKind.Boolean:
                wire = "(value ? 1UL : 0UL)";
                break;
            case GesLayoutKind.Integer when encoding.Wrapping:
                wire = $"GesWireRules.WrappedIndex(value, {Long(encoding.Lower)}, {Unsigned(encoding.MaxIndex + 1)})";
                break;
            case GesLayoutKind.Integer:
                code.Line($"if (value < {Long(encoding.Lower)} || unchecked((ulong)value - {Unsigned(unchecked((ulong)encoding.Lower))}) > {Unsigned(encoding.MaxIndex)}) throw new ArgumentOutOfRangeException(nameof(value), value, {Range(packed.Field)});");
                wire = $"unchecked((ulong)value - {Unsigned(unchecked((ulong)encoding.Lower))})";
                break;
            case GesLayoutKind.Quantized when encoding.Wrapping:
                code.Line($"if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value), value, {Range(packed.Field)});");
                wire = $"GesWireRules.WrappedQuantizedIndex(value, {Double(encoding.TenPower)}, {Double(encoding.Numerator)}, {Long(encoding.Lower)}, {Unsigned(encoding.MaxIndex + 1)}, \"\")";
                break;
            case GesLayoutKind.Quantized:
                code.Line($"if (!(value >= {Bits(encoding.LowerValue)} && value <= {Bits(encoding.UpperValue)})) throw new ArgumentOutOfRangeException(nameof(value), value, {Range(packed.Field)});");
                wire = $"GesWireRules.QuantizedIndex(value, {Bits(encoding.LowerValue)}, {Bits(encoding.UpperValue)}, {Double(encoding.TenPower)}, {Double(encoding.Numerator)}, {Long(encoding.Lower)}, \"\")";
                break;
            case GesLayoutKind.Schema:
                wire = "(ulong)value.RawValue";
                break;
            case GesLayoutKind.Tag:
                code.Line($"if ((ulong)value > {Unsigned((ulong)encoding.Tags.Count - 1)}) throw new ArgumentOutOfRangeException(nameof(value), value, \"No tag of the field.\");");
                wire = "(ulong)value";
                break;
            default:
                if (encoding.Width < 64) code.Line($"if ((ulong)value > {mask}) throw new ArgumentOutOfRangeException(nameof(value), value, \"No member of the field.\");");
                wire = "(ulong)value";
                break;
        }

        code.Line(packed.Width == 0
            ? "_ = value;"
            : $"_raw = unchecked(({raw})(((ulong)_raw & ~({mask} << {shift})) | ({wire} << {shift})));");
        code.Close();
    }

    // Whether a word holds a valid index in every field whose bits allow more indices than it has.
    internal static List<string> PackedChecks(List<PackedField> fields)
    {
        var checks = new List<string>();
        foreach (var packed in fields)
        {
            var shift = packed.Shift.ToString(CultureInfo.InvariantCulture);
            var index = $"(((ulong)raw >> {shift}) & {Unsigned(Full(packed.Width))})";
            if (packed.Field.Encoding.Kind == GesLayoutKind.Schema) checks.Add($"{packed.Type}.IsValid(({packed.NestedRaw}){index})");
            else if (PackedLargest(packed.Field.Encoding) is { } largest) checks.Add($"{index} <= {Unsigned(largest)}");
        }

        return checks;
    }

    // Writes a packed record's word: its bits are the record's wire bits. A word taken unchecked is checked field by field
    // first, so that an error names the field as encoding a record does.
    private static void PackedWrite(GesSchemaPlanner.Code code, GesPlanValue value, string target)
    {
        var word = value.Packed!;
        PackedChecks(code, word, $"(ulong){target}.RawValue", value.Node < 0 ? "\"\"" : Path(value));
        code.Line($"writer.Write({target}.RawValue, {N(word.Bits)});");
    }

    // Checks a word field by field and, when its type is wider than its bits, that no bit above them is set, because the
    // writer does not mask what it writes.
    // Declares the paths and the offset that PackedChecks names a field with, only when a field has values to check.
    internal static void PackedPaths(GesSchemaPlanner.Code code, GesPackedWord word, string paths, string at)
    {
        if (!word.Leaves.Exists(leaf => leaf.Largest is not null)) return;
        code.Line(paths);
        code.Line(at);
    }

    private static void PackedChecks(GesSchemaPlanner.Code code, GesPackedWord word, string raw, string path)
    {
        if (word.Bits < word.Width) code.Line($"if (({raw} >> {N(word.Bits)}) != 0) throw new GesWireException(GesWireException.OutOfRange, {path});");
        foreach (var (node, shift, width, largest) in word.Leaves)
            if (largest is { } valid) code.Line($"if ((({raw} >> {N(shift)}) & {Unsigned(Full(width))}) > {Unsigned(valid)}) throw new GesWireException(GesWireException.OutOfRange, paths[at + {N(node)}]);");
    }

    // The wire members of a packed record that is a wire root, inside its struct: it reads, writes and compares its word, and
    // its delta is a change bit and, when it differs, its word.
    private static void PackedRoot(GesSchemaPlanner.Code code, GesPlanType type, Context context, string? shared = null)
    {
        // Shared with models, the type is the model's struct, which the wire code extends with static methods instead.
        var name = shared ?? type.Name;
        string Sig(bool mutates, string result, string member, string parameters)
            => shared is null
                ? $"public {(mutates ? "" : "readonly ")}{result} {member}({parameters})"
                : $"public static {result} {member}(this {(mutates ? "ref " : "")}{name} record{(parameters.Length > 0 ? ", " + parameters : "")})";
        var me = shared is null ? "" : "record.";
        var rawOf = shared is null ? "_raw" : "record.RawValue";
        string Set(string value) => shared is null ? $"_raw = {value};" : $"record = {name}.FromRawUnchecked({value});";
        var word = GesPackedWord.Of(context.Layout, type.Name, -1);
        var raw = PackedRaw(word.Width);
        // The record is its word; its delta is a change bit and the word, so the largest encoding has one bit more.
        var bytes = N((word.Bits + 7) / 8);
        var maxBytes = N((word.Bits + 8) / 8);
        var self = new GesPlanValue { Node = -1, Packed = word };
        if (shared is null) code.Blank();
        code.Line("/// <summary>The hash over the schema hash and the parameter values this code was generated with.</summary>");
        code.Line($"public const ulong BindingHash = 0x{GesSchemaLayoutJson.Hex(type.Layout.BindingHash)}UL;");
        code.Blank();
        code.Line("/// <summary>The size in bytes of the largest encoding, a delta of a change bit and the word, which every buffer for writing needs.</summary>");
        code.Line($"public const int MaxByteCount = {maxBytes};");
        code.Blank();
        code.Line($"internal static readonly string[] __paths = {{ {string.Join(", ", type.Paths.ConvertAll(path => GesSchemaPlanner.Literal(path, false)))} }};");
        code.Blank();
        code.Line("/// <summary>Writes the record into a buffer; its bytes are its word, little-endian.</summary>");
        code.Line("/// <param name=\"buffer\">The buffer.</param>");
        code.Line("/// <returns>The number of bytes written.</returns>");
        code.Line("/// <exception cref=\"GesWireException\">A word taken unchecked holds an invalid field, or the buffer is too small.</exception>");
        code.Line(Sig(false, "int", "Write", "Span<byte> buffer"));
        code.Open("{");
        code.Line("var writer = new GesBitWriter(buffer);");
        PackedPaths(code, word, "var paths = __paths;", "const int at = 0;");
        PackedWrite(code, self, shared is null ? "this" : "record");
        code.Line("return writer.Finish();");
        code.Close();
        code.Blank();
        code.Line("/// <summary>Counts the bytes that <see cref=\"Write\"/> writes, the same for every record.</summary>");
        code.Line("/// <returns>The byte count.</returns>");
        code.Line("/// <exception cref=\"GesWireException\">A word taken unchecked holds an invalid field.</exception>");
        code.Line(Sig(false, "int", "ByteCount", ""));
        code.Open("{");
        PackedPaths(code, word, "var paths = __paths;", "const int at = 0;");
        PackedChecks(code, word, $"(ulong){rawOf}", "\"\"");
        code.Line($"return {bytes};");
        code.Close();
        code.Blank();
        code.Line("/// <summary>Writes the record into a new array of its size.</summary>");
        code.Line("/// <returns>The bytes.</returns>");
        code.Line("/// <exception cref=\"GesWireException\">A word taken unchecked holds an invalid field.</exception>");
        code.Line(Sig(false, "byte[]", "ToBytes", ""));
        code.Open("{");
        code.Line($"var buffer = new byte[{bytes}];");
        code.Line($"{me}Write(buffer);");
        code.Line("return buffer;");
        code.Close();
        code.Blank();
        code.Line("/// <summary>Reads a record from untrusted data into this instance.</summary>");
        code.Line("/// <param name=\"data\">The data; it may continue after the record.</param>");
        code.Line("/// <param name=\"byteLimit\">An optional limit on the bytes of this record.</param>");
        code.Line("/// <returns>The number of bytes the record occupies.</returns>");
        code.Line("/// <exception cref=\"GesWireException\">The data is no valid record.</exception>");
        code.Line(Sig(true, "int", "Read", "ReadOnlySpan<byte> data, long byteLimit = long.MaxValue"));
        code.Open("{");
        code.Line("var reader = new GesBitReader(data, byteLimit);");
        code.Line("var paths = __paths;");
        code.Line("const int at = 0;");
        PackedReadFields(code, self, "word");
        code.Line(Set($"({raw})word"));
        code.Line("return reader.Finish();");
        code.Close();
        code.Blank();
        code.Line("/// <summary>Compares the words, which are what the wire carries.</summary>");
        code.Line("/// <param name=\"other\">The other record.</param>");
        code.Line("/// <returns>Whether both encode alike.</returns>");
        code.Line($"{Sig(false, "bool", "WireEquals", name + " other")} => {rawOf} == other.RawValue;");
        code.Blank();
        code.Line("/// <summary>Copies another record's word.</summary>");
        code.Line("/// <param name=\"other\">The other record.</param>");
        code.Line($"{Sig(true, "void", "CopyFrom", name + " other")} => {Set("other.RawValue").TrimEnd(';')};");
        code.Blank();
        code.Line("/// <summary>Does nothing: a packed record holds only values its fields encode.</summary>");
        code.Line(Sig(false, "void", "Quantize", ""));
        code.Open("{");
        code.Close();
        code.Blank();
        code.Line("/// <summary>Writes the delta against a baseline: a change bit and, when the words differ, this word.</summary>");
        code.Line("/// <param name=\"baseline\">The baseline.</param>");
        code.Line("/// <param name=\"buffer\">The buffer.</param>");
        code.Line("/// <returns>The number of bytes written.</returns>");
        code.Line("/// <exception cref=\"GesWireException\">A word taken unchecked holds an invalid field, or the buffer is too small.</exception>");
        code.Line(Sig(false, "int", "WriteDelta", name + " baseline, Span<byte> buffer"));
        code.Open("{");
        code.Line("var writer = new GesBitWriter(buffer);");
        PackedPaths(code, word, "var paths = __paths;", "const int at = 0;");
        code.Line($"if ({rawOf} == baseline.RawValue) writer.Write(0, 1);");
        code.Line("else");
        code.Open("{");
        code.Line("writer.Write(1, 1);");
        PackedWrite(code, self, shared is null ? "this" : "record");
        code.Close();
        code.Line("return writer.Finish();");
        code.Close();
        code.Blank();
        code.Line("/// <summary>Counts the bytes of the delta against a baseline.</summary>");
        code.Line("/// <param name=\"baseline\">The baseline.</param>");
        code.Line("/// <returns>The byte count.</returns>");
        code.Line($"{Sig(false, "int", "DeltaByteCount", name + " baseline")} => {rawOf} == baseline.RawValue ? 1 : ({N(word.Bits)} + 8) / 8;");
        code.Blank();
        code.Line("/// <summary>Writes the delta against a baseline into a new array of its size.</summary>");
        code.Line("/// <param name=\"baseline\">The baseline.</param>");
        code.Line("/// <returns>The bytes.</returns>");
        code.Line(Sig(false, "byte[]", "ToDeltaBytes", name + " baseline"));
        code.Open("{");
        code.Line($"var buffer = new byte[{me}DeltaByteCount(baseline)];");
        code.Line($"{me}WriteDelta(baseline, buffer);");
        code.Line("return buffer;");
        code.Close();
        code.Blank();
        code.Line("/// <summary>Reads a delta against a baseline into this instance.</summary>");
        code.Line("/// <param name=\"data\">The delta.</param>");
        code.Line("/// <param name=\"baseline\">The baseline.</param>");
        code.Line("/// <param name=\"byteLimit\">An optional limit on the bytes of this delta.</param>");
        code.Line("/// <returns>The number of bytes the delta occupies.</returns>");
        code.Line("/// <exception cref=\"GesWireException\">The data is no valid delta, such as a word equal to the baseline's.</exception>");
        code.Line(Sig(true, "int", "ReadDelta", $"ReadOnlySpan<byte> data, {name} baseline, long byteLimit = long.MaxValue"));
        code.Open("{");
        code.Line("var reader = new GesBitReader(data, byteLimit);");
        code.Line("var paths = __paths;");
        code.Line("const int at = 0;");
        code.Line($"if (reader.Read(1, \"\") == 0) {Set("baseline.RawValue")}");
        code.Line("else");
        code.Open("{");
        PackedReadFields(code, self, "word");
        code.Line($"if (({raw})word == baseline.RawValue) throw new GesWireException(GesWireException.InvalidDelta, \"\");");
        code.Line(Set($"({raw})word"));
        code.Close();
        code.Line("return reader.Finish();");
        code.Close();
        // A root shared with models converts to and from values through the model's own conversion.
        if (context.Values && shared is null) ValueMembers(code, type, context.Holder);
    }

    // Reads a packed record's word field by field, so that an error names the field as decoding a record does.
    private static void PackedReadFields(GesSchemaPlanner.Code code, GesPlanValue value, string word)
    {
        code.Line($"var {word} = 0UL;");
        foreach (var (node, shift, width, largest) in value.Packed!.Leaves)
        {
            if (width == 0) continue;
            var path = $"paths[at + {N(node)}]";
            if (largest is null)
            {
                code.Line($"{word} |= reader.Read({N(width)}, {path}) << {N(shift)};");
                continue;
            }

            code.Open("{");
            code.Line($"var field = reader.Read({N(width)}, {path});");
            code.Line($"if (field > {Unsigned(largest.Value)}) throw new GesWireException(GesWireException.InvalidIndex, {path});");
            code.Line($"{word} |= field << {N(shift)};");
            code.Close();
        }
    }

    private static void PackedRead(GesSchemaPlanner.Code code, GesPlanValue value, string target)
    {
        var packed = value.Packed!;
        code.Open("{");
        PackedReadFields(code, value, "word" + N(value.Node));
        code.Line($"{target} = {packed.Type}.FromRawUnchecked(({PackedRaw(packed.Width)})word{N(value.Node)});");
        code.Close();
    }

    // Reads a List of packed records a word at a time when the data holds all of them: one read per element, checked only
    // when its bits allow invalid words, and then a word that is not valid is read again field by field for its error. Data
    // that ends inside the list is read field by field, so that the error names the field as decoding a record does.
    private static void PackedList(GesSchemaPlanner.Code code, GesPlanValue value, string target, string count)
    {
        var element = value.Element!;
        var packed = element.Packed!;
        var raw = PackedRaw(packed.Width);
        var bits = N(packed.Bits);
        code.Line($"if (reader.RemainingBits >= (long){count} * {bits})");
        code.Open("{");
        code.Line($"if ({target}.Capacity < {count}) {target}.Capacity = {count};");
        code.Line($"for (var position = 0; position < {count}; position++)");
        code.Open("{");
        if (packed.Full) code.Line($"{target}.Add({packed.Type}.FromRawUnchecked(({raw})reader.Read({bits}, \"\")));");
        else
        {
            code.Line("var start = reader.BitPosition;");
            code.Line($"var word = ({raw})reader.Read({bits}, \"\");");
            code.Line($"if (!{packed.Type}.IsValid(word))");
            code.Open("{");
            code.Line("reader.Seek(start);");
            code.Open("{");
            PackedReadFields(code, element, "slow");
            code.Close();
            code.Close();
            code.Line($"{target}.Add({packed.Type}.FromRawUnchecked(word));");
        }

        code.Close();
        code.Close();
        code.Line("else");
        code.Open("{");
        code.Line($"for (var position = 0; position < {count}; position++)");
        code.Open("{");
        PackedReadFields(code, element, "slow");
        code.Line($"{target}.Add({packed.Type}.FromRawUnchecked(({raw})slow));");
        code.Close();
        code.Close();
    }

    private static string Range(GesLayoutField field) => GesSchemaPlanner.Literal($"Outside the range of {field.Name}.", false);
}
