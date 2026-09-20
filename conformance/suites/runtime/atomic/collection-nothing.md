---
formatVersion: 1
suiteId: runtime.atomic.collection-nothing
title: "Nothing in collection subtraction"
categories: [conformance]
---

# Nothing in collection subtraction

> [!CAUTION]
> **Executable Conformance Test Markdown**
>
> - This file controls executable conformance tests; it is not free-form documentation.
> - Keep its structural elements in the form required by the Conformance Test Markdown syntax.
> - Validate every edit with a conforming parser and runner.

This suite distinguishes removing a Nothing list element from propagation of Nothing in scalar arithmetic.

---

## Test: literal Nothing removes only the first matching list item

This case removes one matching Nothing element, preserves unmatched lists, and keeps scalar Nothing propagation.

### Case description

```yaml
gesBlock: case
id: literal
kind: scriptApi
level: scenario
sources:
  - name: literal.ges
    program: main
```

### Source code under test

```ges
on Start {
  emit Done(removed: [1, nothing, 2, nothing] - nothing, absent: [1, 2] - nothing, emptyList: [] - nothing, scalar: 1 - nothing, left: nothing - [1], added: [1] + nothing)
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
    input: { args: [] }
    local:
      - name: Done
        args:
          - name: removed
            value: { type: ":List", items: [{ type: ":Number.int64", value: "1" }, { type: ":Number.int64", value: "2" }, { type: ":Nothing" }] }
          - name: absent
            value: { type: ":List", items: [{ type: ":Number.int64", value: "1" }, { type: ":Number.int64", value: "2" }] }
          - name: emptyList
            value: { type: ":List", items: [] }
          - name: scalar
            value: { type: ":Nothing" }
          - name: left
            value: { type: ":Nothing" }
          - name: added
            value: { type: ":Nothing" }
```

---

## Test: dynamic Nothing removes only the first matching list item

This case removes one matching Nothing element, preserves unmatched lists, and keeps scalar Nothing propagation.

### Case description

```yaml
gesBlock: case
id: dynamic
kind: scriptApi
level: scenario
sources:
  - name: dynamic.ges
    program: main
```

### Source code under test

```ges
on Start(missing) {
  emit Done(removed: [1, nothing, 2, nothing] - missing, absent: [1, 2] - missing, emptyList: [] - missing, scalar: 1 - missing, left: missing - [1], added: [1] + missing)
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
    input: { args: [{ name: missing, value: { type: ":Nothing" } }] }
    local:
      - name: Done
        args:
          - name: removed
            value: { type: ":List", items: [{ type: ":Number.int64", value: "1" }, { type: ":Number.int64", value: "2" }, { type: ":Nothing" }] }
          - name: absent
            value: { type: ":List", items: [{ type: ":Number.int64", value: "1" }, { type: ":Number.int64", value: "2" }] }
          - name: emptyList
            value: { type: ":List", items: [] }
          - name: scalar
            value: { type: ":Nothing" }
          - name: left
            value: { type: ":Nothing" }
          - name: added
            value: { type: ":Nothing" }
```
