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

## Test: names of platform types

A schema and fields may take names that the platforms give their own types, such as `Math`,
`system` and `array`: generated C# qualifies the platform types it uses, and the generation
comparison compiles the code of this case.

### Case description

```yaml
gesBlock: case
id: platform-type-names
schemaApi:
  sources:
    - "schema :Math as {\n  system: :Text max 8\n  math: :Number integer between 0 and 9\n  array: :Bytes max 4\n  bitConverter: :Number between 0 and 1\n  flags: :Flags(#a, #b)\n}\n"
  root: "Math"
  encode:
    type: ":Math"
    entries:
      - key: "array"
        value:
          type: ":Bytes"
          value: "a0b7"
      - key: "bitConverter"
        value:
          type: ":Number.binary64"
          value: "0.5"
      - key: "flags"
        value:
          type: ":Map"
          entries:
            - key: "a"
              value:
                type: ":Boolean"
                value: "true"
            - key: "b"
              value:
                type: ":Boolean"
                value: "false"
      - key: "math"
        value:
          type: ":Number.int64"
          value: "3"
      - key: "system"
        value:
          type: ":Text"
          value: "sol"
```

### Expectation

```yaml
gesBlock: expect
schema:
  canonical: "gess1\nschema Math\nfield system Text max 8\nfield math Number integer between 0e0 9e0\nfield array Bytes max 4\nfield bitConverter Number between 0e0 1e0\nfield flags Flags(#a #b)\n"
  hash: "6ee045515b30c8ee"
  encoded:
    bytes: "33f7c63602bd05000000000000ff09"
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

## Test: digit separators in a maximum

A maximum may separate digits with `_`, as number literals do; the canonical form writes plain digits.

### Case description

```yaml
gesBlock: case
id: canonical-max-separators
schemaApi:
  sources:
    - "schema :Log as { lines: :List(:Text max 1_024) max 10_000 }\n"
  root: "Log"
```

### Expectation

```yaml
gesBlock: expect
schema:
  canonical: "gess1\nschema Log\nfield lines List(Text max 1024) max 10000\n"
  hash: "a1f97b9aa3b41bf8"
```

---

## Test: a doubled separator in a maximum

A separator stands only between two digits, so the maximum ends before it.

### Case description

```yaml
gesBlock: case
id: error-max-double-separator
schemaApi:
  sources:
    - "schema :S as { a: :Text max 4__234 }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.syntax"
    line: 1
    column: 30
```

---

## Test: a leading zero before a separator

A separator does not hide a leading zero.

### Case description

```yaml
gesBlock: case
id: error-max-leading-zero-separator
schemaApi:
  sources:
    - "schema :S as { a: :Text max 0_1 }\n"
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

## Test: a named tag set is its members

A field of a named tag set has the canonical form and hash of the same `:Tag` written in place.

### Case description

```yaml
gesBlock: case
id: canonical-tag-set
schemaApi:
  sources:
    - "tags :Mount as (#front, #rear)\nschema :Bot as { mount: :Mount optional, mounts: :List(:Mount) max 2 }\n"
  root: "Bot"
```

### Expectation

```yaml
gesBlock: expect
schema:
  canonical: "gess1\nschema Bot\nfield mount Tag(#front #rear) optional\nfield mounts List(Tag(#front #rear)) max 2\n"
  hash: "dd8517bdb2464d84"
```

---

## Test: a tag set named like a schema

A tag set shares its name space with schemas, messages and message sets.

### Case description

```yaml
gesBlock: case
id: error-tag-set-name-taken
schemaApi:
  sources:
    - "tags :Mount as (#a)\nschema :Mount as { x: :Boolean }\n"
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

## Test: a tag set that repeats a tag

Each member of a tag set is listed once.

### Case description

```yaml
gesBlock: case
id: error-tag-set-repeated-tag
schemaApi:
  sources:
    - "tags :M as (#a, #a)\n"
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

## Test: a tag set as a root

Only a schema, message or message set takes a role.

### Case description

```yaml
gesBlock: case
id: error-tag-set-root
schemaApi:
  sources:
    - "wire tags :M as (#a)\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidRole"
    line: 1
    column: 1
```

---

## Test: a tag set beside a Tag in a union

A named tag set is a Tag, so a union cannot hold it beside another Tag.

### Case description

```yaml
gesBlock: case
id: error-tag-set-union
schemaApi:
  sources:
    - "tags :M as (#a)\nschema :S as { u: :M | :Tag(#b) }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidUnion"
    line: 2
    column: 24
```

---

## Test: a union label is no part of the hash

A label only names an alternative for generated code, so the canonical form and hash leave it out.

### Case description

```yaml
gesBlock: case
id: canonical-alternative-label
schemaApi:
  sources:
    - "schema :Bind as { id: :Number integer between 0 and 9 }\nschema :S as { units: :List(:Text max 64 | bind: :Bind) max 4 }\n"
  root: "S"
```

### Expectation

```yaml
gesBlock: expect
schema:
  canonical: "gess1\nschema S\nfield units List(Text max 64 | Bind) max 4\nschema Bind\nfield id Number integer between 0e0 9e0\n"
  hash: "e53e48d424e29431"
```

---

## Test: a label outside a union

A label names an alternative of a union.

### Case description

```yaml
gesBlock: case
id: error-label-outside-union
schemaApi:
  sources:
    - "schema :S as { a: x: :Text }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidUnion"
    line: 1
    column: 19
```

---

## Test: a label on Nothing

A `:Nothing` alternative holds no value to label.

### Case description

```yaml
gesBlock: case
id: error-label-on-nothing
schemaApi:
  sources:
    - "schema :S as { a: :Text | none: :Nothing }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidUnion"
    line: 1
    column: 27
```

---

## Test: a label that repeats an alternative name

Generated code names alternatives after their labels or types, so they must differ.

### Case description

```yaml
gesBlock: case
id: error-label-repeats-name
schemaApi:
  sources:
    - "schema :S as { a: text: :Boolean | :Text }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.duplicateName"
    line: 1
    column: 36
```

---

## Test: a schema constant

A `let` names one value of its schema: bounds, steps, delta ranges and maximums of its fields may name it, and it is replaced there. The canonical form lists the lets in declared order before the fields, so their place among the fields does not matter, and a field may still be named `let`.

### Case description

```yaml
gesBlock: case
id: canonical-let
schemaApi:
  sources:
    - "constant $cap be 4\nschema :Sound as {\n  span: :Quantity(s) between 0s and retention step 0.1s delta within drift\n  names: :List(:Text max 8) max slots\n  let: :Boolean\n  let retention be 2s\n  let drift be 500ms\n  let slots be $cap\n  let share be 50%\n}\n"
  root: "Sound"
```

### Expectation

```yaml
gesBlock: expect
schema:
  canonical: "gess1\nschema Sound\nlet retention Quantity(second) 2e0\nlet drift Quantity(second) 5e-1\nlet slots Number 4e0\nlet share Percentage 5e-1\nfield span Quantity(second) between 0e0 2e0 step 1e-1 delta 5e-1\nfield names List(Text max 8) max 4\nfield let Boolean\n"
  hash: "57dfa98124dc9ccb"
```

---

## Test: the description of schema constants

Each `let` keeps its kind, unit and exact value in base units; a whole unitless value within Int64 is an integer.

### Case description

```yaml
gesBlock: case
id: describe-let
schemaApi:
  sources:
    - "schema :Sound as {\n  let retention be 2s\n  let slots be 16\n  let share be 50%\n  let ratio be 0.25\n  on: :Boolean\n}\n"
  root: "Sound"
  bind: false
  describe: true
```

### Expectation

```yaml
gesBlock: expect
schema:
  description:
    name: "Sound"
    kind: "Schema"
    fields:
      - name: "on"
        alternatives:
          - kind: "Boolean"
    constants:
      - name: "retention"
        kind: "Quantity"
        unit: "second"
        value: "2e0"
      - name: "slots"
        kind: "Number"
        integer: true
        value: "16e0"
      - name: "share"
        kind: "Percentage"
        value: "5e-1"
      - name: "ratio"
        kind: "Number"
        value: "25e-2"
```

---

## Test: a constant named like a field

A `let` and a field share the names of their schema.

### Case description

```yaml
gesBlock: case
id: error-let-field-name
schemaApi:
  sources:
    - "schema :S as {\n  let n be 4\n  n: :Boolean\n}\n"
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

## Test: a constant named like a parameter

A bound may name either a `let` or a parameter, so a `let` cannot take the name of a parameter of its file.

### Case description

```yaml
gesBlock: case
id: error-let-parameter-name
schemaApi:
  sources:
    - "parameter n: :Number\nschema :S as {\n  let n be 4\n}\n"
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

## Test: a constant of another unit

A `let` in a bound has the unit of its field.

### Case description

```yaml
gesBlock: case
id: error-let-unit
schemaApi:
  sources:
    - "schema :S as {\n  let n be 4s\n  x: :Quantity(m) between 0m and n\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.unitMismatch"
    line: 3
    column: 34
```

---

## Test: a maximum from a constant with a unit

A maximum is a whole number without a unit.

### Case description

```yaml
gesBlock: case
id: error-let-max-unit
schemaApi:
  sources:
    - "schema :S as {\n  let n be 4s\n  t: :Text max n\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.unitMismatch"
    line: 3
    column: 16
```

---

## Test: a fractional maximum from a constant

A `let` that a maximum names is a positive whole number of at most 12 digits.

### Case description

```yaml
gesBlock: case
id: error-let-max-fraction
schemaApi:
  sources:
    - "schema :S as {\n  let n be 2.5\n  t: :Text max n\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidConstraint"
    line: 3
    column: 16
```

---

## Test: a step that names a parameter

Steps and delta ranges never name a parameter, so a name there must be a `let` of the schema.

### Case description

```yaml
gesBlock: case
id: error-let-step-parameter
schemaApi:
  sources:
    - "parameter w: :Quantity(m)\nschema :S as {\n  x: :Quantity(m) between 0m and 10m step w\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.unknownName"
    line: 3
    column: 43
```

---

## Test: a constant that names another

A `let` is a literal or a file constant; it does not name another `let`.

### Case description

```yaml
gesBlock: case
id: error-let-names-let
schemaApi:
  sources:
    - "schema :S as {\n  let a be 1\n  let b be a\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.syntax"
    line: 3
    column: 12
```

---

## Test: a constant beyond binary64

A value that is no whole number within Int64 is a binary64, so it must stay finite and non-zero there.

### Case description

```yaml
gesBlock: case
id: error-let-beyond-binary64
schemaApi:
  sources:
    - "schema :S as {\n  let huge be 1e400\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.tooWide"
    line: 2
    column: 3
```

---

## Test: a constant in a message

Only schemas declare constants, so a message argument cannot name one.

### Case description

```yaml
gesBlock: case
id: error-let-in-message
schemaApi:
  sources:
    - "message M(x: :Number between 0 and n)\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.unknownName"
    line: 1
    column: 36
```

---


## Test: field defaults in the canonical form

A default follows the marker as an exact number in base units, `true` or `false`, a tag, or `field` and the sibling it names; a let or constant is replaced by its value. A field may still be named `default`.

### Case description

```yaml
gesBlock: case
id: canonical-default
schemaApi:
  sources:
    - "constant $full be 100%\nschema :Drive as {\n  let waypoints be 32\n  power: :Quantity(W) between 0W and 5kW step 1W default 1.5kW\n  maxPower: :Quantity(W) between 0W and 5kW step 1W delta within 1kW default power\n  efficiency: :Percentage between 0% and 100% default $full\n  stops: :Number integer between 1 and 1024 default waypoints\n  armed: :Boolean default false\n  mount: :Tag(#front, #rear) default #rear\n  default: :Text max 4\n}\n"
  root: "Drive"
```

### Expectation

```yaml
gesBlock: expect
schema:
  canonical: "gess1\nschema Drive\nlet waypoints Number 32e0\nfield power Quantity(watt) between 0e0 5e3 step 1e0 default 15e2\nfield maxPower Quantity(watt) between 0e0 5e3 step 1e0 delta 1e3 default field power\nfield efficiency Percentage between 0e0 1e0 default 1e0\nfield stops Number integer between 1e0 1024e0 default 32e0\nfield armed Boolean default false\nfield mount Tag(#front #rear) default #rear\nfield default Text max 4\n"
  hash: "987d354c1fb999d2"
```

---

## Test: the description of field defaults

Each default keeps its kind: a number as its exact value in base units, a Boolean, a tag without `#`, or the sibling field.

### Case description

```yaml
gesBlock: case
id: describe-default
schemaApi:
  sources:
    - "tags :Mount as (#front, #rear)\nschema :Drive as {\n  power: :Quantity(W) default 2kW\n  maxPower: :Quantity(W) default power\n  share: :Percentage default 50%\n  on: :Boolean default true\n  mount: :Mount default #rear\n  wet: :Text optional default dry\n  dry: :Text optional\n}\n"
  root: "Drive"
  bind: false
  describe: true
```

### Expectation

```yaml
gesBlock: expect
schema:
  description:
    name: "Drive"
    kind: "Schema"
    fields:
      - name: "power"
        alternatives:
          - kind: "Quantity"
            unit: "watt"
        default:
          kind: "Number"
          value: "2e3"
      - name: "maxPower"
        alternatives:
          - kind: "Quantity"
            unit: "watt"
        default:
          kind: "Field"
          name: "power"
      - name: "share"
        alternatives:
          - kind: "Percentage"
        default:
          kind: "Number"
          value: "5e-1"
      - name: "on"
        alternatives:
          - kind: "Boolean"
        default:
          kind: "Boolean"
          value: true
      - name: "mount"
        alternatives:
          - kind: "Tag"
            tags: ["front", "rear"]
        default:
          kind: "Tag"
          name: "rear"
      - name: "wet"
        alternatives:
          - kind: "Text"
        optional: true
        default:
          kind: "Field"
          name: "dry"
      - name: "dry"
        alternatives:
          - kind: "Text"
        optional: true
```

---

## Test: a value takes the defaults of the fields it leaves out

A left-out field takes its literal default, or the value of the sibling it names, after that sibling's own default; the wire carries every field, and decoding gives them all.

### Case description

```yaml
gesBlock: case
id: encode-default
schemaApi:
  sources:
    - "schema :S as {\n  max: :Number integer between 0 and 15 default power\n  power: :Number integer between 0 and 15 default 3\n  on: :Boolean default true\n  mode: :Tag(#a, #b) default #b\n}\n"
  root: "S"
  encode:
    type: ":S"
    entries: []
  decode: "3303"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "3303"
  decoded:
    value:
      type: ":S"
      entries:
        - key: "max"
          value:
            type: ":Number.int64"
            value: "3"
        - key: "mode"
          value:
            type: ":Tag"
            value: "b"
        - key: "on"
          value:
            type: ":Boolean"
            value: true
        - key: "power"
          value:
            type: ":Number.int64"
            value: "3"
    byteCount: 2
```

---

## Test: a given sibling is the default

A field that names a sibling takes the value given for it.

### Case description

```yaml
gesBlock: case
id: encode-default-sibling
schemaApi:
  sources:
    - "schema :S as {\n  max: :Number integer between 0 and 15 default power\n  power: :Number integer between 0 and 15 default 3\n}\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "power"
        value:
          type: ":Number.int64"
          value: "7"
  decode: "77"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "77"
  decoded:
    value:
      type: ":S"
      entries:
        - key: "max"
          value:
            type: ":Number.int64"
            value: "7"
        - key: "power"
          value:
            type: ":Number.int64"
            value: "7"
    byteCount: 1
```

---

## Test: defaults apply in nested records

A nested record or Map takes the defaults of its own schema.

### Case description

```yaml
gesBlock: case
id: encode-default-nested
schemaApi:
  sources:
    - "schema :Inner as { level: :Percentage between 0% and 100% step 1% default 25% }\nschema :S as { inner: :Inner }\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "inner"
        value:
          type: ":Map"
          entries: []
  decode: "19"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "19"
  decoded:
    value:
      type: ":S"
      entries:
        - key: "inner"
          value:
            type: ":Inner"
            entries:
              - key: "level"
                value:
                  type: ":Percentage"
                  value: "0.25"
    byteCount: 1
```

---

## Test: an optional field takes an absent sibling

An optional field may default only to an optional sibling; when that is absent too, both are absent.

### Case description

```yaml
gesBlock: case
id: encode-default-optional
schemaApi:
  sources:
    - "schema :S as {\n  wet: :Number integer between 0 and 7 optional default dry\n  dry: :Number integer between 0 and 7 optional\n}\n"
  root: "S"
  encode:
    type: ":S"
    entries: []
  decode: "00"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "00"
  decoded:
    value:
      type: ":S"
      entries:
        - key: "dry"
          value:
            type: ":Nothing"
        - key: "wet"
          value:
            type: ":Nothing"
    byteCount: 1
```

---

## Test: a default chain that ends without a value

A required field whose sibling has neither a value nor a default is missing at the sibling's path.

### Case description

```yaml
gesBlock: case
id: encode-default-missing
schemaApi:
  sources:
    - "schema :S as {\n  a: :Number integer between 0 and 7 default b\n  b: :Number integer between 0 and 7\n}\n"
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
      path: "b"
```

---

## Test: a default does not replace nothing

Only a left-out field takes its default; `nothing` for a required field stays an error.

### Case description

```yaml
gesBlock: case
id: encode-default-nothing
schemaApi:
  sources:
    - "schema :S as { a: :Number integer between 0 and 7 default 1 }\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "a"
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

## Test: a document may leave out a field with a default

Document checking accepts a left-out field that has a default.

### Case description

```yaml
gesBlock: case
id: document-default-omitted
schemaApi:
  sources:
    - "schema :S as {\n  power: :Quantity(W) default 0W\n  max: :Quantity(W) default power\n  on: :Boolean\n}\n"
  document: ":Schema(:S)\non: true\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    ok: true
```

---

## Test: a default outside a range from a parameter

A bound that a parameter decides checks the default at binding.

### Case description

```yaml
gesBlock: case
id: error-default-parameter-range
schemaApi:
  sources:
    - "parameter limit: :Quantity(m)\nschema :S as {\n  a: :Quantity(m) between 0m and limit default 5m\n}\n"
  root: "S"
  parameters:
    limit: "3m"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidDefault"
    line: 3
    column: 40
```

---

## Test: a default outside the range

A literal default lies in the field's closed range.

### Case description

```yaml
gesBlock: case
id: error-default-range
schemaApi:
  sources:
    - "schema :S as {\n  a: :Number integer between 0 and 10 default 11\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidDefault"
    line: 2
    column: 39
```

---

## Test: a default at the upper bound of a wrapping range

A wrapping range is half-open, so its upper bound is no value.

### Case description

```yaml
gesBlock: case
id: error-default-wrapping
schemaApi:
  sources:
    - "schema :S as {\n  a: :Quantity(\u00b0) wrapping between 0\u00b0 and 360\u00b0 step 1\u00b0 default 360\u00b0\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidDefault"
    line: 2
    column: 56
```

---

## Test: a default off the step

A default of a quantized field is a multiple of its step.

### Case description

```yaml
gesBlock: case
id: error-default-step
schemaApi:
  sources:
    - "schema :S as {\n  a: :Quantity(m) between 0m and 10m step 1m default 2.5m\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidDefault"
    line: 2
    column: 46
```

---

## Test: a fractional default of an integer field

An integer field takes a whole default.

### Case description

```yaml
gesBlock: case
id: error-default-integer
schemaApi:
  sources:
    - "schema :S as {\n  a: :Number integer default 1.5\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidDefault"
    line: 2
    column: 22
```

---

## Test: a default beyond binary64

A fractional default keeps its meaning in binary64.

### Case description

```yaml
gesBlock: case
id: error-default-binary64
schemaApi:
  sources:
    - "schema :S as {\n  a: :Number default 1e-400\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.tooWide"
    line: 2
    column: 14
```

---

## Test: a default of another unit

A numeric default has its field's unit.

### Case description

```yaml
gesBlock: case
id: error-default-unit
schemaApi:
  sources:
    - "schema :S as {\n  a: :Quantity(m) default 5s\n}\n"
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

## Test: a default that names nothing

A name is a sibling field or a let of the schema.

### Case description

```yaml
gesBlock: case
id: error-default-unknown
schemaApi:
  sources:
    - "schema :S as {\n  a: :Number default b\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.unknownName"
    line: 2
    column: 22
```

---

## Test: defaults that form a cycle

Chains of sibling defaults resolve in dependency order, so they cannot form a cycle.

### Case description

```yaml
gesBlock: case
id: error-default-cycle
schemaApi:
  sources:
    - "schema :S as {\n  a: :Number default b\n  b: :Number default a\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.cycle"
    line: 2
    column: 14
```

---

## Test: a default sibling of another type

A sibling default has the field's type and constraints; only delta ranges may differ.

### Case description

```yaml
gesBlock: case
id: error-default-type
schemaApi:
  sources:
    - "schema :S as {\n  a: :Number integer between 0 and 9 default b\n  b: :Number integer between 0 and 8\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidDefault"
    line: 2
    column: 38
```

---

## Test: a sibling default of a type of its own

A closed Tag written in place, Flags and a union get a generated type per field, so they take no sibling default; a tag set can.

### Case description

```yaml
gesBlock: case
id: error-default-own-type
schemaApi:
  sources:
    - "schema :S as {\n  a: :Tag(#x, #y) default b\n  b: :Tag(#x, #y)\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidDefault"
    line: 2
    column: 19
```

---

## Test: a literal default of an optional field

An optional field takes its default only from an optional sibling.

### Case description

```yaml
gesBlock: case
id: error-default-optional-literal
schemaApi:
  sources:
    - "schema :S as {\n  a: :Number optional default 1\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidDefault"
    line: 2
    column: 23
```

---

## Test: an optional field that defaults to a required one

A field and the sibling it names are both optional or both not.

### Case description

```yaml
gesBlock: case
id: error-default-optional-sibling
schemaApi:
  sources:
    - "schema :S as {\n  a: :Number optional default b\n  b: :Number\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidDefault"
    line: 2
    column: 23
```

---

## Test: a key with a default

A key identifies a record and takes no default.

### Case description

```yaml
gesBlock: case
id: error-default-key
schemaApi:
  sources:
    - "schema :S as {\n  a: :Number integer key default 1\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidDefault"
    line: 2
    column: 26
```

---

## Test: a default tag outside the set

A tag default belongs to the field's set.

### Case description

```yaml
gesBlock: case
id: error-default-tag
schemaApi:
  sources:
    - "schema :S as {\n  a: :Tag(#x, #y) default #z\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidDefault"
    line: 2
    column: 19
```

---

## Test: a default tag above its maximum

A tag default of a `:Tag max n` has at most `n` bytes.

### Case description

```yaml
gesBlock: case
id: error-default-tag-max
schemaApi:
  sources:
    - "schema :S as {\n  a: :Tag max 2 default #abc\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidDefault"
    line: 2
    column: 17
```

---

## Test: a default of another kind

A Boolean field takes `true` or `false`.

### Case description

```yaml
gesBlock: case
id: error-default-kind
schemaApi:
  sources:
    - "schema :S as {\n  a: :Boolean default 1\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidDefault"
    line: 2
    column: 15
```

---

## Test: a literal default of a union

A union takes its default only from a sibling field.

### Case description

```yaml
gesBlock: case
id: error-default-union
schemaApi:
  sources:
    - "schema :S as {\n  a: :Number | :Text default 1\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidDefault"
    line: 2
    column: 22
```

---

## Test: a default of a message argument

Defaults belong to schema fields; message arguments have none.

### Case description

```yaml
gesBlock: case
id: error-default-message
schemaApi:
  sources:
    - "message M(a: :Number default 1)\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.syntax"
    line: 1
    column: 22
```

---

## Test: record defaults in the canonical form

A record literal lists the fields it names in the field order of their schema, each value exact in base units, and `[:]` names none; a constant is replaced by its value.

### Case description

```yaml
gesBlock: case
id: canonical-default-record
schemaApi:
  sources:
    - "constant $half be 50%\nschema :C as {\n  power: :Number integer between 0 and 15 default 3\n  level: :Percentage between 0% and 100% step 10% default 100%\n  on: :Boolean default true\n}\nschema :Outer as { c: :C default [on: false] }\nschema :S as {\n  empty: :C default [:]\n  partial: :C default [on: false, level: $half]\n  nested: :Outer default [c: [power: 7]]\n}\n"
  root: "S"
```

### Expectation

```yaml
gesBlock: expect
schema:
  canonical: "gess1\nschema S\nfield empty C default [:]\nfield partial C default [level: 5e-1, on: false]\nfield nested Outer default [c: [power: 7e0]]\nschema C\nfield power Number integer between 0e0 15e0 default 3e0\nfield level Percentage between 0e0 1e0 step 1e-1 default 1e0\nfield on Boolean default true\nschema Outer\nfield c C default [on: false]\n"
  hash: "64574f645f09b5b1"
```

---

## Test: a record literal across lines

Comments and line breaks may stand between all tokens of a record literal, also between a field name and its colon.

### Case description

```yaml
gesBlock: case
id: canonical-default-record-lines
schemaApi:
  sources:
    - "schema :C as {\n  power: :Number default 3\n  on: :Boolean default true\n}\nschema :S as {\n  c: :C default [ // the defaults\n    on // the switch\n      : false,\n    power: 7\n  ]\n}\n"
  root: "S"
```

### Expectation

```yaml
gesBlock: expect
schema:
  canonical: "gess1\nschema S\nfield c C default [power: 7e0, on: false]\nschema C\nfield power Number default 3e0\nfield on Boolean default true\n"
  hash: "3caaafdc0d6ced9b"
```

---

## Test: the description of a record default

A record default lists the fields it names with their values in field order, a nested literal as a record of its own.

### Case description

```yaml
gesBlock: case
id: describe-default-record
schemaApi:
  sources:
    - "schema :C as {\n  power: :Number integer between 0 and 15 default 3\n  level: :Percentage between 0% and 100% step 10% default 100%\n  on: :Boolean default true\n}\nschema :Outer as { c: :C }\nschema :S as {\n  empty: :C default [:]\n  nested: :Outer default [c: [on: false, power: 7]]\n}\n"
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
      - name: "empty"
        alternatives:
          - kind: "Reference"
            schema: "C"
        default:
          kind: "Record"
          entries: []
      - name: "nested"
        alternatives:
          - kind: "Reference"
            schema: "Outer"
        default:
          kind: "Record"
          entries:
            - name: "c"
              value:
                kind: "Record"
                entries:
                  - name: "power"
                    value:
                      kind: "Number"
                      value: "7e0"
                  - name: "on"
                    value:
                      kind: "Boolean"
                      value: false
```

---

## Test: a left-out record takes the defaults of its schema

`[:]` names no field, so each field of the left-out record takes its own default.

### Case description

```yaml
gesBlock: case
id: encode-default-record-empty
schemaApi:
  sources:
    - "schema :C as {\n  power: :Number integer between 0 and 15 default 3\n  level: :Percentage between 0% and 100% step 10% default 100%\n  on: :Boolean default true\n}\nschema :S as { c: :C default [:] }\n"
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
    bytes: "a301"
```

---

## Test: a left-out record takes its literal

The fields a record literal names take its values; every other field takes its own default.

### Case description

```yaml
gesBlock: case
id: encode-default-record-partial
schemaApi:
  sources:
    - "schema :C as {\n  power: :Number integer between 0 and 15 default 3\n  level: :Percentage between 0% and 100% step 10% default 100%\n  on: :Boolean default true\n}\nschema :S as { c: :C default [on: false, level: 50%] }\n"
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
    bytes: "5300"
```

---

## Test: a nested record literal

A record literal of a field whose type is again a schema writes that record as a nested literal, whose left-out fields take their own defaults.

### Case description

```yaml
gesBlock: case
id: encode-default-record-nested
schemaApi:
  sources:
    - "schema :C as {\n  power: :Number integer between 0 and 15 default 3\n  level: :Percentage between 0% and 100% step 10% default 100%\n  on: :Boolean default true\n}\nschema :Outer as {\n  c: :C\n  count: :Number integer between 0 and 7 default 1\n}\nschema :S as { o: :Outer default [c: [power: 7]] }\n"
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
    bytes: "a703"
```

---

## Test: a given record ignores the literal

The literal applies only when the record is left out; a given record takes the defaults of its own schema for the fields it leaves out.

### Case description

```yaml
gesBlock: case
id: encode-default-record-present
schemaApi:
  sources:
    - "schema :C as {\n  power: :Number integer between 0 and 15 default 3\n  level: :Percentage between 0% and 100% step 10% default 100%\n  on: :Boolean default true\n}\nschema :S as { c: :C default [on: false, level: 50%] }\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "c"
        value:
          type: ":Map"
          entries:
            - key: "power"
              value:
                type: ":Number.int64"
                value: "9"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "a901"
```

---

## Test: a taken record default

`with` takes a record default together with its field, also from an abstract schema; `all optional` drops it as any other default.

### Case description

```yaml
gesBlock: case
id: canonical-default-record-with
schemaApi:
  sources:
    - "schema :C as {\n  power: :Number integer between 0 and 15 default 3\n  level: :Percentage between 0% and 100% step 10% default 100%\n  on: :Boolean default true\n}\nabstract schema :Base as {\n  c: :C default [power: 1]\n  d: :C default [:]\n}\nschema :S as {\n  with :Base without (d)\n  with :Base all optional without (c)\n}\n"
  root: "S"
```

### Expectation

```yaml
gesBlock: expect
schema:
  canonical: "gess1\nschema S\nfield c C default [power: 1e0]\nfield d C optional\nschema C\nfield power Number integer between 0e0 15e0 default 3e0\nfield level Percentage between 0e0 1e0 step 1e-1 default 1e0\nfield on Boolean default true\n"
  hash: "a12ef2623472683c"
```

---

## Test: a document may leave out a record with a default

Document checking accepts a left-out record field that has a record default.

### Case description

```yaml
gesBlock: case
id: document-default-record-omitted
schemaApi:
  sources:
    - "schema :C as {\n  power: :Number integer between 0 and 15 default 3\n  level: :Percentage between 0% and 100% step 10% default 100%\n  on: :Boolean default true\n}\nschema :S as {\n  c: :C default [level: 50%]\n  on: :Boolean\n}\n"
  document: ":Schema(:S)\non: true\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    ok: true
```

---

## Test: a record literal without a required field

A required field without a default that the literal leaves out makes the literal invalid.

### Case description

```yaml
gesBlock: case
id: error-default-record-missing
schemaApi:
  sources:
    - "schema :C as {\n  a: :Number\n  b: :Number default 1\n}\nschema :S as { c: :C default [b: 2] }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidDefault"
    line: 5
    column: 22
```

---

## Test: a nested record literal without a required field

A nested literal is checked as its own literal, at its opening bracket.

### Case description

```yaml
gesBlock: case
id: error-default-record-nested-missing
schemaApi:
  sources:
    - "schema :C as { a: :Number }\nschema :O as { c: :C }\nschema :S as { o: :O default [c: [:]] }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidDefault"
    line: 3
    column: 34
```

---

## Test: a record literal names an unknown field

Each name of a record literal is a stored field of the record's schema; a computed field is none.

### Case description

```yaml
gesBlock: case
id: error-default-record-unknown
schemaApi:
  sources:
    - "schema :C as {\n  a: :Number default 1\n  b: :Number computed by a + 1\n}\nschema :S as { c: :C default [b: 2] }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidDefault"
    line: 5
    column: 31
```

---

## Test: a record literal value outside the range

A value of a record literal is checked as the field's own default would be.

### Case description

```yaml
gesBlock: case
id: error-default-record-range
schemaApi:
  sources:
    - "schema :C as { level: :Percentage between 0% and 100% default 100% }\nschema :S as { c: :C default [level: 120%] }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidDefault"
    line: 2
    column: 31
```

---

## Test: a record literal value off the step

A value of a quantized field lies on its step.

### Case description

```yaml
gesBlock: case
id: error-default-record-step
schemaApi:
  sources:
    - "schema :C as { a: :Quantity(m) between 0m and 10m step 1m default 1m }\nschema :S as { c: :C default [a: 2.5m] }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidDefault"
    line: 2
    column: 31
```

---

## Test: a record literal tag outside the set

A tag of a record literal belongs to the field's tag set.

### Case description

```yaml
gesBlock: case
id: error-default-record-tag
schemaApi:
  sources:
    - "schema :C as { mode: :Tag(#a, #b) default #a }\nschema :S as { c: :C default [mode: #c] }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidDefault"
    line: 2
    column: 31
```

---

## Test: a record literal value with another unit

A value of a record literal has its field's unit, as a default does.

### Case description

```yaml
gesBlock: case
id: error-default-record-unit
schemaApi:
  sources:
    - "schema :C as { a: :Quantity(m) default 1m }\nschema :S as { c: :C default [a: 1W] }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.unitMismatch"
    line: 2
    column: 34
```

---

## Test: a record literal value outside a range from a parameter

A bound that a parameter decides checks the value at binding.

### Case description

```yaml
gesBlock: case
id: error-default-record-parameter
schemaApi:
  sources:
    - "parameter limit: :Quantity(m)\nschema :C as { a: :Quantity(m) between 0m and limit default 1m }\nschema :S as { c: :C default [a: 5m] }\n"
  root: "S"
  parameters:
    limit: "3m"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidDefault"
    line: 3
    column: 31
```

---

## Test: a record literal names a field twice

Each field appears at most once in a record literal.

### Case description

```yaml
gesBlock: case
id: error-default-record-twice
schemaApi:
  sources:
    - "schema :C as { a: :Number default 1 }\nschema :S as { c: :C default [a: 2, a: 3] }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidDefault"
    line: 2
    column: 37
```

---

## Test: a record default of an optional field

An optional field takes no record default, since it may be absent.

### Case description

```yaml
gesBlock: case
id: error-default-record-optional
schemaApi:
  sources:
    - "schema :C as { a: :Number default 1 }\nschema :S as { c: :C optional default [:] }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidDefault"
    line: 2
    column: 31
```

---

## Test: a record default of a union

A union takes its default only from a sibling field, also when a schema is one of its alternatives.

### Case description

```yaml
gesBlock: case
id: error-default-record-union
schemaApi:
  sources:
    - "schema :C as { a: :Number default 1 }\nschema :S as { c: :C | :Nothing default [:] }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidDefault"
    line: 2
    column: 33
```

---

## Test: a record literal of a scalar field

Only a field whose type is a schema takes a record literal.

### Case description

```yaml
gesBlock: case
id: error-default-record-scalar
schemaApi:
  sources:
    - "schema :S as { a: :Number default [:] }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidDefault"
    line: 1
    column: 27
```

---

## Test: a number default of a record field

A record field takes a record literal or a sibling field, never a number.

### Case description

```yaml
gesBlock: case
id: error-default-record-number
schemaApi:
  sources:
    - "schema :C as { a: :Number default 1 }\nschema :S as { c: :C default 1 }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidDefault"
    line: 2
    column: 22
```

---

## Test: a record literal names a List

A record literal holds numbers, Booleans, tags and nested records; a List, Text or union field stays out.

### Case description

```yaml
gesBlock: case
id: error-default-record-list-entry
schemaApi:
  sources:
    - "schema :C as {\n  a: :List(:Number) max 4\n  b: :Number default 1\n}\nschema :S as { c: :C default [a: 1] }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidDefault"
    line: 5
    column: 31
```

---

## Test: a record literal names a union

A union field of a record keeps out of its literal.

### Case description

```yaml
gesBlock: case
id: error-default-record-union-entry
schemaApi:
  sources:
    - "schema :C as { a: :Number | :Nothing }\nschema :S as { c: :C default [a: 1] }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidDefault"
    line: 2
    column: 31
```

---

## Test: a record literal names a let

A record literal holds literals and constants; it names no let, parameter or field.

### Case description

```yaml
gesBlock: case
id: error-default-record-let
schemaApi:
  sources:
    - "schema :C as { a: :Number default 1 }\nschema :S as {\n  let one be 1\n  c: :C default [a: one]\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidDefault"
    line: 4
    column: 18
```

---

## Test: an empty record literal without a colon

An empty record literal is written `[:]`.

### Case description

```yaml
gesBlock: case
id: error-default-record-empty-brackets
schemaApi:
  sources:
    - "schema :C as { a: :Number default 1 }\nschema :S as { c: :C default [] }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.syntax"
    line: 2
    column: 31
```

---

## Test: taken fields in the canonical form

`with` copies the fields of another schema where it stands, with their defaults; the canonical form lists them as if written there, and an abstract schema leaves no trace.

### Case description

```yaml
gesBlock: case
id: canonical-with
schemaApi:
  sources:
    - "abstract schema :Base as {\n  on: :Boolean\n  level: :Number integer between 0 and 7 default 3\n}\nschema :S as {\n  with :Base\n  mode: :Tag(#a, #b)\n  top: :Number integer between 0 and 7 default level\n}\n"
  root: "S"
```

### Expectation

```yaml
gesBlock: expect
schema:
  canonical: "gess1\nschema S\nfield on Boolean\nfield level Number integer between 0e0 7e0 default 3e0\nfield mode Tag(#a #b)\nfield top Number integer between 0e0 7e0 default field level\n"
  hash: "b35af4332f489feb"
```

---

## Test: written fields hash like taken ones

A schema that writes the fields a `with` would take has the same canonical form and hash.

### Case description

```yaml
gesBlock: case
id: canonical-with-written
schemaApi:
  sources:
    - "schema :S as {\n  on: :Boolean\n  level: :Number integer between 0 and 7 default 3\n  mode: :Tag(#a, #b)\n  top: :Number integer between 0 and 7 default level\n}\n"
  root: "S"
```

### Expectation

```yaml
gesBlock: expect
schema:
  canonical: "gess1\nschema S\nfield on Boolean\nfield level Number integer between 0e0 7e0 default 3e0\nfield mode Tag(#a #b)\nfield top Number integer between 0e0 7e0 default field level\n"
  hash: "b35af4332f489feb"
```

---

## Test: taking fields all optional and without some

`all optional` makes every taken field optional and drops its default, except a union with a Nothing alternative, which already holds an absent value; `without` leaves fields out, also a key.

### Case description

```yaml
gesBlock: case
id: canonical-with-all-optional
schemaApi:
  sources:
    - "schema :Area as {\n  id: :Number integer between 0 and 99 key\n  width: :Quantity(m) between 1m and 9m step 1m default 4m\n  name: :Text max 8\n  note: :Text max 4 | :Nothing\n}\nschema :Defaults as { with :Area all optional without (id) }\n"
  root: "Defaults"
```

### Expectation

```yaml
gesBlock: expect
schema:
  canonical: "gess1\nschema Defaults\nfield width Quantity(meter) between 1e0 9e0 step 1e0 optional\nfield name Text max 8 optional\nfield note Text max 4 | Nothing\n"
  hash: "b3ad0d11820fbc1c"
```

---

## Test: the description of taken fields

A schema lists what it takes with `with` and its fields in their final order; an abstract schema says so.

### Case description

```yaml
gesBlock: case
id: describe-with
schemaApi:
  sources:
    - "abstract schema :Base as { on: :Boolean }\nschema :Area as { name: :Text, size: :Boolean }\nschema :S as {\n  with :Base\n  with :Area all optional without (size)\n  mode: :Boolean\n}\n"
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
    takes:
      - schema: "Base"
      - schema: "Area"
        allOptional: true
        without: ["size"]
    fields:
      - name: "on"
        alternatives:
          - kind: "Boolean"
      - name: "name"
        alternatives:
          - kind: "Text"
        optional: true
      - name: "mode"
        alternatives:
          - kind: "Boolean"
```

---

## Test: the description of an abstract schema

An abstract schema describes its fields, also those it takes, but is never bound.

### Case description

```yaml
gesBlock: case
id: describe-abstract
schemaApi:
  sources:
    - "abstract schema :Base as { on: :Boolean }\nabstract schema :Module as { with :Base, off: :Boolean }\n"
  root: "Module"
  bind: false
  describe: true
```

### Expectation

```yaml
gesBlock: expect
schema:
  description:
    name: "Module"
    kind: "Schema"
    abstract: true
    takes:
      - schema: "Base"
    fields:
      - name: "on"
        alternatives:
          - kind: "Boolean"
      - name: "off"
        alternatives:
          - kind: "Boolean"
```

---

## Test: a value with taken fields

Composition is a schema-level thing only: the record stays flat on the wire and in values.

### Case description

```yaml
gesBlock: case
id: encode-with
schemaApi:
  sources:
    - "abstract schema :Base as { on: :Boolean, level: :Number integer between 0 and 7 }\nschema :S as { with :Base, mode: :Tag(#a, #b) }\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "on"
        value:
          type: ":Boolean"
          value: true
      - key: "level"
        value:
          type: ":Number.int64"
          value: "5"
      - key: "mode"
        value:
          type: ":Tag"
          value: "b"
  decode: "1b"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "1b"
  decoded:
    value:
      type: ":S"
      entries:
        - key: "level"
          value:
            type: ":Number.int64"
            value: "5"
        - key: "mode"
          value:
            type: ":Tag"
            value: "b"
        - key: "on"
          value:
            type: ":Boolean"
            value: true
    byteCount: 1
```

---

## Test: a document with taken fields

Document checking sees the taken fields like written ones.

### Case description

```yaml
gesBlock: case
id: document-with
schemaApi:
  sources:
    - "abstract schema :Base as { on: :Boolean }\nschema :S as { with :Base, size: :Number integer between 0 and 9 }\n"
  document: ":Schema(:S)\non: true\nsize: 4\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    ok: true
```

---

## Test: a document of an abstract schema

An abstract schema describes no document.

### Case description

```yaml
gesBlock: case
id: document-abstract
schemaApi:
  sources:
    - "abstract schema :Base as { on: :Boolean }\n"
  document: ":Schema(:Base)\non: true\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    error:
      code: "schema.invalidAbstract"
```

---

## Test: binding an abstract schema

An abstract schema is never encoded, so it cannot be bound.

### Case description

```yaml
gesBlock: case
id: error-abstract-bind
schemaApi:
  sources:
    - "abstract schema :Base as { on: :Boolean }\n"
  root: "Base"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidAbstract"
    line: 1
    column: 1
```

---

## Test: an abstract schema as a field type

Only `with` takes an abstract schema; it is no field type.

### Case description

```yaml
gesBlock: case
id: error-abstract-field
schemaApi:
  sources:
    - "abstract schema :Base as { on: :Boolean }\nschema :S as { x: :Base }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidAbstract"
    line: 2
    column: 19
```

---

## Test: an abstract schema as a list element

An abstract schema is no list element either.

### Case description

```yaml
gesBlock: case
id: error-abstract-element
schemaApi:
  sources:
    - "abstract schema :Base as { on: :Boolean }\nschema :S as { x: :List(:Base) max 2 }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidAbstract"
    line: 2
    column: 25
```

---

## Test: an abstract schema with a role

An abstract schema is no root, so it takes no role.

### Case description

```yaml
gesBlock: case
id: error-abstract-role
schemaApi:
  sources:
    - "model abstract schema :Base as { on: :Boolean }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidRole"
    line: 1
    column: 1
```

---

## Test: taking an undeclared schema

`with` names a schema.

### Case description

```yaml
gesBlock: case
id: error-with-unknown
schemaApi:
  sources:
    - "message M(a: :Boolean)\nschema :S as { with :M }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.unknownName"
    line: 2
    column: 16
```

---

## Test: schemas that take each other

`with` must not form a cycle.

### Case description

```yaml
gesBlock: case
id: error-with-cycle
schemaApi:
  sources:
    - "schema :A as { with :B }\nschema :B as { with :A }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.cycle"
    line: 2
    column: 16
```

---

## Test: a taken field named like an own one

Taken fields share the names of the schema that takes them.

### Case description

```yaml
gesBlock: case
id: error-with-duplicate
schemaApi:
  sources:
    - "schema :A as { a: :Boolean }\nschema :S as { a: :Text, with :A }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.duplicateName"
    line: 2
    column: 26
```

---

## Test: leaving out a field the schema does not have

`without` names fields of the taken schema.

### Case description

```yaml
gesBlock: case
id: error-with-without
schemaApi:
  sources:
    - "schema :A as { a: :Boolean }\nschema :S as { with :A without (b) }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.unknownName"
    line: 2
    column: 33
```

---

## Test: a taken key that is not first

A key stays the first field, also where `with` puts it.

### Case description

```yaml
gesBlock: case
id: error-with-key
schemaApi:
  sources:
    - "schema :A as { a: :Number integer key }\nschema :S as { b: :Boolean, with :A }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidKey"
    line: 2
    column: 29
```

---

## Test: a taken default whose field is left out

A taken field keeps its default, so `without` cannot leave out the field it names, unless `all optional` drops the default.

### Case description

```yaml
gesBlock: case
id: error-with-default
schemaApi:
  sources:
    - "schema :A as { a: :Boolean, b: :Boolean default a }\nschema :S as { with :A without (a) }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidDefault"
    line: 2
    column: 16
```

---

## Test: with in a message

Only a schema takes fields with `with`.

### Case description

```yaml
gesBlock: case
id: error-with-message
schemaApi:
  sources:
    - "message M(with :A)\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.syntax"
    line: 1
    column: 17
```

---

## Test: computed fields belong to the hash

A computed field is part of the canonical form, in a prefix form with exact literals; a later
field of the same spelling changes the hash like any other.

### Case description

```yaml
gesBlock: case
id: canonical-computed
schemaApi:
  sources:
    - "schema :Sound as {\n  energy: :Quantity(J) between 0J and 1kJ step 1J\n  duration: :Quantity(s) between 0s and 10s step 1ms\n  power: :Quantity(W) computed by 0W when duration <= 0s, otherwise energy / duration\n  loud: :Boolean computed by power > 50W and not (duration = 0s)\n}\n"
  root: "Sound"
  bind: false
```

### Expectation

```yaml
gesBlock: expect
schema:
  canonical: "gess1\nschema Sound\nfield energy Quantity(joule) between 0e0 1e3 step 1e0\nfield duration Quantity(second) between 0e0 1e1 step 1e-3\ncomputed power Quantity(watt) (choice (quantity watt 0e0) (lessOrEqual (field duration) (quantity second 0e0)) (divide (field energy) (field duration)))\ncomputed loud Boolean (and (greater (field power) (quantity watt 5e1)) (not (equal (field duration) (quantity second 0e0))))\n"
  hash: "df67ebf131d5bfd3"
```

---

## Test: the description of computed fields

Computed fields are described apart from the fields that are encoded.

### Case description

```yaml
gesBlock: case
id: describe-computed
schemaApi:
  sources:
    - "schema :Gauge as {\n  current: :Number between 0 and 1000 step 0.5\n  maximum: :Number integer between 0 and 1000\n  bonus: :Percentage between 0% and 100% step 1%\n  boosted: :Number computed by current + bonus\n  share: :Number computed by current * bonus\n  doubled: :Percentage computed by bonus + bonus\n  spread: :Percentage optional computed by bonus / maximum\n}\n"
  root: "Gauge"
  bind: false
  describe: true
```

### Expectation

```yaml
gesBlock: expect
schema:
  description:
    name: "Gauge"
    kind: "Schema"
    fields:
      - name: "current"
        alternatives:
          - kind: "Number"
            lower: "0e0"
            upper: "1e3"
            step: "5e-1"
      - name: "maximum"
        alternatives:
          - kind: "Number"
            integer: true
            lower: "0e0"
            upper: "1e3"
      - name: "bonus"
        alternatives:
          - kind: "Percentage"
            lower: "0e0"
            upper: "1e0"
            step: "1e-2"
    computed:
      - name: "boosted"
        kind: "Number"
        expression: "(add (field current) (field bonus))"
      - name: "share"
        kind: "Number"
        expression: "(multiply (field current) (field bonus))"
      - name: "doubled"
        kind: "Percentage"
        expression: "(add (field bonus) (field bonus))"
      - name: "spread"
        kind: "Percentage"
        optional: true
        expression: "(divide (field bonus) (field maximum))"
```

---

## Test: a computed quantity and its condition

Decoding derives each computed field from the decoded fields and the computed fields before it,
with the values a script record gives: `300J / 0.25s` is the integer `1200W`.

### Case description

```yaml
gesBlock: case
id: decode-computed-quantity
schemaApi:
  sources:
    - "schema :Sound as {\n  energy: :Quantity(J) between 0J and 1kJ step 1J\n  duration: :Quantity(s) between 0s and 10s step 1ms\n  power: :Quantity(W) computed by 0W when duration <= 0s, otherwise energy / duration\n  loud: :Boolean computed by power > 50W and not (duration = 0s)\n}\n"
  root: "Sound"
  encode:
    type: ":Sound"
    entries:
      - key: "duration"
        value:
          type: ":Quantity.binary64"
          value: "0.25"
          unit: ":second"
      - key: "energy"
        value:
          type: ":Quantity.int64"
          value: "300"
          unit: ":joule"
  decode: "2ce903"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "2ce903"
  decoded:
    value:
      type: ":Sound"
      entries:
        - key: "duration"
          value:
            type: ":Quantity.binary64"
            value: "0.25"
            unit: ":second"
        - key: "energy"
          value:
            type: ":Quantity.int64"
            value: "300"
            unit: ":joule"
        - key: "loud"
          value:
            type: ":Boolean"
            value: true
        - key: "power"
          value:
            type: ":Quantity.int64"
            value: "1200"
            unit: ":watt"
    byteCount: 3
```

---

## Test: a choice guards a division

The first condition that holds selects its value, so a zero duration gives `0W` instead of a division.

### Case description

```yaml
gesBlock: case
id: decode-computed-choice
schemaApi:
  sources:
    - "schema :Sound as {\n  energy: :Quantity(J) between 0J and 1kJ step 1J\n  duration: :Quantity(s) between 0s and 10s step 1ms\n  power: :Quantity(W) computed by 0W when duration <= 0s, otherwise energy / duration\n  loud: :Boolean computed by power > 50W and not (duration = 0s)\n}\n"
  root: "Sound"
  encode:
    type: ":Sound"
    entries:
      - key: "duration"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":second"
      - key: "energy"
        value:
          type: ":Quantity.int64"
          value: "300"
          unit: ":joule"
  decode: "2c0100"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "2c0100"
  decoded:
    value:
      type: ":Sound"
      entries:
        - key: "duration"
          value:
            type: ":Quantity.int64"
            value: "0"
            unit: ":second"
        - key: "energy"
          value:
            type: ":Quantity.int64"
            value: "300"
            unit: ":joule"
        - key: "loud"
          value:
            type: ":Boolean"
            value: false
        - key: "power"
          value:
            type: ":Quantity.int64"
            value: "0"
            unit: ":watt"
    byteCount: 3
```

---

## Test: a computed binary64

A quotient that is not whole stays binary64.

### Case description

```yaml
gesBlock: case
id: decode-computed-binary64
schemaApi:
  sources:
    - "schema :Sound as {\n  energy: :Quantity(J) between 0J and 1kJ step 1J\n  duration: :Quantity(s) between 0s and 10s step 1ms\n  power: :Quantity(W) computed by 0W when duration <= 0s, otherwise energy / duration\n  loud: :Boolean computed by power > 50W and not (duration = 0s)\n}\n"
  root: "Sound"
  encode:
    type: ":Sound"
    entries:
      - key: "duration"
        value:
          type: ":Quantity.int64"
          value: "3"
          unit: ":second"
      - key: "energy"
        value:
          type: ":Quantity.int64"
          value: "7"
          unit: ":joule"
  decode: "07e02e"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "07e02e"
  decoded:
    value:
      type: ":Sound"
      entries:
        - key: "duration"
          value:
            type: ":Quantity.int64"
            value: "3"
            unit: ":second"
        - key: "energy"
          value:
            type: ":Quantity.int64"
            value: "7"
            unit: ":joule"
        - key: "loud"
          value:
            type: ":Boolean"
            value: false
        - key: "power"
          value:
            type: ":Quantity.binary64"
            value: "2.3333333333333335"
            unit: ":watt"
    byteCount: 3
```

---

## Test: percentages in computed fields

A percentage right of a number scales it by its own magnitude, `10.5 + 25%` is `13.125`; beside
another percentage it adds. A percentage divided by zero is infinite, which a Percentage field
turns into nothing.

### Case description

```yaml
gesBlock: case
id: decode-computed-percentages
schemaApi:
  sources:
    - "schema :Gauge as {\n  current: :Number between 0 and 1000 step 0.5\n  maximum: :Number integer between 0 and 1000\n  bonus: :Percentage between 0% and 100% step 1%\n  boosted: :Number computed by current + bonus\n  share: :Number computed by current * bonus\n  doubled: :Percentage computed by bonus + bonus\n  spread: :Percentage optional computed by bonus / maximum\n}\n"
  root: "Gauge"
  encode:
    type: ":Gauge"
    entries:
      - key: "bonus"
        value:
          type: ":Percentage"
          value: "0.25"
      - key: "current"
        value:
          type: ":Number.binary64"
          value: "10.5"
      - key: "maximum"
        value:
          type: ":Number.int64"
          value: "0"
  decode: "15002003"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "15002003"
  decoded:
    value:
      type: ":Gauge"
      entries:
        - key: "boosted"
          value:
            type: ":Number.binary64"
            value: "13.125"
        - key: "bonus"
          value:
            type: ":Percentage"
            value: "0.25"
        - key: "current"
          value:
            type: ":Number.binary64"
            value: "10.5"
        - key: "doubled"
          value:
            type: ":Percentage"
            value: "0.5"
        - key: "maximum"
          value:
            type: ":Number.int64"
            value: "0"
        - key: "share"
          value:
            type: ":Number.binary64"
            value: "2.625"
        - key: "spread"
          value:
            type: ":Nothing"
    byteCount: 4
```

---

## Test: a percentage divided by a number

A percentage divided by a unitless number stays a percentage.

### Case description

```yaml
gesBlock: case
id: decode-computed-percentage-quotient
schemaApi:
  sources:
    - "schema :Gauge as {\n  current: :Number between 0 and 1000 step 0.5\n  maximum: :Number integer between 0 and 1000\n  bonus: :Percentage between 0% and 100% step 1%\n  boosted: :Number computed by current + bonus\n  share: :Number computed by current * bonus\n  doubled: :Percentage computed by bonus + bonus\n  spread: :Percentage optional computed by bonus / maximum\n}\n"
  root: "Gauge"
  encode:
    type: ":Gauge"
    entries:
      - key: "bonus"
        value:
          type: ":Percentage"
          value: "0.25"
      - key: "current"
        value:
          type: ":Number.binary64"
          value: "10.5"
      - key: "maximum"
        value:
          type: ":Number.int64"
          value: "4"
  decode: "15202003"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "15202003"
  decoded:
    value:
      type: ":Gauge"
      entries:
        - key: "boosted"
          value:
            type: ":Number.binary64"
            value: "13.125"
        - key: "bonus"
          value:
            type: ":Percentage"
            value: "0.25"
        - key: "current"
          value:
            type: ":Number.binary64"
            value: "10.5"
        - key: "doubled"
          value:
            type: ":Percentage"
            value: "0.5"
        - key: "maximum"
          value:
            type: ":Number.int64"
            value: "4"
        - key: "share"
          value:
            type: ":Number.binary64"
            value: "2.625"
        - key: "spread"
          value:
            type: ":Percentage"
            value: "0.0625"
    byteCount: 4
```

---

## Test: integers stay exact

Integer arithmetic is exact within Int64 and falls back to binary64 on overflow, so `a + 1 - a` is `1`
where binary64 would give 0; `min of`, `max of`, `clamp`, `abs` and negation keep integers.

### Case description

```yaml
gesBlock: case
id: decode-computed-integers
schemaApi:
  sources:
    - "schema :Counter as {\n  a: :Number integer between -5000000000000000000 and 5000000000000000000\n  b: :Number integer between -1000 and 1000\n  sum: :Number computed by a + 1 - a\n  tripled: :Number computed by a * 3\n  low: :Number computed by min of a and b and 0\n  high: :Number computed by max of b and 0\n  bound: :Number computed by clamp b between 10 and -10\n  gap: :Number computed by abs (b - 7)\n  flipped: :Number computed by -b\n}\n"
  root: "Counter"
  encode:
    type: ":Counter"
    entries:
      - key: "a"
        value:
          type: ":Number.int64"
          value: "4000000000000000000"
      - key: "b"
        value:
          type: ":Number.int64"
          value: "-12"
  decode: "000084e2506ce67cdc03"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "000084e2506ce67cdc03"
  decoded:
    value:
      type: ":Counter"
      entries:
        - key: "a"
          value:
            type: ":Number.int64"
            value: "4000000000000000000"
        - key: "b"
          value:
            type: ":Number.int64"
            value: "-12"
        - key: "bound"
          value:
            type: ":Number.int64"
            value: "-10"
        - key: "flipped"
          value:
            type: ":Number.int64"
            value: "12"
        - key: "gap"
          value:
            type: ":Number.int64"
            value: "19"
        - key: "high"
          value:
            type: ":Number.int64"
            value: "0"
        - key: "low"
          value:
            type: ":Number.int64"
            value: "-12"
        - key: "sum"
          value:
            type: ":Number.int64"
            value: "1"
        - key: "tripled"
          value:
            type: ":Number.binary64"
            value: "1.2e19"
    byteCount: 10
```

---

## Test: lists, records and absent values

A sum projects each element and adds from left to right, a count counts the elements, and an absent
element field makes the sum nothing. `default` replaces an absent value and `has value` tests for one.

### Case description

```yaml
gesBlock: case
id: decode-computed-lists
schemaApi:
  sources:
    - "schema :Source as {\n  power: :Quantity(W) between 0W and 1kW step 1W\n  level: :Number integer between 0 and 9 optional\n}\nschema :Room as {\n  sources: :List(:Source) max 4\n  levels: :List(:Number integer between 0 and 9) max 4\n  main: :Source optional\n  signature: :Quantity(W) computed by sources[:sum s => s.power]\n  total: :Number computed by levels[:sum] + sources[:count]\n  levelSum: :Number optional computed by sources[:sum s => s.level]\n  mainPower: :Quantity(W) computed by main.power default 0W\n  mainPresent: :Boolean computed by main has value\n  fallbackLevel: :Number computed by main.level default 5\n}\n"
  root: "Room"
  encode:
    type: ":Room"
    entries:
      - key: "levels"
        value:
          type: ":List"
          items:
            - type: ":Number.int64"
              value: "1"
            - type: ":Number.int64"
              value: "2"
            - type: ":Number.int64"
              value: "3"
      - key: "sources"
        value:
          type: ":List"
          items:
            - type: ":Source"
              entries:
                - key: "level"
                  value:
                    type: ":Number.int64"
                    value: "2"
                - key: "power"
                  value:
                    type: ":Quantity.int64"
                    value: "100"
                    unit: ":watt"
            - type: ":Source"
              entries:
                - key: "power"
                  value:
                    type: ":Quantity.int64"
                    value: "250"
                    unit: ":watt"
  decode: "22a3e8632103"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "22a3e8632103"
  decoded:
    value:
      type: ":Room"
      entries:
        - key: "fallbackLevel"
          value:
            type: ":Number.int64"
            value: "5"
        - key: "levelSum"
          value:
            type: ":Nothing"
        - key: "levels"
          value:
            type: ":List"
            items:
              - type: ":Number.int64"
                value: "1"
              - type: ":Number.int64"
                value: "2"
              - type: ":Number.int64"
                value: "3"
        - key: "main"
          value:
            type: ":Nothing"
        - key: "mainPower"
          value:
            type: ":Quantity.int64"
            value: "0"
            unit: ":watt"
        - key: "mainPresent"
          value:
            type: ":Boolean"
            value: false
        - key: "signature"
          value:
            type: ":Quantity.int64"
            value: "350"
            unit: ":watt"
        - key: "sources"
          value:
            type: ":List"
            items:
              - type: ":Source"
                entries:
                  - key: "level"
                    value:
                      type: ":Number.int64"
                      value: "2"
                  - key: "power"
                    value:
                      type: ":Quantity.int64"
                      value: "100"
                      unit: ":watt"
              - type: ":Source"
                entries:
                  - key: "level"
                    value:
                      type: ":Nothing"
                  - key: "power"
                    value:
                      type: ":Quantity.int64"
                      value: "250"
                      unit: ":watt"
        - key: "total"
          value:
            type: ":Number.int64"
            value: "8"
    byteCount: 6
```

---

## Test: empty lists and a present record

An empty sum is 0, which the field's type gives its unit; a present record is read through its fields.

### Case description

```yaml
gesBlock: case
id: decode-computed-empty
schemaApi:
  sources:
    - "schema :Source as {\n  power: :Quantity(W) between 0W and 1kW step 1W\n  level: :Number integer between 0 and 9 optional\n}\nschema :Room as {\n  sources: :List(:Source) max 4\n  levels: :List(:Number integer between 0 and 9) max 4\n  main: :Source optional\n  signature: :Quantity(W) computed by sources[:sum s => s.power]\n  total: :Number computed by levels[:sum] + sources[:count]\n  levelSum: :Number optional computed by sources[:sum s => s.level]\n  mainPower: :Quantity(W) computed by main.power default 0W\n  mainPresent: :Boolean computed by main has value\n  fallbackLevel: :Number computed by main.level default 5\n}\n"
  root: "Room"
  encode:
    type: ":Room"
    entries:
      - key: "levels"
        value:
          type: ":List"
          items: []
      - key: "main"
        value:
          type: ":Source"
          entries:
            - key: "level"
              value:
                type: ":Number.int64"
                value: "1"
            - key: "power"
              value:
                type: ":Quantity.int64"
                value: "5"
                unit: ":watt"
      - key: "sources"
        value:
          type: ":List"
          items: []
  decode: "c00206"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "c00206"
  decoded:
    value:
      type: ":Room"
      entries:
        - key: "fallbackLevel"
          value:
            type: ":Number.int64"
            value: "1"
        - key: "levelSum"
          value:
            type: ":Number.int64"
            value: "0"
        - key: "levels"
          value:
            type: ":List"
            items: []
        - key: "main"
          value:
            type: ":Source"
            entries:
              - key: "level"
                value:
                  type: ":Number.int64"
                  value: "1"
              - key: "power"
                value:
                  type: ":Quantity.int64"
                  value: "5"
                  unit: ":watt"
        - key: "mainPower"
          value:
            type: ":Quantity.int64"
            value: "5"
            unit: ":watt"
        - key: "mainPresent"
          value:
            type: ":Boolean"
            value: true
        - key: "signature"
          value:
            type: ":Quantity.int64"
            value: "0"
            unit: ":watt"
        - key: "sources"
          value:
            type: ":List"
            items: []
        - key: "total"
          value:
            type: ":Number.int64"
            value: "0"
    byteCount: 3
```

---

## Test: division by zero and percentages

`0 / 0` is nothing and `1 / 0` infinite; `x - p` takes the percentage of x, and a percentage times a
percentage stays one.

### Case description

```yaml
gesBlock: case
id: decode-computed-edges
schemaApi:
  sources:
    - "schema :Edge as {\n  x: :Number\n  y: :Number\n  p: :Percentage\n  q: :Number optional computed by x / y\n  huge: :Number computed by 1 / y\n  s: :Number computed by x - p\n  t: :Percentage computed by -p * p\n  u: :Number computed by p * x\n  v: :Number computed by clamp x between -1.5 and y + 0.5\n  w: :Number computed by max of x and 0.5 and -2\n  ne: :Boolean computed by x <> 0 and p = 25%\n}\n"
  root: "Edge"
  encode:
    type: ":Edge"
    entries:
      - key: "p"
        value:
          type: ":Percentage"
          value: "0.25"
      - key: "x"
        value:
          type: ":Number.int64"
          value: "0"
      - key: "y"
        value:
          type: ":Number.int64"
          value: "0"
  decode: "00000000000000000000000000000000000000000000d03f"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "00000000000000000000000000000000000000000000d03f"
  decoded:
    value:
      type: ":Edge"
      entries:
        - key: "huge"
          value:
            type: ":Number.binary64"
            value: "Infinity"
        - key: "ne"
          value:
            type: ":Boolean"
            value: false
        - key: "p"
          value:
            type: ":Percentage"
            value: "0.25"
        - key: "q"
          value:
            type: ":Nothing"
        - key: "s"
          value:
            type: ":Number.int64"
            value: "0"
        - key: "t"
          value:
            type: ":Percentage"
            value: "-0.0625"
        - key: "u"
          value:
            type: ":Number.int64"
            value: "0"
        - key: "v"
          value:
            type: ":Number.int64"
            value: "0"
        - key: "w"
          value:
            type: ":Number.binary64"
            value: "0.5"
        - key: "x"
          value:
            type: ":Number.int64"
            value: "0"
        - key: "y"
          value:
            type: ":Number.int64"
            value: "0"
    byteCount: 24
```

---

## Test: clamp, max and a percentage of a binary64

Bounds of `clamp` may come in any order, and `max of` keeps the kind of the winning value.

### Case description

```yaml
gesBlock: case
id: decode-computed-bounds
schemaApi:
  sources:
    - "schema :Edge as {\n  x: :Number\n  y: :Number\n  p: :Percentage\n  q: :Number optional computed by x / y\n  huge: :Number computed by 1 / y\n  s: :Number computed by x - p\n  t: :Percentage computed by -p * p\n  u: :Number computed by p * x\n  v: :Number computed by clamp x between -1.5 and y + 0.5\n  w: :Number computed by max of x and 0.5 and -2\n  ne: :Boolean computed by x <> 0 and p = 25%\n}\n"
  root: "Edge"
  encode:
    type: ":Edge"
    entries:
      - key: "p"
        value:
          type: ":Percentage"
          value: "0.25"
      - key: "x"
        value:
          type: ":Number.binary64"
          value: "3.5"
      - key: "y"
        value:
          type: ":Number.int64"
          value: "-2"
  decode: "0000000000000c4000000000000000c0000000000000d03f"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "0000000000000c4000000000000000c0000000000000d03f"
  decoded:
    value:
      type: ":Edge"
      entries:
        - key: "huge"
          value:
            type: ":Number.binary64"
            value: "-0.5"
        - key: "ne"
          value:
            type: ":Boolean"
            value: true
        - key: "p"
          value:
            type: ":Percentage"
            value: "0.25"
        - key: "q"
          value:
            type: ":Number.binary64"
            value: "-1.75"
        - key: "s"
          value:
            type: ":Number.binary64"
            value: "2.625"
        - key: "t"
          value:
            type: ":Percentage"
            value: "-0.0625"
        - key: "u"
          value:
            type: ":Number.binary64"
            value: "0.875"
        - key: "v"
          value:
            type: ":Number.binary64"
            value: "-1.5"
        - key: "w"
          value:
            type: ":Number.binary64"
            value: "3.5"
        - key: "x"
          value:
            type: ":Number.binary64"
            value: "3.5"
        - key: "y"
          value:
            type: ":Number.int64"
            value: "-2"
    byteCount: 24
```

---
## Test: Booleans with absent values

`=` compares binary64 within two ULPs; `and` and `or` are three-valued, and a computed Boolean that
gives nothing is false, as a script record reads it.

### Case description

```yaml
gesBlock: case
id: decode-computed-logic
schemaApi:
  sources:
    - "schema :Logic as {\n  x: :Number\n  y: :Number\n  flag: :Boolean optional\n  close: :Boolean computed by x + y = 0.3\n  apart: :Boolean computed by x <> y\n  both: :Boolean computed by flag and x > 0 default false\n  either: :Boolean computed by flag or x > 0 default false\n}\n"
  root: "Logic"
  encode:
    type: ":Logic"
    entries:
      - key: "x"
        value:
          type: ":Number.binary64"
          value: "0.1"
      - key: "y"
        value:
          type: ":Number.binary64"
          value: "0.2"
  decode: "9a9999999999b93f9a9999999999c93f00"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "9a9999999999b93f9a9999999999c93f00"
  decoded:
    value:
      type: ":Logic"
      entries:
        - key: "apart"
          value:
            type: ":Boolean"
            value: true
        - key: "both"
          value:
            type: ":Boolean"
            value: false
        - key: "close"
          value:
            type: ":Boolean"
            value: true
        - key: "either"
          value:
            type: ":Boolean"
            value: true
        - key: "flag"
          value:
            type: ":Nothing"
        - key: "x"
          value:
            type: ":Number.binary64"
            value: "0.1"
        - key: "y"
          value:
            type: ":Number.binary64"
            value: "0.2"
    byteCount: 17
```

---

## Test: square roots are correctly rounded

`sqrt`, `√` and the power `^ 0.5` that they lower to give the correctly rounded square root on every platform: a whole
root is an integer, a negative number has none, and `-infinity` gives `infinity` as pow does. `m²` has the root `m`.

### Case description

```yaml
gesBlock: case
id: decode-computed-roots
schemaApi:
  sources:
    - "schema :Roots as {\n  zero: :Number\n  tiny: :Number\n  one: :Number\n  two: :Number\n  sixteen: :Number\n  large: :Number\n  infinite: :Number\n  negative: :Number\n  negativeInfinite: :Number\n  area: :Quantity(m²)\n  rootZero: :Number computed by sqrt zero\n  rootTiny: :Number computed by sqrt tiny\n  rootOne: :Number computed by √one\n  rootTwo: :Number computed by sqrt two\n  rootSixteen: :Number computed by sqrt(sixteen)\n  rootLarge: :Number computed by sqrt large\n  rootInfinite: :Number computed by sqrt infinite\n  rootNegative: :Number computed by sqrt negative\n  rootNegativeInfinite: :Number computed by sqrt negativeInfinite\n  rootNothing: :Number optional computed by sqrt (zero / zero)\n  power: :Number computed by two ^ 0.5\n  side: :Quantity(m) computed by sqrt area\n  scaled: :Number computed by sqrt two * 2 + 1\n}\n"
  root: "Roots"
  encode:
    type: ":Roots"
    entries:
      - key: "area"
        value:
          type: ":Quantity.int64"
          value: "16"
          unit: ":squareMeter"
      - key: "infinite"
        value:
          type: ":Number.binary64"
          value: "Infinity"
      - key: "large"
        value:
          type: ":Number.binary64"
          value: "1.7976931348623157e308"
      - key: "negative"
        value:
          type: ":Number.int64"
          value: "-4"
      - key: "negativeInfinite"
        value:
          type: ":Number.binary64"
          value: "-Infinity"
      - key: "one"
        value:
          type: ":Number.int64"
          value: "1"
      - key: "sixteen"
        value:
          type: ":Number.int64"
          value: "16"
      - key: "tiny"
        value:
          type: ":Number.binary64"
          value: "5e-324"
      - key: "two"
        value:
          type: ":Number.int64"
          value: "2"
      - key: "zero"
        value:
          type: ":Number.int64"
          value: "0"
  decode: "00000000000000000100000000000000000000000000f03f00000000000000400000000000003040ffffffffffffef7f000000000000f07f00000000000010c0000000000000f0ff0000000000003040"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "00000000000000000100000000000000000000000000f03f00000000000000400000000000003040ffffffffffffef7f000000000000f07f00000000000010c0000000000000f0ff0000000000003040"
  decoded:
    value:
      type: ":Roots"
      entries:
        - key: "area"
          value:
            type: ":Quantity.int64"
            value: "16"
            unit: ":squareMeter"
        - key: "infinite"
          value:
            type: ":Number.binary64"
            value: "Infinity"
        - key: "large"
          value:
            type: ":Number.binary64"
            value: "1.7976931348623157e308"
        - key: "negative"
          value:
            type: ":Number.int64"
            value: "-4"
        - key: "negativeInfinite"
          value:
            type: ":Number.binary64"
            value: "-Infinity"
        - key: "one"
          value:
            type: ":Number.int64"
            value: "1"
        - key: "power"
          value:
            type: ":Number.binary64"
            value: "1.4142135623730951"
        - key: "rootInfinite"
          value:
            type: ":Number.binary64"
            value: "Infinity"
        - key: "rootLarge"
          value:
            type: ":Number.binary64"
            value: "1.3407807929942596e154"
        - key: "rootNegative"
          value:
            type: ":Nothing"
        - key: "rootNegativeInfinite"
          value:
            type: ":Number.binary64"
            value: "Infinity"
        - key: "rootNothing"
          value:
            type: ":Nothing"
        - key: "rootOne"
          value:
            type: ":Number.int64"
            value: "1"
        - key: "rootSixteen"
          value:
            type: ":Number.int64"
            value: "4"
        - key: "rootTiny"
          value:
            type: ":Number.binary64"
            value: "2.2227587494850775e-162"
        - key: "rootTwo"
          value:
            type: ":Number.binary64"
            value: "1.4142135623730951"
        - key: "rootZero"
          value:
            type: ":Number.int64"
            value: "0"
        - key: "scaled"
          value:
            type: ":Number.binary64"
            value: "3.8284271247461903"
        - key: "side"
          value:
            type: ":Quantity.int64"
            value: "4"
            unit: ":meter"
        - key: "sixteen"
          value:
            type: ":Number.int64"
            value: "16"
        - key: "tiny"
          value:
            type: ":Number.binary64"
            value: "5e-324"
        - key: "two"
          value:
            type: ":Number.int64"
            value: "2"
        - key: "zero"
          value:
            type: ":Number.int64"
            value: "0"
    byteCount: 80
```

---

## Test: rounding and angle conversions in computed fields

Rounding gives an integer with the unit of its operand, saturated at the bounds of Int64, and a percentage rounds its ratio.
`rad` and `deg` divide and multiply in binary64, so their bits are exact too.

### Case description

```yaml
gesBlock: case
id: decode-computed-rounding
schemaApi:
  sources:
    - "schema :Rounds as {\n  x: :Number\n  big: :Number\n  share: :Percentage\n  span: :Quantity(m)\n  turn: :Quantity(°)\n  floorX: :Number computed by floor x\n  ceilX: :Number computed by ceil x\n  truncX: :Number computed by truncate x\n  evenX: :Number computed by round half even x\n  upX: :Number computed by round half up x\n  downX: :Number computed by round half down x\n  saturated: :Number computed by floor big\n  shareFloor: :Number computed by floor share\n  spanUp: :Quantity(m) computed by round half up span\n  radians: :Number computed by rad turn\n  degrees: :Quantity(°) computed by deg 1\n}\n"
  root: "Rounds"
  encode:
    type: ":Rounds"
    entries:
      - key: "big"
        value:
          type: ":Number.binary64"
          value: "1e300"
      - key: "share"
        value:
          type: ":Percentage"
          value: "2.5"
      - key: "span"
        value:
          type: ":Quantity.binary64"
          value: "2.5"
          unit: ":meter"
      - key: "turn"
        value:
          type: ":Quantity.int64"
          value: "90"
          unit: ":degree"
      - key: "x"
        value:
          type: ":Number.binary64"
          value: "-2.5"
  decode: "00000000000004c09c7500883ce4377e000000000000044000000000000004400000000000805640"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "00000000000004c09c7500883ce4377e000000000000044000000000000004400000000000805640"
  decoded:
    value:
      type: ":Rounds"
      entries:
        - key: "big"
          value:
            type: ":Number.binary64"
            value: "1e300"
        - key: "ceilX"
          value:
            type: ":Number.int64"
            value: "-2"
        - key: "degrees"
          value:
            type: ":Quantity.binary64"
            value: "57.29577951308232"
            unit: ":degree"
        - key: "downX"
          value:
            type: ":Number.int64"
            value: "-2"
        - key: "evenX"
          value:
            type: ":Number.int64"
            value: "-2"
        - key: "floorX"
          value:
            type: ":Number.int64"
            value: "-3"
        - key: "radians"
          value:
            type: ":Number.binary64"
            value: "1.5707963267948966"
        - key: "saturated"
          value:
            type: ":Number.int64"
            value: "9223372036854775807"
        - key: "share"
          value:
            type: ":Percentage"
            value: "2.5"
        - key: "shareFloor"
          value:
            type: ":Number.int64"
            value: "2"
        - key: "span"
          value:
            type: ":Quantity.binary64"
            value: "2.5"
            unit: ":meter"
        - key: "spanUp"
          value:
            type: ":Quantity.int64"
            value: "3"
            unit: ":meter"
        - key: "truncX"
          value:
            type: ":Number.int64"
            value: "-2"
        - key: "turn"
          value:
            type: ":Quantity.int64"
            value: "90"
            unit: ":degree"
        - key: "upX"
          value:
            type: ":Number.int64"
            value: "-3"
        - key: "x"
          value:
            type: ":Number.binary64"
            value: "-2.5"
    byteCount: 40
```

---

## Test: trigonometry in computed fields

Trigonometry reads radians as a plain number or degrees, and `atan2` and the inverse functions give radians; `deg`
turns them into degrees. These functions come from the platform library, so the case compares within four ULPs.

### Case description

```yaml
gesBlock: case
id: decode-computed-trigonometry
comparison:
  binary64:
    mode: ulp
    maxUlps: 4
schemaApi:
  sources:
    - "schema :Aim as {\n  dx: :Quantity(m) between -1000m and 1000m step 0.01m\n  dy: :Quantity(m) between -1000m and 1000m step 0.01m\n  turn: :Quantity(°) between -360° and 360° step 0.5°\n  t: :Number between -10 and 10 step 0.25\n  bearing: :Number computed by atan2(dy, dx)\n  heading: :Quantity(°) computed by deg atan2(dy, dx)\n  reach: :Quantity(m) computed by hypot(dx, dy)\n  rise: :Quantity(m) computed by hypot(dx, dy, 1m)\n  still: :Number computed by atan2(dx - dx, 0m)\n  sine: :Number computed by sin turn\n  cosine: :Number computed by cos turn\n  tangent: :Number computed by tan t\n  arc: :Number computed by asin (t / 10)\n  wide: :Number computed by acos t\n  arctan: :Number computed by atan t\n  inCone: :Boolean computed by heading < 30°\n}\n"
  root: "Aim"
  encode:
    type: ":Aim"
    entries:
      - key: "dx"
        value:
          type: ":Quantity.int64"
          value: "3"
          unit: ":meter"
      - key: "dy"
        value:
          type: ":Quantity.int64"
          value: "4"
          unit: ":meter"
      - key: "t"
        value:
          type: ":Number.binary64"
          value: "2.5"
      - key: "turn"
        value:
          type: ":Quantity.int64"
          value: "30"
          unit: ":degree"
  decode: "cc87c120c63019"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "cc87c120c63019"
  decoded:
    value:
      type: ":Aim"
      entries:
        - key: "arc"
          value:
            type: ":Number.binary64"
            value: "0.25268025514207865"
        - key: "arctan"
          value:
            type: ":Number.binary64"
            value: "1.1902899496825317"
        - key: "bearing"
          value:
            type: ":Number.binary64"
            value: "0.9272952180016122"
        - key: "cosine"
          value:
            type: ":Number.binary64"
            value: "0.8660254037844387"
        - key: "dx"
          value:
            type: ":Quantity.int64"
            value: "3"
            unit: ":meter"
        - key: "dy"
          value:
            type: ":Quantity.int64"
            value: "4"
            unit: ":meter"
        - key: "heading"
          value:
            type: ":Quantity.binary64"
            value: "53.13010235415597"
            unit: ":degree"
        - key: "inCone"
          value:
            type: ":Boolean"
            value: false
        - key: "reach"
          value:
            type: ":Quantity.int64"
            value: "5"
            unit: ":meter"
        - key: "rise"
          value:
            type: ":Quantity.binary64"
            value: "5.0990195135927845"
            unit: ":meter"
        - key: "sine"
          value:
            type: ":Number.binary64"
            value: "0.49999999999999994"
        - key: "still"
          value:
            type: ":Number.int64"
            value: "0"
        - key: "t"
          value:
            type: ":Number.binary64"
            value: "2.5"
        - key: "tangent"
          value:
            type: ":Number.binary64"
            value: "-0.7470222972386602"
        - key: "turn"
          value:
            type: ":Quantity.int64"
            value: "30"
            unit: ":degree"
        - key: "wide"
          value:
            type: ":Nothing"
    byteCount: 7
```

---

## Test: powers, exponentials and logarithms in computed fields

`^`, `²` and `³` use the platform's pow and take the unit of the power from the derivation table; `cbrt` is the power 1/3,
which a negative number does not have. `exp` and `ln` read plain numbers: `ln 0` is `-infinity` and the logarithm of a
negative number is nothing, as is NaN, without making the field absent.

### Case description

```yaml
gesBlock: case
id: decode-computed-powers
comparison:
  binary64:
    mode: ulp
    maxUlps: 4
schemaApi:
  sources:
    - "schema :Growth as {\n  t: :Number between -10 and 10 step 0.25\n  n: :Number between -10 and 10 step 0.25\n  side: :Quantity(m) between 0m and 100m step 0.5m\n  cubed: :Number computed by t ^ 3\n  doubling: :Number computed by 2 ^ t\n  square: :Quantity(m²) computed by side²\n  volume: :Quantity(m³) computed by side³\n  back: :Quantity(m) computed by cbrt volume\n  root: :Number computed by cbrt t\n  noRoot: :Number computed by cbrt n\n  growth: :Number computed by exp t\n  huge: :Number computed by exp (t * 100)\n  log: :Number computed by ln t\n  logZero: :Number computed by ln (t - t)\n  logNegative: :Number computed by ln n\n  inverse: :Number computed by t ^ -1\n}\n"
  root: "Growth"
  encode:
    type: ":Growth"
    entries:
      - key: "n"
        value:
          type: ":Number.int64"
          value: "-2"
      - key: "side"
        value:
          type: ":Quantity.binary64"
          value: "1.5"
          unit: ":meter"
      - key: "t"
        value:
          type: ":Number.binary64"
          value: "2.5"
  decode: "32d000"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "32d000"
  decoded:
    value:
      type: ":Growth"
      entries:
        - key: "back"
          value:
            type: ":Quantity.binary64"
            value: "1.5"
            unit: ":meter"
        - key: "cubed"
          value:
            type: ":Number.binary64"
            value: "15.625"
        - key: "doubling"
          value:
            type: ":Number.binary64"
            value: "5.656854249492381"
        - key: "growth"
          value:
            type: ":Number.binary64"
            value: "12.182493960703473"
        - key: "huge"
          value:
            type: ":Number.binary64"
            value: "3.7464546145026734e108"
        - key: "inverse"
          value:
            type: ":Number.binary64"
            value: "0.4"
        - key: "log"
          value:
            type: ":Number.binary64"
            value: "0.9162907318741551"
        - key: "logNegative"
          value:
            type: ":Nothing"
        - key: "logZero"
          value:
            type: ":Number.binary64"
            value: "-Infinity"
        - key: "n"
          value:
            type: ":Number.int64"
            value: "-2"
        - key: "noRoot"
          value:
            type: ":Nothing"
        - key: "root"
          value:
            type: ":Number.binary64"
            value: "1.3572088082974532"
        - key: "side"
          value:
            type: ":Quantity.binary64"
            value: "1.5"
            unit: ":meter"
        - key: "square"
          value:
            type: ":Quantity.binary64"
            value: "2.25"
            unit: ":squareMeter"
        - key: "t"
          value:
            type: ":Number.binary64"
            value: "2.5"
        - key: "volume"
          value:
            type: ":Quantity.binary64"
            value: "3.375"
            unit: ":cubicMeter"
    byteCount: 3
```

---

## Test: names in backticks

A name in backticks names a field or let also where GES reserves its word: `` `length` `` reads the field `length`, and
`` `e` `` reads the field `e` rather than the constant.

### Case description

```yaml
gesBlock: case
id: decode-computed-backticks
schemaApi:
  sources:
    - "schema :Span as {\n  length: :Quantity(m) between 0m and 100m step 0.5m\n  e: :Number between 0 and 10 step 1\n  half: :Quantity(m) computed by `length` / 2\n  scaled: :Number computed by `e` * 2\n  long: :Boolean computed by `length` > 10m and `e` > 1\n}\n"
  root: "Span"
  encode:
    type: ":Span"
    entries:
      - key: "e"
        value:
          type: ":Number.int64"
          value: "3"
      - key: "length"
        value:
          type: ":Quantity.int64"
          value: "25"
          unit: ":meter"
  decode: "3203"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "3203"
  decoded:
    value:
      type: ":Span"
      entries:
        - key: "e"
          value:
            type: ":Number.int64"
            value: "3"
        - key: "half"
          value:
            type: ":Quantity.binary64"
            value: "12.5"
            unit: ":meter"
        - key: "length"
          value:
            type: ":Quantity.int64"
            value: "25"
            unit: ":meter"
        - key: "long"
          value:
            type: ":Boolean"
            value: true
        - key: "scaled"
          value:
            type: ":Number.int64"
            value: "6"
    byteCount: 2
```

---

## Test: functions and names in backticks in the canonical form

A function is a node of the canonical form like an operator, and `²` is the power 2. Backticks are spelling only: the
canonical form names the field `length` as any other field.

### Case description

```yaml
gesBlock: case
id: canonical-computed-functions
schemaApi:
  sources:
    - "schema :Aim as {\n  length: :Quantity(m) between 0m and 100m step 0.5m\n  turn: :Quantity(°) between 0° and 360° step 0.5°\n  area: :Quantity(m²) computed by `length`²\n  side: :Quantity(m) computed by √area\n  lean: :Number computed by sin turn\n  reach: :Quantity(m) computed by hypot(`length`, side)\n  steps: :Number computed by round half even (`length` / 1m)\n}\n"
  root: "Aim"
  bind: false
```

### Expectation

```yaml
gesBlock: expect
schema:
  canonical: "gess1\nschema Aim\nfield length Quantity(meter) between 0e0 1e2 step 5e-1\nfield turn Quantity(degree) between 0e0 36e1 step 5e-1\ncomputed area Quantity(squareMeter) (power (field length) (number 2e0))\ncomputed side Quantity(meter) (sqrt (field area))\ncomputed lean Number (sin (field turn))\ncomputed reach Quantity(meter) (hypot (field length) (field side))\ncomputed steps Number (roundHalfEven (divide (field length) (quantity meter 1e0)))\n"
```

---

## Test: a square root without a unit of its own

A unit has a square root only where the derivation table holds its square, so the root of `m` is rejected.

### Case description

```yaml
gesBlock: case
id: error-computed-root-unit
schemaApi:
  sources:
    - "schema :S as { a: :Quantity(m), b: :Quantity(m) computed by sqrt a }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.unitMismatch"
    line: 1
    column: 61
```

---

## Test: a cube root of a square

The root of `m²` is `m`, but its cube root has no unit.

### Case description

```yaml
gesBlock: case
id: error-computed-cube-root-unit
schemaApi:
  sources:
    - "schema :S as { a: :Quantity(m²), b: :Quantity(m) computed by cbrt a }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.unitMismatch"
    line: 1
    column: 62
```

---

## Test: a power of a quantity needs a literal exponent

The unit of a power follows from its exponent, so a quantity takes a literal one.

### Case description

```yaml
gesBlock: case
id: error-computed-power-exponent
schemaApi:
  sources:
    - "schema :S as { a: :Quantity(m), n: :Number, b: :Quantity(m²) computed by a ^ n }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.unitMismatch"
    line: 1
    column: 78
```

---

## Test: an exponent with a unit

An exponent has no unit.

### Case description

```yaml
gesBlock: case
id: error-computed-power-unit
schemaApi:
  sources:
    - "schema :S as { a: :Number, n: :Quantity(m), b: :Number computed by a ^ n }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.unitMismatch"
    line: 1
    column: 72
```

---

## Test: trigonometry of a length

Trigonometry reads radians as a plain number or degrees.

### Case description

```yaml
gesBlock: case
id: error-computed-sine-unit
schemaApi:
  sources:
    - "schema :S as { a: :Quantity(m), b: :Number computed by sin a }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.unitMismatch"
    line: 1
    column: 56
```

---

## Test: an inverse function of an angle

`asin` reads a plain number.

### Case description

```yaml
gesBlock: case
id: error-computed-arcsine-unit
schemaApi:
  sources:
    - "schema :S as { a: :Quantity(°), b: :Number computed by asin a }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.unitMismatch"
    line: 1
    column: 56
```

---

## Test: atan2 of two units

The arguments of `atan2` and `hypot` have one unit.

### Case description

```yaml
gesBlock: case
id: error-computed-atan2-units
schemaApi:
  sources:
    - "schema :S as { a: :Quantity(m), b: :Quantity(s), c: :Number computed by atan2(a, b) }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.unitMismatch"
    line: 1
    column: 82
```

---

## Test: hypot of one value

`hypot` takes two or three arguments.

### Case description

```yaml
gesBlock: case
id: error-computed-hypot-arity
schemaApi:
  sources:
    - "schema :S as { a: :Number, b: :Number computed by hypot(a) }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidExpression"
    line: 1
    column: 51
```

---

## Test: a root of a Boolean

A function reads a number or a percentage.

### Case description

```yaml
gesBlock: case
id: error-computed-root-boolean
schemaApi:
  sources:
    - "schema :S as { a: :Boolean, b: :Number computed by sqrt a }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidExpression"
    line: 1
    column: 57
```

---

## Test: a power of a parenthesized function

A parenthesized operand directly after a function or `abs` is the whole operand, as in GES, and the function is then
the base of a power: `sqrt(a) ^ 2` is `(sqrt a) ^ 2`, `floor(a)²` the square of the floor, and unary minus still negates
the power. Without parentheses the power belongs to the operand, so `sqrt a ^ 2` is the root of `a ^ 2`.

### Case description

```yaml
gesBlock: case
id: canonical-computed-call-power
schemaApi:
  sources:
    - "schema :S as {\n  a: :Number\n  b: :Number computed by sqrt(a) ^ 2\n  c: :Number computed by floor(a)²\n  d: :Number computed by abs (a) ^ 3\n  e: :Number computed by -√(a) ^ 2\n  f: :Number computed by sqrt a ^ 2\n  g: :Number computed by round half even(a) ^ 2 ^ 2\n}\n"
  root: "S"
  bind: false
```

### Expectation

```yaml
gesBlock: expect
schema:
  canonical: "gess1\nschema S\nfield a Number\ncomputed b Number (power (sqrt (field a)) (number 2e0))\ncomputed c Number (power (floor (field a)) (number 2e0))\ncomputed d Number (power (abs (field a)) (number 3e0))\ncomputed e Number (negate (power (sqrt (field a)) (number 2e0)))\ncomputed f Number (sqrt (power (field a) (number 2e0)))\ncomputed g Number (power (roundHalfEven (field a)) (power (number 2e0) (number 2e0)))\n"
```

---
## Test: a document sets no computed field

A computed field is never read from a document.

### Case description

```yaml
gesBlock: case
id: document-computed
schemaApi:
  sources:
    - "schema :Sound as {\n  energy: :Quantity(J) between 0J and 1kJ step 1J\n  duration: :Quantity(s) between 0s and 10s step 1ms\n  power: :Quantity(W) computed by 0W when duration <= 0s, otherwise energy / duration\n  loud: :Boolean computed by power > 50W and not (duration = 0s)\n}\n"
  document: ":Schema(:Sound)\nenergy: 3J\nduration: 1s\npower: 3W\n"
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

## Test: with takes computed fields

A taken computed field reads the fields of the schema that takes it, and `without` may leave it out.

### Case description

```yaml
gesBlock: case
id: describe-with-computed
schemaApi:
  sources:
    - "abstract schema :Base as {\n  a: :Number\n  b: :Number\n  sum: :Number computed by a + b\n}\nschema :S as { with :Base, twice: :Number computed by sum * 2 }\n"
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
    takes:
      - schema: "Base"
    fields:
      - name: "a"
        alternatives:
          - kind: "Number"
      - name: "b"
        alternatives:
          - kind: "Number"
    computed:
      - name: "sum"
        kind: "Number"
        expression: "(add (field a) (field b))"
      - name: "twice"
        kind: "Number"
        expression: "(multiply (field sum) (number 2e0))"
```

---

## Test: an unknown name

The source is rejected.

### Case description

```yaml
gesBlock: case
id: error-computed-unknown
schemaApi:
  sources:
    - "schema :S as { a: :Number, b: :Number computed by a + c }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.unknownName"
    line: 1
    column: 55
```

---

## Test: units that do not fit

The source is rejected.

### Case description

```yaml
gesBlock: case
id: error-computed-units
schemaApi:
  sources:
    - "schema :S as { a: :Quantity(m), b: :Quantity(s), c: :Quantity(m) computed by a + b }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.unitMismatch"
    line: 1
    column: 78
```

---

## Test: a result of another unit

The source is rejected.

### Case description

```yaml
gesBlock: case
id: error-computed-result-unit
schemaApi:
  sources:
    - "schema :S as { a: :Quantity(m), b: :Quantity(s), c: :Quantity(m) computed by a / b }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.unitMismatch"
    line: 1
    column: 78
```

---

## Test: a result of another type

The source is rejected.

### Case description

```yaml
gesBlock: case
id: error-computed-result-type
schemaApi:
  sources:
    - "schema :S as { a: :Number, b: :Boolean computed by a + 1 }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidExpression"
    line: 1
    column: 52
```

---

## Test: a percentage on the left

The source is rejected.

### Case description

```yaml
gesBlock: case
id: error-computed-percentage
schemaApi:
  sources:
    - "schema :S as { a: :Percentage, b: :Number, c: :Number computed by a + b }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidExpression"
    line: 1
    column: 67
```

---

## Test: a later computed field

The source is rejected.

### Case description

```yaml
gesBlock: case
id: error-computed-order
schemaApi:
  sources:
    - "schema :S as { a: :Number, b: :Number computed by c, c: :Number computed by a }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidExpression"
    line: 1
    column: 51
```

---

## Test: a keyword as a name

The source is rejected.

### Case description

```yaml
gesBlock: case
id: error-computed-keyword
schemaApi:
  sources:
    - "schema :S as { e: :Number, b: :Number computed by e * 2 }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidExpression"
    line: 1
    column: 51
```

---

## Test: a sum of quantities in arithmetic

The source is rejected.

### Case description

```yaml
gesBlock: case
id: error-computed-sum-alone
schemaApi:
  sources:
    - "schema :P as { w: :Quantity(W) }\nschema :S as { list: :List(:P), b: :Quantity(W) computed by list[:sum p => p.w] + 1W }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidExpression"
    line: 2
    column: 65
```

---

## Test: constraints on a computed field

The source is rejected.

### Case description

```yaml
gesBlock: case
id: error-computed-constraint
schemaApi:
  sources:
    - "schema :S as { a: :Number, b: :Number between 0 and 1 computed by a }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidExpression"
    line: 1
    column: 28
```

---

## Test: an optional computed Boolean

The source is rejected.

### Case description

```yaml
gesBlock: case
id: error-computed-optional-boolean
schemaApi:
  sources:
    - "schema :S as { a: :Number, b: :Boolean optional computed by a > 0 }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidExpression"
    line: 1
    column: 28
```

---

## Test: a choice without otherwise

The source is rejected.

### Case description

```yaml
gesBlock: case
id: error-computed-otherwise
schemaApi:
  sources:
    - "schema :S as {\n  a: :Number\n  b: :Number computed by 1 when a > 0\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.syntax"
    line: 3
    column: 38
```

---

## Test: a computed name twice

The source is rejected.

### Case description

```yaml
gesBlock: case
id: error-computed-duplicate
schemaApi:
  sources:
    - "schema :S as { a: :Number, a: :Number computed by 1 }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.duplicateName"
    line: 1
    column: 28
```

---

## Test: a default that names a computed field

The source is rejected.

### Case description

```yaml
gesBlock: case
id: error-computed-default
schemaApi:
  sources:
    - "schema :S as { a: :Number, b: :Number computed by a, c: :Number default b }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidDefault"
    line: 1
    column: 65
```

---

## Test: a computed message argument

The source is rejected.

### Case description

```yaml
gesBlock: case
id: error-computed-message
schemaApi:
  sources:
    - "message M(a: :Number computed by 1)\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.syntax"
    line: 1
    column: 22
```

---

## Test: without leaves out a field that a taken computed field reads

The source is rejected.

### Case description

```yaml
gesBlock: case
id: error-computed-without
schemaApi:
  sources:
    - "abstract schema :Base as { a: :Number, b: :Number computed by a * 2 }\nschema :S as { with :Base without (a) }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.unknownName"
    line: 1
    column: 63
```

---

## Test: an optional list in a sum

The source is rejected.

### Case description

```yaml
gesBlock: case
id: error-computed-optional-list
schemaApi:
  sources:
    - "schema :S as { list: :List(:Number) optional, b: :Number computed by list[:sum] }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidExpression"
    line: 1
    column: 70
```

---
## Test: a recursive List belongs to the hash

`depth` follows `max` in the canonical form; a recursive schema is written once.

### Case description

```yaml
gesBlock: case
id: canonical-recursive
schemaApi:
  sources:
    - "schema :Loadout as { root: :Bind }\nschema :Bind as {\n  name: :Text max 8\n  units: :List(:Bind) max 4 depth 3\n}\n"
  root: "Loadout"
  bind: false
```

### Expectation

```yaml
gesBlock: expect
schema:
  canonical: "gess1\nschema Loadout\nfield root Bind\nschema Bind\nfield name Text max 8\nfield units List(Bind) max 4 depth 3\n"
  hash: "283ea52df2ff492f"
```

---

## Test: the description of a recursive List

A List type describes its depth.

### Case description

```yaml
gesBlock: case
id: describe-recursive
schemaApi:
  sources:
    - "schema :Loadout as { root: :Bind }\nschema :Bind as {\n  name: :Text max 8\n  units: :List(:Bind) max 4 depth 3\n}\n"
  root: "Bind"
  bind: false
  describe: true
```

### Expectation

```yaml
gesBlock: expect
schema:
  description:
    name: "Bind"
    kind: "Schema"
    fields:
      - name: "name"
        alternatives:
          - kind: "Text"
            max: 8
      - name: "units"
        alternatives:
          - kind: "List"
            max: 4
            element:
              - kind: "Reference"
                schema: "Bind"
            depth: 3
```

---

## Test: a tree as deep as its depth

Records of the schema nest in each other up to the depth; the deepest List is empty and takes no bits.

### Case description

```yaml
gesBlock: case
id: encode-recursive
schemaApi:
  sources:
    - "schema :Loadout as { root: :Bind }\nschema :Bind as {\n  name: :Text max 8\n  units: :List(:Bind) max 4 depth 3\n}\n"
  root: "Loadout"
  encode:
    type: ":Loadout"
    entries:
      - key: "root"
        value:
          type: ":Bind"
          entries:
            - key: "name"
              value:
                type: ":Text"
                value: "a"
            - key: "units"
              value:
                type: ":List"
                items:
                  - type: ":Bind"
                    entries:
                      - key: "name"
                        value:
                          type: ":Text"
                          value: "b"
                      - key: "units"
                        value:
                          type: ":List"
                          items:
                            - type: ":Bind"
                              entries:
                                - key: "name"
                                  value:
                                    type: ":Text"
                                    value: "c"
                                - key: "units"
                                  value:
                                    type: ":List"
                                    items: []
                  - type: ":Bind"
                    entries:
                      - key: "name"
                        value:
                          type: ":Text"
                          value: "d"
                      - key: "units"
                        value:
                          type: ":List"
                          items: []
  decode: "11a6104b8c051900"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "11a6104b8c051900"
  decoded:
    value:
      type: ":Loadout"
      entries:
        - key: "root"
          value:
            type: ":Bind"
            entries:
              - key: "name"
                value:
                  type: ":Text"
                  value: "a"
              - key: "units"
                value:
                  type: ":List"
                  items:
                    - type: ":Bind"
                      entries:
                        - key: "name"
                          value:
                            type: ":Text"
                            value: "b"
                        - key: "units"
                          value:
                            type: ":List"
                            items:
                              - type: ":Bind"
                                entries:
                                  - key: "name"
                                    value:
                                      type: ":Text"
                                      value: "c"
                                  - key: "units"
                                    value:
                                      type: ":List"
                                      items: []
                    - type: ":Bind"
                      entries:
                        - key: "name"
                          value:
                            type: ":Text"
                            value: "d"
                        - key: "units"
                          value:
                            type: ":List"
                            items: []
    byteCount: 8
```

---

## Test: a tree deeper than its depth

A record in the deepest List is deeper than the depth: `wire.tooDeep` at that List.

### Case description

```yaml
gesBlock: case
id: error-encode-too-deep
schemaApi:
  sources:
    - "schema :Loadout as { root: :Bind }\nschema :Bind as {\n  name: :Text max 8\n  units: :List(:Bind) max 4 depth 3\n}\n"
  root: "Loadout"
  encode:
    type: ":Loadout"
    entries:
      - key: "root"
        value:
          type: ":Bind"
          entries:
            - key: "name"
              value:
                type: ":Text"
                value: "a"
            - key: "units"
              value:
                type: ":List"
                items:
                  - type: ":Bind"
                    entries:
                      - key: "name"
                        value:
                          type: ":Text"
                          value: "b"
                      - key: "units"
                        value:
                          type: ":List"
                          items:
                            - type: ":Bind"
                              entries:
                                - key: "name"
                                  value:
                                    type: ":Text"
                                    value: "c"
                                - key: "units"
                                  value:
                                    type: ":List"
                                    items:
                                      - type: ":Bind"
                                        entries:
                                          - key: "name"
                                            value:
                                              type: ":Text"
                                              value: "d"
                                          - key: "units"
                                            value:
                                              type: ":List"
                                              items: []
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    error:
      code: "wire.tooDeep"
      path: "root.units[].units[].units"
```

---

## Test: a document tree as deep as its depth

Document checking counts the records of the schema as encoding does.

### Case description

```yaml
gesBlock: case
id: document-recursive
schemaApi:
  sources:
    - "schema :Loadout as { root: :Bind }\nschema :Bind as {\n  name: :Text max 8\n  units: :List(:Bind) max 4 depth 3\n}\n"
  document: ":Schema(:Loadout)\nroot: [name: 'a', units: [[name: 'b', units: [[name: 'c', units: []]]]]]\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    ok: true
```

---

## Test: a document tree deeper than its depth

A record in the deepest List is `document.tooDeep`.

### Case description

```yaml
gesBlock: case
id: document-too-deep
schemaApi:
  sources:
    - "schema :Loadout as { root: :Bind }\nschema :Bind as {\n  name: :Text max 8\n  units: :List(:Bind) max 4 depth 3\n}\n"
  document: ":Schema(:Loadout)\nroot: [name: 'a', units: [[name: 'b', units: [[name: 'c', units: [[name: 'd', units: []]]]]]]]\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    error:
      code: "document.tooDeep"
      line: 2
      column: 66
```

---

## Test: depth on a list of another schema

The source is rejected.

### Case description

```yaml
gesBlock: case
id: error-depth-other
schemaApi:
  sources:
    - "schema :A as { b: :List(:B) max 2 depth 2 }\nschema :B as { on: :Boolean }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidConstraint"
    line: 1
    column: 35
```

---

## Test: depth inside a union

The source is rejected.

### Case description

```yaml
gesBlock: case
id: error-depth-union
schemaApi:
  sources:
    - "schema :A as { b: :List(:A) max 2 depth 2 | :Nothing }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidConstraint"
    line: 1
    column: 35
```

---

## Test: a tree of binds and unit ids

The element of a recursive List may be a union of scalars and the one schema that declares it, so a bind holds unit ids and
binds. Each record of the schema is one level deeper, and at the depth the List holds neither.

### Case description

```yaml
gesBlock: case
id: encode-recursive-union
schemaApi:
  sources:
    - "schema :Loadout as { root: :Bind }\nschema :Bind as {\n  name: :Text max 8\n  units: :List(id: :Text max 8 | bind: :Bind) max 4 depth 3\n}\n"
  root: "Loadout"
  encode:
    type: ":Loadout"
    entries:
      - key: "root"
        value:
          type: ":Bind"
          entries:
            - key: "name"
              value:
                type: ":Text"
                value: "a"
            - key: "units"
              value:
                type: ":List"
                items:
                  - type: ":Text"
                    value: "x"
                  - type: ":Bind"
                    entries:
                      - key: "name"
                        value:
                          type: ":Text"
                          value: "b"
                      - key: "units"
                        value:
                          type: ":List"
                          items:
                            - type: ":Bind"
                              entries:
                                - key: "name"
                                  value:
                                    type: ":Text"
                                    value: "c"
                                - key: "units"
                                  value:
                                    type: ":List"
                                    items: []
                            - type: ":Text"
                              value: "y"
  decode: "11268137c434c6441e"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "11268137c434c6441e"
  decoded:
    value:
      type: ":Loadout"
      entries:
        - key: "root"
          value:
            type: ":Bind"
            entries:
              - key: "name"
                value:
                  type: ":Text"
                  value: "a"
              - key: "units"
                value:
                  type: ":List"
                  items:
                    - type: ":Text"
                      value: "x"
                    - type: ":Bind"
                      entries:
                        - key: "name"
                          value:
                            type: ":Text"
                            value: "b"
                        - key: "units"
                          value:
                            type: ":List"
                            items:
                              - type: ":Bind"
                                entries:
                                  - key: "name"
                                    value:
                                      type: ":Text"
                                      value: "c"
                                  - key: "units"
                                    value:
                                      type: ":List"
                                      items: []
                              - type: ":Text"
                                value: "y"
    byteCount: 9
```

---

## Test: a unit id deeper than the depth

At the depth the List holds no element of any alternative, so a unit id there is as deep as a bind.

### Case description

```yaml
gesBlock: case
id: error-encode-too-deep-union
schemaApi:
  sources:
    - "schema :Loadout as { root: :Bind }\nschema :Bind as {\n  name: :Text max 8\n  units: :List(id: :Text max 8 | bind: :Bind) max 4 depth 3\n}\n"
  root: "Loadout"
  encode:
    type: ":Loadout"
    entries:
      - key: "root"
        value:
          type: ":Bind"
          entries:
            - key: "name"
              value:
                type: ":Text"
                value: "a"
            - key: "units"
              value:
                type: ":List"
                items:
                  - type: ":Text"
                    value: "x"
                  - type: ":Bind"
                    entries:
                      - key: "name"
                        value:
                          type: ":Text"
                          value: "b"
                      - key: "units"
                        value:
                          type: ":List"
                          items:
                            - type: ":Bind"
                              entries:
                                - key: "name"
                                  value:
                                    type: ":Text"
                                    value: "c"
                                - key: "units"
                                  value:
                                    type: ":List"
                                    items:
                                      - type: ":Text"
                                        value: "z"
                            - type: ":Text"
                              value: "y"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    error:
      code: "wire.tooDeep"
      path: "root.units[]:Bind.units[]:Bind.units"
```

---

## Test: a document of binds and unit ids

A document writes a unit as its id alone or as a bind record.

### Case description

```yaml
gesBlock: case
id: document-recursive-union
schemaApi:
  sources:
    - "schema :Loadout as { root: :Bind }\nschema :Bind as {\n  name: :Text max 8\n  units: :List(id: :Text max 8 | bind: :Bind) max 4 depth 3\n}\n"
  document: ":Schema(:Loadout)\nroot: [name: 'a', units: ['x', [name: 'b', units: [[name: 'c', units: []], 'y']]]]\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    ok: true
```

---

## Test: a document unit id deeper than the depth

The deepest bind holds a unit id, so no alternative of the elements around it holds them, and the outermost element
that none holds is `document.fieldType`, as in any union.

### Case description

```yaml
gesBlock: case
id: document-too-deep-union
schemaApi:
  sources:
    - "schema :Loadout as { root: :Bind }\nschema :Bind as {\n  name: :Text max 8\n  units: :List(id: :Text max 8 | bind: :Bind) max 4 depth 3\n}\n"
  document: ":Schema(:Loadout)\nroot: [name: 'a', units: [[name: 'b', units: [[name: 'c', units: ['z']]]]]]\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    error:
      code: "document.fieldType"
      line: 2
      column: 27
```

---

## Test: a recursive union with a second schema

Only one alternative of the union may be a schema, the one that declares the List.

### Case description

```yaml
gesBlock: case
id: error-depth-union-schema
schemaApi:
  sources:
    - "schema :Unit as { id: :Text max 8 }\nschema :Bind as { units: :List(:Unit | :Bind) max 4 depth 3 }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidConstraint"
    line: 2
    column: 53
```

---

## Test: a recursive union with a List

The other alternatives are scalars: a List alternative holds elements, whose size the levels would have to count too.

### Case description

```yaml
gesBlock: case
id: error-depth-union-list
schemaApi:
  sources:
    - "schema :Bind as { units: :List(:List(:Text max 8) max 2 | :Bind) max 4 depth 3 }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidConstraint"
    line: 1
    column: 72
```

---

## Test: a document read as a model reads it

The header names the expected schema, the document passes its check, and its entries are the value a model converts; a field it leaves out keeps no entry, so the model takes its default.

### Case description

```yaml
gesBlock: case
id: read-document
schemaApi:
  sources:
    - "schema :Unit as {\n  name: :Text max 16\n  hp: :Number integer between 0 and 100 default 100\n  parts: :List(:Part) max 4\n}\nschema :Part as { kind: :Tag(#arm, #leg) }\nschema :Base as { id: :Number integer }\n"
  readDocument:
    text: ":Schema(:Unit)\nname: 'scout'\nparts: [[kind: #arm]]\n"
    expected: ["Unit"]
```

### Expectation

```yaml
gesBlock: expect
schema:
  readDocument:
    schema: "Unit"
    value:
      type: ":Unit"
      entries:
        - key: "name"
          value:
            type: ":Text"
            value: "scout"
        - key: "parts"
          value:
            type: ":List"
            items:
              - type: ":Map"
                entries:
                  - key: "kind"
                    value:
                      type: ":Tag"
                      value: "arm"
```

---

## Test: a document of one of several schemas

The header chooses among the expected schemas.

### Case description

```yaml
gesBlock: case
id: read-document-choice
schemaApi:
  sources:
    - "schema :Unit as {\n  name: :Text max 16\n  hp: :Number integer between 0 and 100 default 100\n  parts: :List(:Part) max 4\n}\nschema :Part as { kind: :Tag(#arm, #leg) }\nschema :Base as { id: :Number integer }\n"
  readDocument:
    text: ":Schema(:Base)\nid: 7\n"
    expected: ["Unit", "Base"]
```

### Expectation

```yaml
gesBlock: expect
schema:
  readDocument:
    schema: "Base"
    value:
      type: ":Base"
      entries:
        - key: "id"
          value:
            type: ":Number.int64"
            value: "7"
```

---

## Test: a document of another schema

A header that names another schema is `document.schemaMismatch`.

### Case description

```yaml
gesBlock: case
id: read-document-other-schema
schemaApi:
  sources:
    - "schema :Unit as {\n  name: :Text max 16\n  hp: :Number integer between 0 and 100 default 100\n  parts: :List(:Part) max 4\n}\nschema :Part as { kind: :Tag(#arm, #leg) }\nschema :Base as { id: :Number integer }\n"
  readDocument:
    text: ":Schema(:Base)\nid: 7\n"
    expected: ["Unit"]
```

### Expectation

```yaml
gesBlock: expect
schema:
  readDocument:
    error:
      code: "document.schemaMismatch"
      line: 1
      column: 1
      path: ""
```

---

## Test: a document of an expected abstract schema

An expected name still has to name a concrete schema of the set: an abstract schema describes no document.

### Case description

```yaml
gesBlock: case
id: read-document-abstract-schema
schemaApi:
  sources:
    - "abstract schema :Base as { on: :Boolean }\n"
  readDocument:
    text: ":Schema(:Base)\non: true\n"
    expected: ["Base"]
```

### Expectation

```yaml
gesBlock: expect
schema:
  readDocument:
    error:
      code: "document.schemaMismatch"
      line: 1
      column: 1
      path: ""
```

---

## Test: a document of an expected message

A message describes no document either, even where the caller expects its name.

### Case description

```yaml
gesBlock: case
id: read-document-message
schemaApi:
  sources:
    - "message Ping()\nschema :Unit as { on: :Boolean }\n"
  readDocument:
    text: ":Schema(:Ping)\n"
    expected: ["Ping"]
```

### Expectation

```yaml
gesBlock: expect
schema:
  readDocument:
    error:
      code: "document.schemaMismatch"
      line: 1
      column: 1
      path: ""
```

---

## Test: a document of an expected undeclared schema

An expected name that the set does not declare is `document.schemaMismatch` too.

### Case description

```yaml
gesBlock: case
id: read-document-undeclared-schema
schemaApi:
  sources:
    - "schema :Unit as { on: :Boolean }\n"
  readDocument:
    text: ":Schema(:Nope)\n"
    expected: ["Nope"]
```

### Expectation

```yaml
gesBlock: expect
schema:
  readDocument:
    error:
      code: "document.schemaMismatch"
      line: 1
      column: 1
      path: ""
```

---

## Test: a document without a header

A document without a header names no schema.

### Case description

```yaml
gesBlock: case
id: read-document-no-header
schemaApi:
  sources:
    - "schema :Unit as {\n  name: :Text max 16\n  hp: :Number integer between 0 and 100 default 100\n  parts: :List(:Part) max 4\n}\nschema :Part as { kind: :Tag(#arm, #leg) }\nschema :Base as { id: :Number integer }\n"
  readDocument:
    text: "name: 'scout'\nparts: []\n"
    expected: ["Unit"]
```

### Expectation

```yaml
gesBlock: expect
schema:
  readDocument:
    error:
      code: "document.schemaMismatch"
      line: 1
      column: 1
      path: ""
```

---

## Test: an error inside a list element names its path

The error has its position and the path of the field, as the value path names it.

### Case description

```yaml
gesBlock: case
id: read-document-nested-path
schemaApi:
  sources:
    - "schema :Unit as {\n  name: :Text max 16\n  hp: :Number integer between 0 and 100 default 100\n  parts: :List(:Part) max 4\n}\nschema :Part as { kind: :Tag(#arm, #leg) }\nschema :Base as { id: :Number integer }\n"
  readDocument:
    text: ":Schema(:Unit)\nname: 'scout'\nparts: [[kind: #arm], [kind: #tail]]\n"
    expected: ["Unit"]
```

### Expectation

```yaml
gesBlock: expect
schema:
  readDocument:
    error:
      code: "document.fieldType"
      line: 3
      column: 30
      path: "parts[].kind"
```

---

## Test: a missing field names its path

A missing field is reported where the record that lacks it starts, here the document.

### Case description

```yaml
gesBlock: case
id: read-document-missing-field
schemaApi:
  sources:
    - "schema :Unit as {\n  name: :Text max 16\n  hp: :Number integer between 0 and 100 default 100\n  parts: :List(:Part) max 4\n}\nschema :Part as { kind: :Tag(#arm, #leg) }\nschema :Base as { id: :Number integer }\n"
  readDocument:
    text: ":Schema(:Unit)\nparts: []\n"
    expected: ["Unit"]
```

### Expectation

```yaml
gesBlock: expect
schema:
  readDocument:
    error:
      code: "document.missingField"
      line: 1
      column: 1
      path: "name"
```

---

## Test: an unknown field names its path


### Case description

```yaml
gesBlock: case
id: read-document-unknown-field
schemaApi:
  sources:
    - "schema :Unit as {\n  name: :Text max 16\n  hp: :Number integer between 0 and 100 default 100\n  parts: :List(:Part) max 4\n}\nschema :Part as { kind: :Tag(#arm, #leg) }\nschema :Base as { id: :Number integer }\n"
  readDocument:
    text: ":Schema(:Unit)\nname: 'scout'\nparts: []\nlevel: 3\n"
    expected: ["Unit"]
```

### Expectation

```yaml
gesBlock: expect
schema:
  readDocument:
    error:
      code: "document.unknownField"
      line: 4
      column: 1
      path: "level"
```

---

## Test: an unreadable document has no path

A reading error comes before the schema and has no field path.

### Case description

```yaml
gesBlock: case
id: read-document-unreadable
schemaApi:
  sources:
    - "schema :Unit as {\n  name: :Text max 16\n  hp: :Number integer between 0 and 100 default 100\n  parts: :List(:Part) max 4\n}\nschema :Part as { kind: :Tag(#arm, #leg) }\nschema :Base as { id: :Number integer }\n"
  readDocument:
    text: ":Schema(:Unit)\nname: 'scout'\nparts: [\n"
    expected: ["Unit"]
```

### Expectation

```yaml
gesBlock: expect
schema:
  readDocument:
    error:
      code: "document.unclosedBracket"
      line: 3
      column: 8
      path: ""
```

---

## Test: a document Disc reads as the library's value

A document writes a Disc as the Map of its centre and radius; reading gives the geometry library's value with the
radius's magnitude.

### Case description

```yaml
gesBlock: case
id: read-document-disc
schemaApi:
  sources:
    - "schema :Zone as {\n  reach: :Disc(m)\n}\n"
  readDocument:
    text: ":Schema(:Zone)\nreach: [center: :Point(2m, 1m), radius: -3m]\n"
    expected: ["Zone"]
```

### Expectation

```yaml
gesBlock: expect
schema:
  readDocument:
    schema: "Zone"
    value:
      type: ":Zone"
      entries:
        - key: "reach"
          value:
            type: ":Disc"
            center:
              type: ":Point"
              x: "2"
              y: "1"
              z: "0"
            radius: "3"
            unit: ":meter"
```

---

## Test: a document Box and Poly read as the library's values

A document writes a Box as the Map of its corners and a Poly as the List of its points; reading gives the geometry library's
values that a model converts.

### Case description

```yaml
gesBlock: case
id: read-document-geometry
schemaApi:
  sources:
    - "schema :Zone as {\n  area: :Box(m)\n  outline: :Poly max 3\n}\n"
  readDocument:
    text: ":Schema(:Zone)\narea: [lower: :Point(20m, 1m), upper: :Point(5m, 3m)]\noutline: [:Point(0, 0), :Point(4, 0)]\n"
    expected: ["Zone"]
```

### Expectation

```yaml
gesBlock: expect
schema:
  readDocument:
    schema: "Zone"
    value:
      type: ":Zone"
      entries:
        - key: "area"
          value:
            type: ":Box"
            lower:
              type: ":Point"
              x: "5"
              y: "1"
              z: "0"
            upper:
              type: ":Point"
              x: "20"
              y: "3"
              z: "0"
            unit: ":meter"
        - key: "outline"
          value:
            type: ":Poly"
            points:
              - type: ":Point"
                x: "0"
                y: "0"
                z: "0"
              - type: ":Point"
                x: "4"
                y: "0"
                z: "0"
```

---

## Test: a document union takes the alternative the check chose

Reading takes the alternative of a union that the document check accepted, not the first one whose shape fits: two points
break the `max` of the Poly, so they stay the List of Points that the second alternative accepts.

### Case description

```yaml
gesBlock: case
id: read-document-union-choice
schemaApi:
  sources:
    - "schema :Zone as {\n  outline: :Poly max 1 | :List(:Point) max 5\n}\n"
  readDocument:
    text: ":Schema(:Zone)\noutline: [:Point(1, 2), :Point(3, 4)]\n"
    expected: ["Zone"]
```

### Expectation

```yaml
gesBlock: expect
schema:
  readDocument:
    schema: "Zone"
    value:
      type: ":Zone"
      entries:
        - key: "outline"
          value:
            type: ":List"
            items:
              - type: ":Point"
                x: "1"
                y: "2"
                z: "0"
              - type: ":Point"
                x: "3"
                y: "4"
                z: "0"
```

---

## Test: a document union inside a Record form takes the checked alternative

A record written as `:Record(...)` keeps the alternatives that the check chose for its fields, as a Map does.

### Case description

```yaml
gesBlock: case
id: read-document-union-choice-in-form
schemaApi:
  sources:
    - "schema :Zone as { inner: :Inner }\nschema :Inner as { outline: :Poly max 1 | :List(:Point) max 5 }\n"
  readDocument:
    text: ":Schema(:Zone)\ninner: :Record('Inner', [outline: [:Point(1, 2), :Point(3, 4)]])\n"
    expected: ["Zone"]
```

### Expectation

```yaml
gesBlock: expect
schema:
  readDocument:
    schema: "Zone"
    value:
      type: ":Zone"
      entries:
        - key: "inner"
          value:
            type: ":Inner"
            entries:
              - key: "outline"
                value:
                  type: ":List"
                  items:
                    - type: ":Point"
                      x: "1"
                      y: "2"
                      z: "0"
                    - type: ":Point"
                      x: "3"
                      y: "4"
                      z: "0"
```

---

## Test: a union continues on a line that starts with |

A line whose first token is `|` continues the union of the field before it, and the field's marker follows the last alternative.

### Case description

```yaml
gesBlock: case
id: union-leading-bar
schemaApi:
  sources:
    - "schema :S as {\n  shape: :Text max 8\n    | :Number optional\n}\n"
  root: "S"
  bind: false
```

### Expectation

```yaml
gesBlock: expect
schema:
  canonical: "gess1\nschema S\nfield shape Text max 8 | Number optional\n"
```

---

## Test: a union continues after a | that ends a line



### Case description

```yaml
gesBlock: case
id: union-trailing-bar
schemaApi:
  sources:
    - "schema :S as {\n  shape: :Text max 8 |\n    :Number optional\n}\n"
  root: "S"
  bind: false
```

### Expectation

```yaml
gesBlock: expect
schema:
  canonical: "gess1\nschema S\nfield shape Text max 8 | Number optional\n"
```

---

## Test: comments and blank lines between alternatives



### Case description

```yaml
gesBlock: case
id: union-comment-between
schemaApi:
  sources:
    - "schema :S as {\n  shape: :Text max 8 // a name\n\n    // or a number\n    | :Number optional\n}\n"
  root: "S"
  bind: false
```

### Expectation

```yaml
gesBlock: expect
schema:
  canonical: "gess1\nschema S\nfield shape Text max 8 | Number optional\n"
```

---

## Test: a list element union spans lines

The element union of a List and its labels may span lines like a field's.

### Case description

```yaml
gesBlock: case
id: union-list-element-lines
schemaApi:
  sources:
    - "schema :S as {\n  items: :List(id: :Text max 8\n    | count: :Number) max 2\n}\n"
  root: "S"
  bind: false
```

### Expectation

```yaml
gesBlock: expect
schema:
  canonical: "gess1\nschema S\nfield items List(Text max 8 | Number) max 2\n"
```

---

## Test: a field that starts with |

A field never starts with `|`; one that does continues nothing.

### Case description

```yaml
gesBlock: case
id: error-union-bar-continues-nothing
schemaApi:
  sources:
    - "schema :S as {\n  | :Text max 8\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.syntax"
    line: 2
    column: 3
```

---

## Test: an error inside a continued alternative

The error has the position of the alternative on its own line.

### Case description

```yaml
gesBlock: case
id: error-union-continued-alternative
schemaApi:
  sources:
    - "schema :S as {\n  shape: :Text max 8\n    | :Number between 5 and 1\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidRange"
    line: 3
    column: 23
```

---

## Test: recursion without depth

The source is rejected.

### Case description

```yaml
gesBlock: case
id: error-depth-missing
schemaApi:
  sources:
    - "schema :A as { b: :List(:A) max 2 }\n"
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

## Test: a depth of zero

The source is rejected.

### Case description

```yaml
gesBlock: case
id: error-depth-zero
schemaApi:
  sources:
    - "schema :A as { b: :List(:A) max 2 depth 0 }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.syntax"
    line: 1
    column: 41
```

---

## Test: a recursion deeper than lists nest

Each level of a recursive List nests one list further, and lists nest at most 4 levels for encoding.

### Case description

```yaml
gesBlock: case
id: error-depth-lists
schemaApi:
  sources:
    - "schema :A as { b: :List(:A) max 2 depth 5 }\n"
  root: "A"
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
## Test: a changed tree

The deepest name differs from the tree as deep as its depth.

### Case description

```yaml
gesBlock: case
id: encode-recursive-changed
schemaApi:
  sources:
    - "schema :Loadout as { root: :Bind }\nschema :Bind as {\n  name: :Text max 8\n  units: :List(:Bind) max 4 depth 3\n}\n"
  root: "Loadout"
  encode:
    type: ":Loadout"
    entries:
      - key: "root"
        value:
          type: ":Bind"
          entries:
            - key: "name"
              value:
                type: ":Text"
                value: "a"
            - key: "units"
              value:
                type: ":List"
                items:
                  - type: ":Bind"
                    entries:
                      - key: "name"
                        value:
                          type: ":Text"
                          value: "b"
                      - key: "units"
                        value:
                          type: ":List"
                          items:
                            - type: ":Bind"
                              entries:
                                - key: "name"
                                  value:
                                    type: ":Text"
                                    value: "e"
                                - key: "units"
                                  value:
                                    type: ":List"
                                    items: []
                  - type: ":Bind"
                    entries:
                      - key: "name"
                        value:
                          type: ":Text"
                          value: "d"
                      - key: "units"
                        value:
                          type: ":List"
                          items: []
  decode: "11a6104b94051900"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "11a6104b94051900"
  decoded:
    value:
      type: ":Loadout"
      entries:
        - key: "root"
          value:
            type: ":Bind"
            entries:
              - key: "name"
                value:
                  type: ":Text"
                  value: "a"
              - key: "units"
                value:
                  type: ":List"
                  items:
                    - type: ":Bind"
                      entries:
                        - key: "name"
                          value:
                            type: ":Text"
                            value: "b"
                        - key: "units"
                          value:
                            type: ":List"
                            items:
                              - type: ":Bind"
                                entries:
                                  - key: "name"
                                    value:
                                      type: ":Text"
                                      value: "e"
                                  - key: "units"
                                    value:
                                      type: ":List"
                                      items: []
                    - type: ":Bind"
                      entries:
                        - key: "name"
                          value:
                            type: ":Text"
                            value: "d"
                        - key: "units"
                          value:
                            type: ":List"
                            items: []
    byteCount: 8
```

---

## Test: a keyed tree

A recursive List of a schema with a key is keyed at every level but the deepest.

### Case description

```yaml
gesBlock: case
id: encode-recursive-keyed
schemaApi:
  sources:
    - "schema :Team as { root: :Unit }\nschema :Unit as {\n  id: :Number integer between 0 and 15 key\n  units: :List(:Unit) max 4 depth 3\n}\n"
  root: "Team"
  encode:
    type: ":Team"
    entries:
      - key: "root"
        value:
          type: ":Unit"
          entries:
            - key: "id"
              value:
                type: ":Number.int64"
                value: "1"
            - key: "units"
              value:
                type: ":List"
                items:
                  - type: ":Unit"
                    entries:
                      - key: "id"
                        value:
                          type: ":Number.int64"
                          value: "2"
                      - key: "units"
                        value:
                          type: ":List"
                          items:
                            - type: ":Unit"
                              entries:
                                - key: "id"
                                  value:
                                    type: ":Number.int64"
                                    value: "3"
                                - key: "units"
                                  value:
                                    type: ":List"
                                    items: []
                  - type: ":Unit"
                    entries:
                      - key: "id"
                        value:
                          type: ":Number.int64"
                          value: "4"
                      - key: "units"
                        value:
                          type: ":List"
                          items: []
  decode: "21c91000"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "21c91000"
  decoded:
    value:
      type: ":Team"
      entries:
        - key: "root"
          value:
            type: ":Unit"
            entries:
              - key: "id"
                value:
                  type: ":Number.int64"
                  value: "1"
              - key: "units"
                value:
                  type: ":List"
                  items:
                    - type: ":Unit"
                      entries:
                        - key: "id"
                          value:
                            type: ":Number.int64"
                            value: "2"
                        - key: "units"
                          value:
                            type: ":List"
                            items:
                              - type: ":Unit"
                                entries:
                                  - key: "id"
                                    value:
                                      type: ":Number.int64"
                                      value: "3"
                                  - key: "units"
                                    value:
                                      type: ":List"
                                      items: []
                    - type: ":Unit"
                      entries:
                        - key: "id"
                          value:
                            type: ":Number.int64"
                            value: "4"
                        - key: "units"
                          value:
                            type: ":List"
                            items: []
    byteCount: 4
```

---

## Test: a keyed tree with an added record

A record is added at the deepest level that holds records.

### Case description

```yaml
gesBlock: case
id: encode-recursive-keyed-added
schemaApi:
  sources:
    - "schema :Team as { root: :Unit }\nschema :Unit as {\n  id: :Number integer between 0 and 15 key\n  units: :List(:Unit) max 4 depth 3\n}\n"
  root: "Team"
  encode:
    type: ":Team"
    entries:
      - key: "root"
        value:
          type: ":Unit"
          entries:
            - key: "id"
              value:
                type: ":Number.int64"
                value: "1"
            - key: "units"
              value:
                type: ":List"
                items:
                  - type: ":Unit"
                    entries:
                      - key: "id"
                        value:
                          type: ":Number.int64"
                          value: "2"
                      - key: "units"
                        value:
                          type: ":List"
                          items:
                            - type: ":Unit"
                              entries:
                                - key: "id"
                                  value:
                                    type: ":Number.int64"
                                    value: "3"
                                - key: "units"
                                  value:
                                    type: ":List"
                                    items: []
                            - type: ":Unit"
                              entries:
                                - key: "id"
                                  value:
                                    type: ":Number.int64"
                                    value: "5"
                                - key: "units"
                                  value:
                                    type: ":List"
                                    items: []
                  - type: ":Unit"
                    entries:
                      - key: "id"
                        value:
                          type: ":Number.int64"
                          value: "4"
                      - key: "units"
                        value:
                          type: ":List"
                          items: []
  decode: "21d11401"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "21d11401"
  decoded:
    value:
      type: ":Team"
      entries:
        - key: "root"
          value:
            type: ":Unit"
            entries:
              - key: "id"
                value:
                  type: ":Number.int64"
                  value: "1"
              - key: "units"
                value:
                  type: ":List"
                  items:
                    - type: ":Unit"
                      entries:
                        - key: "id"
                          value:
                            type: ":Number.int64"
                            value: "2"
                        - key: "units"
                          value:
                            type: ":List"
                            items:
                              - type: ":Unit"
                                entries:
                                  - key: "id"
                                    value:
                                      type: ":Number.int64"
                                      value: "3"
                                  - key: "units"
                                    value:
                                      type: ":List"
                                      items: []
                              - type: ":Unit"
                                entries:
                                  - key: "id"
                                    value:
                                      type: ":Number.int64"
                                      value: "5"
                                  - key: "units"
                                    value:
                                      type: ":List"
                                      items: []
                    - type: ":Unit"
                      entries:
                        - key: "id"
                          value:
                            type: ":Number.int64"
                            value: "4"
                        - key: "units"
                          value:
                            type: ":List"
                            items: []
    byteCount: 4
```

---

## Test: a recursive List that with copies

Where `with` copies a recursive List into another schema, it holds records of the declaring schema, which nest up to its depth below it.

### Case description

```yaml
gesBlock: case
id: encode-recursive-composed
schemaApi:
  sources:
    - "schema :Node as {\n  tag: :Text max 4\n  kids: :List(:Node) max 2 depth 3\n}\nschema :Leaf as {\n  with :Node\n  name: :Text max 4\n}\n"
  root: "Leaf"
  encode:
    type: ":Leaf"
    entries:
      - key: "tag"
        value:
          type: ":Text"
          value: "l"
      - key: "kids"
        value:
          type: ":List"
          items:
            - type: ":Node"
              entries:
                - key: "tag"
                  value:
                    type: ":Text"
                    value: "a"
                - key: "kids"
                  value:
                    type: ":List"
                    items:
                      - type: ":Node"
                        entries:
                          - key: "tag"
                            value:
                              type: ":Text"
                              value: "b"
                          - key: "kids"
                            value:
                              type: ":List"
                              items:
                                - type: ":Node"
                                  entries:
                                    - key: "tag"
                                      value:
                                        type: ":Text"
                                        value: "c"
                                    - key: "kids"
                                      value:
                                        type: ":List"
                                        items: []
      - key: "name"
        value:
          type: ":Text"
          value: "n"
  decode: "612b6145ac8cc50d"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "612b6145ac8cc50d"
  decoded:
    value:
      type: ":Leaf"
      entries:
        - key: "tag"
          value:
            type: ":Text"
            value: "l"
        - key: "kids"
          value:
            type: ":List"
            items:
              - type: ":Node"
                entries:
                  - key: "tag"
                    value:
                      type: ":Text"
                      value: "a"
                  - key: "kids"
                    value:
                      type: ":List"
                      items:
                        - type: ":Node"
                          entries:
                            - key: "tag"
                              value:
                                type: ":Text"
                                value: "b"
                            - key: "kids"
                              value:
                                type: ":List"
                                items:
                                  - type: ":Node"
                                    entries:
                                      - key: "tag"
                                        value:
                                          type: ":Text"
                                          value: "c"
                                      - key: "kids"
                                        value:
                                          type: ":List"
                                          items: []
        - key: "name"
          value:
            type: ":Text"
            value: "n"
    byteCount: 8
```

---

## Test: a copied recursive List deeper than its depth

The fourth record of the declaring schema is deeper than the depth: `wire.tooDeep` at the List that holds it.

### Case description

```yaml
gesBlock: case
id: error-encode-too-deep-composed
schemaApi:
  sources:
    - "schema :Node as {\n  tag: :Text max 4\n  kids: :List(:Node) max 2 depth 3\n}\nschema :Leaf as {\n  with :Node\n  name: :Text max 4\n}\n"
  root: "Leaf"
  encode:
    type: ":Leaf"
    entries:
      - key: "tag"
        value:
          type: ":Text"
          value: "l"
      - key: "kids"
        value:
          type: ":List"
          items:
            - type: ":Node"
              entries:
                - key: "tag"
                  value:
                    type: ":Text"
                    value: "a"
                - key: "kids"
                  value:
                    type: ":List"
                    items:
                      - type: ":Node"
                        entries:
                          - key: "tag"
                            value:
                              type: ":Text"
                              value: "b"
                          - key: "kids"
                            value:
                              type: ":List"
                              items:
                                - type: ":Node"
                                  entries:
                                    - key: "tag"
                                      value:
                                        type: ":Text"
                                        value: "c"
                                    - key: "kids"
                                      value:
                                        type: ":List"
                                        items:
                                          - type: ":Node"
                                            entries:
                                              - key: "tag"
                                                value:
                                                  type: ":Text"
                                                  value: "d"
                                              - key: "kids"
                                                value:
                                                  type: ":List"
                                                  items: []
      - key: "name"
        value:
          type: ":Text"
          value: "n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    error:
      code: "wire.tooDeep"
      path: "kids[].kids[].kids[].kids"
```

---

## Test: a depth of one

A depth of 1 leaves the List always empty, so the source is rejected.

### Case description

```yaml
gesBlock: case
id: error-depth-one
schemaApi:
  sources:
    - "schema :A as { on: :Boolean, b: :List(:A) max 2 depth 1 }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidConstraint"
    line: 1
    column: 49
```

---

## Test: two recursive Lists

A schema holds at most one List with depth.

### Case description

```yaml
gesBlock: case
id: error-depth-twice
schemaApi:
  sources:
    - "schema :A as { on: :Boolean, b: :List(:A) max 2 depth 2, c: :List(:A) max 2 depth 2 }\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.invalidConstraint"
    line: 1
    column: 77
```

---

## Test: a cycle through a copied recursive List

A recursive List that `with` copies is an ordinary List of the declaring schema, so a schema that holds the copy inside the declaring one forms a cycle.

### Case description

```yaml
gesBlock: case
id: error-depth-cycle-composed
schemaApi:
  sources:
    - "schema :Node as {\n  on: :Boolean\n  kids: :List(:Node) max 2 depth 2\n  more: :List(:Extra) max 1\n}\nschema :Extra as {\n  with :Node\n}\n"
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
## Test: a delta of a tree

A record at the deepest level changes; the delta descends through each level of the recursion.

### Case description

```yaml
gesBlock: case
id: delta-recursive
schemaApi:
  sources:
    - "schema :Loadout as { root: :Bind }\nschema :Bind as {\n  name: :Text max 8\n  units: :List(:Bind) max 4 depth 3\n}\n"
  root: "Loadout"
  deltaEncode:
    record: "11a6104b94051900"
    baseline: "11a6104b8c051900"
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaEncoded:
    bytes: "558825ca820c"
```

---

## Test: a delta of a tree gives the tree back

The delta of a tree reads into the changed tree.

### Case description

```yaml
gesBlock: case
id: delta-recursive-decoded
schemaApi:
  sources:
    - "schema :Loadout as { root: :Bind }\nschema :Bind as {\n  name: :Text max 8\n  units: :List(:Bind) max 4 depth 3\n}\n"
  root: "Loadout"
  deltaDecode:
    delta: "558825ca820c"
    baseline: "11a6104b8c051900"
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaDecoded:
    record: "11a6104b94051900"
    deltaByteCount: 6
```

---

## Test: a delta of a keyed tree

A record is added to a keyed List one level below the root; the deepest level holds no records and is no keyed List.

### Case description

```yaml
gesBlock: case
id: delta-recursive-keyed
schemaApi:
  sources:
    - "schema :Team as { root: :Unit }\nschema :Unit as {\n  id: :Number integer between 0 and 15 key\n  units: :List(:Unit) max 4 depth 3\n}\n"
  root: "Team"
  deltaEncode:
    record: "21d11401"
    baseline: "21c91000"
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaEncoded:
    bytes: "05849002"
```

---

## Test: a delta of a keyed tree gives the tree back

The delta of a keyed tree reads into the changed tree.

### Case description

```yaml
gesBlock: case
id: delta-recursive-keyed-decoded
schemaApi:
  sources:
    - "schema :Team as { root: :Unit }\nschema :Unit as {\n  id: :Number integer between 0 and 15 key\n  units: :List(:Unit) max 4 depth 3\n}\n"
  root: "Team"
  deltaDecode:
    delta: "05849002"
    baseline: "21c91000"
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaDecoded:
    record: "21d11401"
    deltaByteCount: 4
```

---
## Test: a guarded optional field that is absent

`has value` guards the reads of an optional field, and `default` replaces it; an optional computed field is absent with it.

### Case description

```yaml
gesBlock: case
id: decode-computed-guarded-absent
schemaApi:
  sources:
    - "schema :Guarded as {\n  a: :Number between 0 and 100 step 0.5 optional\n  twice: :Number computed by a * 2 when a has value, otherwise 0\n  large: :Boolean computed by a has value and a > 1\n  filled: :Number computed by a default 5\n  next: :Number optional computed by a + 1\n}\n"
  root: "Guarded"
  encode:
    type: ":Guarded"
    entries: []
  decode: "00"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "00"
  decoded:
    value:
      type: ":Guarded"
      entries:
        - key: "a"
          value:
            type: ":Nothing"
        - key: "filled"
          value:
            type: ":Number.int64"
            value: "5"
        - key: "large"
          value:
            type: ":Boolean"
            value: false
        - key: "next"
          value:
            type: ":Nothing"
        - key: "twice"
          value:
            type: ":Number.int64"
            value: "0"
    byteCount: 1
```

---

## Test: a guarded optional field that is present

The same fields with the optional field present.

### Case description

```yaml
gesBlock: case
id: decode-computed-guarded-present
schemaApi:
  sources:
    - "schema :Guarded as {\n  a: :Number between 0 and 100 step 0.5 optional\n  twice: :Number computed by a * 2 when a has value, otherwise 0\n  large: :Boolean computed by a has value and a > 1\n  filled: :Number computed by a default 5\n  next: :Number optional computed by a + 1\n}\n"
  root: "Guarded"
  encode:
    type: ":Guarded"
    entries:
      - key: "a"
        value:
          type: ":Number.binary64"
          value: "2.5"
  decode: "0b00"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "0b00"
  decoded:
    value:
      type: ":Guarded"
      entries:
        - key: "a"
          value:
            type: ":Number.binary64"
            value: "2.5"
        - key: "filled"
          value:
            type: ":Number.binary64"
            value: "2.5"
        - key: "large"
          value:
            type: ":Boolean"
            value: true
        - key: "next"
          value:
            type: ":Number.binary64"
            value: "3.5"
        - key: "twice"
          value:
            type: ":Number.int64"
            value: "5"
    byteCount: 2
```

---

## Test: a required computed number that may be absent

An optional operand without `has value` or `default` would give nothing: the source is rejected.

### Case description

```yaml
gesBlock: case
id: error-computed-may-be-absent
schemaApi:
  sources:
    - "schema :S as {\n  a: :Number optional\n  b: :Number computed by a * 2\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.computedMayBeAbsent"
    line: 3
    column: 26
```

---

## Test: a computed Boolean that may be absent

A computed Boolean is never optional, so an operand that may be absent needs a guard or a fallback.

### Case description

```yaml
gesBlock: case
id: error-computed-may-be-absent-boolean
schemaApi:
  sources:
    - "schema :S as {\n  flag: :Boolean optional\n  on: :Boolean computed by flag and true\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.computedMayBeAbsent"
    line: 3
    column: 28
```

---

## Test: a guard on another field

`has value` proves only the field it names present.

### Case description

```yaml
gesBlock: case
id: error-computed-may-be-absent-other-guard
schemaApi:
  sources:
    - "schema :S as {\n  a: :Number optional\n  b: :Number optional\n  c: :Number computed by a + b when a has value, otherwise 0\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.computedMayBeAbsent"
    line: 4
    column: 26
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

## Test: a Box and a Poly with axis blocks

A Box writes its corners `lower` and `upper` as two Points of the block's axes; a Poly writes its point count and
then its points, each with the axes x and y.

### Case description

```yaml
gesBlock: case
id: spatial-box-poly
schemaApi:
  sources:
    - "schema :Zone as {\n  area: :Box(m) {\n    x between 0m and 100m step 1m\n    y between 0m and 3m step 1m\n  }\n  outline: :Poly(m) max 3 {\n    x between 0m and 100m step 1m\n    y between 0m and 3m step 1m\n  }\n}\n"
  root: "Zone"
  encode:
    type: ":Zone"
    entries:
      - key: "area"
        value:
          type: ":Box"
          lower:
            type: ":Point"
            x: "5"
            y: "1"
            z: "0"
          upper:
            type: ":Point"
            x: "20"
            y: "3"
            z: "0"
          unit: ":meter"
      - key: "outline"
        value:
          type: ":Poly"
          points:
            - type: ":Point"
              x: "0"
              y: "0"
              z: "0"
            - type: ":Point"
              x: "10"
              y: "0"
              z: "0"
            - type: ":Point"
              x: "0"
              y: "2"
              z: "0"
          unit: ":meter"
  decode: "85280f400140"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "85280f400140"
  decoded:
    value:
      type: ":Zone"
      entries:
        - key: "area"
          value:
            type: ":Box"
            lower:
              type: ":Point"
              x: "5"
              y: "1"
              z: "0"
            upper:
              type: ":Point"
              x: "20"
              y: "3"
              z: "0"
            unit: ":meter"
        - key: "outline"
          value:
            type: ":Poly"
            points:
              - type: ":Point"
                x: "0"
                y: "0"
                z: "0"
              - type: ":Point"
                x: "10"
                y: "0"
                z: "0"
              - type: ":Point"
                x: "0"
                y: "2"
                z: "0"
            unit: ":meter"
    byteCount: 6
```

---

## Test: a Box and a Poly without axis blocks

Without a block each corner takes x, y and z as binary64 values, and each point of a Poly x and y.

### Case description

```yaml
gesBlock: case
id: spatial-box-poly-raw
schemaApi:
  sources:
    - "schema :Zone as {\n  area: :Box\n  outline: :Poly max 1\n}\n"
  root: "Zone"
  encode:
    type: ":Zone"
    entries:
      - key: "area"
        value:
          type: ":Box"
          lower:
            type: ":Point"
            x: "-1"
            y: "0"
            z: "0"
          upper:
            type: ":Point"
            x: "1.5"
            y: "2"
            z: "0.5"
      - key: "outline"
        value:
          type: ":Poly"
          points:
            - type: ":Point"
              x: "2"
              y: "-0.5"
              z: "0"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "000000000000f0bf00000000000000000000000000000000000000000000f83f0000000000000040000000000000e03f0100000000000080000000000000c07f01"
```

---

## Test: a decoded Box orders its corners

The corners are two Points on the wire. A decoder accepts them in any order, and the Box takes the smaller component
on each axis as `lower` and the larger as `upper`, as creating a Box from two corners does. Writing that Box gives its
corners in order.

### Case description

```yaml
gesBlock: case
id: spatial-box-ordered
schemaApi:
  sources:
    - "schema :Zone as {\n  area: :Box(m) {\n    x between 0m and 100m step 1m\n    y between 0m and 3m step 1m\n  }\n  outline: :Poly(m) max 3 {\n    x between 0m and 100m step 1m\n    y between 0m and 3m step 1m\n  }\n}\n"
  root: "Zone"
  encode:
    type: ":Zone"
    entries:
      - key: "area"
        value:
          type: ":Box"
          lower:
            type: ":Point"
            x: "20"
            y: "1"
            z: "0"
          upper:
            type: ":Point"
            x: "5"
            y: "3"
            z: "0"
          unit: ":meter"
      - key: "outline"
        value:
          type: ":Poly"
          points: []
          unit: ":meter"
  decode: "940a03"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "852803"
  decoded:
    value:
      type: ":Zone"
      entries:
        - key: "area"
          value:
            type: ":Box"
            lower:
              type: ":Point"
              x: "5"
              y: "1"
              z: "0"
            upper:
              type: ":Point"
              x: "20"
              y: "3"
              z: "0"
            unit: ":meter"
        - key: "outline"
          value:
            type: ":Poly"
            points: []
            unit: ":meter"
    byteCount: 3
```

---

## Test: a Disc with an axis block

A Disc writes its centre as a Point of the block's axes x and y, then its radius.

### Case description

```yaml
gesBlock: case
id: spatial-disc
schemaApi:
  sources:
    - "schema :Zone as {\n  reach: :Disc(m) {\n    x between 0m and 100m step 1m\n    y between 0m and 3m step 1m\n    radius between 0m and 7m step 1m\n  }\n}\n"
  root: "Zone"
  encode:
    type: ":Zone"
    entries:
      - key: "reach"
        value:
          type: ":Disc"
          center:
            type: ":Point"
            x: "5"
            y: "1"
            z: "0"
          radius: "3"
          unit: ":meter"
  decode: "8506"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "8506"
  decoded:
    value:
      type: ":Zone"
      entries:
        - key: "reach"
          value:
            type: ":Disc"
            center:
              type: ":Point"
              x: "5"
              y: "1"
              z: "0"
            radius: "3"
            unit: ":meter"
    byteCount: 2
```

---

## Test: a Disc without an axis block

Without a block the centre takes x and y and the radius one more binary64 value; a decoded radius takes its magnitude.

### Case description

```yaml
gesBlock: case
id: spatial-disc-raw
schemaApi:
  sources:
    - "schema :Zone as {\n  reach: :Disc\n}\n"
  root: "Zone"
  encode:
    type: ":Zone"
    entries:
      - key: "reach"
        value:
          type: ":Disc"
          center:
            type: ":Point"
            x: "2"
            y: "-0.5"
            z: "0"
          radius: "1.5"
  decode: "0000000000000040000000000000e0bf000000000000f8bf"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "0000000000000040000000000000e0bf000000000000f83f"
  decoded:
    value:
      type: ":Zone"
      entries:
        - key: "reach"
          value:
            type: ":Disc"
            center:
              type: ":Point"
              x: "2"
              y: "-0.5"
              z: "0"
            radius: "1.5"
    byteCount: 24
```

---

## Test: a Disc centre outside the plane

A Disc lies in the plane z = 0, which it does not transmit, so a centre with another `z` cannot be written.

### Case description

```yaml
gesBlock: case
id: spatial-disc-z
schemaApi:
  sources:
    - "schema :Zone as {\n  reach: :Disc\n}\n"
  root: "Zone"
  encode:
    type: ":Zone"
    entries:
      - key: "reach"
        value:
          type: ":Disc"
          center:
            type: ":Point"
            x: "0"
            y: "0"
            z: "1"
          radius: "1"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    error:
      code: "wire.typeMismatch"
      path: "reach.center"
```

---

## Test: a Disc block without the radius

A block that leaves out the radius transmits none, as for an axis it leaves out, so only a disc of radius 0 can be written.

### Case description

```yaml
gesBlock: case
id: spatial-disc-no-radius
schemaApi:
  sources:
    - "schema :Zone as {\n  reach: :Disc(m) {\n    x between 0m and 100m step 1m\n  }\n}\n"
  root: "Zone"
  encode:
    type: ":Zone"
    entries:
      - key: "reach"
        value:
          type: ":Disc"
          center:
            type: ":Point"
            x: "4"
            y: "0"
            z: "0"
          radius: "1"
          unit: ":meter"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    error:
      code: "wire.typeMismatch"
      path: "reach"
```

---

## Test: a Disc of another unit

A Disc keeps the unit of its centre and radius, which must be the field's unit.

### Case description

```yaml
gesBlock: case
id: spatial-disc-unit
schemaApi:
  sources:
    - "schema :Zone as {\n  reach: :Disc(m)\n}\n"
  root: "Zone"
  encode:
    type: ":Zone"
    entries:
      - key: "reach"
        value:
          type: ":Disc"
          center:
            type: ":Point"
            x: "0"
            y: "0"
            z: "0"
          radius: "1"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    error:
      code: "wire.typeMismatch"
      path: "reach"
```

---

## Test: Disc is a built-in type name

A declaration cannot take the name of a built-in type, so `Disc` names no schema.

### Case description

```yaml
gesBlock: case
id: error-disc-builtin-name
schemaApi:
  sources:
    - "schema :Disc as {\n  a: :Boolean\n}\n"
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

## Test: a Disc has no z axis

A Disc lies in the plane z = 0, so its block names x, y and the radius only.

### Case description

```yaml
gesBlock: case
id: error-disc-axis
schemaApi:
  sources:
    - "schema :Zone as {\n  reach: :Disc {\n    z between 0 and 1\n  }\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.syntax"
    line: 3
    column: 5
```

---

## Test: a Disc radius declared twice



### Case description

```yaml
gesBlock: case
id: error-disc-radius-twice
schemaApi:
  sources:
    - "schema :Zone as {\n  reach: :Disc {\n    radius between 0 and 1\n    radius between 0 and 2\n  }\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.duplicateName"
    line: 4
    column: 5
```

---

## Test: a Disc in the canonical form

The block of a Disc lists x and y, then the radius.

### Case description

```yaml
gesBlock: case
id: canonical-disc
schemaApi:
  sources:
    - "schema :Zone as {\n  reach: :Disc(m) {\n    radius between 0m and 7m step 1m\n    x between 0m and 100m\n  }\n}\n"
  root: "Zone"
```

### Expectation

```yaml
gesBlock: expect
schema:
  canonical: "gess1\nschema Zone\nfield reach Disc(meter) { x between 0e0 1e2 ; radius between 0e0 7e0 step 1e0 }\n"
  hash: "7d73f2dd1237c1b0"
```

---

## Test: a dump of a Disc

A Disc and its centre have no line of their own, only the axes of the centre and the radius.

### Case description

```yaml
gesBlock: case
id: dump-disc
schemaApi:
  sources:
    - "schema :Zone as {\n  reach: :Disc(m) {\n    x between 0m and 100m step 1m\n    y between 0m and 3m step 1m\n    radius between 0m and 7m step 1m\n  }\n}\n"
  root: "Zone"
  dump: "8506"
```

### Expectation

```yaml
gesBlock: expect
schema:
  dump:
    text: "reach.center.x bit=0 width=7 value=5\nreach.center.y bit=7 width=2 value=1\nreach.radius bit=9 width=3 value=3\nbytes 2\n"
    valid: true
```

---

## Test: a delta of a Disc

A Disc is a nested delta of its centre and radius, and its centre one of its axes.

### Case description

```yaml
gesBlock: case
id: delta-disc
schemaApi:
  sources:
    - "schema :Zone as {\n  reach: :Disc(m) {\n    x between 0m and 100m step 1m\n    y between 0m and 3m step 1m\n    radius between 0m and 7m step 1m\n  }\n}\n"
  root: "Zone"
  deltaEncode:
    record: "8506"
    baseline: "8502"
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaEncoded:
    bytes: "1d"
```

---

## Test: the description of a Disc



### Case description

```yaml
gesBlock: case
id: describe-disc
schemaApi:
  sources:
    - "schema :Zone as {\n  reach: :Disc(m) {\n    radius between 0m and 7m\n  }\n}\n"
  root: "Zone"
  bind: false
  describe: true
```

### Expectation

```yaml
gesBlock: expect
schema:
  description:
    name: "Zone"
    kind: "Schema"
    fields:
      - name: "reach"
        alternatives:
          - kind: "Disc"
            unit: "meter"
            axes:
              - name: "radius"
                lower: "0e0"
                upper: "7e0"
```

---

## Test: a Disc in a document

A document has no geometry literal: a Disc is the Map of its `center`, a Point, and its `radius`, a number of the unit, each within the block; a negative radius is checked by its magnitude, which the disc takes.

### Case description

```yaml
gesBlock: case
id: document-disc
schemaApi:
  sources:
    - "schema :Zone as {\n  reach: :Disc(m) {\n    x between 0m and 100m\n    radius between 0m and 7m\n  }\n}\n"
  document: ":Schema(:Zone)\nreach: [center: :Point(1m, 0m), radius: -3m]\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    ok: true
```

---

## Test: a document Disc without a radius



### Case description

```yaml
gesBlock: case
id: document-disc-missing-radius
schemaApi:
  sources:
    - "schema :Zone as {\n  reach: :Disc(m)\n}\n"
  document: ":Schema(:Zone)\nreach: [center: :Point(1m, 0m)]\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    error:
      code: "document.missingField"
      line: 2
      column: 8
```

---

## Test: a document Disc with a radius outside its range



### Case description

```yaml
gesBlock: case
id: document-disc-radius-range
schemaApi:
  sources:
    - "schema :Zone as {\n  reach: :Disc(m) {\n    radius between 0m and 7m\n  }\n}\n"
  document: ":Schema(:Zone)\nreach: [center: :Point(0m, 0m), radius: 8m]\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    error:
      code: "document.outOfRange"
      line: 2
      column: 41
```

---

## Test: a document Poly point outside the plane

A Poly lies in the plane z = 0 also without a block, so a document point with another `z` breaks the field's type.

### Case description

```yaml
gesBlock: case
id: document-poly-outside-plane
schemaApi:
  sources:
    - "schema :Zone as {\n  outline: :Poly max 4\n}\n"
  document: ":Schema(:Zone)\noutline: [:Point(0, 0), :Point(1, 2, 1)]\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    error:
      code: "document.fieldType"
      line: 2
      column: 25
```

---

## Test: a document Disc centre outside the plane

A Disc lies in the plane z = 0 also without a block, so a document centre with another `z` breaks the field's type.

### Case description

```yaml
gesBlock: case
id: document-disc-outside-plane
schemaApi:
  sources:
    - "schema :Zone as {\n  reach: :Disc\n}\n"
  document: ":Schema(:Zone)\nreach: [center: :Point(1, 0, 1), radius: 3]\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    error:
      code: "document.fieldType"
      line: 2
      column: 17
```

---

## Test: a packed schema in the canonical form

A packed schema writes `packed` and the width of its word after its name, so that the hash tells it from a schema of the same fields.

### Case description

```yaml
gesBlock: case
id: packed-canonical
schemaApi:
  sources:
    - "packed schema :MapPoint as {\n  height: :Number integer between 0 and 4095\n  surface: :Number integer between 0 and 15\n  vegetation: :Number integer between 0 and 15\n  moisture: :Number integer between 0 and 15\n  wear: :Number integer between 0 and 15\n  flags: :Flags(#impassable, #blocksLineOfSight, #indestructible, #reserved)\n}\n"
  root: "MapPoint"
```

### Expectation

```yaml
gesBlock: expect
schema:
  canonical: "gess1\nschema MapPoint packed 32\nfield height Number integer between 0e0 4095e0\nfield surface Number integer between 0e0 15e0\nfield vegetation Number integer between 0e0 15e0\nfield moisture Number integer between 0e0 15e0\nfield wear Number integer between 0e0 15e0\nfield flags Flags(#impassable #blocksLineOfSight #indestructible #reserved)\n"
  hash: "fb9ca75ded53145f"
```

---

## Test: a list of a scalar union

A union without Text, Bytes or List alternatives is a plain value, so generated code reads the elements of its list into
value items: a count of 2 in 3 bits, then index 0 with the integer 2 in 2 bits and index 1 with `true`.

### Case description

```yaml
gesBlock: case
id: list-of-scalar-union
schemaApi:
  sources:
    - "schema :U as {\n  items: :List(:Number integer between 0 and 3 | flag: :Boolean) max 4\n}\n"
  root: "U"
  encode:
    type: ":U"
    entries:
      - key: "items"
        value:
          type: ":List"
          items:
            - type: ":Number.int64"
              value: "2"
            - type: ":Boolean"
              value: "true"
  decode: "e2"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "e2"
  decoded:
    value:
      type: ":U"
      entries:
        - key: "items"
          value:
            type: ":List"
            items:
              - type: ":Number.int64"
                value: "2"
              - type: ":Boolean"
                value: "true"
    byteCount: 1
```

---

## Test: a packed record is its word

The fields of a packed record lie at their bits from the least significant bit in declared order, so its wire bytes are its word written little-endian: height 1000, surface 3, vegetation 7, moisture 15, wear 0 and the flags impassable and indestructible make the word 0x50f733e8.

### Case description

```yaml
gesBlock: case
id: packed-word
schemaApi:
  sources:
    - "packed schema :MapPoint as {\n  height: :Number integer between 0 and 4095\n  surface: :Number integer between 0 and 15\n  vegetation: :Number integer between 0 and 15\n  moisture: :Number integer between 0 and 15\n  wear: :Number integer between 0 and 15\n  flags: :Flags(#impassable, #blocksLineOfSight, #indestructible, #reserved)\n}\n"
  root: "MapPoint"
  encode:
    type: ":MapPoint"
    entries:
      - key: "height"
        value:
          type: ":Number.int64"
          value: "1000"
      - key: "surface"
        value:
          type: ":Number.int64"
          value: "3"
      - key: "vegetation"
        value:
          type: ":Number.int64"
          value: "7"
      - key: "moisture"
        value:
          type: ":Number.int64"
          value: "15"
      - key: "wear"
        value:
          type: ":Number.int64"
          value: "0"
      - key: "flags"
        value:
          type: ":Map"
          entries:
            - key: "impassable"
              value:
                type: ":Boolean"
                value: "true"
            - key: "indestructible"
              value:
                type: ":Boolean"
                value: "true"
  decode: "e833f750"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "e833f750"
  decoded:
    value:
      type: ":MapPoint"
      entries:
        - key: "height"
          value:
            type: ":Number.int64"
            value: "1000"
        - key: "surface"
          value:
            type: ":Number.int64"
            value: "3"
        - key: "vegetation"
          value:
            type: ":Number.int64"
            value: "7"
        - key: "moisture"
          value:
            type: ":Number.int64"
            value: "15"
        - key: "wear"
          value:
            type: ":Number.int64"
            value: "0"
        - key: "flags"
          value:
            type: ":Map"
            entries:
              - key: "impassable"
                value:
                  type: ":Boolean"
                  value: "true"
              - key: "indestructible"
                value:
                  type: ":Boolean"
                  value: "true"
    byteCount: 4
```

---

## Test: a nested packed record

A packed field continues the word with the bits of its fields: two Cells of 7 bits and a count of 2 bits fill the 16-bit word 0xd4d5.

### Case description

```yaml
gesBlock: case
id: packed-nested
schemaApi:
  sources:
    - "packed(8) schema :Cell as {\n  level: :Number integer between 0 and 9\n  on: :Boolean\n  kind: :Tag(#a, #b, #c)\n}\npacked(16) schema :Pair as {\n  low: :Cell\n  high: :Cell\n  count: :Number integer between 0 and 3\n}\n"
  root: "Pair"
  encode:
    type: ":Pair"
    entries:
      - key: "low"
        value:
          type: ":Cell"
          entries:
            - key: "level"
              value:
                type: ":Number.int64"
                value: "5"
            - key: "on"
              value:
                type: ":Boolean"
                value: "true"
            - key: "kind"
              value:
                type: ":Tag"
                value: "c"
      - key: "high"
        value:
          type: ":Cell"
          entries:
            - key: "level"
              value:
                type: ":Number.int64"
                value: "9"
            - key: "on"
              value:
                type: ":Boolean"
                value: "false"
            - key: "kind"
              value:
                type: ":Tag"
                value: "b"
      - key: "count"
        value:
          type: ":Number.int64"
          value: "3"
  decode: "d5d4"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "d5d4"
  decoded:
    value:
      type: ":Pair"
      entries:
        - key: "low"
          value:
            type: ":Cell"
            entries:
              - key: "level"
                value:
                  type: ":Number.int64"
                  value: "5"
              - key: "on"
                value:
                  type: ":Boolean"
                  value: "true"
              - key: "kind"
                value:
                  type: ":Tag"
                  value: "c"
        - key: "high"
          value:
            type: ":Cell"
            entries:
              - key: "level"
                value:
                  type: ":Number.int64"
                  value: "9"
              - key: "on"
                value:
                  type: ":Boolean"
                  value: "false"
              - key: "kind"
                value:
                  type: ":Tag"
                  value: "b"
        - key: "count"
          value:
            type: ":Number.int64"
            value: "3"
    byteCount: 2
```

---

## Test: a packed word with an index outside its range

A level of 12 in the 4 bits of a range of 0 to 9 is no value; decoding fails at the field as for any record.

### Case description

```yaml
gesBlock: case
id: packed-invalid-index
schemaApi:
  sources:
    - "packed(8) schema :Cell as {\n  level: :Number integer between 0 and 9\n  on: :Boolean\n  kind: :Tag(#a, #b, #c)\n}\npacked(16) schema :Pair as {\n  low: :Cell\n  high: :Cell\n  count: :Number integer between 0 and 3\n}\n"
  root: "Pair"
  decode: "0c00"
```

### Expectation

```yaml
gesBlock: expect
schema:
  decoded:
    error:
      code: "wire.invalidIndex"
      path: "low.level"
```

---

## Test: the description of a packed schema



### Case description

```yaml
gesBlock: case
id: packed-describe
schemaApi:
  sources:
    - "packed(8) schema :Cell as {\n  level: :Number integer between 0 and 9\n  on: :Boolean\n  kind: :Tag(#a, #b, #c)\n}\npacked(16) schema :Pair as {\n  low: :Cell\n  high: :Cell\n  count: :Number integer between 0 and 3\n}\n"
  root: "Pair"
  bind: false
  describe: true
```

### Expectation

```yaml
gesBlock: expect
schema:
  description:
    name: "Pair"
    kind: "Schema"
    packedWidth: 16
    packedBits: 16
    fields:
      - name: "low"
        alternatives:
          - kind: "Reference"
            schema: "Cell"
      - name: "high"
        alternatives:
          - kind: "Reference"
            schema: "Cell"
      - name: "count"
        alternatives:
          - kind: "Number"
            integer: true
            lower: "0e0"
            upper: "3e0"
```

---

## Test: a List of packed records

A List holds its packed records one after another at their bits, without padding to the word.

### Case description

```yaml
gesBlock: case
id: packed-list
schemaApi:
  sources:
    - "packed(8) schema :Cell as {\n  level: :Number integer between 0 and 9\n  on: :Boolean\n  kind: :Tag(#a, #b, #c)\n}\npacked(16) schema :Pair as {\n  low: :Cell\n  high: :Cell\n  count: :Number integer between 0 and 3\n}\nschema :Grid as {\n  cells: :List(:Cell) max 4\n  tick: :Number integer between 0 and 7\n}\n"
  root: "Grid"
  encode:
    type: ":Grid"
    entries:
      - key: "cells"
        value:
          type: ":List"
          items:
            - type: ":Cell"
              entries:
                - key: "level"
                  value:
                    type: ":Number.int64"
                    value: "5"
                - key: "on"
                  value:
                    type: ":Boolean"
                    value: "true"
                - key: "kind"
                  value:
                    type: ":Tag"
                    value: "c"
            - type: ":Cell"
              entries:
                - key: "level"
                  value:
                    type: ":Number.int64"
                    value: "9"
                - key: "on"
                  value:
                    type: ":Boolean"
                    value: "false"
                - key: "kind"
                  value:
                    type: ":Tag"
                    value: "b"
            - type: ":Cell"
              entries:
                - key: "level"
                  value:
                    type: ":Number.int64"
                    value: "5"
                - key: "on"
                  value:
                    type: ":Boolean"
                    value: "true"
                - key: "kind"
                  value:
                    type: ":Tag"
                    value: "c"
      - key: "tick"
        value:
          type: ":Number.int64"
          value: "6"
  decode: "aba6aa06"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "aba6aa06"
  decoded:
    value:
      type: ":Grid"
      entries:
        - key: "cells"
          value:
            type: ":List"
            items:
              - type: ":Cell"
                entries:
                  - key: "level"
                    value:
                      type: ":Number.int64"
                      value: "5"
                  - key: "on"
                    value:
                      type: ":Boolean"
                      value: "true"
                  - key: "kind"
                    value:
                      type: ":Tag"
                      value: "c"
              - type: ":Cell"
                entries:
                  - key: "level"
                    value:
                      type: ":Number.int64"
                      value: "9"
                  - key: "on"
                    value:
                      type: ":Boolean"
                      value: "false"
                  - key: "kind"
                    value:
                      type: ":Tag"
                      value: "b"
              - type: ":Cell"
                entries:
                  - key: "level"
                    value:
                      type: ":Number.int64"
                      value: "5"
                  - key: "on"
                    value:
                      type: ":Boolean"
                      value: "true"
                  - key: "kind"
                    value:
                      type: ":Tag"
                      value: "c"
        - key: "tick"
          value:
            type: ":Number.int64"
            value: "6"
    byteCount: 4
```

---

## Test: a delta of a packed field

A packed field changes as a whole: its change bit, then its word; the fields inside it have no change bits of their own.

### Case description

```yaml
gesBlock: case
id: packed-delta
schemaApi:
  sources:
    - "packed(8) schema :Cell as {\n  level: :Number integer between 0 and 9\n  on: :Boolean\n  kind: :Tag(#a, #b, #c)\n}\npacked(16) schema :Pair as {\n  low: :Cell\n  high: :Cell\n  count: :Number integer between 0 and 3\n}\nschema :Holder as {\n  pair: :Pair\n  tick: :Number integer between 0 and 7\n}\n"
  root: "Holder"
  deltaEncode:
    record: "d5d402"
    baseline: "55d402"
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaEncoded:
    bytes: "555303"
```

---

## Test: a packed field with a default

A packed record that leaves out a field with a default takes it as any record does: `a` 5 and `b` true make the word 0x0d.

### Case description

```yaml
gesBlock: case
id: packed-default
schemaApi:
  sources:
    - "packed schema :D as {\n  a: :Number integer between 0 and 7 default 5\n  b: :Boolean\n}\n"
  root: "D"
  encode:
    type: ":D"
    entries:
      - key: "b"
        value:
          type: ":Boolean"
          value: "true"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "0d"
```

---

## Test: a packed record in a document

A document writes a packed record as the Map of its fields, as any record.

### Case description

```yaml
gesBlock: case
id: packed-document
schemaApi:
  sources:
    - "packed(8) schema :Cell as {\n  level: :Number integer between 0 and 9\n  on: :Boolean\n  kind: :Tag(#a, #b, #c)\n}\npacked(16) schema :Pair as {\n  low: :Cell\n  high: :Cell\n  count: :Number integer between 0 and 3\n}\n"
  document: ":Schema(:Pair)\nlow: [level: 5, on: true, kind: #c]\nhigh: [level: 9, on: false, kind: #b]\ncount: 3\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    ok: true
```

---

## Test: a packed record in a document outside its range



### Case description

```yaml
gesBlock: case
id: packed-document-range
schemaApi:
  sources:
    - "packed(8) schema :Cell as {\n  level: :Number integer between 0 and 9\n  on: :Boolean\n  kind: :Tag(#a, #b, #c)\n}\npacked(16) schema :Pair as {\n  low: :Cell\n  high: :Cell\n  count: :Number integer between 0 and 3\n}\n"
  document: ":Schema(:Pair)\nlow: [level: 12, on: true, kind: #c]\nhigh: [level: 9, on: false, kind: #b]\ncount: 3\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    error:
      code: "document.outOfRange"
      line: 2
      column: 14
```

---

## Test: an optional field in a packed schema

A packed schema has a fixed width, so it holds no optional field.

### Case description

```yaml
gesBlock: case
id: error-packed-optional
schemaApi:
  sources:
    - "packed schema :A as {\n  a: :Number integer between 0 and 9 optional\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.packedField"
    line: 2
    column: 3
```

---

## Test: a union in a packed schema



### Case description

```yaml
gesBlock: case
id: error-packed-union
schemaApi:
  sources:
    - "packed schema :A as {\n  a: :Boolean | :Nothing\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.packedField"
    line: 2
    column: 3
```

---

## Test: a Text in a packed schema



### Case description

```yaml
gesBlock: case
id: error-packed-text
schemaApi:
  sources:
    - "packed schema :A as {\n  a: :Text max 4\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.packedField"
    line: 2
    column: 6
```

---

## Test: an integer without a range in a packed schema



### Case description

```yaml
gesBlock: case
id: error-packed-unbounded
schemaApi:
  sources:
    - "packed schema :A as {\n  a: :Number integer\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.packedField"
    line: 2
    column: 6
```

---

## Test: a number without a step in a packed schema

A fractional number without a step is a binary64 of 64 bits; a packed field is quantized.

### Case description

```yaml
gesBlock: case
id: error-packed-binary64
schemaApi:
  sources:
    - "packed schema :A as {\n  a: :Number between 0 and 1\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.packedField"
    line: 2
    column: 6
```

---

## Test: a delta range in a packed schema

A packed field changes as a whole, so a delta range has no meaning in it.

### Case description

```yaml
gesBlock: case
id: error-packed-delta
schemaApi:
  sources:
    - "packed schema :A as {\n  a: :Number integer between 0 and 99 delta within 3\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.packedField"
    line: 2
    column: 6
```

---

## Test: a key in a packed schema



### Case description

```yaml
gesBlock: case
id: error-packed-key
schemaApi:
  sources:
    - "packed schema :A as {\n  a: :Number integer between 0 and 9 key\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.packedField"
    line: 2
    column: 3
```

---

## Test: a computed field in a packed schema



### Case description

```yaml
gesBlock: case
id: error-packed-computed
schemaApi:
  sources:
    - "packed schema :A as {\n  a: :Number integer between 0 and 9\n  b: :Number computed by a + 1\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.packedField"
    line: 3
    column: 3
```

---

## Test: a bound from a parameter in a packed schema

The width of a packed schema is known without binding, so no parameter decides a bound.

### Case description

```yaml
gesBlock: case
id: error-packed-parameter
schemaApi:
  sources:
    - "parameter n: :Number\npacked schema :A as {\n  a: :Number integer between 0 and n\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.packedField"
    line: 3
    column: 6
```

---

## Test: a schema that is not packed in a packed schema



### Case description

```yaml
gesBlock: case
id: error-packed-reference
schemaApi:
  sources:
    - "schema :B as {\n  b: :Boolean\n}\npacked schema :A as {\n  a: :B\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.packedField"
    line: 5
    column: 6
```

---

## Test: a packed schema of more than 64 bits



### Case description

```yaml
gesBlock: case
id: error-packed-too-wide
schemaApi:
  sources:
    - "packed schema :A as {\n  a: :Number integer between 0 and 4294967295\n  b: :Number integer between 0 and 4294967295\n  c: :Boolean\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.packedTooWide"
    line: 1
    column: 1
```

---

## Test: a packed schema wider than its declared word



### Case description

```yaml
gesBlock: case
id: error-packed-width
schemaApi:
  sources:
    - "packed(8) schema :A as {\n  a: :Number integer between 0 and 4095\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.packedTooWide"
    line: 1
    column: 1
```

---

## Test: a packed width that is no word



### Case description

```yaml
gesBlock: case
id: error-packed-width-syntax
schemaApi:
  sources:
    - "packed(12) schema :A as {\n  a: :Boolean\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.syntax"
    line: 1
    column: 8
```

---

## Test: an abstract packed schema



### Case description

```yaml
gesBlock: case
id: error-packed-abstract
schemaApi:
  sources:
    - "abstract packed schema :A as {\n  a: :Boolean\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.syntax"
    line: 1
    column: 10
```

---


## Test: a Poly point outside the plane

A Poly lies in the plane z = 0, which it does not transmit, so a point with another `z` cannot be written.

### Case description

```yaml
gesBlock: case
id: spatial-poly-z
schemaApi:
  sources:
    - "schema :Zone as {\n  outline: :Poly max 3\n}\n"
  root: "Zone"
  encode:
    type: ":Zone"
    entries:
      - key: "outline"
        value:
          type: ":Poly"
          points:
            - type: ":Point"
              x: "0"
              y: "0"
              z: "1"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    error:
      code: "wire.typeMismatch"
      path: "outline[]"
```

---

## Test: a Poly with more points than its max

### Case description

```yaml
gesBlock: case
id: spatial-poly-too-long
schemaApi:
  sources:
    - "schema :Zone as {\n  outline: :Poly max 1\n}\n"
  root: "Zone"
  encode:
    type: ":Zone"
    entries:
      - key: "outline"
        value:
          type: ":Poly"
          points:
            - type: ":Point"
              x: "0"
              y: "0"
              z: "0"
            - type: ":Point"
              x: "1"
              y: "0"
              z: "0"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    error:
      code: "wire.tooLong"
      path: "outline"
```

---

## Test: a Box of another unit

A Box keeps the unit of its corners, which must be the field's unit, as for a Point.

### Case description

```yaml
gesBlock: case
id: spatial-box-unit
schemaApi:
  sources:
    - "schema :Zone as {\n  area: :Box(m)\n}\n"
  root: "Zone"
  encode:
    type: ":Zone"
    entries:
      - key: "area"
        value:
          type: ":Box"
          lower:
            type: ":Point"
            x: "0"
            y: "0"
            z: "0"
          upper:
            type: ":Point"
            x: "1"
            y: "1"
            z: "0"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    error:
      code: "wire.typeMismatch"
      path: "area"
```

---

## Test: a Poly without max cannot be bound for encoding

Like a List, a Poly needs a hard limit on its points.

### Case description

```yaml
gesBlock: case
id: notencodable-poly
schemaApi:
  sources:
    - "schema :Zone as {\n  outline: :Poly(m)\n}\n"
  root: "Zone"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.notEncodable"
    line: 2
    column: 12
```

---

## Test: Box and Poly are built-in type names

A declaration cannot take the name of a built-in type, so `Box` and `Poly` name no schema.

### Case description

```yaml
gesBlock: case
id: error-poly-builtin-name
schemaApi:
  sources:
    - "schema :Poly as {\n  a: :Boolean\n}\n"
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

## Test: a Poly has no z axis

### Case description

```yaml
gesBlock: case
id: error-poly-axis
schemaApi:
  sources:
    - "schema :Zone as {\n  outline: :Poly max 4 {\n    z between 0 and 1\n  }\n}\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.syntax"
    line: 3
    column: 5
```

---

## Test: a dump of a Box and a Poly

A Box and its corners have no line of their own, only their axes; a Poly has a line with its count.

### Case description

```yaml
gesBlock: case
id: dump-box-poly
schemaApi:
  sources:
    - "schema :Zone as {\n  area: :Box(m) {\n    x between 0m and 100m step 1m\n    y between 0m and 3m step 1m\n  }\n  outline: :Poly(m) max 3 {\n    x between 0m and 100m step 1m\n    y between 0m and 3m step 1m\n  }\n}\n"
  root: "Zone"
  dump: "940a0700"
```

### Expectation

```yaml
gesBlock: expect
schema:
  dump:
    text: "area.lower.x bit=0 width=7 value=20\narea.lower.y bit=7 width=2 value=1\narea.upper.x bit=9 width=7 value=5\narea.upper.y bit=16 width=2 value=3\noutline bit=18 width=2 value=count=1\noutline[].x bit=20 width=7 value=0\noutline[].y bit=27 width=2 value=0\nbytes 4\n"
    valid: true
```

---

## Test: a delta of a Box

A Box is a nested delta of its corners, and each corner one of its axes.

### Case description

```yaml
gesBlock: case
id: delta-box
schemaApi:
  sources:
    - "schema :Zone as {\n  area: :Box(m) {\n    x between 0m and 100m step 1m\n    y between 0m and 3m step 1m\n  }\n  outline: :Poly(m) max 3 {\n    x between 0m and 100m step 1m\n    y between 0m and 3m step 1m\n  }\n}\n"
  root: "Zone"
  deltaEncode:
    record: "852803"
    baseline: "850c03"
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaEncoded:
    bytes: "1905"
```

---

## Test: the description of a Box and a Poly

### Case description

```yaml
gesBlock: case
id: describe-box-poly
schemaApi:
  sources:
    - "schema :Zone as {\n  area: :Box(m) {\n    y between 0m and 3m\n  }\n  outline: :Poly max 8\n}\n"
  root: "Zone"
  bind: false
  describe: true
```

### Expectation

```yaml
gesBlock: expect
schema:
  description:
    name: "Zone"
    kind: "Schema"
    fields:
      - name: "area"
        alternatives:
          - kind: "Box"
            unit: "meter"
            axes:
              - name: "y"
                lower: "0e0"
                upper: "3e0"
      - name: "outline"
        alternatives:
          - kind: "Poly"
            max: 8
```

---

## Test: a Box and a Poly in a document

A document has no geometry literal: a Box is the Map of its corners `lower` and `upper`, and a Poly the List of its
points, each within the axes of the block.

### Case description

```yaml
gesBlock: case
id: document-box-poly
schemaApi:
  sources:
    - "schema :Zone as {\n  area: :Box(m) {\n    x between 0m and 100m\n    y between 0m and 3m\n  }\n  outline: :Poly max 3\n}\n"
  document: ":Schema(:Zone)\narea: [lower: :Point(1m, 0m), upper: :Point(20m, 3m)]\noutline: [:Point(0, 0), :Point(4, 0), :Point(0, 3)]\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    ok: true
```

---

## Test: a document Box without a corner

### Case description

```yaml
gesBlock: case
id: document-box-missing-corner
schemaApi:
  sources:
    - "schema :Zone as {\n  area: :Box(m)\n}\n"
  document: ":Schema(:Zone)\narea: [lower: :Point(1m, 0m)]\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    error:
      code: "document.missingField"
      line: 2
      column: 7
```

---

## Test: a document Poly with a point outside its range

### Case description

```yaml
gesBlock: case
id: document-poly-range
schemaApi:
  sources:
    - "schema :Zone as {\n  outline: :Poly max 3 {\n    x between 0 and 10\n    y between 0 and 10\n  }\n}\n"
  document: ":Schema(:Zone)\noutline: [:Point(0, 0), :Point(11, 0)]\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    error:
      code: "document.outOfRange"
      line: 2
      column: 25
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
    - "schema :A as {\n  a: :Text max 100000000\n}\n"
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

## Test: a decoded value outside the range of a field without a step

A finite binary64 outside the range is `wire.outOfRange`, as when encoding it; only a
pattern that is no Number value is `wire.invalidNumber`.

### Case description

```yaml
gesBlock: case
id: wire-decode-out-of-range
schemaApi:
  sources:
    - "schema :S as { v: :Number between 0 and 1 }\n"
  root: "S"
  decode: "0000000000001440"
```

### Expectation

```yaml
gesBlock: expect
schema:
  decoded:
    error:
      code: "wire.outOfRange"
      path: "v"
```

---

## Test: a delta that sets the change bit of a Disc centre without axes

A Disc whose block names only its radius never writes its centre, so the canonical delta leaves the centre's change bit
clear and reading one that sets it is `wire.invalidDelta` at the centre.

### Case description

```yaml
gesBlock: case
id: wire-delta-decode-disc-centre-bit
schemaApi:
  sources:
    - "schema :R as { d: :Disc(m) { radius between 0m and 3m step 1m } }\n"
  root: "R"
  deltaDecode:
    delta: "0f"
    baseline: "00"
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaDecoded:
    error:
      code: "wire.invalidDelta"
      path: "d.center"
```

---

## Test: the delta of a packed root fits its largest encoding

A packed root's delta is a change bit and its whole word, one bit more than the record, so eight Boolean fields write two
bytes and the largest encoding counts them.

### Case description

```yaml
gesBlock: case
id: wire-delta-packed-root-size
schemaApi:
  sources:
    - "packed schema :Switches as {\n  a: :Boolean\n  b: :Boolean\n  c: :Boolean\n  d: :Boolean\n  e: :Boolean\n  f: :Boolean\n  g: :Boolean\n  h: :Boolean\n}\n"
  root: "Switches"
  deltaEncode:
    record: "ff"
    baseline: "00"
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaEncoded:
    bytes: "ff01"
```

---

## Test: a delta value outside the range of a field without a step

A delta carries such a value raw, so it fails the same way.

### Case description

```yaml
gesBlock: case
id: wire-delta-decode-out-of-range
schemaApi:
  sources:
    - "schema :S as { v: :Number between 0 and 1 }\n"
  root: "S"
  deltaDecode:
    delta: "010000000000288000"
    baseline: "000000000000e03f"
```

### Expectation

```yaml
gesBlock: expect
schema:
  deltaDecoded:
    error:
      code: "wire.outOfRange"
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

## Test: a Poly point without bits

A Poly whose block names only a wrapping axis of one step has points without bits, so the
data cannot limit its count, as for a List element.

### Case description

```yaml
gesBlock: case
id: error-poly-point-without-bits
schemaApi:
  sources:
    - "schema :A as {\n  outline: :Poly(m) max 999999999999 {\n    x wrapping between 0m and 1m step 1m\n  }\n}\n"
  root: "A"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.notEncodable"
    line: 2
    column: 12
```

---

## Test: a Poly point that a parameter leaves without bits

Binding decides it when a parameter makes the period one step.

### Case description

```yaml
gesBlock: case
id: error-poly-point-without-bits-parameter
schemaApi:
  sources:
    - "parameter period: :Quantity(m)\nschema :A as {\n  outline: :Poly(m) max 8 {\n    x wrapping between 0m and period step 1m\n  }\n}\n"
  root: "A"
  parameters:
    period: "1m"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.notEncodable"
    line: 3
    column: 12
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

Its largest encoding has exactly 64 MiB: a 26-bit length, the bytes and six Booleans.

### Case description

```yaml
gesBlock: case
id: message-at-limit
schemaApi:
  sources:
    - "message A(t: :Text max 67108860, a: :Boolean, b: :Boolean, c: :Boolean, d: :Boolean, e: :Boolean, f: :Boolean)\nmessage B()\nmessages :S as (A, B)\n"
  root: "A"
```

### Expectation

```yaml
gesBlock: expect
schema:
  maxByteCount: 67108864
```

---

## Test: a message set beyond the limit

The index takes the encoding past 64 MiB, which is an error and not clamped.

### Case description

```yaml
gesBlock: case
id: error-set-too-large
schemaApi:
  sources:
    - "message A(t: :Text max 67108860, a: :Boolean, b: :Boolean, c: :Boolean, d: :Boolean, e: :Boolean, f: :Boolean)\nmessage B()\nmessages :S as (A, B)\n"
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
  sources: ["schema :Item as {\n  id: :Number integer between 0 and 255 key\n  hp: :Number integer between 0 and 15\n  on: :Boolean\n}\n\nschema :Crate as {\n  items: :List(:Item) max 5\n}\n\nschema :Top as {\n  crate: :Crate\n}\n"]
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

---

## Test: Flags as a mask of their members

A `:Flags` field writes one bit per member in declared order; `false` and absent keys leave a member unset, and decoding gives a Map of the set members.

### Case description

```yaml
gesBlock: case
id: flags-roundtrip
schemaApi:
  sources:
    - "schema :S as {\n  state: :Flags(#a, #b, #c)\n  extra: :Flags(#x) | :Nothing\n}\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "state"
        value:
          type: ":Map"
          entries:
            - key: "a"
              value:
                type: ":Boolean"
                value: "true"
            - key: "b"
              value:
                type: ":Boolean"
                value: "false"
            - key: "c"
              value:
                type: ":Boolean"
                value: "true"
      - key: "extra"
        value:
          type: ":Map"
          entries:
            - key: "x"
              value:
                type: ":Boolean"
                value: "true"
  decode: "15"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "15"
  decoded:
    value:
      type: ":S"
      entries:
        - key: "state"
          value:
            type: ":Map"
            entries:
              - key: "a"
                value:
                  type: ":Boolean"
                  value: "true"
              - key: "c"
                value:
                  type: ":Boolean"
                  value: "true"
        - key: "extra"
          value:
            type: ":Map"
            entries:
              - key: "x"
                value:
                  type: ":Boolean"
                  value: "true"
    byteCount: 1
```

---

## Test: Flags without set members

An empty Map clears every member; a union picks Nothing for nothing.

### Case description

```yaml
gesBlock: case
id: flags-empty
schemaApi:
  sources:
    - "schema :S as {\n  state: :Flags(#a, #b, #c)\n  extra: :Flags(#x) | :Nothing\n}\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "state"
        value:
          type: ":Map"
          entries: []
      - key: "extra"
        value:
          type: ":Nothing"
  decode: "08"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "08"
  decoded:
    value:
      type: ":S"
      entries:
        - key: "state"
          value:
            type: ":Map"
            entries: []
        - key: "extra"
          value:
            type: ":Nothing"
    byteCount: 1
```

---

## Test: a key outside the Flags set

Only the declared members can be set.

### Case description

```yaml
gesBlock: case
id: flags-unknown-member
schemaApi:
  sources:
    - "schema :S as {\n  state: :Flags(#a, #b, #c)\n  extra: :Flags(#x) | :Nothing\n}\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "state"
        value:
          type: ":Map"
          entries:
            - key: "d"
              value:
                type: ":Boolean"
                value: "true"
      - key: "extra"
        value:
          type: ":Nothing"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    error:
      code: "wire.unknownTag"
      path: "state"
```

---

## Test: a Flags value that is no Boolean

Each member's value is a Boolean.

### Case description

```yaml
gesBlock: case
id: flags-type-mismatch
schemaApi:
  sources:
    - "schema :S as {\n  state: :Flags(#a, #b, #c)\n  extra: :Flags(#x) | :Nothing\n}\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "state"
        value:
          type: ":Map"
          entries:
            - key: "a"
              value:
                type: ":Number.int64"
                value: "1"
      - key: "extra"
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
      path: "state"
```

---

## Test: the canonical form of Flags

Flags list their members in declared order, which is part of the hash.

### Case description

```yaml
gesBlock: case
id: flags-canonical
schemaApi:
  sources:
    - "schema :S as { s: :Flags(#a, #b) }\n"
  root: "S"
```

### Expectation

```yaml
gesBlock: expect
schema:
  canonical: "gess1\nschema S\nfield s Flags(#a #b)\n"
  hash: "9795c02de7d571f0"
```

---

## Test: Flags with more than 64 members

A mask holds at most 64 members.

### Case description

```yaml
gesBlock: case
id: flags-too-wide
schemaApi:
  sources:
    - "schema :S as {\n  s: :Flags(#m0, #m1, #m2, #m3, #m4, #m5, #m6, #m7, #m8, #m9, #m10, #m11, #m12, #m13, #m14, #m15, #m16, #m17, #m18, #m19, #m20, #m21, #m22, #m23, #m24, #m25, #m26, #m27, #m28, #m29, #m30, #m31, #m32, #m33, #m34, #m35, #m36, #m37, #m38, #m39, #m40, #m41, #m42, #m43, #m44, #m45, #m46, #m47, #m48, #m49, #m50, #m51, #m52, #m53, #m54, #m55, #m56, #m57, #m58, #m59, #m60, #m61, #m62, #m63, #m64)\n}\n"
  root: "S"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.tooWide"
    line: 2
    column: 6
```

---

## Test: a Flags member listed twice

Members are unique within their set.

### Case description

```yaml
gesBlock: case
id: flags-duplicate
schemaApi:
  sources:
    - "schema :S as {\n  s: :Flags(#a, #a)\n}\n"
  root: "S"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.duplicateName"
    line: 2
    column: 6
```

---

## Test: Flags without a set

A Flags type lists its members.

### Case description

```yaml
gesBlock: case
id: flags-without-set
schemaApi:
  sources:
    - "schema :S as {\n  s: :Flags\n}\n"
  root: "S"
```

### Expectation

```yaml
gesBlock: expect
schema:
  error:
    code: "schema.syntax"
    line: 2
    column: 12
```

---

## Test: Flags in a dump

A dump lists the set members and the mask.

### Case description

```yaml
gesBlock: case
id: dump-flags
schemaApi:
  sources:
    - "schema :S as { s: :Flags(#a, #b, #c) }\n"
  root: "S"
  dump: "05"
```

### Expectation

```yaml
gesBlock: expect
schema:
  dump:
    text: "s bit=0 width=3 value=#a #c mask=5\nbytes 1\n"
    valid: true
```

---

## Test: document Flags

A document writes Flags as a Map of Booleans whose keys are members.

### Case description

```yaml
gesBlock: case
id: document-flags
schemaApi:
  sources:
    - "schema :S as { s: :Flags(#a, #b) }\n"
  document: ":Schema(:S)\ns: [a: true, b: false]\n"
```

### Expectation

```yaml
gesBlock: expect
schema:
  document:
    ok: true
```

---

## Test: a document Flags key outside the set

The error is positioned at the key.

### Case description

```yaml
gesBlock: case
id: document-flags-unknown
schemaApi:
  sources:
    - "schema :S as { s: :Flags(#a, #b) }\n"
  document: ":Schema(:S)\ns: [a: true, d: true]\n"
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

## Test: a Map that Flags do not hold selects the next alternative

Flags hold a Map whose keys are all members with Boolean values; any other Map tries the following alternatives.

### Case description

```yaml
gesBlock: case
id: flags-union-schema
schemaApi:
  sources:
    - "schema :Pos as { x: :Number integer between 0 and 7 }\nschema :S as { u: :Flags(#a, #b) | :Pos }\n"
  root: "S"
  encode:
    type: ":S"
    entries:
      - key: "u"
        value:
          type: ":Map"
          entries:
            - key: "x"
              value:
                type: ":Number.int64"
                value: "3"
  decode: "07"
```

### Expectation

```yaml
gesBlock: expect
schema:
  encoded:
    bytes: "07"
  decoded:
    value:
      type: ":S"
      entries:
        - key: "u"
          value:
            type: ":Pos"
            entries:
              - key: "x"
                value:
                  type: ":Number.int64"
                  value: "3"
    byteCount: 1
```
