---
formatVersion: 1
suiteId: runtime.bytes
title: "Bytes values, literals and conversions"
kind: scriptApi
level: atomic
categories: [conformance]
---

# Bytes values, literals and conversions

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

These cases define Bytes literals, casts, text, equality, order and collection access.

---

## Test: literals and casts

Both literal forms build the same value; Lists of unitless integers from 0 to 255 and hexadecimal Text convert, anything else gives nothing.

### Case description

```yaml
gesBlock: case
id: "literals"
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start {
  let listed be :Bytes[0xa0, 183, 11]
  let multiline be :Bytes[
    1, // Source comments are normal trivia.
    0x02
  ]
  emit Done(listed: listed, hex: :Bytes("A0b7 0B"), multiline: multiline, none: :Bytes[], emptyText: :Bytes(""), fromList: [0, 255] as :Bytes, outOfRange: [256] as :Bytes, unit: [1m] as :Bytes, fraction: [1.5] as :Bytes, oddText: "abc" as :Bytes, badText: "zz" as :Bytes, number: 5 as :Bytes, labeled: :Bytes(value: "ff"), same: :Bytes(listed))
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
      - name: "Done"
        args:
          - name: "listed"
            value:
              type: ":Bytes"
              value: "a0b70b"
          - name: "hex"
            value:
              type: ":Bytes"
              value: "a0b70b"
          - name: "multiline"
            value:
              type: ":Bytes"
              value: "0102"
          - name: "none"
            value:
              type: ":Bytes"
              value: ""
          - name: "emptyText"
            value:
              type: ":Bytes"
              value: ""
          - name: "fromList"
            value:
              type: ":Bytes"
              value: "00ff"
          - name: "outOfRange"
            value:
              type: ":Nothing"
          - name: "unit"
            value:
              type: ":Nothing"
          - name: "fraction"
            value:
              type: ":Nothing"
          - name: "oddText"
            value:
              type: ":Nothing"
          - name: "badText"
            value:
              type: ":Nothing"
          - name: "number"
            value:
              type: ":Nothing"
          - name: "labeled"
            value:
              type: ":Bytes"
              value: "ff"
          - name: "same"
            value:
              type: ":Bytes"
              value: "a0b70b"
```

---

## Test: text, lists and checks

Bytes print as their canonical literal, parse back, convert to a List of Int64 numbers and are no Text.

### Case description

```yaml
gesBlock: case
id: "text-and-lists"
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start {
  let value be :Bytes("ff00a0")
  emit Done(text: value as :Text, parsed: parse (value as :Text), parsedList: parse ":Bytes[1, 0x2]", strict: parse strict ":Bytes[256]", list: value as :List, isBytes: value is :Bytes, isText: value is :Text, blank: empty :Bytes[], filled: empty value)
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
      - name: "Done"
        args:
          - name: "text"
            value:
              type: ":Text"
              value: ":Bytes(\"ff00a0\")"
          - name: "parsed"
            value:
              type: ":Bytes"
              value: "ff00a0"
          - name: "parsedList"
            value:
              type: ":Bytes"
              value: "0102"
          - name: "strict"
            value:
              type: ":Nothing"
          - name: "list"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "255"
                - type: ":Number.int64"
                  value: "0"
                - type: ":Number.int64"
                  value: "160"
          - name: "isBytes"
            value:
              type: ":Boolean"
              value: "true"
          - name: "isText"
            value:
              type: ":Boolean"
              value: "false"
          - name: "blank"
            value:
              type: ":Boolean"
              value: "true"
          - name: "filled"
            value:
              type: ":Boolean"
              value: "false"
```

---

## Test: count, membership, index and iteration

Bytes count their bytes, contain unitless integers, index from 1 and iterate as Int64 numbers.

### Case description

```yaml
gesBlock: case
id: "collection-access"
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start {
  let value be :Bytes("0a14ff")
  let total be value[:fold sum be 0, b => sum + b]
  emit Done(count: value[:count], found: 20 in value, missing: 21 in value, text: "0a" in value, first: value[1], last: value[3], beyond: value[4], total: total)
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
      - name: "Done"
        args:
          - name: "count"
            value:
              type: ":Number.int64"
              value: "3"
          - name: "found"
            value:
              type: ":Boolean"
              value: "true"
          - name: "missing"
            value:
              type: ":Boolean"
              value: "false"
          - name: "text"
            value:
              type: ":Boolean"
              value: "false"
          - name: "first"
            value:
              type: ":Number.int64"
              value: "10"
          - name: "last"
            value:
              type: ":Number.int64"
              value: "255"
          - name: "beyond"
            value:
              type: ":Nothing"
          - name: "total"
            value:
              type: ":Number.int64"
              value: "285"
```

---

## Test: equality and order

Equality compares the bytes exactly; sorting orders Bytes lexicographically by unsigned byte after every other ranked kind.

### Case description

```yaml
gesBlock: case
id: "equality-and-order"
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start {
  emit Done(equal: :Bytes[1, 2] = :Bytes("0102"), unequal: :Bytes[1] = :Bytes[1, 0], notList: :Bytes[1] = [1], sorted: [:Bytes("02"), :Bytes("0100"), :Bytes("01"), :Bytes("ff")][:sort ascending], mixed: [:Bytes("00"), [1], "a"][:sort ascending], distinct: [:Bytes("01"), :Bytes[1], :Bytes("02")][:distinct])
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
      - name: "Done"
        args:
          - name: "equal"
            value:
              type: ":Boolean"
              value: "true"
          - name: "unequal"
            value:
              type: ":Boolean"
              value: "false"
          - name: "notList"
            value:
              type: ":Boolean"
              value: "false"
          - name: "sorted"
            value:
              type: ":List"
              items:
                - type: ":Bytes"
                  value: "01"
                - type: ":Bytes"
                  value: "0100"
                - type: ":Bytes"
                  value: "02"
                - type: ":Bytes"
                  value: "ff"
          - name: "mixed"
            value:
              type: ":List"
              items:
                - type: ":Text"
                  value: "a"
                - type: ":List"
                  items:
                    - type: ":Number.int64"
                      value: "1"
                - type: ":Bytes"
                  value: "00"
          - name: "distinct"
            value:
              type: ":List"
              items:
                - type: ":Bytes"
                  value: "01"
                - type: ":Bytes"
                  value: "02"
```

---

## Test: source-rejects-256

A Bytes literal rejects an integer above 255; its items are integer literals from 0 to 255.

### Case description

```yaml
gesBlock: case
id: "source-rejects-256"
kind: "compileError"
```

### Source code under test

```ges
on Start { let value be 1; emit Done(value: :Bytes[256]) }
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

---

## Test: source-rejects-negative

A Bytes literal rejects a sign; its items are integer literals from 0 to 255.

### Case description

```yaml
gesBlock: case
id: "source-rejects-negative"
kind: "compileError"
```

### Source code under test

```ges
on Start { let value be 1; emit Done(value: :Bytes[-1]) }
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

---

## Test: source-rejects-fraction

A Bytes literal rejects a fraction; its items are integer literals from 0 to 255.

### Case description

```yaml
gesBlock: case
id: "source-rejects-fraction"
kind: "compileError"
```

### Source code under test

```ges
on Start { let value be 1; emit Done(value: :Bytes[1.5]) }
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```

---

## Test: source-rejects-name

A Bytes literal rejects a name; its items are integer literals from 0 to 255.

### Case description

```yaml
gesBlock: case
id: "source-rejects-name"
kind: "compileError"
```

### Source code under test

```ges
on Start { let value be 1; emit Done(value: :Bytes[value]) }
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
```
