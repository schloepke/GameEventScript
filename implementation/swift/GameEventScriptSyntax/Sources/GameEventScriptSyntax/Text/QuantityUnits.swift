// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

extension GesQuantityUnit {
    /// One spelling of a unit suffix. Prefixed and energy spellings scale the decimal value exactly by
    /// `factor × 10^exponent` before the single binary64 rounding; the value keeps the base unit.
    @_spi(Syntax) public struct Suffix: Sendable {
        @_spi(Syntax) public let text: String
        /// The spelling's Unicode scalars, kept so matching allocates nothing.
        @_spi(Syntax) public let scalars: [Unicode.Scalar]
        @_spi(Syntax) public let unit: GesQuantityUnit
        @_spi(Syntax) public let exponent: Int
        @_spi(Syntax) public let factor: Int

        @_spi(Syntax) public init(text: String, unit: GesQuantityUnit, exponent: Int = 0, factor: Int = 1) {
            self.text = text
            scalars = Array(text.unicodeScalars)
            self.unit = unit
            self.exponent = exponent
            self.factor = factor
        }
    }

    // Longest spellings first, so a suffix match is always the longest matching spelling.
    @_spi(Syntax) public static let suffixes: [Suffix] = [
        .init(text: "m/s²", unit: .meterPerSecondSquared),
        .init(text: "m/s^2", unit: .meterPerSecondSquared),
        .init(text: "deg/s", unit: .degreePerSecond),
        .init(text: "°/s", unit: .degreePerSecond),
        .init(text: "m/s", unit: .meterPerSecond),
        .init(text: "kWh", unit: .joule, exponent: 5, factor: 36),
        .init(text: "kHz", unit: .hertz, exponent: 3),
        .init(text: "deg", unit: .degree),
        .init(text: "Wh", unit: .joule, exponent: 2, factor: 36),
        .init(text: "Hz", unit: .hertz),
        .init(text: "km", unit: .meter, exponent: 3),
        .init(text: "ms", unit: .second, exponent: -3),
        .init(text: "mm", unit: .meter, exponent: -3),
        .init(text: "kg", unit: .kilogram),
        .init(text: "kN", unit: .newton, exponent: 3),
        .init(text: "kJ", unit: .joule, exponent: 3),
        .init(text: "MJ", unit: .joule, exponent: 6),
        .init(text: "kW", unit: .watt, exponent: 3),
        .init(text: "MW", unit: .watt, exponent: 6),
        .init(text: "kV", unit: .volt, exponent: 3),
        .init(text: "°", unit: .degree),
        .init(text: "m", unit: .meter),
        .init(text: "s", unit: .second),
        .init(text: "N", unit: .newton),
        .init(text: "J", unit: .joule),
        .init(text: "W", unit: .watt),
        .init(text: "V", unit: .volt),
        .init(text: "A", unit: .ampere),
        .init(text: "K", unit: .kelvin),
    ]

    /// Stable bytecode UnitId from the Bytecode specification; it also orders units in sorting and map keys.
    /// The first scalars of every unit suffix spelling, so a lexer can skip the suffix table for unitless numbers.
    @_spi(Syntax) public static let suffixStarts = Set(suffixes.compactMap(\.scalars.first))

    /// The longest unit suffix spelling at `start` that ends at a token boundary, as its scalar count; 0 when none matches.
    @_spi(Syntax) public static func suffixLength(_ scalars: [Unicode.Scalar], at start: Int, boundary: (Unicode.Scalar) -> Bool) -> Int {
        for suffix in suffixes {
            let end = start + suffix.scalars.count
            guard end <= scalars.count, scalars[start..<end].elementsEqual(suffix.scalars) else { continue }
            if end == scalars.count || boundary(scalars[end]) { return suffix.scalars.count }
        }
        return 0
    }

    @_spi(Syntax) public var unitID: UInt8 {
        switch self {
        case .none: 0
        case .degree: 1
        case .meter: 2
        case .second: 3
        case .meterPerSecond: 4
        case .meterPerSecondSquared: 5
        case .kilogram: 6
        case .newton: 7
        case .joule: 8
        case .watt: 9
        case .volt: 10
        case .ampere: 11
        case .hertz: 12
        case .kelvin: 16
        case .degreePerSecond: 17
        }
    }

    /// The unit for a bytecode UnitId, or nil for an unassigned id.
    @_spi(Syntax) public init?(unitID: UInt8) {
        switch unitID {
        case 0: self = .none
        case 1: self = .degree
        case 2: self = .meter
        case 3: self = .second
        case 4: self = .meterPerSecond
        case 5: self = .meterPerSecondSquared
        case 6: self = .kilogram
        case 7: self = .newton
        case 8: self = .joule
        case 9: self = .watt
        case 10: self = .volt
        case 11: self = .ampere
        case 12: self = .hertz
        case 16: self = .kelvin
        case 17: self = .degreePerSecond
        default: return nil
        }
    }

    /// A portable unit type name such as `meterPerSecond`; `°` is also accepted for degree.
    @_spi(Syntax) public init?(typeName: String) {
        if typeName == "°" {
            self = .degree
        } else if typeName != "none", let unit = GesQuantityUnit(rawValue: typeName) {
            self = unit
        } else {
            return nil
        }
    }

    // `:Quantity(...)` accepts none, every unit symbol and its ASCII spelling, and the long names meter, second and
    // degree. Prefixed spellings are literal sugar only. `degree` also stands for `°` because the lexer reads `°` so.
    @_spi(Syntax) public init?(quantityName: String) {
        switch quantityName {
        case "none": self = .none
        case "degree", "°", "deg": self = .degree
        case "m", "meter": self = .meter
        case "s", "second": self = .second
        case "m/s": self = .meterPerSecond
        case "m/s²", "m/s^2": self = .meterPerSecondSquared
        case "kg": self = .kilogram
        case "N": self = .newton
        case "J": self = .joule
        case "W": self = .watt
        case "V": self = .volt
        case "A": self = .ampere
        case "Hz": self = .hertz
        case "K": self = .kelvin
        case "°/s", "degree/s", "deg/s": self = .degreePerSecond
        default: return nil
        }
    }

    /// A canonical unit symbol exactly as formatting writes it, for example `m/s`; the empty symbol is none.
    @_spi(Syntax) public init?(symbol: String) {
        if symbol.isEmpty {
            self = .none
            return
        }
        guard let match = GesQuantityUnit.suffixes.first(where: { $0.exponent == 0 && $0.factor == 1 && $0.text == symbol && $0.unit.suffix == symbol }) else { return nil }
        self = match.unit
    }

    // Unit of a product. Multiplying by a unitless factor keeps the unit; other products follow the closed derivation
    // table and are otherwise unsupported.
    @_spi(Syntax) public static func product(_ left: GesQuantityUnit, _ right: GesQuantityUnit) -> GesQuantityUnit? {
        if left == .none { return right }
        if right == .none { return left }
        return derivedProduct(left, right) ?? derivedProduct(right, left)
    }

    // Unit of a quotient. Dividing by a unitless divisor keeps the unit, equal units cancel, and other quotients follow
    // the closed derivation table and are otherwise unsupported.
    @_spi(Syntax) public static func quotient(_ dividend: GesQuantityUnit, _ divisor: GesQuantityUnit) -> GesQuantityUnit? {
        if divisor == .none { return dividend }
        if dividend == divisor { return GesQuantityUnit.none }
        switch (dividend, divisor) {
        case (.none, .second): return .hertz
        case (.none, .hertz): return .second
        case (.meter, .second): return .meterPerSecond
        case (.meter, .meterPerSecond): return .second
        case (.meterPerSecond, .second): return .meterPerSecondSquared
        case (.meterPerSecond, .meterPerSecondSquared): return .second
        case (.degree, .second): return .degreePerSecond
        case (.degree, .degreePerSecond): return .second
        case (.newton, .kilogram): return .meterPerSecondSquared
        case (.newton, .meterPerSecondSquared): return .kilogram
        case (.joule, .meter): return .newton
        case (.joule, .newton): return .meter
        case (.joule, .second): return .watt
        case (.joule, .watt): return .second
        case (.watt, .volt): return .ampere
        case (.watt, .ampere): return .volt
        case (.watt, .hertz): return .joule
        case (.watt, .joule): return .hertz
        case (.meterPerSecond, .hertz): return .meter
        case (.meterPerSecond, .meter): return .hertz
        case (.degreePerSecond, .hertz): return .degree
        case (.degreePerSecond, .degree): return .hertz
        case (.meterPerSecondSquared, .hertz): return .meterPerSecond
        case (.meterPerSecondSquared, .meterPerSecond): return .hertz
        default: return nil
        }
    }

    private static func derivedProduct(_ left: GesQuantityUnit, _ right: GesQuantityUnit) -> GesQuantityUnit? {
        switch (left, right) {
        case (.meterPerSecond, .second): .meter
        case (.meterPerSecondSquared, .second): .meterPerSecond
        case (.degreePerSecond, .second): .degree
        case (.kilogram, .meterPerSecondSquared): .newton
        case (.newton, .meter): .joule
        case (.watt, .second): .joule
        case (.volt, .ampere): .watt
        case (.hertz, .second): GesQuantityUnit.none
        case (.meter, .hertz): .meterPerSecond
        case (.degree, .hertz): .degreePerSecond
        case (.meterPerSecond, .hertz): .meterPerSecondSquared
        case (.joule, .hertz): .watt
        default: nil
        }
    }
}
