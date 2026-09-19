---
formatVersion: 1
suiteId: performance.text-conversion
title: Text conversion allocation profiles
kind: performance
level: scenario
categories: [conformance, allocation]
---

# Text conversion allocation profiles

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

These paired N/2N workloads measure dynamic parsing and formatting separately from ordinary dispatch. Reused numeric inputs, normalization and unrecognized plain Text have a zero-allocation C# managed-thread profile after warmup. Percentage formatting and parsing measure 176 bytes per iteration on this profile and permit at most 192 bytes, including result and temporary strings. Other runtimes select their own instrumentation and budgets. The macOS correctness adapter echoes references without making an allocation measurement.

---

## Test: parse-integer-1000

This case executes 1000 dynamic conversions with reused input. A failed conversion emits Wrong and therefore fails correctness before allocation is evaluated.

### Case description

```yaml
gesBlock: case
id: "parse-integer-1000"
performance:
  iterations: 1000
  warmupIterations: 100
  compileWarmupIterations: 3
  observeRuntime: false
sources:
  - name: "conversion.ges"
    program: "main"
```

### Source code under test

```ges
module allocationconversion
on Start(value) {
  let parsed be parse value
  if not (parsed is :Number and parsed = 12345) { emit Wrong(value: parsed) }
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| dispatch | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  dispatch:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "12345"
    accepted: true
    local: []
    outbound: []
    paused: false
    runtimeLimits:
      exclude:
        - any: true
    diagnostics: []
    trace:
      - event: "dispatchStarted"
        message:
          name: "Start"
          args:
            - name: "value"
              value:
                type: ":Text"
                value: "12345"
        signatureId: "Start(value)"
      - event: "dispatchCompleted"
        message:
          name: "Start"
          args:
            - name: "value"
              value:
                type: ":Text"
                value: "12345"
        signatureId: "Start(value)"
performance:
  profiles:
    csharp-dotnet-release-managed:
      metrics:
        run.allocated:
          reference: 0
          maximum: 0
          unit: "B"
    csharp-dotnet-release-macos-arm64:
      metrics:
        run.allocated:
          reference: 0
          maximum: 0
          unit: "B"
    swift-6.4-release-macos26-arm64-m3max:
      metrics:
        compile.allocated:
          reference: 96710
          maximum: 101546
          unit: B
        compile.elapsed:
          reference: 0.122209
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 12690
          maximum: 13325
          unit: B
        program-load.elapsed:
          reference: 0.013417
          toleranceAbsolute: 0.05
          unit: ms
        run.allocated:
          reference: 107000
          maximum: 112350
          unit: B
        run.allocations:
          reference: 3000
          maximum: 3150
          unit: count
        run.elapsed:
          reference: 1.402125
          toleranceAbsolute: 0.35053125
          unit: ms
        run.per-invoke-allocated:
          reference: 107
          maximum: 113
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.001402125
          toleranceAbsolute: 0.00035053125
          unit: ms/iteration
```

---

## Test: parse-integer-2000

This case executes 2000 dynamic conversions with reused input. A failed conversion emits Wrong and therefore fails correctness before allocation is evaluated.

### Case description

```yaml
gesBlock: case
id: "parse-integer-2000"
performance:
  iterations: 2000
  warmupIterations: 100
  compileWarmupIterations: 3
  observeRuntime: false
sources:
  - name: "conversion.ges"
    program: "main"
```

### Source code under test

```ges
module allocationconversion
on Start(value) {
  let parsed be parse value
  if not (parsed is :Number and parsed = 12345) { emit Wrong(value: parsed) }
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| dispatch | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  dispatch:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "12345"
    accepted: true
    local: []
    outbound: []
    paused: false
    runtimeLimits:
      exclude:
        - any: true
    diagnostics: []
    trace:
      - event: "dispatchStarted"
        message:
          name: "Start"
          args:
            - name: "value"
              value:
                type: ":Text"
                value: "12345"
        signatureId: "Start(value)"
      - event: "dispatchCompleted"
        message:
          name: "Start"
          args:
            - name: "value"
              value:
                type: ":Text"
                value: "12345"
        signatureId: "Start(value)"
performance:
  profiles:
    csharp-dotnet-release-managed:
      metrics:
        run.allocated:
          reference: 0
          maximum: 0
          unit: "B"
    csharp-dotnet-release-macos-arm64:
      metrics:
        run.allocated:
          reference: 0
          maximum: 0
          unit: "B"
    swift-6.4-release-macos26-arm64-m3max:
      metrics:
        compile.allocated:
          reference: 96710
          maximum: 101546
          unit: B
        compile.elapsed:
          reference: 0.12875
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 12690
          maximum: 13325
          unit: B
        program-load.elapsed:
          reference: 0.014458
          toleranceAbsolute: 0.05
          unit: ms
        run.allocated:
          reference: 214000
          maximum: 224700
          unit: B
        run.allocations:
          reference: 6000
          maximum: 6300
          unit: count
        run.elapsed:
          reference: 2.772834
          toleranceAbsolute: 0.6932085
          unit: ms
        run.per-invoke-allocated:
          reference: 107
          maximum: 113
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.001386417
          toleranceAbsolute: 0.00034660425
          unit: ms/iteration
```

---

## Test: parse-plain-text-1000

This case executes 1000 dynamic conversions with reused input. A failed conversion emits Wrong and therefore fails correctness before allocation is evaluated.

### Case description

```yaml
gesBlock: case
id: "parse-plain-text-1000"
performance:
  iterations: 1000
  warmupIterations: 100
  compileWarmupIterations: 3
  observeRuntime: false
sources:
  - name: "conversion.ges"
    program: "main"
```

### Source code under test

```ges
module allocationconversion
on Start(value) {
  let parsed be parse value
  if not (parsed is :Text and parsed = value) { emit Wrong(value: parsed) }
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| dispatch | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  dispatch:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "Hello, world"
    accepted: true
    local: []
    outbound: []
    paused: false
    runtimeLimits:
      exclude:
        - any: true
    diagnostics: []
    trace:
      - event: "dispatchStarted"
        message:
          name: "Start"
          args:
            - name: "value"
              value:
                type: ":Text"
                value: "Hello, world"
        signatureId: "Start(value)"
      - event: "dispatchCompleted"
        message:
          name: "Start"
          args:
            - name: "value"
              value:
                type: ":Text"
                value: "Hello, world"
        signatureId: "Start(value)"
performance:
  profiles:
    csharp-dotnet-release-managed:
      metrics:
        run.allocated:
          reference: 0
          maximum: 0
          unit: "B"
    csharp-dotnet-release-macos-arm64:
      metrics:
        run.allocated:
          reference: 0
          maximum: 0
          unit: "B"
    swift-6.4-release-macos26-arm64-m3max:
      metrics:
        compile.allocated:
          reference: 97041
          maximum: 101894
          unit: B
        compile.elapsed:
          reference: 0.123084
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 12658
          maximum: 13291
          unit: B
        program-load.elapsed:
          reference: 0.012292
          toleranceAbsolute: 0.05
          unit: ms
        run.allocated:
          reference: 81000
          maximum: 85050
          unit: B
        run.allocations:
          reference: 2000
          maximum: 2100
          unit: count
        run.elapsed:
          reference: 1.296042
          toleranceAbsolute: 0.3240105
          unit: ms
        run.per-invoke-allocated:
          reference: 81
          maximum: 86
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.001296042
          toleranceAbsolute: 0.0003240105
          unit: ms/iteration
```

---

## Test: parse-plain-text-2000

This case executes 2000 dynamic conversions with reused input. A failed conversion emits Wrong and therefore fails correctness before allocation is evaluated.

### Case description

```yaml
gesBlock: case
id: "parse-plain-text-2000"
performance:
  iterations: 2000
  warmupIterations: 100
  compileWarmupIterations: 3
  observeRuntime: false
sources:
  - name: "conversion.ges"
    program: "main"
```

### Source code under test

```ges
module allocationconversion
on Start(value) {
  let parsed be parse value
  if not (parsed is :Text and parsed = value) { emit Wrong(value: parsed) }
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| dispatch | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  dispatch:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "Hello, world"
    accepted: true
    local: []
    outbound: []
    paused: false
    runtimeLimits:
      exclude:
        - any: true
    diagnostics: []
    trace:
      - event: "dispatchStarted"
        message:
          name: "Start"
          args:
            - name: "value"
              value:
                type: ":Text"
                value: "Hello, world"
        signatureId: "Start(value)"
      - event: "dispatchCompleted"
        message:
          name: "Start"
          args:
            - name: "value"
              value:
                type: ":Text"
                value: "Hello, world"
        signatureId: "Start(value)"
performance:
  profiles:
    csharp-dotnet-release-managed:
      metrics:
        run.allocated:
          reference: 0
          maximum: 0
          unit: "B"
    csharp-dotnet-release-macos-arm64:
      metrics:
        run.allocated:
          reference: 0
          maximum: 0
          unit: "B"
    swift-6.4-release-macos26-arm64-m3max:
      metrics:
        compile.allocated:
          reference: 97041
          maximum: 101894
          unit: B
        compile.elapsed:
          reference: 0.123791
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 12658
          maximum: 13291
          unit: B
        program-load.elapsed:
          reference: 0.01325
          toleranceAbsolute: 0.05
          unit: ms
        run.allocated:
          reference: 162000
          maximum: 170100
          unit: B
        run.allocations:
          reference: 4000
          maximum: 4200
          unit: count
        run.elapsed:
          reference: 2.721709
          toleranceAbsolute: 0.68042725
          unit: ms
        run.per-invoke-allocated:
          reference: 81
          maximum: 86
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.0013608545
          toleranceAbsolute: 0.000340213625
          unit: ms/iteration
```

---

## Test: cast-separated-number-1000

This case executes 1000 dynamic conversions with reused input. A failed conversion emits Wrong and therefore fails correctness before allocation is evaluated.

### Case description

```yaml
gesBlock: case
id: "cast-separated-number-1000"
performance:
  iterations: 1000
  warmupIterations: 100
  compileWarmupIterations: 3
  observeRuntime: false
sources:
  - name: "conversion.ges"
    program: "main"
```

### Source code under test

```ges
module allocationconversion
on Start(value) {
  let parsed be value as :Number
  if not (parsed is :Number and parsed = 1234.5) { emit Wrong(value: parsed) }
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| dispatch | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  dispatch:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "1_234.5"
    accepted: true
    local: []
    outbound: []
    paused: false
    runtimeLimits:
      exclude:
        - any: true
    diagnostics: []
    trace:
      - event: "dispatchStarted"
        message:
          name: "Start"
          args:
            - name: "value"
              value:
                type: ":Text"
                value: "1_234.5"
        signatureId: "Start(value)"
      - event: "dispatchCompleted"
        message:
          name: "Start"
          args:
            - name: "value"
              value:
                type: ":Text"
                value: "1_234.5"
        signatureId: "Start(value)"
performance:
  profiles:
    csharp-dotnet-release-managed:
      metrics:
        run.allocated:
          reference: 0
          maximum: 0
          unit: "B"
    csharp-dotnet-release-macos-arm64:
      metrics:
        run.allocated:
          reference: 0
          maximum: 0
          unit: "B"
    swift-6.4-release-macos26-arm64-m3max:
      metrics:
        compile.allocated:
          reference: 96475
          maximum: 101299
          unit: B
        compile.elapsed:
          reference: 0.126083
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 12690
          maximum: 13325
          unit: B
        program-load.elapsed:
          reference: 0.01275
          toleranceAbsolute: 0.05
          unit: ms
        run.allocated:
          reference: 105000
          maximum: 110250
          unit: B
        run.allocations:
          reference: 3000
          maximum: 3150
          unit: count
        run.elapsed:
          reference: 1.410125
          toleranceAbsolute: 0.35253125
          unit: ms
        run.per-invoke-allocated:
          reference: 105
          maximum: 111
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.001410125
          toleranceAbsolute: 0.00035253125
          unit: ms/iteration
```

---

## Test: cast-separated-number-2000

This case executes 2000 dynamic conversions with reused input. A failed conversion emits Wrong and therefore fails correctness before allocation is evaluated.

### Case description

```yaml
gesBlock: case
id: "cast-separated-number-2000"
performance:
  iterations: 2000
  warmupIterations: 100
  compileWarmupIterations: 3
  observeRuntime: false
sources:
  - name: "conversion.ges"
    program: "main"
```

### Source code under test

```ges
module allocationconversion
on Start(value) {
  let parsed be value as :Number
  if not (parsed is :Number and parsed = 1234.5) { emit Wrong(value: parsed) }
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| dispatch | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  dispatch:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "1_234.5"
    accepted: true
    local: []
    outbound: []
    paused: false
    runtimeLimits:
      exclude:
        - any: true
    diagnostics: []
    trace:
      - event: "dispatchStarted"
        message:
          name: "Start"
          args:
            - name: "value"
              value:
                type: ":Text"
                value: "1_234.5"
        signatureId: "Start(value)"
      - event: "dispatchCompleted"
        message:
          name: "Start"
          args:
            - name: "value"
              value:
                type: ":Text"
                value: "1_234.5"
        signatureId: "Start(value)"
performance:
  profiles:
    csharp-dotnet-release-managed:
      metrics:
        run.allocated:
          reference: 0
          maximum: 0
          unit: "B"
    csharp-dotnet-release-macos-arm64:
      metrics:
        run.allocated:
          reference: 0
          maximum: 0
          unit: "B"
    swift-6.4-release-macos26-arm64-m3max:
      metrics:
        compile.allocated:
          reference: 96475
          maximum: 101299
          unit: B
        compile.elapsed:
          reference: 0.138584
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 12690
          maximum: 13325
          unit: B
        program-load.elapsed:
          reference: 0.014
          toleranceAbsolute: 0.05
          unit: ms
        run.allocated:
          reference: 210000
          maximum: 220500
          unit: B
        run.allocations:
          reference: 6000
          maximum: 6300
          unit: count
        run.elapsed:
          reference: 2.721875
          toleranceAbsolute: 0.68046875
          unit: ms
        run.per-invoke-allocated:
          reference: 105
          maximum: 111
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.0013609375
          toleranceAbsolute: 0.000340234375
          unit: ms/iteration
```

---

## Test: cast-percentage-1000

This case executes 1000 dynamic conversions with reused input. A failed conversion emits Wrong and therefore fails correctness before allocation is evaluated.

### Case description

```yaml
gesBlock: case
id: "cast-percentage-1000"
performance:
  iterations: 1000
  warmupIterations: 100
  compileWarmupIterations: 3
  observeRuntime: false
sources:
  - name: "conversion.ges"
    program: "main"
```

### Source code under test

```ges
module allocationconversion
on Start(value) {
  let parsed be value as :Percentage
  if not (parsed is :Percentage and parsed = 1.005%) { emit Wrong(value: parsed) }
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| dispatch | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  dispatch:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "1.005%"
    accepted: true
    local: []
    outbound: []
    paused: false
    runtimeLimits:
      exclude:
        - any: true
    diagnostics: []
    trace:
      - event: "dispatchStarted"
        message:
          name: "Start"
          args:
            - name: "value"
              value:
                type: ":Text"
                value: "1.005%"
        signatureId: "Start(value)"
      - event: "dispatchCompleted"
        message:
          name: "Start"
          args:
            - name: "value"
              value:
                type: ":Text"
                value: "1.005%"
        signatureId: "Start(value)"
performance:
  profiles:
    csharp-dotnet-release-managed:
      metrics:
        run.allocated:
          reference: 0
          maximum: 0
          unit: "B"
    csharp-dotnet-release-macos-arm64:
      metrics:
        run.allocated:
          reference: 0
          maximum: 0
          unit: "B"
    swift-6.4-release-macos26-arm64-m3max:
      metrics:
        compile.allocated:
          reference: 100219
          maximum: 105230
          unit: B
        compile.elapsed:
          reference: 0.14925
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 12690
          maximum: 13325
          unit: B
        program-load.elapsed:
          reference: 0.015334
          toleranceAbsolute: 0.05
          unit: ms
        run.allocated:
          reference: 103000
          maximum: 108150
          unit: B
        run.allocations:
          reference: 3000
          maximum: 3150
          unit: count
        run.elapsed:
          reference: 1.365875
          toleranceAbsolute: 0.34146875
          unit: ms
        run.per-invoke-allocated:
          reference: 103
          maximum: 109
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.001365875
          toleranceAbsolute: 0.00034146875
          unit: ms/iteration
```

---

## Test: cast-percentage-2000

This case executes 2000 dynamic conversions with reused input. A failed conversion emits Wrong and therefore fails correctness before allocation is evaluated.

### Case description

```yaml
gesBlock: case
id: "cast-percentage-2000"
performance:
  iterations: 2000
  warmupIterations: 100
  compileWarmupIterations: 3
  observeRuntime: false
sources:
  - name: "conversion.ges"
    program: "main"
```

### Source code under test

```ges
module allocationconversion
on Start(value) {
  let parsed be value as :Percentage
  if not (parsed is :Percentage and parsed = 1.005%) { emit Wrong(value: parsed) }
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| dispatch | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  dispatch:
    input:
      args:
        - name: "value"
          value:
            type: ":Text"
            value: "1.005%"
    accepted: true
    local: []
    outbound: []
    paused: false
    runtimeLimits:
      exclude:
        - any: true
    diagnostics: []
    trace:
      - event: "dispatchStarted"
        message:
          name: "Start"
          args:
            - name: "value"
              value:
                type: ":Text"
                value: "1.005%"
        signatureId: "Start(value)"
      - event: "dispatchCompleted"
        message:
          name: "Start"
          args:
            - name: "value"
              value:
                type: ":Text"
                value: "1.005%"
        signatureId: "Start(value)"
performance:
  profiles:
    csharp-dotnet-release-managed:
      metrics:
        run.allocated:
          reference: 0
          maximum: 0
          unit: "B"
    csharp-dotnet-release-macos-arm64:
      metrics:
        run.allocated:
          reference: 0
          maximum: 0
          unit: "B"
    swift-6.4-release-macos26-arm64-m3max:
      metrics:
        compile.allocated:
          reference: 100219
          maximum: 105230
          unit: B
        compile.elapsed:
          reference: 0.143083
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 12690
          maximum: 13325
          unit: B
        program-load.elapsed:
          reference: 0.015333
          toleranceAbsolute: 0.05
          unit: ms
        run.allocated:
          reference: 206000
          maximum: 216300
          unit: B
        run.allocations:
          reference: 6000
          maximum: 6300
          unit: count
        run.elapsed:
          reference: 2.923042
          toleranceAbsolute: 0.7307605
          unit: ms
        run.per-invoke-allocated:
          reference: 103
          maximum: 109
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.001461521
          toleranceAbsolute: 0.00036538025
          unit: ms/iteration
```

---

## Test: format-percentage-1000

This case executes 1000 dynamic conversions with reused input. A failed conversion emits Wrong and therefore fails correctness before allocation is evaluated.

### Case description

```yaml
gesBlock: case
id: "format-percentage-1000"
performance:
  iterations: 1000
  warmupIterations: 100
  compileWarmupIterations: 3
  observeRuntime: false
sources:
  - name: "conversion.ges"
    program: "main"
```

### Source code under test

```ges
module allocationconversion
on Start(value) {
  let parsed be parse (value as :Text)
  if not (parsed is :Percentage and parsed = value) { emit Wrong(value: parsed) }
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| dispatch | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  dispatch:
    input:
      args:
        - name: "value"
          value:
            type: ":Percentage"
            value: "0.1005"
    accepted: true
    local: []
    outbound: []
    paused: false
    runtimeLimits:
      exclude:
        - any: true
    diagnostics: []
    trace:
      - event: "dispatchStarted"
        message:
          name: "Start"
          args:
            - name: "value"
              value:
                type: ":Percentage"
                value: "0.1005"
        signatureId: "Start(value)"
      - event: "dispatchCompleted"
        message:
          name: "Start"
          args:
            - name: "value"
              value:
                type: ":Percentage"
                value: "0.1005"
        signatureId: "Start(value)"
performance:
  profiles:
    csharp-dotnet-release-managed:
      metrics:
        run.allocated:
          reference: 176000
          maximum: 192000
          unit: "B"
    csharp-dotnet-release-macos-arm64:
      metrics:
        run.allocated:
          reference: 176000
          maximum: 192000
          unit: "B"
    swift-6.4-release-macos26-arm64-m3max:
      metrics:
        compile.allocated:
          reference: 105350
          maximum: 110618
          unit: B
        compile.elapsed:
          reference: 0.144
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 12706
          maximum: 13342
          unit: B
        program-load.elapsed:
          reference: 0.012708
          toleranceAbsolute: 0.05
          unit: ms
        run.allocated:
          reference: 535000
          maximum: 561750
          unit: B
        run.allocations:
          reference: 11000
          maximum: 11550
          unit: count
        run.elapsed:
          reference: 2.594791
          toleranceAbsolute: 0.64869775
          unit: ms
        run.per-invoke-allocated:
          reference: 535
          maximum: 562
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.002594791
          toleranceAbsolute: 0.00064869775
          unit: ms/iteration
```

---

## Test: format-percentage-2000

This case executes 2000 dynamic conversions with reused input. A failed conversion emits Wrong and therefore fails correctness before allocation is evaluated.

### Case description

```yaml
gesBlock: case
id: "format-percentage-2000"
performance:
  iterations: 2000
  warmupIterations: 100
  compileWarmupIterations: 3
  observeRuntime: false
sources:
  - name: "conversion.ges"
    program: "main"
```

### Source code under test

```ges
module allocationconversion
on Start(value) {
  let parsed be parse (value as :Text)
  if not (parsed is :Percentage and parsed = value) { emit Wrong(value: parsed) }
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| dispatch | Start | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  dispatch:
    input:
      args:
        - name: "value"
          value:
            type: ":Percentage"
            value: "0.1005"
    accepted: true
    local: []
    outbound: []
    paused: false
    runtimeLimits:
      exclude:
        - any: true
    diagnostics: []
    trace:
      - event: "dispatchStarted"
        message:
          name: "Start"
          args:
            - name: "value"
              value:
                type: ":Percentage"
                value: "0.1005"
        signatureId: "Start(value)"
      - event: "dispatchCompleted"
        message:
          name: "Start"
          args:
            - name: "value"
              value:
                type: ":Percentage"
                value: "0.1005"
        signatureId: "Start(value)"
performance:
  profiles:
    csharp-dotnet-release-managed:
      metrics:
        run.allocated:
          reference: 352000
          maximum: 384000
          unit: "B"
    csharp-dotnet-release-macos-arm64:
      metrics:
        run.allocated:
          reference: 352000
          maximum: 384000
          unit: "B"
    swift-6.4-release-macos26-arm64-m3max:
      metrics:
        compile.allocated:
          reference: 105350
          maximum: 110618
          unit: B
        compile.elapsed:
          reference: 0.136666
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 12706
          maximum: 13342
          unit: B
        program-load.elapsed:
          reference: 0.013125
          toleranceAbsolute: 0.05
          unit: ms
        run.allocated:
          reference: 1070000
          maximum: 1123500
          unit: B
        run.allocations:
          reference: 22000
          maximum: 23100
          unit: count
        run.elapsed:
          reference: 5.064125
          toleranceAbsolute: 1.26603125
          unit: ms
        run.per-invoke-allocated:
          reference: 535
          maximum: 562
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.0025320625
          toleranceAbsolute: 0.000633015625
          unit: ms/iteration
```
