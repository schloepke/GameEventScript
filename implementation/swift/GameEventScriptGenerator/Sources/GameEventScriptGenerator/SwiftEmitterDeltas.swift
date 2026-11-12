// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

// Wire values, comparison, copying, quantization and deltas of generated Swift types. Comparison and deltas use wire values:
// indices and stored bits, never raw doubles. Local names carry the node of their value, as in the C# output.
extension GesSwiftEmitter {
    // The level argument that a recursive List's own helpers take.
    static func level(_ value: GesPlanValue) -> String { value.depth > 0 ? ", level" : "" }

    static func isScalar(_ value: GesPlanValue) -> Bool {
        switch value.encoding.kind {
        case .boolean, .integer, .quantized, .float, .tag, .flags: true
        default: false
        }
    }

    static func scalars(_ value: GesPlanValue) -> [GesPlanValue] {
        var result: [GesPlanValue] = isScalar(value) ? [value] : []
        result += value.axes
        for alternative in value.alternatives { result += scalars(alternative) }
        if value.item == nil, let element = value.element { result += scalars(element) }
        return result
    }

    static func target(_ field: String) -> String { "self." + member(field) }

    static func other(_ owner: String, _ field: String) -> String { owner + "." + member(field) }

    static func extras(_ code: inout Code, _ type: GesPlanType, _ access: String, _ deltas: Bool, _ element: Bool) {
        let mutating = type.isClass ? "" : "mutating "
        for field in type.fields {
            for value in scalars(field.value) { helpers(&code, value) }
        }
        code.blank()
        code.open("func __same(_ other: \(type.name), _ paths: [String], _ at: Int\(levelParameter(type))) throws(GesWireError) -> Bool {")
        for field in type.fields {
            if field.optional {
                code.line("if self.has\(field.base) != other.has\(field.base) { return false }")
                code.open("if self.has\(field.base) {")
                same(&code, field.value, target(field.base), other("other", field.base), "self", "other")
                code.close()
            } else {
                same(&code, field.value, target(field.base), other("other", field.base), "self", "other")
            }
        }
        for tag in type.tags { code.line("if self.\(member(tag.base)) != other.\(member(tag.base)) { return false }") }
        code.line("return true")
        code.close()
        if type.isClass {
            code.blank()
            code.line("/// Copies the values of another record into the storage of this one, without allocating once it has grown.")
            code.open("\(access)func copy(from other: \(type.name)) {")
            code.line("if self === other { return }")
            for field in type.fields {
                if field.optional { code.line("self.has\(field.base) = other.has\(field.base)") }
                copy(&code, field.value, target(field.base), other("other", field.base), "other")
            }
            for tag in type.tags { code.line("self.\(member(tag.base)) = other.\(member(tag.base))") }
            code.close()
        }
        code.blank()
        code.open("\(mutating)func __quantize(_ paths: [String], _ at: Int\(levelParameter(type))) throws(GesWireError) {")
        for field in type.fields {
            if field.optional {
                code.open("if self.has\(field.base) {")
                quantize(&code, field.value, target(field.base))
                code.close()
            } else {
                quantize(&code, field.value, target(field.base))
            }
        }
        code.close()
        if type.isItem { return }
        code.blank()
        code.line("/// Returns whether another \(type.kind == .message ? "message" : "schema") has the same wire values, so that it writes the same bytes.")
        code.line("/// - Throws: `GesWireError` when a compared value has no wire value, such as a number outside its range.")
        code.open("\(access)func wireEquals(_ other: \(type.name)) throws(GesWireError) -> Bool {")
        code.line("try __same(other, Self.__paths, 0)")
        code.close()
        code.blank()
        code.line("/// Replaces every quantized value by the value its index decodes to, which a receiver reads.")
        code.line("/// - Throws: `GesWireError` when a quantized value lies outside its range.")
        code.open("\(access)\(mutating)func quantize() throws(GesWireError) {")
        code.line("try __quantize(Self.__paths, 0)")
        code.close()
        if element { keyOrder(&code, type) }
        if deltas { self.deltas(&code, type, access, mutating) }
    }

    private static func helpers(_ code: inout Code, _ value: GesPlanValue) {
        let encoding = value.encoding
        let node = String(value.node)
        let type = typeName(value)
        let width = String(encoding.width)
        code.blank()
        code.open("private static func __wire\(node)(_ value: \(type), _ paths: [String], _ at: Int) throws(GesWireError) -> UInt64 {")
        switch encoding.kind {
        case .boolean:
            code.line("value ? 1 : 0")
        case .integer where !encoding.range:
            code.line("UInt64(bitPattern: value)")
        case .integer where encoding.wrapping:
            code.line("GesWireRules.wrappedIndex(value, lower: \(long(encoding.lower)), period: \(encoding.maxIndex &+ 1))")
        case .integer:
            let lower = String(UInt64(bitPattern: encoding.lower))
            code.line("if value < \(long(encoding.lower)) || UInt64(bitPattern: value) &- \(lower) > \(encoding.maxIndex) { \(fail("outOfRange", value)) }")
            code.line("return UInt64(bitPattern: value) &- \(lower)")
        case .quantized where encoding.wrapping:
            code.line(
                "try GesWireRules.wrappedQuantizedIndex(value, tenPower: \(double(encoding.tenPower)), numerator: \(double(encoding.numerator)), lower: \(long(encoding.lower)), period: \(encoding.maxIndex &+ 1), path: \(path(value)))"
            )
        case .quantized:
            code.line(
                "try GesWireRules.quantizedIndex(value, lowerValue: \(bits(encoding.lowerValue)), upperValue: \(bits(encoding.upperValue)), tenPower: \(double(encoding.tenPower)), numerator: \(double(encoding.numerator)), lower: \(long(encoding.lower)), path: \(path(value)))"
            )
        case .float:
            code.line("if value.isNaN { \(fail("notRepresentable", value)) }")
            if encoding.range { code.line("if value < \(bits(encoding.lowerValue)) || value > \(bits(encoding.upperValue)) { \(fail("outOfRange", value)) }") }
            code.line("return (value == 0 ? 0.0 : value).bitPattern")
        case .flags:
            if encoding.width < 64 { code.line("if value.rawValue > \(full(encoding.width)) { \(fail("unknownTag", value)) }") }
            code.line("return value.rawValue")
        default:
            code.line("UInt64(value.rawValue)")
        }
        code.close()
        code.blank()
        code.open("private static func __read\(node)(_ reader: inout GesBitReader, _ paths: [String], _ at: Int) throws(GesWireError) -> UInt64 {")
        code.line("let wire = try reader.read(\(width), path: \(path(value)))")
        if encoding.kind == .float {
            code.line("let value = Double(bitPattern: wire)")
            code.line("if value.isNaN || wire == 0x8000_0000_0000_0000\(encoding.range ? " || value.isInfinite" : "") { \(fail("invalidNumber", value)) }")
            if encoding.range { code.line("if value < \(bits(encoding.lowerValue)) || value > \(bits(encoding.upperValue)) { \(fail("outOfRange", value)) }") }
        } else if encoding.kind != .boolean {
            let largest =
                switch encoding.kind {
                case .tag: UInt64(encoding.tags.count) - 1
                case .flags: full(encoding.width)
                default: encoding.maxIndex
                }
            if largest < full(encoding.width) { code.line("if wire > \(largest) { \(fail("invalidIndex", value)) }") }
        }
        code.line("return wire")
        code.close()
        code.blank()
        switch encoding.kind {
        case .boolean: code.line("private static func __value\(node)(_ wire: UInt64) -> Bool { wire != 0 }")
        case .integer where !encoding.range: code.line("private static func __value\(node)(_ wire: UInt64) -> Int64 { Int64(bitPattern: wire) }")
        case .integer: code.line("private static func __value\(node)(_ wire: UInt64) -> Int64 { Int64(bitPattern: \(UInt64(bitPattern: encoding.lower)) &+ wire) }")
        case .quantized:
            code.line(
                "private static func __value\(node)(_ wire: UInt64) -> Double { GesWireRules.quantizedValue(wire, lower: \(long(encoding.lower)), numerator: \(long(encoding.numerator)), tenPower: \(double(encoding.tenPower))) }"
            )
        case .float: code.line("private static func __value\(node)(_ wire: UInt64) -> Double { Double(bitPattern: wire) }")
        case .flags: code.line("private static func __value\(node)(_ wire: UInt64) -> \(type) { \(type)(rawValue: wire) }")
        default: code.line("private static func __value\(node)(_ wire: UInt64) -> \(type) { \(type)(rawValue: \(underlying(encoding.tags.count))(wire))! }")
        }
    }

    // The UTF-8 storage of a Text in `owner`, "self" or another record.
    private static func bytes(_ value: GesPlanValue, _ owner: String) -> String { storage(value, owner) }

    // Returns false from the enclosing function when the wire values of a and b differ.
    private static func same(_ code: inout Code, _ value: GesPlanValue, _ a: String, _ b: String, _ ownerA: String, _ ownerB: String) {
        let node = String(value.node)
        switch value.encoding.kind {
        case .boolean:
            code.line("if \(a) != \(b) { return false }")
        case .integer, .quantized, .float, .tag, .flags:
            code.line("if try Self.__wire\(node)(\(a), paths, at) != Self.__wire\(node)(\(b), paths, at) { return false }")
        case .text, .bytes:
            code.line("if \(bytes(value, ownerA)) != \(bytes(value, ownerB)) { return false }")
        case .point, .vector:
            for axis in value.axes { same(&code, axis, a + "." + member(axis.base), b + "." + member(axis.base), ownerA, ownerB) }
        case .schema:
            code.line("if !(try \(a).__same(\(b), paths, at + \(value.node + 1)\(nextLevel(value)))) { return false }")
        case .list:
            if value.depth > 0 { openLevels(&code, value, "\(a).count + \(b).count") }
            code.line("if \(a).count != \(b).count { return false }")
            code.open("for index\(node) in 0..<\(a).count {")
            let element = value.element!
            if value.item != nil {
                code.line("if !(try \(a)[index\(node)].__same(\(b)[index\(node)], paths, at + \(element.node))) { return false }")
            } else {
                same(&code, element, "\(a)[index\(node)]", "\(b)[index\(node)]", ownerA, ownerB)
            }
            code.close()
            if value.depth > 0 { code.close() }
        case .union:
            code.line("if \(a) != \(b) { return false }")
            code.line("switch \(a) {")
            for alternative in value.alternatives {
                code.open("case .\(member(alternative.label)):")
                if alternative.encoding.kind == .nothing {
                    code.line("break")
                } else {
                    same(&code, alternative, other(ownerA, alternative.base), other(ownerB, alternative.base), ownerA, ownerB)
                }
                code.close("")
            }
            code.line("}")
        case .nothing:
            break
        }
    }

    // The storage that a read into this record writes: the record, the class records it holds and every pooled element. Debug
    // builds mark it while a delta reads, so that a baseline holding any of it is rejected before the read overwrites what it
    // holds; release builds generate none of it. Swift arrays are values, so only records can be shared.
    static func sharingMembers(_ code: inout Code, _ type: GesPlanType, _ stored: String) {
        code.blank()
        code.line(checkSharing)
        code.line("\(stored)var __busy = false")
        code.blank()
        if !type.fields.contains(where: { GesSchemaPlanner.holdsStorage($0.value, lists: false) }) {
            code.line("func __mark(_ busy: Bool) { self.__busy = busy }")
            code.blank()
            code.line("func __holds() -> Bool { self.__busy }")
        } else {
            code.open("func __mark(_ busy: Bool) {")
            code.line("self.__busy = busy")
            for field in type.fields { mark(&code, field.value) }
            code.close()
            code.blank()
            code.open("func __holds() -> Bool {")
            code.line("if self.__busy { return true }")
            for field in type.fields { holds(&code, field.value) }
            code.line("return false")
            code.close()
        }
        code.line("#endif")
    }

    // The builds that check shared baselines: debug builds and any build that asks.
    private static let checkSharing = "#if DEBUG || GES_CHECK_SHARING"

    private static func mark(_ code: inout Code, _ value: GesPlanValue) {
        switch value.encoding.kind {
        case .schema where value.isClass:
            code.line("\(target(value.base)).__mark(busy)")
        case .list where value.element!.isClass:
            code.line("for item in self.__\(GesSchemaPlanner.camel(value.base))Pool { item.__mark(busy) }")
        case .union:
            for alternative in value.alternatives { mark(&code, alternative) }
        default:
            break
        }
    }

    private static func holds(_ code: inout Code, _ value: GesPlanValue) {
        switch value.encoding.kind {
        case .schema where value.isClass:
            code.line("if \(target(value.base)).__holds() { return true }")
        case .list where value.element!.isClass:
            code.line("for item in \(target(value.base)) where item.__holds() { return true }")
        case .union:
            for alternative in value.alternatives { holds(&code, alternative) }
        default:
            break
        }
    }

    // Copies b into a without allocating once a's storage has grown.
    private static func copy(_ code: inout Code, _ value: GesPlanValue, _ a: String, _ b: String, _ ownerB: String) {
        let node = String(value.node)
        switch value.encoding.kind {
        case .text, .bytes:
            code.line("\(bytes(value, "self")).removeAll(keepingCapacity: true)")
            code.line("\(bytes(value, "self")).append(contentsOf: \(bytes(value, ownerB)))")
        case .schema where value.isClass:
            code.line("\(a).copy(from: \(b))")
        case .list where value.element!.isClass:
            let pool = "self.__\(GesSchemaPlanner.camel(value.base))Pool"
            code.line("\(a).removeAll(keepingCapacity: true)")
            code.open("for index\(node) in 0..<\(b).count {")
            code.line("if index\(node) == \(pool).count { \(pool).append(\(elementName(value))()) }")
            code.line("let item\(node) = \(pool)[index\(node)]")
            code.line("item\(node).copy(from: \(b)[index\(node)])")
            code.line("\(a).append(item\(node))")
            code.close()
        case .list:
            code.line("\(a).removeAll(keepingCapacity: true)")
            code.line("\(a).append(contentsOf: \(b))")
        case .union:
            code.line("\(a) = \(b)")
            code.line("switch \(a) {")
            for alternative in value.alternatives {
                code.open("case .\(member(alternative.label)):")
                if alternative.encoding.kind == .nothing {
                    code.line("break")
                } else {
                    copy(&code, alternative, target(alternative.base), other(ownerB, alternative.base), ownerB)
                }
                code.close("")
            }
            code.line("}")
        default:
            code.line("\(a) = \(b)")
        }
    }

    private static func quantize(_ code: inout Code, _ value: GesPlanValue, _ target: String) {
        let node = String(value.node)
        switch value.encoding.kind {
        case .quantized:
            code.line("\(target) = Self.__value\(node)(try Self.__wire\(node)(\(target), paths, at))")
        case .point, .vector:
            for axis in value.axes { quantize(&code, axis, target + "." + member(axis.base)) }
        case .schema:
            code.line("try \(target).__quantize(paths, at + \(value.node + 1)\(nextLevel(value)))")
        case .list where quantized(value.element!):
            let element = value.element!
            if value.depth > 0 { openLevels(&code, value, target + ".count") }
            code.open("for index\(node) in 0..<\(target).count {")
            if value.item != nil {
                code.line("try \(target)[index\(node)].__quantize(paths, at + \(element.node))")
            } else if element.encoding.kind == .schema {
                code.line("try \(target)[index\(node)].__quantize(paths, at + \(element.node + 1)\(nextLevel(element)))")
            } else {
                quantize(&code, element, "\(target)[index\(node)]")
            }
            code.close()
            if value.depth > 0 { code.close() }
        case .union where value.alternatives.contains(where: quantized):
            code.line("switch \(target) {")
            for alternative in value.alternatives {
                code.open("case .\(member(alternative.label)):")
                if quantized(alternative) { quantize(&code, alternative, self.target(alternative.base)) } else { code.line("break") }
                code.close("")
            }
            code.line("}")
        default:
            break
        }
    }

    private static func quantized(_ value: GesPlanValue) -> Bool {
        switch value.encoding.kind {
        case .quantized, .schema: true
        case .point, .vector: value.axes.contains(where: quantized)
        case .union: value.alternatives.contains(where: quantized)
        case .list: value.item.map { quantized($0.fields[0].value) } ?? quantized(value.element!)
        default: false
        }
    }

    private static func deltas(_ code: inout Code, _ type: GesPlanType, _ access: String, _ mutating: String) {
        let key = type.fields.first.flatMap { $0.key ? $0 : nil }
        code.blank()
        code.line("/// Writes what differs from a baseline, the full record the receiver holds; `maxByteCount` bytes always suffice.")
        code.line("/// - Returns: The number of bytes written.")
        code.line("/// - Throws: `GesWireError` when a value does not fit its field, the keys differ, or the buffer is too small.")
        code.open("\(access)func writeDelta(against baseline: \(type.name), into buffer: UnsafeMutableRawBufferPointer) throws(GesWireError) -> Int {")
        code.line("var writer = GesBitWriter(into: buffer)")
        var body: [String] = []
        if let key {
            body.append("let paths = Self.__paths")
            body.append("let key = try Self.__wire0(\(target(key.base)), paths, 0)")
            body.append("if try key != Self.__wire0(\(other("baseline", key.base)), paths, 0) { throw GesWireError(code: GesWireError.keyMismatch, path: paths[0]) }")
            body.append("try writer.write(key, width: \(key.value.encoding.width))")
        }
        body.append("_ = try __writeDelta(baseline, &writer, Self.__paths, 0, true)")
        body.append("return try writer.finish()")
        for line in body { code.line(line) }
        code.close()
        code.blank()
        code.line("/// Counts the bytes that `writeDelta(against:into:)` writes against a baseline, without writing them.")
        code.line("/// - Throws: `GesWireError` when a value does not fit its field or the keys differ.")
        code.open("\(access)func deltaEncodedByteCount(against baseline: \(type.name)) throws(GesWireError) -> Int {")
        code.line("var writer = GesBitWriter.counting()")
        for line in body { code.line(line) }
        code.close()
        code.blank()
        code.line("/// Writes what differs from a baseline into a new array of its exact size. A count comes first: it sizes the buffer for")
        code.line("/// the elements that the writer tries and takes back, so that the largest delta is not allocated.")
        code.line("/// - Throws: `GesWireError` when a value does not fit its field or the keys differ.")
        code.open("\(access)func deltaEncoded(against baseline: \(type.name)) throws(GesWireError) -> [UInt8] {")
        code.line("var writer = GesBitWriter.counting()")
        for line in body.dropLast() { code.line(line) }
        code.line("_ = try writer.finish()")
        code.line("let buffer = UnsafeMutableRawBufferPointer.allocate(byteCount: Swift.max(writer.requiredByteCount, 1), alignment: 8)")
        code.line("defer { buffer.deallocate() }")
        code.line("let count = try writeDelta(against: baseline, into: buffer)")
        code.line("return Array(buffer[0..<count])")
        code.close()
        code.blank()
        code.line("/// Reads a delta against a baseline into this instance. After an error its values are unspecified.")
        code.line("/// - Parameters:")
        code.line("///   - data: The delta; it may continue after it.")
        code.line("///   - baseline: The baseline, another instance.")
        code.line("///   - byteLimit: An optional limit on the bytes of this delta.")
        code.line("/// - Returns: The number of bytes the delta occupies.")
        code.line("/// - Throws: `GesWireError` when the data is no valid delta for this baseline.")
        code.line("@discardableResult")
        code.open("\(access)\(mutating)func readDelta(from data: UnsafeRawBufferPointer, against baseline: \(type.name), byteLimit: Int = .max) throws(GesWireError) -> Int {")
        if type.isClass {
            // Sharing other storage is a programming error, which only debug builds pay to detect.
            code.line("if self === baseline { throw GesWireError(code: GesWireError.overlappingBuffers, path: \"\") }")
            code.line(checkSharing)
            code.line("self.__mark(true)")
            code.line("let shared = baseline.__holds()")
            code.line("self.__mark(false)")
            code.line("if shared { throw GesWireError(code: GesWireError.overlappingBuffers, path: \"\") }")
            code.line("#endif")
        }
        code.line("var reader = GesBitReader(data: data, byteLimit: byteLimit)")
        if let key {
            code.line("let paths = Self.__paths")
            code.line("let key = try Self.__read0(&reader, paths, 0)")
            code.line("if try key != Self.__wire0(\(other("baseline", key.base)), paths, 0) { throw GesWireError(code: GesWireError.invalidDelta, path: paths[0]) }")
        }
        code.line("_ = try __readDelta(baseline, &reader, Self.__paths, 0, true)")
        code.line("return try reader.finish()")
        code.close()
        code.blank()
        code.line("/// Reads a delta from bytes against a baseline into this instance. After an error its values are unspecified.")
        code.line("/// - Returns: The number of bytes the delta occupies.")
        code.line("/// - Throws: `GesWireError` when the bytes are no valid delta for this baseline.")
        code.line("@discardableResult")
        code.open("\(access)\(mutating)func readDelta(from bytes: [UInt8], against baseline: \(type.name), byteLimit: Int = .max) throws(GesWireError) -> Int {")
        code.line("let buffer = UnsafeMutableRawBufferPointer.allocate(byteCount: Swift.max(bytes.count, 1), alignment: 8)")
        code.line("defer { buffer.deallocate() }")
        code.line("buffer.copyBytes(from: bytes)")
        code.line("return try readDelta(from: UnsafeRawBufferPointer(rebasing: buffer[0..<bytes.count]), against: baseline, byteLimit: byteLimit)")
        code.close()
        let count = String(type.fields.count)
        let counted = key == nil ? count : "top ? \(type.fields.count - 1) : \(count)"
        code.blank()
        code.open("func __writeDelta(_ baseline: \(type.name), _ writer: inout GesBitWriter, _ paths: [String], _ at: Int, _ top: Bool\(levelParameter(type))) throws(GesWireError) -> Bool {")
        if type.fields.isEmpty {
            // A schema without fields has no change bits and never changes.
            code.line("false")
            code.close()
            code.blank()
            code.open("\(mutating)func __readDelta(_ baseline: \(type.name), _ reader: inout GesBitReader, _ paths: [String], _ at: Int, _ top: Bool\(levelParameter(type))) throws(GesWireError) -> Bool {")
            code.line("false")
            code.close()
            return
        }
        code.line("let first = writer.bitPosition")
        code.line("var left = \(counted)")
        code.open("while left > 0 {")
        code.line("try writer.write(0, width: Swift.min(left, 64))")
        code.line("left -= 64")
        code.close()
        code.line("var changed = false")
        code.line("var ordinal = 0")
        for field in type.fields {
            let node = String(field.value.node)
            code.open(field.key ? "if !top {" : "do {")
            code.line("var set\(node) = false")
            deltaField(&code, field, "set\(node)")
            code.open("if set\(node) {")
            code.line("writer.setBit(first + ordinal)")
            code.line("changed = true")
            code.close()
            code.line("ordinal += 1")
            code.close()
        }
        code.line("return changed")
        code.close()
        code.blank()
        code.open("\(mutating)func __readDelta(_ baseline: \(type.name), _ reader: inout GesBitReader, _ paths: [String], _ at: Int, _ top: Bool\(levelParameter(type))) throws(GesWireError) -> Bool {")
        code.line("let fieldCount = \(counted)")
        code.line("try reader.require(fieldCount, path: at == 0 ? \"\" : paths[at - 1])")
        code.line("let changeBits = reader.bitPosition")
        code.line("reader.seek(to: changeBits + fieldCount)")
        code.line("var changed = false")
        code.line("var ordinal = 0")
        for field in type.fields {
            let node = String(field.value.node)
            code.open(field.key ? "if !top {" : "do {")
            code.line("let payload\(node) = reader.bitPosition")
            code.line("reader.seek(to: changeBits + ordinal)")
            code.line("let set\(node) = try reader.read(1, path: \(path(field.value))) == 1")
            code.line("reader.seek(to: payload\(node))")
            code.open("if set\(node) {")
            readDeltaField(&code, field)
            code.line("changed = true")
            code.close("} else {")
            code.indent()
            if field.optional { code.line("self.has\(field.base) = baseline.has\(field.base)") }
            copy(&code, field.value, target(field.base), other("baseline", field.base), "baseline")
            code.close("}")
            code.line("ordinal += 1")
            if !field.key {
                code.close()
                continue
            }
            // Without a change bit the key equals the baseline's: the caller has compared it, or it identifies a list element.
            code.close("} else {")
            code.line("    \(target(field.base)) = Self.__value0(try Self.__wire0(\(other("baseline", field.base)), paths, at))")
            code.line("}")
        }
        code.line("return changed")
        code.close()
    }

    private static func deltaField(_ code: inout Code, _ field: GesPlanField, _ set: String) {
        if !field.optional {
            deltaPayload(&code, field.value, target(field.base), other("baseline", field.base), set)
            return
        }
        let node = String(field.value.node)
        code.open("if self.has\(field.base) != baseline.has\(field.base) {")
        code.line("try writer.write(self.has\(field.base) ? 1 : 0, width: 1)")
        code.open("if self.has\(field.base) {")
        write(&code, field.value, target(field.base))
        code.close()
        code.line("\(set) = true")
        code.close("} else if self.has\(field.base) {")
        code.indent()
        code.line("let presence\(node) = writer.bitPosition")
        code.line("try writer.write(1, width: 1)")
        deltaPayload(&code, field.value, target(field.base), other("baseline", field.base), set)
        code.line("if !\(set) { writer.rewind(to: presence\(node)) }")
        code.close("}")
    }

    private static func deltaPayload(_ code: inout Code, _ value: GesPlanValue, _ a: String, _ b: String, _ set: String) {
        let encoding = value.encoding
        let node = String(value.node)
        let width = String(encoding.width)
        switch encoding.kind {
        case .boolean:
            code.line("if \(a) != \(b) { \(set) = true }")
        case .integer, .quantized, .float, .tag, .flags:
            code.open("do {")
            code.line("let value\(node) = try Self.__wire\(node)(\(a), paths, at)")
            code.line("let held\(node) = try Self.__wire\(node)(\(b), paths, at)")
            code.open("if value\(node) != held\(node) {")
            code.line(
                encoding.deltaRange == 0
                    ? "try writer.write(value\(node), width: \(width))"
                    : "try GesWireRules.writeDeltaRange(&writer, index: value\(node), held: held\(node), maxIndex: \(encoding.maxIndex), wrapping: \(encoding.wrapping ? "true" : "false"), deltaRange: \(encoding.deltaRange), deltaWidth: \(encoding.deltaWidth), width: \(width))"
            )
            code.line("\(set) = true")
            code.close()
            code.close()
        case .list where encoding.keyed:
            code.line("if try __writeKeyed\(node)(baseline, &writer, paths, at\(level(value))) { \(set) = true }")
        case .text, .bytes, .list:
            code.open("if !(try __same\(node)(baseline, paths, at\(level(value)))) {")
            write(&code, value, a)
            code.line("\(set) = true")
            code.close()
        case .point, .vector:
            code.open("do {")
            code.line("let first\(node) = writer.bitPosition")
            code.line("try writer.write(0, width: \(value.axes.count))")
            for (index, axis) in value.axes.enumerated() {
                code.open("do {")
                code.line("var set\(axis.node) = false")
                deltaPayload(&code, axis, a + "." + member(axis.base), b + "." + member(axis.base), "set\(axis.node)")
                code.open("if set\(axis.node) {")
                code.line("writer.setBit(first\(node) + \(index))")
                code.line("\(set) = true")
                code.close()
                code.close()
            }
            for axis in GesSchemaPlanner.omitted(value) { code.line("if \(a).\(member(axis)) != 0 { \(fail("typeMismatch", value)) }") }
            code.line("if !\(set) { writer.rewind(to: first\(node)) }")
            code.close()
        case .schema:
            code.open("do {")
            code.line("let start\(node) = writer.bitPosition")
            code.open("if try \(a).__writeDelta(\(b), &writer, paths, at + \(value.node + 1), false\(nextLevel(value))) {")
            code.line("\(set) = true")
            code.close("} else {")
            code.line("    writer.rewind(to: start\(node))")
            code.line("}")
            code.close()
        case .union:
            code.open("if \(a) != \(b) {")
            write(&code, value, a)
            code.line("\(set) = true")
            code.close("} else {")
            code.indent()
            code.line("let entry\(node) = writer.bitPosition")
            code.line("try writer.write(UInt64(\(a).rawValue), width: \(width))")
            code.line("switch \(a) {")
            for alternative in value.alternatives {
                code.open("case .\(member(alternative.label)):")
                if alternative.encoding.kind == .nothing {
                    code.line("break")
                } else {
                    deltaPayload(&code, alternative, target(alternative.base), other("baseline", alternative.base), set)
                }
                code.close("")
            }
            code.line("}")
            code.line("if !\(set) { writer.rewind(to: entry\(node)) }")
            code.close("}")
        case .nothing:
            break
        }
    }

    private static func readDeltaField(_ code: inout Code, _ field: GesPlanField) {
        if !field.optional {
            readDeltaPayload(&code, field.value, target(field.base), other("baseline", field.base))
            return
        }
        let node = String(field.value.node)
        code.line("let present\(node) = try reader.read(1, path: \(path(field.value))) == 1")
        code.line("if !present\(node) && !baseline.has\(field.base) { \(fail("invalidDelta", field.value)) }")
        code.line("self.has\(field.base) = present\(node)")
        code.open("if present\(node) && baseline.has\(field.base) {")
        readDeltaPayload(&code, field.value, target(field.base), other("baseline", field.base))
        code.close("} else if present\(node) {")
        code.indent()
        read(&code, field.value, target(field.base))
        if plain(field.value) {
            code.close("} else {")
            code.line("    \(target(field.base)) = \(zero(field.value))")
            code.line("}")
        } else {
            code.close("}")
        }
    }

    private static func readDeltaPayload(_ code: inout Code, _ value: GesPlanValue, _ a: String, _ b: String) {
        let encoding = value.encoding
        let node = String(value.node)
        let width = String(encoding.width)
        switch encoding.kind {
        case .boolean:
            code.line("\(a) = !\(b)")
        case .integer where encoding.deltaRange == 0, .quantized where encoding.deltaRange == 0, .float, .tag, .flags:
            code.open("do {")
            code.line("let held\(node) = try Self.__wire\(node)(\(b), paths, at)")
            code.line("let value\(node) = try Self.__read\(node)(&reader, paths, at)")
            code.line("if value\(node) == held\(node) { \(fail("invalidDelta", value)) }")
            code.line("\(a) = Self.__value\(node)(value\(node))")
            code.close()
        case .integer, .quantized:
            code.line("\(a) = Self.__value\(node)(")
            code.line("    try GesWireRules.readDeltaRange(")
            code.line(
                "        &reader, held: try Self.__wire\(node)(\(b), paths, at), maxIndex: \(encoding.maxIndex), wrapping: \(encoding.wrapping ? "true" : "false"), deltaRange: \(encoding.deltaRange),"
            )
            code.line("        deltaWidth: \(encoding.deltaWidth), width: \(width), path: \(path(value))")
            code.line("    )")
            code.line(")")
        case .list where encoding.keyed:
            code.line("try __readKeyed\(node)(baseline, &reader, paths, at\(level(value)))")
        case .text, .bytes, .list:
            read(&code, value, a)
            code.line("if try __same\(node)(baseline, paths, at\(level(value))) { \(fail("invalidDelta", value)) }")
        case .point, .vector:
            code.open("do {")
            code.line("try reader.require(\(value.axes.count), path: \(path(value)))")
            code.line("let bits\(node) = reader.bitPosition")
            code.line("reader.seek(to: bits\(node) + \(value.axes.count))")
            code.line("var changed\(node) = false")
            for (index, axis) in value.axes.enumerated() {
                let axisNode = String(axis.node)
                code.open("do {")
                code.line("let payload\(axisNode) = reader.bitPosition")
                code.line("reader.seek(to: bits\(node) + \(index))")
                code.line("let set\(axisNode) = try reader.read(1, path: \(path(axis))) == 1")
                code.line("reader.seek(to: payload\(axisNode))")
                code.open("if set\(axisNode) {")
                readDeltaPayload(&code, axis, a + "." + member(axis.base), b + "." + member(axis.base))
                code.line("changed\(node) = true")
                code.close("} else {")
                code.line("    \(a).\(member(axis.base)) = \(b).\(member(axis.base))")
                code.line("}")
                code.close()
            }
            for axis in GesSchemaPlanner.omitted(value) { code.line("\(a).\(member(axis)) = 0") }
            code.line("if !changed\(node) { \(fail("invalidDelta", value)) }")
            code.close()
        case .schema:
            code.line("if !(try \(a).__readDelta(\(b), &reader, paths, at + \(value.node + 1), false\(nextLevel(value)))) { \(fail("invalidDelta", value)) }")
        case .union:
            code.open("do {")
            code.line("let choice\(node) = try reader.read(\(width), path: \(path(value)))")
            let last = UInt64(value.alternatives.count) - 1
            if last < full(encoding.width) { code.line("if choice\(node) > \(last) { \(fail("invalidIndex", value)) }") }
            code.line("let same\(node) = choice\(node) == UInt64(\(b).rawValue)")
            code.line("\(a) = \(nestedName(value))(rawValue: \(underlying(value.alternatives.count))(choice\(node)))!")
            code.line("switch \(a) {")
            for alternative in value.alternatives {
                code.open("case .\(member(alternative.label)):")
                if alternative.encoding.kind == .nothing {
                    code.line("if same\(node) { \(fail("invalidDelta", alternative)) }")
                } else {
                    code.open("if same\(node) {")
                    readDeltaPayload(&code, alternative, target(alternative.base), other("baseline", alternative.base))
                    code.close("} else {")
                    code.indent()
                    read(&code, alternative, target(alternative.base))
                    code.close("}")
                }
                code.close("")
            }
            code.line("}")
            code.close()
        case .nothing:
            break
        }
    }

    static func sameHelpers(_ code: inout Code, _ type: GesPlanType) {
        for field in type.fields { sameHelper(&code, field.value, type.name) }
        for field in type.fields { keyedHelpers(&code, field.value, type.name) }
        if type.fields.contains(where: { GesSchemaPlanner.hasKeyed($0.value) }) { keyedStatics(&code) }
    }

    private static func sameHelper(_ code: inout Code, _ value: GesPlanValue, _ type: String) {
        if value.encoding.kind == .text || value.encoding.kind == .bytes || value.encoding.kind == .list {
            code.blank()
            code.open("private func __same\(value.node)(_ baseline: \(type), _ paths: [String], _ at: Int\(value.depth > 0 ? ", _ level: Int" : "")) throws(GesWireError) -> Bool {")
            same(&code, value, target(value.base), other("baseline", value.base), "self", "baseline")
            code.line("return true")
            code.close()
        }
        for alternative in value.alternatives { sameHelper(&code, alternative, type) }
    }
}
