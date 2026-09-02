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

These cases define the portable host-observer order and every V1 publish-sink
mode. A sink invocation is recorded in `outbound` even when it rejects or
throws, because the channel describes handoff attempts rather than acceptance.

## Test: Publish without a sink

```yaml
gesBlock: case
id: publish-without-sink
publishSink: absent
```

```ges
on Start() { publish Remote() }
on Remote() {}
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

## Test: Publish accepted by a sink

```yaml
gesBlock: case
id: publish-accepted
publishSink: accept
```

```ges
on Start() { publish Remote() }
on Remote() {}
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

## Test: Publish rejected by a sink

```yaml
gesBlock: case
id: publish-rejected
publishSink: reject
```

```ges
on Start() { publish Remote() }
on Remote() {}
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

## Test: Publish sink exception preserves local dispatch

```yaml
gesBlock: case
id: publish-sink-exception
publishSink: throw
```

```ges
on Start() { publish Remote() }
on Remote() {}
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

## Test: Emit and runtime-limit observer order

```yaml
gesBlock: case
id: emit-and-runtime-limit-order
runtimeLimits:
  maxProcessedEventsPerRun: 1
```

```ges
on Start() { emit Deferred() }
on Deferred() {}
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
