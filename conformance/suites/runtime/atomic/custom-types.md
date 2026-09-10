---
formatVersion: 1
suiteId: "runtime.atomic.custom-types"
title: "RuntimeAtomicCustomTypes"
categories: [conformance]
---

# RuntimeAtomicCustomTypes

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite isolates custom record types, constructors, fields, and type checks.

---

## Test: create custom record with computed field and casts

This runtime case exercises “create custom record with computed field and casts” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0001
kind: scriptApi
level: atomic
requires:
  core: [external-types]
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "create custom record with computed field and casts.ges"
    program: main
```

### Source code under test

```ges
module atomiccreatecustomrecord
record :Gauge as {
  current: :Number clamped between 0 and maximum,
  maximum: :Number clamped between 0 and infinity,
  percentage: :Percentage computed by
    0% when maximum <= 0,
    otherwise (current / maximum) as :Percentage
}

on Start {
  let gauge be :Gauge(current: 125, maximum: 100)
  let gaugeFromMap be ([current: 125, maximum: 100]) as :Gauge
  let gaugeList be (gauge) as :List
  let gaugeMap be (gauge) as :Map
  emit Done(gauge: gauge, current: gauge.current, percentage: gauge.percentage, gaugeFromMap: gaugeFromMap, fromMapCurrent: gaugeFromMap.current, fromMapPercentage: gaugeFromMap.percentage, fromMapIsGauge: gaugeFromMap is :Gauge, gaugeList: gaugeList, gaugeListLength: gaugeList[:count], gaugeMap: gaugeMap, mapCurrent: gaugeMap.current, mapPercentage: gaugeMap.percentage, recordKeys: gauge[:keys], recordValues: gauge[:values], recordEntries: gauge[:entries], isGauge: gauge is :Gauge, isAim: gauge is :Aim)
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
          - name: "gauge"
            value:
              type: ":Gauge"
              entries:
                - key: "current"
                  value:
                    type: ":Number.int64"
                    value: "100"
                - key: "maximum"
                  value:
                    type: ":Number.int64"
                    value: "100"
                - key: "percentage"
                  value:
                    type: ":Percentage"
                    value: "1"
          - name: "current"
            value:
              type: ":Number.int64"
              value: "100"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "1"
          - name: "gaugeFromMap"
            value:
              type: ":Gauge"
              entries:
                - key: "current"
                  value:
                    type: ":Number.int64"
                    value: "100"
                - key: "maximum"
                  value:
                    type: ":Number.int64"
                    value: "100"
                - key: "percentage"
                  value:
                    type: ":Percentage"
                    value: "1"
          - name: "fromMapCurrent"
            value:
              type: ":Number.int64"
              value: "100"
          - name: "fromMapPercentage"
            value:
              type: ":Percentage"
              value: "1"
          - name: "fromMapIsGauge"
            value:
              type: ":Boolean"
              value: true
          - name: "gaugeList"
            value:
              type: ":List"
              items: []
          - name: "gaugeListLength"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "gaugeMap"
            value:
              type: ":Map"
              entries:
                - key: "current"
                  value:
                    type: ":Number.int64"
                    value: "100"
                - key: "maximum"
                  value:
                    type: ":Number.int64"
                    value: "100"
                - key: "percentage"
                  value:
                    type: ":Percentage"
                    value: "1"
          - name: "mapCurrent"
            value:
              type: ":Number.int64"
              value: "100"
          - name: "mapPercentage"
            value:
              type: ":Percentage"
              value: "1"
          - name: "recordKeys"
            value:
              type: ":List"
              items:
                - type: ":Tag"
                  value: "current"
                - type: ":Tag"
                  value: "maximum"
                - type: ":Tag"
                  value: "percentage"
          - name: "recordValues"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "100"
                - type: ":Number.int64"
                  value: "100"
                - type: ":Percentage"
                  value: "1"
          - name: "recordEntries"
            value:
              type: ":List"
              items:
                - type: ":Map"
                  entries:
                    - key: "key"
                      value:
                        type: ":Tag"
                        value: "current"
                    - key: "value"
                      value:
                        type: ":Number.int64"
                        value: "100"
                - type: ":Map"
                  entries:
                    - key: "key"
                      value:
                        type: ":Tag"
                        value: "maximum"
                    - key: "value"
                      value:
                        type: ":Number.int64"
                        value: "100"
                - type: ":Map"
                  entries:
                    - key: "key"
                      value:
                        type: ":Tag"
                        value: "percentage"
                    - key: "value"
                      value:
                        type: ":Percentage"
                        value: "1"
          - name: "isGauge"
            value:
              type: ":Boolean"
              value: true
          - name: "isAim"
            value:
              type: ":Boolean"
              value: false
```

---

## Test: create record with positional constructor and computed field

This runtime case exercises “create record with positional constructor and computed field” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0002
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "create record with positional constructor and computed field.ges"
    program: main
```

### Source code under test

```ges
module atomiccreaterecordconstructor
record :Stat as {
  _ base: :Number clamped between 0 and cap,
  cap: :Number clamped between 1 and 99,
  bonus: :Number,
  total: :Number computed by base + bonus,
  label: :Text
}

on Start {
  let stat be :Stat(15, cap: 10, bonus: 3, label: 'hp')
  let statMap be (stat) as :Map
  let statList be (stat) as :List
  emit Done(stat: stat, base: stat.base, cap: stat.cap, bonus: stat.bonus, total: stat.total, label: stat.label, map: statMap, mapTotal: statMap.total, listLen: statList[:count], keys: stat[:keys], values: stat[:values], entries: stat[:entries], isStat: stat is :Stat, isGauge: stat is :Gauge, nothing: stat.missing)
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
          - name: "stat"
            value:
              type: ":Stat"
              entries:
                - key: "base"
                  value:
                    type: ":Number.int64"
                    value: "10"
                - key: "bonus"
                  value:
                    type: ":Number.int64"
                    value: "3"
                - key: "cap"
                  value:
                    type: ":Number.int64"
                    value: "10"
                - key: "label"
                  value:
                    type: ":Text"
                    value: "hp"
                - key: "total"
                  value:
                    type: ":Number.int64"
                    value: "13"
          - name: "base"
            value:
              type: ":Number.int64"
              value: "10"
          - name: "cap"
            value:
              type: ":Number.int64"
              value: "10"
          - name: "bonus"
            value:
              type: ":Number.int64"
              value: "3"
          - name: "total"
            value:
              type: ":Number.int64"
              value: "13"
          - name: "label"
            value:
              type: ":Text"
              value: "hp"
          - name: "map"
            value:
              type: ":Map"
              entries:
                - key: "base"
                  value:
                    type: ":Number.int64"
                    value: "10"
                - key: "bonus"
                  value:
                    type: ":Number.int64"
                    value: "3"
                - key: "cap"
                  value:
                    type: ":Number.int64"
                    value: "10"
                - key: "label"
                  value:
                    type: ":Text"
                    value: "hp"
                - key: "total"
                  value:
                    type: ":Number.int64"
                    value: "13"
          - name: "mapTotal"
            value:
              type: ":Number.int64"
              value: "13"
          - name: "listLen"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "keys"
            value:
              type: ":List"
              items:
                - type: ":Tag"
                  value: "base"
                - type: ":Tag"
                  value: "bonus"
                - type: ":Tag"
                  value: "cap"
                - type: ":Tag"
                  value: "label"
                - type: ":Tag"
                  value: "total"
          - name: "values"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "10"
                - type: ":Number.int64"
                  value: "3"
                - type: ":Number.int64"
                  value: "10"
                - type: ":Text"
                  value: "hp"
                - type: ":Number.int64"
                  value: "13"
          - name: "entries"
            value:
              type: ":List"
              items:
                - type: ":Map"
                  entries:
                    - key: "key"
                      value:
                        type: ":Tag"
                        value: "base"
                    - key: "value"
                      value:
                        type: ":Number.int64"
                        value: "10"
                - type: ":Map"
                  entries:
                    - key: "key"
                      value:
                        type: ":Tag"
                        value: "bonus"
                    - key: "value"
                      value:
                        type: ":Number.int64"
                        value: "3"
                - type: ":Map"
                  entries:
                    - key: "key"
                      value:
                        type: ":Tag"
                        value: "cap"
                    - key: "value"
                      value:
                        type: ":Number.int64"
                        value: "10"
                - type: ":Map"
                  entries:
                    - key: "key"
                      value:
                        type: ":Tag"
                        value: "label"
                    - key: "value"
                      value:
                        type: ":Text"
                        value: "hp"
                - type: ":Map"
                  entries:
                    - key: "key"
                      value:
                        type: ":Tag"
                        value: "total"
                    - key: "value"
                      value:
                        type: ":Number.int64"
                        value: "13"
          - name: "isStat"
            value:
              type: ":Boolean"
              value: true
          - name: "isGauge"
            value:
              type: ":Boolean"
              value: false
          - name: "nothing"
            value:
              type: ":Nothing"
```

---

## Test: create external type with computed field and casts

This runtime case exercises “create external type with computed field and casts” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0003
kind: scriptApi
level: atomic
requires:
  core: [external-types]
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "create external type with computed field and casts.ges"
    program: main
```

### Source code under test

```ges
module atomiccreateexternaltype
on Start {
  let aim be :Aim(range: 12m, bearing: 90°, steps: 4m, direction: :Vector(1m, 2m, 3m))
  let aimList be (aim) as :List
  let aimMap be (aim) as :Map
  emit Done(aim: aim, bearing: aim.bearing, checksum: aim.checksum, directionZ: aim.direction.z, aimList: aimList, aimListLength: aimList[:count], aimMap: aimMap, mapChecksum: aimMap.checksum, mapDirectionZ: aimMap.direction.z, aimKeys: aim[:keys], aimValues: aim[:values], aimEntries: aim[:entries], isAim: aim is :Aim, isGauge: aim is :Gauge)
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
          - name: "aim"
            value:
              type: ":Aim"
              entries:
                - key: "bearing"
                  value:
                    type: ":Quantity.int64"
                    unit: ":degree"
                    value: "90"
                - key: "checksum"
                  value:
                    type: ":Number.int64"
                    value: "106"
                - key: "direction"
                  value:
                    type: ":Vector"
                    unit: ":meter"
                    x: "1"
                    y: "2"
                    z: "3"
                - key: "range"
                  value:
                    type: ":Quantity.int64"
                    unit: ":meter"
                    value: "12"
                - key: "steps"
                  value:
                    type: ":Quantity.int64"
                    unit: ":meter"
                    value: "4"
          - name: "bearing"
            value:
              type: ":Quantity.int64"
              unit: ":degree"
              value: "90"
          - name: "checksum"
            value:
              type: ":Number.int64"
              value: "106"
          - name: "directionZ"
            value:
              type: ":Quantity.int64"
              unit: ":meter"
              value: "3"
          - name: "aimList"
            value:
              type: ":List"
              items: []
          - name: "aimListLength"
            value:
              type: ":Number.int64"
              value: "0"
          - name: "aimMap"
            value:
              type: ":Map"
              entries:
                - key: "bearing"
                  value:
                    type: ":Quantity.int64"
                    unit: ":degree"
                    value: "90"
                - key: "checksum"
                  value:
                    type: ":Number.int64"
                    value: "106"
                - key: "direction"
                  value:
                    type: ":Vector"
                    unit: ":meter"
                    x: "1"
                    y: "2"
                    z: "3"
                - key: "range"
                  value:
                    type: ":Quantity.int64"
                    unit: ":meter"
                    value: "12"
                - key: "steps"
                  value:
                    type: ":Quantity.int64"
                    unit: ":meter"
                    value: "4"
          - name: "mapChecksum"
            value:
              type: ":Number.int64"
              value: "106"
          - name: "mapDirectionZ"
            value:
              type: ":Quantity.int64"
              unit: ":meter"
              value: "3"
          - name: "aimKeys"
            value:
              type: ":List"
              items:
                - type: ":Tag"
                  value: "bearing"
                - type: ":Tag"
                  value: "checksum"
                - type: ":Tag"
                  value: "direction"
                - type: ":Tag"
                  value: "range"
                - type: ":Tag"
                  value: "steps"
          - name: "aimValues"
            value:
              type: ":List"
              items:
                - type: ":Quantity.int64"
                  unit: ":degree"
                  value: "90"
                - type: ":Number.int64"
                  value: "106"
                - type: ":Vector"
                  unit: ":meter"
                  x: "1"
                  y: "2"
                  z: "3"
                - type: ":Quantity.int64"
                  unit: ":meter"
                  value: "12"
                - type: ":Quantity.int64"
                  unit: ":meter"
                  value: "4"
          - name: "aimEntries"
            value:
              type: ":List"
              items:
                - type: ":Map"
                  entries:
                    - key: "key"
                      value:
                        type: ":Tag"
                        value: "bearing"
                    - key: "value"
                      value:
                        type: ":Quantity.int64"
                        unit: ":degree"
                        value: "90"
                - type: ":Map"
                  entries:
                    - key: "key"
                      value:
                        type: ":Tag"
                        value: "checksum"
                    - key: "value"
                      value:
                        type: ":Number.int64"
                        value: "106"
                - type: ":Map"
                  entries:
                    - key: "key"
                      value:
                        type: ":Tag"
                        value: "direction"
                    - key: "value"
                      value:
                        type: ":Vector"
                        unit: ":meter"
                        x: "1"
                        y: "2"
                        z: "3"
                - type: ":Map"
                  entries:
                    - key: "key"
                      value:
                        type: ":Tag"
                        value: "range"
                    - key: "value"
                      value:
                        type: ":Quantity.int64"
                        unit: ":meter"
                        value: "12"
                - type: ":Map"
                  entries:
                    - key: "key"
                      value:
                        type: ":Tag"
                        value: "steps"
                    - key: "value"
                      value:
                        type: ":Quantity.int64"
                        unit: ":meter"
                        value: "4"
          - name: "isAim"
            value:
              type: ":Boolean"
              value: true
          - name: "isGauge"
            value:
              type: ":Boolean"
              value: false
```

---

## Test: cast map into custom record constructor

This runtime case exercises “cast map into custom record constructor” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0004
kind: scriptApi
level: atomic
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "cast map into custom record constructor.ges"
    program: main
```

### Source code under test

```ges
module atomiccastmaptorecord
record :Gauge as {
  current: :Number clamped between 0 and maximum,
  maximum: :Number clamped between 0 and infinity,
  percentage: :Percentage computed by
    0% when maximum <= 0,
    otherwise (current / maximum) as :Percentage
}

on Start {
  let raw be [current: 125, maximum: 100]
  let gauge be (raw) as :Gauge
  emit Done(gauge: gauge, current: gauge.current, maximum: gauge.maximum, percentage: gauge.percentage, isGauge: gauge is :Gauge)
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
          - name: "gauge"
            value:
              type: ":Gauge"
              entries:
                - key: "current"
                  value:
                    type: ":Number.int64"
                    value: "100"
                - key: "maximum"
                  value:
                    type: ":Number.int64"
                    value: "100"
                - key: "percentage"
                  value:
                    type: ":Percentage"
                    value: "1"
          - name: "current"
            value:
              type: ":Number.int64"
              value: "100"
          - name: "maximum"
            value:
              type: ":Number.int64"
              value: "100"
          - name: "percentage"
            value:
              type: ":Percentage"
              value: "1"
          - name: "isGauge"
            value:
              type: ":Boolean"
              value: true
```

---

## Test: r23-used-record-cast-consumes-random

This case checks random consumption when the computed Record field is used in a message.

### Case description

```yaml
gesBlock: case
id: r23-used-record-cast-consumes-random
kind: scriptApi
level: atomic
compile:
  binaryRoundTrip: true
random:
  sequence: ["2", "5"]
```

### Source code under test

```ges
record :Sample as { value: :Number, rolled: :Number computed by random 1 to 6 }
on Start(value) {
  let data be [value: value]
  let a be value
  let b be value
  let c be value
  let converted be data as :Sample
  emit Done(rolled: converted.rolled, next: random 1 to 6, a: a, b: b, c: c)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: "value"
          value:
            type: ":Number.int64"
            value: "42"
    local:
      - name: Done
        args:
          - name: "rolled"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "next"
            value:
              type: ":Number.int64"
              value: "5"
          - name: "a"
            value:
              type: ":Number.int64"
              value: "42"
          - name: "b"
            value:
              type: ":Number.int64"
              value: "42"
          - name: "c"
            value:
              type: ":Number.int64"
              value: "42"
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: r23-used-record-cast-keeps-parse-limit

This case checks that a Record conversion whose result is required stops the handler when literal parsing reaches its depth limit.

### Case description

```yaml
gesBlock: case
id: "r23-used-record-cast-keeps-parse-limit"
kind: scriptApi
level: atomic
compile:
  binaryRoundTrip: true
comparison:
  binary64:
    mode: "ulp"
    maxUlps: 0
```

### Source code under test

```ges
record :Parsed as { value: :Text, result: :List computed by parse value }
on Start(value) {
  let data be [value: value]
  let a be value
  let b be value
  let c be value
  emit Before
  emit After(result: (data as :Parsed).result, a: a, b: b, c: c)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[0]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]"
    local:
      - name: "Before"
        args: []
    runtimeLimits:
      include:
        - name: "MaxLiteralDepth"
```

---

## Test: discarded-record-retains-direct-effect

This case checks that a discarded Record cast preserves its computed extension invocation.

### Case description

```yaml
gesBlock: case
id: discarded-record-retains-direct-effect
kind: scriptApi
level: atomic
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
record :Sample as { value: :Number, result: :Number computed by :test.notify(value) }
on Start(value) {
  emit Before
  [value: value] as :Sample
  emit After
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: value
          value: { type: ":Number.int64", value: "42" }
    local:
      - name: Before
      - name: Effect
        args:
          - name: value
            value: { type: ":Number.int64", value: "42" }
      - name: After
```

---

## Test: discarded-record-retains-transitive-effect

This case checks that an extension effect propagates through forward function calls to a discarded Record cast.

### Case description

```yaml
gesBlock: case
id: discarded-record-retains-transitive-effect
kind: scriptApi
level: atomic
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
record :Sample as { value: :Number, result: :Number computed by forward(value) }
function forward(_ value) be finish(value)
function finish(_ value) be :test.notify(value)
on Start(value) {
  emit Before
  [value: value] as :Sample
  emit After
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: value
          value: { type: ":Number.int64", value: "42" }
    local:
      - name: Before
      - name: Effect
        args:
          - name: value
            value: { type: ":Number.int64", value: "42" }
      - name: After
```

---

## Test: discarded-record-retains-nested-constructor-effect

This case checks that an effect propagates through a nested Record constructor.

### Case description

```yaml
gesBlock: case
id: discarded-record-retains-nested-constructor-effect
kind: scriptApi
level: atomic
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
record :Outer as { value: :Number, inner: :Inner computed by :Inner(value: value) }
record :Inner as { value: :Number, result: :Number computed by :test.notify(value) }
on Start(value) {
  emit Before
  [value: value] as :Outer
  emit After
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: value
          value: { type: ":Number.int64", value: "42" }
    local:
      - name: Before
      - name: Effect
        args:
          - name: value
            value: { type: ":Number.int64", value: "42" }
      - name: After
```

---

## Test: discarded-record-retains-nested-cast-effect

This case checks that an effect propagates through a nested Record cast.

### Case description

```yaml
gesBlock: case
id: discarded-record-retains-nested-cast-effect
kind: scriptApi
level: atomic
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
record :Outer as { value: :Number, inner: :Inner computed by [value: value] as :Inner }
record :Inner as { value: :Number, result: :Number computed by :test.notify(value) }
on Start(value) {
  emit Before
  [value: value] as :Outer
  emit After
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: value
          value: { type: ":Number.int64", value: "42" }
    local:
      - name: Before
      - name: Effect
        args:
          - name: value
            value: { type: ":Number.int64", value: "42" }
      - name: After
```

---

## Test: discarded-pure-cast-retains-input-effect

This case checks that an effect in the input expression survives removal of a pure conversion.

### Case description

```yaml
gesBlock: case
id: discarded-pure-cast-retains-input-effect
kind: scriptApi
level: atomic
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
record :Sample as { value: :Number }
on Start(value) {
  emit Before
  (:test.notify(value)) as :Sample
  emit After
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: value
          value: { type: ":Number.int64", value: "42" }
    local:
      - name: Before
      - name: Effect
        args:
          - name: value
            value: { type: ":Number.int64", value: "42" }
      - name: After
```
