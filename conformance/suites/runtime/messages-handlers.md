---
formatVersion: 1
suiteId: "runtime.messages-handlers"
title: "RuntimeMessagesHandlers"
categories: [conformance]
---

# RuntimeMessagesHandlers

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite verifies message construction, handler selection, argument binding, tags, and undeliverable behavior.

---

## Test: handler binding creates message values and publishable calls

This runtime case exercises “handler binding creates message values and publishable calls” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0001
kind: scriptApi
level: scenario
compile:
  binaryRoundTrip: true
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "handler binding creates message values and publishable calls.ges"
    program: main
```

### Source code under test

```ges
module messagebinding
on Start(unit, target) {
  let shoot be (Shoot(unit, target)) as :Handler
  let msg be (shoot(unit: unit, target: target)) as :Message
  let isHandler be shoot is :Handler
  let isMessage be msg is :Message
  emit msg
  emit shoot(unit: unit, target: target)
  emit Shoot(unit: unit, target: target)
  emit Done(isHandler: isHandler, isMessage: isMessage)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args:
        - name: "unit"
          value:
            type: ":Text"
            value: "u1"
        - name: "target"
          value:
            type: ":Text"
            value: "t1"
    local:
      - name: "Shoot"
        args:
          - name: "unit"
            value:
              type: ":Text"
              value: "u1"
          - name: "target"
            value:
              type: ":Text"
              value: "t1"
      - name: "Shoot"
        args:
          - name: "unit"
            value:
              type: ":Text"
              value: "u1"
          - name: "target"
            value:
              type: ":Text"
              value: "t1"
      - name: "Shoot"
        args:
          - name: "unit"
            value:
              type: ":Text"
              value: "u1"
          - name: "target"
            value:
              type: ":Text"
              value: "t1"
      - name: "Done"
        args:
          - name: "isHandler"
            value:
              type: ":Boolean"
              value: true
          - name: "isMessage"
            value:
              type: ":Boolean"
              value: true
```

---

## Test: direct message literal creates first class message value

This runtime case exercises “direct message literal creates first class message value” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0002
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "direct message literal creates first class message value.ges"
    program: main
```

### Source code under test

```ges
module directmessageliteral
on Start(value) {
  let scaled be value * 2
  let myMessageDirect be Success(message: 'world', value: scaled)
  emit Done(isMessage: myMessageDirect is :Message, name: myMessageDirect.name, signature: myMessageDirect.signature, text: myMessageDirect.arguments.message, value: myMessageDirect.arguments.value)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args:
        - name: "value"
          value:
            type: ":Number.int64"
            value: "21"
    local:
      - name: "Done"
        args:
          - name: "isMessage"
            value:
              type: ":Boolean"
              value: true
          - name: "name"
            value:
              type: ":Text"
              value: "Success"
          - name: "signature"
            value:
              type: ":Text"
              value: "Success(message,value)"
          - name: "text"
            value:
              type: ":Text"
              value: "world"
          - name: "value"
            value:
              type: ":Number.int64"
              value: "42"
```

---

## Test: empty uppercase invocation creates handler value

This runtime case exercises “empty uppercase invocation creates handler value” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0003
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "empty uppercase invocation creates handler value.ges"
    program: main
```

### Source code under test

```ges
on Start {
  let handler be (Shoot()) as :Handler
  emit Done(name: handler.name, parameterCount: handler.parameters[:count])
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args: []
    local:
      - name: "Done"
        args:
          - name: "name"
            value:
              type: ":Text"
              value: "Shoot"
          - name: "parameterCount"
            value:
              type: ":Number.int64"
              value: "0"
```

---

## Test: untyped handler literal and bind create first class values

This runtime case exercises “untyped handler literal and bind create first class values” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0004
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "untyped handler literal and bind create first class values.ges"
    program: main
```

### Source code under test

```ges
module untypedhandlerbinding
on Start(success) {
  let myHandler be Success(message, value)
  let myMessage be myHandler(message: 'hello', value: success)
  let invalidMessage be myHandler(message: 'hello', other: success)
  emit Done(handlerIsHandler: myHandler is :Handler, handlerName: myHandler.name, handlerSignature: myHandler.signature, secondParameter: myHandler.parameters[2], messageIsMessage: myMessage is :Message, messageName: myMessage.name, messageSignature: myMessage.signature, text: myMessage.arguments.message, value: myMessage.arguments.value, invalidIsNothing: invalidMessage is nothing)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args:
        - name: "success"
          value:
            type: ":Boolean"
            value: true
    local:
      - name: "Done"
        args:
          - name: "handlerIsHandler"
            value:
              type: ":Boolean"
              value: true
          - name: "handlerName"
            value:
              type: ":Text"
              value: "Success"
          - name: "handlerSignature"
            value:
              type: ":Text"
              value: "Success(message,value)"
          - name: "secondParameter"
            value:
              type: ":Text"
              value: "value"
          - name: "messageIsMessage"
            value:
              type: ":Boolean"
              value: true
          - name: "messageName"
            value:
              type: ":Text"
              value: "Success"
          - name: "messageSignature"
            value:
              type: ":Text"
              value: "Success(message,value)"
          - name: "text"
            value:
              type: ":Text"
              value: "hello"
          - name: "value"
            value:
              type: ":Boolean"
              value: true
          - name: "invalidIsNothing"
            value:
              type: ":Boolean"
              value: true
```

---

## Test: invalid handler binding and non message emit are lenient no ops

This runtime case exercises “invalid handler binding and non message emit are lenient no ops” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0005
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "invalid handler binding and non message emit are lenient no ops.ges"
    program: main
```

### Source code under test

```ges
module messagebindingnoop
on Start(unit, hp) {
  let shoot be (Shoot(unit, target)) as :Handler
  let invalid be (shoot(unit: unit)) as :Message
  emit invalid
  emit unit
  emit Done(isMessage: invalid is :Message, isNothing: invalid is nothing)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args:
        - name: "unit"
          value:
            type: ":Text"
            value: "u1"
        - name: "hp"
          value:
            type: ":Number.int64"
            value: "10"
    local:
      - name: "Done"
        args:
          - name: "isMessage"
            value:
              type: ":Boolean"
              value: false
          - name: "isNothing"
            value:
              type: ":Boolean"
              value: true
```

---

## Test: message and handler values expose members without becoming dictionaries

This runtime case exercises “message and handler values expose members without becoming dictionaries” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0006
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "message and handler values expose members without becoming dictionaries.ges"
    program: main
```

### Source code under test

```ges
module messagefirstclass
on Start(unit, target) {
  let shoot be (Shoot(unit, target)) as :Handler
  let msg be (shoot(unit: unit, target: target)) as :Message
  emit Done(msgIsMessage: msg is :Message, msgIsMap: msg is :Map, handlerIsHandler: shoot is :Handler, handlerIsMap: shoot is :Map, msgName: msg.name, msgSignature: msg[#signature], handlerName: shoot.name, handlerParameterCount: shoot.parameters[:count], messageArgumentCount: msg.arguments[:count])
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args:
        - name: "unit"
          value:
            type: ":Text"
            value: "u1"
        - name: "target"
          value:
            type: ":Text"
            value: "t1"
    local:
      - name: "Done"
        args:
          - name: "msgIsMessage"
            value:
              type: ":Boolean"
              value: true
          - name: "msgIsMap"
            value:
              type: ":Boolean"
              value: false
          - name: "handlerIsHandler"
            value:
              type: ":Boolean"
              value: true
          - name: "handlerIsMap"
            value:
              type: ":Boolean"
              value: false
          - name: "msgName"
            value:
              type: ":Text"
              value: "Shoot"
          - name: "msgSignature"
            value:
              type: ":Text"
              value: "Shoot(unit,target)"
          - name: "handlerName"
            value:
              type: ":Text"
              value: "Shoot"
          - name: "handlerParameterCount"
            value:
              type: ":Number.int64"
              value: "2"
          - name: "messageArgumentCount"
            value:
              type: ":Number.int64"
              value: "2"
```

---

## Test: message values can be transported as published arguments

This runtime case exercises “message values can be transported as published arguments” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0007
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "message values can be transported as published arguments.ges"
    program: main
```

### Source code under test

```ges
on Start(unit, target) {
  let shoot be (Shoot(unit, target)) as :Handler
  let msg be (shoot(unit: unit, target: target)) as :Message
  emit Done(messageValue: msg)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args:
        - name: "unit"
          value:
            type: ":Text"
            value: "u1"
        - name: "target"
          value:
            type: ":Text"
            value: "t1"
    local:
      - name: "Done"
        args:
          - name: "messageValue"
            value:
              type: ":Message"
              message:
                name: "Shoot"
                args:
                  - name: "unit"
                    value:
                      type: ":Text"
                      value: "u1"
                  - name: "target"
                    value:
                      type: ":Text"
                      value: "t1"
```

---

## Test: initialization handler emits when session starts

This runtime case exercises “initialization handler emits when session starts” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0008
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "initialization handler emits when session starts.ges"
    program: main
```

### Source code under test

```ges
module runtimeinitialization

on initialization {
  emit Ready(value: 1)
}
```

### Expectation

```yaml
gesBlock: expect
initialization:
  local:
    - name: "Ready"
      args:
        - name: "value"
          value:
            type: ":Number.int64"
            value: "1"
```
