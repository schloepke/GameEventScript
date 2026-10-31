// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// Portable quantity units. The names do not depend on the embedding platform.
public enum GesQuantityUnit: String, Hashable, Sendable {
    /// A unitless value.
    case none
    /// Distance measured in meters.
    case meter
    /// Duration measured in seconds.
    case second
    /// Angle measured in degrees.
    case degree
    /// Speed measured in meters per second.
    case meterPerSecond
    /// Acceleration measured in meters per second squared.
    case meterPerSecondSquared
    /// Mass measured in kilograms.
    case kilogram
    /// Force measured in newtons.
    case newton
    /// Energy measured in joules.
    case joule
    /// Power measured in watts.
    case watt
    /// Voltage measured in volts.
    case volt
    /// Current measured in amperes.
    case ampere
    /// Frequency measured in hertz.
    case hertz
    /// Temperature measured in kelvins.
    case kelvin
    /// Angular speed measured in degrees per second.
    case degreePerSecond

    /// Canonical text suffix that formatting appends, such as `m`, `m/s` or `°/s`; empty for unitless values.
    public var suffix: String {
        switch self {
        case .none: ""
        case .meter: "m"
        case .second: "s"
        case .degree: "°"
        case .meterPerSecond: "m/s"
        case .meterPerSecondSquared: "m/s²"
        case .kilogram: "kg"
        case .newton: "N"
        case .joule: "J"
        case .watt: "W"
        case .volt: "V"
        case .ampere: "A"
        case .hertz: "Hz"
        case .kelvin: "K"
        case .degreePerSecond: "°/s"
        }
    }
}
