// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

// The fields of a packed schema as Swift properties over `rawValue`, as CSharpEmitterPacked does for C#: a setter checks the
// value as encoding does and traps with a message, `fits<Field>` tests a value without trapping, and a getter never fails.
extension GesSwiftEmitter {
    static func packedRaw(_ width: Int) -> String {
        switch width {
        case 8: "UInt8"
        case 16: "UInt16"
        case 32: "UInt32"
        default: "UInt64"
        }
    }

    // The test `fits<Field>(_:)` of a value, or nil when every value of the field's type fits.
    static func packedFits(_ packed: GesCSharpEmitter.PackedField) -> String? {
        let encoding = packed.field.encoding
        switch encoding.kind {
        case .integer where !encoding.wrapping:
            return "value >= \(long(encoding.lower)) && UInt64(bitPattern: Int64(value)) &- \(UInt64(bitPattern: encoding.lower)) <= \(encoding.maxIndex)"
        case .quantized where encoding.wrapping:
            return "value.isFinite"
        case .quantized:
            return "value >= \(bits(encoding.lowerValue)) && value <= \(bits(encoding.upperValue))"
        case .flags where encoding.width < 64:
            return "value.rawValue <= \(full(encoding.width))"
        default:
            return nil
        }
    }

    // The get and set accessors of one field's property; `tags` names the cases of a Tag in index order.
    static func packedAccessors(_ code: inout Code, _ packed: GesCSharpEmitter.PackedField, _ raw: String, _ member: String, _ tags: [String]) {
        let encoding = packed.field.encoding
        let shift = String(packed.shift)
        let mask = String(full(packed.width))
        let index = "(UInt64(rawValue) >> \(shift)) & \(mask)"
        code.open("get {")
        switch encoding.kind {
        case .boolean:
            code.line("(UInt64(rawValue) >> \(shift)) & 1 != 0")
        case .integer:
            code.line("Int(Int64(bitPattern: \(UInt64(bitPattern: encoding.lower)) &+ (\(index))))")
        case .quantized:
            code.line("GesWireRules.quantizedValue(\(index), lower: \(long(encoding.lower)), numerator: \(long(encoding.numerator)), tenPower: \(double(encoding.tenPower)))")
        case .schema:
            code.line("\(packed.type)(uncheckedRawValue: \(packed.nestedRaw ?? "")(truncatingIfNeeded: \(index)))")
        case .tag:
            code.line("switch \(index) {")
            for (tag, name) in tags.enumerated() { code.line(tag == tags.count - 1 ? "default: .\(name)" : "case \(tag): .\(name)") }
            code.line("}")
        default:
            code.line("\(packed.type)(rawValue: \(index))")
        }
        code.close()
        code.open("set {")
        let wire: String
        switch encoding.kind {
        case .boolean:
            wire = "(newValue ? 1 : 0)"
        case .integer where encoding.wrapping:
            wire = "GesWireRules.wrappedIndex(Int64(newValue), lower: \(long(encoding.lower)), period: \(encoding.maxIndex &+ 1))"
        case .integer:
            wire = "UInt64(bitPattern: Int64(newValue)) &- \(UInt64(bitPattern: encoding.lower))"
        case .quantized where encoding.wrapping:
            wire =
                "try! GesWireRules.wrappedQuantizedIndex(newValue, tenPower: \(double(encoding.tenPower)), numerator: \(double(encoding.numerator)), lower: \(long(encoding.lower)), period: \(encoding.maxIndex &+ 1), path: \"\")"
        case .quantized:
            wire =
                "try! GesWireRules.quantizedIndex(newValue, lowerValue: \(bits(encoding.lowerValue)), upperValue: \(bits(encoding.upperValue)), tenPower: \(double(encoding.tenPower)), numerator: \(double(encoding.numerator)), lower: \(long(encoding.lower)), path: \"\")"
        case .schema:
            wire = "UInt64(newValue.rawValue)"
        case .tag:
            code.line("let wire: UInt64")
            code.line("switch newValue {")
            for (tag, name) in tags.enumerated() { code.line("case .\(name): wire = \(tag)") }
            code.line("}")
            wire = "wire"
        default:
            wire = "newValue.rawValue"
        }
        if packedFits(packed) != nil { code.line("precondition(Self.fits\(GesSchemaPlanner.pascal(member))(newValue), \"Outside the range of \(packed.field.name).\")") }
        code.line(packed.width == 0 ? "_ = newValue" : "rawValue = \(raw)(truncatingIfNeeded: (UInt64(rawValue) & ~(\(mask) << \(shift))) | ((\(wire)) << \(shift)))")
        code.close()
    }

    // Whether a word holds a valid index in every field whose bits allow more indices than it has.
    static func packedChecks(_ fields: [GesCSharpEmitter.PackedField]) -> [String] {
        var checks: [String] = []
        for packed in fields {
            let index = "((UInt64(rawValue) >> \(packed.shift)) & \(full(packed.width)))"
            if packed.field.encoding.kind == .schema {
                checks.append("\(packed.type).isValid(\(packed.nestedRaw ?? "")(truncatingIfNeeded: \(index)))")
            } else if let largest = GesCSharpEmitter.packedLargest(packed.field.encoding) {
                checks.append("\(index) <= \(largest)")
            }
        }
        return checks
    }

    // Checks a word field by field and, when its type is wider than its bits, that no bit above them is set, because the
    // writer does not mask what it writes.
    private static func packedChecks(_ code: inout Code, _ word: GesPackedWord, _ raw: String, _ path: String) {
        if word.bits < word.width { code.line("if (\(raw) >> \(word.bits)) != 0 { throw GesWireError(code: GesWireError.outOfRange, path: \(path)) }") }
        for leaf in word.leaves {
            if let valid = leaf.largest {
                code.line("if ((\(raw) >> \(leaf.shift)) & \(full(leaf.width))) > \(valid) { throw GesWireError(code: GesWireError.outOfRange, path: paths[at + \(leaf.node)]) }")
            }
        }
    }

    // Writes a packed record's word: its bits are the record's wire bits. A word taken unchecked is checked field by field
    // first, so that an error names the field as encoding a record does.
    static func packedWrite(_ code: inout Code, _ value: GesPlanValue, _ target: String) {
        let word = value.packed!
        packedChecks(&code, word, "UInt64(\(target).rawValue)", value.node < 0 ? "\"\"" : path(value))
        code.line("try writer.write(UInt64(\(target).rawValue), width: \(word.bits))")
    }

    // Reads a packed record's word field by field, so that an error names the field as decoding a record does.
    static func packedReadFields(_ code: inout Code, _ value: GesPlanValue, _ word: String) {
        code.line("var \(word): UInt64 = 0")
        for leaf in value.packed!.leaves {
            if leaf.width == 0 { continue }
            let path = "paths[at + \(leaf.node)]"
            guard let largest = leaf.largest else {
                code.line("\(word) |= try reader.read(\(leaf.width), path: \(path)) << \(leaf.shift)")
                continue
            }
            code.open("do {")
            code.line("let field = try reader.read(\(leaf.width), path: \(path))")
            code.line("if field > \(largest) { throw GesWireError(code: GesWireError.invalidIndex, path: \(path)) }")
            code.line("\(word) |= field << \(leaf.shift)")
            code.close()
        }
    }

    static func packedRead(_ code: inout Code, _ value: GesPlanValue, _ target: String) {
        let packed = value.packed!
        code.open("do {")
        packedReadFields(&code, value, "word\(value.node)")
        code.line("\(target) = \(packed.type)(uncheckedRawValue: \(packedRaw(packed.width))(truncatingIfNeeded: word\(value.node)))")
        code.close()
    }

    // The wire members of a packed wire root over its word. Declared here, they extend the type at file scope. Shared with
    // models, they are static members of an enum `<Name>Wire` in the wire namespace instead, as C#'s `<Name>Wire` class, so
    // that two wire generations of one module that share the model type do not declare its members twice.
    static func packedRoot(_ code: inout Code, _ type: GesPlanType, _ access: String, _ name: String, _ context: Context, _ shared: Bool) {
        let word = GesPackedWord.of(context.layout, type.name, -1)
        word.type = name
        let raw = packedRaw(word.width)
        let selfValue = GesPlanValue(node: -1)
        selfValue.packed = word
        let record = shared ? "record" : "self"
        let rawOf = shared ? "record.rawValue" : "rawValue"
        let recordType = shared ? name : "Self"

        func sig(_ mutates: Bool, _ member: String, _ parameters: String, _ result: String) -> String {
            if !shared { return "\(access)\(mutates ? "mutating " : "")func \(member)(\(parameters))\(result) {" }
            let first = "_ record: \(mutates ? "inout " : "")\(name)"
            return "\(access)static func \(member)(\(first)\(parameters.isEmpty ? "" : ", " + parameters))\(result) {"
        }

        func call(_ member: String, _ arguments: String, mutates: Bool = false) -> String {
            shared ? "\(member)(\(mutates ? "&record" : "record")\(arguments.isEmpty ? "" : ", " + arguments))" : "\(member)(\(arguments))"
        }

        func set(_ value: String) -> String { shared ? "record = \(name)(uncheckedRawValue: \(value))" : "self = Self(uncheckedRawValue: \(value))" }

        if shared {
            code.line("/// The wire members of the packed record `:\(type.name)`, a wire root whose type the models declare: its bytes are its word, little-endian.")
            code.open("\(access)enum \(type.name)Wire {")
        } else {
            code.line("/// The wire members of the packed record `:\(type.name)`, a wire root: its bytes are its word, little-endian.")
            code.open("extension \(name) {")
        }
        code.line("/// The hash over the schema hash and the parameter values this code was generated with.")
        code.line("\(access)static let bindingHash: UInt64 = 0x\(GesSchemaLayoutJson.hex(type.layout.bindingHash))")
        code.blank()
        code.line("/// The size of the encoding in bytes, the same for every record.")
        code.line("\(access)static let maxByteCount = \((word.bits + 7) / 8)")
        code.blank()
        code.line("static let __paths: [String] = [\(type.paths.map { GesSchemaPlanner.literal($0, swift: true) }.joined(separator: ", "))]")
        code.blank()
        if !shared {
            code.line("/// Creates the record of the word 0, to read into.")
            code.line("\(access)init() { self.init(uncheckedRawValue: 0) }")
            code.blank()
        }
        let target = shared ? "the record" : "this instance"
        code.line("/// Writes the record into a buffer and returns the number of bytes written; its bytes are its word, little-endian.")
        code.line("/// - Throws: `GesWireError` when a word taken unchecked holds an invalid field or the buffer is too small.")
        code.open(sig(false, "write", "into buffer: UnsafeMutableRawBufferPointer", " throws(GesWireError) -> Int"))
        code.line("var writer = GesBitWriter(into: buffer)")
        GesCSharpEmitter.packedPaths(&code, word, "let paths = Self.__paths", "let at = 0")
        packedWrite(&code, selfValue, record)
        code.line("return try writer.finish()")
        code.close()
        code.blank()
        code.line("/// Counts the bytes that `\(shared ? "write(_:into:)" : "write(into:)")` writes, which are `maxByteCount`.")
        code.line("/// - Throws: `GesWireError` when a word taken unchecked holds an invalid field.")
        code.open(sig(false, "encodedByteCount", "", " throws(GesWireError) -> Int"))
        GesCSharpEmitter.packedPaths(&code, word, "let paths = Self.__paths", "let at = 0")
        packedChecks(&code, word, "UInt64(\(rawOf))", "\"\"")
        code.line("return Self.maxByteCount")
        code.close()
        code.blank()
        code.line("/// Writes the record into a new array of its size.")
        code.line("/// - Throws: `GesWireError` when a word taken unchecked holds an invalid field.")
        code.open(sig(false, "encoded", "", " throws(GesWireError) -> [UInt8]"))
        code.line("let buffer = UnsafeMutableRawBufferPointer.allocate(byteCount: Self.maxByteCount, alignment: 8)")
        code.line("defer { buffer.deallocate() }")
        code.line("let count = try \(call("write", "into: buffer"))")
        code.line("return Array(buffer[0..<count])")
        code.close()
        code.blank()
        code.line("/// Reads a record from untrusted data into \(target) and returns the number of bytes it occupies.")
        code.line("/// - Throws: `GesWireError` when the data is no valid record.")
        code.line("@discardableResult")
        code.open(sig(true, "read", "from data: UnsafeRawBufferPointer, byteLimit: Int = .max", " throws(GesWireError) -> Int"))
        code.line("var reader = GesBitReader(data: data, byteLimit: byteLimit)")
        code.line("let paths = Self.__paths")
        code.line("let at = 0")
        packedReadFields(&code, selfValue, "word")
        code.line(set("\(raw)(truncatingIfNeeded: word)"))
        code.line("return try reader.finish()")
        code.close()
        code.blank()
        code.line("/// Reads a record from bytes into \(target) and returns the number of bytes it occupies.")
        code.line("/// - Throws: `GesWireError` when the bytes are no valid record.")
        code.line("@discardableResult")
        code.open(sig(true, "read", "from bytes: [UInt8], byteLimit: Int = .max", " throws(GesWireError) -> Int"))
        copied(&code, call("read", "from: UnsafeRawBufferPointer(rebasing: buffer[0..<bytes.count]), byteLimit: byteLimit", mutates: true))
        code.close()
        code.blank()
        code.line("/// Whether another record writes the same bytes: whether the words are equal.")
        code.line("\(sig(false, "wireEquals", "_ other: \(recordType)", " -> Bool")) \(rawOf) == other.rawValue }")
        code.blank()
        code.line("/// Copies another record's word.")
        code.line("\(sig(true, "copy", "from other: \(recordType)", "")) \(record) = other }")
        code.blank()
        code.line("/// Does nothing: a packed record holds only values its fields encode.")
        code.line(shared ? "\(access)static func quantize(_ record: \(name)) {}" : "\(access)func quantize() {}")
        code.blank()
        code.line("/// Writes the delta against a baseline: a change bit and, when the words differ, the record's word.")
        code.line("/// - Throws: `GesWireError` when a word taken unchecked holds an invalid field or the buffer is too small.")
        code.open(sig(false, "writeDelta", "against baseline: \(recordType), into buffer: UnsafeMutableRawBufferPointer", " throws(GesWireError) -> Int"))
        code.line("var writer = GesBitWriter(into: buffer)")
        GesCSharpEmitter.packedPaths(&code, word, "let paths = Self.__paths", "let at = 0")
        code.open("if \(rawOf) == baseline.rawValue {")
        code.line("try writer.write(0, width: 1)")
        code.close("} else {")
        code.indent()
        code.line("try writer.write(1, width: 1)")
        packedWrite(&code, selfValue, record)
        code.close()
        code.line("return try writer.finish()")
        code.close()
        code.blank()
        code.line("/// Counts the bytes of the delta against a baseline.")
        code.line("\(sig(false, "deltaEncodedByteCount", "against baseline: \(recordType)", " -> Int")) \(rawOf) == baseline.rawValue ? 1 : (\(word.bits) + 8) / 8 }")
        code.blank()
        code.line("/// Writes the delta against a baseline into a new array of its size.")
        code.line("/// - Throws: `GesWireError` when a word taken unchecked holds an invalid field.")
        code.open(sig(false, "deltaEncoded", "against baseline: \(recordType)", " throws(GesWireError) -> [UInt8]"))
        code.line("let buffer = UnsafeMutableRawBufferPointer.allocate(byteCount: \(call("deltaEncodedByteCount", "against: baseline")), alignment: 8)")
        code.line("defer { buffer.deallocate() }")
        code.line("let count = try \(call("writeDelta", "against: baseline, into: buffer"))")
        code.line("return Array(buffer[0..<count])")
        code.close()
        code.blank()
        code.line("/// Reads a delta against a baseline into \(target) and returns the number of bytes it occupies.")
        code.line("/// - Throws: `GesWireError` when the data is no valid delta, such as a word equal to the baseline's.")
        code.line("@discardableResult")
        code.open(sig(true, "readDelta", "from data: UnsafeRawBufferPointer, against baseline: \(recordType), byteLimit: Int = .max", " throws(GesWireError) -> Int"))
        code.line("var reader = GesBitReader(data: data, byteLimit: byteLimit)")
        code.line("let paths = Self.__paths")
        code.line("let at = 0")
        code.open("if try reader.read(1, path: \"\") == 0 {")
        code.line("\(record) = baseline")
        code.close("} else {")
        code.indent()
        packedReadFields(&code, selfValue, "word")
        code.line("if \(raw)(truncatingIfNeeded: word) == baseline.rawValue { throw GesWireError(code: GesWireError.invalidDelta, path: \"\") }")
        code.line(set("\(raw)(truncatingIfNeeded: word)"))
        code.close()
        code.line("return try reader.finish()")
        code.close()
        code.blank()
        code.line("/// Reads a delta from bytes against a baseline into \(target) and returns the number of bytes it occupies.")
        code.line("/// - Throws: `GesWireError` when the bytes are no valid delta.")
        code.line("@discardableResult")
        code.open(sig(true, "readDelta", "from bytes: [UInt8], against baseline: \(recordType), byteLimit: Int = .max", " throws(GesWireError) -> Int"))
        copied(&code, call("readDelta", "from: UnsafeRawBufferPointer(rebasing: buffer[0..<bytes.count]), against: baseline, byteLimit: byteLimit", mutates: true))
        code.close()
        // A root shared with models converts to and from values through the model's own conversion.
        if context.values && !shared { valueMembers(&code, type, access, context.holder) }
        code.close()
    }

    // Copies bytes into an aligned buffer and calls a read on it, as the other records' byte overloads do.
    private static func copied(_ code: inout Code, _ call: String) {
        code.line("let buffer = UnsafeMutableRawBufferPointer.allocate(byteCount: Swift.max(bytes.count, 1), alignment: 8)")
        code.line("defer { buffer.deallocate() }")
        code.line("buffer.copyBytes(from: bytes)")
        code.line("return try " + call)
    }

    // Reads a List of packed records a word at a time when the data holds all of them; see GesCSharpEmitter.packedList.
    static func packedList(_ code: inout Code, _ value: GesPlanValue, _ target: String, _ count: String) {
        let element = value.element!
        let packed = element.packed!
        let raw = packedRaw(packed.width)
        let bits = String(packed.bits)
        code.line("let whole = reader.remainingBits >= \(count) * \(bits)")
        code.open("if whole {")
        code.line("\(target).reserveCapacity(\(count))")
        code.open("for _ in 0..<\(count) {")
        if packed.full {
            code.line("\(target).append(\(packed.type)(uncheckedRawValue: \(raw)(truncatingIfNeeded: try reader.read(\(bits), path: \"\"))))")
        } else {
            code.line("let start = reader.bitPosition")
            code.line("let word = \(raw)(truncatingIfNeeded: try reader.read(\(bits), path: \"\"))")
            code.open("if !\(packed.type).isValid(word) {")
            code.line("reader.seek(to: start)")
            code.open("do {")
            packedReadFields(&code, element, "slow")
            code.line("_ = slow")
            code.close()
            code.close()
            code.line("\(target).append(\(packed.type)(uncheckedRawValue: word))")
        }
        code.close()
        code.close()
        code.open("if !whole {")
        code.open("for _ in 0..<\(count) {")
        packedReadFields(&code, element, "slow")
        code.line("\(target).append(\(packed.type)(uncheckedRawValue: \(raw)(truncatingIfNeeded: slow)))")
        code.close()
        code.close()
    }
}
