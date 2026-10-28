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
        id: "1"
        message: { name: Start }
        signatureId: "Start()"
      - event: dispatchCompleted
        id: "1"
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
    swift-6.4-release-macos27-arm64-m3max:
      metrics:
        compile.allocated:
          reference: 21323
          maximum: 22390
          unit: B
        compile.elapsed:
          reference: 0.03025
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 10052
          maximum: 10555
          unit: B
        program-load.elapsed:
          reference: 0.008458
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
          reference: 0.44725
          toleranceAbsolute: 0.1118125
          unit: ms
        run.per-invoke-allocated:
          reference: 0
          maximum: 0
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.00044725
          toleranceAbsolute: 0.0001118125
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
        id: "1"
        message: { name: Start }
        signatureId: "Start()"
      - event: dispatchCompleted
        id: "1"
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
    swift-6.4-release-macos27-arm64-m3max:
      metrics:
        compile.allocated:
          reference: 21323
          maximum: 22390
          unit: B
        compile.elapsed:
          reference: 0.029834
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 10052
          maximum: 10555
          unit: B
        program-load.elapsed:
          reference: 0.008291
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
          reference: 0.910208
          toleranceAbsolute: 0.227552
          unit: ms
        run.per-invoke-allocated:
          reference: 0
          maximum: 0
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.000455104
          toleranceAbsolute: 0.000113776
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
        id: "1"
        message: { name: Start }
        signatureId: "Start()"
      - event: dispatchCompleted
        id: "1"
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
    swift-6.4-release-macos27-arm64-m3max:
      metrics:
        compile.allocated:
          reference: 21323
          maximum: 22390
          unit: B
        compile.elapsed:
          reference: 0.029583
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 10052
          maximum: 10555
          unit: B
        program-load.elapsed:
          reference: 0.008208
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
          reference: 0.464125
          toleranceAbsolute: 0.11603125
          unit: ms
        run.per-invoke-allocated:
          reference: 0
          maximum: 0
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.000464125
          toleranceAbsolute: 0.00011603125
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
        id: "1"
        message: { name: Start }
        signatureId: "Start()"
      - event: dispatchCompleted
        id: "1"
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
    swift-6.4-release-macos27-arm64-m3max:
      metrics:
        compile.allocated:
          reference: 21323
          maximum: 22390
          unit: B
        compile.elapsed:
          reference: 0.029208
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
          reference: 0.922666
          toleranceAbsolute: 0.2306665
          unit: ms
        run.per-invoke-allocated:
          reference: 0
          maximum: 0
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.000461333
          toleranceAbsolute: 0.00011533325
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
        id: "1"
        message: { name: Start }
        signatureId: "Start(*)"
      - event: dispatchCompleted
        id: "1"
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
    swift-6.4-release-macos27-arm64-m3max:
      metrics:
        compile.allocated:
          reference: 27671
          maximum: 29055
          unit: B
        compile.elapsed:
          reference: 0.046125
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 10702
          maximum: 11238
          unit: B
        program-load.elapsed:
          reference: 0.009792
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
          reference: 0.467958
          toleranceAbsolute: 0.1169895
          unit: ms
        run.per-invoke-allocated:
          reference: 0
          maximum: 0
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.000467958
          toleranceAbsolute: 0.0001169895
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
        id: "1"
        message: { name: Start }
        signatureId: "Start(*)"
      - event: dispatchCompleted
        id: "1"
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
    swift-6.4-release-macos27-arm64-m3max:
      metrics:
        compile.allocated:
          reference: 27671
          maximum: 29055
          unit: B
        compile.elapsed:
          reference: 0.045541
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 10702
          maximum: 11238
          unit: B
        program-load.elapsed:
          reference: 0.009709
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
          reference: 0.950208
          toleranceAbsolute: 0.237552
          unit: ms
        run.per-invoke-allocated:
          reference: 0
          maximum: 0
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.000475104
          toleranceAbsolute: 0.000118776
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
        id: "1"
        message: { name: Start }
        signatureId: "Start(*)"
      - event: dispatchCompleted
        id: "1"
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
    swift-6.4-release-macos27-arm64-m3max:
      metrics:
        compile.allocated:
          reference: 27671
          maximum: 29055
          unit: B
        compile.elapsed:
          reference: 0.045333
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
          reference: 0.488375
          toleranceAbsolute: 0.12209375
          unit: ms
        run.per-invoke-allocated:
          reference: 0
          maximum: 0
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.000488375
          toleranceAbsolute: 0.00012209375
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
        id: "1"
        message: { name: Start }
        signatureId: "Start(*)"
      - event: dispatchCompleted
        id: "1"
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
    swift-6.4-release-macos27-arm64-m3max:
      metrics:
        compile.allocated:
          reference: 27671
          maximum: 29055
          unit: B
        compile.elapsed:
          reference: 0.045458
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 10702
          maximum: 11238
          unit: B
        program-load.elapsed:
          reference: 0.009417
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
          reference: 0.979291
          toleranceAbsolute: 0.24482275
          unit: ms
        run.per-invoke-allocated:
          reference: 0
          maximum: 0
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.0004896455
          toleranceAbsolute: 0.000122411375
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
        id: "1"
        message: { name: Start }
        signatureId: "Start()"
      - event: dispatchCompleted
        id: "1"
        message: { name: Start }
        signatureId: "Start()"
      - event: dispatchStarted
        id: "1"
        message: { name: Start }
        signatureId: "Start()"
      - event: dispatchCompleted
        id: "1"
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
    swift-6.4-release-macos27-arm64-m3max:
      metrics:
        compile.allocated:
          reference: 32490
          maximum: 34115
          unit: B
        compile.elapsed:
          reference: 0.041584
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 11044
          maximum: 11597
          unit: B
        program-load.elapsed:
          reference: 0.009416
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
          reference: 0.95475
          toleranceAbsolute: 0.2386875
          unit: ms
        run.per-invoke-allocated:
          reference: 0
          maximum: 0
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.00095475
          toleranceAbsolute: 0.0002386875
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
        id: "1"
        message: { name: Start }
        signatureId: "Start()"
      - event: dispatchCompleted
        id: "1"
        message: { name: Start }
        signatureId: "Start()"
      - event: dispatchStarted
        id: "1"
        message: { name: Start }
        signatureId: "Start()"
      - event: dispatchCompleted
        id: "1"
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
    swift-6.4-release-macos27-arm64-m3max:
      metrics:
        compile.allocated:
          reference: 32490
          maximum: 34115
          unit: B
        compile.elapsed:
          reference: 0.04025
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 11044
          maximum: 11597
          unit: B
        program-load.elapsed:
          reference: 0.009375
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
          reference: 1.893542
          toleranceAbsolute: 0.4733855
          unit: ms
        run.per-invoke-allocated:
          reference: 0
          maximum: 0
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.000946771
          toleranceAbsolute: 0.00023669275
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
        id: "1"
        message: { name: Start }
        signatureId: "Start()"
      - event: dispatchCompleted
        id: "1"
        message: { name: Start }
        signatureId: "Start()"
      - event: dispatchStarted
        id: "1"
        message: { name: Start }
        signatureId: "Start()"
      - event: dispatchCompleted
        id: "1"
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
    swift-6.4-release-macos27-arm64-m3max:
      metrics:
        compile.allocated:
          reference: 32490
          maximum: 34115
          unit: B
        compile.elapsed:
          reference: 0.040292
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 11044
          maximum: 11597
          unit: B
        program-load.elapsed:
          reference: 0.009334
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
          reference: 0.974667
          toleranceAbsolute: 0.24366675
          unit: ms
        run.per-invoke-allocated:
          reference: 0
          maximum: 0
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.000974667
          toleranceAbsolute: 0.00024366675
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
        id: "1"
        message: { name: Start }
        signatureId: "Start()"
      - event: dispatchCompleted
        id: "1"
        message: { name: Start }
        signatureId: "Start()"
      - event: dispatchStarted
        id: "1"
        message: { name: Start }
        signatureId: "Start()"
      - event: dispatchCompleted
        id: "1"
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
    swift-6.4-release-macos27-arm64-m3max:
      metrics:
        compile.allocated:
          reference: 32490
          maximum: 34115
          unit: B
        compile.elapsed:
          reference: 0.040542
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 11044
          maximum: 11597
          unit: B
        program-load.elapsed:
          reference: 0.00925
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
          reference: 1.955792
          toleranceAbsolute: 0.488948
          unit: ms
        run.per-invoke-allocated:
          reference: 0
          maximum: 0
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.000977896
          toleranceAbsolute: 0.000244474
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
        id: "1"
        message: { name: Start }
        signatureId: "Start(*)"
      - event: dispatchCompleted
        id: "1"
        message: { name: Start }
        signatureId: "Start(*)"
      - event: dispatchStarted
        id: "1"
        message: { name: Start }
        signatureId: "Start(*)"
      - event: dispatchCompleted
        id: "1"
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
    swift-6.4-release-macos27-arm64-m3max:
      metrics:
        compile.allocated:
          reference: 44754
          maximum: 46992
          unit: B
        compile.elapsed:
          reference: 0.073709
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 12272
          maximum: 12886
          unit: B
        program-load.elapsed:
          reference: 0.01225
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
          reference: 1.173042
          toleranceAbsolute: 0.2932605
          unit: ms
        run.per-invoke-allocated:
          reference: 0
          maximum: 0
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.001173042
          toleranceAbsolute: 0.0002932605
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
        id: "1"
        message: { name: Start }
        signatureId: "Start(*)"
      - event: dispatchCompleted
        id: "1"
        message: { name: Start }
        signatureId: "Start(*)"
      - event: dispatchStarted
        id: "1"
        message: { name: Start }
        signatureId: "Start(*)"
      - event: dispatchCompleted
        id: "1"
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
    swift-6.4-release-macos27-arm64-m3max:
      metrics:
        compile.allocated:
          reference: 44754
          maximum: 46992
          unit: B
        compile.elapsed:
          reference: 0.073084
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 12272
          maximum: 12886
          unit: B
        program-load.elapsed:
          reference: 0.012333
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
          reference: 2.341208
          toleranceAbsolute: 0.585302
          unit: ms
        run.per-invoke-allocated:
          reference: 0
          maximum: 0
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.001170604
          toleranceAbsolute: 0.000292651
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
        id: "1"
        message: { name: Start }
        signatureId: "Start(*)"
      - event: dispatchCompleted
        id: "1"
        message: { name: Start }
        signatureId: "Start(*)"
      - event: dispatchStarted
        id: "1"
        message: { name: Start }
        signatureId: "Start(*)"
      - event: dispatchCompleted
        id: "1"
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
    swift-6.4-release-macos27-arm64-m3max:
      metrics:
        compile.allocated:
          reference: 44754
          maximum: 46992
          unit: B
        compile.elapsed:
          reference: 0.072959
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 12272
          maximum: 12886
          unit: B
        program-load.elapsed:
          reference: 0.01225
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
          reference: 1.199167
          toleranceAbsolute: 0.29979175
          unit: ms
        run.per-invoke-allocated:
          reference: 0
          maximum: 0
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.001199167
          toleranceAbsolute: 0.00029979175
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
        id: "1"
        message: { name: Start }
        signatureId: "Start(*)"
      - event: dispatchCompleted
        id: "1"
        message: { name: Start }
        signatureId: "Start(*)"
      - event: dispatchStarted
        id: "1"
        message: { name: Start }
        signatureId: "Start(*)"
      - event: dispatchCompleted
        id: "1"
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
    swift-6.4-release-macos27-arm64-m3max:
      metrics:
        compile.allocated:
          reference: 44754
          maximum: 46992
          unit: B
        compile.elapsed:
          reference: 0.072542
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 12272
          maximum: 12886
          unit: B
        program-load.elapsed:
          reference: 0.012083
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
          reference: 2.402
          toleranceAbsolute: 0.6005
          unit: ms
        run.per-invoke-allocated:
          reference: 0
          maximum: 0
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.001201
          toleranceAbsolute: 0.00030025
          unit: ms/iteration
```
