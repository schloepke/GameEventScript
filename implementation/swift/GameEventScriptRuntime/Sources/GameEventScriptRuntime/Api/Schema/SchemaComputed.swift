// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

@_spi(Syntax) import GameEventScriptSyntax

#if canImport(Darwin)
    import Darwin
#elseif canImport(Glibc)
    import Glibc
#elseif canImport(Musl)
    import Musl
#elseif canImport(Android)
    import Android
#elseif canImport(WASILibc)
    import WASILibc
#endif

/// The operations of a checked computed expression.
@_spi(Generator) public enum GesComputedOp: Sendable, CaseIterable {
    /// A number, percentage or Boolean literal.
    case literal
    /// A field of the record.
    case field
    /// A field of a list element, or the element itself.
    case item
    /// Unary minus.
    case negate
    /// `not`.
    case not
    /// `abs`.
    case abs
    /// `+`.
    case add
    /// `-`.
    case subtract
    /// `*`.
    case multiply
    /// `/`.
    case divide
    /// `<`.
    case less
    /// `<=`.
    case lessOrEqual
    /// `>`.
    case greater
    /// `>=`.
    case greaterOrEqual
    /// `=`.
    case equal
    /// `<>`.
    case notEqual
    /// `and`.
    case and
    /// `or`.
    case or
    /// `value when condition, …, otherwise fallback`.
    case choice
    /// `min of`.
    case min
    /// `max of`.
    case max
    /// `clamp … between … and …`.
    case clamp
    /// `default`.
    case `default`
    /// `has value`.
    case hasValue
    /// `[:sum]`.
    case sum
    /// `[:count]`.
    case count
    /// `^`, `²` and `³`.
    case power
    /// `sqrt` and `√`.
    case sqrt
    /// `cbrt` and `∛`.
    case cbrt
    /// `sin`.
    case sin
    /// `cos`.
    case cos
    /// `tan`.
    case tan
    /// `asin`.
    case asin
    /// `acos`.
    case acos
    /// `atan`.
    case atan
    /// `atan2`.
    case atan2
    /// `hypot`.
    case hypot
    /// `exp`.
    case exp
    /// `ln`.
    case ln
    /// `floor`.
    case floor
    /// `ceil`.
    case ceil
    /// `truncate`.
    case truncate
    /// `round half even`.
    case roundHalfEven
    /// `round half up`.
    case roundHalfUp
    /// `round half down`.
    case roundHalfDown
    /// `rad`.
    case rad
    /// `deg`.
    case deg
    /// `:Vector(…)`.
    case vector
    /// `:Point(…)`.
    case point
    /// `:Box(lower:, upper:)`.
    case box
    /// `:Box(center:, size:)`.
    case boxAround
    /// `:Disc(center:, radius:)`.
    case disc
    /// `:Disc(radius:)`.
    case discAround
    /// `:Poly(points: [a, b, …])`.
    case poly
    /// `:Poly(points: list)` or `:Poly(points: list[:select item => point])`.
    case polyOf
    /// A part of a spatial value, such as `.x`, `.center` or `.radius`.
    case part
    /// `.points[:count]` of a Poly.
    case pointCount

    /// The name in the canonical form and the layouts.
    @_spi(Generator) public var name: String {
        switch self {
        case .literal: "literal"
        case .field: "field"
        case .item: "item"
        case .negate: "negate"
        case .not: "not"
        case .abs: "abs"
        case .add: "add"
        case .subtract: "subtract"
        case .multiply: "multiply"
        case .divide: "divide"
        case .less: "less"
        case .lessOrEqual: "lessOrEqual"
        case .greater: "greater"
        case .greaterOrEqual: "greaterOrEqual"
        case .equal: "equal"
        case .notEqual: "notEqual"
        case .and: "and"
        case .or: "or"
        case .choice: "choice"
        case .min: "min"
        case .max: "max"
        case .clamp: "clamp"
        case .default: "default"
        case .hasValue: "hasValue"
        case .sum: "sum"
        case .count: "count"
        case .power: "power"
        case .sqrt: "sqrt"
        case .cbrt: "cbrt"
        case .sin: "sin"
        case .cos: "cos"
        case .tan: "tan"
        case .asin: "asin"
        case .acos: "acos"
        case .atan: "atan"
        case .atan2: "atan2"
        case .hypot: "hypot"
        case .exp: "exp"
        case .ln: "ln"
        case .floor: "floor"
        case .ceil: "ceil"
        case .truncate: "truncate"
        case .roundHalfEven: "roundHalfEven"
        case .roundHalfUp: "roundHalfUp"
        case .roundHalfDown: "roundHalfDown"
        case .rad: "rad"
        case .deg: "deg"
        case .vector: "vector"
        case .point: "point"
        case .box: "box"
        case .boxAround: "boxAround"
        case .disc: "disc"
        case .discAround: "discAround"
        case .poly: "poly"
        case .polyOf: "polyOf"
        case .part: "part"
        case .pointCount: "pointCount"
        }
    }

    /// Whether the operation is a function or a power, whose helpers generated code holds only where a field uses one.
    @_spi(Generator) public var isFunction: Bool {
        switch self {
        case .power, .sqrt, .cbrt, .sin, .cos, .tan, .asin, .acos, .atan, .atan2, .hypot, .exp, .ln, .floor, .ceil, .truncate, .roundHalfEven,
            .roundHalfUp, .roundHalfDown, .rad, .deg:
            true
        default: false
        }
    }
}

/// The static type of a node: a number with a unit, a percentage, a Boolean, a spatial value with a unit, or a record or list
/// that only a path, `has value` or an aggregate reads.
@_spi(Generator) public enum GesComputedType: String, Sendable {
    /// A number with its unit.
    case number = "Number"
    /// A percentage ratio.
    case percentage = "Percentage"
    /// A Boolean.
    case boolean = "Boolean"
    /// A record, or a field that is read only by `has value`.
    case record = "Record"
    /// A list.
    case list = "List"
    /// A Vector with its unit.
    case vector = "Vector"
    /// A Point with its unit.
    case point = "Point"
    /// A Box with its unit.
    case box = "Box"
    /// A Disc with its unit.
    case disc = "Disc"
    /// A Poly with its unit.
    case poly = "Poly"

    /// Whether the type is a spatial value.
    @_spi(Generator) public var isSpatial: Bool { self == .vector || self == .point || self == .box || self == .disc || self == .poly }
}

/// The kinds of a computed scalar.
@_spi(Generator) public enum GesComputedKind: Sendable {
    /// Nothing.
    case nothing
    /// An exact Int64.
    case integer
    /// A binary64.
    case float
    /// A percentage ratio.
    case percentage
    /// False.
    case `false`
    /// True.
    case `true`
    /// A Vector.
    case vector
    /// A Point.
    case point
    /// A Box.
    case box
    /// A Disc.
    case disc
    /// A Poly.
    case poly
}

/// A GES value as computed fields see it: an exact Int64, a binary64, a percentage ratio, a Boolean, a spatial value, or
/// nothing. Units are static, so the value carries none. A Vector or Point never holds NaN, which makes it nothing, and holds
/// zero as +0, as the virtual machine stores them; a Box, Disc or Poly holds the geometry library's value.
@_spi(Generator) public struct GesComputedValue: Sendable {
    /// The kind.
    @_spi(Generator) public let kind: GesComputedKind
    /// The value of an integer.
    @_spi(Generator) public let integer: Int64
    /// The binary64 of a float, or the ratio of a percentage.
    @_spi(Generator) public let number: Double
    /// The spatial value.
    let shape: Shape?

    /// The spatial value of a computed value.
    enum Shape: Sendable {
        case vector(GesVector)
        case point(GesPoint)
        case box(GesBox)
        case disc(GesDisc)
        case poly(GesPoly)
    }

    init(kind: GesComputedKind, integer: Int64, number: Double, shape: Shape? = nil) {
        self.kind = kind
        self.integer = integer
        self.number = number
        self.shape = shape
    }

    static let nothing = GesComputedValue(kind: .nothing, integer: 0, number: 0)

    var vector: GesVector {
        guard case .vector(let vector) = shape else { preconditionFailure("The value is no Vector.") }
        return vector
    }

    var point: GesPoint {
        guard case .point(let point) = shape else { preconditionFailure("The value is no Point.") }
        return point
    }

    /// The components of a Vector or Point.
    var components: GesVector {
        if case .point(let point) = shape { return GesVector(x: point.x, y: point.y, z: point.z) }
        return vector
    }

    static func of(vector x: Double, _ y: Double, _ z: Double) -> GesComputedValue {
        if x.isNaN || y.isNaN || z.isNaN { return nothing }
        let vector = GesVector(x: GesNumber.canonicalZero(x), y: GesNumber.canonicalZero(y), z: GesNumber.canonicalZero(z))
        return GesComputedValue(kind: .vector, integer: 0, number: 0, shape: .vector(vector))
    }

    static func of(point x: Double, _ y: Double, _ z: Double) -> GesComputedValue {
        if x.isNaN || y.isNaN || z.isNaN { return nothing }
        let point = GesPoint(x: GesNumber.canonicalZero(x), y: GesNumber.canonicalZero(y), z: GesNumber.canonicalZero(z))
        return GesComputedValue(kind: .point, integer: 0, number: 0, shape: .point(point))
    }

    static func of(box: GesBox) -> GesComputedValue { GesComputedValue(kind: .box, integer: 0, number: 0, shape: .box(box)) }

    static func of(disc: GesDisc) -> GesComputedValue { GesComputedValue(kind: .disc, integer: 0, number: 0, shape: .disc(disc)) }

    static func of(poly: GesPoly) -> GesComputedValue { GesComputedValue(kind: .poly, integer: 0, number: 0, shape: .poly(poly)) }

    var isNothing: Bool { kind == .nothing }
    var isNumeric: Bool { kind == .integer || kind == .float || kind == .percentage }
    /// The numeric view: an Int64 converted to binary64, or the binary64 or ratio.
    var numeric: Double { kind == .integer ? Double(integer) : number }

    static func of(integer value: Int64) -> GesComputedValue { GesComputedValue(kind: .integer, integer: value, number: 0) }

    /// A binary64 result: NaN is nothing, and an integral value within Int64 is an integer.
    static func of(float value: Double) -> GesComputedValue {
        if value.isNaN { return nothing }
        if let whole = GesNumber.exactInteger(value) { return of(integer: whole) }
        return GesComputedValue(kind: .float, integer: 0, number: value)
    }

    /// A percentage result: NaN is nothing, zero is positive, and an infinite ratio is a plain number.
    static func of(percentage ratio: Double) -> GesComputedValue {
        if ratio.isNaN { return nothing }
        let canonical = GesNumber.canonicalZero(ratio)
        return GesComputedValue(kind: canonical.isFinite ? .percentage : .float, integer: 0, number: canonical)
    }

    static func of(boolean value: Bool) -> GesComputedValue { GesComputedValue(kind: value ? .true : .false, integer: 0, number: 0) }

    /// A rounded number: an Int64, saturated at its bounds as the virtual machine rounds.
    static func of(rounded value: Double) -> GesComputedValue { of(integer: GesNumber.saturatedInteger(value)) }
}

/// A checked expression. Units are resolved at binding: each node knows its static type and unit, and an operation knows the
/// unit checks that GES makes at run time, as flags, since a percentage that becomes infinite turns into a plain number
/// there. Add and subtract: `first` = equal units, `second` = the left unit takes a percentage. Multiply: `first` = a product
/// unit exists, `second` and `third` = the left and right unit take a percentage. Divide: `first` = a quotient unit exists,
/// `second` = the left unit takes a percentage, `third` = the right side has no unit. Comparisons, min, max and clamp:
/// `first` = equal units. Sin, cos and tan: `first` = the angle is in degrees. The other functions check their units at
/// binding alone.
@_spi(Generator) public final class GesComputedNode: @unchecked Sendable {
    /// The operation.
    @_spi(Generator) public let op: GesComputedOp
    /// The static type.
    @_spi(Generator) public let type: GesComputedType
    /// The unit of a number.
    @_spi(Generator) public let unit: GesQuantityUnit
    /// The operands.
    @_spi(Generator) public let children: [GesComputedNode]
    /// The value of a literal.
    @_spi(Generator) public let literal: GesComputedValue
    /// The exact decimal of a literal, which the canonical form writes.
    @_spi(Generator) public let decimal: GesSchemaDecimal
    /// The field names from the record, or from the element for an item; for sum and count, the list; for a part, the part's
    /// name.
    @_spi(Generator) public let path: [String]
    /// The first unit check.
    @_spi(Generator) public let first: Bool
    /// The second unit check.
    @_spi(Generator) public let second: Bool
    /// The third unit check.
    @_spi(Generator) public let third: Bool
    /// For a path: whether each field on it is optional. The checker sets it and the next two while it builds the
    /// expression, before the node is shared, so the node is immutable once a schema set holds it.
    @_spi(Generator) public internal(set) var optionalSteps: [Bool] = []
    /// How many leading fields of the path `has value` around the read proves present.
    @_spi(Generator) public internal(set) var present = 0
    /// Whether the node gives nothing where an optional field it reads is absent; NaN, which is nothing too, is not counted.
    @_spi(Generator) public internal(set) var mayBeAbsent = false

    init(
        _ op: GesComputedOp,
        _ type: GesComputedType,
        unit: GesQuantityUnit = .none,
        children: [GesComputedNode] = [],
        literal: GesComputedValue = .nothing,
        decimal: GesSchemaDecimal = GesSchemaDecimal(0, 0),
        path: [String] = [],
        first: Bool = false,
        second: Bool = false,
        third: Bool = false
    ) {
        self.op = op
        self.type = type
        self.unit = unit
        self.children = children
        self.literal = literal
        self.decimal = decimal
        self.path = path
        self.first = first
        self.second = second
        self.third = third
    }
}

/// The operations of the GES virtual machine on the scalars of computed fields, with the unit checks resolved at binding.
/// Generated code repeats them in C# and Swift; all must give the same bits.
enum GesComputedMath {
    static func add(_ a: GesComputedValue, _ b: GesComputedValue, _ sameUnit: Bool, _ percentage: Bool) -> GesComputedValue { sum(a, b, sameUnit, percentage, false) }

    static func subtract(_ a: GesComputedValue, _ b: GesComputedValue, _ sameUnit: Bool, _ percentage: Bool) -> GesComputedValue { sum(a, b, sameUnit, percentage, true) }

    private static func sum(_ a: GesComputedValue, _ b: GesComputedValue, _ sameUnit: Bool, _ percentage: Bool, _ subtract: Bool) -> GesComputedValue {
        if a.isNothing || b.isNothing { return .nothing }
        let left = a.kind == .integer || a.kind == .float
        let right = b.kind == .integer || b.kind == .float
        if a.kind == .integer && b.kind == .integer {
            if !sameUnit { return .nothing }
            let (exact, overflow) = subtract ? a.integer.subtractingReportingOverflow(b.integer) : a.integer.addingReportingOverflow(b.integer)
            return overflow ? .of(float: subtract ? Double(a.integer) - Double(b.integer) : Double(a.integer) + Double(b.integer)) : .of(integer: exact)
        }
        if left && right { return sameUnit ? .of(float: subtract ? a.numeric - b.numeric : a.numeric + b.numeric) : .nothing }
        if left && b.kind == .percentage {
            if !percentage { return .nothing }
            let x = a.numeric
            return .of(float: subtract ? x - x * b.number : x + x * b.number)
        }
        if a.kind == .percentage && b.kind == .percentage { return .of(percentage: subtract ? a.number - b.number : a.number + b.number) }
        return .nothing
    }

    static func multiply(_ a: GesComputedValue, _ b: GesComputedValue, _ product: Bool, _ leftPercentage: Bool, _ rightPercentage: Bool) -> GesComputedValue {
        if a.isNothing || b.isNothing { return .nothing }
        let left = a.kind == .integer || a.kind == .float
        let right = b.kind == .integer || b.kind == .float
        if a.kind == .integer && b.kind == .integer {
            if !product { return .nothing }
            let (exact, overflow) = a.integer.multipliedReportingOverflow(by: b.integer)
            return overflow ? .of(float: Double(a.integer) * Double(b.integer)) : .of(integer: exact)
        }
        if left && right { return product ? .of(float: a.numeric * b.numeric) : .nothing }
        if left && b.kind == .percentage { return leftPercentage ? .of(float: a.numeric * b.number) : .nothing }
        if a.kind == .percentage && b.kind == .percentage { return .of(percentage: a.number * b.number) }
        if a.kind == .percentage && right { return rightPercentage ? .of(float: a.number * b.numeric) : .nothing }
        return .nothing
    }

    static func divide(_ a: GesComputedValue, _ b: GesComputedValue, _ quotient: Bool, _ leftPercentage: Bool, _ rightUnitless: Bool) -> GesComputedValue {
        if a.isNothing || b.isNothing { return .nothing }
        let left = a.kind == .integer || a.kind == .float
        let right = b.kind == .integer || b.kind == .float
        if left && right { return quotient ? .of(float: a.numeric / b.numeric) : .nothing }
        if a.kind == .percentage && b.kind == .percentage { return .of(float: a.number / b.number) }
        if a.kind == .percentage && right { return rightUnitless ? .of(percentage: a.number / b.numeric) : .nothing }
        if left && b.kind == .percentage { return leftPercentage ? .of(float: a.numeric / b.number) : .nothing }
        return .nothing
    }

    static func negate(_ a: GesComputedValue) -> GesComputedValue {
        switch a.kind {
        case .integer: a.integer == Int64.min ? .of(float: -Double(a.integer)) : .of(integer: -a.integer)
        case .float: .of(float: -a.number)
        case .percentage: .of(percentage: -a.number)
        default: .nothing
        }
    }

    static func abs(_ a: GesComputedValue) -> GesComputedValue {
        switch a.kind {
        case .integer: a.integer == Int64.min ? .of(float: -Double(a.integer)) : .of(integer: Swift.abs(a.integer))
        case .float: .of(float: Swift.abs(a.number))
        case .percentage: .of(percentage: Swift.abs(a.number))
        default: .nothing
        }
    }

    /// Ordering: 0 less, 1 less or equal, 2 greater, 3 greater or equal; a unit mismatch is false, nothing is nothing.
    static func compare(_ a: GesComputedValue, _ b: GesComputedValue, _ sameUnit: Bool, _ relation: Int) -> GesComputedValue {
        if a.isNothing || b.isNothing { return .nothing }
        if !sameUnit { return .of(boolean: false) }
        if a.kind == .integer && b.kind == .integer {
            return .of(boolean: relation == 0 ? a.integer < b.integer : relation == 1 ? a.integer <= b.integer : relation == 2 ? a.integer > b.integer : a.integer >= b.integer)
        }
        let x = a.numeric
        let y = b.numeric
        return .of(boolean: relation == 0 ? x < y : relation == 1 ? x <= y : relation == 2 ? x > y : x >= y)
    }

    static func equal(_ a: GesComputedValue, _ b: GesComputedValue, _ sameUnit: Bool, _ negate: Bool) -> GesComputedValue {
        if a.isNothing || b.isNothing { return .nothing }
        let equal: Bool
        if !a.isNumeric || !b.isNumeric {
            equal = a.kind == b.kind
        } else if a.kind == .integer && b.kind == .integer {
            equal = sameUnit && a.integer == b.integer
        } else {
            equal = sameUnit && GesComparison.near(a.numeric, b.numeric)
        }
        return .of(boolean: equal != negate)
    }

    static func not(_ a: GesComputedValue) -> GesComputedValue { a.kind == .true ? .of(boolean: false) : a.kind == .false ? .of(boolean: true) : .nothing }

    static func and(_ a: GesComputedValue, _ b: GesComputedValue) -> GesComputedValue {
        if a.kind == .false || b.kind == .false { return .of(boolean: false) }
        return a.kind == .true && b.kind == .true ? .of(boolean: true) : .nothing
    }

    static func or(_ a: GesComputedValue, _ b: GesComputedValue) -> GesComputedValue {
        if a.kind == .true || b.kind == .true { return .of(boolean: true) }
        return a.kind == .false && b.kind == .false ? .of(boolean: false) : .nothing
    }

    /// The smaller or larger of two numbers of one unit; on a tie the left one, keeping its kind.
    static func extreme(_ a: GesComputedValue, _ b: GesComputedValue, _ sameUnit: Bool, _ maximum: Bool) -> GesComputedValue {
        if a.isNothing || b.isNothing || !sameUnit { return .nothing }
        let right =
            a.kind == .integer && b.kind == .integer
            ? (maximum ? b.integer > a.integer : b.integer < a.integer) : (maximum ? b.numeric > a.numeric : b.numeric < a.numeric)
        return right ? b : a
    }

    static func clamp(_ value: GesComputedValue, _ first: GesComputedValue, _ second: GesComputedValue, _ sameUnit: Bool) -> GesComputedValue {
        if value.isNothing || first.isNothing || second.isNothing || !sameUnit { return .nothing }
        if value.kind == .integer && first.kind == .integer && second.kind == .integer {
            let low = first.integer <= second.integer ? first.integer : second.integer
            let high = first.integer <= second.integer ? second.integer : first.integer
            return .of(integer: value.integer < low ? low : value.integer > high ? high : value.integer)
        }
        let x = value.numeric
        let a = first.numeric
        let b = second.numeric
        let lower = a <= b ? a : b
        let upper = a <= b ? b : a
        return .of(float: x < lower ? lower : x > upper ? upper : x)
    }

    /// A power of the virtual machine: nothing for nothing, the platform's pow, and the correctly rounded square root for the
    /// exponent 0.5 that `sqrt` lowers to. The unit checks are made at binding.
    static func power(_ a: GesComputedValue, _ b: GesComputedValue) -> GesComputedValue {
        a.isNothing || b.isNothing ? .nothing : .of(float: GesNumber.power(a.numeric, b.numeric))
    }

    /// A function of one number in binary64: nothing for nothing, the function's result otherwise.
    static func function(_ op: GesComputedOp, _ a: GesComputedValue, _ degrees: Bool) -> GesComputedValue {
        if a.isNothing { return .nothing }
        let x = a.numeric
        let radians = degrees ? x / 180 * Double.pi : x
        switch op {
        case .sqrt: return .of(float: GesNumber.squareRoot(x))
        case .cbrt: return .of(float: pow(x, 1.0 / 3.0))
        case .sin: return .of(float: sin(radians))
        case .cos: return .of(float: cos(radians))
        case .tan: return .of(float: tan(radians))
        case .asin: return .of(float: asin(x))
        case .acos: return .of(float: acos(x))
        case .atan: return .of(float: atan(x))
        case .exp: return .of(float: exp(x))
        case .ln: return .of(float: log(x))
        case .floor: return .of(rounded: x.rounded(.down))
        case .ceil: return .of(rounded: x.rounded(.up))
        case .truncate: return .of(rounded: x.rounded(.towardZero))
        case .roundHalfEven: return .of(rounded: x.rounded(.toNearestOrEven))
        case .roundHalfUp: return .of(rounded: x.rounded(.toNearestOrAwayFromZero))
        case .roundHalfDown:
            let truncated = x.rounded(.towardZero)
            return .of(rounded: Swift.abs(x - truncated) <= 0.5 ? truncated : x.rounded(.toNearestOrAwayFromZero))
        case .rad: return .of(float: x.isFinite ? x / 180 * Double.pi : .nan)
        default: return .of(float: x.isFinite ? x / Double.pi * 180 : .nan)
        }
    }

    /// `atan2(y, x)` in radians, 0 for (0, 0).
    static func arctangent(_ y: GesComputedValue, _ x: GesComputedValue) -> GesComputedValue {
        if y.isNothing || x.isNothing { return .nothing }
        return .of(float: y.numeric == 0 && x.numeric == 0 ? 0 : atan2(y.numeric, x.numeric))
    }

    /// The Euclidean length of two or three components of one unit.
    static func hypotenuse(_ a: GesComputedValue, _ b: GesComputedValue, _ c: GesComputedValue?) -> GesComputedValue {
        if a.isNothing || b.isNothing || c?.isNothing == true { return .nothing }
        let x = a.numeric
        let y = b.numeric
        guard let z = c?.numeric else { return .of(float: (x * x + y * y).squareRoot()) }
        return .of(float: (x * x + y * y + z * z).squareRoot())
    }

    /// The conversion to the field's type that a script record makes: a Percentage takes a finite number as its ratio, and a
    /// Boolean is true only for true.
    static func cast(_ value: GesComputedValue, _ type: GesComputedType) -> GesComputedValue {
        switch type {
        case .percentage where value.kind == .integer || value.kind == .float:
            let ratio = value.numeric
            return ratio.isFinite ? .of(percentage: ratio) : .nothing
        case .boolean:
            return .of(boolean: value.kind == .true)
        default:
            return value
        }
    }
}

/// The spatial operations of the GES virtual machine and the geometry library on computed values: components are binary64, a
/// NaN component makes a Vector or Point nothing, a scalar must be finite and a divisor finite and not zero. The unit checks
/// are made at binding.
enum GesComputedSpatial {
    static func vector(_ x: GesComputedValue, _ y: GesComputedValue, _ z: GesComputedValue, point: Bool) -> GesComputedValue {
        if x.isNothing || y.isNothing || z.isNothing { return .nothing }
        return point ? .of(point: x.numeric, y.numeric, z.numeric) : .of(vector: x.numeric, y.numeric, z.numeric)
    }

    /// vector + vector, point + vector; with subtract also point - point, which gives a vector.
    static func add(_ a: GesComputedValue, _ b: GesComputedValue, subtract: Bool) -> GesComputedValue {
        if a.isNothing || b.isNothing { return .nothing }
        let left = a.components
        let right = b.components
        let x = subtract ? left.x - right.x : left.x + right.x
        let y = subtract ? left.y - right.y : left.y + right.y
        let z = subtract ? left.z - right.z : left.z + right.z
        return a.kind == .point && b.kind == .vector ? .of(point: x, y, z) : .of(vector: x, y, z)
    }

    /// A vector times a finite number or a percentage's ratio.
    static func scale(_ vector: GesComputedValue, _ scalar: GesComputedValue) -> GesComputedValue {
        if vector.isNothing || scalar.isNothing { return .nothing }
        let s = scalar.numeric
        if !s.isFinite { return .nothing }
        let v = vector.vector
        return .of(vector: v.x * s, v.y * s, v.z * s)
    }

    /// A vector divided by a finite number or percentage ratio other than zero.
    static func divide(_ vector: GesComputedValue, _ scalar: GesComputedValue) -> GesComputedValue {
        if vector.isNothing || scalar.isNothing { return .nothing }
        let s = scalar.numeric
        if !s.isFinite || s == 0 { return .nothing }
        let v = vector.vector
        return .of(vector: v.x / s, v.y / s, v.z / s)
    }

    static func negate(_ vector: GesComputedValue) -> GesComputedValue {
        if vector.isNothing { return .nothing }
        let v = vector.vector
        return .of(vector: -v.x, -v.y, -v.z)
    }

    /// `abs` of a vector: its length, the correctly rounded square root of the summed squares.
    static func length(_ vector: GesComputedValue) -> GesComputedValue {
        if vector.isNothing { return .nothing }
        let v = vector.vector
        return .of(float: (v.x * v.x + v.y * v.y + v.z * v.z).squareRoot())
    }

    static func box(_ a: GesComputedValue, _ b: GesComputedValue, around: Bool) -> GesComputedValue {
        if a.isNothing || b.isNothing { return .nothing }
        return .of(box: around ? GesBox(center: a.point, size: b.vector) : GesBox(a.point, b.point))
    }

    static func disc(_ center: GesComputedValue, _ radius: GesComputedValue) -> GesComputedValue {
        if center.isNothing || radius.isNothing { return .nothing }
        return .of(disc: GesDisc(center: center.point, radius: radius.numeric))
    }

    static func disc(_ radius: GesComputedValue) -> GesComputedValue { radius.isNothing ? .nothing : .of(disc: GesDisc(radius: radius.numeric)) }

    /// A part of a spatial value as a script reads it: an axis, a corner, centre or size of a box, or a centre or radius of a
    /// disc. A NaN part is nothing.
    static func part(_ value: GesComputedValue, _ name: String) -> GesComputedValue {
        switch value.shape {
        case .vector, .point:
            let v = value.components
            return .of(float: name == "x" ? v.x : name == "y" ? v.y : v.z)
        case .box(let box):
            switch name {
            case "lower": return .of(point: box.min.x, box.min.y, box.min.z)
            case "upper": return .of(point: box.max.x, box.max.y, box.max.z)
            case "center":
                let center = box.center
                return .of(point: center.x, center.y, center.z)
            default:
                let size = box.size
                return .of(vector: size.x, size.y, size.z)
            }
        case .disc(let disc):
            return name == "center" ? .of(point: disc.center.x, disc.center.y, disc.center.z) : .of(float: disc.radius)
        default:
            return .nothing
        }
    }

    static func pointCount(_ poly: GesComputedValue) -> GesComputedValue {
        guard case .poly(let value) = poly.shape else { return .nothing }
        return .of(integer: Int64(value.points.count))
    }
}

/// What an expression reads: the fields of a record, and the elements of its lists. An element that is a number reads with
/// an empty path.
protocol GesComputedScope {
    func read(_ path: [String], percentage: Bool) -> GesComputedValue

    func has(_ path: [String]) -> Bool

    func count(_ path: [String]) -> Int

    func element(_ path: [String], _ index: Int) -> any GesComputedScope
}

enum GesComputedEvaluation {
    static func evaluate(_ node: GesComputedNode, _ scope: any GesComputedScope, _ item: (any GesComputedScope)?) -> GesComputedValue {
        let children = node.children
        switch node.op {
        case .literal:
            return node.literal
        case .field:
            return scope.read(node.path, percentage: node.type == .percentage)
        case .item:
            return item!.read(node.path, percentage: node.type == .percentage)
        case .negate where node.type == .vector:
            return GesComputedSpatial.negate(evaluate(children[0], scope, item))
        case .negate:
            return GesComputedMath.negate(evaluate(children[0], scope, item))
        case .not:
            return GesComputedMath.not(evaluate(children[0], scope, item))
        case .abs where children[0].type == .vector:
            return GesComputedSpatial.length(evaluate(children[0], scope, item))
        case .abs:
            return GesComputedMath.abs(evaluate(children[0], scope, item))
        case .add where node.type == .vector || node.type == .point, .subtract where node.type == .vector || node.type == .point:
            return GesComputedSpatial.add(evaluate(children[0], scope, item), evaluate(children[1], scope, item), subtract: node.op == .subtract)
        case .multiply where node.type == .vector:
            let a = evaluate(children[0], scope, item)
            let b = evaluate(children[1], scope, item)
            return children[0].type == .vector ? GesComputedSpatial.scale(a, b) : GesComputedSpatial.scale(b, a)
        case .divide where node.type == .vector:
            return GesComputedSpatial.divide(evaluate(children[0], scope, item), evaluate(children[1], scope, item))
        case .vector, .point:
            return GesComputedSpatial.vector(evaluate(children[0], scope, item), evaluate(children[1], scope, item), evaluate(children[2], scope, item), point: node.op == .point)
        case .box, .boxAround:
            return GesComputedSpatial.box(evaluate(children[0], scope, item), evaluate(children[1], scope, item), around: node.op == .boxAround)
        case .disc:
            return GesComputedSpatial.disc(evaluate(children[0], scope, item), evaluate(children[1], scope, item))
        case .discAround:
            return GesComputedSpatial.disc(evaluate(children[0], scope, item))
        case .poly:
            var points: [GesPoint] = []
            points.reserveCapacity(children.count)
            for child in children {
                let point = evaluate(child, scope, item)
                if point.isNothing { return .nothing }
                points.append(point.point)
            }
            return .of(poly: GesPoly(points))
        case .polyOf:
            let source = children[0]
            let list = source.op == .item ? item! : scope
            let count = list.count(source.path)
            var points: [GesPoint] = []
            points.reserveCapacity(count)
            for index in 0..<count {
                let element = list.element(source.path, index)
                let point = children.count > 1 ? evaluate(children[1], scope, element) : element.read([], percentage: false)
                if point.isNothing { return .nothing }
                points.append(point.point)
            }
            return .of(poly: GesPoly(points))
        case .part:
            return GesComputedSpatial.part(evaluate(children[0], scope, item), node.path[0])
        case .pointCount:
            return GesComputedSpatial.pointCount(evaluate(children[0], scope, item))
        case .add, .subtract:
            let a = evaluate(children[0], scope, item)
            let b = evaluate(children[1], scope, item)
            return node.op == .add ? GesComputedMath.add(a, b, node.first, node.second) : GesComputedMath.subtract(a, b, node.first, node.second)
        case .multiply:
            return GesComputedMath.multiply(evaluate(children[0], scope, item), evaluate(children[1], scope, item), node.first, node.second, node.third)
        case .divide:
            return GesComputedMath.divide(evaluate(children[0], scope, item), evaluate(children[1], scope, item), node.first, node.second, node.third)
        case .less, .lessOrEqual, .greater, .greaterOrEqual:
            let relation = node.op == .less ? 0 : node.op == .lessOrEqual ? 1 : node.op == .greater ? 2 : 3
            return GesComputedMath.compare(evaluate(children[0], scope, item), evaluate(children[1], scope, item), node.first, relation)
        case .equal, .notEqual:
            return GesComputedMath.equal(evaluate(children[0], scope, item), evaluate(children[1], scope, item), node.first, node.op == .notEqual)
        case .and:
            return GesComputedMath.and(evaluate(children[0], scope, item), evaluate(children[1], scope, item))
        case .or:
            return GesComputedMath.or(evaluate(children[0], scope, item), evaluate(children[1], scope, item))
        case .choice:
            for index in stride(from: 0, to: children.count - 1, by: 2) where evaluate(children[index + 1], scope, item).kind == .true {
                return evaluate(children[index], scope, item)
            }
            return evaluate(children[children.count - 1], scope, item)
        case .min, .max:
            var result = evaluate(children[0], scope, item)
            for child in children.dropFirst() { result = GesComputedMath.extreme(result, evaluate(child, scope, item), node.first, node.op == .max) }
            return result
        case .clamp:
            return GesComputedMath.clamp(evaluate(children[0], scope, item), evaluate(children[1], scope, item), evaluate(children[2], scope, item), node.first)
        case .default:
            let value = evaluate(children[0], scope, item)
            return value.isNothing ? evaluate(children[1], scope, item) : value
        case .hasValue:
            let child = children[0]
            if child.type == .record { return .of(boolean: child.op == .item ? item!.has(child.path) : scope.has(child.path)) }
            return .of(boolean: !evaluate(child, scope, item).isNothing)
        case .sum:
            let source = children[0]
            let list = source.op == .item ? item! : scope
            let count = list.count(source.path)
            var sum = GesComputedValue.nothing
            for index in 0..<count {
                let element = list.element(source.path, index)
                let value = children.count > 1 ? evaluate(children[1], scope, element) : element.read([], percentage: node.type == .percentage)
                sum = index == 0 ? value : GesComputedMath.add(sum, value, true, true)
            }
            return count == 0 ? .of(integer: 0) : sum
        case .count:
            let source = children[0]
            return .of(integer: Int64((source.op == .item ? item! : scope).count(source.path)))
        case .power:
            return GesComputedMath.power(evaluate(children[0], scope, item), evaluate(children[1], scope, item))
        case .atan2:
            return GesComputedMath.arctangent(evaluate(children[0], scope, item), evaluate(children[1], scope, item))
        case .hypot:
            return GesComputedMath.hypotenuse(evaluate(children[0], scope, item), evaluate(children[1], scope, item), children.count > 2 ? evaluate(children[2], scope, item) : nil)
        default:
            return GesComputedMath.function(node.op, evaluate(children[0], scope, item), node.first)
        }
    }
}

/// The canonical spelling of computed fields, which the schema hash covers: a prefix form with literals as exact decimals.
enum GesComputedCanonical {
    static func write(_ node: GesComputedNode, _ output: inout String) {
        output += "("
        switch node.op {
        case .literal:
            switch node.literal.kind {
            case .true: output += "true"
            case .false: output += "false"
            default:
                if node.type == .percentage {
                    output += "percentage " + node.decimal.canonical
                } else if node.unit == .none {
                    output += "number " + node.decimal.canonical
                } else {
                    output += "quantity \(node.unit.rawValue) " + node.decimal.canonical
                }
            }
        case .field, .item:
            output += node.op == .field ? "field" : "item"
            for name in node.path { output += " " + name }
        case .part:
            output += "part " + node.path[0] + " "
            write(node.children[0], &output)
        default:
            output += node.op.name
            for child in node.children {
                output += " "
                write(child, &output)
            }
        }
        output += ")"
    }
}
