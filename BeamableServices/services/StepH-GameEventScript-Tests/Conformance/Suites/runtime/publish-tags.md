---
formatVersion: 1
suiteId: "runtime.publish-tags"
title: "RuntimePublishAndTags"
categories: [conformance]
---

# RuntimePublishAndTags

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite verifies local emit, local-plus-outbound publish, and tag-based handler filtering.

---

## Test: emit stays local while publish is local and outbound

This runtime case exercises “emit stays local while publish is local and outbound” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0001
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "emit stays local while publish is local and outbound.ges"
    program: main
```

### Source code under test

```ges
on Start {
  emit Local
  publish Remote with #radio
}

on Local {
  emit Seen(value: 1)
}

on Remote {
  emit ShouldNotRun
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args: []
    local:
      - name: "Local"
        args: []
      - name: "Remote"
        tags:
          - "radio"
        args: []
      - name: "Seen"
        args:
          - name: "value"
            value:
              type: ":integer"
              value: "1"
      - name: "ShouldNotRun"
        args: []
    outbound:
      - name: "Remote"
        tags:
          - "radio"
        args: []
```

---

## Test: branch can publish outbound and continue with local emit

This runtime case exercises “branch can publish outbound and continue with local emit” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0002
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "branch can publish outbound and continue with local emit.ges"
    program: main
```

### Source code under test

```ges
module BinaryRunnerBranch

on Start(value) {
  if value > 10 {
    publish High(value: value)
  }
  emit Done(value: value)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args:
        - name: "value"
          value:
            type: ":integer"
            value: "12"
    local:
      - name: "High"
        args:
          - name: "value"
            value:
              type: ":integer"
              value: "12"
      - name: "Done"
        args:
          - name: "value"
            value:
              type: ":integer"
              value: "12"
    outbound:
      - name: "High"
        args:
          - name: "value"
            value:
              type: ":integer"
              value: "12"
```

---

## Test: emit tags match handler filters

This runtime case exercises “emit tags match handler filters” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0003
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "emit tags match handler filters.ges"
    program: main
```

### Source code under test

```ges
on Start {
  let group be [#radio, #command, #radio]
  emit Signal(value: 1) with group
  emit Signal(value: 2) with #radio, #blocked
  emit Signal(value: 3)
}

on Signal(value) matching #radio, #command {
  emit Seen(value: value)
}

on Signal(value) matching #radio without #blocked {
  emit Seen(value: value + 10)
}

on Signal(value) without #radio {
  emit Seen(value: value + 100)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args: []
    local:
      - name: "Signal"
        tags:
          - "radio"
          - "command"
        args:
          - name: "value"
            value:
              type: ":integer"
              value: "1"
      - name: "Signal"
        tags:
          - "radio"
          - "blocked"
        args:
          - name: "value"
            value:
              type: ":integer"
              value: "2"
      - name: "Signal"
        args:
          - name: "value"
            value:
              type: ":integer"
              value: "3"
      - name: "Seen"
        args:
          - name: "value"
            value:
              type: ":integer"
              value: "1"
      - name: "Seen"
        args:
          - name: "value"
            value:
              type: ":integer"
              value: "11"
      - name: "Seen"
        args:
          - name: "value"
            value:
              type: ":integer"
              value: "103"
```

---

## Test: undeliverable endpoint receives unknown tagged messages

This runtime case exercises “undeliverable endpoint receives unknown tagged messages” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0004
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "undeliverable endpoint receives unknown tagged messages.ges"
    program: main
```

### Source code under test

```ges
on Start {
  emit Unknown(value: 7) with #radio, #encrypted
  emit Silent(value: 1) with #silent
}

on undeliverable as message matching #radio {
  emit Heard(name: message.name, signature: message.signature, value: message.arguments.value, tagCount: message.tags[:count], firstTag: message.tags[1], secondTag: message.tags[2], isMessage: message is :message)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args: []
    local:
      - name: "Unknown"
        tags:
          - "radio"
          - "encrypted"
        args:
          - name: "value"
            value:
              type: ":integer"
              value: "7"
      - name: "Silent"
        tags:
          - "silent"
        args:
          - name: "value"
            value:
              type: ":integer"
              value: "1"
      - name: "Heard"
        args:
          - name: "name"
            value:
              type: ":text"
              value: "Unknown"
          - name: "signature"
            value:
              type: ":text"
              value: "Unknown(value)"
          - name: "value"
            value:
              type: ":integer"
              value: "7"
          - name: "tagCount"
            value:
              type: ":integer"
              value: "2"
          - name: "firstTag"
            value:
              type: ":tag"
              value: "radio"
          - name: "secondTag"
            value:
              type: ":tag"
              value: "encrypted"
          - name: "isMessage"
            value:
              type: ":boolean"
              value: true
```

---

## Test: normal delivery suppresses undeliverable endpoint

This runtime case exercises “normal delivery suppresses undeliverable endpoint” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0005
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "normal delivery suppresses undeliverable endpoint.ges"
    program: main
```

### Source code under test

```ges
on Start {
  emit Known(value: 3) with #radio
}

on Known(value) matching #radio {
  emit Normal(value: value)
}

on undeliverable as message matching #radio {
  emit Unexpected(name: message.name)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args: []
    local:
      - name: "Known"
        tags:
          - "radio"
        args:
          - name: "value"
            value:
              type: ":integer"
              value: "3"
      - name: "Normal"
        args:
          - name: "value"
            value:
              type: ":integer"
              value: "3"
```

---

## Test: message-name handler receives matching names with original message

This runtime case exercises “message-name handler receives matching names with original message” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0006
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "message-name handler receives matching names with original message.ges"
    program: main
```

### Source code under test

```ges
on Start {
  emit Damage(amount: 3) with #radio, #enemy
  emit Damage(amount: 5, kind: 'fire') with #radio
  emit Damage(amount: 7) with #silent
}

on Damage as message matching #radio {
  emit Heard(signature: message.signature, amount: message.arguments.amount, tagCount: message.tags[:count], firstTag: message.tags[1], isMessage: message is :message)
}

on undeliverable as message matching #silent {
  emit Missed(signature: message.signature, amount: message.arguments.amount, tag: message.tags[1])
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args: []
    local:
      - name: "Damage"
        tags:
          - "radio"
          - "enemy"
        args:
          - name: "amount"
            value:
              type: ":integer"
              value: "3"
      - name: "Damage"
        tags:
          - "radio"
        args:
          - name: "amount"
            value:
              type: ":integer"
              value: "5"
          - name: "kind"
            value:
              type: ":text"
              value: "fire"
      - name: "Damage"
        tags:
          - "silent"
        args:
          - name: "amount"
            value:
              type: ":integer"
              value: "7"
      - name: "Heard"
        args:
          - name: "signature"
            value:
              type: ":text"
              value: "Damage(amount)"
          - name: "amount"
            value:
              type: ":integer"
              value: "3"
          - name: "tagCount"
            value:
              type: ":integer"
              value: "2"
          - name: "firstTag"
            value:
              type: ":tag"
              value: "radio"
          - name: "isMessage"
            value:
              type: ":boolean"
              value: true
      - name: "Heard"
        args:
          - name: "signature"
            value:
              type: ":text"
              value: "Damage(amount,kind)"
          - name: "amount"
            value:
              type: ":integer"
              value: "5"
          - name: "tagCount"
            value:
              type: ":integer"
              value: "1"
          - name: "firstTag"
            value:
              type: ":tag"
              value: "radio"
          - name: "isMessage"
            value:
              type: ":boolean"
              value: true
      - name: "Missed"
        args:
          - name: "signature"
            value:
              type: ":text"
              value: "Damage(amount)"
          - name: "amount"
            value:
              type: ":integer"
              value: "7"
          - name: "tag"
            value:
              type: ":tag"
              value: "silent"
```

---

## Test: exact and message-name handlers share dispatch order

This runtime case exercises “exact and message-name handlers share dispatch order” and verifies the declared messages, values, and execution result.

### Case description

```yaml
gesBlock: case
id: case-0007
kind: scriptApi
level: scenario
comparison:
  binary64:
    mode: ulp
    maxUlps: 4096
sources:
  - name: "exact and message-name handlers share dispatch order.ges"
    program: main
```

### Source code under test

```ges
on Start {
  emit Damage(amount: 9) with #radio
}

on Damage(amount) matching #radio {
  emit Exact(amount: amount)
}

on Damage as message matching #radio {
  emit Any(signature: message.signature)
}
```

### Steps

| step | receive | pump | budget |
| --- | --- | --- | --- |
| step-0001 | Start | completion |  |

### Expectation

```yaml
gesBlock: expect
steps:
  step-0001:
    input:
      args: []
    local:
      - name: "Damage"
        tags:
          - "radio"
        args:
          - name: "amount"
            value:
              type: ":integer"
              value: "9"
      - name: "Exact"
        args:
          - name: "amount"
            value:
              type: ":integer"
              value: "9"
      - name: "Any"
        args:
          - name: "signature"
            value:
              type: ":text"
              value: "Damage(amount)"
```
