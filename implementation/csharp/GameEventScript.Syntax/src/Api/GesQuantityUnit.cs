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
    /// Identifies the square meter (m²), for areas.
    /// </summary>
    UnitSquareMeter = 13,
    /// <summary>
    /// Identifies the cubic meter (m³), for volumes.
    /// </summary>
    UnitCubicMeter = 14,
    /// <summary>
    /// Identifies the watt per square meter (W/m²), for flux and intensity.
    /// </summary>
    UnitWattPerSquareMeter = 15,
    /// <summary>
    /// Identifies the kelvin (K), for temperature.
    /// </summary>
    UnitKelvin = 16,
    /// <summary>
    /// Identifies the degree per second (°/s), for angular speed.
    /// </summary>
    UnitDegreePerSecond = 17,
    /// <summary>
    /// Identifies the decibel (dB), for levels on a logarithmic scale.
    /// </summary>
    UnitDecibel = 18,
    /// <summary>
    /// Identifies the watt per kelvin (W/K), for thermal conductance.
    /// </summary>
    UnitWattPerKelvin = 19,
    /// <summary>
    /// Identifies the joule per kelvin (J/K), for heat capacity.
    /// </summary>
    UnitJoulePerKelvin = 20,
    /// <summary>
    /// Identifies the pascal (Pa), for pressure and stress.
    /// </summary>
    UnitPascal = 21,
    /// <summary>
    /// Identifies the kilogram per cubic meter (kg/m³), for density.
    /// </summary>
    UnitKilogramPerCubicMeter = 22,
    /// <summary>
    /// Identifies the joule per kilogram (J/kg), for specific energy.
    /// </summary>
    UnitJoulePerKilogram = 23,
    /// <summary>
    /// Identifies the joule per kilogram kelvin (J/(kg·K)), for specific heat capacity.
    /// </summary>
    UnitJoulePerKilogramKelvin = 24,
    /// <summary>
    /// Identifies the newton meter (N·m), for torque.
    /// </summary>
    UnitNewtonMeter = 25,
    /// <summary>
    /// Identifies the kilogram square meter (kg·m²), for moment of inertia.
    /// </summary>
    UnitKilogramSquareMeter = 26,
    /// <summary>
    /// Identifies the degree per second squared (°/s²), for angular acceleration.
    /// </summary>
    UnitDegreePerSecondSquared = 27,
    /// <summary>
    /// Identifies the kilogram per second (kg/s), for mass flow.
    /// </summary>
    UnitKilogramPerSecond = 28,
    /// <summary>
    /// Identifies the ohm (Ω), for electrical resistance.
    /// </summary>
    UnitOhm = 29,
    /// <summary>
    /// Identifies the coulomb (C), for electric charge.
    /// </summary>
    UnitCoulomb = 30,

    /// <summary>
    /// Identifies the unit invalid value.
    /// </summary>
    UnitInvalid = 255 // Sentinel for invalid or unsupported unit input.
}
