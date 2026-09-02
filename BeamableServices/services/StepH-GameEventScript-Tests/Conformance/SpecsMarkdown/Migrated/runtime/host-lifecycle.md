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

The native actions in this suite are fixed runner operations. They exercise the
portable Host lifecycle without embedding implementation-specific test code.

## Test: Native-only host dispatches messages

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

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: value
          value: { type: ":integer", value: "7" }
    local:
      - name: Pong
        args:
          - name: value
            value: { type: ":integer", value: "7" }
```

## Test: One immutable program runs in multiple hosts

```yaml
gesBlock: case
id: shared-program-multiple-hosts
hostCount: 2
```

```ges
on Start(value) { emit Done(value: value + 1) }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |

```yaml
gesBlock: expect
steps:
  run:
    input:
      args:
        - name: value
          value: { type: ":integer", value: "4" }
    local:
      - name: Done
        args:
          - name: value
            value: { type: ":integer", value: "5" }
```

## Test: Detach and unsubscribe retain the queued dispatch snapshot

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

```ges
on Trigger() { emit ScriptSeen() }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| current-snapshot | Trigger | completion | |
| after-removal | Trigger | completion | |

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

## Test: Load and subscribe become visible after the current dispatch

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

```ges
on initialization { emit Initialized() }
on Trigger() { emit ScriptSeen() }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| install | Trigger | completion | |
| visible | Trigger | completion | |

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

## Test: VM state resets between message handlers

```yaml
gesBlock: case
id: vm-reset-between-handlers
```

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

```yaml
gesBlock: expect
steps:
  first:
    input:
      args:
        - name: value
          value: { type: ":integer", value: "1" }
    local:
      - name: Seen
        args:
          - name: value
            value: { type: ":integer", value: "101" }
  second:
    input:
      args:
        - name: value
          value: { type: ":integer", value: "1" }
    local:
      - name: Seen
        args:
          - name: value
            value: { type: ":integer", value: "2" }
```

## Test: Runtime limits reset for every script handler

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

```yaml
gesBlock: expect
steps:
  run:
    local:
      - name: Tick
        args:
          - name: value
            value: { type: ":integer", value: "1" }
      - name: Tick
        args:
          - name: value
            value: { type: ":integer", value: "2" }
      - name: Tick
        args:
          - name: value
            value: { type: ":integer", value: "3" }
      - name: Tick
        args:
          - name: value
            value: { type: ":integer", value: "4" }
    runtimeLimits:
      exclude:
        - any: true
```
