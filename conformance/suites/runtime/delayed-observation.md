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

These cases verify that the runtime observer sees each delayed `emit … after` and `publish … after` once when it is scheduled, with its per-host send ID and absolute deadline on the host clock, and sees each delayed message removed before it became due exactly once.

---

## Test: scheduling reports absolute deadlines and distinct ids

Two identical delayed emits and one delayed publish report distinct send IDs and absolute deadlines on the virtual clock. Becoming due adds no scheduling callback; the ordinary delivery callbacks follow and repeat each send ID.

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
        id: "1"
        message: { name: Work }
        signatureId: "Work()"
      - event: scheduled
        id: "2"
        message: { name: Later }
        deadline: "1000000"
        publish: false
      - event: emit
        id: "2"
        message: { name: Later }
        accepted: true
      - event: scheduled
        id: "3"
        message: { name: Later }
        deadline: "1000000"
        publish: false
      - event: emit
        id: "3"
        message: { name: Later }
        accepted: true
      - event: scheduled
        id: "4"
        message: { name: Out }
        deadline: "2000000"
        publish: true
      - event: dispatchCompleted
        id: "1"
        message: { name: Work }
        signatureId: "Work()"
  due:
    accepted: false
    waiting: false
    local: [{ name: Out }]
    outbound: [{ name: Out }]
    trace:
      - event: publish
        id: "4"
        message: { name: Out }
        result: { localAccepted: false, outboundAttempted: true, outboundAccepted: true, anyAccepted: true }
      - event: dispatchStarted
        id: "2"
        message: { name: Later }
        signatureId: "Later()"
      - event: dispatchCompleted
        id: "2"
        message: { name: Later }
        signatureId: "Later()"
      - event: dispatchStarted
        id: "3"
        message: { name: Later }
        signatureId: "Later()"
      - event: dispatchCompleted
        id: "3"
        message: { name: Later }
        signatureId: "Later()"
```

---

## Test: delayed emit without recipients is not scheduled

A delayed emit whose recipient snapshot is empty behaves exactly like the instant emit: the script result is true, the emit is observed as not queued, it reports send ID 0 and leaves no delayed work.

### Case description

```yaml
gesBlock: case
id: delayed-emit-without-recipients-is-not-scheduled
sources:
  - name: orphan.ges
    program: main
```

### Source code under test

```ges
module main
on Work { if emit after 1s Nobody() { emit after 1s Later() } }
on Later {}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| begin | Work | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  begin:
    waiting: true
    local: [{ name: Nobody }, { name: Later }]
    trace:
      - event: dispatchStarted
        id: "1"
        message: { name: Work }
        signatureId: "Work()"
      - event: emit
        id: "0"
        message: { name: Nobody }
        accepted: false
      - event: scheduled
        id: "2"
        message: { name: Later }
        deadline: "1000000"
        publish: false
      - event: emit
        id: "2"
        message: { name: Later }
        accepted: true
      - event: dispatchCompleted
        id: "1"
        message: { name: Work }
        signatureId: "Work()"
```

---

## Test: zero delay is observed as an instant send

`emit after 0s` and `publish after 0s` behave exactly like the same sends without `after`: they report no scheduling callback, leave no delayed work, and are observed and delivered within the same pump under ordinary send IDs.

### Case description

```yaml
gesBlock: case
id: zero-delay-is-instant-send
sources:
  - name: zero.ges
    program: main
publishSink: accept
```

### Source code under test

```ges
module main
on Work { emit after 0s Now(); publish after 0s Out() }
on Now {}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| begin | Work | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  begin:
    waiting: false
    local: [{ name: Now }, { name: Out }]
    outbound: [{ name: Out }]
    trace:
      - event: dispatchStarted
        id: "1"
        message: { name: Work }
        signatureId: "Work()"
      - event: emit
        id: "2"
        message: { name: Now }
        accepted: true
      - event: publish
        id: "3"
        message: { name: Out }
        result: { localAccepted: false, outboundAttempted: true, outboundAccepted: true, anyAccepted: true }
      - event: dispatchCompleted
        id: "1"
        message: { name: Work }
        signatureId: "Work()"
      - event: dispatchStarted
        id: "2"
        message: { name: Now }
        signatureId: "Now()"
      - event: dispatchCompleted
        id: "2"
        message: { name: Now }
        signatureId: "Now()"
```

---

## Test: zero delay on a full queue matches the result-bearing instant send

On a full queue, `emit after 0s` behaves like the result-bearing `emit` without `after`: it is not observed as an emit, reports the queue limit and stops the handler before later sends.

### Case description

```yaml
gesBlock: case
id: zero-delay-full-queue-matches-result-bearing-send
sources:
  - name: zero-full.ges
    program: main
runtimeLimits:
  maxQueuedMessagesPerRun: 1
```

### Source code under test

```ges
module main
on Start { emit after 1s Ping(); emit after 0s Ping(); emit Bad() }
on Ping {}
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
    local: [{ name: Ping }]
    runtimeLimits:
      include:
        - name: MaxQueuedMessagesPerRun
          limit: 1
    trace:
      - event: dispatchStarted
        id: "1"
        message: { name: Start }
        signatureId: "Start()"
      - event: scheduled
        id: "2"
        message: { name: Ping }
        deadline: "1000000"
        publish: false
      - event: emit
        id: "2"
        message: { name: Ping }
        accepted: true
      - event: runtimeLimit
        runtimeLimit: { name: MaxQueuedMessagesPerRun, limit: 1 }
      - event: dispatchCompleted
        id: "1"
        message: { name: Start }
        signatureId: "Start()"
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
      id: "1"
      message: { name: initialization }
      signatureId: "initialization()"
    - event: scheduled
      id: "2"
      message: { name: Leak }
      deadline: "1000000"
      publish: false
    - event: emit
      id: "2"
      message: { name: Leak }
      accepted: true
    - event: scheduled
      id: "3"
      message: { name: Out }
      deadline: "2000000"
      publish: true
    - event: scheduled
      id: "4"
      message: { name: Leak }
      deadline: "1000000"
      publish: false
    - event: emit
      id: "4"
      message: { name: Leak }
      accepted: true
    - event: diagnostic
      diagnostic: { phase: runtime, code: test.declaredFault, programName: main, handlerName: initialization() }
    - event: dispatchCompleted
      id: "1"
      message: { name: initialization }
      signatureId: "initialization()"
    - event: discarded
      id: "2"
    - event: discarded
      id: "4"
    - event: discarded
      id: "3"
steps:
  idle:
    accepted: false
    waiting: false
    trace: []
```

---

## Test: failed initial group reports discards across programs in deadline order

A delayed emit that loses its only recipient when another program of the initial group fails is reported together with all other entries, in one deadline-ordered pass.

### Case description

```yaml
gesBlock: case
id: failed-initial-group-discards-in-deadline-order
sources:
  - name: sender.ges
    program: sender
  - name: receiver.ges
    program: receiver
```

### Source code under test

```ges
module sender
on initialization { publish after 1s Out(); emit after 2s Ping() }
```

```ges
module receiver
on Ping {}
on initialization { let failure be :test.declaredFault() }
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
  programStarts: { sender: runtimeError, receiver: runtimeError }
  local: [{ name: Ping }]
  diagnostics:
    - phase: runtime
      code: test.declaredFault
      programName: receiver
      handlerName: initialization()
  trace:
    - event: dispatchStarted
      id: "1"
      message: { name: initialization }
      signatureId: "initialization()"
    - event: scheduled
      id: "3"
      message: { name: Out }
      deadline: "1000000"
      publish: true
    - event: scheduled
      id: "4"
      message: { name: Ping }
      deadline: "2000000"
      publish: false
    - event: emit
      id: "4"
      message: { name: Ping }
      accepted: true
    - event: dispatchCompleted
      id: "1"
      message: { name: initialization }
      signatureId: "initialization()"
    - event: dispatchStarted
      id: "2"
      message: { name: initialization }
      signatureId: "initialization()"
    - event: diagnostic
      diagnostic: { phase: runtime, code: test.declaredFault, programName: receiver, handlerName: initialization() }
    - event: dispatchCompleted
      id: "2"
      message: { name: initialization }
      signatureId: "initialization()"
    - event: discarded
      id: "3"
    - event: discarded
      id: "4"
steps:
  idle:
    accepted: false
    waiting: false
    trace: []
```

---

## Test: failed initial group discards pending, queued and delayed messages

When the initial group fails, every message that was not dispatched is reported once: pending initializations first, then queued messages, then delayed entries in deadline order.

### Case description

```yaml
gesBlock: case
id: failed-initial-group-discards-all-undispatched
sources:
  - name: first.ges
    program: first
  - name: second.ges
    program: second
```

### Source code under test

```ges
module first
on initialization { emit Tick(); emit after 1s Tick(); let failure be :test.declaredFault() }
on Tick {}
```

```ges
module second
on initialization {}
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
  programStarts: { first: runtimeError, second: runtimeError }
  local: [{ name: Tick }, { name: Tick }]
  diagnostics:
    - phase: runtime
      code: test.declaredFault
      programName: first
      handlerName: initialization()
  trace:
    - event: dispatchStarted
      id: "1"
      message: { name: initialization }
      signatureId: "initialization()"
    - event: emit
      id: "3"
      message: { name: Tick }
      accepted: true
    - event: scheduled
      id: "4"
      message: { name: Tick }
      deadline: "1000000"
      publish: false
    - event: emit
      id: "4"
      message: { name: Tick }
      accepted: true
    - event: diagnostic
      diagnostic: { phase: runtime, code: test.declaredFault, programName: first, handlerName: initialization() }
    - event: dispatchCompleted
      id: "1"
      message: { name: initialization }
      signatureId: "initialization()"
    - event: discarded
      id: "2"
    - event: discarded
      id: "3"
    - event: discarded
      id: "4"
steps:
  idle:
    accepted: false
    waiting: false
    trace: []
```

---

## Test: failed later initialization discards its queued and delayed outputs

A later instance whose initialization fails reports its staged instant output before its delayed output; independent work is unaffected.

### Case description

```yaml
gesBlock: case
id: failed-later-initialization-discards-queued-and-delayed
deferredPrograms: [late]
sources:
  - name: base.ges
    program: base
  - name: late.ges
    program: late
stepActions:
  load:
    - loadProgram: late
```

### Source code under test

```ges
module base
on Tick {}
```

```ges
module late
on initialization { emit Tick(); emit after 1s Tick(); let failure be :test.declaredFault() }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| load | Unknown | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  load:
    accepted: false
    waiting: false
    local: [{ name: Tick }, { name: Tick }]
    programStarts: { late: runtimeError }
    diagnostics:
      - phase: runtime
        code: test.declaredFault
        programName: late
        handlerName: initialization()
    trace:
      - event: dispatchStarted
        id: "1"
        message: { name: initialization }
        signatureId: "initialization()"
      - event: emit
        id: "2"
        message: { name: Tick }
        accepted: true
      - event: scheduled
        id: "3"
        message: { name: Tick }
        deadline: "1000000"
        publish: false
      - event: emit
        id: "3"
        message: { name: Tick }
        accepted: true
      - event: diagnostic
        diagnostic: { phase: runtime, code: test.declaredFault, programName: late, handlerName: initialization() }
      - event: dispatchCompleted
        id: "1"
        message: { name: initialization }
        signatureId: "initialization()"
      - event: discarded
        id: "2"
      - event: discarded
        id: "3"
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
        id: "1"
        message: { name: Start }
        signatureId: "Start()"
      - event: scheduled
        id: "3"
        message: { name: Tick }
        deadline: "1000000"
        publish: true
      - event: dispatchCompleted
        id: "1"
        message: { name: Start }
        signatureId: "Start()"
      - event: dispatchStarted
        id: "2"
        message: { name: initialization }
        signatureId: "initialization()"
      - event: scheduled
        id: "4"
        message: { name: Leak }
        deadline: "1000000"
        publish: true
      - event: diagnostic
        diagnostic: { phase: runtime, code: test.declaredFault, programName: late, handlerName: initialization() }
      - event: dispatchCompleted
        id: "2"
        message: { name: initialization }
        signatureId: "initialization()"
      - event: discarded
        id: "4"
  due:
    accepted: false
    waiting: false
    local: [{ name: Tick }]
    outbound: [{ name: Tick }]
    trace:
      - event: publish
        id: "3"
        message: { name: Tick }
        result: { localAccepted: true, outboundAttempted: true, outboundAccepted: true, anyAccepted: true }
      - event: dispatchStarted
        id: "3"
        message: { name: Tick }
        signatureId: "Tick()"
      - event: dispatchCompleted
        id: "3"
        message: { name: Tick }
        signatureId: "Tick()"
```
