---
formatVersion: 1
suiteId: runtime.host-observer
title: Runtime host observer and publish sinks
kind: scriptApi
level: scenario
categories: [conformance]
tags: [host, observer, publish]
---

# Runtime host observer and publish sinks

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite verifies observer event ordering and every portable publish-sink acceptance and failure mode.

---

## Test: Publish without a sink

This runtime case exercises “Publish without a sink” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: publish-without-sink
publishSink: absent
```

### Source code under test

```ges
on Start() { publish Remote() }
on Remote() {}
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
      - name: Remote
    trace:
      - event: dispatchStarted
        message: { name: Start }
        signatureId: "Start()"
      - event: publish
        message: { name: Remote }
        result: { localAccepted: true, outboundAttempted: false, outboundAccepted: false, anyAccepted: true }
      - event: dispatchCompleted
        message: { name: Start }
        signatureId: "Start()"
      - event: dispatchStarted
        message: { name: Remote }
        signatureId: "Remote()"
      - event: dispatchCompleted
        message: { name: Remote }
        signatureId: "Remote()"
```

---

## Test: Publish accepted by a sink

This runtime case exercises “Publish accepted by a sink” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: publish-accepted
publishSink: accept
```

### Source code under test

```ges
on Start() { publish Remote() }
on Remote() {}
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
      - name: Remote
    outbound:
      - name: Remote
    trace:
      - event: dispatchStarted
        message: { name: Start }
        signatureId: "Start()"
      - event: publish
        message: { name: Remote }
        result: { localAccepted: true, outboundAttempted: true, outboundAccepted: true, anyAccepted: true }
      - event: dispatchCompleted
        message: { name: Start }
        signatureId: "Start()"
      - event: dispatchStarted
        message: { name: Remote }
        signatureId: "Remote()"
      - event: dispatchCompleted
        message: { name: Remote }
        signatureId: "Remote()"
```

---

## Test: Publish rejected by a sink

This runtime case exercises “Publish rejected by a sink” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: publish-rejected
publishSink: reject
```

### Source code under test

```ges
on Start() { publish Remote() }
on Remote() {}
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
      - name: Remote
    outbound:
      - name: Remote
    trace:
      - event: dispatchStarted
        message: { name: Start }
        signatureId: "Start()"
      - event: publish
        message: { name: Remote }
        result: { localAccepted: true, outboundAttempted: true, outboundAccepted: false, anyAccepted: true }
      - event: dispatchCompleted
        message: { name: Start }
        signatureId: "Start()"
      - event: dispatchStarted
        message: { name: Remote }
        signatureId: "Remote()"
      - event: dispatchCompleted
        message: { name: Remote }
        signatureId: "Remote()"
```

---

## Test: Publish sink exception preserves local dispatch

This runtime case exercises “Publish sink exception preserves local dispatch” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: publish-sink-exception
publishSink: throw
```

### Source code under test

```ges
on Start() { publish Remote() }
on Remote() {}
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
      - name: Remote
    outbound:
      - name: Remote
    diagnostics:
      - phase: runtime
        code: runtime.publishSinkFailure
        handlerName: "Start()"
    trace:
      - event: dispatchStarted
        message: { name: Start }
        signatureId: "Start()"
      - event: diagnostic
        diagnostic:
          phase: runtime
          code: runtime.publishSinkFailure
          handlerName: "Start()"
      - event: publish
        message: { name: Remote }
        result: { localAccepted: true, outboundAttempted: true, outboundAccepted: false, anyAccepted: true }
      - event: dispatchCompleted
        message: { name: Start }
        signatureId: "Start()"
      - event: dispatchStarted
        message: { name: Remote }
        signatureId: "Remote()"
      - event: dispatchCompleted
        message: { name: Remote }
        signatureId: "Remote()"
```

---

## Test: Emit and runtime-limit observer order

This case stops before the pending message starts, preserves observer order, and resumes that message on the next pump without reporting another processing limit. The unmatched Probe input is rejected and adds no work before that second pump.

### Case description

```yaml
gesBlock: case
id: emit-and-runtime-limit-order
runtimeLimits:
  maxProcessedEventsPerRun: 1
```

### Source code under test

```ges
on Start() { emit Deferred() }
on Deferred() {}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |
| resume | Probe | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    paused: false
    local:
      - name: Deferred
    runtimeLimits:
      include:
        - name: MaxProcessedEventsPerRun
          limit: 1
    trace:
      - event: dispatchStarted
        message: { name: Start }
        signatureId: "Start()"
      - event: emit
        message: { name: Deferred }
        accepted: true
      - event: dispatchCompleted
        message: { name: Start }
        signatureId: "Start()"
      - event: runtimeLimit
        runtimeLimit:
          name: MaxProcessedEventsPerRun
          limit: 1
  resume:
    accepted: false
    paused: false
    runtimeLimits:
      exclude:
        - name: MaxProcessedEventsPerRun
          limit: 1
    trace:
      - event: dispatchStarted
        message: { name: Deferred }
        signatureId: "Deferred()"
      - event: dispatchCompleted
        message: { name: Deferred }
        signatureId: "Deferred()"
```

---

## Test: Queue limit rejects the newest message and preserves observer order

This runtime case exercises “Queue limit rejects the newest message and preserves observer order” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: queue-limit-observer-order
runtimeLimits:
  maxQueuedMessagesPerRun: 2
nativeHandlers:
  - id: start
    message: Start
    parameters: []
    emit:
      - name: A
        args: []
      - name: B
        args: []
      - name: C
        args: []
  - id: a
    message: A
    parameters: []
  - id: b
    message: B
    parameters: []
  - id: c
    message: C
    parameters: []
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
      - name: A
      - name: B
      - name: C
    runtimeLimits:
      include:
        - name: MaxQueuedMessagesPerRun
          limit: 2
    trace:
      - event: dispatchStarted
        message: { name: Start }
        signatureId: "Start()"
      - event: emit
        message: { name: A }
        accepted: true
      - event: emit
        message: { name: B }
        accepted: true
      - event: runtimeLimit
        runtimeLimit:
          name: MaxQueuedMessagesPerRun
          limit: 2
      - event: emit
        message: { name: C }
        accepted: false
      - event: dispatchCompleted
        message: { name: Start }
        signatureId: "Start()"
      - event: dispatchStarted
        message: { name: A }
        signatureId: "A()"
      - event: dispatchCompleted
        message: { name: A }
        signatureId: "A()"
      - event: dispatchStarted
        message: { name: B }
        signatureId: "B()"
      - event: dispatchCompleted
        message: { name: B }
        signatureId: "B()"
```

---

## Test: Exactly 1 script messages finish at the limit during completion

This case reaches the configured message count exactly while draining all work.
The final dispatch must complete without reporting MaxProcessedEventsPerRun;
the existing pending-message case supplies the over-limit control.

### Case description

```yaml
gesBlock: case
id: processed-limit-script-completion-1
runtimeLimits:
  maxProcessedEventsPerRun: 1
```

### Source code under test

```ges
on Start() {}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    paused: false
    runtimeLimits:
      exclude:
        - name: MaxProcessedEventsPerRun
          limit: 1
    trace:
      - event: dispatchStarted
        message: { name: Start }
        signatureId: "Start()"
      - event: dispatchCompleted
        message: { name: Start }
        signatureId: "Start()"
```

---

## Test: Exactly 2 script messages finish at the limit during completion

This case reaches the configured message count exactly while draining all work.
The final dispatch must complete without reporting MaxProcessedEventsPerRun;
the existing pending-message case supplies the over-limit control.

### Case description

```yaml
gesBlock: case
id: processed-limit-script-completion-2
runtimeLimits:
  maxProcessedEventsPerRun: 2
```

### Source code under test

```ges
on Start() { emit Deferred() }
on Deferred() {}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    paused: false
    local:
      - name: Deferred
    runtimeLimits:
      exclude:
        - name: MaxProcessedEventsPerRun
          limit: 2
    trace:
      - event: dispatchStarted
        message: { name: Start }
        signatureId: "Start()"
      - event: emit
        message: { name: Deferred }
        accepted: true
      - event: dispatchCompleted
        message: { name: Start }
        signatureId: "Start()"
      - event: dispatchStarted
        message: { name: Deferred }
        signatureId: "Deferred()"
      - event: dispatchCompleted
        message: { name: Deferred }
        signatureId: "Deferred()"
```

---

## Test: Exactly 1 script messages finish at the limit during frame

This case reaches the configured message count exactly while draining all work.
The final dispatch must complete without reporting MaxProcessedEventsPerRun;
the existing pending-message case supplies the over-limit control.

### Case description

```yaml
gesBlock: case
id: processed-limit-script-frame-1
runtimeLimits:
  maxProcessedEventsPerRun: 1
```

### Source code under test

```ges
on Start() {}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | frame | 1000 |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    paused: false
    runtimeLimits:
      exclude:
        - name: MaxProcessedEventsPerRun
          limit: 1
    trace:
      - event: dispatchStarted
        message: { name: Start }
        signatureId: "Start()"
      - event: dispatchCompleted
        message: { name: Start }
        signatureId: "Start()"
```

---

## Test: Exactly 2 script messages finish at the limit during frame

This case reaches the configured message count exactly while draining all work.
The final dispatch must complete without reporting MaxProcessedEventsPerRun;
the existing pending-message case supplies the over-limit control.

### Case description

```yaml
gesBlock: case
id: processed-limit-script-frame-2
runtimeLimits:
  maxProcessedEventsPerRun: 2
```

### Source code under test

```ges
on Start() { emit Deferred() }
on Deferred() {}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | frame | 1000 |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    paused: false
    local:
      - name: Deferred
    runtimeLimits:
      exclude:
        - name: MaxProcessedEventsPerRun
          limit: 2
    trace:
      - event: dispatchStarted
        message: { name: Start }
        signatureId: "Start()"
      - event: emit
        message: { name: Deferred }
        accepted: true
      - event: dispatchCompleted
        message: { name: Start }
        signatureId: "Start()"
      - event: dispatchStarted
        message: { name: Deferred }
        signatureId: "Deferred()"
      - event: dispatchCompleted
        message: { name: Deferred }
        signatureId: "Deferred()"
```

---

## Test: Exactly 1 native messages finish at the limit during completion

This case reaches the configured message count exactly while draining all work.
The final dispatch must complete without reporting MaxProcessedEventsPerRun;
the existing pending-message case supplies the over-limit control.

### Case description

```yaml
gesBlock: case
id: processed-limit-native-completion-1
runtimeLimits:
  maxProcessedEventsPerRun: 1
nativeHandlers:
  - id: start
    message: Start
    parameters: []
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    paused: false
    runtimeLimits:
      exclude:
        - name: MaxProcessedEventsPerRun
          limit: 1
    trace:
      - event: dispatchStarted
        message: { name: Start }
        signatureId: "Start()"
      - event: dispatchCompleted
        message: { name: Start }
        signatureId: "Start()"
```

---

## Test: Exactly 2 native messages finish at the limit during completion

This case reaches the configured message count exactly while draining all work.
The final dispatch must complete without reporting MaxProcessedEventsPerRun;
the existing pending-message case supplies the over-limit control.

### Case description

```yaml
gesBlock: case
id: processed-limit-native-completion-2
runtimeLimits:
  maxProcessedEventsPerRun: 2
nativeHandlers:
  - id: start
    message: Start
    parameters: []
    emit:
      - name: Deferred
        args: []
  - id: deferred
    message: Deferred
    parameters: []
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    paused: false
    local:
      - name: Deferred
    runtimeLimits:
      exclude:
        - name: MaxProcessedEventsPerRun
          limit: 2
    trace:
      - event: dispatchStarted
        message: { name: Start }
        signatureId: "Start()"
      - event: emit
        message: { name: Deferred }
        accepted: true
      - event: dispatchCompleted
        message: { name: Start }
        signatureId: "Start()"
      - event: dispatchStarted
        message: { name: Deferred }
        signatureId: "Deferred()"
      - event: dispatchCompleted
        message: { name: Deferred }
        signatureId: "Deferred()"
```

---

## Test: Exactly 1 native messages finish at the limit during frame

This case reaches the configured message count exactly while draining all work.
The final dispatch must complete without reporting MaxProcessedEventsPerRun;
the existing pending-message case supplies the over-limit control.

### Case description

```yaml
gesBlock: case
id: processed-limit-native-frame-1
runtimeLimits:
  maxProcessedEventsPerRun: 1
nativeHandlers:
  - id: start
    message: Start
    parameters: []
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | frame | 1000 |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    paused: false
    runtimeLimits:
      exclude:
        - name: MaxProcessedEventsPerRun
          limit: 1
    trace:
      - event: dispatchStarted
        message: { name: Start }
        signatureId: "Start()"
      - event: dispatchCompleted
        message: { name: Start }
        signatureId: "Start()"
```

---

## Test: Exactly 2 native messages finish at the limit during frame

This case reaches the configured message count exactly while draining all work.
The final dispatch must complete without reporting MaxProcessedEventsPerRun;
the existing pending-message case supplies the over-limit control.

### Case description

```yaml
gesBlock: case
id: processed-limit-native-frame-2
runtimeLimits:
  maxProcessedEventsPerRun: 2
nativeHandlers:
  - id: start
    message: Start
    parameters: []
    emit:
      - name: Deferred
        args: []
  - id: deferred
    message: Deferred
    parameters: []
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | frame | 1000 |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    paused: false
    local:
      - name: Deferred
    runtimeLimits:
      exclude:
        - name: MaxProcessedEventsPerRun
          limit: 2
    trace:
      - event: dispatchStarted
        message: { name: Start }
        signatureId: "Start()"
      - event: emit
        message: { name: Deferred }
        accepted: true
      - event: dispatchCompleted
        message: { name: Start }
        signatureId: "Start()"
      - event: dispatchStarted
        message: { name: Deferred }
        signatureId: "Deferred()"
      - event: dispatchCompleted
        message: { name: Deferred }
        signatureId: "Deferred()"
```

---

## Test: Pending native message resumes after a completion processing-limit stop

This case stops before the pending message starts, preserves observer order, and resumes that message on the next pump without reporting another processing limit. The unmatched Probe input is rejected and adds no work before that second pump.

### Case description

```yaml
gesBlock: case
id: processed-limit-pending-native-completion
runtimeLimits:
  maxProcessedEventsPerRun: 1
nativeHandlers:
  - id: start
    message: Start
    parameters: []
    emit:
      - name: Deferred
        args: []
  - id: deferred
    message: Deferred
    parameters: []
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion | |
| resume | Probe | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    paused: false
    local:
      - name: Deferred
    runtimeLimits:
      include:
        - name: MaxProcessedEventsPerRun
          limit: 1
    trace:
      - event: dispatchStarted
        message: { name: Start }
        signatureId: "Start()"
      - event: emit
        message: { name: Deferred }
        accepted: true
      - event: dispatchCompleted
        message: { name: Start }
        signatureId: "Start()"
      - event: runtimeLimit
        runtimeLimit:
          name: MaxProcessedEventsPerRun
          limit: 1
  resume:
    accepted: false
    paused: false
    runtimeLimits:
      exclude:
        - name: MaxProcessedEventsPerRun
          limit: 1
    trace:
      - event: dispatchStarted
        message: { name: Deferred }
        signatureId: "Deferred()"
      - event: dispatchCompleted
        message: { name: Deferred }
        signatureId: "Deferred()"
```

---

## Test: Pending native message resumes after a frame processing-limit stop

This case stops before the pending message starts, preserves observer order, and resumes that message on the next pump without reporting another processing limit. The unmatched Probe input is rejected and adds no work before that second pump.

### Case description

```yaml
gesBlock: case
id: processed-limit-pending-native-frame
runtimeLimits:
  maxProcessedEventsPerRun: 1
nativeHandlers:
  - id: start
    message: Start
    parameters: []
    emit:
      - name: Deferred
        args: []
  - id: deferred
    message: Deferred
    parameters: []
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | frame | 1000 |
| resume | Probe | frame | 1000 |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    paused: false
    local:
      - name: Deferred
    runtimeLimits:
      include:
        - name: MaxProcessedEventsPerRun
          limit: 1
    trace:
      - event: dispatchStarted
        message: { name: Start }
        signatureId: "Start()"
      - event: emit
        message: { name: Deferred }
        accepted: true
      - event: dispatchCompleted
        message: { name: Start }
        signatureId: "Start()"
      - event: runtimeLimit
        runtimeLimit:
          name: MaxProcessedEventsPerRun
          limit: 1
  resume:
    accepted: false
    paused: false
    runtimeLimits:
      exclude:
        - name: MaxProcessedEventsPerRun
          limit: 1
    trace:
      - event: dispatchStarted
        message: { name: Deferred }
        signatureId: "Deferred()"
      - event: dispatchCompleted
        message: { name: Deferred }
        signatureId: "Deferred()"
```

---

## Test: Pending script message resumes after a frame processing-limit stop

This case stops before the pending message starts, preserves observer order, and resumes that message on the next pump without reporting another processing limit. The unmatched Probe input is rejected and adds no work before that second pump.

### Case description

```yaml
gesBlock: case
id: processed-limit-pending-script-frame
runtimeLimits:
  maxProcessedEventsPerRun: 1
```

### Source code under test

```ges
on Start() { emit Deferred() }
on Deferred() {}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | frame | 1000 |
| resume | Probe | frame | 1000 |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    paused: false
    local:
      - name: Deferred
    runtimeLimits:
      include:
        - name: MaxProcessedEventsPerRun
          limit: 1
    trace:
      - event: dispatchStarted
        message: { name: Start }
        signatureId: "Start()"
      - event: emit
        message: { name: Deferred }
        accepted: true
      - event: dispatchCompleted
        message: { name: Start }
        signatureId: "Start()"
      - event: runtimeLimit
        runtimeLimit:
          name: MaxProcessedEventsPerRun
          limit: 1
  resume:
    accepted: false
    paused: false
    runtimeLimits:
      exclude:
        - name: MaxProcessedEventsPerRun
          limit: 1
    trace:
      - event: dispatchStarted
        message: { name: Deferred }
        signatureId: "Deferred()"
      - event: dispatchCompleted
        message: { name: Deferred }
        signatureId: "Deferred()"
```

---

## Test: A generated message chain finishes below the processing limit

This case processes two logical messages with a limit of three. Both dispatches complete without a processing-limit observation, including the message emitted by the first handler.

### Case description

```yaml
gesBlock: case
id: processed-limit-below-script-completion
runtimeLimits:
  maxProcessedEventsPerRun: 3
```

### Source code under test

```ges
on Start() { emit Deferred() }
on Deferred() {}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| run | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  run:
    paused: false
    local:
      - name: Deferred
    runtimeLimits:
      exclude:
        - name: MaxProcessedEventsPerRun
          limit: 3
    trace:
      - event: dispatchStarted
        message: { name: Start }
        signatureId: "Start()"
      - event: emit
        message: { name: Deferred }
        accepted: true
      - event: dispatchCompleted
        message: { name: Start }
        signatureId: "Start()"
      - event: dispatchStarted
        message: { name: Deferred }
        signatureId: "Deferred()"
      - event: dispatchCompleted
        message: { name: Deferred }
        signatureId: "Deferred()"
```
