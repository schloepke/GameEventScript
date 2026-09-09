---
formatVersion: 1
suiteId: runtime.host-load-limits
title: Atomic loading at the initialization queue limit
kind: scriptApi
level: scenario
categories: [conformance]
tags: [host, lifecycle, runtime-limits]
---

# Atomic loading at the initialization queue limit

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite verifies atomic Program loading when initialization needs a queue slot, including rejection, retry, active callbacks, paused scripts, and immutable dispatch snapshots.

---

## Test: Full queue rejects the entire load and permits retry

This scenario verifies a structured link rejection, no leaked instance or handlers, an untouched queued message, and exactly one initialization after a successful retry.

### Case description

```yaml
gesBlock: case
id: "full-queue-reject-and-retry"
runtimeLimits:
  maxQueuedMessagesPerRun: 1
deferredPrograms:
  - "late"
compile:
  binaryRoundTrip: true
stepActions:
  rejected:
    - loadProgram: "late"
      expectError:
        phase: "link"
        code: "link.initializationQueueFull"
        programName: "late"
    - detachProgram: "late"
      expectResult: false
  retry:
    - loadProgram: "late"
      expectResult: true
  ensure:
    - loadProgram: "late"
      expectResult: true
  detach:
    - detachProgram: "late"
      expectResult: true
    - detachProgram: "late"
      expectResult: false
nativeHandlers:
  - id: "before"
    message: "Before"
    emit:
      - name: "BeforeSeen"
        args: []
sources:
  - name: "full-queue-reject-and-retry-late.ges"
    program: "late"
```

### Source code under test

```ges
module late
on initialization { emit Ready() }
on Tick() { emit Seen() }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| queued | Before | enqueue |  |
| rejected | Tick | completion |  |
| probe | Tick | completion |  |
| retry | NoHandler | completion |  |
| tick | Tick | completion |  |
| ensure | NoHandler | completion |  |
| detach | NoHandler | completion |  |
| detached | Tick | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  queued:
    accepted: true
    local: []
    runtimeLimits:
      exclude:
        - any: true
  rejected:
    accepted: false
    local:
      - name: "BeforeSeen"
    runtimeLimits:
      exclude:
        - any: true
    trace:
      - event: "dispatchStarted"
        message:
          name: "Before"
        signatureId: "Before()"
      - event: "emit"
        message:
          name: "BeforeSeen"
        accepted: false
      - event: "dispatchCompleted"
        message:
          name: "Before"
        signatureId: "Before()"
  probe:
    accepted: false
    local: []
    runtimeLimits:
      exclude:
        - any: true
  retry:
    accepted: false
    local:
      - name: "Ready"
    runtimeLimits:
      exclude:
        - any: true
  tick:
    accepted: true
    local:
      - name: "Seen"
    runtimeLimits:
      exclude:
        - any: true
  ensure:
    accepted: false
    local: []
    runtimeLimits:
      exclude:
        - any: true
  detach:
    accepted: false
    local: []
    runtimeLimits:
      exclude:
        - any: true
  detached:
    accepted: false
    local: []
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: One remaining queue slot accepts initialization

This scenario verifies that initialization may occupy the last available queue slot and retains its position behind an older message.

### Case description

```yaml
gesBlock: case
id: "last-slot-accepts-initialization"
runtimeLimits:
  maxQueuedMessagesPerRun: 2
deferredPrograms:
  - "late"
stepActions:
  load:
    - loadProgram: "late"
      expectResult: true
nativeHandlers:
  - id: "before"
    message: "Before"
    emit:
      - name: "BeforeSeen"
        args: []
sources:
  - name: "last-slot-accepts-initialization-late.ges"
    program: "late"
```

### Source code under test

```ges
module late
on initialization { emit Ready() }
on Tick() { emit Seen() }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| queued | Before | enqueue |  |
| load | NoHandler | completion |  |
| tick | Tick | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  queued:
    accepted: true
    local: []
    runtimeLimits:
      exclude:
        - any: true
  load:
    accepted: false
    local:
      - name: "BeforeSeen"
      - name: "Ready"
    runtimeLimits:
      exclude:
        - any: true
  tick:
    accepted: true
    local:
      - name: "Seen"
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: Loading without initialization needs no queue slot

This scenario verifies that a full queue does not reject a Program that has no initialization handlers.

### Case description

```yaml
gesBlock: case
id: "no-initialization-needs-no-slot"
runtimeLimits:
  maxQueuedMessagesPerRun: 1
deferredPrograms:
  - "late"
stepActions:
  load:
    - loadProgram: "late"
      expectResult: true
nativeHandlers:
  - id: "before"
    message: "Before"
    emit:
      - name: "BeforeSeen"
        args: []
sources:
  - name: "no-initialization-needs-no-slot-late.ges"
    program: "late"
```

### Source code under test

```ges
module late
on Tick() { emit Seen() }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| queued | Before | enqueue |  |
| load | NoHandler | completion |  |
| tick | Tick | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  queued:
    accepted: true
    local: []
    runtimeLimits:
      exclude:
        - any: true
  load:
    accepted: false
    local:
      - name: "BeforeSeen"
    runtimeLimits:
      exclude:
        - any: true
  tick:
    accepted: true
    local:
      - name: "Seen"
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: Rejected loading preserves a paused script

This scenario verifies that rejecting a load between frames preserves the active handler registers, its queued successor, and later initialization on retry.

### Case description

```yaml
gesBlock: case
id: "rejection-preserves-paused-script"
runtimeLimits:
  maxQueuedMessagesPerRun: 1
deferredPrograms:
  - "late"
compile:
  binaryRoundTrip: true
stepActions:
  rejected:
    - loadProgram: "late"
      expectError:
        phase: "link"
        code: "link.initializationQueueFull"
        programName: "late"
  retry:
    - loadProgram: "late"
      expectResult: true
nativeHandlers:
  - id: "before"
    message: "Before"
    emit:
      - name: "BeforeSeen"
        args: []
sources:
  - name: "rejection-preserves-paused-script-active.ges"
    program: "active"
  - name: "rejection-preserves-paused-script-late.ges"
    program: "late"
```

### Source code under test

```ges
module active
on Start(value) {
  let preserved be value + 5
  emit Active(value: preserved)
}
```

```ges
module late
on initialization { emit Ready() }
on Tick() { emit Seen() }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| pause | Start | frame | 1 |
| queued | Before | enqueue |  |
| rejected | NoHandler | frames | 1 |
| retry | NoHandler | completion |  |
| tick | Tick | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  pause:
    accepted: true
    local: []
    runtimeLimits:
      exclude:
        - any: true
    paused: true
    input:
      args:
        - name: "value"
          value:
            type: ":Number.int64"
            value: "7"
  queued:
    accepted: true
    local: []
    runtimeLimits:
      exclude:
        - any: true
  rejected:
    accepted: false
    local:
      - name: "Active"
        args:
          - name: "value"
            value:
              type: ":Number.int64"
              value: "12"
      - name: "BeforeSeen"
    runtimeLimits:
      exclude:
        - any: true
    paused: true
  retry:
    accepted: false
    local:
      - name: "Ready"
    runtimeLimits:
      exclude:
        - any: true
  tick:
    accepted: true
    local:
      - name: "Seen"
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: Native loading rejection does not abort a handled callback

This scenario fills the queue from a script handler, catches the expected load rejection in a later native handler of the same message, and verifies that dispatch and queued work continue.

### Case description

```yaml
gesBlock: case
id: "native-callback-handles-rejection"
runtimeLimits:
  maxQueuedMessagesPerRun: 1
deferredPrograms:
  - "late"
stepActions:
  retry:
    - loadProgram: "late"
      expectResult: true
nativeHandlers:
  - id: "before"
    message: "Before"
    emit:
      - name: "BeforeSeen"
        args: []
  - id: "loader"
    message: "Start"
    priority: -1
    actions:
      - loadProgram: "late"
        expectError:
          phase: "link"
          code: "link.initializationQueueFull"
          programName: "late"
      - detachProgram: "late"
        expectResult: false
    emit:
      - name: "Continued"
        args: []
sources:
  - name: "native-callback-handles-rejection-active.ges"
    program: "active"
  - name: "native-callback-handles-rejection-late.ges"
    program: "late"
```

### Source code under test

```ges
module active
on Start() { emit Before() }
```

```ges
module late
on initialization { emit Ready() }
on Tick() { emit Seen() }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion |  |
| probe | Tick | completion |  |
| retry | NoHandler | completion |  |
| tick | Tick | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    accepted: true
    local:
      - name: "Before"
      - name: "Continued"
      - name: "BeforeSeen"
    runtimeLimits:
      exclude:
        - any: true
  probe:
    accepted: false
    local: []
    runtimeLimits:
      exclude:
        - any: true
  retry:
    accepted: false
    local:
      - name: "Ready"
    runtimeLimits:
      exclude:
        - any: true
  tick:
    accepted: true
    local:
      - name: "Seen"
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: Successive loads each need their own initialization slot

This scenario verifies that the first successful load consumes the available slot, the second load fails atomically, and both Programs work after retrying the second.

### Case description

```yaml
gesBlock: case
id: "successive-loads-reserve-separate-slots"
runtimeLimits:
  maxQueuedMessagesPerRun: 1
deferredPrograms:
  - "a"
  - "b"
stepActions:
  load:
    - loadProgram: "a"
      expectResult: true
    - loadProgram: "b"
      expectError:
        phase: "link"
        code: "link.initializationQueueFull"
        programName: "b"
    - detachProgram: "b"
      expectResult: false
  retry:
    - loadProgram: "b"
      expectResult: true
sources:
  - name: "successive-loads-reserve-separate-slots-a.ges"
    program: "a"
  - name: "successive-loads-reserve-separate-slots-b.ges"
    program: "b"
```

### Source code under test

```ges
module a
on initialization { emit ReadyA() }
on TickA() { emit SeenA() }
```

```ges
module b
on initialization { emit ReadyB() }
on TickB() { emit SeenB() }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| load | NoHandler | completion |  |
| probe | TickB | completion |  |
| first | TickA | completion |  |
| retry | NoHandler | completion |  |
| second | TickB | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  load:
    accepted: false
    local:
      - name: "ReadyA"
    runtimeLimits:
      exclude:
        - any: true
  probe:
    accepted: false
    local: []
    runtimeLimits:
      exclude:
        - any: true
  first:
    accepted: true
    local:
      - name: "SeenA"
    runtimeLimits:
      exclude:
        - any: true
  retry:
    accepted: false
    local:
      - name: "ReadyB"
    runtimeLimits:
      exclude:
        - any: true
  second:
    accepted: true
    local:
      - name: "SeenB"
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: Rejected loading does not install an undeliverable handler

This scenario verifies that a rejected Program cannot receive previously unmatched messages through a leaked fallback registration.

### Case description

```yaml
gesBlock: case
id: "rejected-load-does-not-register-fallback"
runtimeLimits:
  maxQueuedMessagesPerRun: 1
deferredPrograms:
  - "late"
stepActions:
  rejected:
    - loadProgram: "late"
      expectError:
        phase: "link"
        code: "link.initializationQueueFull"
        programName: "late"
    - detachProgram: "late"
      expectResult: false
nativeHandlers:
  - id: "before"
    message: "Before"
    emit:
      - name: "BeforeSeen"
        args: []
sources:
  - name: "rejected-load-does-not-register-fallback-late.ges"
    program: "late"
```

### Source code under test

```ges
module late
on initialization { emit Ready() }
on undeliverable as rejectedMessage { emit Unexpected() }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| queued | Before | enqueue |  |
| rejected | NoHandler | completion |  |
| probe | NoHandler | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  queued:
    accepted: true
    local: []
    runtimeLimits:
      exclude:
        - any: true
  rejected:
    accepted: false
    local:
      - name: "BeforeSeen"
    runtimeLimits:
      exclude:
        - any: true
    trace:
      - event: "dispatchStarted"
        message:
          name: "Before"
        signatureId: "Before()"
      - event: "emit"
        message:
          name: "BeforeSeen"
        accepted: false
      - event: "dispatchCompleted"
        message:
          name: "Before"
        signatureId: "Before()"
  probe:
    accepted: false
    local: []
    runtimeLimits:
      exclude:
        - any: true
```

---

## Test: Successful loading preserves an already queued dispatch snapshot

This scenario verifies that filling the final queue slot with initialization does not add the new handler to an older message, while subsequently received messages include both handlers.

### Case description

```yaml
gesBlock: case
id: "successful-load-preserves-captured-snapshot"
runtimeLimits:
  maxQueuedMessagesPerRun: 2
deferredPrograms:
  - "late"
stepActions:
  load:
    - loadProgram: "late"
      expectResult: true
sources:
  - name: "successful-load-preserves-captured-snapshot-active.ges"
    program: "active"
  - name: "successful-load-preserves-captured-snapshot-late.ges"
    program: "late"
```

### Source code under test

```ges
module active
on Tick() { emit Existing() }
```

```ges
module late
on initialization { emit Ready() }
on Tick() { emit Seen() }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| queued | Tick | enqueue |  |
| load | NoHandler | completion |  |
| tick | Tick | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  queued:
    accepted: true
    local: []
    runtimeLimits:
      exclude:
        - any: true
  load:
    accepted: false
    local:
      - name: "Existing"
      - name: "Ready"
    runtimeLimits:
      exclude:
        - any: true
  tick:
    accepted: true
    local:
      - name: "Existing"
      - name: "Seen"
    runtimeLimits:
      exclude:
        - any: true
```
