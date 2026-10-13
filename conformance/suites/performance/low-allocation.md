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
          reference: 21315
          maximum: 22381
          unit: B
        compile.elapsed:
          reference: 0.030375
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 10052
          maximum: 10555
          unit: B
        program-load.elapsed:
          reference: 0.008292
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
          reference: 0.450791
          toleranceAbsolute: 0.11269775
          unit: ms
        run.per-invoke-allocated:
          reference: 0
          maximum: 0
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.000450791
          toleranceAbsolute: 0.00011269775
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
          reference: 21315
          maximum: 22381
          unit: B
        compile.elapsed:
          reference: 0.0295
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 10052
          maximum: 10555
          unit: B
        program-load.elapsed:
          reference: 0.008083
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
          reference: 0.90025
          toleranceAbsolute: 0.2250625
          unit: ms
        run.per-invoke-allocated:
          reference: 0
          maximum: 0
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.000450125
          toleranceAbsolute: 0.00011253125
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
          reference: 21315
          maximum: 22381
          unit: B
        compile.elapsed:
          reference: 0.029334
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 10052
          maximum: 10555
          unit: B
        program-load.elapsed:
          reference: 0.008084
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
          reference: 0.465583
          toleranceAbsolute: 0.11639575
          unit: ms
        run.per-invoke-allocated:
          reference: 0
          maximum: 0
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.000465583
          toleranceAbsolute: 0.00011639575
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
          reference: 21315
          maximum: 22381
          unit: B
        compile.elapsed:
          reference: 0.029125
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 10052
          maximum: 10555
          unit: B
        program-load.elapsed:
          reference: 0.008083
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
          reference: 0.9345
          toleranceAbsolute: 0.233625
          unit: ms
        run.per-invoke-allocated:
          reference: 0
          maximum: 0
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.00046725
          toleranceAbsolute: 0.0001168125
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
          reference: 27663
          maximum: 29047
          unit: B
        compile.elapsed:
          reference: 0.046167
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 10702
          maximum: 11238
          unit: B
        program-load.elapsed:
          reference: 0.00975
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
          reference: 0.466541
          toleranceAbsolute: 0.11663525
          unit: ms
        run.per-invoke-allocated:
          reference: 0
          maximum: 0
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.000466541
          toleranceAbsolute: 0.00011663525
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
          reference: 27663
          maximum: 29047
          unit: B
        compile.elapsed:
          reference: 0.045292
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 10702
          maximum: 11238
          unit: B
        program-load.elapsed:
          reference: 0.0095
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
          reference: 0.947292
          toleranceAbsolute: 0.236823
          unit: ms
        run.per-invoke-allocated:
          reference: 0
          maximum: 0
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.000473646
          toleranceAbsolute: 0.0001184115
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
          reference: 27663
          maximum: 29047
          unit: B
        compile.elapsed:
          reference: 0.045542
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 10702
          maximum: 11238
          unit: B
        program-load.elapsed:
          reference: 0.009458
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
          reference: 0.488459
          toleranceAbsolute: 0.12211475
          unit: ms
        run.per-invoke-allocated:
          reference: 0
          maximum: 0
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.000488459
          toleranceAbsolute: 0.00012211475
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
          reference: 27663
          maximum: 29047
          unit: B
        compile.elapsed:
          reference: 0.045167
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 10702
          maximum: 11238
          unit: B
        program-load.elapsed:
          reference: 0.009625
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
          reference: 0.977375
          toleranceAbsolute: 0.24434375
          unit: ms
        run.per-invoke-allocated:
          reference: 0
          maximum: 0
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.0004886875
          toleranceAbsolute: 0.000122171875
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
          reference: 32482
          maximum: 34107
          unit: B
        compile.elapsed:
          reference: 0.041167
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 11044
          maximum: 11597
          unit: B
        program-load.elapsed:
          reference: 0.009583
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
          reference: 0.954709
          toleranceAbsolute: 0.23867725
          unit: ms
        run.per-invoke-allocated:
          reference: 0
          maximum: 0
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.000954709
          toleranceAbsolute: 0.00023867725
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
          reference: 32482
          maximum: 34107
          unit: B
        compile.elapsed:
          reference: 0.041292
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 11044
          maximum: 11597
          unit: B
        program-load.elapsed:
          reference: 0.009458
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
          reference: 1.950541
          toleranceAbsolute: 0.48763525
          unit: ms
        run.per-invoke-allocated:
          reference: 0
          maximum: 0
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.0009752705
          toleranceAbsolute: 0.000243817625
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
          reference: 32482
          maximum: 34107
          unit: B
        compile.elapsed:
          reference: 0.042209
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 11044
          maximum: 11597
          unit: B
        program-load.elapsed:
          reference: 0.009708
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
          reference: 0.991292
          toleranceAbsolute: 0.247823
          unit: ms
        run.per-invoke-allocated:
          reference: 0
          maximum: 0
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.000991292
          toleranceAbsolute: 0.000247823
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
          reference: 32482
          maximum: 34107
          unit: B
        compile.elapsed:
          reference: 0.041084
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 11044
          maximum: 11597
          unit: B
        program-load.elapsed:
          reference: 0.009667
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
          reference: 2
          toleranceAbsolute: 0.5
          unit: ms
        run.per-invoke-allocated:
          reference: 0
          maximum: 0
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.001
          toleranceAbsolute: 0.00025
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
          reference: 44746
          maximum: 46984
          unit: B
        compile.elapsed:
          reference: 0.075542
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 12272
          maximum: 12886
          unit: B
        program-load.elapsed:
          reference: 0.012833
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
          reference: 1.182459
          toleranceAbsolute: 0.29561475
          unit: ms
        run.per-invoke-allocated:
          reference: 0
          maximum: 0
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.001182459
          toleranceAbsolute: 0.00029561475
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
          reference: 44746
          maximum: 46984
          unit: B
        compile.elapsed:
          reference: 0.074708
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 12272
          maximum: 12886
          unit: B
        program-load.elapsed:
          reference: 0.012458
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
          reference: 2.353416
          toleranceAbsolute: 0.588354
          unit: ms
        run.per-invoke-allocated:
          reference: 0
          maximum: 0
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.001176708
          toleranceAbsolute: 0.000294177
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
          reference: 44746
          maximum: 46984
          unit: B
        compile.elapsed:
          reference: 0.073125
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 12272
          maximum: 12886
          unit: B
        program-load.elapsed:
          reference: 0.012292
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
          reference: 1.177917
          toleranceAbsolute: 0.29447925
          unit: ms
        run.per-invoke-allocated:
          reference: 0
          maximum: 0
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.001177917
          toleranceAbsolute: 0.00029447925
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
          reference: 44746
          maximum: 46984
          unit: B
        compile.elapsed:
          reference: 0.072208
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 12272
          maximum: 12886
          unit: B
        program-load.elapsed:
          reference: 0.012208
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
          reference: 2.381458
          toleranceAbsolute: 0.5953645
          unit: ms
        run.per-invoke-allocated:
          reference: 0
          maximum: 0
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.001190729
          toleranceAbsolute: 0.00029768225
          unit: ms/iteration
```
