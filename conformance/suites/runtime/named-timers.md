---
formatVersion: 1
suiteId: runtime.named-timers
title: Named timers and host reset
kind: scriptApi
level: scenario
categories: [conformance]
tags: [host, delayed, timers, reset]
---

# Named timers, heartbeats and host reset

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

These cases verify named delayed sends and repeating `every` heartbeats: a name replaces the previous entry of the same sending instance, `cancel` removes pending and promoted entries, a heartbeat re-arms one period after its logical message completes without catching up, and detaching the sender ends it. They also verify the host reset, which clears queues and timers while keeping Programs and the random state, and the pending-timer snapshot.

---

## Test: a name replaces the previous entry of the sender

The second named send discards the first entry of the same name, reports it as discarded and is scheduled under the same name. Text and Tag spellings name the same timer, and emit and publish share one namespace.

### Case description

```yaml
gesBlock: case
id: name-replaces-previous-entry
sources:
  - name: replace.ges
    program: main
publishSink: accept
stepActions:
  due:
    - advanceMicroseconds: "2000000"
```

### Source code under test

```ges
module main
on Start {
  emit after 2s named 'a' Ping(n: 1)
  emit after 1s named #a Ping(n: 2)
  publish after 1s named 'b' Out()
  emit after 1s named #b Ping(n: 3)
}
on Ping(n) { emit Seen(n: n) }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| begin | Start | completion | |
| due | Unknown | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  begin:
    waiting: true
    pendingTimers:
      - { name: "'a'", deadline: "1000000", period: "0", message: Ping, program: main, publish: false }
      - { name: "'b'", deadline: "1000000", period: "0", message: Ping, program: main, publish: false }
    local:
      - { name: Ping, args: [{ name: n, value: { type: ":Number.int64", value: "1" } }] }
      - { name: Ping, args: [{ name: n, value: { type: ":Number.int64", value: "2" } }] }
      - { name: Ping, args: [{ name: n, value: { type: ":Number.int64", value: "3" } }] }
  due:
    accepted: false
    waiting: false
    pendingTimers: []
    local:
      - { name: Seen, args: [{ name: n, value: { type: ":Number.int64", value: "2" } }] }
      - { name: Seen, args: [{ name: n, value: { type: ":Number.int64", value: "3" } }] }
```

---

## Test: cancel removes a pending entry before it is due

### Case description

```yaml
gesBlock: case
id: cancel-before-promotion
sources:
  - name: cancel.ges
    program: main
stepActions:
  due:
    - advanceMicroseconds: "2000000"
```

### Source code under test

```ges
module main
on Start { emit after 1s named 'x' Ping() }
on Stop {
  let first be cancel 'x'
  let second be cancel 'x'
  emit Done(first: first, second: second)
}
on Ping { emit Seen() }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| begin | Start | completion | |
| stop | Stop | completion | |
| due | Unknown | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  begin:
    waiting: true
    local: [{ name: Ping }]
  stop:
    waiting: false
    pendingTimers: []
    local:
      - name: Done
        args:
          - { name: first, value: { type: ":Boolean", value: true } }
          - { name: second, value: { type: ":Boolean", value: false } }
  due:
    accepted: false
    local: []
```

---

## Test: cancel removes a promoted firing that is not yet dispatched

Two entries become due together. The first handler cancels the second, which is already promoted to the ready queue, and its own completed one-shot name, which no longer exists.

### Case description

```yaml
gesBlock: case
id: cancel-after-promotion
sources:
  - name: promoted.ges
    program: main
stepActions:
  due:
    - advanceMicroseconds: "1000000"
```

### Source code under test

```ges
module main
on Start {
  emit after 1s named 'a' First()
  emit after 1s named 'b' Second()
}
on First {
  let second be cancel 'b'
  let own be cancel 'a'
  emit Done(second: second, own: own)
}
on Second { emit Seen() }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| begin | Start | completion | |
| due | Unknown | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  begin:
    local: [{ name: First }, { name: Second }]
  due:
    accepted: false
    waiting: false
    pendingTimers: []
    local:
      - name: Done
        args:
          - { name: second, value: { type: ":Boolean", value: true } }
          - { name: own, value: { type: ":Boolean", value: false } }
```

---

## Test: a heartbeat handler cancels its own heartbeat

### Case description

```yaml
gesBlock: case
id: cancel-from-every-handler
sources:
  - name: beat-cancel.ges
    program: main
stepActions:
  first:
    - advanceMicroseconds: "1000000"
  later:
    - advanceMicroseconds: "3000000"
```

### Source code under test

```ges
module main
on Start { emit every 1s named 'beat' Beat() }
on Beat {
  let removed be cancel 'beat'
  emit Done(removed: removed)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| begin | Start | completion | |
| first | Unknown | completion | |
| later | Unknown | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  begin:
    pendingTimers:
      - { name: "'beat'", deadline: "1000000", period: "1000000", message: Beat, program: main, publish: false }
  first:
    accepted: false
    waiting: false
    pendingTimers: []
    local:
      - { name: Beat }
      - { name: Done, args: [{ name: removed, value: { type: ":Boolean", value: true } }] }
  later:
    accepted: false
    local: []
```

---

## Test: a heartbeat re-arms without catching up

After a pause of three and a half periods the heartbeat fires once and re-arms one period after its message completes.

### Case description

```yaml
gesBlock: case
id: every-rearms-without-catch-up
sources:
  - name: beat.ges
    program: main
stepActions:
  late:
    - advanceMicroseconds: "3500000"
```

### Source code under test

```ges
module main
on Start { emit every 1s named 'beat' Beat() }
on Beat { emit Seen() }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| begin | Start | completion | |
| late | Unknown | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  late:
    accepted: false
    waiting: true
    local: [{ name: Beat }, { name: Seen }]
    pendingTimers:
      - { name: "'beat'", deadline: "4500000", period: "1000000", message: Beat, program: main, publish: false }
```

---

## Test: a paused heartbeat handler re-arms after it completes

The heartbeat handler pauses at the frame budget. While its firing is in flight the timer stays listed with its old deadline; it re-arms one period after the clock reading at which the handler completes.

### Case description

```yaml
gesBlock: case
id: every-rearms-after-paused-handler
sources:
  - name: paused.ges
    program: main
stepActions:
  fire:
    - advanceMicroseconds: "1000000"
  finish:
    - advanceMicroseconds: "500000"
```

### Source code under test

```ges
module main
on Start { emit every 1s named 'beat' Beat() }
on Beat {
  let total be (from 1 to 200)[:fold acc be 0, item => acc + item]
  emit Seen(total: total)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| begin | Start | completion | |
| fire | Unknown | frame | 20 |
| finish | Unknown | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  fire:
    paused: true
    accepted: false
    local:
      - { name: Beat }
    pendingTimers:
      - { name: "'beat'", deadline: "1000000", period: "1000000", message: Beat, program: main, publish: false }
  finish:
    accepted: false
    local:
      - { name: Seen, args: [{ name: total, value: { type: ":Number.int64", value: "20100" } }] }
    pendingTimers:
      - { name: "'beat'", deadline: "2500000", period: "1000000", message: Beat, program: main, publish: false }
```

---

## Test: equal deadlines keep scheduling order and a replacement orders last

### Case description

```yaml
gesBlock: case
id: equal-deadlines-and-replacement-order
sources:
  - name: order.ges
    program: main
stepActions:
  due:
    - advanceMicroseconds: "1000000"
```

### Source code under test

```ges
module main
on Start {
  emit after 1s named 'a' Ping(n: 1)
  emit after 1s named 'b' Ping(n: 2)
  emit after 1s named 'a' Ping(n: 3)
}
on Ping(n) { emit Seen(n: n) }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| begin | Start | completion | |
| due | Unknown | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  begin:
    local:
      - { name: Ping, args: [{ name: n, value: { type: ":Number.int64", value: "1" } }] }
      - { name: Ping, args: [{ name: n, value: { type: ":Number.int64", value: "2" } }] }
      - { name: Ping, args: [{ name: n, value: { type: ":Number.int64", value: "3" } }] }
    pendingTimers:
      - { name: "'b'", deadline: "1000000", period: "0", message: Ping, program: main, publish: false }
      - { name: "'a'", deadline: "1000000", period: "0", message: Ping, program: main, publish: false }
  due:
    accepted: false
    local:
      - { name: Seen, args: [{ name: n, value: { type: ":Number.int64", value: "2" } }] }
      - { name: Seen, args: [{ name: n, value: { type: ":Number.int64", value: "3" } }] }
```

---

## Test: the named-timer limit counts a replacement once

### Case description

```yaml
gesBlock: case
id: named-timer-limit-with-replacement
sources:
  - name: limit.ges
    program: main
runtimeLimits:
  maxNamedTimers: 1
```

### Source code under test

```ges
module main
on Start {
  let first be emit after 1s named 'a' Ping()
  let replaced be emit after 2s named 'a' Ping()
  let other be emit after 1s named 'b' Ping()
  emit Done(first: first, replaced: replaced, other: other)
}
on Ping {}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| begin | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  begin:
    pendingTimers:
      - { name: "'a'", deadline: "2000000", period: "0", message: Ping, program: main, publish: false }
    local: [{ name: Ping }, { name: Ping }]
    runtimeLimits:
      include:
        - name: MaxNamedTimers
          limit: 1
```

---

## Test: detaching the sender ends its heartbeat

### Case description

```yaml
gesBlock: case
id: detach-sender-ends-every
sources:
  - name: sender.ges
    program: main
  - name: observer.ges
    program: observer
stepActions:
  first:
    - advanceMicroseconds: "1000000"
  detached:
    - detachProgram: main
      expectResult: true
    - advanceMicroseconds: "3000000"
```

### Source code under test

```ges
module main
on Start { emit every 1s named 'beat' Beat() }
```

```ges
module observer
on Beat { emit Seen() }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| begin | Start | completion | |
| first | Unknown | completion | |
| detached | Unknown | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  first:
    accepted: false
    local: [{ name: Beat }, { name: Seen }]
    pendingTimers:
      - { name: "'beat'", deadline: "2000000", period: "1000000", message: Beat, program: main, publish: false }
  detached:
    accepted: false
    waiting: false
    local: []
    pendingTimers: []
```

---

## Test: reset clears pending timers and Start reruns initialization

### Case description

```yaml
gesBlock: case
id: reset-clears-timers-and-restarts
sources:
  - name: reset.ges
    program: main
stepActions:
  reset:
    - resetHost: host
      expectResult: true
    - startHost: host
      expectResult: true
  later:
    - advanceMicroseconds: "10000000"
```

### Source code under test

```ges
module main
on initialization { emit Ready() }
on Start {
  emit after 1s named 'a' Ping()
  emit after 5s Later()
}
on Ping { emit Seen() }
on Later { emit Seen() }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| begin | Start | completion | |
| reset | Unknown | completion | |
| later | Unknown | completion | |

### Expectation

```yaml
gesBlock: expect
initialization:
  local: [{ name: Ready }]
steps:
  begin:
    waiting: true
    local: [{ name: Ping }, { name: Later }]
  reset:
    accepted: false
    waiting: false
    pendingTimers: []
    local: [{ name: Ready }]
  later:
    accepted: false
    local: []
```

---

## Test: the random state continues across a reset

### Case description

```yaml
gesBlock: case
id: reset-keeps-random-state
sources:
  - name: random.ges
    program: main
random:
  sequence: ["50", "10"]
stepActions:
  second:
    - resetHost: host
      expectResult: true
    - startHost: host
      expectResult: true
```

### Source code under test

```ges
module main
on Start { emit Drawn(value: random from 1 to 100) }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| first | Start | completion | |
| second | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  first:
    local:
      - name: Drawn
        args:
          - name: value
            value: { type: ":Number.int64", value: "50" }
  second:
    local:
      - name: Drawn
        args:
          - name: value
            value: { type: ":Number.int64", value: "10" }
```

---

## Test: a native handler cannot reset its host

### Case description

```yaml
gesBlock: case
id: reset-rejected-during-dispatch
nativeHandlers:
  - id: controller
    message: Trigger
    actions:
      - resetHost: host
        expectResult: false
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Trigger | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    accepted: true
```

---

## Test: a reset after a failed start keeps no Programs of the failed group

The failed start already detached its initial group, so the reset returns a loading host without those Programs; a Program loaded afterwards forms the next initial group.

### Case description

```yaml
gesBlock: case
id: reset-after-failed-start
deferredPrograms: [late]
sources:
  - name: failing.ges
    program: main
  - name: late.ges
    program: late
stepActions:
  retry:
    - resetHost: host
      expectResult: true
    - loadProgram: late
    - startHost: host
      expectResult: true
```

### Source code under test

```ges
module main
on initialization { let failure be :test.declaredFault() }
on Ping { emit Old() }
```

```ges
module late
on Ping { emit Pong() }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| retry | Ping | completion | |

### Expectation

```yaml
gesBlock: expect
initialization:
  hostReady: false
  programStarts: { main: runtimeError }
  diagnostics:
    - phase: runtime
      code: test.declaredFault
      programName: main
      handlerName: initialization()
steps:
  retry:
    accepted: true
    hostReady: true
    programStarts: { main: runtimeError, late: ready }
    local: [{ name: Pong }]
```

---

## Test: a heartbeat without a positive period is rejected

### Case description

```yaml
gesBlock: case
id: every-zero-period-rejected
sources:
  - name: zero.ges
    program: main
```

### Source code under test

```ges
module main
on Start {
  let ok be emit every 0s named 'z' Ping()
  emit Done(ok: ok)
}
on Ping {}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| begin | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  begin:
    waiting: false
    pendingTimers: []
    local:
      - name: Done
        args:
          - name: ok
            value: { type: ":Boolean", value: false }
```

---

## Test: cancel named keeps unnamed entries and cancel all removes them

### Case description

```yaml
gesBlock: case
id: cancel-named-and-all
sources:
  - name: modes.ges
    program: main
```

### Source code under test

```ges
module main
on Start {
  emit after 1s named #a Ping()
  emit after 1s Ping()
  let named1 be cancel named
  let named2 be cancel named
  let all1 be cancel all
  let all2 be cancel all
  emit Done(named1: named1, named2: named2, all1: all1, all2: all2)
}
on Ping {}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| begin | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  begin:
    waiting: false
    pendingTimers: []
    local:
      - { name: Ping }
      - { name: Ping }
      - name: Done
        args:
          - { name: named1, value: { type: ":Boolean", value: true } }
          - { name: named2, value: { type: ":Boolean", value: false } }
          - { name: all1, value: { type: ":Boolean", value: true } }
          - { name: all2, value: { type: ":Boolean", value: false } }
```

---

## Test: every requires a name

### Case description

```yaml
gesBlock: case
id: every-requires-name
kind: compileError
sources:
  - name: unnamed.ges
    program: main
```

### Source code under test

```ges
on Start { emit every 1s Beat() }
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "parse"
  code: "parse.syntax"
  line: 1
  column: 26
```

---

## Test: a statically non-text timer name is rejected

### Case description

```yaml
gesBlock: case
id: static-non-text-timer-name
kind: compileError
sources:
  - name: number-name.ges
    program: main
```

### Source code under test

```ges
on Start {
  let key be 3
  emit after 1s named key Beat()
}
```

### Expectation

```yaml
gesBlock: expect
error:
  phase: "validate"
  code: "validate.invalidTypeConstructor"
  line: 3
  column: 23
```
