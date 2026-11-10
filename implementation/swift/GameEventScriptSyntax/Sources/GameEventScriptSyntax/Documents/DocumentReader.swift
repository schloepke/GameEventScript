// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Reads GES document text: a bracket-free root of entries, an optional schema header, comments and line-break
/// separators around the data literals of `parse`. Reading stops at the first error.
@_spi(Syntax) public struct GesDocumentReader {
    private typealias Code = GesDocumentDiagnostic

    private let bytes: [UInt8]
    private var position = 0, items = 0
    private var error: GesDocumentDiagnostic?

    private init(_ bytes: [UInt8]) { self.bytes = bytes }

    @_spi(Syntax) public static func read(_ text: String) -> GesDocumentSyntax {
        let bytes = Array(text.utf8)
        if text.unicodeScalars.count > 1_048_576 {
            return GesDocumentSyntax(bytes: bytes, root: nil, schema: .none, diagnostic: Code(Code.limitExceeded, 0, 0, limitName: "MaxLiteralInputScalars", limitValue: 1_048_576))
        }
        var reader = Self(bytes)
        let (root, schema) = reader.document()
        let diagnostic = reader.error ?? (root == nil ? reader.diagnostic(Code.invalidLiteral, reader.position) : nil)
        return GesDocumentSyntax(bytes: bytes, root: diagnostic == nil ? root : nil, schema: schema, diagnostic: diagnostic)
    }

    private mutating func document() -> (GesDocumentNode?, GesDocumentSchemaSyntax) {
        if bytes.starts(with: [0xEF, 0xBB, 0xBF]) { position = 3 }
        whitespace()
        var schema = GesDocumentSchemaSyntax.none
        if isSchemaHeader() {
            guard let header = schemaHeader() else { return (nil, .none) }
            schema = header
            spacesAndComments()
            if position < bytes.count && !atLineBreak { return (fail(Code.unexpectedToken, position), schema) }
            whitespace()
        }
        var record = false
        if case .type = schema { record = true }
        var keys: [String] = []
        var values: [GesDocumentNode] = []
        var keyOffsets: [Int] = []
        var valueOffsets: [Int] = []
        var seen: Set<[Unicode.Scalar]> = []
        while position < bytes.count {
            let entryStart = position
            if isSchemaHeader() { return (fail(Code.misplacedSchemaHeader, entryStart), schema) }
            if items >= 65536 { return (setLimit("MaxLiteralItems", 65536, entryStart), schema) }
            items += 1
            guard let key = key() else { return (fail(bytes[entryStart] == 34 || bytes[entryStart] == 39 ? Code.unclosedText : Code.invalidKey, entryStart), schema) }
            if record && !GesNames.plain(key) { return (fail(Code.invalidKey, entryStart), schema) }
            if !seen.insert(Array(key.unicodeScalars)).inserted { return (fail(Code.duplicateKey, entryStart), schema) }
            spacesAndComments()
            if !consume(58) { return (fail(Code.missingColon, position), schema) }
            let implied = keyOnly()
            whitespaceBeforeValue(implied)
            let valueStart = position
            guard let entry = implied ? .boolean(true) : value(depth: 0) else { return (nil, schema) }
            keys.append(key)
            keyOffsets.append(entryStart)
            valueOffsets.append(valueStart)
            values.append(entry)
            spacesAndComments()
            if position == bytes.count { break }
            if !consume(44) && !atLineBreak { return (fail(Code.unexpectedToken, position), schema) }
            whitespace()
            if position < bytes.count && bytes[position] == 44 { return (fail(Code.unexpectedToken, position), schema) }
        }
        return (.map(keys: keys, values: values, keyOffsets: keyOffsets, valueOffsets: valueOffsets), schema)
    }

    // A value starts after whitespace; a key-only entry has none.
    private mutating func whitespaceBeforeValue(_ implied: Bool) {
        if !implied { whitespace() }
    }

    private func isSchemaHeader() -> Bool { startsWord(":Schema") }

    private mutating func schemaHeader() -> GesDocumentSchemaSyntax? {
        position += 7
        whitespace()
        let open = position
        if !consume(40) {
            fail(Code.invalidSchemaHeader, position)
            return nil
        }
        whitespace()
        let start = position
        var schema = GesDocumentSchemaSyntax.none
        if position == bytes.count {
            fail(Code.unclosedBracket, open)
            return nil
        }
        if consume(58) {
            if let name = name(), GesLiteralForms.isRecordTypeName(name) { schema = .type(name) }
        } else if bytes[position] == 34 || bytes[position] == 39 {
            guard let path = quoted() else {
                fail(Code.unclosedText, start)
                return nil
            }
            if !path.isEmpty { schema = .file(path) }
        }
        if schema == .none {
            fail(Code.invalidSchemaHeader, start)
            return nil
        }
        switch separator(41, open) {
        case 1: return schema
        case 0:
            fail(Code.invalidSchemaHeader, position)
            return nil
        default: return nil
        }
    }

    // Before the value after a Map or root key: true for a key-only entry, whose value is true; otherwise the value
    // starts on the key's line.
    private mutating func keyOnly() -> Bool {
        spacesAndComments()
        return position == bytes.count || atLineBreak || bytes[position] == 44 || bytes[position] == 93
    }

    // Nested values recurse through `value`, `collection`, `list`/`map`, `typed`, `typedArguments`, `messageArguments`
    // and `messageEnd`. Debug builds reserve stack for every local of a function on each call, so these stay
    // payload-free dispatchers and leave non-recursive work to their own functions: documents may nest to the
    // MaxLiteralDepth limit on a 512 KB Swift concurrency thread.
    private mutating func value(depth: Int) -> GesDocumentNode? {
        whitespace()
        switch valueForm() {
        case 1: return depth >= 64 ? setLimit("MaxLiteralDepth", 64, position) : collection(depth: depth + 1)
        case 2:
            let saved = position
            if let result = typed(depth: depth) { return result }
            if error != nil { return nil }
            position = saved
            return leaf(depth: depth)
        case 3: return spatial(depth: depth, point: false)
        case 4: return spatial(depth: depth, point: true)
        default: return leaf(depth: depth)
        }
    }

    // 1 for a collection, 2 for an explicit `:Type(...)` form to try first, 3 and 4 for a Vector and a Point, 0 for any
    // other value.
    private func valueForm() -> Int {
        if position == bytes.count { return 0 }
        if bytes[position] == 91 { return 1 }
        if bytes[position] != 58 { return 0 }
        if startsWord(":Vector") { return 3 }
        if startsWord(":Point") { return 4 }
        return starts(":Dice[") ? 0 : 2
    }

    // Text, Dice rolls or an unquoted token; `value` dispatches Vectors and Points itself.
    private mutating func leaf(depth: Int) -> GesDocumentNode? {
        if position == bytes.count { return fail(Code.unexpectedToken, position) }
        if bytes[position] == 34 || bytes[position] == 39 { return quotedValue() }
        return starts(":Dice") ? dice(depth: depth) : scalar()
    }

    private mutating func quotedValue() -> GesDocumentNode? {
        let valueStart = position
        guard let text = quoted() else { return fail(Code.unclosedText, valueStart) }
        return .text(text)
    }

    private mutating func scalar() -> GesDocumentNode? {
        let start = position
        while position < bytes.count && !Self.space(bytes[position]) && ![44, 93, 91, 41].contains(bytes[position]) { position += 1 }
        endTokenAtComment(start)
        if start == position { return fail(Code.unexpectedToken, position) }
        let token = String(decoding: bytes[start..<position], as: UTF8.self)
        switch token {
        case "true": return .boolean(true)
        case "false": return .boolean(false)
        case "nothing": return .nothing
        default: break
        }
        if token.hasPrefix("#") {
            let tag = String(token.dropFirst())
            guard GesNames.plain(tag) else { return fail(Code.invalidLiteral, start, end: position) }
            return .tag(tag)
        }
        let number = GesNumberLiteral.read(token, percentage: token.hasSuffix("%"), allowGrouping: false)
        if !number.isValid { return tokenFailure(start, position) }
        // A number token that constructs nothing, such as `NaN` or `Infinity%`, is no value in a document.
        return number.kind == .nothing ? fail(Code.invalidLiteral, start, end: position) : .number(number)
    }

    private mutating func name() -> String? {
        let start = position
        while position < bytes.count && Self.nameByte(bytes[position]) { position += 1 }
        return start == position ? nil : String(decoding: bytes[start..<position], as: UTF8.self)
    }

    private static func nameByte(_ byte: UInt8) -> Bool { GesNames.lower(byte) || (65...90).contains(byte) || GesNames.digit(byte) || byte == 95 }

    // The opened form `:Type(` of a built-in data type.
    private struct TypedHead {
        enum Form { case data, handler, message }

        let type: String
        let open: Int
        let form: Form
    }

    private mutating func typed(depth: Int) -> GesDocumentNode? {
        let formStart = position
        if depth >= 64 { return setLimit("MaxLiteralDepth", 64, formStart) }
        guard let head = typedHead(formStart) else { return nil }
        switch head.form {
        case .data: return typedArguments(depth: depth + 1, head, formStart: formStart)
        case .handler: return handlerForm(formStart: formStart, outerOpen: head.open)
        case .message:
            guard let message = messageArguments(depth: depth + 1, formStart: formStart, outerOpen: head.open) else { return nil }
            return messageEnd(depth: depth + 1, message, formStart: formStart, outerOpen: head.open)
        }
    }

    private mutating func typedHead(_ formStart: Int) -> TypedHead? {
        position += 1
        guard let name = name(), GesNames.type(name) else {
            fail(Code.invalidLiteral, formStart)
            return nil
        }
        // Built-in names are case-sensitive; documents know no Program Record constructors.
        guard GesLiteralForms.isBuiltinName(name) else {
            fail(Code.unknownType, formStart)
            return nil
        }
        let type = name.lowercased()
        whitespace()
        let open = position
        // `:Dice [...]` with whitespace is the Dice result literal, read after this form fails.
        guard consume(40) else {
            if type != "dice" { fail(Code.invalidLiteral, formStart) }
            return nil
        }
        whitespace()
        var form = TypedHead.Form.data
        if ["handler", "message"].contains(type), position < bytes.count, (65...90).contains(bytes[position]) {
            form = type == "handler" ? .handler : .message
        }
        return TypedHead(type: type, open: open, form: form)
    }

    private mutating func typedArguments(depth: Int, _ head: TypedHead, formStart: Int) -> GesDocumentNode? {
        var labels: [String] = []
        var args: [GesDocumentNode] = []
        if position == bytes.count { return fail(Code.unclosedBracket, head.open) }
        if !consume(41) {
            while position < bytes.count {
                if !item() { return nil }
                guard let label = argumentLabel(labels, open: head.open) else { return nil }
                let argument: GesDocumentNode
                if let kind = seriesKind(head.type, first: label == "kind" || (label == "_" && args.isEmpty)) {
                    argument = kind
                } else {
                    guard let parsed = value(depth: depth) else { return nil }
                    argument = parsed
                }
                labels.append(label)
                args.append(argument)
                let next = separator(41, head.open)
                if next < 0 { return nil }
                if next > 0 { break }
            }
            if position == 0 || bytes[position - 1] != 41 { return nil }
        }
        if GesLiteralForms.positions(head.type, labels) == nil { return fail(Code.invalidLiteral, formStart) }
        return .form(type: head.type, labels: labels, arguments: args, offset: formStart)
    }

    // An optional `label:` before a constructor or Message argument: "_" when unlabeled, nil after a failure.
    private mutating func argumentLabel(_ labels: [String], open: Int) -> String? {
        let saved = position
        var label = key()
        spacesAndComments()
        if label == nil || !consume(58) {
            label = "_"
            position = saved
        } else if !GesNames.plain(label!) {
            fail(Code.invalidKey, saved)
            return nil
        } else if labels.contains(label!) {
            fail(Code.duplicateKey, saved)
            return nil
        }
        whitespace()
        if position == bytes.count {
            fail(Code.unclosedBracket, open)
            return nil
        }
        return label
    }

    // The built-in sequence name as the first `:Series(...)` argument; nil, without consuming input, otherwise.
    private mutating func seriesKind(_ type: String, first: Bool) -> GesDocumentNode? {
        guard first, type == "series", position < bytes.count, GesNames.lower(bytes[position]) else { return nil }
        let valueStart = position
        let kind = name()
        if kind == "fibonacci" || kind == "factorial" { return .text(kind!) }
        position = valueStart
        return nil
    }

    // The message name and opening parenthesis inside `:Handler(` or `:Message(`.
    private mutating func messageHead(_ formStart: Int) -> (name: String, open: Int)? {
        guard let name = name(), GesNames.type(name) else {
            fail(Code.invalidLiteral, formStart)
            return nil
        }
        whitespace()
        let open = position
        guard consume(40) else {
            fail(Code.invalidLiteral, formStart)
            return nil
        }
        whitespace()
        if position == bytes.count {
            fail(Code.unclosedBracket, open)
            return nil
        }
        return (name, open)
    }

    private mutating func handlerForm(formStart: Int, outerOpen: Int) -> GesDocumentNode? {
        guard let (name, open) = messageHead(formStart) else { return nil }
        var labels: [String] = []
        if !consume(41) {
            while position < bytes.count {
                if !item() { return nil }
                let labelStart = position
                guard let label = self.name() else { return fail(Code.invalidLiteral, formStart) }
                if label != "_" && !GesNames.plain(label) { return fail(Code.invalidKey, labelStart) }
                if label != "_" && labels.contains(label) { return fail(Code.duplicateKey, labelStart) }
                labels.append(label)
                let next = separator(41, open)
                if next < 0 { return nil }
                if next > 0 { break }
            }
        }
        whitespace()
        return closeMessage(.handler(name: name, labels: labels), outerOpen: outerOpen)
    }

    // The `Name(arguments)` of a Message form, before optional tags.
    private mutating func messageArguments(depth: Int, formStart: Int, outerOpen: Int) -> GesDocumentNode? {
        guard let head = messageHead(formStart) else { return nil }
        var labels: [String] = []
        var args: [GesDocumentNode] = []
        if !consume(41) {
            while position < bytes.count {
                if !item() { return nil }
                guard let label = argumentLabel(labels, open: head.open) else { return nil }
                guard let arg = value(depth: depth) else { return nil }
                labels.append(label)
                args.append(arg)
                let next = separator(41, head.open)
                if next < 0 { return nil }
                if next > 0 { break }
            }
        }
        return .message(name: head.name, labels: labels, arguments: args, offset: formStart)
    }

    // Optional `with` tags and the closing parenthesis of a Message form.
    private mutating func messageEnd(depth: Int, _ message: GesDocumentNode, formStart: Int, outerOpen: Int) -> GesDocumentNode? {
        whitespace()
        if !startsWord("with") { return closeMessage(message, outerOpen: outerOpen) }
        position += 4
        var tags: [GesDocumentNode] = []
        while true {
            guard let tag = value(depth: depth) else { return nil }
            tags.append(tag)
            let next = separator(41, outerOpen)
            if next < 0 { return nil }
            if next > 0 { break }
        }
        return .form(type: "message", labels: ["_", "tags"], arguments: [message, .list(tags, offsets: [])], offset: formStart)
    }

    private mutating func closeMessage(_ result: GesDocumentNode, outerOpen: Int) -> GesDocumentNode? {
        if consume(41) { return result }
        return position == bytes.count ? fail(Code.unclosedBracket, outerOpen) : fail(Code.unexpectedToken, position)
    }

    private mutating func spatial(depth: Int, point: Bool) -> GesDocumentNode? {
        let formStart = position
        position += point ? 6 : 7
        whitespace()
        let open = position
        if !consume(40) { return fail(Code.invalidLiteral, formStart) }
        if depth >= 64 { return setLimit("MaxLiteralDepth", 64, formStart) }
        var components = [Double](repeating: 0, count: 3)
        var unit: GesQuantityUnit?
        var labeled: Bool?
        var previous = -1
        whitespace()
        if position == bytes.count { return fail(Code.unclosedBracket, open) }
        if consume(41) { return .spatial(point: point, x: 0, y: 0, z: 0, unit: .none) }
        while position < bytes.count {
            if !item() { return nil }
            let hasLabel = (120...122).contains(bytes[position])
            if let labeled, labeled != hasLabel { return fail(Code.invalidLiteral, formStart) }
            labeled = hasLabel
            var index = previous + 1
            let labelStart = position
            if hasLabel {
                index = Int(bytes[position] - 120)
                position += 1
                spacesAndComments()
                if !consume(58) { return fail(Code.invalidLiteral, formStart) }
                whitespace()
                if position == bytes.count { return fail(Code.unclosedBracket, open) }
            }
            if hasLabel && index == previous { return fail(Code.duplicateKey, labelStart) }
            if index > 2 || index <= previous || (previous >= 0 && index != previous + 1) { return fail(Code.invalidLiteral, formStart) }
            previous = index
            let start = position
            while position < bytes.count && !Self.space(bytes[position]) && bytes[position] != 44 && bytes[position] != 41 { position += 1 }
            endTokenAtComment(start)
            if start == position { return fail(Code.unexpectedToken, position) }
            let token = String(decoding: bytes[start..<position], as: UTF8.self)
            let number = GesNumberLiteral.read(token, percentage: token.hasSuffix("%"), allowGrouping: false)
            guard number.isValid else { return tokenFailure(start, position) }
            guard number.kind != .nothing else { return fail(Code.invalidLiteral, formStart) }
            if let unit, unit != number.unit { return fail(Code.invalidLiteral, formStart) }
            unit = number.unit
            components[index] = number.kind == .integer ? Double(number.integerValue) : number.floatValue
            let next = separator(41, open)
            if next < 0 { return nil }
            if next > 0 { return .spatial(point: point, x: components[0], y: components[1], z: components[2], unit: unit!) }
        }
        return nil
    }

    // `:Dice` followed by its bracketed rolls, after the explicit `:Dice(...)` form did not apply.
    private mutating func dice(depth: Int) -> GesDocumentNode? {
        let valueStart = position
        position += 5
        whitespace()
        if position == bytes.count || bytes[position] != 91 { return fail(Code.invalidLiteral, valueStart) }
        if depth >= 64 { return setLimit("MaxLiteralDepth", 64, position) }
        return readDice(formStart: valueStart)
    }

    private mutating func readDice(formStart: Int) -> GesDocumentNode? {
        let open = position
        position += 1
        whitespace()
        if consume(93) { return .dice([]) }
        if position == bytes.count { return fail(Code.unclosedBracket, open) }
        var rolls: [Int32] = []
        while position < bytes.count {
            if !item() { return nil }
            if !GesNames.digit(bytes[position]) { return fail(Code.invalidLiteral, formStart) }
            guard let roll = value(depth: 0) else { return nil }
            guard case .number(let number) = roll, number.kind == .integer, number.unit == .none, number.integerValue > 0, number.integerValue <= Int32.max else {
                return fail(Code.invalidLiteral, formStart)
            }
            rolls.append(Int32(number.integerValue))
            let next = separator(93, open)
            if next < 0 { return nil }
            if next > 0 { return .dice(rolls) }
        }
        return nil
    }

    private mutating func collection(depth: Int) -> GesDocumentNode? {
        let open = position
        switch collectionStart() {
        case 0: return .list([], offsets: [])
        case 1: return .map(keys: [], values: [], keyOffsets: [], valueOffsets: [])
        case 2: return list(depth: depth, open: open)
        case 3: return map(depth: depth, open: open)
        default: return nil
        }
    }

    // Opens a collection: 0 and 1 for the closed empty List and Map, 2 and 3 for a List or Map with entries, -1 on failure.
    private mutating func collectionStart() -> Int {
        let open = position
        position += 1
        whitespace()
        if consume(93) { return 0 }
        let contentStart = position
        if consume(58) {
            whitespace()
            if consume(93) { return 1 }
            position = contentStart
        }
        if position == bytes.count {
            fail(Code.unclosedBracket, open)
            return -1
        }
        let saved = position
        let firstKey = key()
        spacesAndComments()
        let isMap = firstKey != nil && consume(58)
        position = saved
        return isMap ? 3 : 2
    }

    private mutating func list(depth: Int, open: Int) -> GesDocumentNode? {
        var values: [GesDocumentNode] = []
        var offsets: [Int] = []
        while position < bytes.count {
            if !item() { return nil }
            whitespace()
            offsets.append(position)
            guard let parsed = value(depth: depth) else { return nil }
            values.append(parsed)
            let next = separator(93, open)
            if next < 0 { return nil }
            if next > 0 { return .list(values, offsets: offsets) }
        }
        return nil
    }

    private mutating func map(depth: Int, open: Int) -> GesDocumentNode? {
        var keys: [String] = []
        var values: [GesDocumentNode] = []
        var keyOffsets: [Int] = []
        var valueOffsets: [Int] = []
        var seen: Set<[Unicode.Scalar]> = []
        while position < bytes.count {
            if !item() { return nil }
            whitespace()
            let keyStart = position
            guard let key = mapKey(&seen) else { return nil }
            let implied = keyOnly()
            whitespaceBeforeValue(implied)
            let valueStart = position
            guard let parsed = implied ? .boolean(true) : value(depth: depth) else { return nil }
            keys.append(key)
            keyOffsets.append(keyStart)
            valueOffsets.append(valueStart)
            values.append(parsed)
            let next = separator(93, open)
            if next < 0 { return nil }
            if next > 0 { return .map(keys: keys, values: values, keyOffsets: keyOffsets, valueOffsets: valueOffsets) }
        }
        return nil
    }

    // A Map entry key and its colon; nil after a failure.
    private mutating func mapKey(_ seen: inout Set<[Unicode.Scalar]>) -> String? {
        whitespace()
        let keyStart = position
        guard let key = key() else {
            fail(keyStart < bytes.count && (bytes[keyStart] == 34 || bytes[keyStart] == 39) ? Code.unclosedText : Code.invalidKey, keyStart)
            return nil
        }
        if !seen.insert(Array(key.unicodeScalars)).inserted {
            fail(Code.duplicateKey, keyStart)
            return nil
        }
        spacesAndComments()
        if !consume(58) {
            fail(Code.missingColon, position)
            return nil
        }
        return key
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

    // Ends one container entry: 1 when the container closes, 0 when another entry follows, -1 on failure. Line
    // breaks also end entries, and one trailing comma is accepted.
    private mutating func separator(_ close: UInt8, _ open: Int) -> Int {
        spacesAndComments()
        if consume(close) { return 1 }
        if position == bytes.count {
            fail(Code.unclosedBracket, open)
            return -1
        }
        if !consume(44) && !atLineBreak {
            fail(Code.unexpectedToken, position)
            return -1
        }
        whitespace()
        if position == bytes.count {
            fail(Code.unclosedBracket, open)
            return -1
        }
        if consume(close) { return 1 }
        if bytes[position] == 44 {
            fail(Code.unexpectedToken, position)
            return -1
        }
        return 0
    }

    // Classifies an unreadable value token.
    private mutating func tokenFailure(_ start: Int, _ end: Int) -> GesDocumentNode? {
        let first = bytes[start]
        if (65...90).contains(first) || GesNames.lower(first) || first == 95 { return fail(Code.unquotedText, start, end: end) }
        return fail(hasUnknownUnit(start, end) ? Code.unknownUnit : Code.invalidLiteral, start, end: end)
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

    // Records the first error.
    @discardableResult
    private mutating func fail(_ code: String, _ at: Int, end: Int? = nil) -> GesDocumentNode? {
        if error == nil { error = diagnostic(code, at, end: end) }
        return nil
    }

    // Without an explicit end, the range covers the `:Name` of a form, or the scalar at the position, or nothing at a
    // line break and at the end.
    private func diagnostic(_ code: String, _ at: Int, end: Int? = nil) -> GesDocumentDiagnostic {
        var end = end ?? at
        if end == at && at < bytes.count {
            if bytes[at] == 58 && at + 1 < bytes.count && (GesNames.lower(bytes[at + 1]) || GesNames.upper(bytes[at + 1])) {
                end = at + 1
                while end < bytes.count && Self.nameByte(bytes[end]) { end += 1 }
            } else if bytes[at] != 10 && bytes[at] != 13 {
                end = at + 1
                while end < bytes.count && bytes[end] & 0xC0 == 0x80 { end += 1 }
            }
        }
        return Code(code, at, end)
    }

    @discardableResult
    private mutating func setLimit(_ name: String, _ value: Int, _ at: Int) -> GesDocumentNode? {
        if error == nil { error = Code(Code.limitExceeded, at, at, limitName: name, limitValue: value) }
        return nil
    }

    private mutating func item() -> Bool {
        if items >= 65536 {
            setLimit("MaxLiteralItems", 65536, position)
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

    private var atComment: Bool { position + 1 < bytes.count && bytes[position] == 47 && bytes[position + 1] == 47 }

    private var atLineBreak: Bool { position < bytes.count && (bytes[position] == 10 || bytes[position] == 13) }

    private mutating func whitespace() {
        spacesAndComments()
        while atLineBreak {
            position += 1
            spacesAndComments()
        }
    }

    // A comment ends an unquoted token.
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

    // Whether the text continues with `word` and no further name character.
    private func startsWord(_ word: String) -> Bool {
        let end = position + word.utf8.count
        return starts(word) && (end == bytes.count || !Self.nameByte(bytes[end]))
    }
}
