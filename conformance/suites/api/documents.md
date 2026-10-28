---
formatVersion: 1
suiteId: "api.documents"
title: "GES Documents"
categories: [conformance]
tags: [portable-api]
---

# GES Documents

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite verifies reading GES documents (`.gesd`) with their schema header, separators, comments, strictness and
error positions, and that printed documents read back to equal documents. Printed layout itself is not asserted.

---

## Test: a record type document reads its entries as Record fields

This case checks the following contract: A `:Schema(:DriveUnit)` header makes the root a DriveUnit Record whose fields are the entries; nested multiline Maps, comments, quantities with prefixes, percentages, tags and vectors read like `parse` literals, and printing reads back to an equal document.

### Case description

```yaml
gesBlock: "case"
id: "drive-unit"
kind: "documentApi"
level: "atomic"
documentApi:
  text: ":Schema(:DriveUnit)\n// Engine and gearbox of a light tracked drone.\nid: 'drive.tracked.basic'\nkind: #drive\ntier: 1\nname: 'Basic Track Drive'\ncost: [buildCost: 80, weight: 550kg, slotSize: 1]\nheatModel: [\n  heatCapacity: 100\n  passiveCoolingPerSecond: 2   // per second\n  shutdownHeat: 90\n  criticalHeat: 100\n]\nmaxSpeed: 10m/s\nmaxForce: 21.4kN\nefficiency: 90%\nmount: :Vector(x: 0m, y: -0.8m)\n"
  print: true
```

### Expectation

```yaml
gesBlock: "expect"
document:
  value:
    type: ":DriveUnit"
    entries:
      - key: "cost"
        value:
          type: ":Map"
          entries:
            - key: "buildCost"
              value:
                type: ":Number.int64"
                value: "80"
            - key: "slotSize"
              value:
                type: ":Number.int64"
                value: "1"
            - key: "weight"
              value:
                type: ":Quantity.int64"
                value: "550"
                unit: ":kilogram"
      - key: "efficiency"
        value:
          type: ":Percentage"
          value: "0.9"
      - key: "heatModel"
        value:
          type: ":Map"
          entries:
            - key: "criticalHeat"
              value:
                type: ":Number.int64"
                value: "100"
            - key: "heatCapacity"
              value:
                type: ":Number.int64"
                value: "100"
            - key: "passiveCoolingPerSecond"
              value:
                type: ":Number.int64"
                value: "2"
            - key: "shutdownHeat"
              value:
                type: ":Number.int64"
                value: "90"
      - key: "id"
        value:
          type: ":Text"
          value: "drive.tracked.basic"
      - key: "kind"
        value:
          type: ":Tag"
          value: "drive"
      - key: "maxForce"
        value:
          type: ":Quantity.int64"
          value: "21400"
          unit: ":newton"
      - key: "maxSpeed"
        value:
          type: ":Quantity.int64"
          value: "10"
          unit: ":meterPerSecond"
      - key: "mount"
        value:
          type: ":Vector"
          x: "0"
          y: "-0.8"
          z: "0"
          unit: ":meter"
      - key: "name"
        value:
          type: ":Text"
          value: "Basic Track Drive"
      - key: "tier"
        value:
          type: ":Number.int64"
          value: "1"
  schema:
    kind: "type"
    name: "DriveUnit"
```

---

## Test: empty text is the empty Map

This case checks the following contract: Text without entries and without a header is an empty Map root without a schema; printing it reads back equal.

### Case description

```yaml
gesBlock: "case"
id: "empty-text"
kind: "documentApi"
level: "atomic"
documentApi:
  text: ""
  print: true
```

### Expectation

```yaml
gesBlock: "expect"
document:
  value:
    type: ":Map"
    entries: []
```

---

## Test: comments and blank lines alone are the empty Map

This case checks the following contract: Comments, blank lines, spaces and tabs are trivia, so a document consisting only of them is an empty Map root.

### Case description

```yaml
gesBlock: "case"
id: "only-trivia"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "// heading\n\n  \t// indented\n\n"
  print: true
```

### Expectation

```yaml
gesBlock: "expect"
document:
  value:
    type: ":Map"
    entries: []
```

---

## Test: a header without entries is an empty Record

This case checks the following contract: A record type header without entries produces an empty Record of that type.

### Case description

```yaml
gesBlock: "case"
id: "empty-record-root"
kind: "documentApi"
level: "atomic"
documentApi:
  text: ":Schema(:Config)\n"
  print: true
```

### Expectation

```yaml
gesBlock: "expect"
document:
  value:
    type: ":Config"
    entries: []
  schema:
    kind: "type"
    name: "Config"
```

---

## Test: a file schema keeps a Map root

This case checks the following contract: A quoted Text header argument references a schema file; the root stays a Map carrying that reference, and comments may precede and follow the header.

### Case description

```yaml
gesBlock: "case"
id: "file-schema"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "// units\n\n:Schema('units/DriveUnit.gess')  // schema file\nid: 'basic'\n"
  print: true
```

### Expectation

```yaml
gesBlock: "expect"
document:
  value:
    type: ":Map"
    entries:
      - key: "id"
        value:
          type: ":Text"
          value: "basic"
  schema:
    kind: "file"
    name: "units/DriveUnit.gess"
```

---

## Test: the header follows the argument separator rules

This case checks the following contract: The header admits whitespace before its parenthesis and a trailing comma like other argument lists.

### Case description

```yaml
gesBlock: "case"
id: "header-trailing-comma"
kind: "documentApi"
level: "atomic"
documentApi:
  text: ":Schema (:Unit,)\nid: 1"
  print: true
```

### Expectation

```yaml
gesBlock: "expect"
document:
  value:
    type: ":Unit"
    entries:
      - key: "id"
        value:
          type: ":Number.int64"
          value: "1"
  schema:
    kind: "type"
    name: "Unit"
```

---

## Test: key-only entries are true

This case checks the following contract: An entry whose colon is followed only by trivia up to the end of the line, a comma, a closing bracket or the end of the text has the value true, in the root and in Maps.

### Case description

```yaml
gesBlock: "case"
id: "key-only-entries"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "flag:\nother: // note\nnested: [a:, b: 1, c:]\nlast:"
  print: true
```

### Expectation

```yaml
gesBlock: "expect"
document:
  value:
    type: ":Map"
    entries:
      - key: "flag"
        value:
          type: ":Boolean"
          value: "true"
      - key: "last"
        value:
          type: ":Boolean"
          value: "true"
      - key: "nested"
        value:
          type: ":Map"
          entries:
            - key: "a"
              value:
                type: ":Boolean"
                value: "true"
            - key: "b"
              value:
                type: ":Number.int64"
                value: "1"
            - key: "c"
              value:
                type: ":Boolean"
                value: "true"
      - key: "other"
        value:
          type: ":Boolean"
          value: "true"
```

---

## Test: quoted keys hold arbitrary Text

This case checks the following contract: Keys are bare lower names or quoted Text with doubled delimiters, including empty and non-ASCII keys, and print back with quotes where needed.

### Case description

```yaml
gesBlock: "case"
id: "quoted-keys"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "'two words': 1\n\"a\"\"b\": 2\n'': 3\n'Grüße': 4\nplain: [ 'x y': 5 ]"
  print: true
```

### Expectation

```yaml
gesBlock: "expect"
document:
  value:
    type: ":Map"
    entries:
      - key: ""
        value:
          type: ":Number.int64"
          value: "3"
      - key: "Grüße"
        value:
          type: ":Number.int64"
          value: "4"
      - key: "a\"b"
        value:
          type: ":Number.int64"
          value: "2"
      - key: "plain"
        value:
          type: ":Map"
          entries:
            - key: "x y"
              value:
                type: ":Number.int64"
                value: "5"
      - key: "two words"
        value:
          type: ":Number.int64"
          value: "1"
```

---

## Test: commas and line breaks separate entries

This case checks the following contract: Entries end at a comma or a line break; several entries on one line need commas, and one trailing comma is allowed after the last entry of the root or a container, also at a line end.

### Case description

```yaml
gesBlock: "case"
id: "separators"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "a: 1, b: 2,\nc: [1, 2,],\nd: [x: 1,\n],\ne: [\n  1,\n  2\n  3,\n]\nf: 4,\n"
  print: true
```

### Expectation

```yaml
gesBlock: "expect"
document:
  value:
    type: ":Map"
    entries:
      - key: "a"
        value:
          type: ":Number.int64"
          value: "1"
      - key: "b"
        value:
          type: ":Number.int64"
          value: "2"
      - key: "c"
        value:
          type: ":List"
          items:
            - type: ":Number.int64"
              value: "1"
            - type: ":Number.int64"
              value: "2"
      - key: "d"
        value:
          type: ":Map"
          entries:
            - key: "x"
              value:
                type: ":Number.int64"
                value: "1"
      - key: "e"
        value:
          type: ":List"
          items:
            - type: ":Number.int64"
              value: "1"
            - type: ":Number.int64"
              value: "2"
            - type: ":Number.int64"
              value: "3"
      - key: "f"
        value:
          type: ":Number.int64"
          value: "4"
```

---

## Test: comments are trivia wherever whitespace is allowed

This case checks the following contract: Comments may stand on their own lines, after the header, after values and commas, inside containers after an opening bracket and before a closing bracket, and directly after an unquoted value; `//` inside quoted Text is Text.

### Case description

```yaml
gesBlock: "case"
id: "comments-everywhere"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "// before\n:Schema(:Notes) // after header\n// between\nlist: [ // after bracket\n  1, // after comma\n  2 // after value\n  // before bracket\n]\nratio: 90%// directly after\nurl: 'http://example.org' // quoted slashes\nvector: :Vector( // inside arguments\n  x: 1m // component\n)\n"
  print: true
```

### Expectation

```yaml
gesBlock: "expect"
document:
  value:
    type: ":Notes"
    entries:
      - key: "list"
        value:
          type: ":List"
          items:
            - type: ":Number.int64"
              value: "1"
            - type: ":Number.int64"
              value: "2"
      - key: "ratio"
        value:
          type: ":Percentage"
          value: "0.9"
      - key: "url"
        value:
          type: ":Text"
          value: "http://example.org"
      - key: "vector"
        value:
          type: ":Vector"
          x: "1"
          y: "0"
          z: "0"
          unit: ":meter"
  schema:
    kind: "type"
    name: "Notes"
```

---

## Test: prefixed unit spellings read as base quantities

This case checks the following contract: Prefixed spellings scale exactly to the base unit, so `1kW`, `250ms`, `1.5MW`, `2.5km`, `0.5mm`, `21.4kN` and `1kWh` read as base quantities with Int64 or Binary64 storage, and print back to equal values.

### Case description

```yaml
gesBlock: "case"
id: "prefixed-units"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "power: 1kW\ndelay: 250ms\nplant: 1.5MW\nroute: 2.5km\ngap: 0.5mm\nforce: 21.4kN\nenergy: 1kWh\n"
  print: true
```

### Expectation

```yaml
gesBlock: "expect"
document:
  value:
    type: ":Map"
    entries:
      - key: "delay"
        value:
          type: ":Quantity.binary64"
          value: "0.25"
          unit: ":second"
      - key: "energy"
        value:
          type: ":Quantity.int64"
          value: "3600000"
          unit: ":joule"
      - key: "force"
        value:
          type: ":Quantity.int64"
          value: "21400"
          unit: ":newton"
      - key: "gap"
        value:
          type: ":Quantity.binary64"
          value: "0.0005"
          unit: ":meter"
      - key: "plant"
        value:
          type: ":Quantity.int64"
          value: "1500000"
          unit: ":watt"
      - key: "power"
        value:
          type: ":Quantity.int64"
          value: "1000"
          unit: ":watt"
      - key: "route"
        value:
          type: ":Quantity.int64"
          value: "2500"
          unit: ":meter"
```

---

## Test: printed quantities read back bit-identically

This case checks the following contract: Quantities without prefixes, zero, infinities, large Int64 and tiny Binary64 values and values that a prefix cannot shorten all read back to the same storage, bits and unit after printing with prefixed units.

### Case description

```yaml
gesBlock: "case"
id: "quantities-print-exactly"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "mass: 5kg\nspeed: 10m/s\nturn: 90°/s\nzero: 0W\nfar: Infinitym\nexact: 9007199254740993m\ntiny: 1e-300m\ntenth: 0.1s\noffset: -0.8m\nforce: 123456.789N\npi: 3.14159W\nmixed: [1000m, 0.001s, 1500000J]\n"
  print: true
```

### Expectation

```yaml
gesBlock: "expect"
document:
  value:
    type: ":Map"
    entries:
      - key: "exact"
        value:
          type: ":Quantity.int64"
          value: "9007199254740993"
          unit: ":meter"
      - key: "far"
        value:
          type: ":Quantity.binary64"
          value: "Infinity"
          unit: ":meter"
      - key: "force"
        value:
          type: ":Quantity.binary64"
          value: "123456.789"
          unit: ":newton"
      - key: "mass"
        value:
          type: ":Quantity.int64"
          value: "5"
          unit: ":kilogram"
      - key: "mixed"
        value:
          type: ":List"
          items:
            - type: ":Quantity.int64"
              value: "1000"
              unit: ":meter"
            - type: ":Quantity.binary64"
              value: "0.001"
              unit: ":second"
            - type: ":Quantity.int64"
              value: "1500000"
              unit: ":joule"
      - key: "offset"
        value:
          type: ":Quantity.binary64"
          value: "-0.8"
          unit: ":meter"
      - key: "pi"
        value:
          type: ":Quantity.binary64"
          value: "3.14159"
          unit: ":watt"
      - key: "speed"
        value:
          type: ":Quantity.int64"
          value: "10"
          unit: ":meterPerSecond"
      - key: "tenth"
        value:
          type: ":Quantity.binary64"
          value: "0.1"
          unit: ":second"
      - key: "tiny"
        value:
          type: ":Quantity.binary64"
          value: "1e-300"
          unit: ":meter"
      - key: "turn"
        value:
          type: ":Quantity.int64"
          value: "90"
          unit: ":degreePerSecond"
      - key: "zero"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":watt"
```

---

## Test: printing can disable prefixed units

This case checks the following contract: With prefixed units disabled, printed quantities use their base symbols and still read back bit-identically.

### Case description

```yaml
gesBlock: "case"
id: "quantities-print-base-units"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "mass: 5kg\nspeed: 10m/s\nturn: 90°/s\nzero: 0W\nfar: Infinitym\nexact: 9007199254740993m\ntiny: 1e-300m\ntenth: 0.1s\noffset: -0.8m\nforce: 123456.789N\npi: 3.14159W\nmixed: [1000m, 0.001s, 1500000J]\n"
  print:
    prefixedUnits: false
```

### Expectation

```yaml
gesBlock: "expect"
document:
  value:
    type: ":Map"
    entries:
      - key: "exact"
        value:
          type: ":Quantity.int64"
          value: "9007199254740993"
          unit: ":meter"
      - key: "far"
        value:
          type: ":Quantity.binary64"
          value: "Infinity"
          unit: ":meter"
      - key: "force"
        value:
          type: ":Quantity.binary64"
          value: "123456.789"
          unit: ":newton"
      - key: "mass"
        value:
          type: ":Quantity.int64"
          value: "5"
          unit: ":kilogram"
      - key: "mixed"
        value:
          type: ":List"
          items:
            - type: ":Quantity.int64"
              value: "1000"
              unit: ":meter"
            - type: ":Quantity.binary64"
              value: "0.001"
              unit: ":second"
            - type: ":Quantity.int64"
              value: "1500000"
              unit: ":joule"
      - key: "offset"
        value:
          type: ":Quantity.binary64"
          value: "-0.8"
          unit: ":meter"
      - key: "pi"
        value:
          type: ":Quantity.binary64"
          value: "3.14159"
          unit: ":watt"
      - key: "speed"
        value:
          type: ":Quantity.int64"
          value: "10"
          unit: ":meterPerSecond"
      - key: "tenth"
        value:
          type: ":Quantity.binary64"
          value: "0.1"
          unit: ":second"
      - key: "tiny"
        value:
          type: ":Quantity.binary64"
          value: "1e-300"
          unit: ":meter"
      - key: "turn"
        value:
          type: ":Quantity.int64"
          value: "90"
          unit: ":degreePerSecond"
      - key: "zero"
        value:
          type: ":Quantity.int64"
          value: "0"
          unit: ":watt"
```

---

## Test: scalar literals keep their kinds

This case checks the following contract: Numbers, Percentages, Tags, Booleans, nothing and quoted Text with doubled delimiters, line breaks and comment markers read like `parse` literals and print back to equal values.

### Case description

```yaml
gesBlock: "case"
id: "scalars"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "count: 3\nratio: 0.5\nshare: 12.5%\nmode: #standard\nenabled: true\ndisabled: false\nmissing: nothing\nsaid: 'say ''hi'''\nquote: \"a \"\"b\"\"\"\nlines: 'first\nsecond'\nslashes: '// not a comment'\n"
  print: true
```

### Expectation

```yaml
gesBlock: "expect"
document:
  value:
    type: ":Map"
    entries:
      - key: "count"
        value:
          type: ":Number.int64"
          value: "3"
      - key: "disabled"
        value:
          type: ":Boolean"
          value: "false"
      - key: "enabled"
        value:
          type: ":Boolean"
          value: "true"
      - key: "lines"
        value:
          type: ":Text"
          value: "first\nsecond"
      - key: "missing"
        value:
          type: ":Nothing"
      - key: "mode"
        value:
          type: ":Tag"
          value: "standard"
      - key: "quote"
        value:
          type: ":Text"
          value: "a \"b\""
      - key: "ratio"
        value:
          type: ":Number.binary64"
          value: "0.5"
      - key: "said"
        value:
          type: ":Text"
          value: "say 'hi'"
      - key: "share"
        value:
          type: ":Percentage"
          value: "0.125"
      - key: "slashes"
        value:
          type: ":Text"
          value: "// not a comment"
```

---

## Test: Vector, Point and Dice data forms may span lines

This case checks the following contract: Vector and Point components and Dice rolls follow the container separator rules, including line breaks and trailing commas.

### Case description

```yaml
gesBlock: "case"
id: "spatial-and-dice"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "mount: :Vector(x: 0m, y: -0.8m)\nanchor: :Point(\n  1,\n  2,\n)\nrolls: :Dice[\n  6\n  1, 3\n]\n"
  print: true
```

### Expectation

```yaml
gesBlock: "expect"
document:
  value:
    type: ":Map"
    entries:
      - key: "anchor"
        value:
          type: ":Point"
          x: "1"
          y: "2"
          z: "0"
      - key: "mount"
        value:
          type: ":Vector"
          x: "0"
          y: "-0.8"
          z: "0"
          unit: ":meter"
      - key: "rolls"
        value:
          type: ":Dice"
          rolls:
            - 6
            - 3
            - 1
```

---

## Test: explicit data forms construct values without a Program

This case checks the following contract: Explicit Record, Message, Range, Text and List forms construct data as in `parse`; `:Record("Hit", [...])` writes Record data without a constructor.

### Case description

```yaml
gesBlock: "case"
id: "explicit-forms"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "hit: :Record(\"Hit\", [damage: 5])\nsignal: :Message(Done(value: 1) with #ready)\nspan: :Range(from: 1, to: 9, step: 2)\nlabel: :Text(\"123\")\nsteps: :List(:Range(1, 3))\n"
  print: true
```

### Expectation

```yaml
gesBlock: "expect"
document:
  value:
    type: ":Map"
    entries:
      - key: "hit"
        value:
          type: ":Hit"
          entries:
            - key: "damage"
              value:
                type: ":Number.int64"
                value: "5"
      - key: "label"
        value:
          type: ":Text"
          value: "123"
      - key: "signal"
        value:
          type: ":Message"
          message:
            name: "Done"
            tags:
              - "ready"
            args:
              - name: "value"
                value:
                  type: ":Number.int64"
                  value: "1"
      - key: "span"
        value:
          type: ":Range.int64"
          from: "1"
          to: "9"
          step: "2"
      - key: "steps"
        value:
          type: ":List"
          items:
            - type: ":Number.int64"
              value: "1"
            - type: ":Number.int64"
              value: "2"
            - type: ":Number.int64"
              value: "3"
```

---

## Test: a byte order mark and CRLF line breaks are trivia

This case checks the following contract: One leading byte order mark is ignored and CRLF and CR end lines like LF.

### Case description

```yaml
gesBlock: "case"
id: "byte-order-mark-and-crlf"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "﻿a: 1\r\nb: [\r\n  1\r  2\r\n]\r\n"
  print: true
```

### Expectation

```yaml
gesBlock: "expect"
document:
  value:
    type: ":Map"
    entries:
      - key: "a"
        value:
          type: ":Number.int64"
          value: "1"
      - key: "b"
        value:
          type: ":List"
          items:
            - type: ":Number.int64"
              value: "1"
            - type: ":Number.int64"
              value: "2"
```

---

## Test: block layout reads back

This case checks the following contract: Containers that exceed a narrow line width print in block form with the requested indentation and read back to an equal document.

### Case description

```yaml
gesBlock: "case"
id: "block-layout"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "name: 'A long name that does not fit'\nparts: [[id: 1, tags: [#a, #b]], [id: 2, note: 'line one\nline two']]\nrecord: :Record('Part', [id: 3, size: [w: 1m, h: 2m]])\n"
  print:
    lineWidth: 20
    indentation: 4
```

### Expectation

```yaml
gesBlock: "expect"
document:
  value:
    type: ":Map"
    entries:
      - key: "name"
        value:
          type: ":Text"
          value: "A long name that does not fit"
      - key: "parts"
        value:
          type: ":List"
          items:
            - type: ":Map"
              entries:
                - key: "id"
                  value:
                    type: ":Number.int64"
                    value: "1"
                - key: "tags"
                  value:
                    type: ":List"
                    items:
                      - type: ":Tag"
                        value: "a"
                      - type: ":Tag"
                        value: "b"
            - type: ":Map"
              entries:
                - key: "id"
                  value:
                    type: ":Number.int64"
                    value: "2"
                - key: "note"
                  value:
                    type: ":Text"
                    value: "line one\nline two"
      - key: "record"
        value:
          type: ":Part"
          entries:
            - key: "id"
              value:
                type: ":Number.int64"
                value: "3"
            - key: "size"
              value:
                type: ":Map"
                entries:
                  - key: "h"
                    value:
                      type: ":Quantity.int64"
                      value: "2"
                      unit: ":meter"
                  - key: "w"
                    value:
                      type: ":Quantity.int64"
                      value: "1"
                      unit: ":meter"
```

---

## Test: non-ASCII Text and keys read in Unicode scalars

This case checks the following contract: Quoted Text and keys may contain non-BMP scalars; they print back unchanged.

### Case description

```yaml
gesBlock: "case"
id: "positional-message-and-unicode"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "'😀': '😀😀'\nemoji: ['ü', '😀']\n"
  print: true
```

### Expectation

```yaml
gesBlock: "expect"
document:
  value:
    type: ":Map"
    entries:
      - key: "emoji"
        value:
          type: ":List"
          items:
            - type: ":Text"
              value: "ü"
            - type: ":Text"
              value: "😀"
      - key: "😀"
        value:
          type: ":Text"
          value: "😀😀"
```

---

## Test: a Map value builds a document

This case checks the following contract: A host-built Map root with keys that need quotes and Text with quotes and line breaks prints and reads back to an equal document without schema.

### Case description

```yaml
gesBlock: "case"
id: "built-map"
kind: "documentApi"
level: "atomic"
documentApi:
  value:
    type: ":Map"
    entries:
      - key: ""
        value:
          type: ":Boolean"
          value: "true"
      - key: "list"
        value:
          type: ":List"
          items:
            - type: ":Quantity.binary64"
              value: "0.25"
              unit: ":second"
            - type: ":Quantity.int64"
              value: "1000"
              unit: ":watt"
      - key: "plain"
        value:
          type: ":Number.int64"
          value: "1"
      - key: "two words"
        value:
          type: ":Text"
          value: "a \"b\"\nc"
  print: true
```

### Expectation

```yaml
gesBlock: "expect"
document:
  value:
    type: ":Map"
    entries:
      - key: ""
        value:
          type: ":Boolean"
          value: "true"
      - key: "list"
        value:
          type: ":List"
          items:
            - type: ":Quantity.binary64"
              value: "0.25"
              unit: ":second"
            - type: ":Quantity.int64"
              value: "1000"
              unit: ":watt"
      - key: "plain"
        value:
          type: ":Number.int64"
          value: "1"
      - key: "two words"
        value:
          type: ":Text"
          value: "a \"b\"\nc"
```

---

## Test: a Map value carries an attached file schema

This case checks the following contract: A file schema attached to a Map root prints as the header and reads back.

### Case description

```yaml
gesBlock: "case"
id: "built-map-file-schema"
kind: "documentApi"
level: "atomic"
documentApi:
  value:
    type: ":Map"
    entries:
      - key: "id"
        value:
          type: ":Number.int64"
          value: "1"
  print: true
  schema:
    kind: "file"
    name: "schemas/unit.gess"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  value:
    type: ":Map"
    entries:
      - key: "id"
        value:
          type: ":Number.int64"
          value: "1"
  schema:
    kind: "file"
    name: "schemas/unit.gess"
```

---

## Test: a Record value derives its record type schema

This case checks the following contract: A host-built Record root takes the record type schema of its type name, prints the header and reads back as the same Record.

### Case description

```yaml
gesBlock: "case"
id: "built-record"
kind: "documentApi"
level: "atomic"
documentApi:
  value:
    type: ":DriveUnit"
    entries:
      - key: "maxForce"
        value:
          type: ":Quantity.int64"
          value: "21400"
          unit: ":newton"
      - key: "tier"
        value:
          type: ":Number.int64"
          value: "1"
  print: true
```

### Expectation

```yaml
gesBlock: "expect"
document:
  value:
    type: ":DriveUnit"
    entries:
      - key: "maxForce"
        value:
          type: ":Quantity.int64"
          value: "21400"
          unit: ":newton"
      - key: "tier"
        value:
          type: ":Number.int64"
          value: "1"
  schema:
    kind: "type"
    name: "DriveUnit"
```

---

## Test: a Record value accepts its own record type schema

This case checks the following contract: Attaching the record type schema of the Record type name is equivalent to deriving it.

### Case description

```yaml
gesBlock: "case"
id: "built-record-matching-schema"
kind: "documentApi"
level: "atomic"
documentApi:
  value:
    type: ":Unit"
    entries:
      - key: "id"
        value:
          type: ":Number.int64"
          value: "1"
  print: true
  schema:
    kind: "type"
    name: "Unit"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  value:
    type: ":Unit"
    entries:
      - key: "id"
        value:
          type: ":Number.int64"
          value: "1"
  schema:
    kind: "type"
    name: "Unit"
```

---

## Test: Record root keys must be field names

This case checks the following contract: A Record root whose field key is not a lower name cannot be read back as a record type document and is rejected.

### Case description

```yaml
gesBlock: "case"
id: "built-rejects-record-root-key"
kind: "documentApi"
level: "atomic"
documentApi:
  value:
    type: ":Unit"
    entries:
      - key: "two words"
        value:
          type: ":Number.int64"
          value: "1"
  print: true
```

### Expectation

```yaml
gesBlock: "expect"
document:
  rejected: true
```

---

## Test: a List value is not a document

This case checks the following contract: Only Map and Record values can be document roots.

### Case description

```yaml
gesBlock: "case"
id: "built-rejects-list-root"
kind: "documentApi"
level: "atomic"
documentApi:
  value:
    type: ":List"
    items:
      - type: ":Number.int64"
        value: "1"
  print: true
```

### Expectation

```yaml
gesBlock: "expect"
document:
  rejected: true
```

---

## Test: a record type schema needs a Record root

This case checks the following contract: A record type schema cannot be attached to a Map root.

### Case description

```yaml
gesBlock: "case"
id: "built-rejects-type-schema-on-map"
kind: "documentApi"
level: "atomic"
documentApi:
  value:
    type: ":Map"
    entries:
      - key: "id"
        value:
          type: ":Number.int64"
          value: "1"
  print: true
  schema:
    kind: "type"
    name: "Unit"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  rejected: true
```

---

## Test: a Record carries only its own schema

This case checks the following contract: A Record root rejects a file schema or the record type schema of another type.

### Case description

```yaml
gesBlock: "case"
id: "built-rejects-other-schema-on-record"
kind: "documentApi"
level: "atomic"
documentApi:
  value:
    type: ":Unit"
    entries:
      - key: "id"
        value:
          type: ":Number.int64"
          value: "1"
  print: true
  schema:
    kind: "type"
    name: "Other"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  rejected: true
```

---

## Test: unquoted words are an error

This case checks the following contract: Text must be quoted; an unquoted word is `document.unquotedText` at the start of its token.

### Case description

```yaml
gesBlock: "case"
id: "unquoted-text"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "name: Basic Track Drive"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "unquotedText"
    line: 1
    column: 7
```

---

## Test: unquoted words inside containers are an error

This case checks the following contract: Unquoted words are rejected in nested containers too, at the start of the token.

### Case description

```yaml
gesBlock: "case"
id: "unquoted-text-nested"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "list: [1,\n  two]"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "unquotedText"
    line: 2
    column: 3
```

---

## Test: duplicate root keys are an error

This case checks the following contract: A repeated root key is `document.duplicateKey` at the repeated key.

### Case description

```yaml
gesBlock: "case"
id: "duplicate-root-key"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "a: 1\nb: 2\na: 3"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "duplicateKey"
    line: 3
    column: 1
```

---

## Test: keys compare by decoded Text

This case checks the following contract: A bare key and an equal quoted key are duplicates.

### Case description

```yaml
gesBlock: "case"
id: "duplicate-quoted-key"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "a: 1\n'a': 2"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "duplicateKey"
    line: 2
    column: 1
```

---

## Test: duplicate Map keys are an error

This case checks the following contract: Duplicate keys inside a Map are reported at the repeated key instead of keeping the last value.

### Case description

```yaml
gesBlock: "case"
id: "duplicate-nested-key"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "a: [b: 1, c: 2, b: 3]"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "duplicateKey"
    line: 1
    column: 17
```

---

## Test: duplicate Record fields are an error

This case checks the following contract: The field Map of an explicit Record form rejects duplicate keys.

### Case description

```yaml
gesBlock: "case"
id: "duplicate-record-field"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "hit: :Record('Hit', [damage: 1, damage: 2])"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "duplicateKey"
    line: 1
    column: 33
```

---

## Test: duplicate argument labels are an error

This case checks the following contract: Repeated labels in an explicit form are reported at the repeated label.

### Case description

```yaml
gesBlock: "case"
id: "duplicate-argument-label"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "signal: :Message(Done(v: 1, v: 2))"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "duplicateKey"
    line: 1
    column: 29
```

---

## Test: an unknown unit suffix is an error

This case checks the following contract: A number followed by a suffix that is no unit spelling is `document.unknownUnit` at the start of the token.

### Case description

```yaml
gesBlock: "case"
id: "unknown-unit"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "speed: 10mph"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "unknownUnit"
    line: 1
    column: 8
```

---

## Test: an unknown unit after an exponent is an error

This case checks the following contract: The numeric prefix includes a complete exponent before the unknown suffix.

### Case description

```yaml
gesBlock: "case"
id: "unknown-unit-exponent"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "value: 1e5x"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "unknownUnit"
    line: 1
    column: 8
```

---

## Test: malformed numbers are invalid literals

This case checks the following contract: A number token that is neither a literal nor a number with a unit suffix is `document.invalidLiteral`.

### Case description

```yaml
gesBlock: "case"
id: "invalid-number"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "value: 1.2.3"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "invalidLiteral"
    line: 1
    column: 8
```

---

## Test: invalid Tag names are invalid literals

This case checks the following contract: A `#` token that is not a valid Tag name is `document.invalidLiteral`.

### Case description

```yaml
gesBlock: "case"
id: "invalid-tag"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "mode: #Bad"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "invalidLiteral"
    line: 1
    column: 7
```

---

## Test: a key needs a colon

This case checks the following contract: A key followed by anything but `:` is `document.missingColon` at the first non-trivia scalar after the key.

### Case description

```yaml
gesBlock: "case"
id: "missing-colon"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "a [1]"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "missingColon"
    line: 1
    column: 3
```

---

## Test: a key at the end of the text needs a colon

This case checks the following contract: The end of the text after a key reports `document.missingColon` at the end position.

### Case description

```yaml
gesBlock: "case"
id: "missing-colon-at-end"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "a"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "missingColon"
    line: 1
    column: 2
```

---

## Test: Map keys need a colon

This case checks the following contract: A later Map entry without colon reports `document.missingColon`.

### Case description

```yaml
gesBlock: "case"
id: "missing-colon-nested"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "a: [b: 1, c 2]"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "missingColon"
    line: 1
    column: 13
```

---

## Test: root entries start with a key

This case checks the following contract: An entry that does not start with a lower name or quoted Text is `document.invalidKey` at the entry start.

### Case description

```yaml
gesBlock: "case"
id: "invalid-root-key"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "1: 2"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "invalidKey"
    line: 1
    column: 1
```

---

## Test: keys are lower names

This case checks the following contract: An upper-case bare word is not a key.

### Case description

```yaml
gesBlock: "case"
id: "invalid-uppercase-key"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "a: 1\nName: 2"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "invalidKey"
    line: 2
    column: 1
```

---

## Test: later Map entries need keys

This case checks the following contract: Inside a Map, an entry without a key is `document.invalidKey` at its start.

### Case description

```yaml
gesBlock: "case"
id: "invalid-map-entry"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "a: [b: 1, 2]"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "invalidKey"
    line: 1
    column: 11
```

---

## Test: Record root keys must be field names

This case checks the following contract: With a record type header, a quoted key that is not a lower name is `document.invalidKey`.

### Case description

```yaml
gesBlock: "case"
id: "record-root-field-name"
kind: "documentApi"
level: "atomic"
documentApi:
  text: ":Schema(:Unit)\n'two words': 1"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "invalidKey"
    line: 2
    column: 1
```

---

## Test: values start on the line of their key

This case checks the following contract: A key followed by a line break is a key-only entry, so a value on the next line starts an invalid entry.

### Case description

```yaml
gesBlock: "case"
id: "value-on-next-line"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "a:\n  1"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "invalidKey"
    line: 2
    column: 3
```

---

## Test: an unclosed List is an error

This case checks the following contract: The end of the text inside a container is `document.unclosedBracket` at the opening bracket.

### Case description

```yaml
gesBlock: "case"
id: "unclosed-list"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "a: [1, 2"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "unclosedBracket"
    line: 1
    column: 4
```

---

## Test: an unclosed multiline Map is an error

This case checks the following contract: An unclosed bracket is reported at its opening line and column.

### Case description

```yaml
gesBlock: "case"
id: "unclosed-multiline-map"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "a: 1\nb: [\n  c: 1\n"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "unclosedBracket"
    line: 2
    column: 4
```

---

## Test: an unclosed argument list is an error

This case checks the following contract: The end of the text inside an explicit form reports its opening parenthesis.

### Case description

```yaml
gesBlock: "case"
id: "unclosed-arguments"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "mount: :Vector(1m, 2m"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "unclosedBracket"
    line: 1
    column: 15
```

---

## Test: unclosed quoted Text is an error

This case checks the following contract: The end of the text inside quoted Text is `document.unclosedText` at the opening delimiter.

### Case description

```yaml
gesBlock: "case"
id: "unclosed-text"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "a: 'abc"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "unclosedText"
    line: 1
    column: 4
```

---

## Test: an unclosed quoted key is an error

This case checks the following contract: An unclosed quoted key reports its opening delimiter.

### Case description

```yaml
gesBlock: "case"
id: "unclosed-key"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "a: 1\n'b: 2"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "unclosedText"
    line: 2
    column: 1
```

---

## Test: entries on one line need commas

This case checks the following contract: A second entry on the same line without comma is `document.unexpectedToken`.

### Case description

```yaml
gesBlock: "case"
id: "entries-on-one-line"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "a: 1 b: 2"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "unexpectedToken"
    line: 1
    column: 6
```

---

## Test: an empty entry between commas is an error

This case checks the following contract: Two commas in a row are `document.unexpectedToken` at the second comma.

### Case description

```yaml
gesBlock: "case"
id: "empty-list-item"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "a: [1,,2]"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "unexpectedToken"
    line: 1
    column: 7
```

---

## Test: a closing bracket at the root is an error

This case checks the following contract: A closing bracket after a root value is `document.unexpectedToken`.

### Case description

```yaml
gesBlock: "case"
id: "closing-bracket-at-root"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "a: 1]"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "unexpectedToken"
    line: 1
    column: 5
```

---

## Test: a line cannot start with a separating comma

This case checks the following contract: A comma after a line break that already separated two entries is `document.unexpectedToken`.

### Case description

```yaml
gesBlock: "case"
id: "leading-comma"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "a: 1\n, b: 2"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "unexpectedToken"
    line: 2
    column: 1
```

---

## Test: Program Record constructors are unknown types

This case checks the following contract: Documents have no Program, so `:Hit(5)` is `document.unknownType` at its colon.

### Case description

```yaml
gesBlock: "case"
id: "unknown-record-constructor"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "hit: :Hit(5)"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "unknownType"
    line: 1
    column: 6
```

---

## Test: a schema header is not a value

This case checks the following contract: Inside a value, `:Schema(...)` is an unknown type.

### Case description

```yaml
gesBlock: "case"
id: "schema-as-value"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "schema: :Schema(:Unit)"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "unknownType"
    line: 1
    column: 9
```

---

## Test: a Vector with mixed units is an invalid literal

This case checks the following contract: An explicit form whose valid arguments do not form a valid value reports `document.invalidLiteral` at its colon.

### Case description

```yaml
gesBlock: "case"
id: "invalid-vector"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "mount: :Vector(1m, 2)"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "invalidLiteral"
    line: 1
    column: 8
```

---

## Test: non-positive Dice rolls are an invalid literal

This case checks the following contract: Invalid Dice rolls report the Dice form.

### Case description

```yaml
gesBlock: "case"
id: "invalid-dice"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "rolls: :Dice[0]"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "invalidLiteral"
    line: 1
    column: 8
```

---

## Test: an explicit form with wrong arguments is an invalid literal

This case checks the following contract: Argument labels that do not match a built-in constructor report the form.

### Case description

```yaml
gesBlock: "case"
id: "invalid-constructor-arguments"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "flag: :Boolean(x: 1)"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "invalidLiteral"
    line: 1
    column: 7
```

---

## Test: an invalid nested argument reports its own error

This case checks the following contract: An invalid argument inside an explicit form is reported before the form itself.

### Case description

```yaml
gesBlock: "case"
id: "nested-error-first"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "mount: :Vector(x: 1m, y: 2q)"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "unknownUnit"
    line: 1
    column: 26
```

---

## Test: a failed construction is an error

This case checks the following contract: An explicit form whose recognized arguments construct nothing is `document.invalidLiteral` at its colon instead of the value nothing.

### Case description

```yaml
gesBlock: "case"
id: "invalid-construction-range"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "span: :Range(1)"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "invalidLiteral"
    line: 1
    column: 7
```

---

## Test: a failed cast form is an error

This case checks the following contract: A Number form whose Text does not convert is rejected at its colon.

### Case description

```yaml
gesBlock: "case"
id: "invalid-construction-number"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "n: :Number('abc')"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "invalidLiteral"
    line: 1
    column: 4
```

---

## Test: a Message with an invalid tag is an error

This case checks the following contract: A Message form whose tags are not Tags constructs nothing and is rejected.

### Case description

```yaml
gesBlock: "case"
id: "invalid-construction-message-tag"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "signal: :Message(Done(v: 1) with 5)"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "invalidLiteral"
    line: 1
    column: 9
```

---

## Test: the Nothing form still reads as nothing

This case checks the following contract: Only `:Nothing(...)` may construct nothing in a document.

### Case description

```yaml
gesBlock: "case"
id: "nothing-constructor"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "missing: :Nothing()"
  print: true
```

### Expectation

```yaml
gesBlock: "expect"
document:
  value:
    type: ":Map"
    entries:
      - key: "missing"
        value:
          type: ":Nothing"
```

---

## Test: repeated Vector labels are duplicate keys

This case checks the following contract: A repeated component label is `document.duplicateKey` at the repeated label.

### Case description

```yaml
gesBlock: "case"
id: "duplicate-vector-label"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "mount: :Vector(x: 1m, x: 2m)"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "duplicateKey"
    line: 1
    column: 23
```

---

## Test: reading errors precede construction errors

This case checks the following contract: A syntax error anywhere in the text is reported before a construction failure or Range limit earlier in the text.

### Case description

```yaml
gesBlock: "case"
id: "syntax-before-construction"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "a: :List(:Range(1, 10001))\nb: oops"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "unquotedText"
    line: 2
    column: 4
```

---

## Test: construction errors follow construction order

This case checks the following contract: Among construction failures, the first form constructed is reported; siblings construct left to right.

### Case description

```yaml
gesBlock: "case"
id: "construction-order"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "a: [:Range(1), :Number('x')]"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "invalidLiteral"
    line: 1
    column: 5
```

---

## Test: built-in type names are not schemas

This case checks the following contract: A built-in type name in the header is `document.invalidSchemaHeader` at the argument.

### Case description

```yaml
gesBlock: "case"
id: "header-built-in-type"
kind: "documentApi"
level: "atomic"
documentApi:
  text: ":Schema(:List)\n"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "invalidSchemaHeader"
    line: 1
    column: 9
```

---

## Test: schema type names follow the type name grammar

This case checks the following contract: A lower-case name is not a type name.

### Case description

```yaml
gesBlock: "case"
id: "header-lower-case-type"
kind: "documentApi"
level: "atomic"
documentApi:
  text: ":Schema(:unit)"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "invalidSchemaHeader"
    line: 1
    column: 9
```

---

## Test: the header needs a schema argument

This case checks the following contract: An empty argument list is `document.invalidSchemaHeader` at the closing parenthesis.

### Case description

```yaml
gesBlock: "case"
id: "header-without-argument"
kind: "documentApi"
level: "atomic"
documentApi:
  text: ":Schema()"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "invalidSchemaHeader"
    line: 1
    column: 9
```

---

## Test: labeled header arguments are reserved

This case checks the following contract: No labeled arguments are defined yet, so a label is `document.invalidSchemaHeader` at that argument.

### Case description

```yaml
gesBlock: "case"
id: "header-labeled-argument"
kind: "documentApi"
level: "atomic"
documentApi:
  text: ":Schema(:Unit, version: 2)"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "invalidSchemaHeader"
    line: 1
    column: 16
```

---

## Test: the header names one schema

This case checks the following contract: A second positional argument is `document.invalidSchemaHeader`.

### Case description

```yaml
gesBlock: "case"
id: "header-two-schemas"
kind: "documentApi"
level: "atomic"
documentApi:
  text: ":Schema(:Unit, 'unit.gess')"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "invalidSchemaHeader"
    line: 1
    column: 16
```

---

## Test: schema paths are not empty

This case checks the following contract: An empty Text path is `document.invalidSchemaHeader`.

### Case description

```yaml
gesBlock: "case"
id: "header-empty-path"
kind: "documentApi"
level: "atomic"
documentApi:
  text: ":Schema('')"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "invalidSchemaHeader"
    line: 1
    column: 9
```

---

## Test: schemas are type names or Text

This case checks the following contract: Any other argument kind is `document.invalidSchemaHeader`.

### Case description

```yaml
gesBlock: "case"
id: "header-number"
kind: "documentApi"
level: "atomic"
documentApi:
  text: ":Schema(42)"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "invalidSchemaHeader"
    line: 1
    column: 9
```

---

## Test: an unclosed header is an error

This case checks the following contract: The end of the text inside the header reports its opening parenthesis.

### Case description

```yaml
gesBlock: "case"
id: "header-unclosed"
kind: "documentApi"
level: "atomic"
documentApi:
  text: ":Schema(:Unit"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "unclosedBracket"
    line: 1
    column: 8
```

---

## Test: the first entry starts on a later line

This case checks the following contract: An entry on the header line is `document.unexpectedToken`.

### Case description

```yaml
gesBlock: "case"
id: "header-followed-by-entry"
kind: "documentApi"
level: "atomic"
documentApi:
  text: ":Schema(:Unit) id: 1"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "unexpectedToken"
    line: 1
    column: 16
```

---

## Test: the header precedes every entry

This case checks the following contract: A header after the first entry is `document.misplacedSchemaHeader` at its colon.

### Case description

```yaml
gesBlock: "case"
id: "header-after-entry"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "id: 1\n:Schema(:Unit)"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "misplacedSchemaHeader"
    line: 2
    column: 1
```

---

## Test: only one header is allowed

This case checks the following contract: A second header is `document.misplacedSchemaHeader`.

### Case description

```yaml
gesBlock: "case"
id: "header-repeated"
kind: "documentApi"
level: "atomic"
documentApi:
  text: ":Schema(:Unit)\n// again\n:Schema(:Other)"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "misplacedSchemaHeader"
    line: 3
    column: 1
```

---

## Test: the byte order mark is not counted

This case checks the following contract: Columns on the first line ignore a leading byte order mark.

### Case description

```yaml
gesBlock: "case"
id: "column-after-byte-order-mark"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "﻿a 1"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "missingColon"
    line: 1
    column: 3
```

---

## Test: columns count Unicode scalars

This case checks the following contract: A non-BMP scalar advances the column by one.

### Case description

```yaml
gesBlock: "case"
id: "column-in-scalars"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "'😀😀': 1 b"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "unexpectedToken"
    line: 1
    column: 9
```

---

## Test: CRLF is one line break

This case checks the following contract: Line numbers count CRLF and CR once.

### Case description

```yaml
gesBlock: "case"
id: "line-after-crlf"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "a: 1\r\nb: 1\rb: 2"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "duplicateKey"
    line: 3
    column: 1
```

---

## Test: documents share the nesting limit

This case checks the following contract: The 65th nested container exceeds MaxLiteralDepth at its opening bracket.

### Case description

```yaml
gesBlock: "case"
id: "depth-limit"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "a: [[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "limitExceeded"
    line: 1
    column: 68
    limitName: "MaxLiteralDepth"
    limit: 64
```

---

## Test: documents share the Range materialization limit

This case checks the following contract: Materializing more than the default MaxRangeItems from a Range reports the List form.

### Case description

```yaml
gesBlock: "case"
id: "range-limit"
kind: "documentApi"
level: "atomic"
documentApi:
  text: "a: :List(:Range(1, 10001))"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "limitExceeded"
    line: 1
    column: 4
    limitName: "MaxRangeItems"
    limit: 10000
```

---

## Test: documents share the item limit

This case checks the following contract: Root entries and container items share MaxLiteralItems; the item past the limit reports its position.

### Case description

```yaml
gesBlock: "case"
id: "items-limit"
kind: "documentApi"
level: "atomic"
documentApi:
  text:
    prefix: "a: ["
    repeat: "0, "
    count: 65535
    suffix: "0]"
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "limitExceeded"
    line: 1
    column: 196610
    limitName: "MaxLiteralItems"
    limit: 65536
```

---

## Test: documents share the input length limit

This case checks the following contract: Text longer than MaxLiteralInputScalars is rejected at line 1, column 1 before reading.

### Case description

```yaml
gesBlock: "case"
id: "input-limit"
kind: "documentApi"
level: "atomic"
documentApi:
  text:
    repeat: " "
    count: 1048577
```

### Expectation

```yaml
gesBlock: "expect"
document:
  error:
    code: "limitExceeded"
    line: 1
    column: 1
    limitName: "MaxLiteralInputScalars"
    limit: 1048576
```
