---
formatVersion: 1
suiteId: "performance.maps"
title: "Map Storage"
categories: [conformance]
---

# Map Storage

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite measures typical Map and Record workloads: Records with a handful of fields, lookups in five- and 32-entry Maps, and iteration over values, keys with lookup, and entries. It provides the evidence for the shared map storage layout.

---

## Test: record creation and field access

This benchmark creates a six-field Record and reads every field and checks its observable result and configured performance limits.

### Case description

```yaml
gesBlock: case
id: record-fields
kind: performance
level: scenario
performance:
  iterations: 1000
  warmupIterations: 100
  compileWarmupIterations: 3
sources:
  - name: "record-fields.ges"
    program: main
```

### Source code under test

```ges
module performancemaprecords

record :Unit as {
  name: :Text,
  health: :Number,
  armor: :Number,
  speed: :Number,
  level: :Number,
  team: :Tag
}

on Start {
  let unit be :Unit(name: 'scout', health: 80, armor: 12, speed: 7, level: 3, team: #blue)
  let power be unit.health + unit.armor * 2 + unit.speed * unit.level
  emit Done(name: unit.name, power: power, team: unit.team)
}

```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args: []
    local:
      - name: "Done"
        args:
          - name: "name"
            value:
              type: ":Text"
              value: "scout"
          - name: "power"
            value:
              type: ":Number.int64"
              value: "125"
          - name: "team"
            value:
              type: ":Tag"
              value: "blue"
performance:
  profiles:
    csharp-dotnet-release-macos-arm64:
      metrics:
        ast-build.elapsed:
          reference: 0.0745
          toleranceAbsolute: 0.05
          unit: ms
        binary-build.elapsed:
          reference: 0.2052
          toleranceAbsolute: 0.0513
          unit: ms
        program-load.elapsed:
          reference: 0.0629
          toleranceAbsolute: 0.05
          unit: ms
        compile.elapsed:
          reference: 0.3438
          toleranceAbsolute: 0.08595
          unit: ms
        run.elapsed:
          reference: 3.0965
          toleranceAbsolute: 0.774125
          unit: ms
        ast-build.allocated:
          reference: 24.859375
          maximum: 26.1023
          unit: KiB
        binary-build.allocated:
          reference: 67.4296875
          maximum: 70.8012
          unit: KiB
        program-load.allocated:
          reference: 15.34375
          maximum: 16.1109
          unit: KiB
        compile.allocated:
          reference: 107.6328125
          maximum: 113.0145
          unit: KiB
        run.allocated:
          reference: 492.1875
          maximum: 516.7969
          unit: KiB
        run.per-invoke-elapsed:
          reference: 0.0030965
          toleranceAbsolute: 0.000774125
          unit: ms
        run.per-invoke-allocated:
          reference: 0.4921875
          maximum: 0.5168
          unit: KiB
    swift-6.4-release-macos26-arm64-m3max:
      metrics:
        compile.allocated:
          reference: 241070
          maximum: 253124
          unit: B
        compile.elapsed:
          reference: 0.298667
          toleranceAbsolute: 0.0746668
          unit: ms
        program-load.allocated:
          reference: 17668
          maximum: 18552
          unit: B
        program-load.elapsed:
          reference: 0.025542
          toleranceAbsolute: 0.05
          unit: ms
        run.allocated:
          reference: 512000
          maximum: 537600
          unit: B
        run.allocations:
          reference: 5000
          maximum: 5250
          unit: count
        run.elapsed:
          reference: 1.68829
          toleranceAbsolute: 0.422073
          unit: ms
        run.per-invoke-allocated:
          reference: 512
          maximum: 538
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.00168829
          toleranceAbsolute: 0.000422073
          unit: ms/iteration
```

---

## Test: small and large map lookup

This benchmark reads constant keys from a five-entry and a 32-entry Map inside a loop and checks its observable result and configured performance limits.

### Case description

```yaml
gesBlock: case
id: map-lookup
kind: performance
level: scenario
performance:
  iterations: 1000
  warmupIterations: 100
  compileWarmupIterations: 3
sources:
  - name: "map-lookup.ges"
    program: main
```

### Source code under test

```ges
module performancemaplookup

on Start {
  let small be [health: 80, armor: 12, speed: 7, level: 3, range: 5]
  let large be [a1: 1, a2: 2, a3: 3, a4: 4, a5: 5, a6: 6, a7: 7, a8: 8, a9: 9, a10: 10, a11: 11, a12: 12, a13: 13, a14: 14, a15: 15, a16: 16, a17: 17, a18: 18, a19: 19, a20: 20, a21: 21, a22: 22, a23: 23, a24: 24, a25: 25, a26: 26, a27: 27, a28: 28, a29: 29, a30: 30, a31: 31, a32: 32]
  let smallTotal be :List[:select i from 1 to 20 => small['health'] + small['speed'] * small['level']][:sum value => value]
  let largeTotal be :List[:select i from 1 to 20 => large['a3'] + large['a17'] + large['a32']][:sum value => value]
  emit Done(small: smallTotal, large: largeTotal)
}

```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args: []
    local:
      - name: "Done"
        args:
          - name: "small"
            value:
              type: ":Number.int64"
              value: "2020"
          - name: "large"
            value:
              type: ":Number.int64"
              value: "1040"
performance:
  profiles:
    csharp-dotnet-release-macos-arm64:
      metrics:
        ast-build.elapsed:
          reference: 0.1715
          toleranceAbsolute: 0.05
          unit: ms
        binary-build.elapsed:
          reference: 0.4215
          toleranceAbsolute: 0.105375
          unit: ms
        program-load.elapsed:
          reference: 0.1587
          toleranceAbsolute: 0.05
          unit: ms
        compile.elapsed:
          reference: 0.7451
          toleranceAbsolute: 0.186275
          unit: ms
        run.elapsed:
          reference: 35.8235
          toleranceAbsolute: 8.95588
          unit: ms
        ast-build.allocated:
          reference: 54.890625
          maximum: 57.6352
          unit: KiB
        binary-build.allocated:
          reference: 131.0078125
          maximum: 137.5582
          unit: KiB
        program-load.allocated:
          reference: 24.2421875
          maximum: 25.4543
          unit: KiB
        compile.allocated:
          reference: 210.140625
          maximum: 220.6477
          unit: KiB
        run.allocated:
          reference: 6929.6875
          maximum: 7276.1719
          unit: KiB
        run.per-invoke-elapsed:
          reference: 0.0358235
          toleranceAbsolute: 0.00895588
          unit: ms
        run.per-invoke-allocated:
          reference: 6.9296875
          maximum: 7.2762
          unit: KiB
    swift-6.4-release-macos26-arm64-m3max:
      metrics:
        compile.allocated:
          reference: 440689
          maximum: 462724
          unit: B
        compile.elapsed:
          reference: 0.495167
          toleranceAbsolute: 0.123792
          unit: ms
        program-load.allocated:
          reference: 23123
          maximum: 24280
          unit: B
        program-load.elapsed:
          reference: 0.033458
          toleranceAbsolute: 0.05
          unit: ms
        run.allocated:
          reference: 5600000
          maximum: 5880000
          unit: B
        run.allocations:
          reference: 28000
          maximum: 29400
          unit: count
        run.elapsed:
          reference: 12.4307
          toleranceAbsolute: 3.10767
          unit: ms
        run.per-invoke-allocated:
          reference: 5600
          maximum: 5880
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.0124307
          toleranceAbsolute: 0.00310767
          unit: ms/iteration
```

---

## Test: map value iteration

This benchmark aggregates the values of a five-entry and a 32-entry Map and checks its observable result and configured performance limits.

### Case description

```yaml
gesBlock: case
id: map-values
kind: performance
level: scenario
performance:
  iterations: 1000
  warmupIterations: 100
  compileWarmupIterations: 3
sources:
  - name: "map-values.ges"
    program: main
```

### Source code under test

```ges
module performancemapvalues

on Start {
  let small be [health: 80, armor: 12, speed: 7, level: 3, range: 5]
  let large be [a1: 1, a2: 2, a3: 3, a4: 4, a5: 5, a6: 6, a7: 7, a8: 8, a9: 9, a10: 10, a11: 11, a12: 12, a13: 13, a14: 14, a15: 15, a16: 16, a17: 17, a18: 18, a19: 19, a20: 20, a21: 21, a22: 22, a23: 23, a24: 24, a25: 25, a26: 26, a27: 27, a28: 28, a29: 29, a30: 30, a31: 31, a32: 32]
  let smallTotal be small[:sum value => value]
  let largeTotal be large[:sum value => value]
  let largeHigh be large[:count value where value > 16]
  emit Done(small: smallTotal, large: largeTotal, high: largeHigh)
}

```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args: []
    local:
      - name: "Done"
        args:
          - name: "small"
            value:
              type: ":Number.int64"
              value: "107"
          - name: "large"
            value:
              type: ":Number.int64"
              value: "528"
          - name: "high"
            value:
              type: ":Number.int64"
              value: "16"
performance:
  profiles:
    csharp-dotnet-release-macos-arm64:
      metrics:
        ast-build.elapsed:
          reference: 0.1507
          toleranceAbsolute: 0.05
          unit: ms
        binary-build.elapsed:
          reference: 0.3539
          toleranceAbsolute: 0.088475
          unit: ms
        program-load.elapsed:
          reference: 0.1122
          toleranceAbsolute: 0.05
          unit: ms
        compile.elapsed:
          reference: 0.6138
          toleranceAbsolute: 0.15345
          unit: ms
        run.elapsed:
          reference: 14.0895
          toleranceAbsolute: 3.52237
          unit: ms
        ast-build.allocated:
          reference: 49.5703125
          maximum: 52.0488
          unit: KiB
        binary-build.allocated:
          reference: 111.8671875
          maximum: 117.4605
          unit: KiB
        program-load.allocated:
          reference: 23.4140625
          maximum: 24.5848
          unit: KiB
        compile.allocated:
          reference: 184.8515625
          maximum: 194.0941
          unit: KiB
        run.allocated:
          reference: 2804.6875
          maximum: 2944.9219
          unit: KiB
        run.per-invoke-elapsed:
          reference: 0.0140895
          toleranceAbsolute: 0.00352237
          unit: ms
        run.per-invoke-allocated:
          reference: 2.8046875
          maximum: 2.9449
          unit: KiB
    swift-6.4-release-macos26-arm64-m3max:
      metrics:
        compile.allocated:
          reference: 359663
          maximum: 377647
          unit: B
        compile.elapsed:
          reference: 0.411625
          toleranceAbsolute: 0.102906
          unit: ms
        program-load.allocated:
          reference: 22512
          maximum: 23638
          unit: B
        program-load.elapsed:
          reference: 0.02825
          toleranceAbsolute: 0.05
          unit: ms
        run.allocated:
          reference: 1888000
          maximum: 1982400
          unit: B
        run.allocations:
          reference: 9000
          maximum: 9450
          unit: count
        run.elapsed:
          reference: 3.21
          toleranceAbsolute: 0.8025
          unit: ms
        run.per-invoke-allocated:
          reference: 1888
          maximum: 1983
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.00321
          toleranceAbsolute: 0.0008025
          unit: ms/iteration
```

---

## Test: map key iteration with lookup

This benchmark iterates the keys of a five-entry and a 32-entry Map and looks each value up and checks its observable result and configured performance limits.

### Case description

```yaml
gesBlock: case
id: map-keys
kind: performance
level: scenario
performance:
  iterations: 1000
  warmupIterations: 100
  compileWarmupIterations: 3
sources:
  - name: "map-keys.ges"
    program: main
```

### Source code under test

```ges
module performancemapkeys

on Start {
  let small be [health: 80, armor: 12, speed: 7, level: 3, range: 5]
  let large be [a1: 1, a2: 2, a3: 3, a4: 4, a5: 5, a6: 6, a7: 7, a8: 8, a9: 9, a10: 10, a11: 11, a12: 12, a13: 13, a14: 14, a15: 15, a16: 16, a17: 17, a18: 18, a19: 19, a20: 20, a21: 21, a22: 22, a23: 23, a24: 24, a25: 25, a26: 26, a27: 27, a28: 28, a29: 29, a30: 30, a31: 31, a32: 32]
  for key in small[:keys] {
    emit Item(key: key, value: small[key])
  }
  let largeTotal be large[:keys][:sum key => large[key]]
  emit Done(large: largeTotal)
}

```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args: []
    local:
      - name: "Item"
        args:
          - name: "key"
            value:
              type: ":Text"
              value: "armor"
          - name: "value"
            value:
              type: ":Number.int64"
              value: "12"
      - name: "Item"
        args:
          - name: "key"
            value:
              type: ":Text"
              value: "health"
          - name: "value"
            value:
              type: ":Number.int64"
              value: "80"
      - name: "Item"
        args:
          - name: "key"
            value:
              type: ":Text"
              value: "level"
          - name: "value"
            value:
              type: ":Number.int64"
              value: "3"
      - name: "Item"
        args:
          - name: "key"
            value:
              type: ":Text"
              value: "range"
          - name: "value"
            value:
              type: ":Number.int64"
              value: "5"
      - name: "Item"
        args:
          - name: "key"
            value:
              type: ":Text"
              value: "speed"
          - name: "value"
            value:
              type: ":Number.int64"
              value: "7"
      - name: "Done"
        args:
          - name: "large"
            value:
              type: ":Number.int64"
              value: "528"
performance:
  profiles:
    csharp-dotnet-release-macos-arm64:
      metrics:
        ast-build.elapsed:
          reference: 0.1447
          toleranceAbsolute: 0.05
          unit: ms
        binary-build.elapsed:
          reference: 0.3025
          toleranceAbsolute: 0.075625
          unit: ms
        program-load.elapsed:
          reference: 0.0915
          toleranceAbsolute: 0.05
          unit: ms
        compile.elapsed:
          reference: 0.5402
          toleranceAbsolute: 0.13505
          unit: ms
        run.elapsed:
          reference: 17.9367
          toleranceAbsolute: 4.48417
          unit: ms
        ast-build.allocated:
          reference: 50.59375
          maximum: 53.1234
          unit: KiB
        binary-build.allocated:
          reference: 99.265625
          maximum: 104.2289
          unit: KiB
        program-load.allocated:
          reference: 23.0625
          maximum: 24.2156
          unit: KiB
        compile.allocated:
          reference: 172.921875
          maximum: 181.568
          unit: KiB
        run.allocated:
          reference: 3531.25
          maximum: 3707.8125
          unit: KiB
        run.per-invoke-elapsed:
          reference: 0.0179367
          toleranceAbsolute: 0.00448417
          unit: ms
        run.per-invoke-allocated:
          reference: 3.53125
          maximum: 3.7078
          unit: KiB
    swift-6.4-release-macos26-arm64-m3max:
      metrics:
        compile.allocated:
          reference: 346579
          maximum: 363908
          unit: B
        compile.elapsed:
          reference: 0.377792
          toleranceAbsolute: 0.094448
          unit: ms
        program-load.allocated:
          reference: 22415
          maximum: 23536
          unit: B
        program-load.elapsed:
          reference: 0.027
          toleranceAbsolute: 0.05
          unit: ms
        run.allocated:
          reference: 5040000
          maximum: 5292000
          unit: B
        run.allocations:
          reference: 59000
          maximum: 61950
          unit: count
        run.elapsed:
          reference: 10.804
          toleranceAbsolute: 2.701
          unit: ms
        run.per-invoke-allocated:
          reference: 5040
          maximum: 5292
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.010804
          toleranceAbsolute: 0.002701
          unit: ms/iteration
```

---

## Test: map entry iteration

This benchmark iterates the key-value entries of a five-entry and a 32-entry Map and checks its observable result and configured performance limits.

### Case description

```yaml
gesBlock: case
id: map-entries
kind: performance
level: scenario
performance:
  iterations: 1000
  warmupIterations: 100
  compileWarmupIterations: 3
sources:
  - name: "map-entries.ges"
    program: main
```

### Source code under test

```ges
module performancemapentries

on Start {
  let small be [health: 80, armor: 12, speed: 7, level: 3, range: 5]
  let large be [a1: 1, a2: 2, a3: 3, a4: 4, a5: 5, a6: 6, a7: 7, a8: 8, a9: 9, a10: 10, a11: 11, a12: 12, a13: 13, a14: 14, a15: 15, a16: 16, a17: 17, a18: 18, a19: 19, a20: 20, a21: 21, a22: 22, a23: 23, a24: 24, a25: 25, a26: 26, a27: 27, a28: 28, a29: 29, a30: 30, a31: 31, a32: 32]
  let smallKeys be small[:entries][:select entry => entry.key]
  let largeTotal be large[:entries][:sum entry => entry.value]
  emit Done(keys: smallKeys, large: largeTotal)
}

```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args: []
    local:
      - name: "Done"
        args:
          - name: "keys"
            value:
              type: ":List"
              items:
                - type: ":Text"
                  value: "armor"
                - type: ":Text"
                  value: "health"
                - type: ":Text"
                  value: "level"
                - type: ":Text"
                  value: "range"
                - type: ":Text"
                  value: "speed"
          - name: "large"
            value:
              type: ":Number.int64"
              value: "528"
performance:
  profiles:
    csharp-dotnet-release-macos-arm64:
      metrics:
        ast-build.elapsed:
          reference: 0.138
          toleranceAbsolute: 0.05
          unit: ms
        binary-build.elapsed:
          reference: 0.3732
          toleranceAbsolute: 0.0933
          unit: ms
        program-load.elapsed:
          reference: 0.1262
          toleranceAbsolute: 0.05
          unit: ms
        compile.elapsed:
          reference: 0.6376
          toleranceAbsolute: 0.1594
          unit: ms
        run.elapsed:
          reference: 33.4244
          toleranceAbsolute: 8.3561
          unit: ms
        ast-build.allocated:
          reference: 48.4140625
          maximum: 50.8348
          unit: KiB
        binary-build.allocated:
          reference: 111.6640625
          maximum: 117.2473
          unit: KiB
        program-load.allocated:
          reference: 23.328125
          maximum: 24.4945
          unit: KiB
        compile.allocated:
          reference: 183.40625
          maximum: 192.5766
          unit: KiB
        run.allocated:
          reference: 12593.75
          maximum: 13223.4375
          unit: KiB
        run.per-invoke-elapsed:
          reference: 0.0334244
          toleranceAbsolute: 0.0083561
          unit: ms
        run.per-invoke-allocated:
          reference: 12.59375
          maximum: 13.2234
          unit: KiB
    swift-6.4-release-macos26-arm64-m3max:
      metrics:
        compile.allocated:
          reference: 351302
          maximum: 368868
          unit: B
        compile.elapsed:
          reference: 0.402792
          toleranceAbsolute: 0.100698
          unit: ms
        program-load.allocated:
          reference: 22606
          maximum: 23737
          unit: B
        program-load.elapsed:
          reference: 0.030167
          toleranceAbsolute: 0.05
          unit: ms
        run.allocated:
          reference: 8922000
          maximum: 9368100
          unit: B
        run.allocations:
          reference: 125000
          maximum: 131250
          unit: count
        run.elapsed:
          reference: 19.322
          toleranceAbsolute: 4.83051
          unit: ms
        run.per-invoke-allocated:
          reference: 8922
          maximum: 9369
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.019322
          toleranceAbsolute: 0.00483051
          unit: ms/iteration
```
