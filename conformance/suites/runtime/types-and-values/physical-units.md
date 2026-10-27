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
            value: { type: ":Nothing" }
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
  emit Runtime(area: m * m, mixed: force + energy, massRate: mass / s, integerDivision: m div s, percentage: 50% / s, pace: s / m, less: m < s)
  emit Folded(area: 2m * 3m, mixed: 2N + 3J, massRate: 6kg / 2s, integerDivision: 10m div 2s, percentage: 50% / 2s, pace: 2s / 3m, less: 2m < 3s)
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
          - name: area
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
          - name: area
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

Casts apply a unit to unitless input, keep a matching unit, reject a conflicting unit and remove units with none; checks match the exact unit.

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
  emit Cast(force: 5 as :Quantity(N), speed: 5 as :Quantity(m/s), accel: 5 as :Quantity(m/s²), accelAscii: 5 as :Quantity(m/s^2), turn: 5 as :Quantity(°/s), turnAscii: 5 as :Quantity(deg/s), turnName: 5 as :Quantity(degree/s), kept: 5N as :Quantity(N), conflict: 5N as :Quantity(J), removed: 5m/s as :Quantity(none))
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

## Test: reserved units are not quantity type names

Bits, bytes and bit rates are reserved and unimplemented, so `:Quantity(bit)` is rejected.

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
