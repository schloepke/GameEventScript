// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Globalization;
using GameEventScript.Api;

namespace GameEventScript.Generator;

// Wire values, comparison, copying, quantization and deltas of generated Swift types. Comparison and deltas use wire values:
// indices and stored bits, never raw doubles. Local names carry the node of their value, as in the C# output.
internal static partial class GesSwiftEmitter
{
    private static string N(int node) => node.ToString(CultureInfo.InvariantCulture);

    // The level argument that a recursive List's own helpers take.
    private static string Level(GesPlanValue value) => value.Depth > 0 ? ", level" : "";

    private static bool Scalar(GesPlanValue value) => value.Encoding.Kind is GesLayoutKind.Boolean or GesLayoutKind.Integer or GesLayoutKind.Quantized or GesLayoutKind.Float or GesLayoutKind.Tag or GesLayoutKind.Flags;

    private static IEnumerable<GesPlanValue> Scalars(GesPlanValue value)
    {
        if (Scalar(value)) yield return value;
        foreach (var axis in value.Axes) yield return axis;
        foreach (var corner in value.Corners)
        {
            if (Scalar(corner)) yield return corner;
            foreach (var axis in corner.Axes) yield return axis;
        }
        foreach (var alternative in value.Alternatives)
            foreach (var nested in Scalars(alternative)) yield return nested;
        if (value.Item is null && value.Element is not null)
            foreach (var nested in Scalars(value.Element)) yield return nested;
    }

    private static string Target(string field) => "self." + Member(field);

    private static string Other(string owner, string field) => owner + "." + Member(field);

    private static void Extras(GesSchemaPlanner.Code code, GesPlanType type, string access, bool deltas, bool element)
    {
        var mutating = type.IsClass ? "" : "mutating ";
        foreach (var field in type.Fields)
            foreach (var value in Scalars(field.Value)) Helpers(code, value);

        code.Blank();
        code.Open($"func __same(_ other: {type.Name}, _ paths: [String], _ at: Int{LevelParameter(type)}) throws(GesWireError) -> Bool {{");
        foreach (var field in type.Fields)
        {
            if (field.Optional)
            {
                code.Line($"if self.has{field.Base} != other.has{field.Base} {{ return false }}");
                code.Open($"if self.has{field.Base} {{");
                Same(code, field.Value, Target(field.Base), Other("other", field.Base), "self", "other");
                code.Close();
            }
            else Same(code, field.Value, Target(field.Base), Other("other", field.Base), "self", "other");
        }

        foreach (var (_, name, _) in type.Tags) code.Line($"if self.{Member(name)} != other.{Member(name)} {{ return false }}");
        code.Line("return true");
        code.Close();

        if (type.IsClass)
        {
            code.Blank();
            code.Line("/// Copies the values of another record into the storage of this one, without allocating once it has grown.");
            code.Open($"{access}func copy(from other: {type.Name}) {{");
            code.Line("if self === other { return }");
            foreach (var field in type.Fields)
            {
                if (field.Optional) code.Line($"self.has{field.Base} = other.has{field.Base}");
                Copy(code, field.Value, Target(field.Base), Other("other", field.Base), "other");
            }

            foreach (var (_, name, _) in type.Tags) code.Line($"self.{Member(name)} = other.{Member(name)}");
            code.Close();
        }

        code.Blank();
        code.Open($"{mutating}func __quantize(_ paths: [String], _ at: Int{LevelParameter(type)}) throws(GesWireError) {{");
        foreach (var field in type.Fields)
        {
            if (field.Optional)
            {
                code.Open($"if self.has{field.Base} {{");
                Quantize(code, field.Value, Target(field.Base));
                code.Close();
            }
            else Quantize(code, field.Value, Target(field.Base));
        }

        code.Close();
        if (type.IsItem) return;

        code.Blank();
        code.Line($"/// Returns whether another {Kind(type)} has the same wire values, so that it writes the same bytes.");
        code.Line("/// - Throws: `GesWireError` when a compared value has no wire value, such as a number outside its range.");
        code.Open($"{access}func wireEquals(_ other: {type.Name}) throws(GesWireError) -> Bool {{");
        code.Line("try __same(other, Self.__paths, 0)");
        code.Close();
        code.Blank();
        code.Line("/// Replaces every quantized value by the value its index decodes to, which a receiver reads.");
        code.Line("/// - Throws: `GesWireError` when a quantized value lies outside its range.");
        code.Open($"{access}{mutating}func quantize() throws(GesWireError) {{");
        code.Line("try __quantize(Self.__paths, 0)");
        code.Close();
        if (element) KeyOrder(code, type);
        if (deltas) Deltas(code, type, access, mutating);
    }

    private static void Helpers(GesSchemaPlanner.Code code, GesPlanValue value)
    {
        var encoding = value.Encoding;
        var node = N(value.Node);
        var type = TypeName(value);
        var width = encoding.Width.ToString(CultureInfo.InvariantCulture);
        code.Blank();
        code.Open($"private static func __wire{node}(_ value: {type}, _ paths: [String], _ at: Int) throws(GesWireError) -> UInt64 {{");
        switch (encoding.Kind)
        {
            case GesLayoutKind.Boolean:
                code.Line("value ? 1 : 0");
                break;
            case GesLayoutKind.Integer when !encoding.Range:
                code.Line("UInt64(bitPattern: value)");
                break;
            case GesLayoutKind.Integer when encoding.Wrapping:
                code.Line($"GesWireRules.wrappedIndex(value, lower: {Long(encoding.Lower)}, period: {Unsigned(encoding.MaxIndex + 1)})");
                break;
            case GesLayoutKind.Integer:
                var lower = Unsigned(unchecked((ulong)encoding.Lower));
                code.Line($"if value < {Long(encoding.Lower)} || UInt64(bitPattern: value) &- {lower} > {Unsigned(encoding.MaxIndex)} {{ {Throw("outOfRange", value)} }}");
                code.Line($"return UInt64(bitPattern: value) &- {lower}");
                break;
            case GesLayoutKind.Quantized when encoding.Wrapping:
                code.Line($"try GesWireRules.wrappedQuantizedIndex(value, tenPower: {Double(encoding.TenPower)}, numerator: {Double(encoding.Numerator)}, lower: {Long(encoding.Lower)}, period: {Unsigned(encoding.MaxIndex + 1)}, path: {Path(value)})");
                break;
            case GesLayoutKind.Quantized:
                code.Line($"try GesWireRules.quantizedIndex(value, lowerValue: {Bits(encoding.LowerValue)}, upperValue: {Bits(encoding.UpperValue)}, tenPower: {Double(encoding.TenPower)}, numerator: {Double(encoding.Numerator)}, lower: {Long(encoding.Lower)}, path: {Path(value)})");
                break;
            case GesLayoutKind.Float:
                code.Line($"if value.isNaN {{ {Throw("notRepresentable", value)} }}");
                if (encoding.Range) code.Line($"if value < {Bits(encoding.LowerValue)} || value > {Bits(encoding.UpperValue)} {{ {Throw("outOfRange", value)} }}");
                code.Line("return (value == 0 ? 0.0 : value).bitPattern");
                break;
            case GesLayoutKind.Flags:
                if (encoding.Width < 64) code.Line($"if value.rawValue > {Unsigned(Full(encoding.Width))} {{ {Throw("unknownTag", value)} }}");
                code.Line("return value.rawValue");
                break;
            default:
                code.Line("UInt64(value.rawValue)");
                break;
        }

        code.Close();
        code.Blank();
        code.Open($"private static func __read{node}(_ reader: inout GesBitReader, _ paths: [String], _ at: Int) throws(GesWireError) -> UInt64 {{");
        code.Line($"let wire = try reader.read({width}, path: {Path(value)})");
        if (encoding.Kind == GesLayoutKind.Float)
        {
            code.Line("let value = Double(bitPattern: wire)");
            code.Line($"if value.isNaN || wire == 0x8000_0000_0000_0000{(encoding.Range ? " || value.isInfinite" : "")} {{ {Throw("invalidNumber", value)} }}");
            if (encoding.Range) code.Line($"if value < {Bits(encoding.LowerValue)} || value > {Bits(encoding.UpperValue)} {{ {Throw("outOfRange", value)} }}");
        }
        else if (encoding.Kind != GesLayoutKind.Boolean)
        {
            var largest = encoding.Kind switch
            {
                GesLayoutKind.Tag => (ulong)encoding.Tags.Count - 1,
                GesLayoutKind.Flags => Full(encoding.Width),
                _ => encoding.MaxIndex
            };
            if (largest < Full(encoding.Width)) code.Line($"if wire > {Unsigned(largest)} {{ {Throw("invalidIndex", value)} }}");
        }

        code.Line("return wire");
        code.Close();
        code.Blank();
        code.Line(encoding.Kind switch
        {
            GesLayoutKind.Boolean => $"private static func __value{node}(_ wire: UInt64) -> Bool {{ wire != 0 }}",
            GesLayoutKind.Integer when !encoding.Range => $"private static func __value{node}(_ wire: UInt64) -> Int64 {{ Int64(bitPattern: wire) }}",
            GesLayoutKind.Integer => $"private static func __value{node}(_ wire: UInt64) -> Int64 {{ Int64(bitPattern: {Unsigned(unchecked((ulong)encoding.Lower))} &+ wire) }}",
            GesLayoutKind.Quantized => $"private static func __value{node}(_ wire: UInt64) -> Double {{ GesWireRules.quantizedValue(wire, lower: {Long(encoding.Lower)}, numerator: {Long(encoding.Numerator)}, tenPower: {Double(encoding.TenPower)}) }}",
            GesLayoutKind.Float => $"private static func __value{node}(_ wire: UInt64) -> Double {{ Double(bitPattern: wire) }}",
            GesLayoutKind.Flags => $"private static func __value{node}(_ wire: UInt64) -> {type} {{ {type}(rawValue: wire) }}",
            _ => $"private static func __value{node}(_ wire: UInt64) -> {type} {{ {type}(rawValue: {Underlying(encoding.Tags.Count)}(wire))! }}"
        });
    }

    // The UTF-8 storage of a Text in `owner`, "self" or another record.
    private static string Bytes(GesPlanValue value, string owner) => Storage(value, owner);

    // Returns false from the enclosing function when the wire values of a and b differ.
    private static void Same(GesSchemaPlanner.Code code, GesPlanValue value, string a, string b, string ownerA, string ownerB)
    {
        var node = N(value.Node);
        switch (value.Encoding.Kind)
        {
            case GesLayoutKind.Boolean:
                code.Line($"if {a} != {b} {{ return false }}");
                break;
            case GesLayoutKind.Integer or GesLayoutKind.Quantized or GesLayoutKind.Float or GesLayoutKind.Tag or GesLayoutKind.Flags:
                code.Line($"if try Self.__wire{node}({a}, paths, at) != Self.__wire{node}({b}, paths, at) {{ return false }}");
                break;
            case GesLayoutKind.Text or GesLayoutKind.Bytes:
                code.Line($"if {Bytes(value, ownerA)} != {Bytes(value, ownerB)} {{ return false }}");
                break;
            case GesLayoutKind.Point or GesLayoutKind.Vector:
                foreach (var axis in value.Axes) Same(code, axis, a + "." + Member(axis.Base), b + "." + Member(axis.Base), ownerA, ownerB);
                break;
            case GesLayoutKind.Box or GesLayoutKind.Disc:
                foreach (var corner in value.Corners) Same(code, corner, a + "." + Member(corner.Base), b + "." + Member(corner.Base), ownerA, ownerB);
                break;
            case GesLayoutKind.Schema when value.Packed is not null:
                code.Line($"if {a}.rawValue != {b}.rawValue {{ return false }}");
                break;
            case GesLayoutKind.Schema:
                code.Line($"if !(try {a}.__same({b}, paths, at + {N(value.Node + 1)}{NextLevel(value)})) {{ return false }}");
                break;
            case GesLayoutKind.List:
                if (value.Depth > 0) OpenLevels(code, value, $"{a}.count + {b}.count");
                code.Line($"if {a}.count != {b}.count {{ return false }}");
                code.Open($"for index{node} in 0..<{a}.count {{");
                var element = value.Element!;
                if (value.Item is not null) code.Line($"if !(try {a}[index{node}].__same({b}[index{node}], paths, at + {N(element.Node)}{ItemLevel(element)})) {{ return false }}");
                else Same(code, element, $"{a}[index{node}]", $"{b}[index{node}]", ownerA, ownerB);
                code.Close();
                if (value.Depth > 0) code.Close();
                break;
            case GesLayoutKind.Union:
                code.Line($"if {a} != {b} {{ return false }}");
                code.Line($"switch {a} {{");
                foreach (var alternative in value.Alternatives)
                {
                    code.Open($"case .{Member(alternative.Label)}:");
                    if (alternative.Encoding.Kind == GesLayoutKind.Nothing) code.Line("break");
                    else Same(code, alternative, Other(ownerA, alternative.Base), Other(ownerB, alternative.Base), ownerA, ownerB);
                    code.Close("");
                }

                code.Line("}");
                break;
        }
    }

    // The storage that a read into this record writes: the record, the class records it holds and every pooled element. Debug
    // builds mark it while a delta reads, so that a baseline holding any of it is rejected before the read overwrites what it
    // holds; release builds generate none of it. Swift arrays are values, so only records can be shared.
    private static void Sharing(GesSchemaPlanner.Code code, GesPlanType type, string stored)
    {
        code.Blank();
        code.Line(CheckSharing);
        code.Line($"{stored}var __busy = false");
        code.Blank();
        if (!type.Fields.Exists(field => GesSchemaPlanner.HoldsStorage(field.Value, false)))
        {
            code.Line("func __mark(_ busy: Bool) { self.__busy = busy }");
            code.Blank();
            code.Line("func __holds() -> Bool { self.__busy }");
        }
        else
        {
            code.Open("func __mark(_ busy: Bool) {");
            code.Line("self.__busy = busy");
            foreach (var field in type.Fields) Mark(code, field.Value);
            code.Close();
            code.Blank();
            code.Open("func __holds() -> Bool {");
            code.Line("if self.__busy { return true }");
            foreach (var field in type.Fields) Holds(code, field.Value);
            code.Line("return false");
            code.Close();
        }

        code.Line("#endif");
    }

    // The builds that check shared baselines: debug builds and any build that asks.
    private const string CheckSharing = "#if DEBUG || GES_CHECK_SHARING";

    private static void Mark(GesSchemaPlanner.Code code, GesPlanValue value)
    {
        switch (value.Encoding.Kind)
        {
            case GesLayoutKind.Schema when value.IsClass:
                code.Line($"{Target(value.Base)}.__mark(busy)");
                break;
            case GesLayoutKind.List when value.Element!.IsClass:
                code.Line($"for item in self.__{GesSchemaPlanner.Camel(value.Base)}Pool {{ item.__mark(busy) }}");
                break;
            case GesLayoutKind.Union:
                foreach (var alternative in value.Alternatives) Mark(code, alternative);
                break;
        }
    }

    private static void Holds(GesSchemaPlanner.Code code, GesPlanValue value)
    {
        switch (value.Encoding.Kind)
        {
            case GesLayoutKind.Schema when value.IsClass:
                code.Line($"if {Target(value.Base)}.__holds() {{ return true }}");
                break;
            case GesLayoutKind.List when value.Element!.IsClass:
                code.Line($"for item in {Target(value.Base)} where item.__holds() {{ return true }}");
                break;
            case GesLayoutKind.Union:
                foreach (var alternative in value.Alternatives) Holds(code, alternative);
                break;
        }
    }

    // Copies b into a without allocating once a's storage has grown.
    private static void Copy(GesSchemaPlanner.Code code, GesPlanValue value, string a, string b, string ownerB)
    {
        var node = N(value.Node);
        switch (value.Encoding.Kind)
        {
            case GesLayoutKind.Text or GesLayoutKind.Bytes:
                code.Line($"{Bytes(value, "self")}.removeAll(keepingCapacity: true)");
                code.Line($"{Bytes(value, "self")}.append(contentsOf: {Bytes(value, ownerB)})");
                break;
            case GesLayoutKind.Schema when value.IsClass:
                code.Line($"{a}.copy(from: {b})");
                break;
            case GesLayoutKind.List when value.Element!.IsClass:
                var pool = $"self.__{GesSchemaPlanner.Camel(value.Base)}Pool";
                code.Line($"{a}.removeAll(keepingCapacity: true)");
                code.Open($"for index{node} in 0..<{b}.count {{");
                code.Line($"if index{node} == {pool}.count {{ {pool}.append({ElementName(value)}()) }}");
                code.Line($"let item{node} = {pool}[index{node}]");
                code.Line($"item{node}.copy(from: {b}[index{node}])");
                code.Line($"{a}.append(item{node})");
                code.Close();
                break;
            case GesLayoutKind.List:
                code.Line($"{a}.removeAll(keepingCapacity: true)");
                code.Line($"{a}.append(contentsOf: {b})");
                break;
            case GesLayoutKind.Union:
                code.Line($"{a} = {b}");
                code.Line($"switch {a} {{");
                foreach (var alternative in value.Alternatives)
                {
                    code.Open($"case .{Member(alternative.Label)}:");
                    if (alternative.Encoding.Kind == GesLayoutKind.Nothing) code.Line("break");
                    else Copy(code, alternative, Target(alternative.Base), Other(ownerB, alternative.Base), ownerB);
                    code.Close("");
                }

                code.Line("}");
                break;
            default:
                code.Line($"{a} = {b}");
                break;
        }
    }

    private static void Quantize(GesSchemaPlanner.Code code, GesPlanValue value, string target)
    {
        var node = N(value.Node);
        switch (value.Encoding.Kind)
        {
            case GesLayoutKind.Quantized:
                code.Line($"{target} = Self.__value{node}(try Self.__wire{node}({target}, paths, at))");
                break;
            case GesLayoutKind.Point or GesLayoutKind.Vector:
                foreach (var axis in value.Axes) Quantize(code, axis, target + "." + Member(axis.Base));
                break;
            case GesLayoutKind.Box when Quantized(value):
                // A box is immutable: its corners are quantized as Points and make a new box, still in order.
                code.Open("do {");
                foreach (var corner in value.Corners)
                {
                    code.Line($"var {corner.Label}{node} = {target}.{Member(corner.Base)}");
                    Quantize(code, corner, corner.Label + node);
                }

                code.Line($"{target} = GesBox(lower{node}, upper{node})");
                code.Close();
                break;
            case GesLayoutKind.Disc when Quantized(value):
                // A disc is immutable: its centre and radius are quantized and make a new disc.
                code.Open("do {");
                foreach (var part in value.Corners)
                {
                    code.Line($"{Binding(Quantized(part))} {part.Label}{node} = {target}.{Member(part.Base)}");
                    Quantize(code, part, part.Label + node);
                }

                if (value.Corners.Count == 1) code.Line($"let radius{node} = {target}.radius");
                code.Line($"{target} = GesDisc(center: center{node}, radius: radius{node})");
                code.Close();
                break;
            case GesLayoutKind.Schema when value.Packed is not null:
                break;
            case GesLayoutKind.Schema:
                code.Line($"try {target}.__quantize(paths, at + {N(value.Node + 1)}{NextLevel(value)})");
                break;
            case GesLayoutKind.List when Quantized(value):
                var element = value.Element!;
                if (value.Depth > 0) OpenLevels(code, value, target + ".count");
                code.Open($"for index{node} in 0..<{target}.count {{");
                if (value.Item is not null) code.Line($"try {target}[index{node}].__quantize(paths, at + {N(element.Node)}{ItemLevel(element)})");
                else if (element.Encoding.Kind == GesLayoutKind.Schema) code.Line($"try {target}[index{node}].__quantize(paths, at + {N(element.Node + 1)}{NextLevel(element)})");
                else Quantize(code, element, $"{target}[index{node}]");
                code.Close();
                if (value.Depth > 0) code.Close();
                break;
            case GesLayoutKind.Union when value.Alternatives.Exists(Quantized):
                code.Line($"switch {target} {{");
                foreach (var alternative in value.Alternatives)
                {
                    code.Open($"case .{Member(alternative.Label)}:");
                    if (Quantized(alternative)) Quantize(code, alternative, Target(alternative.Base));
                    else code.Line("break");
                    code.Close("");
                }

                code.Line("}");
                break;
        }
    }

    // A Point without axes, the centre of a Disc whose block names only the radius, which transmits nothing.
    private static bool Empty(GesPlanValue value) => value.Encoding.Kind == GesLayoutKind.Point && value.Axes.Count == 0;

    private static bool Quantized(GesPlanValue value) => value.Encoding.Kind switch
    {
        GesLayoutKind.Schema => value.Packed is null,
        GesLayoutKind.Quantized => true,
        GesLayoutKind.Point or GesLayoutKind.Vector => value.Axes.Exists(Quantized),
        GesLayoutKind.Box or GesLayoutKind.Disc => value.Corners.Exists(Quantized),
        GesLayoutKind.Union => value.Alternatives.Exists(Quantized),
        GesLayoutKind.List => value.Item is not null ? Quantized(value.Item.Fields[0].Value) : Quantized(value.Element!),
        _ => false
    };

    private static void Deltas(GesSchemaPlanner.Code code, GesPlanType type, string access, string mutating)
    {
        var key = type.Fields.Count > 0 && type.Fields[0].Key ? type.Fields[0] : null;
        code.Blank();
        code.Line("/// Writes what differs from a baseline, the full record the receiver holds; `maxByteCount` bytes always suffice.");
        code.Line("/// - Returns: The number of bytes written.");
        code.Line("/// - Throws: `GesWireError` when a value does not fit its field, the keys differ, or the buffer is too small.");
        code.Open($"{access}func writeDelta(against baseline: {type.Name}, into buffer: UnsafeMutableRawBufferPointer) throws(GesWireError) -> Int {{");
        code.Line("var writer = GesBitWriter(into: buffer)");
        var body = new List<string>();
        if (key is not null)
        {
            body.Add("let paths = Self.__paths");
            body.Add($"let key = try Self.__wire0({Target(key.Base)}, paths, 0)");
            body.Add($"if try key != Self.__wire0({Other("baseline", key.Base)}, paths, 0) {{ throw GesWireError(code: GesWireError.keyMismatch, path: paths[0]) }}");
            body.Add($"try writer.write(key, width: {key.Value.Encoding.Width.ToString(CultureInfo.InvariantCulture)})");
        }

        body.Add("_ = try __writeDelta(baseline, &writer, Self.__paths, 0, true)");
        body.Add("return try writer.finish()");
        foreach (var line in body) code.Line(line);
        code.Close();
        code.Blank();
        code.Line("/// Counts the bytes that `writeDelta(against:into:)` writes against a baseline, without writing them.");
        code.Line("/// - Throws: `GesWireError` when a value does not fit its field or the keys differ.");
        code.Open($"{access}func deltaEncodedByteCount(against baseline: {type.Name}) throws(GesWireError) -> Int {{");
        code.Line("var writer = GesBitWriter.counting()");
        foreach (var line in body) code.Line(line);
        code.Close();
        code.Blank();
        code.Line("/// Writes what differs from a baseline into a new array of its exact size. A count comes first: it sizes the buffer for");
        code.Line("/// the elements that the writer tries and takes back, so that the largest delta is not allocated.");
        code.Line("/// - Throws: `GesWireError` when a value does not fit its field or the keys differ.");
        code.Open($"{access}func deltaEncoded(against baseline: {type.Name}) throws(GesWireError) -> [UInt8] {{");
        code.Line("var writer = GesBitWriter.counting()");
        for (var index = 0; index < body.Count - 1; index++) code.Line(body[index]);
        code.Line("_ = try writer.finish()");
        code.Line("let buffer = UnsafeMutableRawBufferPointer.allocate(byteCount: Swift.max(writer.requiredByteCount, 1), alignment: 8)");
        code.Line("defer { buffer.deallocate() }");
        code.Line("let count = try writeDelta(against: baseline, into: buffer)");
        code.Line("return Array(buffer[0..<count])");
        code.Close();
        code.Blank();
        code.Line("/// Reads a delta against a baseline into this instance. After an error its values are unspecified.");
        code.Line("/// - Parameters:");
        code.Line("///   - data: The delta; it may continue after it.");
        code.Line("///   - baseline: The baseline, another instance.");
        code.Line("///   - byteLimit: An optional limit on the bytes of this delta.");
        code.Line("/// - Returns: The number of bytes the delta occupies.");
        code.Line("/// - Throws: `GesWireError` when the data is no valid delta for this baseline.");
        code.Line("@discardableResult");
        code.Open($"{access}{mutating}func readDelta(from data: UnsafeRawBufferPointer, against baseline: {type.Name}, byteLimit: Int = .max) throws(GesWireError) -> Int {{");
        if (type.IsClass)
        {
            // Sharing other storage is a programming error, which only debug builds pay to detect.
            code.Line("if self === baseline { throw GesWireError(code: GesWireError.overlappingBuffers, path: \"\") }");
            code.Line(CheckSharing);
            code.Line("self.__mark(true)");
            code.Line("let shared = baseline.__holds()");
            code.Line("self.__mark(false)");
            code.Line("if shared { throw GesWireError(code: GesWireError.overlappingBuffers, path: \"\") }");
            code.Line("#endif");
        }

        code.Line("var reader = GesBitReader(data: data, byteLimit: byteLimit)");
        if (key is not null)
        {
            code.Line("let paths = Self.__paths");
            code.Line("let key = try Self.__read0(&reader, paths, 0)");
            code.Line($"if try key != Self.__wire0({Other("baseline", key.Base)}, paths, 0) {{ throw GesWireError(code: GesWireError.invalidDelta, path: paths[0]) }}");
        }

        code.Line("_ = try __readDelta(baseline, &reader, Self.__paths, 0, true)");
        code.Line("return try reader.finish()");
        code.Close();
        code.Blank();
        code.Line("/// Reads a delta from bytes against a baseline into this instance. After an error its values are unspecified.");
        code.Line("/// - Returns: The number of bytes the delta occupies.");
        code.Line("/// - Throws: `GesWireError` when the bytes are no valid delta for this baseline.");
        code.Line("@discardableResult");
        code.Open($"{access}{mutating}func readDelta(from bytes: [UInt8], against baseline: {type.Name}, byteLimit: Int = .max) throws(GesWireError) -> Int {{");
        code.Line("let buffer = UnsafeMutableRawBufferPointer.allocate(byteCount: Swift.max(bytes.count, 1), alignment: 8)");
        code.Line("defer { buffer.deallocate() }");
        code.Line("buffer.copyBytes(from: bytes)");
        code.Line("return try readDelta(from: UnsafeRawBufferPointer(rebasing: buffer[0..<bytes.count]), against: baseline, byteLimit: byteLimit)");
        code.Close();

        var count = N(type.Fields.Count);
        var counted = key is null ? count : $"top ? {N(type.Fields.Count - 1)} : {count}";
        code.Blank();
        code.Open($"func __writeDelta(_ baseline: {type.Name}, _ writer: inout GesBitWriter, _ paths: [String], _ at: Int, _ top: Bool{LevelParameter(type)}) throws(GesWireError) -> Bool {{");
        if (type.Fields.Count == 0)
        {
            // A schema without fields has no change bits and never changes.
            code.Line("false");
            code.Close();
            code.Blank();
            code.Open($"{mutating}func __readDelta(_ baseline: {type.Name}, _ reader: inout GesBitReader, _ paths: [String], _ at: Int, _ top: Bool{LevelParameter(type)}) throws(GesWireError) -> Bool {{");
            code.Line("false");
            code.Close();
            return;
        }

        code.Line("let first = writer.bitPosition");
        code.Line($"var left = {counted}");
        code.Open("while left > 0 {");
        code.Line("try writer.write(0, width: Swift.min(left, 64))");
        code.Line("left -= 64");
        code.Close();
        code.Line("var changed = false");
        code.Line("var ordinal = 0");
        foreach (var field in type.Fields)
        {
            var node = N(field.Value.Node);
            code.Open(field.Key ? "if !top {" : "do {");
            code.Line($"var set{node} = false");
            DeltaField(code, field, $"set{node}");
            code.Open($"if set{node} {{");
            code.Line("writer.setBit(first + ordinal)");
            code.Line("changed = true");
            code.Close();
            code.Line("ordinal += 1");
            code.Close();
        }

        code.Line("return changed");
        code.Close();
        code.Blank();
        code.Open($"{mutating}func __readDelta(_ baseline: {type.Name}, _ reader: inout GesBitReader, _ paths: [String], _ at: Int, _ top: Bool{LevelParameter(type)}) throws(GesWireError) -> Bool {{");
        code.Line($"let fieldCount = {counted}");
        code.Line("try reader.require(fieldCount, path: at == 0 ? \"\" : paths[at - 1])");
        code.Line("let changeBits = reader.bitPosition");
        code.Line("reader.seek(to: changeBits + fieldCount)");
        code.Line("var changed = false");
        code.Line("var ordinal = 0");
        foreach (var field in type.Fields)
        {
            var node = N(field.Value.Node);
            code.Open(field.Key ? "if !top {" : "do {");
            code.Line($"let payload{node} = reader.bitPosition");
            code.Line("reader.seek(to: changeBits + ordinal)");
            code.Line($"let set{node} = try reader.read(1, path: {Path(field.Value)}) == 1");
            code.Line($"reader.seek(to: payload{node})");
            code.Open($"if set{node} {{");
            ReadDeltaField(code, field);
            code.Line("changed = true");
            code.Close("} else {");
            code.Indent();
            if (field.Optional) code.Line($"self.has{field.Base} = baseline.has{field.Base}");
            Copy(code, field.Value, Target(field.Base), Other("baseline", field.Base), "baseline");
            code.Close("}");
            code.Line("ordinal += 1");
            if (!field.Key)
            {
                code.Close();
                continue;
            }

            // Without a change bit the key equals the baseline's: the caller has compared it, or it identifies a list element.
            code.Close("} else {");
            code.Line($"    {Target(field.Base)} = Self.__value0(try Self.__wire0({Other("baseline", field.Base)}, paths, at))");
            code.Line("}");
        }

        code.Line("return changed");
        code.Close();
    }

    private static void DeltaField(GesSchemaPlanner.Code code, GesPlanField field, string set)
    {
        if (!field.Optional)
        {
            DeltaPayload(code, field.Value, Target(field.Base), Other("baseline", field.Base), set);
            return;
        }

        var node = N(field.Value.Node);
        code.Open($"if self.has{field.Base} != baseline.has{field.Base} {{");
        code.Line($"try writer.write(self.has{field.Base} ? 1 : 0, width: 1)");
        code.Open($"if self.has{field.Base} {{");
        Write(code, field.Value, Target(field.Base));
        code.Close();
        code.Line($"{set} = true");
        code.Close($"}} else if self.has{field.Base} {{");
        code.Indent();
        code.Line($"let presence{node} = writer.bitPosition");
        code.Line("try writer.write(1, width: 1)");
        DeltaPayload(code, field.Value, Target(field.Base), Other("baseline", field.Base), set);
        code.Line($"if !{set} {{ writer.rewind(to: presence{node}) }}");
        code.Close("}");
    }

    private static void DeltaPayload(GesSchemaPlanner.Code code, GesPlanValue value, string a, string b, string set)
    {
        var encoding = value.Encoding;
        var node = N(value.Node);
        var width = encoding.Width.ToString(CultureInfo.InvariantCulture);
        switch (encoding.Kind)
        {
            case GesLayoutKind.Boolean:
                code.Line($"if {a} != {b} {{ {set} = true }}");
                break;
            case GesLayoutKind.Integer or GesLayoutKind.Quantized or GesLayoutKind.Float or GesLayoutKind.Tag or GesLayoutKind.Flags:
                code.Open("do {");
                code.Line($"let value{node} = try Self.__wire{node}({a}, paths, at)");
                code.Line($"let held{node} = try Self.__wire{node}({b}, paths, at)");
                code.Open($"if value{node} != held{node} {{");
                code.Line(encoding.DeltaRange == 0
                    ? $"try writer.write(value{node}, width: {width})"
                    : $"try GesWireRules.writeDeltaRange(&writer, index: value{node}, held: held{node}, maxIndex: {Unsigned(encoding.MaxIndex)}, wrapping: {(encoding.Wrapping ? "true" : "false")}, deltaRange: {Unsigned(encoding.DeltaRange)}, deltaWidth: {encoding.DeltaWidth.ToString(CultureInfo.InvariantCulture)}, width: {width})");
                code.Line($"{set} = true");
                code.Close();
                code.Close();
                break;
            case GesLayoutKind.List when encoding.Keyed:
                code.Line($"if try __writeKeyed{node}(baseline, &writer, paths, at{Level(value)}) {{ {set} = true }}");
                break;
            case GesLayoutKind.Text or GesLayoutKind.Bytes or GesLayoutKind.List:
                code.Open($"if !(try __same{node}(baseline, paths, at{Level(value)})) {{");
                Write(code, value, a);
                code.Line($"{set} = true");
                code.Close();
                break;
            case GesLayoutKind.Point or GesLayoutKind.Vector:
                code.Open("do {");
                code.Line($"let first{node} = writer.bitPosition");
                code.Line($"try writer.write(0, width: {N(value.Axes.Count)})");
                for (var index = 0; index < value.Axes.Count; index++)
                {
                    var axis = value.Axes[index];
                    code.Open("do {");
                    code.Line($"var set{N(axis.Node)} = false");
                    DeltaPayload(code, axis, a + "." + Member(axis.Base), b + "." + Member(axis.Base), $"set{N(axis.Node)}");
                    code.Open($"if set{N(axis.Node)} {{");
                    code.Line($"writer.setBit(first{node} + {N(index)})");
                    code.Line($"{set} = true");
                    code.Close();
                    code.Close();
                }

                foreach (var axis in GesSchemaPlanner.Omitted(value)) code.Line($"if {a}.{Member(axis)} != 0 {{ {Throw("typeMismatch", value)} }}");
                code.Line($"if !{set} {{ writer.rewind(to: first{node}) }}");
                code.Close();
                break;
            case GesLayoutKind.Box or GesLayoutKind.Disc:
                // A nested delta of the corners, each one of a Point, or of a disc's centre and radius.
                code.Open("do {");
                code.Line($"let first{node} = writer.bitPosition");
                code.Line($"try writer.write(0, width: {N(value.Corners.Count)})");
                for (var index = 0; index < value.Corners.Count; index++)
                {
                    var corner = value.Corners[index];
                    if (Empty(corner))
                    {
                        // A centre without axes transmits nothing: its change bit stays clear, and it must lie at the origin.
                        foreach (var axis in GesSchemaPlanner.Omitted(corner)) code.Line($"if {a}.{Member(corner.Base)}.{Member(axis)} != 0 {{ {Throw("typeMismatch", corner)} }}");
                        continue;
                    }

                    code.Open("do {");
                    code.Line($"var set{N(corner.Node)} = false");
                    DeltaPayload(code, corner, a + "." + Member(corner.Base), b + "." + Member(corner.Base), $"set{N(corner.Node)}");
                    code.Open($"if set{N(corner.Node)} {{");
                    code.Line($"writer.setBit(first{node} + {N(index)})");
                    code.Line($"{set} = true");
                    code.Close();
                    code.Close();
                }

                if (value.Encoding.Kind == GesLayoutKind.Disc && value.Corners.Count == 1) code.Line($"if {a}.radius != 0 {{ {Throw("typeMismatch", value)} }}");
                code.Line($"if !{set} {{ writer.rewind(to: first{node}) }}");
                code.Close();
                break;
            case GesLayoutKind.Schema when value.Packed is not null:
                // A packed record changes as a whole: its word follows the change bit.
                code.Open($"if {a}.rawValue != {b}.rawValue {{");
                PackedWrite(code, value, a);
                code.Line($"{set} = true");
                code.Close();
                break;
            case GesLayoutKind.Schema:
                code.Open("do {");
                code.Line($"let start{node} = writer.bitPosition");
                code.Open($"if try {a}.__writeDelta({b}, &writer, paths, at + {N(value.Node + 1)}, false{NextLevel(value)}) {{");
                code.Line($"{set} = true");
                code.Close("} else {");
                code.Line($"    writer.rewind(to: start{node})");
                code.Line("}");
                code.Close();
                break;
            case GesLayoutKind.Union:
                code.Open($"if {a} != {b} {{");
                Write(code, value, a);
                code.Line($"{set} = true");
                code.Close("} else {");
                code.Indent();
                code.Line($"let entry{node} = writer.bitPosition");
                code.Line($"try writer.write(UInt64({a}.rawValue), width: {width})");
                code.Line($"switch {a} {{");
                foreach (var alternative in value.Alternatives)
                {
                    code.Open($"case .{Member(alternative.Label)}:");
                    if (alternative.Encoding.Kind == GesLayoutKind.Nothing) code.Line("break");
                    else DeltaPayload(code, alternative, Target(alternative.Base), Other("baseline", alternative.Base), set);
                    code.Close("");
                }

                code.Line("}");
                code.Line($"if !{set} {{ writer.rewind(to: entry{node}) }}");
                code.Close("}");
                break;
        }
    }

    private static void ReadDeltaField(GesSchemaPlanner.Code code, GesPlanField field)
    {
        if (!field.Optional)
        {
            ReadDeltaPayload(code, field.Value, Target(field.Base), Other("baseline", field.Base));
            return;
        }

        var node = N(field.Value.Node);
        code.Line($"let present{node} = try reader.read(1, path: {Path(field.Value)}) == 1");
        code.Line($"if !present{node} && !baseline.has{field.Base} {{ {Throw("invalidDelta", field.Value)} }}");
        code.Line($"self.has{field.Base} = present{node}");
        code.Open($"if present{node} && baseline.has{field.Base} {{");
        ReadDeltaPayload(code, field.Value, Target(field.Base), Other("baseline", field.Base));
        code.Close($"}} else if present{node} {{");
        code.Indent();
        Read(code, field.Value, Target(field.Base));
        if (Plain(field.Value))
        {
            code.Close("} else {");
            code.Line($"    {Target(field.Base)} = {Zero(field.Value)}");
            code.Line("}");
        }
        else code.Close("}");
    }

    private static void ReadDeltaPayload(GesSchemaPlanner.Code code, GesPlanValue value, string a, string b)
    {
        var encoding = value.Encoding;
        var node = N(value.Node);
        var width = encoding.Width.ToString(CultureInfo.InvariantCulture);
        switch (encoding.Kind)
        {
            case GesLayoutKind.Boolean:
                code.Line($"{a} = !{b}");
                break;
            case GesLayoutKind.Integer or GesLayoutKind.Quantized or GesLayoutKind.Float or GesLayoutKind.Tag or GesLayoutKind.Flags when encoding.DeltaRange == 0:
                code.Open("do {");
                code.Line($"let held{node} = try Self.__wire{node}({b}, paths, at)");
                code.Line($"let value{node} = try Self.__read{node}(&reader, paths, at)");
                code.Line($"if value{node} == held{node} {{ {Throw("invalidDelta", value)} }}");
                code.Line($"{a} = Self.__value{node}(value{node})");
                code.Close();
                break;
            case GesLayoutKind.Integer or GesLayoutKind.Quantized:
                code.Line($"{a} = Self.__value{node}(");
                code.Line($"    try GesWireRules.readDeltaRange(");
                code.Line($"        &reader, held: try Self.__wire{node}({b}, paths, at), maxIndex: {Unsigned(encoding.MaxIndex)}, wrapping: {(encoding.Wrapping ? "true" : "false")}, deltaRange: {Unsigned(encoding.DeltaRange)},");
                code.Line($"        deltaWidth: {encoding.DeltaWidth.ToString(CultureInfo.InvariantCulture)}, width: {width}, path: {Path(value)}");
                code.Line("    )");
                code.Line(")");
                break;
            case GesLayoutKind.List when encoding.Keyed:
                code.Line($"try __readKeyed{node}(baseline, &reader, paths, at{Level(value)})");
                break;
            case GesLayoutKind.Text or GesLayoutKind.Bytes or GesLayoutKind.List:
                Read(code, value, a);
                code.Line($"if try __same{node}(baseline, paths, at{Level(value)}) {{ {Throw("invalidDelta", value)} }}");
                break;
            case GesLayoutKind.Point or GesLayoutKind.Vector:
                code.Open("do {");
                code.Line($"try reader.require({N(value.Axes.Count)}, path: {Path(value)})");
                code.Line($"let bits{node} = reader.bitPosition");
                code.Line($"reader.seek(to: bits{node} + {N(value.Axes.Count)})");
                code.Line($"var changed{node} = false");
                for (var index = 0; index < value.Axes.Count; index++)
                {
                    var axis = value.Axes[index];
                    var axisNode = N(axis.Node);
                    code.Open("do {");
                    code.Line($"let payload{axisNode} = reader.bitPosition");
                    code.Line($"reader.seek(to: bits{node} + {N(index)})");
                    code.Line($"let set{axisNode} = try reader.read(1, path: {Path(axis)}) == 1");
                    code.Line($"reader.seek(to: payload{axisNode})");
                    code.Open($"if set{axisNode} {{");
                    ReadDeltaPayload(code, axis, a + "." + Member(axis.Base), b + "." + Member(axis.Base));
                    code.Line($"changed{node} = true");
                    code.Close("} else {");
                    code.Line($"    {a}.{Member(axis.Base)} = {b}.{Member(axis.Base)}");
                    code.Line("}");
                    code.Close();
                }

                foreach (var axis in GesSchemaPlanner.Omitted(value)) code.Line($"{a}.{Member(axis)} = 0");
                code.Line($"if !changed{node} {{ {Throw("invalidDelta", value)} }}");
                code.Close();
                break;
            case GesLayoutKind.Box or GesLayoutKind.Disc:
                // The corners start as the baseline's; the box orders what the delta changes. A disc takes its centre and radius
                // likewise.
                code.Open("do {");
                code.Line($"try reader.require({N(value.Corners.Count)}, path: {Path(value)})");
                code.Line($"let bits{node} = reader.bitPosition");
                code.Line($"reader.seek(to: bits{node} + {N(value.Corners.Count)})");
                code.Line($"var changed{node} = false");
                for (var index = 0; index < value.Corners.Count; index++)
                {
                    var corner = value.Corners[index];
                    var cornerNode = N(corner.Node);
                    code.Line($"{Binding(!Empty(corner))} {corner.Label}{node} = {b}.{Member(corner.Base)}");
                    if (Empty(corner))
                    {
                        // A centre without axes never changes, so its change bit must be clear in the canonical delta.
                        code.Open("do {");
                        code.Line($"let payload{cornerNode} = reader.bitPosition");
                        code.Line($"reader.seek(to: bits{node} + {N(index)})");
                        code.Line($"if try reader.read(1, path: {Path(corner)}) == 1 {{ {Throw("invalidDelta", corner)} }}");
                        code.Line($"reader.seek(to: payload{cornerNode})");
                        code.Close();
                        continue;
                    }

                    code.Open("do {");
                    code.Line($"let payload{cornerNode} = reader.bitPosition");
                    code.Line($"reader.seek(to: bits{node} + {N(index)})");
                    code.Line($"let set{cornerNode} = try reader.read(1, path: {Path(corner)}) == 1");
                    code.Line($"reader.seek(to: payload{cornerNode})");
                    code.Open($"if set{cornerNode} {{");
                    ReadDeltaPayload(code, corner, corner.Label + node, b + "." + Member(corner.Base));
                    code.Line($"changed{node} = true");
                    code.Close();
                    code.Close();
                }

                code.Line($"if !changed{node} {{ {Throw("invalidDelta", value)} }}");
                if (value.Encoding.Kind == GesLayoutKind.Disc && value.Corners.Count == 1) code.Line($"let radius{node} = 0.0");
                code.Line(value.Encoding.Kind == GesLayoutKind.Disc ? $"{a} = GesDisc(center: center{node}, radius: radius{node})" : $"{a} = GesBox(lower{node}, upper{node})");
                code.Close();
                break;
            case GesLayoutKind.Schema when value.Packed is not null:
                PackedRead(code, value, a);
                code.Line($"if {a}.rawValue == {b}.rawValue {{ {Throw("invalidDelta", value)} }}");
                break;
            case GesLayoutKind.Schema:
                code.Line($"if !(try {a}.__readDelta({b}, &reader, paths, at + {N(value.Node + 1)}, false{NextLevel(value)})) {{ {Throw("invalidDelta", value)} }}");
                break;
            case GesLayoutKind.Union:
                code.Open("do {");
                code.Line($"let choice{node} = try reader.read({width}, path: {Path(value)})");
                if ((ulong)value.Alternatives.Count - 1 < Full(encoding.Width)) code.Line($"if choice{node} > {Unsigned((ulong)value.Alternatives.Count - 1)} {{ {Throw("invalidIndex", value)} }}");
                code.Line($"let same{node} = choice{node} == UInt64({b}.rawValue)");
                code.Line($"{a} = {NestedName(value)}(rawValue: {Underlying(value.Alternatives.Count)}(choice{node}))!");
                code.Line($"switch {a} {{");
                foreach (var alternative in value.Alternatives)
                {
                    code.Open($"case .{Member(alternative.Label)}:");
                    if (alternative.Encoding.Kind == GesLayoutKind.Nothing) code.Line($"if same{node} {{ {Throw("invalidDelta", alternative)} }}");
                    else
                    {
                        code.Open($"if same{node} {{");
                        ReadDeltaPayload(code, alternative, Target(alternative.Base), Other("baseline", alternative.Base));
                        code.Close("} else {");
                        code.Indent();
                        Read(code, alternative, Target(alternative.Base));
                        code.Close("}");
                    }

                    code.Close("");
                }

                code.Line("}");
                code.Close();
                break;
        }
    }

    private static void SameHelpers(GesSchemaPlanner.Code code, GesPlanType type)
    {
        foreach (var field in type.Fields) SameHelper(code, field.Value, type.Name);
        foreach (var field in type.Fields) KeyedHelpers(code, field.Value, type.Name);
        if (type.Fields.Exists(field => GesSchemaPlanner.HasKeyed(field.Value))) KeyedStatics(code);
    }

    private static void SameHelper(GesSchemaPlanner.Code code, GesPlanValue value, string type)
    {
        if (value.Encoding.Kind is GesLayoutKind.Text or GesLayoutKind.Bytes or GesLayoutKind.List)
        {
            code.Blank();
            code.Open($"private func __same{N(value.Node)}(_ baseline: {type}, _ paths: [String], _ at: Int{(value.Depth > 0 ? ", _ level: Int" : "")}) throws(GesWireError) -> Bool {{");
            Same(code, value, Target(value.Base), Other("baseline", value.Base), "self", "baseline");
            code.Line("return true");
            code.Close();
        }

        foreach (var alternative in value.Alternatives) SameHelper(code, alternative, type);
    }
}
