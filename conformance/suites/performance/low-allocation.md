---
formatVersion: 1
suiteId: "performance.low-allocation"
title: "Low Allocation Dispatch"
categories: [conformance, allocation]
---

# Low Allocation Dispatch

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite checks warmed dispatch with reused inputs and no new payloads. Exact and message-name handlers share zero-allocation expectations, with and without a counting observer. Completion uses one handler; frames use two handlers and a one-opcode budget. Paired N and 2N batches expose recurring allocations. Correctness traces are collected separately from measurement even when the measured observer is disabled.

The managed profile measures cumulative bytes on the calling .NET thread. Other heaps are outside that evidence; ports require their own instrumentation and profile. The macOS profile entry also permits the existing deterministic corpus adapter to check correctness and report shape; its echoed values are not allocation measurements.

---

## Test: exact-completion-observer-off-1000

This case dispatches 1000 reused inputs to 1 exact handler using completion, with the measured observer absent, and permits zero bytes after warmup.

### Case description

```yaml
gesBlock: case
id: exact-completion-observer-off-1000
kind: performance
level: scenario
performance:
  iterations: 1000
  warmupIterations: 100
  compileWarmupIterations: 3
  observeRuntime: false
sources:
  - name: dispatch.ges
    program: main
```

### Source code under test

```ges
module allocationdispatch

on Start() {}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| dispatch | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  dispatch:
    accepted: true
    local: []
    outbound: []
    paused: false
    runtimeLimits:
      exclude: [{ any: true }]
    diagnostics: []
    trace:
      - event: dispatchStarted
        message: { name: Start }
        signatureId: "Start()"
      - event: dispatchCompleted
        message: { name: Start }
        signatureId: "Start()"
performance:
  profiles:
    csharp-dotnet-release-managed:
      metrics:
        run.allocated: { reference: 0, maximum: 0, unit: B }
    csharp-dotnet-release-macos-arm64:
      metrics:
        run.allocated: { reference: 0, maximum: 0, unit: B }
```

---

## Test: exact-completion-observer-off-2000

This case dispatches 2000 reused inputs to 1 exact handler using completion, with the measured observer absent, and permits zero bytes after warmup.

### Case description

```yaml
gesBlock: case
id: exact-completion-observer-off-2000
kind: performance
level: scenario
performance:
  iterations: 2000
  warmupIterations: 100
  compileWarmupIterations: 3
  observeRuntime: false
sources:
  - name: dispatch.ges
    program: main
```

### Source code under test

```ges
module allocationdispatch

on Start() {}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| dispatch | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  dispatch:
    accepted: true
    local: []
    outbound: []
    paused: false
    runtimeLimits:
      exclude: [{ any: true }]
    diagnostics: []
    trace:
      - event: dispatchStarted
        message: { name: Start }
        signatureId: "Start()"
      - event: dispatchCompleted
        message: { name: Start }
        signatureId: "Start()"
performance:
  profiles:
    csharp-dotnet-release-managed:
      metrics:
        run.allocated: { reference: 0, maximum: 0, unit: B }
    csharp-dotnet-release-macos-arm64:
      metrics:
        run.allocated: { reference: 0, maximum: 0, unit: B }
```

---

## Test: exact-completion-observer-on-1000

This case dispatches 1000 reused inputs to 1 exact handler using completion, with the measured observer enabled, and permits zero bytes after warmup.

### Case description

```yaml
gesBlock: case
id: exact-completion-observer-on-1000
kind: performance
level: scenario
performance:
  iterations: 1000
  warmupIterations: 100
  compileWarmupIterations: 3
  observeRuntime: true
sources:
  - name: dispatch.ges
    program: main
```

### Source code under test

```ges
module allocationdispatch

on Start() {}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| dispatch | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  dispatch:
    accepted: true
    local: []
    outbound: []
    paused: false
    runtimeLimits:
      exclude: [{ any: true }]
    diagnostics: []
    trace:
      - event: dispatchStarted
        message: { name: Start }
        signatureId: "Start()"
      - event: dispatchCompleted
        message: { name: Start }
        signatureId: "Start()"
performance:
  profiles:
    csharp-dotnet-release-managed:
      metrics:
        run.allocated: { reference: 0, maximum: 0, unit: B }
    csharp-dotnet-release-macos-arm64:
      metrics:
        run.allocated: { reference: 0, maximum: 0, unit: B }
```

---

## Test: exact-completion-observer-on-2000

This case dispatches 2000 reused inputs to 1 exact handler using completion, with the measured observer enabled, and permits zero bytes after warmup.

### Case description

```yaml
gesBlock: case
id: exact-completion-observer-on-2000
kind: performance
level: scenario
performance:
  iterations: 2000
  warmupIterations: 100
  compileWarmupIterations: 3
  observeRuntime: true
sources:
  - name: dispatch.ges
    program: main
```

### Source code under test

```ges
module allocationdispatch

on Start() {}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| dispatch | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  dispatch:
    accepted: true
    local: []
    outbound: []
    paused: false
    runtimeLimits:
      exclude: [{ any: true }]
    diagnostics: []
    trace:
      - event: dispatchStarted
        message: { name: Start }
        signatureId: "Start()"
      - event: dispatchCompleted
        message: { name: Start }
        signatureId: "Start()"
performance:
  profiles:
    csharp-dotnet-release-managed:
      metrics:
        run.allocated: { reference: 0, maximum: 0, unit: B }
    csharp-dotnet-release-macos-arm64:
      metrics:
        run.allocated: { reference: 0, maximum: 0, unit: B }
```

---

## Test: message-name-completion-observer-off-1000

This case dispatches 1000 reused inputs to 1 message-name handler using completion, with the measured observer absent, and permits zero bytes after warmup.

### Case description

```yaml
gesBlock: case
id: message-name-completion-observer-off-1000
kind: performance
level: scenario
performance:
  iterations: 1000
  warmupIterations: 100
  compileWarmupIterations: 3
  observeRuntime: false
sources:
  - name: dispatch.ges
    program: main
```

### Source code under test

```ges
module allocationdispatch

on Start as incoming {}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| dispatch | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  dispatch:
    accepted: true
    local: []
    outbound: []
    paused: false
    runtimeLimits:
      exclude: [{ any: true }]
    diagnostics: []
    trace:
      - event: dispatchStarted
        message: { name: Start }
        signatureId: "Start(*)"
      - event: dispatchCompleted
        message: { name: Start }
        signatureId: "Start(*)"
performance:
  profiles:
    csharp-dotnet-release-managed:
      metrics:
        run.allocated: { reference: 0, maximum: 0, unit: B }
    csharp-dotnet-release-macos-arm64:
      metrics:
        run.allocated: { reference: 0, maximum: 0, unit: B }
```

---

## Test: message-name-completion-observer-off-2000

This case dispatches 2000 reused inputs to 1 message-name handler using completion, with the measured observer absent, and permits zero bytes after warmup.

### Case description

```yaml
gesBlock: case
id: message-name-completion-observer-off-2000
kind: performance
level: scenario
performance:
  iterations: 2000
  warmupIterations: 100
  compileWarmupIterations: 3
  observeRuntime: false
sources:
  - name: dispatch.ges
    program: main
```

### Source code under test

```ges
module allocationdispatch

on Start as incoming {}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| dispatch | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  dispatch:
    accepted: true
    local: []
    outbound: []
    paused: false
    runtimeLimits:
      exclude: [{ any: true }]
    diagnostics: []
    trace:
      - event: dispatchStarted
        message: { name: Start }
        signatureId: "Start(*)"
      - event: dispatchCompleted
        message: { name: Start }
        signatureId: "Start(*)"
performance:
  profiles:
    csharp-dotnet-release-managed:
      metrics:
        run.allocated: { reference: 0, maximum: 0, unit: B }
    csharp-dotnet-release-macos-arm64:
      metrics:
        run.allocated: { reference: 0, maximum: 0, unit: B }
```

---

## Test: message-name-completion-observer-on-1000

This case dispatches 1000 reused inputs to 1 message-name handler using completion, with the measured observer enabled, and permits zero bytes after warmup.

### Case description

```yaml
gesBlock: case
id: message-name-completion-observer-on-1000
kind: performance
level: scenario
performance:
  iterations: 1000
  warmupIterations: 100
  compileWarmupIterations: 3
  observeRuntime: true
sources:
  - name: dispatch.ges
    program: main
```

### Source code under test

```ges
module allocationdispatch

on Start as incoming {}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| dispatch | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  dispatch:
    accepted: true
    local: []
    outbound: []
    paused: false
    runtimeLimits:
      exclude: [{ any: true }]
    diagnostics: []
    trace:
      - event: dispatchStarted
        message: { name: Start }
        signatureId: "Start(*)"
      - event: dispatchCompleted
        message: { name: Start }
        signatureId: "Start(*)"
performance:
  profiles:
    csharp-dotnet-release-managed:
      metrics:
        run.allocated: { reference: 0, maximum: 0, unit: B }
    csharp-dotnet-release-macos-arm64:
      metrics:
        run.allocated: { reference: 0, maximum: 0, unit: B }
```

---

## Test: message-name-completion-observer-on-2000

This case dispatches 2000 reused inputs to 1 message-name handler using completion, with the measured observer enabled, and permits zero bytes after warmup.

### Case description

```yaml
gesBlock: case
id: message-name-completion-observer-on-2000
kind: performance
level: scenario
performance:
  iterations: 2000
  warmupIterations: 100
  compileWarmupIterations: 3
  observeRuntime: true
sources:
  - name: dispatch.ges
    program: main
```

### Source code under test

```ges
module allocationdispatch

on Start as incoming {}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| dispatch | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  dispatch:
    accepted: true
    local: []
    outbound: []
    paused: false
    runtimeLimits:
      exclude: [{ any: true }]
    diagnostics: []
    trace:
      - event: dispatchStarted
        message: { name: Start }
        signatureId: "Start(*)"
      - event: dispatchCompleted
        message: { name: Start }
        signatureId: "Start(*)"
performance:
  profiles:
    csharp-dotnet-release-managed:
      metrics:
        run.allocated: { reference: 0, maximum: 0, unit: B }
    csharp-dotnet-release-macos-arm64:
      metrics:
        run.allocated: { reference: 0, maximum: 0, unit: B }
```

---

## Test: exact-frames-observer-off-1000

This case dispatches 1000 reused inputs to 2 exact handlers using frames, with the measured observer absent, and permits zero bytes after warmup.

### Case description

```yaml
gesBlock: case
id: exact-frames-observer-off-1000
kind: performance
level: scenario
performance:
  iterations: 1000
  warmupIterations: 100
  compileWarmupIterations: 3
  observeRuntime: false
sources:
  - name: dispatch.ges
    program: main
```

### Source code under test

```ges
module allocationdispatch

on Start() {}
on Start() {}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| dispatch | Start | frames | 1 |

### Expectation

```yaml
gesBlock: expect
steps:
  dispatch:
    accepted: true
    local: []
    outbound: []
    paused: true
    runtimeLimits:
      exclude: [{ any: true }]
    diagnostics: []
    trace:
      - event: dispatchStarted
        message: { name: Start }
        signatureId: "Start()"
      - event: dispatchCompleted
        message: { name: Start }
        signatureId: "Start()"
      - event: dispatchStarted
        message: { name: Start }
        signatureId: "Start()"
      - event: dispatchCompleted
        message: { name: Start }
        signatureId: "Start()"
performance:
  profiles:
    csharp-dotnet-release-managed:
      metrics:
        run.allocated: { reference: 0, maximum: 0, unit: B }
    csharp-dotnet-release-macos-arm64:
      metrics:
        run.allocated: { reference: 0, maximum: 0, unit: B }
```

---

## Test: exact-frames-observer-off-2000

This case dispatches 2000 reused inputs to 2 exact handlers using frames, with the measured observer absent, and permits zero bytes after warmup.

### Case description

```yaml
gesBlock: case
id: exact-frames-observer-off-2000
kind: performance
level: scenario
performance:
  iterations: 2000
  warmupIterations: 100
  compileWarmupIterations: 3
  observeRuntime: false
sources:
  - name: dispatch.ges
    program: main
```

### Source code under test

```ges
module allocationdispatch

on Start() {}
on Start() {}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| dispatch | Start | frames | 1 |

### Expectation

```yaml
gesBlock: expect
steps:
  dispatch:
    accepted: true
    local: []
    outbound: []
    paused: true
    runtimeLimits:
      exclude: [{ any: true }]
    diagnostics: []
    trace:
      - event: dispatchStarted
        message: { name: Start }
        signatureId: "Start()"
      - event: dispatchCompleted
        message: { name: Start }
        signatureId: "Start()"
      - event: dispatchStarted
        message: { name: Start }
        signatureId: "Start()"
      - event: dispatchCompleted
        message: { name: Start }
        signatureId: "Start()"
performance:
  profiles:
    csharp-dotnet-release-managed:
      metrics:
        run.allocated: { reference: 0, maximum: 0, unit: B }
    csharp-dotnet-release-macos-arm64:
      metrics:
        run.allocated: { reference: 0, maximum: 0, unit: B }
```

---

## Test: exact-frames-observer-on-1000

This case dispatches 1000 reused inputs to 2 exact handlers using frames, with the measured observer enabled, and permits zero bytes after warmup.

### Case description

```yaml
gesBlock: case
id: exact-frames-observer-on-1000
kind: performance
level: scenario
performance:
  iterations: 1000
  warmupIterations: 100
  compileWarmupIterations: 3
  observeRuntime: true
sources:
  - name: dispatch.ges
    program: main
```

### Source code under test

```ges
module allocationdispatch

on Start() {}
on Start() {}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| dispatch | Start | frames | 1 |

### Expectation

```yaml
gesBlock: expect
steps:
  dispatch:
    accepted: true
    local: []
    outbound: []
    paused: true
    runtimeLimits:
      exclude: [{ any: true }]
    diagnostics: []
    trace:
      - event: dispatchStarted
        message: { name: Start }
        signatureId: "Start()"
      - event: dispatchCompleted
        message: { name: Start }
        signatureId: "Start()"
      - event: dispatchStarted
        message: { name: Start }
        signatureId: "Start()"
      - event: dispatchCompleted
        message: { name: Start }
        signatureId: "Start()"
performance:
  profiles:
    csharp-dotnet-release-managed:
      metrics:
        run.allocated: { reference: 0, maximum: 0, unit: B }
    csharp-dotnet-release-macos-arm64:
      metrics:
        run.allocated: { reference: 0, maximum: 0, unit: B }
```

---

## Test: exact-frames-observer-on-2000

This case dispatches 2000 reused inputs to 2 exact handlers using frames, with the measured observer enabled, and permits zero bytes after warmup.

### Case description

```yaml
gesBlock: case
id: exact-frames-observer-on-2000
kind: performance
level: scenario
performance:
  iterations: 2000
  warmupIterations: 100
  compileWarmupIterations: 3
  observeRuntime: true
sources:
  - name: dispatch.ges
    program: main
```

### Source code under test

```ges
module allocationdispatch

on Start() {}
on Start() {}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| dispatch | Start | frames | 1 |

### Expectation

```yaml
gesBlock: expect
steps:
  dispatch:
    accepted: true
    local: []
    outbound: []
    paused: true
    runtimeLimits:
      exclude: [{ any: true }]
    diagnostics: []
    trace:
      - event: dispatchStarted
        message: { name: Start }
        signatureId: "Start()"
      - event: dispatchCompleted
        message: { name: Start }
        signatureId: "Start()"
      - event: dispatchStarted
        message: { name: Start }
        signatureId: "Start()"
      - event: dispatchCompleted
        message: { name: Start }
        signatureId: "Start()"
performance:
  profiles:
    csharp-dotnet-release-managed:
      metrics:
        run.allocated: { reference: 0, maximum: 0, unit: B }
    csharp-dotnet-release-macos-arm64:
      metrics:
        run.allocated: { reference: 0, maximum: 0, unit: B }
```

---

## Test: message-name-frames-observer-off-1000

This case dispatches 1000 reused inputs to 2 message-name handlers using frames, with the measured observer absent, and permits zero bytes after warmup.

### Case description

```yaml
gesBlock: case
id: message-name-frames-observer-off-1000
kind: performance
level: scenario
performance:
  iterations: 1000
  warmupIterations: 100
  compileWarmupIterations: 3
  observeRuntime: false
sources:
  - name: dispatch.ges
    program: main
```

### Source code under test

```ges
module allocationdispatch

on Start as incoming {}
on Start as incoming {}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| dispatch | Start | frames | 1 |

### Expectation

```yaml
gesBlock: expect
steps:
  dispatch:
    accepted: true
    local: []
    outbound: []
    paused: true
    runtimeLimits:
      exclude: [{ any: true }]
    diagnostics: []
    trace:
      - event: dispatchStarted
        message: { name: Start }
        signatureId: "Start(*)"
      - event: dispatchCompleted
        message: { name: Start }
        signatureId: "Start(*)"
      - event: dispatchStarted
        message: { name: Start }
        signatureId: "Start(*)"
      - event: dispatchCompleted
        message: { name: Start }
        signatureId: "Start(*)"
performance:
  profiles:
    csharp-dotnet-release-managed:
      metrics:
        run.allocated: { reference: 0, maximum: 0, unit: B }
    csharp-dotnet-release-macos-arm64:
      metrics:
        run.allocated: { reference: 0, maximum: 0, unit: B }
```

---

## Test: message-name-frames-observer-off-2000

This case dispatches 2000 reused inputs to 2 message-name handlers using frames, with the measured observer absent, and permits zero bytes after warmup.

### Case description

```yaml
gesBlock: case
id: message-name-frames-observer-off-2000
kind: performance
level: scenario
performance:
  iterations: 2000
  warmupIterations: 100
  compileWarmupIterations: 3
  observeRuntime: false
sources:
  - name: dispatch.ges
    program: main
```

### Source code under test

```ges
module allocationdispatch

on Start as incoming {}
on Start as incoming {}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| dispatch | Start | frames | 1 |

### Expectation

```yaml
gesBlock: expect
steps:
  dispatch:
    accepted: true
    local: []
    outbound: []
    paused: true
    runtimeLimits:
      exclude: [{ any: true }]
    diagnostics: []
    trace:
      - event: dispatchStarted
        message: { name: Start }
        signatureId: "Start(*)"
      - event: dispatchCompleted
        message: { name: Start }
        signatureId: "Start(*)"
      - event: dispatchStarted
        message: { name: Start }
        signatureId: "Start(*)"
      - event: dispatchCompleted
        message: { name: Start }
        signatureId: "Start(*)"
performance:
  profiles:
    csharp-dotnet-release-managed:
      metrics:
        run.allocated: { reference: 0, maximum: 0, unit: B }
    csharp-dotnet-release-macos-arm64:
      metrics:
        run.allocated: { reference: 0, maximum: 0, unit: B }
```

---

## Test: message-name-frames-observer-on-1000

This case dispatches 1000 reused inputs to 2 message-name handlers using frames, with the measured observer enabled, and permits zero bytes after warmup.

### Case description

```yaml
gesBlock: case
id: message-name-frames-observer-on-1000
kind: performance
level: scenario
performance:
  iterations: 1000
  warmupIterations: 100
  compileWarmupIterations: 3
  observeRuntime: true
sources:
  - name: dispatch.ges
    program: main
```

### Source code under test

```ges
module allocationdispatch

on Start as incoming {}
on Start as incoming {}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| dispatch | Start | frames | 1 |

### Expectation

```yaml
gesBlock: expect
steps:
  dispatch:
    accepted: true
    local: []
    outbound: []
    paused: true
    runtimeLimits:
      exclude: [{ any: true }]
    diagnostics: []
    trace:
      - event: dispatchStarted
        message: { name: Start }
        signatureId: "Start(*)"
      - event: dispatchCompleted
        message: { name: Start }
        signatureId: "Start(*)"
      - event: dispatchStarted
        message: { name: Start }
        signatureId: "Start(*)"
      - event: dispatchCompleted
        message: { name: Start }
        signatureId: "Start(*)"
performance:
  profiles:
    csharp-dotnet-release-managed:
      metrics:
        run.allocated: { reference: 0, maximum: 0, unit: B }
    csharp-dotnet-release-macos-arm64:
      metrics:
        run.allocated: { reference: 0, maximum: 0, unit: B }
```

---

## Test: message-name-frames-observer-on-2000

This case dispatches 2000 reused inputs to 2 message-name handlers using frames, with the measured observer enabled, and permits zero bytes after warmup.

### Case description

```yaml
gesBlock: case
id: message-name-frames-observer-on-2000
kind: performance
level: scenario
performance:
  iterations: 2000
  warmupIterations: 100
  compileWarmupIterations: 3
  observeRuntime: true
sources:
  - name: dispatch.ges
    program: main
```

### Source code under test

```ges
module allocationdispatch

on Start as incoming {}
on Start as incoming {}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| dispatch | Start | frames | 1 |

### Expectation

```yaml
gesBlock: expect
steps:
  dispatch:
    accepted: true
    local: []
    outbound: []
    paused: true
    runtimeLimits:
      exclude: [{ any: true }]
    diagnostics: []
    trace:
      - event: dispatchStarted
        message: { name: Start }
        signatureId: "Start(*)"
      - event: dispatchCompleted
        message: { name: Start }
        signatureId: "Start(*)"
      - event: dispatchStarted
        message: { name: Start }
        signatureId: "Start(*)"
      - event: dispatchCompleted
        message: { name: Start }
        signatureId: "Start(*)"
performance:
  profiles:
    csharp-dotnet-release-managed:
      metrics:
        run.allocated: { reference: 0, maximum: 0, unit: B }
    csharp-dotnet-release-macos-arm64:
      metrics:
        run.allocated: { reference: 0, maximum: 0, unit: B }
```

