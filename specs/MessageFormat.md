<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Product message JSON V1

> **Since: Unreleased**

This document owns the optional, language-neutral product wire format. It is
independent of the Conformance transport and of GESB. Encoding and decoding are
explicit API operations; local Receive, Emit and Publish continue to forward
immutable values directly without JSON, external projection or constructor calls.

## Envelope and canonical writing

A message document contains exactly `version: 1` and `message`. A value document
contains exactly `version: 1` and `value`. A message object contains exactly
`name`, `tags` and `args`. Arguments are an ordered array of objects containing
`name` and `value`; `_` denotes an unlabeled argument. Labels and their order
are part of the signature. Named labels must be unique; `_` may repeat.
Tags use bare lowercase tag names, normalized by the ordinary message API.

```json
{"message":{"args":[{"name":"value","value":{"type":"Number","unit":"","value":"100.25"}}],"name":"Done","tags":["ready"]},"version":1}
```

Writers use compact JSON, scalar-ordinal sorted object properties, and retain
array order. They escape quote and backslash with a backslash and U+0000–001F
with lowercase four-digit `\u` escapes; other Unicode scalars remain literal.
JSON strings use JSON escaping, not GES's doubled-delimiter convention.
Readers accept JSON whitespace, equivalent JSON escapes and arbitrary object
property order. Duplicate object properties and unknown/missing properties are
errors. No prefix, trailing content, comments or malformed surrogate escape is
accepted. JSON numeric tokens in this schema are signed decimal integers;
only the envelope version uses one. GES numeric payloads are strings, avoiding
precision loss through an intermediary JSON implementation.

## Value objects

Every value object contains `type` and exactly the fields listed below.
There are no public Integer/Binary64 value tags.

| type | Additional fields |
| --- | --- |
| `Nothing` | none |
| `Number` | `value`: numeric Text; `unit`: `""`, `"s"`, `"m"` or `"°"` |
| `Percentage` | `value`: numeric Text containing the ratio, e.g. `"0.1"` for 10% |
| `Boolean` | `value`: JSON Boolean |
| `Text` | `value`: JSON string |
| `Tag` | `value`: bare valid tag name |
| `List` | `items`: ordered value objects |
| `Map` | `entries`: ordered `{key: Text, value: value-object}` objects |
| `Record` | `name`: PascalCase type name; `entries`: Map-style entries |
| `Vector`, `Point` | `x`, `y`, `z`: numeric Text; `unit`: the Number unit strings |
| `Dice` | `rolls`: positive Int32 decimal numeric strings, descending on output |
| `Range` | `from`, `to`, `step`: unitless numeric Text |
| `Series` | `kind`: `"fibonacci"` or `"factorial"`; `offset`: nonnegative Int64 numeric Text |
| `Handler` | `name`: message name; `labels`: ordered argument labels |
| `Message` | `value`: message object |

Numeric decoding and normalization follow [Numbers](Semantics/Numbers.md).
Number payloads do not embed units or percent suffixes. NaN is not a Number
value; its canonical value representation is Nothing. Number infinities use
`Infinity` and `-Infinity`. Percentage and Range components must be finite.
Map/Record writers order entries by Unicode scalar key order; duplicate decoded
keys are rejected. Scalar-distinct Unicode keys remain distinct.

A Record contains immutable typed field data. Decoding never resolves a Record
definition and never invokes its constructor. External values are exported as
Records using their declared type name and readable field snapshot, recursively.
There is no External wire type and decoding does not recreate native identity.
Getter failures propagate to the encoding caller. Host-defined executable
Series are unsupported; only the two built-in data series can cross this boundary.

Ranges remain lazy. Integral representable endpoints and steps normalize to
exact Int64 ranges as defined by [Language](Language.md#range); other finite
ranges use the existing Binary64 term rule. Transport does not materialize terms.

## Limits and failures

Both implementations enforce at most 64 data nesting levels (the root is level
zero), 65,536 visited data values/message objects, and 4,194,304 UTF-16 code units
of JSON. Syntax parsing additionally limits JSON nesting to 256 and visited JSON
values to 524,288. Cyclic external graphs terminate with a resource-limit error.
No partially decoded message is returned or enqueued. Limits do not consume a
Host's opcode budget; the codec operates independently of a Host.

Stable failure codes are `message.invalidJson`, `message.invalidValue`,
`message.unsupportedVersion`, `message.unsupportedSeries`, and
`message.resourceLimit`. In C# they are carried by
`GameEventScriptMessageFormatException.Code`; in Swift by
`GameEventScriptMessageFormatError.code`.
