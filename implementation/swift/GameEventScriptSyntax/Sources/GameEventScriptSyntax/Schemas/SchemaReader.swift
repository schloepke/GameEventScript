// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Reads schema source text (`.gess`) into its declarations. Reading stops at the first syntax error; the rules across
/// declarations belong to the Runtime.
@_spi(Syntax) public struct GesSchemaSourceReader {
    private typealias Code = GesSchemaDiagnostic

    struct Failure: Error {
        let offset: Int
        let message: String
        var code = GesSchemaDiagnostic.syntax
    }

    private static let maximumTypeNesting = 32

    let bytes: [UInt8]
    var position = 0
    private var typeNesting = 0
    var expressionNesting = 0

    private init(_ bytes: [UInt8]) { self.bytes = bytes }

    @_spi(Syntax) public static func read(_ text: String) -> GesSchemaSourceSyntax {
        let bytes = Array(text.utf8)
        if text.unicodeScalars.count > 1_048_576 {
            return GesSchemaSourceSyntax(bytes: bytes, declarations: [], error: Code(Code.tooLarge, bytes, 0, "The schema source is too long."))
        }
        var reader = Self(bytes)
        var declarations: [GesSchemaDeclarationSyntax] = []
        do {
            if bytes.starts(with: [0xEF, 0xBB, 0xBF]) { reader.position = 3 }
            reader.skipTrivia()
            while reader.position < bytes.count {
                declarations.append(try reader.declaration())
                reader.skipTrivia()
            }
        } catch let failure as Failure {
            return GesSchemaSourceSyntax(bytes: bytes, declarations: declarations, error: Code(failure.code, bytes, failure.offset, failure.message))
        } catch {
            return GesSchemaSourceSyntax(bytes: bytes, declarations: declarations, error: Code(Code.syntax, bytes, reader.position, "Invalid schema source."))
        }
        return GesSchemaSourceSyntax(bytes: bytes, declarations: declarations, error: nil)
    }

    private mutating func declaration() throws -> GesSchemaDeclarationSyntax {
        let offset = position
        var wire: String?
        var model: String?
        var wordOffset = position
        var written = word()
        while written == "wire" || written == "model" {
            var group = "default"
            if consume(40) {
                let groupOffset = position
                group = word()
                if !GesNames.plain(group) { throw Failure(offset: groupOffset, message: "A role group is a lowercase name.", code: Code.invalidRole) }
                try expect(41)
            }
            if (written == "wire" ? wire : model) != nil {
                throw Failure(offset: wordOffset, message: "The declaration is already a \(written) root.", code: Code.duplicateRole)
            }
            if written == "wire" { wire = group } else { model = group }
            skipSpaces()
            wordOffset = position
            written = word()
        }
        var isAbstract = false
        if written == "abstract" && position < bytes.count && (bytes[position] == 32 || bytes[position] == 9) {
            if wire != nil || model != nil {
                throw Failure(offset: offset, message: "An abstract schema only lends its fields and takes no role.", code: Code.invalidRole)
            }
            isAbstract = true
            skipSpaces()
            wordOffset = position
            written = word()
            if written != "schema" { throw Failure(offset: wordOffset, message: "Expected 'schema' after 'abstract'.") }
        }
        if wire != nil || model != nil {
            if written == "constant" || written == "parameter" || written == "tags" {
                throw Failure(offset: offset, message: "Only a schema, message or message set can be a root.", code: Code.invalidRole)
            }
            if model != nil && (written == "message" || written == "messages") {
                throw Failure(offset: offset, message: "A model root is a schema.", code: Code.invalidRole)
            }
        } else if !written.isEmpty && !["constant", "parameter", "schema", "message", "messages", "tags"].contains(written) && atRoleEnd() {
            throw Failure(offset: offset, message: "'\(written)' is no role; a root is marked wire or model.", code: Code.invalidRole)
        }
        var result = try declaration(offset: offset, wordOffset: wordOffset, word: written)
        result.isAbstract = isAbstract
        result.wireGroup = wire
        result.modelGroup = model
        return result
    }

    /// Whether an unknown word stands where a role does: before a group or a declaration keyword.
    private mutating func atRoleEnd() -> Bool {
        if position < bytes.count && bytes[position] == 40 { return true }
        let start = position
        skipSpaces()
        let role = position > start && (atWord("schema") || atWord("message") || atWord("messages") || atWord("abstract"))
        position = start
        return role
    }

    private mutating func declaration(offset: Int, wordOffset: Int, word written: String) throws -> GesSchemaDeclarationSyntax {
        var result = GesSchemaDeclarationSyntax()
        result.offset = offset
        switch written {
        case "constant":
            skipSpaces()
            if !consume(36) { throw failure("Expected a constant name.") }
            result.kind = .constant
            result.name = try name(GesNames.plain, "constant name")
            skipSpaces()
            try expectWord("be")
            skipSpaces()
            result.value = try literal()
        case "parameter":
            skipSpaces()
            result.kind = .parameter
            result.name = try name(GesNames.plain, "parameter name")
            skipSpaces()
            try expect(58)
            skipSpaces()
            let start = position
            let type = try self.type()
            if type.kind != .number && type.kind != .percentage && type.kind != .quantity {
                throw Failure(offset: start, message: "A parameter is a Number, Percentage or Quantity.")
            }
            let constraints = type.constraints
            if constraints.integer || constraints.lower != nil || constraints.step != nil || constraints.delta != nil {
                throw Failure(offset: start, message: "A parameter has only a kind and a unit; its value comes with the binding.")
            }
            result.type = type
        case "schema":
            result.kind = .schema
            result.name = try typeName()
            skipSpaces()
            try expectWord("as")
            skipTrivia()
            try expect(123)
            skipSeparators()
            var sequence = 0
            while !consume(125) {
                if let declared = try letDeclaration() {
                    result.lets.append(declared)
                } else if var with = try withEntry(result.fields.count) {
                    with.sequence = sequence
                    result.withs.append(with)
                } else {
                    var entry = try field(allowUnlabeled: false, allowDefault: true)
                    entry.sequence = sequence
                    if entry.computed == nil { result.fields.append(entry) } else { result.computed.append(entry) }
                }
                try endEntry(125)
                skipSeparators()
                sequence += 1
            }
        case "message":
            skipSpaces()
            result.kind = .message
            result.name = try name(GesNames.type, "message name")
            skipSpaces()
            try expect(40)
            skipTrivia()
            while !consume(41) {
                if !result.fields.isEmpty {
                    try expect(44)
                    skipTrivia()
                }
                result.fields.append(try field(allowUnlabeled: true, allowDefault: false))
                skipTrivia()
            }
            skipSpaces()
            if atWord("tags") {
                _ = word()
                skipSpaces()
                result.tags = try tagSet()
            }
        case "messages":
            result.kind = .messages
            result.name = try typeName()
            skipSpaces()
            try expectWord("as")
            skipTrivia()
            try expect(40)
            repeat {
                skipTrivia()
                result.memberOffsets.append(position)
                result.members.append(try name(GesNames.type, "message name"))
                skipTrivia()
            } while consume(44)
            try expect(41)
        case "tags":
            result.kind = .tagSet
            result.name = try typeName()
            skipSpaces()
            try expectWord("as")
            skipSpaces()
            result.tags = try tagSet()
        default:
            throw Failure(offset: wordOffset, message: "Expected constant, parameter, tags, schema, message or messages.")
        }
        return result
    }

    /// `let name be value`; a field named let is followed by its colon instead of a name.
    private mutating func letDeclaration() throws -> GesSchemaLetSyntax? {
        let offset = position
        if !atWord("let") { return nil }
        _ = word()
        skipSpaces()
        if position >= bytes.count || !GesNames.lower(bytes[position]) {
            position = offset
            return nil
        }
        let letName = try name(GesNames.plain, "let name")
        skipSpaces()
        try expectWord("be")
        skipSpaces()
        if position < bytes.count && GesNames.lower(bytes[position]) { throw failure("A let is a literal or a constant.") }
        return GesSchemaLetSyntax(name: letName, value: try value(), offset: offset)
    }

    /// `with :Name [all optional] [without (a, b)]`; a field named with is followed by its colon instead of a type name.
    private mutating func withEntry(_ fieldsBefore: Int) throws -> GesSchemaWithSyntax? {
        let offset = position
        if !atWord("with") { return nil }
        _ = word()
        skipSpaces()
        if position == offset + 4 || position + 1 >= bytes.count || bytes[position] != 58 || !GesNames.upper(bytes[position + 1]) {
            position = offset
            return nil
        }
        var result = GesSchemaWithSyntax()
        result.offset = offset
        result.position = fieldsBefore
        result.name = try typeName()
        skipSpaces()
        if atWord("all") {
            _ = word()
            skipSpaces()
            try expectWord("optional")
            result.allOptional = true
            skipSpaces()
        }
        if atWord("without") {
            _ = word()
            skipSpaces()
            try expect(40)
            repeat {
                skipTrivia()
                result.withoutOffsets.append(position)
                result.without.append(try name(GesNames.plain, "field name"))
                skipTrivia()
            } while consume(44)
            try expect(41)
        }
        return result
    }

    private mutating func field(allowUnlabeled: Bool, allowDefault: Bool) throws -> GesSchemaFieldSyntax {
        let offset = position
        var unlabeled = false
        if allowUnlabeled && consume(95) {
            unlabeled = true
            skipSpaces()
        }
        let fieldName = try name(GesNames.plain, "field name")
        skipSpaces()
        try expect(58)
        skipSpaces()
        let alternatives = try union()
        skipSpaces()
        var marker = GesSchemaFieldMarker.none
        if atWord("optional") {
            _ = word()
            marker = .optional
        } else if atWord("key") {
            _ = word()
            marker = .key
        }
        var fallback: GesSchemaDefaultSyntax?
        skipSpaces()
        if atWord("computed") {
            if !allowDefault { throw failure("A message argument is not computed.") }
            _ = word()
            skipSpaces()
            try expectWord("by")
            skipTrivia()
            expressionNesting = 0
            let expression = try readExpression()
            var result = GesSchemaFieldSyntax(name: fieldName, unlabeled: unlabeled, alternatives: alternatives, marker: marker, defaultValue: nil, offset: offset)
            result.computed = expression
            return result
        }
        if allowDefault && atWord("default") {
            let start = position
            _ = word()
            skipSpaces()
            fallback = try defaultValue(start)
        }
        return GesSchemaFieldSyntax(name: fieldName, unlabeled: unlabeled, alternatives: alternatives, marker: marker, defaultValue: fallback, offset: offset)
    }

    /// A tag, `true`, `false`, or a value: a number, a constant, or the name of a let or a sibling field.
    private mutating func defaultValue(_ offset: Int) throws -> GesSchemaDefaultSyntax {
        if consume(35) { return GesSchemaDefaultSyntax(tag: try name(GesNames.plain, "tag name"), offset: offset) }
        for written in ["true", "false"] where atWord(written) {
            _ = word()
            return GesSchemaDefaultSyntax(boolean: written == "true", offset: offset)
        }
        return GesSchemaDefaultSyntax(value: try value(), offset: offset)
    }

    private mutating func union() throws -> [GesSchemaTypeSyntax] {
        var alternatives = [try alternative()]
        skipSpaces()
        while consume(124) {
            skipTrivia()
            alternatives.append(try alternative())
            skipSpaces()
        }
        return alternatives
    }

    // A type with an optional label in front, as in `bind: :Loadout_UnitBind`; a type itself starts with ':'.
    private mutating func alternative() throws -> GesSchemaTypeSyntax {
        guard position < bytes.count, GesNames.lower(bytes[position]) else { return try type() }
        let offset = position
        let label = try name(GesNames.plain, "alternative label")
        skipSpaces()
        try expect(58)
        skipSpaces()
        var result = try type()
        result.label = label
        result.labelOffset = offset
        return result
    }

    private mutating func type() throws -> GesSchemaTypeSyntax {
        var result = GesSchemaTypeSyntax()
        result.offset = position
        try expect(58)
        let written = try name(GesNames.type, "type name")
        skipSpaces()
        switch GesSchemaTypeNames.kind(written) {
        case .boolean: result.kind = .boolean
        case .number:
            result.kind = .number
            result.constraints = try constraints()
        case .percentage:
            result.kind = .percentage
            result.constraints = try constraints()
        case .quantity:
            guard let unit = try unit() else { throw failure("Expected a unit in parentheses.") }
            skipSpaces()
            result.unit = unit
            result.kind = unit == .none ? .number : .quantity
            result.constraints = try constraints()
        case .point, .vector:
            result.kind = written == "Point" ? .point : .vector
            if position < bytes.count && bytes[position] == 40 { result.unit = try unit() ?? .none }
            skipSpaces()
            if consume(123) {
                var axes: [GesSchemaAxisSyntax] = []
                skipSeparators()
                while !consume(125) {
                    let axisOffset = position
                    let axis = word()
                    if axis != "x" && axis != "y" && axis != "z" { throw Failure(offset: axisOffset, message: "Expected the axis x, y or z.") }
                    skipSpaces()
                    axes.append(GesSchemaAxisSyntax(axis: axis, constraints: try constraints(), offset: axisOffset))
                    try endEntry(125)
                    skipSeparators()
                }
                if axes.isEmpty { throw Failure(offset: result.offset, message: "An axis block names at least one axis.") }
                result.axes = axes
            }
        case .tag:
            result.kind = .tag
            if position < bytes.count && bytes[position] == 40 { result.tags = try tagSet() } else { try maximum(&result) }
        case .flags:
            result.kind = .flags
            result.tags = try tagSet()
        case .text, .bytes:
            result.kind = written == "Text" ? .text : .bytes
            try maximum(&result)
        case .list:
            result.kind = .list
            if consume(40) {
                // Every pass over a type recurses, so its nesting has a limit below any stack.
                typeNesting += 1
                if typeNesting > Self.maximumTypeNesting { throw Failure(offset: result.offset, message: "List types nest at most \(Self.maximumTypeNesting) levels.", code: Code.cycle) }
                skipTrivia()
                result.element = try union()
                skipTrivia()
                try expect(41)
                typeNesting -= 1
                skipSpaces()
                try maximum(&result)
                try depth(&result)
            }
        case .map: result.kind = .map
        case .range: result.kind = .range
        case .series: result.kind = .series
        case .dice:
            result.kind = .dice
            try maximum(&result)
            result.constraints = try constraints()
        case .message: result.kind = .message
        case .handler: result.kind = .handler
        case .nothing: result.kind = .nothing
        default:
            result.kind = .reference
            result.name = written
        }
        return result
    }

    private mutating func unit() throws -> GesQuantityUnit? {
        if !consume(40) { return nil }
        let start = position
        guard let end = bytes[start...].firstIndex(of: 41) else { throw Failure(offset: start, message: "Expected ')'.") }
        var written: [UInt8] = []
        var index = start
        while index < end {
            if bytes[index] == 0xC3 && index + 1 < end && bytes[index + 1] == 0xB7 {
                written.append(47)
                index += 2
                continue
            }
            if bytes[index] != 32 && bytes[index] != 9 { written.append(bytes[index]) }
            index += 1
        }
        position = end + 1
        guard let unit = GesQuantityUnit(quantityName: String(decoding: written, as: UTF8.self)) else {
            throw Failure(offset: start, message: "A type names a base unit; a prefixed spelling such as kN is only for literals.")
        }
        return unit
    }

    /// A whole number, or the name of a let that holds one.
    private mutating func maximum(_ type: inout GesSchemaTypeSyntax) throws {
        if !atWord("max") { return }
        _ = word()
        skipSpaces()
        if position < bytes.count && GesNames.lower(bytes[position]) {
            type.maxOffset = position
            type.maxName = try name(GesNames.plain, "let name")
            skipSpaces()
            return
        }
        // Up to 12 decimal digits without a leading zero; a single `_` may separate two digits, as in number literals.
        let start = position
        var value: Int64 = 0
        var digits = 0
        while position < bytes.count {
            let byte = bytes[position]
            if GesNames.digit(byte) {
                value = value * 10 + Int64(byte - 48)
                digits += 1
            } else if byte != 95 || digits == 0 || position + 1 >= bytes.count || !GesNames.digit(bytes[position + 1]) {
                break
            }
            position += 1
            if digits > 12 { break }
        }
        if digits == 0 || digits > 12 || (bytes[start] == 48 && digits > 1) {
            throw Failure(offset: start, message: "Expected a whole number.")
        }
        skipSpaces()
        type.max = value
        type.maxOffset = start
    }

    /// The depth of a recursive List: 1 to 999 without a leading zero.
    private mutating func depth(_ type: inout GesSchemaTypeSyntax) throws {
        if !atWord("depth") { return }
        type.depthOffset = position
        _ = word()
        skipSpaces()
        let start = position
        var value = 0
        while position < bytes.count && GesNames.digit(bytes[position]) && position - start < 3 {
            value = value * 10 + Int(bytes[position] - 48)
            position += 1
        }
        if position == start || bytes[start] == 48 || (position < bytes.count && GesNames.digit(bytes[position])) {
            throw Failure(offset: start, message: "Expected a depth from 1 to 999.")
        }
        skipSpaces()
        type.depth = value
    }

    private mutating func tagSet() throws -> [String] {
        try expect(40)
        var tags: [String] = []
        repeat {
            skipTrivia()
            try expect(35)
            tags.append(try name(GesNames.plain, "tag name"))
            skipTrivia()
        } while consume(44)
        try expect(41)
        return tags
    }

    private mutating func constraints() throws -> GesSchemaConstraintsSyntax {
        var result = GesSchemaConstraintsSyntax()
        if atWord("integer") {
            _ = word()
            result.integer = true
            skipSpaces()
        }
        if atWord("wrapping") || atWord("between") {
            if atWord("wrapping") {
                _ = word()
                result.wrapping = true
                skipSpaces()
            }
            try expectWord("between")
            skipSpaces()
            result.lower = try value()
            skipSpaces()
            try expectWord("and")
            skipSpaces()
            result.upper = try value()
            skipSpaces()
        }
        if atWord("step") {
            _ = word()
            skipSpaces()
            result.step = try value()
            skipSpaces()
        }
        if atWord("delta") {
            _ = word()
            skipSpaces()
            try expectWord("within")
            skipSpaces()
            result.delta = try value()
            skipSpaces()
        }
        return result
    }

    /// A literal, a constant, or the name of a let or parameter.
    private mutating func value() throws -> GesSchemaValueSyntax {
        let offset = position
        if consume(36) {
            var result = GesSchemaValueSyntax()
            result.offset = offset
            result.constant = try name(GesNames.plain, "constant name")
            return result
        }
        if position < bytes.count && GesNames.lower(bytes[position]) {
            var result = GesSchemaValueSyntax()
            result.offset = offset
            result.name = try name(GesNames.plain, "name")
            return result
        }
        return try literal()
    }

    // A number with its suffix. The value stays an exact decimal, since quantization and the hash depend on it.
    private mutating func literal() throws -> GesSchemaValueSyntax {
        let offset = position
        var end = position
        while end < bytes.count && !isLiteralEnd(end) { end += 1 }
        if end == offset { throw failure("Expected a number.") }
        guard let read = GesSchemaLiterals.read(Array(bytes[offset..<end])) else {
            throw Failure(offset: offset, message: "Expected an exact finite number with a unit known to literals.")
        }
        position = end
        var result = GesSchemaValueSyntax()
        result.value = read.value
        result.unit = read.unit
        result.isPercent = read.isPercent
        result.text = String(decoding: bytes[offset..<end], as: UTF8.self)
        result.offset = offset
        return result
    }

    func isLiteralEnd(_ index: Int) -> Bool {
        switch bytes[index] {
        case 32, 9, 13, 10, 44, 40, 41, 123, 125, 124, 35: return true
        case 47: return index + 1 < bytes.count && bytes[index + 1] == 47
        default: return false
        }
    }

    private mutating func endEntry(_ closing: UInt8) throws {
        skipSpacesAndComments()
        if position == bytes.count { throw failure("Expected '\(Character(Unicode.Scalar(closing)))'.") }
        let byte = bytes[position]
        if byte == 44 || byte == 13 || byte == 10 || byte == closing { return }
        throw failure("Expected a comma, a line break or '\(Character(Unicode.Scalar(closing)))'.")
    }

    private mutating func skipSeparators() {
        skipTrivia()
        while consume(44) { skipTrivia() }
    }

    mutating func skipSpaces() {
        while position < bytes.count && (bytes[position] == 32 || bytes[position] == 9) { position += 1 }
    }

    mutating func skipSpacesAndComments() {
        skipSpaces()
        if atComment { skipLine() }
    }

    mutating func skipLine() {
        while position < bytes.count && bytes[position] != 13 && bytes[position] != 10 { position += 1 }
    }

    mutating func skipTrivia() {
        while position < bytes.count {
            let byte = bytes[position]
            if byte == 32 || byte == 9 || byte == 13 || byte == 10 {
                position += 1
            } else if atComment {
                skipLine()
            } else {
                return
            }
        }
    }

    var atComment: Bool { position + 1 < bytes.count && bytes[position] == 47 && bytes[position + 1] == 47 }

    mutating func consume(_ byte: UInt8) -> Bool {
        guard position < bytes.count && bytes[position] == byte else { return false }
        position += 1
        return true
    }

    mutating func expect(_ byte: UInt8) throws {
        if !consume(byte) { throw failure("Expected '\(Character(Unicode.Scalar(byte)))'.") }
    }

    func atWord(_ written: String) -> Bool {
        let scalars = Array(written.utf8)
        guard position + scalars.count <= bytes.count, bytes[position..<(position + scalars.count)].elementsEqual(scalars) else { return false }
        let end = position + scalars.count
        return end == bytes.count || !GesNames.alnum(bytes[end])
    }

    mutating func expectWord(_ written: String) throws {
        if !atWord(written) { throw failure("Expected '\(written)'.") }
        position += written.utf8.count
    }

    mutating func word() -> String {
        let start = position
        while position < bytes.count && GesNames.alnum(bytes[position]) { position += 1 }
        return String(decoding: bytes[start..<position], as: UTF8.self)
    }

    private mutating func typeName() throws -> String {
        skipSpaces()
        try expect(58)
        return try name(GesNames.type, "type name")
    }

    mutating func name(_ valid: (String) -> Bool, _ description: String) throws -> String {
        let start = position
        var written = word()
        if position > start && GesNames.upper(bytes[start]) {
            let end = GesNames.upperNameEnd(bytes, position)
            if end != position {
                position = end
                written = String(decoding: bytes[start..<end], as: UTF8.self)
            }
        }
        if !valid(written) { throw Failure(offset: start, message: "Expected a \(description).") }
        return written
    }

    func failure(_ message: String) -> Failure { Failure(offset: position, message: message) }
}

/// Reads schema literals exactly, with the number grammar and spelling table of GES literals.
@_spi(Syntax) public enum GesSchemaLiterals {
    @_spi(Syntax) public struct Literal: Sendable {
        @_spi(Syntax) public let value: GesSchemaDecimal
        @_spi(Syntax) public let unit: GesQuantityUnit
        @_spi(Syntax) public let isPercent: Bool
    }

    @_spi(Syntax) public static func read(_ text: String) -> Literal? { read(Array(text.utf8)) }

    static func read(_ bytes: [UInt8]) -> Literal? {
        var unit = GesQuantityUnit.none
        var exponent = 0
        var factor: Int64 = 1
        var percent = false
        var last = bytes.count
        if last > 0 && bytes[last - 1] == 37 {
            percent = true
            exponent = -2
            last -= 1
        } else {
            for suffix in GesQuantityUnit.suffixes {
                let spelling = Array(suffix.text.utf8)
                if last > spelling.count && bytes[(last - spelling.count)..<last].elementsEqual(spelling) {
                    unit = suffix.unit
                    exponent = suffix.exponent
                    factor = Int64(suffix.factor)
                    last -= spelling.count
                    break
                }
            }
        }
        var position = 0
        var negative = false
        if position < last && (bytes[position] == 45 || bytes[position] == 43) {
            negative = bytes[position] == 45
            position += 1
        }
        var magnitude: UInt64 = 0
        let limit: UInt64 = negative ? 1 << 63 : UInt64(Int64.max)
        var digits = 0
        var afterPoint = false
        var previousDigit = false
        while position < last {
            let byte = bytes[position]
            if GesNames.digit(byte) {
                if magnitude > (limit - UInt64(byte - 48)) / 10 {
                    // Trailing zeros beyond the range keep the value; other digits are too exact for a schema.
                    if byte != 48 { return nil }
                    if !afterPoint { exponent += 1 }
                } else {
                    magnitude = magnitude * 10 + UInt64(byte - 48)
                    if afterPoint { exponent -= 1 }
                }
                digits += 1
                previousDigit = true
            } else if byte == 95 && previousDigit && position + 1 < last && GesNames.digit(bytes[position + 1]) {
                previousDigit = false
            } else if byte == 46 && !afterPoint && previousDigit {
                afterPoint = true
                previousDigit = false
            } else if (byte == 101 || byte == 69) && previousDigit {
                break
            } else {
                return nil
            }
            position += 1
        }
        if digits == 0 || (!previousDigit && position == last) { return nil }
        if position < last {
            position += 1
            var exponentNegative = false
            if position < last && (bytes[position] == 45 || bytes[position] == 43) {
                exponentNegative = bytes[position] == 45
                position += 1
            }
            if position == last || last - position > 4 { return nil }
            var written = 0
            while position < last {
                guard GesNames.digit(bytes[position]) else { return nil }
                written = written * 10 + Int(bytes[position] - 48)
                position += 1
            }
            exponent += exponentNegative ? -written : written
        }
        if factor != 1 {
            if magnitude > limit / UInt64(factor) { return nil }
            magnitude *= UInt64(factor)
        }
        return Literal(value: GesSchemaDecimal(Int64(truncatingIfNeeded: negative ? 0 &- magnitude : magnitude), exponent), unit: unit, isPercent: percent)
    }
}
