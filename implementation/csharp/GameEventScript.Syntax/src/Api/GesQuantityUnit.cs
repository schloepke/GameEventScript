// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

namespace GameEventScript.Api;

/// <summary>
/// Defines the supported game event script bytecode instruction unit values.
/// </summary>
public enum GesQuantityUnit : byte
{
    /// <summary>
    /// Identifies the unit none value.
    /// </summary>
    UnitNone = 0,
    /// <summary>
    /// Identifies the unit degree value.
    /// </summary>
    UnitDegree = 1,
    /// <summary>
    /// Identifies the unit meter value.
    /// </summary>
    UnitMeter = 2,
    /// <summary>
    /// Identifies the unit second value.
    /// </summary>
    UnitSecond = 3,
    /// <summary>
    /// Identifies the meter per second (m/s), for speed.
    /// </summary>
    UnitMeterPerSecond = 4,
    /// <summary>
    /// Identifies the meter per second squared (m/s²), for acceleration.
    /// </summary>
    UnitMeterPerSecondSquared = 5,
    /// <summary>
    /// Identifies the kilogram (kg), for mass.
    /// </summary>
    UnitKilogram = 6,
    /// <summary>
    /// Identifies the newton (N), for force.
    /// </summary>
    UnitNewton = 7,
    /// <summary>
    /// Identifies the joule (J), for energy.
    /// </summary>
    UnitJoule = 8,
    /// <summary>
    /// Identifies the watt (W), for power.
    /// </summary>
    UnitWatt = 9,
    /// <summary>
    /// Identifies the volt (V), for voltage.
    /// </summary>
    UnitVolt = 10,
    /// <summary>
    /// Identifies the ampere (A), for current.
    /// </summary>
    UnitAmpere = 11,
    /// <summary>
    /// Identifies the hertz (Hz), for frequency.
    /// </summary>
    UnitHertz = 12,
    /// <summary>
    /// Identifies the kelvin (K), for temperature.
    /// </summary>
    UnitKelvin = 16,
    /// <summary>
    /// Identifies the degree per second (°/s), for angular speed.
    /// </summary>
    UnitDegreePerSecond = 17,

    /// <summary>
    /// Identifies the unit invalid value.
    /// </summary>
    UnitInvalid = 255 // Sentinel for invalid or unsupported unit input.
}
