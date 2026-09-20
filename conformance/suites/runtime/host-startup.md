---
formatVersion: 1
suiteId: runtime.host-startup
title: Explicit host startup and instance initialization
kind: scriptApi
level: scenario
categories: [conformance]
tags: [host, lifecycle, initialization]
---

# Explicit host startup and instance initialization

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

These cases distinguish the initial Start barrier from deferred initialization on an already ready host.
Local observations include emit attempts, including attempts discarded after a failed initialization.
Only actual handler output proves delivery.

---

## Test: Start initializes the complete group before any ordinary delivery

This scenario verifies start initializes the complete group before any ordinary delivery.

### Case description

```yaml
gesBlock: case
id: group-order-start-only
sources:
  - name: a.ges
    program: a
  - name: b.ges
    program: b
```

### Source code under test

```ges
module a
on initialization { emit Kick() }
on KickTwo { emit DeliveredA() }
```

```ges
module b
on initialization { emit KickTwo() }
on Kick { emit DeliveredB() }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| drain | Unknown | completion |  |
| again | Unknown | completion |  |

### Expectation

```yaml
gesBlock: expect
initialization:
  pump: start
  hostReady: true
  programStarts: { a: ready, b: ready }
  local:
    - name: Kick
    - name: KickTwo
steps:
  drain:
    accepted: false
    local:
      - name: DeliveredB
      - name: DeliveredA
    hostReady: true
  again:
    accepted: false
    programStarts: { a: ready, b: ready }
```

---

## Test: One failed initialization prevents the entire initial group from starting

This scenario verifies one failed initialization prevents the entire initial group from starting.

### Case description

```yaml
gesBlock: case
id: group-failure-discards-startup
publishSink: accept
sources:
  - name: a.ges
    program: a
  - name: b.ges
    program: b
  - name: c.ges
    program: c
```

### Source code under test

```ges
module a
on initialization { emit Kick(); publish Notice() }
on Kick { emit Delivered() }
```

```ges
module b
on initialization { emit Leak(); let outcome be :test.declaredFault() }
on Kick { emit Bad() }
```

```ges
module c
on initialization { emit Never() }
on Leak { emit Leaked() }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| rejected | Kick | completion |  |

### Expectation

```yaml
gesBlock: expect
initialization:
  pump: start
  hostReady: false
  programStarts: { a: runtimeError, b: runtimeError, c: runtimeError }
  local:
    - name: Kick
    - name: Leak
  diagnostics:
    - phase: runtime
      code: test.declaredFault
      programName: b
      handlerName: initialization()
steps:
  rejected:
    accepted: false
    hostReady: false
```

---

## Test: A late instance is pending until its queued initialization completes

This scenario verifies a late instance is pending until its queued initialization completes.

### Case description

```yaml
gesBlock: case
id: late-pending-and-ready
deferredPrograms: [late]
stepActions:
  enqueue:
    - loadProgram: late
      expectResult: true
sources:
  - name: base.ges
    program: base
  - name: late.ges
    program: late
```

### Source code under test

```ges
module base
on Tick { emit Old() }
```

```ges
module late
on initialization { emit Boot() }
on Tick { emit New() }
on Boot { emit BootDone() }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| enqueue | Tick | enqueue |  |
| drain | Unknown | completion |  |

### Expectation

```yaml
gesBlock: expect
initialization:
  hostReady: true
  programStarts: { base: ready }
steps:
  enqueue:
    hostReady: true
    programStarts: { late: pending }
  drain:
    accepted: false
    hostReady: true
    programStarts: { late: ready }
    local:
      - name: Boot
      - name: Old
      - name: New
      - name: BootDone
```

---

## Test: A late failure discards its output and its captured deliveries only

This scenario verifies a late failure discards its output and its captured deliveries only.

### Case description

```yaml
gesBlock: case
id: late-failure-preserves-other-recipients
deferredPrograms: [late]
publishSink: accept
stepActions:
  enqueue:
    - loadProgram: late
      expectResult: true
sources:
  - name: base.ges
    program: base
  - name: late.ges
    program: late
```

### Source code under test

```ges
module base
on Tick { emit Old() }
on Leak { emit Leaked() }
on Notice { emit PublishedLeak() }
```

```ges
module late
on initialization { emit Leak(); publish Notice(); let outcome be :test.declaredFault() }
on Tick { emit Bad() }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| enqueue | Tick | enqueue |  |
| drain | Unknown | completion |  |
| later | Tick | completion |  |

### Expectation

```yaml
gesBlock: expect
initialization:
  hostReady: true
steps:
  enqueue:
    programStarts: { late: pending }
  drain:
    accepted: false
    hostReady: true
    programStarts: { base: ready, late: runtimeError }
    local:
      - name: Leak
      - name: Old
    diagnostics:
      - phase: runtime
        code: test.declaredFault
        programName: late
        handlerName: initialization()
  later:
    hostReady: true
    programStarts: { late: runtimeError }
    local:
      - name: Old
```

---

## Test: All init handlers of one instance share the same success boundary

This scenario verifies all init handlers of one instance share the same success boundary.

### Case description

```yaml
gesBlock: case
id: multiple-init-handlers-fail-together
sources:
  - name: example.ges
    program: example
```

### Source code under test

```ges
module example
on initialization { emit Kick() }
on initialization { let outcome be :test.declaredFault() }
on Kick { emit Delivered() }
```

### Expectation

```yaml
gesBlock: expect
initialization:
  hostReady: false
  programStarts: { example: runtimeError }
  local:
    - name: Kick
  diagnostics:
    - phase: runtime
      code: test.declaredFault
      programName: example
      handlerName: initialization()
```

---

## Test: A startup safety limit prevents the entire group from becoming ready

This scenario verifies a startup safety limit prevents the entire group from becoming ready.

### Case description

```yaml
gesBlock: case
id: initial-limit-fails-start
runtimeLimits:
  maxLoopIterations: 1
sources:
  - name: example.ges
    program: example
```

### Source code under test

```ges
module example
on initialization { for item from 1 to 2 emit Kick() }
on Kick { emit Delivered() }
```

### Expectation

```yaml
gesBlock: expect
initialization:
  hostReady: false
  programStarts: { example: runtimeLimitReached }
  local:
    - name: Kick
  runtimeLimits:
    include:
      - name: MaxLoopIterations
        limit: 1
```

---

## Test: Late initialization can pause without enabling ordinary handlers

This scenario verifies late initialization can pause without enabling ordinary handlers.

### Case description

```yaml
gesBlock: case
id: late-init-pauses
deferredPrograms: [late]
stepActions:
  pause:
    - loadProgram: late
      expectResult: true
sources:
  - name: late.ges
    program: late
```

### Source code under test

```ges
module late
on initialization { emit Boot() }
on Tick { emit Delivered() }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| pause | Tick | frame | 1 |
| finish | Unknown | completion |  |

### Expectation

```yaml
gesBlock: expect
initialization:
  hostReady: true
steps:
  pause:
    paused: true
    hostReady: true
    programStarts: { late: pending }
  finish:
    accepted: false
    hostReady: true
    programStarts: { late: ready }
    local:
      - name: Boot
      - name: Delivered
```

---

## Test: Programs without initialization become ready at the start barrier

This scenario verifies programs without initialization become ready at the start barrier.

### Case description

```yaml
gesBlock: case
id: no-init-start
sources:
  - name: example.ges
    program: example
```

### Source code under test

```ges
module example
on Tick { emit Done() }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Tick | completion |  |

### Expectation

```yaml
gesBlock: expect
initialization:
  pump: start
  hostReady: true
  programStarts: { example: ready }
steps:
  run:
    local:
      - name: Done
```

---

## Test: Successful publication is released only after the complete group initializes

This scenario verifies successful publication is released only after the complete group initializes.

### Case description

```yaml
gesBlock: case
id: publication-after-group-success
publishSink: accept
sources:
  - name: a.ges
    program: a
  - name: b.ges
    program: b
```

### Source code under test

```ges
module a
on initialization { publish Notice() }
```

```ges
module b
on initialization { emit Marker() }
on Notice { emit Delivered() }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| drain | Unknown | completion |  |

### Expectation

```yaml
gesBlock: expect
initialization:
  pump: start
  hostReady: true
  local:
    - name: Marker
    - name: Notice
  outbound:
    - name: Notice
steps:
  drain:
    accepted: false
    local:
      - name: Delivered
```

---

## Test: External messages received during paused init are not its discarded outputs

This scenario verifies that failing a resumed init preserves independently received messages for other instances.

### Case description

```yaml
gesBlock: case
id: external-input-during-paused-init
deferredPrograms: [late]
stepActions:
  pause:
    - loadProgram: late
sources:
  - name: base.ges
    program: base
  - name: late.ges
    program: late
```

### Source code under test

```ges
module base
on Tick { emit Old() }
```

```ges
module late
on initialization { let outcome be :test.declaredFault() }
on Tick { emit Bad() }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| pause | Unknown | frame | 1 |
| enqueue | Tick | enqueue | |
| finish | Unknown | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  pause:
    accepted: false
    paused: true
    programStarts: { late: pending }
  enqueue:
    programStarts: { late: pending }
  finish:
    accepted: false
    hostReady: true
    programStarts: { late: runtimeError }
    local:
      - name: Old
    diagnostics:
      - phase: runtime
        code: test.declaredFault
        programName: late
        handlerName: initialization()
```

---

## Test: Paused initialization preserves enqueue order against external input

This case verifies that an emitted message retains its FIFO position even when external input arrives before its init completes.

### Case description

```yaml
gesBlock: case
id: paused-init-output-retains-fifo
deferredPrograms: [late]
stepActions:
  pause:
    - loadProgram: late
sources:
  - name: base.ges
    program: base
  - name: late.ges
    program: late
```

### Source code under test

```ges
module base
on Boot { emit First() }
on Tick { emit Second() }
```

```ges
module late
on initialization { emit Boot() }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| pause | Unknown | frame | 2 |
| enqueue | Tick | enqueue | |
| finish | Unknown | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  pause:
    accepted: false
    paused: true
    programStarts: { late: pending }
    local:
      - name: Boot
  enqueue:
    programStarts: { late: pending }
  finish:
    accepted: false
    hostReady: true
    programStarts: { late: ready }
    local:
      - name: First
      - name: Second
```
