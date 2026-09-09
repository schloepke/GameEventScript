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
```
