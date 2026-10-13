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
          reference: 93769
          maximum: 98458
          unit: B
        compile.elapsed:
          reference: 0.114042
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 12144
          maximum: 12752
          unit: B
        program-load.elapsed:
          reference: 0.013125
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
          reference: 0.871709
          toleranceAbsolute: 0.21792725
          unit: ms
        run.per-invoke-allocated:
          reference: 107
          maximum: 113
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.000871709
          toleranceAbsolute: 0.00021792725
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
          reference: 93769
          maximum: 98458
          unit: B
        compile.elapsed:
          reference: 0.112333
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 12144
          maximum: 12752
          unit: B
        program-load.elapsed:
          reference: 0.012917
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
          reference: 1.745042
          toleranceAbsolute: 0.4362605
          unit: ms
        run.per-invoke-allocated:
          reference: 107
          maximum: 113
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.000872521
          toleranceAbsolute: 0.00021813025
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
          reference: 94164
          maximum: 98873
          unit: B
        compile.elapsed:
          reference: 0.115583
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 12112
          maximum: 12718
          unit: B
        program-load.elapsed:
          reference: 0.012625
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
          reference: 0.827334
          toleranceAbsolute: 0.2068335
          unit: ms
        run.per-invoke-allocated:
          reference: 81
          maximum: 86
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.000827334
          toleranceAbsolute: 0.0002068335
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
          reference: 94164
          maximum: 98873
          unit: B
        compile.elapsed:
          reference: 0.115667
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 12112
          maximum: 12718
          unit: B
        program-load.elapsed:
          reference: 0.012708
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
          reference: 1.656833
          toleranceAbsolute: 0.41420825
          unit: ms
        run.per-invoke-allocated:
          reference: 81
          maximum: 86
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.0008284165
          toleranceAbsolute: 0.000207104125
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
          reference: 92878
          maximum: 97522
          unit: B
        compile.elapsed:
          reference: 0.112667
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 12144
          maximum: 12752
          unit: B
        program-load.elapsed:
          reference: 0.012833
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
          reference: 0.866292
          toleranceAbsolute: 0.216573
          unit: ms
        run.per-invoke-allocated:
          reference: 105
          maximum: 111
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.000866292
          toleranceAbsolute: 0.000216573
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
          reference: 92878
          maximum: 97522
          unit: B
        compile.elapsed:
          reference: 0.111958
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 12144
          maximum: 12752
          unit: B
        program-load.elapsed:
          reference: 0.012875
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
          reference: 1.734125
          toleranceAbsolute: 0.43353125
          unit: ms
        run.per-invoke-allocated:
          reference: 105
          maximum: 111
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.0008670625
          toleranceAbsolute: 0.000216765625
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
          reference: 96622
          maximum: 101454
          unit: B
        compile.elapsed:
          reference: 0.120958
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 12144
          maximum: 12752
          unit: B
        program-load.elapsed:
          reference: 0.012792
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
          reference: 0.869959
          toleranceAbsolute: 0.21748975
          unit: ms
        run.per-invoke-allocated:
          reference: 103
          maximum: 109
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.000869959
          toleranceAbsolute: 0.00021748975
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
          reference: 96622
          maximum: 101454
          unit: B
        compile.elapsed:
          reference: 0.121584
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 12144
          maximum: 12752
          unit: B
        program-load.elapsed:
          reference: 0.012792
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
          reference: 1.754916
          toleranceAbsolute: 0.438729
          unit: ms
        run.per-invoke-allocated:
          reference: 103
          maximum: 109
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.000877458
          toleranceAbsolute: 0.0002193645
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
          reference: 102649
          maximum: 107782
          unit: B
        compile.elapsed:
          reference: 0.126458
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 12160
          maximum: 12768
          unit: B
        program-load.elapsed:
          reference: 0.012792
          toleranceAbsolute: 0.05
          unit: ms
        run.allocated:
          reference: 575000
          maximum: 603750
          unit: B
        run.allocations:
          reference: 12000
          maximum: 12600
          unit: count
        run.elapsed:
          reference: 2.136
          toleranceAbsolute: 0.534
          unit: ms
        run.per-invoke-allocated:
          reference: 575
          maximum: 604
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.002136
          toleranceAbsolute: 0.000534
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
          reference: 102649
          maximum: 107782
          unit: B
        compile.elapsed:
          reference: 0.127125
          toleranceAbsolute: 0.05
          unit: ms
        program-load.allocated:
          reference: 12160
          maximum: 12768
          unit: B
        program-load.elapsed:
          reference: 0.012833
          toleranceAbsolute: 0.05
          unit: ms
        run.allocated:
          reference: 1150000
          maximum: 1207500
          unit: B
        run.allocations:
          reference: 24000
          maximum: 25200
          unit: count
        run.elapsed:
          reference: 4.265917
          toleranceAbsolute: 1.06647925
          unit: ms
        run.per-invoke-allocated:
          reference: 575
          maximum: 604
          unit: B/iteration
        run.per-invoke-elapsed:
          reference: 0.0021329585
          toleranceAbsolute: 0.000533239625
          unit: ms/iteration
```
