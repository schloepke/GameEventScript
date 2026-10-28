// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

struct GesLiteralParser {
    static func sourceText(_ text: String) -> String? {
        var parser = Self(bytes: Array(text.utf8))
        let value = parser.quoted()
        return parser.position == parser.bytes.count ? value : nil
    }

    private let bytes: [UInt8]
    private var position = 0, items = 0
    private var limit: String?
    private var limitPosition = 0
    private var nodes: [GesLiteralNode] = []
    // Absent outside a running Program: Program Record constructors are then unknown types.
    private var state: GesVirtualMachine?
    // Document mode adds comments, line-break separators, trailing commas, duplicate-key errors and error positions.
    private var document = false
    private var error: String?
    private var errorPosition = 0

    static func evaluate(_ input: GesValue, strict: Bool, context: GesContext, state: GesVirtualMachine, destination: Int) throws {
        let (value, nodes) = parse(input, strict: strict, context: context, state: state)
        if nodes.isEmpty { state.setValue(destination, value) } else { try GesLiteralEvaluation(value, nodes: nodes, state: state, context: context, destination: destination).run() }
    }

    static func parse(_ input: GesValue, strict: Bool, context: GesContext, state: GesVirtualMachine) -> (GesValue, [GesLiteralNode]) {
        guard input.kind == .text, let text = input.textValue else { return (.nothing, []) }
        if text.unicodeScalars.count > 1_048_576 {
            context.budget.exhaust("MaxLiteralInputScalars", 1_048_576)
            return (.nothing, [])
        }
        var parser = Self(bytes: Array(text.utf8))
        parser.state = state
        let result = parser.recognize()
        if let limit = parser.limit {
            context.budget.exhaust(limit, limitValue(limit))
            return (.nothing, [])
        }
        if let result { return (result, parser.nodes) }
        return (strict ? .nothing : input, [])
    }

    /// Recognizes data literals outside a running Program, for the public GesValue API; nil when the input is
    /// not a complete data literal. Throws GesLiteralLimitError when a fixed parsing bound is exceeded.
    static func parseData(_ text: String) throws -> GesValue? {
        if text.unicodeScalars.count > 1_048_576 { throw GesLiteralLimitError(limitName: "MaxLiteralInputScalars", limit: 1_048_576) }
        var parser = Self(bytes: Array(text.utf8))
        let result = parser.recognize()
        if let limit = parser.limit { throw GesLiteralLimitError(limitName: limit, limit: limitValue(limit)) }
        guard let result else { return nil }
        return parser.nodes.isEmpty ? result : try GesLiteralEvaluation.evaluate(result, nodes: parser.nodes)
    }

    /// Reads GES document text into its evaluated Map or Record root and optional schema reference. Throws
    /// GesDocumentError for invalid text or an exceeded literal parsing limit.
    static func parseDocument(_ text: String) throws -> (GesValue, GesDocumentSchema?) {
        let bytes = Array(text.utf8)
        if text.unicodeScalars.count > 1_048_576 { throw documentLimit(bytes, "MaxLiteralInputScalars", 1_048_576, 0) }
        var parser = Self(bytes: bytes)
        parser.document = true
        let (root, schema) = parser.readDocument()
        if let limit = parser.limit { throw documentLimit(bytes, limit, limitValue(limit), parser.limitPosition) }
        guard let root else { throw documentError(bytes, parser.error ?? GesDocumentErrorCodes.invalidLiteral, parser.errorPosition) }
        let map: GesValue
        do {
            let evaluated = try GesLiteralEvaluation.evaluateDocument(root, nodes: parser.nodes)
            guard let value = evaluated.value else { throw documentError(bytes, GesDocumentErrorCodes.invalidLiteral, evaluated.invalidOffset) }
            map = value
        } catch let error as GesLiteralLimitError {
            throw documentLimit(bytes, error.limitName, error.limit, error.offset)
        }
        guard let schema, schema.kind == .type else { return (map, schema) }
        return (.record(typeName: schema.name, fields: map.asMap!), schema)
    }

    private static func limitValue(_ limit: String) -> Int { limit == "MaxLiteralDepth" ? 64 : 65536 }

    private static func documentError(_ bytes: [UInt8], _ code: String, _ offset: Int) -> GesDocumentError {
        let (line, column) = location(bytes, offset)
        return GesDocumentError(code: code, line: line, column: column)
    }

    private static func documentLimit(_ bytes: [UInt8], _ limit: String, _ value: Int, _ offset: Int) -> GesDocumentError {
        let (line, column) = location(bytes, offset)
        return GesDocumentError(code: GesDocumentErrorCodes.limitExceeded, line: line, column: column, limitName: limit, limit: value)
    }

    // 1-based line and column in Unicode scalars; CRLF is one line break and a leading byte order mark is not counted.
    private static func location(_ bytes: [UInt8], _ offset: Int) -> (Int, Int) {
        var line = 1
        var column = 1
        var index = bytes.starts(with: [0xEF, 0xBB, 0xBF]) ? 3 : 0
        while index < offset && index < bytes.count {
            let byte = bytes[index]
            index += 1
            if byte == 10 || byte == 13 {
                if byte == 13 && index < bytes.count && bytes[index] == 10 && index < offset { index += 1 }
                line += 1
                column = 1
            } else if byte & 0xC0 != 0x80 {
                column += 1
            }
        }
        return (line, column)
    }

    private mutating func recognize() -> GesValue? {
        let result = value(depth: 0)
        whitespace()
        return limit == nil && position == bytes.count ? result : nil
    }

    private mutating func readDocument() -> (GesValue?, GesDocumentSchema?) {
        if bytes.starts(with: [0xEF, 0xBB, 0xBF]) { position = 3 }
        whitespace()
        var schema: GesDocumentSchema?
        if isSchemaHeader() {
            guard let header = schemaHeader() else { return (nil, nil) }
            schema = header
            spacesAndComments()
            if position < bytes.count && !atLineBreak { return (fail(GesDocumentErrorCodes.unexpectedToken, position), schema) }
            whitespace()
        }
        let record = schema?.kind == .type
        var keys: [String] = []
        var values: [GesValue] = []
        var seen: Set<[Unicode.Scalar]> = []
        while position < bytes.count {
            let entryStart = position
            if isSchemaHeader() { return (fail(GesDocumentErrorCodes.misplacedSchemaHeader, entryStart), schema) }
            if items >= 65536 { return (setLimit("MaxLiteralItems", entryStart), schema) }
            items += 1
            guard let key = key() else { return (fail(bytes[entryStart] == 34 || bytes[entryStart] == 39 ? GesDocumentErrorCodes.unclosedText : GesDocumentErrorCodes.invalidKey, entryStart), schema) }
            if record && !GesNames.plain(key) { return (fail(GesDocumentErrorCodes.invalidKey, entryStart), schema) }
            if !seen.insert(Array(key.unicodeScalars)).inserted { return (fail(GesDocumentErrorCodes.duplicateKey, entryStart), schema) }
            spacesAndComments()
            if !consume(58) { return (fail(GesDocumentErrorCodes.missingColon, position), schema) }
            guard let value = entryValue(depth: 0) else { return (nil, schema) }
            keys.append(key)
            values.append(value)
            spacesAndComments()
            if position == bytes.count { break }
            if !consume(44) && !atLineBreak { return (fail(GesDocumentErrorCodes.unexpectedToken, position), schema) }
            whitespace()
            if position < bytes.count && bytes[position] == 44 { return (fail(GesDocumentErrorCodes.unexpectedToken, position), schema) }
        }
        return (node("@map", keys, values), schema)
    }

    private func isSchemaHeader() -> Bool {
        starts(":Schema") && (position + 7 == bytes.count || !Self.nameByte(bytes[position + 7]))
    }

    private mutating func schemaHeader() -> GesDocumentSchema? {
        position += 7
        whitespace()
        let open = position
        if !consume(40) {
            fail(GesDocumentErrorCodes.invalidSchemaHeader, position)
            return nil
        }
        whitespace()
        let start = position
        var schema: GesDocumentSchema?
        if position == bytes.count {
            fail(GesDocumentErrorCodes.unclosedBracket, open)
            return nil
        }
        if consume(58) {
            if let name = name(), GesDocumentSchema.isRecordTypeName(name) { schema = try? GesDocumentSchema(kind: .type, name: name) }
        } else if bytes[position] == 34 || bytes[position] == 39 {
            guard let path = quoted() else {
                fail(GesDocumentErrorCodes.unclosedText, start)
                return nil
            }
            if !path.isEmpty { schema = try? GesDocumentSchema(kind: .file, name: path) }
        }
        guard let schema else {
            fail(GesDocumentErrorCodes.invalidSchemaHeader, start)
            return nil
        }
        switch separator(41, open) {
        case 1: return schema
        case 0:
            fail(GesDocumentErrorCodes.invalidSchemaHeader, position)
            return nil
        default: return nil
        }
    }

    // The value after a Map or root key: key-only entries are true; otherwise the value starts on the key's line.
    private mutating func entryValue(depth: Int) -> GesValue? {
        if !document {
            whitespace()
            return position < bytes.count && (bytes[position] == 44 || bytes[position] == 93) ? .boolean(true) : value(depth: depth)
        }
        spacesAndComments()
        return position == bytes.count || atLineBreak || bytes[position] == 44 || bytes[position] == 93 ? .boolean(true) : value(depth: depth)
    }

    private mutating func value(depth: Int) -> GesValue? {
        whitespace()
        if position == bytes.count { return fail(GesDocumentErrorCodes.unexpectedToken, position) }
        let valueStart = position
        if bytes[position] == 34 || bytes[position] == 39 {
            guard let text = quoted() else { return fail(GesDocumentErrorCodes.unclosedText, valueStart) }
            return .text(text)
        }
        if starts(":Vector") { return spatial(depth: depth, point: false) }
        if starts(":Point") { return spatial(depth: depth, point: true) }
        if bytes[position] == 58 && !starts(":Dice[") {
            let saved = position
            if let result = typed(depth: depth) { return result }
            if document && (error != nil || limit != nil) { return nil }
            position = saved
        }
        let dice = starts(":Dice")
        if dice {
            position += 5
            whitespace()
            if position == bytes.count || bytes[position] != 91 { return fail(GesDocumentErrorCodes.invalidLiteral, valueStart) }
        }
        if bytes[position] == 91 {
            if depth >= 64 { return setLimit("MaxLiteralDepth", position) }
            return dice ? readDice(formStart: valueStart) : collection(depth: depth + 1)
        }
        let start = position
        while position < bytes.count && !Self.space(bytes[position]) && ![44, 93, 91, 41].contains(bytes[position]) { position += 1 }
        if document { endTokenAtComment(start) }
        if start == position { return fail(GesDocumentErrorCodes.unexpectedToken, position) }
        let token = String(decoding: bytes[start..<position], as: UTF8.self)
        switch token {
        case "true": return .boolean(true)
        case "false": return .boolean(false)
        case "nothing": return .nothing
        default: break
        }
        if token.hasPrefix("#") {
            let tag = String(token.dropFirst())
            guard GesNames.plain(tag), let value = try? GesValue.tag(tag) else { return fail(GesDocumentErrorCodes.invalidLiteral, start) }
            return value
        }
        return GesTextNumberCast.read(token, percentage: token.hasSuffix("%"), allowGrouping: false) ?? tokenFailure(start, position)
    }

    private mutating func node(_ type: String, _ labels: [String], _ arguments: [GesValue], _ constructor: GesBindingSegment.Entry? = nil, offset: Int = 0) -> GesValue {
        let index = nodes.count
        nodes.append(.init(type: type, labels: labels, arguments: arguments, constructor: constructor, offset: offset))
        return .record(typeName: "@literal", entries: [.init(key: "id", value: .integer(Int64(index)))])
    }

    private mutating func name() -> String? {
        let start = position
        while position < bytes.count && Self.nameByte(bytes[position]) { position += 1 }
        return start == position ? nil : String(decoding: bytes[start..<position], as: UTF8.self)
    }

    private static func nameByte(_ byte: UInt8) -> Bool { GesNames.lower(byte) || (65...90).contains(byte) || GesNames.digit(byte) || byte == 95 }

    private mutating func typed(depth: Int) -> GesValue? {
        let formStart = position
        if depth >= 64 { return setLimit("MaxLiteralDepth", formStart) }
        position += 1
        guard let name = name(), GesNames.type(name) else { return fail(GesDocumentErrorCodes.invalidLiteral, formStart) }
        let type = name.lowercased()
        let builtin = GesDataConstructionShape.types.contains(type) && name == type.prefix(1).uppercased() + type.dropFirst()
        let constructor = builtin ? nil : state?.linked?.recordsByName[name]
        if !builtin && constructor == nil { return fail(GesDocumentErrorCodes.unknownType, formStart) }
        whitespace()
        let open = position
        // `:Dice [...]` with whitespace is the Dice result literal, read after this form fails.
        guard consume(40) else { return type == "dice" ? nil : fail(GesDocumentErrorCodes.invalidLiteral, formStart) }
        whitespace()
        if ["handler", "message"].contains(type), position < bytes.count, (65...90).contains(bytes[position]) || starts("initialization") || starts("undeliverable") {
            return messageForm(depth: depth + 1, handler: type == "handler", formStart: formStart, outerOpen: open)
        }
        var labels: [String] = []
        var args: [GesValue] = []
        if document && position == bytes.count { return fail(GesDocumentErrorCodes.unclosedBracket, open) }
        if !consume(41) {
            while position < bytes.count {
                if !item() { return nil }
                let saved = position
                var label = key()
                inline()
                if label == nil || !consume(58) {
                    label = "_"
                    position = saved
                } else if !GesNames.plain(label!) {
                    return fail(GesDocumentErrorCodes.invalidKey, saved)
                } else if labels.contains(label!) {
                    return fail(GesDocumentErrorCodes.duplicateKey, saved)
                }
                whitespace()
                if document && position == bytes.count { return fail(GesDocumentErrorCodes.unclosedBracket, open) }
                let argument: GesValue?
                if type == "series", args.isEmpty, position < bytes.count, GesNames.lower(bytes[position]) {
                    let valueStart = position
                    let kind = self.name()
                    if kind == "fibonacci" || kind == "factorial" {
                        argument = .text(kind!)
                    } else {
                        position = valueStart
                        argument = value(depth: depth + 1)
                    }
                } else {
                    argument = value(depth: depth + 1)
                }
                guard let argument else { return nil }
                labels.append(label!)
                args.append(argument)
                let next = separator(41, open)
                if next < 0 { return nil }
                if next > 0 { break }
            }
            if position == 0 || bytes[position - 1] != 41 { return nil }
        }
        if builtin && GesDataConstructionShape.positions(type, labels) == nil { return fail(GesDocumentErrorCodes.invalidLiteral, formStart) }
        if let constructor, let state {
            let allowed = constructor.argumentNames.map(state.text)
            if labels.filter({ $0 == "_" }).count > allowed.filter({ $0 == "_" }).count || labels.contains(where: { $0 != "_" && !allowed.contains($0) }) { return nil }
        }
        return node(type, labels, args, constructor, offset: formStart)
    }

    private mutating func messageForm(depth: Int, handler: Bool, formStart: Int, outerOpen: Int) -> GesValue? {
        guard let name = name(), GesNames.message(name) else { return fail(GesDocumentErrorCodes.invalidLiteral, formStart) }
        whitespace()
        let open = position
        guard consume(40) else { return fail(GesDocumentErrorCodes.invalidLiteral, formStart) }
        whitespace()
        var labels: [String] = []
        var args: [GesValue] = []
        if document && position == bytes.count { return fail(GesDocumentErrorCodes.unclosedBracket, open) }
        if !consume(41) {
            while position < bytes.count {
                if !item() { return nil }
                if handler {
                    let labelStart = position
                    guard let label = self.name() else { return fail(GesDocumentErrorCodes.invalidLiteral, formStart) }
                    if label != "_" && !GesNames.plain(label) { return fail(GesDocumentErrorCodes.invalidKey, labelStart) }
                    if label != "_" && labels.contains(label) { return fail(GesDocumentErrorCodes.duplicateKey, labelStart) }
                    labels.append(label)
                } else {
                    let saved = position
                    var label = key()
                    inline()
                    if label == nil || !consume(58) {
                        label = "_"
                        position = saved
                    } else if !GesNames.plain(label!) {
                        return fail(GesDocumentErrorCodes.invalidKey, saved)
                    } else if labels.contains(label!) {
                        return fail(GesDocumentErrorCodes.duplicateKey, saved)
                    }
                    if document {
                        whitespace()
                        if position == bytes.count { return fail(GesDocumentErrorCodes.unclosedBracket, open) }
                    }
                    guard let arg = value(depth: depth) else { return nil }
                    labels.append(label!)
                    args.append(arg)
                }
                let next = separator(41, open)
                if next < 0 { return nil }
                if next > 0 { break }
            }
        }
        whitespace()
        var result: GesValue
        if handler {
            guard let signature = try? GesMessageSignature(name: name, parameters: labels) else { return fail(GesDocumentErrorCodes.invalidLiteral, formStart) }
            result = .handler(signature)
        } else {
            result = node("@message:" + name, labels, args, offset: formStart)
        }
        if !handler && starts("with") {
            position += 4
            var tags: [GesValue] = []
            if document {
                while true {
                    guard let tag = value(depth: depth) else { return nil }
                    tags.append(tag)
                    let next = separator(41, outerOpen)
                    if next < 0 { return nil }
                    if next > 0 { break }
                }
                return node("message", ["_", "tags"], [result, .list(tags)], offset: formStart)
            }
            repeat {
                guard let tag = value(depth: depth) else { return nil }
                tags.append(tag)
                whitespace()
            } while consume(44)
            result = node("message", ["_", "tags"], [result, .list(tags)], offset: formStart)
        }
        if consume(41) { return result }
        return position == bytes.count ? fail(GesDocumentErrorCodes.unclosedBracket, outerOpen) : fail(GesDocumentErrorCodes.unexpectedToken, position)
    }

    private mutating func spatial(depth: Int, point: Bool) -> GesValue? {
        let formStart = position
        position += point ? 6 : 7
        whitespace()
        let open = position
        if !consume(40) { return fail(GesDocumentErrorCodes.invalidLiteral, formStart) }
        if depth >= 64 { return setLimit("MaxLiteralDepth", formStart) }
        var components = [Double](repeating: 0, count: 3)
        var unit: GesQuantityUnit?
        var labeled: Bool?
        var previous = -1
        whitespace()
        if document && position == bytes.count { return fail(GesDocumentErrorCodes.unclosedBracket, open) }
        if !consume(41) {
            while position < bytes.count {
                if !item() { return nil }
                let hasLabel = (120...122).contains(bytes[position])
                if let labeled, labeled != hasLabel { return fail(GesDocumentErrorCodes.invalidLiteral, formStart) }
                labeled = hasLabel
                var index = previous + 1
                let labelStart = position
                if hasLabel {
                    index = Int(bytes[position] - 120)
                    position += 1
                    inline()
                    if !consume(58) { return fail(GesDocumentErrorCodes.invalidLiteral, formStart) }
                    whitespace()
                    if document && position == bytes.count { return fail(GesDocumentErrorCodes.unclosedBracket, open) }
                }
                if hasLabel && index == previous { return fail(GesDocumentErrorCodes.duplicateKey, labelStart) }
                if index > 2 || index <= previous { return fail(GesDocumentErrorCodes.invalidLiteral, formStart) }
                previous = index
                let start = position
                while position < bytes.count && !Self.space(bytes[position]) && bytes[position] != 44 && bytes[position] != 41 { position += 1 }
                if document { endTokenAtComment(start) }
                if start == position { return fail(GesDocumentErrorCodes.unexpectedToken, position) }
                let token = String(decoding: bytes[start..<position], as: UTF8.self)
                guard let value = GesTextNumberCast.read(token, percentage: token.hasSuffix("%"), allowGrouping: false) else { return tokenFailure(start, position) }
                guard value.numericOnly else { return fail(GesDocumentErrorCodes.invalidLiteral, formStart) }
                if let unit, unit != value.unit { return fail(GesDocumentErrorCodes.invalidLiteral, formStart) }
                unit = value.unit
                components[index] = value.asNumber
                let next = separator(41, open)
                if next < 0 { return nil }
                if next > 0 { return point ? .point(x: components[0], y: components[1], z: components[2], unit: unit!) : .vector(x: components[0], y: components[1], z: components[2], unit: unit!) }
            }
            return nil
        }
        return point ? .point(x: 0) : .vector(x: 0)
    }

    private mutating func readDice(formStart: Int) -> GesValue? {
        let open = position
        position += 1
        whitespace()
        if consume(93) { return .dice([]) }
        if document && position == bytes.count { return fail(GesDocumentErrorCodes.unclosedBracket, open) }
        var rolls: [Int32] = []
        while position < bytes.count {
            if !item() { return nil }
            if !GesNames.digit(bytes[position]) { return fail(GesDocumentErrorCodes.invalidLiteral, formStart) }
            guard let roll = value(depth: 0) else { return nil }
            guard !roll.hasUnit, let integer = roll.integerValue, integer > 0, integer <= Int32.max else { return fail(GesDocumentErrorCodes.invalidLiteral, formStart) }
            rolls.append(Int32(integer))
            let next = separator(93, open)
            if next < 0 { return nil }
            if next > 0 { return .dice(rolls) }
        }
        return nil
    }

    private mutating func collection(depth: Int) -> GesValue? {
        let open = position
        position += 1
        whitespace()
        if consume(93) { return .list([]) }
        let contentStart = position
        if consume(58) {
            whitespace()
            if consume(93) { return .map([]) }
            position = contentStart
        }
        if document && position == bytes.count { return fail(GesDocumentErrorCodes.unclosedBracket, open) }
        let saved = position
        let firstKey = key()
        inline()
        let isMap = firstKey != nil && consume(58)
        position = saved
        var values: [GesValue] = []
        var entries: [GesMapEntry] = []
        var seen: Set<[Unicode.Scalar]> = []
        while position < bytes.count {
            if !item() { return nil }
            if isMap {
                whitespace()
                let keyStart = position
                guard let key = key() else { return fail(keyStart < bytes.count && (bytes[keyStart] == 34 || bytes[keyStart] == 39) ? GesDocumentErrorCodes.unclosedText : GesDocumentErrorCodes.invalidKey, keyStart) }
                if document && !seen.insert(Array(key.unicodeScalars)).inserted { return fail(GesDocumentErrorCodes.duplicateKey, keyStart) }
                inline()
                if !consume(58) { return fail(GesDocumentErrorCodes.missingColon, position) }
                guard let parsed = entryValue(depth: depth) else { return nil }
                entries.append(.init(key: key, value: parsed))
            } else {
                guard let parsed = value(depth: depth) else { return nil }
                values.append(parsed)
            }
            let next = separator(93, open)
            if next < 0 { return nil }
            if next > 0 { return isMap ? node("@map", entries.map(\.key), entries.map(\.value)) : .list(values) }
        }
        return nil
    }

    private mutating func key() -> String? {
        whitespace()
        if position == bytes.count { return nil }
        if bytes[position] == 34 || bytes[position] == 39 { return quoted() }
        let start = position
        if !GesNames.lower(bytes[position]) { return nil }
        position += 1
        while position < bytes.count && GesNames.alnum(bytes[position]) { position += 1 }
        return String(decoding: bytes[start..<position], as: UTF8.self)
    }

    private mutating func quoted() -> String? {
        let quote = bytes[position]
        position += 1
        var output: [UInt8] = []
        while position < bytes.count {
            let byte = bytes[position]
            position += 1
            if byte != quote {
                output.append(byte)
                continue
            }
            if position < bytes.count && bytes[position] == quote {
                output.append(quote)
                position += 1
                continue
            }
            return String(decoding: output, as: UTF8.self)
        }
        return nil
    }

    // Ends one container entry: 1 when the container closes, 0 when another entry follows, -1 on failure.
    // Inlined like the former inline separator code of `parse` collection loops (measured on List, Map, Vector).
    @inline(__always)
    private mutating func separator(_ close: UInt8, _ open: Int) -> Int {
        if document { return documentSeparator(close, open) }
        whitespace()
        if consume(close) { return 1 }
        if !consume(44) { return -1 }
        whitespace()
        return position == bytes.count || bytes[position] == close ? -1 : 0
    }

    // Documents also end entries at line breaks and accept one trailing comma.
    private mutating func documentSeparator(_ close: UInt8, _ open: Int) -> Int {
        spacesAndComments()
        if consume(close) { return 1 }
        if position == bytes.count {
            fail(GesDocumentErrorCodes.unclosedBracket, open)
            return -1
        }
        if !consume(44) && !atLineBreak {
            fail(GesDocumentErrorCodes.unexpectedToken, position)
            return -1
        }
        whitespace()
        if position == bytes.count {
            fail(GesDocumentErrorCodes.unclosedBracket, open)
            return -1
        }
        if consume(close) { return 1 }
        if bytes[position] == 44 {
            fail(GesDocumentErrorCodes.unexpectedToken, position)
            return -1
        }
        return 0
    }

    // Classifies an unreadable value token for document errors.
    private mutating func tokenFailure(_ start: Int, _ end: Int) -> GesValue? {
        let first = bytes[start]
        if (65...90).contains(first) || GesNames.lower(first) || first == 95 { return fail(GesDocumentErrorCodes.unquotedText, start) }
        return fail(hasUnknownUnit(start, end) ? GesDocumentErrorCodes.unknownUnit : GesDocumentErrorCodes.invalidLiteral, start)
    }

    // A numeric prefix followed by an ASCII letter, `°` or `/` is a number with an unknown unit suffix.
    private func hasUnknownUnit(_ start: Int, _ end: Int) -> Bool {
        var index = start
        if index < end && (bytes[index] == 43 || bytes[index] == 45) { index += 1 }
        var digits = skipDigits(&index, end)
        if index < end && bytes[index] == 46 {
            index += 1
            if skipDigits(&index, end) { digits = true }
        }
        if !digits { return false }
        if index < end && (bytes[index] == 101 || bytes[index] == 69) {
            var exponent = index + 1
            if exponent < end && (bytes[exponent] == 43 || bytes[exponent] == 45) { exponent += 1 }
            if skipDigits(&exponent, end) { index = exponent }
        }
        guard index < end else { return false }
        let byte = bytes[index]
        // `°` is U+00B0, encoded as C2 B0.
        return (65...90).contains(byte) || GesNames.lower(byte) || byte == 47 || (byte == 0xC2 && index + 1 < end && bytes[index + 1] == 0xB0)
    }

    private func skipDigits(_ index: inout Int, _ end: Int) -> Bool {
        guard index < end, GesNames.digit(bytes[index]) else { return false }
        index += 1
        while index < end && (GesNames.digit(bytes[index]) || bytes[index] == 95) { index += 1 }
        return true
    }

    // Records the first document error; outside document mode failures carry no diagnostics.
    @discardableResult
    private mutating func fail(_ code: String, _ at: Int) -> GesValue? {
        if document && error == nil && limit == nil {
            error = code
            errorPosition = at
        }
        return nil
    }

    @discardableResult
    private mutating func setLimit(_ name: String, _ at: Int) -> GesValue? {
        if limit == nil {
            limit = name
            limitPosition = at
        }
        return nil
    }

    private mutating func item() -> Bool {
        if items >= 65536 {
            setLimit("MaxLiteralItems", position)
            return false
        }
        items += 1
        return true
    }

    private mutating func consume(_ byte: UInt8) -> Bool {
        if position >= bytes.count || bytes[position] != byte { return false }
        position += 1
        return true
    }

    private var atComment: Bool { document && position + 1 < bytes.count && bytes[position] == 47 && bytes[position + 1] == 47 }

    private var atLineBreak: Bool { position < bytes.count && (bytes[position] == 10 || bytes[position] == 13) }

    // Inlined so the document branch does not slow `parse` (measured: quoted Text and collection literals).
    @inline(__always)
    private mutating func whitespace() {
        if document {
            documentWhitespace()
            return
        }
        while position < bytes.count && Self.space(bytes[position]) { position += 1 }
    }

    private mutating func documentWhitespace() {
        spacesAndComments()
        while atLineBreak {
            position += 1
            spacesAndComments()
        }
    }

    // A document comment ends an unquoted token; the scan stays comment-free so `parse` keeps its tight loop.
    private mutating func endTokenAtComment(_ start: Int) {
        var index = start
        while index + 1 < position {
            if bytes[index] == 47 && bytes[index + 1] == 47 {
                position = index
                return
            }
            index += 1
        }
    }

    // Trivia between a key or label and its colon or value: documents keep both on one line.
    private mutating func inline() {
        if document { spacesAndComments() } else { whitespace() }
    }

    private mutating func spacesAndComments() {
        while position < bytes.count {
            if bytes[position] == 32 || bytes[position] == 9 {
                position += 1
            } else if atComment {
                while position < bytes.count && bytes[position] != 10 && bytes[position] != 13 { position += 1 }
            } else {
                return
            }
        }
    }

    private static func space(_ byte: UInt8) -> Bool { byte == 32 || byte == 9 || byte == 10 || byte == 13 }

    private func starts(_ token: String) -> Bool { bytes[position...].starts(with: token.utf8) }
}
