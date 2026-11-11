// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Globalization;
using GameEventScript.Api;

namespace GameEventScript.Generator;

// Wire values, comparison, copying, quantization and deltas of generated C# types. Comparison and deltas use wire values:
// indices and stored bits, never raw doubles. Local names carry the node of their value, since C# forbids shadowing.
internal static partial class GesCSharpEmitter
{
    private static string N(int node) => node.ToString(CultureInfo.InvariantCulture);

    private static bool Scalar(GesPlanValue value) => value.Encoding.Kind is GesLayoutKind.Boolean or GesLayoutKind.Integer or GesLayoutKind.Quantized or GesLayoutKind.Float or GesLayoutKind.Tag;

    // Every value of a type with a wire value of its own, for the helpers that convert it.
    private static IEnumerable<GesPlanValue> Scalars(GesPlanValue value)
    {
        if (Scalar(value)) yield return value;
        foreach (var axis in value.Axes) yield return axis;
        foreach (var alternative in value.Alternatives)
            foreach (var nested in Scalars(alternative)) yield return nested;
        if (value.Item is null && value.Element is not null)
            foreach (var nested in Scalars(value.Element)) yield return nested;
    }

    // The helpers, comparison, copy, quantization and deltas of a record type.
    private static void Extras(GesSchemaPlanner.Code code, GesPlanType type, bool deltas, bool element)
    {
        var self = type.IsClass ? "" : "readonly ";
        var parameter = type.IsClass ? type.Name : "in " + type.Name;
        foreach (var field in type.Fields)
            foreach (var value in Scalars(field.Value)) Helpers(code, value);

        code.Blank();
        code.Line($"internal {self}bool __Same({parameter} other, string[] paths, int at)");
        code.Open("{");
        foreach (var field in type.Fields)
        {
            if (field.Optional)
            {
                code.Line($"if (Has{field.Base} != other.Has{field.Base}) return false;");
                code.Line($"if (Has{field.Base})");
                code.Open("{");
                Same(code, field.Value, field.Base, "other." + field.Base);
                code.Close();
            }
            else Same(code, field.Value, field.Base, "other." + field.Base);
        }

        foreach (var (_, name, _) in type.Tags) code.Line($"if ({name} != other.{name}) return false;");
        code.Line("return true;");
        code.Close();

        if (type.IsClass)
        {
            code.Blank();
            code.Line("/// <summary>Copies the values of another record into the storage of this one, without allocating once it has grown.</summary>");
            code.Line("/// <param name=\"other\">The record to copy.</param>");
            code.Line($"public void CopyFrom({type.Name} other)");
            code.Open("{");
            code.Line("if (ReferenceEquals(this, other)) return;");
            foreach (var field in type.Fields)
            {
                if (field.Optional) code.Line($"Has{field.Base} = other.Has{field.Base};");
                Copy(code, field.Value, field.Base, "other." + field.Base);
            }

            foreach (var (_, name, _) in type.Tags) code.Line($"{name} = other.{name};");
            code.Close();
        }

        code.Blank();
        code.Line("internal void __Quantize(string[] paths, int at)");
        code.Open("{");
        foreach (var field in type.Fields)
        {
            if (field.Optional)
            {
                code.Line($"if (Has{field.Base})");
                code.Open("{");
                Quantize(code, field.Value, field.Base);
                code.Close();
            }
            else Quantize(code, field.Value, field.Base);
        }

        code.Close();
        if (type.IsItem) return;

        code.Blank();
        code.Line($"/// <summary>Gets whether another {Kind(type)} has the same wire values, so that it writes the same bytes.</summary>");
        code.Line("/// <param name=\"other\">The other record.</param>");
        code.Line("/// <returns>Whether the wire values are equal.</returns>");
        code.Line("/// <exception cref=\"GesWireException\">A compared value has no wire value, such as a number outside its range.</exception>");
        code.Line($"public {self}bool WireEquals({parameter} other) => __Same(other, __paths, 0);");
        code.Blank();
        code.Line("/// <summary>Replaces every quantized value by the value its index decodes to, which a receiver reads.</summary>");
        code.Line("/// <exception cref=\"GesWireException\">A quantized value lies outside its range.</exception>");
        code.Line("public void Quantize() => __Quantize(__paths, 0);");
        if (element) KeyOrder(code, type, self);
        if (deltas) Deltas(code, type, self, parameter);
    }

    private static void Helpers(GesSchemaPlanner.Code code, GesPlanValue value)
    {
        var encoding = value.Encoding;
        var node = N(value.Node);
        var type = TypeName(value);
        var width = encoding.Width.ToString(CultureInfo.InvariantCulture);
        code.Blank();
        code.Line($"private static ulong __Wire{node}({type} value, string[] paths, int at)");
        code.Open("{");
        switch (encoding.Kind)
        {
            case GesLayoutKind.Boolean:
                code.Line("return value ? 1UL : 0UL;");
                break;
            case GesLayoutKind.Integer when !encoding.Range:
                code.Line("return unchecked((ulong)value);");
                break;
            case GesLayoutKind.Integer when encoding.Wrapping:
                code.Line($"return GesWireRules.WrappedIndex(value, {Long(encoding.Lower)}, {Unsigned(encoding.MaxIndex + 1)});");
                break;
            case GesLayoutKind.Integer:
                code.Line($"if (value < {Long(encoding.Lower)} || unchecked((ulong)value - {Unsigned(unchecked((ulong)encoding.Lower))}) > {Unsigned(encoding.MaxIndex)}) {Throw("OutOfRange", value)}");
                code.Line($"return unchecked((ulong)value - {Unsigned(unchecked((ulong)encoding.Lower))});");
                break;
            case GesLayoutKind.Quantized when encoding.Wrapping:
                code.Line($"return GesWireRules.WrappedQuantizedIndex(value, {Double(encoding.TenPower)}, {Double(encoding.Numerator)}, {Long(encoding.Lower)}, {Unsigned(encoding.MaxIndex + 1)}, {Path(value)});");
                break;
            case GesLayoutKind.Quantized:
                code.Line($"return GesWireRules.QuantizedIndex(value, {Bits(encoding.LowerValue)}, {Bits(encoding.UpperValue)}, {Double(encoding.TenPower)}, {Double(encoding.Numerator)}, {Long(encoding.Lower)}, {Path(value)});");
                break;
            case GesLayoutKind.Float:
                code.Line($"if (double.IsNaN(value)) {Throw("NotRepresentable", value)}");
                if (encoding.Range) code.Line($"if (value < {Bits(encoding.LowerValue)} || value > {Bits(encoding.UpperValue)}) {Throw("OutOfRange", value)}");
                code.Line("return unchecked((ulong)BitConverter.DoubleToInt64Bits(value == 0 ? 0d : value));");
                break;
            default:
                code.Line($"if ((ulong)value > {Unsigned((ulong)encoding.Tags.Count - 1)}) {Throw("UnknownTag", value)}");
                code.Line("return (ulong)value;");
                break;
        }

        code.Close();
        code.Blank();
        code.Line($"private static ulong __Read{node}(ref GesBitReader reader, string[] paths, int at)");
        code.Open("{");
        code.Line($"var wire = reader.Read({width}, {Path(value)});");
        if (encoding.Kind == GesLayoutKind.Float)
        {
            code.Line("var value = BitConverter.Int64BitsToDouble(unchecked((long)wire));");
            code.Line($"if (double.IsNaN(value) || wire == 0x8000000000000000UL{(encoding.Range ? " || double.IsInfinity(value)" : "")}) {Throw("InvalidNumber", value)}");
            if (encoding.Range) code.Line($"if (value < {Bits(encoding.LowerValue)} || value > {Bits(encoding.UpperValue)}) {Throw("OutOfRange", value)}");
        }
        else if (encoding.Kind != GesLayoutKind.Boolean)
        {
            var largest = encoding.Kind == GesLayoutKind.Tag ? (ulong)encoding.Tags.Count - 1 : encoding.MaxIndex;
            if (largest < Full(encoding.Width)) code.Line($"if (wire > {Unsigned(largest)}) {Throw("InvalidIndex", value)}");
        }

        code.Line("return wire;");
        code.Close();
        code.Blank();
        code.Line(encoding.Kind switch
        {
            GesLayoutKind.Boolean => $"private static bool __Value{node}(ulong wire) => wire != 0;",
            GesLayoutKind.Integer when !encoding.Range => $"private static long __Value{node}(ulong wire) => unchecked((long)wire);",
            GesLayoutKind.Integer => $"private static long __Value{node}(ulong wire) => unchecked((long)({Unsigned(unchecked((ulong)encoding.Lower))} + wire));",
            GesLayoutKind.Quantized => $"private static double __Value{node}(ulong wire) => GesWireRules.QuantizedValue(wire, {Long(encoding.Lower)}, {Long(encoding.Numerator)}, {Double(encoding.TenPower)});",
            GesLayoutKind.Float => $"private static double __Value{node}(ulong wire) => BitConverter.Int64BitsToDouble(unchecked((long)wire));",
            _ => $"private static {type} __Value{node}(ulong wire) => ({type})wire;"
        });
    }

    // The storage of a Text: `prefix` is empty for this record and "other." or "baseline." for another.
    private static string Text(GesPlanValue value, string prefix) => $"new ReadOnlySpan<byte>({prefix}__{value.Base}, 0, {prefix}__{value.Base}Length)";

    private static string Prefix(string target) => target.Contains('.') ? target.Substring(0, target.LastIndexOf('.') + 1) : "";

    // Returns false from the enclosing method when the wire values of a and b differ.
    private static void Same(GesSchemaPlanner.Code code, GesPlanValue value, string a, string b)
    {
        var node = N(value.Node);
        switch (value.Encoding.Kind)
        {
            case GesLayoutKind.Boolean:
                code.Line($"if ({a} != {b}) return false;");
                break;
            case GesLayoutKind.Integer or GesLayoutKind.Quantized or GesLayoutKind.Float or GesLayoutKind.Tag:
                code.Line($"if (__Wire{node}({a}, paths, at) != __Wire{node}({b}, paths, at)) return false;");
                break;
            case GesLayoutKind.Text or GesLayoutKind.Bytes:
                code.Line($"if (!{Text(value, Prefix(a))}.SequenceEqual({Text(value, Prefix(b))})) return false;");
                break;
            case GesLayoutKind.Point or GesLayoutKind.Vector:
                foreach (var axis in value.Axes) Same(code, axis, a + "." + axis.Base, b + "." + axis.Base);
                break;
            case GesLayoutKind.Schema:
                code.Line($"if (!{a}.__Same({b}, paths, at + {N(value.Node + 1)})) return false;");
                break;
            case GesLayoutKind.List:
                code.Line($"if ({a}.Count != {b}.Count) return false;");
                code.Line($"for (var index{node} = 0; index{node} < {a}.Count; index{node}++)");
                code.Open("{");
                var element = value.Element!;
                if (value.Item is not null) code.Line($"if (!{a}[index{node}].__Same({b}[index{node}], paths, at + {N(element.Node)})) return false;");
                else Same(code, element, $"{a}[index{node}]", $"{b}[index{node}]");
                code.Close();
                break;
            case GesLayoutKind.Union:
                code.Line($"if ({a} != {b}) return false;");
                code.Line($"switch ({a})");
                code.Open("{");
                foreach (var alternative in value.Alternatives)
                {
                    if (alternative.Encoding.Kind == GesLayoutKind.Nothing) continue;
                    code.Line($"case {NestedName(value)}.{alternative.Label}:");
                    code.Open("{");
                    Same(code, alternative, Prefix(a) + alternative.Base, Prefix(b) + alternative.Base);
                    code.Line("break;");
                    code.Close();
                }

                code.Close();
                break;
        }
    }

    // The storage that a read into this record writes: the record, the class records and lists it holds and every pooled element.
    // Debug builds mark it while a delta reads, so that a baseline holding any of it is rejected before the read overwrites what
    // it holds; release builds generate none of it.
    private static void Sharing(GesSchemaPlanner.Code code, GesPlanType type)
    {
        code.Blank();
        code.Line(CheckSharing);
        if (type.Kind == GesSchemaKind.Schema && !type.IsItem) code.Line("[ThreadStatic] private static HashSet<object>? __SharingLists;");
        code.Line("internal bool __Busy;");
        code.Blank();
        if (!type.Fields.Exists(field => GesSchemaPlanner.HoldsStorage(field.Value, true)))
        {
            code.Line("internal void __Mark(bool busy, HashSet<object>? lists) => __Busy = busy;");
            code.Blank();
            code.Line("internal bool __Holds(HashSet<object> lists) => __Busy;");
        }
        else
        {
            code.Line("internal void __Mark(bool busy, HashSet<object>? lists)");
            code.Open("{");
            code.Line("__Busy = busy;");
            foreach (var field in type.Fields) Mark(code, field.Value, field.Base);
            code.Close();
            code.Blank();
            code.Line("internal bool __Holds(HashSet<object> lists)");
            code.Open("{");
            code.Line("if (__Busy) return true;");
            foreach (var field in type.Fields) Holds(code, field.Value, field.Base);
            code.Line("return false;");
            code.Close();
        }

        code.Line("#endif");
    }

    // The builds that check shared baselines: debug builds, Unity's editor and development players, and any build that asks.
    private const string CheckSharing = "#if DEBUG || DEVELOPMENT_BUILD || UNITY_EDITOR || GES_CHECK_SHARING";

    private static void Mark(GesSchemaPlanner.Code code, GesPlanValue value, string member)
    {
        var node = N(value.Node);
        switch (value.Encoding.Kind)
        {
            case GesLayoutKind.Schema when value.IsClass:
                code.Line($"{member}.__Mark(busy, lists);");
                break;
            case GesLayoutKind.List:
                code.Line($"lists?.Add({member});");
                if (value.Element!.IsClass) code.Line($"for (var index{node} = 0; index{node} < __{value.Base}Pool.Count; index{node}++) __{value.Base}Pool[index{node}].__Mark(busy, lists);");
                break;
            case GesLayoutKind.Union:
                foreach (var alternative in value.Alternatives) Mark(code, alternative, Prefix(member) + alternative.Base);
                break;
        }
    }

    private static void Holds(GesSchemaPlanner.Code code, GesPlanValue value, string member)
    {
        var node = N(value.Node);
        switch (value.Encoding.Kind)
        {
            case GesLayoutKind.Schema when value.IsClass:
                code.Line($"if ({member}.__Holds(lists)) return true;");
                break;
            case GesLayoutKind.List:
                code.Line($"if (lists.Contains({member})) return true;");
                if (!value.Element!.IsClass) break;
                code.Line($"for (var index{node} = 0; index{node} < {member}.Count; index{node}++)");
                code.Line($"    if ({member}[index{node}].__Holds(lists)) return true;");
                break;
            case GesLayoutKind.Union:
                foreach (var alternative in value.Alternatives) Holds(code, alternative, Prefix(member) + alternative.Base);
                break;
        }
    }

    // Copies b into a without allocating once a's storage has grown; a list that b shares with a already holds the values.
    private static void Copy(GesSchemaPlanner.Code code, GesPlanValue value, string a, string b)
    {
        var node = N(value.Node);
        switch (value.Encoding.Kind)
        {
            case GesLayoutKind.Text or GesLayoutKind.Bytes:
                code.Line($"Set{value.Base}{(value.Encoding.Kind == GesLayoutKind.Text ? "Utf8" : "")}({Text(value, Prefix(b))});");
                break;
            case GesLayoutKind.Schema when value.IsClass:
                code.Line($"{a}.CopyFrom({b});");
                break;
            case GesLayoutKind.List when value.Element!.IsClass:
                // The same list already holds the values; clearing it first would lose them.
                code.Line($"if (!ReferenceEquals({a}, {b}))");
                code.Open("{");
                code.Line($"{a}.Clear();");
                code.Line($"for (var index{node} = 0; index{node} < {b}.Count; index{node}++)");
                code.Open("{");
                code.Line($"if (index{node} == __{value.Base}Pool.Count) __{value.Base}Pool.Add(new {ElementName(value)}());");
                code.Line($"var item{node} = __{value.Base}Pool[index{node}];");
                code.Line($"item{node}.CopyFrom({b}[index{node}]);");
                code.Line($"{a}.Add(item{node});");
                code.Close();
                code.Close();
                break;
            case GesLayoutKind.List:
                code.Line($"if (!ReferenceEquals({a}, {b}))");
                code.Open("{");
                code.Line($"{a}.Clear();");
                code.Line($"{a}.AddRange({b});");
                code.Close();
                break;
            case GesLayoutKind.Union:
                code.Line($"{a} = {b};");
                code.Line($"switch ({a})");
                code.Open("{");
                foreach (var alternative in value.Alternatives)
                {
                    if (alternative.Encoding.Kind == GesLayoutKind.Nothing) continue;
                    code.Line($"case {NestedName(value)}.{alternative.Label}:");
                    code.Open("{");
                    Copy(code, alternative, Prefix(a) + alternative.Base, Prefix(b) + alternative.Base);
                    code.Line("break;");
                    code.Close();
                }

                code.Close();
                break;
            default:
                code.Line($"{a} = {b};");
                break;
        }
    }

    private static void Quantize(GesSchemaPlanner.Code code, GesPlanValue value, string target)
    {
        var node = N(value.Node);
        switch (value.Encoding.Kind)
        {
            case GesLayoutKind.Quantized:
                code.Line($"{target} = __Value{node}(__Wire{node}({target}, paths, at));");
                break;
            case GesLayoutKind.Point or GesLayoutKind.Vector:
                foreach (var axis in value.Axes) Quantize(code, axis, target + "." + axis.Base);
                break;
            case GesLayoutKind.Schema:
                code.Line($"{target}.__Quantize(paths, at + {N(value.Node + 1)});");
                break;
            case GesLayoutKind.List when Quantized(value.Element!):
                var element = value.Element!;
                code.Line($"for (var index{node} = 0; index{node} < {target}.Count; index{node}++)");
                code.Open("{");
                if (element.IsClass) code.Line($"{target}[index{node}].__Quantize(paths, at + {N(value.Item is not null ? element.Node : element.Node + 1)});");
                else
                {
                    code.Line($"var item{node} = {target}[index{node}];");
                    if (value.Item is not null) code.Line($"item{node}.__Quantize(paths, at + {N(element.Node)});");
                    else Quantize(code, element, $"item{node}");
                    code.Line($"{target}[index{node}] = item{node};");
                }

                code.Close();
                break;
            case GesLayoutKind.Union when value.Alternatives.Exists(Quantized):
                code.Line($"switch ({target})");
                code.Open("{");
                foreach (var alternative in value.Alternatives)
                {
                    if (!Quantized(alternative)) continue;
                    code.Line($"case {NestedName(value)}.{alternative.Label}:");
                    code.Open("{");
                    Quantize(code, alternative, alternative.Base);
                    code.Line("break;");
                    code.Close();
                }

                code.Close();
                break;
        }
    }

    // Whether a value holds a quantized number that Quantize changes; schemas always forward, as theirs may.
    private static bool Quantized(GesPlanValue value) => value.Encoding.Kind switch
    {
        GesLayoutKind.Quantized or GesLayoutKind.Schema => true,
        GesLayoutKind.Point or GesLayoutKind.Vector => value.Axes.Exists(Quantized),
        GesLayoutKind.Union => value.Alternatives.Exists(Quantized),
        GesLayoutKind.List => value.Item is not null ? Quantized(value.Item.Fields[0].Value) : Quantized(value.Element!),
        _ => false
    };

    private static void Deltas(GesSchemaPlanner.Code code, GesPlanType type, string self, string parameter)
    {
        var key = type.Fields.Count > 0 && type.Fields[0].Key ? type.Fields[0] : null;
        code.Blank();
        code.Line("/// <summary>Writes what differs from a baseline, the full record the receiver holds; <see cref=\"MaxByteCount\"/> bytes always suffice.</summary>");
        code.Line("/// <param name=\"baseline\">The baseline, another instance.</param>");
        code.Line("/// <param name=\"buffer\">The buffer.</param>");
        code.Line("/// <returns>The number of bytes written.</returns>");
        code.Line("/// <exception cref=\"GesWireException\">A value does not fit its field, the keys differ, or the buffer is too small.</exception>");
        code.Line($"public {self}int WriteDelta({parameter} baseline, Span<byte> buffer)");
        code.Open("{");
        code.Line("var writer = new GesBitWriter(buffer);");
        if (key is not null)
        {
            code.Line("var paths = __paths;");
            code.Line("const int at = 0;");
            code.Line($"var key = __Wire0({key.Base}, paths, at);");
            code.Line($"if (key != __Wire0(baseline.{key.Base}, paths, at)) throw new GesWireException(GesWireException.KeyMismatch, paths[0]);");
            code.Line($"writer.Write(key, {key.Value.Encoding.Width.ToString(CultureInfo.InvariantCulture)});");
        }

        code.Line("__WriteDelta(baseline, ref writer, __paths, 0, true);");
        code.Line("return writer.Finish();");
        code.Close();
        code.Blank();
        code.Line("/// <summary>Writes what differs from a baseline into a new array of its exact size.</summary>");
        code.Line("/// <param name=\"baseline\">The baseline, another instance.</param>");
        code.Line("/// <returns>The bytes.</returns>");
        code.Line("/// <exception cref=\"GesWireException\">A value does not fit its field, or the keys differ.</exception>");
        code.Line($"public {self}byte[] ToDeltaBytes({parameter} baseline)");
        code.Open("{");
        code.Line("var buffer = new byte[MaxByteCount];");
        code.Line("return buffer.AsSpan(0, WriteDelta(baseline, buffer)).ToArray();");
        code.Close();
        code.Blank();
        code.Line("/// <summary>Reads a delta against a baseline into this instance. After an error its values are unspecified.</summary>");
        code.Line("/// <param name=\"data\">The delta; it may continue after it.</param>");
        code.Line("/// <param name=\"baseline\">The baseline, another instance.</param>");
        code.Line("/// <param name=\"byteLimit\">An optional limit on the bytes of this delta.</param>");
        code.Line("/// <returns>The number of bytes the delta occupies.</returns>");
        code.Line("/// <exception cref=\"GesWireException\">The data is no valid delta for this baseline.</exception>");
        code.Line($"public int ReadDelta(ReadOnlySpan<byte> data, {parameter} baseline, long byteLimit = long.MaxValue)");
        code.Open("{");
        if (type.IsClass)
        {
            // Sharing other storage is a programming error, which only debug builds pay to detect.
            code.Line("if (ReferenceEquals(this, baseline)) throw new GesWireException(GesWireException.OverlappingBuffers, \"\");");
            code.Line(CheckSharing);
            code.Line("var lists = __SharingLists ??= new HashSet<object>();");
            code.Line("__Mark(true, lists);");
            code.Line("bool shared;");
            code.Line("try");
            code.Open("{");
            code.Line("shared = baseline.__Holds(lists);");
            code.Close();
            code.Line("finally");
            code.Open("{");
            code.Line("__Mark(false, null);");
            code.Line("// A set that grew beyond common records is dropped, so that no thread keeps its capacity for good.");
            code.Line("if (lists.Count > 1024) __SharingLists = null;");
            code.Line("else lists.Clear();");
            code.Close();
            code.Line("if (shared) throw new GesWireException(GesWireException.OverlappingBuffers, \"\");");
            code.Line("#endif");
        }

        code.Line("var reader = new GesBitReader(data, byteLimit);");
        if (key is not null)
        {
            code.Line("var paths = __paths;");
            code.Line("const int at = 0;");
            code.Line("var key = __Read0(ref reader, paths, at);");
            code.Line($"if (key != __Wire0(baseline.{key.Base}, paths, at)) throw new GesWireException(GesWireException.InvalidDelta, paths[0]);");
        }

        code.Line("__ReadDelta(baseline, ref reader, __paths, 0, true);");
        code.Line("return reader.Finish();");
        code.Close();

        var count = type.Fields.Count.ToString(CultureInfo.InvariantCulture);
        var counted = key is null ? count : $"top ? {N(type.Fields.Count - 1)} : {count}";
        code.Blank();
        code.Line($"internal {self}bool __WriteDelta({parameter} baseline, ref GesBitWriter writer, string[] paths, int at, bool top)");
        code.Open("{");
        if (type.Fields.Count == 0)
        {
            // A schema without fields has no change bits and never changes.
            code.Line("return false;");
            code.Close();
            code.Blank();
            code.Line($"internal bool __ReadDelta({parameter} baseline, ref GesBitReader reader, string[] paths, int at, bool top) => false;");
            return;
        }

        code.Line("var first = writer.BitPosition;");
        code.Line($"for (var left = {counted}; left > 0; left -= 64) writer.Write(0, Math.Min(left, 64));");
        code.Line("var changed = false;");
        code.Line("var ordinal = 0;");
        foreach (var field in type.Fields)
        {
            var node = N(field.Value.Node);
            if (field.Key) code.Line("if (!top)");
            code.Open("{");
            code.Line($"var set{node} = false;");
            DeltaField(code, field, $"set{node}");
            code.Line($"if (set{node})");
            code.Open("{");
            code.Line("writer.SetBit(first + ordinal);");
            code.Line("changed = true;");
            code.Close();
            code.Line("ordinal++;");
            code.Close();
        }

        code.Line("return changed;");
        code.Close();
        code.Blank();
        code.Line($"internal bool __ReadDelta({parameter} baseline, ref GesBitReader reader, string[] paths, int at, bool top)");
        code.Open("{");
        code.Line($"var fieldCount = {counted};");
        code.Line("reader.Require(fieldCount, at == 0 ? \"\" : paths[at - 1]);");
        code.Line("var changeBits = reader.BitPosition;");
        code.Line("reader.Seek(changeBits + fieldCount);");
        code.Line("var changed = false;");
        code.Line("var ordinal = 0;");
        foreach (var field in type.Fields)
        {
            var node = N(field.Value.Node);
            if (field.Key) code.Line("if (!top)");
            code.Open("{");
            code.Line($"var payload{node} = reader.BitPosition;");
            code.Line("reader.Seek(changeBits + ordinal);");
            code.Line($"var set{node} = reader.Read(1, {Path(field.Value)}) == 1;");
            code.Line($"reader.Seek(payload{node});");
            code.Line($"if (set{node})");
            code.Open("{");
            ReadDeltaField(code, field);
            code.Line("changed = true;");
            code.Close();
            code.Line("else");
            code.Open("{");
            if (field.Optional) code.Line($"Has{field.Base} = baseline.Has{field.Base};");
            Copy(code, field.Value, field.Base, "baseline." + field.Base);
            code.Close();
            code.Line("ordinal++;");
            code.Close();

            // Without a change bit the key equals the baseline's: the caller has compared it, or it identifies a list element.
            if (field.Key) code.Line($"else {field.Base} = __Value0(__Wire0(baseline.{field.Base}, paths, at));");
        }

        code.Line("return changed;");
        code.Close();
    }

    private static void DeltaField(GesSchemaPlanner.Code code, GesPlanField field, string set)
    {
        if (!field.Optional)
        {
            DeltaPayload(code, field.Value, field.Base, "baseline." + field.Base, set);
            return;
        }

        var node = N(field.Value.Node);
        code.Line($"if (Has{field.Base} != baseline.Has{field.Base})");
        code.Open("{");
        code.Line($"writer.Write(Has{field.Base} ? 1UL : 0UL, 1);");
        code.Line($"if (Has{field.Base})");
        code.Open("{");
        Write(code, field.Value, field.Base);
        code.Close();
        code.Line($"{set} = true;");
        code.Close();
        code.Line($"else if (Has{field.Base})");
        code.Open("{");
        code.Line($"var presence{node} = writer.BitPosition;");
        code.Line("writer.Write(1, 1);");
        DeltaPayload(code, field.Value, field.Base, "baseline." + field.Base, set);
        code.Line($"if (!{set}) writer.Rewind(presence{node});");
        code.Close();
    }

    // Writes the payload of a changed value and sets `set`; leaves nothing for an unchanged one.
    private static void DeltaPayload(GesSchemaPlanner.Code code, GesPlanValue value, string a, string b, string set)
    {
        var encoding = value.Encoding;
        var node = N(value.Node);
        var width = encoding.Width.ToString(CultureInfo.InvariantCulture);
        switch (encoding.Kind)
        {
            case GesLayoutKind.Boolean:
                code.Line($"if ({a} != {b}) {set} = true;");
                break;
            case GesLayoutKind.Integer or GesLayoutKind.Quantized or GesLayoutKind.Float or GesLayoutKind.Tag:
                code.Open("{");
                code.Line($"var value{node} = __Wire{node}({a}, paths, at);");
                code.Line($"var held{node} = __Wire{node}({b}, paths, at);");
                code.Line($"if (value{node} != held{node})");
                code.Open("{");
                code.Line(encoding.DeltaRange == 0
                    ? $"writer.Write(value{node}, {width});"
                    : $"GesWireRules.WriteDeltaRange(ref writer, value{node}, held{node}, {Unsigned(encoding.MaxIndex)}, {(encoding.Wrapping ? "true" : "false")}, {Unsigned(encoding.DeltaRange)}, {encoding.DeltaWidth.ToString(CultureInfo.InvariantCulture)}, {width});");
                code.Line($"{set} = true;");
                code.Close();
                code.Close();
                break;
            case GesLayoutKind.List when encoding.Keyed:
                code.Line($"if (__WriteKeyed{node}(baseline, ref writer, paths, at)) {set} = true;");
                break;
            case GesLayoutKind.Text or GesLayoutKind.Bytes or GesLayoutKind.List:
                code.Line($"if (!__Same{node}(baseline, paths, at))");
                code.Open("{");
                Write(code, value, a);
                code.Line($"{set} = true;");
                code.Close();
                break;
            case GesLayoutKind.Point or GesLayoutKind.Vector:
                code.Open("{");
                code.Line($"var first{node} = writer.BitPosition;");
                code.Line($"writer.Write(0, {N(value.Axes.Count)});");
                for (var index = 0; index < value.Axes.Count; index++)
                {
                    var axis = value.Axes[index];
                    code.Open("{");
                    code.Line($"var set{N(axis.Node)} = false;");
                    DeltaPayload(code, axis, a + "." + axis.Base, b + "." + axis.Base, $"set{N(axis.Node)}");
                    code.Line($"if (set{N(axis.Node)})");
                    code.Open("{");
                    code.Line($"writer.SetBit(first{node} + {N(index)});");
                    code.Line($"{set} = true;");
                    code.Close();
                    code.Close();
                }

                foreach (var axis in GesSchemaPlanner.Omitted(value)) code.Line($"if ({a}.{axis} != 0) {Throw("TypeMismatch", value)}");
                code.Line($"if (!{set}) writer.Rewind(first{node});");
                code.Close();
                break;
            case GesLayoutKind.Schema:
                code.Open("{");
                code.Line($"var start{node} = writer.BitPosition;");
                code.Line($"if ({a}.__WriteDelta({b}, ref writer, paths, at + {N(value.Node + 1)}, false)) {set} = true;");
                code.Line($"else writer.Rewind(start{node});");
                code.Close();
                break;
            case GesLayoutKind.Union:
                code.Open("{");
                code.Line($"if ((ulong){a} > {Unsigned((ulong)value.Alternatives.Count - 1)}) {Throw("OutOfRange", value)}");
                code.Line($"if ({a} != {b})");
                code.Open("{");
                Write(code, value, a);
                code.Line($"{set} = true;");
                code.Close();
                code.Line("else");
                code.Open("{");
                code.Line($"var entry{node} = writer.BitPosition;");
                code.Line($"writer.Write((ulong){a}, {width});");
                code.Line($"switch ({a})");
                code.Open("{");
                foreach (var alternative in value.Alternatives)
                {
                    if (alternative.Encoding.Kind == GesLayoutKind.Nothing) continue;
                    code.Line($"case {NestedName(value)}.{alternative.Label}:");
                    code.Open("{");
                    DeltaPayload(code, alternative, Prefix(a) + alternative.Base, Prefix(b) + alternative.Base, set);
                    code.Line("break;");
                    code.Close();
                }

                code.Close();
                code.Line($"if (!{set}) writer.Rewind(entry{node});");
                code.Close();
                code.Close();
                break;
        }
    }

    private static void ReadDeltaField(GesSchemaPlanner.Code code, GesPlanField field)
    {
        if (!field.Optional)
        {
            ReadDeltaPayload(code, field.Value, field.Base, "baseline." + field.Base);
            return;
        }

        var node = N(field.Value.Node);
        code.Line($"var present{node} = reader.Read(1, {Path(field.Value)}) == 1;");
        code.Line($"if (!present{node} && !baseline.Has{field.Base}) {Throw("InvalidDelta", field.Value)}");
        code.Line($"Has{field.Base} = present{node};");
        code.Line($"if (present{node} && baseline.Has{field.Base})");
        code.Open("{");
        ReadDeltaPayload(code, field.Value, field.Base, "baseline." + field.Base);
        code.Close();
        code.Line($"else if (present{node})");
        code.Open("{");
        Read(code, field.Value, field.Base);
        code.Close();
        if (Plain(field.Value)) code.Line($"else {field.Base} = default;");
    }

    // Reads the payload of a value whose change bit is set, which must be a change.
    private static void ReadDeltaPayload(GesSchemaPlanner.Code code, GesPlanValue value, string a, string b)
    {
        var encoding = value.Encoding;
        var node = N(value.Node);
        var width = encoding.Width.ToString(CultureInfo.InvariantCulture);
        switch (encoding.Kind)
        {
            case GesLayoutKind.Boolean:
                code.Line($"{a} = !{b};");
                break;
            case GesLayoutKind.Integer or GesLayoutKind.Quantized or GesLayoutKind.Float or GesLayoutKind.Tag when encoding.DeltaRange == 0:
                code.Open("{");
                code.Line($"var held{node} = __Wire{node}({b}, paths, at);");
                code.Line($"var value{node} = __Read{node}(ref reader, paths, at);");
                code.Line($"if (value{node} == held{node}) {Throw("InvalidDelta", value)}");
                code.Line($"{a} = __Value{node}(value{node});");
                code.Close();
                break;
            case GesLayoutKind.Integer or GesLayoutKind.Quantized:
                code.Line($"{a} = __Value{node}(GesWireRules.ReadDeltaRange(ref reader, __Wire{node}({b}, paths, at), {Unsigned(encoding.MaxIndex)}, {(encoding.Wrapping ? "true" : "false")}, {Unsigned(encoding.DeltaRange)}, {encoding.DeltaWidth.ToString(CultureInfo.InvariantCulture)}, {width}, {Path(value)}));");
                break;
            case GesLayoutKind.List when encoding.Keyed:
                code.Line($"__ReadKeyed{node}(baseline, ref reader, paths, at);");
                break;
            case GesLayoutKind.Text or GesLayoutKind.Bytes or GesLayoutKind.List:
                Read(code, value, a);
                code.Line($"if (__Same{node}(baseline, paths, at)) {Throw("InvalidDelta", value)}");
                break;
            case GesLayoutKind.Point or GesLayoutKind.Vector:
                code.Open("{");
                code.Line($"reader.Require({N(value.Axes.Count)}, {Path(value)});");
                code.Line($"var bits{node} = reader.BitPosition;");
                code.Line($"reader.Seek(bits{node} + {N(value.Axes.Count)});");
                code.Line($"var changed{node} = false;");
                for (var index = 0; index < value.Axes.Count; index++)
                {
                    var axis = value.Axes[index];
                    var axisNode = N(axis.Node);
                    code.Open("{");
                    code.Line($"var payload{axisNode} = reader.BitPosition;");
                    code.Line($"reader.Seek(bits{node} + {N(index)});");
                    code.Line($"var set{axisNode} = reader.Read(1, {Path(axis)}) == 1;");
                    code.Line($"reader.Seek(payload{axisNode});");
                    code.Line($"if (set{axisNode})");
                    code.Open("{");
                    ReadDeltaPayload(code, axis, a + "." + axis.Base, b + "." + axis.Base);
                    code.Line($"changed{node} = true;");
                    code.Close();
                    code.Line($"else {a}.{axis.Base} = {b}.{axis.Base};");
                    code.Close();
                }

                foreach (var axis in GesSchemaPlanner.Omitted(value)) code.Line($"{a}.{axis} = 0;");
                code.Line($"if (!changed{node}) {Throw("InvalidDelta", value)}");
                code.Close();
                break;
            case GesLayoutKind.Schema:
                code.Line($"if (!{a}.__ReadDelta({b}, ref reader, paths, at + {N(value.Node + 1)}, false)) {Throw("InvalidDelta", value)}");
                break;
            case GesLayoutKind.Union:
                code.Open("{");
                code.Line($"var choice{node} = reader.Read({width}, {Path(value)});");
                if ((ulong)value.Alternatives.Count - 1 < Full(encoding.Width)) code.Line($"if (choice{node} > {Unsigned((ulong)value.Alternatives.Count - 1)}) {Throw("InvalidIndex", value)}");
                code.Line($"var same{node} = choice{node} == (ulong){b};");
                code.Line($"{a} = ({NestedName(value)})choice{node};");
                code.Line($"switch (choice{node})");
                code.Open("{");
                for (var index = 0; index < value.Alternatives.Count; index++)
                {
                    var alternative = value.Alternatives[index];
                    code.Line($"case {index.ToString(CultureInfo.InvariantCulture)}:");
                    code.Open("{");
                    if (alternative.Encoding.Kind == GesLayoutKind.Nothing) code.Line($"if (same{node}) {Throw("InvalidDelta", alternative)}");
                    else
                    {
                        code.Line($"if (same{node})");
                        code.Open("{");
                        ReadDeltaPayload(code, alternative, Prefix(a) + alternative.Base, Prefix(b) + alternative.Base);
                        code.Close();
                        code.Line("else");
                        code.Open("{");
                        Read(code, alternative, Prefix(a) + alternative.Base);
                        code.Close();
                    }

                    code.Line("break;");
                    code.Close();
                }

                code.Close();
                code.Close();
                break;
        }
    }

    // A comparison of one Text or List field with the baseline's, for the whole-value payloads.
    private static void SameHelpers(GesSchemaPlanner.Code code, GesPlanType type, string global)
    {
        var parameter = type.IsClass ? type.Name : "in " + type.Name;
        foreach (var field in type.Fields) SameHelper(code, field.Value, parameter, type.IsClass);
        foreach (var field in type.Fields) KeyedHelpers(code, field.Value, type.Name, global);
        if (type.Fields.Exists(field => GesSchemaPlanner.HasKeyed(field.Value))) KeyedStatics(code);
    }

    private static void SameHelper(GesSchemaPlanner.Code code, GesPlanValue value, string parameter, bool isClass)
    {
        if (value.Encoding.Kind is GesLayoutKind.Text or GesLayoutKind.Bytes or GesLayoutKind.List)
        {
            code.Blank();
            code.Line($"private {(isClass ? "" : "readonly ")}bool __Same{N(value.Node)}({parameter} baseline, string[] paths, int at)");
            code.Open("{");
            Same(code, value, value.Base, "baseline." + value.Base);
            code.Line("return true;");
            code.Close();
        }

        foreach (var alternative in value.Alternatives) SameHelper(code, alternative, parameter, isClass);
    }
}
