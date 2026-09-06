---
formatVersion: 1
suiteId: runtime.host-lifecycle
title: Runtime host lifecycle
kind: scriptApi
level: scenario
categories: [conformance]
tags: [host, lifecycle]
---

# Runtime host lifecycle

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite verifies program instances, native subscriptions, detach operations, lifecycle snapshots, and per-handler runtime limits.

---

## Test: Native-only host dispatches messages

This runtime case exercises “Native-only host dispatches messages” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: native-only
nativeHandlers:
  - id: ping
    message: Ping
    parameters: [value]
    emit:
      - name: Pong
        forwardArguments: true
  - id: pong
    message: Pong
    parameters: [value]
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Ping | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: value
          value: { type: ":Number.int64", value: "7" }
    local:
      - name: Pong
        args:
          - name: value
            value: { type: ":Number.int64", value: "7" }
```

---

## Test: One immutable program runs in multiple hosts

This runtime case exercises “One immutable program runs in multiple hosts” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: shared-program-multiple-hosts
hostCount: 2
```

### Source code under test

```ges
on Start(value) { emit Done(value: value + 1) }
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
      args:
        - name: value
          value: { type: ":Number.int64", value: "4" }
    local:
      - name: Done
        args:
          - name: value
            value: { type: ":Number.int64", value: "5" }
```

---

## Test: Detach and unsubscribe retain the queued dispatch snapshot

This runtime case exercises “Detach and unsubscribe retain the queued dispatch snapshot” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: detach-unsubscribe-snapshot
nativeHandlers:
  - id: controller
    message: Trigger
    priority: 10
    actions:
      - detachProgram: main
      - unsubscribeHandler: controller
      - unsubscribeHandler: target
    emit:
      - name: ControllerSeen
        args: []
  - id: target
    message: Trigger
    emit:
      - name: NativeSeen
        args: []
```

### Source code under test

```ges
on Trigger() { emit ScriptSeen() }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| current-snapshot | Trigger | completion | |
| after-removal | Trigger | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  current-snapshot:
    local:
      - name: ControllerSeen
      - name: ScriptSeen
      - name: NativeSeen
  after-removal:
    accepted: false
```

---

## Test: Load and subscribe become visible after the current dispatch

This runtime case exercises “Load and subscribe become visible after the current dispatch” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: load-subscribe-after-dispatch
deferredPrograms: [late]
nativeHandlers:
  - id: controller
    message: Trigger
    priority: 10
    actions:
      - loadProgram: late
      - subscribeHandler: late-native
      - unsubscribeHandler: controller
  - id: late-native
    message: Trigger
    initiallySubscribed: false
    emit:
      - name: NativeSeen
        args: []
sources:
  - name: late.ges
    program: late
```

### Source code under test

```ges
on initialization { emit Initialized() }
on Trigger() { emit ScriptSeen() }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| install | Trigger | completion | |
| visible | Trigger | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  install:
    local:
      - name: Initialized
  visible:
    local:
      - name: ScriptSeen
      - name: NativeSeen
```

---

## Test: VM state resets between message handlers

This runtime case exercises “VM state resets between message handlers” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: vm-reset-between-handlers
```

### Source code under test

```ges
on First(value) {
  let firstLocal be value + 100
  emit Seen(value: firstLocal)
}

on Second(value) {
  let secondLocal be value + 1
  emit Seen(value: secondLocal)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| first | First | completion | |
| second | Second | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  first:
    input:
      args:
        - name: value
          value: { type: ":Number.int64", value: "1" }
    local:
      - name: Seen
        args:
          - name: value
            value: { type: ":Number.int64", value: "101" }
  second:
    input:
      args:
        - name: value
          value: { type: ":Number.int64", value: "1" }
    local:
      - name: Seen
        args:
          - name: value
            value: { type: ":Number.int64", value: "2" }
```

---

## Test: Runtime limits reset for every script handler

This runtime case exercises “Runtime limits reset for every script handler” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: runtime-limits-per-handler
runtimeLimits:
  maxLoopIterations: 2
sources:
  - name: first.ges
    program: first
  - name: second.ges
    program: second
```

### Source code under test

```ges
on Start() { for item from 1 to 2 emit Tick(value: item) }
```

```ges
on Start() { for item from 1 to 2 emit Tick(value: item + 2) }
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
    local:
      - name: Tick
        args:
          - name: value
            value: { type: ":Number.int64", value: "1" }
      - name: Tick
        args:
          - name: value
            value: { type: ":Number.int64", value: "2" }
      - name: Tick
        args:
          - name: value
            value: { type: ":Number.int64", value: "3" }
      - name: Tick
        args:
          - name: value
            value: { type: ":Number.int64", value: "4" }
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: Initialization is queued between older and newer messages

This runtime case exercises “Initialization is queued between older and newer messages” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: initialization-queue-order
deferredPrograms: [initializing]
stepActions:
  load:
    - loadProgram: initializing
nativeHandlers:
  - id: before
    message: Before
    parameters: []
    emit:
      - name: BeforeSeen
        args: []
  - id: after
    message: After
    parameters: []
    emit:
      - name: AfterSeen
        args: []
sources:
  - name: initializing.ges
    program: initializing
```

### Source code under test

```ges
on initialization { emit Ready() }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| queued | Before | enqueue | |
| load | After | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  queued:
    local: []
  load:
    local:
      - name: BeforeSeen
      - name: Ready
      - name: AfterSeen
```

---

## Test: Loading while a script is paused preserves its VM state

This runtime case exercises “Loading while a script is paused preserves its VM state” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: load-while-paused
deferredPrograms: [late]
stepActions:
  finish:
    - loadProgram: late
sources:
  - name: active.ges
    program: active
  - name: late.ges
    program: late
```

### Source code under test

```ges
on Start() {
  let value be 1 + 2 + 3
  emit First(value: value)
}
```

```ges
on Next() { emit Second() }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| pause | Start | frame | 1 |
| finish | Next | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  pause:
    paused: true
    local: []
  finish:
    local:
      - name: First
        args:
          - name: value
            value: { type: ":Number.int64", value: "6" }
      - name: Second
```

---

## Test: Native message-name subscriptions match every signature

This runtime case exercises “Native message-name subscriptions match every signature” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: native-message-name-subscription
nativeHandlers:
  - id: ping
    message: Ping
    messageName: true
    emit:
      - name: Seen
        forwardArguments: true
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Ping | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: amount
          value: { type: ":Number.int64", value: "7" }
    local:
      - name: Seen
        args:
          - name: amount
            value: { type: ":Number.int64", value: "7" }
```

---

## Test: Native handlers are atomic for a frame budget

This runtime case exercises “Native handlers are atomic for a frame budget” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: native-handler-frame-atomicity
nativeHandlers:
  - id: start
    message: Start
    parameters: []
    emit:
      - name: Done
        args: []
  - id: done
    message: Done
    parameters: []
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | frame | 1 |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    paused: false
    local:
      - name: Done
```

---

## Test: Subscription handles report idempotent unsubscription and preserve queued snapshots

This runtime case exercises “Subscription handles report idempotent unsubscription and preserve queued snapshots” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: subscription-handle-state
nativeHandlers:
  - id: start
    message: Start
    parameters: []
    emit:
      - name: Seen
        args: []
stepActions:
  detach:
    - unsubscribeHandler: start
      expectResult: true
    - unsubscribeHandler: start
      expectResult: false
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| queue | Start | enqueue | |
| detach | Tick | completion | |
| after | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  queue:
    local: []
  detach:
    accepted: false
    local:
      - name: Seen
  after:
    accepted: false
    local: []
```

---

## Test: Program instance handles report idempotent detach and preserve queued snapshots

This runtime case exercises “Program instance handles report idempotent detach and preserve queued snapshots” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: instance-handle-state
stepActions:
  detach:
    - detachProgram: main
      expectResult: true
    - detachProgram: main
      expectResult: false
```

### Source code under test

```ges
on Start { emit Seen() }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| queue | Start | enqueue | |
| detach | Tick | completion | |
| after | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  queue:
    local: []
  detach:
    accepted: false
    local:
      - name: Seen
  after:
    accepted: false
    local: []
```
