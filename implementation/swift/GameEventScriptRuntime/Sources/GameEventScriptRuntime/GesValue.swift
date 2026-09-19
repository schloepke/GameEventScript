// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Portable quantity units. The names do not depend on the embedding platform.
public enum GesUnit: String, Hashable {
    case none, meter, second, degree

    public var suffix: String {
        switch self {
        case .none: ""
        case .meter: "m"
        case .second: "s"
        case .degree: "°"
        }
    }
}

/// Exact storage discriminants; integer and binary64 range payloads remain distinct.
public enum GesValueKind: Hashable {
    case nothing, boolean, integer, float, percentage, text, tag, vector, point
    case dice, list, map, record, integerRange, floatRange, message, handler, series
}

/// An immutable vector or point's binary64 coordinates.
public struct GesSpatialValue: Hashable {
    public let x: Double
    public let y: Double
    public let z: Double

    public init(x: Double, y: Double = 0, z: Double = 0) {
        self.x = GesNumber.canonicalZero(x)
        self.y = GesNumber.canonicalZero(y)
        self.z = GesNumber.canonicalZero(z)
    }
}

/// Invalid arguments to public value factories.
public enum GesValueError: Error, Equatable {
    case invalidTag(String)
}

/// Immutable metadata for a built-in series; execution of series terms belongs to the VM.
public struct GesSeriesValue: Hashable {
    public let signatureID: String
    public let offset: Int64

    public init(signatureID: String, offset: Int64 = 0) {
        self.signatureID = signatureID
        self.offset = offset
    }

    public static func == (left: Self, right: Self) -> Bool {
        GesText.scalarEqual(left.signatureID, right.signatureID) && left.offset == right.offset
    }

    public func hash(into hasher: inout Hasher) {
        GesText.hashScalars(signatureID, into: &hasher)
        hasher.combine(offset)
    }
}

private struct ScalarText: Hashable {
    let value: String

    static func == (left: Self, right: Self) -> Bool { GesText.scalarEqual(left.value, right.value) }
    func hash(into hasher: inout Hasher) { GesText.hashScalars(value, into: &hasher) }
}

/// Immutable portable GES value with strict structural equality and copy-on-write collection ownership.
/// Factories canonicalize invalid numeric values to Nothing and never expose mutable shared storage.
public struct GesValue: Hashable, CustomStringConvertible {
    private enum Storage: Hashable {
        case nothing
        case boolean(Bool)
        case integer(Int64)
        case float(Double)
        case percentage(Double)
        case text(ScalarText)
        case tag(ScalarText)
        case vector(GesSpatialValue)
        case point(GesSpatialValue)
        case dice([Int32])
        case list([GesValue])
        case map(GesValueMap)
        case record(ScalarText, GesValueMap)
        case integerRange(GesIntegerRange)
        case floatRange(GesFloatRange)
        indirect case message(GameEventScriptMessage)
        indirect case handler(GameEventScriptMessageSignature)
        case series(GesSeriesValue)
    }

    private let storage: Storage
    public let unit: GesUnit

    private init(_ storage: Storage, unit: GesUnit = .none) {
        self.storage = storage
        self.unit = unit
    }

    public static var nothing: Self { Self(.nothing) }
    public static func boolean(_ value: Bool) -> Self { Self(.boolean(value)) }
    public static func integer(_ value: Int64, unit: GesUnit = .none) -> Self { Self(.integer(value), unit: unit) }

    /// Creates a number, normalizing NaN, zero, and finite integral Int64 values.
    public static func float(_ value: Double, unit: GesUnit = .none) -> Self {
        if value.isNaN { return .nothing }
        if let integer = GesNumber.exactInteger(value) { return .integer(integer, unit: unit) }
        return Self(.float(value), unit: unit)
    }

    /// Stores finite ratios as Percentage. Infinite ratios become unitless binary64 infinity.
    public static func percentage(_ ratio: Double) -> Self {
        if !ratio.isFinite { return .float(ratio) }
        return Self(.percentage(GesNumber.canonicalZero(ratio)))
    }

    public static func text(_ value: String) -> Self { Self(.text(ScalarText(value: value))) }

    public static func tag(_ name: String) throws -> Self {
        guard GesText.isLowerName(name) else { throw GesValueError.invalidTag(name) }
        return Self(.tag(ScalarText(value: name)))
    }

    public static func vector(x: Double, y: Double = 0, z: Double = 0, unit: GesUnit = .none) -> Self {
        guard !x.isNaN, !y.isNaN, !z.isNaN else { return .nothing }
        return Self(.vector(GesSpatialValue(x: x, y: y, z: z)), unit: unit)
    }

    public static func point(x: Double, y: Double = 0, z: Double = 0, unit: GesUnit = .none) -> Self {
        guard !x.isNaN, !y.isNaN, !z.isNaN else { return .nothing }
        return Self(.point(GesSpatialValue(x: x, y: y, z: z)), unit: unit)
    }

    public static func dice(_ rolls: [Int32]) -> Self { Self(.dice(rolls.sorted(by: >))) }
    public static func list(_ values: [GesValue]) -> Self { Self(.list(values)) }
    public static func map(_ entries: [GesMapEntry]) -> Self { Self(.map(GesValueMap(entries))) }
    public static func record(typeName: String, entries: [GesMapEntry]) -> Self {
        Self(.record(ScalarText(value: typeName), GesValueMap(entries)))
    }

    public static func integerRange(from: Int64, to: Int64, step: Int64 = 1) -> Self {
        let range = GesIntegerRange(from: from, to: to, step: step)
        return Self(.integerRange(range.count == 0 ? GesIntegerRange(from: 0, to: 0, step: 0) : range))
    }

    public static func floatRange(from: Double, to: Double, step: Double = 1) -> Self {
        guard from.isFinite, to.isFinite, step.isFinite else { return .nothing }
        let range = GesFloatRange(from: from, to: to, step: step)
        return range.count == 0 ? .integerRange(from: 0, to: 0, step: 0) : Self(.floatRange(range))
    }

    public static func message(_ value: GameEventScriptMessage) -> Self { Self(.message(value)) }
    public static func handler(_ value: GameEventScriptMessageSignature) -> Self { Self(.handler(value)) }
    public static func series(_ value: GesSeriesValue) -> Self { Self(.series(value)) }

    public var kind: GesValueKind {
        switch storage {
        case .nothing: .nothing
        case .boolean: .boolean
        case .integer: .integer
        case .float: .float
        case .percentage: .percentage
        case .text: .text
        case .tag: .tag
        case .vector: .vector
        case .point: .point
        case .dice: .dice
        case .list: .list
        case .map: .map
        case .record: .record
        case .integerRange: .integerRange
        case .floatRange: .floatRange
        case .message: .message
        case .handler: .handler
        case .series: .series
        }
    }

    public var isNumeric: Bool {
        switch storage {
        case .boolean, .integer, .float, .percentage, .dice: true
        default: false
        }
    }

    public var isNothing: Bool { kind == .nothing }
    public var hasUnit: Bool { unit != .none }

    public var hasValue: Bool {
        switch storage {
        case .nothing: false
        case .text(let text): !text.value.isEmpty
        case .list(let values): !values.isEmpty
        case .dice(let values): !values.isEmpty
        case .map(let map), .record(_, let map): map.length != 0
        case .integerRange(let range): range.count != 0
        case .floatRange(let range): range.count != 0
        default: true
        }
    }

    public var asBoolean: Bool {
        switch storage {
        case .boolean(let value): value
        case .integer(let value): value != 0
        case .float(let value), .percentage(let value): value != 0
        case .vector(let value), .point(let value): value.x != 0 || value.y != 0 || value.z != 0
        case .text(let value):
            value.value.utf8.elementsEqual("1".utf8)
                || value.value.utf8.lazy.map { $0 >= 65 && $0 <= 90 ? $0 + 32 : $0 }.elementsEqual("true".utf8)
        default: false
        }
    }

    public var asNumber: Double {
        switch storage {
        case .boolean(let value): value ? 1 : 0
        case .integer(let value): Double(value)
        case .float(let value), .percentage(let value): value
        case .dice(let values): Double(values.reduce(Int64(0)) { $0 + Int64($1) })
        default: .nan
        }
    }

    public var asInteger: Int64 {
        if case .integer(let value) = storage { return value }
        return GesNumber.saturatedInteger(asNumber)
    }

    /// Number of Unicode scalars or collection entries; range lengths saturate at Int32.max like the API.
    /// Values without a logical length return zero; numeric payload bits never define a length.
    public var length: Int {
        let count: Int64
        switch storage {
        case .text(let value), .tag(let value): count = Int64(value.value.unicodeScalars.count)
        case .list(let values): count = Int64(values.count)
        case .dice(let values): count = Int64(values.count)
        case .map(let map), .record(_, let map): count = Int64(map.length)
        case .integerRange(let range): count = range.count
        case .floatRange(let range): count = range.count
        default: count = 0
        }
        return Int(min(count, Int64(Int32.max)))
    }

    public var integerValue: Int64? { if case .integer(let value) = storage { value } else { nil } }
    public var floatValue: Double? {
        switch storage {
        case .float(let value), .percentage(let value): value
        default: nil
        }
    }
    public var textValue: String? {
        switch storage {
        case .text(let value), .tag(let value): value.value
        default: nil
        }
    }
    public var spatialValue: GesSpatialValue? {
        switch storage {
        case .vector(let value), .point(let value): value
        default: nil
        }
    }
    public var x: Double { spatialValue?.x ?? 0 }
    public var y: Double { spatialValue?.y ?? 0 }
    public var z: Double { spatialValue?.z ?? 0 }
    public var listValue: [GesValue]? { if case .list(let value) = storage { value } else { nil } }
    public var diceRolls: [Int32]? { if case .dice(let value) = storage { value } else { nil } }
    public var asMap: GesValueMap? {
        switch storage {
        case .map(let value), .record(_, let value): value
        default: nil
        }
    }
    public var asList: [GesValue] { listValue ?? [] }
    public var asDice: [Int32] { diceRolls ?? [] }
    public var mapEntries: [GesMapEntry]? { asMap?.entries }
    public var customTypeName: String? { if case .record(let name, _) = storage { name.value } else { nil } }
    public var integerRangeValue: GesIntegerRange? { if case .integerRange(let value) = storage { value } else { nil } }
    public var floatRangeValue: GesFloatRange? { if case .floatRange(let value) = storage { value } else { nil } }
    public var messageValue: GameEventScriptMessage? { if case .message(let value) = storage { value } else { nil } }
    public var signatureValue: GameEventScriptMessageSignature? {
        if case .handler(let value) = storage { value } else { nil }
    }
    public var seriesValue: GesSeriesValue? { if case .series(let value) = storage { value } else { nil } }

    /// Typed API text view. Tags expose their name; use toText for the language representation with '#'.
    public var asText: String { textValue ?? toText }
    public var description: String { toText }

    /// Language text representation, quoting nested Text values and preserving canonical map ordering.
    public var toText: String {
        switch storage {
        case .nothing: return "nothing"
        case .boolean(let value): return value ? "true" : "false"
        case .integer(let value): return String(value) + unit.suffix
        case .float(let value): return GesNumber.format(value) + unit.suffix
        case .percentage(let value): return GesNumber.formatPercentage(value)
        case .text(let value): return value.value
        case .tag(let value): return "#" + value.value
        case .vector(let value): return formatSpatial(":Vector", value)
        case .point(let value): return formatSpatial(":Point", value)
        case .dice(let values): return ":Dice[" + values.map(String.init).joined(separator: ", ") + "]"
        case .list(let values): return "[" + values.map(\.nestedText).joined(separator: ", ") + "]"
        case .map(let map), .record(_, let map):
            return (map.length == 0 ? "[:" : "[")
                + map.entries.map {
                    (GesText.isLowerName($0.key) ? $0.key : GesText.quoted($0.key)) + ": " + $0.value.nestedText
                }.joined(separator: ", ") + "]"
        case .integerRange(let range): return "range[\(range.from) to \(range.to) step \(range.step)]"
        case .floatRange(let range):
            return
                "range[\(GesNumber.format(range.from)) to \(GesNumber.format(range.to)) step \(GesNumber.format(range.step))]"
        case .message(let value): return value.description
        case .handler(let value): return "handler " + value.signatureId
        case .series(let value): return "series[\(value.signatureID) offset \(value.offset)]"
        }
    }

    private var nestedText: String {
        if case .text(let value) = storage { return GesText.quoted(value.value) }
        return toText
    }

    private func formatSpatial(_ name: String, _ value: GesSpatialValue) -> String {
        "\(name)(x: \(GesNumber.format(value.x))\(unit.suffix), y: \(GesNumber.format(value.y))\(unit.suffix), z: \(GesNumber.format(value.z))\(unit.suffix))"
    }
}
