// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

// The fields of a packed schema as properties over one word: each field's wire index sits at its bit offset, in declared
// order from the least significant bit, as the record's wire bytes hold it. A setter checks the value as encoding does and
// throws ArgumentOutOfRangeException; a getter never fails, since only valid words are stored, unless one was taken unchecked.
extension GesCSharpEmitter {
    // The unsigned C# type of a word of 8, 16, 32 or 64 bits.
    static func packedRaw(_ width: Int) -> String {
        switch width {
        case 8: "byte"
        case 16: "ushort"
        case 32: "uint"
        default: "ulong"
        }
    }

    // A field of a packed word: its encoding, its first bit, and the C# type and member that hold it.
    struct PackedField {
        var field: GesLayoutField
        var shift: Int
        var width: Int
        var member: String
        var type: String
        // The word type of a nested packed schema, nil for other fields.
        var nestedRaw: String?
    }

    // The bits of each field in declared order; a nested packed schema takes the bits of its fields.
    static func packedLayout(_ layout: GesSchemaLayout, _ type: GesLayoutType) -> [(field: GesLayoutField, shift: Int, width: Int)] {
        var result: [(field: GesLayoutField, shift: Int, width: Int)] = []
        var shift = 0
        for field in type.fields {
            let width = field.encoding.kind == .schema ? layout.type(field.encoding.schema).packedBits : field.encoding.bits
            result.append((field, shift, width))
            shift += width
        }
        return result
    }

    // The largest valid index of a field, below which a word of its bits is valid; nil when every index of its bits is.
    static func packedLargest(_ encoding: GesLayoutEncoding) -> UInt64? {
        let largest: UInt64 =
            switch encoding.kind {
            case .tag: UInt64(encoding.tags.count) &- 1
            case .integer, .quantized: encoding.maxIndex
            default: full(encoding.width)
            }
        return largest < full(encoding.width) ? largest : nil
    }

    // The get and set accessors of one field's property.
    static func packedAccessors(_ code: inout Code, _ packed: PackedField, _ raw: String) {
        let encoding = packed.field.encoding
        let shift = String(packed.shift)
        let mask = unsigned(full(packed.width))
        let index = "((ulong)_raw >> \(shift)) & \(mask)"
        let getter: String =
            switch encoding.kind {
            case .boolean: "(((ulong)_raw >> \(shift)) & 1UL) != 0"
            case .integer: "unchecked((long)(\(unsigned(UInt64(bitPattern: encoding.lower))) + (\(index))))"
            case .quantized: "GesWireRules.QuantizedValue(\(index), \(long(encoding.lower)), \(long(encoding.numerator)), \(double(encoding.tenPower)))"
            case .schema: "\(packed.type).FromRawUnchecked((\(packed.nestedRaw ?? ""))(\(index)))"
            case .tag: "(\(packed.type))(int)(\(index))"
            default: "(\(packed.type))(\(index))"
            }
        code.line("readonly get => \(getter);")
        code.line("set")
        code.open("{")
        let wire: String
        switch encoding.kind {
        case .boolean:
            wire = "(value ? 1UL : 0UL)"
        case .integer where encoding.wrapping:
            wire = "GesWireRules.WrappedIndex(value, \(long(encoding.lower)), \(unsigned(encoding.maxIndex &+ 1)))"
        case .integer:
            code.line(
                "if (value < \(long(encoding.lower)) || unchecked((ulong)value - \(unsigned(UInt64(bitPattern: encoding.lower)))) > \(unsigned(encoding.maxIndex))) throw new ArgumentOutOfRangeException(nameof(value), value, \(range(packed.field)));"
            )
            wire = "unchecked((ulong)value - \(unsigned(UInt64(bitPattern: encoding.lower))))"
        case .quantized where encoding.wrapping:
            code.line("if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value), value, \(range(packed.field)));")
            wire =
                "GesWireRules.WrappedQuantizedIndex(value, \(double(encoding.tenPower)), \(double(encoding.numerator)), \(long(encoding.lower)), \(unsigned(encoding.maxIndex &+ 1)), \"\")"
        case .quantized:
            code.line(
                "if (!(value >= \(bits(encoding.lowerValue)) && value <= \(bits(encoding.upperValue)))) throw new ArgumentOutOfRangeException(nameof(value), value, \(range(packed.field)));"
            )
            wire =
                "GesWireRules.QuantizedIndex(value, \(bits(encoding.lowerValue)), \(bits(encoding.upperValue)), \(double(encoding.tenPower)), \(double(encoding.numerator)), \(long(encoding.lower)), \"\")"
        case .schema:
            wire = "(ulong)value.RawValue"
        case .tag:
            code.line("if ((ulong)value > \(unsigned(UInt64(encoding.tags.count) &- 1))) throw new ArgumentOutOfRangeException(nameof(value), value, \"No tag of the field.\");")
            wire = "(ulong)value"
        default:
            if encoding.width < 64 { code.line("if ((ulong)value > \(mask)) throw new ArgumentOutOfRangeException(nameof(value), value, \"No member of the field.\");") }
            wire = "(ulong)value"
        }
        code.line(packed.width == 0 ? "_ = value;" : "_raw = unchecked((\(raw))(((ulong)_raw & ~(\(mask) << \(shift))) | (\(wire) << \(shift))));")
        code.close()
    }

    // Whether a word holds a valid index in every field whose bits allow more indices than it has.
    static func packedChecks(_ fields: [PackedField]) -> [String] {
        var checks: [String] = []
        for packed in fields {
            let index = "(((ulong)raw >> \(packed.shift)) & \(unsigned(full(packed.width))))"
            if packed.field.encoding.kind == .schema {
                checks.append("\(packed.type).IsValid((\(packed.nestedRaw ?? ""))\(index))")
            } else if let largest = packedLargest(packed.field.encoding) {
                checks.append("\(index) <= \(unsigned(largest))")
            }
        }
        return checks
    }

    // Writes a packed record's word: its bits are the record's wire bits. A word taken unchecked is checked field by field
    // first, so that an error names the field as encoding a record does.
    static func packedWrite(_ code: inout Code, _ value: GesPlanValue, _ target: String) {
        let word = value.packed!
        packedChecks(&code, word, "(ulong)\(target).RawValue", value.node < 0 ? "\"\"" : path(value))
        code.line("writer.Write(\(target).RawValue, \(word.bits));")
    }

    // Declares the paths and the offset that packedChecks names a field with, only when a field has values to check.
    static func packedPaths(_ code: inout Code, _ word: GesPackedWord, _ paths: String, _ at: String) {
        if !word.leaves.contains(where: { $0.largest != nil }) { return }
        code.line(paths)
        code.line(at)
    }

    // Checks a word field by field and, when its type is wider than its bits, that no bit above them is set, because the
    // writer does not mask what it writes.
    private static func packedChecks(_ code: inout Code, _ word: GesPackedWord, _ raw: String, _ path: String) {
        if word.bits < word.width { code.line("if ((\(raw) >> \(word.bits)) != 0) throw new GesWireException(GesWireException.OutOfRange, \(path));") }
        for leaf in word.leaves {
            if let valid = leaf.largest {
                code.line(
                    "if (((\(raw) >> \(leaf.shift)) & \(unsigned(full(leaf.width)))) > \(unsigned(valid))) throw new GesWireException(GesWireException.OutOfRange, paths[at + \(leaf.node)]);"
                )
            }
        }
    }

    // The wire members of a packed record that is a wire root, inside its struct: it reads, writes and compares its word, and
    // its delta is a change bit and, when it differs, its word.
    static func packedRoot(_ code: inout Code, _ type: GesPlanType, _ context: Context, _ shared: String? = nil) {
        // Shared with models, the type is the model's struct, which the wire code extends with static methods instead.
        let name = shared ?? type.name

        func sig(_ mutates: Bool, _ result: String, _ member: String, _ parameters: String) -> String {
            shared == nil
                ? "public \(mutates ? "" : "readonly ")\(result) \(member)(\(parameters))"
                : "public static \(result) \(member)(this \(mutates ? "ref " : "")\(name) record\(parameters.isEmpty ? "" : ", " + parameters))"
        }

        let me = shared == nil ? "" : "record."
        let rawOf = shared == nil ? "_raw" : "record.RawValue"

        func set(_ value: String) -> String { shared == nil ? "_raw = \(value);" : "record = \(name).FromRawUnchecked(\(value));" }

        let word = GesPackedWord.of(context.layout, type.name, -1)
        let raw = packedRaw(word.width)
        let bytes = String((word.bits + 7) / 8)
        let selfValue = GesPlanValue(node: -1)
        selfValue.packed = word
        if shared == nil { code.blank() }
        code.line("/// <summary>The hash over the schema hash and the parameter values this code was generated with.</summary>")
        code.line("public const ulong BindingHash = 0x\(GesSchemaLayoutJson.hex(type.layout.bindingHash))UL;")
        code.blank()
        code.line("/// <summary>The size of the encoding in bytes, the same for every record.</summary>")
        code.line("public const int MaxByteCount = \(bytes);")
        code.blank()
        code.line("internal static readonly string[] __paths = { \(type.paths.map { GesSchemaPlanner.literal($0, swift: false) }.joined(separator: ", ")) };")
        code.blank()
        code.line("/// <summary>Writes the record into a buffer; its bytes are its word, little-endian.</summary>")
        code.line("/// <param name=\"buffer\">The buffer.</param>")
        code.line("/// <returns>The number of bytes written.</returns>")
        code.line("/// <exception cref=\"GesWireException\">A word taken unchecked holds an invalid field, or the buffer is too small.</exception>")
        code.line(sig(false, "int", "Write", "Span<byte> buffer"))
        code.open("{")
        code.line("var writer = new GesBitWriter(buffer);")
        packedPaths(&code, word, "var paths = __paths;", "const int at = 0;")
        packedWrite(&code, selfValue, shared == nil ? "this" : "record")
        code.line("return writer.Finish();")
        code.close()
        code.blank()
        code.line("/// <summary>Counts the bytes that <see cref=\"Write\"/> writes, which are <see cref=\"MaxByteCount\"/>.</summary>")
        code.line("/// <returns>The byte count.</returns>")
        code.line("/// <exception cref=\"GesWireException\">A word taken unchecked holds an invalid field.</exception>")
        code.line(sig(false, "int", "ByteCount", ""))
        code.open("{")
        packedPaths(&code, word, "var paths = __paths;", "const int at = 0;")
        packedChecks(&code, word, "(ulong)\(rawOf)", "\"\"")
        code.line("return MaxByteCount;")
        code.close()
        code.blank()
        code.line("/// <summary>Writes the record into a new array of its size.</summary>")
        code.line("/// <returns>The bytes.</returns>")
        code.line("/// <exception cref=\"GesWireException\">A word taken unchecked holds an invalid field.</exception>")
        code.line(sig(false, "byte[]", "ToBytes", ""))
        code.open("{")
        code.line("var buffer = new byte[MaxByteCount];")
        code.line("\(me)Write(buffer);")
        code.line("return buffer;")
        code.close()
        code.blank()
        code.line("/// <summary>Reads a record from untrusted data into this instance.</summary>")
        code.line("/// <param name=\"data\">The data; it may continue after the record.</param>")
        code.line("/// <param name=\"byteLimit\">An optional limit on the bytes of this record.</param>")
        code.line("/// <returns>The number of bytes the record occupies.</returns>")
        code.line("/// <exception cref=\"GesWireException\">The data is no valid record.</exception>")
        code.line(sig(true, "int", "Read", "ReadOnlySpan<byte> data, long byteLimit = long.MaxValue"))
        code.open("{")
        code.line("var reader = new GesBitReader(data, byteLimit);")
        code.line("var paths = __paths;")
        code.line("const int at = 0;")
        packedReadFields(&code, selfValue, "word")
        code.line(set("(\(raw))word"))
        code.line("return reader.Finish();")
        code.close()
        code.blank()
        code.line("/// <summary>Compares the words, which are what the wire carries.</summary>")
        code.line("/// <param name=\"other\">The other record.</param>")
        code.line("/// <returns>Whether both encode alike.</returns>")
        code.line("\(sig(false, "bool", "WireEquals", name + " other")) => \(rawOf) == other.RawValue;")
        code.blank()
        code.line("/// <summary>Copies another record's word.</summary>")
        code.line("/// <param name=\"other\">The other record.</param>")
        var copied = set("other.RawValue")
        while copied.hasSuffix(";") { copied.removeLast() }
        code.line("\(sig(true, "void", "CopyFrom", name + " other")) => \(copied);")
        code.blank()
        code.line("/// <summary>Does nothing: a packed record holds only values its fields encode.</summary>")
        code.line(sig(false, "void", "Quantize", ""))
        code.open("{")
        code.close()
        code.blank()
        code.line("/// <summary>Writes the delta against a baseline: a change bit and, when the words differ, this word.</summary>")
        code.line("/// <param name=\"baseline\">The baseline.</param>")
        code.line("/// <param name=\"buffer\">The buffer.</param>")
        code.line("/// <returns>The number of bytes written.</returns>")
        code.line("/// <exception cref=\"GesWireException\">A word taken unchecked holds an invalid field, or the buffer is too small.</exception>")
        code.line(sig(false, "int", "WriteDelta", name + " baseline, Span<byte> buffer"))
        code.open("{")
        code.line("var writer = new GesBitWriter(buffer);")
        packedPaths(&code, word, "var paths = __paths;", "const int at = 0;")
        code.line("if (\(rawOf) == baseline.RawValue) writer.Write(0, 1);")
        code.line("else")
        code.open("{")
        code.line("writer.Write(1, 1);")
        packedWrite(&code, selfValue, shared == nil ? "this" : "record")
        code.close()
        code.line("return writer.Finish();")
        code.close()
        code.blank()
        code.line("/// <summary>Counts the bytes of the delta against a baseline.</summary>")
        code.line("/// <param name=\"baseline\">The baseline.</param>")
        code.line("/// <returns>The byte count.</returns>")
        code.line("\(sig(false, "int", "DeltaByteCount", name + " baseline")) => \(rawOf) == baseline.RawValue ? 1 : (\(word.bits) + 8) / 8;")
        code.blank()
        code.line("/// <summary>Writes the delta against a baseline into a new array of its size.</summary>")
        code.line("/// <param name=\"baseline\">The baseline.</param>")
        code.line("/// <returns>The bytes.</returns>")
        code.line(sig(false, "byte[]", "ToDeltaBytes", name + " baseline"))
        code.open("{")
        code.line("var buffer = new byte[\(me)DeltaByteCount(baseline)];")
        code.line("\(me)WriteDelta(baseline, buffer);")
        code.line("return buffer;")
        code.close()
        code.blank()
        code.line("/// <summary>Reads a delta against a baseline into this instance.</summary>")
        code.line("/// <param name=\"data\">The delta.</param>")
        code.line("/// <param name=\"baseline\">The baseline.</param>")
        code.line("/// <param name=\"byteLimit\">An optional limit on the bytes of this delta.</param>")
        code.line("/// <returns>The number of bytes the delta occupies.</returns>")
        code.line("/// <exception cref=\"GesWireException\">The data is no valid delta, such as a word equal to the baseline's.</exception>")
        code.line(sig(true, "int", "ReadDelta", "ReadOnlySpan<byte> data, \(name) baseline, long byteLimit = long.MaxValue"))
        code.open("{")
        code.line("var reader = new GesBitReader(data, byteLimit);")
        code.line("var paths = __paths;")
        code.line("const int at = 0;")
        code.line("if (reader.Read(1, \"\") == 0) \(set("baseline.RawValue"))")
        code.line("else")
        code.open("{")
        packedReadFields(&code, selfValue, "word")
        code.line("if ((\(raw))word == baseline.RawValue) throw new GesWireException(GesWireException.InvalidDelta, \"\");")
        code.line(set("(\(raw))word"))
        code.close()
        code.line("return reader.Finish();")
        code.close()
        // A root shared with models converts to and from values through the model's own conversion.
        if context.values && shared == nil { valueMembers(&code, type, context.holder) }
    }

    // Reads a packed record's word field by field, so that an error names the field as decoding a record does.
    static func packedReadFields(_ code: inout Code, _ value: GesPlanValue, _ word: String) {
        code.line("var \(word) = 0UL;")
        for leaf in value.packed!.leaves {
            if leaf.width == 0 { continue }
            let path = "paths[at + \(leaf.node)]"
            guard let largest = leaf.largest else {
                code.line("\(word) |= reader.Read(\(leaf.width), \(path)) << \(leaf.shift);")
                continue
            }
            code.open("{")
            code.line("var field = reader.Read(\(leaf.width), \(path));")
            code.line("if (field > \(unsigned(largest))) throw new GesWireException(GesWireException.InvalidIndex, \(path));")
            code.line("\(word) |= field << \(leaf.shift);")
            code.close()
        }
    }

    static func packedRead(_ code: inout Code, _ value: GesPlanValue, _ target: String) {
        let packed = value.packed!
        code.open("{")
        packedReadFields(&code, value, "word\(value.node)")
        code.line("\(target) = \(packed.type).FromRawUnchecked((\(packedRaw(packed.width)))word\(value.node));")
        code.close()
    }

    // Reads a List of packed records a word at a time when the data holds all of them: one read per element, checked only
    // when its bits allow invalid words, and then a word that is not valid is read again field by field for its error. Data
    // that ends inside the list is read field by field, so that the error names the field as decoding a record does.
    static func packedList(_ code: inout Code, _ value: GesPlanValue, _ target: String, _ count: String) {
        let element = value.element!
        let packed = element.packed!
        let raw = packedRaw(packed.width)
        let bits = String(packed.bits)
        code.line("if (reader.RemainingBits >= (long)\(count) * \(bits))")
        code.open("{")
        code.line("if (\(target).Capacity < \(count)) \(target).Capacity = \(count);")
        code.line("for (var position = 0; position < \(count); position++)")
        code.open("{")
        if packed.full {
            code.line("\(target).Add(\(packed.type).FromRawUnchecked((\(raw))reader.Read(\(bits), \"\")));")
        } else {
            code.line("var start = reader.BitPosition;")
            code.line("var word = (\(raw))reader.Read(\(bits), \"\");")
            code.line("if (!\(packed.type).IsValid(word))")
            code.open("{")
            code.line("reader.Seek(start);")
            code.open("{")
            packedReadFields(&code, element, "slow")
            code.close()
            code.close()
            code.line("\(target).Add(\(packed.type).FromRawUnchecked(word));")
        }
        code.close()
        code.close()
        code.line("else")
        code.open("{")
        code.line("for (var position = 0; position < \(count); position++)")
        code.open("{")
        packedReadFields(&code, element, "slow")
        code.line("\(target).Add(\(packed.type).FromRawUnchecked((\(raw))slow));")
        code.close()
        code.close()
    }

    private static func range(_ field: GesLayoutField) -> String { GesSchemaPlanner.literal("Outside the range of \(field.name).", swift: false) }
}
