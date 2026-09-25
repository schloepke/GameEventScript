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
    private var nodes: [GesLiteralNode] = []
    private var state: GesVmState?

    static func evaluate(_ input: GesValue, context: GameEventScriptContext, state: GesVmState, destination: Int) throws {
        let (value, nodes) = parse(input, context: context, state: state)
        if nodes.isEmpty { state.set(destination, value) } else { try GesLiteralEvaluation(value, nodes: nodes, state: state, context: context, destination: destination).run() }
    }

    static func parse(_ input: GesValue, context: GameEventScriptContext, state: GesVmState? = nil) -> (GesValue, [GesLiteralNode]) {
        guard input.kind == .text, let text = input.textValue else { return (.nothing, []) }
        if text.unicodeScalars.count > 1_048_576 {
            context.budget.exhaust("MaxLiteralInputScalars", 1_048_576)
            return (.nothing, [])
        }
        var parser = Self(bytes: Array(text.utf8))
        parser.state = state
        let result = parser.value(depth: 0)
        parser.whitespace()
        if let limit = parser.limit {
            context.budget.exhaust(limit, limit == "MaxLiteralDepth" ? 64 : 65536)
            return (.nothing, [])
        }
        if parser.position == parser.bytes.count, let result { return (result, parser.nodes) }
        return (input, [])
    }

    private mutating func value(depth: Int) -> GesValue? {
        whitespace()
        if position == bytes.count { return nil }
        if bytes[position] == 34 || bytes[position] == 39 { return quoted().map(GesValue.text) }
        if starts(":Vector") { return spatial(depth: depth, point: false) }
        if starts(":Point") { return spatial(depth: depth, point: true) }
        if bytes[position] == 58 && !starts(":Dice[") {
            let saved = position
            if let result = typed(depth: depth) { return result }
            position = saved
        }
        let dice = starts(":Dice")
        if dice {
            position += 5
            whitespace()
            if position == bytes.count || bytes[position] != 91 { return nil }
        }
        if bytes[position] == 91 {
            if depth >= 64 {
                limit = "MaxLiteralDepth"
                return nil
            }
            return dice ? readDice() : collection(depth: depth + 1)
        }
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
            return GesNames.plain(tag) ? try? .tag(tag) : nil
        }
        return TextNumberCast.read(token, percentage: token.hasSuffix("%"), allowGrouping: false)
    }

    private mutating func node(_ type: String, _ labels: [String], _ arguments: [GesValue], _ constructor: GameEventScriptBinding? = nil) -> GesValue {
        let index = nodes.count
        nodes.append(.init(type: type, labels: labels, arguments: arguments, constructor: constructor))
        return .record(typeName: "@literal", entries: [.init(key: "id", value: .integer(Int64(index)))])
    }

    private mutating func name() -> String? {
        let start = position
        while position < bytes.count && (GesNames.lower(bytes[position]) || (65...90).contains(bytes[position]) || GesNames.digit(bytes[position]) || bytes[position] == 95) { position += 1 }
        return start == position ? nil : String(decoding: bytes[start..<position], as: UTF8.self)
    }

    private mutating func typed(depth: Int) -> GesValue? {
        if depth >= 64 {
            limit = "MaxLiteralDepth"
            return nil
        }
        position += 1
        guard let name = name(), GesNames.type(name) else { return nil }
        let type = name.lowercased()
        let builtin = GesDataConstruction.types.contains(type) && name == type.prefix(1).uppercased() + type.dropFirst()
        let constructor = builtin ? nil : state?.linked?.recordsByName[name]
        if !builtin && constructor == nil { return nil }
        whitespace()
        guard consume(40) else { return nil }
        whitespace()
        if ["handler", "message"].contains(type), position < bytes.count, (65...90).contains(bytes[position]) || starts("initialization") || starts("undeliverable") { return messageForm(depth: depth + 1, handler: type == "handler") }
        var labels: [String] = []
        var args: [GesValue] = []
        if !consume(41) {
            while position < bytes.count {
                if !item() { return nil }
                let saved = position
                var label = key()
                whitespace()
                if label == nil || !consume(58) {
                    label = "_"
                    position = saved
                } else if !GesNames.plain(label!) || labels.contains(label!) {
                    return nil
                }
                whitespace()
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
                whitespace()
                if consume(41) { break }
                guard consume(44) else { return nil }
                whitespace()
                if position == bytes.count || bytes[position] == 41 { return nil }
            }
            if position == 0 || bytes[position - 1] != 41 { return nil }
        }
        if builtin && GesDataConstruction.positions(type, labels) == nil { return nil }
        if let constructor, let state {
            let allowed = constructor.argumentNames.map(state.text)
            if labels.filter({ $0 == "_" }).count > allowed.filter({ $0 == "_" }).count || labels.contains(where: { $0 != "_" && !allowed.contains($0) }) { return nil }
        }
        return node(type, labels, args, constructor)
    }

    private mutating func messageForm(depth: Int, handler: Bool) -> GesValue? {
        guard let name = name(), GesNames.message(name) else { return nil }
        whitespace()
        guard consume(40) else { return nil }
        whitespace()
        var labels: [String] = []
        var args: [GesValue] = []
        if !consume(41) {
            while position < bytes.count {
                if !item() { return nil }
                if handler {
                    guard let label = self.name(), label == "_" || GesNames.plain(label) else { return nil }
                    if label != "_" && labels.contains(label) { return nil }
                    labels.append(label)
                } else {
                    let saved = position
                    var label = key()
                    whitespace()
                    if label == nil || !consume(58) {
                        label = "_"
                        position = saved
                    } else if !GesNames.plain(label!) || labels.contains(label!) {
                        return nil
                    }
                    guard let arg = value(depth: depth) else { return nil }
                    labels.append(label!)
                    args.append(arg)
                }
                whitespace()
                if consume(41) { break }
                guard consume(44) else { return nil }
                whitespace()
                if position == bytes.count || bytes[position] == 41 { return nil }
            }
        }
        whitespace()
        var result: GesValue
        if handler {
            guard let signature = try? GameEventScriptMessageSignature(name: name, parameters: labels) else { return nil }
            result = .handler(signature)
        } else {
            result = node("@message:" + name, labels, args)
        }
        if !handler && starts("with") {
            position += 4
            var tags: [GesValue] = []
            repeat {
                guard let tag = value(depth: depth) else { return nil }
                tags.append(tag)
                whitespace()
            } while consume(44)
            result = node("message", ["_", "tags"], [result, .list(tags)])
        }
        return consume(41) ? result : nil
    }

    private mutating func spatial(depth: Int, point: Bool) -> GesValue? {
        position += point ? 6 : 7
        whitespace()
        if !consume(40) { return nil }
        if depth >= 64 {
            limit = "MaxLiteralDepth"
            return nil
        }
        var components = [Double](repeating: 0, count: 3)
        var unit: GesUnit?
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
                if index > 2 || index <= previous { return nil }
                previous = index
                let start = position
                while position < bytes.count && !Self.space(bytes[position]) && bytes[position] != 44 && bytes[position] != 41 { position += 1 }
                if start == position { return nil }
                let token = String(decoding: bytes[start..<position], as: UTF8.self)
                guard let value = TextNumberCast.read(token, percentage: token.hasSuffix("%"), allowGrouping: false), value.numericOnly else { return nil }
                if let unit, unit != value.unit { return nil }
                unit = value.unit
                components[index] = value.asNumber
                whitespace()
                if consume(41) { return point ? .point(x: components[0], y: components[1], z: components[2], unit: unit!) : .vector(x: components[0], y: components[1], z: components[2], unit: unit!) }
                if !consume(44) { return nil }
                whitespace()
                if position == bytes.count || bytes[position] == 41 { return nil }
            }
            return nil
        }
        return point ? .point(x: 0) : .vector(x: 0)
    }

    private mutating func readDice() -> GesValue? {
        position += 1
        whitespace()
        if consume(93) { return .dice([]) }
        var rolls: [Int32] = []
        while position < bytes.count {
            if !item() || !GesNames.digit(bytes[position]) { return nil }
            guard let roll = value(depth: 0), !roll.hasUnit, let integer = roll.integerValue, integer > 0, integer <= Int32.max else { return nil }
            rolls.append(Int32(integer))
            whitespace()
            if consume(93) { return .dice(rolls) }
            if !consume(44) { return nil }
            whitespace()
            if position == bytes.count || bytes[position] == 93 { return nil }
        }
        return nil
    }

    private mutating func collection(depth: Int) -> GesValue? {
        position += 1
        whitespace()
        if consume(93) { return .list([]) }
        let contentStart = position
        if consume(58) {
            whitespace()
            if consume(93) { return .map([]) }
            position = contentStart
        }
        let saved = position
        let firstKey = key()
        whitespace()
        let isMap = firstKey != nil && consume(58)
        position = saved
        var values: [GesValue] = []
        var entries: [GesMapEntry] = []
        while position < bytes.count {
            if !item() { return nil }
            if isMap {
                let key = key()
                whitespace()
                guard let key, consume(58) else { return nil }
                whitespace()
                let parsed: GesValue? = position < bytes.count && (bytes[position] == 44 || bytes[position] == 93) ? .boolean(true) : value(depth: depth)
                guard let parsed else { return nil }
                entries.append(.init(key: key, value: parsed))
            } else {
                guard let parsed = value(depth: depth) else { return nil }
                values.append(parsed)
            }
            whitespace()
            if consume(93) { return isMap ? node("@map", entries.map(\.key), entries.map(\.value)) : .list(values) }
            if !consume(44) { return nil }
            whitespace()
            if position == bytes.count || bytes[position] == 93 { return nil }
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

    private mutating func item() -> Bool {
        if items >= 65536 {
            limit = "MaxLiteralItems"
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

    private mutating func whitespace() { while position < bytes.count && Self.space(bytes[position]) { position += 1 } }

    private static func space(_ byte: UInt8) -> Bool { byte == 32 || byte == 9 || byte == 10 || byte == 13 }

    private func starts(_ token: String) -> Bool { bytes[position...].starts(with: token.utf8) }
}
