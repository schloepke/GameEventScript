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
    public static func == (left: Self, right: Self) -> Bool { GesText.scalarEqual(left.signatureID, right.signatureID) && left.offset == right.offset }

    /// Hashes the scalar-exact signature and offset consistently with equality.
    public func hash(into hasher: inout Hasher) {
        GesText.hashScalars(signatureID, into: &hasher)
        hasher.combine(offset)
    }
}

// Immutable reference payloads correspond to C# ObjectValue. Scalars never allocate a payload.
final class GesValueObject<Value> {
    let value: Value

    init(_ value: Value) { self.value = value }
}

private struct GesRecordPayload {
    let name: String
    let fields: GesValueMap
}

/// Immutable portable GES value with strict structural equality and copy-on-write collection ownership.
/// Factories canonicalize invalid numeric values to Nothing and never expose mutable shared storage.
public struct GesValue: Hashable, CustomStringConvertible {
    // Same architecture as C#: one numeric word, one object reference, kind, unit and flags.
    // Double.bitPattern gives the Int64/Double union without depending on Swift struct layout.
    private var numericBits: UInt64 = 0
    private var object: AnyObject?
    private var valueKind: GesValueKind = .nothing
    private var valueUnit: GesUnit = .none
    private var flags: UInt8 = 0

    private static let trueFlag: UInt8 = 1
    private static let falseFlag: UInt8 = 2
    private static let hasValueFlag: UInt8 = 4
    private static let numericFlag: UInt8 = 8
    private static let objectFlag: UInt8 = 16
    private static let iteratorFlag: UInt8 = 64
    private static let builderFlag: UInt8 = 128
    // These shared payloads contain only immutable empty data, never host values or VM objects.
    nonisolated(unsafe) private static let emptyRange = GesValueObject(GesIntegerRange(from: 0, to: 0, step: 0))
    // These shared payloads contain only immutable empty data, never host values or VM objects.
    nonisolated(unsafe) private static let emptyList = GesValueObject([GesValue]())
    // These shared payloads contain only immutable empty data, never host values or VM objects.
    nonisolated(unsafe) private static let emptyDice = GesValueObject([Int32]())

    private init() {}

    /// Quantity unit carried by numeric or spatial storage.
    public var unit: GesUnit { valueUnit }

    /// Exact storage kind, including integer versus binary64 distinctions.
    public var kind: GesValueKind { valueKind }

    // Every object accessor is guarded by a private discriminator set together with its payload.
    private func payload<Value>(_ type: Value.Type) -> Value { unsafeDowncast(object!, to: GesValueObject<Value>.self).value }

    @inline(__always)
    mutating func setNothing() {
        valueKind = .nothing
        valueUnit = .none
        flags = 0
        numericBits = 0
        object = nil
    }

    @inline(__always)
    mutating func setBoolean(_ value: Bool) {
        valueKind = .boolean
        valueUnit = .none
        flags = Self.numericFlag | Self.hasValueFlag | (value ? Self.trueFlag : Self.falseFlag)
        numericBits = value ? 1 : 0
        object = nil
    }

    @inline(__always)
    mutating func setInteger(_ value: Int64, unit: GesUnit = .none) {
        valueKind = .integer
        valueUnit = unit
        flags = Self.numericFlag | Self.hasValueFlag | (value != 0 ? Self.trueFlag : Self.falseFlag)
        numericBits = UInt64(bitPattern: value)
        object = nil
    }

    @inline(__always)
    mutating func setFloat(_ value: Double, unit: GesUnit = .none) {
        if value.isNaN {
            setNothing()
            return
        }
        if let integer = GesNumber.exactInteger(value) {
            setInteger(integer, unit: unit)
            return
        }
        valueKind = .float
        valueUnit = unit
        flags = Self.numericFlag | Self.hasValueFlag | (value != 0 ? Self.trueFlag : Self.falseFlag)
        numericBits = value.bitPattern
        object = nil
    }

    @inline(__always)
    mutating func setPercentage(_ value: Double) {
        if !value.isFinite {
            setFloat(value)
            return
        }
        valueKind = .percentage
        valueUnit = .none
        flags = Self.numericFlag | Self.hasValueFlag | (value != 0 ? Self.trueFlag : Self.falseFlag)
        numericBits = GesNumber.canonicalZero(value).bitPattern
        object = nil
    }

    private mutating func setObject(_ kind: GesValueKind, _ value: AnyObject, count: Int64 = 0, unit: GesUnit = .none, hasValue: Bool = true, truth: Bool? = nil, numeric: Bool = false) {
        valueKind = kind
        valueUnit = unit
        flags = Self.objectFlag | (hasValue ? Self.hasValueFlag : 0) | (truth == true ? Self.trueFlag : truth == false ? Self.falseFlag : 0) | (numeric ? Self.numericFlag : 0)
        numericBits = UInt64(bitPattern: count)
        object = value
    }

    mutating func setText(_ value: String) {
        let truth = value.utf8.elementsEqual("1".utf8) || value.utf8.lazy.map { $0 >= 65 && $0 <= 90 ? $0 + 32 : $0 }.elementsEqual("true".utf8)
        setObject(.text, GesValueObject(value), count: Int64(value.unicodeScalars.count), hasValue: !value.isEmpty, truth: truth)
    }

    // Linked string constants already own their immutable payload; loading or staging must not box again.
    mutating func setTextConstant(_ value: GesValue, tag: Bool) {
        valueKind = tag ? .tag : .text
        valueUnit = .none
        flags = tag ? Self.objectFlag | Self.hasValueFlag | Self.falseFlag : value.flags
        numericBits = value.numericBits
        object = value.object
    }

    mutating func setTag(_ value: String) throws {
        guard GesText.isLowerName(value) else { throw GesValueError.invalidTag(value) }
        setObject(.tag, GesValueObject(value), count: Int64(value.unicodeScalars.count), truth: false)
    }

    mutating func setVector(x: Double, y: Double = 0, z: Double = 0, unit: GesUnit = .none) {
        guard !x.isNaN, !y.isNaN, !z.isNaN else {
            setNothing()
            return
        }
        setObject(.vector, GesValueObject(GesSpatialValue(x: x, y: y, z: z)), unit: unit, truth: x != 0 || y != 0 || z != 0)
    }

    mutating func setPoint(x: Double, y: Double = 0, z: Double = 0, unit: GesUnit = .none) {
        guard !x.isNaN, !y.isNaN, !z.isNaN else {
            setNothing()
            return
        }
        setObject(.point, GesValueObject(GesSpatialValue(x: x, y: y, z: z)), unit: unit, truth: x != 0 || y != 0 || z != 0)
    }

    mutating func setDice(_ value: [Int32]) {
        setObject(.dice, value.isEmpty ? Self.emptyDice : GesValueObject(value.sorted(by: >)), count: Int64(value.count), hasValue: !value.isEmpty, numeric: true)
    }

    mutating func setList(_ value: [GesValue]) {
        setObject(.list, value.isEmpty ? Self.emptyList : GesValueObject(value), count: Int64(value.count), hasValue: !value.isEmpty)
    }

    mutating func setMap(_ entries: [GesMapEntry]) {
        let map = GesValueMap(entries)
        setObject(.map, GesValueObject(map), count: Int64(map.length), hasValue: map.length != 0)
    }

    mutating func setRecord(typeName: String, entries: [GesMapEntry]) {
        setRecord(typeName: typeName, fields: GesValueMap(entries))
    }

    mutating func setRecord(typeName: String, fields: GesValueMap) {
        setObject(.record, GesValueObject(GesRecordPayload(name: typeName, fields: fields)), count: Int64(fields.length), hasValue: fields.length != 0)
    }

    mutating func setIntegerRange(from: Int64, to: Int64, step: Int64 = 1) {
        let range = GesIntegerRange(from: from, to: to, step: step)
        let count = range.count
        setObject(.integerRange, count == 0 ? Self.emptyRange : GesValueObject(range), count: count, hasValue: count != 0)
    }

    mutating func setFloatRange(from: Double, to: Double, step: Double = 1) {
        guard from.isFinite, to.isFinite, step.isFinite else {
            setNothing()
            return
        }
        if let from = GesNumber.exactInteger(from), let to = GesNumber.exactInteger(to), let step = GesNumber.exactInteger(step) {
            setIntegerRange(from: from, to: to, step: step)
            return
        }
        let range = GesFloatRange(from: from, to: to, step: step)
        let count = range.count
        if count == 0 {
            setIntegerRange(from: 0, to: 0, step: 0)
            return
        }
        setObject(.floatRange, GesValueObject(range), count: count)
    }

    mutating func setMessage(_ value: GameEventScriptMessage) { setObject(.message, value.storage) }

    mutating func setHandler(_ value: GameEventScriptMessageSignature) { setObject(.handler, value.storage) }

    mutating func setSeries(_ value: GesSeriesValue) { setObject(.series, GesValueObject(value)) }

    mutating func setExternal(_ value: any GameEventScriptExternalValue) {
        let storage = GesExternalStorage(value: value)
        setObject(.external, storage, count: Int64(storage.definition.fields.count))
    }

    mutating func setIterator(_ value: GesIterator) {
        setObject(.nothing, value, hasValue: false)
        flags |= Self.iteratorFlag
    }

    mutating func setBuilder(_ value: GesCollectionBuilder) {
        setObject(.nothing, value, hasValue: false)
        flags |= Self.builderFlag
    }

    var truth: Bool? { flags & Self.trueFlag != 0 ? true : flags & Self.falseFlag != 0 ? false : nil }

    var iteratorValue: GesIterator? { flags & Self.iteratorFlag != 0 ? unsafeDowncast(object!, to: GesIterator.self) : nil }
    var builderValue: GesCollectionBuilder? { flags & Self.builderFlag != 0 ? unsafeDowncast(object!, to: GesCollectionBuilder.self) : nil }
    var isRegisterData: Bool { flags & (Self.iteratorFlag | Self.builderFlag) == 0 }
    var registerValue: GesValue { isRegisterData ? self : .nothing }

    @inline(__always)
    var integerOperand: (value: Int64, unit: GesUnit)? { kind == .integer ? (Int64(bitPattern: numericBits), valueUnit) : nil }

    /// The canonical absence value.
    public static var nothing: Self { Self() }

    /// Creates a Boolean value.
    public static func boolean(_ value: Bool) -> Self {
        var result = Self()
        result.setBoolean(value)
        return result
    }

    /// Creates an exact signed Int64 value with an optional quantity unit.
    public static func integer(_ value: Int64, unit: GesUnit = .none) -> Self {
        var result = Self()
        result.setInteger(value, unit: unit)
        return result
    }

    /// Creates a number, normalizing NaN, zero, and finite integral Int64 values.
    public static func float(_ value: Double, unit: GesUnit = .none) -> Self {
        var result = Self()
        result.setFloat(value, unit: unit)
        return result
    }

    /// Stores finite ratios as Percentage. Infinite ratios become unitless binary64 infinity.
    public static func percentage(_ ratio: Double) -> Self {
        var result = Self()
        result.setPercentage(ratio)
        return result
    }

    /// Preserves text verbatim; equality uses Unicode scalars rather than canonical-equivalence normalization.
    public static func text(_ value: String) -> Self {
        var result = Self()
        result.setText(value)
        return result
    }

    /// Creates a tag from its name without a leading #.
    ///
    /// - Throws: `GesValueError.invalidTag` when the name violates the lowercase portable grammar.
    public static func tag(_ name: String) throws -> Self {
        var result = Self()
        try result.setTag(name)
        return result
    }

    /// Creates a binary64 vector with optional unit. Any NaN coordinate yields Nothing; signed zeros are normalized.
    public static func vector(x: Double, y: Double = 0, z: Double = 0, unit: GesUnit = .none) -> Self {
        var result = Self()
        result.setVector(x: x, y: y, z: z, unit: unit)
        return result
    }

    /// Creates a binary64 point with optional unit. Any NaN coordinate yields Nothing; signed zeros are normalized.
    public static func point(x: Double, y: Double = 0, z: Double = 0, unit: GesUnit = .none) -> Self {
        var result = Self()
        result.setPoint(x: x, y: y, z: z, unit: unit)
        return result
    }

    /// Copies and sorts supplied Int32 rolls in descending order without drawing random numbers.
    public static func dice(_ rolls: [Int32]) -> Self {
        var result = Self()
        result.setDice(rolls)
        return result
    }

    /// Creates an immutable ordered list of values.
    public static func list(_ values: [GesValue]) -> Self {
        var result = Self()
        result.setList(values)
        return result
    }

    /// Creates a map in scalar key order, keeping the last entry for duplicate keys.
    public static func map(_ entries: [GesMapEntry]) -> Self {
        var result = Self()
        result.setMap(entries)
        return result
    }

    /// Creates record storage with a type name and canonical field map; does not invoke a script constructor.
    public static func record(typeName: String, entries: [GesMapEntry]) -> Self {
        var result = Self()
        result.setRecord(typeName: typeName, entries: entries)
        return result
    }

    static func record(typeName: String, fields: GesValueMap) -> Self {
        var result = Self()
        result.setRecord(typeName: typeName, fields: fields)
        return result
    }

    /// Creates an exact lazy Int64 range; a zero step or incompatible direction yields the canonical empty range.
    public static func integerRange(from: Int64, to: Int64, step: Int64 = 1) -> Self {
        var result = Self()
        result.setIntegerRange(from: from, to: to, step: step)
        return result
    }

    /// Creates a lazy range, using exact Int64 arithmetic when all three values are integral and representable.
    /// Nonfinite bounds or step yield Nothing; empty results use the canonical empty
    /// integer range.
    public static func floatRange(from: Double, to: Double, step: Double = 1) -> Self {
        var result = Self()
        result.setFloatRange(from: from, to: to, step: step)
        return result
    }

    /// Wraps a message as a value.
    public static func message(_ value: GameEventScriptMessage) -> Self {
        var result = Self()
        result.setMessage(value)
        return result
    }

    /// Wraps a message signature as a Handler value.
    public static func handler(_ value: GameEventScriptMessageSignature) -> Self {
        var result = Self()
        result.setHandler(value)
        return result
    }

    /// Wraps a built-in series descriptor without evaluating terms.
    public static func series(_ value: GesSeriesValue) -> Self {
        var result = Self()
        result.setSeries(value)
        return result
    }

    /// Wraps a host-provided external value and captures its declared type definition.
    public static func external(_ value: any GameEventScriptExternalValue) -> Self {
        var result = Self()
        result.setExternal(value)
        return result
    }

    /// Underlying host value for external storage, otherwise nil.
    public var externalValue: (any GameEventScriptExternalValue)? { externalStorage?.value }

    private var externalStorage: GesExternalStorage? { kind == .external ? unsafeDowncast(object!, to: GesExternalStorage.self) : nil }

    func externalField(_ name: String) throws -> GesValue? { try externalStorage?.field(name) }

    /// Returns map data, materializing declared external fields when necessary.
    /// External field failures propagate to the caller.
    public func materializedMap() throws -> GesValueMap? { try asMap ?? externalMap() }

    func externalMap() throws -> GesValueMap? { try externalStorage?.map() }

    /// Whether storage participates in numeric conversion: Boolean, integer, binary64, Percentage or Dice.
    public var isNumeric: Bool { flags & Self.numericFlag != 0 }
    /// Whether the value represents absence.
    public var isNothing: Bool { kind == .nothing }
    /// Whether a quantity unit other than none is attached.
    public var hasUnit: Bool { unit != .none }
    /// False for Nothing or an empty text/collection/range; true for other values, including false and zero.
    public var hasValue: Bool { flags & Self.hasValueFlag != 0 }
    /// Applies the portable Boolean conversion. Text accepts 1 or ASCII case-insensitive true; unsupported values yield false.
    public var asBoolean: Bool { flags & Self.trueFlag != 0 }

    /// Numeric projection of Boolean, Number, Percentage or summed Dice; other storage yields NaN. Text parsing belongs to the language cast operation.
    public var asNumber: Double {
        switch kind {
        case .boolean, .integer: Double(Int64(bitPattern: numericBits))
        case .float, .percentage: Double(bitPattern: numericBits)
        case .dice: Double(diceRolls!.reduce(Int64(0)) { $0 + Int64($1) })
        default: .nan
        }
    }

    /// Preserves exact integer storage; otherwise saturates the numeric projection to Int64, with NaN becoming zero.
    public var asInteger: Int64 { integerValue ?? GesNumber.saturatedInteger(asNumber) }
    /// Number of Unicode scalars or collection entries; range lengths saturate at Int32.max like the API. Numeric payload bits never define a length.
    public var length: Int { flags & Self.objectFlag != 0 ? Int(min(Int64(bitPattern: numericBits), Int64(Int32.max))) : 0 }
    /// Exact Int64 payload when stored as integer, otherwise nil.
    public var integerValue: Int64? { kind == .integer ? Int64(bitPattern: numericBits) : nil }
    /// Binary64 payload for Number or Percentage storage, otherwise nil.
    public var floatValue: Double? { kind == .float || kind == .percentage ? Double(bitPattern: numericBits) : nil }
    /// Unquoted text or bare tag name, otherwise nil.
    public var textValue: String? { kind == .text || kind == .tag ? payload(String.self) : nil }
    /// Coordinates for a Vector or Point, otherwise nil.
    public var spatialValue: GesSpatialValue? { kind == .vector || kind == .point ? payload(GesSpatialValue.self) : nil }
    /// Spatial x coordinate, or zero for non-spatial values.
    public var x: Double { spatialValue?.x ?? 0 }
    /// Spatial y coordinate, or zero for non-spatial values.
    public var y: Double { spatialValue?.y ?? 0 }
    /// Spatial z coordinate, or zero for non-spatial values.
    public var z: Double { spatialValue?.z ?? 0 }
    /// Stored List elements, or nil for other kinds.
    public var listValue: [GesValue]? { kind == .list ? payload([GesValue].self) : nil }
    /// Stored descending dice rolls, or nil for other kinds.
    public var diceRolls: [Int32]? { kind == .dice ? payload([Int32].self) : nil }
    /// Stored Map or Record fields, otherwise nil; does not invoke external field getters.
    public var asMap: GesValueMap? {
        switch kind {
        case .map: payload(GesValueMap.self)
        case .record: payload(GesRecordPayload.self).fields
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
        switch kind {
        case .record: payload(GesRecordPayload.self).name
        case .external: externalStorage!.definition.name
        default: nil
        }
    }
    /// Exact integer range descriptor, or nil for other kinds.
    public var integerRangeValue: GesIntegerRange? { kind == .integerRange ? payload(GesIntegerRange.self) : nil }
    /// Binary64 range descriptor, or nil for other kinds.
    public var floatRangeValue: GesFloatRange? { kind == .floatRange ? payload(GesFloatRange.self) : nil }
    /// Stored Message, or nil for other kinds.
    public var messageValue: GameEventScriptMessage? { kind == .message ? .init(storage: unsafeDowncast(object!, to: GesMessageStorage.self)) : nil }
    /// Stored Handler signature, or nil for other kinds.
    public var signatureValue: GameEventScriptMessageSignature? { kind == .handler ? .init(storage: unsafeDowncast(object!, to: GesSignatureStorage.self)) : nil }
    /// Stored series descriptor, or nil for other kinds.
    public var seriesValue: GesSeriesValue? { kind == .series ? payload(GesSeriesValue.self) : nil }

    /// Compares exact kinds, units and immutable payloads, with scalar-exact text and record names.
    public static func == (a: Self, b: Self) -> Bool {
        guard a.kind == b.kind, a.unit == b.unit else { return false }
        switch a.kind {
        case .nothing: return true
        case .boolean, .integer, .float, .percentage: return a.numericBits == b.numericBits
        case .text, .tag: return GesText.scalarEqual(a.textValue!, b.textValue!)
        case .vector, .point: return a.spatialValue == b.spatialValue
        case .dice: return a.diceRolls == b.diceRolls
        case .list: return a.listValue == b.listValue
        case .map: return a.asMap == b.asMap
        case .record: return GesText.scalarEqual(a.customTypeName!, b.customTypeName!) && a.asMap == b.asMap
        case .integerRange: return a.integerRangeValue == b.integerRangeValue
        case .floatRange: return a.floatRangeValue == b.floatRangeValue
        case .message: return a.messageValue == b.messageValue
        case .handler: return a.signatureValue == b.signatureValue
        case .series: return a.seriesValue == b.seriesValue
        case .external: return a.externalStorage == b.externalStorage
        }
    }

    /// Hashes exact kinds, units and scalar-exact payloads consistently with structural equality.
    public func hash(into hasher: inout Hasher) {
        hasher.combine(kind)
        hasher.combine(unit)
        switch kind {
        case .nothing: break
        case .boolean, .integer, .float, .percentage: hasher.combine(numericBits)
        case .text, .tag: GesText.hashScalars(textValue!, into: &hasher)
        case .vector, .point: hasher.combine(spatialValue!)
        case .dice: hasher.combine(diceRolls!)
        case .list: hasher.combine(listValue!)
        case .map: hasher.combine(asMap!)
        case .record:
            GesText.hashScalars(customTypeName!, into: &hasher)
            hasher.combine(asMap!)
        case .integerRange: hasher.combine(integerRangeValue!)
        case .floatRange: hasher.combine(floatRangeValue!)
        case .message: hasher.combine(messageValue!)
        case .handler: hasher.combine(signatureValue!)
        case .series: hasher.combine(seriesValue!)
        case .external: hasher.combine(externalStorage!)
        }
    }

    /// Typed API text view. Tags expose their name; use toText for the language representation with '#'.
    public var asText: String { textValue ?? toText }
    /// The same portable language text representation as `toText`.
    public var description: String { toText }

    /// Language text representation, quoting nested Text values and preserving canonical map ordering.
    public var toText: String {
        switch kind {
        case .nothing: return "nothing"
        case .boolean:
            let value = asBoolean
            return value ? "true" : "false"
        case .integer:
            let value = asInteger
            return String(value) + unit.suffix
        case .float:
            let value = asNumber
            return GesNumber.format(value) + unit.suffix
        case .percentage:
            let value = asNumber
            return GesNumber.formatPercentage(value)
        case .text:
            let value = textValue!
            return value
        case .tag:
            let value = textValue!
            return "#" + value
        case .vector:
            let value = spatialValue!
            return formatSpatial(":Vector", value)
        case .point:
            let value = spatialValue!
            return formatSpatial(":Point", value)
        case .dice:
            let values = diceRolls!
            return ":Dice[" + values.map(String.init).joined(separator: ", ") + "]"
        case .list:
            let values = listValue!
            return "[" + values.map(\.nestedText).joined(separator: ", ") + "]"
        case .map:
            let map = asMap!
            return (map.length == 0 ? "[:" : "[") + map.entries.map { (GesText.isLowerName($0.key) ? $0.key : GesText.quoted($0.key)) + ": " + $0.value.nestedText }.joined(separator: ", ") + "]"
        case .record:
            let name = customTypeName!
            let map = asMap!
            return ":Record(" + GesText.quoted(name) + ", " + GesValue.map(map.entries).toText + ")"
        case .integerRange:
            let range = integerRangeValue!
            return ":Range(from: \(range.from), to: \(range.to), step: \(range.step))"
        case .floatRange:
            let range = floatRangeValue!
            return ":Range(from: \(GesNumber.format(range.from)), to: \(GesNumber.format(range.to)), step: \(GesNumber.format(range.step)))"
        case .message:
            let value = messageValue!

            let arguments = (0..<value.arguments.count).map { index in
                let label = value.arguments.nameAt(index)
                return (label == "_" ? "" : label + ": ") + value.arguments[index].nestedText
            }.joined(separator: ", ")
            let tags = value.tags.isEmpty ? "" : " with " + value.tags.map { "#" + $0 }.joined(separator: ", ")
            return ":Message(" + value.name + "(" + arguments + ")" + tags + ")"
        case .handler:
            let value = signatureValue!
            return ":Handler(" + value.signatureId + ")"
        case .series:
            let value = seriesValue!
            return ":Series(\(value.signatureID), offset: \(value.offset))"
        case .external: return "Custom"
        }
    }

    private var nestedText: String {
        if kind == .text { return GesText.quoted(textValue!) }
        return toText
    }

    private func formatSpatial(_ name: String, _ value: GesSpatialValue) -> String { "\(name)(x: \(GesNumber.format(value.x))\(unit.suffix), y: \(GesNumber.format(value.y))\(unit.suffix), z: \(GesNumber.format(value.z))\(unit.suffix))" }
}
