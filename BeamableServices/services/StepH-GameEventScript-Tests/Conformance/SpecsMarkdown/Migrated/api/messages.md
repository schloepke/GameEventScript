---
formatVersion: 1
suiteId: "api.messages"
title: "ApiMessages"
categories: [conformance]
tags: [migrated-json-v1]
---

# ApiMessages

Mechanically migrated from the former JSON conformance corpus.

## Test: message signatures are ordered

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

```yaml
gesBlock: expect
message:
  name: "Start"
  signatureId: "Start(a,b)"
  messageSignatureId: "Start(b,a)"
  matches: false
  argumentCount: 2
```

## Test: signature ids trim message names preserve parameter order and include name

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

```yaml
gesBlock: expect
message:
  name: "Shoot"
  signatureId: "Shoot(unit,target)"
  messageSignatureId: "Shoot(target,unit)"
  matches: false
  argumentCount: 2
```

## Test: signature id includes the message name

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

```yaml
gesBlock: expect
message:
  name: "Run"
  signatureId: "Run(target,unit)"
  messageSignatureId: "Run(target,unit)"
  matches: true
  argumentCount: 2
```

## Test: empty ordered arguments produce an empty signature

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

```yaml
gesBlock: expect
message:
  signatureId: "Ready()"
  messageSignatureId: "Ready()"
  matches: true
  argumentCount: 0
```

## Test: duplicate normalized message argument names are rejected

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

```yaml
gesBlock: expect
message:
  error: "duplicateArgumentName"
```

## Test: message argument objects are rejected because order must be explicit

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

```yaml
gesBlock: expect
message:
  error: "invalidArgumentsShape"
```

## Test: compiled scripts expose message definitions

```yaml
gesBlock: case
id: case-0007
kind: compileMetadata
level: scenario
sources:
  - name: "compiled scripts expose message definitions.ges"
    program: main
```

```ges
module MessageDefinitions
on Start(playerId) {
  emit Done(playerId: playerId)
}
```

```yaml
gesBlock: expect
metadata:
  messageDefinitions:
    - name: "Start"
      signatureIds: ["Start(playerId)"]
      count: 1
```
