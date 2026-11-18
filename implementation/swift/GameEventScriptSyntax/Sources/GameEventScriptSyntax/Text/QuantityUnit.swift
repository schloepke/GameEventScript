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
    /// Area measured in square meters.
    case squareMeter
    /// Volume measured in cubic meters.
    case cubicMeter
    /// Flux or intensity measured in watts per square meter.
    case wattPerSquareMeter
    /// Temperature measured in kelvins.
    case kelvin
    /// Angular speed measured in degrees per second.
    case degreePerSecond
    /// A level on a logarithmic scale measured in decibels.
    case decibel
    /// Thermal conductance measured in watts per kelvin.
    case wattPerKelvin
    /// Heat capacity measured in joules per kelvin.
    case joulePerKelvin
    /// Pressure or stress measured in pascals.
    case pascal
    /// Density measured in kilograms per cubic meter.
    case kilogramPerCubicMeter
    /// Specific energy measured in joules per kilogram.
    case joulePerKilogram
    /// Specific heat capacity measured in joules per kilogram kelvin.
    case joulePerKilogramKelvin
    /// Torque measured in newton meters.
    case newtonMeter
    /// Moment of inertia measured in kilogram square meters.
    case kilogramSquareMeter
    /// Angular acceleration measured in degrees per second squared.
    case degreePerSecondSquared
    /// Mass flow measured in kilograms per second.
    case kilogramPerSecond
    /// Electrical resistance measured in ohms.
    case ohm
    /// Electric charge measured in coulombs.
    case coulomb

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
        case .squareMeter: "m²"
        case .cubicMeter: "m³"
        case .wattPerSquareMeter: "W/m²"
        case .kelvin: "K"
        case .degreePerSecond: "°/s"
        case .decibel: "dB"
        case .wattPerKelvin: "W/K"
        case .joulePerKelvin: "J/K"
        case .pascal: "Pa"
        case .kilogramPerCubicMeter: "kg/m³"
        case .joulePerKilogram: "J/kg"
        case .joulePerKilogramKelvin: "J/(kg·K)"
        case .newtonMeter: "N·m"
        case .kilogramSquareMeter: "kg·m²"
        case .degreePerSecondSquared: "°/s²"
        case .kilogramPerSecond: "kg/s"
        case .ohm: "Ω"
        case .coulomb: "C"
        }
    }
}
