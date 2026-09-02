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

This runtime case exercises “Emit and runtime-limit observer order” and verifies the declared messages, values, and execution result.

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

### Expectation

```yaml
gesBlock: expect
steps:
  run:
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
