---
formatVersion: 1
suiteId: api.product-json
title: Portable V1 product JSON
kind: messageApi
level: atomic
categories: [conformance]
---

# Portable V1 product JSON

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite specifies portable data construction and interchange with independent expectations.

---

## Test: nothing

This case verifies nothing.

### Case description

```yaml
gesBlock: case
id: "nothing"
messageApi:
  signature:
    name: "Done"
    parameters:
      - "value"
  message:
    name: "Done"
    args: []
  json: "{\"message\":{\"args\":[{\"name\":\"value\",\"value\":{\"type\":\"Nothing\"}}],\"name\":\"Done\",\"tags\":[\"ready\"]},\"version\":1}"
  roundTripJson: true
```

### Expectation

```yaml
gesBlock: expect
message:
  json: "{\"message\":{\"args\":[{\"name\":\"value\",\"value\":{\"type\":\"Nothing\"}}],\"name\":\"Done\",\"tags\":[\"ready\"]},\"version\":1}"
  messageSignatureId: "Done(value)"
  argumentCount: 1
```

---

## Test: integer-boundary

This case verifies integer boundary.

### Case description

```yaml
gesBlock: case
id: "integer-boundary"
messageApi:
  signature:
    name: "Done"
    parameters:
      - "value"
  message:
    name: "Done"
    args: []
  json: "{\"message\":{\"args\":[{\"name\":\"value\",\"value\":{\"type\":\"Number\",\"unit\":\"\",\"value\":\"9223372036854775807\"}}],\"name\":\"Done\",\"tags\":[\"ready\"]},\"version\":1}"
  roundTripJson: true
```

### Expectation

```yaml
gesBlock: expect
message:
  json: "{\"message\":{\"args\":[{\"name\":\"value\",\"value\":{\"type\":\"Number\",\"unit\":\"\",\"value\":\"9223372036854775807\"}}],\"name\":\"Done\",\"tags\":[\"ready\"]},\"version\":1}"
  messageSignatureId: "Done(value)"
  argumentCount: 1
```

---

## Test: negative-integer-boundary

This case verifies negative integer boundary.

### Case description

```yaml
gesBlock: case
id: "negative-integer-boundary"
messageApi:
  signature:
    name: "Done"
    parameters:
      - "value"
  message:
    name: "Done"
    args: []
  json: "{\"message\":{\"args\":[{\"name\":\"value\",\"value\":{\"type\":\"Number\",\"unit\":\"\",\"value\":\"-9223372036854775808\"}}],\"name\":\"Done\",\"tags\":[\"ready\"]},\"version\":1}"
  roundTripJson: true
```

### Expectation

```yaml
gesBlock: expect
message:
  json: "{\"message\":{\"args\":[{\"name\":\"value\",\"value\":{\"type\":\"Number\",\"unit\":\"\",\"value\":\"-9223372036854775808\"}}],\"name\":\"Done\",\"tags\":[\"ready\"]},\"version\":1}"
  messageSignatureId: "Done(value)"
  argumentCount: 1
```

---

## Test: binary64

This case verifies binary64.

### Case description

```yaml
gesBlock: case
id: "binary64"
messageApi:
  signature:
    name: "Done"
    parameters:
      - "value"
  message:
    name: "Done"
    args: []
  json: "{\"message\":{\"args\":[{\"name\":\"value\",\"value\":{\"type\":\"Number\",\"unit\":\"\",\"value\":\"1e-5\"}}],\"name\":\"Done\",\"tags\":[\"ready\"]},\"version\":1}"
  roundTripJson: true
```

### Expectation

```yaml
gesBlock: expect
message:
  json: "{\"message\":{\"args\":[{\"name\":\"value\",\"value\":{\"type\":\"Number\",\"unit\":\"\",\"value\":\"1e-5\"}}],\"name\":\"Done\",\"tags\":[\"ready\"]},\"version\":1}"
  messageSignatureId: "Done(value)"
  argumentCount: 1
```

---

## Test: nan

This case verifies nan.

### Case description

```yaml
gesBlock: case
id: "nan"
messageApi:
  signature:
    name: "Done"
    parameters:
      - "value"
  message:
    name: "Done"
    args: []
  json: "{\"message\":{\"args\":[{\"name\":\"value\",\"value\":{\"type\":\"Number\",\"unit\":\"\",\"value\":\"NaN\"}}],\"name\":\"Done\",\"tags\":[\"ready\"]},\"version\":1}"
  roundTripJson: true
```

### Expectation

```yaml
gesBlock: expect
message:
  error: "message.invalidValue"
```

---

## Test: infinity

This case verifies infinity.

### Case description

```yaml
gesBlock: case
id: "infinity"
messageApi:
  signature:
    name: "Done"
    parameters:
      - "value"
  message:
    name: "Done"
    args: []
  json: "{\"message\":{\"args\":[{\"name\":\"value\",\"value\":{\"type\":\"Number\",\"unit\":\"\",\"value\":\"Infinity\"}}],\"name\":\"Done\",\"tags\":[\"ready\"]},\"version\":1}"
  roundTripJson: true
```

### Expectation

```yaml
gesBlock: expect
message:
  json: "{\"message\":{\"args\":[{\"name\":\"value\",\"value\":{\"type\":\"Number\",\"unit\":\"\",\"value\":\"Infinity\"}}],\"name\":\"Done\",\"tags\":[\"ready\"]},\"version\":1}"
  messageSignatureId: "Done(value)"
  argumentCount: 1
```

---

## Test: quantity

This case verifies quantity.

### Case description

```yaml
gesBlock: case
id: "quantity"
messageApi:
  signature:
    name: "Done"
    parameters:
      - "value"
  message:
    name: "Done"
    args: []
  json: "{\"message\":{\"args\":[{\"name\":\"value\",\"value\":{\"type\":\"Number\",\"unit\":\"s\",\"value\":\"2.5\"}}],\"name\":\"Done\",\"tags\":[\"ready\"]},\"version\":1}"
  roundTripJson: true
```

### Expectation

```yaml
gesBlock: expect
message:
  json: "{\"message\":{\"args\":[{\"name\":\"value\",\"value\":{\"type\":\"Number\",\"unit\":\"s\",\"value\":\"2.5\"}}],\"name\":\"Done\",\"tags\":[\"ready\"]},\"version\":1}"
  messageSignatureId: "Done(value)"
  argumentCount: 1
```

---

## Test: percentage

This case verifies percentage.

### Case description

```yaml
gesBlock: case
id: "percentage"
messageApi:
  signature:
    name: "Done"
    parameters:
      - "value"
  message:
    name: "Done"
    args: []
  json: "{\"message\":{\"args\":[{\"name\":\"value\",\"value\":{\"type\":\"Percentage\",\"value\":\"0.1\"}}],\"name\":\"Done\",\"tags\":[\"ready\"]},\"version\":1}"
  roundTripJson: true
```

### Expectation

```yaml
gesBlock: expect
message:
  json: "{\"message\":{\"args\":[{\"name\":\"value\",\"value\":{\"type\":\"Percentage\",\"value\":\"0.1\"}}],\"name\":\"Done\",\"tags\":[\"ready\"]},\"version\":1}"
  messageSignatureId: "Done(value)"
  argumentCount: 1
```

---

## Test: boolean

This case verifies boolean.

### Case description

```yaml
gesBlock: case
id: "boolean"
messageApi:
  signature:
    name: "Done"
    parameters:
      - "value"
  message:
    name: "Done"
    args: []
  json: "{\"message\":{\"args\":[{\"name\":\"value\",\"value\":{\"type\":\"Boolean\",\"value\":false}}],\"name\":\"Done\",\"tags\":[\"ready\"]},\"version\":1}"
  roundTripJson: true
```

### Expectation

```yaml
gesBlock: expect
message:
  json: "{\"message\":{\"args\":[{\"name\":\"value\",\"value\":{\"type\":\"Boolean\",\"value\":false}}],\"name\":\"Done\",\"tags\":[\"ready\"]},\"version\":1}"
  messageSignatureId: "Done(value)"
  argumentCount: 1
```

---

## Test: text

This case verifies text.

### Case description

```yaml
gesBlock: case
id: "text"
messageApi:
  signature:
    name: "Done"
    parameters:
      - "value"
  message:
    name: "Done"
    args: []
  json: "{\"message\":{\"args\":[{\"name\":\"value\",\"value\":{\"type\":\"Text\",\"value\":\"true \\\"quoted\\\" é é 😀\"}}],\"name\":\"Done\",\"tags\":[\"ready\"]},\"version\":1}"
  roundTripJson: true
```

### Expectation

```yaml
gesBlock: expect
message:
  json: "{\"message\":{\"args\":[{\"name\":\"value\",\"value\":{\"type\":\"Text\",\"value\":\"true \\\"quoted\\\" é é 😀\"}}],\"name\":\"Done\",\"tags\":[\"ready\"]},\"version\":1}"
  messageSignatureId: "Done(value)"
  argumentCount: 1
```

---

## Test: tag

This case verifies tag.

### Case description

```yaml
gesBlock: case
id: "tag"
messageApi:
  signature:
    name: "Done"
    parameters:
      - "value"
  message:
    name: "Done"
    args: []
  json: "{\"message\":{\"args\":[{\"name\":\"value\",\"value\":{\"type\":\"Tag\",\"value\":\"ready\"}}],\"name\":\"Done\",\"tags\":[\"ready\"]},\"version\":1}"
  roundTripJson: true
```

### Expectation

```yaml
gesBlock: expect
message:
  json: "{\"message\":{\"args\":[{\"name\":\"value\",\"value\":{\"type\":\"Tag\",\"value\":\"ready\"}}],\"name\":\"Done\",\"tags\":[\"ready\"]},\"version\":1}"
  messageSignatureId: "Done(value)"
  argumentCount: 1
```

---

## Test: list

This case verifies list.

### Case description

```yaml
gesBlock: case
id: "list"
messageApi:
  signature:
    name: "Done"
    parameters:
      - "value"
  message:
    name: "Done"
    args: []
  json: "{\"message\":{\"args\":[{\"name\":\"value\",\"value\":{\"items\":[{\"type\":\"Number\",\"unit\":\"\",\"value\":\"1\"},{\"type\":\"Text\",\"value\":\"1\"},{\"type\":\"Nothing\"}],\"type\":\"List\"}}],\"name\":\"Done\",\"tags\":[\"ready\"]},\"version\":1}"
  roundTripJson: true
```

### Expectation

```yaml
gesBlock: expect
message:
  json: "{\"message\":{\"args\":[{\"name\":\"value\",\"value\":{\"items\":[{\"type\":\"Number\",\"unit\":\"\",\"value\":\"1\"},{\"type\":\"Text\",\"value\":\"1\"},{\"type\":\"Nothing\"}],\"type\":\"List\"}}],\"name\":\"Done\",\"tags\":[\"ready\"]},\"version\":1}"
  messageSignatureId: "Done(value)"
  argumentCount: 1
```

---

## Test: map

This case verifies map.

### Case description

```yaml
gesBlock: case
id: "map"
messageApi:
  signature:
    name: "Done"
    parameters:
      - "value"
  message:
    name: "Done"
    args: []
  json: "{\"message\":{\"args\":[{\"name\":\"value\",\"value\":{\"entries\":[{\"key\":\"é\",\"value\":{\"type\":\"Number\",\"unit\":\"\",\"value\":\"1\"}},{\"key\":\"é\",\"value\":{\"type\":\"Number\",\"unit\":\"\",\"value\":\"2\"}}],\"type\":\"Map\"}}],\"name\":\"Done\",\"tags\":[\"ready\"]},\"version\":1}"
  roundTripJson: true
```

### Expectation

```yaml
gesBlock: expect
message:
  json: "{\"message\":{\"args\":[{\"name\":\"value\",\"value\":{\"entries\":[{\"key\":\"é\",\"value\":{\"type\":\"Number\",\"unit\":\"\",\"value\":\"1\"}},{\"key\":\"é\",\"value\":{\"type\":\"Number\",\"unit\":\"\",\"value\":\"2\"}}],\"type\":\"Map\"}}],\"name\":\"Done\",\"tags\":[\"ready\"]},\"version\":1}"
  messageSignatureId: "Done(value)"
  argumentCount: 1
```

---

## Test: record

This case verifies record.

### Case description

```yaml
gesBlock: case
id: "record"
messageApi:
  signature:
    name: "Done"
    parameters:
      - "value"
  message:
    name: "Done"
    args: []
  json: "{\"message\":{\"args\":[{\"name\":\"value\",\"value\":{\"entries\":[{\"key\":\"amount\",\"value\":{\"type\":\"Number\",\"unit\":\"\",\"value\":\"100\"}}],\"name\":\"Hit\",\"type\":\"Record\"}}],\"name\":\"Done\",\"tags\":[\"ready\"]},\"version\":1}"
  roundTripJson: true
```

### Expectation

```yaml
gesBlock: expect
message:
  json: "{\"message\":{\"args\":[{\"name\":\"value\",\"value\":{\"entries\":[{\"key\":\"amount\",\"value\":{\"type\":\"Number\",\"unit\":\"\",\"value\":\"100\"}}],\"name\":\"Hit\",\"type\":\"Record\"}}],\"name\":\"Done\",\"tags\":[\"ready\"]},\"version\":1}"
  messageSignatureId: "Done(value)"
  argumentCount: 1
```

---

## Test: vector

This case verifies vector.

### Case description

```yaml
gesBlock: case
id: "vector"
messageApi:
  signature:
    name: "Done"
    parameters:
      - "value"
  message:
    name: "Done"
    args: []
  json: "{\"message\":{\"args\":[{\"name\":\"value\",\"value\":{\"type\":\"Vector\",\"unit\":\"m\",\"x\":\"1\",\"y\":\"2\",\"z\":\"3\"}}],\"name\":\"Done\",\"tags\":[\"ready\"]},\"version\":1}"
  roundTripJson: true
```

### Expectation

```yaml
gesBlock: expect
message:
  json: "{\"message\":{\"args\":[{\"name\":\"value\",\"value\":{\"type\":\"Vector\",\"unit\":\"m\",\"x\":\"1\",\"y\":\"2\",\"z\":\"3\"}}],\"name\":\"Done\",\"tags\":[\"ready\"]},\"version\":1}"
  messageSignatureId: "Done(value)"
  argumentCount: 1
```

---

## Test: point

This case verifies point.

### Case description

```yaml
gesBlock: case
id: "point"
messageApi:
  signature:
    name: "Done"
    parameters:
      - "value"
  message:
    name: "Done"
    args: []
  json: "{\"message\":{\"args\":[{\"name\":\"value\",\"value\":{\"type\":\"Point\",\"unit\":\"\",\"x\":\"-1\",\"y\":\"0\",\"z\":\"2.5\"}}],\"name\":\"Done\",\"tags\":[\"ready\"]},\"version\":1}"
  roundTripJson: true
```

### Expectation

```yaml
gesBlock: expect
message:
  json: "{\"message\":{\"args\":[{\"name\":\"value\",\"value\":{\"type\":\"Point\",\"unit\":\"\",\"x\":\"-1\",\"y\":\"0\",\"z\":\"2.5\"}}],\"name\":\"Done\",\"tags\":[\"ready\"]},\"version\":1}"
  messageSignatureId: "Done(value)"
  argumentCount: 1
```

---

## Test: dice

This case verifies dice.

### Case description

```yaml
gesBlock: case
id: "dice"
messageApi:
  signature:
    name: "Done"
    parameters:
      - "value"
  message:
    name: "Done"
    args: []
  json: "{\"message\":{\"args\":[{\"name\":\"value\",\"value\":{\"rolls\":[\"6\",\"3\",\"1\"],\"type\":\"Dice\"}}],\"name\":\"Done\",\"tags\":[\"ready\"]},\"version\":1}"
  roundTripJson: true
```

### Expectation

```yaml
gesBlock: expect
message:
  json: "{\"message\":{\"args\":[{\"name\":\"value\",\"value\":{\"rolls\":[\"6\",\"3\",\"1\"],\"type\":\"Dice\"}}],\"name\":\"Done\",\"tags\":[\"ready\"]},\"version\":1}"
  messageSignatureId: "Done(value)"
  argumentCount: 1
```

---

## Test: range

This case verifies range.

### Case description

```yaml
gesBlock: case
id: "range"
messageApi:
  signature:
    name: "Done"
    parameters:
      - "value"
  message:
    name: "Done"
    args: []
  json: "{\"message\":{\"args\":[{\"name\":\"value\",\"value\":{\"from\":\"1.5\",\"step\":\"0.5\",\"to\":\"3.5\",\"type\":\"Range\"}}],\"name\":\"Done\",\"tags\":[\"ready\"]},\"version\":1}"
  roundTripJson: true
```

### Expectation

```yaml
gesBlock: expect
message:
  json: "{\"message\":{\"args\":[{\"name\":\"value\",\"value\":{\"from\":\"1.5\",\"step\":\"0.5\",\"to\":\"3.5\",\"type\":\"Range\"}}],\"name\":\"Done\",\"tags\":[\"ready\"]},\"version\":1}"
  messageSignatureId: "Done(value)"
  argumentCount: 1
```

---

## Test: series

This case verifies series.

### Case description

```yaml
gesBlock: case
id: "series"
messageApi:
  signature:
    name: "Done"
    parameters:
      - "value"
  message:
    name: "Done"
    args: []
  json: "{\"message\":{\"args\":[{\"name\":\"value\",\"value\":{\"kind\":\"fibonacci\",\"offset\":\"3\",\"type\":\"Series\"}}],\"name\":\"Done\",\"tags\":[\"ready\"]},\"version\":1}"
  roundTripJson: true
```

### Expectation

```yaml
gesBlock: expect
message:
  json: "{\"message\":{\"args\":[{\"name\":\"value\",\"value\":{\"kind\":\"fibonacci\",\"offset\":\"3\",\"type\":\"Series\"}}],\"name\":\"Done\",\"tags\":[\"ready\"]},\"version\":1}"
  messageSignatureId: "Done(value)"
  argumentCount: 1
```

---

## Test: handler

This case verifies handler.

### Case description

```yaml
gesBlock: case
id: "handler"
messageApi:
  signature:
    name: "Done"
    parameters:
      - "value"
  message:
    name: "Done"
    args: []
  json: "{\"message\":{\"args\":[{\"name\":\"value\",\"value\":{\"labels\":[\"value\",\"_\",\"_\"],\"name\":\"Done\",\"type\":\"Handler\"}}],\"name\":\"Done\",\"tags\":[\"ready\"]},\"version\":1}"
  roundTripJson: true
```

### Expectation

```yaml
gesBlock: expect
message:
  json: "{\"message\":{\"args\":[{\"name\":\"value\",\"value\":{\"labels\":[\"value\",\"_\",\"_\"],\"name\":\"Done\",\"type\":\"Handler\"}}],\"name\":\"Done\",\"tags\":[\"ready\"]},\"version\":1}"
  messageSignatureId: "Done(value)"
  argumentCount: 1
```

---

## Test: message

This case verifies message.

### Case description

```yaml
gesBlock: case
id: "message"
messageApi:
  signature:
    name: "Done"
    parameters:
      - "value"
  message:
    name: "Done"
    args: []
  json: "{\"message\":{\"args\":[{\"name\":\"value\",\"value\":{\"type\":\"Message\",\"value\":{\"args\":[{\"name\":\"_\",\"value\":{\"type\":\"Number\",\"unit\":\"\",\"value\":\"5\"}},{\"name\":\"label\",\"value\":{\"type\":\"Text\",\"value\":\"hi\"}}],\"name\":\"Other\",\"tags\":[]}}}],\"name\":\"Done\",\"tags\":[\"ready\"]},\"version\":1}"
  roundTripJson: true
```

### Expectation

```yaml
gesBlock: expect
message:
  json: "{\"message\":{\"args\":[{\"name\":\"value\",\"value\":{\"type\":\"Message\",\"value\":{\"args\":[{\"name\":\"_\",\"value\":{\"type\":\"Number\",\"unit\":\"\",\"value\":\"5\"}},{\"name\":\"label\",\"value\":{\"type\":\"Text\",\"value\":\"hi\"}}],\"name\":\"Other\",\"tags\":[]}}}],\"name\":\"Done\",\"tags\":[\"ready\"]},\"version\":1}"
  messageSignatureId: "Done(value)"
  argumentCount: 1
```

---

## Test: ordered-arguments

This case verifies ordered arguments.

### Case description

```yaml
gesBlock: case
id: "ordered-arguments"
messageApi:
  signature:
    name: "Done"
    parameters:
      - "value"
  message:
    name: "Done"
    args: []
  json: "{\"message\":{\"args\":[{\"name\":\"z\",\"value\":{\"type\":\"Number\",\"unit\":\"\",\"value\":\"1\"}},{\"name\":\"_\",\"value\":{\"type\":\"Number\",\"unit\":\"\",\"value\":\"2\"}},{\"name\":\"a\",\"value\":{\"type\":\"Number\",\"unit\":\"\",\"value\":\"3\"}}],\"name\":\"Ordered\",\"tags\":[]},\"version\":1}"
  roundTripJson: true
```

### Expectation

```yaml
gesBlock: expect
message:
  json: "{\"message\":{\"args\":[{\"name\":\"z\",\"value\":{\"type\":\"Number\",\"unit\":\"\",\"value\":\"1\"}},{\"name\":\"_\",\"value\":{\"type\":\"Number\",\"unit\":\"\",\"value\":\"2\"}},{\"name\":\"a\",\"value\":{\"type\":\"Number\",\"unit\":\"\",\"value\":\"3\"}}],\"name\":\"Ordered\",\"tags\":[]},\"version\":1}"
  messageSignatureId: "Ordered(z,_,a)"
```

---

## Test: write-from-value

This case verifies write from value.

### Case description

```yaml
gesBlock: case
id: "write-from-value"
messageApi:
  signature:
    name: "Done"
    parameters:
      - "value"
  message:
    name: "Done"
    args:
      -
        name: "value"
        value:
          type: ":Quantity.int64"
          value: "10"
          unit: ":second"
    tags:
      - "ready"
  roundTripJson: true
```

### Expectation

```yaml
gesBlock: expect
message:
  json: "{\"message\":{\"args\":[{\"name\":\"value\",\"value\":{\"type\":\"Number\",\"unit\":\"s\",\"value\":\"10\"}}],\"name\":\"Done\",\"tags\":[\"ready\"]},\"version\":1}"
```

---

## Test: wrong-version

This case verifies wrong version.

### Case description

```yaml
gesBlock: case
id: "wrong-version"
messageApi:
  signature:
    name: "Done"
    parameters:
      - "value"
  message:
    name: "Done"
    args: []
  json: "{\"message\":{\"args\":[],\"name\":\"Done\",\"tags\":[]},\"version\":2}"
```

### Expectation

```yaml
gesBlock: expect
message:
  error: "message.unsupportedVersion"
```

---

## Test: duplicate-json-field

This case verifies duplicate json field.

### Case description

```yaml
gesBlock: case
id: "duplicate-json-field"
messageApi:
  signature:
    name: "Done"
    parameters:
      - "value"
  message:
    name: "Done"
    args: []
  json: "{\"version\":1,\"version\":1,\"message\":{\"name\":\"Done\",\"tags\":[],\"args\":[]}}"
```

### Expectation

```yaml
gesBlock: expect
message:
  error: "message.invalidJson"
```

---

## Test: trailing-json

This case verifies trailing json.

### Case description

```yaml
gesBlock: case
id: "trailing-json"
messageApi:
  signature:
    name: "Done"
    parameters:
      - "value"
  message:
    name: "Done"
    args: []
  json: "{\"message\":{\"args\":[{\"name\":\"value\",\"value\":{\"type\":\"Number\",\"unit\":\"\",\"value\":\"1\"}}],\"name\":\"Done\",\"tags\":[\"ready\"]},\"version\":1} false"
```

### Expectation

```yaml
gesBlock: expect
message:
  error: "message.invalidJson"
```

---

## Test: unknown-value-kind

This case verifies unknown value kind.

### Case description

```yaml
gesBlock: case
id: "unknown-value-kind"
messageApi:
  signature:
    name: "Done"
    parameters:
      - "value"
  message:
    name: "Done"
    args: []
  json: "{\"message\":{\"args\":[{\"name\":\"value\",\"value\":{\"name\":\"Hit\",\"type\":\"External\"}}],\"name\":\"Done\",\"tags\":[\"ready\"]},\"version\":1}"
```

### Expectation

```yaml
gesBlock: expect
message:
  error: "message.invalidValue"
```

---

## Test: unknown-series

This case verifies unknown series.

### Case description

```yaml
gesBlock: case
id: "unknown-series"
messageApi:
  signature:
    name: "Done"
    parameters:
      - "value"
  message:
    name: "Done"
    args: []
  json: "{\"message\":{\"args\":[{\"name\":\"value\",\"value\":{\"kind\":\"hostSeries\",\"offset\":\"0\",\"type\":\"Series\"}}],\"name\":\"Done\",\"tags\":[\"ready\"]},\"version\":1}"
```

### Expectation

```yaml
gesBlock: expect
message:
  error: "message.unsupportedSeries"
```

---

## Test: bad-number

This case verifies bad number.

### Case description

```yaml
gesBlock: case
id: "bad-number"
messageApi:
  signature:
    name: "Done"
    parameters:
      - "value"
  message:
    name: "Done"
    args: []
  json: "{\"message\":{\"args\":[{\"name\":\"value\",\"value\":{\"type\":\"Number\",\"unit\":\"\",\"value\":\"12oops\"}}],\"name\":\"Done\",\"tags\":[\"ready\"]},\"version\":1}"
```

### Expectation

```yaml
gesBlock: expect
message:
  error: "message.invalidValue"
```

---

## Test: bad-unit

This case verifies bad unit.

### Case description

```yaml
gesBlock: case
id: "bad-unit"
messageApi:
  signature:
    name: "Done"
    parameters:
      - "value"
  message:
    name: "Done"
    args: []
  json: "{\"message\":{\"args\":[{\"name\":\"value\",\"value\":{\"type\":\"Number\",\"unit\":\"fortnight\",\"value\":\"1\"}}],\"name\":\"Done\",\"tags\":[\"ready\"]},\"version\":1}"
```

### Expectation

```yaml
gesBlock: expect
message:
  error: "message.invalidValue"
```

---

## Test: embedded-number-unit

This case verifies embedded number unit.

### Case description

```yaml
gesBlock: case
id: "embedded-number-unit"
messageApi:
  signature:
    name: "Done"
    parameters:
      - "value"
  message:
    name: "Done"
    args: []
  json: "{\"message\":{\"args\":[{\"name\":\"value\",\"value\":{\"type\":\"Number\",\"unit\":\"s\",\"value\":\"1s\"}}],\"name\":\"Done\",\"tags\":[\"ready\"]},\"version\":1}"
```

### Expectation

```yaml
gesBlock: expect
message:
  error: "message.invalidValue"
```

---

## Test: bad-dice

This case verifies bad dice.

### Case description

```yaml
gesBlock: case
id: "bad-dice"
messageApi:
  signature:
    name: "Done"
    parameters:
      - "value"
  message:
    name: "Done"
    args: []
  json: "{\"message\":{\"args\":[{\"name\":\"value\",\"value\":{\"rolls\":[\"0\"],\"type\":\"Dice\"}}],\"name\":\"Done\",\"tags\":[\"ready\"]},\"version\":1}"
```

### Expectation

```yaml
gesBlock: expect
message:
  error: "message.invalidValue"
```

---

## Test: extra-property

This case verifies extra property.

### Case description

```yaml
gesBlock: case
id: "extra-property"
messageApi:
  signature:
    name: "Done"
    parameters:
      - "value"
  message:
    name: "Done"
    args: []
  json: "{\"message\":{\"args\":[{\"name\":\"value\",\"value\":{\"type\":\"Nothing\",\"value\":0}}],\"name\":\"Done\",\"tags\":[\"ready\"]},\"version\":1}"
```

### Expectation

```yaml
gesBlock: expect
message:
  error: "message.invalidValue"
```

---

## Test: bad-record-name

This case verifies bad record name.

### Case description

```yaml
gesBlock: case
id: "bad-record-name"
messageApi:
  signature:
    name: "Done"
    parameters:
      - "value"
  message:
    name: "Done"
    args: []
  json: "{\"message\":{\"args\":[{\"name\":\"value\",\"value\":{\"entries\":[],\"name\":\"bad\",\"type\":\"Record\"}}],\"name\":\"Done\",\"tags\":[\"ready\"]},\"version\":1}"
```

### Expectation

```yaml
gesBlock: expect
message:
  error: "message.invalidValue"
```

---

## Test: duplicate-map-key

This case verifies duplicate map key.

### Case description

```yaml
gesBlock: case
id: "duplicate-map-key"
messageApi:
  signature:
    name: "Done"
    parameters:
      - "value"
  message:
    name: "Done"
    args: []
  json: "{\"message\":{\"args\":[{\"name\":\"value\",\"value\":{\"entries\":[{\"key\":\"x\",\"value\":{\"type\":\"Number\",\"unit\":\"\",\"value\":\"1\"}},{\"key\":\"x\",\"value\":{\"type\":\"Number\",\"unit\":\"\",\"value\":\"2\"}}],\"type\":\"Map\"}}],\"name\":\"Done\",\"tags\":[\"ready\"]},\"version\":1}"
```

### Expectation

```yaml
gesBlock: expect
message:
  error: "message.invalidValue"
```

---

## Test: unpaired-surrogate

This case verifies unpaired surrogate.

### Case description

```yaml
gesBlock: case
id: "unpaired-surrogate"
messageApi:
  signature:
    name: "Done"
    parameters:
      - "value"
  message:
    name: "Done"
    args: []
  json: "{\"version\":1,\"message\":{\"name\":\"Done\",\"tags\":[],\"args\":[{\"name\":\"value\",\"value\":{\"type\":\"Text\",\"value\":\"\\uD800\"}}]}}"
```

### Expectation

```yaml
gesBlock: expect
message:
  error: "message.invalidJson"
```

---

## Test: depth-limit

This case verifies depth limit.

### Case description

```yaml
gesBlock: case
id: "depth-limit"
messageApi:
  signature:
    name: "Done"
    parameters:
      - "value"
  message:
    name: "Done"
    args: []
  json: "{\"message\":{\"args\":[{\"name\":\"value\",\"value\":{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"items\":[{\"type\":\"Nothing\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}],\"type\":\"List\"}}],\"name\":\"Done\",\"tags\":[\"ready\"]},\"version\":1}"
```

### Expectation

```yaml
gesBlock: expect
message:
  error: "message.resourceLimit"
```

---

## Test: number-percent-suffix

This case verifies number-percent-suffix.

### Case description

```yaml
gesBlock: case
id: "number-percent-suffix"
messageApi:
  signature:
    name: "Done"
    parameters:
      - "value"
  message:
    name: "Done"
    args: []
  json: "{\"version\":1,\"message\":{\"name\":\"Done\",\"args\":[{\"name\":\"value\",\"value\":{\"type\":\"Number\",\"unit\":\"\",\"value\":\"10%\"}}],\"tags\":[]}}"
  roundTripJson: true
```

### Expectation

```yaml
gesBlock: expect
message:
  error: "message.invalidValue"
```

---

## Test: percentage-infinite

This case verifies percentage-infinite.

### Case description

```yaml
gesBlock: case
id: "percentage-infinite"
messageApi:
  signature:
    name: "Done"
    parameters:
      - "value"
  message:
    name: "Done"
    args: []
  json: "{\"version\":1,\"message\":{\"name\":\"Done\",\"args\":[{\"name\":\"value\",\"value\":{\"type\":\"Percentage\",\"value\":\"Infinity\"}}],\"tags\":[]}}"
  roundTripJson: true
```

### Expectation

```yaml
gesBlock: expect
message:
  error: "message.invalidValue"
```

---

## Test: range-infinite

This case verifies range-infinite.

### Case description

```yaml
gesBlock: case
id: "range-infinite"
messageApi:
  signature:
    name: "Done"
    parameters:
      - "value"
  message:
    name: "Done"
    args: []
  json: "{\"version\":1,\"message\":{\"name\":\"Done\",\"args\":[{\"name\":\"value\",\"value\":{\"type\":\"Range\",\"from\":\"0\",\"to\":\"Infinity\",\"step\":\"1\"}}],\"tags\":[]}}"
  roundTripJson: true
```

### Expectation

```yaml
gesBlock: expect
message:
  error: "message.invalidValue"
```

---

## Test: handler-empty

This case verifies handler-empty.

### Case description

```yaml
gesBlock: case
id: "handler-empty"
messageApi:
  signature:
    name: "Done"
    parameters:
      - "value"
  message:
    name: "Done"
    args: []
  json: "{\"version\":1,\"message\":{\"name\":\"Done\",\"args\":[{\"name\":\"value\",\"value\":{\"type\":\"Handler\",\"name\":\"\",\"labels\":[]}}],\"tags\":[]}}"
  roundTripJson: true
```

### Expectation

```yaml
gesBlock: expect
message:
  error: "message.invalidValue"
```
