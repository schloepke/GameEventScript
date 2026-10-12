// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Invalid, unsupported or over-limit portable message/value JSON.
public struct GameEventScriptMessageFormatError: Error, Equatable {
    /// Stable language-neutral message.* failure code.
    public let code: String

    /// Creates a failure with the supplied stable code.
    public init(code: String) { self.code = code }
}

/// Explicit synchronous V1 product JSON transport. Local Publish never invokes this codec.
/// Ordered arguments are preserved; external values become immutable Record snapshots.
/// Limits: 64 data levels, 65536 values and 4194304 UTF-16 code units of JSON.
/// Decoding never invokes native or script constructors.
public enum GameEventScriptMessageJson {
    /// Serializes a complete message graph. Throws for unsupported or over-limit data and failed external getters.
    public static func serialize(_ message: GameEventScriptMessage) throws -> String {
        try GesJson.write(GesJson.object([("version", .integer(1)), ("message", try GesMessageJsonCodec().encodeMessage(message, 0))]))
    }

    /// Reconstructs a V1 message envelope as data only. Throws a stable format error for invalid input.
    public static func deserialize(_ json: String) throws -> GameEventScriptMessage {
        try GesMessageJsonCodec().decodeMessage(GesMessageJsonCodec.envelope(GesJson.read(json), "message"), 0)
    }

    /// Serializes a value envelope, exporting external objects as Records. Throws for unsupported or over-limit data.
    public static func serializeValue(_ value: GesValue) throws -> String {
        try GesJson.write(GesJson.object([("version", .integer(1)), ("value", try GesMessageJsonCodec().encode(value, 0))]))
    }

    /// Reconstructs a V1 value envelope without executing constructors. External snapshots become Records.
    public static func deserializeValue(_ json: String) throws -> GesValue {
        try GesMessageJsonCodec().decode(GesMessageJsonCodec.envelope(GesJson.read(json), "value"), 0)
    }
}

private final class GesMessageJsonCodec {
    var items = 0

    func enter(_ depth: Int) throws {
        items += 1
        if depth > 64 || items > 65536 { throw GesJson.error("message.resourceLimit") }
    }

    static func fields(_ value: GesValue, _ names: [String]) throws {
        guard value.kind == .map, let map = value.asMap, map.length == names.count, names.allSatisfy({ map.get($0) != nil }) else { throw GesJson.error() }
    }

    static func get(_ value: GesValue, _ name: String) throws -> GesValue {
        guard let field = value.asMap?.get(name) else { throw GesJson.error() }
        return field
    }

    static func string(_ value: GesValue) throws -> String {
        guard value.kind == .text, let text = value.textValue else { throw GesJson.error() }
        return text
    }

    static func list(_ value: GesValue) throws -> [GesValue] {
        guard let list = value.listValue else { throw GesJson.error() }
        return list
    }

    static func typed(_ name: String, _ fields: [(String, GesValue)] = []) -> GesValue { GesJson.object([("type", .text(name))] + fields) }

    static func number(_ value: GesValue) throws -> GesValue {
        let text = try string(value)
        guard !text.contains("%"), let number = TextNumberCast.read(text, percentage: false, allowGrouping: false), number.numericOnly, !number.hasUnit else { throw GesJson.error() }
        return number
    }

    static func messageName(_ value: GesValue) throws -> String {
        let name = try string(value)
        guard GesNames.message(name) || name == "initialization" || name == "undeliverable" else { throw GesJson.error() }
        return name
    }

    static func unit(_ value: GesValue) throws -> GesUnit {
        switch try string(value) {
        case "": .none
        case "s": .second
        case "m": .meter
        case "°": .degree
        default: throw GesJson.error()
        }
    }

    static func envelope(_ value: GesValue, _ content: String) throws -> GesValue {
        try fields(value, ["version", content])
        guard try get(value, "version").integerValue == 1 else { throw GesJson.error("message.unsupportedVersion") }
        return try get(value, content)
    }

    func encodeMessage(_ message: GameEventScriptMessage, _ depth: Int) throws -> GesValue {
        try enter(depth)
        _ = try Self.messageName(.text(message.name))
        let arguments = try (0..<message.arguments.count).map { index in
            GesJson.object([("name", .text(message.arguments.nameAt(index))), ("value", try encode(message.arguments[index], depth + 1))])
        }
        return GesJson.object([("name", .text(message.name)), ("args", .list(arguments)), ("tags", .list(message.tags.map(GesValue.text)))])
    }

    func decodeMessage(_ value: GesValue, _ depth: Int) throws -> GameEventScriptMessage {
        try enter(depth)
        try Self.fields(value, ["name", "args", "tags"])
        let args = try Self.list(Self.get(value, "args")).map { item -> GameEventScriptMessageArgument in
            try Self.fields(item, ["name", "value"])
            let label = try Self.string(Self.get(item, "name"))
            if label != "_" && !GesNames.plain(label) { throw GesJson.error() }
            return try .init(name: label, value: decode(Self.get(item, "value"), depth + 1))
        }
        let tags = try Self.list(Self.get(value, "tags")).map { item in
            let name = try Self.string(item)
            if !GesNames.plain(name) { throw GesJson.error() }
            return name
        }
        do { return try .init(name: Self.messageName(Self.get(value, "name")), arguments: args, tags: tags) } catch is GameEventScriptMessageError { throw GesJson.error() }
    }

    func encode(_ value: GesValue, _ depth: Int) throws -> GesValue {
        try enter(depth)
        switch value.kind {
        case .nothing: return Self.typed("Nothing")
        case .integer, .float:
            return Self.typed("Number", [("value", .text(value.integerValue.map(String.init) ?? GesNumber.format(value.asNumber))), ("unit", .text(value.unit.suffix))])
        case .percentage: return Self.typed("Percentage", [("value", .text(GesNumber.format(value.asNumber)))])
        case .boolean: return Self.typed("Boolean", [("value", value)])
        case .text: return Self.typed("Text", [("value", value)])
        case .tag: return Self.typed("Tag", [("value", .text(value.textValue!))])
        case .list: return Self.typed("List", [("items", .list(try value.asList.map { try encode($0, depth + 1) }))])
        case .map, .record, .external:
            guard let map = try value.materializedMap() else { throw GesJson.error() }
            let entries = try map.entries.map { entry in GesJson.object([("key", .text(entry.key)), ("value", try encode(entry.value, depth + 1))]) }
            if value.kind == .map { return Self.typed("Map", [("entries", .list(entries))]) }
            guard let name = value.customTypeName, GesNames.type(name) else { throw GesJson.error() }
            return Self.typed("Record", [("name", .text(name)), ("entries", .list(entries))])
        case .vector, .point:
            let spatial = value.spatialValue!
            return Self.typed(value.kind == .vector ? "Vector" : "Point", [("x", .text(GesNumber.format(spatial.x))), ("y", .text(GesNumber.format(spatial.y))), ("z", .text(GesNumber.format(spatial.z))), ("unit", .text(value.unit.suffix))])
        case .dice:
            guard value.diceRolls!.allSatisfy({ $0 > 0 }) else { throw GesJson.error() }
            return Self.typed("Dice", [("rolls", .list(value.diceRolls!.map { .text(String($0)) }))])
        case .integerRange:
            let range = value.integerRangeValue!
            return Self.typed("Range", [("from", .text(String(range.from))), ("to", .text(String(range.to))), ("step", .text(String(range.step)))])
        case .floatRange:
            let range = value.floatRangeValue!
            return Self.typed("Range", [("from", .text(GesNumber.format(range.from))), ("to", .text(GesNumber.format(range.to))), ("step", .text(GesNumber.format(range.step)))])
        case .series:
            let series = value.seriesValue!
            if !["fibonacci", "factorial"].contains(series.signatureID) { throw GesJson.error("message.unsupportedSeries") }
            if series.offset < 0 { throw GesJson.error() }
            return Self.typed("Series", [("kind", .text(series.signatureID)), ("offset", .text(String(series.offset)))])
        case .handler:
            let signature = value.signatureValue!
            _ = try Self.messageName(.text(signature.name))
            return Self.typed("Handler", [("name", .text(signature.name)), ("labels", .list(signature.parameters.map(GesValue.text)))])
        case .message: return Self.typed("Message", [("value", try encodeMessage(value.messageValue!, depth + 1))])
        }
    }

    func decode(_ value: GesValue, _ depth: Int) throws -> GesValue {
        try enter(depth)
        let type = try Self.string(Self.get(value, "type"))

        func get(_ name: String) throws -> GesValue { try Self.get(value, name) }

        func fields(_ names: String...) throws { try Self.fields(value, ["type"] + names) }

        switch type {
        case "Nothing":
            try fields()
            return .nothing
        case "Number":
            try fields("value", "unit")
            let number = try Self.number(get("value"))
            let unit = try Self.unit(get("unit"))
            return number.integerValue.map { .integer($0, unit: unit) } ?? .float(number.asNumber, unit: unit)
        case "Percentage":
            try fields("value")
            let ratio = try Self.number(get("value")).asNumber
            guard ratio.isFinite else { throw GesJson.error() }
            return .percentage(ratio)
        case "Text":
            try fields("value")
            return .text(try Self.string(get("value")))
        case "Tag":
            try fields("value")
            let tag = try Self.string(get("value"))
            if !GesNames.plain(tag) { throw GesJson.error() }
            return try .tag(tag)
        case "Boolean":
            try fields("value")
            let boolean = try get("value")
            if boolean.kind != .boolean { throw GesJson.error() }
            return boolean
        case "List":
            try fields("items")
            return .list(try Self.list(get("items")).map { try decode($0, depth + 1) })
        case "Map", "Record":
            if type == "Map" { try fields("entries") } else { try fields("entries", "name") }
            var seen = Set<GesValue>()
            let entries = try Self.list(get("entries")).map { item -> GesMapEntry in
                try Self.fields(item, ["key", "value"])
                let key = try Self.string(Self.get(item, "key"))
                if !seen.insert(.text(key)).inserted { throw GesJson.error() }
                return .init(key: key, value: try decode(Self.get(item, "value"), depth + 1))
            }
            if type == "Map" { return .map(entries) }
            let name = try Self.string(get("name"))
            if !GesNames.type(name) { throw GesJson.error() }
            return .record(typeName: name, entries: entries)
        case "Vector", "Point":
            try fields("x", "y", "z", "unit")
            let x = try Self.number(get("x")).asNumber
            let y = try Self.number(get("y")).asNumber
            let z = try Self.number(get("z")).asNumber
            let unit = try Self.unit(get("unit"))
            return type == "Vector" ? .vector(x: x, y: y, z: z, unit: unit) : .point(x: x, y: y, z: z, unit: unit)
        case "Dice":
            try fields("rolls")
            return .dice(
                try Self.list(get("rolls")).map { item in
                    guard let roll = try Self.number(item).integerValue, roll > 0, roll <= Int32.max else { throw GesJson.error() }
                    return Int32(roll)
                }
            )
        case "Range":
            try fields("from", "to", "step")
            let from = try Self.number(get("from"))
            let to = try Self.number(get("to"))
            let step = try Self.number(get("step"))
            guard from.asNumber.isFinite, to.asNumber.isFinite, step.asNumber.isFinite else { throw GesJson.error() }
            if let first = from.integerValue, let last = to.integerValue, let stride = step.integerValue { return .integerRange(from: first, to: last, step: stride) }
            return .floatRange(from: from.asNumber, to: to.asNumber, step: step.asNumber)
        case "Series":
            try fields("kind", "offset")
            let kind = try Self.string(get("kind"))
            if !["fibonacci", "factorial"].contains(kind) { throw GesJson.error("message.unsupportedSeries") }
            guard let offset = try Self.number(get("offset")).integerValue, offset >= 0 else { throw GesJson.error() }
            return .series(.init(signatureID: kind, offset: offset))
        case "Handler":
            try fields("name", "labels")
            let labels = try Self.list(get("labels")).map { item in
                let label = try Self.string(item)
                if label != "_" && !GesNames.plain(label) { throw GesJson.error() }
                return label
            }
            do { return .handler(try .init(name: Self.messageName(get("name")), parameters: labels)) } catch is GameEventScriptMessageError { throw GesJson.error() }
        case "Message":
            try fields("value")
            return .message(try decodeMessage(get("value"), depth + 1))
        default: throw GesJson.error()
        }
    }
}
