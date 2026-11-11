---
formatVersion: 1
suiteId: api.schemas
title: GES schemas and wire format
kind: schemaApi
level: atomic
categories: [conformance]
tags: [portable-api, schemas]
---

# GES schemas and wire format

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite verifies schema source and binding errors, the canonical form and hash, exact bytes, decoding, and document checks.

---

## Test: the Bot of the worked example

The full record takes 101 bits in 13 bytes, and the hashes match the specified vectors.

### Case description

```yaml
gesBlock: case
id: bot-full-record
schemaApi:
  sources:
    - "constant $maxHealth be 135\nparameter worldWidth: :Quantity(m)\nparameter worldHeight: :Quantity(m)\n\nschema :Bot as {\n  id: :Number integer between 0 and 65535 key\n  x: :Quantity(m) between 0m and worldWidth step 1cm delta within 1.27m\n  y: :Quantity(m) between 0m and worldHeight step 1cm delta within 1.27m\n  heading: :Quantity(°) wrapping between 0° and 360° step 0.1° delta within 8°\n  turretBearing: :Quantity(°) wrapping between 0° and 360° step 0.1° delta within 8°\n  radarBearing: :Quantity(°) wrapping between 0° and 360° step 0.1° delta within 8°\n  health: :Number integer between 0 and $maxHealth\n  firing: :Boolean\n  shielded: :Boolean\n  overheated: :Boolean\n}\n"
  root: "Bot"
  parameters:
    worldWidth: "5000m"
    worldHeight: "5km"
  encode:
    type: ":Bot"
    entries:
      - key: "firing"
        value:
          type: ":Boolean"
          value: true
      - key: "heading"
        value:
          type: ":Quantity.int64"
          value: "-90"
          unit: ":degree"
      - key: "health"
        value:
          type: ":Number.int64"
          value: "135"
      - key: "id"
        value:
          type: ":Number.int64"
          value: "42"
      - key: "overheated"
        value:
          type: ":Boolean"
          value: true
      - key: "radarBearing"
        value:
          type: ":Quantity.int64"
          value: "10"
          unit: ":degree"
      - key: "shielded"
        value:
          type: ":Boolean"
          value: false
      - key: "turretBearing"
        value:
          type: ":Quantity.binary64"
          value: "359.95"
          unit: ":degree"
      - key: "x"
        value:
          type: ":Quantity.binary64"
          value: "1234.565"
          unit: ":meter"
      - key: "y"
        value:
          type: ":Quantity.binary64"
          value: "4999.996"
          unit: ":meter"
  decode: "2a0040e201093da30200191c16"
  key: "2a0040e201093da30200191c16"
```

### Expectation

```yaml
gesBlock: expect
schema:
  canonical: "gess1\nparameter worldHeight Quantity(meter)\nparameter worldWidth Quantity(meter)\nschema Bot\nfield id Number integer between 0e0 65535e0 key\nfield x Quantity(meter) between 0e0 worldWidth step 1e-2 delta 127e-2\nfield y Quantity(meter) between 0e0 worldHeight step 1e-2 delta 127e-2\nfield heading Quantity(degree) wrapping between 0e0 36e1 step 1e-1 delta 8e0\nfield turretBearing Quantity(degree) wrapping between 0e0 36e1 step 1e-1 delta 8e0\nfield radarBearing Quantity(degree) wrapping between 0e0 36e1 step 1e-1 delta 8e0\nfield health Number integer between 0e0 135e0\nfield firing Boolean\nfield shielded Boolean\nfield overheated Boolean\n"
  hash: "baecf12abb34e7c1"
  bindingHash: "551dd726cf2392d4"
  maxByteCount: 14
  encoded:
    bytes: "2a0040e201093da30200191c16"
  decoded:
    value:
      type: ":Bot"
      entries:
        - key: "firing"
          value:
            type: ":Boolean"
            value: true
        - key: "heading"
          value:
            type: ":Quantity.int64"
            value: "270"
            unit: ":degree"
        - key: "health"
          value:
            type: ":Number.int64"
            value: "135"
        - key: "id"
          value:
            type: ":Number.int64"
            value: "42"
        - key: "overheated"
          value:
            type: ":Boolean"
            value: true
        - key: "radarBearing"
          value:
            type: ":Quantity.int64"
            value: "10"
            unit: ":degree"
        - key: "shielded"
          value:
            type: ":Boolean"
            value: false
        - key: "turretBearing"
          value:
            type: ":Quantity.int64"
            value: "0"
            unit: ":degree"
        - key: "x"
          value:
            type: ":Quantity.binary64"
            value: "1234.56"
            unit: ":meter"
        - key: "y"
          value:
            type: ":Quantity.int64"
            value: "5000"
            unit: ":meter"
    byteCount: 13
  key: 42
```

---

## Test: the Drone of the worked example

The key comes first and is read before the rest, and the full record takes 44 bits in 6 bytes.

### Case description

```yaml
gesBlock: case
id: drone-key
schemaApi:
  sources:
    - "schema :Drone as {\n  id: :Number integer between 0 and 255 key\n  x: :Quantity(m) between -200m and 200m step 5cm delta within 1.55m\n  y: :Quantity(m) between -200m and 200m step 5cm delta within 1.55m\n  heading: :Quantity(°) wrapping between 0° and 360° step 0.5° delta within 15.5°\n}\n"
  root: "Drone"
  encode:
    type: ":Drone"
    entries:
      - key: "heading"
        value:
          type: ":Quantity.int64"
          value: "90"
          unit: ":degree"
      - key: "id"
        value:
          type: ":Number.int64"
          value: "7"
      - key: "x"
        value:
          type: ":Quantity.binary64"
          value: "0.5"
          unit: ":meter"
      - key: "y"
        value:
          type: ":Quantity.int64"
          value: "-200"
          unit: ":meter"
  decode: "07aa0f00d002"
  key: "07aa0f00d002"
```

### Expectation

```yaml
gesBlock: expect
schema:
  canonical: "gess1\nschema Drone\nfield id Number integer between 0e0 255e0 key\nfield x Quantity(meter) between -2e2 2e2 step 5e-2 delta 155e-2\nfield y Quantity(meter) between -2e2 2e2 step 5e-2 delta 155e-2\nfield heading Quantity(degree) wrapping between 0e0 36e1 step 5e-1 delta 155e-1\n"
  hash: "585afee9e1eddd77"
  encoded:
    bytes: "07aa0f00d002"
  key: 7
  decoded:
    value:
      type: ":Drone"
      entries:
        - key: "heading"
          value:
            type: ":Quantity.int64"
            value: "90"
            unit: ":degree"
        - key: "id"
          value:
            type: ":Number.int64"
            value: "7"
        - key: "x"
          value:
            type: ":Quantity.binary64"
            value: "0.5"
            unit: ":meter"
        - key: "y"
          value:
            type: ":Quantity.int64"
            value: "-200"
            unit: ":meter"
    byteCount: 6
```

---

## Test: prefixed spellings do not change the hash

The canonical form uses exact values in base units.

### Case description

```yaml
gesBlock: case
id: canonical-prefixed
schemaApi:
  sources:
    - "schema :Force as { force: :Quantity(N) between 0kN and 50kN step 0.01kN }\n"
  root: "Force"
```

### Expectation

```yaml
gesBlock: expect
schema:
  canonical: "gess1\nschema Force\nfield force Quantity(newton) between 0e0 5e4 step 1e1\n"
  hash: "1d28eb5c826c5a24"
```

---

## Test: comments and layout do not change the hash

A respelled source has the same canonical form.

### Case description

```yaml
gesBlock: case
id: canonical-respelled
schemaApi:
  sources:
    - "// other\n\nschema :Force as {\n  force: :Quantity(N) between 0N and 50000N step 10N // comment\n}\n"
  root: "Force"
```

### Expectation

```yaml
gesBlock: expect
schema:
  canonical: "gess1\nschema Force\nfield force Quantity(newton) between 0e0 5e4 step 1e1\n"
  hash: "1d28eb5c826c5a24"
```

---

## Test: field order a is part of the hash

Field order is the wire order.

### Case description

```yaml
gesBlock: case
id: canonical-order-a
schemaApi:
  sources:
    - "schema :Pair as { a: :Boolean\n b: :Boolean }\n"
  root: "Pair"
```

### Expectation

```yaml
gesBlock: expect
schema:
  canonical: "gess1\nschema Pair\nfield a Boolean\nfield b Boolean\n"
  hash: "25005d9631cf059e"
```

---

## Test: field order b is part of the hash

Field order is the wire order.

### Case description

```yaml
gesBlock: case
id: canonical-order-b
schemaApi:
  sources:
    - "schema :Pair as { b: :Boolean\n a: :Boolean }\n"
  root: "Pair"
```

### Expectation

```yaml
gesBlock: expect
schema:
  canonical: "gess1\nschema Pair\nfield b Boolean\nfield a Boolean\n"
  hash: "50803baeb6aa7c06"
```

---

## Test: quantization vector 1234.565

The exact encoding rounds the binary64 quotient with ties to even.

### Case description

```yaml
gesBlock: case
id: quantize-0
schemaApi:
  sources:
    - "schema :Q as { v: :Quantity(m) between 0m and 5000m step 1cm }\n"
  root: "Q"
  encode:
    type: ":Q"
    entries:
      - key: "v"
        value:
          type: ":Quantity.binary64"
          value: "1234.565"
          unit: ":meter"
  decode: "40e201"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "40e201"
  decoded:
    value:
      type: ":Q"
      entries:
        - key: "v"
          value:
            type: ":Quantity.binary64"
            value: "1234.56"
            unit: ":meter"
    byteCount: 3
```

---

## Test: quantization vector 0.135

The exact encoding rounds the binary64 quotient with ties to even.

### Case description

```yaml
gesBlock: case
id: quantize-1
schemaApi:
  sources:
    - "schema :Q as { v: :Quantity(m) between 0m and 5000m step 1cm }\n"
  root: "Q"
  encode:
    type: ":Q"
    entries:
      - key: "v"
        value:
          type: ":Quantity.binary64"
          value: "0.135"
          unit: ":meter"
  decode: "0e0000"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "0e0000"
  decoded:
    value:
      type: ":Q"
      entries:
        - key: "v"
          value:
            type: ":Quantity.binary64"
            value: "0.14"
            unit: ":meter"
    byteCount: 3
```

---

## Test: quantization vector 1.005

The exact encoding rounds the binary64 quotient with ties to even.

### Case description

```yaml
gesBlock: case
id: quantize-2
schemaApi:
  sources:
    - "schema :Q as { v: :Quantity(m) between 0m and 5000m step 1cm }\n"
  root: "Q"
  encode:
    type: ":Q"
    entries:
      - key: "v"
        value:
          type: ":Quantity.binary64"
          value: "1.005"
          unit: ":meter"
  decode: "640000"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "640000"
  decoded:
    value:
      type: ":Q"
      entries:
        - key: "v"
          value:
            type: ":Quantity.int64"
            value: "1"
            unit: ":meter"
    byteCount: 3
```

---

## Test: quantization vector 0.30000000000000004

The exact encoding rounds the binary64 quotient with ties to even.

### Case description

```yaml
gesBlock: case
id: quantize-3
schemaApi:
  sources:
    - "schema :Q as { v: :Quantity(m) between 0m and 5000m step 1cm }\n"
  root: "Q"
  encode:
    type: ":Q"
    entries:
      - key: "v"
        value:
          type: ":Quantity.binary64"
          value: "0.30000000000000004"
          unit: ":meter"
  decode: "1e0000"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "1e0000"
  decoded:
    value:
      type: ":Q"
      entries:
        - key: "v"
          value:
            type: ":Quantity.binary64"
            value: "0.3"
            unit: ":meter"
    byteCount: 3
```

---

## Test: quantization vector 4999.996

The exact encoding rounds the binary64 quotient with ties to even.

### Case description

```yaml
gesBlock: case
id: quantize-4
schemaApi:
  sources:
    - "schema :Q as { v: :Quantity(m) between 0m and 5000m step 1cm }\n"
  root: "Q"
  encode:
    type: ":Q"
    entries:
      - key: "v"
        value:
          type: ":Quantity.binary64"
          value: "4999.996"
          unit: ":meter"
  decode: "20a107"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "20a107"
  decoded:
    value:
      type: ":Q"
      entries:
        - key: "v"
          value:
            type: ":Quantity.int64"
            value: "5000"
            unit: ":meter"
    byteCount: 3
```

---

## Test: quantization vector 5000.0

The exact encoding rounds the binary64 quotient with ties to even.

### Case description

```yaml
gesBlock: case
id: quantize-5
schemaApi:
  sources:
    - "schema :Q as { v: :Quantity(m) between 0m and 5000m step 1cm }\n"
  root: "Q"
  encode:
    type: ":Q"
    entries:
      - key: "v"
        value:
          type: ":Quantity.int64"
          value: "5000"
          unit: ":meter"
  decode: "20a107"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "20a107"
  decoded:
    value:
      type: ":Q"
      entries:
        - key: "v"
          value:
            type: ":Quantity.int64"
            value: "5000"
            unit: ":meter"
    byteCount: 3
```

---

## Test: a value outside its closed range 5000.004

Nothing is clamped.

### Case description

```yaml
gesBlock: case
id: quantize-out-0
schemaApi:
  sources:
    - "schema :Q as { v: :Quantity(m) between 0m and 5000m step 1cm }\n"
  root: "Q"
  encode:
    type: ":Q"
    entries:
      - key: "v"
        value:
          type: ":Quantity.binary64"
          value: "5000.004"
          unit: ":meter"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    error:
      code: "wire.outOfRange"
      path: "v"
```

---

## Test: a value outside its closed range -0.004

Nothing is clamped.

### Case description

```yaml
gesBlock: case
id: quantize-out-1
schemaApi:
  sources:
    - "schema :Q as { v: :Quantity(m) between 0m and 5000m step 1cm }\n"
  root: "Q"
  encode:
    type: ":Q"
    entries:
      - key: "v"
        value:
          type: ":Quantity.binary64"
          value: "-0.004"
          unit: ":meter"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    error:
      code: "wire.outOfRange"
      path: "v"
```

---

## Test: a step with p above one

12.34 at step 5cm rounds to 247 steps and decodes to 12.35.

### Case description

```yaml
gesBlock: case
id: quantize-five
schemaApi:
  sources:
    - "schema :Q as { v: :Quantity(m) between 0m and 100m step 5cm }\n"
  root: "Q"
  encode:
    type: ":Q"
    entries:
      - key: "v"
        value:
          type: ":Quantity.binary64"
          value: "12.34"
          unit: ":meter"
  decode: "f700"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "f700"
  decoded:
    value:
      type: ":Q"
      entries:
        - key: "v"
          value:
            type: ":Quantity.binary64"
            value: "12.35"
            unit: ":meter"
    byteCount: 2
```

---

## Test: wrapping vector 359.9949

A wrapping value denotes a point on the circle.

### Case description

```yaml
gesBlock: case
id: wrap-0
schemaApi:
  sources:
    - "schema :W as { a: :Quantity(°) wrapping between 0° and 360° step 0.01° }\n"
  root: "W"
  encode:
    type: ":W"
    entries:
      - key: "a"
        value:
          type: ":Quantity.binary64"
          value: "359.9949"
          unit: ":degree"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "9f8c"
```

---

## Test: wrapping vector 359.995

A wrapping value denotes a point on the circle.

### Case description

```yaml
gesBlock: case
id: wrap-1
schemaApi:
  sources:
    - "schema :W as { a: :Quantity(°) wrapping between 0° and 360° step 0.01° }\n"
  root: "W"
  encode:
    type: ":W"
    entries:
      - key: "a"
        value:
          type: ":Quantity.binary64"
          value: "359.995"
          unit: ":degree"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "0000"
```

---

## Test: wrapping vector 360.0

A wrapping value denotes a point on the circle.

### Case description

```yaml
gesBlock: case
id: wrap-2
schemaApi:
  sources:
    - "schema :W as { a: :Quantity(°) wrapping between 0° and 360° step 0.01° }\n"
  root: "W"
  encode:
    type: ":W"
    entries:
      - key: "a"
        value:
          type: ":Quantity.int64"
          value: "360"
          unit: ":degree"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "0000"
```

---

## Test: wrapping vector -0.004

A wrapping value denotes a point on the circle.

### Case description

```yaml
gesBlock: case
id: wrap-3
schemaApi:
  sources:
    - "schema :W as { a: :Quantity(°) wrapping between 0° and 360° step 0.01° }\n"
  root: "W"
  encode:
    type: ":W"
    entries:
      - key: "a"
        value:
          type: ":Quantity.binary64"
          value: "-0.004"
          unit: ":degree"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "0000"
```

---

## Test: wrapping vector -90.0

A wrapping value denotes a point on the circle.

### Case description

```yaml
gesBlock: case
id: wrap-4
schemaApi:
  sources:
    - "schema :W as { a: :Quantity(°) wrapping between 0° and 360° step 0.01° }\n"
  root: "W"
  encode:
    type: ":W"
    entries:
      - key: "a"
        value:
          type: ":Quantity.int64"
          value: "-90"
          unit: ":degree"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "7869"
```

---

## Test: a wrapping value above 2^50 steps

Its rounding would no longer be exact.

### Case description

```yaml
gesBlock: case
id: wrap-huge
schemaApi:
  sources:
    - "schema :W as { a: :Quantity(°) wrapping between 0° and 360° step 0.01° }\n"
  root: "W"
  encode:
    type: ":W"
    entries:
      - key: "a"
        value:
          type: ":Quantity.binary64"
          value: "1e20"
          unit: ":degree"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    error:
      code: "wire.outOfRange"
      path: "a"
```

---

## Test: a wrapping index beyond its period

A decoder rejects it.

### Case description

```yaml
gesBlock: case
id: wrap-invalid-index
schemaApi:
  sources:
    - "schema :W as { a: :Quantity(°) wrapping between 0° and 360° step 120° }\n"
  root: "W"
  decode: "03"
```

### Expectation

```yaml
gesBlock: expect
schema:
  decoded:
    error:
      code: "wire.invalidIndex"
      path: "a"
```

---

## Test: tags, percentages, lists, optionals, text, raw numbers

Each encoding takes its specified bits; elements follow inline without alignment.

### Case description

```yaml
gesBlock: case
id: types-together
schemaApi:
  sources:
    - "schema :Mount as {\n  size: :Number integer between 0 and 3\n  label: :Text max 4 optional\n}\nschema :Unit as {\n  state: :Tag(#idle, #moving, #dead)\n  ratio: :Percentage between 0% and 100% step 1%\n  mounts: :List(:Mount) max 2\n  level: :Number\n  count: :Number integer\n}\n"
  root: "Unit"
  encode:
    type: ":Unit"
    entries:
      - key: "count"
        value:
          type: ":Number.int64"
          value: "-1"
      - key: "level"
        value:
          type: ":Number.binary64"
          value: "1.5"
      - key: "mounts"
        value:
          type: ":List"
          items:
            - type: ":Mount"
              entries:
                - key: "label"
                  value:
                    type: ":Text"
                    value: "ab"
                - key: "size"
                  value:
                    type: ":Number.int64"
                    value: "1"
            - type: ":Mount"
              entries:
                - key: "label"
                  value:
                    type: ":Nothing"
                - key: "size"
                  value:
                    type: ":Number.int64"
                    value: "3"
      - key: "ratio"
        value:
          type: ":Percentage"
          value: "0.25"
      - key: "state"
        value:
          type: ":Tag"
          value: "moving"
  decode: "65acc2c406000000000080fff3ffffffffffffff0f"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "65acc2c406000000000080fff3ffffffffffffff0f"
  decoded:
    value:
      type: ":Unit"
      entries:
        - key: "count"
          value:
            type: ":Number.int64"
            value: "-1"
        - key: "level"
          value:
            type: ":Number.binary64"
            value: "1.5"
        - key: "mounts"
          value:
            type: ":List"
            items:
              - type: ":Mount"
                entries:
                  - key: "label"
                    value:
                      type: ":Text"
                      value: "ab"
                  - key: "size"
                    value:
                      type: ":Number.int64"
                      value: "1"
              - type: ":Mount"
                entries:
                  - key: "label"
                    value:
                      type: ":Nothing"
                  - key: "size"
                    value:
                      type: ":Number.int64"
                      value: "3"
        - key: "ratio"
          value:
            type: ":Percentage"
            value: "0.25"
        - key: "state"
          value:
            type: ":Tag"
            value: "moving"
    byteCount: 21
```

---

## Test: Point with an axis block and Vector without

Declared axes take their width; a Vector without a block takes three binary64 values.

### Case description

```yaml
gesBlock: case
id: spatial
schemaApi:
  sources:
    - "schema :Beacon as {\n  position: :Point(m) {\n    x between 0m and 100m step 1m\n    y between 0m and 3m step 1m\n  }\n  drift: :Vector\n}\n"
  root: "Beacon"
  encode:
    type: ":Beacon"
    entries:
      - key: "drift"
        value:
          type: ":Vector"
          x: "1.5"
          y: "0"
          z: "-2"
      - key: "position"
        value:
          type: ":Point"
          x: "5"
          y: "2"
          z: "0"
          unit: ":meter"
  decode: "05010000000000f07f0000000000000000000000000000008001"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "05010000000000f07f0000000000000000000000000000008001"
  decoded:
    value:
      type: ":Beacon"
      entries:
        - key: "drift"
          value:
            type: ":Vector"
            x: "1.5"
            y: "0"
            z: "-2"
        - key: "position"
          value:
            type: ":Point"
            x: "5"
            y: "2"
            z: "0"
            unit: ":meter"
    byteCount: 26
```

---

## Test: a nonzero omitted axis

An axis without a declaration must be zero.

### Case description

```yaml
gesBlock: case
id: spatial-omitted-axis
schemaApi:
  sources:
    - "schema :Beacon as {\n  position: :Point(m) {\n    x between 0m and 100m step 1m\n    y between 0m and 3m step 1m\n  }\n  drift: :Vector\n}\n"
  root: "Beacon"
  encode:
    type: ":Beacon"
    entries:
      - key: "drift"
        value:
          type: ":Vector"
          x: "0"
          y: "0"
          z: "0"
      - key: "position"
        value:
          type: ":Point"
          x: "5"
          y: "2"
          z: "1"
          unit: ":meter"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    error:
      code: "wire.typeMismatch"
      path: "position"
```

---

## Test: a message with arguments and tags

Arguments follow in signature order, then one bit per declared tag.

### Case description

```yaml
gesBlock: case
id: message-hit
schemaApi:
  sources:
    - "message Hit(target: :Number integer between 0 and 15, _ damage: :Number integer between 0 and 7) tags (#critical, #splash)\nmessage Died(target: :Number integer between 0 and 15)\nmessages :Combat as (Hit, Died)\n"
  root: "Hit"
  encode:
    type: ":Message"
    message:
      name: "Hit"
      args:
        - name: "target"
          value:
            type: ":Number.int64"
            value: "9"
        - name: "_"
          value:
            type: ":Number.int64"
            value: "5"
      tags:
        - "splash"
  decode: "5901"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "5901"
  decoded:
    value:
      type: ":Message"
      message:
        name: "Hit"
        args:
          - name: "target"
            value:
              type: ":Number.int64"
              value: "9"
          - name: "_"
            value:
              type: ":Number.int64"
              value: "5"
        tags:
          - "splash"
    byteCount: 2
```

---

## Test: a message set writes the member index

The index comes first, in declared order.

### Case description

```yaml
gesBlock: case
id: message-set
schemaApi:
  sources:
    - "message Hit(target: :Number integer between 0 and 15, _ damage: :Number integer between 0 and 7) tags (#critical, #splash)\nmessage Died(target: :Number integer between 0 and 15)\nmessages :Combat as (Hit, Died)\n"
  root: "Combat"
  encode:
    type: ":Message"
    message:
      name: "Died"
      args:
        - name: "target"
          value:
            type: ":Number.int64"
            value: "9"
  decode: "13"
```

### Expectation

```yaml
gesBlock: expect
schema:
  canonical: "gess1\nmessages Combat\nmember Hit\nmember Died\nmessage Hit tags #critical #splash\nargument target Number integer between 0e0 15e0\nargument _ damage Number integer between 0e0 7e0\nmessage Died\nargument target Number integer between 0e0 15e0\n"
  hash: "1172b25dd3bc7f0e"
  encoded:
    bytes: "13"
  decoded:
    value:
      type: ":Message"
      message:
        name: "Died"
        args:
          - name: "target"
            value:
              type: ":Number.int64"
              value: "9"
    byteCount: 1
```

---

## Test: a tag outside the message set

A message with an undeclared tag cannot be encoded.

### Case description

```yaml
gesBlock: case
id: message-unknown-tag
schemaApi:
  sources:
    - "message Hit(target: :Number integer between 0 and 15, _ damage: :Number integer between 0 and 7) tags (#critical, #splash)\nmessage Died(target: :Number integer between 0 and 15)\nmessages :Combat as (Hit, Died)\n"
  root: "Hit"
  encode:
    type: ":Message"
    message:
      name: "Hit"
      args:
        - name: "target"
          value:
            type: ":Number.int64"
            value: "9"
        - name: "_"
          value:
            type: ":Number.int64"
            value: "5"
      tags:
        - "other"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    error:
      code: "wire.unknownTag"
```

---

## Test: a syntax error

The error has its stable code and position.

### Case description

```yaml
gesBlock: case
id: error-syntax
schemaApi:
  sources:
    - "schema :A as { a }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.syntax"
    line: 1
    column: 18
```

---

## Test: a prefixed spelling as a type name

The error has its stable code and position.

### Case description

```yaml
gesBlock: case
id: error-prefixed-type
schemaApi:
  sources:
    - "schema :A as { a: :Quantity(kN) }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.syntax"
    line: 1
    column: 29
```

---

## Test: a repeated field

The error has its stable code and position.

### Case description

```yaml
gesBlock: case
id: error-duplicate-field
schemaApi:
  sources:
    - "schema :A as {\n  a: :Boolean\n  a: :Boolean\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.duplicateName"
    line: 3
    column: 3
```

---

## Test: a schema with a built-in name

A built-in type name is taken in every kind of declaration.

### Case description

```yaml
gesBlock: case
id: error-schema-builtin-name
schemaApi:
  sources:
    - "schema :Map as { a: :Boolean }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.duplicateName"
    line: 1
    column: 1
```

---

## Test: a schema named Record

Record is no schema type, but the language's type of all Records.

### Case description

```yaml
gesBlock: case
id: error-schema-record-name
schemaApi:
  sources:
    - "schema :Record as { a: :Boolean }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.duplicateName"
    line: 1
    column: 1
```

---

## Test: a message with a built-in name

A built-in type name is taken in every kind of declaration.

### Case description

```yaml
gesBlock: case
id: error-message-builtin-name
schemaApi:
  sources:
    - "message Text()\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.duplicateName"
    line: 1
    column: 1
```

---

## Test: a message set with a built-in name

A built-in type name is taken in every kind of declaration.

### Case description

```yaml
gesBlock: case
id: error-set-builtin-name
schemaApi:
  sources:
    - "message A()\nmessages :Dice as (A)\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.duplicateName"
    line: 2
    column: 1
```

---

## Test: a repeated schema

The error has its stable code and position.

### Case description

```yaml
gesBlock: case
id: error-duplicate-schema
schemaApi:
  sources:
    - "schema :A as { a: :Boolean }\nschema :A as { b: :Boolean }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.duplicateName"
    line: 2
    column: 1
```

---

## Test: an unknown schema

The error has its stable code and position.

### Case description

```yaml
gesBlock: case
id: error-unknown-type
schemaApi:
  sources:
    - "schema :A as {\n  a: :Missing\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.unknownName"
    line: 2
    column: 6
```

---

## Test: an unknown parameter

The error has its stable code and position.

### Case description

```yaml
gesBlock: case
id: error-unknown-parameter
schemaApi:
  sources:
    - "schema :A as {\n  a: :Number between 0 and limit\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.unknownName"
    line: 2
    column: 28
```

---

## Test: a reference cycle

The error has its stable code and position.

### Case description

```yaml
gesBlock: case
id: error-cycle
schemaApi:
  sources:
    - "schema :A as { b: :B }\nschema :B as { a: :A }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.cycle"
    line: 1
    column: 1
```

---

## Test: a bound with another unit

The error has its stable code and position.

### Case description

```yaml
gesBlock: case
id: error-unit
schemaApi:
  sources:
    - "schema :A as {\n  a: :Quantity(m) between 0s and 1m\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.unitMismatch"
    line: 2
    column: 27
```

---

## Test: a lower bound above its upper bound

The error has its stable code and position.

### Case description

```yaml
gesBlock: case
id: error-range
schemaApi:
  sources:
    - "schema :A as {\n  a: :Number integer between 5 and 1\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidRange"
    line: 2
    column: 30
```

---

## Test: a bound no multiple of its step

The error has its stable code and position.

### Case description

```yaml
gesBlock: case
id: error-step-multiple
schemaApi:
  sources:
    - "schema :A as {\n  a: :Quantity(m) between 0m and 1.005m step 1cm\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidStep"
    line: 2
    column: 34
```

---

## Test: a nonpositive step

The error has its stable code and position.

### Case description

```yaml
gesBlock: case
id: error-step-sign
schemaApi:
  sources:
    - "schema :A as {\n  a: :Quantity(m) between 0m and 1m step 0m\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidStep"
    line: 2
    column: 42
```

---

## Test: integer with step

The error has its stable code and position.

### Case description

```yaml
gesBlock: case
id: error-integer-step
schemaApi:
  sources:
    - "schema :A as {\n  a: :Number integer between 0 and 1 step 1\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidConstraint"
    line: 2
    column: 6
```

---

## Test: a step without a range

The error has its stable code and position.

### Case description

```yaml
gesBlock: case
id: error-step-range
schemaApi:
  sources:
    - "schema :A as {\n  a: :Quantity(m) step 1m\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidConstraint"
    line: 2
    column: 6
```

---

## Test: a delta range not narrower than its field

The error has its stable code and position.

### Case description

```yaml
gesBlock: case
id: error-delta-width
schemaApi:
  sources:
    - "schema :A as {\n  a: :Number integer between 0 and 3 delta within 3\n}\n"
  root: "A"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidConstraint"
    line: 2
    column: 51
```

---

## Test: a key that is not first

The error has its stable code and position.

### Case description

```yaml
gesBlock: case
id: error-key-position
schemaApi:
  sources:
    - "schema :A as {\n  a: :Boolean\n  b: :Number integer key\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidKey"
    line: 3
    column: 3
```

---

## Test: a key that is neither integer nor Tag

The error has its stable code and position.

### Case description

```yaml
gesBlock: case
id: error-key-type
schemaApi:
  sources:
    - "schema :A as {\n  a: :Text max 4 key\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidKey"
    line: 2
    column: 3
```

---

## Test: a second marker after key

The error has its stable code and position.

### Case description

```yaml
gesBlock: case
id: error-key-optional
schemaApi:
  sources:
    - "schema :A as {\n  a: :Number integer key optional\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.syntax"
    line: 2
    column: 26
```

---

## Test: a key with a union

The error has its stable code and position.

### Case description

```yaml
gesBlock: case
id: error-key-union
schemaApi:
  sources:
    - "schema :A as {\n  a: :Number integer | :Text key\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidKey"
    line: 2
    column: 3
```

---

## Test: a key with delta within

The error has its stable code and position.

### Case description

```yaml
gesBlock: case
id: error-key-delta
schemaApi:
  sources:
    - "schema :A as {\n  a: :Number integer between 0 and 255 delta within 1 key\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidKey"
    line: 2
    column: 3
```

---

## Test: a union that a unit would have to distinguish

The error has its stable code and position.

### Case description

```yaml
gesBlock: case
id: error-union-units
schemaApi:
  sources:
    - "schema :A as {\n  a: :Quantity(m) | :Quantity(s)\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidUnion"
    line: 2
    column: 21
```

---

## Test: a union that repeats a schema

The error has its stable code and position.

### Case description

```yaml
gesBlock: case
id: error-union-repeat
schemaApi:
  sources:
    - "schema :B as { b: :Boolean }\nschema :A as {\n  a: :B | :B\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidUnion"
    line: 3
    column: 11
```

---

## Test: a union alternative with a delta range

The error has its stable code and position.

### Case description

```yaml
gesBlock: case
id: error-union-delta
schemaApi:
  sources:
    - "schema :A as {\n  a: :Quantity(m) between 0m and 9m step 1m delta within 1m | :Text\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidUnion"
    line: 2
    column: 6
```

---

## Test: a fractional integer bound

The error has its stable code and position.

### Case description

```yaml
gesBlock: case
id: error-bound-fraction
schemaApi:
  sources:
    - "schema :A as {\n  a: :Number integer between 0 and 1.5\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidBound"
    line: 2
    column: 36
```

---

## Test: an integer bound outside Int64

The error has its stable code and position.

### Case description

```yaml
gesBlock: case
id: error-bound-int64
schemaApi:
  sources:
    - "schema :A as {\n  a: :Number integer between 0 and 1e19\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidBound"
    line: 2
    column: 36
```

---

## Test: a parameter with a fractional value as an integer bound

The error has its stable code and position.

### Case description

```yaml
gesBlock: case
id: error-bound-parameter
schemaApi:
  sources:
    - "parameter limit: :Number\nschema :A as {\n  a: :Number integer between 0 and limit\n}\n"
  root: "A"
  parameters:
    limit: "1.5"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidBound"
    line: 3
    column: 36
```

---

## Test: a parameter value that is no multiple of the step

The error has its stable code and position.

### Case description

```yaml
gesBlock: case
id: error-parameter-step
schemaApi:
  sources:
    - "parameter limit: :Quantity(m)\nschema :A as {\n  a: :Quantity(m) between 0m and limit step 1m\n}\n"
  root: "A"
  parameters:
    limit: "1.5m"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidStep"
    line: 3
    column: 34
```

---

## Test: a parameter value with another unit

The error has its stable code and position.

### Case description

```yaml
gesBlock: case
id: error-parameter-unit
schemaApi:
  sources:
    - "parameter limit: :Quantity(m)\nschema :A as {\n  a: :Quantity(m) between 0m and limit step 1m\n}\n"
  root: "A"
  parameters:
    limit: "1s"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.unitMismatch"
    line: 2
    column: 1
```

---

## Test: a parameter without a value

The error has its stable code and position.

### Case description

```yaml
gesBlock: case
id: error-parameter-missing
schemaApi:
  sources:
    - "parameter limit: :Quantity(m)\nschema :A as {\n  a: :Quantity(m) between 0m and limit step 1m\n}\n"
  root: "A"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.unknownName"
    line: 2
    column: 1
```

---

## Test: a quantized field beyond 2^50 steps

The error has its stable code and position.

### Case description

```yaml
gesBlock: case
id: error-too-wide
schemaApi:
  sources:
    - "schema :A as {\n  a: :Quantity(m) between 0m and 100000000km step 0.000001m\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.tooWide"
    line: 2
    column: 34
```

---

## Test: a largest encoding beyond its limit

The error has its stable code and position.

### Case description

```yaml
gesBlock: case
id: error-too-large
schemaApi:
  sources:
    - "schema :A as {\n  a: :Text max 20000000\n}\n"
  root: "A"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.tooLarge"
    line: 1
    column: 1
```

---

## Test: a largest encoding whose computation would overflow

The error has its stable code and position.

### Case description

```yaml
gesBlock: case
id: error-too-large-overflow
schemaApi:
  sources:
    - "schema :C as { c: :List(:Boolean) max 999999999999 }\nschema :B as { b: :List(:C) max 999999999999 }\nschema :A as {\n  a: :List(:B) max 999999999999\n}\n"
  root: "A"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.tooLarge"
    line: 3
    column: 1
```

---

## Test: a union with a document-only alternative cannot be bound for encoding

The error has its stable code and points at the alternative.

### Case description

```yaml
gesBlock: case
id: notencodable-union
schemaApi:
  sources:
    - "schema :A as {\n  a: :Text max 2 | :Map\n}\n"
  root: "A"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.notEncodable"
    line: 2
    column: 20
```

---

## Test: a Tag without a set cannot be bound for encoding

The error has its stable code and position.

### Case description

```yaml
gesBlock: case
id: notencodable-tag
schemaApi:
  sources:
    - "schema :A as {\n  a: :Tag\n}\n"
  root: "A"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.notEncodable"
    line: 2
    column: 6
```

---

## Test: a Text without max cannot be bound for encoding

The error has its stable code and position.

### Case description

```yaml
gesBlock: case
id: notencodable-text
schemaApi:
  sources:
    - "schema :A as {\n  a: :Text\n}\n"
  root: "A"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.notEncodable"
    line: 2
    column: 6
```

---

## Test: a Bytes without max cannot be bound for encoding

The error has its stable code and position.

### Case description

```yaml
gesBlock: case
id: notencodable-bytes
schemaApi:
  sources:
    - "schema :A as {\n  a: :Bytes\n}\n"
  root: "A"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.notEncodable"
    line: 2
    column: 6
```

---

## Test: a List without max cannot be bound for encoding

The error has its stable code and position.

### Case description

```yaml
gesBlock: case
id: notencodable-list
schemaApi:
  sources:
    - "schema :A as {\n  a: :List(:Boolean)\n}\n"
  root: "A"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.notEncodable"
    line: 2
    column: 6
```

---

## Test: a Map cannot be bound for encoding

The error has its stable code and position.

### Case description

```yaml
gesBlock: case
id: notencodable-map
schemaApi:
  sources:
    - "schema :A as {\n  a: :Map\n}\n"
  root: "A"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.notEncodable"
    line: 2
    column: 6
```

---

## Test: data that ends early

The decoder reports it.

### Case description

```yaml
gesBlock: case
id: wire-truncated
schemaApi:
  sources:
    - "schema :S as { v: :Number integer between 0 and 4 }\n"
  root: "S"
  decode: ""
```

### Expectation

```yaml
gesBlock: expect
schema:
  decoded:
    error:
      code: "wire.truncated"
      path: "v"
```

---

## Test: an index beyond the range

The decoder rejects bit patterns above the last value.

### Case description

```yaml
gesBlock: case
id: wire-invalid-index
schemaApi:
  sources:
    - "schema :S as { v: :Number integer between 0 and 4 }\n"
  root: "S"
  decode: "07"
```

### Expectation

```yaml
gesBlock: expect
schema:
  decoded:
    error:
      code: "wire.invalidIndex"
      path: "v"
```

---

## Test: nonzero padding

Padding bits are zero.

### Case description

```yaml
gesBlock: case
id: wire-invalid-padding
schemaApi:
  sources:
    - "schema :S as { v: :Number integer between 0 and 4 }\n"
  root: "S"
  decode: "09"
```

### Expectation

```yaml
gesBlock: expect
schema:
  decoded:
    error:
      code: "wire.invalidPadding"
```

---

## Test: a record above the caller limit

The limit applies to untrusted data.

### Case description

```yaml
gesBlock: case
id: wire-limit
schemaApi:
  sources:
    - "schema :S as { a: :Number integer\n b: :Number integer }\n"
  root: "S"
  decode: "00000000000000000000000000000000"
  decodeLimit: 8
```

### Expectation

```yaml
gesBlock: expect
schema:
  decoded:
    error:
      code: "wire.limitExceeded"
```

---

## Test: a Text count above its maximum

A count larger than max is invalid.

### Case description

```yaml
gesBlock: case
id: wire-invalid-count
schemaApi:
  sources:
    - "schema :S as { t: :Text max 2 }\n"
  root: "S"
  decode: "0300"
```

### Expectation

```yaml
gesBlock: expect
schema:
  decoded:
    error:
      code: "wire.invalidCount"
      path: "t"
```

---

## Test: Text that is not UTF-8

Invalid bytes are rejected.

### Case description

```yaml
gesBlock: case
id: wire-invalid-text
schemaApi:
  sources:
    - "schema :S as { t: :Text max 2 }\n"
  root: "S"
  decode: "fd03"
```

### Expectation

```yaml
gesBlock: expect
schema:
  decoded:
    error:
      code: "wire.invalidText"
      path: "t"
```

---

## Test: Bytes with a count and raw bytes

A `:Bytes max n` writes its count in `width(n + 1)` bits and then its bytes, which need not be UTF-8.

### Case description

```yaml
gesBlock: case
id: bytes-roundtrip
schemaApi:
  sources:
    - "schema :S as {\n  data: :Bytes max 4\n  tail: :Bytes max 2 | :Nothing\n}\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "data"
        value:
          type: ":Bytes"
          value: "a0b70b"
      - key: "tail"
        value:
          type: ":Nothing"
  decode: "03bd5d08"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "03bd5d08"
  decoded:
    value:
      type: ":S"
      entries:
        - key: "data"
          value:
            type: ":Bytes"
            value: "a0b70b"
        - key: "tail"
          value:
            type: ":Nothing"
    byteCount: 4
```

---

## Test: Bytes as a union alternative

A Bytes alternative follows its union index like any other value.

### Case description

```yaml
gesBlock: case
id: bytes-in-union
schemaApi:
  sources:
    - "schema :S as {\n  data: :Bytes max 4\n  tail: :Bytes max 2 | :Nothing\n}\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "data"
        value:
          type: ":Bytes"
          value: "ff"
      - key: "tail"
        value:
          type: ":Bytes"
          value: "0a"
  decode: "f99702"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "f99702"
  decoded:
    value:
      type: ":S"
      entries:
        - key: "data"
          value:
            type: ":Bytes"
            value: "ff"
        - key: "tail"
          value:
            type: ":Bytes"
            value: "0a"
    byteCount: 3
```

---

## Test: Bytes above their maximum cannot be encoded

Encoding checks the byte count against max.

### Case description

```yaml
gesBlock: case
id: bytes-too-long
schemaApi:
  sources:
    - "schema :S as {\n  data: :Bytes max 4\n  tail: :Bytes max 2 | :Nothing\n}\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "data"
        value:
          type: ":Bytes"
          value: "0102030405"
      - key: "tail"
        value:
          type: ":Nothing"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    error:
      code: "wire.tooLong"
      path: "data"
```

---

## Test: Text is no Bytes value

A Bytes field takes only Bytes values.

### Case description

```yaml
gesBlock: case
id: bytes-type-mismatch
schemaApi:
  sources:
    - "schema :S as {\n  data: :Bytes max 4\n  tail: :Bytes max 2 | :Nothing\n}\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "data"
        value:
          type: ":Text"
          value: "ab"
      - key: "tail"
        value:
          type: ":Nothing"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    error:
      code: "wire.typeMismatch"
      path: "data"
```

---

## Test: A Bytes count above its maximum

A count larger than max is invalid.

### Case description

```yaml
gesBlock: case
id: bytes-invalid-count
schemaApi:
  sources:
    - "schema :S as { data: :Bytes max 4 }\n"
  root: "S"
  decode: "0500"
```

### Expectation

```yaml
gesBlock: expect
schema:
  decoded:
    error:
      code: "wire.invalidCount"
      path: "data"
```

---

## Test: a NaN pattern

NaN is not a Number value.

### Case description

```yaml
gesBlock: case
id: wire-invalid-nan
schemaApi:
  sources:
    - "schema :S as { v: :Number }\n"
  root: "S"
  decode: "000000000000f87f"
```

### Expectation

```yaml
gesBlock: expect
schema:
  decoded:
    error:
      code: "wire.invalidNumber"
      path: "v"
```

---

## Test: a negative zero pattern

Zero is canonically positive.

### Case description

```yaml
gesBlock: case
id: wire-invalid-zero
schemaApi:
  sources:
    - "schema :S as { v: :Number }\n"
  root: "S"
  decode: "0000000000000080"
```

### Expectation

```yaml
gesBlock: expect
schema:
  decoded:
    error:
      code: "wire.invalidNumber"
      path: "v"
```

---

## Test: an infinity with a range

A ranged field holds finite values.

### Case description

```yaml
gesBlock: case
id: wire-invalid-infinity
schemaApi:
  sources:
    - "schema :S as { v: :Number between 0 and 1 }\n"
  root: "S"
  decode: "000000000000f07f"
```

### Expectation

```yaml
gesBlock: expect
schema:
  decoded:
    error:
      code: "wire.invalidNumber"
      path: "v"
```

---

## Test: a value above its range

Nothing is clamped.

### Case description

```yaml
gesBlock: case
id: wire-out-of-range
schemaApi:
  sources:
    - "schema :S as { v: :Number integer between 0 and 4 }\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "v"
        value:
          type: ":Number.int64"
          value: "5"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    error:
      code: "wire.outOfRange"
      path: "v"
```

---

## Test: a fractional value for an integer

An integer field is exact.

### Case description

```yaml
gesBlock: case
id: wire-not-representable
schemaApi:
  sources:
    - "schema :S as { v: :Number integer between 0 and 4 }\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "v"
        value:
          type: ":Number.binary64"
          value: "1.5"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    error:
      code: "wire.notRepresentable"
      path: "v"
```

---

## Test: a missing field

Only optional fields may be absent.

### Case description

```yaml
gesBlock: case
id: wire-missing
schemaApi:
  sources:
    - "schema :S as { v: :Number integer between 0 and 4 }\n"
  root: "S"
  encode:
    type: ":S"
    entries: []
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    error:
      code: "wire.missingField"
      path: "v"
```

---

## Test: a value of another kind

Kinds do not convert.

### Case description

```yaml
gesBlock: case
id: wire-type
schemaApi:
  sources:
    - "schema :S as { v: :Number integer between 0 and 4 }\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "v"
        value:
          type: ":Text"
          value: "a"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    error:
      code: "wire.typeMismatch"
      path: "v"
```

---

## Test: a Boolean in an integer field

The language computes with a Boolean as a number; a field takes it only after a conversion.

### Case description

```yaml
gesBlock: case
id: wire-boolean-integer
schemaApi:
  sources:
    - "schema :S as { v: :Number integer between 0 and 10 }\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "v"
        value:
          type: ":Boolean"
          value: true
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    error:
      code: "wire.typeMismatch"
      path: "v"
```

---

## Test: a Boolean in a Number field

An unconstrained Number field takes no Boolean either.

### Case description

```yaml
gesBlock: case
id: wire-boolean-number
schemaApi:
  sources:
    - "schema :S as { v: :Number }\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "v"
        value:
          type: ":Boolean"
          value: true
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    error:
      code: "wire.typeMismatch"
      path: "v"
```

---

## Test: a union writes the index of its alternative

One bit selects the Tag, and a union alternative may have a step.

### Case description

```yaml
gesBlock: case
id: union-tag
schemaApi:
  sources:
    - "schema :S as { facing: :Quantity(°) wrapping between 0° and 360° step 1° | :Tag(#north, #east, #south, #west) }\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "facing"
        value:
          type: ":Tag"
          value: "south"
  decode: "05"
```

### Expectation

```yaml
gesBlock: expect
schema:
  maxByteCount: 2
  encoded:
    bytes: "05"
  decoded:
    value:
      type: ":S"
      entries:
        - key: "facing"
          value:
            type: ":Tag"
            value: "south"
    byteCount: 1
```

---

## Test: the first alternative of a union

The index is zero and the quantized value follows.

### Case description

```yaml
gesBlock: case
id: union-quantity
schemaApi:
  sources:
    - "schema :S as { facing: :Quantity(°) wrapping between 0° and 360° step 1° | :Tag(#north, #east, #south, #west) }\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "facing"
        value:
          type: ":Quantity.int64"
          value: "90"
          unit: "degree"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "b400"
```

---

## Test: a value that fits no alternative

The kind selects the alternative, and no other kind converts.

### Case description

```yaml
gesBlock: case
id: union-mismatch
schemaApi:
  sources:
    - "schema :S as { facing: :Quantity(°) wrapping between 0° and 360° step 1° | :Tag(#north, #east, #south, #west) }\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "facing"
        value:
          type: ":Boolean"
          value: true
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    error:
      code: "wire.typeMismatch"
      path: "facing"
```

---

## Test: a value outside the alternative its kind selects

The error names the alternative.

### Case description

```yaml
gesBlock: case
id: union-out-of-range
schemaApi:
  sources:
    - "schema :S as { a: :Number integer between 0 and 7 | :Text max 1 }\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "a"
        value:
          type: ":Number.int64"
          value: "8"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    error:
      code: "wire.outOfRange"
      path: "a:Number"
```

---

## Test: a Nothing alternative takes no bits

Only the index is written.

### Case description

```yaml
gesBlock: case
id: union-nothing
schemaApi:
  sources:
    - "schema :S as { a: :Text max 3 | :Nothing }\n"
  root: "S"
  encode:
    type: ":S"
    entries: []
  decode: "01"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "01"
  decoded:
    value:
      type: ":S"
      entries:
        - key: "a"
          value:
            type: ":Nothing"
    byteCount: 1
```

---

## Test: a union with Nothing still holds text

The count and bytes follow the index.

### Case description

```yaml
gesBlock: case
id: union-nothing-text
schemaApi:
  sources:
    - "schema :S as { a: :Text max 3 | :Nothing }\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "a"
        value:
          type: ":Text"
          value: "hi"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "444b03"
```

---

## Test: a list of a union

Each element has its own index.

### Case description

```yaml
gesBlock: case
id: union-list
schemaApi:
  sources:
    - "schema :S as { l: :List(:Number integer between 0 and 7 | :Text max 1) max 2 }\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "l"
        value:
          type: ":List"
          items:
            - type: ":Number.int64"
              value: "5"
            - type: ":Text"
              value: "a"
  decode: "ea61"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "ea61"
  decoded:
    value:
      type: ":S"
      entries:
        - key: "l"
          value:
            type: ":List"
            items:
              - type: ":Number.int64"
                value: "5"
              - type: ":Text"
                value: "a"
    byteCount: 2
```

---

## Test: a field follows a union whose schema alternative ended

The next field comes after the union, not at the next alternative.

### Case description

```yaml
gesBlock: case
id: union-then-field
schemaApi:
  sources:
    - "schema :U as { n: :Boolean }\nschema :S as { a: :U | :Text max 1, b: :Boolean }\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "a"
        value:
          type: ":U"
          entries:
            - key: "n"
              value:
                type: ":Boolean"
                value: true
      - key: "b"
        value:
          type: ":Boolean"
          value: true
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "06"
```

---

## Test: a Map selects the first schema that holds

A Record of a schema selects it by name; a Map has no name.

### Case description

```yaml
gesBlock: case
id: union-map
schemaApi:
  sources:
    - "schema :P as { x: :Boolean }\nschema :Q as { y: :Boolean }\nschema :S as { a: :P | :Q }\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "a"
        value:
          type: ":Map"
          entries:
            - key: "y"
              value:
                type: ":Boolean"
                value: true
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "03"
```

---

## Test: a Map with a key the first schema lacks

An alternative without required fields does not hold a Map with another key, so no data is lost.

### Case description

```yaml
gesBlock: case
id: union-map-unknown-key
schemaApi:
  sources:
    - "schema :P as { x: :Boolean optional }\nschema :Q as { y: :Boolean }\nschema :S as { a: :P | :Q }\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "a"
        value:
          type: ":Map"
          entries:
            - key: "y"
              value:
                type: ":Boolean"
                value: true
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "03"
```

---

## Test: a nested Map with a key the first schema lacks

The key decides at every level of the alternative.

### Case description

```yaml
gesBlock: case
id: union-map-unknown-nested
schemaApi:
  sources:
    - "schema :C as { x: :Boolean optional }\nschema :D as { y: :Boolean }\nschema :P as { c: :C }\nschema :Q as { c: :D }\nschema :S as { a: :P | :Q }\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "a"
        value:
          type: ":Map"
          entries:
            - key: "c"
              value:
                type: ":Map"
                entries:
                  - key: "y"
                    value:
                      type: ":Boolean"
                      value: true
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "03"
```

---

## Test: a Map that no schema holds

The last alternative reports why not.

### Case description

```yaml
gesBlock: case
id: union-map-none-holds
schemaApi:
  sources:
    - "schema :P as { x: :Boolean optional }\nschema :Q as { z: :Boolean optional }\nschema :S as { a: :P | :Q }\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "a"
        value:
          type: ":Map"
          entries:
            - key: "y"
              value:
                type: ":Boolean"
                value: true
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    error:
      code: "wire.typeMismatch"
      path: "a:Q"
```

---

## Test: a Record in a union keeps its other fields out

A Record selects by name, so a field without a schema entry is not transmitted, as for a computed field.

### Case description

```yaml
gesBlock: case
id: union-record-extra-field
schemaApi:
  sources:
    - "schema :P as { x: :Boolean }\nschema :Q as { y: :Boolean }\nschema :S as { a: :P | :Q }\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "a"
        value:
          type: ":Q"
          entries:
            - key: "y"
              value:
                type: ":Boolean"
                value: true
            - key: "e"
              value:
                type: ":Boolean"
                value: true
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "03"
```

---

## Test: a Map outside a union ignores other keys

Only the choice of an alternative looks at them.

### Case description

```yaml
gesBlock: case
id: map-extra-key
schemaApi:
  sources:
    - "schema :S as { x: :Boolean }\n"
  root: "S"
  encode:
    type: ":Map"
    entries:
      - key: "x"
        value:
          type: ":Boolean"
          value: true
      - key: "e"
        value:
          type: ":Boolean"
          value: true
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "01"
```

---

## Test: a Message holds an argument with a Nothing alternative

Labels and their order are the signature of a Message, so it cannot leave one out.

### Case description

```yaml
gesBlock: case
id: message-union-omitted
schemaApi:
  sources:
    - "message N(a: :Boolean, b: :Text max 2 | :Nothing)\n"
  root: "N"
  encode:
    type: ":Message"
    message:
      name: "N"
      args:
        - name: "a"
          value:
            type: ":Boolean"
            value: true
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    error:
      code: "wire.missingField"
      path: "b"
```

---

## Test: a Message passes nothing to a union argument

The index of the Nothing alternative follows.

### Case description

```yaml
gesBlock: case
id: message-union-nothing
schemaApi:
  sources:
    - "message N(a: :Boolean, b: :Text max 2 | :Nothing)\n"
  root: "N"
  encode:
    type: ":Message"
    message:
      name: "N"
      args:
        - name: "a"
          value:
            type: ":Boolean"
            value: true
        - name: "b"
          value:
            type: ":Nothing"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "03"
```

---

## Test: an optional union

The presence bit comes before the index.

### Case description

```yaml
gesBlock: case
id: union-optional
schemaApi:
  sources:
    - "schema :S as { a: :Boolean | :Text max 1 optional }\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "a"
        value:
          type: ":Boolean"
          value: true
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "05"
```

---

## Test: an index no alternative has

Data from a peer is untrusted.

### Case description

```yaml
gesBlock: case
id: union-invalid-index
schemaApi:
  sources:
    - "schema :S as { a: :Boolean | :Text max 1 | :Nothing }\n"
  root: "S"
  decode: "03"
```

### Expectation

```yaml
gesBlock: expect
schema:
  decoded:
    error:
      code: "wire.invalidIndex"
      path: "a"
```

---

## Test: a Map in a list with a key the first schema lacks

The key decides inside a list too.

### Case description

```yaml
gesBlock: case
id: union-map-unknown-list
schemaApi:
  sources:
    - "schema :C as { x: :Boolean optional }\nschema :D as { y: :Boolean }\nschema :P as { l: :List(:C) max 1 }\nschema :Q as { l: :List(:D) max 1 }\nschema :S as { a: :P | :Q }\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "a"
        value:
          type: ":Map"
          entries:
            - key: "l"
              value:
                type: ":List"
                items:
                  - type: ":Map"
                    entries:
                      - key: "y"
                        value:
                          type: ":Boolean"
                          value: true
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "07"
```

---

## Test: a Map in an inner union with a key the first schema lacks

An inner alternative selected by its kind stays part of the outer choice.

### Case description

```yaml
gesBlock: case
id: union-map-unknown-inner
schemaApi:
  sources:
    - "schema :C as { x: :Boolean optional }\nschema :D as { y: :Boolean }\nschema :P as { c: :List(:C) max 1 | :Nothing }\nschema :Q as { c: :List(:D) max 1 | :Nothing }\nschema :S as { a: :P | :Q }\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "a"
        value:
          type: ":Map"
          entries:
            - key: "c"
              value:
                type: ":List"
                items:
                  - type: ":Map"
                    entries:
                      - key: "y"
                        value:
                          type: ":Boolean"
                          value: true
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "0d"
```

---

## Test: a Message holds an optional argument

An absent argument is passed as nothing.

### Case description

```yaml
gesBlock: case
id: message-optional-omitted
schemaApi:
  sources:
    - "message O(a: :Boolean, b: :Text max 2 optional)\n"
  root: "O"
  encode:
    type: ":Message"
    message:
      name: "O"
      args:
        - name: "a"
          value:
            type: ":Boolean"
            value: true
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    error:
      code: "wire.missingField"
      path: "b"
```

---

## Test: Dice, lists and absent fields in a dump

Lists and Dice show their count, and each element follows.

### Case description

```yaml
gesBlock: case
id: dump-lists
schemaApi:
  sources:
    - "schema :S as { d: :Dice max 3 between 1 and 6, o: :Boolean optional, l: :List(:Tag(#a, #b)) max 2 }\n"
  root: "S"
  dump: "5501"
```

### Expectation

```yaml
gesBlock: expect
schema:
  dump:
    text: "d bit=0 width=2 value=count=1\nd[] bit=2 width=3 value=6\no bit=5 width=1 value=absent\nl bit=6 width=2 value=count=1\nl[] bit=8 width=1 value=#b index=1\nbytes 2\n"
    valid: true
```

---

## Test: a presence bit and Nothing in a dump

The width includes the presence bit, and a Nothing alternative has no line of its own.

### Case description

```yaml
gesBlock: case
id: dump-presence
schemaApi:
  sources:
    - "schema :S as { l: :List(:Boolean) max 1 optional, u: :Boolean | :Nothing }\n"
  root: "S"
  dump: "05"
```

### Expectation

```yaml
gesBlock: expect
schema:
  dump:
    text: "l bit=0 width=2 value=count=0\nu bit=2 width=1 value=Nothing index=1\nbytes 1\n"
    valid: true
```

---

## Test: a Tag with a maximum

It travels like a Text and arrives as a Tag.

### Case description

```yaml
gesBlock: case
id: tag-max
schemaApi:
  sources:
    - "schema :S as { t: :Tag max 3 }\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "t"
        value:
          type: ":Tag"
          value: "ab"
  decode: "868901"
```

### Expectation

```yaml
gesBlock: expect
schema:
  canonical: "gess1\nschema S\nfield t Tag max 3\n"
  encoded:
    bytes: "868901"
  decoded:
    value:
      type: ":S"
      entries:
        - key: "t"
          value:
            type: ":Tag"
            value: "ab"
    byteCount: 3
```

---

## Test: a Tag beyond its maximum

The maximum counts characters.

### Case description

```yaml
gesBlock: case
id: tag-max-too-long
schemaApi:
  sources:
    - "schema :S as { t: :Tag max 3 }\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "t"
        value:
          type: ":Tag"
          value: "abcd"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    error:
      code: "wire.tooLong"
      path: "t"
```

---

## Test: a Text in a Tag field

Kinds do not convert, even with the same bytes on the wire.

### Case description

```yaml
gesBlock: case
id: tag-max-text
schemaApi:
  sources:
    - "schema :S as { t: :Tag max 3 }\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "t"
        value:
          type: ":Text"
          value: "ab"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    error:
      code: "wire.typeMismatch"
      path: "t"
```

---

## Test: a decoded Tag with an uppercase start

Data from a peer is untrusted, and only a valid name becomes a Tag.

### Case description

```yaml
gesBlock: case
id: tag-max-invalid-upper
schemaApi:
  sources:
    - "schema :S as { t: :Tag max 3 }\n"
  root: "S"
  decode: "068901"
```

### Expectation

```yaml
gesBlock: expect
schema:
  decoded:
    error:
      code: "wire.invalidTag"
      path: "t"
```

---

## Test: a decoded Tag with an empty name

Data from a peer is untrusted, and only a valid name becomes a Tag.

### Case description

```yaml
gesBlock: case
id: tag-max-invalid-empty
schemaApi:
  sources:
    - "schema :S as { t: :Tag max 3 }\n"
  root: "S"
  decode: "00"
```

### Expectation

```yaml
gesBlock: expect
schema:
  decoded:
    error:
      code: "wire.invalidTag"
      path: "t"
```

---

## Test: a decoded Tag with a byte outside ASCII

Data from a peer is untrusted, and only a valid name becomes a Tag.

### Case description

```yaml
gesBlock: case
id: tag-max-invalid-byte
schemaApi:
  sources:
    - "schema :S as { t: :Tag max 3 }\n"
  root: "S"
  decode: "fd03"
```

### Expectation

```yaml
gesBlock: expect
schema:
  decoded:
    error:
      code: "wire.invalidTag"
      path: "t"
```

---

## Test: a decoded Tag with an underscore

Data from a peer is untrusted, and only a valid name becomes a Tag.

### Case description

```yaml
gesBlock: case
id: tag-max-invalid-underscore
schemaApi:
  sources:
    - "schema :S as { t: :Tag max 3 }\n"
  root: "S"
  decode: "867d01"
```

### Expectation

```yaml
gesBlock: expect
schema:
  decoded:
    error:
      code: "wire.invalidTag"
      path: "t"
```

---

## Test: a Tag with a set and a maximum

The set already fixes the width.

### Case description

```yaml
gesBlock: case
id: error-tag-set-max
schemaApi:
  sources:
    - "schema :S as { t: :Tag(#a) max 3 }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.syntax"
    line: 1
    column: 28
```

---

## Test: a Tag with a maximum as a key

A key has a fixed width.

### Case description

```yaml
gesBlock: case
id: error-tag-max-key
schemaApi:
  sources:
    - "schema :S as { t: :Tag max 3 key }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidKey"
    line: 1
    column: 16
```

---

## Test: a Tag with a maximum of zero

A maximum is positive.

### Case description

```yaml
gesBlock: case
id: error-tag-max-zero
schemaApi:
  sources:
    - "schema :S as { t: :Tag max 0 }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidConstraint"
    line: 1
    column: 19
```

---

## Test: a Tag with a maximum in a union

The value selects the alternative by its kind.

### Case description

```yaml
gesBlock: case
id: tag-max-union
schemaApi:
  sources:
    - "schema :S as { t: :Tag max 2 | :Boolean }\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "t"
        value:
          type: ":Tag"
          value: "a"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "0a03"
```

---

## Test: a Tag with a maximum next to a Tag with a set

Both have the type name Tag.

### Case description

```yaml
gesBlock: case
id: error-tag-max-union
schemaApi:
  sources:
    - "schema :S as { t: :Tag(#a) | :Tag max 2 }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidUnion"
    line: 1
    column: 30
```

---

## Test: a Tag with a maximum in a dump

It shows the name and has no index.

### Case description

```yaml
gesBlock: case
id: tag-max-dump
schemaApi:
  sources:
    - "schema :S as { t: :Tag max 3 }\n"
  root: "S"
  dump: "868901"
```

### Expectation

```yaml
gesBlock: expect
schema:
  dump:
    text: "t bit=0 width=18 value=#ab\nbytes 3\n"
    valid: true
```

---

## Test: a document Tag beyond its maximum

The maximum is checked in a document too.

### Case description

```yaml
gesBlock: case
id: document-tag-max
schemaApi:
  sources:
    - "schema :S as { t: :Tag max 2 }\n"
  document: ":Schema(:S)\nt: #abc\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    error:
      code: "document.outOfRange"
      line: 2
      column: 4
```

---

## Test: a thermal unit in a schema

A quantity field with a prefixed bound uses the base unit on the wire and in the canonical form.

### Case description

```yaml
gesBlock: case
id: thermal-field
schemaApi:
  sources:
    - "schema :S as { g: :Quantity(W/K) between 0W/K and 5kW/K step 1W/K }\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "g"
        value:
          type: ":Quantity.int64"
          value: "1000"
          unit: ":wattPerKelvin"
```

### Expectation

```yaml
gesBlock: expect
schema:
  canonical: "gess1\nschema S\nfield g Quantity(wattPerKelvin) between 0e0 5e3 step 1e0\n"
  encoded:
    bytes: "e803"
```

---

## Test: a union in a dump

The union names its alternative.

### Case description

```yaml
gesBlock: case
id: union-dump
schemaApi:
  sources:
    - "schema :S as { a: :Text max 1 | :Tag(#x, #y) }\n"
  root: "S"
  dump: "03"
```

### Expectation

```yaml
gesBlock: expect
schema:
  dump:
    text: "a bit=0 width=1 value=Tag index=1\na:Tag bit=1 width=1 value=#y index=1\nbytes 1\n"
    valid: true
```

---

## Test: Dice with a maximum and a range

The rolls are written in descending order, whatever order they came in.

### Case description

```yaml
gesBlock: case
id: dice-range
schemaApi:
  sources:
    - "schema :S as { d: :Dice max 3 between 1 and 6 }\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "d"
        value:
          type: ":Dice"
          rolls: [3, 6, 1]
  decode: "5700"
```

### Expectation

```yaml
gesBlock: expect
schema:
  canonical: "gess1\nschema S\nfield d Dice max 3 between 1e0 6e0\n"
  encoded:
    bytes: "5700"
  decoded:
    value:
      type: ":S"
      entries:
        - key: "d"
          value:
            type: ":Dice"
            rolls: [6, 3, 1]
    byteCount: 2
```

---

## Test: Dice decoded in any order

Creating the value sorts the rolls.

### Case description

```yaml
gesBlock: case
id: dice-any-order
schemaApi:
  sources:
    - "schema :S as { d: :Dice max 2 between 1 and 6 }\n"
  root: "S"
  decode: "6a"
```

### Expectation

```yaml
gesBlock: expect
schema:
  decoded:
    value:
      type: ":S"
      entries:
        - key: "d"
          value:
            type: ":Dice"
            rolls: [4, 3]
    byteCount: 1
```

---

## Test: Dice without a range

A roll takes 31 bits, as a positive Int32.

### Case description

```yaml
gesBlock: case
id: dice-no-range
schemaApi:
  sources:
    - "schema :S as { d: :Dice max 1 }\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "d"
        value:
          type: ":Dice"
          rolls: [7]
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "0d000000"
```

---

## Test: too many rolls

The maximum counts rolls.

### Case description

```yaml
gesBlock: case
id: dice-too-long
schemaApi:
  sources:
    - "schema :S as { d: :Dice max 1 between 1 and 6 }\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "d"
        value:
          type: ":Dice"
          rolls: [2, 1]
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    error:
      code: "wire.tooLong"
      path: "d"
```

---

## Test: a roll outside the range

Nothing is clamped.

### Case description

```yaml
gesBlock: case
id: dice-out-of-range
schemaApi:
  sources:
    - "schema :S as { d: :Dice max 1 between 1 and 6 }\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "d"
        value:
          type: ":Dice"
          rolls: [7]
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    error:
      code: "wire.outOfRange"
      path: "d[]"
```

---

## Test: a roll index outside the range

Data from a peer is untrusted.

### Case description

```yaml
gesBlock: case
id: dice-invalid-index
schemaApi:
  sources:
    - "schema :S as { d: :Dice max 1 between 1 and 6 }\n"
  root: "S"
  decode: "0f"
```

### Expectation

```yaml
gesBlock: expect
schema:
  decoded:
    error:
      code: "wire.invalidIndex"
      path: "d[]"
```

---

## Test: Dice without a maximum cannot be bound for encoding

The error has its stable code and position.

### Case description

```yaml
gesBlock: case
id: notencodable-dice
schemaApi:
  sources:
    - "schema :S as { d: :Dice between 1 and 6 }\n"
  root: "S"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.notEncodable"
    line: 1
    column: 19
```

---

## Test: a Dice range below one

A roll is positive.

### Case description

```yaml
gesBlock: case
id: error-dice-bound
schemaApi:
  sources:
    - "schema :S as { d: :Dice max 1 between 0 and 6 }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidBound"
    line: 1
    column: 19
```

---

## Test: a Dice range beyond Int32

A roll is a positive Int32.

### Case description

```yaml
gesBlock: case
id: error-dice-upper
schemaApi:
  sources:
    - "schema :S as { d: :Dice max 1 between 1 and 2147483648 }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidBound"
    line: 1
    column: 19
```

---

## Test: Dice with a step

A Dice has only a range.

### Case description

```yaml
gesBlock: case
id: error-dice-step
schemaApi:
  sources:
    - "schema :S as { d: :Dice max 1 between 1 and 6 step 1 }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidConstraint"
    line: 1
    column: 19
```

---

## Test: a document with Dice within its limits

Documents check the same limits.

### Case description

```yaml
gesBlock: case
id: document-dice
schemaApi:
  sources:
    - "schema :S as { d: :Dice max 2 between 1 and 6 }\n"
  document: ":Schema(:S)\nd: :Dice[6, 1]\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    ok: true
```

---

## Test: a document with too many rolls

The error is positioned at the value.

### Case description

```yaml
gesBlock: case
id: document-dice-long
schemaApi:
  sources:
    - "schema :S as { d: :Dice max 1 between 1 and 6 }\n"
  document: ":Schema(:S)\nd: :Dice[6, 1]\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    error:
      code: "document.outOfRange"
      line: 2
      column: 4
```

---

## Test: a document with a roll outside the range

The error is positioned at the value.

### Case description

```yaml
gesBlock: case
id: document-dice-range
schemaApi:
  sources:
    - "schema :S as { d: :Dice max 2 between 1 and 6 }\n"
  document: ":Schema(:S)\nd: :Dice[7]\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    error:
      code: "document.outOfRange"
      line: 2
      column: 4
```

---

## Test: Nothing as a list element

The element is only its index.

### Case description

```yaml
gesBlock: case
id: union-list-nothing
schemaApi:
  sources:
    - "schema :S as { l: :List(:Text max 1 | :Nothing) max 2 }\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "l"
        value:
          type: ":List"
          items:
            - type: ":Nothing"
            - type: ":Text"
              value: "a"
  decode: "360c"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "360c"
  decoded:
    value:
      type: ":S"
      entries:
        - key: "l"
          value:
            type: ":List"
            items:
              - type: ":Nothing"
              - type: ":Text"
                value: "a"
    byteCount: 2
```

---

## Test: a schema alternative without fields

The next field comes behind the union.

### Case description

```yaml
gesBlock: case
id: union-empty-schema
schemaApi:
  sources:
    - "schema :E as { }\nschema :S as { a: :E | :Text max 1, b: :Boolean }\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "a"
        value:
          type: ":E"
          entries: []
      - key: "b"
        value:
          type: ":Boolean"
          value: true
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "02"
```

---

## Test: a list as an alternative in a list

Both lists continue correctly when the inner one ends.

### Case description

```yaml
gesBlock: case
id: union-list-in-list
schemaApi:
  sources:
    - "schema :S as { l: :List(:List(:Boolean) max 2 | :Text max 1) max 2 }\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "l"
        value:
          type: ":List"
          items:
            - type: ":List"
              items:
                - type: ":Boolean"
                  value: true
            - type: ":Text"
              value: ""
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "6a"
```

---

## Test: a union in a referenced schema

The next field of the outer schema follows.

### Case description

```yaml
gesBlock: case
id: union-in-reference
schemaApi:
  sources:
    - "schema :U as { a: :Boolean, c: :Text max 1 | :Boolean }\nschema :S as { u: :U, z: :Boolean }\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "u"
        value:
          type: ":U"
          entries:
            - key: "a"
              value:
                type: ":Boolean"
                value: true
            - key: "c"
              value:
                type: ":Boolean"
                value: true
      - key: "z"
        value:
          type: ":Boolean"
          value: true
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "0f"
```

---

## Test: a union at the end of an alternative

The inner union continues behind the outer one.

### Case description

```yaml
gesBlock: case
id: union-in-union
schemaApi:
  sources:
    - "schema :U as { c: :Text max 1 | :Boolean }\nschema :S as { a: :U | :Text max 1, z: :Boolean }\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "a"
        value:
          type: ":U"
          entries:
            - key: "c"
              value:
                type: ":Boolean"
                value: true
      - key: "z"
        value:
          type: ":Boolean"
          value: true
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "0e"
```

---

## Test: a Map that fits no schema alternative

The last schema reports why.

### Case description

```yaml
gesBlock: case
id: union-map-error
schemaApi:
  sources:
    - "schema :P as { x: :Number integer between 0 and 1 }\nschema :S as { a: :P | :Text max 1 }\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "a"
        value:
          type: ":Map"
          entries:
            - key: "x"
              value:
                type: ":Number.int64"
                value: "5"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    error:
      code: "wire.outOfRange"
      path: "a:P.x"
```

---

## Test: a Map with too small a buffer

A full buffer is no reason to try the next schema.

### Case description

```yaml
gesBlock: case
id: union-map-buffer
schemaApi:
  sources:
    - "schema :P as { x: :Number }\nschema :Q as { y: :Number optional }\nschema :S as { a: :P | :Q }\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "a"
        value:
          type: ":Map"
          entries:
            - key: "x"
              value:
                type: ":Number.binary64"
                value: "1.5"
  encodeBuffer: 1
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    error:
      code: "wire.bufferTooSmall"
```

---

## Test: an optional union with Nothing

Nothing would have two encodings.

### Case description

```yaml
gesBlock: case
id: error-union-optional-nothing
schemaApi:
  sources:
    - "schema :S as { a: :Text max 2 | :Nothing optional }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidUnion"
    line: 1
    column: 16
```

---

## Test: a document may leave out a field with a Nothing alternative

It counts as nothing, as in a value.

### Case description

```yaml
gesBlock: case
id: document-union-nothing-omitted
schemaApi:
  sources:
    - "schema :S as { a: :Text max 2 | :Nothing, b: :Boolean }\n"
  document: ":Schema(:S)\nb: true\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    ok: true
```

---

## Test: a document may not leave out another union

Only a Nothing alternative or optional allows it.

### Case description

```yaml
gesBlock: case
id: document-union-omitted
schemaApi:
  sources:
    - "schema :S as { a: :Text max 2 | :Boolean, b: :Boolean }\n"
  document: ":Schema(:S)\nb: true\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    error:
      code: "document.missingField"
      line: 1
      column: 1
```

---

## Test: the description of Dice

Its limits are read-only data, like those of a number.

### Case description

```yaml
gesBlock: case
id: describe-dice
schemaApi:
  sources:
    - "schema :S as { d: :Dice max 3 between 1 and 6, e: :Dice }\n"
  root: "S"
  bind: false
  describe: true
```

### Expectation

```yaml
gesBlock: expect
schema:
  description:
    name: "S"
    kind: "Schema"
    fields:
      - name: "d"
        alternatives:
          - kind: "Dice"
            lower: "1e0"
            upper: "6e0"
            max: 3
      - name: "e"
        alternatives:
          - kind: "Dice"
```

---

## Test: Dice in an integer field

The sum of the rolls is a number only after a conversion, and decoding could not return the rolls.

### Case description

```yaml
gesBlock: case
id: wire-dice-integer
schemaApi:
  sources:
    - "schema :S as { v: :Number integer between 0 and 10 }\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "v"
        value:
          type: ":Dice"
          rolls: [3, 4]
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    error:
      code: "wire.typeMismatch"
      path: "v"
```

---

## Test: the converted sum of Dice

The result of `as :Number` is an ordinary number.

### Case description

```yaml
gesBlock: case
id: wire-dice-converted
schemaApi:
  sources:
    - "schema :S as { v: :Number integer between 0 and 10 }\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "v"
        value:
          type: ":Number.int64"
          value: "7"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "07"
```

---

## Test: a Boolean in a document's Number field

A document checks kinds as strictly as the wire.

### Case description

```yaml
gesBlock: case
id: document-boolean-number
schemaApi:
  sources:
    - "schema :S as { v: :Number integer between 0 and 10 }\n"
  root: "S"
  document: ":Schema(:S)\nv: true\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    error:
      code: "document.fieldType"
      line: 2
      column: 4
```

---

## Test: a value with another unit

Units do not convert.

### Case description

```yaml
gesBlock: case
id: wire-unit
schemaApi:
  sources:
    - "schema :Q as { v: :Quantity(m) between 0m and 5000m step 1cm }\n"
  root: "Q"
  encode:
    type: ":Q"
    entries:
      - key: "v"
        value:
          type: ":Quantity.int64"
          value: "1"
          unit: ":second"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    error:
      code: "wire.typeMismatch"
      path: "v"
```

---

## Test: a tag outside its set

The set is closed.

### Case description

```yaml
gesBlock: case
id: wire-unknown-tag
schemaApi:
  sources:
    - "schema :S as { t: :Tag(#a, #b) }\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "t"
        value:
          type: ":Tag"
          value: "c"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    error:
      code: "wire.unknownTag"
      path: "t"
```

---

## Test: a Text above its maximum

max counts UTF-8 bytes.

### Case description

```yaml
gesBlock: case
id: wire-too-long
schemaApi:
  sources:
    - "schema :S as { t: :Text max 2 }\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "t"
        value:
          type: ":Text"
          value: "äa"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    error:
      code: "wire.tooLong"
      path: "t"
```

---

## Test: a buffer that is too small

The caller gets an error, not a partial record.

### Case description

```yaml
gesBlock: case
id: wire-buffer
schemaApi:
  sources:
    - "schema :S as { v: :Number integer between 0 and 4 }\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "v"
        value:
          type: ":Number.int64"
          value: "1"
  encodeBuffer: 0
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    error:
      code: "wire.bufferTooSmall"
```

---

## Test: a field without a schema entry

Computed fields of a script record are not transmitted.

### Case description

```yaml
gesBlock: case
id: extra-field
schemaApi:
  sources:
    - "schema :S as { v: :Number integer between 0 and 4 }\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "computed"
        value:
          type: ":Number.int64"
          value: "9"
      - key: "v"
        value:
          type: ":Number.int64"
          value: "1"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "01"
```

---

## Test: a document with union alternatives

Documents use the same schemas.

### Case description

```yaml
gesBlock: case
id: document-ok
schemaApi:
  sources:
    - "schema :Unit as {\n  name: :Text\n  count: :Number integer between 1 and 12\n}\n\nschema :Loadout as {\n  units: :List(:Text | :Unit)\n  facing: :Quantity(°) between 0° and 360° | :Tag(#north, #east)\n  range: :Quantity(m) between 0m and limit optional\n}\nparameter limit: :Quantity(m)\n"
  document: ":Schema(:Loadout)\nunits: ['scout', [name: 'tank', count: 2]]\nfacing: #north\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    ok: true
```

---

## Test: the Number form in a document

An explicit form counts as the value it constructs.

### Case description

```yaml
gesBlock: case
id: document-form-number
schemaApi:
  sources:
    - "schema :S as { v: :Number integer between 0 and 10 }\n"
  root: "S"
  document: ":Schema(:S)\nv: :Number(3)\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    ok: true
```

---

## Test: the Number form outside its range

An explicit form counts as the value it constructs.

### Case description

```yaml
gesBlock: case
id: document-form-number-range
schemaApi:
  sources:
    - "schema :S as { v: :Number integer between 0 and 10 }\n"
  root: "S"
  document: ":Schema(:S)\nv: :Number(30)\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    error:
      code: "document.outOfRange"
      line: 2
      column: 4
```

---

## Test: the Number form from Text

An explicit form counts as the value it constructs.

### Case description

```yaml
gesBlock: case
id: document-form-number-text
schemaApi:
  sources:
    - "schema :S as { v: :Number integer between 0 and 10 }\n"
  root: "S"
  document: ":Schema(:S)\nv: :Number('3')\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    ok: true
```

---

## Test: the Number form with a unit

An explicit form counts as the value it constructs.

### Case description

```yaml
gesBlock: case
id: document-form-number-unit
schemaApi:
  sources:
    - "schema :S as { v: :Quantity(m) between 0m and 10m }\n"
  root: "S"
  document: ":Schema(:S)\nv: :Number(3, 'm')\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    ok: true
```

---

## Test: the Number form with another unit

An explicit form counts as the value it constructs.

### Case description

```yaml
gesBlock: case
id: document-form-number-other-unit
schemaApi:
  sources:
    - "schema :S as { v: :Quantity(m) between 0m and 10m }\n"
  root: "S"
  document: ":Schema(:S)\nv: :Number(3, 's')\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    error:
      code: "document.fieldType"
      line: 2
      column: 4
```

---

## Test: a form that constructs nothing

An explicit form counts as the value it constructs.

### Case description

```yaml
gesBlock: case
id: document-form-invalid
schemaApi:
  sources:
    - "schema :S as { v: :Number integer between 0 and 10 }\n"
  root: "S"
  document: ":Schema(:S)\nv: :Number('abc')\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    error:
      code: "document.invalidLiteral"
      line: 2
      column: 4
```

---

## Test: a Range form that constructs nothing

An explicit form counts as the value it constructs.

### Case description

```yaml
gesBlock: case
id: document-form-invalid-range
schemaApi:
  sources:
    - "schema :S as { v: :Range }\n"
  root: "S"
  document: ":Schema(:S)\nv: :Range(1)\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    error:
      code: "document.invalidLiteral"
      line: 2
      column: 4
```

---

## Test: the Boolean form

An explicit form counts as the value it constructs.

### Case description

```yaml
gesBlock: case
id: document-form-boolean
schemaApi:
  sources:
    - "schema :S as { v: :Boolean }\n"
  root: "S"
  document: ":Schema(:S)\nv: :Boolean(true)\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    ok: true
```

---

## Test: the Text form over its limit

An explicit form counts as the value it constructs.

### Case description

```yaml
gesBlock: case
id: document-form-text-max
schemaApi:
  sources:
    - "schema :S as { v: :Text max 2 }\n"
  root: "S"
  document: ":Schema(:S)\nv: :Text('abc')\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    error:
      code: "document.outOfRange"
      line: 2
      column: 4
```

---

## Test: the Nothing form in an optional field

An explicit form counts as the value it constructs.

### Case description

```yaml
gesBlock: case
id: document-form-nothing
schemaApi:
  sources:
    - "schema :S as { v: :Number optional }\n"
  root: "S"
  document: ":Schema(:S)\nv: :Nothing()\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    ok: true
```

---

## Test: a form in a List form

An explicit form counts as the value it constructs.

### Case description

```yaml
gesBlock: case
id: document-form-list
schemaApi:
  sources:
    - "schema :S as { v: :List(:Number integer between 0 and 5) }\n"
  root: "S"
  document: ":Schema(:S)\nv: :List([:Number(9)])\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    error:
      code: "document.outOfRange"
      line: 2
      column: 4
```

---

## Test: the Dice form

An explicit form counts as the value it constructs.

### Case description

```yaml
gesBlock: case
id: document-form-dice
schemaApi:
  sources:
    - "schema :S as { v: :Dice }\n"
  root: "S"
  document: ":Schema(:S)\nv: :Dice([3, 4])\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    ok: true
```

---

## Test: the Vector form

An explicit form counts as the value it constructs.

### Case description

```yaml
gesBlock: case
id: document-form-vector
schemaApi:
  sources:
    - "schema :S as { v: :Vector(m) }\n"
  root: "S"
  document: ":Schema(:S)\nv: :Vector(x: 1m, y: 2m)\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    ok: true
```

---

## Test: the Point form

An explicit form counts as the value it constructs.

### Case description

```yaml
gesBlock: case
id: document-form-point
schemaApi:
  sources:
    - "schema :S as { v: :Point(m) }\n"
  root: "S"
  document: ":Schema(:S)\nv: :Point(1m, 2m)\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    ok: true
```

---

## Test: the Percentage form

An explicit form counts as the value it constructs.

### Case description

```yaml
gesBlock: case
id: document-form-percentage
schemaApi:
  sources:
    - "schema :S as { v: :Percentage between 0% and 100% }\n"
  root: "S"
  document: ":Schema(:S)\nv: :Percentage(50%)\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    ok: true
```

---

## Test: the Tag form

An explicit form counts as the value it constructs.

### Case description

```yaml
gesBlock: case
id: document-form-tag
schemaApi:
  sources:
    - "schema :S as { v: :Tag(#a) }\n"
  root: "S"
  document: ":Schema(:S)\nv: :Tag('a')\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    ok: true
```

---

## Test: the Map form for a schema

An explicit form counts as the value it constructs.

### Case description

```yaml
gesBlock: case
id: document-form-map
schemaApi:
  sources:
    - "schema :U as { a: :Number integer between 0 and 5 }\nschema :S as { v: :U }\n"
  root: "S"
  document: ":Schema(:S)\nv: :Map([a: 1])\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    ok: true
```

---

## Test: the Map form outside a range

An explicit form counts as the value it constructs.

### Case description

```yaml
gesBlock: case
id: document-form-map-range
schemaApi:
  sources:
    - "schema :U as { a: :Number integer between 0 and 5 }\nschema :S as { v: :U }\n"
  root: "S"
  document: ":Schema(:S)\nv: :Map([a: 9])\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    error:
      code: "document.outOfRange"
      line: 2
      column: 4
```

---

## Test: forms inside a Record form

An explicit form counts as the value it constructs.

### Case description

```yaml
gesBlock: case
id: document-form-record-nested
schemaApi:
  sources:
    - "schema :U as { a: :Number integer between 0 and 5 }\nschema :S as { v: :U }\n"
  root: "S"
  document: ":Schema(:S)\nv: :Record('U', :Map([a: :Number(1)]))\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    ok: true
```

---

## Test: a Record type written as a form

An explicit form counts as the value it constructs.

### Case description

```yaml
gesBlock: case
id: document-form-record-type
schemaApi:
  sources:
    - "schema :U as { a: :Number integer between 0 and 5 }\nschema :S as { v: :U }\n"
  root: "S"
  document: ":Schema(:S)\nv: :Record(:Text('U'), [a: 1])\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    ok: true
```

---

## Test: Record forms in a List form

An explicit form counts as the value it constructs.

### Case description

```yaml
gesBlock: case
id: document-form-list-records
schemaApi:
  sources:
    - "schema :U as { a: :Number integer between 0 and 5 }\nschema :S as { v: :List(:U) }\n"
  root: "S"
  document: ":Schema(:S)\nv: :List([:Record('U', [a: 1])])\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    ok: true
```

---

## Test: a form in a union

An explicit form counts as the value it constructs.

### Case description

```yaml
gesBlock: case
id: document-form-union
schemaApi:
  sources:
    - "schema :S as { v: :Text | :Number integer between 0 and 5 }\n"
  root: "S"
  document: ":Schema(:S)\nv: :Number('3')\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    ok: true
```

---

## Test: nested unions stay fast

Without remembering what each alternative answered, this document would take minutes.

### Case description

```yaml
gesBlock: case
id: document-union-depth
schemaApi:
  sources:
    - "schema :A0 as { v: :Number integer between 0 and 1 }\nschema :B0 as { v: :Number integer between 0 and 1 }\nschema :A1 as { v: :A0 | :B0 }\nschema :B1 as { v: :A0 | :B0 }\nschema :A2 as { v: :A1 | :B1 }\nschema :B2 as { v: :A1 | :B1 }\nschema :A3 as { v: :A2 | :B2 }\nschema :B3 as { v: :A2 | :B2 }\nschema :A4 as { v: :A3 | :B3 }\nschema :B4 as { v: :A3 | :B3 }\nschema :A5 as { v: :A4 | :B4 }\nschema :B5 as { v: :A4 | :B4 }\nschema :A6 as { v: :A5 | :B5 }\nschema :B6 as { v: :A5 | :B5 }\nschema :A7 as { v: :A6 | :B6 }\nschema :B7 as { v: :A6 | :B6 }\nschema :A8 as { v: :A7 | :B7 }\nschema :B8 as { v: :A7 | :B7 }\nschema :A9 as { v: :A8 | :B8 }\nschema :B9 as { v: :A8 | :B8 }\nschema :A10 as { v: :A9 | :B9 }\nschema :B10 as { v: :A9 | :B9 }\nschema :A11 as { v: :A10 | :B10 }\nschema :B11 as { v: :A10 | :B10 }\nschema :A12 as { v: :A11 | :B11 }\nschema :B12 as { v: :A11 | :B11 }\nschema :A13 as { v: :A12 | :B12 }\nschema :B13 as { v: :A12 | :B12 }\nschema :A14 as { v: :A13 | :B13 }\nschema :B14 as { v: :A13 | :B13 }\nschema :A15 as { v: :A14 | :B14 }\nschema :B15 as { v: :A14 | :B14 }\nschema :A16 as { v: :A15 | :B15 }\nschema :B16 as { v: :A15 | :B15 }\nschema :A17 as { v: :A16 | :B16 }\nschema :B17 as { v: :A16 | :B16 }\nschema :A18 as { v: :A17 | :B17 }\nschema :B18 as { v: :A17 | :B17 }\nschema :A19 as { v: :A18 | :B18 }\nschema :B19 as { v: :A18 | :B18 }\nschema :A20 as { v: :A19 | :B19 }\nschema :B20 as { v: :A19 | :B19 }\nschema :A21 as { v: :A20 | :B20 }\nschema :B21 as { v: :A20 | :B20 }\nschema :A22 as { v: :A21 | :B21 }\nschema :B22 as { v: :A21 | :B21 }\nschema :A23 as { v: :A22 | :B22 }\nschema :B23 as { v: :A22 | :B22 }\nschema :A24 as { v: :A23 | :B23 }\nschema :B24 as { v: :A23 | :B23 }\nschema :S as { v: :A24 | :B24 }\n"
  root: "S"
  document: ":Schema(:S)\nv: [v: [v: [v: [v: [v: [v: [v: [v: [v: [v: [v: [v: [v: [v: [v: [v: [v: [v: [v: [v: [v: [v: [v: [v: [v: 5]]]]]]]]]]]]]]]]]]]]]]]]]\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    error:
      code: "document.fieldType"
      line: 2
      column: 4
```

---

## Test: a nested union that holds stays fast

The first alternative of each level passes `x` and fails on `y`, so the second must not check `x` again.

### Case description

```yaml
gesBlock: case
id: document-union-depth-holds
schemaApi:
  sources:
    - "schema :A0 as { y: :Boolean }\nschema :B0 as { y: :Number }\nschema :A1 as {\n  x: :A0 | :B0\n  y: :Boolean\n}\nschema :B1 as {\n  x: :A0 | :B0\n  y: :Number\n}\nschema :A2 as {\n  x: :A1 | :B1\n  y: :Boolean\n}\nschema :B2 as {\n  x: :A1 | :B1\n  y: :Number\n}\nschema :A3 as {\n  x: :A2 | :B2\n  y: :Boolean\n}\nschema :B3 as {\n  x: :A2 | :B2\n  y: :Number\n}\nschema :A4 as {\n  x: :A3 | :B3\n  y: :Boolean\n}\nschema :B4 as {\n  x: :A3 | :B3\n  y: :Number\n}\nschema :A5 as {\n  x: :A4 | :B4\n  y: :Boolean\n}\nschema :B5 as {\n  x: :A4 | :B4\n  y: :Number\n}\nschema :A6 as {\n  x: :A5 | :B5\n  y: :Boolean\n}\nschema :B6 as {\n  x: :A5 | :B5\n  y: :Number\n}\nschema :A7 as {\n  x: :A6 | :B6\n  y: :Boolean\n}\nschema :B7 as {\n  x: :A6 | :B6\n  y: :Number\n}\nschema :A8 as {\n  x: :A7 | :B7\n  y: :Boolean\n}\nschema :B8 as {\n  x: :A7 | :B7\n  y: :Number\n}\nschema :A9 as {\n  x: :A8 | :B8\n  y: :Boolean\n}\nschema :B9 as {\n  x: :A8 | :B8\n  y: :Number\n}\nschema :A10 as {\n  x: :A9 | :B9\n  y: :Boolean\n}\nschema :B10 as {\n  x: :A9 | :B9\n  y: :Number\n}\nschema :A11 as {\n  x: :A10 | :B10\n  y: :Boolean\n}\nschema :B11 as {\n  x: :A10 | :B10\n  y: :Number\n}\nschema :A12 as {\n  x: :A11 | :B11\n  y: :Boolean\n}\nschema :B12 as {\n  x: :A11 | :B11\n  y: :Number\n}\nschema :A13 as {\n  x: :A12 | :B12\n  y: :Boolean\n}\nschema :B13 as {\n  x: :A12 | :B12\n  y: :Number\n}\nschema :A14 as {\n  x: :A13 | :B13\n  y: :Boolean\n}\nschema :B14 as {\n  x: :A13 | :B13\n  y: :Number\n}\nschema :A15 as {\n  x: :A14 | :B14\n  y: :Boolean\n}\nschema :B15 as {\n  x: :A14 | :B14\n  y: :Number\n}\nschema :A16 as {\n  x: :A15 | :B15\n  y: :Boolean\n}\nschema :B16 as {\n  x: :A15 | :B15\n  y: :Number\n}\nschema :A17 as {\n  x: :A16 | :B16\n  y: :Boolean\n}\nschema :B17 as {\n  x: :A16 | :B16\n  y: :Number\n}\nschema :A18 as {\n  x: :A17 | :B17\n  y: :Boolean\n}\nschema :B18 as {\n  x: :A17 | :B17\n  y: :Number\n}\nschema :A19 as {\n  x: :A18 | :B18\n  y: :Boolean\n}\nschema :B19 as {\n  x: :A18 | :B18\n  y: :Number\n}\nschema :A20 as {\n  x: :A19 | :B19\n  y: :Boolean\n}\nschema :B20 as {\n  x: :A19 | :B19\n  y: :Number\n}\nschema :A21 as {\n  x: :A20 | :B20\n  y: :Boolean\n}\nschema :B21 as {\n  x: :A20 | :B20\n  y: :Number\n}\nschema :A22 as {\n  x: :A21 | :B21\n  y: :Boolean\n}\nschema :B22 as {\n  x: :A21 | :B21\n  y: :Number\n}\nschema :A23 as {\n  x: :A22 | :B22\n  y: :Boolean\n}\nschema :B23 as {\n  x: :A22 | :B22\n  y: :Number\n}\nschema :A24 as {\n  x: :A23 | :B23\n  y: :Boolean\n}\nschema :B24 as {\n  x: :A23 | :B23\n  y: :Number\n}\nschema :S as { v: :A24 | :B24 }\n"
  root: "S"
  document: ":Schema(:S)\nv: [x: [x: [x: [x: [x: [x: [x: [x: [x: [x: [x: [x: [x: [x: [x: [x: [x: [x: [x: [x: [x: [x: [x: [x: [y: 1], y: 1], y: 1], y: 1], y: 1], y: 1], y: 1], y: 1], y: 1], y: 1], y: 1], y: 1], y: 1], y: 1], y: 1], y: 1], y: 1], y: 1], y: 1], y: 1], y: 1], y: 1], y: 1], y: 1], y: 1]\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    ok: true
```

---

## Test: the first alternative of a union

Documents use the same schemas.

### Case description

```yaml
gesBlock: case
id: document-quantity
schemaApi:
  sources:
    - "schema :Unit as {\n  name: :Text\n  count: :Number integer between 1 and 12\n}\n\nschema :Loadout as {\n  units: :List(:Text | :Unit)\n  facing: :Quantity(°) between 0° and 360° | :Tag(#north, #east)\n  range: :Quantity(m) between 0m and limit optional\n}\nparameter limit: :Quantity(m)\n"
  document: ":Schema(:Loadout)\nunits: []\nfacing: 90°\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    ok: true
```

---

## Test: a missing field

Documents use the same schemas.

### Case description

```yaml
gesBlock: case
id: document-missing
schemaApi:
  sources:
    - "schema :Unit as {\n  name: :Text\n  count: :Number integer between 1 and 12\n}\n\nschema :Loadout as {\n  units: :List(:Text | :Unit)\n  facing: :Quantity(°) between 0° and 360° | :Tag(#north, #east)\n  range: :Quantity(m) between 0m and limit optional\n}\nparameter limit: :Quantity(m)\n"
  document: ":Schema(:Loadout)\nunits: []\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    error:
      code: "document.missingField"
      line: 1
      column: 1
```

---

## Test: an unknown field

Documents use the same schemas.

### Case description

```yaml
gesBlock: case
id: document-unknown
schemaApi:
  sources:
    - "schema :Unit as {\n  name: :Text\n  count: :Number integer between 1 and 12\n}\n\nschema :Loadout as {\n  units: :List(:Text | :Unit)\n  facing: :Quantity(°) between 0° and 360° | :Tag(#north, #east)\n  range: :Quantity(m) between 0m and limit optional\n}\nparameter limit: :Quantity(m)\n"
  document: ":Schema(:Loadout)\nunits: []\nfacing: #east\nextra: 1\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    error:
      code: "document.unknownField"
      line: 4
      column: 1
```

---

## Test: a value of no alternative

Documents use the same schemas.

### Case description

```yaml
gesBlock: case
id: document-type
schemaApi:
  sources:
    - "schema :Unit as {\n  name: :Text\n  count: :Number integer between 1 and 12\n}\n\nschema :Loadout as {\n  units: :List(:Text | :Unit)\n  facing: :Quantity(°) between 0° and 360° | :Tag(#north, #east)\n  range: :Quantity(m) between 0m and limit optional\n}\nparameter limit: :Quantity(m)\n"
  document: ":Schema(:Loadout)\nunits: []\nfacing: 'north'\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    error:
      code: "document.fieldType"
      line: 3
      column: 9
```

---

## Test: a list element of no alternative

Documents use the same schemas.

### Case description

```yaml
gesBlock: case
id: document-element
schemaApi:
  sources:
    - "schema :Unit as {\n  name: :Text\n  count: :Number integer between 1 and 12\n}\n\nschema :Loadout as {\n  units: :List(:Text | :Unit)\n  facing: :Quantity(°) between 0° and 360° | :Tag(#north, #east)\n  range: :Quantity(m) between 0m and limit optional\n}\nparameter limit: :Quantity(m)\n"
  document: ":Schema(:Loadout)\nunits: ['a', 5]\nfacing: 1°\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    error:
      code: "document.fieldType"
      line: 2
      column: 14
```

---

## Test: a nested field out of range

Documents use the same schemas.

### Case description

```yaml
gesBlock: case
id: document-nested
schemaApi:
  sources:
    - "schema :Unit as {\n  name: :Text\n  count: :Number integer between 1 and 12\n}\n\nschema :Loadout as {\n  units: :List(:Text | :Unit)\n  facing: :Quantity(°) between 0° and 360° | :Tag(#north, #east)\n  range: :Quantity(m) between 0m and limit optional\n}\nparameter limit: :Quantity(m)\n"
  document: ":Schema(:Unit)\nname: 'tank'\ncount: 13\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    error:
      code: "document.outOfRange"
      line: 3
      column: 8
```

---

## Test: a bound with a parameter value

Documents use the same schemas.

### Case description

```yaml
gesBlock: case
id: document-parameter
schemaApi:
  sources:
    - "schema :Unit as {\n  name: :Text\n  count: :Number integer between 1 and 12\n}\n\nschema :Loadout as {\n  units: :List(:Text | :Unit)\n  facing: :Quantity(°) between 0° and 360° | :Tag(#north, #east)\n  range: :Quantity(m) between 0m and limit optional\n}\nparameter limit: :Quantity(m)\n"
  document: ":Schema(:Loadout)\nunits: []\nfacing: 1°\nrange: 11m\n"
  parameters:
    limit: "10m"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    error:
      code: "document.outOfRange"
      line: 4
      column: 8
```

---

## Test: a bound without a parameter value is not checked

Documents use the same schemas.

### Case description

```yaml
gesBlock: case
id: document-unbound
schemaApi:
  sources:
    - "schema :Unit as {\n  name: :Text\n  count: :Number integer between 1 and 12\n}\n\nschema :Loadout as {\n  units: :List(:Text | :Unit)\n  facing: :Quantity(°) between 0° and 360° | :Tag(#north, #east)\n  range: :Quantity(m) between 0m and limit optional\n}\nparameter limit: :Quantity(m)\n"
  document: ":Schema(:Loadout)\nunits: []\nfacing: 1°\nrange: 11m\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    ok: true
```

---

## Test: units are not cast

Documents use the same schemas.

### Case description

```yaml
gesBlock: case
id: document-no-unit-cast
schemaApi:
  sources:
    - "schema :Unit as {\n  name: :Text\n  count: :Number integer between 1 and 12\n}\n\nschema :Loadout as {\n  units: :List(:Text | :Unit)\n  facing: :Quantity(°) between 0° and 360° | :Tag(#north, #east)\n  range: :Quantity(m) between 0m and limit optional\n}\nparameter limit: :Quantity(m)\n"
  document: ":Schema(:Loadout)\nunits: []\nfacing: #east\nrange: 11s\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    error:
      code: "document.fieldType"
      line: 4
      column: 8
```

---

## Test: a document that does not read

Documents use the same schemas.

### Case description

```yaml
gesBlock: case
id: document-reader-error
schemaApi:
  sources:
    - "schema :Unit as {\n  name: :Text\n  count: :Number integer between 1 and 12\n}\n\nschema :Loadout as {\n  units: :List(:Text | :Unit)\n  facing: :Quantity(°) between 0° and 360° | :Tag(#north, #east)\n  range: :Quantity(m) between 0m and limit optional\n}\nparameter limit: :Quantity(m)\n"
  document: ":Schema(:Loadout)\nunits: unquoted\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    error:
      code: "document.unquotedText"
      line: 2
      column: 8
```

---

## Test: a List element without bits

Only an element with bits lets the data limit the count.

### Case description

```yaml
gesBlock: case
id: error-list-element-tag
schemaApi:
  sources:
    - "schema :A as {\n  xs: :List(:Tag(#a)) max 8\n}\n"
  root: "A"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.notEncodable"
    line: 2
    column: 7
```

---

## Test: a List of a schema without fields

Such an element has no bits either.

### Case description

```yaml
gesBlock: case
id: error-list-element-empty
schemaApi:
  sources:
    - "schema :E as {\n}\nschema :A as {\n  xs: :List(:E) max 8\n}\n"
  root: "A"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.notEncodable"
    line: 4
    column: 7
```

---

## Test: a Message with another label

Labels are part of a Message's signature.

### Case description

```yaml
gesBlock: case
id: message-wrong-label
schemaApi:
  sources:
    - "message Hit(target: :Number integer between 0 and 15, _ damage: :Number integer between 0 and 7)\n"
  root: "Hit"
  encode:
    type: ":Message"
    message:
      name: "Hit"
      args:
        - name: "source"
          value:
            type: ":Number.int64"
            value: "9"
        - name: "_"
          value:
            type: ":Number.int64"
            value: "5"
      tags: []
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    error:
      code: "wire.typeMismatch"
      path: "target"
```

---

## Test: a Message with labels in another order

The order of labels is part of the signature as well.

### Case description

```yaml
gesBlock: case
id: message-wrong-order
schemaApi:
  sources:
    - "message Hit(target: :Number integer between 0 and 15, damage: :Number integer between 0 and 7)\n"
  root: "Hit"
  encode:
    type: ":Message"
    message:
      name: "Hit"
      args:
        - name: "damage"
          value:
            type: ":Number.int64"
            value: "5"
        - name: "target"
          value:
            type: ":Number.int64"
            value: "9"
      tags: []
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    error:
      code: "wire.typeMismatch"
      path: "target"
```

---

## Test: schemas that refer to each other many times

A small source with 2^30 paths is checked and hashed without following each one.

### Case description

```yaml
gesBlock: case
id: nesting-many-paths
schemaApi:
  sources:
    - "schema :S0 as {\n  a: :S1\n  b: :S1\n}\nschema :S1 as {\n  a: :S2\n  b: :S2\n}\nschema :S2 as {\n  a: :S3\n  b: :S3\n}\nschema :S3 as {\n  a: :S4\n  b: :S4\n}\nschema :S4 as {\n  a: :S5\n  b: :S5\n}\nschema :S5 as {\n  a: :S6\n  b: :S6\n}\nschema :S6 as {\n  a: :S7\n  b: :S7\n}\nschema :S7 as {\n  a: :S8\n  b: :S8\n}\nschema :S8 as {\n  a: :S9\n  b: :S9\n}\nschema :S9 as {\n  a: :S10\n  b: :S10\n}\nschema :S10 as {\n  a: :S11\n  b: :S11\n}\nschema :S11 as {\n  a: :S12\n  b: :S12\n}\nschema :S12 as {\n  a: :S13\n  b: :S13\n}\nschema :S13 as {\n  a: :S14\n  b: :S14\n}\nschema :S14 as {\n  a: :S15\n  b: :S15\n}\nschema :S15 as {\n  a: :S16\n  b: :S16\n}\nschema :S16 as {\n  a: :S17\n  b: :S17\n}\nschema :S17 as {\n  a: :S18\n  b: :S18\n}\nschema :S18 as {\n  a: :S19\n  b: :S19\n}\nschema :S19 as {\n  a: :S20\n  b: :S20\n}\nschema :S20 as {\n  a: :S21\n  b: :S21\n}\nschema :S21 as {\n  a: :S22\n  b: :S22\n}\nschema :S22 as {\n  a: :S23\n  b: :S23\n}\nschema :S23 as {\n  a: :S24\n  b: :S24\n}\nschema :S24 as {\n  a: :S25\n  b: :S25\n}\nschema :S25 as {\n  a: :S26\n  b: :S26\n}\nschema :S26 as {\n  a: :S27\n  b: :S27\n}\nschema :S27 as {\n  a: :S28\n  b: :S28\n}\nschema :S28 as {\n  a: :S29\n  b: :S29\n}\nschema :S29 as {\n  a: :S30\n  b: :S30\n}\nschema :S30 as {\n  f: :Boolean\n}\n"
  root: "S0"
  bind: false
```

### Expectation

```yaml
gesBlock: expect
schema:
  canonical: "gess1\nschema S0\nfield a S1\nfield b S1\nschema S1\nfield a S2\nfield b S2\nschema S2\nfield a S3\nfield b S3\nschema S3\nfield a S4\nfield b S4\nschema S4\nfield a S5\nfield b S5\nschema S5\nfield a S6\nfield b S6\nschema S6\nfield a S7\nfield b S7\nschema S7\nfield a S8\nfield b S8\nschema S8\nfield a S9\nfield b S9\nschema S9\nfield a S10\nfield b S10\nschema S10\nfield a S11\nfield b S11\nschema S11\nfield a S12\nfield b S12\nschema S12\nfield a S13\nfield b S13\nschema S13\nfield a S14\nfield b S14\nschema S14\nfield a S15\nfield b S15\nschema S15\nfield a S16\nfield b S16\nschema S16\nfield a S17\nfield b S17\nschema S17\nfield a S18\nfield b S18\nschema S18\nfield a S19\nfield b S19\nschema S19\nfield a S20\nfield b S20\nschema S20\nfield a S21\nfield b S21\nschema S21\nfield a S22\nfield b S22\nschema S22\nfield a S23\nfield b S23\nschema S23\nfield a S24\nfield b S24\nschema S24\nfield a S25\nfield b S25\nschema S25\nfield a S26\nfield b S26\nschema S26\nfield a S27\nfield b S27\nschema S27\nfield a S28\nfield b S28\nschema S28\nfield a S29\nfield b S29\nschema S29\nfield a S30\nfield b S30\nschema S30\nfield f Boolean\n"
  hash: "8aeca4d7e2aa94d4"
```

---

## Test: a layout of too many fields

Binding stops before it expands 2^25 fields.

### Case description

```yaml
gesBlock: case
id: error-too-many-fields
schemaApi:
  sources:
    - "schema :S0 as {\n  a: :S1\n  b: :S1\n}\nschema :S1 as {\n  a: :S2\n  b: :S2\n}\nschema :S2 as {\n  a: :S3\n  b: :S3\n}\nschema :S3 as {\n  a: :S4\n  b: :S4\n}\nschema :S4 as {\n  a: :S5\n  b: :S5\n}\nschema :S5 as {\n  a: :S6\n  b: :S6\n}\nschema :S6 as {\n  a: :S7\n  b: :S7\n}\nschema :S7 as {\n  a: :S8\n  b: :S8\n}\nschema :S8 as {\n  a: :S9\n  b: :S9\n}\nschema :S9 as {\n  a: :S10\n  b: :S10\n}\nschema :S10 as {\n  a: :S11\n  b: :S11\n}\nschema :S11 as {\n  a: :S12\n  b: :S12\n}\nschema :S12 as {\n  a: :S13\n  b: :S13\n}\nschema :S13 as {\n  a: :S14\n  b: :S14\n}\nschema :S14 as {\n  a: :S15\n  b: :S15\n}\nschema :S15 as {\n  a: :S16\n  b: :S16\n}\nschema :S16 as {\n  a: :S17\n  b: :S17\n}\nschema :S17 as {\n  a: :S18\n  b: :S18\n}\nschema :S18 as {\n  a: :S19\n  b: :S19\n}\nschema :S19 as {\n  a: :S20\n  b: :S20\n}\nschema :S20 as {\n  a: :S21\n  b: :S21\n}\nschema :S21 as {\n  a: :S22\n  b: :S22\n}\nschema :S22 as {\n  a: :S23\n  b: :S23\n}\nschema :S23 as {\n  a: :S24\n  b: :S24\n}\nschema :S24 as {\n  a: :S25\n  b: :S25\n}\nschema :S25 as {\n  f: :Boolean\n}\n"
  root: "S0"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.tooLarge"
```

---

## Test: a layout of too many fields without bits

The field limit also holds where the encoding stays empty.

### Case description

```yaml
gesBlock: case
id: error-too-many-empty-fields
schemaApi:
  sources:
    - "schema :S0 as {\n  a: :S1\n  b: :S1\n}\nschema :S1 as {\n  a: :S2\n  b: :S2\n}\nschema :S2 as {\n  a: :S3\n  b: :S3\n}\nschema :S3 as {\n  a: :S4\n  b: :S4\n}\nschema :S4 as {\n  a: :S5\n  b: :S5\n}\nschema :S5 as {\n  a: :S6\n  b: :S6\n}\nschema :S6 as {\n  a: :S7\n  b: :S7\n}\nschema :S7 as {\n  a: :S8\n  b: :S8\n}\nschema :S8 as {\n  a: :S9\n  b: :S9\n}\nschema :S9 as {\n  a: :S10\n  b: :S10\n}\nschema :S10 as {\n  a: :S11\n  b: :S11\n}\nschema :S11 as {\n  a: :S12\n  b: :S12\n}\nschema :S12 as {\n  a: :S13\n  b: :S13\n}\nschema :S13 as {\n  a: :S14\n  b: :S14\n}\nschema :S14 as {\n  a: :S15\n  b: :S15\n}\nschema :S15 as {\n  a: :S16\n  b: :S16\n}\nschema :S16 as {\n  a: :S17\n  b: :S17\n}\nschema :S17 as {\n  a: :S18\n  b: :S18\n}\nschema :S18 as {\n  a: :S19\n  b: :S19\n}\nschema :S19 as {\n  a: :S20\n  b: :S20\n}\nschema :S20 as {\n  a: :S21\n  b: :S21\n}\nschema :S21 as {\n  a: :S22\n  b: :S22\n}\nschema :S22 as {\n  a: :S23\n  b: :S23\n}\nschema :S23 as {\n  a: :S24\n  b: :S24\n}\nschema :S24 as {\n  a: :S25\n  b: :S25\n}\nschema :S25 as {\n  f: :Tag(#a)\n}\n"
  root: "S0"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.tooLarge"
```

---

## Test: the index of a large message set

A set of 257 messages has a 9-bit index, and the largest encoding counts it in bits.

### Case description

```yaml
gesBlock: case
id: message-set-wide-index
schemaApi:
  sources:
    - "message M0(x: :Number)\nmessage M1(x: :Number)\nmessage M2(x: :Number)\nmessage M3(x: :Number)\nmessage M4(x: :Number)\nmessage M5(x: :Number)\nmessage M6(x: :Number)\nmessage M7(x: :Number)\nmessage M8(x: :Number)\nmessage M9(x: :Number)\nmessage M10(x: :Number)\nmessage M11(x: :Number)\nmessage M12(x: :Number)\nmessage M13(x: :Number)\nmessage M14(x: :Number)\nmessage M15(x: :Number)\nmessage M16(x: :Number)\nmessage M17(x: :Number)\nmessage M18(x: :Number)\nmessage M19(x: :Number)\nmessage M20(x: :Number)\nmessage M21(x: :Number)\nmessage M22(x: :Number)\nmessage M23(x: :Number)\nmessage M24(x: :Number)\nmessage M25(x: :Number)\nmessage M26(x: :Number)\nmessage M27(x: :Number)\nmessage M28(x: :Number)\nmessage M29(x: :Number)\nmessage M30(x: :Number)\nmessage M31(x: :Number)\nmessage M32(x: :Number)\nmessage M33(x: :Number)\nmessage M34(x: :Number)\nmessage M35(x: :Number)\nmessage M36(x: :Number)\nmessage M37(x: :Number)\nmessage M38(x: :Number)\nmessage M39(x: :Number)\nmessage M40(x: :Number)\nmessage M41(x: :Number)\nmessage M42(x: :Number)\nmessage M43(x: :Number)\nmessage M44(x: :Number)\nmessage M45(x: :Number)\nmessage M46(x: :Number)\nmessage M47(x: :Number)\nmessage M48(x: :Number)\nmessage M49(x: :Number)\nmessage M50(x: :Number)\nmessage M51(x: :Number)\nmessage M52(x: :Number)\nmessage M53(x: :Number)\nmessage M54(x: :Number)\nmessage M55(x: :Number)\nmessage M56(x: :Number)\nmessage M57(x: :Number)\nmessage M58(x: :Number)\nmessage M59(x: :Number)\nmessage M60(x: :Number)\nmessage M61(x: :Number)\nmessage M62(x: :Number)\nmessage M63(x: :Number)\nmessage M64(x: :Number)\nmessage M65(x: :Number)\nmessage M66(x: :Number)\nmessage M67(x: :Number)\nmessage M68(x: :Number)\nmessage M69(x: :Number)\nmessage M70(x: :Number)\nmessage M71(x: :Number)\nmessage M72(x: :Number)\nmessage M73(x: :Number)\nmessage M74(x: :Number)\nmessage M75(x: :Number)\nmessage M76(x: :Number)\nmessage M77(x: :Number)\nmessage M78(x: :Number)\nmessage M79(x: :Number)\nmessage M80(x: :Number)\nmessage M81(x: :Number)\nmessage M82(x: :Number)\nmessage M83(x: :Number)\nmessage M84(x: :Number)\nmessage M85(x: :Number)\nmessage M86(x: :Number)\nmessage M87(x: :Number)\nmessage M88(x: :Number)\nmessage M89(x: :Number)\nmessage M90(x: :Number)\nmessage M91(x: :Number)\nmessage M92(x: :Number)\nmessage M93(x: :Number)\nmessage M94(x: :Number)\nmessage M95(x: :Number)\nmessage M96(x: :Number)\nmessage M97(x: :Number)\nmessage M98(x: :Number)\nmessage M99(x: :Number)\nmessage M100(x: :Number)\nmessage M101(x: :Number)\nmessage M102(x: :Number)\nmessage M103(x: :Number)\nmessage M104(x: :Number)\nmessage M105(x: :Number)\nmessage M106(x: :Number)\nmessage M107(x: :Number)\nmessage M108(x: :Number)\nmessage M109(x: :Number)\nmessage M110(x: :Number)\nmessage M111(x: :Number)\nmessage M112(x: :Number)\nmessage M113(x: :Number)\nmessage M114(x: :Number)\nmessage M115(x: :Number)\nmessage M116(x: :Number)\nmessage M117(x: :Number)\nmessage M118(x: :Number)\nmessage M119(x: :Number)\nmessage M120(x: :Number)\nmessage M121(x: :Number)\nmessage M122(x: :Number)\nmessage M123(x: :Number)\nmessage M124(x: :Number)\nmessage M125(x: :Number)\nmessage M126(x: :Number)\nmessage M127(x: :Number)\nmessage M128(x: :Number)\nmessage M129(x: :Number)\nmessage M130(x: :Number)\nmessage M131(x: :Number)\nmessage M132(x: :Number)\nmessage M133(x: :Number)\nmessage M134(x: :Number)\nmessage M135(x: :Number)\nmessage M136(x: :Number)\nmessage M137(x: :Number)\nmessage M138(x: :Number)\nmessage M139(x: :Number)\nmessage M140(x: :Number)\nmessage M141(x: :Number)\nmessage M142(x: :Number)\nmessage M143(x: :Number)\nmessage M144(x: :Number)\nmessage M145(x: :Number)\nmessage M146(x: :Number)\nmessage M147(x: :Number)\nmessage M148(x: :Number)\nmessage M149(x: :Number)\nmessage M150(x: :Number)\nmessage M151(x: :Number)\nmessage M152(x: :Number)\nmessage M153(x: :Number)\nmessage M154(x: :Number)\nmessage M155(x: :Number)\nmessage M156(x: :Number)\nmessage M157(x: :Number)\nmessage M158(x: :Number)\nmessage M159(x: :Number)\nmessage M160(x: :Number)\nmessage M161(x: :Number)\nmessage M162(x: :Number)\nmessage M163(x: :Number)\nmessage M164(x: :Number)\nmessage M165(x: :Number)\nmessage M166(x: :Number)\nmessage M167(x: :Number)\nmessage M168(x: :Number)\nmessage M169(x: :Number)\nmessage M170(x: :Number)\nmessage M171(x: :Number)\nmessage M172(x: :Number)\nmessage M173(x: :Number)\nmessage M174(x: :Number)\nmessage M175(x: :Number)\nmessage M176(x: :Number)\nmessage M177(x: :Number)\nmessage M178(x: :Number)\nmessage M179(x: :Number)\nmessage M180(x: :Number)\nmessage M181(x: :Number)\nmessage M182(x: :Number)\nmessage M183(x: :Number)\nmessage M184(x: :Number)\nmessage M185(x: :Number)\nmessage M186(x: :Number)\nmessage M187(x: :Number)\nmessage M188(x: :Number)\nmessage M189(x: :Number)\nmessage M190(x: :Number)\nmessage M191(x: :Number)\nmessage M192(x: :Number)\nmessage M193(x: :Number)\nmessage M194(x: :Number)\nmessage M195(x: :Number)\nmessage M196(x: :Number)\nmessage M197(x: :Number)\nmessage M198(x: :Number)\nmessage M199(x: :Number)\nmessage M200(x: :Number)\nmessage M201(x: :Number)\nmessage M202(x: :Number)\nmessage M203(x: :Number)\nmessage M204(x: :Number)\nmessage M205(x: :Number)\nmessage M206(x: :Number)\nmessage M207(x: :Number)\nmessage M208(x: :Number)\nmessage M209(x: :Number)\nmessage M210(x: :Number)\nmessage M211(x: :Number)\nmessage M212(x: :Number)\nmessage M213(x: :Number)\nmessage M214(x: :Number)\nmessage M215(x: :Number)\nmessage M216(x: :Number)\nmessage M217(x: :Number)\nmessage M218(x: :Number)\nmessage M219(x: :Number)\nmessage M220(x: :Number)\nmessage M221(x: :Number)\nmessage M222(x: :Number)\nmessage M223(x: :Number)\nmessage M224(x: :Number)\nmessage M225(x: :Number)\nmessage M226(x: :Number)\nmessage M227(x: :Number)\nmessage M228(x: :Number)\nmessage M229(x: :Number)\nmessage M230(x: :Number)\nmessage M231(x: :Number)\nmessage M232(x: :Number)\nmessage M233(x: :Number)\nmessage M234(x: :Number)\nmessage M235(x: :Number)\nmessage M236(x: :Number)\nmessage M237(x: :Number)\nmessage M238(x: :Number)\nmessage M239(x: :Number)\nmessage M240(x: :Number)\nmessage M241(x: :Number)\nmessage M242(x: :Number)\nmessage M243(x: :Number)\nmessage M244(x: :Number)\nmessage M245(x: :Number)\nmessage M246(x: :Number)\nmessage M247(x: :Number)\nmessage M248(x: :Number)\nmessage M249(x: :Number)\nmessage M250(x: :Number)\nmessage M251(x: :Number)\nmessage M252(x: :Number)\nmessage M253(x: :Number)\nmessage M254(x: :Number)\nmessage M255(x: :Number)\nmessage M256(x: :Number)\nmessages :Many as (M0, M1, M2, M3, M4, M5, M6, M7, M8, M9, M10, M11, M12, M13, M14, M15, M16, M17, M18, M19, M20, M21, M22, M23, M24, M25, M26, M27, M28, M29, M30, M31, M32, M33, M34, M35, M36, M37, M38, M39, M40, M41, M42, M43, M44, M45, M46, M47, M48, M49, M50, M51, M52, M53, M54, M55, M56, M57, M58, M59, M60, M61, M62, M63, M64, M65, M66, M67, M68, M69, M70, M71, M72, M73, M74, M75, M76, M77, M78, M79, M80, M81, M82, M83, M84, M85, M86, M87, M88, M89, M90, M91, M92, M93, M94, M95, M96, M97, M98, M99, M100, M101, M102, M103, M104, M105, M106, M107, M108, M109, M110, M111, M112, M113, M114, M115, M116, M117, M118, M119, M120, M121, M122, M123, M124, M125, M126, M127, M128, M129, M130, M131, M132, M133, M134, M135, M136, M137, M138, M139, M140, M141, M142, M143, M144, M145, M146, M147, M148, M149, M150, M151, M152, M153, M154, M155, M156, M157, M158, M159, M160, M161, M162, M163, M164, M165, M166, M167, M168, M169, M170, M171, M172, M173, M174, M175, M176, M177, M178, M179, M180, M181, M182, M183, M184, M185, M186, M187, M188, M189, M190, M191, M192, M193, M194, M195, M196, M197, M198, M199, M200, M201, M202, M203, M204, M205, M206, M207, M208, M209, M210, M211, M212, M213, M214, M215, M216, M217, M218, M219, M220, M221, M222, M223, M224, M225, M226, M227, M228, M229, M230, M231, M232, M233, M234, M235, M236, M237, M238, M239, M240, M241, M242, M243, M244, M245, M246, M247, M248, M249, M250, M251, M252, M253, M254, M255, M256)\n"
  root: "Many"
  encode:
    type: ":Message"
    message:
      name: "M256"
      args:
        - name: "x"
          value:
            type: ":Number.binary64"
            value: "1.5"
      tags: []
```

### Expectation

```yaml
gesBlock: expect
schema:
  maxByteCount: 10
  encoded:
    bytes: "00010000000000f07f00"
```

---

## Test: a message at the limit

Its largest encoding has exactly 10 MiB.

### Case description

```yaml
gesBlock: case
id: message-at-limit
schemaApi:
  sources:
    - "message A(t: :Text max 10485756, a: :Boolean, b: :Boolean, c: :Boolean, d: :Boolean, e: :Boolean, f: :Boolean, g: :Boolean, h: :Boolean)\nmessage B()\nmessages :S as (A, B)\n"
  root: "A"
```

### Expectation

```yaml
gesBlock: expect
schema:
  maxByteCount: 10485760
```

---

## Test: a message set beyond the limit

The index takes the encoding past 10 MiB, which is an error and not clamped.

### Case description

```yaml
gesBlock: case
id: error-set-too-large
schemaApi:
  sources:
    - "message A(t: :Text max 10485756, a: :Boolean, b: :Boolean, c: :Boolean, d: :Boolean, e: :Boolean, f: :Boolean, g: :Boolean, h: :Boolean)\nmessage B()\nmessages :S as (A, B)\n"
  root: "S"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.tooLarge"
    line: 3
    column: 1
```

---

## Test: the largest Int64 bound

Both bounds of Int64 can be written and encoded.

### Case description

```yaml
gesBlock: case
id: integer-bounds-largest
schemaApi:
  sources:
    - "schema :I as {\n  x: :Number integer between -9223372036854775808 and 9223372036854775807\n}\n"
  root: "I"
  encode:
    type: ":I"
    entries:
      - key: "x"
        value:
          type: ":Number.int64"
          value: "9223372036854775807"
  decode: "ffffffffffffffff"
```

### Expectation

```yaml
gesBlock: expect
schema:
  canonical: "gess1\nschema I\nfield x Number integer between -9223372036854775808e0 9223372036854775807e0\n"
  hash: "0ba837cb190f129b"
  encoded:
    bytes: "ffffffffffffffff"
  decoded:
    value:
      type: ":I"
      entries:
        - key: "x"
          value:
            type: ":Number.int64"
            value: "9223372036854775807"
    byteCount: 8
```

---

## Test: the smallest Int64 bound

The lower bound has index zero.

### Case description

```yaml
gesBlock: case
id: integer-bounds-smallest
schemaApi:
  sources:
    - "schema :I as {\n  x: :Number integer between -9223372036854775808 and 9223372036854775807\n}\n"
  root: "I"
  encode:
    type: ":I"
    entries:
      - key: "x"
        value:
          type: ":Number.int64"
          value: "-9223372036854775808"
  decode: "0000000000000000"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "0000000000000000"
  decoded:
    value:
      type: ":I"
      entries:
        - key: "x"
          value:
            type: ":Number.int64"
            value: "-9223372036854775808"
    byteCount: 8
```

---

## Test: an Int64 beyond its bound

One more than the largest Int64 cannot be read as an exact number.

### Case description

```yaml
gesBlock: case
id: error-integer-beyond-int64
schemaApi:
  sources:
    - "schema :I as {\n  x: :Number integer between 0 and 9223372036854775808\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.syntax"
    line: 2
    column: 36
```

---

## Test: bounds of equal magnitude that do not fit an Int64 together

The order of the bounds is exact.

### Case description

```yaml
gesBlock: case
id: bounds-compare-exactly
schemaApi:
  sources:
    - "schema :C as {\n  x: :Number between 9000000000000000001 and 95e17\n  y: :Number between -95e17 and -9000000000000000001\n}\n"
  root: "C"
```

### Expectation

```yaml
gesBlock: expect
schema:
  maxByteCount: 17
```

---

## Test: such bounds in the wrong order

The error has its stable code and position.

### Case description

```yaml
gesBlock: case
id: error-bounds-compare-exactly
schemaApi:
  sources:
    - "schema :C as {\n  x: :Number between 95e17 and 9000000000000000001\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidRange"
    line: 2
    column: 22
```

---

## Test: a parameter with a range

A parameter has only a kind and a unit.

### Case description

```yaml
gesBlock: case
id: error-parameter-range
schemaApi:
  sources:
    - "parameter w: :Quantity(m) between 0m and 1m\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.syntax"
    line: 1
    column: 14
```

---

## Test: a parameter with a kind of number

A parameter is not declared integer.

### Case description

```yaml
gesBlock: case
id: error-parameter-integer
schemaApi:
  sources:
    - "parameter w: :Number integer\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.syntax"
    line: 1
    column: 14
```

---

## Test: a dump of valid data

A field may be called error and a Text may hold line breaks: the text stays one line per field, and the data is valid.

### Case description

```yaml
gesBlock: case
id: dump-valid
schemaApi:
  sources:
    - "schema :D as {\n  error: :Boolean\n  t: :Text max 100\n  a: :Number\n  c: :Number between 0 and 1 step 0.25\n}\n"
  root: "D"
  dump: "1b780a6572726f722027795c090176830df4f521843e03"
```

### Expectation

```yaml
gesBlock: expect
schema:
  dump:
    text: "error bit=0 width=1 value=true\nt bit=1 width=111 value='x\\nerror \\'y\\\\\\t\\u{1}'\na bit=112 width=64 value=1.5e-7\nc bit=176 width=3 value=0.75\nbytes 23\n"
    valid: true
```

---

## Test: numbers in a dump

Every port spells a decoded number the same way.

### Case description

```yaml
gesBlock: case
id: dump-numbers
schemaApi:
  sources:
    - "schema :N as {\n  a: :Number\n  b: :Number\n  c: :Number\n  d: :Number\n  e: :Number\n  f: :Number\n  g: :Number\n  h: :Number\n  i: :Number\n  j: :Number\n}\n"
  root: "N"
  dump: "00003426f56b0c4350efe2d6e41a4b44408cb5781daf1544355800662deb417e0000000000e05e40fca9f1d24d62503f2d431cebe2361abff168e388b5f8e43e0000000000000000000000000000f07f"
```

### Expectation

```yaml
gesBlock: expect
schema:
  dump:
    text: "a bit=0 width=64 value=1000000000000000\nb bit=64 width=64 value=1e+21\nc bit=128 width=64 value=100000000000000000000\nd bit=192 width=64 value=1.5e+300\ne bit=256 width=64 value=123.5\nf bit=320 width=64 value=0.001\ng bit=384 width=64 value=-0.0001\nh bit=448 width=64 value=1e-5\ni bit=512 width=64 value=0\nj bit=576 width=64 value=Infinity\nbytes 80\n"
    valid: true
```

---

## Test: a dump of invalid data

The first invalid field ends the text, and the result says that the data is invalid.

### Case description

```yaml
gesBlock: case
id: dump-invalid
schemaApi:
  sources:
    - "schema :N as {\n  a: :Boolean\n  b: :Number\n}\n"
  root: "N"
  dump: "01"
```

### Expectation

```yaml
gesBlock: expect
schema:
  dump:
    text: "a bit=0 width=1 value=true\nerror wire.truncated path=b bit=1\n"
    valid: false
```

---

## Test: List types at the nesting limit

32 levels are read and hashed.

### Case description

```yaml
gesBlock: case
id: list-nesting-at-limit
schemaApi:
  sources:
    - "schema :A as {\n  x: :List(:List(:List(:List(:List(:List(:List(:List(:List(:List(:List(:List(:List(:List(:List(:List(:List(:List(:List(:List(:List(:List(:List(:List(:List(:List(:List(:List(:List(:List(:List(:List(:Boolean))))))))))))))))))))))))))))))))\n}\n"
  root: "A"
  bind: false
```

### Expectation

```yaml
gesBlock: expect
schema:
  canonical: "gess1\nschema A\nfield x List(List(List(List(List(List(List(List(List(List(List(List(List(List(List(List(List(List(List(List(List(List(List(List(List(List(List(List(List(List(List(List(Boolean))))))))))))))))))))))))))))))))\n"
  hash: "94f316e7b50d9b64"
```

---

## Test: List types beyond the nesting limit

The 33rd level is an error with its position, not an exhausted stack.

### Case description

```yaml
gesBlock: case
id: error-list-nesting
schemaApi:
  sources:
    - "schema :A as {\n  x: :List(:List(:List(:List(:List(:List(:List(:List(:List(:List(:List(:List(:List(:List(:List(:List(:List(:List(:List(:List(:List(:List(:List(:List(:List(:List(:List(:List(:List(:List(:List(:List(:List(:Boolean)))))))))))))))))))))))))))))))))\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.cycle"
    line: 2
    column: 198
```

---

## Test: a document that names one of several sources

The root comes from the named source alone.

### Case description

```yaml
gesBlock: case
id: document-file-source
schemaApi:
  sources:
    - "schema :A as {\n  a: :Boolean\n}\n"
    - "schema :B as {\n  b: :Boolean\n}\n"
  document: ":Schema('schema1.gess')\nb: true\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    ok: true
```

---

## Test: a document that names an unknown source

The name must match a source exactly.

### Case description

```yaml
gesBlock: case
id: document-file-unknown
schemaApi:
  sources:
    - "schema :A as {\n  a: :Boolean\n}\n"
    - "schema :B as {\n  b: :Boolean\n}\n"
  document: ":Schema('typo.gess')\nb: true\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    error:
      code: "schema.unknownName"
```

---

## Test: a root schema that a message refers to

Only schemas of the source decide what its root is.

### Case description

```yaml
gesBlock: case
id: document-file-message-reference
schemaApi:
  sources:
    - "schema :P as {\n  p: :Boolean\n}\nmessage M(p: :P)\n"
  document: ":Schema('schema0.gess')\np: true\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    ok: true
```

---

## Test: a fractional bound that is infinite in binary64

The field compares in binary64, so its bound must stay finite there.

### Case description

```yaml
gesBlock: case
id: error-bound-infinite
schemaApi:
  sources:
    - "schema :F as {\n  x: :Number between 0 and 1e9999\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.tooWide"
    line: 2
    column: 28
```

---

## Test: a fractional bound that becomes zero in binary64

A nonzero bound must not lose its meaning.

### Case description

```yaml
gesBlock: case
id: error-bound-vanishing
schemaApi:
  sources:
    - "schema :F as {\n  x: :Number between 1e-9999 and 1\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.tooWide"
    line: 2
    column: 22
```

---

## Test: a decimal point directly before an exponent

Schema literals use the number grammar of documents and scripts.

### Case description

```yaml
gesBlock: case
id: error-literal-point-exponent
schemaApi:
  sources:
    - "schema :D as {\n  x: :Number between 1.e0 and 2\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.syntax"
    line: 2
    column: 22
```

---

## Test: a parameter value that is no literal

Binding reports it like other source errors.

### Case description

```yaml
gesBlock: case
id: error-parameter-literal
schemaApi:
  sources:
    - "parameter w: :Quantity(m)\nschema :O as {\n  x: :Quantity(m) between 0m and w\n}\n"
  root: "O"
  parameters:
    w: "abc"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.syntax"
```

---

## Test: a document with a parameter of another unit

A given value must have the declared unit.

### Case description

```yaml
gesBlock: case
id: document-parameter-unit
schemaApi:
  sources:
    - "parameter w: :Quantity(m)\nschema :A as {\n  d: :Quantity(m) between 0m and w\n}\n"
  parameters:
    w: "10s"
  document: ":Schema(:A)\nd: 7m\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    error:
      code: "schema.unitMismatch"
```

---

## Test: a document with a parameter of another kind

A plain number literal is no Percentage.

### Case description

```yaml
gesBlock: case
id: document-parameter-kind
schemaApi:
  sources:
    - "parameter w: :Percentage\nschema :A as {\n  d: :Percentage between 0% and w\n}\n"
  parameters:
    w: "10"
  document: ":Schema(:A)\nd: 7%\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    error:
      code: "schema.unitMismatch"
```

---

## Test: a Record form with labels in another order

The labels decide, not the written order.

### Case description

```yaml
gesBlock: case
id: document-record-labels
schemaApi:
  sources:
    - "schema :A as {\n  u: :U\n}\nschema :U as {\n  a: :Boolean\n}\n"
  document: ":Schema(:A)\nu: :Record(fields: [a: true], type: 'U')\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    ok: true
```

---

## Test: a missing field in a Record form

It is reported where the value starts, as for a Map.

### Case description

```yaml
gesBlock: case
id: document-record-missing
schemaApi:
  sources:
    - "schema :A as {\n  u: :U\n}\nschema :U as {\n  a: :Boolean\n}\n"
  document: ":Schema(:A)\nu: :Record('U', [:])\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    error:
      code: "document.missingField"
      line: 2
      column: 4
```

---

## Test: a Map key that only looks like a field name

Keys compare by Unicode scalars: the Kelvin sign is no K.

### Case description

```yaml
gesBlock: case
id: map-key-scalars
schemaApi:
  sources:
    - "schema :A as {\n  maxK: :Boolean\n}\n"
  root: "A"
  encode:
    type: ":Map"
    entries:
      - key: "maxK"
        value:
          type: ":Boolean"
          value: true
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    error:
      code: "wire.missingField"
      path: "maxK"
```

---

## Test: a fractional parameter value

The numeric overload converts to the same exact decimal in every port, so the binding hash is equal.

### Case description

```yaml
gesBlock: case
id: parameter-binary64-fraction
schemaApi:
  sources:
    - "parameter w: :Quantity(m)\nschema :A as {\n  x: :Quantity(m) between 0m and w\n}\n"
  root: "A"
  parameters:
    w:
      type: ":Quantity.binary64"
      value: "1234.5678"
      unit: "m"
```

### Expectation

```yaml
gesBlock: expect
schema:
  hash: "02c695ff0e28e962"
  bindingHash: "a8e6ca97100ee448"
```

---

## Test: a parameter value without an exact binary form

The numeric overload converts to the same exact decimal in every port, so the binding hash is equal.

### Case description

```yaml
gesBlock: case
id: parameter-binary64-tenth
schemaApi:
  sources:
    - "parameter w: :Quantity(m)\nschema :A as {\n  x: :Quantity(m) between 0m and w\n}\n"
  root: "A"
  parameters:
    w:
      type: ":Quantity.binary64"
      value: "0.1"
      unit: "m"
```

### Expectation

```yaml
gesBlock: expect
schema:
  hash: "02c695ff0e28e962"
  bindingHash: "6e7b47d3172da156"
```

---

## Test: a small parameter value

The numeric overload converts to the same exact decimal in every port, so the binding hash is equal.

### Case description

```yaml
gesBlock: case
id: parameter-binary64-small
schemaApi:
  sources:
    - "parameter w: :Quantity(m)\nschema :A as {\n  x: :Quantity(m) between 0m and w\n}\n"
  root: "A"
  parameters:
    w:
      type: ":Quantity.binary64"
      value: "1.5e-7"
      unit: "m"
```

### Expectation

```yaml
gesBlock: expect
schema:
  hash: "02c695ff0e28e962"
  bindingHash: "66224e01e49cbada"
```

---

## Test: the smallest binary64 parameter value

The numeric overload converts to the same exact decimal in every port, so the binding hash is equal.

### Case description

```yaml
gesBlock: case
id: parameter-binary64-smallest
schemaApi:
  sources:
    - "parameter w: :Quantity(m)\nschema :A as {\n  x: :Quantity(m) between 0m and w\n}\n"
  root: "A"
  parameters:
    w:
      type: ":Quantity.binary64"
      value: "5e-324"
      unit: "m"
```

### Expectation

```yaml
gesBlock: expect
schema:
  hash: "02c695ff0e28e962"
  bindingHash: "c6bf11be4f2ef79a"
```

---

## Test: a parameter value beyond Int64

The numeric overload converts to the same exact decimal in every port, so the binding hash is equal.

### Case description

```yaml
gesBlock: case
id: parameter-binary64-large
schemaApi:
  sources:
    - "parameter w: :Quantity(m)\nschema :A as {\n  x: :Quantity(m) between 0m and w\n}\n"
  root: "A"
  parameters:
    w:
      type: ":Quantity.binary64"
      value: "1e21"
      unit: "m"
```

### Expectation

```yaml
gesBlock: expect
schema:
  hash: "02c695ff0e28e962"
  bindingHash: "470f12d391db3f8f"
```

---

## Test: the largest binary64 parameter value

The numeric overload converts to the same exact decimal in every port, so the binding hash is equal.

### Case description

```yaml
gesBlock: case
id: parameter-binary64-largest
schemaApi:
  sources:
    - "parameter w: :Quantity(m)\nschema :A as {\n  x: :Quantity(m) between 0m and w\n}\n"
  root: "A"
  parameters:
    w:
      type: ":Quantity.binary64"
      value: "1.7976931348623157e308"
      unit: "m"
```

### Expectation

```yaml
gesBlock: expect
schema:
  hash: "02c695ff0e28e962"
  bindingHash: "03fd9a096d7870b8"
```

---

## Test: a parameter value with seventeen digits

The numeric overload converts to the same exact decimal in every port, so the binding hash is equal.

### Case description

```yaml
gesBlock: case
id: parameter-binary64-digits
schemaApi:
  sources:
    - "parameter w: :Quantity(m)\nschema :A as {\n  x: :Quantity(m) between 0m and w\n}\n"
  root: "A"
  parameters:
    w:
      type: ":Quantity.binary64"
      value: "123456789.12345678"
      unit: "m"
```

### Expectation

```yaml
gesBlock: expect
schema:
  hash: "02c695ff0e28e962"
  bindingHash: "9116312ff29cbabb"
```

---

## Test: an integer parameter value

The numeric overload converts to the same exact decimal in every port, so the binding hash is equal.

### Case description

```yaml
gesBlock: case
id: parameter-int64
schemaApi:
  sources:
    - "parameter w: :Quantity(m)\nschema :A as {\n  x: :Quantity(m) between 0m and w\n}\n"
  root: "A"
  parameters:
    w:
      type: ":Quantity.int64"
      value: "5000"
      unit: "m"
```

### Expectation

```yaml
gesBlock: expect
schema:
  hash: "02c695ff0e28e962"
  bindingHash: "2181c527c72b5a8b"
```

---

## Test: an integer parameter value beyond binary64

The numeric overload converts to the same exact decimal in every port, so the binding hash is equal.

### Case description

```yaml
gesBlock: case
id: parameter-int64-exact
schemaApi:
  sources:
    - "parameter w: :Quantity(m)\nschema :A as {\n  x: :Quantity(m) between 0m and w\n}\n"
  root: "A"
  parameters:
    w:
      type: ":Quantity.int64"
      value: "9007199254740993"
      unit: "m"
```

### Expectation

```yaml
gesBlock: expect
schema:
  hash: "02c695ff0e28e962"
  bindingHash: "9b26348c249b7cf1"
```

---

## Test: a plain number for a Percentage parameter

The numeric overload converts to the same exact decimal in every port, so the binding hash is equal.

### Case description

```yaml
gesBlock: case
id: parameter-ratio
schemaApi:
  sources:
    - "parameter w: :Percentage\nschema :A as {\n  x: :Percentage between 0% and w\n}\n"
  root: "A"
  parameters:
    w:
      type: ":Number.binary64"
      value: "0.125"
```

### Expectation

```yaml
gesBlock: expect
schema:
  hash: "f3f58225236af4fc"
  bindingHash: "4dc8526180e6c971"
```

---

## Test: the description of a schema

Fields, markers, limits, tags, axes, lists, unions and references are read-only data.

### Case description

```yaml
gesBlock: case
id: describe-schema
schemaApi:
  sources:
    - "constant $full be 135\nparameter w: :Quantity(m)\nschema :Mount as {\n  size: :Number integer between 0 and 3\n}\nschema :Unit as {\n  id: :Number integer between 0 and 65535 key\n  x: :Quantity(m) between -2.5m and w step 1cm delta within 5m\n  a: :Quantity(°) wrapping between 0° and 360° step 0.5°\n  hp: :Number integer between 0 and $full optional\n  state: :Tag(#idle, #dead)\n  any: :Tag\n  name: :Text max 8\n  p: :Point(m) {\n    z between 0m and 1m\n    x integer wrapping between 0m and 8m\n  }\n  v: :Vector\n  m: :List(:Mount) max 2\n  u: :Text | :Boolean\n  r: :Percentage\n}\n"
  root: "Unit"
  bind: false
  describe: true
```

### Expectation

```yaml
gesBlock: expect
schema:
  description:
    name: "Unit"
    kind: "Schema"
    parameters:
      - name: "w"
        kind: "Quantity"
        unit: "meter"
    fields:
      - name: "id"
        key: true
        alternatives:
          - kind: "Number"
            integer: true
            lower: "0e0"
            upper: "65535e0"
      - name: "x"
        alternatives:
          - kind: "Quantity"
            unit: "meter"
            lower: "-25e-1"
            upper: "parameter w"
            step: "1e-2"
            delta: "5e0"
      - name: "a"
        alternatives:
          - kind: "Quantity"
            unit: "degree"
            wrapping: true
            lower: "0e0"
            upper: "36e1"
            step: "5e-1"
      - name: "hp"
        optional: true
        alternatives:
          - kind: "Number"
            integer: true
            lower: "0e0"
            upper: "135e0"
      - name: "state"
        alternatives:
          - kind: "Tag"
            tags:
              - "idle"
              - "dead"
      - name: "any"
        alternatives:
          - kind: "Tag"
      - name: "name"
        alternatives:
          - kind: "Text"
            max: 8
      - name: "p"
        alternatives:
          - kind: "Point"
            unit: "meter"
            axes:
              - name: "x"
                integer: true
                wrapping: true
                lower: "0e0"
                upper: "8e0"
              - name: "z"
                lower: "0e0"
                upper: "1e0"
      - name: "v"
        alternatives:
          - kind: "Vector"
      - name: "m"
        alternatives:
          - kind: "List"
            max: 2
            element:
              - kind: "Reference"
                schema: "Mount"
      - name: "u"
        alternatives:
          - kind: "Text"
          - kind: "Boolean"
      - name: "r"
        alternatives:
          - kind: "Percentage"
```

---

## Test: the description of a message

Arguments keep their order and labels, and tags their declared order.

### Case description

```yaml
gesBlock: case
id: describe-message
schemaApi:
  sources:
    - "message Hit(target: :Number integer between 0 and 15, _ damage: :Number optional) tags (#splash, #critical)\n"
  root: "Hit"
  bind: false
  describe: true
```

### Expectation

```yaml
gesBlock: expect
schema:
  description:
    name: "Hit"
    kind: "Message"
    fields:
      - name: "target"
        alternatives:
          - kind: "Number"
            integer: true
            lower: "0e0"
            upper: "15e0"
      - name: "damage"
        unlabeled: true
        optional: true
        alternatives:
          - kind: "Number"
    tags:
      - "splash"
      - "critical"
```

---

## Test: the description of a message set

Members keep their declared order, and the parameters of all members are listed by name.

### Case description

```yaml
gesBlock: case
id: describe-messages
schemaApi:
  sources:
    - "parameter b: :Number\nparameter a: :Percentage\nmessage M(x: :Number between 0 and b)\nmessage N(y: :Percentage between 0% and a)\nmessages :S as (N, M)\n"
  root: "S"
  bind: false
  describe: true
```

### Expectation

```yaml
gesBlock: expect
schema:
  description:
    name: "S"
    kind: "Messages"
    parameters:
      - name: "a"
        kind: "Percentage"
      - name: "b"
        kind: "Number"
    members:
      - "N"
      - "M"
```

---

## Test: an axis block without an axis

The grammar requires at least one axis.

### Case description

```yaml
gesBlock: case
id: error-empty-axis-block
schemaApi:
  sources:
    - "schema :A as {\n  p: :Point {}\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.syntax"
    line: 2
    column: 6
```

---

## Test: a parameter of two kinds is found when reading

The conflict needs no binding.

### Case description

```yaml
gesBlock: case
id: error-parameter-kinds-reading
schemaApi:
  sources:
    - "parameter w: :Quantity(m)\nschema :A as {\n  x: :Quantity(m) between 0m and w\n}\n"
    - "parameter w: :Number\nschema :B as {\n  a: :A\n  y: :Number between 0 and w\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.duplicateName"
```

---

## Test: type names with underscores

A schema reference and its canonical form keep the whole name.

### Case description

```yaml
gesBlock: case
id: underscore-type-names
schemaApi:
  sources:
    - "schema :Unit_Mount as { slot: :Number integer between 0 and 7 }\nschema :Unit_Weapon as { mount: :Unit_Mount }\n"
  root: "Unit_Weapon"
  encode:
    type: ":Unit_Weapon"
    entries:
      - key: "mount"
        value:
          type: ":Unit_Mount"
          entries:
            - key: "slot"
              value:
                type: ":Number.int64"
                value: "5"
```

### Expectation

```yaml
gesBlock: expect
schema:
  canonical: "gess1\nschema Unit_Weapon\nfield mount Unit_Mount\nschema Unit_Mount\nfield slot Number integer between 0e0 7e0\n"
  encoded:
    bytes: "05"
```

---

## Test: a union alternative with an underscore in a path

The field after the alternative stays apart from its type name.

### Case description

```yaml
gesBlock: case
id: underscore-union-path
schemaApi:
  sources:
    - "schema :Unit_Mount as { x: :Number integer between 0 and 1 }\nschema :S as { a: :Unit_Mount | :Text max 1 }\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "a"
        value:
          type: ":Map"
          entries:
            - key: "x"
              value:
                type: ":Number.int64"
                value: "5"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    error:
      code: "wire.outOfRange"
      path: "a:Unit_Mount.x"
```

---

## Test: the invalid underscore name in schema :Unit_1

A `_` joins two segments that each start uppercase.

### Case description

```yaml
gesBlock: case
id: error-underscore-name-1
schemaApi:
  sources:
    - "schema :Unit_1 as { a: :Boolean }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.syntax"
```

---

## Test: the invalid underscore name in schema :Unit__Weapon

A `_` joins two segments that each start uppercase.

### Case description

```yaml
gesBlock: case
id: error-underscore-name-2
schemaApi:
  sources:
    - "schema :Unit__Weapon as { a: :Boolean }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.syntax"
```

---

## Test: the invalid underscore name in schema :Unit_

A `_` joins two segments that each start uppercase.

### Case description

```yaml
gesBlock: case
id: error-underscore-name-3
schemaApi:
  sources:
    - "schema :Unit_ as { a: :Boolean }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.syntax"
```

---

## Test: the invalid underscore name in schema :Unit_weapon

A `_` joins two segments that each start uppercase.

### Case description

```yaml
gesBlock: case
id: error-underscore-name-4
schemaApi:
  sources:
    - "schema :Unit_weapon as { a: :Boolean }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.syntax"
```

---

## Test: the invalid underscore name in message Unit__Fire()

A `_` joins two segments that each start uppercase.

### Case description

```yaml
gesBlock: case
id: error-underscore-name-5
schemaApi:
  sources:
    - "message Unit__Fire()\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.syntax"
```

---

## Test: the invalid underscore name in message Unit_1()

A `_` joins two segments that each start uppercase.

### Case description

```yaml
gesBlock: case
id: error-underscore-name-6
schemaApi:
  sources:
    - "message Unit_1()\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.syntax"
```

---

## Test: the invalid underscore name in a message set

A `_` joins two segments that each start uppercase.

### Case description

```yaml
gesBlock: case
id: error-underscore-name-7
schemaApi:
  sources:
    - "messages :All as (Unit__Fire)\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.syntax"
```

---

## Test: message and message set names with underscores

A message set finds its members by the whole name, and the index comes first.

### Case description

```yaml
gesBlock: case
id: underscore-message-names
schemaApi:
  sources:
    - "message Unit_Fire(target: :Number integer between 0 and 15)\nmessage Unit_Stop()\nmessages :Unit_Events as (Unit_Fire, Unit_Stop)\n"
  root: "Unit_Events"
  encode:
    type: ":Message"
    message:
      name: "Unit_Fire"
      args:
        - name: "target"
          value:
            type: ":Number.int64"
            value: "9"
  decode: "12"
```

### Expectation

```yaml
gesBlock: expect
schema:
  canonical: "gess1\nmessages Unit_Events\nmember Unit_Fire\nmember Unit_Stop\nmessage Unit_Fire\nargument target Number integer between 0e0 15e0\nmessage Unit_Stop\n"
  encoded:
    bytes: "12"
  decoded:
    value:
      type: ":Message"
      message:
        name: "Unit_Fire"
        args:
          - name: "target"
            value:
              type: ":Number.int64"
              value: "9"
    byteCount: 1
```

---

## Test: nothing in a required field is no value for the wire

nothing in a required field is no value for the wire.

### Case description

```yaml
gesBlock: case
id: wire-nothing-field
schemaApi:
  sources:
    - "schema :S as { v: :Number integer between 0 and 4 }\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "v"
        value:
          type: ":Nothing"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    error:
      code: "wire.nothingValue"
      path: "v"
```


---

## Test: nothing as a list element without a Nothing alternative is no value for the wire

nothing as a list element without a Nothing alternative is no value for the wire.

### Case description

```yaml
gesBlock: case
id: wire-nothing-list-element
schemaApi:
  sources:
    - "schema :S as { v: :List(:Number integer between 0 and 4) max 2 }\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "v"
        value:
          type: ":List"
          items:
            - type: ":Number.int64"
              value: "1"
            - type: ":Nothing"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    error:
      code: "wire.nothingValue"
      path: "v[]"
```


---

## Test: nothing in a union without a Nothing alternative is no value for the wire

nothing in a union without a Nothing alternative is no value for the wire.

### Case description

```yaml
gesBlock: case
id: wire-nothing-union
schemaApi:
  sources:
    - "schema :S as { v: :Text max 2 | :Boolean }\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "v"
        value:
          type: ":Nothing"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    error:
      code: "wire.nothingValue"
      path: "v"
```


---

## Test: a union field without a Nothing alternative needs its key

a union field without a Nothing alternative needs its key.

### Case description

```yaml
gesBlock: case
id: wire-missing-union
schemaApi:
  sources:
    - "schema :S as { v: :Text max 2 | :Boolean }\n"
  root: "S"
  encode:
    type: ":S"
    entries: []
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    error:
      code: "wire.missingField"
      path: "v"
```


---

## Test: nothing in a required message argument is no value for the wire

nothing in a required message argument is no value for the wire.

### Case description

```yaml
gesBlock: case
id: wire-nothing-argument
schemaApi:
  sources:
    - "message S(a: :Boolean)\n"
  root: "S"
  encode:
    type: ":Message"
    message:
      name: "S"
      args:
        - name: "a"
          value:
            type: ":Nothing"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    error:
      code: "wire.nothingValue"
      path: "a"
```


---

## Test: nothing in a required document field is no value

nothing in a required document field is no value.

### Case description

```yaml
gesBlock: case
id: document-nothing-required
schemaApi:
  sources:
    - "schema :S as { v: :Number integer between 0 and 4 }\n"
  document: ":Schema(:S)\nv: nothing\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    error:
      code: "document.fieldType"
      line: 2
      column: 4
```

---

## Test: an unchanged record writes its key and zero change bits

Delta: an unchanged record writes its key and zero change bits.

### Case description

```yaml
gesBlock: case
id: delta-unchanged
schemaApi:
  sources:
    - "schema :Drone as {\n  id: :Number integer between 0 and 255 key\n  x: :Quantity(m) between -200m and 200m step 5cm delta within 1.55m\n  y: :Quantity(m) between -200m and 200m step 5cm delta within 1.55m\n  heading: :Quantity(°) wrapping between 0° and 360° step 0.5° delta within 15.5°\n}\n"
  root: "Drone"
  encode:
    type: ":Drone"
    entries:
      - key: "id"
        value:
          type: ":Number.int64"
          value: "7"
      - key: "x"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":meter"
      - key: "y"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":meter"
      - key: "heading"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":degree"
  baseline:
    type: ":Drone"
    entries:
      - key: "id"
        value:
          type: ":Number.int64"
          value: "7"
      - key: "x"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":meter"
      - key: "y"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":meter"
      - key: "heading"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":degree"
  decode: "0700"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "0700"
  decoded:
    value:
      type: ":Drone"
      entries:
        - key: "id"
          value:
            type: ":Number.int64"
            value: "7"
        - key: "x"
          value:
            type: ":Quantity.int64"
            value: "0"
            unit: ":meter"
        - key: "y"
          value:
            type: ":Quantity.int64"
            value: "0"
            unit: ":meter"
        - key: "heading"
          value:
            type: ":Quantity.int64"
            value: "0"
            unit: ":degree"
    byteCount: 2
  maxByteCount: 7
```


---

## Test: a small move uses the range form

Delta: a small move uses the range form.

### Case description

```yaml
gesBlock: case
id: delta-range-both
schemaApi:
  sources:
    - "schema :Drone as {\n  id: :Number integer between 0 and 255 key\n  x: :Quantity(m) between -200m and 200m step 5cm delta within 1.55m\n  y: :Quantity(m) between -200m and 200m step 5cm delta within 1.55m\n  heading: :Quantity(°) wrapping between 0° and 360° step 0.5° delta within 15.5°\n}\n"
  root: "Drone"
  encode:
    type: ":Drone"
    entries:
      - key: "id"
        value:
          type: ":Number.int64"
          value: "7"
      - key: "x"
        value:
          type: ":Quantity.int64"
          value: "1"
          unit: ":meter"
      - key: "y"
        value:
          type: ":Quantity.binary64"
          value: "-0.5"
          unit: ":meter"
      - key: "heading"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":degree"
  baseline:
    type: ":Drone"
    entries:
      - key: "id"
        value:
          type: ":Number.int64"
          value: "7"
      - key: "x"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":meter"
      - key: "y"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":meter"
      - key: "heading"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":degree"
  decode: "0733ab00"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "0733ab00"
  decoded:
    value:
      type: ":Drone"
      entries:
        - key: "id"
          value:
            type: ":Number.int64"
            value: "7"
        - key: "x"
          value:
            type: ":Quantity.int64"
            value: "1"
            unit: ":meter"
        - key: "y"
          value:
            type: ":Quantity.binary64"
            value: "-0.5"
            unit: ":meter"
        - key: "heading"
          value:
            type: ":Quantity.int64"
            value: "0"
            unit: ":degree"
    byteCount: 4
```


---

## Test: a wrapping field takes the shorter way forward

Delta: a wrapping field takes the shorter way forward.

### Case description

```yaml
gesBlock: case
id: delta-wrap-forward
schemaApi:
  sources:
    - "schema :Drone as {\n  id: :Number integer between 0 and 255 key\n  x: :Quantity(m) between -200m and 200m step 5cm delta within 1.55m\n  y: :Quantity(m) between -200m and 200m step 5cm delta within 1.55m\n  heading: :Quantity(°) wrapping between 0° and 360° step 0.5° delta within 15.5°\n}\n"
  root: "Drone"
  encode:
    type: ":Drone"
    entries:
      - key: "id"
        value:
          type: ":Number.int64"
          value: "7"
      - key: "x"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":meter"
      - key: "y"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":meter"
      - key: "heading"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":degree"
  baseline:
    type: ":Drone"
    entries:
      - key: "id"
        value:
          type: ":Number.int64"
          value: "7"
      - key: "x"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":meter"
      - key: "y"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":meter"
      - key: "heading"
        value:
          type: ":Quantity.binary64"
          value: "359.5"
          unit: ":degree"
  decode: "070402"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "070402"
  decoded:
    value:
      type: ":Drone"
      entries:
        - key: "id"
          value:
            type: ":Number.int64"
            value: "7"
        - key: "x"
          value:
            type: ":Quantity.int64"
            value: "0"
            unit: ":meter"
        - key: "y"
          value:
            type: ":Quantity.int64"
            value: "0"
            unit: ":meter"
        - key: "heading"
          value:
            type: ":Quantity.int64"
            value: "0"
            unit: ":degree"
    byteCount: 3
```


---

## Test: a wrapping field takes the shorter way backward

Delta: a wrapping field takes the shorter way backward.

### Case description

```yaml
gesBlock: case
id: delta-wrap-backward
schemaApi:
  sources:
    - "schema :Drone as {\n  id: :Number integer between 0 and 255 key\n  x: :Quantity(m) between -200m and 200m step 5cm delta within 1.55m\n  y: :Quantity(m) between -200m and 200m step 5cm delta within 1.55m\n  heading: :Quantity(°) wrapping between 0° and 360° step 0.5° delta within 15.5°\n}\n"
  root: "Drone"
  encode:
    type: ":Drone"
    entries:
      - key: "id"
        value:
          type: ":Number.int64"
          value: "7"
      - key: "x"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":meter"
      - key: "y"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":meter"
      - key: "heading"
        value:
          type: ":Quantity.binary64"
          value: "359.5"
          unit: ":degree"
  baseline:
    type: ":Drone"
    entries:
      - key: "id"
        value:
          type: ":Number.int64"
          value: "7"
      - key: "x"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":meter"
      - key: "y"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":meter"
      - key: "heading"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":degree"
  decode: "07e401"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "07e401"
  decoded:
    value:
      type: ":Drone"
      entries:
        - key: "id"
          value:
            type: ":Number.int64"
            value: "7"
        - key: "x"
          value:
            type: ":Quantity.int64"
            value: "0"
            unit: ":meter"
        - key: "y"
          value:
            type: ":Quantity.int64"
            value: "0"
            unit: ":meter"
        - key: "heading"
          value:
            type: ":Quantity.binary64"
            value: "359.5"
            unit: ":degree"
    byteCount: 3
```


---

## Test: a move beyond the range uses the full index

Delta: a move beyond the range uses the full index.

### Case description

```yaml
gesBlock: case
id: delta-fallback
schemaApi:
  sources:
    - "schema :Drone as {\n  id: :Number integer between 0 and 255 key\n  x: :Quantity(m) between -200m and 200m step 5cm delta within 1.55m\n  y: :Quantity(m) between -200m and 200m step 5cm delta within 1.55m\n  heading: :Quantity(°) wrapping between 0° and 360° step 0.5° delta within 15.5°\n}\n"
  root: "Drone"
  encode:
    type: ":Drone"
    entries:
      - key: "id"
        value:
          type: ":Number.int64"
          value: "7"
      - key: "x"
        value:
          type: ":Quantity.int64"
          value: "10"
          unit: ":meter"
      - key: "y"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":meter"
      - key: "heading"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":degree"
  baseline:
    type: ":Drone"
    entries:
      - key: "id"
        value:
          type: ":Number.int64"
          value: "7"
      - key: "x"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":meter"
      - key: "y"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":meter"
      - key: "heading"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":degree"
  decode: "07890601"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "07890601"
  decoded:
    value:
      type: ":Drone"
      entries:
        - key: "id"
          value:
            type: ":Number.int64"
            value: "7"
        - key: "x"
          value:
            type: ":Quantity.int64"
            value: "10"
            unit: ":meter"
        - key: "y"
          value:
            type: ":Quantity.int64"
            value: "0"
            unit: ":meter"
        - key: "heading"
          value:
            type: ":Quantity.int64"
            value: "0"
            unit: ":degree"
    byteCount: 4
```


---

## Test: a move below a step is no change

Delta: a move below a step is no change.

### Case description

```yaml
gesBlock: case
id: delta-below-step
schemaApi:
  sources:
    - "schema :Drone as {\n  id: :Number integer between 0 and 255 key\n  x: :Quantity(m) between -200m and 200m step 5cm delta within 1.55m\n  y: :Quantity(m) between -200m and 200m step 5cm delta within 1.55m\n  heading: :Quantity(°) wrapping between 0° and 360° step 0.5° delta within 15.5°\n}\n"
  root: "Drone"
  encode:
    type: ":Drone"
    entries:
      - key: "id"
        value:
          type: ":Number.int64"
          value: "7"
      - key: "x"
        value:
          type: ":Quantity.binary64"
          value: "0.01"
          unit: ":meter"
      - key: "y"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":meter"
      - key: "heading"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":degree"
  baseline:
    type: ":Drone"
    entries:
      - key: "id"
        value:
          type: ":Number.int64"
          value: "7"
      - key: "x"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":meter"
      - key: "y"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":meter"
      - key: "heading"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":degree"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "0700"
```


---

## Test: a baseline with another key is no valid baseline for encoding

Delta: a baseline with another key is no valid baseline for encoding.

### Case description

```yaml
gesBlock: case
id: delta-key-mismatch
schemaApi:
  sources:
    - "schema :Drone as {\n  id: :Number integer between 0 and 255 key\n  x: :Quantity(m) between -200m and 200m step 5cm delta within 1.55m\n  y: :Quantity(m) between -200m and 200m step 5cm delta within 1.55m\n  heading: :Quantity(°) wrapping between 0° and 360° step 0.5° delta within 15.5°\n}\n"
  root: "Drone"
  encode:
    type: ":Drone"
    entries:
      - key: "id"
        value:
          type: ":Number.int64"
          value: "8"
      - key: "x"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":meter"
      - key: "y"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":meter"
      - key: "heading"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":degree"
  baseline:
    type: ":Drone"
    entries:
      - key: "id"
        value:
          type: ":Number.int64"
          value: "7"
      - key: "x"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":meter"
      - key: "y"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":meter"
      - key: "heading"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":degree"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    error:
      code: "wire.keyMismatch"
      path: "id"
```


---

## Test: a delta with another key is invalid

Delta: a delta with another key is invalid.

### Case description

```yaml
gesBlock: case
id: delta-reject-key
schemaApi:
  sources:
    - "schema :Drone as {\n  id: :Number integer between 0 and 255 key\n  x: :Quantity(m) between -200m and 200m step 5cm delta within 1.55m\n  y: :Quantity(m) between -200m and 200m step 5cm delta within 1.55m\n  heading: :Quantity(°) wrapping between 0° and 360° step 0.5° delta within 15.5°\n}\n"
  root: "Drone"
  baseline:
    type: ":Drone"
    entries:
      - key: "id"
        value:
          type: ":Number.int64"
          value: "7"
      - key: "x"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":meter"
      - key: "y"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":meter"
      - key: "heading"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":degree"
  decode: "0800"
```

### Expectation

```yaml
gesBlock: expect
schema:
  decoded:
    error:
      code: "wire.invalidDelta"
      path: "id"
```


---

## Test: a range form without a difference is invalid

Delta: a range form without a difference is invalid.

### Case description

```yaml
gesBlock: case
id: delta-reject-zero-range
schemaApi:
  sources:
    - "schema :Drone as {\n  id: :Number integer between 0 and 255 key\n  x: :Quantity(m) between -200m and 200m step 5cm delta within 1.55m\n  y: :Quantity(m) between -200m and 200m step 5cm delta within 1.55m\n  heading: :Quantity(°) wrapping between 0° and 360° step 0.5° delta within 15.5°\n}\n"
  root: "Drone"
  baseline:
    type: ":Drone"
    entries:
      - key: "id"
        value:
          type: ":Number.int64"
          value: "7"
      - key: "x"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":meter"
      - key: "y"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":meter"
      - key: "heading"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":degree"
  decode: "07f101"
```

### Expectation

```yaml
gesBlock: expect
schema:
  decoded:
    error:
      code: "wire.invalidDelta"
      path: "x"
```


---

## Test: a range form above its range is invalid

Delta: a range form above its range is invalid.

### Case description

```yaml
gesBlock: case
id: delta-reject-range-width
schemaApi:
  sources:
    - "schema :Drone as {\n  id: :Number integer between 0 and 255 key\n  x: :Quantity(m) between -200m and 200m step 5cm delta within 1.55m\n  y: :Quantity(m) between -200m and 200m step 5cm delta within 1.55m\n  heading: :Quantity(°) wrapping between 0° and 360° step 0.5° delta within 15.5°\n}\n"
  root: "Drone"
  baseline:
    type: ":Drone"
    entries:
      - key: "id"
        value:
          type: ":Number.int64"
          value: "7"
      - key: "x"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":meter"
      - key: "y"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":meter"
      - key: "heading"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":degree"
  decode: "07f103"
```

### Expectation

```yaml
gesBlock: expect
schema:
  decoded:
    error:
      code: "wire.invalidDelta"
      path: "x"
```


---

## Test: a full index within the range is invalid

Delta: a full index within the range is invalid.

### Case description

```yaml
gesBlock: case
id: delta-reject-fallback-near
schemaApi:
  sources:
    - "schema :Drone as {\n  id: :Number integer between 0 and 255 key\n  x: :Quantity(m) between -200m and 200m step 5cm delta within 1.55m\n  y: :Quantity(m) between -200m and 200m step 5cm delta within 1.55m\n  heading: :Quantity(°) wrapping between 0° and 360° step 0.5° delta within 15.5°\n}\n"
  root: "Drone"
  baseline:
    type: ":Drone"
    entries:
      - key: "id"
        value:
          type: ":Number.int64"
          value: "7"
      - key: "x"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":meter"
      - key: "y"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":meter"
      - key: "heading"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":degree"
  decode: "0719fa00"
```

### Expectation

```yaml
gesBlock: expect
schema:
  decoded:
    error:
      code: "wire.invalidDelta"
      path: "x"
```


---

## Test: a full index without a difference is invalid

Delta: a full index without a difference is invalid.

### Case description

```yaml
gesBlock: case
id: delta-reject-fallback-same
schemaApi:
  sources:
    - "schema :Drone as {\n  id: :Number integer between 0 and 255 key\n  x: :Quantity(m) between -200m and 200m step 5cm delta within 1.55m\n  y: :Quantity(m) between -200m and 200m step 5cm delta within 1.55m\n  heading: :Quantity(°) wrapping between 0° and 360° step 0.5° delta within 15.5°\n}\n"
  root: "Drone"
  baseline:
    type: ":Drone"
    entries:
      - key: "id"
        value:
          type: ":Number.int64"
          value: "7"
      - key: "x"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":meter"
      - key: "y"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":meter"
      - key: "heading"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":degree"
  decode: "0709fa00"
```

### Expectation

```yaml
gesBlock: expect
schema:
  decoded:
    error:
      code: "wire.invalidDelta"
      path: "x"
```


---

## Test: a difference that leaves a range is invalid

Delta: a difference that leaves a range is invalid.

### Case description

```yaml
gesBlock: case
id: delta-reject-outside
schemaApi:
  sources:
    - "schema :Drone as {\n  id: :Number integer between 0 and 255 key\n  x: :Quantity(m) between -200m and 200m step 5cm delta within 1.55m\n  y: :Quantity(m) between -200m and 200m step 5cm delta within 1.55m\n  heading: :Quantity(°) wrapping between 0° and 360° step 0.5° delta within 15.5°\n}\n"
  root: "Drone"
  baseline:
    type: ":Drone"
    entries:
      - key: "id"
        value:
          type: ":Number.int64"
          value: "7"
      - key: "x"
        value:
          type: ":Quantity.int64"
          value: "200"
          unit: ":meter"
      - key: "y"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":meter"
      - key: "heading"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":degree"
  decode: "070102"
```

### Expectation

```yaml
gesBlock: expect
schema:
  decoded:
    error:
      code: "wire.invalidDelta"
      path: "x"
```


---

## Test: padding of a delta is checked

Delta: padding of a delta is checked.

### Case description

```yaml
gesBlock: case
id: delta-reject-padding
schemaApi:
  sources:
    - "schema :Drone as {\n  id: :Number integer between 0 and 255 key\n  x: :Quantity(m) between -200m and 200m step 5cm delta within 1.55m\n  y: :Quantity(m) between -200m and 200m step 5cm delta within 1.55m\n  heading: :Quantity(°) wrapping between 0° and 360° step 0.5° delta within 15.5°\n}\n"
  root: "Drone"
  baseline:
    type: ":Drone"
    entries:
      - key: "id"
        value:
          type: ":Number.int64"
          value: "7"
      - key: "x"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":meter"
      - key: "y"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":meter"
      - key: "heading"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":degree"
  decode: "0708"
```

### Expectation

```yaml
gesBlock: expect
schema:
  decoded:
    error:
      code: "wire.invalidPadding"
      path: ""
```


---

## Test: a delta that ends early is truncated

Delta: a delta that ends early is truncated.

### Case description

```yaml
gesBlock: case
id: delta-reject-truncated
schemaApi:
  sources:
    - "schema :Drone as {\n  id: :Number integer between 0 and 255 key\n  x: :Quantity(m) between -200m and 200m step 5cm delta within 1.55m\n  y: :Quantity(m) between -200m and 200m step 5cm delta within 1.55m\n  heading: :Quantity(°) wrapping between 0° and 360° step 0.5° delta within 15.5°\n}\n"
  root: "Drone"
  baseline:
    type: ":Drone"
    entries:
      - key: "id"
        value:
          type: ":Number.int64"
          value: "7"
      - key: "x"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":meter"
      - key: "y"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":meter"
      - key: "heading"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":degree"
  decode: "07"
```

### Expectation

```yaml
gesBlock: expect
schema:
  decoded:
    error:
      code: "wire.truncated"
```


---

## Test: a delta over the caller limit is reported

Delta: a delta over the caller limit is reported.

### Case description

```yaml
gesBlock: case
id: delta-reject-limit
schemaApi:
  sources:
    - "schema :Drone as {\n  id: :Number integer between 0 and 255 key\n  x: :Quantity(m) between -200m and 200m step 5cm delta within 1.55m\n  y: :Quantity(m) between -200m and 200m step 5cm delta within 1.55m\n  heading: :Quantity(°) wrapping between 0° and 360° step 0.5° delta within 15.5°\n}\n"
  root: "Drone"
  baseline:
    type: ":Drone"
    entries:
      - key: "id"
        value:
          type: ":Number.int64"
          value: "7"
      - key: "x"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":meter"
      - key: "y"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":meter"
      - key: "heading"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":degree"
  decode: "07890601"
  decodeLimit: 2
```

### Expectation

```yaml
gesBlock: expect
schema:
  decoded:
    error:
      code: "wire.limitExceeded"
```


---

## Test: a Boolean has no payload and a Tag is written in full

Delta: a Boolean has no payload and a Tag is written in full.

### Case description

```yaml
gesBlock: case
id: delta-boolean-and-tag
schemaApi:
  sources:
    - "schema :Inner as {\n  v: :Number integer between 0 and 3\n  f: :Boolean\n}\n\nschema :M as {\n  flag: :Boolean\n  state: :Tag(#a, #b, #c)\n  label: :Text max 3 optional\n  items: :List(:Number integer between 0 and 7) max 2\n  inner: :Inner\n  at: :Point(m) {\n    x between 0m and 100m step 1m delta within 3m\n    y between 0m and 100m step 1m\n  }\n  free: :Vector\n  pick: :Number integer between 0 and 7 | :Inner | :Nothing\n  raw: :Number optional\n}\n"
  root: "M"
  encode:
    type: ":M"
    entries:
      - key: "flag"
        value:
          type: ":Boolean"
          value: true
      - key: "state"
        value:
          type: ":Tag"
          value: "b"
      - key: "label"
        value:
          type: ":Nothing"
      - key: "items"
        value:
          type: ":List"
          items: []
      - key: "inner"
        value:
          type: ":Inner"
          entries:
            - key: "v"
              value:
                type: ":Number.int64"
                value: "0"
            - key: "f"
              value:
                type: ":Boolean"
                value: false
      - key: "at"
        value:
          type: ":Point"
          x: "0"
          y: "0"
          z: "0"
          unit: ":meter"
      - key: "free"
        value:
          type: ":Vector"
          x: "0"
          y: "0"
          z: "0"
      - key: "pick"
        value:
          type: ":Nothing"
      - key: "raw"
        value:
          type: ":Nothing"
  baseline:
    type: ":M"
    entries:
      - key: "flag"
        value:
          type: ":Boolean"
          value: false
      - key: "state"
        value:
          type: ":Tag"
          value: "a"
      - key: "label"
        value:
          type: ":Nothing"
      - key: "items"
        value:
          type: ":List"
          items: []
      - key: "inner"
        value:
          type: ":Inner"
          entries:
            - key: "v"
              value:
                type: ":Number.int64"
                value: "0"
            - key: "f"
              value:
                type: ":Boolean"
                value: false
      - key: "at"
        value:
          type: ":Point"
          x: "0"
          y: "0"
          z: "0"
          unit: ":meter"
      - key: "free"
        value:
          type: ":Vector"
          x: "0"
          y: "0"
          z: "0"
      - key: "pick"
        value:
          type: ":Nothing"
      - key: "raw"
        value:
          type: ":Nothing"
  decode: "0302"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "0302"
  decoded:
    value:
      type: ":M"
      entries:
        - key: "flag"
          value:
            type: ":Boolean"
            value: true
        - key: "state"
          value:
            type: ":Tag"
            value: "b"
        - key: "label"
          value:
            type: ":Nothing"
        - key: "items"
          value:
            type: ":List"
            items: []
        - key: "inner"
          value:
            type: ":Inner"
            entries:
              - key: "v"
                value:
                  type: ":Number.int64"
                  value: "0"
              - key: "f"
                value:
                  type: ":Boolean"
                  value: false
        - key: "at"
          value:
            type: ":Point"
            x: "0"
            y: "0"
            z: "0"
            unit: ":meter"
        - key: "free"
          value:
            type: ":Vector"
            x: "0"
            y: "0"
            z: "0"
        - key: "pick"
          value:
            type: ":Nothing"
        - key: "raw"
          value:
            type: ":Nothing"
    byteCount: 2
```


---

## Test: a value that appears is written in full

Delta: a value that appears is written in full.

### Case description

```yaml
gesBlock: case
id: delta-optional-appears
schemaApi:
  sources:
    - "schema :Inner as {\n  v: :Number integer between 0 and 3\n  f: :Boolean\n}\n\nschema :M as {\n  flag: :Boolean\n  state: :Tag(#a, #b, #c)\n  label: :Text max 3 optional\n  items: :List(:Number integer between 0 and 7) max 2\n  inner: :Inner\n  at: :Point(m) {\n    x between 0m and 100m step 1m delta within 3m\n    y between 0m and 100m step 1m\n  }\n  free: :Vector\n  pick: :Number integer between 0 and 7 | :Inner | :Nothing\n  raw: :Number optional\n}\n"
  root: "M"
  encode:
    type: ":M"
    entries:
      - key: "flag"
        value:
          type: ":Boolean"
          value: false
      - key: "state"
        value:
          type: ":Tag"
          value: "a"
      - key: "label"
        value:
          type: ":Text"
          value: "a"
      - key: "items"
        value:
          type: ":List"
          items: []
      - key: "inner"
        value:
          type: ":Inner"
          entries:
            - key: "v"
              value:
                type: ":Number.int64"
                value: "0"
            - key: "f"
              value:
                type: ":Boolean"
                value: false
      - key: "at"
        value:
          type: ":Point"
          x: "0"
          y: "0"
          z: "0"
          unit: ":meter"
      - key: "free"
        value:
          type: ":Vector"
          x: "0"
          y: "0"
          z: "0"
      - key: "pick"
        value:
          type: ":Nothing"
      - key: "raw"
        value:
          type: ":Nothing"
  baseline:
    type: ":M"
    entries:
      - key: "flag"
        value:
          type: ":Boolean"
          value: false
      - key: "state"
        value:
          type: ":Tag"
          value: "a"
      - key: "label"
        value:
          type: ":Nothing"
      - key: "items"
        value:
          type: ":List"
          items: []
      - key: "inner"
        value:
          type: ":Inner"
          entries:
            - key: "v"
              value:
                type: ":Number.int64"
                value: "0"
            - key: "f"
              value:
                type: ":Boolean"
                value: false
      - key: "at"
        value:
          type: ":Point"
          x: "0"
          y: "0"
          z: "0"
          unit: ":meter"
      - key: "free"
        value:
          type: ":Vector"
          x: "0"
          y: "0"
          z: "0"
      - key: "pick"
        value:
          type: ":Nothing"
      - key: "raw"
        value:
          type: ":Nothing"
  decode: "041606"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "041606"
  decoded:
    value:
      type: ":M"
      entries:
        - key: "flag"
          value:
            type: ":Boolean"
            value: false
        - key: "state"
          value:
            type: ":Tag"
            value: "a"
        - key: "label"
          value:
            type: ":Text"
            value: "a"
        - key: "items"
          value:
            type: ":List"
            items: []
        - key: "inner"
          value:
            type: ":Inner"
            entries:
              - key: "v"
                value:
                  type: ":Number.int64"
                  value: "0"
              - key: "f"
                value:
                  type: ":Boolean"
                  value: false
        - key: "at"
          value:
            type: ":Point"
            x: "0"
            y: "0"
            z: "0"
            unit: ":meter"
        - key: "free"
          value:
            type: ":Vector"
            x: "0"
            y: "0"
            z: "0"
        - key: "pick"
          value:
            type: ":Nothing"
        - key: "raw"
          value:
            type: ":Nothing"
    byteCount: 3
```


---

## Test: a value that vanishes writes its presence bit

Delta: a value that vanishes writes its presence bit.

### Case description

```yaml
gesBlock: case
id: delta-optional-vanishes
schemaApi:
  sources:
    - "schema :Inner as {\n  v: :Number integer between 0 and 3\n  f: :Boolean\n}\n\nschema :M as {\n  flag: :Boolean\n  state: :Tag(#a, #b, #c)\n  label: :Text max 3 optional\n  items: :List(:Number integer between 0 and 7) max 2\n  inner: :Inner\n  at: :Point(m) {\n    x between 0m and 100m step 1m delta within 3m\n    y between 0m and 100m step 1m\n  }\n  free: :Vector\n  pick: :Number integer between 0 and 7 | :Inner | :Nothing\n  raw: :Number optional\n}\n"
  root: "M"
  encode:
    type: ":M"
    entries:
      - key: "flag"
        value:
          type: ":Boolean"
          value: false
      - key: "state"
        value:
          type: ":Tag"
          value: "a"
      - key: "label"
        value:
          type: ":Nothing"
      - key: "items"
        value:
          type: ":List"
          items: []
      - key: "inner"
        value:
          type: ":Inner"
          entries:
            - key: "v"
              value:
                type: ":Number.int64"
                value: "0"
            - key: "f"
              value:
                type: ":Boolean"
                value: false
      - key: "at"
        value:
          type: ":Point"
          x: "0"
          y: "0"
          z: "0"
          unit: ":meter"
      - key: "free"
        value:
          type: ":Vector"
          x: "0"
          y: "0"
          z: "0"
      - key: "pick"
        value:
          type: ":Nothing"
      - key: "raw"
        value:
          type: ":Nothing"
  baseline:
    type: ":M"
    entries:
      - key: "flag"
        value:
          type: ":Boolean"
          value: false
      - key: "state"
        value:
          type: ":Tag"
          value: "a"
      - key: "label"
        value:
          type: ":Text"
          value: "a"
      - key: "items"
        value:
          type: ":List"
          items: []
      - key: "inner"
        value:
          type: ":Inner"
          entries:
            - key: "v"
              value:
                type: ":Number.int64"
                value: "0"
            - key: "f"
              value:
                type: ":Boolean"
                value: false
      - key: "at"
        value:
          type: ":Point"
          x: "0"
          y: "0"
          z: "0"
          unit: ":meter"
      - key: "free"
        value:
          type: ":Vector"
          x: "0"
          y: "0"
          z: "0"
      - key: "pick"
        value:
          type: ":Nothing"
      - key: "raw"
        value:
          type: ":Nothing"
  decode: "0400"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "0400"
  decoded:
    value:
      type: ":M"
      entries:
        - key: "flag"
          value:
            type: ":Boolean"
            value: false
        - key: "state"
          value:
            type: ":Tag"
            value: "a"
        - key: "label"
          value:
            type: ":Nothing"
        - key: "items"
          value:
            type: ":List"
            items: []
        - key: "inner"
          value:
            type: ":Inner"
            entries:
              - key: "v"
                value:
                  type: ":Number.int64"
                  value: "0"
              - key: "f"
                value:
                  type: ":Boolean"
                  value: false
        - key: "at"
          value:
            type: ":Point"
            x: "0"
            y: "0"
            z: "0"
            unit: ":meter"
        - key: "free"
          value:
            type: ":Vector"
            x: "0"
            y: "0"
            z: "0"
        - key: "pick"
          value:
            type: ":Nothing"
        - key: "raw"
          value:
            type: ":Nothing"
    byteCount: 2
```


---

## Test: a present value that changes writes its payload

Delta: a present value that changes writes its payload.

### Case description

```yaml
gesBlock: case
id: delta-optional-changes
schemaApi:
  sources:
    - "schema :Inner as {\n  v: :Number integer between 0 and 3\n  f: :Boolean\n}\n\nschema :M as {\n  flag: :Boolean\n  state: :Tag(#a, #b, #c)\n  label: :Text max 3 optional\n  items: :List(:Number integer between 0 and 7) max 2\n  inner: :Inner\n  at: :Point(m) {\n    x between 0m and 100m step 1m delta within 3m\n    y between 0m and 100m step 1m\n  }\n  free: :Vector\n  pick: :Number integer between 0 and 7 | :Inner | :Nothing\n  raw: :Number optional\n}\n"
  root: "M"
  encode:
    type: ":M"
    entries:
      - key: "flag"
        value:
          type: ":Boolean"
          value: false
      - key: "state"
        value:
          type: ":Tag"
          value: "a"
      - key: "label"
        value:
          type: ":Text"
          value: "b"
      - key: "items"
        value:
          type: ":List"
          items: []
      - key: "inner"
        value:
          type: ":Inner"
          entries:
            - key: "v"
              value:
                type: ":Number.int64"
                value: "0"
            - key: "f"
              value:
                type: ":Boolean"
                value: false
      - key: "at"
        value:
          type: ":Point"
          x: "0"
          y: "0"
          z: "0"
          unit: ":meter"
      - key: "free"
        value:
          type: ":Vector"
          x: "0"
          y: "0"
          z: "0"
      - key: "pick"
        value:
          type: ":Nothing"
      - key: "raw"
        value:
          type: ":Nothing"
  baseline:
    type: ":M"
    entries:
      - key: "flag"
        value:
          type: ":Boolean"
          value: false
      - key: "state"
        value:
          type: ":Tag"
          value: "a"
      - key: "label"
        value:
          type: ":Text"
          value: "a"
      - key: "items"
        value:
          type: ":List"
          items: []
      - key: "inner"
        value:
          type: ":Inner"
          entries:
            - key: "v"
              value:
                type: ":Number.int64"
                value: "0"
            - key: "f"
              value:
                type: ":Boolean"
                value: false
      - key: "at"
        value:
          type: ":Point"
          x: "0"
          y: "0"
          z: "0"
          unit: ":meter"
      - key: "free"
        value:
          type: ":Vector"
          x: "0"
          y: "0"
          z: "0"
      - key: "pick"
        value:
          type: ":Nothing"
      - key: "raw"
        value:
          type: ":Nothing"
  decode: "042606"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "042606"
  decoded:
    value:
      type: ":M"
      entries:
        - key: "flag"
          value:
            type: ":Boolean"
            value: false
        - key: "state"
          value:
            type: ":Tag"
            value: "a"
        - key: "label"
          value:
            type: ":Text"
            value: "b"
        - key: "items"
          value:
            type: ":List"
            items: []
        - key: "inner"
          value:
            type: ":Inner"
            entries:
              - key: "v"
                value:
                  type: ":Number.int64"
                  value: "0"
              - key: "f"
                value:
                  type: ":Boolean"
                  value: false
        - key: "at"
          value:
            type: ":Point"
            x: "0"
            y: "0"
            z: "0"
            unit: ":meter"
        - key: "free"
          value:
            type: ":Vector"
            x: "0"
            y: "0"
            z: "0"
        - key: "pick"
          value:
            type: ":Nothing"
        - key: "raw"
          value:
            type: ":Nothing"
    byteCount: 3
```


---

## Test: a list is replaced as a whole

Delta: a list is replaced as a whole.

### Case description

```yaml
gesBlock: case
id: delta-list
schemaApi:
  sources:
    - "schema :Inner as {\n  v: :Number integer between 0 and 3\n  f: :Boolean\n}\n\nschema :M as {\n  flag: :Boolean\n  state: :Tag(#a, #b, #c)\n  label: :Text max 3 optional\n  items: :List(:Number integer between 0 and 7) max 2\n  inner: :Inner\n  at: :Point(m) {\n    x between 0m and 100m step 1m delta within 3m\n    y between 0m and 100m step 1m\n  }\n  free: :Vector\n  pick: :Number integer between 0 and 7 | :Inner | :Nothing\n  raw: :Number optional\n}\n"
  root: "M"
  encode:
    type: ":M"
    entries:
      - key: "flag"
        value:
          type: ":Boolean"
          value: false
      - key: "state"
        value:
          type: ":Tag"
          value: "a"
      - key: "label"
        value:
          type: ":Nothing"
      - key: "items"
        value:
          type: ":List"
          items:
            - type: ":Number.int64"
              value: "1"
            - type: ":Number.int64"
              value: "2"
      - key: "inner"
        value:
          type: ":Inner"
          entries:
            - key: "v"
              value:
                type: ":Number.int64"
                value: "0"
            - key: "f"
              value:
                type: ":Boolean"
                value: false
      - key: "at"
        value:
          type: ":Point"
          x: "0"
          y: "0"
          z: "0"
          unit: ":meter"
      - key: "free"
        value:
          type: ":Vector"
          x: "0"
          y: "0"
          z: "0"
      - key: "pick"
        value:
          type: ":Nothing"
      - key: "raw"
        value:
          type: ":Nothing"
  baseline:
    type: ":M"
    entries:
      - key: "flag"
        value:
          type: ":Boolean"
          value: false
      - key: "state"
        value:
          type: ":Tag"
          value: "a"
      - key: "label"
        value:
          type: ":Nothing"
      - key: "items"
        value:
          type: ":List"
          items:
            - type: ":Number.int64"
              value: "1"
      - key: "inner"
        value:
          type: ":Inner"
          entries:
            - key: "v"
              value:
                type: ":Number.int64"
                value: "0"
            - key: "f"
              value:
                type: ":Boolean"
                value: false
      - key: "at"
        value:
          type: ":Point"
          x: "0"
          y: "0"
          z: "0"
          unit: ":meter"
      - key: "free"
        value:
          type: ":Vector"
          x: "0"
          y: "0"
          z: "0"
      - key: "pick"
        value:
          type: ":Nothing"
      - key: "raw"
        value:
          type: ":Nothing"
  decode: "088c00"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "088c00"
  decoded:
    value:
      type: ":M"
      entries:
        - key: "flag"
          value:
            type: ":Boolean"
            value: false
        - key: "state"
          value:
            type: ":Tag"
            value: "a"
        - key: "label"
          value:
            type: ":Nothing"
        - key: "items"
          value:
            type: ":List"
            items:
              - type: ":Number.int64"
                value: "1"
              - type: ":Number.int64"
                value: "2"
        - key: "inner"
          value:
            type: ":Inner"
            entries:
              - key: "v"
                value:
                  type: ":Number.int64"
                  value: "0"
              - key: "f"
                value:
                  type: ":Boolean"
                  value: false
        - key: "at"
          value:
            type: ":Point"
            x: "0"
            y: "0"
            z: "0"
            unit: ":meter"
        - key: "free"
          value:
            type: ":Vector"
            x: "0"
            y: "0"
            z: "0"
        - key: "pick"
          value:
            type: ":Nothing"
        - key: "raw"
          value:
            type: ":Nothing"
    byteCount: 3
```


---

## Test: a nested schema writes its own change bits

Delta: a nested schema writes its own change bits.

### Case description

```yaml
gesBlock: case
id: delta-nested
schemaApi:
  sources:
    - "schema :Inner as {\n  v: :Number integer between 0 and 3\n  f: :Boolean\n}\n\nschema :M as {\n  flag: :Boolean\n  state: :Tag(#a, #b, #c)\n  label: :Text max 3 optional\n  items: :List(:Number integer between 0 and 7) max 2\n  inner: :Inner\n  at: :Point(m) {\n    x between 0m and 100m step 1m delta within 3m\n    y between 0m and 100m step 1m\n  }\n  free: :Vector\n  pick: :Number integer between 0 and 7 | :Inner | :Nothing\n  raw: :Number optional\n}\n"
  root: "M"
  encode:
    type: ":M"
    entries:
      - key: "flag"
        value:
          type: ":Boolean"
          value: false
      - key: "state"
        value:
          type: ":Tag"
          value: "a"
      - key: "label"
        value:
          type: ":Nothing"
      - key: "items"
        value:
          type: ":List"
          items: []
      - key: "inner"
        value:
          type: ":Inner"
          entries:
            - key: "v"
              value:
                type: ":Number.int64"
                value: "0"
            - key: "f"
              value:
                type: ":Boolean"
                value: true
      - key: "at"
        value:
          type: ":Point"
          x: "0"
          y: "0"
          z: "0"
          unit: ":meter"
      - key: "free"
        value:
          type: ":Vector"
          x: "0"
          y: "0"
          z: "0"
      - key: "pick"
        value:
          type: ":Nothing"
      - key: "raw"
        value:
          type: ":Nothing"
  baseline:
    type: ":M"
    entries:
      - key: "flag"
        value:
          type: ":Boolean"
          value: false
      - key: "state"
        value:
          type: ":Tag"
          value: "a"
      - key: "label"
        value:
          type: ":Nothing"
      - key: "items"
        value:
          type: ":List"
          items: []
      - key: "inner"
        value:
          type: ":Inner"
          entries:
            - key: "v"
              value:
                type: ":Number.int64"
                value: "0"
            - key: "f"
              value:
                type: ":Boolean"
                value: false
      - key: "at"
        value:
          type: ":Point"
          x: "0"
          y: "0"
          z: "0"
          unit: ":meter"
      - key: "free"
        value:
          type: ":Vector"
          x: "0"
          y: "0"
          z: "0"
      - key: "pick"
        value:
          type: ":Nothing"
      - key: "raw"
        value:
          type: ":Nothing"
  decode: "1004"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "1004"
  decoded:
    value:
      type: ":M"
      entries:
        - key: "flag"
          value:
            type: ":Boolean"
            value: false
        - key: "state"
          value:
            type: ":Tag"
            value: "a"
        - key: "label"
          value:
            type: ":Nothing"
        - key: "items"
          value:
            type: ":List"
            items: []
        - key: "inner"
          value:
            type: ":Inner"
            entries:
              - key: "v"
                value:
                  type: ":Number.int64"
                  value: "0"
              - key: "f"
                value:
                  type: ":Boolean"
                  value: true
        - key: "at"
          value:
            type: ":Point"
            x: "0"
            y: "0"
            z: "0"
            unit: ":meter"
        - key: "free"
          value:
            type: ":Vector"
            x: "0"
            y: "0"
            z: "0"
        - key: "pick"
          value:
            type: ":Nothing"
        - key: "raw"
          value:
            type: ":Nothing"
    byteCount: 2
```


---

## Test: an axis with a block uses its own range

Delta: an axis with a block uses its own range.

### Case description

```yaml
gesBlock: case
id: delta-axis-range
schemaApi:
  sources:
    - "schema :Inner as {\n  v: :Number integer between 0 and 3\n  f: :Boolean\n}\n\nschema :M as {\n  flag: :Boolean\n  state: :Tag(#a, #b, #c)\n  label: :Text max 3 optional\n  items: :List(:Number integer between 0 and 7) max 2\n  inner: :Inner\n  at: :Point(m) {\n    x between 0m and 100m step 1m delta within 3m\n    y between 0m and 100m step 1m\n  }\n  free: :Vector\n  pick: :Number integer between 0 and 7 | :Inner | :Nothing\n  raw: :Number optional\n}\n"
  root: "M"
  encode:
    type: ":M"
    entries:
      - key: "flag"
        value:
          type: ":Boolean"
          value: false
      - key: "state"
        value:
          type: ":Tag"
          value: "a"
      - key: "label"
        value:
          type: ":Nothing"
      - key: "items"
        value:
          type: ":List"
          items: []
      - key: "inner"
        value:
          type: ":Inner"
          entries:
            - key: "v"
              value:
                type: ":Number.int64"
                value: "0"
            - key: "f"
              value:
                type: ":Boolean"
                value: false
      - key: "at"
        value:
          type: ":Point"
          x: "2"
          y: "0"
          z: "0"
          unit: ":meter"
      - key: "free"
        value:
          type: ":Vector"
          x: "0"
          y: "0"
          z: "0"
      - key: "pick"
        value:
          type: ":Nothing"
      - key: "raw"
        value:
          type: ":Nothing"
  baseline:
    type: ":M"
    entries:
      - key: "flag"
        value:
          type: ":Boolean"
          value: false
      - key: "state"
        value:
          type: ":Tag"
          value: "a"
      - key: "label"
        value:
          type: ":Nothing"
      - key: "items"
        value:
          type: ":List"
          items: []
      - key: "inner"
        value:
          type: ":Inner"
          entries:
            - key: "v"
              value:
                type: ":Number.int64"
                value: "0"
            - key: "f"
              value:
                type: ":Boolean"
                value: false
      - key: "at"
        value:
          type: ":Point"
          x: "0"
          y: "0"
          z: "0"
          unit: ":meter"
      - key: "free"
        value:
          type: ":Vector"
          x: "0"
          y: "0"
          z: "0"
      - key: "pick"
        value:
          type: ":Nothing"
      - key: "raw"
        value:
          type: ":Nothing"
  decode: "2052"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "2052"
  decoded:
    value:
      type: ":M"
      entries:
        - key: "flag"
          value:
            type: ":Boolean"
            value: false
        - key: "state"
          value:
            type: ":Tag"
            value: "a"
        - key: "label"
          value:
            type: ":Nothing"
        - key: "items"
          value:
            type: ":List"
            items: []
        - key: "inner"
          value:
            type: ":Inner"
            entries:
              - key: "v"
                value:
                  type: ":Number.int64"
                  value: "0"
              - key: "f"
                value:
                  type: ":Boolean"
                  value: false
        - key: "at"
          value:
            type: ":Point"
            x: "2"
            y: "0"
            z: "0"
            unit: ":meter"
        - key: "free"
          value:
            type: ":Vector"
            x: "0"
            y: "0"
            z: "0"
        - key: "pick"
          value:
            type: ":Nothing"
        - key: "raw"
          value:
            type: ":Nothing"
    byteCount: 2
```


---

## Test: an axis without a range is written in full

Delta: an axis without a range is written in full.

### Case description

```yaml
gesBlock: case
id: delta-axis-full
schemaApi:
  sources:
    - "schema :Inner as {\n  v: :Number integer between 0 and 3\n  f: :Boolean\n}\n\nschema :M as {\n  flag: :Boolean\n  state: :Tag(#a, #b, #c)\n  label: :Text max 3 optional\n  items: :List(:Number integer between 0 and 7) max 2\n  inner: :Inner\n  at: :Point(m) {\n    x between 0m and 100m step 1m delta within 3m\n    y between 0m and 100m step 1m\n  }\n  free: :Vector\n  pick: :Number integer between 0 and 7 | :Inner | :Nothing\n  raw: :Number optional\n}\n"
  root: "M"
  encode:
    type: ":M"
    entries:
      - key: "flag"
        value:
          type: ":Boolean"
          value: false
      - key: "state"
        value:
          type: ":Tag"
          value: "a"
      - key: "label"
        value:
          type: ":Nothing"
      - key: "items"
        value:
          type: ":List"
          items: []
      - key: "inner"
        value:
          type: ":Inner"
          entries:
            - key: "v"
              value:
                type: ":Number.int64"
                value: "0"
            - key: "f"
              value:
                type: ":Boolean"
                value: false
      - key: "at"
        value:
          type: ":Point"
          x: "0"
          y: "7"
          z: "0"
          unit: ":meter"
      - key: "free"
        value:
          type: ":Vector"
          x: "0"
          y: "0"
          z: "0"
      - key: "pick"
        value:
          type: ":Nothing"
      - key: "raw"
        value:
          type: ":Nothing"
  baseline:
    type: ":M"
    entries:
      - key: "flag"
        value:
          type: ":Boolean"
          value: false
      - key: "state"
        value:
          type: ":Tag"
          value: "a"
      - key: "label"
        value:
          type: ":Nothing"
      - key: "items"
        value:
          type: ":List"
          items: []
      - key: "inner"
        value:
          type: ":Inner"
          entries:
            - key: "v"
              value:
                type: ":Number.int64"
                value: "0"
            - key: "f"
              value:
                type: ":Boolean"
                value: false
      - key: "at"
        value:
          type: ":Point"
          x: "0"
          y: "0"
          z: "0"
          unit: ":meter"
      - key: "free"
        value:
          type: ":Vector"
          x: "0"
          y: "0"
          z: "0"
      - key: "pick"
        value:
          type: ":Nothing"
      - key: "raw"
        value:
          type: ":Nothing"
  decode: "203c00"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "203c00"
  decoded:
    value:
      type: ":M"
      entries:
        - key: "flag"
          value:
            type: ":Boolean"
            value: false
        - key: "state"
          value:
            type: ":Tag"
            value: "a"
        - key: "label"
          value:
            type: ":Nothing"
        - key: "items"
          value:
            type: ":List"
            items: []
        - key: "inner"
          value:
            type: ":Inner"
            entries:
              - key: "v"
                value:
                  type: ":Number.int64"
                  value: "0"
              - key: "f"
                value:
                  type: ":Boolean"
                  value: false
        - key: "at"
          value:
            type: ":Point"
            x: "0"
            y: "7"
            z: "0"
            unit: ":meter"
        - key: "free"
          value:
            type: ":Vector"
            x: "0"
            y: "0"
            z: "0"
        - key: "pick"
          value:
            type: ":Nothing"
        - key: "raw"
          value:
            type: ":Nothing"
    byteCount: 3
```


---

## Test: an axis without a block writes its raw bits

Delta: an axis without a block writes its raw bits.

### Case description

```yaml
gesBlock: case
id: delta-vector-raw
schemaApi:
  sources:
    - "schema :Inner as {\n  v: :Number integer between 0 and 3\n  f: :Boolean\n}\n\nschema :M as {\n  flag: :Boolean\n  state: :Tag(#a, #b, #c)\n  label: :Text max 3 optional\n  items: :List(:Number integer between 0 and 7) max 2\n  inner: :Inner\n  at: :Point(m) {\n    x between 0m and 100m step 1m delta within 3m\n    y between 0m and 100m step 1m\n  }\n  free: :Vector\n  pick: :Number integer between 0 and 7 | :Inner | :Nothing\n  raw: :Number optional\n}\n"
  root: "M"
  encode:
    type: ":M"
    entries:
      - key: "flag"
        value:
          type: ":Boolean"
          value: false
      - key: "state"
        value:
          type: ":Tag"
          value: "a"
      - key: "label"
        value:
          type: ":Nothing"
      - key: "items"
        value:
          type: ":List"
          items: []
      - key: "inner"
        value:
          type: ":Inner"
          entries:
            - key: "v"
              value:
                type: ":Number.int64"
                value: "0"
            - key: "f"
              value:
                type: ":Boolean"
                value: false
      - key: "at"
        value:
          type: ":Point"
          x: "0"
          y: "0"
          z: "0"
          unit: ":meter"
      - key: "free"
        value:
          type: ":Vector"
          x: "0"
          y: "0"
          z: "1.5"
      - key: "pick"
        value:
          type: ":Nothing"
      - key: "raw"
        value:
          type: ":Nothing"
  baseline:
    type: ":M"
    entries:
      - key: "flag"
        value:
          type: ":Boolean"
          value: false
      - key: "state"
        value:
          type: ":Tag"
          value: "a"
      - key: "label"
        value:
          type: ":Nothing"
      - key: "items"
        value:
          type: ":List"
          items: []
      - key: "inner"
        value:
          type: ":Inner"
          entries:
            - key: "v"
              value:
                type: ":Number.int64"
                value: "0"
            - key: "f"
              value:
                type: ":Boolean"
                value: false
      - key: "at"
        value:
          type: ":Point"
          x: "0"
          y: "0"
          z: "0"
          unit: ":meter"
      - key: "free"
        value:
          type: ":Vector"
          x: "0"
          y: "0"
          z: "0"
      - key: "pick"
        value:
          type: ":Nothing"
      - key: "raw"
        value:
          type: ":Nothing"
  decode: "4008000000000080ff03"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "4008000000000080ff03"
  decoded:
    value:
      type: ":M"
      entries:
        - key: "flag"
          value:
            type: ":Boolean"
            value: false
        - key: "state"
          value:
            type: ":Tag"
            value: "a"
        - key: "label"
          value:
            type: ":Nothing"
        - key: "items"
          value:
            type: ":List"
            items: []
        - key: "inner"
          value:
            type: ":Inner"
            entries:
              - key: "v"
                value:
                  type: ":Number.int64"
                  value: "0"
              - key: "f"
                value:
                  type: ":Boolean"
                  value: false
        - key: "at"
          value:
            type: ":Point"
            x: "0"
            y: "0"
            z: "0"
            unit: ":meter"
        - key: "free"
          value:
            type: ":Vector"
            x: "0"
            y: "0"
            z: "1.5"
        - key: "pick"
          value:
            type: ":Nothing"
        - key: "raw"
          value:
            type: ":Nothing"
    byteCount: 10
```


---

## Test: another alternative is written in full

Delta: another alternative is written in full.

### Case description

```yaml
gesBlock: case
id: delta-union-other
schemaApi:
  sources:
    - "schema :Inner as {\n  v: :Number integer between 0 and 3\n  f: :Boolean\n}\n\nschema :M as {\n  flag: :Boolean\n  state: :Tag(#a, #b, #c)\n  label: :Text max 3 optional\n  items: :List(:Number integer between 0 and 7) max 2\n  inner: :Inner\n  at: :Point(m) {\n    x between 0m and 100m step 1m delta within 3m\n    y between 0m and 100m step 1m\n  }\n  free: :Vector\n  pick: :Number integer between 0 and 7 | :Inner | :Nothing\n  raw: :Number optional\n}\n"
  root: "M"
  encode:
    type: ":M"
    entries:
      - key: "flag"
        value:
          type: ":Boolean"
          value: false
      - key: "state"
        value:
          type: ":Tag"
          value: "a"
      - key: "label"
        value:
          type: ":Nothing"
      - key: "items"
        value:
          type: ":List"
          items: []
      - key: "inner"
        value:
          type: ":Inner"
          entries:
            - key: "v"
              value:
                type: ":Number.int64"
                value: "0"
            - key: "f"
              value:
                type: ":Boolean"
                value: false
      - key: "at"
        value:
          type: ":Point"
          x: "0"
          y: "0"
          z: "0"
          unit: ":meter"
      - key: "free"
        value:
          type: ":Vector"
          x: "0"
          y: "0"
          z: "0"
      - key: "pick"
        value:
          type: ":Number.int64"
          value: "5"
      - key: "raw"
        value:
          type: ":Nothing"
  baseline:
    type: ":M"
    entries:
      - key: "flag"
        value:
          type: ":Boolean"
          value: false
      - key: "state"
        value:
          type: ":Tag"
          value: "a"
      - key: "label"
        value:
          type: ":Nothing"
      - key: "items"
        value:
          type: ":List"
          items: []
      - key: "inner"
        value:
          type: ":Inner"
          entries:
            - key: "v"
              value:
                type: ":Number.int64"
                value: "0"
            - key: "f"
              value:
                type: ":Boolean"
                value: false
      - key: "at"
        value:
          type: ":Point"
          x: "0"
          y: "0"
          z: "0"
          unit: ":meter"
      - key: "free"
        value:
          type: ":Vector"
          x: "0"
          y: "0"
          z: "0"
      - key: "pick"
        value:
          type: ":Nothing"
      - key: "raw"
        value:
          type: ":Nothing"
  decode: "8028"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "8028"
  decoded:
    value:
      type: ":M"
      entries:
        - key: "flag"
          value:
            type: ":Boolean"
            value: false
        - key: "state"
          value:
            type: ":Tag"
            value: "a"
        - key: "label"
          value:
            type: ":Nothing"
        - key: "items"
          value:
            type: ":List"
            items: []
        - key: "inner"
          value:
            type: ":Inner"
            entries:
              - key: "v"
                value:
                  type: ":Number.int64"
                  value: "0"
              - key: "f"
                value:
                  type: ":Boolean"
                  value: false
        - key: "at"
          value:
            type: ":Point"
            x: "0"
            y: "0"
            z: "0"
            unit: ":meter"
        - key: "free"
          value:
            type: ":Vector"
            x: "0"
            y: "0"
            z: "0"
        - key: "pick"
          value:
            type: ":Number.int64"
            value: "5"
        - key: "raw"
          value:
            type: ":Nothing"
    byteCount: 2
```


---

## Test: the same alternative writes its payload

Delta: the same alternative writes its payload.

### Case description

```yaml
gesBlock: case
id: delta-union-same
schemaApi:
  sources:
    - "schema :Inner as {\n  v: :Number integer between 0 and 3\n  f: :Boolean\n}\n\nschema :M as {\n  flag: :Boolean\n  state: :Tag(#a, #b, #c)\n  label: :Text max 3 optional\n  items: :List(:Number integer between 0 and 7) max 2\n  inner: :Inner\n  at: :Point(m) {\n    x between 0m and 100m step 1m delta within 3m\n    y between 0m and 100m step 1m\n  }\n  free: :Vector\n  pick: :Number integer between 0 and 7 | :Inner | :Nothing\n  raw: :Number optional\n}\n"
  root: "M"
  encode:
    type: ":M"
    entries:
      - key: "flag"
        value:
          type: ":Boolean"
          value: false
      - key: "state"
        value:
          type: ":Tag"
          value: "a"
      - key: "label"
        value:
          type: ":Nothing"
      - key: "items"
        value:
          type: ":List"
          items: []
      - key: "inner"
        value:
          type: ":Inner"
          entries:
            - key: "v"
              value:
                type: ":Number.int64"
                value: "0"
            - key: "f"
              value:
                type: ":Boolean"
                value: false
      - key: "at"
        value:
          type: ":Point"
          x: "0"
          y: "0"
          z: "0"
          unit: ":meter"
      - key: "free"
        value:
          type: ":Vector"
          x: "0"
          y: "0"
          z: "0"
      - key: "pick"
        value:
          type: ":Number.int64"
          value: "6"
      - key: "raw"
        value:
          type: ":Nothing"
  baseline:
    type: ":M"
    entries:
      - key: "flag"
        value:
          type: ":Boolean"
          value: false
      - key: "state"
        value:
          type: ":Tag"
          value: "a"
      - key: "label"
        value:
          type: ":Nothing"
      - key: "items"
        value:
          type: ":List"
          items: []
      - key: "inner"
        value:
          type: ":Inner"
          entries:
            - key: "v"
              value:
                type: ":Number.int64"
                value: "0"
            - key: "f"
              value:
                type: ":Boolean"
                value: false
      - key: "at"
        value:
          type: ":Point"
          x: "0"
          y: "0"
          z: "0"
          unit: ":meter"
      - key: "free"
        value:
          type: ":Vector"
          x: "0"
          y: "0"
          z: "0"
      - key: "pick"
        value:
          type: ":Number.int64"
          value: "5"
      - key: "raw"
        value:
          type: ":Nothing"
  decode: "8030"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "8030"
  decoded:
    value:
      type: ":M"
      entries:
        - key: "flag"
          value:
            type: ":Boolean"
            value: false
        - key: "state"
          value:
            type: ":Tag"
            value: "a"
        - key: "label"
          value:
            type: ":Nothing"
        - key: "items"
          value:
            type: ":List"
            items: []
        - key: "inner"
          value:
            type: ":Inner"
            entries:
              - key: "v"
                value:
                  type: ":Number.int64"
                  value: "0"
              - key: "f"
                value:
                  type: ":Boolean"
                  value: false
        - key: "at"
          value:
            type: ":Point"
            x: "0"
            y: "0"
            z: "0"
            unit: ":meter"
        - key: "free"
          value:
            type: ":Vector"
            x: "0"
            y: "0"
            z: "0"
        - key: "pick"
          value:
            type: ":Number.int64"
            value: "6"
        - key: "raw"
          value:
            type: ":Nothing"
    byteCount: 2
```


---

## Test: the same schema alternative writes a nested delta

Delta: the same schema alternative writes a nested delta.

### Case description

```yaml
gesBlock: case
id: delta-union-nested
schemaApi:
  sources:
    - "schema :Inner as {\n  v: :Number integer between 0 and 3\n  f: :Boolean\n}\n\nschema :M as {\n  flag: :Boolean\n  state: :Tag(#a, #b, #c)\n  label: :Text max 3 optional\n  items: :List(:Number integer between 0 and 7) max 2\n  inner: :Inner\n  at: :Point(m) {\n    x between 0m and 100m step 1m delta within 3m\n    y between 0m and 100m step 1m\n  }\n  free: :Vector\n  pick: :Number integer between 0 and 7 | :Inner | :Nothing\n  raw: :Number optional\n}\n"
  root: "M"
  encode:
    type: ":M"
    entries:
      - key: "flag"
        value:
          type: ":Boolean"
          value: false
      - key: "state"
        value:
          type: ":Tag"
          value: "a"
      - key: "label"
        value:
          type: ":Nothing"
      - key: "items"
        value:
          type: ":List"
          items: []
      - key: "inner"
        value:
          type: ":Inner"
          entries:
            - key: "v"
              value:
                type: ":Number.int64"
                value: "0"
            - key: "f"
              value:
                type: ":Boolean"
                value: false
      - key: "at"
        value:
          type: ":Point"
          x: "0"
          y: "0"
          z: "0"
          unit: ":meter"
      - key: "free"
        value:
          type: ":Vector"
          x: "0"
          y: "0"
          z: "0"
      - key: "pick"
        value:
          type: ":Inner"
          entries:
            - key: "v"
              value:
                type: ":Number.int64"
                value: "2"
            - key: "f"
              value:
                type: ":Boolean"
                value: false
      - key: "raw"
        value:
          type: ":Nothing"
  baseline:
    type: ":M"
    entries:
      - key: "flag"
        value:
          type: ":Boolean"
          value: false
      - key: "state"
        value:
          type: ":Tag"
          value: "a"
      - key: "label"
        value:
          type: ":Nothing"
      - key: "items"
        value:
          type: ":List"
          items: []
      - key: "inner"
        value:
          type: ":Inner"
          entries:
            - key: "v"
              value:
                type: ":Number.int64"
                value: "0"
            - key: "f"
              value:
                type: ":Boolean"
                value: false
      - key: "at"
        value:
          type: ":Point"
          x: "0"
          y: "0"
          z: "0"
          unit: ":meter"
      - key: "free"
        value:
          type: ":Vector"
          x: "0"
          y: "0"
          z: "0"
      - key: "pick"
        value:
          type: ":Inner"
          entries:
            - key: "v"
              value:
                type: ":Number.int64"
                value: "0"
            - key: "f"
              value:
                type: ":Boolean"
                value: false
      - key: "raw"
        value:
          type: ":Nothing"
  decode: "804a"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "804a"
  decoded:
    value:
      type: ":M"
      entries:
        - key: "flag"
          value:
            type: ":Boolean"
            value: false
        - key: "state"
          value:
            type: ":Tag"
            value: "a"
        - key: "label"
          value:
            type: ":Nothing"
        - key: "items"
          value:
            type: ":List"
            items: []
        - key: "inner"
          value:
            type: ":Inner"
            entries:
              - key: "v"
                value:
                  type: ":Number.int64"
                  value: "0"
              - key: "f"
                value:
                  type: ":Boolean"
                  value: false
        - key: "at"
          value:
            type: ":Point"
            x: "0"
            y: "0"
            z: "0"
            unit: ":meter"
        - key: "free"
          value:
            type: ":Vector"
            x: "0"
            y: "0"
            z: "0"
        - key: "pick"
          value:
            type: ":Inner"
            entries:
              - key: "v"
                value:
                  type: ":Number.int64"
                  value: "2"
              - key: "f"
                value:
                  type: ":Boolean"
                  value: false
        - key: "raw"
          value:
            type: ":Nothing"
    byteCount: 2
```


---

## Test: a Nothing alternative has no payload

Delta: a Nothing alternative has no payload.

### Case description

```yaml
gesBlock: case
id: delta-union-nothing
schemaApi:
  sources:
    - "schema :Inner as {\n  v: :Number integer between 0 and 3\n  f: :Boolean\n}\n\nschema :M as {\n  flag: :Boolean\n  state: :Tag(#a, #b, #c)\n  label: :Text max 3 optional\n  items: :List(:Number integer between 0 and 7) max 2\n  inner: :Inner\n  at: :Point(m) {\n    x between 0m and 100m step 1m delta within 3m\n    y between 0m and 100m step 1m\n  }\n  free: :Vector\n  pick: :Number integer between 0 and 7 | :Inner | :Nothing\n  raw: :Number optional\n}\n"
  root: "M"
  encode:
    type: ":M"
    entries:
      - key: "flag"
        value:
          type: ":Boolean"
          value: false
      - key: "state"
        value:
          type: ":Tag"
          value: "a"
      - key: "label"
        value:
          type: ":Nothing"
      - key: "items"
        value:
          type: ":List"
          items: []
      - key: "inner"
        value:
          type: ":Inner"
          entries:
            - key: "v"
              value:
                type: ":Number.int64"
                value: "0"
            - key: "f"
              value:
                type: ":Boolean"
                value: false
      - key: "at"
        value:
          type: ":Point"
          x: "0"
          y: "0"
          z: "0"
          unit: ":meter"
      - key: "free"
        value:
          type: ":Vector"
          x: "0"
          y: "0"
          z: "0"
      - key: "pick"
        value:
          type: ":Nothing"
      - key: "raw"
        value:
          type: ":Nothing"
  baseline:
    type: ":M"
    entries:
      - key: "flag"
        value:
          type: ":Boolean"
          value: false
      - key: "state"
        value:
          type: ":Tag"
          value: "a"
      - key: "label"
        value:
          type: ":Nothing"
      - key: "items"
        value:
          type: ":List"
          items: []
      - key: "inner"
        value:
          type: ":Inner"
          entries:
            - key: "v"
              value:
                type: ":Number.int64"
                value: "0"
            - key: "f"
              value:
                type: ":Boolean"
                value: false
      - key: "at"
        value:
          type: ":Point"
          x: "0"
          y: "0"
          z: "0"
          unit: ":meter"
      - key: "free"
        value:
          type: ":Vector"
          x: "0"
          y: "0"
          z: "0"
      - key: "pick"
        value:
          type: ":Number.int64"
          value: "5"
      - key: "raw"
        value:
          type: ":Nothing"
  decode: "8004"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "8004"
  decoded:
    value:
      type: ":M"
      entries:
        - key: "flag"
          value:
            type: ":Boolean"
            value: false
        - key: "state"
          value:
            type: ":Tag"
            value: "a"
        - key: "label"
          value:
            type: ":Nothing"
        - key: "items"
          value:
            type: ":List"
            items: []
        - key: "inner"
          value:
            type: ":Inner"
            entries:
              - key: "v"
                value:
                  type: ":Number.int64"
                  value: "0"
              - key: "f"
                value:
                  type: ":Boolean"
                  value: false
        - key: "at"
          value:
            type: ":Point"
            x: "0"
            y: "0"
            z: "0"
            unit: ":meter"
        - key: "free"
          value:
            type: ":Vector"
            x: "0"
            y: "0"
            z: "0"
        - key: "pick"
          value:
            type: ":Nothing"
        - key: "raw"
          value:
            type: ":Nothing"
    byteCount: 2
```


---

## Test: an optional number writes its presence and bits

Delta: an optional number writes its presence and bits.

### Case description

```yaml
gesBlock: case
id: delta-number-optional
schemaApi:
  sources:
    - "schema :Inner as {\n  v: :Number integer between 0 and 3\n  f: :Boolean\n}\n\nschema :M as {\n  flag: :Boolean\n  state: :Tag(#a, #b, #c)\n  label: :Text max 3 optional\n  items: :List(:Number integer between 0 and 7) max 2\n  inner: :Inner\n  at: :Point(m) {\n    x between 0m and 100m step 1m delta within 3m\n    y between 0m and 100m step 1m\n  }\n  free: :Vector\n  pick: :Number integer between 0 and 7 | :Inner | :Nothing\n  raw: :Number optional\n}\n"
  root: "M"
  encode:
    type: ":M"
    entries:
      - key: "flag"
        value:
          type: ":Boolean"
          value: false
      - key: "state"
        value:
          type: ":Tag"
          value: "a"
      - key: "label"
        value:
          type: ":Nothing"
      - key: "items"
        value:
          type: ":List"
          items: []
      - key: "inner"
        value:
          type: ":Inner"
          entries:
            - key: "v"
              value:
                type: ":Number.int64"
                value: "0"
            - key: "f"
              value:
                type: ":Boolean"
                value: false
      - key: "at"
        value:
          type: ":Point"
          x: "0"
          y: "0"
          z: "0"
          unit: ":meter"
      - key: "free"
        value:
          type: ":Vector"
          x: "0"
          y: "0"
          z: "0"
      - key: "pick"
        value:
          type: ":Nothing"
      - key: "raw"
        value:
          type: ":Number.binary64"
          value: "2.5"
  baseline:
    type: ":M"
    entries:
      - key: "flag"
        value:
          type: ":Boolean"
          value: false
      - key: "state"
        value:
          type: ":Tag"
          value: "a"
      - key: "label"
        value:
          type: ":Nothing"
      - key: "items"
        value:
          type: ":List"
          items: []
      - key: "inner"
        value:
          type: ":Inner"
          entries:
            - key: "v"
              value:
                type: ":Number.int64"
                value: "0"
            - key: "f"
              value:
                type: ":Boolean"
                value: false
      - key: "at"
        value:
          type: ":Point"
          x: "0"
          y: "0"
          z: "0"
          unit: ":meter"
      - key: "free"
        value:
          type: ":Vector"
          x: "0"
          y: "0"
          z: "0"
      - key: "pick"
        value:
          type: ":Nothing"
      - key: "raw"
        value:
          type: ":Number.binary64"
          value: "1.5"
  decode: "00030000000000100001"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "00030000000000100001"
  decoded:
    value:
      type: ":M"
      entries:
        - key: "flag"
          value:
            type: ":Boolean"
            value: false
        - key: "state"
          value:
            type: ":Tag"
            value: "a"
        - key: "label"
          value:
            type: ":Nothing"
        - key: "items"
          value:
            type: ":List"
            items: []
        - key: "inner"
          value:
            type: ":Inner"
            entries:
              - key: "v"
                value:
                  type: ":Number.int64"
                  value: "0"
              - key: "f"
                value:
                  type: ":Boolean"
                  value: false
        - key: "at"
          value:
            type: ":Point"
            x: "0"
            y: "0"
            z: "0"
            unit: ":meter"
        - key: "free"
          value:
            type: ":Vector"
            x: "0"
            y: "0"
            z: "0"
        - key: "pick"
          value:
            type: ":Nothing"
        - key: "raw"
          value:
            type: ":Number.binary64"
            value: "2.5"
    byteCount: 10
```


---

## Test: a nested delta without a change is invalid

Delta: a nested delta without a change is invalid.

### Case description

```yaml
gesBlock: case
id: delta-reject-nested-empty
schemaApi:
  sources:
    - "schema :Inner as {\n  v: :Number integer between 0 and 3\n  f: :Boolean\n}\n\nschema :M as {\n  flag: :Boolean\n  state: :Tag(#a, #b, #c)\n  label: :Text max 3 optional\n  items: :List(:Number integer between 0 and 7) max 2\n  inner: :Inner\n  at: :Point(m) {\n    x between 0m and 100m step 1m delta within 3m\n    y between 0m and 100m step 1m\n  }\n  free: :Vector\n  pick: :Number integer between 0 and 7 | :Inner | :Nothing\n  raw: :Number optional\n}\n"
  root: "M"
  baseline:
    type: ":M"
    entries:
      - key: "flag"
        value:
          type: ":Boolean"
          value: false
      - key: "state"
        value:
          type: ":Tag"
          value: "a"
      - key: "label"
        value:
          type: ":Nothing"
      - key: "items"
        value:
          type: ":List"
          items: []
      - key: "inner"
        value:
          type: ":Inner"
          entries:
            - key: "v"
              value:
                type: ":Number.int64"
                value: "0"
            - key: "f"
              value:
                type: ":Boolean"
                value: false
      - key: "at"
        value:
          type: ":Point"
          x: "0"
          y: "0"
          z: "0"
          unit: ":meter"
      - key: "free"
        value:
          type: ":Vector"
          x: "0"
          y: "0"
          z: "0"
      - key: "pick"
        value:
          type: ":Nothing"
      - key: "raw"
        value:
          type: ":Nothing"
  decode: "1000"
```

### Expectation

```yaml
gesBlock: expect
schema:
  decoded:
    error:
      code: "wire.invalidDelta"
      path: "inner"
```


---

## Test: an axis group without a change is invalid

Delta: an axis group without a change is invalid.

### Case description

```yaml
gesBlock: case
id: delta-reject-axes-empty
schemaApi:
  sources:
    - "schema :Inner as {\n  v: :Number integer between 0 and 3\n  f: :Boolean\n}\n\nschema :M as {\n  flag: :Boolean\n  state: :Tag(#a, #b, #c)\n  label: :Text max 3 optional\n  items: :List(:Number integer between 0 and 7) max 2\n  inner: :Inner\n  at: :Point(m) {\n    x between 0m and 100m step 1m delta within 3m\n    y between 0m and 100m step 1m\n  }\n  free: :Vector\n  pick: :Number integer between 0 and 7 | :Inner | :Nothing\n  raw: :Number optional\n}\n"
  root: "M"
  baseline:
    type: ":M"
    entries:
      - key: "flag"
        value:
          type: ":Boolean"
          value: false
      - key: "state"
        value:
          type: ":Tag"
          value: "a"
      - key: "label"
        value:
          type: ":Nothing"
      - key: "items"
        value:
          type: ":List"
          items: []
      - key: "inner"
        value:
          type: ":Inner"
          entries:
            - key: "v"
              value:
                type: ":Number.int64"
                value: "0"
            - key: "f"
              value:
                type: ":Boolean"
                value: false
      - key: "at"
        value:
          type: ":Point"
          x: "0"
          y: "0"
          z: "0"
          unit: ":meter"
      - key: "free"
        value:
          type: ":Vector"
          x: "0"
          y: "0"
          z: "0"
      - key: "pick"
        value:
          type: ":Nothing"
      - key: "raw"
        value:
          type: ":Nothing"
  decode: "2000"
```

### Expectation

```yaml
gesBlock: expect
schema:
  decoded:
    error:
      code: "wire.invalidDelta"
      path: "at"
```


---

## Test: a change bit between two absent values is invalid

Delta: a change bit between two absent values is invalid.

### Case description

```yaml
gesBlock: case
id: delta-reject-optional-absent
schemaApi:
  sources:
    - "schema :Inner as {\n  v: :Number integer between 0 and 3\n  f: :Boolean\n}\n\nschema :M as {\n  flag: :Boolean\n  state: :Tag(#a, #b, #c)\n  label: :Text max 3 optional\n  items: :List(:Number integer between 0 and 7) max 2\n  inner: :Inner\n  at: :Point(m) {\n    x between 0m and 100m step 1m delta within 3m\n    y between 0m and 100m step 1m\n  }\n  free: :Vector\n  pick: :Number integer between 0 and 7 | :Inner | :Nothing\n  raw: :Number optional\n}\n"
  root: "M"
  baseline:
    type: ":M"
    entries:
      - key: "flag"
        value:
          type: ":Boolean"
          value: false
      - key: "state"
        value:
          type: ":Tag"
          value: "a"
      - key: "label"
        value:
          type: ":Nothing"
      - key: "items"
        value:
          type: ":List"
          items: []
      - key: "inner"
        value:
          type: ":Inner"
          entries:
            - key: "v"
              value:
                type: ":Number.int64"
                value: "0"
            - key: "f"
              value:
                type: ":Boolean"
                value: false
      - key: "at"
        value:
          type: ":Point"
          x: "0"
          y: "0"
          z: "0"
          unit: ":meter"
      - key: "free"
        value:
          type: ":Vector"
          x: "0"
          y: "0"
          z: "0"
      - key: "pick"
        value:
          type: ":Nothing"
      - key: "raw"
        value:
          type: ":Nothing"
  decode: "0400"
```

### Expectation

```yaml
gesBlock: expect
schema:
  decoded:
    error:
      code: "wire.invalidDelta"
      path: "label"
```


---

## Test: a Text without a difference is invalid

Delta: a Text without a difference is invalid.

### Case description

```yaml
gesBlock: case
id: delta-reject-text-same
schemaApi:
  sources:
    - "schema :Inner as {\n  v: :Number integer between 0 and 3\n  f: :Boolean\n}\n\nschema :M as {\n  flag: :Boolean\n  state: :Tag(#a, #b, #c)\n  label: :Text max 3 optional\n  items: :List(:Number integer between 0 and 7) max 2\n  inner: :Inner\n  at: :Point(m) {\n    x between 0m and 100m step 1m delta within 3m\n    y between 0m and 100m step 1m\n  }\n  free: :Vector\n  pick: :Number integer between 0 and 7 | :Inner | :Nothing\n  raw: :Number optional\n}\n"
  root: "M"
  baseline:
    type: ":M"
    entries:
      - key: "flag"
        value:
          type: ":Boolean"
          value: false
      - key: "state"
        value:
          type: ":Tag"
          value: "a"
      - key: "label"
        value:
          type: ":Text"
          value: "a"
      - key: "items"
        value:
          type: ":List"
          items: []
      - key: "inner"
        value:
          type: ":Inner"
          entries:
            - key: "v"
              value:
                type: ":Number.int64"
                value: "0"
            - key: "f"
              value:
                type: ":Boolean"
                value: false
      - key: "at"
        value:
          type: ":Point"
          x: "0"
          y: "0"
          z: "0"
          unit: ":meter"
      - key: "free"
        value:
          type: ":Vector"
          x: "0"
          y: "0"
          z: "0"
      - key: "pick"
        value:
          type: ":Nothing"
      - key: "raw"
        value:
          type: ":Nothing"
  decode: "041606"
```

### Expectation

```yaml
gesBlock: expect
schema:
  decoded:
    error:
      code: "wire.invalidDelta"
      path: "label"
```


---

## Test: a List without a difference is invalid

Delta: a List without a difference is invalid.

### Case description

```yaml
gesBlock: case
id: delta-reject-list-same
schemaApi:
  sources:
    - "schema :Inner as {\n  v: :Number integer between 0 and 3\n  f: :Boolean\n}\n\nschema :M as {\n  flag: :Boolean\n  state: :Tag(#a, #b, #c)\n  label: :Text max 3 optional\n  items: :List(:Number integer between 0 and 7) max 2\n  inner: :Inner\n  at: :Point(m) {\n    x between 0m and 100m step 1m delta within 3m\n    y between 0m and 100m step 1m\n  }\n  free: :Vector\n  pick: :Number integer between 0 and 7 | :Inner | :Nothing\n  raw: :Number optional\n}\n"
  root: "M"
  baseline:
    type: ":M"
    entries:
      - key: "flag"
        value:
          type: ":Boolean"
          value: false
      - key: "state"
        value:
          type: ":Tag"
          value: "a"
      - key: "label"
        value:
          type: ":Nothing"
      - key: "items"
        value:
          type: ":List"
          items:
            - type: ":Number.int64"
              value: "1"
      - key: "inner"
        value:
          type: ":Inner"
          entries:
            - key: "v"
              value:
                type: ":Number.int64"
                value: "0"
            - key: "f"
              value:
                type: ":Boolean"
                value: false
      - key: "at"
        value:
          type: ":Point"
          x: "0"
          y: "0"
          z: "0"
          unit: ":meter"
      - key: "free"
        value:
          type: ":Vector"
          x: "0"
          y: "0"
          z: "0"
      - key: "pick"
        value:
          type: ":Nothing"
      - key: "raw"
        value:
          type: ":Nothing"
  decode: "080a"
```

### Expectation

```yaml
gesBlock: expect
schema:
  decoded:
    error:
      code: "wire.invalidDelta"
      path: "items"
```


---

## Test: a change between two Nothing alternatives is invalid

Delta: a change between two Nothing alternatives is invalid.

### Case description

```yaml
gesBlock: case
id: delta-reject-union-nothing
schemaApi:
  sources:
    - "schema :Inner as {\n  v: :Number integer between 0 and 3\n  f: :Boolean\n}\n\nschema :M as {\n  flag: :Boolean\n  state: :Tag(#a, #b, #c)\n  label: :Text max 3 optional\n  items: :List(:Number integer between 0 and 7) max 2\n  inner: :Inner\n  at: :Point(m) {\n    x between 0m and 100m step 1m delta within 3m\n    y between 0m and 100m step 1m\n  }\n  free: :Vector\n  pick: :Number integer between 0 and 7 | :Inner | :Nothing\n  raw: :Number optional\n}\n"
  root: "M"
  baseline:
    type: ":M"
    entries:
      - key: "flag"
        value:
          type: ":Boolean"
          value: false
      - key: "state"
        value:
          type: ":Tag"
          value: "a"
      - key: "label"
        value:
          type: ":Nothing"
      - key: "items"
        value:
          type: ":List"
          items: []
      - key: "inner"
        value:
          type: ":Inner"
          entries:
            - key: "v"
              value:
                type: ":Number.int64"
                value: "0"
            - key: "f"
              value:
                type: ":Boolean"
                value: false
      - key: "at"
        value:
          type: ":Point"
          x: "0"
          y: "0"
          z: "0"
          unit: ":meter"
      - key: "free"
        value:
          type: ":Vector"
          x: "0"
          y: "0"
          z: "0"
      - key: "pick"
        value:
          type: ":Nothing"
      - key: "raw"
        value:
          type: ":Nothing"
  decode: "8004"
```

### Expectation

```yaml
gesBlock: expect
schema:
  decoded:
    error:
      code: "wire.invalidDelta"
      path: "pick:Nothing"
```


---

## Test: a Text in a delta is validated

Delta: a Text in a delta is validated.

### Case description

```yaml
gesBlock: case
id: delta-reject-text-utf8
schemaApi:
  sources:
    - "schema :Inner as {\n  v: :Number integer between 0 and 3\n  f: :Boolean\n}\n\nschema :M as {\n  flag: :Boolean\n  state: :Tag(#a, #b, #c)\n  label: :Text max 3 optional\n  items: :List(:Number integer between 0 and 7) max 2\n  inner: :Inner\n  at: :Point(m) {\n    x between 0m and 100m step 1m delta within 3m\n    y between 0m and 100m step 1m\n  }\n  free: :Vector\n  pick: :Number integer between 0 and 7 | :Inner | :Nothing\n  raw: :Number optional\n}\n"
  root: "M"
  baseline:
    type: ":M"
    entries:
      - key: "flag"
        value:
          type: ":Boolean"
          value: false
      - key: "state"
        value:
          type: ":Tag"
          value: "a"
      - key: "label"
        value:
          type: ":Nothing"
      - key: "items"
        value:
          type: ":List"
          items: []
      - key: "inner"
        value:
          type: ":Inner"
          entries:
            - key: "v"
              value:
                type: ":Number.int64"
                value: "0"
            - key: "f"
              value:
                type: ":Boolean"
                value: false
      - key: "at"
        value:
          type: ":Point"
          x: "0"
          y: "0"
          z: "0"
          unit: ":meter"
      - key: "free"
        value:
          type: ":Vector"
          x: "0"
          y: "0"
          z: "0"
      - key: "pick"
        value:
          type: ":Nothing"
      - key: "raw"
        value:
          type: ":Nothing"
  decode: "04f60f"
```

### Expectation

```yaml
gesBlock: expect
schema:
  decoded:
    error:
      code: "wire.invalidText"
      path: "label"
```


---

## Test: a key that is reached as a nested field has a change bit

Delta: a key that is reached as a nested field has a change bit.

### Case description

```yaml
gesBlock: case
id: delta-nested-key
schemaApi:
  sources:
    - "schema :Keyed as {\n  id: :Number integer between 0 and 255 key\n  v: :Boolean\n}\n\nschema :Outer as {\n  k: :Keyed\n}\n"
  root: "Outer"
  encode:
    type: ":Outer"
    entries:
      - key: "k"
        value:
          type: ":Keyed"
          entries:
            - key: "id"
              value:
                type: ":Number.int64"
                value: "8"
            - key: "v"
              value:
                type: ":Boolean"
                value: false
  baseline:
    type: ":Outer"
    entries:
      - key: "k"
        value:
          type: ":Keyed"
          entries:
            - key: "id"
              value:
                type: ":Number.int64"
                value: "7"
            - key: "v"
              value:
                type: ":Boolean"
                value: false
  decode: "4300"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "4300"
  decoded:
    value:
      type: ":Outer"
      entries:
        - key: "k"
          value:
            type: ":Keyed"
            entries:
              - key: "id"
                value:
                  type: ":Number.int64"
                  value: "8"
              - key: "v"
                value:
                  type: ":Boolean"
                  value: false
    byteCount: 2
```


---

## Test: a message has no delta

Delta: a message has no delta.

### Case description

```yaml
gesBlock: case
id: delta-message
schemaApi:
  sources:
    - "message Hit(a: :Boolean)\n"
  root: "Hit"
  encode:
    type: ":Message"
    message:
      name: "Hit"
      args:
        - name: "a"
          value:
            type: ":Boolean"
            value: true
  baseline:
    type: ":Message"
    message:
      name: "Hit"
      args:
        - name: "a"
          value:
            type: ":Boolean"
            value: false
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    error:
      code: "wire.typeMismatch"
```


---

## Test: a dump with a baseline lists the resulting record

Delta: a dump with a baseline lists the resulting record.

### Case description

```yaml
gesBlock: case
id: delta-dump
schemaApi:
  sources:
    - "schema :Drone as {\n  id: :Number integer between 0 and 255 key\n  x: :Quantity(m) between -200m and 200m step 5cm delta within 1.55m\n  y: :Quantity(m) between -200m and 200m step 5cm delta within 1.55m\n  heading: :Quantity(°) wrapping between 0° and 360° step 0.5° delta within 15.5°\n}\n"
  root: "Drone"
  dump: "0733ab00"
  dumpBaseline: "07a00ff40100"
```

### Expectation

```yaml
gesBlock: expect
schema:
  dump:
    text: "delta bytes 4\nid bit=0 width=8 value=7\nx bit=8 width=13 value=1\ny bit=21 width=13 value=-0.5\nheading bit=34 width=10 value=0\nbytes 6\n"
    valid: true
```


---

## Test: a dump with a baseline names the error in the delta

Delta: a dump with a baseline names the error in the delta.

### Case description

```yaml
gesBlock: case
id: delta-dump-error
schemaApi:
  sources:
    - "schema :Drone as {\n  id: :Number integer between 0 and 255 key\n  x: :Quantity(m) between -200m and 200m step 5cm delta within 1.55m\n  y: :Quantity(m) between -200m and 200m step 5cm delta within 1.55m\n  heading: :Quantity(°) wrapping between 0° and 360° step 0.5° delta within 15.5°\n}\n"
  root: "Drone"
  dump: "0800"
  dumpBaseline: "07a00ff40100"
```

### Expectation

```yaml
gesBlock: expect
schema:
  dump:
    text: "error wire.invalidDelta path=id bit=8\n"
    valid: false
```

---

## Test: more change bits than one word

More change bits than one word.

### Case description

```yaml
gesBlock: case
id: delta-many-fields
schemaApi:
  sources:
    - "schema :Many as {\n  f0: :Boolean\n  f1: :Boolean\n  f2: :Boolean\n  f3: :Boolean\n  f4: :Boolean\n  f5: :Boolean\n  f6: :Boolean\n  f7: :Boolean\n  f8: :Boolean\n  f9: :Boolean\n  f10: :Boolean\n  f11: :Boolean\n  f12: :Boolean\n  f13: :Boolean\n  f14: :Boolean\n  f15: :Boolean\n  f16: :Boolean\n  f17: :Boolean\n  f18: :Boolean\n  f19: :Boolean\n  f20: :Boolean\n  f21: :Boolean\n  f22: :Boolean\n  f23: :Boolean\n  f24: :Boolean\n  f25: :Boolean\n  f26: :Boolean\n  f27: :Boolean\n  f28: :Boolean\n  f29: :Boolean\n  f30: :Boolean\n  f31: :Boolean\n  f32: :Boolean\n  f33: :Boolean\n  f34: :Boolean\n  f35: :Boolean\n  f36: :Boolean\n  f37: :Boolean\n  f38: :Boolean\n  f39: :Boolean\n  f40: :Boolean\n  f41: :Boolean\n  f42: :Boolean\n  f43: :Boolean\n  f44: :Boolean\n  f45: :Boolean\n  f46: :Boolean\n  f47: :Boolean\n  f48: :Boolean\n  f49: :Boolean\n  f50: :Boolean\n  f51: :Boolean\n  f52: :Boolean\n  f53: :Boolean\n  f54: :Boolean\n  f55: :Boolean\n  f56: :Boolean\n  f57: :Boolean\n  f58: :Boolean\n  f59: :Boolean\n  f60: :Boolean\n  f61: :Boolean\n  f62: :Boolean\n  f63: :Boolean\n  f64: :Boolean\n  f65: :Boolean\n  f66: :Boolean\n  f67: :Boolean\n  f68: :Boolean\n  f69: :Boolean\n}\n"
  root: "Many"
  encode:
    type: ":Many"
    entries:
      - key: "f0"
        value:
          type: ":Boolean"
          value: true
      - key: "f1"
        value:
          type: ":Boolean"
          value: false
      - key: "f2"
        value:
          type: ":Boolean"
          value: false
      - key: "f3"
        value:
          type: ":Boolean"
          value: false
      - key: "f4"
        value:
          type: ":Boolean"
          value: false
      - key: "f5"
        value:
          type: ":Boolean"
          value: false
      - key: "f6"
        value:
          type: ":Boolean"
          value: false
      - key: "f7"
        value:
          type: ":Boolean"
          value: false
      - key: "f8"
        value:
          type: ":Boolean"
          value: false
      - key: "f9"
        value:
          type: ":Boolean"
          value: false
      - key: "f10"
        value:
          type: ":Boolean"
          value: false
      - key: "f11"
        value:
          type: ":Boolean"
          value: false
      - key: "f12"
        value:
          type: ":Boolean"
          value: false
      - key: "f13"
        value:
          type: ":Boolean"
          value: false
      - key: "f14"
        value:
          type: ":Boolean"
          value: false
      - key: "f15"
        value:
          type: ":Boolean"
          value: false
      - key: "f16"
        value:
          type: ":Boolean"
          value: false
      - key: "f17"
        value:
          type: ":Boolean"
          value: false
      - key: "f18"
        value:
          type: ":Boolean"
          value: false
      - key: "f19"
        value:
          type: ":Boolean"
          value: false
      - key: "f20"
        value:
          type: ":Boolean"
          value: false
      - key: "f21"
        value:
          type: ":Boolean"
          value: false
      - key: "f22"
        value:
          type: ":Boolean"
          value: false
      - key: "f23"
        value:
          type: ":Boolean"
          value: false
      - key: "f24"
        value:
          type: ":Boolean"
          value: false
      - key: "f25"
        value:
          type: ":Boolean"
          value: false
      - key: "f26"
        value:
          type: ":Boolean"
          value: false
      - key: "f27"
        value:
          type: ":Boolean"
          value: false
      - key: "f28"
        value:
          type: ":Boolean"
          value: false
      - key: "f29"
        value:
          type: ":Boolean"
          value: false
      - key: "f30"
        value:
          type: ":Boolean"
          value: false
      - key: "f31"
        value:
          type: ":Boolean"
          value: false
      - key: "f32"
        value:
          type: ":Boolean"
          value: false
      - key: "f33"
        value:
          type: ":Boolean"
          value: false
      - key: "f34"
        value:
          type: ":Boolean"
          value: false
      - key: "f35"
        value:
          type: ":Boolean"
          value: false
      - key: "f36"
        value:
          type: ":Boolean"
          value: false
      - key: "f37"
        value:
          type: ":Boolean"
          value: false
      - key: "f38"
        value:
          type: ":Boolean"
          value: false
      - key: "f39"
        value:
          type: ":Boolean"
          value: false
      - key: "f40"
        value:
          type: ":Boolean"
          value: false
      - key: "f41"
        value:
          type: ":Boolean"
          value: false
      - key: "f42"
        value:
          type: ":Boolean"
          value: false
      - key: "f43"
        value:
          type: ":Boolean"
          value: false
      - key: "f44"
        value:
          type: ":Boolean"
          value: false
      - key: "f45"
        value:
          type: ":Boolean"
          value: false
      - key: "f46"
        value:
          type: ":Boolean"
          value: false
      - key: "f47"
        value:
          type: ":Boolean"
          value: false
      - key: "f48"
        value:
          type: ":Boolean"
          value: false
      - key: "f49"
        value:
          type: ":Boolean"
          value: false
      - key: "f50"
        value:
          type: ":Boolean"
          value: false
      - key: "f51"
        value:
          type: ":Boolean"
          value: false
      - key: "f52"
        value:
          type: ":Boolean"
          value: false
      - key: "f53"
        value:
          type: ":Boolean"
          value: false
      - key: "f54"
        value:
          type: ":Boolean"
          value: false
      - key: "f55"
        value:
          type: ":Boolean"
          value: false
      - key: "f56"
        value:
          type: ":Boolean"
          value: false
      - key: "f57"
        value:
          type: ":Boolean"
          value: false
      - key: "f58"
        value:
          type: ":Boolean"
          value: false
      - key: "f59"
        value:
          type: ":Boolean"
          value: false
      - key: "f60"
        value:
          type: ":Boolean"
          value: false
      - key: "f61"
        value:
          type: ":Boolean"
          value: false
      - key: "f62"
        value:
          type: ":Boolean"
          value: false
      - key: "f63"
        value:
          type: ":Boolean"
          value: false
      - key: "f64"
        value:
          type: ":Boolean"
          value: false
      - key: "f65"
        value:
          type: ":Boolean"
          value: false
      - key: "f66"
        value:
          type: ":Boolean"
          value: false
      - key: "f67"
        value:
          type: ":Boolean"
          value: false
      - key: "f68"
        value:
          type: ":Boolean"
          value: false
      - key: "f69"
        value:
          type: ":Boolean"
          value: true
  baseline:
    type: ":Many"
    entries:
      - key: "f0"
        value:
          type: ":Boolean"
          value: false
      - key: "f1"
        value:
          type: ":Boolean"
          value: false
      - key: "f2"
        value:
          type: ":Boolean"
          value: false
      - key: "f3"
        value:
          type: ":Boolean"
          value: false
      - key: "f4"
        value:
          type: ":Boolean"
          value: false
      - key: "f5"
        value:
          type: ":Boolean"
          value: false
      - key: "f6"
        value:
          type: ":Boolean"
          value: false
      - key: "f7"
        value:
          type: ":Boolean"
          value: false
      - key: "f8"
        value:
          type: ":Boolean"
          value: false
      - key: "f9"
        value:
          type: ":Boolean"
          value: false
      - key: "f10"
        value:
          type: ":Boolean"
          value: false
      - key: "f11"
        value:
          type: ":Boolean"
          value: false
      - key: "f12"
        value:
          type: ":Boolean"
          value: false
      - key: "f13"
        value:
          type: ":Boolean"
          value: false
      - key: "f14"
        value:
          type: ":Boolean"
          value: false
      - key: "f15"
        value:
          type: ":Boolean"
          value: false
      - key: "f16"
        value:
          type: ":Boolean"
          value: false
      - key: "f17"
        value:
          type: ":Boolean"
          value: false
      - key: "f18"
        value:
          type: ":Boolean"
          value: false
      - key: "f19"
        value:
          type: ":Boolean"
          value: false
      - key: "f20"
        value:
          type: ":Boolean"
          value: false
      - key: "f21"
        value:
          type: ":Boolean"
          value: false
      - key: "f22"
        value:
          type: ":Boolean"
          value: false
      - key: "f23"
        value:
          type: ":Boolean"
          value: false
      - key: "f24"
        value:
          type: ":Boolean"
          value: false
      - key: "f25"
        value:
          type: ":Boolean"
          value: false
      - key: "f26"
        value:
          type: ":Boolean"
          value: false
      - key: "f27"
        value:
          type: ":Boolean"
          value: false
      - key: "f28"
        value:
          type: ":Boolean"
          value: false
      - key: "f29"
        value:
          type: ":Boolean"
          value: false
      - key: "f30"
        value:
          type: ":Boolean"
          value: false
      - key: "f31"
        value:
          type: ":Boolean"
          value: false
      - key: "f32"
        value:
          type: ":Boolean"
          value: false
      - key: "f33"
        value:
          type: ":Boolean"
          value: false
      - key: "f34"
        value:
          type: ":Boolean"
          value: false
      - key: "f35"
        value:
          type: ":Boolean"
          value: false
      - key: "f36"
        value:
          type: ":Boolean"
          value: false
      - key: "f37"
        value:
          type: ":Boolean"
          value: false
      - key: "f38"
        value:
          type: ":Boolean"
          value: false
      - key: "f39"
        value:
          type: ":Boolean"
          value: false
      - key: "f40"
        value:
          type: ":Boolean"
          value: false
      - key: "f41"
        value:
          type: ":Boolean"
          value: false
      - key: "f42"
        value:
          type: ":Boolean"
          value: false
      - key: "f43"
        value:
          type: ":Boolean"
          value: false
      - key: "f44"
        value:
          type: ":Boolean"
          value: false
      - key: "f45"
        value:
          type: ":Boolean"
          value: false
      - key: "f46"
        value:
          type: ":Boolean"
          value: false
      - key: "f47"
        value:
          type: ":Boolean"
          value: false
      - key: "f48"
        value:
          type: ":Boolean"
          value: false
      - key: "f49"
        value:
          type: ":Boolean"
          value: false
      - key: "f50"
        value:
          type: ":Boolean"
          value: false
      - key: "f51"
        value:
          type: ":Boolean"
          value: false
      - key: "f52"
        value:
          type: ":Boolean"
          value: false
      - key: "f53"
        value:
          type: ":Boolean"
          value: false
      - key: "f54"
        value:
          type: ":Boolean"
          value: false
      - key: "f55"
        value:
          type: ":Boolean"
          value: false
      - key: "f56"
        value:
          type: ":Boolean"
          value: false
      - key: "f57"
        value:
          type: ":Boolean"
          value: false
      - key: "f58"
        value:
          type: ":Boolean"
          value: false
      - key: "f59"
        value:
          type: ":Boolean"
          value: false
      - key: "f60"
        value:
          type: ":Boolean"
          value: false
      - key: "f61"
        value:
          type: ":Boolean"
          value: false
      - key: "f62"
        value:
          type: ":Boolean"
          value: false
      - key: "f63"
        value:
          type: ":Boolean"
          value: false
      - key: "f64"
        value:
          type: ":Boolean"
          value: false
      - key: "f65"
        value:
          type: ":Boolean"
          value: false
      - key: "f66"
        value:
          type: ":Boolean"
          value: false
      - key: "f67"
        value:
          type: ":Boolean"
          value: false
      - key: "f68"
        value:
          type: ":Boolean"
          value: false
      - key: "f69"
        value:
          type: ":Boolean"
          value: false
  decode: "010000000000000020"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "010000000000000020"
  decoded:
    value:
      type: ":Many"
      entries:
        - key: "f0"
          value:
            type: ":Boolean"
            value: true
        - key: "f1"
          value:
            type: ":Boolean"
            value: false
        - key: "f2"
          value:
            type: ":Boolean"
            value: false
        - key: "f3"
          value:
            type: ":Boolean"
            value: false
        - key: "f4"
          value:
            type: ":Boolean"
            value: false
        - key: "f5"
          value:
            type: ":Boolean"
            value: false
        - key: "f6"
          value:
            type: ":Boolean"
            value: false
        - key: "f7"
          value:
            type: ":Boolean"
            value: false
        - key: "f8"
          value:
            type: ":Boolean"
            value: false
        - key: "f9"
          value:
            type: ":Boolean"
            value: false
        - key: "f10"
          value:
            type: ":Boolean"
            value: false
        - key: "f11"
          value:
            type: ":Boolean"
            value: false
        - key: "f12"
          value:
            type: ":Boolean"
            value: false
        - key: "f13"
          value:
            type: ":Boolean"
            value: false
        - key: "f14"
          value:
            type: ":Boolean"
            value: false
        - key: "f15"
          value:
            type: ":Boolean"
            value: false
        - key: "f16"
          value:
            type: ":Boolean"
            value: false
        - key: "f17"
          value:
            type: ":Boolean"
            value: false
        - key: "f18"
          value:
            type: ":Boolean"
            value: false
        - key: "f19"
          value:
            type: ":Boolean"
            value: false
        - key: "f20"
          value:
            type: ":Boolean"
            value: false
        - key: "f21"
          value:
            type: ":Boolean"
            value: false
        - key: "f22"
          value:
            type: ":Boolean"
            value: false
        - key: "f23"
          value:
            type: ":Boolean"
            value: false
        - key: "f24"
          value:
            type: ":Boolean"
            value: false
        - key: "f25"
          value:
            type: ":Boolean"
            value: false
        - key: "f26"
          value:
            type: ":Boolean"
            value: false
        - key: "f27"
          value:
            type: ":Boolean"
            value: false
        - key: "f28"
          value:
            type: ":Boolean"
            value: false
        - key: "f29"
          value:
            type: ":Boolean"
            value: false
        - key: "f30"
          value:
            type: ":Boolean"
            value: false
        - key: "f31"
          value:
            type: ":Boolean"
            value: false
        - key: "f32"
          value:
            type: ":Boolean"
            value: false
        - key: "f33"
          value:
            type: ":Boolean"
            value: false
        - key: "f34"
          value:
            type: ":Boolean"
            value: false
        - key: "f35"
          value:
            type: ":Boolean"
            value: false
        - key: "f36"
          value:
            type: ":Boolean"
            value: false
        - key: "f37"
          value:
            type: ":Boolean"
            value: false
        - key: "f38"
          value:
            type: ":Boolean"
            value: false
        - key: "f39"
          value:
            type: ":Boolean"
            value: false
        - key: "f40"
          value:
            type: ":Boolean"
            value: false
        - key: "f41"
          value:
            type: ":Boolean"
            value: false
        - key: "f42"
          value:
            type: ":Boolean"
            value: false
        - key: "f43"
          value:
            type: ":Boolean"
            value: false
        - key: "f44"
          value:
            type: ":Boolean"
            value: false
        - key: "f45"
          value:
            type: ":Boolean"
            value: false
        - key: "f46"
          value:
            type: ":Boolean"
            value: false
        - key: "f47"
          value:
            type: ":Boolean"
            value: false
        - key: "f48"
          value:
            type: ":Boolean"
            value: false
        - key: "f49"
          value:
            type: ":Boolean"
            value: false
        - key: "f50"
          value:
            type: ":Boolean"
            value: false
        - key: "f51"
          value:
            type: ":Boolean"
            value: false
        - key: "f52"
          value:
            type: ":Boolean"
            value: false
        - key: "f53"
          value:
            type: ":Boolean"
            value: false
        - key: "f54"
          value:
            type: ":Boolean"
            value: false
        - key: "f55"
          value:
            type: ":Boolean"
            value: false
        - key: "f56"
          value:
            type: ":Boolean"
            value: false
        - key: "f57"
          value:
            type: ":Boolean"
            value: false
        - key: "f58"
          value:
            type: ":Boolean"
            value: false
        - key: "f59"
          value:
            type: ":Boolean"
            value: false
        - key: "f60"
          value:
            type: ":Boolean"
            value: false
        - key: "f61"
          value:
            type: ":Boolean"
            value: false
        - key: "f62"
          value:
            type: ":Boolean"
            value: false
        - key: "f63"
          value:
            type: ":Boolean"
            value: false
        - key: "f64"
          value:
            type: ":Boolean"
            value: false
        - key: "f65"
          value:
            type: ":Boolean"
            value: false
        - key: "f66"
          value:
            type: ":Boolean"
            value: false
        - key: "f67"
          value:
            type: ":Boolean"
            value: false
        - key: "f68"
          value:
            type: ":Boolean"
            value: false
        - key: "f69"
          value:
            type: ":Boolean"
            value: true
    byteCount: 9
```

---

## Test: a delta is written from two full records

Delta: a delta is written from two full records.

### Case description

```yaml
gesBlock: case
id: delta-bytes
schemaApi:
  sources:
    - "schema :V as {\n  id: :Number integer between 0 and 255 key\n  n: :Number integer between 0 and 4\n  t: :Text max 1\n  g: :Tag max 1\n  f: :Number between 0 and 1\n  l: :List(:Boolean) max 2\n  u: :Boolean | :Text max 1 | :Nothing\n}\n"
  root: "V"
  deltaEncode:
    record: "07310c000000000000000001"
    baseline: "07300c000000000000000001"
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaEncoded:
    bytes: "074100"
```


---

## Test: a delta gives the full record back

Delta: a delta gives the full record back.

### Case description

```yaml
gesBlock: case
id: delta-bytes-decoded
schemaApi:
  sources:
    - "schema :V as {\n  id: :Number integer between 0 and 255 key\n  n: :Number integer between 0 and 4\n  t: :Text max 1\n  g: :Tag max 1\n  f: :Number between 0 and 1\n  l: :List(:Boolean) max 2\n  u: :Boolean | :Text max 1 | :Nothing\n}\n"
  root: "V"
  deltaDecode:
    delta: "074100"
    baseline: "07300c000000000000000001"
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaDecoded:
    record: "07310c000000000000000001"
    deltaByteCount: 3
```


---

## Test: two records with different keys have no delta

Delta: two records with different keys have no delta.

### Case description

```yaml
gesBlock: case
id: delta-bytes-key
schemaApi:
  sources:
    - "schema :V as {\n  id: :Number integer between 0 and 255 key\n  n: :Number integer between 0 and 4\n  t: :Text max 1\n  g: :Tag max 1\n  f: :Number between 0 and 1\n  l: :List(:Boolean) max 2\n  u: :Boolean | :Text max 1 | :Nothing\n}\n"
  root: "V"
  deltaEncode:
    record: "08300c000000000000000001"
    baseline: "07300c000000000000000001"
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaEncoded:
    error:
      code: "wire.keyMismatch"
      path: "id"
```


---

## Test: a truncated record is rejected

Delta: a truncated record is rejected.

### Case description

```yaml
gesBlock: case
id: delta-record-truncated
schemaApi:
  sources:
    - "schema :V as {\n  id: :Number integer between 0 and 255 key\n  n: :Number integer between 0 and 4\n  t: :Text max 1\n  g: :Tag max 1\n  f: :Number between 0 and 1\n  l: :List(:Boolean) max 2\n  u: :Boolean | :Text max 1 | :Nothing\n}\n"
  root: "V"
  deltaEncode:
    record: "07"
    baseline: "07300c000000000000000001"
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaEncoded:
    error:
      code: "wire.truncated"
      path: "n"
```


---

## Test: a truncated baseline is rejected

Delta: a truncated baseline is rejected.

### Case description

```yaml
gesBlock: case
id: delta-baseline-truncated
schemaApi:
  sources:
    - "schema :V as {\n  id: :Number integer between 0 and 255 key\n  n: :Number integer between 0 and 4\n  t: :Text max 1\n  g: :Tag max 1\n  f: :Number between 0 and 1\n  l: :List(:Boolean) max 2\n  u: :Boolean | :Text max 1 | :Nothing\n}\n"
  root: "V"
  deltaEncode:
    record: "07300c000000000000000001"
    baseline: "07"
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaEncoded:
    error:
      code: "wire.truncated"
      path: "n"
```


---

## Test: an index above a range is rejected in a record

Delta: an index above a range is rejected in a record.

### Case description

```yaml
gesBlock: case
id: delta-record-index
schemaApi:
  sources:
    - "schema :V as {\n  id: :Number integer between 0 and 255 key\n  n: :Number integer between 0 and 4\n  t: :Text max 1\n  g: :Tag max 1\n  f: :Number between 0 and 1\n  l: :List(:Boolean) max 2\n  u: :Boolean | :Text max 1 | :Nothing\n}\n"
  root: "V"
  deltaEncode:
    record: "07350c000000000000000001"
    baseline: "07300c000000000000000001"
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaEncoded:
    error:
      code: "wire.invalidIndex"
      path: "n"
```


---

## Test: an index above a range is rejected in a baseline

Delta: an index above a range is rejected in a baseline.

### Case description

```yaml
gesBlock: case
id: delta-baseline-index
schemaApi:
  sources:
    - "schema :V as {\n  id: :Number integer between 0 and 255 key\n  n: :Number integer between 0 and 4\n  t: :Text max 1\n  g: :Tag max 1\n  f: :Number between 0 and 1\n  l: :List(:Boolean) max 2\n  u: :Boolean | :Text max 1 | :Nothing\n}\n"
  root: "V"
  deltaEncode:
    record: "07300c000000000000000001"
    baseline: "07350c000000000000000001"
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaEncoded:
    error:
      code: "wire.invalidIndex"
      path: "n"
```


---

## Test: invalid text is rejected in a record

Delta: invalid text is rejected in a record.

### Case description

```yaml
gesBlock: case
id: delta-record-text
schemaApi:
  sources:
    - "schema :V as {\n  id: :Number integer between 0 and 255 key\n  n: :Number integer between 0 and 4\n  t: :Text max 1\n  g: :Tag max 1\n  f: :Number between 0 and 1\n  l: :List(:Boolean) max 2\n  u: :Boolean | :Text max 1 | :Nothing\n}\n"
  root: "V"
  deltaEncode:
    record: "07f83f0c000000000000000001"
    baseline: "07300c000000000000000001"
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaEncoded:
    error:
      code: "wire.invalidText"
      path: "t"
```


---

## Test: invalid text is rejected in a baseline

Delta: invalid text is rejected in a baseline.

### Case description

```yaml
gesBlock: case
id: delta-baseline-text
schemaApi:
  sources:
    - "schema :V as {\n  id: :Number integer between 0 and 255 key\n  n: :Number integer between 0 and 4\n  t: :Text max 1\n  g: :Tag max 1\n  f: :Number between 0 and 1\n  l: :List(:Boolean) max 2\n  u: :Boolean | :Text max 1 | :Nothing\n}\n"
  root: "V"
  deltaEncode:
    record: "07300c000000000000000001"
    baseline: "07f83f0c000000000000000001"
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaEncoded:
    error:
      code: "wire.invalidText"
      path: "t"
```


---

## Test: an invalid tag name is rejected

Delta: an invalid tag name is rejected.

### Case description

```yaml
gesBlock: case
id: delta-record-tag
schemaApi:
  sources:
    - "schema :V as {\n  id: :Number integer between 0 and 255 key\n  n: :Number integer between 0 and 4\n  t: :Text max 1\n  g: :Tag max 1\n  f: :Number between 0 and 1\n  l: :List(:Boolean) max 2\n  u: :Boolean | :Text max 1 | :Nothing\n}\n"
  root: "V"
  deltaEncode:
    record: "073008000000000000000001"
    baseline: "07300c000000000000000001"
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaEncoded:
    error:
      code: "wire.invalidTag"
      path: "g"
```


---

## Test: an empty tag name is rejected

Delta: an empty tag name is rejected.

### Case description

```yaml
gesBlock: case
id: delta-record-tag-empty
schemaApi:
  sources:
    - "schema :V as {\n  id: :Number integer between 0 and 255 key\n  n: :Number integer between 0 and 4\n  t: :Text max 1\n  g: :Tag max 1\n  f: :Number between 0 and 1\n  l: :List(:Boolean) max 2\n  u: :Boolean | :Text max 1 | :Nothing\n}\n"
  root: "V"
  deltaEncode:
    record: "0700000000000000000001"
    baseline: "07300c000000000000000001"
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaEncoded:
    error:
      code: "wire.invalidTag"
      path: "g"
```


---

## Test: NaN is rejected

Delta: NaN is rejected.

### Case description

```yaml
gesBlock: case
id: delta-record-nan
schemaApi:
  sources:
    - "schema :V as {\n  id: :Number integer between 0 and 255 key\n  n: :Number integer between 0 and 4\n  t: :Text max 1\n  g: :Tag max 1\n  f: :Number between 0 and 1\n  l: :List(:Boolean) max 2\n  u: :Boolean | :Text max 1 | :Nothing\n}\n"
  root: "V"
  deltaEncode:
    record: "07300c000000000000ff0f01"
    baseline: "07300c000000000000000001"
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaEncoded:
    error:
      code: "wire.invalidNumber"
      path: "f"
```


---

## Test: negative zero is rejected

Delta: negative zero is rejected.

### Case description

```yaml
gesBlock: case
id: delta-record-negative-zero
schemaApi:
  sources:
    - "schema :V as {\n  id: :Number integer between 0 and 255 key\n  n: :Number integer between 0 and 4\n  t: :Text max 1\n  g: :Tag max 1\n  f: :Number between 0 and 1\n  l: :List(:Boolean) max 2\n  u: :Boolean | :Text max 1 | :Nothing\n}\n"
  root: "V"
  deltaEncode:
    record: "07300c000000000000001001"
    baseline: "07300c000000000000000001"
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaEncoded:
    error:
      code: "wire.invalidNumber"
      path: "f"
```


---

## Test: a number outside its range is rejected

Delta: a number outside its range is rejected.

### Case description

```yaml
gesBlock: case
id: delta-record-outside
schemaApi:
  sources:
    - "schema :V as {\n  id: :Number integer between 0 and 255 key\n  n: :Number integer between 0 and 4\n  t: :Text max 1\n  g: :Tag max 1\n  f: :Number between 0 and 1\n  l: :List(:Boolean) max 2\n  u: :Boolean | :Text max 1 | :Nothing\n}\n"
  root: "V"
  deltaEncode:
    record: "07300c000000000000000801"
    baseline: "07300c000000000000000001"
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaEncoded:
    error:
      code: "wire.outOfRange"
      path: "f"
```


---

## Test: a count above its max is rejected

Delta: a count above its max is rejected.

### Case description

```yaml
gesBlock: case
id: delta-record-count
schemaApi:
  sources:
    - "schema :V as {\n  id: :Number integer between 0 and 255 key\n  n: :Number integer between 0 and 4\n  t: :Text max 1\n  g: :Tag max 1\n  f: :Number between 0 and 1\n  l: :List(:Boolean) max 2\n  u: :Boolean | :Text max 1 | :Nothing\n}\n"
  root: "V"
  deltaEncode:
    record: "07300c000000000000006001"
    baseline: "07300c000000000000000001"
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaEncoded:
    error:
      code: "wire.invalidCount"
      path: "l"
```


---

## Test: an alternative above the union is rejected

Delta: an alternative above the union is rejected.

### Case description

```yaml
gesBlock: case
id: delta-record-union
schemaApi:
  sources:
    - "schema :V as {\n  id: :Number integer between 0 and 255 key\n  n: :Number integer between 0 and 4\n  t: :Text max 1\n  g: :Tag max 1\n  f: :Number between 0 and 1\n  l: :List(:Boolean) max 2\n  u: :Boolean | :Text max 1 | :Nothing\n}\n"
  root: "V"
  deltaEncode:
    record: "07300c000000000000008001"
    baseline: "07300c000000000000000001"
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaEncoded:
    error:
      code: "wire.invalidIndex"
      path: "u"
```


---

## Test: an alternative above the union is rejected in a baseline

Delta: an alternative above the union is rejected in a baseline.

### Case description

```yaml
gesBlock: case
id: delta-baseline-union
schemaApi:
  sources:
    - "schema :V as {\n  id: :Number integer between 0 and 255 key\n  n: :Number integer between 0 and 4\n  t: :Text max 1\n  g: :Tag max 1\n  f: :Number between 0 and 1\n  l: :List(:Boolean) max 2\n  u: :Boolean | :Text max 1 | :Nothing\n}\n"
  root: "V"
  deltaEncode:
    record: "07300c000000000000000001"
    baseline: "07300c000000000000008001"
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaEncoded:
    error:
      code: "wire.invalidIndex"
      path: "u"
```


---

## Test: padding of a record is checked

Delta: padding of a record is checked.

### Case description

```yaml
gesBlock: case
id: delta-record-padding
schemaApi:
  sources:
    - "schema :V as {\n  id: :Number integer between 0 and 255 key\n  n: :Number integer between 0 and 4\n  t: :Text max 1\n  g: :Tag max 1\n  f: :Number between 0 and 1\n  l: :List(:Boolean) max 2\n  u: :Boolean | :Text max 1 | :Nothing\n}\n"
  root: "V"
  deltaEncode:
    record: "07300c000000000000000003"
    baseline: "07300c000000000000000001"
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaEncoded:
    error:
      code: "wire.invalidPadding"
      path: ""
```


---

## Test: padding of a baseline is checked

Delta: padding of a baseline is checked.

### Case description

```yaml
gesBlock: case
id: delta-baseline-padding
schemaApi:
  sources:
    - "schema :V as {\n  id: :Number integer between 0 and 255 key\n  n: :Number integer between 0 and 4\n  t: :Text max 1\n  g: :Tag max 1\n  f: :Number between 0 and 1\n  l: :List(:Boolean) max 2\n  u: :Boolean | :Text max 1 | :Nothing\n}\n"
  root: "V"
  deltaEncode:
    record: "07300c000000000000000001"
    baseline: "07300c000000000000000003"
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaEncoded:
    error:
      code: "wire.invalidPadding"
      path: ""
```


---

## Test: decoding rejects a truncated baseline

Delta: decoding rejects a truncated baseline.

### Case description

```yaml
gesBlock: case
id: delta-decode-baseline-truncated
schemaApi:
  sources:
    - "schema :V as {\n  id: :Number integer between 0 and 255 key\n  n: :Number integer between 0 and 4\n  t: :Text max 1\n  g: :Tag max 1\n  f: :Number between 0 and 1\n  l: :List(:Boolean) max 2\n  u: :Boolean | :Text max 1 | :Nothing\n}\n"
  root: "V"
  deltaDecode:
    delta: "0700"
    baseline: "07"
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaDecoded:
    error:
      code: "wire.truncated"
      path: "n"
```


---

## Test: decoding copies only a valid baseline

Delta: decoding copies only a valid baseline.

### Case description

```yaml
gesBlock: case
id: delta-decode-baseline-text
schemaApi:
  sources:
    - "schema :V as {\n  id: :Number integer between 0 and 255 key\n  n: :Number integer between 0 and 4\n  t: :Text max 1\n  g: :Tag max 1\n  f: :Number between 0 and 1\n  l: :List(:Boolean) max 2\n  u: :Boolean | :Text max 1 | :Nothing\n}\n"
  root: "V"
  deltaDecode:
    delta: "0700"
    baseline: "07f83f0c000000000000000001"
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaDecoded:
    error:
      code: "wire.invalidText"
      path: "t"
```


---

## Test: a message has no delta

Delta: a message has no delta.

### Case description

```yaml
gesBlock: case
id: delta-bytes-message
schemaApi:
  sources:
    - "message Hit(a: :Boolean)\n"
  root: "Hit"
  deltaDecode:
    delta: "00"
    baseline: "00"
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaDecoded:
    error:
      code: "wire.typeMismatch"
```


---

## Test: the key of a delta can be read before the rest

Delta: the key of a delta can be read before the rest.

### Case description

```yaml
gesBlock: case
id: delta-key
schemaApi:
  sources:
    - "schema :Drone as {\n  id: :Number integer between 0 and 255 key\n  x: :Quantity(m) between -200m and 200m step 5cm delta within 1.55m\n  y: :Quantity(m) between -200m and 200m step 5cm delta within 1.55m\n  heading: :Quantity(°) wrapping between 0° and 360° step 0.5° delta within 15.5°\n}\n"
  root: "Drone"
  key: "0700"
```

### Expectation

```yaml
gesBlock: expect
schema:
  key: 7
```


---

## Test: a dump names a broken baseline

Delta: a dump names a broken baseline.

### Case description

```yaml
gesBlock: case
id: delta-dump-baseline
schemaApi:
  sources:
    - "schema :Drone as {\n  id: :Number integer between 0 and 255 key\n  x: :Quantity(m) between -200m and 200m step 5cm delta within 1.55m\n  y: :Quantity(m) between -200m and 200m step 5cm delta within 1.55m\n  heading: :Quantity(°) wrapping between 0° and 360° step 0.5° delta within 15.5°\n}\n"
  root: "Drone"
  dump: "0700"
  dumpBaseline: "07"
```

### Expectation

```yaml
gesBlock: expect
schema:
  dump:
    text: "baseline error wire.truncated path=x bit=8\n"
    valid: false
```

---

## Test: a Record encodes its fields

This public API case encodes a Record; the next case gives its external form the same bytes.

### Case description

```yaml
gesBlock: case
id: record-next-to-external
kind: schemaApi
level: atomic
schemaApi:
  sources:
    - "schema :Hit as {\n  a: :Number integer between 0 and 255\n}\n"
  root: Hit
  encode:
    type: ":Hit"
    entries:
      - { key: a, value: { type: ":Number.int64", value: "7" } }
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "07"
```

---

## Test: an external value encodes through its declared fields

This public API case encodes an external value whose type name matches the schema; its fields are materialized, as JSON does.

### Case description

```yaml
gesBlock: case
id: external-value
kind: schemaApi
level: atomic
schemaApi:
  sources:
    - "schema :Hit as {\n  a: :Number integer between 0 and 255\n}\n"
  root: Hit
  encode:
    type: ":Hit"
    entries:
      - { key: a, value: { type: ":Number.int64", value: "7" } }
  encodeExternal: true
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "07"
```

---

## Test: an external value of another type is rejected

This public API case encodes an external value whose type name differs from the schema.

### Case description

```yaml
gesBlock: case
id: external-value-other-type
kind: schemaApi
level: atomic
schemaApi:
  sources:
    - "schema :Hit as {\n  a: :Number integer between 0 and 255\n}\n"
  root: Hit
  encode:
    type: ":Other"
    entries:
      - { key: a, value: { type: ":Number.int64", value: "7" } }
  encodeExternal: true
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    error: { code: "wire.typeMismatch", path: "" }
```

---

## Test: a nested Record encodes its fields

This public API case encodes a nested Record; the next case gives its external form the same bytes.

### Case description

```yaml
gesBlock: case
id: nested-record-next-to-external
kind: schemaApi
level: atomic
schemaApi:
  sources:
    - "schema :Pos as {\n  x: :Number integer between 0 and 255\n}\n\nschema :Hit as {\n  pos: :Pos\n}\n"
  root: Hit
  encode:
    type: ":Hit"
    entries:
      - key: pos
        value:
          type: ":Pos"
          entries:
            - { key: x, value: { type: ":Number.int64", value: "7" } }
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "07"
```

---

## Test: a nested external value encodes through its declared fields

This public API case encodes a Record with an external value as a field.

### Case description

```yaml
gesBlock: case
id: external-value-nested
kind: schemaApi
level: atomic
schemaApi:
  sources:
    - "schema :Pos as {\n  x: :Number integer between 0 and 255\n}\n\nschema :Hit as {\n  pos: :Pos\n}\n"
  root: Hit
  encode:
    type: ":Hit"
    entries:
      - key: pos
        value:
          type: ":Pos"
          entries:
            - { key: x, value: { type: ":Number.int64", value: "7" } }
  externalFields: [pos]
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "07"
```

---

## Test: a nested external value of another type is rejected at its path

This public API case encodes a Record with an external value of another type as a field.

### Case description

```yaml
gesBlock: case
id: external-value-nested-other-type
kind: schemaApi
level: atomic
schemaApi:
  sources:
    - "schema :Pos as {\n  x: :Number integer between 0 and 255\n}\n\nschema :Hit as {\n  pos: :Pos\n}\n"
  root: Hit
  encode:
    type: ":Hit"
    entries:
      - key: pos
        value:
          type: ":Other"
          entries:
            - { key: x, value: { type: ":Number.int64", value: "7" } }
  externalFields: [pos]
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    error: { code: "wire.typeMismatch", path: "pos" }
```

---

## Test: an external value encodes as a delta

This public API case encodes an external value against a Record baseline; the bytes equal those of the Record itself.

### Case description

```yaml
gesBlock: case
id: external-value-delta
schemaApi:
  sources:
    - "schema :Drone as {\n  id: :Number integer between 0 and 255 key\n  x: :Quantity(m) between -200m and 200m step 5cm delta within 1.55m\n  y: :Quantity(m) between -200m and 200m step 5cm delta within 1.55m\n  heading: :Quantity(°) wrapping between 0° and 360° step 0.5° delta within 15.5°\n}\n"
  root: "Drone"
  encode:
    type: ":Drone"
    entries:
      - key: "id"
        value:
          type: ":Number.int64"
          value: "7"
      - key: "x"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":meter"
      - key: "y"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":meter"
      - key: "heading"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":degree"
  baseline:
    type: ":Drone"
    entries:
      - key: "id"
        value:
          type: ":Number.int64"
          value: "7"
      - key: "x"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":meter"
      - key: "y"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":meter"
      - key: "heading"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":degree"
  encodeExternal: true
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "0700"
```

---

## Test: an external baseline encodes and decodes a delta

This public API case uses an external value as the baseline of an encoding and a decoding.

### Case description

```yaml
gesBlock: case
id: external-baseline-delta
schemaApi:
  sources:
    - "schema :Drone as {\n  id: :Number integer between 0 and 255 key\n  x: :Quantity(m) between -200m and 200m step 5cm delta within 1.55m\n  y: :Quantity(m) between -200m and 200m step 5cm delta within 1.55m\n  heading: :Quantity(°) wrapping between 0° and 360° step 0.5° delta within 15.5°\n}\n"
  root: "Drone"
  encode:
    type: ":Drone"
    entries:
      - key: "id"
        value:
          type: ":Number.int64"
          value: "7"
      - key: "x"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":meter"
      - key: "y"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":meter"
      - key: "heading"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":degree"
  baseline:
    type: ":Drone"
    entries:
      - key: "id"
        value:
          type: ":Number.int64"
          value: "7"
      - key: "x"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":meter"
      - key: "y"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":meter"
      - key: "heading"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":degree"
  decode: "0700"
  baselineExternal: true
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "0700"
  decoded:
    value:
      type: ":Drone"
      entries:
        - key: "id"
          value:
            type: ":Number.int64"
            value: "7"
        - key: "x"
          value:
            type: ":Quantity.int64"
            value: "0"
            unit: ":meter"
        - key: "y"
          value:
            type: ":Quantity.int64"
            value: "0"
            unit: ":meter"
        - key: "heading"
          value:
            type: ":Quantity.int64"
            value: "0"
            unit: ":degree"
    byteCount: 2
  maxByteCount: 7
```

---

## Test: a failing getter is no wire error

This public API case encodes an external value whose getter fails; the encoding reports the runtime failure of the field read.

### Case description

```yaml
gesBlock: case
id: external-value-getter-failure
kind: schemaApi
level: atomic
schemaApi:
  sources:
    - "schema :Hit as {\n  a: :Number integer between 0 and 255\n}\n"
  root: Hit
  encode:
    type: ":Hit"
    entries:
      - { key: a, value: { type: ":Number.int64", value: "7" } }
  encodeExternal: true
  failingField: a
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    failure: runtime.externalFieldAccessFailed
```

---

## Test: a deliberate fault of a getter keeps its code through the encoding

This public API case encodes an external value whose getter raises a deliberate fault; the encoding reports the fault's own code.

### Case description

```yaml
gesBlock: case
id: external-value-getter-fault
kind: schemaApi
level: atomic
schemaApi:
  sources:
    - "schema :Hit as {\n  a: :Number integer between 0 and 255\n}\n"
  root: Hit
  encode:
    type: ":Hit"
    entries:
      - { key: a, value: { type: ":Number.int64", value: "7" } }
  encodeExternal: true
  failingField: a
  failingCode: probe.fault
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    failure: probe.fault
```

---

## Test: a failing getter of a baseline is no wire error

This public API case encodes and decodes a delta against an external baseline whose getter fails; both report the runtime failure of the field read.

### Case description

```yaml
gesBlock: case
id: external-baseline-getter-failure
schemaApi:
  sources:
    - "schema :Drone as {\n  id: :Number integer between 0 and 255 key\n  x: :Quantity(m) between -200m and 200m step 5cm delta within 1.55m\n  y: :Quantity(m) between -200m and 200m step 5cm delta within 1.55m\n  heading: :Quantity(°) wrapping between 0° and 360° step 0.5° delta within 15.5°\n}\n"
  root: "Drone"
  encode:
    type: ":Drone"
    entries:
      - key: "id"
        value:
          type: ":Number.int64"
          value: "7"
      - key: "x"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":meter"
      - key: "y"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":meter"
      - key: "heading"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":degree"
  baseline:
    type: ":Drone"
    entries:
      - key: "id"
        value:
          type: ":Number.int64"
          value: "7"
      - key: "x"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":meter"
      - key: "y"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":meter"
      - key: "heading"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":degree"
  decode: "0700"
  baselineExternal: true
  baselineFailingField: x
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    failure: runtime.externalFieldAccessFailed
  decoded:
    failure: runtime.externalFieldAccessFailed
```

---

## Test: a deliberate fault of a baseline getter keeps its code

This public API case encodes and decodes a delta against an external baseline whose getter raises a deliberate fault; both report its own code.

### Case description

```yaml
gesBlock: case
id: external-baseline-getter-fault
schemaApi:
  sources:
    - "schema :Drone as {\n  id: :Number integer between 0 and 255 key\n  x: :Quantity(m) between -200m and 200m step 5cm delta within 1.55m\n  y: :Quantity(m) between -200m and 200m step 5cm delta within 1.55m\n  heading: :Quantity(°) wrapping between 0° and 360° step 0.5° delta within 15.5°\n}\n"
  root: "Drone"
  encode:
    type: ":Drone"
    entries:
      - key: "id"
        value:
          type: ":Number.int64"
          value: "7"
      - key: "x"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":meter"
      - key: "y"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":meter"
      - key: "heading"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":degree"
  baseline:
    type: ":Drone"
    entries:
      - key: "id"
        value:
          type: ":Number.int64"
          value: "7"
      - key: "x"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":meter"
      - key: "y"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":meter"
      - key: "heading"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":degree"
  decode: "0700"
  baselineExternal: true
  baselineFailingField: x
  failingCode: probe.fault
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    failure: probe.fault
  decoded:
    failure: probe.fault
```

---

## Test: a keyed list that did not change costs its change bit

Keyed list delta: both change bits are zero.

### Case description

```yaml
gesBlock: case
id: keyed-unchanged
kind: schemaApi
level: atomic
schemaApi:
  sources: ["schema :Item as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 15\n  on: :Boolean\n}\n\nschema :World as {\n  tick: :Number integer between 0 and 255\n  items: :List(:Item) max 5\n}\n"]
  root: "World"
  deltaEncode: {"record": "090a280206", "baseline": "090a280206"}
  deltaDecode: {"delta": "00", "baseline": "090a280206"}
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaEncoded: {"bytes": "00"}
  deltaDecoded: {"record": "090a280206", "deltaByteCount": 1}
```

---

## Test: a keyed list removes an element

Keyed list delta: only a removal.

### Case description

```yaml
gesBlock: case
id: keyed-removed
kind: schemaApi
level: atomic
schemaApi:
  sources: ["schema :Item as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 15\n  on: :Boolean\n}\n\nschema :World as {\n  tick: :Number integer between 0 and 255\n  items: :List(:Item) max 5\n}\n"]
  root: "World"
  deltaEncode: {"record": "090a280307", "baseline": "090b280266e000"}
  deltaDecode: {"delta": "4a00", "baseline": "090b280266e000"}
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaEncoded: {"bytes": "4a00"}
  deltaDecoded: {"record": "090a280307", "deltaByteCount": 2}
```

---

## Test: a keyed list adds an element at the end

Keyed list delta: only an addition.

### Case description

```yaml
gesBlock: case
id: keyed-added
kind: schemaApi
level: atomic
schemaApi:
  sources: ["schema :Item as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 15\n  on: :Boolean\n}\n\nschema :World as {\n  tick: :Number integer between 0 and 255\n  items: :List(:Item) max 5\n}\n"]
  root: "World"
  deltaEncode: {"record": "090b280266e000", "baseline": "090a280206"}
  deltaDecode: {"delta": "42060e00", "baseline": "090a280206"}
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaEncoded: {"bytes": "42060e00"}
  deltaDecoded: {"record": "090b280266e000", "deltaByteCount": 4}
```

---

## Test: a keyed list adds an element in the middle

Keyed list delta: the merge places the addition by key.

### Case description

```yaml
gesBlock: case
id: keyed-added-middle
kind: schemaApi
level: atomic
schemaApi:
  sources: ["schema :Item as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 15\n  on: :Boolean\n}\n\nschema :World as {\n  tick: :Number integer between 0 and 255\n  items: :List(:Item) max 5\n}\n"]
  root: "World"
  deltaEncode: {"record": "090b280266e000", "baseline": "090a280307"}
  deltaDecode: {"delta": "42040c00", "baseline": "090a280307"}
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaEncoded: {"bytes": "42040c00"}
  deltaDecoded: {"record": "090b280266e000", "deltaByteCount": 4}
```

---

## Test: a keyed list adds an element at the front

Keyed list delta: the merge places the addition by key.

### Case description

```yaml
gesBlock: case
id: keyed-added-front
kind: schemaApi
level: atomic
schemaApi:
  sources: ["schema :Item as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 15\n  on: :Boolean\n}\n\nschema :World as {\n  tick: :Number integer between 0 and 255\n  items: :List(:Item) max 5\n}\n"]
  root: "World"
  deltaEncode: {"record": "090b280266e000", "baseline": "0912300307"}
  deltaDecode: {"delta": "42020a00", "baseline": "0912300307"}
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaEncoded: {"bytes": "42020a00"}
  deltaDecoded: {"record": "090b280266e000", "deltaByteCount": 4}
```

---

## Test: a keyed list changes one field of an element

Keyed list delta: the nested delta has no key.

### Case description

```yaml
gesBlock: case
id: keyed-changed
kind: schemaApi
level: atomic
schemaApi:
  sources: ["schema :Item as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 15\n  on: :Boolean\n}\n\nschema :World as {\n  tick: :Number integer between 0 and 255\n  items: :List(:Item) max 5\n}\n"]
  root: "World"
  deltaEncode: {"record": "090a280201", "baseline": "090a280206"}
  deltaDecode: {"delta": "029202", "baseline": "090a280206"}
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaEncoded: {"bytes": "029202"}
  deltaDecoded: {"record": "090a280201", "deltaByteCount": 3}
```

---

## Test: a keyed list toggles a Boolean of an element

Keyed list delta: a Boolean costs its change bit.

### Case description

```yaml
gesBlock: case
id: keyed-changed-flag
kind: schemaApi
level: atomic
schemaApi:
  sources: ["schema :Item as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 15\n  on: :Boolean\n}\n\nschema :World as {\n  tick: :Number integer between 0 and 255\n  items: :List(:Item) max 5\n}\n"]
  root: "World"
  deltaEncode: {"record": "090a280216", "baseline": "090a280206"}
  deltaDecode: {"delta": "021201", "baseline": "090a280206"}
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaEncoded: {"bytes": "021201"}
  deltaDecoded: {"record": "090a280216", "deltaByteCount": 3}
```

---

## Test: a keyed list removes, adds and changes

The worked example of the specification.

### Case description

```yaml
gesBlock: case
id: keyed-all-three
kind: schemaApi
level: atomic
schemaApi:
  sources: ["schema :Item as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 15\n  on: :Boolean\n}\n\nschema :World as {\n  tick: :Number integer between 0 and 255\n  items: :List(:Item) max 5\n}\n"]
  root: "World"
  deltaEncode: {"record": "090d2803876016848304", "baseline": "090d280266e014a08304"}
  deltaDecode: {"delta": "4a4230b302", "baseline": "090d280266e014a08304"}
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaEncoded: {"bytes": "4a4230b302"}
  deltaDecoded: {"record": "090d2803876016848304", "deltaByteCount": 5}
```

---

## Test: a keyed list removes everything

Keyed list delta: the result is empty.

### Case description

```yaml
gesBlock: case
id: keyed-all-removed
kind: schemaApi
level: atomic
schemaApi:
  sources: ["schema :Item as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 15\n  on: :Boolean\n}\n\nschema :World as {\n  tick: :Number integer between 0 and 255\n  items: :List(:Item) max 5\n}\n"]
  root: "World"
  deltaEncode: {"record": "0900", "baseline": "090a280206"}
  deltaDecode: {"delta": "120200", "baseline": "090a280206"}
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaEncoded: {"bytes": "120200"}
  deltaDecoded: {"record": "0900", "deltaByteCount": 3}
```

---

## Test: a keyed list fills an empty baseline

Keyed list delta: everything is added, by element.

### Case description

```yaml
gesBlock: case
id: keyed-from-empty
kind: schemaApi
level: atomic
schemaApi:
  sources: ["schema :Item as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 15\n  on: :Boolean\n}\n\nschema :World as {\n  tick: :Number integer between 0 and 255\n  items: :List(:Item) max 5\n}\n"]
  root: "World"
  deltaEncode: {"record": "090a280206", "baseline": "0900"}
  deltaDecode: {"delta": "82028a8001", "baseline": "0900"}
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaEncoded: {"bytes": "82028a8001"}
  deltaDecoded: {"record": "090a280206", "deltaByteCount": 5}
```

---

## Test: a key leaves a keyed list

First half of a key that goes away and comes back.

### Case description

```yaml
gesBlock: case
id: keyed-removed-then
kind: schemaApi
level: atomic
schemaApi:
  sources: ["schema :Item as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 15\n  on: :Boolean\n}\n\nschema :World as {\n  tick: :Number integer between 0 and 255\n  items: :List(:Item) max 5\n}\n"]
  root: "World"
  deltaEncode: {"record": "090928", "baseline": "090a280206"}
  deltaDecode: {"delta": "4a00", "baseline": "090a280206"}
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaEncoded: {"bytes": "4a00"}
  deltaDecoded: {"record": "090928", "deltaByteCount": 2}
```

---

## Test: a key comes back to a keyed list

Second half: the same key is an addition against the new baseline.

### Case description

```yaml
gesBlock: case
id: keyed-back
kind: schemaApi
level: atomic
schemaApi:
  sources: ["schema :Item as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 15\n  on: :Boolean\n}\n\nschema :World as {\n  tick: :Number integer between 0 and 255\n  items: :List(:Item) max 5\n}\n"]
  root: "World"
  deltaEncode: {"record": "090a280206", "baseline": "090928"}
  deltaDecode: {"delta": "42040c00", "baseline": "090928"}
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaEncoded: {"bytes": "42040c00"}
  deltaDecoded: {"record": "090a280206", "deltaByteCount": 4}
```

---

## Test: an unsorted baseline takes the whole list

Keyed list delta: the baseline does not ascend.

### Case description

```yaml
gesBlock: case
id: keyed-unsorted-baseline
kind: schemaApi
level: atomic
schemaApi:
  sources: ["schema :Item as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 15\n  on: :Boolean\n}\n\nschema :World as {\n  tick: :Number integer between 0 and 255\n  items: :List(:Item) max 5\n}\n"]
  root: "World"
  deltaEncode: {"record": "090a280206", "baseline": "0912300105"}
  deltaDecode: {"delta": "56401130", "baseline": "0912300105"}
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaEncoded: {"bytes": "56401130"}
  deltaDecoded: {"record": "090a280206", "deltaByteCount": 4}
```

---

## Test: an unsorted record takes the whole list

Keyed list delta: the record does not ascend.

### Case description

```yaml
gesBlock: case
id: keyed-unsorted-record
kind: schemaApi
level: atomic
schemaApi:
  sources: ["schema :Item as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 15\n  on: :Boolean\n}\n\nschema :World as {\n  tick: :Number integer between 0 and 255\n  items: :List(:Item) max 5\n}\n"]
  root: "World"
  deltaEncode: {"record": "0912300105", "baseline": "090a280206"}
  deltaDecode: {"delta": "96800928", "baseline": "090a280206"}
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaEncoded: {"bytes": "96800928"}
  deltaDecoded: {"record": "0912300105", "deltaByteCount": 4}
```

---

## Test: duplicate keys in a baseline take the whole list

Duplicates are simply not ascending.

### Case description

```yaml
gesBlock: case
id: keyed-duplicate-baseline
kind: schemaApi
level: atomic
schemaApi:
  sources: ["schema :Item as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 15\n  on: :Boolean\n}\n\nschema :World as {\n  tick: :Number integer between 0 and 255\n  items: :List(:Item) max 5\n}\n"]
  root: "World"
  deltaEncode: {"record": "090928", "baseline": "090a280106"}
  deltaDecode: {"delta": "4e4001", "baseline": "090a280106"}
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaEncoded: {"bytes": "4e4001"}
  deltaDecoded: {"record": "090928", "deltaByteCount": 3}
```

---

## Test: duplicate keys in a record take the whole list

Duplicates are simply not ascending.

### Case description

```yaml
gesBlock: case
id: keyed-duplicate-record
kind: schemaApi
level: atomic
schemaApi:
  sources: ["schema :Item as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 15\n  on: :Boolean\n}\n\nschema :World as {\n  tick: :Number integer between 0 and 255\n  items: :List(:Item) max 5\n}\n"]
  root: "World"
  deltaEncode: {"record": "090a280106", "baseline": "090928"}
  deltaDecode: {"delta": "56400930", "baseline": "090928"}
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaEncoded: {"bytes": "56400930"}
  deltaDecoded: {"record": "090a280106", "deltaByteCount": 4}
```

---

## Test: an unchanged unsorted list has no payload

Equal lists are no change in any order.

### Case description

```yaml
gesBlock: case
id: keyed-unsorted-unchanged
kind: schemaApi
level: atomic
schemaApi:
  sources: ["schema :Item as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 15\n  on: :Boolean\n}\n\nschema :World as {\n  tick: :Number integer between 0 and 255\n  items: :List(:Item) max 5\n}\n"]
  root: "World"
  deltaEncode: {"record": "0912300105", "baseline": "0912300105"}
  deltaDecode: {"delta": "00", "baseline": "0912300105"}
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaEncoded: {"bytes": "00"}
  deltaDecoded: {"record": "0912300105", "deltaByteCount": 1}
```

---

## Test: a reused key is a change of the same object

The caller owns what a key means.

### Case description

```yaml
gesBlock: case
id: keyed-reused-key
kind: schemaApi
level: atomic
schemaApi:
  sources: ["schema :Item as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 15\n  on: :Boolean\n}\n\nschema :World as {\n  tick: :Number integer between 0 and 255\n  items: :List(:Item) max 5\n}\n"]
  root: "World"
  deltaEncode: {"record": "0909c8", "baseline": "090928"}
  deltaDecode: {"delta": "028213", "baseline": "090928"}
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaEncoded: {"bytes": "028213"}
  deltaDecoded: {"record": "0909c8", "deltaByteCount": 3}
```

---

## Test: a keyed list follows other changed fields

Keyed list delta: tick and list both change.

### Case description

```yaml
gesBlock: case
id: keyed-tick-and-list
kind: schemaApi
level: atomic
schemaApi:
  sources: ["schema :Item as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 15\n  on: :Boolean\n}\n\nschema :World as {\n  tick: :Number integer between 0 and 255\n  items: :List(:Item) max 5\n}\n"]
  root: "World"
  deltaEncode: {"record": "0a0930", "baseline": "090928"}
  deltaDecode: {"delta": "2b00820c", "baseline": "090928"}
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaEncoded: {"bytes": "2b00820c"}
  deltaDecoded: {"record": "0a0930", "deltaByteCount": 4}
```

---

## Test: the largest delta replaces every element

Keyed list delta at its bound, 13 bytes.

### Case description

```yaml
gesBlock: case
id: keyed-largest-replaced
kind: schemaApi
level: atomic
schemaApi:
  sources: ["schema :Item as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 15\n  on: :Boolean\n}\n\nschema :World as {\n  tick: :Number integer between 0 and 255\n  items: :List(:Item) max 5\n}\n"]
  root: "World"
  deltaEncode: {"record": "0a358807112126448508", "baseline": "090d0002600010800200"}
  deltaDecode: {"delta": "2b28a2b106f12022c484a81001", "baseline": "090d0002600010800200"}
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaEncoded: {"bytes": "2b28a2b106f12022c484a81001"}
  deltaDecoded: {"record": "0a358807112126448508", "deltaByteCount": 13}
```

---

## Test: the largest encoding counts the element form

1 + 8 + 1 + 1 + 3 * 3 + 5 * (3 + 13) = 100 bits.

### Case description

```yaml
gesBlock: case
id: keyed-largest-size
kind: schemaApi
level: atomic
schemaApi:
  sources: ["schema :Item as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 15\n  on: :Boolean\n}\n\nschema :World as {\n  tick: :Number integer between 0 and 255\n  items: :List(:Item) max 5\n}\n"]
  root: "World"
```

### Expectation

```yaml
gesBlock: expect
schema:
  maxByteCount: 13
```

---

## Test: the largest delta can change every element

The element delta exceeds the element: 1 + 1 + 3 * 2 + 2 * (1 + 5) = 20 bits.

### Case description

```yaml
gesBlock: case
id: keyed-largest-changed
kind: schemaApi
level: atomic
schemaApi:
  sources: ["schema :E as {\n  id: :Number integer between 0 and 1 key\n  v: :Number integer between 0 and 7 delta within 1\n}\n\nschema :L as {\n  l: :List(:E) max 2\n}\n"]
  root: "L"
  deltaEncode: {"record": "fa03", "baseline": "4200"}
```

### Expectation

```yaml
gesBlock: expect
schema:
  maxByteCount: 3
  deltaEncoded: {"bytes": "81fe0f"}
```

---

## Test: a keyed list works inside a nested field

Keyed list delta below a nested delta.

### Case description

```yaml
gesBlock: case
id: keyed-nested-field
kind: schemaApi
level: atomic
schemaApi:
  sources: ["schema :Item as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 15\n  on: :Boolean\n}\n\nschema :Box as {\n  items: :List(:Item) max 5\n}\n\nschema :Top as {\n  box: :Box\n}\n"]
  root: "Top"
  deltaEncode: {"record": "0a280206", "baseline": "0928"}
  deltaDecode: {"delta": "43040c00", "baseline": "0928"}
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaEncoded: {"bytes": "43040c00"}
  deltaDecoded: {"record": "0a280206", "deltaByteCount": 4}
```

---

## Test: a present optional keyed list takes the element form

Presence bit, then the keyed payload.

### Case description

```yaml
gesBlock: case
id: keyed-optional-both
kind: schemaApi
level: atomic
schemaApi:
  sources: ["schema :Item as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 15\n  on: :Boolean\n}\n\nschema :O as {\n  items: :List(:Item) max 5 optional\n}\n"]
  root: "O"
  deltaEncode: {"record": "1550040c", "baseline": "135000"}
  deltaDecode: {"delta": "43040c00", "baseline": "135000"}
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaEncoded: {"bytes": "43040c00"}
  deltaDecoded: {"record": "1550040c", "deltaByteCount": 4}
```

---

## Test: a new optional keyed list has no form bit

Without a value in the baseline a field takes its full encoding.

### Case description

```yaml
gesBlock: case
id: keyed-optional-new
kind: schemaApi
level: atomic
schemaApi:
  sources: ["schema :Item as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 15\n  on: :Boolean\n}\n\nschema :O as {\n  items: :List(:Item) max 5 optional\n}\n"]
  root: "O"
  deltaEncode: {"record": "135000", "baseline": "00"}
  deltaDecode: {"delta": "27a000", "baseline": "00"}
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaEncoded: {"bytes": "27a000"}
  deltaDecoded: {"record": "135000", "deltaByteCount": 3}
```

---

## Test: a removed optional keyed list has only its presence bit

Nothing follows an absent value.

### Case description

```yaml
gesBlock: case
id: keyed-optional-gone
kind: schemaApi
level: atomic
schemaApi:
  sources: ["schema :Item as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 15\n  on: :Boolean\n}\n\nschema :O as {\n  items: :List(:Item) max 5 optional\n}\n"]
  root: "O"
  deltaEncode: {"record": "00", "baseline": "135000"}
  deltaDecode: {"delta": "01", "baseline": "135000"}
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaEncoded: {"bytes": "01"}
  deltaDecoded: {"record": "00", "deltaByteCount": 1}
```

---

## Test: a keyed list works as a union alternative

The same alternative takes its payload.

### Case description

```yaml
gesBlock: case
id: keyed-union-alternative
kind: schemaApi
level: atomic
schemaApi:
  sources: ["schema :Item as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 15\n  on: :Boolean\n}\n\nschema :U as {\n  pick: :List(:Item) max 5 | :Boolean\n}\n"]
  root: "U"
  deltaEncode: {"record": "1450040c", "baseline": "125000"}
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaEncoded: {"bytes": "41040c00"}
```

---

## Test: a keyed list works inside a keyed element

Each level compares by its own key.

### Case description

```yaml
gesBlock: case
id: keyed-inside-keyed
kind: schemaApi
level: atomic
schemaApi:
  sources: ["schema :Leaf as {\n  id: :Number integer between 0 and 7 key\n  v: :Number integer between 0 and 3\n}\n\nschema :Node as {\n  id: :Number integer between 0 and 7 key\n  leaves: :List(:Leaf) max 2\n}\n\nschema :Tree as {\n  nodes: :List(:Node) max 2\n}\n"]
  root: "Tree"
  deltaEncode: {"record": "250d", "baseline": "2501"}
  deltaDecode: {"delta": "41821c", "baseline": "2501"}
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaEncoded: {"bytes": "41821c"}
  deltaDecoded: {"record": "250d", "deltaByteCount": 3}
```

---

## Test: an integer key without a range compares numerically

-1 sorts before 1, so this list takes the element form.

### Case description

```yaml
gesBlock: case
id: keyed-signed-order
kind: schemaApi
level: atomic
schemaApi:
  sources: ["schema :K as {\n  id: :Number integer key\n}\n\nschema :L as {\n  l: :List(:K) max 2\n}\n"]
  root: "L"
  deltaEncode: {"record": "feffffffffffffff070000000000000000", "baseline": "050000000000000000"}
  deltaDecode: {"delta": "d1ffffffffffffff3f", "baseline": "050000000000000000"}
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaEncoded: {"bytes": "d1ffffffffffffff3f"}
  deltaDecoded: {"record": "feffffffffffffff070000000000000000", "deltaByteCount": 9}
```

---

## Test: a negative key after a positive one does not ascend

Numeric order makes 1, -1 a whole list.

### Case description

```yaml
gesBlock: case
id: keyed-signed-unsorted
kind: schemaApi
level: atomic
schemaApi:
  sources: ["schema :K as {\n  id: :Number integer key\n}\n\nschema :L as {\n  l: :List(:K) max 2\n}\n"]
  root: "L"
  deltaEncode: {"record": "0600000000000000fcffffffffffffff03", "baseline": "050000000000000000"}
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaEncoded: {"bytes": "1b00000000000000f0ffffffffffffff0f"}
```

---

## Test: a Tag key compares in declared order

#z comes before #a.

### Case description

```yaml
gesBlock: case
id: keyed-tag-order
kind: schemaApi
level: atomic
schemaApi:
  sources: ["schema :K as {\n  t: :Tag(#z, #a) key\n  v: :Boolean\n}\n\nschema :L as {\n  l: :List(:K) max 2\n}\n"]
  root: "L"
  deltaEncode: {"record": "12", "baseline": "01"}
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaEncoded: {"bytes": "5100"}
```

---

## Test: an element with only a key is added or removed

It cannot change.

### Case description

```yaml
gesBlock: case
id: keyed-key-only
kind: schemaApi
level: atomic
schemaApi:
  sources: ["schema :K as {\n  id: :Number integer between 0 and 3 key\n}\n\nschema :L as {\n  l: :List(:K) max 2\n}\n"]
  root: "L"
  deltaEncode: {"record": "09", "baseline": "05"}
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaEncoded: {"bytes": "2501"}
```

---

## Test: the value path encodes a keyed list by element

The caller sends values; their list order decides the form.

### Case description

```yaml
gesBlock: case
id: keyed-value-path
kind: schemaApi
level: atomic
schemaApi:
  sources: ["schema :Item as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 15\n  on: :Boolean\n}\n\nschema :World as {\n  tick: :Number integer between 0 and 255\n  items: :List(:Item) max 5\n}\n"]
  root: "World"
  encode: {"type": ":World", "entries": [{"key": "items", "value": {"type": ":List", "items": [{"type": ":Item", "entries": [{"key": "hp", "value": {"type": ":Number.int64", "value": "5"}}, {"key": "id", "value": {"type": ":Number.int64", "value": "1"}}, {"key": "on", "value": {"type": ":Boolean", "value": false}}]}, {"type": ":Item", "entries": [{"key": "hp", "value": {"type": ":Number.int64", "value": "7"}}, {"key": "id", "value": {"type": ":Number.int64", "value": "3"}}, {"key": "on", "value": {"type": ":Boolean", "value": false}}]}, {"type": ":Item", "entries": [{"key": "hp", "value": {"type": ":Number.int64", "value": "3"}}, {"key": "id", "value": {"type": ":Number.int64", "value": "4"}}, {"key": "on", "value": {"type": ":Boolean", "value": true}}]}, {"type": ":Item", "entries": [{"key": "hp", "value": {"type": ":Number.int64", "value": "1"}}, {"key": "id", "value": {"type": ":Number.int64", "value": "5"}}, {"key": "on", "value": {"type": ":Boolean", "value": false}}]}, {"type": ":Item", "entries": [{"key": "hp", "value": {"type": ":Number.int64", "value": "9"}}, {"key": "id", "value": {"type": ":Number.int64", "value": "7"}}, {"key": "on", "value": {"type": ":Boolean", "value": false}}]}]}}, {"key": "tick", "value": {"type": ":Number.int64", "value": "9"}}]}
  baseline: {"type": ":World", "entries": [{"key": "items", "value": {"type": ":List", "items": [{"type": ":Item", "entries": [{"key": "hp", "value": {"type": ":Number.int64", "value": "5"}}, {"key": "id", "value": {"type": ":Number.int64", "value": "1"}}, {"key": "on", "value": {"type": ":Boolean", "value": false}}]}, {"type": ":Item", "entries": [{"key": "hp", "value": {"type": ":Number.int64", "value": "6"}}, {"key": "id", "value": {"type": ":Number.int64", "value": "2"}}, {"key": "on", "value": {"type": ":Boolean", "value": false}}]}, {"type": ":Item", "entries": [{"key": "hp", "value": {"type": ":Number.int64", "value": "7"}}, {"key": "id", "value": {"type": ":Number.int64", "value": "3"}}, {"key": "on", "value": {"type": ":Boolean", "value": false}}]}, {"type": ":Item", "entries": [{"key": "hp", "value": {"type": ":Number.int64", "value": "8"}}, {"key": "id", "value": {"type": ":Number.int64", "value": "5"}}, {"key": "on", "value": {"type": ":Boolean", "value": false}}]}, {"type": ":Item", "entries": [{"key": "hp", "value": {"type": ":Number.int64", "value": "9"}}, {"key": "id", "value": {"type": ":Number.int64", "value": "7"}}, {"key": "on", "value": {"type": ":Boolean", "value": false}}]}]}}, {"key": "tick", "value": {"type": ":Number.int64", "value": "9"}}]}
  decode: "4a4230b302"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded: {"bytes": "4a4230b302"}
  decoded: {"value": {"type": ":World", "entries": [{"key": "items", "value": {"type": ":List", "items": [{"type": ":Item", "entries": [{"key": "hp", "value": {"type": ":Number.int64", "value": "5"}}, {"key": "id", "value": {"type": ":Number.int64", "value": "1"}}, {"key": "on", "value": {"type": ":Boolean", "value": false}}]}, {"type": ":Item", "entries": [{"key": "hp", "value": {"type": ":Number.int64", "value": "7"}}, {"key": "id", "value": {"type": ":Number.int64", "value": "3"}}, {"key": "on", "value": {"type": ":Boolean", "value": false}}]}, {"type": ":Item", "entries": [{"key": "hp", "value": {"type": ":Number.int64", "value": "3"}}, {"key": "id", "value": {"type": ":Number.int64", "value": "4"}}, {"key": "on", "value": {"type": ":Boolean", "value": true}}]}, {"type": ":Item", "entries": [{"key": "hp", "value": {"type": ":Number.int64", "value": "1"}}, {"key": "id", "value": {"type": ":Number.int64", "value": "5"}}, {"key": "on", "value": {"type": ":Boolean", "value": false}}]}, {"type": ":Item", "entries": [{"key": "hp", "value": {"type": ":Number.int64", "value": "9"}}, {"key": "id", "value": {"type": ":Number.int64", "value": "7"}}, {"key": "on", "value": {"type": ":Boolean", "value": false}}]}]}}, {"key": "tick", "value": {"type": ":Number.int64", "value": "9"}}]}, "byteCount": 5}
```

---

## Test: whole-list form is rejected when both lists ascend

A delta has one valid encoding.

### Case description

```yaml
gesBlock: case
id: keyed-reject-whole-ascending
kind: schemaApi
level: atomic
schemaApi:
  sources: ["schema :Item as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 15\n  on: :Boolean\n}\n\nschema :World as {\n  tick: :Number integer between 0 and 255\n  items: :List(:Item) max 5\n}\n"]
  root: "World"
  deltaDecode: {"delta": "56401130", "baseline": "090928"}
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaDecoded: {"error": {"code": "wire.invalidDelta", "path": "items"}}
```

---

## Test: a whole list equal to its baseline is rejected

The change bit is set for an unchanged value.

### Case description

```yaml
gesBlock: case
id: keyed-reject-whole-equal
kind: schemaApi
level: atomic
schemaApi:
  sources: ["schema :Item as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 15\n  on: :Boolean\n}\n\nschema :World as {\n  tick: :Number integer between 0 and 255\n  items: :List(:Item) max 5\n}\n"]
  root: "World"
  deltaDecode: {"delta": "96800928", "baseline": "0912300105"}
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaDecoded: {"error": {"code": "wire.invalidDelta", "path": "items"}}
```

---

## Test: element form is rejected against an unsorted baseline

The merge needs ascending keys.

### Case description

```yaml
gesBlock: case
id: keyed-reject-unsorted-baseline
kind: schemaApi
level: atomic
schemaApi:
  sources: ["schema :Item as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 15\n  on: :Boolean\n}\n\nschema :World as {\n  tick: :Number integer between 0 and 255\n  items: :List(:Item) max 5\n}\n"]
  root: "World"
  deltaDecode: {"delta": "4a00", "baseline": "0912300105"}
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaDecoded: {"error": {"code": "wire.invalidDelta", "path": "items"}}
```

---

## Test: removed positions must ascend

Keyed list rejection.

### Case description

```yaml
gesBlock: case
id: keyed-reject-removed-order
kind: schemaApi
level: atomic
schemaApi:
  sources: ["schema :Item as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 15\n  on: :Boolean\n}\n\nschema :World as {\n  tick: :Number integer between 0 and 255\n  items: :List(:Item) max 5\n}\n"]
  root: "World"
  deltaDecode: {"delta": "520000", "baseline": "090b280266e000"}
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaDecoded: {"error": {"code": "wire.invalidDelta", "path": "items"}}
```

---

## Test: a removed position must not repeat

Keyed list rejection.

### Case description

```yaml
gesBlock: case
id: keyed-reject-removed-repeat
kind: schemaApi
level: atomic
schemaApi:
  sources: ["schema :Item as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 15\n  on: :Boolean\n}\n\nschema :World as {\n  tick: :Number integer between 0 and 255\n  items: :List(:Item) max 5\n}\n"]
  root: "World"
  deltaDecode: {"delta": "520200", "baseline": "090b280266e000"}
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaDecoded: {"error": {"code": "wire.invalidDelta", "path": "items"}}
```

---

## Test: a removed position must lie inside the baseline

Keyed list rejection.

### Case description

```yaml
gesBlock: case
id: keyed-reject-removed-outside
kind: schemaApi
level: atomic
schemaApi:
  sources: ["schema :Item as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 15\n  on: :Boolean\n}\n\nschema :World as {\n  tick: :Number integer between 0 and 255\n  items: :List(:Item) max 5\n}\n"]
  root: "World"
  deltaDecode: {"delta": "4a00", "baseline": "090928"}
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaDecoded: {"error": {"code": "wire.invalidDelta", "path": "items"}}
```

---

## Test: changed positions must ascend

Keyed list rejection.

### Case description

```yaml
gesBlock: case
id: keyed-reject-changed-order
kind: schemaApi
level: atomic
schemaApi:
  sources: ["schema :Item as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 15\n  on: :Boolean\n}\n\nschema :World as {\n  tick: :Number integer between 0 and 255\n  items: :List(:Item) max 5\n}\n"]
  root: "World"
  deltaDecode: {"delta": "02940205", "baseline": "090b280266e000"}
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaDecoded: {"error": {"code": "wire.invalidDelta", "path": "items"}}
```

---

## Test: a changed position must lie inside the baseline

Keyed list rejection.

### Case description

```yaml
gesBlock: case
id: keyed-reject-changed-outside
kind: schemaApi
level: atomic
schemaApi:
  sources: ["schema :Item as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 15\n  on: :Boolean\n}\n\nschema :World as {\n  tick: :Number integer between 0 and 255\n  items: :List(:Item) max 5\n}\n"]
  root: "World"
  deltaDecode: {"delta": "029202", "baseline": "090928"}
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaDecoded: {"error": {"code": "wire.invalidDelta", "path": "items"}}
```

---

## Test: one position cannot be removed and changed

Keyed list rejection.

### Case description

```yaml
gesBlock: case
id: keyed-reject-removed-changed
kind: schemaApi
level: atomic
schemaApi:
  sources: ["schema :Item as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 15\n  on: :Boolean\n}\n\nschema :World as {\n  tick: :Number integer between 0 and 255\n  items: :List(:Item) max 5\n}\n"]
  root: "World"
  deltaDecode: {"delta": "4a9014", "baseline": "090a280206"}
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaDecoded: {"error": {"code": "wire.invalidDelta", "path": "items"}}
```

---

## Test: added keys must ascend

Keyed list rejection.

### Case description

```yaml
gesBlock: case
id: keyed-reject-added-order
kind: schemaApi
level: atomic
schemaApi:
  sources: ["schema :Item as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 15\n  on: :Boolean\n}\n\nschema :World as {\n  tick: :Number integer between 0 and 255\n  items: :List(:Item) max 5\n}\n"]
  root: "World"
  deltaDecode: {"delta": "8208c00000", "baseline": "090928"}
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaDecoded: {"error": {"code": "wire.invalidDelta", "path": "items"}}
```

---

## Test: added keys must not repeat

Keyed list rejection.

### Case description

```yaml
gesBlock: case
id: keyed-reject-added-repeat
kind: schemaApi
level: atomic
schemaApi:
  sources: ["schema :Item as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 15\n  on: :Boolean\n}\n\nschema :World as {\n  tick: :Number integer between 0 and 255\n  items: :List(:Item) max 5\n}\n"]
  root: "World"
  deltaDecode: {"delta": "8206c04000", "baseline": "090928"}
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaDecoded: {"error": {"code": "wire.invalidDelta", "path": "items"}}
```

---

## Test: an added key must not equal a kept key

An element that stays is a change.

### Case description

```yaml
gesBlock: case
id: keyed-reject-added-kept
kind: schemaApi
level: atomic
schemaApi:
  sources: ["schema :Item as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 15\n  on: :Boolean\n}\n\nschema :World as {\n  tick: :Number integer between 0 and 255\n  items: :List(:Item) max 5\n}\n"]
  root: "World"
  deltaDecode: {"delta": "42040000", "baseline": "090a280206"}
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaDecoded: {"error": {"code": "wire.invalidDelta", "path": "items"}}
```

---

## Test: an added key must not equal a removed key

An element that stays is a change.

### Case description

```yaml
gesBlock: case
id: keyed-reject-added-removed
kind: schemaApi
level: atomic
schemaApi:
  sources: ["schema :Item as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 15\n  on: :Boolean\n}\n\nschema :World as {\n  tick: :Number integer between 0 and 255\n  items: :List(:Item) max 5\n}\n"]
  root: "World"
  deltaDecode: {"delta": "4a220000", "baseline": "090a280206"}
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaDecoded: {"error": {"code": "wire.invalidDelta", "path": "items"}}
```

---

## Test: all three counts zero are rejected

The change bit is set for an unchanged value.

### Case description

```yaml
gesBlock: case
id: keyed-reject-no-section
kind: schemaApi
level: atomic
schemaApi:
  sources: ["schema :Item as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 15\n  on: :Boolean\n}\n\nschema :World as {\n  tick: :Number integer between 0 and 255\n  items: :List(:Item) max 5\n}\n"]
  root: "World"
  deltaDecode: {"delta": "0200", "baseline": "090928"}
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaDecoded: {"error": {"code": "wire.invalidDelta", "path": "items"}}
```

---

## Test: a result longer than the list is rejected

Keyed list rejection.

### Case description

```yaml
gesBlock: case
id: keyed-reject-too-long
kind: schemaApi
level: atomic
schemaApi:
  sources: ["schema :Item as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 15\n  on: :Boolean\n}\n\nschema :World as {\n  tick: :Number integer between 0 and 255\n  items: :List(:Item) max 5\n}\n"]
  root: "World"
  deltaDecode: {"delta": "42120000", "baseline": "090d280266e014a08304"}
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaDecoded: {"error": {"code": "wire.invalidDelta", "path": "items"}}
```

---

## Test: a removed count above max is an invalid count

As in a full record.

### Case description

```yaml
gesBlock: case
id: keyed-reject-removed-count
kind: schemaApi
level: atomic
schemaApi:
  sources: ["schema :Item as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 15\n  on: :Boolean\n}\n\nschema :World as {\n  tick: :Number integer between 0 and 255\n  items: :List(:Item) max 5\n}\n"]
  root: "World"
  deltaDecode: {"delta": "3200", "baseline": "090928"}
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaDecoded: {"error": {"code": "wire.invalidCount", "path": "items"}}
```

---

## Test: an added count above max is an invalid count

As in a full record.

### Case description

```yaml
gesBlock: case
id: keyed-reject-added-count
kind: schemaApi
level: atomic
schemaApi:
  sources: ["schema :Item as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 15\n  on: :Boolean\n}\n\nschema :World as {\n  tick: :Number integer between 0 and 255\n  items: :List(:Item) max 5\n}\n"]
  root: "World"
  deltaDecode: {"delta": "8201", "baseline": "090928"}
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaDecoded: {"error": {"code": "wire.invalidCount", "path": "items"}}
```

---

## Test: a changed count above max is an invalid count

As in a full record.

### Case description

```yaml
gesBlock: case
id: keyed-reject-changed-count
kind: schemaApi
level: atomic
schemaApi:
  sources: ["schema :Item as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 15\n  on: :Boolean\n}\n\nschema :World as {\n  tick: :Number integer between 0 and 255\n  items: :List(:Item) max 5\n}\n"]
  root: "World"
  deltaDecode: {"delta": "020c", "baseline": "090928"}
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaDecoded: {"error": {"code": "wire.invalidCount", "path": "items"}}
```

---

## Test: a changed element with no change bit is rejected

The existing rule for nested deltas.

### Case description

```yaml
gesBlock: case
id: keyed-reject-changed-zero
kind: schemaApi
level: atomic
schemaApi:
  sources: ["schema :Item as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 15\n  on: :Boolean\n}\n\nschema :World as {\n  tick: :Number integer between 0 and 255\n  items: :List(:Item) max 5\n}\n"]
  root: "World"
  deltaDecode: {"delta": "020200", "baseline": "090928"}
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaDecoded: {"error": {"code": "wire.invalidDelta", "path": "items[]"}}
```

---

## Test: elements with Text, a union and a 64-bit number are passed over correctly

Removing, adding and changing elements skips their variable fields in every pass.

### Case description

```yaml
gesBlock: case
id: keyed-variable-fields
kind: schemaApi
level: atomic
schemaApi:
  sources: ["schema :Item as {\n  id: :Number integer between 0 and 255 key\n  name: :Text max 3 optional\n  mark: :Boolean | :Tag(#a, #b)\n  raw: :Number\n}\n\nschema :World as {\n  items: :List(:Item) max 5\n}\n"]
  root: "World"
  deltaEncode: {"record": "1b200000000000800030c18749c5040000000000c0fe2d48000000000000f83f", "baseline": "0b6898d8000000000000f83f0304000000000010002e08000000000000c03f"}
  deltaDecode: {"delta": "0521f830a998000000000000d8bf91030000000000c0ff01", "baseline": "0b6898d8000000000000f83f0304000000000010002e08000000000000c03f"}
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaEncoded: {"bytes": "0521f830a998000000000000d8bf91030000000000c0ff01"}
  deltaDecoded: {"record": "1b200000000000800030c18749c5040000000000c0fe2d48000000000000f83f", "deltaByteCount": 24}
```

---

## Test: an added key equal to a baseline key fails before a later changed element

The added section precedes the changed one on the wire, so its baseline check comes first.

### Case description

```yaml
gesBlock: case
id: keyed-reject-order-added
kind: schemaApi
level: atomic
schemaApi:
  sources: ["schema :Item as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 15\n  on: :Boolean\n}\n\nschema :World as {\n  tick: :Number integer between 0 and 255\n  items: :List(:Item) max 5\n}\n"]
  root: "World"
  deltaDecode: {"delta": "420a4000", "baseline": "090a100513"}
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaDecoded: {"error": {"code": "wire.invalidDelta", "path": "items"}}
```

---

## Test: an unsorted baseline fails before any changed element

The check belongs to the form bit, which comes first on the wire.

### Case description

```yaml
gesBlock: case
id: keyed-reject-order-baseline
kind: schemaApi
level: atomic
schemaApi:
  sources: ["schema :Item as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 15\n  on: :Boolean\n}\n\nschema :World as {\n  tick: :Number integer between 0 and 255\n  items: :List(:Item) max 5\n}\n"]
  root: "World"
  deltaDecode: {"delta": "020200", "baseline": "092a980102"}
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaDecoded: {"error": {"code": "wire.invalidDelta", "path": "items"}}
```

---

## Test: an element with only a key cannot change

It has no bits for a change.

### Case description

```yaml
gesBlock: case
id: keyed-reject-key-only-changed
kind: schemaApi
level: atomic
schemaApi:
  sources: ["schema :K as {\n  id: :Number integer between 0 and 3 key\n}\n\nschema :L as {\n  l: :List(:K) max 2\n}\n"]
  root: "L"
  deltaDecode: {"delta": "4100", "baseline": "05"}
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaDecoded: {"error": {"code": "wire.invalidDelta", "path": "l[]"}}
```

---

## Test: the largest whole list stays below the bound

An unsorted record of five elements takes the whole-list form within the same buffer: 79 of 100 bits.

### Case description

```yaml
gesBlock: case
id: keyed-largest-whole
kind: schemaApi
level: atomic
schemaApi:
  sources: ["schema :Item as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 15\n  on: :Boolean\n}\n\nschema :World as {\n  tick: :Number integer between 0 and 255\n  items: :List(:Item) max 5\n}\n"]
  root: "World"
  deltaEncode: {"record": "0a4d8808f1201ac48208", "baseline": "090900"}
```

### Expectation

```yaml
gesBlock: expect
schema:
  maxByteCount: 13
  deltaEncoded: {"bytes": "2b6c42448807d1201644"}
```
