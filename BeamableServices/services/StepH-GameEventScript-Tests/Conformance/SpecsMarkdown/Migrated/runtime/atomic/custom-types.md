---
formatVersion: 1
suiteId: "runtime.atomic.custom-types"
title: "RuntimeAtomicCustomTypes"
categories: [conformance]
tags: [migrated-json-v1]
---

# RuntimeAtomicCustomTypes

Mechanically migrated from the former JSON conformance corpus.

## Test: create custom record with computed field and casts

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

```ges
module AtomicCreateCustomRecord
record :gauge as {
  current: :number clamped between 0 and maximum,
  maximum: :number clamped between 0 and infinity,
  percentage: :percentage computed by
    0% when maximum <= 0,
    otherwise (current / maximum) as :percentage
}

on Start {
  let gauge be :gauge(current: 125, maximum: 100)
  let gaugeFromMap as :gauge be [current: 125, maximum: 100]
  let gaugeList as :list be gauge
  let gaugeMap as :map be gauge
  emit Done(gauge: gauge, current: gauge.current, percentage: gauge.percentage, gaugeFromMap: gaugeFromMap, fromMapCurrent: gaugeFromMap.current, fromMapPercentage: gaugeFromMap.percentage, fromMapIsGauge: gaugeFromMap is :gauge, gaugeList: gaugeList, gaugeListLength: gaugeList[:count], gaugeMap: gaugeMap, mapCurrent: gaugeMap.current, mapPercentage: gaugeMap.percentage, recordKeys: gauge[:keys], recordValues: gauge[:values], recordEntries: gauge[:entries], isGauge: gauge is :gauge, isAim: gauge is :aim)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

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
              type: ":gauge"
              entries:
                - key: "current"
                  value:
                    type: ":integer"
                    value: "100"
                - key: "maximum"
                  value:
                    type: ":integer"
                    value: "100"
                - key: "percentage"
                  value:
                    type: ":percentage"
                    value: "0.01"
          - name: "current"
            value:
              type: ":integer"
              value: "100"
          - name: "percentage"
            value:
              type: ":percentage"
              value: "0.01"
          - name: "gaugeFromMap"
            value:
              type: ":gauge"
              entries:
                - key: "current"
                  value:
                    type: ":integer"
                    value: "100"
                - key: "maximum"
                  value:
                    type: ":integer"
                    value: "100"
                - key: "percentage"
                  value:
                    type: ":percentage"
                    value: "0.01"
          - name: "fromMapCurrent"
            value:
              type: ":integer"
              value: "100"
          - name: "fromMapPercentage"
            value:
              type: ":percentage"
              value: "0.01"
          - name: "fromMapIsGauge"
            value:
              type: ":boolean"
              value: true
          - name: "gaugeList"
            value:
              type: ":list"
              items: []
          - name: "gaugeListLength"
            value:
              type: ":integer"
              value: "0"
          - name: "gaugeMap"
            value:
              type: ":map"
              entries:
                - key: "current"
                  value:
                    type: ":integer"
                    value: "100"
                - key: "maximum"
                  value:
                    type: ":integer"
                    value: "100"
                - key: "percentage"
                  value:
                    type: ":percentage"
                    value: "0.01"
          - name: "mapCurrent"
            value:
              type: ":integer"
              value: "100"
          - name: "mapPercentage"
            value:
              type: ":percentage"
              value: "0.01"
          - name: "recordKeys"
            value:
              type: ":list"
              items:
                - type: ":tag"
                  value: "current"
                - type: ":tag"
                  value: "maximum"
                - type: ":tag"
                  value: "percentage"
          - name: "recordValues"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "100"
                - type: ":integer"
                  value: "100"
                - type: ":percentage"
                  value: "0.01"
          - name: "recordEntries"
            value:
              type: ":list"
              items:
                - type: ":map"
                  entries:
                    - key: "key"
                      value:
                        type: ":tag"
                        value: "current"
                    - key: "value"
                      value:
                        type: ":integer"
                        value: "100"
                - type: ":map"
                  entries:
                    - key: "key"
                      value:
                        type: ":tag"
                        value: "maximum"
                    - key: "value"
                      value:
                        type: ":integer"
                        value: "100"
                - type: ":map"
                  entries:
                    - key: "key"
                      value:
                        type: ":tag"
                        value: "percentage"
                    - key: "value"
                      value:
                        type: ":percentage"
                        value: "0.01"
          - name: "isGauge"
            value:
              type: ":boolean"
              value: true
          - name: "isAim"
            value:
              type: ":boolean"
              value: false
```

## Test: create record with positional constructor and computed field

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

```ges
module AtomicCreateRecordConstructor
record :stat as {
  _ base: :number clamped between 0 and cap,
  cap: :number clamped between 1 and 99,
  bonus: :number,
  total: :number computed by base + bonus,
  label: :text
}

on Start {
  let stat be :stat(15, cap: 10, bonus: 3, label: 'hp')
  let statMap as :map be stat
  let statList as :list be stat
  emit Done(stat: stat, base: stat.base, cap: stat.cap, bonus: stat.bonus, total: stat.total, label: stat.label, map: statMap, mapTotal: statMap.total, listLen: statList[:count], keys: stat[:keys], values: stat[:values], entries: stat[:entries], isStat: stat is :stat, isGauge: stat is :gauge, nothing: stat.missing)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

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
              type: ":stat"
              entries:
                - key: "base"
                  value:
                    type: ":integer"
                    value: "10"
                - key: "bonus"
                  value:
                    type: ":integer"
                    value: "3"
                - key: "cap"
                  value:
                    type: ":integer"
                    value: "10"
                - key: "label"
                  value:
                    type: ":text"
                    value: "hp"
                - key: "total"
                  value:
                    type: ":integer"
                    value: "13"
          - name: "base"
            value:
              type: ":integer"
              value: "10"
          - name: "cap"
            value:
              type: ":integer"
              value: "10"
          - name: "bonus"
            value:
              type: ":integer"
              value: "3"
          - name: "total"
            value:
              type: ":integer"
              value: "13"
          - name: "label"
            value:
              type: ":text"
              value: "hp"
          - name: "map"
            value:
              type: ":map"
              entries:
                - key: "base"
                  value:
                    type: ":integer"
                    value: "10"
                - key: "bonus"
                  value:
                    type: ":integer"
                    value: "3"
                - key: "cap"
                  value:
                    type: ":integer"
                    value: "10"
                - key: "label"
                  value:
                    type: ":text"
                    value: "hp"
                - key: "total"
                  value:
                    type: ":integer"
                    value: "13"
          - name: "mapTotal"
            value:
              type: ":integer"
              value: "13"
          - name: "listLen"
            value:
              type: ":integer"
              value: "0"
          - name: "keys"
            value:
              type: ":list"
              items:
                - type: ":tag"
                  value: "base"
                - type: ":tag"
                  value: "bonus"
                - type: ":tag"
                  value: "cap"
                - type: ":tag"
                  value: "label"
                - type: ":tag"
                  value: "total"
          - name: "values"
            value:
              type: ":list"
              items:
                - type: ":integer"
                  value: "10"
                - type: ":integer"
                  value: "3"
                - type: ":integer"
                  value: "10"
                - type: ":text"
                  value: "hp"
                - type: ":integer"
                  value: "13"
          - name: "entries"
            value:
              type: ":list"
              items:
                - type: ":map"
                  entries:
                    - key: "key"
                      value:
                        type: ":tag"
                        value: "base"
                    - key: "value"
                      value:
                        type: ":integer"
                        value: "10"
                - type: ":map"
                  entries:
                    - key: "key"
                      value:
                        type: ":tag"
                        value: "bonus"
                    - key: "value"
                      value:
                        type: ":integer"
                        value: "3"
                - type: ":map"
                  entries:
                    - key: "key"
                      value:
                        type: ":tag"
                        value: "cap"
                    - key: "value"
                      value:
                        type: ":integer"
                        value: "10"
                - type: ":map"
                  entries:
                    - key: "key"
                      value:
                        type: ":tag"
                        value: "label"
                    - key: "value"
                      value:
                        type: ":text"
                        value: "hp"
                - type: ":map"
                  entries:
                    - key: "key"
                      value:
                        type: ":tag"
                        value: "total"
                    - key: "value"
                      value:
                        type: ":integer"
                        value: "13"
          - name: "isStat"
            value:
              type: ":boolean"
              value: true
          - name: "isGauge"
            value:
              type: ":boolean"
              value: false
          - name: "nothing"
            value:
              type: ":nothing"
```

## Test: create external type with computed field and casts

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

```ges
module AtomicCreateExternalType
on Start {
  let aim be :aim(range: 12m, bearing: 90°, steps: 4m, direction: :vector(1m, 2m, 3m))
  let aimList as :list be aim
  let aimMap as :map be aim
  emit Done(aim: aim, bearing: aim.bearing, checksum: aim.checksum, directionZ: aim.direction.z, aimList: aimList, aimListLength: aimList[:count], aimMap: aimMap, mapChecksum: aimMap.checksum, mapDirectionZ: aimMap.direction.z, aimKeys: aim[:keys], aimValues: aim[:values], aimEntries: aim[:entries], isAim: aim is :aim, isGauge: aim is :gauge)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

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
              type: ":aim"
              entries:
                - key: "bearing"
                  value:
                    type: ":float"
                    unit: ":degree"
                    value: "90"
                - key: "checksum"
                  value:
                    type: ":integer"
                    value: "106"
                - key: "direction"
                  value:
                    type: ":vector"
                    unit: ":meter"
                    x: "1"
                    y: "2"
                    z: "3"
                - key: "range"
                  value:
                    type: ":float"
                    unit: ":meter"
                    value: "12"
                - key: "steps"
                  value:
                    type: ":integer"
                    unit: ":meter"
                    value: "4"
          - name: "bearing"
            value:
              type: ":float"
              unit: ":degree"
              value: "90"
          - name: "checksum"
            value:
              type: ":integer"
              value: "106"
          - name: "directionZ"
            value:
              type: ":float"
              unit: ":meter"
              value: "3"
          - name: "aimList"
            value:
              type: ":list"
              items: []
          - name: "aimListLength"
            value:
              type: ":integer"
              value: "0"
          - name: "aimMap"
            value:
              type: ":map"
              entries:
                - key: "bearing"
                  value:
                    type: ":float"
                    unit: ":degree"
                    value: "90"
                - key: "checksum"
                  value:
                    type: ":integer"
                    value: "106"
                - key: "direction"
                  value:
                    type: ":vector"
                    unit: ":meter"
                    x: "1"
                    y: "2"
                    z: "3"
                - key: "range"
                  value:
                    type: ":float"
                    unit: ":meter"
                    value: "12"
                - key: "steps"
                  value:
                    type: ":integer"
                    unit: ":meter"
                    value: "4"
          - name: "mapChecksum"
            value:
              type: ":integer"
              value: "106"
          - name: "mapDirectionZ"
            value:
              type: ":float"
              unit: ":meter"
              value: "3"
          - name: "aimKeys"
            value:
              type: ":list"
              items:
                - type: ":tag"
                  value: "bearing"
                - type: ":tag"
                  value: "checksum"
                - type: ":tag"
                  value: "direction"
                - type: ":tag"
                  value: "range"
                - type: ":tag"
                  value: "steps"
          - name: "aimValues"
            value:
              type: ":list"
              items:
                - type: ":float"
                  unit: ":degree"
                  value: "90"
                - type: ":integer"
                  value: "106"
                - type: ":vector"
                  unit: ":meter"
                  x: "1"
                  y: "2"
                  z: "3"
                - type: ":float"
                  unit: ":meter"
                  value: "12"
                - type: ":integer"
                  unit: ":meter"
                  value: "4"
          - name: "aimEntries"
            value:
              type: ":list"
              items:
                - type: ":map"
                  entries:
                    - key: "key"
                      value:
                        type: ":tag"
                        value: "bearing"
                    - key: "value"
                      value:
                        type: ":float"
                        unit: ":degree"
                        value: "90"
                - type: ":map"
                  entries:
                    - key: "key"
                      value:
                        type: ":tag"
                        value: "checksum"
                    - key: "value"
                      value:
                        type: ":integer"
                        value: "106"
                - type: ":map"
                  entries:
                    - key: "key"
                      value:
                        type: ":tag"
                        value: "direction"
                    - key: "value"
                      value:
                        type: ":vector"
                        unit: ":meter"
                        x: "1"
                        y: "2"
                        z: "3"
                - type: ":map"
                  entries:
                    - key: "key"
                      value:
                        type: ":tag"
                        value: "range"
                    - key: "value"
                      value:
                        type: ":float"
                        unit: ":meter"
                        value: "12"
                - type: ":map"
                  entries:
                    - key: "key"
                      value:
                        type: ":tag"
                        value: "steps"
                    - key: "value"
                      value:
                        type: ":integer"
                        unit: ":meter"
                        value: "4"
          - name: "isAim"
            value:
              type: ":boolean"
              value: true
          - name: "isGauge"
            value:
              type: ":boolean"
              value: false
```

## Test: cast map into custom record constructor

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

```ges
module AtomicCastMapToRecord
record :gauge as {
  current: :number clamped between 0 and maximum,
  maximum: :number clamped between 0 and infinity,
  percentage: :percentage computed by
    0% when maximum <= 0,
    otherwise (current / maximum) as :percentage
}

on Start {
  let raw be [current: 125, maximum: 100]
  let gauge as :gauge be raw
  emit Done(gauge: gauge, current: gauge.current, maximum: gauge.maximum, percentage: gauge.percentage, isGauge: gauge is :gauge)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

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
              type: ":gauge"
              entries:
                - key: "current"
                  value:
                    type: ":integer"
                    value: "100"
                - key: "maximum"
                  value:
                    type: ":integer"
                    value: "100"
                - key: "percentage"
                  value:
                    type: ":percentage"
                    value: "0.01"
          - name: "current"
            value:
              type: ":integer"
              value: "100"
          - name: "maximum"
            value:
              type: ":integer"
              value: "100"
          - name: "percentage"
            value:
              type: ":percentage"
              value: "0.01"
          - name: "isGauge"
            value:
              type: ":boolean"
              value: true
```
