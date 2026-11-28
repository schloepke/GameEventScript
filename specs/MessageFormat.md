<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Product message JSON V1

> **Since: 0.2.0**

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

> **Since: 0.5.0** — system endpoint names

The `name` of a message or Handler starts uppercase: writing or reading the system
endpoints `initialization` and `undeliverable` is `message.invalidValue`.

```json
{"message":{"args":[{"name":"value","value":{"type":"Number","unit":"","value":"100.25"}}],"name":"Done","tags":["ready"]},"version":1}
```

Writers use compact JSON, scalar-ordinal sorted object properties, and retain
array order. They escape quote and backslash with a backslash and U+0000–001F
with lowercase four-digit `\u` escapes; other Unicode scalars remain literal.
JSON strings use JSON escaping, not GES's doubled-delimiter convention.
Readers accept JSON whitespace, equivalent JSON escapes and arbitrary object
property order. A `\u` escape consists of exactly four ASCII hex digits. Duplicate object properties and unknown/missing properties are
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
| `Number` | `value`: numeric Text; `unit`: `""` or the canonical unit symbol, such as `"s"`, `"m/s"` or `"°/s"`; ASCII and prefixed spellings are invalid |
| `Percentage` | `value`: numeric Text containing the ratio, e.g. `"0.1"` for 10% |
| `Boolean` | `value`: JSON Boolean |
| `Text` | `value`: JSON string |
| `Tag` | `value`: bare valid tag name |
| `List` | `items`: ordered value objects |
| `Map` | `entries`: ordered `{key: Text, value: value-object}` objects |
| `Record` | `name`: type name; `entries`: Map-style entries |
| `Vector`, `Point` | `x`, `y`, `z`: numeric Text; `unit`: the Number unit strings |
| `Dice` | `rolls`: positive Int32 numeric strings, decimal and descending on output |
| `Bytes` | `base64`: the bytes in padded standard Base64 (RFC 4648 section 4) without line breaks; other spellings, such as missing padding or set unused bits, are invalid |
| `Range` | `from`, `to`, `step`: unitless numeric Text |
| `Series` | `kind`: `"fibonacci"` or `"factorial"`; `offset`: nonnegative Int64 numeric Text |
| `Handler` | `name`: message name; `labels`: ordered argument labels |
| `Message` | `value`: message object |
| `Handle` | `name`: handle type name; `id`: a Number with integer storage and unit `""`, or a Text value object; for a type with a root `name` is that type's name and `id` its root's, and a handle with a part additionally has `part`: like `id`; a keyed handle with a part additionally has `keyed`: `true`; optionally `version`: a Number value object for a revision or a Bytes value object of 20 or 32 bytes; an attached handle's version is its root's, a keyed handle's its own. *Since: Unreleased* |

Numeric decoding and normalization follow [Numbers](Semantics/Numbers.md).
Number payloads do not embed units or percent suffixes. NaN is not a Number
value; its canonical value representation is Nothing. Number infinities use
`Infinity` and `-Infinity`. Percentage and Range components must be finite.
Map/Record writers order entries by Unicode scalar key order; duplicate decoded
keys are rejected. A Map entry key may also be a Handle object without a version;
such entries follow the Text keys in the [key order](Language.md#handles) of handles.
*Since: Unreleased — handle keys.* Refs are never written: encoding a value that
contains one fails with `message.invalidValue`. Scalar-distinct Unicode keys remain distinct.

A Record contains immutable typed field data. Decoding never resolves a Record
definition and never invokes its constructor. External values are exported as
Records using their declared type name and readable field snapshot, recursively.
There is no External wire type and decoding does not recreate native identity.
Getter failures propagate to the encoding caller. Series transport supports
only the two built-in data series and their offsets.

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
`GesMessageFormatException.Code`; in Swift by
`GesMessageFormatError.code`.
