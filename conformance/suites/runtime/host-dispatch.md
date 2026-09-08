---
formatVersion: 1
suiteId: "runtime.host-dispatch"
title: "RuntimeHostDispatch"
categories: [conformance]
---

# RuntimeHostDispatch

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite verifies serial host dispatch, multiple loaded programs, initialization, priorities, and frame-resumed execution.

---

## Test: published messages route to external subscribers

This runtime case exercises “published messages route to external subscribers” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

```ges
on Start(playerId) {
  emit Notify(playerId: playerId, count: 3)
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
        - name: "playerId"
          value:
            type: ":Text"
            value: "p1"
    local:
      - name: "Notify"
        args:
          - name: "playerId"
            value:
              type: ":Text"
              value: "p1"
          - name: "count"
            value:
              type: ":Number.int64"
              value: "3"
      - name: "ExternalSeen"
        args:
          - name: "playerId"
            value:
              type: ":Text"
              value: "p1"
          - name: "count"
            value:
              type: ":Number.int64"
              value: "3"
```

---

## Test: equal priority preserves script then external registration order

This runtime case exercises “equal priority preserves script then external registration order” and verifies the declared messages, values, and execution result.

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
nativeHandlers:
  - message: "Notify"
    parameters: ["value"]
    emit:
      - name: "SeenByExternal"
        args:
          - name: "marker"
            value:
              type: ":Text"
              value: "external"
sources:
  - name: "equal priority preserves script then external registration order.ges"
    program: main
```

### Source code under test

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

### Expectation

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args:
        - name: "value"
          value:
            type: ":Number.binary64"
            value: "4"
    local:
      - name: "Notify"
        args:
          - name: "value"
            value:
              type: ":Number.int64"
              value: "4"
      - name: "SeenByScript"
        args:
          - name: "value"
            value:
              type: ":Number.int64"
              value: "5"
      - name: "SeenByExternal"
        args:
          - name: "marker"
            value:
              type: ":Text"
              value: "external"
```

---

## Test: external subscribers keep registration order

This runtime case exercises “external subscribers keep registration order” and verifies the declared messages, values, and execution result.

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
nativeHandlers:
  - message: "Notify"
    parameters: ["value"]
    emit:
      - name: "ExternalFirst"
        args:
          - name: "order"
            value:
              type: ":Number.int64"
              value: "1"
  - message: "Notify"
    parameters: ["value"]
    emit:
      - name: "ExternalSecond"
        args:
          - name: "order"
            value:
              type: ":Number.int64"
              value: "2"
sources:
  - name: "external subscribers keep registration order.ges"
    program: main
```

### Source code under test

```ges
on Start(value) {
  emit Notify(value: value)
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
            type: ":Number.binary64"
            value: "4"
    local:
      - name: "Notify"
        args:
          - name: "value"
            value:
              type: ":Number.int64"
              value: "4"
      - name: "ExternalFirst"
        args:
          - name: "order"
            value:
              type: ":Number.int64"
              value: "1"
      - name: "ExternalSecond"
        args:
          - name: "order"
            value:
              type: ":Number.int64"
              value: "2"
```

---

## Test: higher priority subscribers run first

This runtime case exercises “higher priority subscribers run first” and verifies the declared messages, values, and execution result.

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
nativeHandlers:
  - message: "Notify"
    parameters: ["value"]
    priority: 10
    emit:
      - name: "SeenByExternal"
        args:
          - name: "marker"
            value:
              type: ":Text"
              value: "external"
sources:
  - name: "higher priority subscribers run first.ges"
    program: main
```

### Source code under test

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

### Expectation

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args:
        - name: "value"
          value:
            type: ":Number.binary64"
            value: "4"
    local:
      - name: "Notify"
        args:
          - name: "value"
            value:
              type: ":Number.int64"
              value: "4"
      - name: "SeenByExternal"
        args:
          - name: "marker"
            value:
              type: ":Text"
              value: "external"
      - name: "SeenByScript"
        args:
          - name: "value"
            value:
              type: ":Number.int64"
              value: "5"
```

---

## Test: failing external subscribers are ignored and later subscribers continue

This runtime case exercises “failing external subscribers are ignored and later subscribers continue” and verifies the declared messages, values, and execution result.

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

### Source code under test

```ges
on Start(value) {
  emit Notify(value: value)
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
            type: ":Number.binary64"
            value: "4"
    local:
      - name: "Notify"
        args:
          - name: "value"
            value:
              type: ":Number.int64"
              value: "4"
      - name: "ExternalOk"
        args:
          - name: "value"
            value:
              type: ":Number.int64"
              value: "4"
    diagnostics:
      - phase: "runtime"
        code: "runtime.nativeHandlerFailure"
        handlerName: "Notify(value)"
```

---

## Test: external subscribers emit follow up messages through same run

This runtime case exercises “external subscribers emit follow up messages through same run” and verifies the declared messages, values, and execution result.

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

### Source code under test

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

### Expectation

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args:
        - name: "value"
          value:
            type: ":Number.binary64"
            value: "2"
    local:
      - name: "Notify"
        args:
          - name: "value"
            value:
              type: ":Number.int64"
              value: "2"
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":Number.int64"
              value: "2"
      - name: "Final"
        args:
          - name: "value"
            value:
              type: ":Number.int64"
              value: "2"
```

---

## Test: multiple loaded programs dispatch in load order

This runtime case exercises “multiple loaded programs dispatch in load order” and verifies the declared messages, values, and execution result.

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
  - name: "first.ges"
    program: program-0001
  - name: "second.ges"
    program: program-0002
```

### Source code under test

```ges
module firstprogram
on Start { emit First }
```

```ges
module secondprogram
on Start { emit Second }
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
      - name: "First"
        args: []
      - name: "Second"
        args: []
```

---

## Test: initialization runs once for every loaded program instance

This runtime case exercises “initialization runs once for every loaded program instance” and verifies the declared messages, values, and execution result.

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
  - name: "first-init.ges"
    program: program-0001
  - name: "second-init.ges"
    program: program-0002
```

### Source code under test

```ges
on initialization { emit FirstReady }
```

```ges
on initialization { emit SecondReady }
```

### Expectation

```yaml
gesBlock: expect
initialization:
  local:
    - name: "FirstReady"
      args: []
    - name: "SecondReady"
      args: []
```

---

## Test: frame budget pauses and resumes one script handler

This runtime case exercises “frame budget pauses and resumes one script handler” and verifies the declared messages, values, and execution result.

### Case description

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

### Source code under test

```ges
on Start { for item from 1 to 3 emit Tick(value: item) }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | frames | 1 |

### Expectation

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
              type: ":Number.int64"
              value: "1"
      - name: "Tick"
        args:
          - name: "value"
            value:
              type: ":Number.int64"
              value: "2"
      - name: "Tick"
        args:
          - name: "value"
            value:
              type: ":Number.int64"
              value: "3"
    paused: true
```
