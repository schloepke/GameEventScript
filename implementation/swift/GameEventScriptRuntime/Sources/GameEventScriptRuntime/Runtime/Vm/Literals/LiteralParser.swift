// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Syntax) import GameEventScriptSyntax

struct GesLiteralParser {
    private let bytes: [UInt8]
    private var position = 0, items = 0
    private var limit: String?
    private var nodes: [GesLiteralNode] = []
    // Absent outside a running Program: Program Record constructors are then unknown types.
    private var state: GesVirtualMachine?

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

    private static func limitValue(_ limit: String) -> Int { limit == "MaxLiteralDepth" ? 64 : 65536 }

    private mutating func recognize() -> GesValue? {
        let result = value(depth: 0)
        whitespace()
        return limit == nil && position == bytes.count ? result : nil
    }

    // Before the value after a Map key: true for a key-only entry, whose value is true.
    private mutating func keyOnly() -> Bool {
        whitespace()
        return position < bytes.count && (bytes[position] == 44 || bytes[position] == 93)
    }

    // Nested values recurse through `value`, `collection`, `list`/`map`, `typed`, `typedArguments`, `messageArguments`
    // and `messageEnd`. Debug builds reserve stack for every local of a function on each call, so these stay
    // payload-free dispatchers and leave non-recursive work to their own functions: hosts may parse at the
    // MaxLiteralDepth limit on a 512 KB Swift concurrency thread.
    private mutating func value(depth: Int) -> GesValue? {
        whitespace()
        switch valueForm() {
        case 1: return depth >= 64 ? setLimit("MaxLiteralDepth") : collection(depth: depth + 1)
        case 2:
            let saved = position
            if let result = typed(depth: depth) { return result }
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
        if starts(":Vector") { return 3 }
        if starts(":Point") { return 4 }
        return starts(":Dice[") ? 0 : 2
    }

    // Text, Dice rolls or an unquoted token; `value` dispatches Vectors and Points itself.
    private mutating func leaf(depth: Int) -> GesValue? {
        if position == bytes.count { return nil }
        if bytes[position] == 34 || bytes[position] == 39 { return quotedValue() }
        return starts(":Dice") ? dice(depth: depth) : scalar()
    }

    private mutating func quotedValue() -> GesValue? {
        guard let text = quoted() else { return nil }
        return .text(text)
    }

    private mutating func scalar() -> GesValue? {
        let start = position
        while position < bytes.count && !Self.space(bytes[position]) && ![44, 93, 91, 41].contains(bytes[position]) { position += 1 }
        if start == position { return nil }
        let token = String(decoding: bytes[start..<position], as: UTF8.self)
        switch token {
        case "true": return .boolean(true)
        case "false": return .boolean(false)
        case "nothing": return .nothing
        default: break
        }
        if token.hasPrefix("#") {
            let tag = String(token.dropFirst())
            guard GesNames.plain(tag), let value = try? GesValue.tag(tag) else { return nil }
            return value
        }
        return GesTextNumberCast.read(token, percentage: token.hasSuffix("%"), allowGrouping: false)
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

    // The opened form `:Type(`: a built-in data type without constructor, or a current-Program Record type.
    private struct TypedHead {
        enum Form { case data, handler, message }

        let type: String
        let constructor: GesBindingSegment.Entry?
        let form: Form
    }

    private mutating func typed(depth: Int) -> GesValue? {
        let formStart = position
        if depth >= 64 { return setLimit("MaxLiteralDepth") }
        guard let head = typedHead(formStart) else { return nil }
        switch head.form {
        case .data: return typedArguments(depth: depth + 1, head, formStart: formStart)
        case .handler: return handlerForm()
        case .message:
            guard let message = messageArguments(depth: depth + 1, formStart: formStart) else { return nil }
            return messageEnd(depth: depth + 1, message, formStart: formStart)
        }
    }

    private mutating func typedHead(_ formStart: Int) -> TypedHead? {
        position += 1
        guard let name = name(), GesNames.type(name) else { return nil }
        let type = name.lowercased()
        let builtin = GesLiteralForms.isBuiltinName(name)
        let constructor = builtin ? nil : state?.linked?.recordsByName[name]
        if !builtin && constructor == nil { return nil }
        whitespace()
        // `:Dice [...]` with whitespace is the Dice result literal, read after this form fails.
        guard consume(40) else { return nil }
        whitespace()
        var form = TypedHead.Form.data
        if ["handler", "message"].contains(type), position < bytes.count, (65...90).contains(bytes[position]) || starts("initialization") || starts("undeliverable") {
            form = type == "handler" ? .handler : .message
        }
        return TypedHead(type: type, constructor: constructor, form: form)
    }

    private mutating func typedArguments(depth: Int, _ head: TypedHead, formStart: Int) -> GesValue? {
        var labels: [String] = []
        var args: [GesValue] = []
        if !consume(41) {
            while position < bytes.count {
                if !item() { return nil }
                guard let label = argumentLabel(labels) else { return nil }
                let argument: GesValue
                if let kind = seriesKind(head.type, first: args.isEmpty) {
                    argument = kind
                } else {
                    guard let parsed = value(depth: depth) else { return nil }
                    argument = parsed
                }
                labels.append(label)
                args.append(argument)
                let next = separator(41)
                if next < 0 { return nil }
                if next > 0 { break }
            }
            if position == 0 || bytes[position - 1] != 41 { return nil }
        }
        return typedNode(head, labels, args, formStart: formStart)
    }

    // An optional `label:` before a constructor or Message argument: "_" when unlabeled, nil after a failure.
    private mutating func argumentLabel(_ labels: [String]) -> String? {
        let saved = position
        var label = key()
        whitespace()
        if label == nil || !consume(58) {
            label = "_"
            position = saved
        } else if !GesNames.plain(label!) {
            return nil
        } else if labels.contains(label!) {
            return nil
        }
        whitespace()
        return label
    }

    // The built-in sequence name as the first `:Series(...)` argument; nil, without consuming input, otherwise.
    private mutating func seriesKind(_ type: String, first: Bool) -> GesValue? {
        guard first, type == "series", position < bytes.count, GesNames.lower(bytes[position]) else { return nil }
        let valueStart = position
        let kind = name()
        if kind == "fibonacci" || kind == "factorial" { return .text(kind!) }
        position = valueStart
        return nil
    }

    private mutating func typedNode(_ head: TypedHead, _ labels: [String], _ args: [GesValue], formStart: Int) -> GesValue? {
        if head.constructor == nil && GesLiteralForms.positions(head.type, labels) == nil { return nil }
        if let constructor = head.constructor, let state {
            let allowed = constructor.argumentNames.map(state.text)
            if labels.filter({ $0 == "_" }).count > allowed.filter({ $0 == "_" }).count || labels.contains(where: { $0 != "_" && !allowed.contains($0) }) { return nil }
        }
        return node(head.type, labels, args, head.constructor, offset: formStart)
    }

    // The message name and opening parenthesis inside `:Handler(` or `:Message(`.
    private mutating func messageHead() -> String? {
        guard let name = name(), GesNames.message(name) else { return nil }
        whitespace()
        guard consume(40) else { return nil }
        whitespace()
        return name
    }

    private mutating func handlerForm() -> GesValue? {
        guard let name = messageHead() else { return nil }
        var labels: [String] = []
        if !consume(41) {
            while position < bytes.count {
                if !item() { return nil }
                guard let label = self.name() else { return nil }
                if label != "_" && !GesNames.plain(label) { return nil }
                if label != "_" && labels.contains(label) { return nil }
                labels.append(label)
                let next = separator(41)
                if next < 0 { return nil }
                if next > 0 { break }
            }
        }
        whitespace()
        guard let signature = try? GesMessageSignature(name: name, parameters: labels) else { return nil }
        return closeMessage(.handler(signature))
    }

    // The `Name(arguments)` of a Message form, before optional tags.
    private mutating func messageArguments(depth: Int, formStart: Int) -> GesValue? {
        guard let name = messageHead() else { return nil }
        var labels: [String] = []
        var args: [GesValue] = []
        if !consume(41) {
            while position < bytes.count {
                if !item() { return nil }
                guard let label = argumentLabel(labels) else { return nil }
                guard let arg = value(depth: depth) else { return nil }
                labels.append(label)
                args.append(arg)
                let next = separator(41)
                if next < 0 { return nil }
                if next > 0 { break }
            }
        }
        return node("@message:" + name, labels, args, offset: formStart)
    }

    // Optional `with` tags and the closing parenthesis of a Message form.
    private mutating func messageEnd(depth: Int, _ message: GesValue, formStart: Int) -> GesValue? {
        whitespace()
        if !starts("with") { return closeMessage(message) }
        position += 4
        var tags: [GesValue] = []
        repeat {
            guard let tag = value(depth: depth) else { return nil }
            tags.append(tag)
            whitespace()
        } while consume(44)
        return closeMessage(taggedMessage(message, tags, formStart: formStart))
    }

    private mutating func taggedMessage(_ message: GesValue, _ tags: [GesValue], formStart: Int) -> GesValue {
        node("message", ["_", "tags"], [message, .list(tags)], offset: formStart)
    }

    private mutating func closeMessage(_ result: GesValue) -> GesValue? {
        consume(41) ? result : nil
    }

    private mutating func spatial(depth: Int, point: Bool) -> GesValue? {
        position += point ? 6 : 7
        whitespace()
        if !consume(40) { return nil }
        if depth >= 64 { return setLimit("MaxLiteralDepth") }
        var components = [Double](repeating: 0, count: 3)
        var unit: GesQuantityUnit?
        var labeled: Bool?
        var previous = -1
        whitespace()
        if !consume(41) {
            while position < bytes.count {
                if !item() { return nil }
                let hasLabel = (120...122).contains(bytes[position])
                if let labeled, labeled != hasLabel { return nil }
                labeled = hasLabel
                var index = previous + 1
                if hasLabel {
                    index = Int(bytes[position] - 120)
                    position += 1
                    whitespace()
                    if !consume(58) { return nil }
                    whitespace()
                }
                if hasLabel && index == previous { return nil }
                if index > 2 || index <= previous || (previous >= 0 && index != previous + 1) { return nil }
                previous = index
                let start = position
                while position < bytes.count && !Self.space(bytes[position]) && bytes[position] != 44 && bytes[position] != 41 { position += 1 }
                if start == position { return nil }
                let token = String(decoding: bytes[start..<position], as: UTF8.self)
                guard let value = GesTextNumberCast.read(token, percentage: token.hasSuffix("%"), allowGrouping: false) else { return nil }
                guard value.numericOnly else { return nil }
                if let unit, unit != value.unit { return nil }
                unit = value.unit
                components[index] = value.asNumber
                let next = separator(41)
                if next < 0 { return nil }
                if next > 0 { return point ? .point(x: components[0], y: components[1], z: components[2], unit: unit!) : .vector(x: components[0], y: components[1], z: components[2], unit: unit!) }
            }
            return nil
        }
        return point ? .point(x: 0) : .vector(x: 0)
    }

    // `:Dice` followed by its bracketed rolls, after the explicit `:Dice(...)` form did not apply.
    private mutating func dice(depth: Int) -> GesValue? {
        position += 5
        whitespace()
        if position == bytes.count || bytes[position] != 91 { return nil }
        if depth >= 64 { return setLimit("MaxLiteralDepth") }
        return readDice()
    }

    private mutating func readDice() -> GesValue? {
        position += 1
        whitespace()
        if consume(93) { return .dice([]) }
        var rolls: [Int32] = []
        while position < bytes.count {
            if !item() { return nil }
            if !GesNames.digit(bytes[position]) { return nil }
            guard let roll = value(depth: 0) else { return nil }
            guard !roll.hasUnit, let integer = roll.integerValue, integer > 0, integer <= Int32.max else { return nil }
            rolls.append(Int32(integer))
            let next = separator(93)
            if next < 0 { return nil }
            if next > 0 { return .dice(rolls) }
        }
        return nil
    }

    private mutating func collection(depth: Int) -> GesValue? {
        switch collectionStart() {
        case 0: return .list([])
        case 1: return .map([])
        case 2: return list(depth: depth)
        case 3: return map(depth: depth)
        default: return nil
        }
    }

    // Opens a collection: 0 and 1 for the closed empty List and Map, 2 and 3 for a List or Map with entries.
    private mutating func collectionStart() -> Int {
        position += 1
        whitespace()
        if consume(93) { return 0 }
        let contentStart = position
        if consume(58) {
            whitespace()
            if consume(93) { return 1 }
            position = contentStart
        }
        let saved = position
        let firstKey = key()
        whitespace()
        let isMap = firstKey != nil && consume(58)
        position = saved
        return isMap ? 3 : 2
    }

    private mutating func list(depth: Int) -> GesValue? {
        var values: [GesValue] = []
        while position < bytes.count {
            if !item() { return nil }
            guard let parsed = value(depth: depth) else { return nil }
            values.append(parsed)
            let next = separator(93)
            if next < 0 { return nil }
            if next > 0 { return .list(values) }
        }
        return nil
    }

    private mutating func map(depth: Int) -> GesValue? {
        var keys: [String] = []
        var values: [GesValue] = []
        while position < bytes.count {
            if !item() { return nil }
            guard let key = mapKey() else { return nil }
            guard let parsed = keyOnly() ? .boolean(true) : value(depth: depth) else { return nil }
            keys.append(key)
            values.append(parsed)
            let next = separator(93)
            if next < 0 { return nil }
            if next > 0 { return node("@map", keys, values) }
        }
        return nil
    }

    // A Map entry key and its colon; nil after a failure.
    private mutating func mapKey() -> String? {
        guard let key = key() else { return nil }
        whitespace()
        return consume(58) ? key : nil
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
    private mutating func separator(_ close: UInt8) -> Int {
        whitespace()
        if consume(close) { return 1 }
        if !consume(44) { return -1 }
        whitespace()
        return position == bytes.count || bytes[position] == close ? -1 : 0
    }

    @discardableResult
    private mutating func setLimit(_ name: String) -> GesValue? {
        if limit == nil { limit = name }
        return nil
    }

    private mutating func item() -> Bool {
        if items >= 65536 {
            setLimit("MaxLiteralItems")
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

    private mutating func whitespace() {
        while position < bytes.count && Self.space(bytes[position]) { position += 1 }
    }

    private static func space(_ byte: UInt8) -> Bool { byte == 32 || byte == 9 || byte == 10 || byte == 13 }

    private func starts(_ token: String) -> Bool { bytes[position...].starts(with: token.utf8) }
}
