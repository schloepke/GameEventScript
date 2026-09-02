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
module MessageBinding
on Start(unit, target) {
  let shoot as :handler be Shoot(unit, target)
  let msg as :message be shoot(unit: unit, target: target)
  let isHandler be shoot is :handler
  let isMessage be msg is :message
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
            type: ":text"
            value: "u_1"
        - name: "target"
          value:
            type: ":text"
            value: "t_1"
    local:
      - name: "Shoot"
        args:
          - name: "unit"
            value:
              type: ":text"
              value: "u_1"
          - name: "target"
            value:
              type: ":text"
              value: "t_1"
      - name: "Shoot"
        args:
          - name: "unit"
            value:
              type: ":text"
              value: "u_1"
          - name: "target"
            value:
              type: ":text"
              value: "t_1"
      - name: "Shoot"
        args:
          - name: "unit"
            value:
              type: ":text"
              value: "u_1"
          - name: "target"
            value:
              type: ":text"
              value: "t_1"
      - name: "Done"
        args:
          - name: "isHandler"
            value:
              type: ":boolean"
              value: true
          - name: "isMessage"
            value:
              type: ":boolean"
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
module DirectMessageLiteral
on Start(value) {
  let scaled be value * 2
  let myMessageDirect be Success(message: 'world', value: scaled)
  emit Done(isMessage: myMessageDirect is :message, name: myMessageDirect.name, signature: myMessageDirect.signature, text: myMessageDirect.arguments.message, value: myMessageDirect.arguments.value)
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
            type: ":integer"
            value: "21"
    local:
      - name: "Done"
        args:
          - name: "isMessage"
            value:
              type: ":boolean"
              value: true
          - name: "name"
            value:
              type: ":text"
              value: "Success"
          - name: "signature"
            value:
              type: ":text"
              value: "Success(message,value)"
          - name: "text"
            value:
              type: ":text"
              value: "world"
          - name: "value"
            value:
              type: ":integer"
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
  let handler as :handler be Shoot()
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
              type: ":text"
              value: "Shoot"
          - name: "parameterCount"
            value:
              type: ":integer"
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
module UntypedHandlerBinding
on Start(success) {
  let myHandler be Success(message, value)
  let myMessage be myHandler(message: 'hello', value: success)
  let invalidMessage be myHandler(message: 'hello', other: success)
  emit Done(handlerIsHandler: myHandler is :handler, handlerName: myHandler.name, handlerSignature: myHandler.signature, secondParameter: myHandler.parameters[2], messageIsMessage: myMessage is :message, messageName: myMessage.name, messageSignature: myMessage.signature, text: myMessage.arguments.message, value: myMessage.arguments.value, invalidIsNothing: invalidMessage is nothing)
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
            type: ":boolean"
            value: true
    local:
      - name: "Done"
        args:
          - name: "handlerIsHandler"
            value:
              type: ":boolean"
              value: true
          - name: "handlerName"
            value:
              type: ":text"
              value: "Success"
          - name: "handlerSignature"
            value:
              type: ":text"
              value: "Success(message,value)"
          - name: "secondParameter"
            value:
              type: ":text"
              value: "value"
          - name: "messageIsMessage"
            value:
              type: ":boolean"
              value: true
          - name: "messageName"
            value:
              type: ":text"
              value: "Success"
          - name: "messageSignature"
            value:
              type: ":text"
              value: "Success(message,value)"
          - name: "text"
            value:
              type: ":text"
              value: "hello"
          - name: "value"
            value:
              type: ":boolean"
              value: true
          - name: "invalidIsNothing"
            value:
              type: ":boolean"
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
module MessageBindingNoOp
on Start(unit, hp) {
  let shoot as :handler be Shoot(unit, target)
  let invalid as :message be shoot(unit: unit, hp: hp)
  emit invalid
  emit unit
  emit Done(isMessage: invalid is :message, isNothing: invalid is nothing)
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
            type: ":text"
            value: "u_1"
        - name: "hp"
          value:
            type: ":integer"
            value: "10"
    local:
      - name: "Done"
        args:
          - name: "isMessage"
            value:
              type: ":boolean"
              value: false
          - name: "isNothing"
            value:
              type: ":boolean"
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
module MessageFirstClass
on Start(unit, target) {
  let shoot as :handler be Shoot(unit, target)
  let msg as :message be shoot(unit: unit, target: target)
  emit Done(msgIsMessage: msg is :message, msgIsMap: msg is :map, handlerIsHandler: shoot is :handler, handlerIsMap: shoot is :map, msgName: msg.name, msgSignature: msg[#signature], handlerName: shoot.name, handlerParameterCount: shoot.parameters[:count], messageArgumentCount: msg.arguments[:count])
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
            type: ":text"
            value: "u_1"
        - name: "target"
          value:
            type: ":text"
            value: "t_1"
    local:
      - name: "Done"
        args:
          - name: "msgIsMessage"
            value:
              type: ":boolean"
              value: true
          - name: "msgIsMap"
            value:
              type: ":boolean"
              value: false
          - name: "handlerIsHandler"
            value:
              type: ":boolean"
              value: true
          - name: "handlerIsMap"
            value:
              type: ":boolean"
              value: false
          - name: "msgName"
            value:
              type: ":text"
              value: "Shoot"
          - name: "msgSignature"
            value:
              type: ":text"
              value: "Shoot(unit,target)"
          - name: "handlerName"
            value:
              type: ":text"
              value: "Shoot"
          - name: "handlerParameterCount"
            value:
              type: ":integer"
              value: "2"
          - name: "messageArgumentCount"
            value:
              type: ":integer"
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
  let shoot as :handler be Shoot(unit, target)
  let msg as :message be shoot(unit: unit, target: target)
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
            type: ":text"
            value: "u_1"
        - name: "target"
          value:
            type: ":text"
            value: "t_1"
    local:
      - name: "Done"
        args:
          - name: "messageValue"
            value:
              type: ":message"
              message:
                name: "Shoot"
                args:
                  - name: "unit"
                    value:
                      type: ":text"
                      value: "u_1"
                  - name: "target"
                    value:
                      type: ":text"
                      value: "t_1"
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
module RuntimeInitialization

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
            type: ":integer"
            value: "1"
```
