---
formatVersion: 1
suiteId: "runtime.external-callback-failures"
title: "External callback failures"
kind: scriptApi
level: scenario
categories: [conformance]
requires:
  core: [external-types]
---

# External callback failures

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite verifies failure classification, handler isolation, Host reuse, and diagnostic context through the fixed portable CallbackProbe external type.

---

## Test: constructor unexpected failures abort only the current handler

This case checks the diagnostic phase, code, symbol, and context. The failing handler cannot emit MustNotRun; a later sibling emits Continued and a subsequent Check message completes normally.

### Case description

```yaml
gesBlock: case
id: constructor-unexpected
nativeHandlers:
  - message: Start
    parameters: []
    priority: -1
    emit:
      - name: Continued
        args: []
```

### Source code under test

```ges
module callbackfailures
on Start() {
  let probe be :CallbackProbe(failure: "constructorUnexpected", context: "none")
  emit MustNotRun(value: probe)
}
on Check { emit Done }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| fail | Start | completion | |
| again | Start | completion | |
| check | Check | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  fail:
    local:
      - name: Continued
    diagnostics:
      - phase: runtime
        code: runtime.externalConstructorFailed
        symbol: CallbackProbe
        programName: "callbackfailures"
        handlerName: "Start()"
  again:
    local:
      - name: Continued
    diagnostics:
      - phase: runtime
        code: runtime.externalConstructorFailed
        symbol: CallbackProbe
        programName: "callbackfailures"
        handlerName: "Start()"
  check:
    local:
      - name: Done
```

---

## Test: constructor declared faults fill missing context and preserve supplied context

This case checks the diagnostic phase, code, symbol, and context. The failing handler cannot emit MustNotRun; a later sibling emits Continued and a subsequent Check message completes normally.

### Case description

```yaml
gesBlock: case
id: constructor-declared-context
nativeHandlers:
  - message: Start
    parameters: [context]
    priority: -1
    emit:
      - name: Continued
        args: []
```

### Source code under test

```ges
module callbackfailures
on Start(context) {
  let probe be :CallbackProbe(failure: "constructorDeclared", context: context)
  emit MustNotRun(value: probe)
}
on Check { emit Done }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| none | Start | completion | |
| program | Start | completion | |
| handler | Start | completion | |
| both | Start | completion | |
| check | Check | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  none:
    input:
      args:
        - name: context
          value: { type: ":Text", value: "none" }
    local:
      - name: Continued
    diagnostics:
      - phase: runtime
        code: test.callbackFault
        symbol: CallbackProbe
        programName: "callbackfailures"
        handlerName: "Start(context)"
  program:
    input:
      args:
        - name: context
          value: { type: ":Text", value: "program" }
    local:
      - name: Continued
    diagnostics:
      - phase: runtime
        code: test.callbackFault
        symbol: CallbackProbe
        programName: "reported.program"
        handlerName: "Start(context)"
  handler:
    input:
      args:
        - name: context
          value: { type: ":Text", value: "handler" }
    local:
      - name: Continued
    diagnostics:
      - phase: runtime
        code: test.callbackFault
        symbol: CallbackProbe
        programName: "callbackfailures"
        handlerName: "Reported()"
  both:
    input:
      args:
        - name: context
          value: { type: ":Text", value: "both" }
    local:
      - name: Continued
    diagnostics:
      - phase: runtime
        code: test.callbackFault
        symbol: CallbackProbe
        programName: "reported.program"
        handlerName: "Reported()"
  check:
    local:
      - name: Done
```

---

## Test: field unexpected failures abort only the current handler

This case checks the diagnostic phase, code, symbol, and context. The failing handler cannot emit MustNotRun; a later sibling emits Continued and a subsequent Check message completes normally.

### Case description

```yaml
gesBlock: case
id: field-unexpected
nativeHandlers:
  - message: Start
    parameters: []
    priority: -1
    emit:
      - name: Continued
        args: []
```

### Source code under test

```ges
module callbackfailures
on Start() {
  let probe be :CallbackProbe(failure: "fieldUnexpected", context: "none")
  emit MustNotRun(value: probe.value)
}
on Check { emit Done }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| fail | Start | completion | |
| again | Start | completion | |
| check | Check | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  fail:
    local:
      - name: Continued
    diagnostics:
      - phase: runtime
        code: runtime.externalFieldAccessFailed
        symbol: CallbackProbe.value
        programName: "callbackfailures"
        handlerName: "Start()"
  again:
    local:
      - name: Continued
    diagnostics:
      - phase: runtime
        code: runtime.externalFieldAccessFailed
        symbol: CallbackProbe.value
        programName: "callbackfailures"
        handlerName: "Start()"
  check:
    local:
      - name: Done
```

---

## Test: field declared faults fill missing context and preserve supplied context

This case checks the diagnostic phase, code, symbol, and context. The failing handler cannot emit MustNotRun; a later sibling emits Continued and a subsequent Check message completes normally.

### Case description

```yaml
gesBlock: case
id: field-declared-context
nativeHandlers:
  - message: Start
    parameters: [context]
    priority: -1
    emit:
      - name: Continued
        args: []
```

### Source code under test

```ges
module callbackfailures
on Start(context) {
  let probe be :CallbackProbe(failure: "fieldDeclared", context: context)
  emit MustNotRun(value: probe.value)
}
on Check { emit Done }
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| none | Start | completion | |
| program | Start | completion | |
| handler | Start | completion | |
| both | Start | completion | |
| check | Check | completion | |

### Expectation

```yaml
gesBlock: expect
steps:
  none:
    input:
      args:
        - name: context
          value: { type: ":Text", value: "none" }
    local:
      - name: Continued
    diagnostics:
      - phase: runtime
        code: test.callbackFault
        symbol: CallbackProbe.value
        programName: "callbackfailures"
        handlerName: "Start(context)"
  program:
    input:
      args:
        - name: context
          value: { type: ":Text", value: "program" }
    local:
      - name: Continued
    diagnostics:
      - phase: runtime
        code: test.callbackFault
        symbol: CallbackProbe.value
        programName: "reported.program"
        handlerName: "Start(context)"
  handler:
    input:
      args:
        - name: context
          value: { type: ":Text", value: "handler" }
    local:
      - name: Continued
    diagnostics:
      - phase: runtime
        code: test.callbackFault
        symbol: CallbackProbe.value
        programName: "callbackfailures"
        handlerName: "Reported()"
  both:
    input:
      args:
        - name: context
          value: { type: ":Text", value: "both" }
    local:
      - name: Continued
    diagnostics:
      - phase: runtime
        code: test.callbackFault
        symbol: CallbackProbe.value
        programName: "reported.program"
        handlerName: "Reported()"
  check:
    local:
      - name: Done
```

---

## Test: a field read runs only that getter and keeps nothing

Each read of `count` runs its getter again, and the failing getter of the other field stays unread.

### Case description

```yaml
gesBlock: case
id: field-read-runs-one-getter
```

### Source code under test

```ges
module readprobe
on Start() {
  let probe be :ReadProbe(failOther: true)
  emit Done(first: probe.count, second: probe.count)
}
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
      - name: Done
        args:
          - name: first
            value: { type: ":Number.int64", value: "1" }
          - name: second
            value: { type: ":Number.int64", value: "2" }
```

---

## Test: materializing runs every getter

A conversion to Map reads every declared field, so the failing getter aborts the handler.

### Case description

```yaml
gesBlock: case
id: materializing-runs-every-getter
```

### Source code under test

```ges
module readprobe
on Start() {
  let probe be :ReadProbe(failOther: true)
  emit MustNotRun(value: probe as :Map)
}
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
    diagnostics:
      - phase: runtime
        code: runtime.externalFieldAccessFailed
        symbol: ReadProbe.other
```

---

## Test: every materialization reads the fields again

Two conversions to Map see two different counts.

### Case description

```yaml
gesBlock: case
id: materializing-keeps-nothing
```

### Source code under test

```ges
module readprobe
on Start() {
  let probe be :ReadProbe(failOther: false)
  let first be probe as :Map
  let second be probe as :Map
  emit Done(first: first.count, second: second.count)
}
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
      - name: Done
        args:
          - name: first
            value: { type: ":Number.int64", value: "1" }
          - name: second
            value: { type: ":Number.int64", value: "2" }
```
