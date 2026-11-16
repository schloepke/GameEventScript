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

These cases define Bytes literals, casts, text, equality, order, collection access, slicing, concatenation and UTF-8 conversion.

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

## Test: selection and membership of several values

Bytes select their first, last and single byte and answer `:contains any` and `:contains all` with their bytes, as Lists of
those numbers do.

### Case description

```yaml
gesBlock: case
id: "selection-and-membership"
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start {
  let value be :Bytes[5, 7, 9]
  let one be :Bytes[4]
  let blank be :Bytes[]
  let numbers be [1, 7, 9]
  emit Done(first: value[:first], last: value[:last], single: value[:single], one: one[:single], fromEmpty: blank[:first], any: value[:contains any [2, 7]], anyMissing: value[:contains any [2, 8]], all: value[:contains all [9, 5]], allMissing: value[:contains all [5, 6]], into: numbers[:contains any value])
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
          - name: "first"
            value:
              type: ":Number.int64"
              value: "5"
          - name: "last"
            value:
              type: ":Number.int64"
              value: "9"
          - name: "single"
            value:
              type: ":Nothing"
          - name: "one"
            value:
              type: ":Number.int64"
              value: "4"
          - name: "fromEmpty"
            value:
              type: ":Nothing"
          - name: "any"
            value:
              type: ":Boolean"
              value: "true"
          - name: "anyMissing"
            value:
              type: ":Boolean"
              value: "false"
          - name: "all"
            value:
              type: ":Boolean"
              value: "true"
          - name: "allMissing"
            value:
              type: ":Boolean"
              value: "false"
          - name: "into"
            value:
              type: ":Boolean"
              value: "true"
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

## Test: take and drop

Taking or dropping the first or last bytes gives Bytes; a count beyond the length selects all or none. Highest, lowest and reverse are not defined for Bytes.

### Case description

```yaml
gesBlock: case
id: "slicing"
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start {
  let value be :Bytes[1, 2, 3, 4]
  emit Done(takeFirst: value[:take first 2], dropFirst: value[:drop first 1], takeLast: value[:take last 3], dropLast: value[:drop last 1], all: value[:take first 9], none: value[:drop last 9], highest: value[:take highest 2], reverse: value[:reverse])
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
          - name: "takeFirst"
            value:
              type: ":Bytes"
              value: "0102"
          - name: "dropFirst"
            value:
              type: ":Bytes"
              value: "020304"
          - name: "takeLast"
            value:
              type: ":Bytes"
              value: "020304"
          - name: "dropLast"
            value:
              type: ":Bytes"
              value: "010203"
          - name: "all"
            value:
              type: ":Bytes"
              value: "01020304"
          - name: "none"
            value:
              type: ":Bytes"
              value: ""
          - name: "highest"
            value:
              type: ":Nothing"
          - name: "reverse"
            value:
              type: ":Nothing"
```

---

## Test: concatenation

`+` joins two Bytes; Text and Lists keep their own `+` rules, and every other partner gives nothing.

### Case description

```yaml
gesBlock: case
id: "concatenation"
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start {
  let value be :Bytes[1, 2]
  emit Done(joined: value + :Bytes("ff"), leading: :Bytes[] + value, number: value + 3, text: "x" + value, list: [0] + value, difference: value - value)
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
          - name: "joined"
            value:
              type: ":Bytes"
              value: "0102ff"
          - name: "leading"
            value:
              type: ":Bytes"
              value: "0102"
          - name: "number"
            value:
              type: ":Nothing"
          - name: "text"
            value:
              type: ":Text"
              value: "x:Bytes(\"0102\")"
          - name: "list"
            value:
              type: ":List"
              items:
                - type: ":Number.int64"
                  value: "0"
                - type: ":Bytes"
                  value: "0102"
          - name: "difference"
            value:
              type: ":Nothing"
```

---

## Test: UTF-8 conversion

`:Bytes(utf8:)` encodes Text or a Tag as UTF-8; `:Text(utf8:)` decodes valid UTF-8 and gives nothing for invalid input, such as surrogates and overlong forms, or another kind.

### Case description

```yaml
gesBlock: case
id: "utf8"
compile:
  binaryRoundTrip: true
```

### Source code under test

```ges
on Start {
  emit Done(encoded: :Bytes(utf8: "hé"), tag: :Bytes(utf8: #ok), emptyText: :Bytes(utf8: ""), number: :Bytes(utf8: 5), decoded: :Text(utf8: :Bytes("68c3a9")), truncated: :Text(utf8: :Bytes("c3")), surrogate: :Text(utf8: :Bytes("eda080")), overlong: :Text(utf8: :Bytes("c0af")), notBytes: :Text(utf8: "x"), parsed: parse ':Text(utf8: :Bytes("6869"))')
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
          - name: "encoded"
            value:
              type: ":Bytes"
              value: "68c3a9"
          - name: "tag"
            value:
              type: ":Bytes"
              value: "6f6b"
          - name: "emptyText"
            value:
              type: ":Bytes"
              value: ""
          - name: "number"
            value:
              type: ":Nothing"
          - name: "decoded"
            value:
              type: ":Text"
              value: "h\u00e9"
          - name: "truncated"
            value:
              type: ":Nothing"
          - name: "surrogate"
            value:
              type: ":Nothing"
          - name: "overlong"
            value:
              type: ":Nothing"
          - name: "notBytes"
            value:
              type: ":Nothing"
          - name: "parsed"
            value:
              type: ":Text"
              value: "hi"
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
