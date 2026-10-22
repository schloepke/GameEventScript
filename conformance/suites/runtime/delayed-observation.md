---
formatVersion: 1
suiteId: runtime.delayed-observation
title: Observation of delayed messages
kind: scriptApi
level: scenario
categories: [conformance]
tags: [host, delayed, observer]
---

# Observation of delayed messages

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

These cases verify that the runtime observer sees each delayed `emit … after` and `publish … after` once when it is scheduled, with its per-host scheduling ID and absolute deadline on the host clock, and sees each delayed message removed before it became due exactly once.

---

## Test: scheduling reports absolute deadlines and distinct ids

Two identical delayed emits and one delayed publish report increasing scheduling IDs and absolute deadlines on the virtual clock. Becoming due adds no scheduling callback; the ordinary delivery callbacks follow.

### Case description

```yaml
gesBlock: case
id: scheduling-reports-deadlines-and-ids
sources:
  - name: scheduling.ges
    program: main
publishSink: accept
stepActions:
  due:
    - advanceMicroseconds: "2000000"
```

### Source code under test

```ges
module main
on Work { emit after 1s Later(); emit after 1s Later(); publish after 2s Out() }
on Later {}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| begin | Work | completion | |
| due | Unknown | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  begin:
    waiting: true
    local: [{ name: Later }, { name: Later }]
    trace:
      - event: dispatchStarted
        message: { name: Work }
        signatureId: "Work()"
      - event: scheduled
        message: { name: Later }
        id: "1"
        deadline: "1000000"
        publish: false
      - event: emit
        message: { name: Later }
        accepted: true
      - event: scheduled
        message: { name: Later }
        id: "2"
        deadline: "1000000"
        publish: false
      - event: emit
        message: { name: Later }
        accepted: true
      - event: scheduled
        message: { name: Out }
        id: "3"
        deadline: "2000000"
        publish: true
      - event: dispatchCompleted
        message: { name: Work }
        signatureId: "Work()"
  due:
    accepted: false
    waiting: false
    local: [{ name: Out }]
    outbound: [{ name: Out }]
    trace:
      - event: publish
        message: { name: Out }
        result: { localAccepted: false, outboundAttempted: true, outboundAccepted: true, anyAccepted: true }
      - event: dispatchStarted
        message: { name: Later }
        signatureId: "Later()"
      - event: dispatchCompleted
        message: { name: Later }
        signatureId: "Later()"
      - event: dispatchStarted
        message: { name: Later }
        signatureId: "Later()"
      - event: dispatchCompleted
        message: { name: Later }
        signatureId: "Later()"
```

---

## Test: failed initial group discards every scheduled message once

When the initial group fails, every delayed message it scheduled is reported as discarded exactly once, in deadline order.

### Case description

```yaml
gesBlock: case
id: failed-initial-group-discards-scheduled
sources:
  - name: failed-start.ges
    program: main
```

### Source code under test

```ges
module main
on initialization { emit after 1s Leak(); publish after 2s Out(); emit after 1s Leak(); let outcome be :test.declaredFault() }
on Leak {}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| idle | Unknown | completion | |

### Expectation

```yaml
gesBlock: expect
initialization:
  hostReady: false
  programStarts: { main: runtimeError }
  local: [{ name: Leak }, { name: Leak }]
  diagnostics:
    - phase: runtime
      code: test.declaredFault
      programName: main
      handlerName: initialization()
  trace:
    - event: dispatchStarted
      message: { name: initialization }
      signatureId: "initialization()"
    - event: scheduled
      message: { name: Leak }
      id: "1"
      deadline: "1000000"
      publish: false
    - event: emit
      message: { name: Leak }
      accepted: true
    - event: scheduled
      message: { name: Out }
      id: "2"
      deadline: "2000000"
      publish: true
    - event: scheduled
      message: { name: Leak }
      id: "3"
      deadline: "1000000"
      publish: false
    - event: emit
      message: { name: Leak }
      accepted: true
    - event: diagnostic
      diagnostic: { phase: runtime, code: test.declaredFault, programName: main, handlerName: initialization() }
    - event: dispatchCompleted
      message: { name: initialization }
      signatureId: "initialization()"
    - event: scheduledDiscarded
      id: "1"
    - event: scheduledDiscarded
      id: "3"
    - event: scheduledDiscarded
      id: "2"
steps:
  idle:
    accepted: false
    waiting: false
    trace: []
```

---

## Test: failed later initialization discards only its scheduled message

A later-loaded program whose initialization fails reports its own delayed message as discarded, while an independent delayed message keeps its ID and is delivered when due.

### Case description

```yaml
gesBlock: case
id: failed-later-initialization-discards-own-scheduled
deferredPrograms: [late]
sources:
  - name: base.ges
    program: base
  - name: late.ges
    program: late
publishSink: accept
stepActions:
  schedule:
    - loadProgram: late
  due:
    - advanceMicroseconds: "1000000"
```

### Source code under test

```ges
module base
on Start { publish after 1s Tick() }
on Tick {}
```

```ges
module late
on initialization { publish after 1s Leak(); let failure be :test.declaredFault() }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| queued | Start | enqueue | |
| schedule | Unknown | completion | |
| due | Unknown | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  schedule:
    accepted: false
    waiting: false
    programStarts: { late: runtimeError }
    diagnostics:
      - phase: runtime
        code: test.declaredFault
        programName: late
        handlerName: initialization()
    trace:
      - event: dispatchStarted
        message: { name: Start }
        signatureId: "Start()"
      - event: scheduled
        message: { name: Tick }
        id: "1"
        deadline: "1000000"
        publish: true
      - event: dispatchCompleted
        message: { name: Start }
        signatureId: "Start()"
      - event: dispatchStarted
        message: { name: initialization }
        signatureId: "initialization()"
      - event: scheduled
        message: { name: Leak }
        id: "2"
        deadline: "1000000"
        publish: true
      - event: diagnostic
        diagnostic: { phase: runtime, code: test.declaredFault, programName: late, handlerName: initialization() }
      - event: dispatchCompleted
        message: { name: initialization }
        signatureId: "initialization()"
      - event: scheduledDiscarded
        id: "2"
  due:
    accepted: false
    waiting: false
    local: [{ name: Tick }]
    outbound: [{ name: Tick }]
    trace:
      - event: publish
        message: { name: Tick }
        result: { localAccepted: true, outboundAttempted: true, outboundAccepted: true, anyAccepted: true }
      - event: dispatchStarted
        message: { name: Tick }
        signatureId: "Tick()"
      - event: dispatchCompleted
        message: { name: Tick }
        signatureId: "Tick()"
```
