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

The measured dispatch includes obtaining the signature used by VM entry and observer callbacks. Repeated message-name dispatch must preserve the expected `Start(*)` trace labels without allocating a new label for each invocation or callback.

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
    swift-6.4-release-macos26-arm64-m3max:
      metrics:
        compile.allocated:
          reference: 27020
          maximum: 28371
          unit: B
        compile.elapsed:
          reference: 0.039791
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 10718
          maximum: 11254
          unit: B
        program-load.elapsed:
          reference: 0.007584
          toleranceAbsolute: 0.05
          unit: ms
        run.allocated:
          reference: 0
          maximum: 0
          unit: B
        run.allocations:
          reference: 0
          maximum: 0
          unit: count
        run.elapsed:
          reference: 0.709792
          toleranceAbsolute: 0.177448
          unit: ms
        run.per-invoke-allocated:
          reference: 0
          maximum: 0
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.000709792
          toleranceAbsolute: 0.000177448
          unit: ms/iteration
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
    swift-6.4-release-macos26-arm64-m3max:
      metrics:
        compile.allocated:
          reference: 27020
          maximum: 28371
          unit: B
        compile.elapsed:
          reference: 0.044959
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 10718
          maximum: 11254
          unit: B
        program-load.elapsed:
          reference: 0.007625
          toleranceAbsolute: 0.05
          unit: ms
        run.allocated:
          reference: 0
          maximum: 0
          unit: B
        run.allocations:
          reference: 0
          maximum: 0
          unit: count
        run.elapsed:
          reference: 1.487875
          toleranceAbsolute: 0.37196875
          unit: ms
        run.per-invoke-allocated:
          reference: 0
          maximum: 0
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.0007439375
          toleranceAbsolute: 0.000185984375
          unit: ms/iteration
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
    swift-6.4-release-macos26-arm64-m3max:
      metrics:
        compile.allocated:
          reference: 27020
          maximum: 28371
          unit: B
        compile.elapsed:
          reference: 0.044583
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 10718
          maximum: 11254
          unit: B
        program-load.elapsed:
          reference: 0.008666
          toleranceAbsolute: 0.05
          unit: ms
        run.allocated:
          reference: 0
          maximum: 0
          unit: B
        run.allocations:
          reference: 0
          maximum: 0
          unit: count
        run.elapsed:
          reference: 0.778917
          toleranceAbsolute: 0.19472925
          unit: ms
        run.per-invoke-allocated:
          reference: 0
          maximum: 0
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.000778917
          toleranceAbsolute: 0.00019472925
          unit: ms/iteration
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
    swift-6.4-release-macos26-arm64-m3max:
      metrics:
        compile.allocated:
          reference: 27020
          maximum: 28371
          unit: B
        compile.elapsed:
          reference: 0.044292
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 10718
          maximum: 11254
          unit: B
        program-load.elapsed:
          reference: 0.008334
          toleranceAbsolute: 0.05
          unit: ms
        run.allocated:
          reference: 0
          maximum: 0
          unit: B
        run.allocations:
          reference: 0
          maximum: 0
          unit: count
        run.elapsed:
          reference: 1.481417
          toleranceAbsolute: 0.37035425
          unit: ms
        run.per-invoke-allocated:
          reference: 0
          maximum: 0
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.0007407085
          toleranceAbsolute: 0.000185177125
          unit: ms/iteration
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
    swift-6.4-release-macos26-arm64-m3max:
      metrics:
        compile.allocated:
          reference: 33336
          maximum: 35003
          unit: B
        compile.elapsed:
          reference: 0.055208
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 11296
          maximum: 11861
          unit: B
        program-load.elapsed:
          reference: 0.008917
          toleranceAbsolute: 0.05
          unit: ms
        run.allocated:
          reference: 0
          maximum: 0
          unit: B
        run.allocations:
          reference: 0
          maximum: 0
          unit: count
        run.elapsed:
          reference: 0.867042
          toleranceAbsolute: 0.2167605
          unit: ms
        run.per-invoke-allocated:
          reference: 0
          maximum: 0
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.000867042
          toleranceAbsolute: 0.0002167605
          unit: ms/iteration
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
    swift-6.4-release-macos26-arm64-m3max:
      metrics:
        compile.allocated:
          reference: 33336
          maximum: 35003
          unit: B
        compile.elapsed:
          reference: 0.065833
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 11296
          maximum: 11861
          unit: B
        program-load.elapsed:
          reference: 0.008875
          toleranceAbsolute: 0.05
          unit: ms
        run.allocated:
          reference: 0
          maximum: 0
          unit: B
        run.allocations:
          reference: 0
          maximum: 0
          unit: count
        run.elapsed:
          reference: 1.717417
          toleranceAbsolute: 0.42935425
          unit: ms
        run.per-invoke-allocated:
          reference: 0
          maximum: 0
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.0008587085
          toleranceAbsolute: 0.000214677125
          unit: ms/iteration
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
    swift-6.4-release-macos26-arm64-m3max:
      metrics:
        compile.allocated:
          reference: 33336
          maximum: 35003
          unit: B
        compile.elapsed:
          reference: 0.058208
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 11296
          maximum: 11861
          unit: B
        program-load.elapsed:
          reference: 0.01025
          toleranceAbsolute: 0.05
          unit: ms
        run.allocated:
          reference: 0
          maximum: 0
          unit: B
        run.allocations:
          reference: 0
          maximum: 0
          unit: count
        run.elapsed:
          reference: 0.910042
          toleranceAbsolute: 0.2275105
          unit: ms
        run.per-invoke-allocated:
          reference: 0
          maximum: 0
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.000910042
          toleranceAbsolute: 0.0002275105
          unit: ms/iteration
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
    swift-6.4-release-macos26-arm64-m3max:
      metrics:
        compile.allocated:
          reference: 33336
          maximum: 35003
          unit: B
        compile.elapsed:
          reference: 0.059083
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 11296
          maximum: 11861
          unit: B
        program-load.elapsed:
          reference: 0.010167
          toleranceAbsolute: 0.05
          unit: ms
        run.allocated:
          reference: 0
          maximum: 0
          unit: B
        run.allocations:
          reference: 0
          maximum: 0
          unit: count
        run.elapsed:
          reference: 1.837292
          toleranceAbsolute: 0.459323
          unit: ms
        run.per-invoke-allocated:
          reference: 0
          maximum: 0
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.000918646
          toleranceAbsolute: 0.0002296615
          unit: ms/iteration
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
    swift-6.4-release-macos26-arm64-m3max:
      metrics:
        compile.allocated:
          reference: 37979
          maximum: 39878
          unit: B
        compile.elapsed:
          reference: 0.052375
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 11750
          maximum: 12338
          unit: B
        program-load.elapsed:
          reference: 0.008375
          toleranceAbsolute: 0.05
          unit: ms
        run.allocated:
          reference: 0
          maximum: 0
          unit: B
        run.allocations:
          reference: 0
          maximum: 0
          unit: count
        run.elapsed:
          reference: 1.409167
          toleranceAbsolute: 0.35229175
          unit: ms
        run.per-invoke-allocated:
          reference: 0
          maximum: 0
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.001409167
          toleranceAbsolute: 0.00035229175
          unit: ms/iteration
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
    swift-6.4-release-macos26-arm64-m3max:
      metrics:
        compile.allocated:
          reference: 37979
          maximum: 39878
          unit: B
        compile.elapsed:
          reference: 0.058875
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 11750
          maximum: 12338
          unit: B
        program-load.elapsed:
          reference: 0.010458
          toleranceAbsolute: 0.05
          unit: ms
        run.allocated:
          reference: 0
          maximum: 0
          unit: B
        run.allocations:
          reference: 0
          maximum: 0
          unit: count
        run.elapsed:
          reference: 2.852458
          toleranceAbsolute: 0.7131145
          unit: ms
        run.per-invoke-allocated:
          reference: 0
          maximum: 0
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.001426229
          toleranceAbsolute: 0.00035655725
          unit: ms/iteration
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
    swift-6.4-release-macos26-arm64-m3max:
      metrics:
        compile.allocated:
          reference: 37979
          maximum: 39878
          unit: B
        compile.elapsed:
          reference: 0.054417
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 11750
          maximum: 12338
          unit: B
        program-load.elapsed:
          reference: 0.010333
          toleranceAbsolute: 0.05
          unit: ms
        run.allocated:
          reference: 0
          maximum: 0
          unit: B
        run.allocations:
          reference: 0
          maximum: 0
          unit: count
        run.elapsed:
          reference: 1.511458
          toleranceAbsolute: 0.3778645
          unit: ms
        run.per-invoke-allocated:
          reference: 0
          maximum: 0
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.001511458
          toleranceAbsolute: 0.0003778645
          unit: ms/iteration
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
    swift-6.4-release-macos26-arm64-m3max:
      metrics:
        compile.allocated:
          reference: 37979
          maximum: 39878
          unit: B
        compile.elapsed:
          reference: 0.05625
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 11750
          maximum: 12338
          unit: B
        program-load.elapsed:
          reference: 0.0105
          toleranceAbsolute: 0.05
          unit: ms
        run.allocated:
          reference: 0
          maximum: 0
          unit: B
        run.allocations:
          reference: 0
          maximum: 0
          unit: count
        run.elapsed:
          reference: 2.919833
          toleranceAbsolute: 0.72995825
          unit: ms
        run.per-invoke-allocated:
          reference: 0
          maximum: 0
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.0014599165
          toleranceAbsolute: 0.000364979125
          unit: ms/iteration
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
    swift-6.4-release-macos26-arm64-m3max:
      metrics:
        compile.allocated:
          reference: 50259
          maximum: 52772
          unit: B
        compile.elapsed:
          reference: 0.086625
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 12906
          maximum: 13552
          unit: B
        program-load.elapsed:
          reference: 0.012334
          toleranceAbsolute: 0.05
          unit: ms
        run.allocated:
          reference: 0
          maximum: 0
          unit: B
        run.allocations:
          reference: 0
          maximum: 0
          unit: count
        run.elapsed:
          reference: 1.873042
          toleranceAbsolute: 0.4682605
          unit: ms
        run.per-invoke-allocated:
          reference: 0
          maximum: 0
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.001873042
          toleranceAbsolute: 0.0004682605
          unit: ms/iteration
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
    swift-6.4-release-macos26-arm64-m3max:
      metrics:
        compile.allocated:
          reference: 50259
          maximum: 52772
          unit: B
        compile.elapsed:
          reference: 0.09375
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 12906
          maximum: 13552
          unit: B
        program-load.elapsed:
          reference: 0.013292
          toleranceAbsolute: 0.05
          unit: ms
        run.allocated:
          reference: 0
          maximum: 0
          unit: B
        run.allocations:
          reference: 0
          maximum: 0
          unit: count
        run.elapsed:
          reference: 3.869875
          toleranceAbsolute: 0.96746875
          unit: ms
        run.per-invoke-allocated:
          reference: 0
          maximum: 0
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.0019349375
          toleranceAbsolute: 0.000483734375
          unit: ms/iteration
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
    swift-6.4-release-macos26-arm64-m3max:
      metrics:
        compile.allocated:
          reference: 50259
          maximum: 52772
          unit: B
        compile.elapsed:
          reference: 0.093125
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 12906
          maximum: 13552
          unit: B
        program-load.elapsed:
          reference: 0.013042
          toleranceAbsolute: 0.05
          unit: ms
        run.allocated:
          reference: 0
          maximum: 0
          unit: B
        run.allocations:
          reference: 0
          maximum: 0
          unit: count
        run.elapsed:
          reference: 1.893708
          toleranceAbsolute: 0.473427
          unit: ms
        run.per-invoke-allocated:
          reference: 0
          maximum: 0
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.001893708
          toleranceAbsolute: 0.000473427
          unit: ms/iteration
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
    swift-6.4-release-macos26-arm64-m3max:
      metrics:
        compile.allocated:
          reference: 50259
          maximum: 52772
          unit: B
        compile.elapsed:
          reference: 0.089541
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 12906
          maximum: 13552
          unit: B
        program-load.elapsed:
          reference: 0.013625
          toleranceAbsolute: 0.05
          unit: ms
        run.allocated:
          reference: 0
          maximum: 0
          unit: B
        run.allocations:
          reference: 0
          maximum: 0
          unit: count
        run.elapsed:
          reference: 3.928708
          toleranceAbsolute: 0.982177
          unit: ms
        run.per-invoke-allocated:
          reference: 0
          maximum: 0
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.001964354
          toleranceAbsolute: 0.0004910885
          unit: ms/iteration
```
