---
formatVersion: 1
suiteId: "runtime.host-dispatch"
title: "RuntimeHostDispatch"
categories: [conformance]
tags: [migrated-json-v1]
---

# RuntimeHostDispatch

Mechanically migrated from the former JSON conformance corpus.

## Test: published messages route to external subscribers

```yaml
gesBlock: case
id: case-0001
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
nativeHandlers:
  - message: "Notify"
    parameters: ["playerId", "count"]
    emit:
      - name: "ExternalSeen"
        forwardArguments: true
sources:
  - name: "published messages route to external subscribers.ges"
    program: main
```

```ges
on Start(playerId) {
  emit Notify(playerId: playerId, count: 3)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args:
        - name: "playerId"
          value:
            type: ":text"
            value: "p_1"
    local:
      - name: "Notify"
        args:
          - name: "playerId"
            value:
              type: ":text"
              value: "p_1"
          - name: "count"
            value:
              type: ":integer"
              value: "3"
      - name: "ExternalSeen"
        args:
          - name: "playerId"
            value:
              type: ":text"
              value: "p_1"
          - name: "count"
            value:
              type: ":integer"
              value: "3"
```

## Test: equal priority preserves script then external registration order

```yaml
gesBlock: case
id: case-0002
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
nativeHandlers:
  - message: "Notify"
    parameters: ["value"]
    emit:
      - name: "SeenByExternal"
        args:
          - name: "marker"
            value:
              type: ":text"
              value: "external"
sources:
  - name: "equal priority preserves script then external registration order.ges"
    program: main
```

```ges
on Start(value) {
  emit Notify(value: value)
}

on Notify(value) {
  emit SeenByScript(value: value + 1)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args:
        - name: "value"
          value:
            type: ":float"
            value: "4"
    local:
      - name: "Notify"
        args:
          - name: "value"
            value:
              type: ":float"
              value: "4"
      - name: "SeenByScript"
        args:
          - name: "value"
            value:
              type: ":integer"
              value: "5"
      - name: "SeenByExternal"
        args:
          - name: "marker"
            value:
              type: ":text"
              value: "external"
```

## Test: external subscribers keep registration order

```yaml
gesBlock: case
id: case-0003
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
nativeHandlers:
  - message: "Notify"
    parameters: ["value"]
    emit:
      - name: "ExternalFirst"
        args:
          - name: "order"
            value:
              type: ":integer"
              value: "1"
  - message: "Notify"
    parameters: ["value"]
    emit:
      - name: "ExternalSecond"
        args:
          - name: "order"
            value:
              type: ":integer"
              value: "2"
sources:
  - name: "external subscribers keep registration order.ges"
    program: main
```

```ges
on Start(value) {
  emit Notify(value: value)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args:
        - name: "value"
          value:
            type: ":float"
            value: "4"
    local:
      - name: "Notify"
        args:
          - name: "value"
            value:
              type: ":float"
              value: "4"
      - name: "ExternalFirst"
        args:
          - name: "order"
            value:
              type: ":integer"
              value: "1"
      - name: "ExternalSecond"
        args:
          - name: "order"
            value:
              type: ":integer"
              value: "2"
```

## Test: higher priority subscribers run first

```yaml
gesBlock: case
id: case-0004
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
nativeHandlers:
  - message: "Notify"
    parameters: ["value"]
    priority: 10
    emit:
      - name: "SeenByExternal"
        args:
          - name: "marker"
            value:
              type: ":text"
              value: "external"
sources:
  - name: "higher priority subscribers run first.ges"
    program: main
```

```ges
on Start(value) {
  emit Notify(value: value)
}

on Notify(value) {
  emit SeenByScript(value: value + 1)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args:
        - name: "value"
          value:
            type: ":float"
            value: "4"
    local:
      - name: "Notify"
        args:
          - name: "value"
            value:
              type: ":float"
              value: "4"
      - name: "SeenByExternal"
        args:
          - name: "marker"
            value:
              type: ":text"
              value: "external"
      - name: "SeenByScript"
        args:
          - name: "value"
            value:
              type: ":integer"
              value: "5"
```

## Test: failing external subscribers are ignored and later subscribers continue

```yaml
gesBlock: case
id: case-0005
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
nativeHandlers:
  - message: "Notify"
    parameters: ["value"]
    throw: true
  - message: "Notify"
    parameters: ["value"]
    emit:
      - name: "ExternalOk"
        forwardArguments: true
sources:
  - name: "failing external subscribers are ignored and later subscribers continue.ges"
    program: main
```

```ges
on Start(value) {
  emit Notify(value: value)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args:
        - name: "value"
          value:
            type: ":float"
            value: "4"
    local:
      - name: "Notify"
        args:
          - name: "value"
            value:
              type: ":float"
              value: "4"
      - name: "ExternalOk"
        args:
          - name: "value"
            value:
              type: ":float"
              value: "4"
    diagnostics:
      - phase: "runtime"
        code: "runtime.nativeHandlerFailure"
        handlerName: "Notify(value)"
```

## Test: external subscribers emit follow up messages through same run

```yaml
gesBlock: case
id: case-0006
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
nativeHandlers:
  - message: "Notify"
    parameters: ["value"]
    emit:
      - name: "Done"
        forwardArguments: true
sources:
  - name: "external subscribers emit follow up messages through same run.ges"
    program: main
```

```ges
on Start(value) {
  emit Notify(value: value)
}

on Done(value) {
  emit Final(value: value)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args:
        - name: "value"
          value:
            type: ":float"
            value: "2"
    local:
      - name: "Notify"
        args:
          - name: "value"
            value:
              type: ":float"
              value: "2"
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":float"
              value: "2"
      - name: "Final"
        args:
          - name: "value"
            value:
              type: ":float"
              value: "2"
```

## Test: multiple loaded programs dispatch in load order

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
  - name: "first.ges"
    program: program-0001
  - name: "second.ges"
    program: program-0002
```

```ges
module FirstProgram
on Start { emit First }
```

```ges
module SecondProgram
on Start { emit Second }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args: []
    local:
      - name: "First"
        args: []
      - name: "Second"
        args: []
```

## Test: initialization runs once for every loaded program instance

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
  - name: "first-init.ges"
    program: program-0001
  - name: "second-init.ges"
    program: program-0002
```

```ges
on initialization { emit FirstReady }
```

```ges
on initialization { emit SecondReady }
```

```yaml
gesBlock: expect
initialization:
  local:
    - name: "FirstReady"
      args: []
    - name: "SecondReady"
      args: []
```

## Test: frame budget pauses and resumes one script handler

```yaml
gesBlock: case
id: case-0009
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "frame budget pauses and resumes one script handler.ges"
    program: main
```

```ges
on Start { for item from 1 to 3 emit Tick(value: item) }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | frames | 1 |

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args: []
    local:
      - name: "Tick"
        args:
          - name: "value"
            value:
              type: ":integer"
              value: "1"
      - name: "Tick"
        args:
          - name: "value"
            value:
              type: ":integer"
              value: "2"
      - name: "Tick"
        args:
          - name: "value"
            value:
              type: ":integer"
              value: "3"
    paused: true
```
