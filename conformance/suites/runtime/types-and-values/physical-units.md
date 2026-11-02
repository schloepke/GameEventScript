---
formatVersion: 1
suiteId: runtime.physical-units
title: Physical quantity units
kind: scriptApi
level: scenario
categories: [conformance]
tags: [quantity, units, arithmetic]
---

# Physical quantity units

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

These cases verify the closed set of quantity units: literal spellings and prefixes, the derivation table for products and quotients with folded and runtime operands, rejected combinations, casts, checks, and text formatting and parsing.

---

## Test: unit literals take the longest attached spelling

Every unit symbol and ASCII spelling forms one quantity literal when attached; with whitespace `10m / s` stays a division by the binding `s`.

### Case description

```yaml
gesBlock: case
id: literal-spellings
sources:
  - name: literal-spellings.ges
    program: main
```

### Source code under test

```ges
module main
on Start {
  let s be 2
  emit Lexed(speed: 10m/s, divided: 10m / s, accel: 6m/s², accelAscii: 6m/s^2, turn: 120°/s, turnAscii: 120deg/s, angle: 90deg, squared: 10m²)
  emit Units(mass: 3300kg, force: 21400N, energy: 500J, power: 120W, voltage: 400V, current: 300A, frequency: 20Hz, temperature: 350K)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| start | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  start:
    local:
      - name: Lexed
        args:
          - name: speed
            value: { type: ":Quantity.int64", value: "10", unit: ":meterPerSecond" }
          - name: divided
            value: { type: ":Quantity.int64", value: "5", unit: ":meter" }
          - name: accel
            value: { type: ":Quantity.int64", value: "6", unit: ":meterPerSecondSquared" }
          - name: accelAscii
            value: { type: ":Quantity.int64", value: "6", unit: ":meterPerSecondSquared" }
          - name: turn
            value: { type: ":Quantity.int64", value: "120", unit: ":degreePerSecond" }
          - name: turnAscii
            value: { type: ":Quantity.int64", value: "120", unit: ":degreePerSecond" }
          - name: angle
            value: { type: ":Quantity.int64", value: "90", unit: ":degree" }
          - name: squared
            value: { type: ":Quantity.int64", value: "10", unit: ":squareMeter" }
      - name: Units
        args:
          - name: mass
            value: { type: ":Quantity.int64", value: "3300", unit: ":kilogram" }
          - name: force
            value: { type: ":Quantity.int64", value: "21400", unit: ":newton" }
          - name: energy
            value: { type: ":Quantity.int64", value: "500", unit: ":joule" }
          - name: power
            value: { type: ":Quantity.int64", value: "120", unit: ":watt" }
          - name: voltage
            value: { type: ":Quantity.int64", value: "400", unit: ":volt" }
          - name: current
            value: { type: ":Quantity.int64", value: "300", unit: ":ampere" }
          - name: frequency
            value: { type: ":Quantity.int64", value: "20", unit: ":hertz" }
          - name: temperature
            value: { type: ":Quantity.int64", value: "350", unit: ":kelvin" }
```

---

## Test: prefixed spellings normalize exactly to the base unit

Prefixed literal spellings scale the exact decimal value once and keep the base unit.

### Case description

```yaml
gesBlock: case
id: prefixed-spellings
sources:
  - name: prefixed-spellings.ges
    program: main
```

### Source code under test

```ges
module main
on Start {
  emit Prefixed(km: 2km, mm: 250mm, ms: 3ms, halfKm: 0.5km, kN: 1.5kN, kJ: 2kJ, megaJoule: 1MJ, kW: 3kW, megaWatt: 2MW, kV: 11kV, kHz: 2kHz, wattHour: 1Wh, kWh: 1.5kWh, smallMs: 0.1ms)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| start | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  start:
    local:
      - name: Prefixed
        args:
          - name: km
            value: { type: ":Quantity.int64", value: "2000", unit: ":meter" }
          - name: mm
            value: { type: ":Quantity.binary64", value: "0.25", unit: ":meter" }
          - name: ms
            value: { type: ":Quantity.binary64", value: "0.003", unit: ":second" }
          - name: halfKm
            value: { type: ":Quantity.int64", value: "500", unit: ":meter" }
          - name: kN
            value: { type: ":Quantity.int64", value: "1500", unit: ":newton" }
          - name: kJ
            value: { type: ":Quantity.int64", value: "2000", unit: ":joule" }
          - name: megaJoule
            value: { type: ":Quantity.int64", value: "1000000", unit: ":joule" }
          - name: kW
            value: { type: ":Quantity.int64", value: "3000", unit: ":watt" }
          - name: megaWatt
            value: { type: ":Quantity.int64", value: "2000000", unit: ":watt" }
          - name: kV
            value: { type: ":Quantity.int64", value: "11000", unit: ":volt" }
          - name: kHz
            value: { type: ":Quantity.int64", value: "2000", unit: ":hertz" }
          - name: wattHour
            value: { type: ":Quantity.int64", value: "3600", unit: ":joule" }
          - name: kWh
            value: { type: ":Quantity.int64", value: "5400000", unit: ":joule" }
          - name: smallMs
            value: { type: ":Quantity.binary64", value: "0.0001", unit: ":second" }
```

---

## Test: kinematic and angular derivations match for folded and runtime operands

Products and quotients of meters, seconds, speeds, accelerations, degrees and angular speeds follow the derivation table, identically for constant-folded literals and runtime arguments.

### Case description

```yaml
gesBlock: case
id: kinematic-derivations
sources:
  - name: kinematic-derivations.ges
    program: main
```

### Source code under test

```ges
module main
on Derive(m, s, speed, accel, heading, turn) {
  emit Runtime(speed: m / s, distance: speed * s, distanceSwapped: s * speed, time: m / speed, accel: speed / s, speedFromAccel: accel * s, accelTime: speed / accel, turn: heading / s, heading: turn * s, turnTime: heading / turn)
  emit Folded(speed: 10m / 2s, distance: 5m/s * 2s, distanceSwapped: 2s * 5m/s, time: 10m / 5m/s, accel: 5m/s / 2s, speedFromAccel: 2.5m/s² * 2s, accelTime: 5m/s / 2.5m/s², turn: 90° / 2s, heading: 45°/s * 2s, turnTime: 90° / 45°/s)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| derive | Derive | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  derive:
    input:
      args:
        - name: m
          value: { type: ":Quantity.int64", value: "10", unit: ":meter" }
        - name: s
          value: { type: ":Quantity.int64", value: "2", unit: ":second" }
        - name: speed
          value: { type: ":Quantity.int64", value: "5", unit: ":meterPerSecond" }
        - name: accel
          value: { type: ":Quantity.binary64", value: "2.5", unit: ":meterPerSecondSquared" }
        - name: heading
          value: { type: ":Quantity.int64", value: "90", unit: ":degree" }
        - name: turn
          value: { type: ":Quantity.int64", value: "45", unit: ":degreePerSecond" }
    local:
      - name: Runtime
        args:
          - name: speed
            value: { type: ":Quantity.int64", value: "5", unit: ":meterPerSecond" }
          - name: distance
            value: { type: ":Quantity.int64", value: "10", unit: ":meter" }
          - name: distanceSwapped
            value: { type: ":Quantity.int64", value: "10", unit: ":meter" }
          - name: time
            value: { type: ":Quantity.int64", value: "2", unit: ":second" }
          - name: accel
            value: { type: ":Quantity.binary64", value: "2.5", unit: ":meterPerSecondSquared" }
          - name: speedFromAccel
            value: { type: ":Quantity.int64", value: "5", unit: ":meterPerSecond" }
          - name: accelTime
            value: { type: ":Quantity.int64", value: "2", unit: ":second" }
          - name: turn
            value: { type: ":Quantity.int64", value: "45", unit: ":degreePerSecond" }
          - name: heading
            value: { type: ":Quantity.int64", value: "90", unit: ":degree" }
          - name: turnTime
            value: { type: ":Quantity.int64", value: "2", unit: ":second" }
      - name: Folded
        args:
          - name: speed
            value: { type: ":Quantity.int64", value: "5", unit: ":meterPerSecond" }
          - name: distance
            value: { type: ":Quantity.int64", value: "10", unit: ":meter" }
          - name: distanceSwapped
            value: { type: ":Quantity.int64", value: "10", unit: ":meter" }
          - name: time
            value: { type: ":Quantity.int64", value: "2", unit: ":second" }
          - name: accel
            value: { type: ":Quantity.binary64", value: "2.5", unit: ":meterPerSecondSquared" }
          - name: speedFromAccel
            value: { type: ":Quantity.int64", value: "5", unit: ":meterPerSecond" }
          - name: accelTime
            value: { type: ":Quantity.int64", value: "2", unit: ":second" }
          - name: turn
            value: { type: ":Quantity.int64", value: "45", unit: ":degreePerSecond" }
          - name: heading
            value: { type: ":Quantity.int64", value: "90", unit: ":degree" }
          - name: turnTime
            value: { type: ":Quantity.int64", value: "2", unit: ":second" }
```

---

## Test: mechanical and electrical derivations match for folded and runtime operands

Mass, force, energy, power, voltage and current follow the derivation table, identically for constant-folded literals and runtime arguments.

### Case description

```yaml
gesBlock: case
id: mechanical-electrical-derivations
sources:
  - name: mechanical-electrical-derivations.ges
    program: main
```

### Source code under test

```ges
module main
on Derive(kg, accel, force, m, energy, s, power, volt, amp) {
  emit Runtime(force: kg * accel, forceSwapped: accel * kg, accel: force / kg, mass: force / accel, energy: force * m, forceFromEnergy: energy / m, distance: energy / force, power: energy / s, energyFromPower: power * s, time: energy / power, electric: volt * amp, current: power / volt, voltage: power / amp)
  emit Folded(force: 2kg * 3m/s², forceSwapped: 3m/s² * 2kg, accel: 6N / 2kg, mass: 6N / 3m/s², energy: 6N * 2m, forceFromEnergy: 12J / 2m, distance: 12J / 6N, power: 12J / 3s, energyFromPower: 4W * 3s, time: 12J / 4W, electric: 2V * 3A, current: 6W / 2V, voltage: 6W / 3A)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| derive | Derive | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  derive:
    input:
      args:
        - name: kg
          value: { type: ":Quantity.int64", value: "2", unit: ":kilogram" }
        - name: accel
          value: { type: ":Quantity.int64", value: "3", unit: ":meterPerSecondSquared" }
        - name: force
          value: { type: ":Quantity.int64", value: "6", unit: ":newton" }
        - name: m
          value: { type: ":Quantity.int64", value: "2", unit: ":meter" }
        - name: energy
          value: { type: ":Quantity.int64", value: "12", unit: ":joule" }
        - name: s
          value: { type: ":Quantity.int64", value: "3", unit: ":second" }
        - name: power
          value: { type: ":Quantity.int64", value: "4", unit: ":watt" }
        - name: volt
          value: { type: ":Quantity.int64", value: "2", unit: ":volt" }
        - name: amp
          value: { type: ":Quantity.int64", value: "3", unit: ":ampere" }
    local:
      - name: Runtime
        args:
          - name: force
            value: { type: ":Quantity.int64", value: "6", unit: ":newton" }
          - name: forceSwapped
            value: { type: ":Quantity.int64", value: "6", unit: ":newton" }
          - name: accel
            value: { type: ":Quantity.int64", value: "3", unit: ":meterPerSecondSquared" }
          - name: mass
            value: { type: ":Quantity.int64", value: "2", unit: ":kilogram" }
          - name: energy
            value: { type: ":Quantity.int64", value: "12", unit: ":joule" }
          - name: forceFromEnergy
            value: { type: ":Quantity.int64", value: "6", unit: ":newton" }
          - name: distance
            value: { type: ":Quantity.int64", value: "2", unit: ":meter" }
          - name: power
            value: { type: ":Quantity.int64", value: "4", unit: ":watt" }
          - name: energyFromPower
            value: { type: ":Quantity.int64", value: "12", unit: ":joule" }
          - name: time
            value: { type: ":Quantity.int64", value: "3", unit: ":second" }
          - name: electric
            value: { type: ":Quantity.int64", value: "6", unit: ":watt" }
          - name: current
            value: { type: ":Quantity.int64", value: "2", unit: ":ampere" }
          - name: voltage
            value: { type: ":Quantity.binary64", value: "1.3333333333333333", unit: ":volt" }
      - name: Folded
        args:
          - name: force
            value: { type: ":Quantity.int64", value: "6", unit: ":newton" }
          - name: forceSwapped
            value: { type: ":Quantity.int64", value: "6", unit: ":newton" }
          - name: accel
            value: { type: ":Quantity.int64", value: "3", unit: ":meterPerSecondSquared" }
          - name: mass
            value: { type: ":Quantity.int64", value: "2", unit: ":kilogram" }
          - name: energy
            value: { type: ":Quantity.int64", value: "12", unit: ":joule" }
          - name: forceFromEnergy
            value: { type: ":Quantity.int64", value: "6", unit: ":newton" }
          - name: distance
            value: { type: ":Quantity.int64", value: "2", unit: ":meter" }
          - name: power
            value: { type: ":Quantity.int64", value: "4", unit: ":watt" }
          - name: energyFromPower
            value: { type: ":Quantity.int64", value: "12", unit: ":joule" }
          - name: time
            value: { type: ":Quantity.int64", value: "3", unit: ":second" }
          - name: electric
            value: { type: ":Quantity.int64", value: "6", unit: ":watt" }
          - name: current
            value: { type: ":Quantity.int64", value: "3", unit: ":ampere" }
          - name: voltage
            value: { type: ":Quantity.int64", value: "2", unit: ":volt" }
```

---

## Test: frequency derivations match for folded and runtime operands

Unitless numbers, seconds and hertz convert into each other, and hertz turns distances, angles, speeds and energies into their rates, identically for constant-folded literals and runtime arguments.

### Case description

```yaml
gesBlock: case
id: frequency-derivations
sources:
  - name: frequency-derivations.ges
    program: main
```

### Source code under test

```ges
module main
on Derive(n, s, hz, m, heading, speed, energy, power) {
  emit Runtime(frequency: n / s, period: n / hz, cycles: hz * s, cyclesSwapped: s * hz, speed: m * hz, turn: heading * hz, accel: speed * hz, power: energy * hz, energy: power / hz)
  emit Inverse(rate: power / energy, distance: speed / hz, speedRate: speed / m, heading: (heading * hz) / hz, turnRate: (heading * hz) / heading, speed: (speed * hz) / hz, accelRate: (speed * hz) / speed)
  emit Folded(frequency: 1 / 2s, period: 1 / 4Hz, cycles: 4Hz * 2s, cyclesSwapped: 2s * 4Hz, speed: 3m * 2Hz, turn: 90° * 2Hz, accel: 5m/s * 2Hz, power: 10J * 2Hz, energy: 20W / 2Hz)
  emit FoldedInverse(rate: 20W / 10J, distance: 6m/s / 2Hz, speedRate: 6m/s / 3m, heading: 180°/s / 2Hz, turnRate: 180°/s / 90°, speed: 10m/s² / 2Hz, accelRate: 10m/s² / 5m/s)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| derive | Derive | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  derive:
    input:
      args:
        - name: n
          value: { type: ":Number.int64", value: "1" }
        - name: s
          value: { type: ":Quantity.int64", value: "2", unit: ":second" }
        - name: hz
          value: { type: ":Quantity.int64", value: "4", unit: ":hertz" }
        - name: m
          value: { type: ":Quantity.int64", value: "3", unit: ":meter" }
        - name: heading
          value: { type: ":Quantity.int64", value: "90", unit: ":degree" }
        - name: speed
          value: { type: ":Quantity.int64", value: "5", unit: ":meterPerSecond" }
        - name: energy
          value: { type: ":Quantity.int64", value: "10", unit: ":joule" }
        - name: power
          value: { type: ":Quantity.int64", value: "20", unit: ":watt" }
    local:
      - name: Runtime
        args:
          - name: frequency
            value: { type: ":Quantity.binary64", value: "0.5", unit: ":hertz" }
          - name: period
            value: { type: ":Quantity.binary64", value: "0.25", unit: ":second" }
          - name: cycles
            value: { type: ":Number.int64", value: "8" }
          - name: cyclesSwapped
            value: { type: ":Number.int64", value: "8" }
          - name: speed
            value: { type: ":Quantity.int64", value: "12", unit: ":meterPerSecond" }
          - name: turn
            value: { type: ":Quantity.int64", value: "360", unit: ":degreePerSecond" }
          - name: accel
            value: { type: ":Quantity.int64", value: "20", unit: ":meterPerSecondSquared" }
          - name: power
            value: { type: ":Quantity.int64", value: "40", unit: ":watt" }
          - name: energy
            value: { type: ":Quantity.int64", value: "5", unit: ":joule" }
      - name: Inverse
        args:
          - name: rate
            value: { type: ":Quantity.int64", value: "2", unit: ":hertz" }
          - name: distance
            value: { type: ":Quantity.binary64", value: "1.25", unit: ":meter" }
          - name: speedRate
            value: { type: ":Quantity.binary64", value: "1.6666666666666667", unit: ":hertz" }
          - name: heading
            value: { type: ":Quantity.int64", value: "90", unit: ":degree" }
          - name: turnRate
            value: { type: ":Quantity.int64", value: "4", unit: ":hertz" }
          - name: speed
            value: { type: ":Quantity.int64", value: "5", unit: ":meterPerSecond" }
          - name: accelRate
            value: { type: ":Quantity.int64", value: "4", unit: ":hertz" }
      - name: Folded
        args:
          - name: frequency
            value: { type: ":Quantity.binary64", value: "0.5", unit: ":hertz" }
          - name: period
            value: { type: ":Quantity.binary64", value: "0.25", unit: ":second" }
          - name: cycles
            value: { type: ":Number.int64", value: "8" }
          - name: cyclesSwapped
            value: { type: ":Number.int64", value: "8" }
          - name: speed
            value: { type: ":Quantity.int64", value: "6", unit: ":meterPerSecond" }
          - name: turn
            value: { type: ":Quantity.int64", value: "180", unit: ":degreePerSecond" }
          - name: accel
            value: { type: ":Quantity.int64", value: "10", unit: ":meterPerSecondSquared" }
          - name: power
            value: { type: ":Quantity.int64", value: "20", unit: ":watt" }
          - name: energy
            value: { type: ":Quantity.int64", value: "10", unit: ":joule" }
      - name: FoldedInverse
        args:
          - name: rate
            value: { type: ":Quantity.int64", value: "2", unit: ":hertz" }
          - name: distance
            value: { type: ":Quantity.int64", value: "3", unit: ":meter" }
          - name: speedRate
            value: { type: ":Quantity.int64", value: "2", unit: ":hertz" }
          - name: heading
            value: { type: ":Quantity.int64", value: "90", unit: ":degree" }
          - name: turnRate
            value: { type: ":Quantity.int64", value: "2", unit: ":hertz" }
          - name: speed
            value: { type: ":Quantity.int64", value: "5", unit: ":meterPerSecond" }
          - name: accelRate
            value: { type: ":Quantity.int64", value: "2", unit: ":hertz" }
```

---

## Test: area, volume, flux, level and rate spellings form one literal

The square and cube meter, watt per square meter and decibel take their symbols and ASCII spellings, `2/s` is a rate while `2 / s` divides, `(2m)²` squares, and the new prefixed spellings scale exactly to the base unit.

### Case description

```yaml
gesBlock: case
id: area-volume-flux-level-literals
sources:
  - name: area-volume-flux-level-literals.ges
    program: main
```

### Source code under test

```ges
module main
on Start {
  let s be 4
  emit Lexed(area: 2m², areaAscii: 2m^2, volume: 0.5m³, volumeAscii: 3m^3, flux: 3W/m², fluxAscii: 3W/m^2, level: 6dB, rate: 2/s, divided: 2 / s, squared: (2m)²)
  emit Prefixed(centimeter: 3cm², kilometer: 2km^2, milliwatt: 3mW, sensitivity: 14mW/m², kiloFlux: 2kW/m², tonne: 2t, halfTonne: 0.5t)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| start | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  start:
    local:
      - name: Lexed
        args:
          - name: area
            value: { type: ":Quantity.int64", value: "2", unit: ":squareMeter" }
          - name: areaAscii
            value: { type: ":Quantity.int64", value: "2", unit: ":squareMeter" }
          - name: volume
            value: { type: ":Quantity.binary64", value: "0.5", unit: ":cubicMeter" }
          - name: volumeAscii
            value: { type: ":Quantity.int64", value: "3", unit: ":cubicMeter" }
          - name: flux
            value: { type: ":Quantity.int64", value: "3", unit: ":wattPerSquareMeter" }
          - name: fluxAscii
            value: { type: ":Quantity.int64", value: "3", unit: ":wattPerSquareMeter" }
          - name: level
            value: { type: ":Quantity.int64", value: "6", unit: ":decibel" }
          - name: rate
            value: { type: ":Quantity.int64", value: "2", unit: ":hertz" }
          - name: divided
            value: { type: ":Number.binary64", value: "0.5" }
          - name: squared
            value: { type: ":Quantity.int64", value: "4", unit: ":squareMeter" }
      - name: Prefixed
        args:
          - name: centimeter
            value: { type: ":Quantity.binary64", value: "0.0003", unit: ":squareMeter" }
          - name: kilometer
            value: { type: ":Quantity.int64", value: "2000000", unit: ":squareMeter" }
          - name: milliwatt
            value: { type: ":Quantity.binary64", value: "0.003", unit: ":watt" }
          - name: sensitivity
            value: { type: ":Quantity.binary64", value: "0.014", unit: ":wattPerSquareMeter" }
          - name: kiloFlux
            value: { type: ":Quantity.int64", value: "2000", unit: ":wattPerSquareMeter" }
          - name: tonne
            value: { type: ":Quantity.int64", value: "2000", unit: ":kilogram" }
          - name: halfTonne
            value: { type: ":Quantity.int64", value: "500", unit: ":kilogram" }
```

---

## Test: area, volume and flux derivations match for folded and runtime operands

Meters multiply into areas and volumes and divide back, and watts divided by an area give a flux that multiplies back into watts, identically for constant-folded literals and runtime arguments.

### Case description

```yaml
gesBlock: case
id: area-volume-flux-derivations
sources:
  - name: area-volume-flux-derivations.ges
    program: main
```

### Source code under test

```ges
module main
on Derive(m, area, volume, power, flux) {
  emit Runtime(area: m * m, volume: area * m, volumeSwapped: m * area, length: area / m, areaFromVolume: volume / m, lengthFromVolume: volume / area, flux: power / area, power: flux * area, powerSwapped: area * flux, aperture: power / flux)
  emit Folded(area: 3m * 3m, volume: 6m² * 3m, volumeSwapped: 3m * 6m², length: 6m² / 3m, areaFromVolume: 12m³ / 3m, lengthFromVolume: 12m³ / 6m², flux: 10W / 6m², power: 5W/m² * 6m², powerSwapped: 6m² * 5W/m², aperture: 10W / 5W/m²)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| derive | Derive | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  derive:
    input:
      args:
        - name: m
          value: { type: ":Quantity.int64", value: "3", unit: ":meter" }
        - name: area
          value: { type: ":Quantity.int64", value: "6", unit: ":squareMeter" }
        - name: volume
          value: { type: ":Quantity.int64", value: "12", unit: ":cubicMeter" }
        - name: power
          value: { type: ":Quantity.int64", value: "10", unit: ":watt" }
        - name: flux
          value: { type: ":Quantity.int64", value: "5", unit: ":wattPerSquareMeter" }
    local:
      - name: Runtime
        args:
          - name: area
            value: { type: ":Quantity.int64", value: "9", unit: ":squareMeter" }
          - name: volume
            value: { type: ":Quantity.int64", value: "18", unit: ":cubicMeter" }
          - name: volumeSwapped
            value: { type: ":Quantity.int64", value: "18", unit: ":cubicMeter" }
          - name: length
            value: { type: ":Quantity.int64", value: "2", unit: ":meter" }
          - name: areaFromVolume
            value: { type: ":Quantity.int64", value: "4", unit: ":squareMeter" }
          - name: lengthFromVolume
            value: { type: ":Quantity.int64", value: "2", unit: ":meter" }
          - name: flux
            value: { type: ":Quantity.binary64", value: "1.6666666666666667", unit: ":wattPerSquareMeter" }
          - name: power
            value: { type: ":Quantity.int64", value: "30", unit: ":watt" }
          - name: powerSwapped
            value: { type: ":Quantity.int64", value: "30", unit: ":watt" }
          - name: aperture
            value: { type: ":Quantity.int64", value: "2", unit: ":squareMeter" }
      - name: Folded
        args:
          - name: area
            value: { type: ":Quantity.int64", value: "9", unit: ":squareMeter" }
          - name: volume
            value: { type: ":Quantity.int64", value: "18", unit: ":cubicMeter" }
          - name: volumeSwapped
            value: { type: ":Quantity.int64", value: "18", unit: ":cubicMeter" }
          - name: length
            value: { type: ":Quantity.int64", value: "2", unit: ":meter" }
          - name: areaFromVolume
            value: { type: ":Quantity.int64", value: "4", unit: ":squareMeter" }
          - name: lengthFromVolume
            value: { type: ":Quantity.int64", value: "2", unit: ":meter" }
          - name: flux
            value: { type: ":Quantity.binary64", value: "1.6666666666666667", unit: ":wattPerSquareMeter" }
          - name: power
            value: { type: ":Quantity.int64", value: "30", unit: ":watt" }
          - name: powerSwapped
            value: { type: ":Quantity.int64", value: "30", unit: ":watt" }
          - name: aperture
            value: { type: ":Quantity.int64", value: "2", unit: ":squareMeter" }
```

---

## Test: quantity squares, cubes and roots follow the unit derivations

Exponents 2 and 3 take the unit of the derived product, square and cube roots invert it, and powers without a derivation are invalid, identically for constant-folded literals and runtime arguments.

### Case description

```yaml
gesBlock: case
id: quantity-squares-cubes-roots
sources:
  - name: quantity-squares-cubes-roots.ges
    program: main
```

### Source code under test

```ges
module main
on Power(m, area, volume, s) {
  emit Runtime(square: m², squareAscii: m ^ 2, cube: m³, cubeAscii: m ^ 3, root: sqrt area, rootSymbol: √area, cubeRoot: cbrt volume, cubeRootSymbol: ∛volume, time: s², rootOfLength: sqrt m, squareOfArea: area², half: m ^ 0.5)
  emit Folded(square: (3m)², squareAscii: (3m) ^ 2, cube: (2m)³, cubeAscii: (2m) ^ 3, root: sqrt 16m², rootSymbol: √16m², cubeRoot: cbrt 8m³, cubeRootSymbol: ∛8m³, time: (2s)², rootOfLength: sqrt 4m, squareOfArea: (2m²)², half: (4m) ^ 0.5)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| power | Power | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  power:
    input:
      args:
        - name: m
          value: { type: ":Quantity.int64", value: "3", unit: ":meter" }
        - name: area
          value: { type: ":Quantity.int64", value: "16", unit: ":squareMeter" }
        - name: volume
          value: { type: ":Quantity.int64", value: "8", unit: ":cubicMeter" }
        - name: s
          value: { type: ":Quantity.int64", value: "2", unit: ":second" }
    local:
      - name: Runtime
        args:
          - name: square
            value: { type: ":Quantity.int64", value: "9", unit: ":squareMeter" }
          - name: squareAscii
            value: { type: ":Quantity.int64", value: "9", unit: ":squareMeter" }
          - name: cube
            value: { type: ":Quantity.int64", value: "27", unit: ":cubicMeter" }
          - name: cubeAscii
            value: { type: ":Quantity.int64", value: "27", unit: ":cubicMeter" }
          - name: root
            value: { type: ":Quantity.int64", value: "4", unit: ":meter" }
          - name: rootSymbol
            value: { type: ":Quantity.int64", value: "4", unit: ":meter" }
          - name: cubeRoot
            value: { type: ":Quantity.int64", value: "2", unit: ":meter" }
          - name: cubeRootSymbol
            value: { type: ":Quantity.int64", value: "2", unit: ":meter" }
          - name: time
            value: { type: ":Nothing" }
          - name: rootOfLength
            value: { type: ":Nothing" }
          - name: squareOfArea
            value: { type: ":Nothing" }
          - name: half
            value: { type: ":Nothing" }
      - name: Folded
        args:
          - name: square
            value: { type: ":Quantity.int64", value: "9", unit: ":squareMeter" }
          - name: squareAscii
            value: { type: ":Quantity.int64", value: "9", unit: ":squareMeter" }
          - name: cube
            value: { type: ":Quantity.int64", value: "8", unit: ":cubicMeter" }
          - name: cubeAscii
            value: { type: ":Quantity.int64", value: "8", unit: ":cubicMeter" }
          - name: root
            value: { type: ":Quantity.int64", value: "4", unit: ":meter" }
          - name: rootSymbol
            value: { type: ":Quantity.int64", value: "4", unit: ":meter" }
          - name: cubeRoot
            value: { type: ":Quantity.int64", value: "2", unit: ":meter" }
          - name: cubeRootSymbol
            value: { type: ":Quantity.int64", value: "2", unit: ":meter" }
          - name: time
            value: { type: ":Nothing" }
          - name: rootOfLength
            value: { type: ":Nothing" }
          - name: squareOfArea
            value: { type: ":Nothing" }
          - name: half
            value: { type: ":Nothing" }
```

---

## Test: decibel levels keep same-unit rules without derivations or percentages

Levels add, compare and scale by unitless numbers, a level divided by a level is unitless, and every combination with another unit or a percentage gives nothing, while other quantities keep the percentage rule.

### Case description

```yaml
gesBlock: case
id: decibel-levels
sources:
  - name: decibel-levels.ges
    program: main
```

### Source code under test

```ges
module main
on Level(level, gain, percent, power) {
  emit Runtime(sum: level + gain, difference: level - gain, half: level * 0.5, scaled: 2 * gain, ratio: level / gain, lower: min of level and gain, louder: level > gain, magnitude: abs (gain - level))
  emit Rejected(plus: level + percent, minus: level - percent, times: level * percent, timesSwapped: percent * level, over: level / percent, integerOver: level div percent, watts: level * power, mixed: level + power)
  emit Folded(sum: 3dB + 3dB, half: 6dB * 0.5, ratio: 6dB / 2dB, plus: 6dB + 50%, times: 6dB * 50%, timesSwapped: 50% * 6dB, watts: 3dB * 2W, meters: 10m + 50%)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| level | Level | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  level:
    input:
      args:
        - name: level
          value: { type: ":Quantity.int64", value: "6", unit: ":decibel" }
        - name: gain
          value: { type: ":Quantity.int64", value: "3", unit: ":decibel" }
        - name: percent
          value: { type: ":Percentage", value: "0.5" }
        - name: power
          value: { type: ":Quantity.int64", value: "2", unit: ":watt" }
    local:
      - name: Runtime
        args:
          - name: sum
            value: { type: ":Quantity.int64", value: "9", unit: ":decibel" }
          - name: difference
            value: { type: ":Quantity.int64", value: "3", unit: ":decibel" }
          - name: half
            value: { type: ":Quantity.int64", value: "3", unit: ":decibel" }
          - name: scaled
            value: { type: ":Quantity.int64", value: "6", unit: ":decibel" }
          - name: ratio
            value: { type: ":Number.int64", value: "2" }
          - name: lower
            value: { type: ":Quantity.int64", value: "3", unit: ":decibel" }
          - name: louder
            value: { type: ":Boolean", value: true }
          - name: magnitude
            value: { type: ":Quantity.int64", value: "3", unit: ":decibel" }
      - name: Rejected
        args:
          - name: plus
            value: { type: ":Nothing" }
          - name: minus
            value: { type: ":Nothing" }
          - name: times
            value: { type: ":Nothing" }
          - name: timesSwapped
            value: { type: ":Nothing" }
          - name: over
            value: { type: ":Nothing" }
          - name: integerOver
            value: { type: ":Nothing" }
          - name: watts
            value: { type: ":Nothing" }
          - name: mixed
            value: { type: ":Nothing" }
      - name: Folded
        args:
          - name: sum
            value: { type: ":Quantity.int64", value: "6", unit: ":decibel" }
          - name: half
            value: { type: ":Quantity.int64", value: "3", unit: ":decibel" }
          - name: ratio
            value: { type: ":Number.int64", value: "3" }
          - name: plus
            value: { type: ":Nothing" }
          - name: times
            value: { type: ":Nothing" }
          - name: timesSwapped
            value: { type: ":Nothing" }
          - name: watts
            value: { type: ":Nothing" }
          - name: meters
            value: { type: ":Quantity.int64", value: "15", unit: ":meter" }
```

---

## Test: area, volume, flux and level names are quantity types

Casts and checks accept the new symbols and ASCII spellings, also with whitespace between their parts, and reject a conflicting unit.

### Case description

```yaml
gesBlock: case
id: area-volume-flux-level-types
sources:
  - name: area-volume-flux-level-types.ges
    program: main
```

### Source code under test

```ges
module main
on Start {
  emit Cast(area: 5 as :Quantity(m²), areaAscii: 5 as :Quantity(m^2), volume: 5 as :Quantity(m³), volumeAscii: 5 as :Quantity(m^3), flux: 5 as :Quantity(W/m²), fluxSpaced: 5 as :Quantity(W / m ^ 2), level: 5 as :Quantity(dB), conflict: 5m² as :Quantity(m³))
  emit Check(area: 2m² is :Quantity(m^2), volume: 2m³ is :Quantity(m²), flux: 14mW/m² is :Quantity(W/m²), level: 6dB is :Quantity(dB))
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| start | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  start:
    local:
      - name: Cast
        args:
          - name: area
            value: { type: ":Quantity.int64", value: "5", unit: ":squareMeter" }
          - name: areaAscii
            value: { type: ":Quantity.int64", value: "5", unit: ":squareMeter" }
          - name: volume
            value: { type: ":Quantity.int64", value: "5", unit: ":cubicMeter" }
          - name: volumeAscii
            value: { type: ":Quantity.int64", value: "5", unit: ":cubicMeter" }
          - name: flux
            value: { type: ":Quantity.int64", value: "5", unit: ":wattPerSquareMeter" }
          - name: fluxSpaced
            value: { type: ":Quantity.int64", value: "5", unit: ":wattPerSquareMeter" }
          - name: level
            value: { type: ":Quantity.int64", value: "5", unit: ":decibel" }
          - name: conflict
            value: { type: ":Nothing" }
      - name: Check
        args:
          - name: area
            value: { type: ":Boolean", value: true }
          - name: volume
            value: { type: ":Boolean", value: false }
          - name: flux
            value: { type: ":Boolean", value: true }
          - name: level
            value: { type: ":Boolean", value: true }
```

---

## Test: area, volume, flux, level and rate format with the base symbol and parse back

Formatting writes the base symbol, so prefixed, tonne and rate spellings appear in their base unit; parsing accepts every new symbol, ASCII and prefixed spelling.

### Case description

```yaml
gesBlock: case
id: area-volume-flux-level-text
sources:
  - name: area-volume-flux-level-text.ges
    program: main
```

### Source code under test

```ges
module main
on Start {
  emit Formatted(area: 2m² as :Text, volume: 0.5m³ as :Text, sensitivity: 14mW/m² as :Text, level: 6dB as :Text, tonne: 2t as :Text, rate: 2/s as :Text)
  emit Parsed(area: '2m^2' as :Number, volume: '0.5m³' as :Number, sensitivity: '14mW/m²' as :Number, level: '-3dB' as :Number, tonne: '2t' as :Number, rate: '2/s' as :Number, square: '3km²' as :Number, roundtrip: ((14mW/m² as :Text) as :Number) = 14mW/m²)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| start | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  start:
    local:
      - name: Formatted
        args:
          - name: area
            value: { type: ":Text", value: "2m²" }
          - name: volume
            value: { type: ":Text", value: "0.5m³" }
          - name: sensitivity
            value: { type: ":Text", value: "0.014W/m²" }
          - name: level
            value: { type: ":Text", value: "6dB" }
          - name: tonne
            value: { type: ":Text", value: "2000kg" }
          - name: rate
            value: { type: ":Text", value: "2Hz" }
      - name: Parsed
        args:
          - name: area
            value: { type: ":Quantity.int64", value: "2", unit: ":squareMeter" }
          - name: volume
            value: { type: ":Quantity.binary64", value: "0.5", unit: ":cubicMeter" }
          - name: sensitivity
            value: { type: ":Quantity.binary64", value: "0.014", unit: ":wattPerSquareMeter" }
          - name: level
            value: { type: ":Quantity.int64", value: "-3", unit: ":decibel" }
          - name: tonne
            value: { type: ":Quantity.int64", value: "2000", unit: ":kilogram" }
          - name: rate
            value: { type: ":Quantity.int64", value: "2", unit: ":hertz" }
          - name: square
            value: { type: ":Quantity.int64", value: "3000000", unit: ":squareMeter" }
          - name: roundtrip
            value: { type: ":Boolean", value: true }
```

---

## Test: same-unit quotients cancel and scalars keep the unit

Dividing equal units gives a unitless number, scalar factors keep any unit, and vectors divided by seconds become velocities.

### Case description

```yaml
gesBlock: case
id: same-and-scalar-units
sources:
  - name: same-and-scalar-units.ges
    program: main
```

### Source code under test

```ges
module main
on Derive(m, s, force, n) {
  emit Runtime(ratio: m / (m * 2), scaled: force * n, halved: force / n, vector: (:Vector(x: 3, y: 4) as :Quantity(m)) / s, vectorScaled: :Vector(x: 1, y: 2) * force)
  emit Folded(ratio: 10m / 2m, scaled: 3N * 2, halved: 6N / 2, frequencyRatio: 4Hz / 2Hz)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| derive | Derive | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  derive:
    input:
      args:
        - name: m
          value: { type: ":Quantity.int64", value: "10", unit: ":meter" }
        - name: s
          value: { type: ":Quantity.int64", value: "2", unit: ":second" }
        - name: force
          value: { type: ":Quantity.int64", value: "3", unit: ":newton" }
        - name: n
          value: { type: ":Number.int64", value: "2" }
    local:
      - name: Runtime
        args:
          - name: ratio
            value: { type: ":Number.binary64", value: "0.5" }
          - name: scaled
            value: { type: ":Quantity.int64", value: "6", unit: ":newton" }
          - name: halved
            value: { type: ":Quantity.binary64", value: "1.5", unit: ":newton" }
          - name: vector
            value: { type: ":Vector", x: "1.5", y: "2", z: "0", unit: ":meterPerSecond" }
          - name: vectorScaled
            value: { type: ":Vector", x: "3", y: "6", z: "0", unit: ":newton" }
      - name: Folded
        args:
          - name: ratio
            value: { type: ":Number.int64", value: "5" }
          - name: scaled
            value: { type: ":Quantity.int64", value: "6", unit: ":newton" }
          - name: halved
            value: { type: ":Quantity.int64", value: "3", unit: ":newton" }
          - name: frequencyRatio
            value: { type: ":Number.int64", value: "2" }
```

---

## Test: rounding keeps the quantity unit

floor, ceil, truncate and every round mode keep the unit of a quantity, identically for constant-folded literals and runtime arguments; rounded percentages stay unitless numbers.

### Case description

```yaml
gesBlock: case
id: rounding-keeps-units
sources:
  - name: rounding-keeps-units.ges
    program: main
```

### Source code under test

```ges
module main
on Derive(force, speed, heading) {
  emit Runtime(floor: floor force, ceil: ceil force, truncate: truncate force, halfEven: round half even speed, halfUp: round half up speed, halfDown: round half down speed, heading: floor heading)
  emit Folded(floor: floor 2.5N, ceil: ceil 2.5N, truncate: truncate -2.5N, halfEven: round half even 2.5m/s, halfUp: round half up 2.5m/s, halfDown: round half down 2.5m/s, heading: floor 43.9°, percentage: floor 150%)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| derive | Derive | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  derive:
    input:
      args:
        - name: force
          value: { type: ":Quantity.binary64", value: "2.5", unit: ":newton" }
        - name: speed
          value: { type: ":Quantity.binary64", value: "2.5", unit: ":meterPerSecond" }
        - name: heading
          value: { type: ":Quantity.binary64", value: "43.9", unit: ":degree" }
    local:
      - name: Runtime
        args:
          - name: floor
            value: { type: ":Quantity.int64", value: "2", unit: ":newton" }
          - name: ceil
            value: { type: ":Quantity.int64", value: "3", unit: ":newton" }
          - name: truncate
            value: { type: ":Quantity.int64", value: "2", unit: ":newton" }
          - name: halfEven
            value: { type: ":Quantity.int64", value: "2", unit: ":meterPerSecond" }
          - name: halfUp
            value: { type: ":Quantity.int64", value: "3", unit: ":meterPerSecond" }
          - name: halfDown
            value: { type: ":Quantity.int64", value: "2", unit: ":meterPerSecond" }
          - name: heading
            value: { type: ":Quantity.int64", value: "43", unit: ":degree" }
      - name: Folded
        args:
          - name: floor
            value: { type: ":Quantity.int64", value: "2", unit: ":newton" }
          - name: ceil
            value: { type: ":Quantity.int64", value: "3", unit: ":newton" }
          - name: truncate
            value: { type: ":Quantity.int64", value: "-2", unit: ":newton" }
          - name: halfEven
            value: { type: ":Quantity.int64", value: "2", unit: ":meterPerSecond" }
          - name: halfUp
            value: { type: ":Quantity.int64", value: "3", unit: ":meterPerSecond" }
          - name: halfDown
            value: { type: ":Quantity.int64", value: "2", unit: ":meterPerSecond" }
          - name: heading
            value: { type: ":Quantity.int64", value: "43", unit: ":degree" }
          - name: percentage
            value: { type: ":Number.int64", value: "1" }
```

---

## Test: unsupported unit combinations give nothing

Products and quotients outside the derivation table, sums of different units, integer division and percentage quotients with units give nothing; comparisons keep their existing results.

### Case description

```yaml
gesBlock: case
id: rejected-combinations
sources:
  - name: rejected-combinations.ges
    program: main
```

### Source code under test

```ges
module main
on Derive(m, s, force, energy, mass) {
  emit Runtime(squaredTime: s * s, mixed: force + energy, massRate: mass / s, integerDivision: m div s, percentage: 50% / s, pace: s / m, less: m < s)
  emit Folded(squaredTime: 2s * 3s, mixed: 2N + 3J, massRate: 6kg / 2s, integerDivision: 10m div 2s, percentage: 50% / 2s, pace: 2s / 3m, less: 2m < 3s)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| derive | Derive | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  derive:
    input:
      args:
        - name: m
          value: { type: ":Quantity.int64", value: "2", unit: ":meter" }
        - name: s
          value: { type: ":Quantity.int64", value: "3", unit: ":second" }
        - name: force
          value: { type: ":Quantity.int64", value: "2", unit: ":newton" }
        - name: energy
          value: { type: ":Quantity.int64", value: "3", unit: ":joule" }
        - name: mass
          value: { type: ":Quantity.int64", value: "6", unit: ":kilogram" }
    local:
      - name: Runtime
        args:
          - name: squaredTime
            value: { type: ":Nothing" }
          - name: mixed
            value: { type: ":Nothing" }
          - name: massRate
            value: { type: ":Nothing" }
          - name: integerDivision
            value: { type: ":Nothing" }
          - name: percentage
            value: { type: ":Nothing" }
          - name: pace
            value: { type: ":Nothing" }
          - name: less
            value: { type: ":Boolean", value: false }
      - name: Folded
        args:
          - name: squaredTime
            value: { type: ":Nothing" }
          - name: mixed
            value: { type: ":Nothing" }
          - name: massRate
            value: { type: ":Nothing" }
          - name: integerDivision
            value: { type: ":Nothing" }
          - name: percentage
            value: { type: ":Nothing" }
          - name: pace
            value: { type: ":Nothing" }
          - name: less
            value: { type: ":Boolean", value: false }
```

---

## Test: quantity casts and checks accept every unit name

Casts apply a unit to unitless input, keep a matching unit, reject a conflicting unit and remove units with none; whitespace in a unit name is ignored and ÷ reads as /; checks match the exact unit.

### Case description

```yaml
gesBlock: case
id: casts-and-checks
sources:
  - name: casts-and-checks.ges
    program: main
```

### Source code under test

```ges
module main
on Start {
  emit Cast(force: 5 as :Quantity(N), speed: 5 as :Quantity(m/s), accel: 5 as :Quantity(m/s²), accelAscii: 5 as :Quantity(m/s^2), turn: 5 as :Quantity(°/s), turnAscii: 5 as :Quantity(deg/s), turnName: 5 as :Quantity(degree/s), spaced: 5 as :Quantity(m / s), divided: 5 as :Quantity(m÷s), kept: 5N as :Quantity(N), conflict: 5N as :Quantity(J), removed: 5m/s as :Quantity(none))
  emit Check(force: 5N is :Quantity(N), other: 5N is :Quantity(J), hertz: 2Hz is :Quantity(Hz), kelvin: 350K is :Quantity(K), unitless: 5 is :Quantity(none))
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| start | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  start:
    local:
      - name: Cast
        args:
          - name: force
            value: { type: ":Quantity.int64", value: "5", unit: ":newton" }
          - name: speed
            value: { type: ":Quantity.int64", value: "5", unit: ":meterPerSecond" }
          - name: accel
            value: { type: ":Quantity.int64", value: "5", unit: ":meterPerSecondSquared" }
          - name: accelAscii
            value: { type: ":Quantity.int64", value: "5", unit: ":meterPerSecondSquared" }
          - name: turn
            value: { type: ":Quantity.int64", value: "5", unit: ":degreePerSecond" }
          - name: turnAscii
            value: { type: ":Quantity.int64", value: "5", unit: ":degreePerSecond" }
          - name: turnName
            value: { type: ":Quantity.int64", value: "5", unit: ":degreePerSecond" }
          - name: spaced
            value: { type: ":Quantity.int64", value: "5", unit: ":meterPerSecond" }
          - name: divided
            value: { type: ":Quantity.int64", value: "5", unit: ":meterPerSecond" }
          - name: kept
            value: { type: ":Quantity.int64", value: "5", unit: ":newton" }
          - name: conflict
            value: { type: ":Nothing" }
          - name: removed
            value: { type: ":Number.int64", value: "5" }
      - name: Check
        args:
          - name: force
            value: { type: ":Boolean", value: true }
          - name: other
            value: { type: ":Boolean", value: false }
          - name: hertz
            value: { type: ":Boolean", value: true }
          - name: kelvin
            value: { type: ":Boolean", value: true }
          - name: unitless
            value: { type: ":Boolean", value: true }
```

---

## Test: quantities format with the base symbol and parse every spelling

Formatting appends the canonical base symbol; parsing accepts symbols, ASCII and prefixed spellings and recovers the exact value and unit, while unknown spellings and NaN give nothing.

### Case description

```yaml
gesBlock: case
id: text-formatting-and-parsing
sources:
  - name: text-formatting-and-parsing.ges
    program: main
```

### Source code under test

```ges
module main
on Start {
  emit Formatted(speed: 3m/s as :Text, accel: 0.1m/s² as :Text, turn: 120deg/s as :Text, energy: 1.5kWh as :Text, delay: 250ms as :Text, frequency: 20Hz as :Text)
  emit Parsed(speed: '3m/s' as :Number, accel: '6m/s^2' as :Number, turn: '120deg/s' as :Number, energy: '1.5kWh' as :Number, delay: '250ms' as :Number, force: '5N' as :Number, nan: 'NaN' as :Number, millimeter: '10mm' as :Number, unknown: '10ft' as :Number, roundtrip: ((0.1m/s² as :Text) as :Number) = 0.1m/s²)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| start | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  start:
    local:
      - name: Formatted
        args:
          - name: speed
            value: { type: ":Text", value: "3m/s" }
          - name: accel
            value: { type: ":Text", value: "0.1m/s²" }
          - name: turn
            value: { type: ":Text", value: "120°/s" }
          - name: energy
            value: { type: ":Text", value: "5400000J" }
          - name: delay
            value: { type: ":Text", value: "0.25s" }
          - name: frequency
            value: { type: ":Text", value: "20Hz" }
      - name: Parsed
        args:
          - name: speed
            value: { type: ":Quantity.int64", value: "3", unit: ":meterPerSecond" }
          - name: accel
            value: { type: ":Quantity.int64", value: "6", unit: ":meterPerSecondSquared" }
          - name: turn
            value: { type: ":Quantity.int64", value: "120", unit: ":degreePerSecond" }
          - name: energy
            value: { type: ":Quantity.int64", value: "5400000", unit: ":joule" }
          - name: delay
            value: { type: ":Quantity.binary64", value: "0.25", unit: ":second" }
          - name: force
            value: { type: ":Quantity.int64", value: "5", unit: ":newton" }
          - name: nan
            value: { type: ":Nothing" }
          - name: millimeter
            value: { type: ":Quantity.binary64", value: "0.01", unit: ":meter" }
          - name: unknown
            value: { type: ":Nothing" }
          - name: roundtrip
            value: { type: ":Boolean", value: true }
```

---

## Test: inline assembly accepts the portable unit names as tags

LoadInteger, LoadFloat and CastUnit take the portable name of every unit as a tag.

### Case description

```yaml
gesBlock: case
id: assembly-unit-tags
sources:
  - name: assembly-unit-tags.ges
    program: main
```

### Source code under test

```ges
module main
on Start {
  let speed be asm { LoadInteger speed, 5, #meterPerSecond }
  let force be asm { LoadFloat force, 2.5, #newton }
  let turn be asm { CastUnit turn, 3, #degreePerSecond }
  emit Assembled(speed: speed, force: force, turn: turn)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| start | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  start:
    local:
      - name: Assembled
        args:
          - name: speed
            value: { type: ":Quantity.int64", value: "5", unit: ":meterPerSecond" }
          - name: force
            value: { type: ":Quantity.binary64", value: "2.5", unit: ":newton" }
          - name: turn
            value: { type: ":Quantity.int64", value: "3", unit: ":degreePerSecond" }
```

---

## Test: prefixed spellings are not quantity type names

Prefixed spellings are literal sugar only, so `:Quantity(km)` is rejected.

### Case description

```yaml
gesBlock: case
id: prefixed-type-name-rejected
kind: compileError
sources:
  - name: prefixed-type-name-rejected.ges
    program: main
```

### Source code under test

```ges
module main
on Start { let value be 5 as :Quantity(km) }
```

### Expectation

```yaml
gesBlock: expect
error: { phase: compile, code: compile.unsupportedConstruct }
```

---

## Test: information units are not quantity type names

Bits, bytes and bit rates are not units, so `:Quantity(bit)` is rejected.

### Case description

```yaml
gesBlock: case
id: reserved-unit-rejected
kind: compileError
sources:
  - name: reserved-unit-rejected.ges
    program: main
```

### Source code under test

```ges
module main
on Start { let value be 5 as :Quantity(bit) }
```

### Expectation

```yaml
gesBlock: expect
error: { phase: compile, code: compile.unsupportedConstruct }
```

---

## Test: unit operands use portable unit names in the assembler dump

The assembler dump writes every unit operand as `unit:` followed by the portable unit name.

### Case description

```yaml
gesBlock: case
id: assembler-unit-names
kind: bytecodeSnapshot
sources:
  - name: assembler-unit-names.ges
    program: main
```

### Source code under test

```ges
module units
on Start(value) {
  let speed be value as :Quantity(m/s)
  emit Done(speed: speed, accel: 2m/s², turn: 120°/s, force: 3N, frequency: 20Hz, temperature: 350K)
}
```

### Expected Game Event Script Assembler

```gesa
// -------------------------------------------------------------------------------
//  Module: units
//  Type: Game Event Script Assembler
//  Format version: 1.0
// -------------------------------------------------------------------------------

.gesb 1
.module "units"
.program-version 0

// -------------------------------------------------------------------------------
.region "Source: assembler-unit-names.ges"

.segment source "assembler-unit-names.ges"

module units
on Start(value) {
  let speed be value as :Quantity(m/s)
  emit Done(speed: speed, accel: 2m/s², turn: 120°/s, force: 3N, frequency: 20Hz, temperature: 350K)
}

.region-end "Source: assembler-unit-names.ges"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Text"

.segment text

T_value:			.text "value"
T_Start:			.text "Start"
T_speed:			.text "speed"
T_accel:			.text "accel"
T_turn:				.text "turn"
T_force:			.text "force"
T_frequency:		.text "frequency"
T_temperature:		.text "temperature"
T_Done:				.text "Done"
T_units:			.text "units"

.region-end "Text"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Lists"

.segment lists

U16_0:				.u16 [0]
U16_1:				.u16 []
U16_2:				.u16 [2, 3, 4, 5, 6, 7]
Args_3:				.registers [r1, r2, r3, r4, r5, r6]

.region-end "Lists"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Bindings"

.segment bind

Handler_Start:		.bind MessageHandler id=0 name=T_Start args=[T_value] entry=Start // "Start(value)"
Outbound_Done:		.bind OutboundMessage id=0 name=T_Done args=[T_speed, T_accel, T_turn, T_force, T_frequency, T_temperature] // "Done(speed, accel, turn, force, frequency, temperature)"

.region-end "Bindings"
// -------------------------------------------------------------------------------

// -------------------------------------------------------------------------------
.region "Code"

.segment code

.source-line "assembler-unit-names.ges" 2 | on Start(value) {
Start:				 // handler Start(value)
					RegisterLocals #6

.source-line "assembler-unit-names.ges" 3 |   let speed be value as :Quantity(m/s)
					CastUnit r1(speed), r0(value), unit:meterPerSecond

.source-line "assembler-unit-names.ges" 4 |   emit Done(speed: speed, accel: 2m/s², turn: 120°/s, force: 3N, frequency: 20Hz, temperature: 350K)
					LoadInteger r2, #2, unit:meterPerSecondSquared
					LoadInteger r3, #120, unit:degreePerSecond
					LoadInteger r4, #3, unit:newton
					LoadInteger r5, #20, unit:hertz
					LoadInteger r6, #350, unit:kelvin
					EmitMessage Outbound_Done, Args_3 // "Done(speed, accel, turn, force, frequency, temperature)"

.source-line "assembler-unit-names.ges" 2 | on Start(value) {
					ReturnVoid

.region-end "Code"
// -------------------------------------------------------------------------------

```
