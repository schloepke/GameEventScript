// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

// Wire values, comparison, copying, quantization and deltas of generated C# types. Comparison and deltas use wire values:
// indices and stored bits, never raw doubles. Local names carry the node of their value, since C# forbids shadowing.
extension GesCSharpEmitter {
    // The level argument that a recursive List's own helpers take.
    static func level(_ value: GesPlanValue) -> String { value.depth > 0 ? ", level" : "" }

    static func isScalar(_ value: GesPlanValue) -> Bool {
        switch value.encoding.kind {
        case .boolean, .integer, .quantized, .float, .tag, .flags: true
        default: false
        }
    }

    // Every value of a type with a wire value of its own, for the helpers that convert it.
    static func scalars(_ value: GesPlanValue) -> [GesPlanValue] {
        var result: [GesPlanValue] = isScalar(value) ? [value] : []
        result += value.axes
        for corner in value.corners {
            if isScalar(corner) { result.append(corner) }
            result += corner.axes
        }
        for alternative in value.alternatives { result += scalars(alternative) }
        if value.item == nil, let element = value.element { result += scalars(element) }
        return result
    }

    // The helpers, comparison, copy, quantization and deltas of a record type.
    static func extras(_ code: inout Code, _ type: GesPlanType, _ deltas: Bool, _ element: Bool) {
        let selfModifier = type.isClass ? "" : "readonly "
        let parameter = type.isClass ? type.name : "in " + type.name
        for field in type.fields {
            for value in scalars(field.value) { helpers(&code, value) }
        }
        code.blank()
        code.line("internal \(selfModifier)bool __Same(\(parameter) other, string[] paths, int at\(levelParameter(type)))")
        code.open("{")
        for field in type.fields {
            if field.optional {
                code.line("if (Has\(field.base) != other.Has\(field.base)) return false;")
                code.line("if (Has\(field.base))")
                code.open("{")
                same(&code, field.value, field.base, "other." + field.base)
                code.close()
            } else {
                same(&code, field.value, field.base, "other." + field.base)
            }
        }
        for tag in type.tags { code.line("if (\(tag.base) != other.\(tag.base)) return false;") }
        code.line("return true;")
        code.close()
        if type.isClass {
            code.blank()
            code.line("/// <summary>Copies the values of another record into the storage of this one, without allocating once it has grown.</summary>")
            code.line("/// <param name=\"other\">The record to copy.</param>")
            code.line("public void CopyFrom(\(type.name) other)")
            code.open("{")
            code.line("if (ReferenceEquals(this, other)) return;")
            for field in type.fields {
                if field.optional { code.line("Has\(field.base) = other.Has\(field.base);") }
                copy(&code, field.value, field.base, "other." + field.base)
            }
            for tag in type.tags { code.line("\(tag.base) = other.\(tag.base);") }
            code.close()
        }
        code.blank()
        code.line("internal void __Quantize(string[] paths, int at\(levelParameter(type)))")
        code.open("{")
        for field in type.fields {
            if field.optional {
                code.line("if (Has\(field.base))")
                code.open("{")
                quantize(&code, field.value, field.base)
                code.close()
            } else {
                quantize(&code, field.value, field.base)
            }
        }
        code.close()
        if type.isItem { return }
        code.blank()
        code.line("/// <summary>Gets whether another \(kindName(type)) has the same wire values, so that it writes the same bytes.</summary>")
        code.line("/// <param name=\"other\">The other record.</param>")
        code.line("/// <returns>Whether the wire values are equal.</returns>")
        code.line("/// <exception cref=\"GesWireException\">A compared value has no wire value, such as a number outside its range.</exception>")
        code.line("public \(selfModifier)bool WireEquals(\(parameter) other) => __Same(other, __paths, 0);")
        code.blank()
        code.line("/// <summary>Replaces every quantized value by the value its index decodes to, which a receiver reads.</summary>")
        code.line("/// <exception cref=\"GesWireException\">A quantized value lies outside its range.</exception>")
        code.line("public void Quantize() => __Quantize(__paths, 0);")
        if element { keyOrder(&code, type, selfModifier) }
        if deltas { self.deltas(&code, type, selfModifier, parameter) }
    }

    static func kindName(_ type: GesPlanType) -> String { type.kind == .message ? "message" : "schema" }

    private static func helpers(_ code: inout Code, _ value: GesPlanValue) {
        let encoding = value.encoding
        let node = String(value.node)
        let type = typeName(value)
        let width = String(encoding.width)
        code.blank()
        code.line("private static ulong __Wire\(node)(\(type) value, string[] paths, int at)")
        code.open("{")
        switch encoding.kind {
        case .boolean:
            code.line("return value ? 1UL : 0UL;")
        case .integer where !encoding.range:
            code.line("return unchecked((ulong)value);")
        case .integer where encoding.wrapping:
            code.line("return GesWireRules.WrappedIndex(value, \(long(encoding.lower)), \(unsigned(encoding.maxIndex &+ 1)));")
        case .integer:
            code.line("if (value < \(long(encoding.lower)) || unchecked((ulong)value - \(unsigned(UInt64(bitPattern: encoding.lower)))) > \(unsigned(encoding.maxIndex))) \(fail("OutOfRange", value))")
            code.line("return unchecked((ulong)value - \(unsigned(UInt64(bitPattern: encoding.lower))));")
        case .quantized where encoding.wrapping:
            code.line(
                "return GesWireRules.WrappedQuantizedIndex(value, \(double(encoding.tenPower)), \(double(encoding.numerator)), \(long(encoding.lower)), \(unsigned(encoding.maxIndex &+ 1)), \(path(value)));"
            )
        case .quantized:
            code.line(
                "return GesWireRules.QuantizedIndex(value, \(bits(encoding.lowerValue)), \(bits(encoding.upperValue)), \(double(encoding.tenPower)), \(double(encoding.numerator)), \(long(encoding.lower)), \(path(value)));"
            )
        case .float:
            code.line("if (double.IsNaN(value)) \(fail("NotRepresentable", value))")
            if encoding.range { code.line("if (value < \(bits(encoding.lowerValue)) || value > \(bits(encoding.upperValue))) \(fail("OutOfRange", value))") }
            code.line("return unchecked((ulong)global::System.BitConverter.DoubleToInt64Bits(value == 0 ? 0d : value));")
        case .flags:
            if encoding.width < 64 { code.line("if ((ulong)value > \(unsigned(full(encoding.width)))) \(fail("UnknownTag", value))") }
            code.line("return (ulong)value;")
        default:
            code.line("if ((ulong)value > \(unsigned(UInt64(encoding.tags.count) - 1))) \(fail("UnknownTag", value))")
            code.line("return (ulong)value;")
        }
        code.close()
        code.blank()
        code.line("private static ulong __Read\(node)(ref GesBitReader reader, string[] paths, int at)")
        code.open("{")
        code.line("var wire = reader.Read(\(width), \(path(value)));")
        if encoding.kind == .float {
            code.line("var value = global::System.BitConverter.Int64BitsToDouble(unchecked((long)wire));")
            code.line("if (double.IsNaN(value) || wire == 0x8000000000000000UL\(encoding.range ? " || double.IsInfinity(value)" : "")) \(fail("InvalidNumber", value))")
            if encoding.range { code.line("if (value < \(bits(encoding.lowerValue)) || value > \(bits(encoding.upperValue))) \(fail("OutOfRange", value))") }
        } else if encoding.kind != .boolean {
            let largest =
                switch encoding.kind {
                case .tag: UInt64(encoding.tags.count) - 1
                case .flags: full(encoding.width)
                default: encoding.maxIndex
                }
            if largest < full(encoding.width) { code.line("if (wire > \(unsigned(largest))) \(fail("InvalidIndex", value))") }
        }
        code.line("return wire;")
        code.close()
        code.blank()
        switch encoding.kind {
        case .boolean: code.line("private static bool __Value\(node)(ulong wire) => wire != 0;")
        case .integer where !encoding.range: code.line("private static long __Value\(node)(ulong wire) => unchecked((long)wire);")
        case .integer: code.line("private static long __Value\(node)(ulong wire) => unchecked((long)(\(unsigned(UInt64(bitPattern: encoding.lower))) + wire));")
        case .quantized:
            code.line("private static double __Value\(node)(ulong wire) => GesWireRules.QuantizedValue(wire, \(long(encoding.lower)), \(long(encoding.numerator)), \(double(encoding.tenPower)));")
        case .float: code.line("private static double __Value\(node)(ulong wire) => global::System.BitConverter.Int64BitsToDouble(unchecked((long)wire));")
        default: code.line("private static \(type) __Value\(node)(ulong wire) => (\(type))wire;")
        }
    }

    // The storage of a Text: `prefix` is empty for this record and "other." or "baseline." for another.
    private static func text(_ value: GesPlanValue, _ prefix: String) -> String { "new ReadOnlySpan<byte>(\(prefix)__\(value.base), 0, \(prefix)__\(value.base)Length)" }

    private static func prefix(_ target: String) -> String {
        guard let dot = target.lastIndex(of: ".") else { return "" }
        return String(target[...dot])
    }

    // Returns false from the enclosing method when the wire values of a and b differ.
    private static func same(_ code: inout Code, _ value: GesPlanValue, _ a: String, _ b: String) {
        let node = String(value.node)
        switch value.encoding.kind {
        case .boolean:
            code.line("if (\(a) != \(b)) return false;")
        case .integer, .quantized, .float, .tag, .flags:
            code.line("if (__Wire\(node)(\(a), paths, at) != __Wire\(node)(\(b), paths, at)) return false;")
        case .text, .bytes:
            code.line("if (!\(text(value, prefix(a))).SequenceEqual(\(text(value, prefix(b))))) return false;")
        case .point, .vector:
            for axis in value.axes { same(&code, axis, a + "." + axis.base, b + "." + axis.base) }
        case .box, .disc:
            for corner in value.corners { same(&code, corner, a + "." + corner.base, b + "." + corner.base) }
        case .schema where value.packed != nil:
            code.line("if (\(a).RawValue != \(b).RawValue) return false;")
        case .schema:
            code.line("if (!\(a).__Same(\(b), paths, at + \(value.node + 1)\(nextLevel(value)))) return false;")
        case .list:
            if value.depth > 0 { openLevels(&code, value, "\(a).Count + \(b).Count") }
            code.line("if (\(a).Count != \(b).Count) return false;")
            code.line("for (var index\(node) = 0; index\(node) < \(a).Count; index\(node)++)")
            code.open("{")
            let element = value.element!
            if value.item != nil {
                code.line("if (!\(a)[index\(node)].__Same(\(b)[index\(node)], paths, at + \(element.node)\(itemLevel(element)))) return false;")
            } else {
                same(&code, element, "\(a)[index\(node)]", "\(b)[index\(node)]")
            }
            code.close()
            if value.depth > 0 { code.close() }
        case .union:
            code.line("if (\(a) != \(b)) return false;")
            code.line("switch (\(a))")
            code.open("{")
            for alternative in value.alternatives where alternative.encoding.kind != .nothing {
                code.line("case \(nestedName(value)).\(alternative.label):")
                code.open("{")
                same(&code, alternative, prefix(a) + alternative.base, prefix(b) + alternative.base)
                code.line("break;")
                code.close()
            }
            code.close()
        case .nothing:
            break
        }
    }

    // The storage that a read into this record writes: the record, the class records and lists it holds and every pooled element.
    // Debug builds mark it while a delta reads, so that a baseline holding any of it is rejected before the read overwrites what
    // it holds; release builds generate none of it.
    static func sharing(_ code: inout Code, _ type: GesPlanType) {
        code.blank()
        code.line(checkSharing)
        if type.kind == .schema && !type.isItem { code.line("[global::System.ThreadStatic] private static HashSet<object>? __SharingLists;") }
        code.line("internal bool __Busy;")
        code.blank()
        if !type.fields.contains(where: { GesSchemaPlanner.holdsStorage($0.value, lists: true) }) {
            code.line("internal void __Mark(bool busy, HashSet<object>? lists) => __Busy = busy;")
            code.blank()
            code.line("internal bool __Holds(HashSet<object> lists) => __Busy;")
        } else {
            code.line("internal void __Mark(bool busy, HashSet<object>? lists)")
            code.open("{")
            code.line("__Busy = busy;")
            for field in type.fields { mark(&code, field.value, field.base) }
            code.close()
            code.blank()
            code.line("internal bool __Holds(HashSet<object> lists)")
            code.open("{")
            code.line("if (__Busy) return true;")
            for field in type.fields { holds(&code, field.value, field.base) }
            code.line("return false;")
            code.close()
        }
        code.line("#endif")
    }

    // The builds that check shared baselines: debug builds, Unity's editor and development players, and any build that asks.
    private static let checkSharing = "#if DEBUG || DEVELOPMENT_BUILD || UNITY_EDITOR || GES_CHECK_SHARING"

    private static func mark(_ code: inout Code, _ value: GesPlanValue, _ member: String) {
        let node = String(value.node)
        switch value.encoding.kind {
        case .schema where value.isClass:
            code.line("\(member).__Mark(busy, lists);")
        case .list:
            code.line("lists?.Add(\(member));")
            if value.element!.isClass {
                code.line("for (var index\(node) = 0; index\(node) < __\(value.base)Pool.Count; index\(node)++) __\(value.base)Pool[index\(node)].__Mark(busy, lists);")
            }
        case .union:
            for alternative in value.alternatives { mark(&code, alternative, prefix(member) + alternative.base) }
        default:
            break
        }
    }

    private static func holds(_ code: inout Code, _ value: GesPlanValue, _ member: String) {
        let node = String(value.node)
        switch value.encoding.kind {
        case .schema where value.isClass:
            code.line("if (\(member).__Holds(lists)) return true;")
        case .list:
            code.line("if (lists.Contains(\(member))) return true;")
            if !value.element!.isClass { break }
            code.line("for (var index\(node) = 0; index\(node) < \(member).Count; index\(node)++)")
            code.line("    if (\(member)[index\(node)].__Holds(lists)) return true;")
        case .union:
            for alternative in value.alternatives { holds(&code, alternative, prefix(member) + alternative.base) }
        default:
            break
        }
    }

    // Copies b into a without allocating once a's storage has grown; a list that b shares with a already holds the values.
    private static func copy(_ code: inout Code, _ value: GesPlanValue, _ a: String, _ b: String) {
        let node = String(value.node)
        switch value.encoding.kind {
        case .text, .bytes:
            code.line("Set\(value.base)\(value.encoding.kind == .text ? "Utf8" : "")(\(text(value, prefix(b))));")
        case .schema where value.isClass:
            code.line("\(a).CopyFrom(\(b));")
        case .list where value.element!.isClass:
            // The same list already holds the values; clearing it first would lose them.
            code.line("if (!ReferenceEquals(\(a), \(b)))")
            code.open("{")
            code.line("\(a).Clear();")
            code.line("for (var index\(node) = 0; index\(node) < \(b).Count; index\(node)++)")
            code.open("{")
            code.line("if (index\(node) == __\(value.base)Pool.Count) __\(value.base)Pool.Add(new \(elementName(value))());")
            code.line("var item\(node) = __\(value.base)Pool[index\(node)];")
            code.line("item\(node).CopyFrom(\(b)[index\(node)]);")
            code.line("\(a).Add(item\(node));")
            code.close()
            code.close()
        case .list:
            code.line("if (!ReferenceEquals(\(a), \(b)))")
            code.open("{")
            code.line("\(a).Clear();")
            code.line("\(a).AddRange(\(b));")
            code.close()
        case .union:
            code.line("\(a) = \(b);")
            code.line("switch (\(a))")
            code.open("{")
            for alternative in value.alternatives where alternative.encoding.kind != .nothing {
                code.line("case \(nestedName(value)).\(alternative.label):")
                code.open("{")
                copy(&code, alternative, prefix(a) + alternative.base, prefix(b) + alternative.base)
                code.line("break;")
                code.close()
            }
            code.close()
        default:
            code.line("\(a) = \(b);")
        }
    }

    private static func quantize(_ code: inout Code, _ value: GesPlanValue, _ target: String) {
        let node = String(value.node)
        switch value.encoding.kind {
        case .quantized:
            code.line("\(target) = __Value\(node)(__Wire\(node)(\(target), paths, at));")
        case .point, .vector:
            for axis in value.axes { quantize(&code, axis, target + "." + axis.base) }
        case .box where quantized(value):
            // A box is immutable: its corners are quantized as Points and make a new box, still in order.
            code.open("{")
            for corner in value.corners {
                code.line("var \(corner.label)\(node) = \(target).\(corner.base);")
                quantize(&code, corner, corner.label + node)
            }
            code.line("\(target) = new GesBox(lower\(node), upper\(node));")
            code.close()
        case .disc where quantized(value):
            // A disc is immutable: its centre and radius are quantized and make a new disc.
            code.open("{")
            code.line("var center\(node) = \(target).Center;")
            code.line("var radius\(node) = \(target).Radius;")
            for part in value.corners { quantize(&code, part, part.label + node) }
            code.line("\(target) = new GesDisc(center\(node), radius\(node));")
            code.close()
        case .schema where value.packed != nil:
            break
        case .schema:
            code.line("\(target).__Quantize(paths, at + \(value.node + 1)\(nextLevel(value)));")
        case .list where quantized(value):
            let element = value.element!
            if value.depth > 0 { openLevels(&code, value, target + ".Count") }
            code.line("for (var index\(node) = 0; index\(node) < \(target).Count; index\(node)++)")
            code.open("{")
            if element.isClass {
                code.line("\(target)[index\(node)].__Quantize(paths, at + \(value.item != nil ? element.node : element.node + 1)\(nextLevel(element))\(itemLevel(element)));")
            } else {
                code.line("var item\(node) = \(target)[index\(node)];")
                if value.item != nil { code.line("item\(node).__Quantize(paths, at + \(element.node)\(itemLevel(element)));") } else { quantize(&code, element, "item\(node)") }
                code.line("\(target)[index\(node)] = item\(node);")
            }
            code.close()
            if value.depth > 0 { code.close() }
        case .union where value.alternatives.contains(where: quantized):
            code.line("switch (\(target))")
            code.open("{")
            for alternative in value.alternatives where quantized(alternative) {
                code.line("case \(nestedName(value)).\(alternative.label):")
                code.open("{")
                quantize(&code, alternative, alternative.base)
                code.line("break;")
                code.close()
            }
            code.close()
        default:
            break
        }
    }

    // A Point without axes, the centre of a Disc whose block names only the radius, which transmits nothing.
    private static func empty(_ value: GesPlanValue) -> Bool { value.encoding.kind == .point && value.axes.isEmpty }

    // Whether a value holds a quantized number that Quantize changes; schemas always forward, as theirs may.
    private static func quantized(_ value: GesPlanValue) -> Bool {
        switch value.encoding.kind {
        case .schema: value.packed == nil
        case .quantized: true
        case .point, .vector: value.axes.contains(where: quantized)
        case .box, .disc: value.corners.contains(where: quantized)
        case .union: value.alternatives.contains(where: quantized)
        case .list: value.item.map { quantized($0.fields[0].value) } ?? quantized(value.element!)
        default: false
        }
    }

    private static func deltas(_ code: inout Code, _ type: GesPlanType, _ selfModifier: String, _ parameter: String) {
        let key = type.fields.first.flatMap { $0.key ? $0 : nil }
        code.blank()
        code.line("/// <summary>Writes what differs from a baseline, the full record the receiver holds; <see cref=\"MaxByteCount\"/> bytes always suffice.</summary>")
        code.line("/// <param name=\"baseline\">The baseline, another instance.</param>")
        code.line("/// <param name=\"buffer\">The buffer.</param>")
        code.line("/// <returns>The number of bytes written.</returns>")
        code.line("/// <exception cref=\"GesWireException\">A value does not fit its field, the keys differ, or the buffer is too small.</exception>")
        code.line("public \(selfModifier)int WriteDelta(\(parameter) baseline, Span<byte> buffer)")
        code.open("{")
        code.line("var writer = new GesBitWriter(buffer);")
        var body: [String] = []
        if let key {
            body.append("var paths = __paths;")
            body.append("const int at = 0;")
            body.append("var key = __Wire0(\(key.base), paths, at);")
            body.append("if (key != __Wire0(baseline.\(key.base), paths, at)) throw new GesWireException(GesWireException.KeyMismatch, paths[0]);")
            body.append("writer.Write(key, \(key.value.encoding.width));")
        }
        body.append("__WriteDelta(baseline, ref writer, __paths, 0, true);")
        body.append("return writer.Finish();")
        for line in body { code.line(line) }
        code.close()
        code.blank()
        code.line("/// <summary>Counts the bytes that <see cref=\"WriteDelta\"/> writes against a baseline, without writing them.</summary>")
        code.line("/// <param name=\"baseline\">The baseline, another instance.</param>")
        code.line("/// <returns>The exact byte count.</returns>")
        code.line("/// <exception cref=\"GesWireException\">A value does not fit its field, or the keys differ.</exception>")
        code.line("public \(selfModifier)int DeltaByteCount(\(parameter) baseline)")
        code.open("{")
        code.line("var writer = GesBitWriter.Counting();")
        for line in body { code.line(line) }
        code.close()
        code.blank()
        code.line("/// <summary>Writes what differs from a baseline into a new array of its exact size. A count comes first: it sizes the")
        code.line("/// buffer for the elements that the writer tries and takes back, so that the largest delta is not allocated.</summary>")
        code.line("/// <param name=\"baseline\">The baseline, another instance.</param>")
        code.line("/// <returns>The bytes.</returns>")
        code.line("/// <exception cref=\"GesWireException\">A value does not fit its field, or the keys differ.</exception>")
        code.line("public \(selfModifier)byte[] ToDeltaBytes(\(parameter) baseline)")
        code.open("{")
        code.line("var writer = GesBitWriter.Counting();")
        for line in body.dropLast() { code.line(line) }
        code.line("writer.Finish();")
        code.line("var buffer = new byte[writer.RequiredByteCount];")
        code.line("var count = WriteDelta(baseline, buffer);")
        code.line("return count == buffer.Length ? buffer : buffer.AsSpan(0, count).ToArray();")
        code.close()
        code.blank()
        code.line("/// <summary>Reads a delta against a baseline into this instance. After an error its values are unspecified.</summary>")
        code.line("/// <param name=\"data\">The delta; it may continue after it.</param>")
        code.line("/// <param name=\"baseline\">The baseline, another instance.</param>")
        code.line("/// <param name=\"byteLimit\">An optional limit on the bytes of this delta.</param>")
        code.line("/// <returns>The number of bytes the delta occupies.</returns>")
        code.line("/// <exception cref=\"GesWireException\">The data is no valid delta for this baseline.</exception>")
        code.line("public int ReadDelta(ReadOnlySpan<byte> data, \(parameter) baseline, long byteLimit = long.MaxValue)")
        code.open("{")
        if type.isClass {
            // Sharing other storage is a programming error, which only debug builds pay to detect.
            code.line("if (ReferenceEquals(this, baseline)) throw new GesWireException(GesWireException.OverlappingBuffers, \"\");")
            code.line(checkSharing)
            code.line("var lists = __SharingLists ??= new HashSet<object>();")
            code.line("__Mark(true, lists);")
            code.line("bool shared;")
            code.line("try")
            code.open("{")
            code.line("shared = baseline.__Holds(lists);")
            code.close()
            code.line("finally")
            code.open("{")
            code.line("__Mark(false, null);")
            code.line("// A set that grew beyond common records is dropped, so that no thread keeps its capacity for good.")
            code.line("if (lists.Count > 1024) __SharingLists = null;")
            code.line("else lists.Clear();")
            code.close()
            code.line("if (shared) throw new GesWireException(GesWireException.OverlappingBuffers, \"\");")
            code.line("#endif")
        }
        code.line("var reader = new GesBitReader(data, byteLimit);")
        if let key {
            code.line("var paths = __paths;")
            code.line("const int at = 0;")
            code.line("var key = __Read0(ref reader, paths, at);")
            code.line("if (key != __Wire0(baseline.\(key.base), paths, at)) throw new GesWireException(GesWireException.InvalidDelta, paths[0]);")
        }
        code.line("__ReadDelta(baseline, ref reader, __paths, 0, true);")
        code.line("return reader.Finish();")
        code.close()
        let count = String(type.fields.count)
        let counted = key == nil ? count : "top ? \(type.fields.count - 1) : \(count)"
        code.blank()
        code.line("internal \(selfModifier)bool __WriteDelta(\(parameter) baseline, ref GesBitWriter writer, string[] paths, int at, bool top\(levelParameter(type)))")
        code.open("{")
        if type.fields.isEmpty {
            // A schema without fields has no change bits and never changes.
            code.line("return false;")
            code.close()
            code.blank()
            code.line("internal bool __ReadDelta(\(parameter) baseline, ref GesBitReader reader, string[] paths, int at, bool top) => false;")
            return
        }
        code.line("var first = writer.BitPosition;")
        code.line("for (var left = \(counted); left > 0; left -= 64) writer.Write(0, global::System.Math.Min(left, 64));")
        code.line("var changed = false;")
        code.line("var ordinal = 0;")
        for field in type.fields {
            let node = String(field.value.node)
            if field.key { code.line("if (!top)") }
            code.open("{")
            code.line("var set\(node) = false;")
            deltaField(&code, field, "set\(node)")
            code.line("if (set\(node))")
            code.open("{")
            code.line("writer.SetBit(first + ordinal);")
            code.line("changed = true;")
            code.close()
            code.line("ordinal++;")
            code.close()
        }
        code.line("return changed;")
        code.close()
        code.blank()
        code.line("internal bool __ReadDelta(\(parameter) baseline, ref GesBitReader reader, string[] paths, int at, bool top\(levelParameter(type)))")
        code.open("{")
        code.line("var fieldCount = \(counted);")
        code.line("reader.Require(fieldCount, at == 0 ? \"\" : paths[at - 1]);")
        code.line("var changeBits = reader.BitPosition;")
        code.line("reader.Seek(changeBits + fieldCount);")
        code.line("var changed = false;")
        code.line("var ordinal = 0;")
        for field in type.fields {
            let node = String(field.value.node)
            if field.key { code.line("if (!top)") }
            code.open("{")
            code.line("var payload\(node) = reader.BitPosition;")
            code.line("reader.Seek(changeBits + ordinal);")
            code.line("var set\(node) = reader.Read(1, \(path(field.value))) == 1;")
            code.line("reader.Seek(payload\(node));")
            code.line("if (set\(node))")
            code.open("{")
            readDeltaField(&code, field)
            code.line("changed = true;")
            code.close()
            code.line("else")
            code.open("{")
            if field.optional { code.line("Has\(field.base) = baseline.Has\(field.base);") }
            copy(&code, field.value, field.base, "baseline." + field.base)
            code.close()
            code.line("ordinal++;")
            code.close()
            // Without a change bit the key equals the baseline's: the caller has compared it, or it identifies a list element.
            if field.key { code.line("else \(field.base) = __Value0(__Wire0(baseline.\(field.base), paths, at));") }
        }
        code.line("return changed;")
        code.close()
    }

    private static func deltaField(_ code: inout Code, _ field: GesPlanField, _ set: String) {
        if !field.optional {
            deltaPayload(&code, field.value, field.base, "baseline." + field.base, set)
            return
        }
        let node = String(field.value.node)
        code.line("if (Has\(field.base) != baseline.Has\(field.base))")
        code.open("{")
        code.line("writer.Write(Has\(field.base) ? 1UL : 0UL, 1);")
        code.line("if (Has\(field.base))")
        code.open("{")
        write(&code, field.value, field.base)
        code.close()
        code.line("\(set) = true;")
        code.close()
        code.line("else if (Has\(field.base))")
        code.open("{")
        code.line("var presence\(node) = writer.BitPosition;")
        code.line("writer.Write(1, 1);")
        deltaPayload(&code, field.value, field.base, "baseline." + field.base, set)
        code.line("if (!\(set)) writer.Rewind(presence\(node));")
        code.close()
    }

    // Writes the payload of a changed value and sets `set`; leaves nothing for an unchanged one.
    private static func deltaPayload(_ code: inout Code, _ value: GesPlanValue, _ a: String, _ b: String, _ set: String) {
        let encoding = value.encoding
        let node = String(value.node)
        let width = String(encoding.width)
        switch encoding.kind {
        case .boolean:
            code.line("if (\(a) != \(b)) \(set) = true;")
        case .integer, .quantized, .float, .tag, .flags:
            code.open("{")
            code.line("var value\(node) = __Wire\(node)(\(a), paths, at);")
            code.line("var held\(node) = __Wire\(node)(\(b), paths, at);")
            code.line("if (value\(node) != held\(node))")
            code.open("{")
            code.line(
                encoding.deltaRange == 0
                    ? "writer.Write(value\(node), \(width));"
                    : "GesWireRules.WriteDeltaRange(ref writer, value\(node), held\(node), \(unsigned(encoding.maxIndex)), \(encoding.wrapping ? "true" : "false"), \(unsigned(encoding.deltaRange)), \(encoding.deltaWidth), \(width));"
            )
            code.line("\(set) = true;")
            code.close()
            code.close()
        case .list where encoding.keyed:
            code.line("if (__WriteKeyed\(node)(baseline, ref writer, paths, at\(level(value)))) \(set) = true;")
        case .text, .bytes, .list:
            code.line("if (!__Same\(node)(baseline, paths, at\(level(value))))")
            code.open("{")
            write(&code, value, a)
            code.line("\(set) = true;")
            code.close()
        case .point, .vector:
            code.open("{")
            code.line("var first\(node) = writer.BitPosition;")
            code.line("writer.Write(0, \(value.axes.count));")
            for (index, axis) in value.axes.enumerated() {
                code.open("{")
                code.line("var set\(axis.node) = false;")
                deltaPayload(&code, axis, a + "." + axis.base, b + "." + axis.base, "set\(axis.node)")
                code.line("if (set\(axis.node))")
                code.open("{")
                code.line("writer.SetBit(first\(node) + \(index));")
                code.line("\(set) = true;")
                code.close()
                code.close()
            }
            for axis in GesSchemaPlanner.omitted(value) { code.line("if (\(a).\(axis) != 0) \(fail("TypeMismatch", value))") }
            code.line("if (!\(set)) writer.Rewind(first\(node));")
            code.close()
        case .box, .disc:
            // A nested delta of the corners, each one of a Point, or of a disc's centre and radius.
            code.open("{")
            code.line("var first\(node) = writer.BitPosition;")
            code.line("writer.Write(0, \(value.corners.count));")
            for (index, corner) in value.corners.enumerated() {
                if empty(corner) {
                    // A centre without axes transmits nothing: its change bit stays clear, and it must lie at the origin.
                    for axis in GesSchemaPlanner.omitted(corner) { code.line("if (\(a).\(corner.base).\(axis) != 0) \(fail("TypeMismatch", corner))") }
                    continue
                }
                code.open("{")
                code.line("var set\(corner.node) = false;")
                deltaPayload(&code, corner, a + "." + corner.base, b + "." + corner.base, "set\(corner.node)")
                code.line("if (set\(corner.node))")
                code.open("{")
                code.line("writer.SetBit(first\(node) + \(index));")
                code.line("\(set) = true;")
                code.close()
                code.close()
            }
            if value.encoding.kind == .disc && value.corners.count == 1 { code.line("if (\(a).Radius != 0) \(fail("TypeMismatch", value))") }
            code.line("if (!\(set)) writer.Rewind(first\(node));")
            code.close()
        case .schema where value.packed != nil:
            // A packed record changes as a whole: its word follows the change bit.
            code.line("if (\(a).RawValue != \(b).RawValue)")
            code.open("{")
            packedWrite(&code, value, a)
            code.line("\(set) = true;")
            code.close()
        case .schema:
            code.open("{")
            code.line("var start\(node) = writer.BitPosition;")
            code.line("if (\(a).__WriteDelta(\(b), ref writer, paths, at + \(value.node + 1), false\(nextLevel(value)))) \(set) = true;")
            code.line("else writer.Rewind(start\(node));")
            code.close()
        case .union:
            code.open("{")
            code.line("if ((ulong)\(a) > \(unsigned(UInt64(value.alternatives.count) - 1))) \(fail("OutOfRange", value))")
            code.line("if (\(a) != \(b))")
            code.open("{")
            write(&code, value, a)
            code.line("\(set) = true;")
            code.close()
            code.line("else")
            code.open("{")
            code.line("var entry\(node) = writer.BitPosition;")
            code.line("writer.Write((ulong)\(a), \(width));")
            code.line("switch (\(a))")
            code.open("{")
            for alternative in value.alternatives where alternative.encoding.kind != .nothing {
                code.line("case \(nestedName(value)).\(alternative.label):")
                code.open("{")
                deltaPayload(&code, alternative, prefix(a) + alternative.base, prefix(b) + alternative.base, set)
                code.line("break;")
                code.close()
            }
            code.close()
            code.line("if (!\(set)) writer.Rewind(entry\(node));")
            code.close()
            code.close()
        case .nothing:
            break
        }
    }

    private static func readDeltaField(_ code: inout Code, _ field: GesPlanField) {
        if !field.optional {
            readDeltaPayload(&code, field.value, field.base, "baseline." + field.base)
            return
        }
        let node = String(field.value.node)
        code.line("var present\(node) = reader.Read(1, \(path(field.value))) == 1;")
        code.line("if (!present\(node) && !baseline.Has\(field.base)) \(fail("InvalidDelta", field.value))")
        code.line("Has\(field.base) = present\(node);")
        code.line("if (present\(node) && baseline.Has\(field.base))")
        code.open("{")
        readDeltaPayload(&code, field.value, field.base, "baseline." + field.base)
        code.close()
        code.line("else if (present\(node))")
        code.open("{")
        read(&code, field.value, field.base)
        code.close()
        if plain(field.value) { code.line("else \(field.base) = default;") }
    }

    // Reads the payload of a value whose change bit is set, which must be a change.
    private static func readDeltaPayload(_ code: inout Code, _ value: GesPlanValue, _ a: String, _ b: String) {
        let encoding = value.encoding
        let node = String(value.node)
        let width = String(encoding.width)
        switch encoding.kind {
        case .boolean:
            code.line("\(a) = !\(b);")
        case .integer where encoding.deltaRange == 0, .quantized where encoding.deltaRange == 0, .float, .tag, .flags:
            code.open("{")
            code.line("var held\(node) = __Wire\(node)(\(b), paths, at);")
            code.line("var value\(node) = __Read\(node)(ref reader, paths, at);")
            code.line("if (value\(node) == held\(node)) \(fail("InvalidDelta", value))")
            code.line("\(a) = __Value\(node)(value\(node));")
            code.close()
        case .integer, .quantized:
            code.line(
                "\(a) = __Value\(node)(GesWireRules.ReadDeltaRange(ref reader, __Wire\(node)(\(b), paths, at), \(unsigned(encoding.maxIndex)), \(encoding.wrapping ? "true" : "false"), \(unsigned(encoding.deltaRange)), \(encoding.deltaWidth), \(width), \(path(value))));"
            )
        case .list where encoding.keyed:
            code.line("__ReadKeyed\(node)(baseline, ref reader, paths, at\(level(value)));")
        case .text, .bytes, .list:
            read(&code, value, a)
            code.line("if (__Same\(node)(baseline, paths, at\(level(value)))) \(fail("InvalidDelta", value))")
        case .point, .vector:
            code.open("{")
            code.line("reader.Require(\(value.axes.count), \(path(value)));")
            code.line("var bits\(node) = reader.BitPosition;")
            code.line("reader.Seek(bits\(node) + \(value.axes.count));")
            code.line("var changed\(node) = false;")
            for (index, axis) in value.axes.enumerated() {
                let axisNode = String(axis.node)
                code.open("{")
                code.line("var payload\(axisNode) = reader.BitPosition;")
                code.line("reader.Seek(bits\(node) + \(index));")
                code.line("var set\(axisNode) = reader.Read(1, \(path(axis))) == 1;")
                code.line("reader.Seek(payload\(axisNode));")
                code.line("if (set\(axisNode))")
                code.open("{")
                readDeltaPayload(&code, axis, a + "." + axis.base, b + "." + axis.base)
                code.line("changed\(node) = true;")
                code.close()
                code.line("else \(a).\(axis.base) = \(b).\(axis.base);")
                code.close()
            }
            for axis in GesSchemaPlanner.omitted(value) { code.line("\(a).\(axis) = 0;") }
            code.line("if (!changed\(node)) \(fail("InvalidDelta", value))")
            code.close()
        case .box, .disc:
            // The corners start as the baseline's; the box orders what the delta changes. A disc takes its centre and radius
            // likewise.
            code.open("{")
            code.line("reader.Require(\(value.corners.count), \(path(value)));")
            code.line("var bits\(node) = reader.BitPosition;")
            code.line("reader.Seek(bits\(node) + \(value.corners.count));")
            code.line("var changed\(node) = false;")
            for (index, corner) in value.corners.enumerated() {
                let cornerNode = String(corner.node)
                code.line("var \(corner.label)\(node) = \(b).\(corner.base);")
                if empty(corner) {
                    // A centre without axes never changes, so its change bit must be clear in the canonical delta.
                    code.open("{")
                    code.line("var payload\(cornerNode) = reader.BitPosition;")
                    code.line("reader.Seek(bits\(node) + \(index));")
                    code.line("if (reader.Read(1, \(path(corner))) == 1) \(fail("InvalidDelta", corner))")
                    code.line("reader.Seek(payload\(cornerNode));")
                    code.close()
                    continue
                }
                code.open("{")
                code.line("var payload\(cornerNode) = reader.BitPosition;")
                code.line("reader.Seek(bits\(node) + \(index));")
                code.line("var set\(cornerNode) = reader.Read(1, \(path(corner))) == 1;")
                code.line("reader.Seek(payload\(cornerNode));")
                code.line("if (set\(cornerNode))")
                code.open("{")
                readDeltaPayload(&code, corner, corner.label + node, b + "." + corner.base)
                code.line("changed\(node) = true;")
                code.close()
                code.close()
            }
            code.line("if (!changed\(node)) \(fail("InvalidDelta", value))")
            if value.encoding.kind == .disc && value.corners.count == 1 { code.line("var radius\(node) = 0d;") }
            code.line(value.encoding.kind == .disc ? "\(a) = new GesDisc(center\(node), radius\(node));" : "\(a) = new GesBox(lower\(node), upper\(node));")
            code.close()
        case .schema where value.packed != nil:
            packedRead(&code, value, a)
            code.line("if (\(a).RawValue == \(b).RawValue) \(fail("InvalidDelta", value))")
        case .schema:
            code.line("if (!\(a).__ReadDelta(\(b), ref reader, paths, at + \(value.node + 1), false\(nextLevel(value)))) \(fail("InvalidDelta", value))")
        case .union:
            code.open("{")
            code.line("var choice\(node) = reader.Read(\(width), \(path(value)));")
            let last = UInt64(value.alternatives.count) - 1
            if last < full(encoding.width) { code.line("if (choice\(node) > \(unsigned(last))) \(fail("InvalidIndex", value))") }
            code.line("var same\(node) = choice\(node) == (ulong)\(b);")
            code.line("\(a) = (\(nestedName(value)))choice\(node);")
            code.line("switch (choice\(node))")
            code.open("{")
            for (index, alternative) in value.alternatives.enumerated() {
                code.line("case \(index):")
                code.open("{")
                if alternative.encoding.kind == .nothing {
                    code.line("if (same\(node)) \(fail("InvalidDelta", alternative))")
                } else {
                    code.line("if (same\(node))")
                    code.open("{")
                    readDeltaPayload(&code, alternative, prefix(a) + alternative.base, prefix(b) + alternative.base)
                    code.close()
                    code.line("else")
                    code.open("{")
                    read(&code, alternative, prefix(a) + alternative.base)
                    code.close()
                }
                code.line("break;")
                code.close()
            }
            code.close()
            code.close()
        default:
            break
        }
    }

    // A comparison of one Text or List field with the baseline's, for the whole-value payloads.
    static func sameHelpers(_ code: inout Code, _ type: GesPlanType, _ global: String) {
        let parameter = type.isClass ? type.name : "in " + type.name
        for field in type.fields { sameHelper(&code, field.value, parameter, type.isClass) }
        for field in type.fields { keyedHelpers(&code, field.value, type.name, global) }
        if type.fields.contains(where: { GesSchemaPlanner.hasKeyed($0.value) }) { keyedStatics(&code) }
    }

    private static func sameHelper(_ code: inout Code, _ value: GesPlanValue, _ parameter: String, _ isClass: Bool) {
        if value.encoding.kind == .text || value.encoding.kind == .bytes || value.encoding.kind == .list {
            code.blank()
            code.line("private \(isClass ? "" : "readonly ")bool __Same\(value.node)(\(parameter) baseline, string[] paths, int at\(value.depth > 0 ? ", int level" : ""))")
            code.open("{")
            same(&code, value, value.base, "baseline." + value.base)
            code.line("return true;")
            code.close()
        }
        for alternative in value.alternatives { sameHelper(&code, alternative, parameter, isClass) }
    }
}
