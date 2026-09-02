---
formatVersion: 1
suiteId: "api.messages"
title: "ApiMessages"
categories: [conformance]
tags: [migrated-json-v1]
---

# ApiMessages

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite verifies portable message signatures, ordered arguments, handler identities, message construction, and equality semantics.

---

## Test: message signatures are ordered

This public API case exercises “message signatures are ordered” and verifies its language-neutral result.

### Case description

```yaml
gesBlock: case
id: case-0001
kind: messageApi
level: scenario
messageApi:
  signature:
    name: "Start"
    parameters: ["a", "b"]
  message:
    name: "Start"
    args:
      - name: "b"
        value:
          type: ":integer"
          value: "2"
      - name: "a"
        value:
          type: ":integer"
          value: "1"
```

### Expectation

```yaml
gesBlock: expect
message:
  name: "Start"
  signatureId: "Start(a,b)"
  messageSignatureId: "Start(b,a)"
  matches: false
  argumentCount: 2
```

---

## Test: signature ids trim message names preserve parameter order and include name

This public API case exercises “signature ids trim message names preserve parameter order and include name” and verifies its language-neutral result.

### Case description

```yaml
gesBlock: case
id: case-0002
kind: messageApi
level: scenario
messageApi:
  signature:
    name: " Shoot "
    parameters: ["unit", "target"]
  message:
    name: " Shoot "
    args:
      - name: "target"
        value:
          type: ":integer"
          value: "2"
      - name: "unit"
        value:
          type: ":integer"
          value: "1"
```

### Expectation

```yaml
gesBlock: expect
message:
  name: "Shoot"
  signatureId: "Shoot(unit,target)"
  messageSignatureId: "Shoot(target,unit)"
  matches: false
  argumentCount: 2
```

---

## Test: signature id includes the message name

This public API case exercises “signature id includes the message name” and verifies its language-neutral result.

### Case description

```yaml
gesBlock: case
id: case-0003
kind: messageApi
level: scenario
messageApi:
  signature:
    name: "Run"
    parameters: ["target", "unit"]
  message:
    name: "Run"
    args:
      - name: "target"
        value:
          type: ":integer"
          value: "2"
      - name: "unit"
        value:
          type: ":integer"
          value: "1"
```

### Expectation

```yaml
gesBlock: expect
message:
  name: "Run"
  signatureId: "Run(target,unit)"
  messageSignatureId: "Run(target,unit)"
  matches: true
  argumentCount: 2
```

---

## Test: empty ordered arguments produce an empty signature

This public API case exercises “empty ordered arguments produce an empty signature” and verifies its language-neutral result.

### Case description

```yaml
gesBlock: case
id: case-0004
kind: messageApi
level: scenario
messageApi:
  signature:
    name: "Ready"
    parameters: []
  message:
    name: "Ready"
    args: []
```

### Expectation

```yaml
gesBlock: expect
message:
  signatureId: "Ready()"
  messageSignatureId: "Ready()"
  matches: true
  argumentCount: 0
```

---

## Test: duplicate normalized message argument names are rejected

This public API case exercises “duplicate normalized message argument names are rejected” and verifies its language-neutral result.

### Case description

```yaml
gesBlock: case
id: case-0005
kind: messageApi
level: scenario
messageApi:
  signature:
    name: "Score"
    parameters: ["score"]
  message:
    name: "Score"
    args:
      - name: "score"
        value:
          type: ":integer"
          value: "1"
      - name: " score "
        value:
          type: ":integer"
          value: "2"
```

### Expectation

```yaml
gesBlock: expect
message:
  error: "duplicateArgumentName"
```

---

## Test: message argument objects are rejected because order must be explicit

This public API case exercises “message argument objects are rejected because order must be explicit” and verifies its language-neutral result.

### Case description

```yaml
gesBlock: case
id: case-0006
kind: messageApi
level: scenario
messageApi:
  signature:
    name: "Score"
    parameters: ["score"]
  message:
    name: "Score"
    args:
      score:
        type: ":integer"
        value: "1"
```

### Expectation

```yaml
gesBlock: expect
message:
  error: "invalidArgumentsShape"
```

---

## Test: compiled scripts expose message definitions

This compiler case exercises “compiled scripts expose message definitions” and verifies the declared portable output.

### Case description

```yaml
gesBlock: case
id: case-0007
kind: compileMetadata
level: scenario
sources:
  - name: "compiled scripts expose message definitions.ges"
    program: main
```

### Source code under test

```ges
module MessageDefinitions
on Start(playerId) {
  emit Done(playerId: playerId)
}
```

### Expectation

```yaml
gesBlock: expect
metadata:
  messageDefinitions:
    - name: "Start"
      signatureIds: ["Start(playerId)"]
      count: 1
```

---

## Test: repeated unlabeled arguments remain positional

This public API case exercises “repeated unlabeled arguments remain positional” and verifies its language-neutral result.

### Case description

```yaml
gesBlock: case
id: case-0008
kind: messageApi
level: atomic
messageApi:
  signature:
    name: Pair
    parameters: ["_", "_"]
  message:
    name: Pair
    args:
      - name: "_"
        value: { type: ":integer", value: "1" }
      - name: "_"
        value: { type: ":integer", value: "2" }
```

### Expectation

```yaml
gesBlock: expect
message:
  signatureId: "Pair(_,_)"
  messageSignatureId: "Pair(_,_)"
  matches: true
  argumentCount: 2
```

---

## Test: non ASCII message names are rejected

This public API case exercises “non ASCII message names are rejected” and verifies its language-neutral result.

### Case description

```yaml
gesBlock: case
id: case-0009
kind: messageApi
level: atomic
messageApi:
  signature:
    name: "Pïng"
    parameters: []
  message:
    name: Ping
    args: []
```

### Expectation

```yaml
gesBlock: expect
message:
  error: invalidMessage
```

---

## Test: non ASCII parameter names are rejected

This public API case exercises “non ASCII parameter names are rejected” and verifies its language-neutral result.

### Case description

```yaml
gesBlock: case
id: case-0010
kind: messageApi
level: atomic
messageApi:
  signature:
    name: Ping
    parameters: ["ämount"]
  message:
    name: Ping
    args: []
```

### Expectation

```yaml
gesBlock: expect
message:
  error: invalidMessage
```

---

## Test: non ASCII tags are rejected

This public API case exercises “non ASCII tags are rejected” and verifies its language-neutral result.

### Case description

```yaml
gesBlock: case
id: case-0011
kind: messageApi
level: atomic
messageApi:
  signature:
    name: Ping
    parameters: []
  message:
    name: Ping
    tags: ["#réady"]
    args: []
```

### Expectation

```yaml
gesBlock: expect
message:
  error: invalidMessage
```

---

## Test: non ASCII whitespace is not trimmed from public names

This public API case exercises “non ASCII whitespace is not trimmed from public names” and verifies its language-neutral result.

### Case description

```yaml
gesBlock: case
id: case-0012
kind: messageApi
level: atomic
messageApi:
  signature:
    name: " Ping "
    parameters: []
  message:
    name: Ping
    args: []
```

### Expectation

```yaml
gesBlock: expect
message:
  error: invalidMessage
```

---

## Test: normalized signatures compare by signature identity

This public API case exercises “normalized signatures compare by signature identity” and verifies its language-neutral result.

### Case description

```yaml
gesBlock: case
id: signature-equality
kind: messageApi
level: atomic
messageApi:
  signature: { name: " Ping ", parameters: [" amount "] }
  message:
    name: Ping
    args:
      - name: amount
        value: { type: ":integer", value: "7" }
  compareSignature: { name: Ping, parameters: [amount] }
```

### Expectation

```yaml
gesBlock: expect
message:
  signatureEquals: true
  signatureHashEquals: true
```

---

## Test: signatures with different parameters are unequal

This public API case exercises “signatures with different parameters are unequal” and verifies its language-neutral result.

### Case description

```yaml
gesBlock: case
id: signature-parameter-inequality
kind: messageApi
level: atomic
messageApi:
  signature: { name: Ping, parameters: [amount] }
  message: { name: Ping, args: [] }
  compareSignature: { name: Ping, parameters: [value] }
```

### Expectation

```yaml
gesBlock: expect
message:
  signatureEquals: false
```

---

## Test: signatures with different arity are unequal

This public API case exercises “signatures with different arity are unequal” and verifies its language-neutral result.

### Case description

```yaml
gesBlock: case
id: signature-arity-inequality
kind: messageApi
level: atomic
messageApi:
  signature: { name: Ping, parameters: [amount] }
  message: { name: Ping, args: [] }
  compareSignature: { name: Ping, parameters: [amount, kind] }
```

### Expectation

```yaml
gesBlock: expect
message:
  signatureEquals: false
```

---

## Test: equal messages include arguments and normalized tags

This public API case exercises “equal messages include arguments and normalized tags” and verifies its language-neutral result.

### Case description

```yaml
gesBlock: case
id: message-equality
kind: messageApi
level: atomic
messageApi:
  signature: { name: Ping, parameters: [amount] }
  message:
    name: Ping
    tags: [radio]
    args:
      - name: amount
        value: { type: ":integer", value: "7" }
  compareMessage:
    name: Ping
    tags: ["#radio"]
    args:
      - name: amount
        value: { type: ":integer", value: "7" }
```

### Expectation

```yaml
gesBlock: expect
message:
  messageEquals: true
  messageHashEquals: true
```

---

## Test: messages with different argument values are unequal

This public API case exercises “messages with different argument values are unequal” and verifies its language-neutral result.

### Case description

```yaml
gesBlock: case
id: message-argument-inequality
kind: messageApi
level: atomic
messageApi:
  signature: { name: Ping, parameters: [amount] }
  message:
    name: Ping
    args:
      - name: amount
        value: { type: ":integer", value: "7" }
  compareMessage:
    name: Ping
    args:
      - name: amount
        value: { type: ":integer", value: "8" }
```

### Expectation

```yaml
gesBlock: expect
message:
  messageEquals: false
```

---

## Test: messages with different tags are unequal

This public API case exercises “messages with different tags are unequal” and verifies its language-neutral result.

### Case description

```yaml
gesBlock: case
id: message-tag-inequality
kind: messageApi
level: atomic
messageApi:
  signature: { name: Ping, parameters: [amount] }
  message:
    name: Ping
    tags: [radio]
    args:
      - name: amount
        value: { type: ":integer", value: "7" }
  compareMessage:
    name: Ping
    tags: [silent]
    args:
      - name: amount
        value: { type: ":integer", value: "7" }
```

### Expectation

```yaml
gesBlock: expect
message:
  messageEquals: false
```

---

## Test: unlabeled messages compare by positional value

This public API case exercises “unlabeled messages compare by positional value” and verifies its language-neutral result.

### Case description

```yaml
gesBlock: case
id: unlabeled-message-equality
kind: messageApi
level: atomic
messageApi:
  signature: { name: Ping, parameters: ["_"] }
  message:
    name: Ping
    args:
      - name: "_"
        value: { type: ":integer", value: "7" }
  compareMessage:
    name: Ping
    args:
      - name: "_"
        value: { type: ":integer", value: "7" }
```

### Expectation

```yaml
gesBlock: expect
message:
  messageEquals: true
  messageHashEquals: true
```

---

## Test: handler values compare by signature

This public API case exercises “handler values compare by signature” and verifies its language-neutral result.

### Case description

```yaml
gesBlock: case
id: handler-equality
kind: messageApi
level: atomic
messageApi:
  signature: { name: Ping, parameters: [amount] }
  message: { name: Ping, args: [] }
  compareHandler: { name: Ping, parameters: [amount] }
```

### Expectation

```yaml
gesBlock: expect
message:
  handlerEquals: true
  handlerHashEquals: true
```

---

## Test: handler values with different signatures are unequal

This public API case exercises “handler values with different signatures are unequal” and verifies its language-neutral result.

### Case description

```yaml
gesBlock: case
id: handler-inequality
kind: messageApi
level: atomic
messageApi:
  signature: { name: Ping, parameters: [amount] }
  message: { name: Ping, args: [] }
  compareHandler: { name: Ping, parameters: [value] }
```

### Expectation

```yaml
gesBlock: expect
message:
  handlerEquals: false
```

---

## Test: a signature creates a normal message

This public API case exercises “a signature creates a normal message” and verifies its language-neutral result.

### Case description

```yaml
gesBlock: case
id: signature-create-message
kind: messageApi
level: atomic
messageApi:
  signature: { name: Ping, parameters: [amount] }
  message: { name: Ping, args: [] }
  createArguments:
    - { type: ":integer", value: "7" }
```

### Expectation

```yaml
gesBlock: expect
message:
  createdMessageSignatureId: "Ping(amount)"
```
