// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Portable quantity units. The names do not depend on the embedding platform.
public enum GesUnit: String, Hashable {
    /// A unitless value.
    case none
    /// Distance measured in meters.
    case meter
    /// Duration measured in seconds.
    case second
    /// Angle measured in degrees.
    case degree

    /// Portable text suffix: m, s, °, or an empty string for unitless values.
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
    /// Absence of a value.
    case nothing
    /// Boolean value.
    case boolean
    /// Exact signed Int64 number.
    case integer
    /// IEEE 754 binary64 number.
    case float
    /// Binary64 ratio carrying Percentage kind.
    case percentage
    /// Unicode text.
    case text
    /// Validated tag name.
    case tag
    /// Three-coordinate vector.
    case vector
    /// Three-coordinate point.
    case point
    /// Descending Int32 dice rolls.
    case dice
    /// Ordered immutable value collection.
    case list
    /// Canonical Text-keyed value map.
    case map
    /// A script record carrying its declared type name and fields.
    case record
    /// A lazy range with exact Int64 bounds and step.
    case integerRange
    /// A lazy range with binary64 bounds and step.
    case floatRange
    /// Message signature, arguments and tags.
    case message
    /// Message-handler signature.
    case handler
    /// Lazy built-in numeric series.
    case series
    /// A host-provided external value.
    case external
}

/// An immutable vector or point's binary64 coordinates.
public struct GesSpatialValue: Hashable {
    /// Binary64 x coordinate.
    public let x: Double
    /// Binary64 y coordinate.
    public let y: Double
    /// Binary64 z coordinate.
    public let z: Double

    /// Creates coordinates, normalizing signed zero; omitted y and z coordinates are zero.
    public init(x: Double, y: Double = 0, z: Double = 0) {
        self.x = GesNumber.canonicalZero(x)
        self.y = GesNumber.canonicalZero(y)
        self.z = GesNumber.canonicalZero(z)
    }
}

/// Invalid arguments to public value factories.
public enum GesValueError: Error, Equatable {
    /// The associated name violates the portable lowercase tag-name grammar.
    case invalidTag(String)
}

/// Immutable metadata for a built-in series; execution of series terms belongs to the VM.
public struct GesSeriesValue: Hashable {
    /// Portable series signature identifying the built-in series.
    public let signatureID: String
    /// Signed term offset applied before series evaluation.
    public let offset: Int64

    /// Creates a series descriptor with a signature and optional term offset.
    public init(signatureID: String, offset: Int64 = 0) {
        self.signatureID = signatureID
        self.offset = offset
    }

    /// Compares scalar-exact series signatures and exact term offsets.
    public static func == (left: Self, right: Self) -> Bool {
        GesText.scalarEqual(left.signatureID, right.signatureID) && left.offset == right.offset
    }

    /// Hashes the scalar-exact signature and offset consistently with equality.
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
        case message(GameEventScriptMessage)
        case handler(GameEventScriptMessageSignature)
        case series(GesSeriesValue)
        case external(GesExternalStorage)
    }

    private let storage: Storage
    /// Quantity unit carried by numeric or spatial storage.
    public let unit: GesUnit

    private init(_ storage: Storage, unit: GesUnit = .none) {
        self.storage = storage
        self.unit = unit
    }

    /// The canonical absence value.
    public static var nothing: Self { Self(.nothing) }
    /// Creates a Boolean value.
    public static func boolean(_ value: Bool) -> Self { Self(.boolean(value)) }
    /// Creates an exact signed Int64 value with an optional quantity unit.
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

    /// Preserves text verbatim; equality uses Unicode scalars rather than canonical-equivalence normalization.
    public static func text(_ value: String) -> Self { Self(.text(ScalarText(value: value))) }

    /// Creates a tag from its name without a leading #.
    ///
    /// - Throws: `GesValueError.invalidTag` when the name violates the lowercase portable grammar.
    public static func tag(_ name: String) throws -> Self {
        guard GesText.isLowerName(name) else { throw GesValueError.invalidTag(name) }
        return Self(.tag(ScalarText(value: name)))
    }

    /// Creates a binary64 vector with optional unit. Any NaN coordinate yields Nothing; signed zeros are normalized.
    public static func vector(x: Double, y: Double = 0, z: Double = 0, unit: GesUnit = .none) -> Self {
        guard !x.isNaN, !y.isNaN, !z.isNaN else { return .nothing }
        return Self(.vector(GesSpatialValue(x: x, y: y, z: z)), unit: unit)
    }

    /// Creates a binary64 point with optional unit. Any NaN coordinate yields Nothing; signed zeros are normalized.
    public static func point(x: Double, y: Double = 0, z: Double = 0, unit: GesUnit = .none) -> Self {
        guard !x.isNaN, !y.isNaN, !z.isNaN else { return .nothing }
        return Self(.point(GesSpatialValue(x: x, y: y, z: z)), unit: unit)
    }

    /// Copies and sorts supplied Int32 rolls in descending order without drawing random numbers.
    public static func dice(_ rolls: [Int32]) -> Self { Self(.dice(rolls.sorted(by: >))) }
    /// Creates an immutable ordered list of values.
    public static func list(_ values: [GesValue]) -> Self { Self(.list(values)) }
    /// Creates a map in scalar key order, keeping the last entry for duplicate keys.
    public static func map(_ entries: [GesMapEntry]) -> Self { Self(.map(GesValueMap(entries))) }
    /// Creates record storage with a type name and canonical field map; does not invoke a script constructor.
    public static func record(typeName: String, entries: [GesMapEntry]) -> Self {
        Self(.record(ScalarText(value: typeName), GesValueMap(entries)))
    }

    /// Creates an exact lazy Int64 range; a zero step or incompatible direction yields the canonical empty range.
    public static func integerRange(from: Int64, to: Int64, step: Int64 = 1) -> Self {
        let range = GesIntegerRange(from: from, to: to, step: step)
        return Self(.integerRange(range.count == 0 ? GesIntegerRange(from: 0, to: 0, step: 0) : range))
    }

    /// Creates a lazy binary64 range. Nonfinite bounds or step yield Nothing; empty results use the canonical empty
    /// integer range.
    public static func floatRange(from: Double, to: Double, step: Double = 1) -> Self {
        guard from.isFinite, to.isFinite, step.isFinite else { return .nothing }
        let range = GesFloatRange(from: from, to: to, step: step)
        return range.count == 0 ? .integerRange(from: 0, to: 0, step: 0) : Self(.floatRange(range))
    }

    /// Wraps a message as a value.
    public static func message(_ value: GameEventScriptMessage) -> Self { Self(.message(value)) }
    /// Wraps a message signature as a Handler value.
    public static func handler(_ value: GameEventScriptMessageSignature) -> Self { Self(.handler(value)) }
    /// Wraps a built-in series descriptor without evaluating terms.
    public static func series(_ value: GesSeriesValue) -> Self { Self(.series(value)) }

    /// Wraps a host-provided external value and captures its declared type definition.
    public static func external(_ value: any GameEventScriptExternalValue) -> Self {
        Self(.external(GesExternalStorage(value: value)))
    }
    /// Underlying host value for external storage, otherwise nil.
    public var externalValue: (any GameEventScriptExternalValue)? {
        if case .external(let storage) = storage { storage.value } else { nil }
    }
    func externalField(_ name: String) throws -> GesValue? {
        if case .external(let storage) = storage { return try storage.field(name) }
        return nil
    }
    /// Returns map data, materializing declared external fields when necessary.
    /// External field failures propagate to the caller.
    public func materializedMap() throws -> GesValueMap? { try asMap ?? externalMap() }

    func externalMap() throws -> GesValueMap? {
        if case .external(let storage) = storage { return try storage.map() }
        return nil
    }

    /// Exact storage kind, including integer versus binary64 distinctions.
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
        case .external: .external
        }
    }

    /// Whether storage participates in numeric conversion: Boolean, integer, binary64, Percentage or Dice.
    public var isNumeric: Bool {
        switch storage {
        case .boolean, .integer, .float, .percentage, .dice: true
        default: false
        }
    }

    /// Whether the value represents absence.
    public var isNothing: Bool { kind == .nothing }
    /// Whether a quantity unit other than none is attached.
    public var hasUnit: Bool { unit != .none }

    /// False for Nothing or an empty text/collection/range; true for other values, including false and zero.
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

    /// Applies the portable Boolean conversion. Text accepts 1 or ASCII case-insensitive true; unsupported values yield
    /// false.
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

    /// Numeric projection of Boolean, Number, Percentage or summed Dice; other storage yields NaN. Text parsing belongs
    /// to the language cast operation.
    public var asNumber: Double {
        switch storage {
        case .boolean(let value): value ? 1 : 0
        case .integer(let value): Double(value)
        case .float(let value), .percentage(let value): value
        case .dice(let values): Double(values.reduce(Int64(0)) { $0 + Int64($1) })
        default: .nan
        }
    }

    /// Preserves exact integer storage; otherwise saturates the numeric projection to Int64, with NaN becoming zero.
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
        case .external(let external): count = Int64(external.definition.fields.count)
        default: count = 0
        }
        return Int(min(count, Int64(Int32.max)))
    }

    /// Exact Int64 payload when stored as integer, otherwise nil.
    public var integerValue: Int64? { if case .integer(let value) = storage { value } else { nil } }
    /// Binary64 payload for Number or Percentage storage, otherwise nil.
    public var floatValue: Double? {
        switch storage {
        case .float(let value), .percentage(let value): value
        default: nil
        }
    }
    /// Unquoted text or bare tag name, otherwise nil.
    public var textValue: String? {
        switch storage {
        case .text(let value), .tag(let value): value.value
        default: nil
        }
    }
    /// Coordinates for a Vector or Point, otherwise nil.
    public var spatialValue: GesSpatialValue? {
        switch storage {
        case .vector(let value), .point(let value): value
        default: nil
        }
    }
    /// Spatial x coordinate, or zero for non-spatial values.
    public var x: Double { spatialValue?.x ?? 0 }
    /// Spatial y coordinate, or zero for non-spatial values.
    public var y: Double { spatialValue?.y ?? 0 }
    /// Spatial z coordinate, or zero for non-spatial values.
    public var z: Double { spatialValue?.z ?? 0 }
    /// Stored List elements, or nil for other kinds.
    public var listValue: [GesValue]? { if case .list(let value) = storage { value } else { nil } }
    /// Stored descending dice rolls, or nil for other kinds.
    public var diceRolls: [Int32]? { if case .dice(let value) = storage { value } else { nil } }
    /// Stored Map or Record fields, otherwise nil; does not invoke external field getters.
    public var asMap: GesValueMap? {
        switch storage {
        case .map(let value), .record(_, let value): value
        default: nil
        }
    }
    /// Stored List elements, or an empty array for other kinds.
    public var asList: [GesValue] { listValue ?? [] }
    /// Stored Dice rolls, or an empty array for other kinds.
    public var asDice: [Int32] { diceRolls ?? [] }
    /// Canonical Map or Record entries, otherwise nil.
    public var mapEntries: [GesMapEntry]? { asMap?.entries }
    /// Declared Record or external type name, otherwise nil.
    public var customTypeName: String? {
        switch storage {
        case .record(let name, _): name.value
        case .external(let external): external.definition.name
        default: nil
        }
    }
    /// Exact integer range descriptor, or nil for other kinds.
    public var integerRangeValue: GesIntegerRange? { if case .integerRange(let value) = storage { value } else { nil } }
    /// Binary64 range descriptor, or nil for other kinds.
    public var floatRangeValue: GesFloatRange? { if case .floatRange(let value) = storage { value } else { nil } }
    /// Stored Message, or nil for other kinds.
    public var messageValue: GameEventScriptMessage? { if case .message(let value) = storage { value } else { nil } }
    /// Stored Handler signature, or nil for other kinds.
    public var signatureValue: GameEventScriptMessageSignature? {
        if case .handler(let value) = storage { value } else { nil }
    }
    /// Stored series descriptor, or nil for other kinds.
    public var seriesValue: GesSeriesValue? { if case .series(let value) = storage { value } else { nil } }

    /// Typed API text view. Tags expose their name; use toText for the language representation with '#'.
    public var asText: String { textValue ?? toText }
    /// The same portable language text representation as `toText`.
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
        case .external: return "Custom"
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
