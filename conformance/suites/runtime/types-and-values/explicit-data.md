---
formatVersion: 1
suiteId: runtime.explicit-data
title: Explicit portable data and text splitting
kind: scriptApi
level: atomic
categories: [conformance]
---

# Explicit portable data and text splitting

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite specifies portable data construction and interchange with independent expectations.

---

## Test: number-unit-constructors

This case verifies number unit constructors using public language behavior and a binary roundtrip.

### Case description

```yaml
gesBlock: case
id: number-unit-constructors
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start {
 
 emit Done(ok: :Text(value: 123) = "123" and :List(value: [1, 2]) = [1, 2] and (parse ' :Text(value: 123) ') = "123" and :Number(10, "s") = 10s and :Number(value: 2, unit: "m") = 2m and (parse ' :Number(1.2e2, "s") ') = 120s)
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
      args: []
    local:
      - name: Done
        args:
          - name: ok
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: primitive-wrappers

This case verifies primitive wrappers using public language behavior and a binary roundtrip.

### Case description

```yaml
gesBlock: case
id: primitive-wrappers
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start {
 
 emit Done(ok: (parse ":Boolean(true)") = true and (parse ":Number(100.25)") = 100.25 and (parse ":Percentage(0.1)") = 10% and (parse ':Text("123")') = "123" and (parse ':Tag("ready")') = #ready and (parse ":Nothing()") is :Nothing)
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
      args: []
    local:
      - name: Done
        args:
          - name: ok
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: container-wrappers

This case verifies container wrappers using public language behavior and a binary roundtrip.

### Case description

```yaml
gesBlock: case
id: container-wrappers
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start {
 
 emit Done(ok: (parse ":List([1, 2])") = [1, 2] and (parse ":Map([a: 1])") = [a: 1] and (parse ":Dice([1, 3, 6])") = :Dice[1, 3, 6])
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
      args: []
    local:
      - name: Done
        args:
          - name: ok
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: source-data-forms

This case verifies source data forms using public language behavior and a binary roundtrip.

### Case description

```yaml
gesBlock: case
id: source-data-forms
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start {
 
 emit Done(ok: :Nothing() is :Nothing and :Boolean(true) = true and :Text(12) = "12" and :Tag("ready") = #ready and :List([1, 2]) = [1, 2] and :Map([a: 1]) = [a: 1])
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
      args: []
    local:
      - name: Done
        args:
          - name: ok
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: range-fractional-roundtrip

This case verifies range fractional roundtrip using public language behavior and a binary roundtrip.

### Case description

```yaml
gesBlock: case
id: range-fractional-roundtrip
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start {
 let data be :Range(from: 1.5, to: 3.5, step: 0.5)
 let restored be parse (data as :Text)
 emit Done(ok: restored = data and restored[3] = 2.5 and data[:count] = 5)
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
      args: []
    local:
      - name: Done
        args:
          - name: ok
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: range-exact-boundary

This case verifies range exact boundary using public language behavior and a binary roundtrip.

### Case description

```yaml
gesBlock: case
id: range-exact-boundary
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start {
 let data be :Range(9007199254740992, 9007199254740994, 1)
 let restored be parse (data as :Text)
 emit Done(ok: restored[2] = 9007199254740993 and restored = data)
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
      args: []
    local:
      - name: Done
        args:
          - name: ok
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: series-data

This case verifies series data using public language behavior and a binary roundtrip.

### Case description

```yaml
gesBlock: case
id: series-data
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start {
 let data be :Series(fibonacci, offset: 3)
 let restored be parse (data as :Text)
 emit Done(ok: (parse ":Series(true)") is :Nothing and (parse ":Series(nothing)") is :Nothing and restored = data and restored[:term 0] = 2 and (parse ":Series(factorial, offset: 3)")[:term 0] = 6)
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
      args: []
    local:
      - name: Done
        args:
          - name: ok
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: record-snapshot

This case verifies record snapshot using public language behavior and a binary roundtrip.

### Case description

```yaml
gesBlock: case
id: record-snapshot
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start {
 let data be :Record("Unknown", [z: 3, name: "hi", nested: [:Record("Inner", [n: 2])]])
 let restored be parse (data as :Text)
 emit Done(ok: restored = data and restored.name = "hi")
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
      args: []
    local:
      - name: Done
        args:
          - name: ok
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: known-record-constructor

This case verifies known record constructor using public language behavior and a binary roundtrip.

### Case description

```yaml
gesBlock: case
id: known-record-constructor
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
record :Hit as { _ amount: :Number }
on Start {
 let value be parse ":Hit(10)"
 emit Done(ok: value = :Hit(10))
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
      args: []
    local:
      - name: Done
        args:
          - name: ok
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: record-data-bypasses-constructor

This case verifies record data bypasses constructor using public language behavior and a binary roundtrip.

### Case description

```yaml
gesBlock: case
id: record-data-bypasses-constructor
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
record :Hit as { _ amount: :Number clamped between 0 and 100 }
on Start {
 let value be parse ':Record("Hit", [amount: 999])'
 emit Done(ok: value.amount = 999)
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
      args: []
    local:
      - name: Done
        args:
          - name: ok
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: handler-roundtrip

This case verifies handler roundtrip using public language behavior and a binary roundtrip.

### Case description

```yaml
gesBlock: case
id: handler-roundtrip
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start {
 let handler be :Handler(Done(value, _))
 emit Done(ok: (parse (handler as :Text)) = handler and (parse "Done (and i mean it)") = "Done (and i mean it)")
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
      args: []
    local:
      - name: Done
        args:
          - name: ok
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: message-roundtrip

This case verifies message roundtrip using public language behavior and a binary roundtrip.

### Case description

```yaml
gesBlock: case
id: message-roundtrip
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start {
 let message be :Message(Done(value: [1, "two"]) with #ready, #done)
 emit Done(ok: (parse (message as :Text)) = message)
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
      args: []
    local:
      - name: Done
        args:
          - name: ok
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: argumentless-message

This case verifies argumentless message using public language behavior and a binary roundtrip.

### Case description

```yaml
gesBlock: case
id: argumentless-message
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start {
 let message be :Message(Done())
 emit Done(ok: (parse (message as :Text)) = message)
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
      args: []
    local:
      - name: Done
        args:
          - name: ok
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: nested-types

This case verifies nested types using public language behavior and a binary roundtrip.

### Case description

```yaml
gesBlock: case
id: nested-types
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start {
 let value be [:Range(1, 3), :Series(factorial, offset: 2), :Record("Unknown", [ok: true]), :Handler(Done(value)), :Message(Done(value: 5))]
 emit Done(ok: (parse (value as :Text)) = value)
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
      args: []
    local:
      - name: Done
        args:
          - name: ok
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: unknown-type-fallback

This case verifies unknown type fallback using public language behavior and a binary roundtrip.

### Case description

```yaml
gesBlock: case
id: unknown-type-fallback
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start {
 
 emit Done(ok: (parse " :Unknown(12) ") = " :Unknown(12) " and (parse ":Number(1) trailing") = ":Number(1) trailing")
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
      args: []
    local:
      - name: Done
        args:
          - name: ok
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: explicit-split

This case verifies explicit split using public language behavior and a binary roundtrip.

### Case description

```yaml
gesBlock: case
id: explicit-split
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start {
 
 emit Done(ok: "A,,B,"[:split on ","] = ["A", nothing, "B", nothing] and ",A"[:split on ","] = [nothing, "A"] and ""[:split on ","] = [nothing])
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
      args: []
    local:
      - name: Done
        args:
          - name: ok
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: split-preserves-text

This case verifies split preserves text using public language behavior and a binary roundtrip.

### Case description

```yaml
gesBlock: case
id: split-preserves-text
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start {
 
 emit Done(ok: " 12 , true "[:split on ","] = [" 12 ", " true "] and "a--b----c"[:split on "--"] = ["a", "b", nothing, "c"])
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
      args: []
    local:
      - name: Done
        args:
          - name: ok
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: split-whitespace

This case verifies split whitespace using public language behavior and a binary roundtrip.

### Case description

```yaml
gesBlock: case
id: split-whitespace
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start {
 
 emit Done(ok: "  Hello   my Name is Mike  "[:split on whitespace] = ["Hello", "my", "Name", "is", "Mike"] and ""[:split on whitespace] = [])
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
      args: []
    local:
      - name: Done
        args:
          - name: ok
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: split-unicode

This case verifies split unicode using public language behavior and a binary roundtrip.

### Case description

```yaml
gesBlock: case
id: split-unicode
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start {
 
 emit Done(ok: "A  B C"[:split on whitespace] = ["A", "B", "C"] and "A​B"[:split on whitespace] = ["A​B"])
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
      args: []
    local:
      - name: Done
        args:
          - name: ok
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: split-scalar-exact

This case verifies split scalar exact using public language behavior and a binary roundtrip.

### Case description

```yaml
gesBlock: case
id: split-scalar-exact
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start {
 
 emit Done(ok: "é-é"[:split on "é"] = ["é-", nothing])
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
      args: []
    local:
      - name: Done
        args:
          - name: ok
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: split-invalid

This case verifies split invalid using public language behavior and a binary roundtrip.

### Case description

```yaml
gesBlock: case
id: split-invalid
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start {
 
 emit Done(ok: ("A"[:split on nothing]) is :Nothing and ("A"[:split on ""]) is :Nothing and (12[:split on ","]) is :Nothing and ("A"[:split on true]) is :Nothing)
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
      args: []
    local:
      - name: Done
        args:
          - name: ok
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: constructor-effects-retained

This case verifies constructor effects retained.

### Case description

```yaml
gesBlock: case
id: "constructor-effects-retained"
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
record :Probe as { _ number: :Number, sent: :Boolean computed by emit Observed(value: number) }
on Start { parse ":Probe(7)"; emit Done(ok: true) }
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
      args: []
    local:
      -
        name: "Observed"
        args:
          -
            name: "value"
            value:
              type: ":Number.int64"
              value: "7"
      -
        name: "Done"
        args:
          -
            name: "ok"
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        -
          any: true
```

---

## Test: complete-recognition-before-effects

This case verifies complete recognition before effects.

### Case description

```yaml
gesBlock: case
id: "complete-recognition-before-effects"
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
record :Probe as { _ number: :Number, sent: :Boolean computed by emit Observed(value: number) }
on Start { parse ":Probe(7) trailing"; parse "[:Probe(8), broken]"; emit Done(ok: true) }
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
      args: []
    local:
      -
        name: "Done"
        args:
          -
            name: "ok"
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        -
          any: true
```

---

## Test: map-constructor-evaluation-order

This case verifies map constructor evaluation order.

### Case description

```yaml
gesBlock: case
id: "map-constructor-evaluation-order"
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
record :Probe as { _ number: :Number, sent: :Boolean computed by emit Observed(value: number) }
on Start {
 let data be parse "[z: :Probe(1), a: :Probe(2), z: :Probe(3)]"
 emit Done(ok: data.z.number = 3 and data.a.number = 2)
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
      args: []
    local:
      -
        name: "Observed"
        args:
          -
            name: "value"
            value:
              type: ":Number.int64"
              value: "1"
      -
        name: "Observed"
        args:
          -
            name: "value"
            value:
              type: ":Number.int64"
              value: "2"
      -
        name: "Observed"
        args:
          -
            name: "value"
            value:
              type: ":Number.int64"
              value: "3"
      -
        name: "Done"
        args:
          -
            name: "ok"
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        -
          any: true
```

---

## Test: record-reconstruction-skips-effects

This case verifies record reconstruction skips effects.

### Case description

```yaml
gesBlock: case
id: "record-reconstruction-skips-effects"
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
record :Probe as { _ number: :Number, sent: :Boolean computed by emit Observed(value: number) }
on Start { let data be parse ':Record("Probe", [number: 9, sent: false])'; emit Done(ok: data.number = 9 and data.sent = false) }
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
      args: []
    local:
      -
        name: "Done"
        args:
          -
            name: "ok"
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        -
          any: true
```

---

## Test: dynamic-constructor-budget-resume

This case verifies dynamic constructor budget resume.

### Case description

```yaml
gesBlock: case
id: "dynamic-constructor-budget-resume"
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
record :Probe as { _ number: :Number, sent: :Boolean computed by emit Observed(value: number) }
on Start { let data be parse "[:Probe(1), :Probe(2)]"; emit Done(ok: data[1].number = 1 and data[2].number = 2) }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| begin | Start | frame | 1 |
| finish | Probe | frames | 1 |

### Expectation

```yaml
gesBlock: expect
steps:
  begin:
    input:
      args: []
    local: []
    runtimeLimits:
      exclude:
        -
          any: true
  finish:
    accepted: false
    input:
      args: []
    local:
      -
        name: "Observed"
        args:
          -
            name: "value"
            value:
              type: ":Number.int64"
              value: "1"
      -
        name: "Observed"
        args:
          -
            name: "value"
            value:
              type: ":Number.int64"
              value: "2"
      -
        name: "Done"
        args:
          -
            name: "ok"
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        -
          any: true
```

---

## Test: external-json-snapshot

This case verifies external json snapshot using public language behavior and a binary roundtrip.

### Case description

```yaml
gesBlock: case
id: external-json-snapshot
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start {
 let native be :SpatialProbe(vector: :Vector(1m, 2m, 3m), point: :Point(4m, 5m, 6m))
 let snapshot be :test.fromJson(:test.toJson(native))
 emit Done(ok: snapshot = :Record("SpatialProbe", native as :Map) and snapshot <> native and (parse ":SpatialProbe()") is :Text)
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
      args: []
    local:
      - name: Done
        args:
          - name: ok
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: value-json-all-data

This case verifies value json all data using public language behavior and a binary roundtrip.

### Case description

```yaml
gesBlock: case
id: value-json-all-data
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start {
 let data be [:Range(1, 3), :Series(factorial, offset: 4), :Record("Hit", [amount: 25]), :Handler(Done(value, _)), :Message(Done(value: "hi") with #ready), :Dice[1, 4]]
 emit Done(ok: :test.fromJson(:test.toJson(data)) = data)
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
      args: []
    local:
      - name: Done
        args:
          - name: ok
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: float-range-integral-normalization

This case verifies float range integral normalization using public language behavior and a binary roundtrip.

### Case description

```yaml
gesBlock: case
id: float-range-integral-normalization
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start {
 let data be from 9007199254740992.0 to 9007199254740994.0 step 1.0
 let restored be :test.fromJson(:test.toJson(data))
 emit Done(ok: data[2] = 9007199254740993 and restored = data)
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
      args: []
    local:
      - name: Done
        args:
          - name: ok
            value:
              type: ":Boolean"
              value: true
    runtimeLimits:
      exclude:
        - any: true
```

